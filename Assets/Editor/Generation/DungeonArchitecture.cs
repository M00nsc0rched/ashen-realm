using UnityEngine;

namespace AshenRealm.EditorTools
{
    /// <summary>Silhouettes and traces of former lives. Everything joins the dungeon's static, noncolliding chunks.</summary>
    internal static class DungeonArchitecture
    {
        public static Vector3 Focus(DungeonDecor decor, Vector3 min, Vector3 max, bool hall = false) =>
            new Vector3((hall && decor == DungeonDecor.Marrow ? Mathf.Floor((min.x + max.x) * 0.5f / 2.5f) * 2.5f : (min.x + max.x) * 0.5f) +
                (decor == DungeonDecor.Harbor || (hall && decor != DungeonDecor.Marrow) ? -(max.x - min.x) * 0.26f : 0f),
                decor == DungeonDecor.Fungal ? 2.8f : 2.6f, max.z - (decor == DungeonDecor.Harbor || hall ? 5f : 2.2f));

        public static Color GlowColor(DungeonDecor decor)
        {
            switch (decor)
            {
                case DungeonDecor.Fungal: return new Color(0.25f, 0.95f, 0.62f);
                case DungeonDecor.Harbor: return new Color(0.3f, 0.68f, 0.95f);
                case DungeonDecor.Ember: return new Color(1f, 0.35f, 0.09f);
                case DungeonDecor.Marrow: return new Color(0.76f, 0.62f, 1f);
                default: return new Color(1f, 0.62f, 0.27f);
            }
        }

        public static void BuildRoom(MeshBuilder b, DungeonDecor decor, Vector3 min, Vector3 max, float ceiling, int role,
            System.Random rng, System.Func<Vector3, bool> isSolid)
        {
            Vector3 centre = (min + max) * 0.5f;
            float width = max.x - min.x, depth = max.z - min.z;
            Pal accent = decor == DungeonDecor.Fungal ? Pal.FungalGlow : decor == DungeonDecor.Harbor ? Pal.Brass :
                decor == DungeonDecor.Ember ? Pal.Lava : decor == DungeonDecor.Marrow ? Pal.Bone : Pal.Brass;
            FloorInlay(b, centre, width, depth, decor, accent, role);

            if (decor == DungeonDecor.Fungal) Cave(b, min, max, ceiling, rng, isSolid);
            else if (decor == DungeonDecor.Harbor) Docks(b, min, max, ceiling, rng);
            else
            {
                Pal stone = decor == DungeonDecor.Marrow ? Pal.Bone : decor == DungeonDecor.Ember ? Pal.Basalt : Pal.Stone;
                int bays = Mathf.Max(2, Mathf.RoundToInt(depth / 6f));
                for (int i = 0; i <= bays; i++)
                {
                    float z = Mathf.Lerp(min.z + 1.2f, max.z - 1.2f, i / (float)bays);
                    Arch(b, new Vector3(centre.x, 0f, z), width * 0.5f - 0.7f,
                        decor == DungeonDecor.Marrow ? 2.9f : 3.2f, ceiling - 3.6f, stone, decor == DungeonDecor.Marrow ? 0.24f : 0.33f);
                }
                for (float z = min.z + 3.5f; z < max.z - 2f; z += 4f)
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 p = new Vector3(side < 0f ? min.x + 0.22f : max.x - 0.22f, 0f, z);
                    if (!isSolid(p + new Vector3(side, 0f, 0f)) || !isSolid(p + new Vector3(side, 0f, -1.4f)) ||
                        !isSolid(p + new Vector3(side, 0f, 1.4f))) continue;
                    if (decor == DungeonDecor.Catacombs) TombNiche(b, p, side, rng);
                    else if (decor == DungeonDecor.Marrow) Ossuary(b, p, side, rng);
                    else BasaltFin(b, p, side, ceiling, rng);
                }
            }

            if (role == 0) Entrance(b, decor, centre.x, min.z + 1.3f, rng);
            if (role == 1 || role == 2)
            {
                Vector3 focus = Focus(decor, min, max, role == 1);
                focus.y = 0f;
                Landmark(b, decor, focus, ceiling, role == 1, rng);
            }
            else if (role == 3 || role == 4)
                Story(b, decor, min, max, role, rng);
        }

        public static void Inscription(MeshBuilder b, Vector3 p, DungeonDecor decor)
        {
            b.SetColor(decor == DungeonDecor.Marrow ? Pal.BoneWall : Pal.Stone, 0.53f);
            b.Box(p + Vector3.up * 0.1f, new Vector3(0.9f, 0.2f, 0.7f));
            b.Box(p + Vector3.up * 0.7f, new Vector3(0.66f, 1.2f, 0.25f));
            b.SetColor(Pal.Black, 0.16f);
            b.Box(p + new Vector3(0f, 0.95f, -0.14f), new Vector3(0.53f, 0.5f, 0.03f));
            b.SetColor(Pal.Brass, 0.58f);
            for (int i = 0; i < 3; i++)
                b.Box(p + new Vector3(0f, 1.1f - i * 0.13f, -0.165f), new Vector3(i == 2 ? 0.22f : 0.38f, 0.035f, 0.02f));
        }

        static void Beam(MeshBuilder b, Vector3 a, Vector3 c, float width, float depth, Pal family, float shade = 0.5f)
        {
            Vector3 span = c - a;
            if (span.sqrMagnitude < 0.0001f) return;
            b.Push((a + c) * 0.5f, Quaternion.FromToRotation(Vector3.up, span.normalized), Vector3.one);
            b.SetColor(family, shade);
            b.Box(Vector3.zero, new Vector3(width, span.magnitude, depth));
            b.Pop();
        }

        static void Ring(MeshBuilder b, Vector3 centre, float radius, float thickness, Pal family, float shade, int sides = 32)
        {
            b.SetColor(family, shade);
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides, c = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 u = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 v = new Vector3(Mathf.Cos(c), 0f, Mathf.Sin(c));
                b.QuadOutward(centre + u * radius, centre + v * radius, centre + v * (radius - thickness),
                    centre + u * (radius - thickness), centre - Vector3.up);
            }
        }

        static void FloorInlay(MeshBuilder b, Vector3 c, float width, float depth, DungeonDecor decor, Pal accent, int role)
        {
            if (decor == DungeonDecor.Fungal)
            {
                // Broken branching hyphae grow from the edges; there is no built or painted processional aisle here.
                var growth = new System.Random(Mathf.RoundToInt(c.x * 31f + c.z * 71f) + role * 13);
                for (int branch = 0; branch < 7; branch++)
                {
                    float side = branch % 2 == 0 ? -1f : 1f;
                    Vector3 p = c + new Vector3(side * width * (0.22f + (float)growth.NextDouble() * 0.14f), 0.025f,
                        ((float)growth.NextDouble() - 0.5f) * depth * 0.7f);
                    for (int segment = 0; segment < 4; segment++)
                    {
                        Vector3 q = p + new Vector3(((float)growth.NextDouble() - 0.5f) * 1.6f, 0f,
                            0.65f + (float)growth.NextDouble() * 0.8f);
                        b.SetColor(Pal.FungalGlow, 0.27f);
                        b.Current.Emission = 0.035f;
                        FloorStroke(b, p, q, segment == 0 ? 0.08f : 0.045f);
                        if (segment % 2 == 0)
                        {
                            b.SetColor(Pal.FungalGlow, 0.3f);
                            b.Current.Emission = 0.055f;
                            b.Prism(q, 0.1f, 0.08f, 0.012f, 6);
                            FloorStroke(b, q, q + new Vector3(-side * 0.8f, 0f, 0.35f), 0.035f);
                        }
                        p = q;
                    }
                }
                return;
            }
            if (decor == DungeonDecor.Harbor)
            {
                for (float z = c.z - depth * 0.5f + 2f; z < c.z + depth * 0.5f - 1f; z += 2.8f)
                {
                    b.SetColor(Pal.SteelDark, 0.3f);
                    b.Box(new Vector3(c.x, 0.06f, z), new Vector3(3f, 0.012f, 0.09f));
                    foreach (float side in new[] { -1f, 1f })
                    {
                        b.SetColor(Pal.Brass, 0.35f);
                        b.Box(new Vector3(c.x + side * 1.3f, 0.07f, z), new Vector3(0.09f, 0.015f, 0.09f));
                    }
                }
                return;
            }
            if (decor == DungeonDecor.Ember)
            {
                // Cooled slag has split beside the haul route; only isolated hearts are still warm.
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 p = c + new Vector3(side * width * 0.29f, 0.024f, -depth * 0.36f);
                    int segments = Mathf.Max(4, Mathf.FloorToInt(depth / 2.4f));
                    for (int i = 0; i < segments; i++)
                    {
                        Vector3 q = p + new Vector3(Mathf.Sin(i * 2.1f + side) * 0.9f, 0f, 1.7f);
                        b.SetColor(Pal.RockDark, 0.2f);
                        FloorStroke(b, p, q, 0.23f);
                        if (i % 3 == 1)
                        {
                            b.SetColor(Pal.Lava, 0.32f);
                            b.Current.Emission = 0.035f;
                            Vector3 start = Vector3.Lerp(p, q, 0.25f) + Vector3.up * 0.012f;
                            Vector3 end = Vector3.Lerp(p, q, 0.64f) + Vector3.up * 0.012f;
                            FloorStroke(b, start, end, 0.055f);
                        }
                        p = q;
                    }
                }
                return;
            }
            if (decor == DungeonDecor.Marrow)
            {
                if (role != 1 && role != 2) return;
                float boneRadius = Mathf.Min(width, depth) * 0.27f;
                for (int i = 0; i < 18; i++)
                {
                    if (i % 4 == 1 || i % 5 == 2) continue;
                    float angle = i * Mathf.PI * 2f / 18f;
                    float length = i % 3 == 0 ? 0.75f : 0.42f;
                    Vector3 p = c + new Vector3(Mathf.Cos(angle) * boneRadius, 0.025f, Mathf.Sin(angle) * boneRadius);
                    Vector3 along = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                    b.SetColor(Pal.Bone, 0.29f);
                    FloorStroke(b, p - along * length * 0.5f, p + along * length * 0.5f, 0.12f);
                    if (i % 3 != 0) continue;
                    Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    FloorStroke(b, p + direction * 0.25f, p + direction * 0.85f, 0.07f);
                }
                return;
            }

            // The central aisle is readable from eye height and remains entirely free of raised scenery.
            for (float z = c.z - depth * 0.5f + 1.5f; z < c.z + depth * 0.5f - 1.5f; z += 2.2f)
            foreach (float side in new[] { -1f, 1f })
            {
                b.SetColor(accent, decor == DungeonDecor.Ember ? 0.65f : 0.4f);
                b.Box(new Vector3(c.x + side * 1.8f, 0.018f, z), new Vector3(0.1f, 0.015f, 1.15f));
                if (role < 3)
                    b.Box(new Vector3(c.x + side * 2.05f, 0.02f, z), new Vector3(0.3f, 0.015f, 0.1f));
            }
            if (role != 1 && role != 2) return;
            float radius = Mathf.Min(width, depth) * (role == 1 ? 0.31f : 0.26f);
            Ring(b, c + Vector3.up * 0.022f, radius, 0.14f, accent, 0.52f);
            Ring(b, c + Vector3.up * 0.024f, radius - 0.48f, 0.045f, accent, 0.35f);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                b.Push(c + new Vector3(Mathf.Cos(a) * (radius + 0.24f), 0.03f, Mathf.Sin(a) * (radius + 0.24f)),
                    Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f), Vector3.one);
                b.SetColor(accent, 0.46f);
                b.Box(Vector3.zero, new Vector3(0.5f, 0.015f, 0.1f));
                b.Box(new Vector3(0.12f, 0f, 0.13f), new Vector3(0.1f, 0.015f, 0.3f));
                b.Pop();
            }
        }

        /// <summary>A shallow line laid on the floor, regardless of its horizontal direction.</summary>
        static void FloorStroke(MeshBuilder b, Vector3 from, Vector3 to, float width)
        {
            Vector3 span = to - from;
            if (span.sqrMagnitude < 0.0001f) return;
            b.Push((from + to) * 0.5f, Quaternion.LookRotation(span, Vector3.up), Vector3.one);
            b.Box(Vector3.zero, new Vector3(width, 0.012f, span.magnitude));
            b.Pop();
        }

        static void Arch(MeshBuilder b, Vector3 at, float halfWidth, float spring, float rise, Pal family, float thickness)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                b.SetColor(family, 0.43f);
                b.Box(at + new Vector3(side * halfWidth, spring * 0.5f, 0f), new Vector3(thickness * 1.65f, spring, thickness * 1.8f));
                b.SetColor(family, 0.59f);
                b.Box(at + new Vector3(side * halfWidth, 0.22f, 0f), new Vector3(thickness * 2.4f, 0.44f, thickness * 2.5f));
                b.Box(at + new Vector3(side * halfWidth, spring - 0.12f, 0f), new Vector3(thickness * 2.1f, 0.24f, thickness * 2.2f));
            }
            const int segments = 14;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI / segments, c = (i + 1) * Mathf.PI / segments;
                Vector3 from = at + new Vector3(Mathf.Cos(a) * halfWidth, spring + Mathf.Sin(a) * rise, 0f);
                Vector3 to = at + new Vector3(Mathf.Cos(c) * halfWidth, spring + Mathf.Sin(c) * rise, 0f);
                Beam(b, from, to, thickness, thickness * 1.2f, family, i % 2 == 0 ? 0.55f : 0.42f);
            }
        }

        static void TombNiche(MeshBuilder b, Vector3 p, float side, System.Random rng)
        {
            b.Push(p, Quaternion.Euler(0f, side < 0f ? 90f : -90f, 0f), Vector3.one);
            b.SetColor(Pal.Black, 0.12f);
            b.Box(new Vector3(0f, 2.2f, 0.04f), new Vector3(2.2f, 2.4f, 0.08f));
            foreach (float x in new[] { -1.15f, 1.15f })
            {
                b.SetColor(Pal.Stone, 0.59f);
                b.Box(new Vector3(x, 2.1f, 0.2f), new Vector3(0.22f, 2.7f, 0.36f));
            }
            b.Box(new Vector3(0f, 3.48f, 0.2f), new Vector3(2.5f, 0.24f, 0.36f));
            b.SetColor(Pal.Bone, 0.48f);
            b.Box(new Vector3(0f, 1.33f, 0.3f), new Vector3(1.8f, 0.3f, 0.42f));
            b.SetColor(Pal.Brass, 0.52f);
            b.Box(new Vector3(0f, 2.3f, 0.1f), new Vector3(0.12f, 0.72f, 0.08f));
            b.Box(new Vector3(0f, 2.4f, 0.1f), new Vector3(0.55f, 0.12f, 0.08f));
            b.Push(new Vector3(0.9f, 0f, 0.65f), Quaternion.identity, Vector3.one * 0.65f);
            PropMeshes.Candles(b, rng);
            b.Pop();
            b.Pop();
        }

        static void Ossuary(MeshBuilder b, Vector3 p, float side, System.Random rng)
        {
            b.Push(p, Quaternion.Euler(0f, side < 0f ? 90f : -90f, 0f), Vector3.one);
            b.SetColor(Pal.Shadow, 0.2f);
            b.Box(new Vector3(0f, 2.3f, 0.03f), new Vector3(2.3f, 3.8f, 0.08f));
            for (int row = 0; row < 4; row++)
            {
                b.SetColor(Pal.BoneWall, 0.58f);
                b.Box(new Vector3(0f, 0.65f + row * 0.9f, 0.25f), new Vector3(2.5f, 0.12f, 0.45f));
                for (int i = 0; i < 5; i++) Skull(b, new Vector3((i - 2) * 0.43f, 0.82f + row * 0.9f, 0.3f), 0.22f);
            }
            b.Pop();
        }

        static void Skull(MeshBuilder b, Vector3 p, float size)
        {
            b.SetColor(Pal.Bone, 0.58f);
            b.Box(p, new Vector3(size, size, size * 0.85f));
            b.Box(p + new Vector3(0f, -size * 0.5f, size * 0.1f), new Vector3(size * 0.72f, size * 0.22f, size * 0.75f));
            b.SetColor(Pal.Shadow, 0.07f);
            foreach (float side in new[] { -1f, 1f })
                b.Box(p + new Vector3(side * size * 0.25f, size * 0.08f, size * 0.43f), new Vector3(size * 0.22f, size * 0.25f, 0.016f));
        }

        static void Cave(MeshBuilder b, Vector3 min, Vector3 max, float ceiling, System.Random rng, System.Func<Vector3, bool> isSolid)
        {
            for (float z = min.z + 2.5f; z < max.z - 1f; z += 4.3f)
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 p = new Vector3(side < 0f ? min.x + 0.8f : max.x - 0.8f, 0f, z);
                if (!isSolid(p + new Vector3(side * 1.2f, 0f, 0f)) || !isSolid(p + new Vector3(side * 1.2f, 0f, 1.5f))) continue;
                b.Rock(p + Vector3.up * 0.8f, new Vector3(2.6f, 2.8f, 3.3f), rng, Pal.FungalRock, 0.35f, 0.57f);
                b.Push(p, Quaternion.identity, Vector3.one * 1.65f);
                PropMeshes.Mushroom(b, rng, Vector3.zero, 1.6f);
                b.Pop();
                Beam(b, p + new Vector3(side * -0.6f, 1f, 0f), p + new Vector3(side * -1.7f, ceiling - 0.15f, 1f),
                    0.18f, 0.22f, Pal.Bark, 0.33f);
                b.Push(p + new Vector3(-side * 1.4f, ceiling, 0f), Quaternion.Euler(180f, 0f, 0f), Vector3.one);
                PropMeshes.Stalagmite(b, rng, Pal.FungalRock, 1.8f);
                b.Pop();
                b.SetColor(Pal.FungalGlow, 0.52f);
                b.Current.Emission = 0.7f;
                b.Prism(p + new Vector3(-side * 1.4f, ceiling - 2f, 0f), 0.08f, 0.13f, 0.25f, 6);
            }
            Vector3 c = (min + max) * 0.5f;
            foreach (float side in new[] { -1f, 1f })
            {
                b.Push(new Vector3(c.x + side * (max.x - min.x) * 0.31f, 0f, max.z - 4.8f), Quaternion.identity, Vector3.one);
                PropMeshes.Mushroom(b, rng, Vector3.zero, 5.2f);
                b.Pop();
            }
        }

        static void Docks(MeshBuilder b, Vector3 min, Vector3 max, float ceiling, System.Random rng)
        {
            float centre = (min.x + max.x) * 0.5f;
            foreach (float side in new[] { -1f, 1f })
            {
                float x = centre + side * ((max.x - min.x) * 0.5f - 2.2f);
                b.SetColor(Pal.Water, 0.38f);
                b.Box(new Vector3(x, 0.015f, (min.z + max.z) * 0.5f), new Vector3(3.2f, 0.018f, max.z - min.z - 1.5f));
                for (float z = min.z + 2f; z < max.z - 1f; z += 3.8f)
                {
                    b.Push(new Vector3(x - side * 1.5f, 0f, z), Quaternion.identity, Vector3.one);
                    PropMeshes.RottenPost(b, rng);
                    b.Pop();
                    Beam(b, new Vector3(x, 0f, z), new Vector3(x + side * 1.1f, ceiling - 0.2f, z), 0.25f, 0.26f, Pal.WoodDark, 0.38f);
                    Beam(b, new Vector3(min.x + 0.4f, ceiling - 0.5f, z), new Vector3(max.x - 0.4f, ceiling - 0.5f, z),
                        0.25f, 0.3f, Pal.WoodDark, 0.35f);
                    b.SetColor(Pal.FungalGlow, 0.24f);
                    b.Box(new Vector3(x, 0.035f, z), new Vector3(1.8f, 0.015f, 0.025f));
                }
            }
            for (float z = min.z + 1f; z < max.z - 1f; z += 0.7f)
            {
                b.SetColor(Pal.Wood, 0.44f + (Mathf.FloorToInt(z) % 3) * 0.03f);
                b.Box(new Vector3(centre, 0.035f, z), new Vector3(3f, 0.04f, 0.58f));
            }
        }

        static void BasaltFin(MeshBuilder b, Vector3 p, float side, float ceiling, System.Random rng)
        {
            b.SetColor(Pal.Basalt, 0.3f);
            b.Prism(p + new Vector3(-side * 0.1f, 0f, 0f), 0.62f, 0.4f, ceiling - 0.2f, 6);
            b.SetColor(Pal.Lava, 0.58f);
            b.Current.Emission = 0.75f;
            b.Box(p + new Vector3(-side * 0.65f, 1.6f, 0f), new Vector3(0.05f, 2f, 0.11f));
        }

        static void Entrance(MeshBuilder b, DungeonDecor decor, float x, float z, System.Random rng)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 p = new Vector3(x + side * 3.6f, 0f, z + 2.6f);
                b.Push(p, Quaternion.identity, Vector3.one);
                if (decor == DungeonDecor.Fungal) PropMeshes.Mushroom(b, rng, Vector3.zero, 2.6f);
                else if (decor == DungeonDecor.Harbor) { PropMeshes.Barrel(b, rng); PropMeshes.RottenPost(b, rng); }
                else if (decor == DungeonDecor.Ember) PropMeshes.Brazier(b, rng);
                else PropMeshes.Candles(b, rng);
                b.Pop();
            }
        }

        static void Landmark(MeshBuilder b, DungeonDecor decor, Vector3 p, float ceiling, bool hall, System.Random rng)
        {
            b.Push(p, Quaternion.identity, Vector3.one);
            switch (decor)
            {
                case DungeonDecor.Catacombs: Memorial(b, hall, rng); break;
                case DungeonDecor.Fungal: SporeHeart(b, ceiling, rng); break;
                case DungeonDecor.Harbor: Barge(b, rng); break;
                case DungeonDecor.Ember: Furnace(b, ceiling, hall, rng); break;
                default: BoneAltar(b, ceiling, hall, rng); break;
            }
            b.Pop();
        }

        static void Memorial(MeshBuilder b, bool hall, System.Random rng)
        {
            b.SetColor(Pal.Stone, 0.46f);
            b.Box(new Vector3(0f, 0.25f, 0f), new Vector3(6.4f, 0.5f, 2.4f));
            b.Box(new Vector3(0f, 0.62f, 0f), new Vector3(5.5f, 0.24f, 1.8f));
            b.SetColor(Pal.RockDark, 0.37f);
            b.Box(new Vector3(0f, 3.3f, 0.55f), new Vector3(3.6f, 5.4f, 0.5f));
            b.SetColor(Pal.Brass, 0.5f);
            b.Box(new Vector3(0f, 3.55f, 0.22f), new Vector3(0.2f, 3.7f, 0.12f));
            b.Box(new Vector3(0f, 4.15f, 0.22f), new Vector3(2.4f, 0.2f, 0.12f));
            for (int row = 0; row < 4; row++)
            for (int col = 0; col < 5; col++)
            {
                b.SetColor(Pal.Bone, 0.43f);
                b.Box(new Vector3((col - 2) * 0.5f, 1.1f + row * 0.32f, 0.24f), new Vector3(0.3f, 0.055f, 0.045f));
            }
            foreach (float side in new[] { -1f, 1f })
            {
                b.Push(new Vector3(side * 2.4f, 0.76f, 0f), Quaternion.identity, Vector3.one * 1.6f);
                PropMeshes.Candles(b, rng);
                b.Pop();
                b.SetColor(Pal.ClothRed, 0.4f);
                b.Box(new Vector3(side * 3.6f, 3.9f, 0.4f), new Vector3(1.15f, 3.6f, 0.045f));
                b.SetColor(Pal.Brass, 0.42f);
                b.Box(new Vector3(side * 3.6f, 4.2f, 0.35f), new Vector3(0.12f, 1.1f, 0.04f));
            }
        }

        static void SporeHeart(MeshBuilder b, float ceiling, System.Random rng)
        {
            b.SetColor(Pal.Slime, 0.4f);
            b.Prism(new Vector3(0f, 0.02f, -0.7f), 3f, 3f, 0.025f, 18);
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                Vector3 root = new Vector3(Mathf.Cos(a) * 1.3f, 0f, Mathf.Sin(a) * 1.1f);
                Beam(b, root, new Vector3(Mathf.Cos(a) * 0.5f, ceiling - 1.1f, Mathf.Sin(a) * 0.4f),
                    0.24f, 0.28f, Pal.Bark, 0.42f);
            }
            b.SetColor(Pal.FungalGlow, 0.58f);
            b.Current.Emission = 0.7f;
            b.Prism(new Vector3(0f, 1.3f, 0f), 0.7f, 0.9f, 2.6f, 8);
            b.Prism(new Vector3(0f, 3.9f, 0f), 0.9f, 0.15f, 1.4f, 8);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                PropMeshes.Mushroom(b, rng, new Vector3(Mathf.Cos(a) * 2.8f, 0f, Mathf.Sin(a) * 1.7f), 1.1f + (i % 3) * 0.3f);
            }
        }

        static void Barge(MeshBuilder b, System.Random rng)
        {
            b.SetColor(Pal.WetStone, 0.38f);
            b.Box(new Vector3(0f, 0.025f, -0.5f), new Vector3(6f, 0.02f, 11f));
            b.Push(Vector3.zero, Quaternion.Euler(0f, -8f, 5f), Vector3.one);
            b.SetColor(Pal.WoodDark, 0.4f);
            b.Box(new Vector3(0f, 0.5f, 0f), new Vector3(3.6f, 0.6f, 8.4f));
            for (int i = 0; i < 3; i++)
            {
                float length = 8.7f - i * 0.3f;
                foreach (float side in new[] { -1f, 1f })
                {
                    b.SetColor(Pal.Wood, 0.46f + i * 0.035f);
                    b.Box(new Vector3(side * (1.8f + i * 0.1f), 0.8f + i * 0.24f, 0f), new Vector3(0.16f, 0.22f, length));
                    Beam(b, new Vector3(side * 1.8f, 0.55f, length * 0.5f), new Vector3(0f, 1.65f, length * 0.5f + 0.9f),
                        0.18f, 0.2f, Pal.WoodDark, 0.44f);
                }
            }
            b.SetColor(Pal.Wood, 0.51f);
            for (int i = 0; i < 12; i++)
                b.Box(new Vector3(0f, 0.84f, -3.8f + i * 0.65f), new Vector3(3.4f, 0.07f, 0.58f));
            Beam(b, new Vector3(0f, 0.85f, -0.5f), new Vector3(0.2f, 5.7f, -0.2f), 0.22f, 0.24f, Pal.WoodDark, 0.45f);
            Beam(b, new Vector3(-2.6f, 4.6f, -0.2f), new Vector3(2.6f, 4.6f, -0.2f), 0.15f, 0.15f, Pal.WoodDark, 0.4f);
            b.SetColor(Pal.ClothDark, 0.36f);
            b.DoubleSided(new Vector3(-2.5f, 4.5f, -0.15f), new Vector3(-0.3f, 2.5f, -0.15f),
                new Vector3(-0.2f, 4.5f, -0.15f), Vector3.back);
            for (int i = 0; i < 3; i++)
            {
                b.Push(new Vector3(-0.8f + i * 0.7f, 0.9f, 1.4f + (i % 2) * 0.8f), Quaternion.Euler(0f, i * 13f, 0f), Vector3.one);
                PropMeshes.Crate(b, rng, 0.8f);
                b.Pop();
            }
            b.Pop();
        }

        static void Furnace(MeshBuilder b, float ceiling, bool hall, System.Random rng)
        {
            b.SetColor(Pal.Basalt, 0.25f);
            b.Box(new Vector3(0f, 2.5f, 0.3f), new Vector3(7.4f, 5f, 2f));
            b.SetColor(Pal.SteelDark, 0.43f);
            b.Box(new Vector3(0f, 0.35f, -0.65f), new Vector3(8.2f, 0.7f, 3f));
            b.SetColor(Pal.Black, 0.06f);
            b.Box(new Vector3(0f, 2.5f, -0.73f), new Vector3(4.4f, 3.1f, 0.1f));
            b.SetColor(Pal.Lava, 0.7f);
            b.Current.Emission = 0.9f;
            b.Box(new Vector3(0f, 2.5f, -0.8f), new Vector3(3.6f, 2.5f, 0.03f));
            for (int i = 0; i < 7; i++)
            {
                b.SetColor(Pal.SteelDark, 0.42f);
                b.Box(new Vector3(-1.8f + i * 0.6f, 2.5f, -0.88f), new Vector3(0.16f, 3f, 0.15f));
            }
            foreach (float side in new[] { -1f, 1f })
            {
                b.SetColor(Pal.Basalt, 0.42f);
                b.Prism(new Vector3(side * 3f, 0f, -0.9f), 0.6f, 0.4f, 5.8f, 6);
                Beam(b, new Vector3(side * 2.3f, 5.5f, -0.2f), new Vector3(side * 2.3f, ceiling - 0.2f, -0.2f),
                    0.09f, 0.09f, Pal.SteelDark, 0.5f);
                b.Push(new Vector3(side * 4.1f, 0f, -2f), Quaternion.identity, Vector3.one * 1.5f);
                PropMeshes.Brazier(b, rng);
                b.Pop();
            }
            for (int i = 0; i < 6; i++)
            {
                b.SetColor(Pal.Lava, 0.55f);
                b.Current.Emission = 0.65f;
                b.Box(new Vector3(-3.5f - i * 0.35f, 0.025f, -1.8f - i * 1.1f), new Vector3(0.16f, 0.02f, 1.4f));
                b.Box(new Vector3(3.5f + i * 0.3f, 0.025f, -1.6f - i * 1.3f), new Vector3(0.14f, 0.02f, 1.4f));
            }
        }

        static void BoneAltar(MeshBuilder b, float ceiling, bool hall, System.Random rng)
        {
            // The boss's existing throne remains the centre; the empty ritual halo hangs above it.
            b.SetColor(Pal.BoneWall, 0.46f);
            foreach (float side in new[] { -1f, 1f })
            {
                b.SetColor(Pal.BoneWall, 0.46f);
                b.Prism(new Vector3(side * 3.1f, 0f, 0f), 0.7f, 0.42f, ceiling - 1.2f, 8);
                for (int i = 0; i < 6; i++) Skull(b, new Vector3(side * 3.1f, 1f + i * 0.8f, -0.46f), 0.32f);
                b.Push(new Vector3(side * 2.4f, 0f, -1.4f), Quaternion.identity, Vector3.one * 2.3f);
                PropMeshes.Candles(b, rng);
                b.Pop();
            }
            Vector3 halo = new Vector3(0f, ceiling - 2.6f, 0.1f);
            for (int i = 1; i < 19; i++)
            {
                float a = i * Mathf.PI * 2f / 20f, c = (i + 1) * Mathf.PI * 2f / 20f;
                Beam(b, halo + new Vector3(Mathf.Cos(a) * 2.3f, Mathf.Sin(a) * 2.3f, 0f),
                    halo + new Vector3(Mathf.Cos(c) * 2.3f, Mathf.Sin(c) * 2.3f, 0f), 0.18f, 0.25f, Pal.Bone, 0.65f);
            }
            if (!hall)
            {
                b.SetColor(Pal.RockDark, 0.3f);
                b.Box(new Vector3(0f, 0.9f, 0f), new Vector3(3.8f, 1.8f, 1.6f));
                for (int i = 0; i < 7; i++) Skull(b, new Vector3((i - 3) * 0.46f, 2f, -0.2f), 0.32f);
            }
        }

        static void Story(MeshBuilder b, DungeonDecor decor, Vector3 min, Vector3 max, int role, System.Random rng)
        {
            Vector3 at = new Vector3(min.x + 3.7f, 0f, max.z - 3.7f);
            b.Push(at, Quaternion.Euler(0f, role == 3 ? 14f : -18f, 0f), Vector3.one);
            if (decor == DungeonDecor.Catacombs)
            {
                for (int i = 0; i < 3; i++)
                {
                    b.Push(new Vector3(i * 2.1f, 0f, 0f), Quaternion.identity, Vector3.one);
                    PropMeshes.Sarcophagus(b, rng);
                    b.Pop();
                }
                b.Push(new Vector3(1.6f, 0f, -2.4f), Quaternion.identity, Vector3.one);
                PropMeshes.Candles(b, rng);
                b.Pop();
            }
            else if (decor == DungeonDecor.Fungal)
            {
                for (int i = 0; i < 3; i++)
                {
                    b.SetColor(Pal.ClothDark, 0.38f);
                    b.Box(new Vector3(i * 1.1f, 0.11f, -1f), new Vector3(0.68f, 0.18f, 1.6f));
                    b.SetColor(Pal.Leather, 0.47f);
                    b.Box(new Vector3(i * 1.1f, 0.28f, 0.4f), new Vector3(0.58f, 0.48f, 0.4f));
                    PropMeshes.Mushroom(b, rng, new Vector3(i * 1.1f, 0.5f, 0.4f), 0.65f);
                }
                PropMeshes.BonePile(b, rng);
            }
            else if (decor == DungeonDecor.Harbor)
            {
                for (int i = 0; i < 6; i++)
                {
                    b.Push(new Vector3((i % 3) * 1.1f, (i / 3) * 0.9f, (i / 3) * 0.2f), Quaternion.Euler(0f, i * 9f, 0f), Vector3.one);
                    PropMeshes.Crate(b, rng, 0.9f);
                    b.Pop();
                }
                b.Push(new Vector3(3.8f, 0f, 0f), Quaternion.identity, Vector3.one);
                PropMeshes.Barrel(b, rng);
                b.Pop();
            }
            else if (decor == DungeonDecor.Ember)
            {
                b.SetColor(Pal.SteelDark, 0.4f);
                b.Box(new Vector3(1f, 0.75f, 0f), new Vector3(2.6f, 1.2f, 1.6f));
                foreach (float x in new[] { -0.3f, 2.3f })
                foreach (float z in new[] { -0.65f, 0.65f })
                {
                    b.Push(new Vector3(x, 0.4f, z), Quaternion.Euler(0f, 0f, 90f), Vector3.one);
                    b.Prism(Vector3.zero, 0.4f, 0.4f, 0.12f, 9);
                    b.Pop();
                }
                for (int i = 0; i < 9; i++)
                    b.Rock(new Vector3((i % 3) * 0.7f + 0.2f, 1.4f, (i / 3) * 0.4f - 0.4f), Vector3.one * 0.65f, rng, Pal.Lava, 0.4f, 0.65f);
            }
            else
            {
                for (int i = 0; i < 7; i++)
                {
                    b.SetColor(Pal.BoneWall, 0.53f);
                    b.Prism(new Vector3(i * 0.55f, 0f, 0f), 0.12f, 0.08f, 2.2f + Mathf.Sin(i * 0.5f) * 1.4f, 6);
                    Skull(b, new Vector3(i * 0.55f, 1.2f, -0.25f), 0.28f);
                }
                PropMeshes.Candles(b, rng);
            }
            b.Pop();
        }
    }
}
