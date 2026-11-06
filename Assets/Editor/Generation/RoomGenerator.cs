using System;
using System.Collections.Generic;
using AshenRealm.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace AshenRealm.EditorTools
{
    /// <summary>Key positions of a house for the scene builder.</summary>
    internal class RoomLayout
    {
        public Vector3 Entry;
        public Vector3 DoorOut;
        public readonly List<(string id, Vector3 position, float yaw, Action<MeshBuilder> figure)> People = new List<(string, Vector3, float, Action<MeshBuilder>)>();
    }

    /// <summary>
    /// What the walkable houses share: one mesh whose light is baked into its vertices, which costs nothing at run time and
    /// suits the pixel shader (its point lights only reach a metre or two), and a kit of timber, iron, stone and flames.
    /// Whoever stands in a room is baked with the same light.
    /// </summary>
    internal abstract class RoomGenerator
    {
        protected const float LongPiece = 1.5f;      // baked light lands on vertices, so long timbers are cut into lengths

        protected static readonly Color FireLight = new Color(1.05f, 0.5f, 0.2f);
        protected static readonly Color LampLight = new Color(0.8f, 0.52f, 0.26f);
        protected static readonly Color CandleLight = new Color(0.62f, 0.42f, 0.24f);
        protected static readonly Color StreetLight = new Color(0.22f, 0.3f, 0.5f);

        readonly struct Glow
        {
            public readonly Vector3 Position;
            public readonly Color Color;
            public readonly float Range;

            public Glow(Vector3 position, Color color, float range)
            {
                Position = position;
                Color = color;
                Range = range;
            }
        }

        protected readonly Vector3 origin;
        protected readonly System.Random rng;
        protected MeshBuilder room;
        protected Transform root, colliderRoot;

        readonly Material material;
        readonly List<Glow> glows = new List<Glow>();

        protected RoomGenerator(Material material, Vector3 origin, int seed)
        {
            this.material = material;
            this.origin = origin;
            rng = new System.Random(seed);
        }

        /// <summary>The light every surface has before any flame reaches it.</summary>
        protected virtual Color Dark => new Color(0.2f, 0.16f, 0.14f);

        protected float R(float min, float max) => MeshBuilder.Lerp(rng, min, max);

        protected void Begin(Transform parent)
        {
            root = new GameObject("Environment").transform;
            root.SetParent(parent, false);
            colliderRoot = new GameObject("Colliders").transform;
            colliderRoot.SetParent(root, false);
            room = new MeshBuilder();
        }

        /// <summary>Bakes the room and saves it as one mesh.</summary>
        protected void Finish(string meshName)
        {
            room.Bake(LightAt);
            Mesh mesh = GeneratedAssets.SaveMesh(room, meshName);
            var go = new GameObject(meshName);
            go.transform.SetParent(root, false);
            go.transform.position = origin;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        protected void AddGlow(Vector3 position, Color color, float range) => glows.Add(new Glow(position, color, range));

        protected Color LightAt(Vector3 position, Vector3 normal)
        {
            Color light = Dark;
            foreach (Glow glow in glows)
            {
                Vector3 towards = glow.Position - position;
                float distance = towards.magnitude;
                float reach = Mathf.Clamp01(1f - distance / glow.Range);
                float facing = distance < 0.01f ? 1f : Mathf.Clamp01(Vector3.Dot(normal, towards / distance) * 0.75f + 0.25f);
                light += glow.Color * (reach * reach * facing);
            }
            return new Color(Mathf.Min(light.r, 1.15f), Mathf.Min(light.g, 1.05f), Mathf.Min(light.b, 0.95f));
        }

        /// <summary>Someone standing in the room, lit by it.</summary>
        protected void Person(RoomLayout layout, string id, Vector3 local, float yaw, Action<MeshBuilder> figure)
        {
            Matrix4x4 toRoom = Matrix4x4.TRS(local, Quaternion.Euler(0f, yaw, 0f), Vector3.one);
            layout.People.Add((id, origin + local, yaw, b =>
            {
                figure(b);
                b.Bake((p, n) => LightAt(toRoom.MultiplyPoint3x4(p), toRoom.MultiplyVector(n)));
            }));
        }

        protected GameObject Solid(string name, Vector3 local, Vector3 size, float yaw = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(colliderRoot, false);
            go.transform.SetPositionAndRotation(origin + local, Quaternion.Euler(0f, yaw, 0f));
            go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        /// <summary>A timber, cut into lengths if it is long.</summary>
        protected void Timber(Vector3 centre, Vector3 size, float shade = 0.42f)
        {
            float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            int pieces = Mathf.Max(1, Mathf.CeilToInt(longest / LongPiece));
            Vector3 along = size.x == longest ? Vector3.right : size.y == longest ? Vector3.up : Vector3.forward;
            Vector3 piece = size - along * (longest - longest / pieces);
            room.SetColor(Pal.WoodDark, shade + R(-0.05f, 0.05f));
            for (int i = 0; i < pieces; i++)
                room.Box(centre + along * ((i + 0.5f) / pieces - 0.5f) * longest, piece);
        }

        protected void Iron(Vector3 centre, Vector3 size)
        {
            room.SetColor(Pal.SteelDark, R(0.3f, 0.42f));
            room.Box(centre, size);
        }

        protected void Prop(Vector3 position, float yaw, Action<MeshBuilder> build)
        {
            room.Push(position, Quaternion.Euler(0f, yaw, 0f), Vector3.one);
            build(room);
            room.Pop();
        }

        /// <summary>A floor of big flags in a running bond, each with its own shade.</summary>
        protected void Flagstones(float halfWidth, float halfDepth, float flag, float y, float shadeMin, float shadeMax)
        {
            room.SetColor(Pal.WoodDark, 0.12f);
            room.Box(new Vector3(0f, y - 0.3f, 0f), new Vector3(halfWidth * 2f, 0.4f, halfDepth * 2f));
            int row = 0;
            for (float z = -halfDepth; z < halfDepth; z += flag, row++)
            for (float x = -halfWidth - (row % 2) * flag * 0.5f; x < halfWidth; x += flag)
            {
                float left = Mathf.Max(x, -halfWidth), right = Mathf.Min(x + flag, halfWidth);
                float near = z, far = Mathf.Min(z + flag, halfDepth);
                room.ShadedBox(new Vector3((left + right) * 0.5f, y - 0.1f, (near + far) * 0.5f), new Vector3(right - left - 0.05f, 0.2f, far - near - 0.05f), Pal.Stone, rng, shadeMin, shadeMax);
            }
        }

        /// <summary>Courses of big rough blocks in a running bond, each with its own shade, along x or z.</summary>
        protected void StoneWall(Vector3 from, Vector3 to, float height, float thickness, float shadeMin = 0.18f, float shadeMax = 0.36f)
        {
            const float blockLength = 1.3f, blockHeight = 0.62f;
            Vector3 direction = (to - from).normalized;
            float length = Vector3.Distance(from, to);
            Vector3 across = new Vector3(Mathf.Abs(direction.z), 0f, Mathf.Abs(direction.x)) * thickness;
            int course = 0;
            for (float y = 0f; y < height; y += blockHeight, course++)
            {
                float course_height = Mathf.Min(blockHeight, height - y);
                for (float s = -(course % 2) * blockLength * 0.5f; s < length; s += blockLength)
                {
                    float start = Mathf.Max(s, 0f), stop = Mathf.Min(s + blockLength, length);
                    if (stop - start < 0.02f) continue;
                    Vector3 centre = from + direction * ((start + stop) * 0.5f) + Vector3.up * (y + course_height * 0.5f);
                    Vector3 size = new Vector3(Mathf.Abs(direction.x), 0f, Mathf.Abs(direction.z)) * (stop - start - 0.03f) + across + Vector3.up * (course_height - 0.03f);
                    room.ShadedBox(centre, size, Pal.Stone, rng, shadeMin, shadeMax);
                }
            }
            room.SetColor(Pal.Stone, 0.08f);
            Vector3 middle = (from + to) * 0.5f;
            room.Box(middle + Vector3.up * (height * 0.5f) + (from.x == to.x ? Vector3.right * Mathf.Sign(from.x) : Vector3.forward * Mathf.Sign(from.z)) * 0.1f,
                new Vector3(Mathf.Abs(direction.x), 0f, Mathf.Abs(direction.z)) * length + across * 0.9f + Vector3.up * height);
        }

        /// <summary>The closed door to the street, in the wall at z.</summary>
        protected void StreetDoor(float z)
        {
            room.ShadedBox(new Vector3(0f, 1.5f, z), new Vector3(2.1f, 3f, 0.1f), Pal.Wood, rng, 0.4f, 0.46f);
            for (float x = -0.75f; x <= 0.76f; x += 0.5f)
                Timber(new Vector3(x, 1.5f, z + 0.06f), new Vector3(0.03f, 3f, 0.02f), 0.2f);
            foreach (float y in new[] { 0.6f, 2.4f })
                Iron(new Vector3(0f, y, z + 0.08f), new Vector3(2.1f, 0.12f, 0.03f));
            foreach (float side in new[] { -1f, 1f })
                Timber(new Vector3(side * 1.2f, 1.6f, z + 0.1f), new Vector3(0.34f, 3.2f, 0.3f), 0.42f);
            Timber(new Vector3(0f, 3.25f, z + 0.1f), new Vector3(2.8f, 0.34f, 0.3f), 0.42f);
            room.SetColor(Pal.Brass, 0.5f);
            room.Prism(new Vector3(0.68f, 1.3f, z + 0.1f), 0.06f, 0.06f, 0.14f, 6);
        }

        protected void Candle(Vector3 p, float height)
        {
            room.SetColor(Pal.Brass, 0.4f);
            room.Prism(p, 0.1f, 0.1f, 0.015f, 8);
            room.SetColor(Pal.Plaster, 0.62f);
            room.Prism(p, 0.04f, 0.036f, height, 6);
            room.SetColor(Pal.Lantern, 0.85f);
            room.Prism(p + Vector3.up * height, 0.024f, 0f, 0.1f, 4, false);
            AddGlow(p + Vector3.up * (height + 0.3f), CandleLight, 4f);
        }

        protected void Lantern(Vector3 p, float size)
        {
            float w = 0.26f * size, h = 0.42f * size;
            room.SetColor(Pal.SteelDark, 0.34f);
            room.Prism(p + new Vector3(0f, -0.04f, 0f), w * 0.8f, w, 0.05f, 8, true, true, Mathf.PI / 8f);
            room.Prism(p + new Vector3(0f, h, 0f), w * 1.15f, w * 0.3f, 0.18f * size, 8, true, true, Mathf.PI / 8f);
            room.Box(p + new Vector3(0f, h + 0.26f * size, 0f), new Vector3(0.04f, 0.14f * size, 0.04f));
            foreach (float x in new[] { -1f, 1f })
            foreach (float zz in new[] { -1f, 1f })
                room.Box(p + new Vector3(x * w * 0.8f, h * 0.5f, zz * w * 0.8f), new Vector3(0.03f, h, 0.03f));
            room.SetColor(Pal.FireOrange, 0.42f);
            room.Current.Emission = 0.5f;
            room.Prism(p, w * 0.72f, w * 0.72f, h, 8, true, true, Mathf.PI / 8f);
            room.SetColor(Pal.FireYellow, 0.7f);
            room.Prism(p + new Vector3(0f, h * 0.12f, 0f), w * 0.36f, 0f, h * 0.7f, 5, false);
        }

        /// <summary>A real light on top of the baked ones, so a fire still breathes.</summary>
        protected void Light(string name, Vector3 position, Color color, float range, float intensity, float variation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = origin + position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            var flicker = go.AddComponent<LightFlicker>();
            SceneWiring.Set(flicker, "baseIntensity", intensity);
            SceneWiring.Set(flicker, "intensityVariation", variation);
        }
    }
}
