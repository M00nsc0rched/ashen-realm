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
                b.SetColor(Pal.PaintBlue, i % 2 == 0 ? R(rng, 0.5f, 0.6f) : R(rng, 0.4f, 0.48f));
                b.Triangle(center, p0 + Vector3.forward * thickness, p1 + Vector3.forward * thickness, b.Matrix.MultiplyVector(Vector3.forward));
                // The back faces the camera and is usually unlit, so it gets lighter shades.
                b.SetColor(Pal.Wood, i % 2 == 0 ? 0.72f : 0.62f);
                b.Triangle(Vector3.zero, p0, p1, b.Matrix.MultiplyVector(Vector3.back));
                // Iron rim.
                b.SetColor(Pal.Brass, R(rng, 0.45f, 0.6f));
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

        /// <summary>
        /// Pine: trunk and a dark core cone go into <paramref name="solid"/>; tiers of drooping bough cards with the
        /// ragged pixel fringe go into <paramref name="cutout"/> (alpha-clipped foliage material).
        /// </summary>
        public static void Pine(MeshBuilder solid, MeshBuilder cutout, System.Random rng, float height)
        {
            float trunkRadius = height * 0.035f;
            solid.SetColor(Pal.Bark, R(rng, 0.35f, 0.55f));
            solid.Prism(new Vector3(0f, -0.3f, 0f), trunkRadius * 1.2f, trunkRadius * 0.5f, height * 0.9f, 6, false, false);
            solid.SetColor(Pal.PineDark, R(rng, 0.18f, 0.3f));
            solid.Prism(new Vector3(0f, height * 0.24f, 0f), height * 0.15f, 0f, height * 0.78f, 6, true, false);

            const int tiers = 6;
            for (int t = 0; t < tiers; t++)
            {
                float k = t / (float)(tiers - 1);
                float y = Mathf.Lerp(height * 0.26f, height * 0.94f, k);
                float reach = Mathf.Lerp(height * 0.3f, height * 0.09f, k);
                float drop = reach * 0.85f;
                int boughs = Mathf.RoundToInt(Mathf.Lerp(9f, 5f, k));
                float offset = R(rng, 0f, 360f);
                for (int i = 0; i < boughs; i++)
                {
                    float angle = (offset + i * 360f / boughs + R(rng, -12f, 12f)) * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    var side = new Vector3(-dir.z, 0f, dir.x);
                    float width = reach * R(rng, 0.85f, 1.15f);
                    Vector3 root = new Vector3(0f, y, 0f) + dir * (reach * 0.08f);
                    Vector3 outer = root + dir * reach + Vector3.down * drop;
                    cutout.SetColor(R(rng, 0f, 1f) < 0.5f ? Pal.PineMid : Pal.PineDark, R(rng, 0.42f, 0.66f) + k * 0.08f);
                    cutout.SetLayer(TexLayer.PineBranch, UVMode.Explicit);
                    cutout.Card(outer - side * width * 0.55f, outer + side * width * 0.55f, root + side * width * 0.2f, root - side * width * 0.2f,
                        (Vector3.up + dir * 0.7f).normalized, 0.05f, true, 0.5f);
                }
            }
        }

        /// <summary>Broadleaf tree with clustered leaf cards (golden autumn birch look).</summary>
        public static void AutumnTree(MeshBuilder solid, MeshBuilder cutout, System.Random rng, float height, Pal leaves)
        {
            solid.SetColor(Pal.BirchBark, R(rng, 0.45f, 0.62f));
            solid.Prism(new Vector3(0f, -0.3f, 0f), 0.17f, 0.07f, height * 0.85f, 6, false, true);
            for (int i = 0; i < 3; i++)
            {
                solid.Push(new Vector3(0f, height * R(rng, 0.45f, 0.65f), 0f), Quaternion.Euler(0f, R(rng, 0f, 360f), R(rng, 30f, 50f)), Vector3.one);
                solid.Prism(Vector3.zero, 0.06f, 0.02f, height * 0.3f, 5, false, false);
                solid.Pop();
            }

            Vector3 crown = new Vector3(0f, height * 0.72f, 0f);
            int clusters = rng.Next(6, 9);
            for (int c = 0; c < clusters; c++)
            {
                float a = R(rng, 0f, Mathf.PI * 2f);
                float r = R(rng, 0f, height * 0.22f);
                Vector3 centre = crown + new Vector3(Mathf.Cos(a) * r, R(rng, -height * 0.15f, height * 0.25f), Mathf.Sin(a) * r);
                float size = R(rng, height * 0.3f, height * 0.42f);
                Vector3 normal = (centre - crown + Vector3.up * height * 0.3f).normalized;
                cutout.SetColor(leaves, R(rng, 0.42f, 0.7f));
                cutout.SetLayer(TexLayer.Leaves, UVMode.Explicit);
                float yaw = R(rng, 0f, 180f);
                for (int k = 0; k < 3; k++)
                {
                    float ang = (yaw + k * 60f) * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    Vector3 half = dir * size * 0.5f, up = Vector3.up * size * 0.5f;
                    cutout.Card(centre - half - up, centre + half - up, centre + half + up, centre - half + up, normal, 0.6f, true, 0.3f);
                }
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

        /// <summary>Two crossed cards; normals point up so the clump is lit like the ground under it.</summary>
        static void CrossedCards(MeshBuilder b, float width, float height, float wind, float yaw)
        {
            for (int k = 0; k < 2; k++)
            {
                float a = (yaw + k * 90f) * Mathf.Deg2Rad;
                var half = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (width * 0.5f);
                b.Card(-half, half, half + Vector3.up * height, -half + Vector3.up * height, Vector3.up, wind);
            }
        }

        public static void GrassClump(MeshBuilder b, System.Random rng, Pal family)
        {
            b.SetColor(family, R(rng, 0.38f, 0.66f));
            b.SetLayer(TexLayer.GrassCard, UVMode.Explicit);
            CrossedCards(b, R(rng, 0.6f, 1.0f), R(rng, 0.35f, 0.65f), 1f, R(rng, 0f, 90f));
        }

        public static void FlowerClump(MeshBuilder b, System.Random rng)
        {
            b.SetColor(Pal.FlowerWhite, 0.5f);
            b.SetLayer(TexLayer.Flowers, UVMode.Explicit, 1f, 1f);
            b.Current.Color = Color.white;
            CrossedCards(b, R(rng, 0.5f, 0.8f), R(rng, 0.35f, 0.55f), 0.8f, R(rng, 0f, 90f));
        }

        public static void WheatClump(MeshBuilder b, System.Random rng)
        {
            b.SetColor(Pal.Wheat, 0.5f);
            b.Current.Color = Color.white * R(rng, 0.85f, 1f);
            CrossedCards(b, R(rng, 1.0f, 1.4f), R(rng, 0.95f, 1.3f), 0.7f, R(rng, 0f, 90f));
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

        // ---- Class weapons: grip at the origin, blade or shaft along +Y ----

        public static void Dagger(MeshBuilder b)
        {
            var rng = new System.Random(61);
            b.ShadedBox(new Vector3(0f, -0.04f, 0f), new Vector3(0.04f, 0.2f, 0.04f), Pal.Leather, rng, 0.3f, 0.4f);
            b.ShadedBox(new Vector3(0f, 0.08f, 0f), new Vector3(0.16f, 0.03f, 0.05f), Pal.SteelDark, rng, 0.5f, 0.6f);
            b.SetColor(Pal.Steel, 0.55f);
            b.Prism(new Vector3(0f, 0.09f, 0f), 0.05f, 0f, 0.56f, 4, false, false, Mathf.PI * 0.25f);
        }

        public static void WitchStaff(MeshBuilder b)
        {
            var rng = new System.Random(62);
            b.SetColor(Pal.WoodDark, 0.32f);
            b.Prism(new Vector3(0f, -0.3f, 0f), 0.022f, 0.02f, 1.2f, 6);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                b.ShadedBox(new Vector3(Mathf.Cos(a) * 0.05f, 0.92f, Mathf.Sin(a) * 0.05f), new Vector3(0.02f, 0.14f, 0.02f), Pal.Bone, rng, 0.4f, 0.5f);
            }
            b.SetColor(Pal.FungalGlow, 0.75f);
            b.Box(new Vector3(0f, 0.94f, 0f), new Vector3(0.07f, 0.07f, 0.07f));
        }

        /// <summary>The free hand of a caster: an open glove around a small cold flame.</summary>
        public static void CastingHand(MeshBuilder b)
        {
            var rng = new System.Random(63);
            b.ShadedBox(new Vector3(0f, -0.12f, 0f), new Vector3(0.09f, 0.26f, 0.09f), Pal.ClothDark, rng, 0.35f, 0.45f);
            b.ShadedBox(new Vector3(0f, 0.04f, 0f), new Vector3(0.12f, 0.08f, 0.1f), Pal.Leather, rng, 0.35f, 0.45f);
            b.SetColor(Pal.FungalGlow, 0.7f);
            b.Prism(new Vector3(0f, 0.1f, 0f), 0.05f, 0f, 0.15f, 5, false);
        }

        public static void Mace(MeshBuilder b)
        {
            var rng = new System.Random(64);
            b.SetColor(Pal.WoodDark, 0.4f);
            b.Prism(new Vector3(0f, -0.14f, 0f), 0.024f, 0.024f, 0.62f, 6);
            b.ShadedBox(new Vector3(0f, -0.12f, 0f), new Vector3(0.05f, 0.14f, 0.05f), Pal.Leather, rng, 0.3f, 0.4f);
            b.ShadedBox(new Vector3(0f, 0.5f, 0f), new Vector3(0.13f, 0.16f, 0.13f), Pal.Steel, rng, 0.5f, 0.6f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                b.ShadedBox(new Vector3(Mathf.Cos(a) * 0.09f, 0.5f, Mathf.Sin(a) * 0.09f), new Vector3(0.07f, 0.13f, 0.07f), Pal.Brass, rng, 0.45f, 0.6f);
            }
            b.ShadedBox(new Vector3(0f, 0.6f, 0f), new Vector3(0.06f, 0.08f, 0.06f), Pal.Brass, rng, 0.5f, 0.6f);
        }

        public static void HandLantern(MeshBuilder b)
        {
            var rng = new System.Random(65);
            b.ShadedBox(new Vector3(0f, -0.1f, 0f), new Vector3(0.09f, 0.24f, 0.09f), Pal.ClothDark, rng, 0.35f, 0.45f);
            b.ShadedBox(new Vector3(0f, 0.06f, 0f), new Vector3(0.12f, 0.08f, 0.1f), Pal.Leather, rng, 0.35f, 0.45f);
            b.SetColor(Pal.Brass, 0.45f);
            b.Prism(new Vector3(0f, 0.12f, 0f), 0.01f, 0.01f, 0.12f, 4);
            b.ShadedBox(new Vector3(0f, 0.26f, 0f), new Vector3(0.17f, 0.03f, 0.17f), Pal.Brass, rng, 0.4f, 0.5f);
            b.ShadedBox(new Vector3(0f, 0.46f, 0f), new Vector3(0.17f, 0.03f, 0.17f), Pal.Brass, rng, 0.4f, 0.5f);
            b.SetColor(Pal.Lantern, 0.8f);
            b.Box(new Vector3(0f, 0.36f, 0f), new Vector3(0.12f, 0.18f, 0.12f));
            for (int i = 0; i < 4; i++)
            {
                float a = Mathf.PI * 0.25f + i * Mathf.PI * 0.5f;
                b.ShadedBox(new Vector3(Mathf.Cos(a) * 0.11f, 0.36f, Mathf.Sin(a) * 0.11f), new Vector3(0.02f, 0.2f, 0.02f), Pal.Brass, rng, 0.4f, 0.5f);
            }
        }

        // ---- Dressing for the Deep Dark: purely decorative, the player walks through it (as in Graveborne) ----

        public static void Mushroom(MeshBuilder b, System.Random rng, Vector3 position, float size)
        {
            b.Push(position, Quaternion.Euler(R(rng, -8f, 8f), R(rng, 0f, 360f), R(rng, -8f, 8f)), Vector3.one * size);
            b.SetColor(Pal.Bone, 0.5f);
            b.Prism(Vector3.zero, 0.12f, 0.08f, 0.7f, 6, false, false);
            b.SetColor(Pal.FungalCap, R(rng, 0.4f, 0.6f));
            b.Prism(new Vector3(0f, 0.66f, 0f), 0.46f, 0.1f, 0.3f, 7);
            b.SetColor(Pal.FungalGlow, 0.65f);
            b.Prism(new Vector3(0f, 0.62f, 0f), 0.3f, 0.44f, 0.05f, 7, false, false);
            b.Pop();
        }

        public static void Sarcophagus(MeshBuilder b, System.Random rng)
        {
            b.ShadedBox(new Vector3(0f, 0.3f, 0f), new Vector3(0.95f, 0.6f, 2.1f), Pal.Stone, rng, 0.32f, 0.5f);
            b.ShadedBox(new Vector3(0f, 0.68f, 0f), new Vector3(1.05f, 0.16f, 2.2f), Pal.Stone, rng, 0.42f, 0.55f);
            b.ShadedBox(new Vector3(0f, 0.8f, 0.3f), new Vector3(0.5f, 0.1f, 1.3f), Pal.Stone, rng, 0.55f, 0.65f);
        }

        public static void Candles(MeshBuilder b, System.Random rng)
        {
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = new Vector3(R(rng, -0.3f, 0.3f), 0f, R(rng, -0.3f, 0.3f));
                float h = R(rng, 0.15f, 0.42f);
                b.SetColor(Pal.Plaster, 0.6f);
                b.Prism(p, 0.045f, 0.04f, h, 5, false);
                b.SetColor(Pal.Lantern, 0.85f);
                b.Prism(p + Vector3.up * h, 0.025f, 0f, 0.09f, 4, false);
            }
        }

        public static void Urn(MeshBuilder b, System.Random rng)
        {
            b.SetColor(Pal.RoofTile, R(rng, 0.35f, 0.5f));
            b.Prism(Vector3.zero, 0.15f, 0.26f, 0.32f, 7);
            b.Prism(new Vector3(0f, 0.32f, 0f), 0.26f, 0.12f, 0.26f, 7);
            b.SetColor(Pal.RoofTile, 0.3f);
            b.Prism(new Vector3(0f, 0.58f, 0f), 0.11f, 0.14f, 0.1f, 7);
        }

        public static void BonePile(MeshBuilder b, System.Random rng)
        {
            for (int i = 0; i < 9; i++)
            {
                b.Push(new Vector3(R(rng, -0.5f, 0.5f), R(rng, 0.03f, 0.18f), R(rng, -0.5f, 0.5f)), Quaternion.Euler(R(rng, -30f, 30f), R(rng, 0f, 180f), 90f + R(rng, -20f, 20f)), Vector3.one);
                b.SetColor(Pal.Bone, R(rng, 0.4f, 0.7f));
                b.Prism(new Vector3(0f, -0.28f, 0f), 0.035f, 0.03f, 0.56f, 5);
                b.Pop();
            }
            for (int i = 0; i < 3; i++)
                Skull(b, rng, new Vector3(R(rng, -0.35f, 0.35f), 0.14f, R(rng, -0.35f, 0.35f)));
        }

        public static void Skull(MeshBuilder b, System.Random rng, Vector3 position)
        {
            b.Push(position, Quaternion.Euler(R(rng, -15f, 15f), R(rng, 0f, 360f), R(rng, -15f, 15f)), Vector3.one);
            b.ShadedBox(Vector3.zero, new Vector3(0.22f, 0.2f, 0.25f), Pal.Bone, rng, 0.5f, 0.68f);
            b.SetColor(Pal.Black, 0.3f);
            b.Box(new Vector3(-0.055f, 0.01f, 0.125f), new Vector3(0.06f, 0.06f, 0.01f));
            b.Box(new Vector3(0.055f, 0.01f, 0.125f), new Vector3(0.06f, 0.06f, 0.01f));
            b.Pop();
        }

        public static void Stalagmite(MeshBuilder b, System.Random rng, Pal family, float height)
        {
            b.SetColor(family, R(rng, 0.35f, 0.55f));
            b.Prism(Vector3.zero, height * 0.22f, 0f, height, 6, false, false, R(rng, 0f, 1f));
        }

        public static void Brazier(MeshBuilder b, System.Random rng)
        {
            b.SetColor(Pal.SteelDark, 0.35f);
            b.Prism(Vector3.zero, 0.18f, 0.05f, 0.7f, 6);
            b.Prism(new Vector3(0f, 0.7f, 0f), 0.08f, 0.34f, 0.24f, 7, true, false);
            b.SetColor(Pal.FireOrange, 0.75f);
            b.Prism(new Vector3(0f, 0.86f, 0f), 0.26f, 0.05f, 0.45f, 6, false);
            b.SetColor(Pal.FireYellow, 0.85f);
            b.Prism(new Vector3(0.02f, 0.86f, 0f), 0.13f, 0f, 0.3f, 5, false);
        }

        public static void RottenPost(MeshBuilder b, System.Random rng)
        {
            b.Push(Vector3.zero, Quaternion.Euler(R(rng, -7f, 7f), 0f, R(rng, -7f, 7f)), Vector3.one);
            b.SetColor(Pal.WoodDark, R(rng, 0.25f, 0.4f));
            b.Prism(Vector3.zero, 0.15f, 0.12f, R(rng, 1.4f, 2.6f), 6);
            b.Pop();
        }

        /// <summary>Stone frame around a dark stair; the doorway faces -Z and the origin is on the threshold.</summary>
        public static void StairArch(MeshBuilder b, System.Random rng, Pal stone, bool leadsDown)
        {
            const float width = 3.4f, height = 3.8f, depth = 1.2f;
            for (int i = -1; i <= 1; i += 2)
                b.ShadedBox(new Vector3(i * (width * 0.5f + 0.4f), height * 0.5f, 0f), new Vector3(0.8f, height, depth), stone, rng, 0.4f, 0.55f);
            b.ShadedBox(new Vector3(0f, height + 0.35f, 0f), new Vector3(width + 1.6f, 0.7f, depth), stone, rng, 0.45f, 0.6f);
            // The darkness sits in front of the wall behind the frame; the world has no real stairwell to walk into.
            b.SetColor(Pal.Black, 0.2f);
            b.Box(new Vector3(0f, height * 0.5f, 0.52f), new Vector3(width, height, 0.1f));
            if (leadsDown)
            {
                b.Box(new Vector3(0f, 0.015f, 0.05f), new Vector3(width, 0.03f, 0.9f));
                return;
            }
            for (int i = 0; i < 3; i++)
                b.ShadedBox(new Vector3(0f, 0.09f + i * 0.18f, 0.32f - i * 0.15f + 0.1f), new Vector3(width - 0.1f, 0.18f, 0.3f), stone, rng, 0.32f, 0.42f);
        }

        /// <summary>A throne of stacked bone, facing -Z.</summary>
        public static void BoneThrone(MeshBuilder b, System.Random rng)
        {
            b.ShadedBox(new Vector3(0f, 0.25f, 0f), new Vector3(4.6f, 0.5f, 3.6f), Pal.BoneWall, rng, 0.4f, 0.55f);
            b.ShadedBox(new Vector3(0f, 0.75f, 0.2f), new Vector3(3.2f, 0.5f, 2.6f), Pal.BoneWall, rng, 0.45f, 0.6f);
            b.ShadedBox(new Vector3(0f, 2.7f, 1.2f), new Vector3(2.6f, 3.8f, 0.5f), Pal.BoneWall, rng, 0.42f, 0.58f);
            for (int i = -1; i <= 1; i += 2)
                b.ShadedBox(new Vector3(i * 1.25f, 1.4f, 0.2f), new Vector3(0.4f, 0.8f, 1.8f), Pal.BoneWall, rng, 0.45f, 0.6f);
            for (int i = 0; i < 9; i++)
                Skull(b, rng, new Vector3(-1.2f + i * 0.3f, 4.75f + Mathf.Sin(i * 0.8f) * 0.15f, 1.15f));
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

        // ---- Hamlet ----------------------------------------------------------------------------------

        /// <summary>
        /// Half-timbered house: stone footing, plaster walls with dark timber frame, lit windows, tiled gable roof
        /// with the ridge along X and a chimney. Origin at ground centre, front (door) faces -Z.
        /// </summary>
        public static void House(MeshBuilder b, System.Random rng, float width, float depth, float wallHeight, bool tiledRoof)
        {
            const float footing = 0.6f;
            float top = footing + wallHeight;
            float roofHeight = depth * 0.55f;

            b.ShadedBox(new Vector3(0f, footing * 0.5f - 0.1f, 0f), new Vector3(width + 0.25f, footing + 0.2f, depth + 0.25f), Pal.Stone, rng, 0.38f, 0.5f);
            b.SetColor(Pal.Plaster, R(rng, 0.45f, 0.6f));
            b.Box(new Vector3(0f, footing + wallHeight * 0.5f, 0f), new Vector3(width, wallHeight, depth));

            // Timber frame: corner posts, sill / mid / top beams and braces on the long walls.
            float tx = width * 0.5f + 0.04f, tz = depth * 0.5f + 0.04f;
            void Beam(Vector3 centre, Vector3 size, float roll = 0f)
            {
                b.SetColor(Pal.WoodDark, R(rng, 0.25f, 0.4f));
                b.Push(centre, Quaternion.Euler(0f, 0f, roll), Vector3.one);
                b.Box(Vector3.zero, size);
                b.Pop();
            }
            foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
                Beam(new Vector3(sx * (tx - 0.08f), footing + wallHeight * 0.5f, sz * (tz - 0.08f)), new Vector3(0.24f, wallHeight, 0.24f));
            foreach (float y in new[] { footing + 0.08f, footing + wallHeight * 0.52f, top - 0.08f })
            foreach (float sz in new[] { -1f, 1f })
                Beam(new Vector3(0f, y, sz * tz), new Vector3(width + 0.1f, 0.16f, 0.1f));
            foreach (float sz in new[] { -1f, 1f })
            for (int i = 0; i < 2; i++)
            {
                float x = (i == 0 ? -1f : 1f) * width * 0.38f;
                Beam(new Vector3(x, footing + wallHeight * 0.76f, sz * tz), new Vector3(0.12f, wallHeight * 0.55f, 0.1f), (i == 0 ? 1f : -1f) * 35f);
            }

            // Door and lit windows.
            b.SetColor(Pal.WoodDark, 0.32f);
            b.Box(new Vector3(-width * 0.12f, footing + 0.95f, -tz - 0.03f), new Vector3(1.0f, 1.9f, 0.08f));
            b.SetColor(Pal.Brass, 0.5f);
            b.Box(new Vector3(-width * 0.12f + 0.32f, footing + 0.95f, -tz - 0.08f), new Vector3(0.08f, 0.08f, 0.05f));
            foreach (float sz in new[] { -1f, 1f })
            foreach (float x in new[] { width * 0.22f, -width * 0.36f })
            {
                if (sz < 0f && x < 0f) continue; // door side
                Vector3 c = new Vector3(x, footing + wallHeight * 0.62f, sz * (tz + 0.02f));
                b.SetColor(Pal.WoodDark, 0.3f);
                b.Box(c, new Vector3(0.86f, 0.74f, 0.08f));
                b.SetColor(Pal.Lantern, 0.55f);
                b.Current.Emission = 0.45f;
                b.Box(c + new Vector3(0f, 0f, sz * 0.03f), new Vector3(0.66f, 0.54f, 0.06f));
                b.SetColor(Pal.WoodDark, 0.25f);
                b.Box(c + new Vector3(0f, 0f, sz * 0.06f), new Vector3(0.06f, 0.6f, 0.03f));
            }

            // Gable ends (x = ±width/2) and the two roof slopes.
            foreach (float sx in new[] { -1f, 1f })
            {
                b.SetColor(Pal.Plaster, 0.5f);
                b.TriangleOutward(new Vector3(sx * width * 0.5f, top, -depth * 0.5f), new Vector3(sx * width * 0.5f, top, depth * 0.5f),
                    new Vector3(sx * width * 0.5f, top + roofHeight, 0f), Vector3.up * (top + roofHeight * 0.3f));
                Beam(new Vector3(sx * (width * 0.5f + 0.04f), top + roofHeight * 0.4f, 0f), new Vector3(0.1f, roofHeight * 0.8f, 0.14f));
            }
            // Each slope runs from the ridge to an eave that overhangs the wall by `overhang`.
            const float overhang = 0.45f;
            float halfSpan = depth * 0.5f + overhang;
            float drop = overhang * roofHeight / (depth * 0.5f);
            float slope = Mathf.Atan2(roofHeight, depth * 0.5f) * Mathf.Rad2Deg;
            float slabLength = Mathf.Sqrt(halfSpan * halfSpan + (roofHeight + drop) * (roofHeight + drop));
            foreach (float sz in new[] { -1f, 1f })
            {
                b.SetColor(tiledRoof ? Pal.RoofTile : Pal.Thatch, R(rng, 0.42f, 0.55f));
                Vector3 centre = new Vector3(0f, top + (roofHeight - drop) * 0.5f + 0.1f, sz * halfSpan * 0.5f);
                b.Push(centre, Quaternion.Euler(sz * slope, 0f, 0f), Vector3.one);
                b.Box(Vector3.zero, new Vector3(width + overhang * 2f, 0.2f, slabLength));
                b.Pop();
            }
            b.SetColor(Pal.WoodDark, 0.3f);
            b.Box(new Vector3(0f, top + roofHeight + 0.08f, 0f), new Vector3(width + overhang * 2f + 0.05f, 0.18f, 0.24f));

            // Chimney.
            b.ShadedBox(new Vector3(width * 0.28f, top + roofHeight * 0.75f, depth * 0.18f), new Vector3(0.65f, roofHeight + 1.0f, 0.65f), Pal.Stone, rng, 0.35f, 0.5f);
        }

        /// <summary>Stone windmill tower with a conical cap. The rotating sails are built separately.</summary>
        public static void WindmillTower(MeshBuilder b, System.Random rng, float height)
        {
            b.SetColor(Pal.Stone, 0.5f);
            b.Prism(new Vector3(0f, -0.4f, 0f), 2.3f, 1.65f, height + 0.4f, 8, false, false, Mathf.PI / 8f);
            b.SetColor(Pal.Thatch, 0.45f);
            b.Prism(new Vector3(0f, height - 0.05f, 0f), 2.15f, 0f, 2.4f, 8, true, false, Mathf.PI / 8f);
            b.SetColor(Pal.WoodDark, 0.3f);
            b.Box(new Vector3(0f, 1.1f, -2.05f), new Vector3(1.1f, 2.1f, 0.2f));
            b.Box(new Vector3(0f, height + 0.6f, -1.85f), new Vector3(0.5f, 0.5f, 0.8f));   // axle housing
            foreach (float y in new[] { height * 0.45f, height * 0.75f })
            {
                b.SetColor(Pal.Lantern, 0.4f);
                b.Current.Emission = 0.3f;
                b.Box(new Vector3(0f, y, -1.86f - (height - y) * 0.06f), new Vector3(0.45f, 0.6f, 0.1f));
            }
        }

        /// <summary>Four lattice sails around the local Z axis (hub at the origin, sails face -Z).</summary>
        public static void WindmillSails(MeshBuilder b, System.Random rng, float length)
        {
            b.SetColor(Pal.WoodDark, 0.35f);
            b.Prism(new Vector3(0f, 0f, 0f), 0.25f, 0.25f, 0.4f, 8);
            for (int i = 0; i < 4; i++)
            {
                b.Push(Vector3.zero, Quaternion.Euler(0f, 0f, i * 90f + 12f), Vector3.one);
                b.SetColor(Pal.Wood, R(rng, 0.38f, 0.5f));
                b.Box(new Vector3(0f, length * 0.5f, -0.1f), new Vector3(0.18f, length, 0.14f));
                b.SetColor(Pal.ClothRed, 0.2f);
                b.SetLayer(TexLayer.Cloth, UVMode.Triplanar, 0.8f);
                b.Current.Color = Palette.Linear(Pal.Bone, R(rng, 0.45f, 0.6f));
                b.DoubleSided(new Vector3(0.12f, length * 0.2f, -0.12f), new Vector3(0.95f, length * 0.24f, -0.12f), new Vector3(0.95f, length * 0.98f, -0.12f), Vector3.back);
                b.DoubleSided(new Vector3(0.12f, length * 0.2f, -0.12f), new Vector3(0.95f, length * 0.98f, -0.12f), new Vector3(0.12f, length, -0.12f), Vector3.back);
                b.SetColor(Pal.Wood, 0.42f);
                for (int r = 1; r <= 5; r++)
                    b.Box(new Vector3(0.52f, length * (0.2f + r * 0.15f), -0.16f), new Vector3(0.9f, 0.05f, 0.05f));
                b.Pop();
            }
        }

        /// <summary>Wooden post with an arm and a hanging lantern (glass emissive). Origin at the post base.</summary>
        public static void LanternPost(MeshBuilder b, System.Random rng)
        {
            b.SetColor(Pal.WoodDark, R(rng, 0.3f, 0.42f));
            b.Box(new Vector3(0f, 1.4f, 0f), new Vector3(0.18f, 2.9f, 0.18f));
            b.Box(new Vector3(0.35f, 2.75f, 0f), new Vector3(0.8f, 0.12f, 0.12f));
            b.Push(new Vector3(0.62f, 2.75f, 0f), Quaternion.Euler(0f, 0f, 45f), Vector3.one);
            b.Box(new Vector3(-0.2f, -0.2f, 0f), new Vector3(0.08f, 0.5f, 0.08f));
            b.Pop();
            b.SetColor(Pal.SteelDark, 0.45f);
            b.Box(new Vector3(0.62f, 2.5f, 0f), new Vector3(0.03f, 0.3f, 0.03f));
            b.Box(new Vector3(0.62f, 2.3f, 0f), new Vector3(0.3f, 0.06f, 0.3f));
            b.Box(new Vector3(0.62f, 1.96f, 0f), new Vector3(0.3f, 0.05f, 0.3f));
            b.SetColor(Pal.Lantern, 0.6f);
            b.Box(new Vector3(0.62f, 2.13f, 0f), new Vector3(0.22f, 0.3f, 0.22f));
        }

        public static void Barrel(MeshBuilder b, System.Random rng)
        {
            b.SetColor(Pal.Wood, R(rng, 0.4f, 0.55f));
            b.Prism(new Vector3(0f, 0f, 0f), 0.34f, 0.4f, 0.45f, 10, true, false);
            b.Prism(new Vector3(0f, 0.45f, 0f), 0.4f, 0.34f, 0.45f, 10, false, true);
            b.SetColor(Pal.SteelDark, 0.4f);
            foreach (float y in new[] { 0.12f, 0.75f })
                b.Prism(new Vector3(0f, y, 0f), y < 0.4f ? 0.37f : 0.38f, y < 0.4f ? 0.38f : 0.36f, 0.06f, 10, false, false);
        }

        public static void Crate(MeshBuilder b, System.Random rng, float size)
        {
            b.SetColor(Pal.Wood, R(rng, 0.45f, 0.6f));
            b.Box(new Vector3(0f, size * 0.5f, 0f), Vector3.one * size);
            b.SetColor(Pal.WoodDark, 0.35f);
            float e = size * 0.5f + 0.01f, t = 0.07f;
            foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
                b.Box(new Vector3(sx * (e - t * 0.5f), size * 0.5f, sz * (e - t * 0.5f)), new Vector3(t, size + 0.02f, t));
            foreach (float y in new[] { t * 0.5f, size - t * 0.5f })
            {
                b.Box(new Vector3(0f, y, -e + t * 0.5f), new Vector3(size, t, t));
                b.Box(new Vector3(0f, y, e - t * 0.5f), new Vector3(size, t, t));
            }
        }

        /// <summary>Signpost with two arrow boards (pointing ±X).</summary>
        public static void Signpost(MeshBuilder b, System.Random rng)
        {
            b.SetColor(Pal.WoodDark, 0.38f);
            b.Box(new Vector3(0f, 1.2f, 0f), new Vector3(0.14f, 2.4f, 0.14f));
            for (int i = 0; i < 2; i++)
            {
                float dir = i == 0 ? 1f : -1f;
                b.SetColor(Pal.Wood, R(rng, 0.45f, 0.6f));
                b.Push(new Vector3(dir * 0.45f, 2.05f - i * 0.38f, 0f), Quaternion.Euler(0f, R(rng, -10f, 10f), R(rng, -4f, 4f)), Vector3.one);
                b.Box(Vector3.zero, new Vector3(0.8f, 0.24f, 0.05f));
                b.TriangleOutward(new Vector3(dir * 0.4f, 0.12f, -0.025f), new Vector3(dir * 0.4f, -0.12f, -0.025f), new Vector3(dir * 0.58f, 0f, -0.025f), new Vector3(0f, 0f, 1f));
                b.TriangleOutward(new Vector3(dir * 0.4f, 0.12f, 0.025f), new Vector3(dir * 0.4f, -0.12f, 0.025f), new Vector3(dir * 0.58f, 0f, 0.025f), new Vector3(0f, 0f, -1f));
                b.Pop();
            }
        }

        /// <summary>Irregular flat puddle (fan) slightly above the ground.</summary>
        public static void Puddle(MeshBuilder b, System.Random rng, float radius)
        {
            const int sides = 10;
            b.SetColor(Pal.Water, R(rng, 0.45f, 0.55f));
            var ring = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                float r = radius * R(rng, 0.6f, 1.15f);
                ring[i] = new Vector3(Mathf.Cos(a) * r * 1.4f, 0.04f, Mathf.Sin(a) * r);
            }
            for (int i = 0; i < sides; i++)
                b.Triangle(new Vector3(0f, 0.04f, 0f), ring[i], ring[(i + 1) % sides], Vector3.up);
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
