using AshenRealm.Story;
using UnityEngine;

namespace AshenRealm.AI
{
    /// <summary>Reusable floor outlines. Their final size is honest from the start of the windup.</summary>
    public sealed class EnemyAttackTelegraph : MonoBehaviour
    {
        [SerializeField] Transform area;
        [SerializeField] Transform inner;
        [SerializeField] Transform lane;
        Renderer[] outlines;
        MaterialPropertyBlock block;
        static readonly int Tint = Shader.PropertyToID("_Tint");
        public bool Visible => area.gameObject.activeSelf || lane.gameObject.activeSelf;

        void Awake()
        {
            outlines = new[] { area.GetComponent<Renderer>(), inner.GetComponent<Renderer>(), lane.GetComponent<Renderer>() };
            block = new MaterialPropertyBlock();
            Hide();
        }

        public void Hide()
        {
            area.gameObject.SetActive(false);
            inner.gameObject.SetActive(false);
            lane.gameObject.SetActive(false);
        }

        public void Show(EnemyMove move, Vector3 source, Vector3 destination, Vector3 direction, float reach, float progress)
        {
            bool line = move.Kind == MoveKind.Charge || move.Kind == MoveKind.Pounce;
            bool warning = line || move.Kind == MoveKind.Ground || move.Kind == MoveKind.Ring || move.Kind == MoveKind.Burrow ||
                move.Kind == MoveKind.Sweep || move.Kind == MoveKind.Counter || move.Kind == MoveKind.Teleport;
            if (!warning) { Hide(); return; }
            area.gameObject.SetActive(!line);
            inner.gameObject.SetActive(move.Kind == MoveKind.Ring);
            lane.gameObject.SetActive(line);
            Vector3 centre = move.Kind == MoveKind.Ground || move.Kind == MoveKind.Burrow || move.Kind == MoveKind.Teleport ? destination : source;
            centre.y = source.y + 0.045f;
            float radius = move.Kind == MoveKind.Counter ? reach + 0.4f : move.Radius;
            area.SetPositionAndRotation(centre, Quaternion.identity);
            area.localScale = new Vector3(radius, 1f, radius);
            inner.SetPositionAndRotation(centre, Quaternion.identity);
            inner.localScale = new Vector3(radius * 0.53f, 1f, radius * 0.53f);
            if (line)
            {
                float halfWidth = Mathf.Max(0.8f, reach - 0.1f);
                float length = move.TravelDistance + halfWidth + 0.55f;
                lane.SetPositionAndRotation(source + Vector3.up * 0.045f + direction * length * 0.5f, Quaternion.LookRotation(direction));
                lane.localScale = new Vector3(halfWidth * 2f, 1f, Mathf.Max(0.5f, length));
            }
            Color color = move.Kind == MoveKind.Counter ? new Color(1f, 0.73f, 0.19f) :
                move.Bolt == BoltKind.Venom ? new Color(0.35f, 0.9f, 0.48f) :
                move.Bolt == BoltKind.Brine ? new Color(0.25f, 0.74f, 1f) :
                move.Bolt == BoltKind.Fire ? new Color(1f, 0.4f, 0.12f) :
                move.Kind == MoveKind.Ring ? new Color(0.8f, 0.46f, 1f) : new Color(1f, 0.35f, 0.2f);
            color *= 0.7f + 0.3f * Mathf.Clamp01(progress);
            color.a = 1f;
            block.SetColor(Tint, color);
            foreach (Renderer outline in outlines) outline.SetPropertyBlock(block);
        }
    }
}
