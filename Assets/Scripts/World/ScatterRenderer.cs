using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AshenRealm.World
{
    /// <summary>
    /// Draws the valley's <see cref="ScatterData"/> with <c>Graphics.RenderMeshInstanced</c> for each camera that sees this
    /// layer. The visible lists are rebuilt only after the camera moves 4 m or turns 10 degrees: a whole cell is skipped when
    /// no view within that move and turn could find it inside its kind's cull distance and the frustum. Cells within the shadow reach cast shadows, even
    /// behind the camera; farther ones do not, which keeps the shadow pass small.
    /// </summary>
    [ExecuteAlways]
    public sealed class ScatterRenderer : MonoBehaviour
    {
        // WebGL 2 promises 16 KB uniform blocks: 128 instances of two matrices each.
        const int Batch = 128;
        const float ShadowReach = 45f;
        public const float RebuildMove = 4f, RebuildTurn = 10f;

        [SerializeField] ScatterData data;

        Matrix4x4[] matrices;
        List<Matrix4x4>[] near, far;
        Bounds[] nearBounds, farBounds;
        bool[] groupDrawn;
        Camera lastCamera;
        Vector3 lastPosition;
        Quaternion lastRotation;
        readonly Plane[] planes = new Plane[6];

        public ScatterData Data => data;
        /// <summary>Frame of the last camera this renderer drew for (the playtest proves drawing with it).</summary>
        public int LastDrawFrame { get; private set; } = -1;
        public int DrawCalls { get; private set; }
        public int Refreshes { get; private set; }
        public Vector3 LastCameraPosition => lastPosition;
        public bool GroupDrawn(int group) => groupDrawn != null && group >= 0 && group < groupDrawn.Length && groupDrawn[group];
        public int Drawn(int kind) => near == null ? 0 : near[kind].Count + far[kind].Count;

        void OnEnable()
        {
            lastCamera = null;
            RenderPipelineManager.beginCameraRendering += Draw;
        }

        void OnDisable() => RenderPipelineManager.beginCameraRendering -= Draw;

        void Build()
        {
            matrices = new Matrix4x4[data.Instances.Length];
            for (int i = 0; i < matrices.Length; i++)
            {
                var s = data.Instances[i];
                matrices[i] = Matrix4x4.TRS(s.Position, Quaternion.Euler(0f, s.Yaw, 0f), Vector3.one * s.Scale);
            }
            int kinds = data.Kinds.Length;
            near = new List<Matrix4x4>[kinds];
            far = new List<Matrix4x4>[kinds];
            for (int k = 0; k < kinds; k++) { near[k] = new List<Matrix4x4>(); far[k] = new List<Matrix4x4>(); }
            nearBounds = new Bounds[kinds];
            farBounds = new Bounds[kinds];
            groupDrawn = new bool[data.Groups.Length];
        }

        /// <summary>Rebuilds the visible lists for a camera.</summary>
        public void Refresh(Camera camera)
        {
            if (data == null) return;
            if (matrices == null || matrices.Length != data.Instances.Length || groupDrawn.Length != data.Groups.Length) Build();
            lastCamera = camera;
            lastPosition = camera.transform.position;
            lastRotation = camera.transform.rotation;
            Refreshes++;
            // The lists must hold for every view until the next rebuild: a turn of up to RebuildTurn moves no ray farther than
            // that, so each frustum side opens by it; a move of up to RebuildMove is the same as each cell grown by it.
            float tanV = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float vertical = camera.fieldOfView + 2f * RebuildTurn;
            float horizontal = 2f * Mathf.Atan(tanV * camera.aspect) * Mathf.Rad2Deg + 2f * RebuildTurn;
            float aspect = Mathf.Tan(horizontal * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(vertical * 0.5f * Mathf.Deg2Rad);
            GeometryUtility.CalculateFrustumPlanes(
                Matrix4x4.Perspective(vertical, aspect, 0.01f, camera.farClipPlane + RebuildMove) * camera.worldToCameraMatrix, planes);
            var eye = new Vector2(lastPosition.x, lastPosition.z);
            for (int k = 0; k < near.Length; k++) { near[k].Clear(); far[k].Clear(); }
            for (int g = 0; g < data.Groups.Length; g++)
            {
                var group = data.Groups[g];
                Bounds b = group.Bounds;
                Vector2 closest = new Vector2(Mathf.Clamp(eye.x, b.min.x, b.max.x), Mathf.Clamp(eye.y, b.min.z, b.max.z));
                float distance = Vector2.Distance(eye, closest);
                var kind = data.Kinds[group.Kind];
                bool inShadowReach = kind.Shadows && distance < ShadowReach;
                Bounds reach = b;
                reach.Expand(2f * RebuildMove);
                groupDrawn[g] = distance < kind.CullDistance + RebuildMove && (inShadowReach || GeometryUtility.TestPlanesAABB(planes, reach));
                if (!groupDrawn[g]) continue;
                var list = inShadowReach ? near[group.Kind] : far[group.Kind];
                Bounds[] bounds = inShadowReach ? nearBounds : farBounds;
                if (list.Count == 0) bounds[group.Kind] = b;
                else bounds[group.Kind].Encapsulate(b);
                for (int i = group.Start; i < group.Start + group.Count; i++) list.Add(matrices[i]);
            }
        }

        void Draw(ScriptableRenderContext context, Camera camera)
        {
            if (data == null || camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection) return;
            if ((camera.cullingMask & (1 << gameObject.layer)) == 0) return;
            if (camera != lastCamera || matrices == null || (camera.transform.position - lastPosition).sqrMagnitude > RebuildMove * RebuildMove ||
                Quaternion.Angle(camera.transform.rotation, lastRotation) > RebuildTurn)
                Refresh(camera);
            int calls = 0;
            for (int k = 0; k < data.Kinds.Length; k++)
            {
                var kind = data.Kinds[k];
                for (int m = 0; m < kind.Meshes.Length; m++)
                {
                    calls += Submit(camera, kind.Meshes[m], kind.Materials[m], near[k], nearBounds[k], kind.Shadows);
                    calls += Submit(camera, kind.Meshes[m], kind.Materials[m], far[k], farBounds[k], false);
                }
            }
            DrawCalls = calls;
            LastDrawFrame = Time.frameCount;
        }

        int Submit(Camera camera, Mesh mesh, Material material, List<Matrix4x4> list, Bounds bounds, bool shadows)
        {
            if (list.Count == 0) return 0;
            // Light probes off: each instance gets the ambient probe and no per-instance SH, so the batch fits WebGL's buffers.
            var parameters = new RenderParams(material)
            {
                camera = camera,
                layer = gameObject.layer,
                worldBounds = bounds,
                shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                receiveShadows = true,
                lightProbeUsage = LightProbeUsage.Off,
            };
            int calls = 0;
            for (int start = 0; start < list.Count; start += Batch, calls++)
                Graphics.RenderMeshInstanced(parameters, mesh, 0, list, Mathf.Min(Batch, list.Count - start), start);
            return calls;
        }
    }
}
