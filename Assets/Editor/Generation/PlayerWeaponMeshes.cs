using UnityEngine;

namespace AshenRealm.EditorTools
{
    /// <summary>Class-specific tools, all authored with the grip at the origin.</summary>
    internal static partial class PlayerWeaponMeshes
    {
        public static void Glove(MeshBuilder b)
        {
            b.SetColor(Pal.ClothDark, 0.4f);
            b.Box(new Vector3(0f, -0.2f, -0.035f), new Vector3(0.11f, 0.25f, 0.12f));
            b.SetColor(Pal.Leather, 0.52f);
            b.Box(new Vector3(0f, -0.035f, -0.025f), new Vector3(0.12f, 0.11f, 0.11f));
            for (int i = 0; i < 3; i++)
            {
                b.SetColor(Pal.Leather, 0.42f + i * 0.035f);
                b.Box(new Vector3(-0.035f + i * 0.035f, -0.01f, 0.035f), new Vector3(0.028f, 0.055f, 0.04f));
            }
        }

        public static void KnightAxe(MeshBuilder b)
        {
            b.SetColor(Pal.WoodDark, 0.43f);
            b.Prism(new Vector3(0f, -0.22f, 0f), 0.025f, 0.021f, 1.1f, 7);
            b.SetColor(Pal.Leather, 0.34f);
            b.Prism(new Vector3(0f, -0.11f, 0f), 0.03f, 0.03f, 0.23f, 7);
            b.SetColor(Pal.SteelDark, 0.62f);
            b.Box(new Vector3(0f, 0.67f, 0f), new Vector3(0.09f, 0.21f, 0.07f));
            Blade(b, new[] { new Vector2(-0.025f, 0.77f), new Vector2(-0.23f, 0.91f),
                new Vector2(-0.34f, 0.79f), new Vector2(-0.35f, 0.53f), new Vector2(-0.22f, 0.42f),
                new Vector2(-0.025f, 0.58f) }, 0.045f);
            b.SetColor(Pal.Brass, 0.55f);
            b.Prism(new Vector3(0f, 0.86f, 0f), 0.036f, 0f, 0.11f, 5);
        }

        static void Blade(MeshBuilder b, Vector2[] outline, float thickness)
        {
            Vector3 middle = Vector3.zero;
            foreach (Vector2 point in outline) middle += new Vector3(point.x, point.y, 0f) / outline.Length;
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 from = outline[i], to = outline[(i + 1) % outline.Length];
                Vector3 a = new Vector3(from.x, from.y, -thickness), c = new Vector3(to.x, to.y, -thickness);
                b.SetColor(Pal.Steel, 0.5f + (i % 3) * 0.06f);
                b.Triangle(middle + Vector3.back * thickness, a, c, Vector3.back);
                b.Triangle(middle + Vector3.forward * thickness, c + Vector3.forward * thickness * 2f,
                    a + Vector3.forward * thickness * 2f, Vector3.forward);
                b.SetColor(Pal.Steel, 0.72f);
                b.QuadOutward(a, a + Vector3.forward * thickness * 2f, c + Vector3.forward * thickness * 2f, c, middle);
            }
        }

        public static void BoneStaff(MeshBuilder b)
        {
            b.SetColor(Pal.WoodDark, 0.24f);
            b.Prism(new Vector3(0f, -0.3f, 0f), 0.028f, 0.023f, 1.22f, 6);
            b.SetColor(Pal.Bone, 0.62f);
            for (int i = 0; i < 5; i++) b.Box(new Vector3(0f, 0.36f + i * 0.11f, 0f), new Vector3(0.075f, 0.035f, 0.06f));
            b.Box(new Vector3(0f, 0.96f, 0f), new Vector3(0.2f, 0.2f, 0.17f));
            b.Box(new Vector3(0f, 0.83f, 0.025f), new Vector3(0.14f, 0.07f, 0.13f));
            b.SetColor(Pal.Shadow, 0.08f);
            foreach (float side in new[] { -1f, 1f }) b.Box(new Vector3(side * 0.05f, 0.98f, -0.088f), new Vector3(0.045f, 0.05f, 0.016f));
            b.SetColor(Pal.FungalGlow, 0.8f);
            b.Box(new Vector3(0f, 1.1f, 0f), new Vector3(0.05f, 0.08f, 0.05f));
        }

        public static void BoneTalisman(MeshBuilder b)
        {
            PropMeshes.CastingHand(b);
            b.SetColor(Pal.Bone, 0.58f);
            b.Box(new Vector3(0f, 0.16f, 0f), new Vector3(0.13f, 0.1f, 0.11f));
            b.Prism(new Vector3(0f, 0.21f, 0f), 0.04f, 0f, 0.1f, 5);
        }

        public static void ReagentBottle(MeshBuilder b)
        {
            b.SetColor(Pal.FungalGlow, 0.43f);
            b.Current.Emission = 0.03f;
            b.Prism(new Vector3(0f, 0.03f, 0f), 0.095f, 0.11f, 0.19f, 8);
            b.Prism(new Vector3(0f, 0.22f, 0f), 0.11f, 0.045f, 0.09f, 8);
            b.SetColor(Pal.Brass, 0.5f);
            b.Prism(new Vector3(0f, 0.31f, 0f), 0.047f, 0.047f, 0.08f, 8);
            b.SetColor(Pal.Wood, 0.42f);
            b.Prism(new Vector3(0f, 0.39f, 0f), 0.045f, 0.04f, 0.06f, 7);
            b.SetColor(Pal.Plaster, 0.78f);
            b.Box(new Vector3(-0.045f, 0.13f, -0.099f), new Vector3(0.018f, 0.08f, 0.012f));
        }

        public static void AlchemistKit(MeshBuilder b)
        {
            b.SetColor(Pal.Leather, 0.42f);
            b.Box(new Vector3(0f, 0.1f, 0f), new Vector3(0.25f, 0.27f, 0.16f));
            b.SetColor(Pal.Brass, 0.58f);
            b.Box(new Vector3(0f, 0.16f, -0.09f), new Vector3(0.05f, 0.09f, 0.025f));
            foreach (float side in new[] { -1f, 1f })
            {
                b.SetColor(side < 0f ? Pal.FireOrange : Pal.FungalGlow, 0.62f);
                b.Prism(new Vector3(side * 0.073f, 0.24f, 0f), 0.028f, 0.025f, 0.16f, 6);
                b.SetColor(Pal.Wood, 0.35f);
                b.Prism(new Vector3(side * 0.073f, 0.4f, 0f), 0.027f, 0.027f, 0.04f, 6);
            }
        }
    }
}
