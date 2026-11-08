using UnityEngine;

namespace AshenRealm.EditorTools
{
    /// <summary>Small valley landmarks and household objects, merged into their owner's static mesh.</summary>
    internal static class WayfarerMeshes
    {
        public static void Beam(MeshBuilder b, Vector3 from, Vector3 to, float width, Pal family = Pal.WoodDark)
        {
            Vector3 delta = to - from;
            b.SetColor(family, 0.38f);
            b.Push((from + to) * 0.5f, Quaternion.LookRotation(delta), Vector3.one);
            b.Box(Vector3.zero, new Vector3(width, width, delta.magnitude));
            b.Pop();
        }

        public static void Well(MeshBuilder b, System.Random rng)
        {
            b.SetColor(Pal.Black, 0.06f);
            b.Prism(new Vector3(0f, 0.05f, 0f), 0.8f, 0.8f, 0.1f, 16);
            b.SetColor(Pal.Water, 0.3f);
            b.Prism(new Vector3(0f, 0.17f, 0f), 0.7f, 0.7f, 0.02f, 16);
            for (int row = 0; row < 3; row++)
            for (int i = 0; i < 14; i++)
            {
                float a = (i + row % 2 * 0.5f) * Mathf.PI * 2f / 14f;
                b.Push(new Vector3(Mathf.Cos(a), 0.18f + row * 0.27f, Mathf.Sin(a)), Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f), Vector3.one);
                b.ShadedBox(Vector3.zero, new Vector3(0.34f, 0.25f, 0.47f), row == 0 ? Pal.StoneMoss : Pal.Stone, rng, 0.34f, 0.55f);
                b.Pop();
            }
            foreach (float side in new[] { -1f, 1f })
            {
                b.SetColor(Pal.WoodDark, 0.34f);
                b.Box(new Vector3(side * 1.35f, 1.6f, 0f), new Vector3(0.18f, 3.2f, 0.2f));
                Beam(b, new Vector3(side * 1.35f, 2.6f, 0f), new Vector3(0f, 3.75f, 0f), 0.16f);
                b.Push(new Vector3(side * 0.73f, 3.3f, 0f), Quaternion.Euler(0f, 0f, side * -32f), Vector3.one);
                b.SetColor(Pal.RoofTile, 0.48f);
                b.Box(Vector3.zero, new Vector3(1.9f, 0.14f, 2.2f));
                b.Pop();
            }
            Beam(b, new Vector3(-1.4f, 2.3f, 0f), new Vector3(1.55f, 2.3f, 0f), 0.16f);
            b.SetColor(Pal.Thatch, 0.42f);
            b.Box(new Vector3(0.3f, 1.55f, 0f), new Vector3(0.035f, 1.45f, 0.035f));
            Bucket(b, new Vector3(0.3f, 0.62f, 0f), 0.85f);
        }

        public static void Bucket(MeshBuilder b, Vector3 at, float size)
        {
            b.Push(at, Quaternion.identity, Vector3.one * size);
            b.SetColor(Pal.Wood, 0.43f);
            b.Prism(Vector3.zero, 0.16f, 0.23f, 0.34f, 10, true, false);
            b.SetColor(Pal.Black, 0.1f);
            b.Prism(Vector3.up * 0.31f, 0.18f, 0.18f, 0.02f, 10);
            foreach (float y in new[] { 0.05f, 0.27f })
            {
                b.SetColor(Pal.SteelDark, 0.42f);
                b.Prism(Vector3.up * y, 0.18f + y * 0.16f, 0.19f + y * 0.16f, 0.035f, 10, false, false);
            }
            Beam(b, new Vector3(-0.19f, 0.3f, 0f), new Vector3(0f, 0.57f, 0f), 0.025f, Pal.SteelDark);
            Beam(b, new Vector3(0.19f, 0.3f, 0f), new Vector3(0f, 0.57f, 0f), 0.025f, Pal.SteelDark);
            b.Pop();
        }

        public static void Bunting(MeshBuilder b, Vector3 left, Vector3 right, int flags)
        {
            Vector3 previous = left;
            for (int i = 1; i <= flags * 2; i++)
            {
                float t = i / (flags * 2f);
                Vector3 point = Vector3.Lerp(left, right, t) + Vector3.down * (Mathf.Sin(t * Mathf.PI) * 0.35f);
                Beam(b, previous, point, 0.025f, Pal.Thatch);
                previous = point;
            }
            for (int i = 0; i < flags; i++)
            {
                float t = (i + 0.5f) / flags;
                Vector3 at = Vector3.Lerp(left, right, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * 0.35f;
                Vector3 along = (right - left).normalized * 0.28f;
                b.SetColor(i % 3 == 0 ? Pal.ClothRed : i % 3 == 1 ? Pal.Thatch : Pal.ClothDark, 0.5f);
                b.DoubleSided(at - along, at + along, at + new Vector3(0.08f, -0.55f, 0f), Vector3.back);
            }
        }

        public static void PackHorse(MeshBuilder b, System.Random rng)
        {
            b.ShadedBox(new Vector3(0f, 1.1f, 0f), new Vector3(0.78f, 0.9f, 1.8f), Pal.FurDark, rng, 0.4f, 0.54f);
            foreach (float side in new[] { -1f, 1f })
            foreach (float end in new[] { -1f, 1f })
            {
                b.ShadedBox(new Vector3(side * 0.28f, 0.48f, end * 0.65f), new Vector3(0.17f, 0.96f, 0.18f), Pal.FurDark, rng, 0.35f, 0.48f);
                b.SetColor(Pal.Black, 0.3f);
                b.Box(new Vector3(side * 0.28f, 0.08f, end * 0.65f + 0.04f), new Vector3(0.22f, 0.16f, 0.27f));
            }
            b.Push(new Vector3(0f, 1.48f, 0.73f), Quaternion.Euler(-25f, 0f, 0f), Vector3.one);
            b.ShadedBox(Vector3.up * 0.25f, new Vector3(0.4f, 0.95f, 0.55f), Pal.FurDark, rng, 0.42f, 0.58f);
            b.Pop();
            b.ShadedBox(new Vector3(0f, 2f, 1.02f), new Vector3(0.36f, 0.42f, 0.75f), Pal.FurDark, rng, 0.45f, 0.6f);
            foreach (float side in new[] { -1f, 1f })
            {
                b.SetColor(Pal.FurDark, 0.38f);
                b.Prism(new Vector3(side * 0.13f, 2.18f, 0.82f), 0.09f, 0.025f, 0.43f, 5);
                b.SetColor(Pal.Black, 0.1f);
                b.Box(new Vector3(side * 0.185f, 2.08f, 1.08f), new Vector3(0.02f, 0.045f, 0.06f));
                b.SetColor(Pal.Leather, 0.4f);
                b.Box(new Vector3(side * 0.41f, 1.12f, -0.1f), new Vector3(0.24f, 0.65f, 0.86f));
            }
            b.SetColor(Pal.ClothRed, 0.35f);
            b.Box(new Vector3(0f, 1.59f, -0.12f), new Vector3(0.85f, 0.08f, 0.8f));
            Beam(b, new Vector3(0f, 1.4f, -0.9f), new Vector3(0f, 0.72f, -1.15f), 0.12f, Pal.FurDark);
            foreach (float y in new[] { 1f, 1.35f })
            {
                b.SetColor(Pal.Leather, 0.25f);
                b.Box(new Vector3(0f, y, 0.42f), new Vector3(0.85f, 0.06f, 0.08f));
            }
        }

        public static void LogPile(MeshBuilder b, System.Random rng, float length, int rows = 3)
        {
            for (int row = 0; row < rows; row++)
            for (int col = 0; col < rows - row; col++)
            {
                Vector3 at = new Vector3((col - (rows - row - 1) * 0.5f) * 0.35f, 0.16f + row * 0.29f, 0f);
                b.Push(at, Quaternion.Euler(90f, 0f, 0f), Vector3.one);
                b.SetColor(Pal.Bark, 0.33f);
                b.Prism(Vector3.down * length * 0.5f, 0.17f, 0.16f, length, 7);
                b.SetColor(Pal.Wood, MeshBuilder.Lerp(rng, 0.5f, 0.7f));
                b.Prism(Vector3.down * (length * 0.5f + 0.015f), 0.13f, 0.13f, 0.025f, 7);
                b.Pop();
            }
        }

        public static void Books(MeshBuilder b, System.Random rng, Vector3 at, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float height = MeshBuilder.Lerp(rng, 0.24f, 0.4f);
                b.SetColor(i % 3 == 0 ? Pal.ClothRed : i % 3 == 1 ? Pal.Leather : Pal.ClothDark, 0.35f + i % 4 * 0.07f);
                b.Box(at + new Vector3(i * 0.085f, height * 0.5f, 0f), new Vector3(0.075f, height, 0.22f));
                b.SetColor(Pal.Brass, 0.42f);
                foreach (float y in new[] { 0.06f, height - 0.06f })
                    b.Box(at + new Vector3(i * 0.085f, y, -0.115f), new Vector3(0.06f, 0.025f, 0.015f));
            }
        }
    }
}
