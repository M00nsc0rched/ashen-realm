using UnityEngine;

namespace AshenRealm.EditorTools
{
    /// <summary>
    /// Procedural low-poly props. Each method writes into a <see cref="MeshBuilder"/> in its current space.
    /// Conventions: Y up, models stand on y = 0 unless noted, "front" faces -Z.
    /// </summary>
    internal static class PropMeshes
    {
        static float R(System.Random rng, float min, float max) => MeshBuilder.Lerp(rng, min, max);

        /// <summary>One-handed sword with its origin at the grip centre and the blade along +Y (≈1 m long).</summary>
        public static void Sword(MeshBuilder b, Pal blade = Pal.Steel, float bladeShade = 0.55f)
        {
            b.SetColor(Pal.Brass, 0.45f);
            b.Box(new Vector3(0f, -0.115f, 0f), new Vector3(0.05f, 0.05f, 0.05f));            // pommel
            b.SetColor(Pal.Leather, 0.4f);
            b.Prism(new Vector3(0f, -0.09f, 0f), 0.022f, 0.02f, 0.18f, 6);                     // grip
            b.SetColor(Pal.SteelDark, 0.6f);
            b.Box(new Vector3(0f, 0.105f, 0f), new Vector3(0.24f, 0.035f, 0.05f));             // crossguard
            b.SetColor(Pal.Brass, 0.5f);
            b.Box(new Vector3(0f, 0.105f, 0f), new Vector3(0.06f, 0.05f, 0.06f));              // guard boss

            // Diamond-section blade: a 4-sided prism flattened on Z.
            b.Push(Vector3.zero, Quaternion.identity, new Vector3(1f, 1f, 0.32f));
            b.SetColor(blade, bladeShade);
            b.Prism(new Vector3(0f, 0.12f, 0f), 0.034f, 0.026f, 0.7f, 4, false, false);
            b.SetColor(blade, bladeShade + 0.1f);
            b.Prism(new Vector3(0f, 0.82f, 0f), 0.026f, 0f, 0.14f, 4, false, false);
            b.Pop();
        }

        /// <summary>Round wooden shield in the XY plane, face toward +Z, origin at the centre of the back.</summary>
        public static void Shield(MeshBuilder b, float radius = 0.33f)
        {
            const int sides = 14;
            const float thickness = 0.04f;
            var rng = new System.Random(77);
            Vector3 center = new Vector3(0f, 0f, thickness);
            // Planked face: a triangle fan with alternating wood shades.
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 p0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * radius;
                Vector3 p1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * radius;
                b.SetColor(i % 2 == 0 ? Pal.Wood : Pal.WoodDark, R(rng, 0.42f, 0.6f));
                b.Triangle(center, p0 + Vector3.forward * thickness, p1 + Vector3.forward * thickness, b.Matrix.MultiplyVector(Vector3.forward));
                // The back faces the camera and is usually unlit, so it gets lighter shades.
                b.SetColor(i % 2 == 0 ? Pal.Wood : Pal.Bone, i % 2 == 0 ? 0.72f : 0.3f);
                b.Triangle(Vector3.zero, p0, p1, b.Matrix.MultiplyVector(Vector3.back));
                // Iron rim.
                b.SetColor(Pal.SteelDark, R(rng, 0.45f, 0.6f));
                Vector3 mid = (p0 + p1) * 0.5f;
                b.Push(mid + Vector3.forward * thickness * 0.5f, Quaternion.LookRotation(Vector3.forward, mid.normalized), Vector3.one);
                b.Box(Vector3.zero, new Vector3((p1 - p0).magnitude + 0.01f, 0.035f, thickness + 0.03f));
                b.Pop();
            }
            // Boss and studs.
            b.Push(new Vector3(0f, 0f, thickness), Quaternion.Euler(90f, 0f, 0f), Vector3.one);
            b.SetColor(Pal.SteelDark, 0.6f);
            b.Prism(Vector3.zero, 0.09f, 0.07f, 0.03f, 8, false, true);
            b.SetColor(Pal.Steel, 0.5f);
            b.Prism(new Vector3(0f, 0.03f, 0f), 0.07f, 0.02f, 0.045f, 8, false, true);
            b.Pop();
            b.SetColor(Pal.Brass, 0.55f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                b.Box(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (radius * 0.62f) + Vector3.forward * (thickness + 0.008f), Vector3.one * 0.03f);
            }
            // Leather strap on the back.
            b.SetColor(Pal.Leather, 0.45f);
            b.Box(new Vector3(0f, 0f, -0.02f), new Vector3(0.05f, 0.26f, 0.025f));
        }

        public static void Pine(MeshBuilder b, System.Random rng, float height)
        {
            float trunkRadius = height * 0.035f;
            b.SetColor(Pal.Bark, R(rng, 0.35f, 0.55f));
            b.Prism(new Vector3(0f, -0.3f, 0f), trunkRadius * 1.2f, trunkRadius * 0.7f, height * 0.32f + 0.3f, 6, false, false);

            int tiers = 4;
            float y = height * 0.2f;
            float tierHeight = (height - y) / tiers * 1.45f;
            float radius = height * 0.27f;
            for (int i = 0; i < tiers; i++)
            {
                b.SetColor(i % 2 == 0 ? Pal.PineDark : Pal.PineMid, R(rng, 0.3f, 0.6f) + i * 0.05f);
                b.Push(new Vector3(R(rng, -0.08f, 0.08f), y, R(rng, -0.08f, 0.08f)),
                    Quaternion.Euler(R(rng, -4f, 4f), R(rng, 0f, 360f), R(rng, -4f, 4f)), Vector3.one);
                b.Prism(Vector3.zero, radius, 0f, tierHeight, 7, true, false);
                b.Pop();
                y += tierHeight * 0.58f;
                radius *= 0.74f;
                tierHeight *= 0.86f;
            }
        }

        public static void DeadTree(MeshBuilder b, System.Random rng, float height)
        {
            b.SetColor(Pal.Bark, R(rng, 0.25f, 0.4f));
            b.Prism(new Vector3(0f, -0.3f, 0f), 0.22f, 0.1f, height + 0.3f, 6, false, true);
            int branches = 5;
            for (int i = 0; i < branches; i++)
            {
                float y = height * R(rng, 0.4f, 0.85f);
                b.SetColor(Pal.Bark, R(rng, 0.2f, 0.45f));
                b.Push(new Vector3(0f, y, 0f), Quaternion.Euler(0f, R(rng, 0f, 360f), R(rng, 35f, 65f)), Vector3.one);
                b.Prism(Vector3.zero, 0.07f, 0.015f, height * R(rng, 0.25f, 0.45f), 5, false, false);
                b.Pop();
            }
        }

        public static void GrassTuft(MeshBuilder b, System.Random rng, Pal family)
        {
            int blades = 3;
            for (int i = 0; i < blades; i++)
            {
                float angle = R(rng, 0f, Mathf.PI * 2f);
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var side = new Vector3(-dir.z, 0f, dir.x);
                float h = R(rng, 0.25f, 0.55f);
                float w = R(rng, 0.05f, 0.09f);
                Vector3 root = dir * R(rng, 0f, 0.08f);
                Vector3 tip = root + dir * R(rng, 0.08f, 0.22f) + Vector3.up * h;
                b.SetColor(family, R(rng, 0.35f, 0.75f));
                b.DoubleSided(root - side * w, root + side * w, tip, Vector3.up);
            }
        }

        public static void Flower(MeshBuilder b, System.Random rng, Pal petal)
        {
            float h = R(rng, 0.25f, 0.45f);
            b.SetColor(Pal.GrassOlive, 0.4f);
            b.DoubleSided(new Vector3(-0.012f, 0f, 0f), new Vector3(0.012f, 0f, 0f), new Vector3(0f, h, 0f), Vector3.up);
            float s = R(rng, 0.05f, 0.08f);
            Vector3 c = new Vector3(0f, h, 0f);
            b.SetColor(petal, R(rng, 0.45f, 0.75f));
            b.DoubleSided(c + new Vector3(-s, 0f, 0f), c + new Vector3(0f, 0.01f, s), c + new Vector3(s, 0f, 0f), Vector3.up);
            b.DoubleSided(c + new Vector3(-s, 0f, 0f), c + new Vector3(s, 0f, 0f), c + new Vector3(0f, 0.01f, -s), Vector3.up);
            b.SetColor(Pal.FlowerYellow, 0.6f);
            b.DoubleSided(c + new Vector3(-0.015f, 0.012f, 0f), c + new Vector3(0f, 0.012f, 0.015f), c + new Vector3(0.015f, 0.012f, 0f), Vector3.up);
        }

        /// <summary>Fence section from local origin along +X of the given length.</summary>
        public static void FenceSection(MeshBuilder b, System.Random rng, float length, bool broken)
        {
            b.SetColor(Pal.Wood, R(rng, 0.3f, 0.5f));
            b.ShadedBox(new Vector3(0f, 0.5f, 0f), new Vector3(0.13f, 1.2f, 0.13f), Pal.WoodDark, rng, 0.35f, 0.55f);
            b.ShadedBox(new Vector3(0f, 1.12f, 0f), new Vector3(0.09f, 0.06f, 0.09f), Pal.WoodDark, rng, 0.25f, 0.35f);
            float[] heights = { 0.45f, 0.88f };
            for (int i = 0; i < heights.Length; i++)
            {
                if (broken && i == 1)
                {
                    // Snapped rail hanging to the ground.
                    b.Push(new Vector3(0.05f, heights[i], 0f), Quaternion.Euler(0f, R(rng, -8f, 8f), -35f), Vector3.one);
                    b.ShadedBox(new Vector3(length * 0.28f, 0f, 0f), new Vector3(length * 0.55f, 0.08f, 0.05f), Pal.Wood, rng, 0.3f, 0.5f);
                    b.Pop();
                    continue;
                }
                b.Push(new Vector3(length * 0.5f, heights[i], 0.06f), Quaternion.Euler(0f, 0f, R(rng, -3f, 3f)), Vector3.one);
                b.ShadedBox(Vector3.zero, new Vector3(length + 0.1f, 0.08f, 0.05f), Pal.Wood, rng, 0.35f, 0.6f);
                b.Pop();
            }
        }

        /// <summary>Jagged distant mountain, base at y = 0.</summary>
        public static void Mountain(MeshBuilder b, System.Random rng, float radius, float height)
        {
            const int sides = 7;
            var baseRing = new Vector3[sides];
            var midRing = new Vector3[sides];
            float midHeight = height * R(rng, 0.55f, 0.7f);
            Vector3 apex = new Vector3(R(rng, -0.15f, 0.15f) * radius, height, R(rng, -0.15f, 0.15f) * radius);
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides + R(rng, -0.2f, 0.2f);
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                baseRing[i] = dir * radius * R(rng, 0.8f, 1.15f) + Vector3.down * 25f;
                midRing[i] = Vector3.Lerp(dir * radius * R(rng, 0.35f, 0.5f), apex, 0.15f) + Vector3.up * midHeight * R(rng, 0.85f, 1.1f);
            }
            Vector3 interior = new Vector3(0f, height * 0.4f, 0f);
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                b.SetColor(Pal.Mountain, R(rng, 0.3f, 0.6f));
                b.QuadOutward(baseRing[i], baseRing[j], midRing[j], midRing[i], interior);
                b.SetColor(R(rng, 0f, 1f) > 0.35f ? Pal.Snow : Pal.Mountain, R(rng, 0.35f, 0.65f));
                b.TriangleOutward(midRing[i], midRing[j], apex, interior);
            }
        }

        public static void StoneBlock(MeshBuilder b, System.Random rng, Vector3 center, Vector3 size, bool mossy)
        {
            b.Push(center, Quaternion.Euler(R(rng, -2.5f, 2.5f), R(rng, -4f, 4f), R(rng, -2.5f, 2.5f)), Vector3.one);
            Vector3 s = Vector3.Scale(size, new Vector3(R(rng, 0.92f, 1f), R(rng, 0.9f, 1f), R(rng, 0.92f, 1f)));
            b.ShadedBox(Vector3.zero, s, mossy ? Pal.StoneMoss : Pal.Stone, rng, 0.4f, 0.68f);
            b.Pop();
        }

        public static void Gravestone(MeshBuilder b, System.Random rng)
        {
            b.Push(Vector3.zero, Quaternion.Euler(R(rng, -10f, 10f), 0f, R(rng, -8f, 8f)), Vector3.one);
            b.ShadedBox(new Vector3(0f, 0.35f, 0f), new Vector3(0.55f, 0.9f, 0.14f), Pal.Stone, rng, 0.3f, 0.5f);
            b.ShadedBox(new Vector3(0f, 0.85f, 0f), new Vector3(0.4f, 0.14f, 0.13f), Pal.StoneMoss, rng, 0.3f, 0.5f);
            b.Pop();
        }

        /// <summary>Chest body: front faces -Z, footprint ≈ 0.95 × 0.6, height 0.5.</summary>
        public static void ChestBody(MeshBuilder b)
        {
            var rng = new System.Random(5);
            b.ShadedBox(new Vector3(0f, 0.25f, 0f), new Vector3(0.95f, 0.5f, 0.6f), Pal.Wood, rng, 0.35f, 0.5f);
            for (int i = -1; i <= 1; i += 2)
                b.ShadedBox(new Vector3(0.36f * i, 0.25f, 0f), new Vector3(0.07f, 0.52f, 0.62f), Pal.SteelDark, rng, 0.45f, 0.55f);
            b.ShadedBox(new Vector3(0f, 0.04f, 0f), new Vector3(0.97f, 0.06f, 0.62f), Pal.SteelDark, rng, 0.35f, 0.45f);
            b.SetColor(Pal.Brass, 0.55f);
            b.Box(new Vector3(0f, 0.4f, -0.31f), new Vector3(0.12f, 0.15f, 0.04f));
            b.SetColor(Pal.Black, 0.5f);
            b.Box(new Vector3(0f, 0.38f, -0.33f), new Vector3(0.03f, 0.05f, 0.01f));
        }

        /// <summary>Chest lid with its hinge at the origin; the lid extends toward -Z.</summary>
        public static void ChestLid(MeshBuilder b)
        {
            var rng = new System.Random(6);
            b.ShadedBox(new Vector3(0f, 0.07f, -0.3f), new Vector3(0.97f, 0.14f, 0.62f), Pal.Wood, rng, 0.4f, 0.55f);
            b.ShadedBox(new Vector3(0f, 0.17f, -0.3f), new Vector3(0.9f, 0.08f, 0.45f), Pal.Wood, rng, 0.45f, 0.6f);
            for (int i = -1; i <= 1; i += 2)
                b.ShadedBox(new Vector3(0.36f * i, 0.11f, -0.3f), new Vector3(0.07f, 0.24f, 0.64f), Pal.SteelDark, rng, 0.45f, 0.55f);
        }

        public static void ChestGold(MeshBuilder b)
        {
            var rng = new System.Random(8);
            for (int i = 0; i < 26; i++)
            {
                b.SetColor(Pal.Brass, R(rng, 0.6f, 0.95f));
                b.Push(new Vector3(R(rng, -0.38f, 0.38f), 0.505f + R(rng, 0f, 0.015f), R(rng, -0.22f, 0.22f)),
                    Quaternion.Euler(R(rng, -20f, 20f), R(rng, 0f, 180f), R(rng, -20f, 20f)), Vector3.one);
                b.Prism(Vector3.zero, 0.04f, 0.04f, 0.012f, 6);
                b.Pop();
            }
        }

        public static void Campfire(MeshBuilder b, System.Random rng)
        {
            for (int i = 0; i < 9; i++)
            {
                float a = i * Mathf.PI * 2f / 9f;
                b.Rock(new Vector3(Mathf.Cos(a) * 0.75f, 0.1f, Mathf.Sin(a) * 0.75f), new Vector3(0.35f, 0.28f, 0.3f), rng, Pal.RockDark, 0.3f, 0.55f);
            }
            for (int i = 0; i < 4; i++)
            {
                b.SetColor(Pal.Bark, R(rng, 0.2f, 0.35f));
                b.Push(new Vector3(0f, 0.05f, 0f), Quaternion.Euler(0f, i * 47f + R(rng, -10f, 10f), 70f), Vector3.one);
                b.Prism(new Vector3(0f, -0.5f, 0f), 0.07f, 0.06f, 1.0f, 6);
                b.Pop();
            }
            b.SetColor(Pal.Black, 0.6f);
            b.Prism(new Vector3(0f, 0.01f, 0f), 0.5f, 0.45f, 0.04f, 8);
        }

        /// <summary>Flame cones (render with the unlit fire material).</summary>
        public static void Flames(MeshBuilder b, System.Random rng)
        {
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                b.SetColor(i % 2 == 0 ? Pal.FireOrange : Pal.FireYellow, R(rng, 0.5f, 0.9f));
                b.Push(new Vector3(Mathf.Cos(a) * 0.15f, 0.1f, Mathf.Sin(a) * 0.15f), Quaternion.Euler(R(rng, -12f, 12f), 0f, R(rng, -12f, 12f)), Vector3.one);
                b.Prism(Vector3.zero, R(rng, 0.12f, 0.18f), 0f, R(rng, 0.55f, 0.95f), 5, false);
                b.Pop();
            }
            b.SetColor(Pal.FireYellow, 1f);
            b.Prism(new Vector3(0f, 0.1f, 0f), 0.16f, 0f, 1.15f, 5, false);
        }

        public static void Tent(MeshBuilder b, System.Random rng)
        {
            const float w = 2.2f, h = 1.7f, d = 2.8f;
            Vector3 ridgeFront = new Vector3(0f, h, -d * 0.5f), ridgeBack = new Vector3(0f, h, d * 0.5f);
            Vector3 lf = new Vector3(-w * 0.5f, 0f, -d * 0.5f), lb = new Vector3(-w * 0.5f, 0f, d * 0.5f);
            Vector3 rf = new Vector3(w * 0.5f, 0f, -d * 0.5f), rb = new Vector3(w * 0.5f, 0f, d * 0.5f);
            Vector3 interior = new Vector3(0f, h * 0.35f, 0f);
            b.SetColor(Pal.Leather, 0.55f);
            b.QuadOutward(lf, lb, ridgeBack, ridgeFront, interior);
            b.SetColor(Pal.Leather, 0.45f);
            b.QuadOutward(rf, ridgeFront, ridgeBack, rb, interior);
            b.SetColor(Pal.Leather, 0.35f);
            b.TriangleOutward(lb, rb, ridgeBack, interior);
            // Open front flap pulled aside.
            b.SetColor(Pal.Leather, 0.5f);
            b.TriangleOutward(lf, ridgeFront, lf + new Vector3(0.6f, 0f, -0.3f), interior + Vector3.back * 2f);
            b.SetColor(Pal.WoodDark, 0.4f);
            b.Prism(ridgeFront + Vector3.down * h, 0.04f, 0.04f, h + 0.25f, 5);
            b.Prism(ridgeBack + Vector3.down * h, 0.04f, 0.04f, h + 0.25f, 5);
        }

        /// <summary>Banner pole with a tattered cloth hanging toward -Z side.</summary>
        public static void Banner(MeshBuilder b, System.Random rng, float height)
        {
            b.SetColor(Pal.WoodDark, 0.4f);
            b.Prism(new Vector3(0f, -0.3f, 0f), 0.06f, 0.05f, height + 0.3f, 6);
            b.Push(new Vector3(0f, height - 0.15f, 0f), Quaternion.identity, Vector3.one);
            b.SetColor(Pal.SteelDark, 0.5f);
            b.Box(Vector3.zero, new Vector3(1.2f, 0.05f, 0.05f));
            float clothHeight = height * 0.55f;
            int strips = 4;
            for (int i = 0; i < strips; i++)
            {
                float x0 = -0.55f + i * 1.1f / strips, x1 = x0 + 1.1f / strips;
                float len = clothHeight * R(rng, 0.7f, 1f);
                b.SetColor(Pal.ClothRed, R(rng, 0.3f, 0.5f));
                b.DoubleSided(new Vector3(x0, 0f, -0.03f), new Vector3(x1, 0f, -0.03f), new Vector3(x1, -len, -0.08f), Vector3.forward);
                b.DoubleSided(new Vector3(x0, 0f, -0.03f), new Vector3(x1, -len, -0.08f), new Vector3((x0 + x1) * 0.5f, -len - 0.15f, -0.08f), Vector3.forward);
                b.DoubleSided(new Vector3(x0, 0f, -0.03f), new Vector3((x0 + x1) * 0.5f, -len - 0.15f, -0.08f), new Vector3(x0, -len * 0.9f, -0.08f), Vector3.forward);
            }
            b.Pop();
        }

        // ---- Cursed Knight parts (each animated separately) --------------------------------------

        /// <summary>Torso with its pivot at the hips.</summary>
        public static void KnightTorso(MeshBuilder b)
        {
            var rng = new System.Random(21);
            b.ShadedBox(new Vector3(0f, 0.1f, 0f), new Vector3(0.44f, 0.24f, 0.28f), Pal.SteelDark, rng, 0.35f, 0.45f);  // waist
            b.ShadedBox(new Vector3(0f, 0.02f, 0f), new Vector3(0.48f, 0.08f, 0.31f), Pal.Leather, rng, 0.3f, 0.4f);    // belt
            b.ShadedBox(new Vector3(0f, 0.45f, 0f), new Vector3(0.58f, 0.45f, 0.34f), Pal.SteelDark, rng, 0.42f, 0.55f); // chest
            b.ShadedBox(new Vector3(0f, 0.47f, -0.17f), new Vector3(0.12f, 0.38f, 0.04f), Pal.SteelDark, rng, 0.6f, 0.7f); // ridge
            for (int i = -1; i <= 1; i += 2)
            {
                b.Push(new Vector3(0.36f * i, 0.66f, 0f), Quaternion.Euler(0f, 0f, -18f * i), Vector3.one);
                b.ShadedBox(Vector3.zero, new Vector3(0.24f, 0.13f, 0.32f), Pal.SteelDark, rng, 0.45f, 0.6f);     // pauldrons
                b.ShadedBox(new Vector3(0f, -0.08f, 0f), new Vector3(0.22f, 0.06f, 0.3f), Pal.Brass, rng, 0.25f, 0.35f);
                b.Pop();
            }
            // Tattered tabard.
            b.SetColor(Pal.ClothRed, 0.3f);
            for (int i = 0; i < 3; i++)
            {
                float x0 = -0.15f + i * 0.1f, x1 = x0 + 0.1f;
                float len = 0.45f + (i == 1 ? 0.12f : 0.02f * i);
                b.DoubleSided(new Vector3(x0, 0.08f, -0.16f), new Vector3(x1, 0.08f, -0.16f), new Vector3((x0 + x1) * 0.5f, 0.08f - len, -0.2f), Vector3.back);
            }
        }

        /// <summary>Helmet with its pivot at the neck.</summary>
        public static void KnightHead(MeshBuilder b)
        {
            var rng = new System.Random(22);
            b.SetColor(Pal.SteelDark, 0.45f);
            b.Prism(new Vector3(0f, 0f, 0f), 0.15f, 0.17f, 0.24f, 7, true, false, 0.2f);
            b.SetColor(Pal.SteelDark, 0.55f);
            b.Prism(new Vector3(0f, 0.24f, 0f), 0.17f, 0.04f, 0.12f, 7, false, false, 0.2f);
            b.SetColor(Pal.Black, 0.3f);
            b.Box(new Vector3(0f, 0.15f, -0.16f), new Vector3(0.2f, 0.035f, 0.03f));                     // visor slit
            b.ShadedBox(new Vector3(0f, 0.08f, -0.165f), new Vector3(0.03f, 0.12f, 0.03f), Pal.SteelDark, rng, 0.6f, 0.7f);
            // Horns.
            for (int i = -1; i <= 1; i += 2)
            {
                b.SetColor(Pal.Bone, 0.45f);
                b.Push(new Vector3(0.15f * i, 0.24f, 0f), Quaternion.Euler(-10f, 0f, -55f * i), Vector3.one);
                b.Prism(Vector3.zero, 0.04f, 0f, 0.2f, 5, false);
                b.Pop();
            }
        }

        public static void KnightEyes(MeshBuilder b)
        {
            b.SetColor(Pal.Glow, 0.8f);
            b.Box(new Vector3(-0.05f, 0.15f, -0.175f), new Vector3(0.04f, 0.022f, 0.01f));
            b.Box(new Vector3(0.05f, 0.15f, -0.175f), new Vector3(0.04f, 0.022f, 0.01f));
        }

        /// <summary>Arm hanging down from a shoulder pivot (0.75 long).</summary>
        public static void KnightArm(MeshBuilder b)
        {
            var rng = new System.Random(23);
            b.ShadedBox(new Vector3(0f, -0.17f, 0f), new Vector3(0.15f, 0.34f, 0.15f), Pal.SteelDark, rng, 0.35f, 0.45f);
            b.ShadedBox(new Vector3(0f, -0.36f, 0f), new Vector3(0.17f, 0.08f, 0.17f), Pal.SteelDark, rng, 0.55f, 0.6f);
            b.ShadedBox(new Vector3(0f, -0.52f, 0f), new Vector3(0.14f, 0.3f, 0.14f), Pal.SteelDark, rng, 0.4f, 0.5f);
            b.ShadedBox(new Vector3(0f, -0.71f, 0f), new Vector3(0.15f, 0.12f, 0.16f), Pal.Leather, rng, 0.3f, 0.4f);
        }

        /// <summary>Leg hanging down from a hip pivot (≈1.0 long).</summary>
        public static void KnightLeg(MeshBuilder b)
        {
            var rng = new System.Random(24);
            b.ShadedBox(new Vector3(0f, -0.24f, 0f), new Vector3(0.19f, 0.46f, 0.21f), Pal.SteelDark, rng, 0.35f, 0.45f);
            b.ShadedBox(new Vector3(0f, -0.49f, -0.02f), new Vector3(0.18f, 0.1f, 0.2f), Pal.SteelDark, rng, 0.55f, 0.62f);
            b.ShadedBox(new Vector3(0f, -0.72f, 0f), new Vector3(0.16f, 0.4f, 0.18f), Pal.SteelDark, rng, 0.4f, 0.5f);
            b.ShadedBox(new Vector3(0f, -0.95f, -0.05f), new Vector3(0.18f, 0.1f, 0.3f), Pal.Leather, rng, 0.25f, 0.35f);
        }

        /// <summary>Cape hanging from the upper back pivot.</summary>
        public static void KnightCape(MeshBuilder b)
        {
            var rng = new System.Random(25);
            int strips = 5;
            for (int i = 0; i < strips; i++)
            {
                float x0 = -0.3f + i * 0.6f / strips, x1 = x0 + 0.6f / strips;
                float len = R(rng, 1.0f, 1.3f);
                b.SetColor(Pal.ClothDark, R(rng, 0.35f, 0.6f));
                Vector3 a = new Vector3(x0, 0f, 0f), c = new Vector3(x1, 0f, 0f);
                Vector3 d = new Vector3(x1 + 0.02f, -len, 0.12f), e = new Vector3(x0 - 0.02f, -len * R(rng, 0.85f, 1f), 0.12f);
                b.DoubleSided(a, c, d, Vector3.back);
                b.DoubleSided(a, d, e, Vector3.back);
            }
        }
    }
}
