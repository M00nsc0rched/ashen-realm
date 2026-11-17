using UnityEngine;

namespace AshenRealm.EditorTools
{
    /// <summary>
    /// The horse: Y up, front -Z, the ground under the saddle at the origin, about 1.55 m at the withers. Each moving part is built
    /// about its own pivot so <see cref="AshenRealm.AI.HorseAnimator"/> can turn it; <see cref="Standing"/> puts them together
    /// for the paddock's still horses. The neck rises steeply and the head hangs forward, so from the saddle (eye 2.45 m above
    /// the origin) only the poll and the ears show, low in the middle of the frame.
    /// </summary>
    internal static class HorseMeshes
    {
        public const int MaxRiddenVertices = 4000;
        public static readonly Vector3 NeckPivot = new Vector3(0f, 1.38f, -0.62f);
        /// <summary>The ears' pivot in the neck's frame: on top of the poll.</summary>
        public static readonly Vector3 EarsPivot = new Vector3(0f, 0.74f, -0.5f);
        public static readonly Vector3 TailPivot = new Vector3(0f, 1.38f, 0.92f);
        /// <summary>Front left, front right, hind left, hind right: shoulders and hips, one metre above the hooves.</summary>
        public static readonly Vector3[] LegPivots =
        {
            new Vector3(-0.16f, 1f, -0.62f), new Vector3(0.16f, 1f, -0.62f), new Vector3(-0.16f, 1f, 0.62f), new Vector3(0.16f, 1f, 0.62f),
        };

        static readonly Vector3 Poll = new Vector3(0f, 0.62f, -0.5f);
        static readonly Quaternion HeadTilt = Quaternion.Euler(-50f, 0f, 0f);
        static readonly Quaternion NeckTilt = Quaternion.Euler(-39f, 0f, 0f);

        public static void Body(MeshBuilder b, Pal coat, bool saddled)
        {
            b.SetColor(coat, 0.5f);
            b.Box(new Vector3(0f, 1.12f, 0.05f), new Vector3(0.56f, 0.6f, 1.5f));
            b.SetColor(coat, 0.56f);
            b.Box(new Vector3(0f, 1.13f, -0.7f), new Vector3(0.52f, 0.62f, 0.36f));
            b.Box(new Vector3(0f, 1.42f, -0.45f), new Vector3(0.3f, 0.2f, 0.5f));
            b.SetColor(coat, 0.46f);
            b.Box(new Vector3(0f, 1.2f, 0.72f), new Vector3(0.54f, 0.58f, 0.46f));
            b.Box(new Vector3(0f, 0.86f, 0.05f), new Vector3(0.46f, 0.14f, 1.2f));
            if (!saddled) return;

            b.SetColor(Pal.ClothRed, 0.4f);
            b.Box(new Vector3(0f, 1.43f, -0.02f), new Vector3(0.62f, 0.04f, 0.72f));
            b.SetColor(Pal.Leather, 0.36f);
            b.Box(new Vector3(0f, 1.5f, 0f), new Vector3(0.42f, 0.12f, 0.56f));
            b.Box(new Vector3(0f, 1.61f, -0.25f), new Vector3(0.2f, 0.14f, 0.1f));
            b.Box(new Vector3(0f, 1.6f, 0.27f), new Vector3(0.36f, 0.14f, 0.08f));
            b.SetColor(Pal.Leather, 0.26f);
            b.Box(new Vector3(0f, 1.12f, -0.2f), new Vector3(0.59f, 0.63f, 0.08f));
            foreach (float side in new[] { -1f, 1f })
            {
                b.SetColor(Pal.Leather, 0.3f);
                b.Box(new Vector3(side * 0.3f, 1.17f, 0f), new Vector3(0.03f, 0.56f, 0.05f));
                b.SetColor(Pal.SteelDark, 0.45f);
                b.Box(new Vector3(side * 0.31f, 0.88f, 0f), new Vector3(0.06f, 0.05f, 0.14f));
            }
        }

        /// <summary>Neck, head, mane and bridle, about <see cref="NeckPivot"/>.</summary>
        public static void Neck(MeshBuilder b, Pal coat, Pal mane, bool bridled)
        {
            b.Push(Vector3.zero, NeckTilt, Vector3.one);
            b.SetColor(coat, 0.52f);
            b.Box(new Vector3(0f, 0.22f, 0f), new Vector3(0.3f, 0.5f, 0.44f));
            b.SetColor(coat, 0.56f);
            b.Box(new Vector3(0f, 0.6f, 0f), new Vector3(0.22f, 0.42f, 0.32f));
            b.SetColor(mane, 0.3f);
            b.Box(new Vector3(0f, 0.42f, 0.2f), new Vector3(0.08f, 0.86f, 0.1f));
            for (int tuft = 0; tuft < 4; tuft++)
                b.Box(new Vector3(tuft % 2 == 0 ? 0.06f : -0.06f, 0.15f + tuft * 0.2f, 0.18f), new Vector3(0.05f, 0.16f, 0.12f));
            b.Pop();

            b.Push(Poll, HeadTilt, Vector3.one);
            b.SetColor(coat, 0.58f);
            b.Box(new Vector3(0f, 0f, -0.1f), new Vector3(0.24f, 0.26f, 0.3f));
            b.SetColor(coat, 0.52f);
            b.Box(new Vector3(0f, -0.02f, -0.38f), new Vector3(0.19f, 0.2f, 0.32f));
            b.SetColor(Pal.Black, 0.15f);
            foreach (float side in new[] { -1f, 1f })
            {
                b.Box(new Vector3(side * 0.123f, 0.05f, -0.1f), new Vector3(0.02f, 0.05f, 0.06f));
                b.Box(new Vector3(side * 0.05f, -0.06f, -0.545f), new Vector3(0.04f, 0.04f, 0.02f));
            }
            b.SetColor(mane, 0.32f);
            b.Box(new Vector3(0f, 0.14f, -0.06f), new Vector3(0.1f, 0.05f, 0.16f));
            if (bridled)
            {
                b.SetColor(Pal.Leather, 0.3f);
                b.Box(new Vector3(0f, -0.02f, -0.34f), new Vector3(0.21f, 0.22f, 0.04f));
                b.Box(new Vector3(0f, 0.06f, 0.02f), new Vector3(0.26f, 0.04f, 0.04f));
                foreach (float side in new[] { -1f, 1f })
                {
                    b.Box(new Vector3(side * 0.125f, 0f, -0.18f), new Vector3(0.015f, 0.04f, 0.34f));
                    b.SetColor(Pal.Brass, 0.55f);
                    b.Box(new Vector3(side * 0.105f, -0.08f, -0.42f), new Vector3(0.03f, 0.05f, 0.05f));
                    b.SetColor(Pal.Leather, 0.3f);
                }
            }
            b.Pop();

            if (!bridled) return;
            // The reins run from the bit back along the neck to the withers, where the rider's hands would hold them.
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 bit = Poll + HeadTilt * new Vector3(side * 0.11f, -0.08f, -0.42f);
                WayfarerMeshes.Beam(b, bit, new Vector3(side * 0.17f, 0.05f, 0.3f), 0.025f, Pal.Leather);
            }
        }

        /// <summary>Both ears, about <see cref="EarsPivot"/>: they flick together.</summary>
        public static void Ears(MeshBuilder b, Pal coat)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                b.Push(new Vector3(side * 0.07f, 0f, 0f), Quaternion.Euler(-12f, 0f, side * -14f), Vector3.one);
                b.SetColor(coat, 0.48f);
                b.Prism(Vector3.zero, 0.045f, 0.012f, 0.17f, 4, false, false, Mathf.PI * 0.25f);
                b.Pop();
            }
        }

        /// <summary>One leg hanging from its shoulder or hip, the hoof on the ground one metre below.</summary>
        public static void Leg(MeshBuilder b, Pal coat, bool hind)
        {
            b.SetColor(coat, 0.5f);
            b.Box(new Vector3(0f, -0.22f, hind ? 0.03f : 0f), hind ? new Vector3(0.18f, 0.5f, 0.28f) : new Vector3(0.17f, 0.5f, 0.22f));
            b.SetColor(coat, 0.44f);
            b.Box(new Vector3(0f, -0.66f, 0f), new Vector3(0.1f, 0.42f, 0.11f));
            b.SetColor(Pal.Black, 0.25f);
            b.Box(new Vector3(0f, -0.94f, -0.01f), new Vector3(0.13f, 0.12f, 0.15f));
        }

        public static void Tail(MeshBuilder b, Pal mane)
        {
            b.SetColor(mane, 0.34f);
            b.Box(new Vector3(0f, -0.06f, 0.08f), new Vector3(0.12f, 0.2f, 0.18f));
            b.SetColor(mane, 0.28f);
            b.Box(new Vector3(0f, -0.45f, 0.14f), new Vector3(0.16f, 0.68f, 0.1f));
            b.Box(new Vector3(0f, -0.85f, 0.12f), new Vector3(0.11f, 0.18f, 0.08f));
        }

        /// <summary>A whole unsaddled horse standing still: the paddock's.</summary>
        public static void Standing(MeshBuilder b, Pal coat, Pal mane)
        {
            Body(b, coat, false);
            b.Push(NeckPivot, Quaternion.identity, Vector3.one);
            Neck(b, coat, mane, false);
            b.Push(EarsPivot, Quaternion.identity, Vector3.one);
            Ears(b, coat);
            b.Pop();
            b.Pop();
            b.Push(TailPivot, Quaternion.identity, Vector3.one);
            Tail(b, mane);
            b.Pop();
            for (int i = 0; i < LegPivots.Length; i++)
            {
                b.Push(LegPivots[i], Quaternion.identity, Vector3.one);
                Leg(b, coat, i >= 2);
                b.Pop();
            }
        }
    }
}
