using UnityEngine;

namespace AshenRealm.EditorTools
{
    /// <summary>Small architectural details shared by the town's curated compositions, all using its existing material.</summary>
    internal static class TownDetailMeshes
    {
        public static void Beam(MeshBuilder b, Vector3 from, Vector3 to, float width, Pal family = Pal.WoodDark, float shade = 0.4f)
        {
            Vector3 direction = to - from;
            b.SetColor(family, shade);
            b.Push((from + to) * 0.5f, Quaternion.FromToRotation(Vector3.up, direction.normalized), Vector3.one);
            b.Box(Vector3.zero, new Vector3(width, direction.magnitude, width));
            b.Pop();
        }

        public static void Ring(MeshBuilder b, Vector3 centre, float radius, float width, Pal family, float shade, int sides = 16)
        {
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides, next = (i + 1) * Mathf.PI * 2f / sides;
                Beam(b, centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius,
                    centre + new Vector3(Mathf.Cos(next), 0f, Mathf.Sin(next)) * radius, width, family, shade);
            }
        }

        public static void Window(MeshBuilder b, Vector3 centre, float yaw, bool shutters)
        {
            b.Push(centre, Quaternion.Euler(0f, yaw, 0f), Vector3.one);
            b.SetColor(Pal.Black, 0.15f);
            b.Box(Vector3.zero, new Vector3(0.98f, 0.88f, 0.1f));
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            {
                b.SetColor(Pal.Lantern, y > 0 ? 0.4f : 0.2f);
                b.Current.Emission = y > 0 ? 0.2f : 0.1f;
                b.Box(new Vector3(x * 0.185f, y * 0.15f, -0.07f), new Vector3(0.29f, 0.23f, 0.035f));
            }
            b.SetColor(Pal.WoodDark, 0.4f);
            foreach (float x in new[] { -0.46f, 0f, 0.46f }) b.Box(new Vector3(x, 0f, -0.11f), new Vector3(0.07f, 0.89f, 0.08f));
            foreach (float y in new[] { -0.42f, 0f, 0.42f }) b.Box(new Vector3(0f, y, -0.11f), new Vector3(0.98f, 0.07f, 0.08f));
            b.SetColor(Pal.Stone, 0.52f);
            b.Box(new Vector3(0f, -0.49f, -0.14f), new Vector3(1.22f, 0.14f, 0.38f));
            b.Box(new Vector3(0f, 0.52f, -0.06f), new Vector3(1.15f, 0.14f, 0.24f));
            if (shutters)
                foreach (float side in new[] { -1f, 1f })
                {
                    b.Push(new Vector3(side * 0.63f, 0f, -0.07f), Quaternion.Euler(0f, side * 22f, 0f), Vector3.one);
                    b.SetColor(Pal.PaintBlue, 0.28f);
                    b.Box(Vector3.zero, new Vector3(0.28f, 0.83f, 0.07f));
                    b.SetColor(Pal.SteelDark, 0.28f);
                    foreach (float y in new[] { -0.25f, 0.25f }) b.Box(new Vector3(0f, y, -0.05f), new Vector3(0.3f, 0.045f, 0.035f));
                    b.Pop();
                }
            b.Pop();
        }

        public static void Awning(MeshBuilder b, Vector3 centre, float width, Pal cloth)
        {
            const int strips = 8;
            for (int i = 0; i < strips; i++)
            {
                float x = ((i + 0.5f) / strips - 0.5f) * width;
                b.SetColor(i % 2 == 0 ? cloth : Pal.ClothDark, i % 2 == 0 ? 0.43f : 0.35f);
                b.Push(centre + new Vector3(x, -0.17f, -0.58f), Quaternion.Euler(-16f, 0f, 0f), Vector3.one);
                b.Box(Vector3.zero, new Vector3(width / strips, 0.035f, 1.26f));
                b.Pop();
                b.SetColor(i % 2 == 0 ? cloth : Pal.ClothDark, 0.38f);
                b.Triangle(centre + new Vector3(x - width / strips * 0.5f, -0.32f, -1.17f),
                    centre + new Vector3(x + width / strips * 0.5f, -0.32f, -1.17f), centre + new Vector3(x, -0.56f, -1.17f), Vector3.back);
            }
            foreach (float side in new[] { -1f, 1f })
                Beam(b, centre + new Vector3(side * (width * 0.5f - 0.05f), -0.68f, -0.05f),
                    centre + new Vector3(side * (width * 0.5f - 0.05f), -0.3f, -1.1f), 0.075f);
        }

        public static void Pennants(MeshBuilder b, Vector3 from, Vector3 to, Pal first, int count)
        {
            Vector3 Point(float t) => Vector3.Lerp(from, to, t) + Vector3.down * (Mathf.Sin(t * Mathf.PI) * 0.7f);
            for (int i = 0; i < count; i++)
            {
                float t = (i + 0.5f) / count;
                Vector3 at = Point(t);
                Beam(b, Point((float)i / count), Point((float)(i + 1) / count), 0.025f, Pal.Thatch, 0.35f);
                Vector3 along = (to - from).normalized * 0.25f;
                b.SetColor(i % 3 == 0 ? Pal.ClothDark : first, 0.38f + (i % 2) * 0.08f);
                b.DoubleSided(at - along, at + along, at + Vector3.down * 0.65f, Vector3.back);
            }
        }

        public static void Runner(MeshBuilder b, Vector3 centre, float width, float length, Pal cloth)
        {
            b.SetColor(Pal.ClothDark, 0.36f);
            b.Box(centre, new Vector3(width, 0.018f, length));
            b.SetColor(cloth, 0.38f);
            b.Box(centre + Vector3.up * 0.012f, new Vector3(width - 0.2f, 0.012f, length - 0.25f));
            b.SetColor(Pal.Brass, 0.33f);
            foreach (float side in new[] { -1f, 1f })
                b.Box(centre + new Vector3(side * (width * 0.5f - 0.13f), 0.024f, 0f), new Vector3(0.055f, 0.012f, length - 0.25f));
            for (float z = -length * 0.5f + 0.5f; z < length * 0.5f; z += 0.85f)
            {
                b.SetColor(Pal.Thatch, 0.45f);
                b.Push(centre + new Vector3(0f, 0.03f, z), Quaternion.Euler(0f, 45f, 0f), Vector3.one);
                b.Box(Vector3.zero, new Vector3(0.16f, 0.012f, 0.16f));
                b.Pop();
            }
        }

        public static void CandleWheel(MeshBuilder b, Vector3 centre, float radius, float top)
        {
            Ring(b, centre, radius, 0.085f, Pal.SteelDark, 0.38f);
            Ring(b, centre + Vector3.up * 0.1f, radius * 0.94f, 0.035f, Pal.Brass, 0.42f);
            foreach (float a in new[] { 0f, 120f, 240f })
            {
                Vector3 rim = centre + Quaternion.Euler(0f, a, 0f) * Vector3.forward * radius;
                Beam(b, rim, new Vector3(centre.x, top, centre.z), 0.035f, Pal.SteelDark, 0.35f);
            }
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                Vector3 p = centre + new Vector3(Mathf.Cos(a), 0.05f, Mathf.Sin(a)) * radius;
                b.SetColor(Pal.Brass, 0.44f);
                b.Prism(p, 0.12f, 0.1f, 0.035f, 8);
                b.SetColor(Pal.Plaster, 0.62f);
                b.Prism(p + Vector3.up * 0.035f, 0.05f, 0.045f, 0.2f + (i % 3) * 0.025f, 6);
                b.SetColor(Pal.Lantern, 0.7f);
                b.Prism(p + Vector3.up * (0.24f + (i % 3) * 0.025f), 0.035f, 0f, 0.13f, 5);
            }
        }

        public static void RoseWindow(MeshBuilder b, Vector3 centre, Quaternion rotation, float radius)
        {
            b.Push(centre, rotation, Vector3.one);
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 0.125f, c = (i + 1) * Mathf.PI * 0.125f;
                Vector3 start = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
                Vector3 end = new Vector3(Mathf.Cos(c), Mathf.Sin(c), 0f) * radius;
                b.SetColor(i % 4 == 0 ? Pal.Lantern : i % 2 == 0 ? Pal.PaintBlue : Pal.FlowerViolet, 0.44f);
                b.Current.Emission = 0.2f;
                b.DoubleSided(Vector3.zero, start, end, Vector3.back);
                Beam(b, start, end, 0.14f, Pal.Stone, 0.52f);
                if (i % 2 == 0) Beam(b, start * 0.12f, start, 0.055f, Pal.SteelDark, 0.25f);
            }
            b.SetColor(Pal.Brass, 0.5f);
            b.Push(new Vector3(0f, 0f, -0.07f), Quaternion.Euler(90f, 0f, 0f), Vector3.one);
            b.Prism(Vector3.zero, radius * 0.18f, radius * 0.18f, 0.1f, 8);
            b.Pop();
            b.Pop();
        }

        public static void Planter(MeshBuilder b, Vector3 centre, System.Random rng, float length = 2.5f)
        {
            b.ShadedBox(centre + Vector3.up * 0.22f, new Vector3(length, 0.44f, 0.8f), Pal.StoneMoss, rng, 0.4f, 0.53f);
            b.SetColor(Pal.DirtDark, 0.22f);
            b.Box(centre + Vector3.up * 0.45f, new Vector3(length - 0.2f, 0.035f, 0.6f));
            for (int i = 0; i < 15; i++)
            {
                Vector3 p = centre + new Vector3(MeshBuilder.Lerp(rng, -length * 0.42f, length * 0.42f), 0.46f, MeshBuilder.Lerp(rng, -0.24f, 0.24f));
                float height = MeshBuilder.Lerp(rng, 0.2f, 0.5f);
                b.SetColor(Pal.LeafGreen, 0.35f);
                b.Prism(p, 0.025f, 0.015f, height, 4);
                b.SetColor(i % 3 == 0 ? Pal.FlowerViolet : i % 3 == 1 ? Pal.FlowerWhite : Pal.FlowerYellow, 0.55f);
                b.Prism(p + Vector3.up * height, 0.09f, 0.04f, 0.07f, 5);
            }
        }
    }
}
