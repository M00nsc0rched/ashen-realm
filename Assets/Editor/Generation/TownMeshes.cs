using UnityEngine;

namespace AshenRealm.EditorTools
{
    /// <summary>
    /// What was drawn for Hollowgate's forge, chapel and market: the goods as models, the two house fronts, the stalls and
    /// the gallows, and the keep on the hill behind them. Fronts and stalls face -Z like the other props.
    /// </summary>
    internal static class TownMeshes
    {
        static float R(System.Random rng, float min, float max) => MeshBuilder.Lerp(rng, min, max);

        // ---- The Cold Forge's goods: origin at the grip, blade along +Y, flat towards Z ------------------------------

        static void Blade(MeshBuilder b, Pal family, float shade, float length, float width, float grooveShade)
        {
            b.Push(Vector3.zero, Quaternion.identity, new Vector3(1f, 1f, 0.3f));
            b.SetColor(family, shade);
            b.Prism(new Vector3(0f, 0.09f, 0f), width, width * 0.72f, length, 4, false, false);
            b.SetColor(family, shade + 0.1f);
            b.Prism(new Vector3(0f, 0.09f + length, 0f), width * 0.72f, 0f, width * 3.2f, 4, false, false);
            b.Pop();
            b.SetColor(Pal.SteelDark, grooveShade);
            b.Box(new Vector3(0f, 0.09f + length * 0.45f, 0f), new Vector3(width * 0.2f, length * 0.86f, width * 0.68f));
        }

        static void WrappedGrip(MeshBuilder b, Pal family, float shade, float length, float radius)
        {
            b.SetColor(family, shade);
            b.Prism(new Vector3(0f, 0.06f - length, 0f), radius, radius, length, 6);
            b.SetColor(family, shade * 0.7f);
            int wraps = Mathf.RoundToInt(length / 0.04f);
            for (int i = 0; i < wraps; i++)
            {
                b.Push(new Vector3(0f, 0.06f - length * (i + 0.5f) / wraps, 0f), Quaternion.Euler(0f, 0f, 16f), Vector3.one);
                b.Box(Vector3.zero, new Vector3(radius * 2.3f, 0.012f, radius * 2.3f));
                b.Pop();
            }
        }

        static void Guard(MeshBuilder b, float width, Pal family, float shade)
        {
            b.SetColor(family, shade);
            b.Box(new Vector3(0f, 0.075f, 0f), new Vector3(width, 0.03f, 0.04f));
            foreach (float side in new[] { -1f, 1f })
                b.Box(new Vector3(side * (width * 0.5f - 0.015f), 0.075f, 0f), new Vector3(0.04f, 0.05f, 0.05f));
            b.SetColor(family, shade + 0.1f);
            b.Box(new Vector3(0f, 0.075f, 0f), new Vector3(0.06f, 0.045f, 0.055f));
        }

        /// <summary>Vaskard: honest steel, a long blade and a leather grip.</summary>
        public static void IronSword(MeshBuilder b)
        {
            Blade(b, Pal.Steel, 0.46f, 0.8f, 0.04f, 0.5f);
            Guard(b, 0.3f, Pal.Steel, 0.36f);
            WrappedGrip(b, Pal.Leather, 0.32f, 0.2f, 0.022f);
            b.SetColor(Pal.Steel, 0.4f);
            b.Prism(new Vector3(0f, -0.19f, 0f), 0.03f, 0.042f, 0.03f, 8);
            b.Prism(new Vector3(0f, -0.16f, 0f), 0.042f, 0.026f, 0.03f, 8);
        }

        /// <summary>Rozsdás Penge: shorter, pitted, more rust than steel.</summary>
        public static void RustedBlade(MeshBuilder b)
        {
            Blade(b, Pal.Steel, 0.3f, 0.68f, 0.042f, 0.34f);
            var rng = new System.Random(71);
            for (int i = 0; i < 16; i++)
            {
                float y = R(rng, 0.14f, 0.82f), x = R(rng, -0.026f, 0.026f);
                b.SetColor(Pal.Leather, R(rng, 0.34f, 0.5f));
                b.Box(new Vector3(x, y, 0f), new Vector3(R(rng, 0.016f, 0.036f), R(rng, 0.02f, 0.07f), 0.03f));
            }
            Guard(b, 0.26f, Pal.SteelDark, 0.4f);
            WrappedGrip(b, Pal.Leather, 0.24f, 0.19f, 0.023f);
            b.SetColor(Pal.SteelDark, 0.46f);
            b.Prism(new Vector3(0f, -0.19f, 0f), 0.04f, 0.04f, 0.05f, 6);
        }

        /// <summary>Sírtőr: a dark leaf of a blade, broadest in the middle, on a black grip.</summary>
        public static void GraveDagger(MeshBuilder b)
        {
            b.Push(Vector3.zero, Quaternion.identity, new Vector3(1f, 1f, 0.3f));
            b.SetColor(Pal.SteelDark, 0.42f);
            b.Prism(new Vector3(0f, 0.09f, 0f), 0.05f, 0.076f, 0.2f, 4, false, false);
            b.SetColor(Pal.SteelDark, 0.5f);
            b.Prism(new Vector3(0f, 0.29f, 0f), 0.076f, 0f, 0.4f, 4, false, false);
            b.Pop();
            b.SetColor(Pal.Bone, 0.5f);
            b.Box(new Vector3(0f, 0.27f, 0.02f), new Vector3(0.03f, 0.05f, 0.012f));
            Guard(b, 0.22f, Pal.SteelDark, 0.46f);
            WrappedGrip(b, Pal.ClothDark, 0.36f, 0.17f, 0.024f);
            b.SetColor(Pal.Steel, 0.36f);
            b.Prism(new Vector3(0f, -0.15f, 0f), 0.034f, 0.034f, 0.05f, 8);
        }

        /// <summary>Zarándokbot: a forked branch holding a bound stone, wrapped where ten thousand miles of hands held it.</summary>
        public static void PilgrimStaff(MeshBuilder b)
        {
            b.SetColor(Pal.Wood, 0.46f);
            b.Prism(new Vector3(0f, -0.62f, 0f), 0.021f, 0.024f, 1.42f, 6);
            foreach (float y in new[] { -0.3f, 0.05f, 0.4f })
            {
                b.SetColor(Pal.WoodDark, 0.34f);
                b.Box(new Vector3(0.018f, y, 0f), new Vector3(0.022f, 0.04f, 0.03f));
            }
            b.SetColor(Pal.SteelDark, 0.4f);
            b.Prism(new Vector3(0f, -0.7f, 0f), 0.012f, 0.026f, 0.1f, 6);
            foreach (float side in new[] { -1f, 1f })
            {
                b.SetColor(Pal.Wood, 0.46f);
                b.Push(new Vector3(0f, 0.8f, 0f), Quaternion.Euler(0f, 0f, -side * 20f), Vector3.one);
                b.Prism(Vector3.zero, 0.022f, 0.016f, 0.2f, 5);
                b.Pop();
            }
            b.SetColor(Pal.FlowerViolet, 0.3f);
            b.Prism(new Vector3(0f, 0.83f, 0f), 0.03f, 0.05f, 0.05f, 6);
            b.Prism(new Vector3(0f, 0.88f, 0f), 0.05f, 0.028f, 0.07f, 6);
            b.SetColor(Pal.Plaster, 0.4f);
            b.Prism(new Vector3(0f, 0.36f, 0f), 0.03f, 0.03f, 0.2f, 6);
            b.SetColor(Pal.Leather, 0.4f);
            foreach (float y in new[] { 0.42f, 0.5f })
                b.Prism(new Vector3(0f, y, 0f), 0.032f, 0.032f, 0.05f, 6);
        }

        // ---- The Grey Chapel's relics, for the cushions behind the rail: about 0.3 tall -------------------------------

        public static void Bloodstone(MeshBuilder b, System.Random rng)
        {
            b.Rock(new Vector3(0f, 0.15f, 0f), new Vector3(0.22f, 0.3f, 0.2f), rng, Pal.ClothRed, 0.42f, 0.62f, 0.2f);
            b.SetColor(Pal.SteelDark, 0.34f);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f + 0.4f;
                b.Push(new Vector3(Mathf.Cos(a) * 0.1f, 0.02f, Mathf.Sin(a) * 0.1f), Quaternion.Euler(Mathf.Sin(a) * 18f, 0f, -Mathf.Cos(a) * 18f), Vector3.one);
                b.Box(new Vector3(0f, 0.09f, 0f), new Vector3(0.03f, 0.18f, 0.03f));
                b.Pop();
            }
        }

        /// <summary>The eye in its pointed silver frame, standing on its back rest.</summary>
        public static void WitchEye(MeshBuilder b)
        {
            b.SetColor(Pal.SteelDark, 0.46f);
            b.Box(new Vector3(0f, 0.01f, 0.02f), new Vector3(0.14f, 0.02f, 0.1f));
            b.Box(new Vector3(0f, 0.06f, 0.02f), new Vector3(0.03f, 0.1f, 0.03f));
            b.Push(new Vector3(0f, 0.17f, 0f), Quaternion.Euler(-90f, 0f, 0f), new Vector3(1.5f, 1f, 0.8f));
            b.SetColor(Pal.SteelDark, 0.52f);
            b.Prism(new Vector3(0f, -0.015f, 0f), 0.15f, 0.15f, 0.03f, 4);
            b.Pop();
            b.Push(new Vector3(0f, 0.17f, -0.02f), Quaternion.Euler(-90f, 0f, 0f), new Vector3(1.2f, 1f, 1f));
            b.SetColor(Pal.FireOrange, 0.48f);
            b.Prism(new Vector3(0f, -0.01f, 0f), 0.085f, 0.075f, 0.03f, 10);
            b.SetColor(Pal.FlowerViolet, 0.4f);
            b.Prism(new Vector3(0.03f, 0.018f, 0f), 0.05f, 0.04f, 0.01f, 8);
            b.Pop();
            b.SetColor(Pal.Black, 0.2f);
            b.Box(new Vector3(0f, 0.17f, -0.048f), new Vector3(0.016f, 0.13f, 0.006f));
        }

        /// <summary>A hooded woman on her knees, holding a tablet: a small god for small mercies.</summary>
        public static void GraveIdol(MeshBuilder b, System.Random rng)
        {
            b.SetColor(Pal.Stone, 0.42f);
            b.Prism(Vector3.zero, 0.15f, 0.11f, 0.16f, 8);
            b.Prism(new Vector3(0f, 0.16f, 0f), 0.11f, 0.08f, 0.12f, 8);
            b.SetColor(Pal.Stone, 0.5f);
            b.Prism(new Vector3(0f, 0.28f, 0f), 0.085f, 0.06f, 0.07f, 7);
            b.Prism(new Vector3(0f, 0.35f, 0f), 0.06f, 0.012f, 0.05f, 7);
            b.SetColor(Pal.Stone, 0.62f);
            b.Box(new Vector3(0f, 0.31f, -0.06f), new Vector3(0.05f, 0.06f, 0.03f));
            b.ShadedBox(new Vector3(0f, 0.13f, -0.115f), new Vector3(0.11f, 0.19f, 0.024f), Pal.Stone, rng, 0.56f, 0.64f);
            foreach (float side in new[] { -1f, 1f })
                b.Box(new Vector3(side * 0.06f, 0.18f, -0.13f), new Vector3(0.035f, 0.04f, 0.03f));
            b.SetColor(Pal.StoneMoss, 0.4f);
            b.Box(new Vector3(0.06f, 0.05f, -0.09f), new Vector3(0.06f, 0.05f, 0.05f));
        }

        // ---- House fronts --------------------------------------------------------------------------------------------

        /// <summary>The Cold Forge's door: heavy planks scorched at the foot, an anvil sign and the fire showing through the gap.</summary>
        public static void ForgeFront(MeshBuilder b, System.Random rng)
        {
            const float width = 2.4f, height = 2.9f;
            b.ShadedBox(new Vector3(0f, 0.06f, -0.45f), new Vector3(width + 1.2f, 0.14f, 1f), Pal.Stone, rng, 0.36f, 0.44f);
            foreach (float side in new[] { -1f, 1f })
            {
                for (int i = 0; i < 5; i++)
                    b.ShadedBox(new Vector3(side * (width * 0.5f + 0.32f), 0.31f + i * 0.62f, -0.1f), new Vector3(0.64f, 0.6f, 0.36f), Pal.Stone, rng, 0.3f, 0.42f);
                b.ShadedBox(new Vector3(side * width * 0.25f, height * 0.5f + 0.06f, -0.03f), new Vector3(width * 0.5f - 0.02f, height, 0.08f), Pal.WoodDark, rng, 0.26f, 0.34f);
                b.SetColor(Pal.Black, 0.3f);
                b.Box(new Vector3(side * width * 0.25f, 0.34f, -0.08f), new Vector3(width * 0.5f - 0.04f, 0.5f, 0.02f));
                b.SetColor(Pal.SteelDark, 0.3f);
                foreach (float y in new[] { 0.7f, 1.7f, 2.6f })
                    b.Box(new Vector3(side * width * 0.25f, y, -0.09f), new Vector3(width * 0.5f - 0.06f, 0.1f, 0.03f));
            }
            b.SetColor(Pal.FireOrange, 0.5f);
            b.Current.Emission = 0.7f;
            b.Box(new Vector3(0f, height * 0.5f + 0.06f, -0.06f), new Vector3(0.05f, height - 0.1f, 0.02f));
            b.SetColor(Pal.WoodDark, 0.26f);
            b.Box(new Vector3(0f, height + 0.32f, -0.12f), new Vector3(width + 1.3f, 0.44f, 0.4f));

            // The sign: an iron arm and an anvil cut out of black plate.
            b.SetColor(Pal.SteelDark, 0.28f);
            b.Box(new Vector3(0f, 3.92f, -0.72f), new Vector3(0.06f, 0.06f, 1.44f));
            foreach (float z in new[] { -1.2f, -0.5f })
                b.Box(new Vector3(0f, 3.8f, z), new Vector3(0.03f, 0.2f, 0.03f));
            b.SetColor(Pal.Black, 0.3f);
            b.Box(new Vector3(0f, 3.58f, -0.85f), new Vector3(0.06f, 0.18f, 1f));
            b.Box(new Vector3(0f, 3.4f, -0.85f), new Vector3(0.06f, 0.2f, 0.34f));
            b.Box(new Vector3(0f, 3.24f, -0.85f), new Vector3(0.06f, 0.12f, 0.66f));
        }

        /// <summary>The Grey Chapel's door: a pointed arch of pale stone, dark oak, and a lancet of cold light above it.</summary>
        public static void ChapelFront(MeshBuilder b, System.Random rng)
        {
            const float halfWidth = 1.15f, spring = 2.3f;
            b.ShadedBox(new Vector3(0f, 0.08f, -0.55f), new Vector3(3.8f, 0.16f, 1.1f), Pal.Stone, rng, 0.44f, 0.52f);
            b.ShadedBox(new Vector3(0f, 0.24f, -0.3f), new Vector3(3.2f, 0.16f, 0.6f), Pal.Stone, rng, 0.46f, 0.54f);
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                for (int y = 0; y < 4; y++)
                    b.ShadedBox(new Vector3(side * (halfWidth + 0.3f), 0.3f + y * 0.58f, -0.16f), new Vector3(0.6f, 0.56f, 0.34f), Pal.Stone, rng, 0.44f, 0.56f);
                for (int s = 0; s < 6; s++)
                {
                    float t = (s + 0.5f) / 6f, a = t * Mathf.PI * 0.42f;
                    Vector3 p = new Vector3(side * (halfWidth + 0.3f - (1f - Mathf.Cos(a)) * 1.4f - t * 0.1f), spring + Mathf.Sin(a) * 1.5f, -0.16f);
                    b.Push(p, Quaternion.Euler(0f, 0f, side * (a * Mathf.Rad2Deg)), Vector3.one);
                    b.ShadedBox(Vector3.zero, new Vector3(0.6f, 0.42f, 0.34f), Pal.Stone, rng, 0.46f, 0.56f);
                    b.Pop();
                }
            }

            // The door fills the arch: planks that narrow towards the point.
            for (int i = 0; i < 12; i++)
            {
                float y = 0.3f + i * 0.31f, half = i < 8 ? halfWidth : halfWidth * (1f - (i - 7) * 0.18f);
                b.ShadedBox(new Vector3(0f, y + 0.155f, -0.04f), new Vector3(half * 2f, 0.3f, 0.08f), Pal.WoodDark, rng, 0.24f, 0.32f);
            }
            b.SetColor(Pal.SteelDark, 0.3f);
            foreach (float y in new[] { 0.9f, 1.9f })
                b.Box(new Vector3(0f, y, -0.09f), new Vector3(halfWidth * 2f - 0.1f, 0.09f, 0.03f));
            b.SetColor(Pal.Brass, 0.4f);
            b.Prism(new Vector3(0.3f, 1.3f, -0.12f), 0.06f, 0.06f, 0.1f, 6);
            b.ShadedBox(new Vector3(0f, 3.86f, -0.16f), new Vector3(0.78f, 0.62f, 0.34f), Pal.Stone, rng, 0.5f, 0.56f);

            for (int i = 0; i < 9; i++)
            {
                float y = 5.1f + i * 0.33f, half = i < 6 ? 0.42f : 0.42f * (1f - (i - 5) * 0.27f);
                b.SetColor(Pal.PaintBlue, 0.7f);
                b.Current.Layer = TexLayer.Plain;
                b.Current.Emission = 0.5f;
                b.Box(new Vector3(0f, y + 0.165f, -0.02f), new Vector3(half * 2f, 0.32f, 0.04f));
            }
            b.SetColor(Pal.Stone, 0.4f);
            b.Box(new Vector3(0f, 6.3f, -0.05f), new Vector3(0.05f, 2.5f, 0.05f));
        }

        // ---- Gallows Market ------------------------------------------------------------------------------------------

        /// <summary>
        /// One stall of the market: a plank counter on a braced front, posts, and a canopy of cloth that has seen too many
        /// winters. Centred on the counter; the counter's front looks to -Z.
        /// </summary>
        public static void MarketStall(MeshBuilder b, System.Random rng, float width, bool lantern, bool cleanCloth)
        {
            const float depth = 1.5f, counter = 0.98f, front = 2.25f, back = 2.85f, overhang = 0.55f;
            float half = width * 0.5f;

            foreach (float side in new[] { -1f, 1f })
            {
                b.ShadedBox(new Vector3(side * half, front * 0.5f, -depth * 0.5f), new Vector3(0.16f, front, 0.16f), Pal.WoodDark, rng, 0.26f, 0.36f);
                b.ShadedBox(new Vector3(side * half, back * 0.5f, depth * 0.5f), new Vector3(0.16f, back, 0.16f), Pal.WoodDark, rng, 0.26f, 0.36f);
                b.ShadedBox(new Vector3(side * half, 0.2f, 0f), new Vector3(0.1f, 0.1f, depth), Pal.WoodDark, rng, 0.26f, 0.34f);
            }

            const int planks = 4;
            for (int i = 0; i < planks; i++)
                b.ShadedBox(new Vector3(0f, counter, -depth * 0.5f + (i + 0.5f) * depth / planks), new Vector3(width + 0.1f, 0.07f, depth / planks - 0.03f), Pal.Wood, rng, 0.28f, 0.42f);
            for (float x = -half + 0.17f; x < half; x += 0.34f)
                b.ShadedBox(new Vector3(x, counter * 0.5f - 0.03f, -depth * 0.5f + 0.04f), new Vector3(0.32f, counter - 0.1f, 0.05f), Pal.WoodDark, rng, 0.28f, 0.4f);

            // The cross brace on the front, as drawn.
            float span = Mathf.Sqrt(width * width * 0.25f + 0.64f);
            float angle = Mathf.Atan2(0.8f, half) * Mathf.Rad2Deg;
            foreach (float tilt in new[] { angle, -angle })
            {
                b.Push(new Vector3(-half * 0.5f, 0.5f, -depth * 0.5f), Quaternion.Euler(0f, 0f, tilt), Vector3.one);
                b.SetColor(Pal.WoodDark, 0.34f);
                b.Box(Vector3.zero, new Vector3(span, 0.09f, 0.05f));
                b.Pop();
            }

            b.ShadedBox(new Vector3(0f, front, -depth * 0.5f), new Vector3(width + 0.2f, 0.12f, 0.12f), Pal.WoodDark, rng, 0.28f, 0.36f);
            b.ShadedBox(new Vector3(0f, back, depth * 0.5f), new Vector3(width + 0.2f, 0.12f, 0.12f), Pal.WoodDark, rng, 0.28f, 0.36f);

            // Strips of cloth from the back rail over the front one, each its own length: the hem is the point of them.
            float slope = Mathf.Atan2(back - front, depth);
            float rise = (back - front) / depth;
            const int strips = 9;
            for (int i = 0; i < strips; i++)
            {
                float x = -half - 0.1f + (i + 0.5f) * (width + 0.2f) / strips;
                float hang = overhang + R(rng, -0.05f, 0.36f);
                float length = depth + hang + 0.2f;
                float centre = depth * 0.5f + 0.1f - length * 0.5f;
                b.SetColor(Pal.Plaster, R(rng, 0.2f, 0.36f));
                b.Push(new Vector3(x, front + 0.1f + (centre + depth * 0.5f) * rise, centre), Quaternion.Euler(-slope * Mathf.Rad2Deg, 0f, 0f), Vector3.one);
                b.Box(Vector3.zero, new Vector3((width + 0.2f) / strips - 0.015f, 0.02f, length));
                b.Pop();
            }
            for (int i = 0; i < 7; i++)
            {
                float x = R(rng, -half, half), length = R(rng, 0.2f, 0.6f);
                float z = -depth * 0.5f - overhang - 0.04f;
                b.SetColor(Pal.Plaster, R(rng, 0.2f, 0.32f));
                b.Box(new Vector3(x, front + 0.1f + (z + depth * 0.5f) * rise - length * 0.5f, z), new Vector3(R(rng, 0.1f, 0.26f), length, 0.02f));
            }

            if (cleanCloth)
            {
                b.SetColor(Pal.Plaster, 0.62f);
                b.Box(new Vector3(0f, counter + 0.05f, 0f), new Vector3(width - 0.3f, 0.02f, depth - 0.3f));
            }

            if (lantern)
            {
                float x = half - 0.02f, z = -depth * 0.5f - 0.34f;
                b.SetColor(Pal.SteelDark, 0.3f);
                b.Box(new Vector3(x, front - 0.2f, -depth * 0.5f - 0.2f), new Vector3(0.04f, 0.04f, 0.42f));
                b.Box(new Vector3(x, front - 0.34f, z), new Vector3(0.03f, 0.26f, 0.03f));
                b.Prism(new Vector3(x, front - 0.58f, z), 0.15f, 0.05f, 0.14f, 6);
                b.Box(new Vector3(x, front - 1.02f, z), new Vector3(0.26f, 0.04f, 0.26f));
                foreach (float cx in new[] { -0.1f, 0.1f })
                foreach (float cz in new[] { -0.1f, 0.1f })
                    b.Box(new Vector3(x + cx, front - 0.8f, z + cz), new Vector3(0.025f, 0.42f, 0.025f));
                b.SetColor(Pal.Lantern, 0.75f);
                b.Box(new Vector3(x, front - 0.8f, z), new Vector3(0.17f, 0.38f, 0.17f));
            }
        }

        /// <summary>Halloway's notices on the street: a little roof, a lantern on an arm and the morning's paper. Faces -Z.</summary>
        public static void NoticeBoard(MeshBuilder b, System.Random rng)
        {
            foreach (float x in new[] { -0.95f, 0.95f })
                b.ShadedBox(new Vector3(x, 1.4f, 0f), new Vector3(0.16f, 2.8f, 0.16f), Pal.WoodDark, rng, 0.28f, 0.38f);
            for (float y = 0.85f; y < 2.55f; y += 0.3f)
                b.ShadedBox(new Vector3(0f, y, 0f), new Vector3(1.8f, 0.28f, 0.06f), Pal.Wood, rng, 0.28f, 0.4f);
            for (int i = 0; i < 2; i++)
            {
                b.SetColor(Pal.RoofTile, 0.28f + i * 0.06f);
                b.Push(new Vector3(0f, 2.9f, i == 0 ? -0.2f : 0.2f), Quaternion.Euler(i == 0 ? -22f : 22f, 0f, 0f), Vector3.one);
                b.Box(Vector3.zero, new Vector3(2.3f, 0.05f, 0.56f));
                b.Pop();
            }
            for (int i = 0; i < 8; i++)
            {
                b.SetColor(Pal.Bone, R(rng, 0.42f, 0.62f));
                b.Current.Layer = TexLayer.Plain;
                b.Push(new Vector3(-0.56f + (i % 4) * 0.37f + R(rng, -0.04f, 0.04f), 2.16f - (i / 4) * 0.72f, -0.05f), Quaternion.Euler(0f, 0f, R(rng, -6f, 6f)), Vector3.one);
                b.Box(Vector3.zero, new Vector3(0.3f, 0.5f, 0.01f));
                b.Pop();
            }
            b.ShadedBox(new Vector3(0f, 0.5f, 0f), new Vector3(1.8f, 0.14f, 0.1f), Pal.WoodDark, rng, 0.3f, 0.4f);

            b.SetColor(Pal.SteelDark, 0.3f);
            b.Box(new Vector3(1.2f, 2.62f, 0f), new Vector3(0.5f, 0.04f, 0.04f));
            b.Prism(new Vector3(1.38f, 2.32f, 0f), 0.14f, 0.05f, 0.14f, 6);
            b.Box(new Vector3(1.38f, 1.9f, 0f), new Vector3(0.24f, 0.04f, 0.24f));
            b.SetColor(Pal.Lantern, 0.75f);
            b.Box(new Vector3(1.38f, 2.1f, 0f), new Vector3(0.16f, 0.36f, 0.16f));
        }

        /// <summary>The gallows that gives the market its name: a stone foot, a plank deck with a rail, and steps down to -Z.</summary>
        public static void Gallows(MeshBuilder b, System.Random rng, float halfWidth, float halfDepth, float deck, int steps, float stepRise, float stepRun)
        {
            for (float x = -halfWidth; x < halfWidth; x += 1.3f)
            for (int side = 0; side < 2; side++)
                b.ShadedBox(new Vector3(x + 0.65f, 0.3f, (side == 0 ? -1f : 1f) * (halfDepth + 0.15f)), new Vector3(1.26f, 0.6f, 0.5f), Pal.Stone, rng, 0.3f, 0.44f);
            for (float z = -halfDepth; z < halfDepth; z += 1.3f)
            for (int side = 0; side < 2; side++)
                b.ShadedBox(new Vector3((side == 0 ? -1f : 1f) * (halfWidth + 0.15f), 0.3f, Mathf.Min(z + 0.65f, halfDepth - 0.1f)), new Vector3(0.5f, 0.6f, 1.26f), Pal.Stone, rng, 0.3f, 0.44f);

            // Legs and braces under the deck.
            for (float x = -halfWidth + 0.3f; x <= halfWidth - 0.2f; x += (halfWidth * 2f - 0.6f) / 4f)
            foreach (float z in new[] { -halfDepth + 0.3f, 0f, halfDepth - 0.3f })
                b.ShadedBox(new Vector3(x, deck * 0.5f, z), new Vector3(0.24f, deck, 0.24f), Pal.WoodDark, rng, 0.24f, 0.34f);
            foreach (float z in new[] { -halfDepth + 0.3f, halfDepth - 0.3f })
            for (float x = -halfWidth + 1.2f; x < halfWidth; x += 2.4f)
            foreach (float tilt in new[] { 38f, -38f })
            {
                b.Push(new Vector3(x, deck * 0.5f, z), Quaternion.Euler(0f, 0f, tilt), Vector3.one);
                b.SetColor(Pal.WoodDark, 0.28f);
                b.Box(Vector3.zero, new Vector3(2.5f, 0.12f, 0.08f));
                b.Pop();
            }

            for (float z = -halfDepth; z < halfDepth; z += 0.5f)
                b.ShadedBox(new Vector3(0f, deck, z + 0.25f), new Vector3(halfWidth * 2f + 0.2f, 0.1f, 0.47f), Pal.Wood, rng, 0.26f, 0.38f);

            // The rail, open where the steps arrive.
            void Rail(Vector3 from, Vector3 to)
            {
                float length = Vector3.Distance(from, to);
                int posts = Mathf.Max(1, Mathf.RoundToInt(length / 1.4f));
                for (int i = 0; i <= posts; i++)
                    b.ShadedBox(Vector3.Lerp(from, to, i / (float)posts) + Vector3.up * 0.55f, new Vector3(0.1f, 1.1f, 0.1f), Pal.WoodDark, rng, 0.26f, 0.36f);
                bool alongX = Mathf.Abs(to.x - from.x) > Mathf.Abs(to.z - from.z);
                Vector3 size = alongX ? new Vector3(length, 0.1f, 0.08f) : new Vector3(0.08f, 0.1f, length);
                b.ShadedBox((from + to) * 0.5f + Vector3.up * 1.08f, size, Pal.WoodDark, rng, 0.28f, 0.38f);
                b.ShadedBox((from + to) * 0.5f + Vector3.up * 0.6f, size * 0.98f, Pal.WoodDark, rng, 0.26f, 0.34f);
            }
            float top = deck + 0.05f, gap = 1.3f;
            Rail(new Vector3(-halfWidth, top, -halfDepth), new Vector3(-gap, top, -halfDepth));
            Rail(new Vector3(gap, top, -halfDepth), new Vector3(halfWidth, top, -halfDepth));
            Rail(new Vector3(-halfWidth, top, halfDepth), new Vector3(halfWidth, top, halfDepth));
            Rail(new Vector3(-halfWidth, top, -halfDepth), new Vector3(-halfWidth, top, halfDepth));
            Rail(new Vector3(halfWidth, top, -halfDepth), new Vector3(halfWidth, top, halfDepth));

            // The steps.
            for (int i = 0; i < steps; i++)
            {
                float y = deck - (i + 1) * stepRise;
                b.ShadedBox(new Vector3(0f, y + 0.02f, -halfDepth - (i + 0.5f) * stepRun), new Vector3(2.4f, 0.09f, stepRun - 0.02f), Pal.Wood, rng, 0.26f, 0.38f);
            }
            foreach (float side in new[] { -1.26f, 1.26f })
            {
                float run = steps * stepRun, drop = steps * stepRise;
                float angle = Mathf.Atan2(drop, run) * Mathf.Rad2Deg, length = Mathf.Sqrt(run * run + drop * drop);
                b.Push(new Vector3(side, deck - drop * 0.5f, -halfDepth - run * 0.5f), Quaternion.Euler(-angle, 0f, 0f), Vector3.one);
                b.SetColor(Pal.WoodDark, 0.3f);
                b.Box(Vector3.zero, new Vector3(0.12f, 0.26f, length + 0.2f));
                b.Box(new Vector3(0f, 1.1f, 0f), new Vector3(0.08f, 0.1f, length));
                b.Pop();
            }

            // The frame.
            const float beamHeight = 4.9f, beamHalf = 2.7f;
            foreach (float side in new[] { -1f, 1f })
            {
                b.ShadedBox(new Vector3(side * beamHalf, deck + beamHeight * 0.5f, 0.8f), new Vector3(0.36f, beamHeight + 0.2f, 0.36f), Pal.WoodDark, rng, 0.24f, 0.32f);
                b.Push(new Vector3(side * (beamHalf - 0.6f), deck + beamHeight - 0.62f, 0.8f), Quaternion.Euler(0f, 0f, side * 45f), Vector3.one);
                b.SetColor(Pal.WoodDark, 0.28f);
                b.Box(Vector3.zero, new Vector3(0.18f, 1.7f, 0.18f));
                b.Pop();
                b.Push(new Vector3(side * beamHalf, deck + 0.8f, 1.4f), Quaternion.Euler(45f, 0f, 0f), Vector3.one);
                b.SetColor(Pal.WoodDark, 0.28f);
                b.Box(Vector3.zero, new Vector3(0.16f, 1.8f, 0.16f));
                b.Pop();
            }
            b.ShadedBox(new Vector3(0f, deck + beamHeight, 0.8f), new Vector3(beamHalf * 2f + 1.2f, 0.42f, 0.42f), Pal.WoodDark, rng, 0.24f, 0.32f);
            foreach (float x in new[] { -1.2f, 0.7f })
            {
                b.SetColor(Pal.Thatch, 0.34f);
                b.Box(new Vector3(x, deck + beamHeight - 0.9f, 0.8f), new Vector3(0.04f, 1.5f, 0.04f));
                b.Prism(new Vector3(x, deck + beamHeight - 2.1f, 0.8f), 0.13f, 0.13f, 0.5f, 6, false, false);
            }
        }

        /// <summary>The keep on the hill behind the city, seen through the haze. Origin on the hilltop.</summary>
        public static void Keep(MeshBuilder b, System.Random rng)
        {
            void Tower(Vector3 at, float width, float height)
            {
                b.ShadedBox(at + Vector3.up * (height * 0.5f), new Vector3(width, height, width), Pal.Mountain, rng, 0.22f, 0.32f);
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.PI * 0.5f;
                    Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Vector3 side = new Vector3(d.z, 0f, -d.x);
                    foreach (float s in new[] { -0.36f, 0f, 0.36f })
                        b.ShadedBox(at + d * (width * 0.46f) + side * (s * width) + Vector3.up * (height + 0.9f), new Vector3(width * 0.18f, 1.8f, width * 0.18f), Pal.Mountain, rng, 0.22f, 0.3f);
                }
            }

            Tower(new Vector3(0f, 0f, 0f), 16f, 40f);
            Tower(new Vector3(-20f, 0f, 2f), 9f, 28f);
            Tower(new Vector3(20f, 0f, 2f), 9f, 30f);
            Tower(new Vector3(-30f, 0f, -6f), 7f, 20f);
            Tower(new Vector3(32f, 0f, -6f), 7f, 18f);
            b.ShadedBox(new Vector3(0f, 8f, 2f), new Vector3(60f, 16f, 8f), Pal.Mountain, rng, 0.22f, 0.3f);
            foreach (Vector3 p in new[] { new Vector3(-6f, 24f, -8.1f), new Vector3(5f, 30f, -8.1f), new Vector3(-20f, 16f, -2.6f), new Vector3(20f, 20f, -2.6f), new Vector3(-12f, 11f, -2.1f), new Vector3(12f, 11f, -2.1f) })
            {
                b.SetColor(Pal.Lantern, 0.7f);
                b.Box(p, new Vector3(1.4f, 2.2f, 0.2f));
            }
            b.SetColor(Pal.ClothRed, 0.34f);
            b.Box(new Vector3(0f, 32f, -8.2f), new Vector3(3f, 9f, 0.2f));
        }
    }
}
