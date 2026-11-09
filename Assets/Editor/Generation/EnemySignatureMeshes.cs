using UnityEngine;

namespace AshenRealm.EditorTools
{
    /// <summary>Named foes carry recognisable silhouettes; pieces share the existing world material.</summary>
    internal static class EnemySignatureMeshes
    {
        static void Beam(MeshBuilder b, Vector3 from, Vector3 to, float width, Pal family, float shade = 0.5f)
        {
            Vector3 span = to - from;
            if (span.sqrMagnitude < 0.0001f) return;
            b.Push((from + to) * 0.5f, Quaternion.FromToRotation(Vector3.up, span.normalized), Vector3.one);
            b.SetColor(family, shade); b.Box(Vector3.zero, new Vector3(width, span.magnitude, width)); b.Pop();
        }
        static void Halo(MeshBuilder b, Vector3 at, float radius, Pal family, bool broken = false)
        {
            for (int i = 0; i < 24; i++)
            {
                if (broken && (i == 3 || i == 4 || i == 15)) continue;
                float a = i * Mathf.PI / 12f, c = (i + 1) * Mathf.PI / 12f;
                Beam(b, at + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius,
                    at + new Vector3(Mathf.Cos(c), Mathf.Sin(c), 0f) * radius, 0.04f, family, 0.55f);
            }
        }
        static void Coin(MeshBuilder b, Vector3 at, float radius)
        {
            b.Push(at, Quaternion.Euler(90f, 0f, 0f), Vector3.one);
            b.SetColor(Pal.Brass, 0.62f); b.Prism(Vector3.zero, radius, radius, 0.025f, 8); b.Pop();
        }
        static void Skull(MeshBuilder b, Vector3 at, float size)
        {
            b.SetColor(Pal.Bone, 0.5f); b.Box(at, new Vector3(0.23f, 0.24f, 0.18f) * size);
            b.Box(at + new Vector3(0f, -0.13f, 0.02f) * size, new Vector3(0.16f, 0.08f, 0.13f) * size);
            b.SetColor(Pal.Black, 0.15f);
            foreach (float side in new[] { -1f, 1f }) b.Box(at + new Vector3(side * 0.055f, 0.025f, 0.095f) * size, new Vector3(0.055f, 0.06f, 0.015f) * size);
        }
        static void Chain(MeshBuilder b, Vector3 from, Vector3 to, int links)
        {
            for (int i = 0; i < links; i++)
            {
                Vector3 p = Vector3.Lerp(from, to, i / (float)(links - 1));
                b.SetColor(Pal.SteelDark, 0.45f);
                b.Box(p, i % 2 == 0 ? new Vector3(0.035f, 0.075f, 0.07f) : new Vector3(0.07f, 0.075f, 0.035f));
            }
        }

        public static void Regalia(MeshBuilder b, string id)
        {
            switch (id)
            {
                case "morr":
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Chain(b, new Vector3(side * 0.42f, 1.62f, 0.16f), new Vector3(side * 0.31f, 0.56f, 0.3f), 11);
                        for (int i = 0; i < 4; i++) Coin(b, new Vector3(side * (0.32f + i * 0.018f), 0.6f + i * 0.24f, 0.32f), 0.075f);
                    }
                    b.SetColor(Pal.Leather, 0.35f); b.Box(new Vector3(-0.4f, 0.92f, 0.2f), new Vector3(0.21f, 0.31f, 0.1f));
                    Halo(b, new Vector3(0f, 1.88f, -0.16f), 0.38f, Pal.Brass, true);
                    break;
                case "sporetyrant":
                    for (int i = 0; i < 7; i++)
                    {
                        float side = i % 2 == 0 ? -1f : 1f;
                        float x = side * (0.24f + (i % 3) * 0.17f), y = 1.55f + (i % 3) * 0.12f;
                        b.SetColor(Pal.FungalRock, 0.4f); b.Prism(new Vector3(x, y - 0.2f, 0f), 0.035f, 0.05f, 0.32f, 5);
                        b.SetColor(Pal.FungalCap, 0.48f); b.Prism(new Vector3(x, y + 0.12f, 0f), 0.25f, 0.035f, 0.16f, 7);
                        b.SetColor(Pal.FungalGlow, 0.4f); b.Current.Emission = 0.14f;
                        b.Prism(new Vector3(x, y + 0.1f, 0f), 0.19f, 0.19f, 0.025f, 7);
                    }
                    break;
                case "brine":
                    b.SetColor(Pal.PaintBlue, 0.35f);
                    b.Box(new Vector3(0f, 1.96f, 0f), new Vector3(0.28f, 0.4f, 0.17f));
                    b.SetColor(Pal.Brass, 0.45f);
                    b.Triangle(new Vector3(-0.19f, 1.9f, 0.11f), new Vector3(0.19f, 1.9f, 0.11f), new Vector3(0f, 2.5f, 0.11f), Vector3.forward);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Beam(b, new Vector3(side * 0.16f, 2.07f, -0.07f), new Vector3(side * 0.3f, 1.03f, -0.1f), 0.09f, Pal.RobeRot);
                        Chain(b, new Vector3(side * 0.33f, 1.5f, 0.12f), new Vector3(side * 0.44f, 0.7f, 0.18f), 8);
                    }
                    break;
                case "pyraxes":
                    b.SetColor(Pal.Black, 0.1f); b.Box(new Vector3(0f, 1.42f, 0.2f), new Vector3(0.36f, 0.42f, 0.12f));
                    b.SetColor(Pal.Lava, 0.6f); b.Current.Emission = 0.5f;
                    b.Box(new Vector3(0f, 1.42f, 0.27f), new Vector3(0.26f, 0.3f, 0.025f));
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Beam(b, new Vector3(side * 0.065f, 1.2f, 0.3f), new Vector3(side * 0.065f, 1.63f, 0.3f), 0.035f, Pal.SteelDark);
                        b.SetColor(Pal.Basalt, 0.32f); b.Prism(new Vector3(side * 0.45f, 1.57f, -0.03f), 0.16f, 0.12f, 0.5f, 5);
                        b.SetColor(Pal.Lava, 0.45f); b.Current.Emission = 0.3f;
                        b.Prism(new Vector3(side * 0.45f, 2.07f, -0.03f), 0.08f, 0.01f, 0.25f, 5);
                    }
                    break;
                case "gloamlord":
                    Halo(b, new Vector3(0f, 2.16f, -0.25f), 0.55f, Pal.Bone, true);
                    for (int i = 0; i < 9; i++)
                    {
                        float a = (20f + i * 17.5f) * Mathf.Deg2Rad;
                        Vector3 direction = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                        Beam(b, new Vector3(0f, 1.82f, -0.28f) + direction * 0.25f,
                            new Vector3(0f, 1.82f, -0.28f) + direction * (i % 2 == 0 ? 1.05f : 0.86f), 0.065f, Pal.Bone, 0.5f);
                    }
                    foreach (float side in new[] { -1f, 1f }) Skull(b, new Vector3(side * 0.45f, 1.65f, 0.04f), 1.15f);
                    break;
                case "marrowcantor":
                    Halo(b, new Vector3(0f, 1.92f, -0.12f), 0.46f, Pal.Brass);
                    Skull(b, new Vector3(-0.45f, 1.65f, 0.05f), 0.85f);
                    Skull(b, new Vector3(0.45f, 1.65f, 0.05f), 0.85f);
                    for (int i = 0; i < 4; i++)
                        Beam(b, new Vector3(-0.25f + i * 0.17f, 1.35f, 0.2f), new Vector3(-0.25f + i * 0.17f, 1.77f, 0.2f), 0.045f, Pal.Brass);
                    break;
                case "cinderprophet":
                    for (int i = 0; i < 7; i++)
                    {
                        float angle = (i * 25f + 15f) * Mathf.Deg2Rad;
                        Vector3 ray = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                        Beam(b, new Vector3(0f, 1.88f, -0.12f) + ray * 0.25f, new Vector3(0f, 1.88f, -0.12f) + ray * 0.66f, 0.035f, Pal.Basalt);
                        b.SetColor(Pal.Lava, 0.5f); b.Current.Emission = 0.3f;
                        b.Box(new Vector3(0f, 1.88f, -0.12f) + ray * 0.7f, new Vector3(0.06f, 0.09f, 0.05f));
                    }
                    break;
                case "gravemaw":
                    for (int i = 0; i < 7; i++)
                    {
                        b.SetColor(Pal.Bone, 0.45f);
                        b.Push(new Vector3(0f, 0.93f, -0.45f + i * 0.13f), Quaternion.Euler(-25f, 0f, 0f), Vector3.one);
                        b.Prism(Vector3.zero, 0.075f, 0.005f, 0.27f + (i % 2) * 0.1f, 4); b.Pop();
                    }
                    Skull(b, new Vector3(0f, 0.64f, 0.73f), 1.8f);
                    break;
                case "undertow":
                    for (int i = 0; i < 4; i++)
                    {
                        float y = 0.3f + i * 0.35f;
                        b.SetColor(Pal.WetStone, 0.3f); b.Prism(new Vector3(0f, y, 0f), 0.32f - i * 0.025f, 0.27f, 0.06f, 12);
                    }
                    foreach (float side in new[] { -1f, 1f })
                        Chain(b, new Vector3(side * 0.22f, 1.4f, 0f), new Vector3(side * 0.4f, -0.15f, 0.1f), 12);
                    break;
                case "mycel":
                    for (int i = 0; i < 6; i++)
                    {
                        float angle = i * Mathf.PI / 3f;
                        Vector3 p = new Vector3(Mathf.Cos(angle) * 0.55f, 0.6f, Mathf.Sin(angle) * 0.5f);
                        b.SetColor(Pal.FungalRock, 0.4f); b.Prism(p, 0.07f, 0.05f, 0.6f + (i % 2) * 0.2f, 6);
                        b.SetColor(Pal.FungalCap, 0.52f); b.Prism(p + Vector3.up * (0.6f + (i % 2) * 0.2f), 0.32f, 0.03f, 0.2f, 8);
                        Skull(b, p + new Vector3(0f, 0.15f, 0.09f), 0.45f);
                    }
                    break;
            }
        }

        public static void Weapon(MeshBuilder b, string id)
        {
            b.SetColor(Pal.WoodDark, 0.32f); b.Prism(new Vector3(0f, -0.32f, 0f), 0.035f, 0.03f, 1.55f, 6);
            if (id == "pyraxes")
            {
                b.SetColor(Pal.Basalt, 0.42f); b.Box(new Vector3(0f, 1.12f, 0f), new Vector3(0.75f, 0.42f, 0.42f));
                for (int i = 0; i < 3; i++)
                {
                    b.SetColor(Pal.Lava, 0.55f); b.Current.Emission = 0.4f;
                    b.Box(new Vector3(-0.25f + i * 0.25f, 1.12f, -0.22f), new Vector3(0.06f, 0.3f, 0.03f));
                }
            }
            else if (id == "brine")
            {
                Beam(b, new Vector3(-0.28f, 1.12f, 0f), new Vector3(0.28f, 1.12f, 0f), 0.055f, Pal.Brass);
                foreach (float x in new[] { -0.28f, 0f, 0.28f })
                {
                    b.SetColor(Pal.Steel, 0.6f); b.Prism(new Vector3(x, 1.1f, 0f), 0.06f, 0.005f, x == 0f ? 0.6f : 0.4f, 4);
                }
            }
            else
            {
                Chain(b, new Vector3(0f, 1.15f, 0f), new Vector3(0.25f, 0.65f, 0f), 7);
                b.SetColor(Pal.Basalt, 0.38f); b.Prism(new Vector3(0.25f, 0.45f, 0f), 0.19f, 0.13f, 0.24f, 7);
                b.SetColor(Pal.Lava, 0.5f); b.Current.Emission = 0.4f;
                b.Prism(new Vector3(0.25f, 0.7f, 0f), 0.12f, 0.005f, 0.27f, 6);
            }
        }
    }
}
