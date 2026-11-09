using UnityEngine;

namespace AshenRealm.EditorTools
{
    /// <summary>
    /// The people of Hollowgate who have a painted portrait, as blocky pixel-art figures dressed after those paintings. They
    /// use a quiet living pose at runtime; the smith's working arm is a separate pivot. Feet on y = 0, facing +Z.
    /// </summary>
    internal static class PeopleMeshes
    {
        static readonly Color Skin = new Color(0.72f, 0.5f, 0.38f);
        static readonly Color AgedSkin = new Color(0.62f, 0.44f, 0.34f);
        static readonly Color GreyHair = new Color(0.5f, 0.46f, 0.43f);
        static readonly Color DarkHair = new Color(0.27f, 0.21f, 0.16f);
        static readonly Color Linen = new Color(0.68f, 0.56f, 0.4f);
        static readonly Color Apron = new Color(0.34f, 0.19f, 0.11f);
        static readonly Color Skirt = new Color(0.33f, 0.33f, 0.24f);
        static readonly Color Coat = new Color(0.3f, 0.22f, 0.15f);
        static readonly Color Soot = new Color(0.15f, 0.13f, 0.14f);

        /// <summary>Takes a family's texture with a colour of its own: skin and linen have no family in the palette.</summary>
        static void Paint(MeshBuilder b, Pal texture, Color color)
        {
            b.SetColor(texture);
            Color linear = color.linear;
            linear.a = 1f;
            b.Current.Color = linear;
        }

        static void Limb(MeshBuilder b, Vector3 from, Vector3 to, float width, float depth)
        {
            Vector3 span = to - from;
            b.Push((from + to) * 0.5f, Quaternion.FromToRotation(Vector3.up, span.normalized), Vector3.one);
            b.Box(Vector3.zero, new Vector3(width, span.magnitude, depth));
            b.Pop();
        }

        static void Hand(MeshBuilder b, Vector3 at, Color skin)
        {
            Paint(b, Pal.Leather, skin);
            b.Box(at, new Vector3(0.1f, 0.1f, 0.12f));
        }

        /// <summary>Head on a neck at <paramref name="neck"/>: the wide face of a figure seen at 360 lines.</summary>
        static void Head(MeshBuilder b, Vector3 neck, Color skin)
        {
            Paint(b, Pal.Leather, skin * 0.85f);
            b.Box(neck + new Vector3(0f, 0.03f, 0f), new Vector3(0.14f, 0.1f, 0.14f));
            Paint(b, Pal.Leather, skin);
            b.Box(neck + new Vector3(0f, 0.2f, 0.01f), new Vector3(0.25f, 0.28f, 0.26f));
            b.Box(neck + new Vector3(0f, 0.17f, 0.15f), new Vector3(0.05f, 0.09f, 0.05f));
            b.SetColor(Pal.Black, 0.3f);
            b.Box(neck + new Vector3(-0.06f, 0.22f, 0.142f), new Vector3(0.036f, 0.026f, 0.01f));
            b.Box(neck + new Vector3(0.06f, 0.22f, 0.142f), new Vector3(0.036f, 0.026f, 0.01f));
        }

        static void Boots(MeshBuilder b, float x, float z, Color color)
        {
            Paint(b, Pal.Leather, color);
            b.Box(new Vector3(x, 0.09f, z + 0.04f), new Vector3(0.17f, 0.18f, 0.32f));
            b.Box(new Vector3(x, 0.3f, z), new Vector3(0.16f, 0.3f, 0.2f));
        }

        // ---- The Rope and Lantern ----------------------------------------------------------------------

        /// <summary>Aggie Voss: linen shirt with the sleeves rolled, leather apron and keys, a mug and a rag in her hands.</summary>
        public static void Landlord(MeshBuilder b)
        {
            var rng = new System.Random(101);
            Paint(b, Pal.ClothDark, Skirt);
            b.Prism(new Vector3(0f, 0.02f, 0f), 0.4f, 0.25f, 0.96f, 8, true, false, 0.2f);
            Paint(b, Pal.ClothDark, Linen);
            b.Box(new Vector3(0f, 1.24f, 0f), new Vector3(0.44f, 0.58f, 0.26f));

            Paint(b, Pal.Leather, Apron);
            b.Box(new Vector3(0f, 1.2f, 0.14f), new Vector3(0.34f, 0.46f, 0.03f));
            b.Push(new Vector3(0f, 0.56f, 0.31f), Quaternion.Euler(-6f, 0f, 0f), Vector3.one);
            b.Box(Vector3.zero, new Vector3(0.56f, 0.82f, 0.04f));
            b.Pop();
            foreach (float x in new[] { -0.15f, 0.15f })
                Limb(b, new Vector3(x, 1.42f, 0.14f), new Vector3(x * 0.9f, 1.55f, -0.13f), 0.05f, 0.03f);
            b.ShadedBox(new Vector3(0f, 0.95f, 0.02f), new Vector3(0.52f, 0.07f, 0.36f), Pal.Leather, rng, 0.2f, 0.3f);
            b.SetColor(Pal.Brass, 0.5f);
            b.Prism(new Vector3(-0.27f, 0.84f, 0.17f), 0.05f, 0.05f, 0.02f, 6);
            for (int i = 0; i < 3; i++) b.Box(new Vector3(-0.29f + i * 0.03f, 0.76f, 0.19f), new Vector3(0.015f, 0.12f, 0.015f));

            foreach (float side in new[] { -1f, 1f })
            {
                Paint(b, Pal.ClothDark, Linen * 0.92f);
                Limb(b, new Vector3(side * 0.27f, 1.48f, 0f), new Vector3(side * 0.3f, 1.16f, 0.1f), 0.14f, 0.14f);
                Paint(b, Pal.Leather, Skin);
                Limb(b, new Vector3(side * 0.3f, 1.16f, 0.1f), new Vector3(side * 0.12f, 1.1f, 0.34f), 0.11f, 0.11f);
                Hand(b, new Vector3(side * 0.09f, 1.1f, 0.38f), Skin);
            }
            b.SetColor(Pal.Steel, 0.42f);
            b.Prism(new Vector3(0f, 1.04f, 0.4f), 0.075f, 0.075f, 0.17f, 8);
            Paint(b, Pal.ClothDark, Linen * 1.15f);
            b.Box(new Vector3(0.1f, 0.92f, 0.42f), new Vector3(0.16f, 0.26f, 0.03f));

            Vector3 neck = new Vector3(0f, 1.53f, 0.01f);
            Head(b, neck, Skin);
            Paint(b, Pal.ClothDark, GreyHair);
            b.Box(neck + new Vector3(0f, 0.34f, -0.02f), new Vector3(0.29f, 0.09f, 0.29f));
            b.Box(neck + new Vector3(0f, 0.2f, -0.13f), new Vector3(0.29f, 0.26f, 0.06f));
            foreach (float side in new[] { -1f, 1f })
                b.Box(neck + new Vector3(side * 0.135f, 0.19f, -0.03f), new Vector3(0.045f, 0.3f, 0.17f));
            Paint(b, Pal.ClothDark, new Color(0.42f, 0.28f, 0.19f));
            b.Box(neck + new Vector3(0f, 0.43f, -0.1f), new Vector3(0.14f, 0.14f, 0.14f));
        }

        /// <summary>Sergeant Halloway at his table: dented plate, red sash, his head on his hand. Sits on a seat 0.5 high.</summary>
        public static void Sergeant(MeshBuilder b)
        {
            var rng = new System.Random(102);
            Color trousers = new Color(0.25f, 0.2f, 0.17f);
            foreach (float x in new[] { -0.15f, 0.15f })
            {
                Paint(b, Pal.ClothDark, trousers);
                Limb(b, new Vector3(x, 0.6f, 0f), new Vector3(x, 0.58f, 0.46f), 0.18f, 0.19f);
                Paint(b, Pal.Leather, Coat);
                Limb(b, new Vector3(x, 0.58f, 0.46f), new Vector3(x, 0.14f, 0.5f), 0.16f, 0.18f);
                Boots(b, x, 0.5f, Coat);
            }

            b.Push(new Vector3(0f, 0.62f, 0f), Quaternion.Euler(14f, 0f, 0f), Vector3.one);
            b.ShadedBox(new Vector3(0f, 0.04f, 0f), new Vector3(0.44f, 0.14f, 0.28f), Pal.Leather, rng, 0.22f, 0.3f);
            b.ShadedBox(new Vector3(0f, 0.3f, 0f), new Vector3(0.5f, 0.44f, 0.32f), Pal.SteelDark, rng, 0.42f, 0.52f);
            b.ShadedBox(new Vector3(0f, 0.3f, 0.17f), new Vector3(0.28f, 0.34f, 0.03f), Pal.Steel, rng, 0.24f, 0.32f);
            foreach (float side in new[] { -1f, 1f })
            {
                b.Push(new Vector3(side * 0.3f, 0.5f, 0f), Quaternion.Euler(0f, 0f, -side * 22f), Vector3.one);
                b.ShadedBox(Vector3.zero, new Vector3(0.26f, 0.13f, 0.34f), Pal.SteelDark, rng, 0.38f, 0.48f);
                b.Pop();
            }
            b.SetColor(Pal.ClothRed, 0.42f);
            Limb(b, new Vector3(-0.2f, 0.54f, 0.18f), new Vector3(0.22f, 0.06f, 0.18f), 0.15f, 0.03f);
            b.SetColor(Pal.ClothRed, 0.32f);
            b.Box(new Vector3(0.2f, -0.12f, 0.2f), new Vector3(0.18f, 0.36f, 0.03f));
            b.Pop();

            Vector3 neck = new Vector3(0f, 1.12f, 0.2f);
            Vector3 elbow = new Vector3(0.38f, 0.82f, 0.46f), hand = new Vector3(0.16f, 1.26f, 0.38f);
            b.SetColor(Pal.SteelDark, 0.38f);
            Limb(b, new Vector3(0.3f, 1.08f, 0.14f), elbow, 0.15f, 0.15f);
            Limb(b, elbow, hand, 0.13f, 0.13f);
            Hand(b, hand, AgedSkin);
            elbow = new Vector3(-0.36f, 0.86f, 0.38f);
            b.SetColor(Pal.SteelDark, 0.38f);
            Limb(b, new Vector3(-0.3f, 1.08f, 0.14f), elbow, 0.15f, 0.15f);
            Limb(b, elbow, new Vector3(-0.16f, 0.82f, 0.68f), 0.13f, 0.13f);
            Hand(b, new Vector3(-0.14f, 0.82f, 0.72f), AgedSkin);

            Head(b, neck, AgedSkin);
            Paint(b, Pal.ClothDark, GreyHair);
            b.Box(neck + new Vector3(0f, 0.33f, -0.01f), new Vector3(0.28f, 0.08f, 0.29f));
            b.Box(neck + new Vector3(0f, 0.2f, -0.12f), new Vector3(0.28f, 0.22f, 0.05f));
            foreach (float side in new[] { -1f, 1f })
                b.Box(neck + new Vector3(side * 0.13f, 0.21f, -0.02f), new Vector3(0.04f, 0.2f, 0.17f));
            b.Box(neck + new Vector3(0f, 0.09f, 0.14f), new Vector3(0.19f, 0.08f, 0.03f));
        }

        /// <summary>The Dark Wanderer at the table: layers of torn cloak, a hood with nothing to see in it, and his staff.</summary>
        public static void Wanderer(MeshBuilder b)
        {
            var rng = new System.Random(103);
            Paint(b, Pal.Shadow, Soot);
            b.Prism(new Vector3(0f, 0f, 0f), 0.54f, 0.3f, 1.14f, 8, false, false, 0.2f);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                float length = MeshBuilder.Lerp(rng, 0.34f, 0.6f);
                Paint(b, Pal.Shadow, Soot * MeshBuilder.Lerp(rng, 0.85f, 1.35f));
                b.Push(new Vector3(Mathf.Cos(a) * 0.45f, 0.72f, Mathf.Sin(a) * 0.45f), Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f), Vector3.one);
                b.Box(new Vector3(0f, -length * 0.5f, 0f), new Vector3(0.2f, length, 0.04f));
                b.Pop();
            }
            Paint(b, Pal.Shadow, Soot * 1.25f);
            b.Prism(new Vector3(0f, 0.86f, 0f), 0.5f, 0.2f, 0.46f, 8, false, false, 0.2f);
            Paint(b, Pal.Shadow, Soot * 1.5f);
            b.Prism(new Vector3(0f, 1.2f, 0.03f), 0.27f, 0.06f, 0.56f, 8, true, true, 0.2f);
            b.SetColor(Pal.Black, 0.25f);
            b.Box(new Vector3(0f, 1.38f, 0.19f), new Vector3(0.18f, 0.2f, 0.06f));

            Paint(b, Pal.Shadow, Soot * 1.15f);
            Limb(b, new Vector3(0.24f, 1.12f, 0.1f), new Vector3(0.22f, 0.86f, 0.5f), 0.15f, 0.15f);
            Limb(b, new Vector3(-0.24f, 1.12f, 0.1f), new Vector3(-0.2f, 0.86f, 0.5f), 0.15f, 0.15f);
            Hand(b, new Vector3(0.2f, 0.84f, 0.56f), Coat);
            Hand(b, new Vector3(-0.18f, 0.84f, 0.56f), Coat);

            b.SetColor(Pal.Bark, 0.42f);
            Limb(b, new Vector3(0.6f, 0f, -0.24f), new Vector3(0.74f, 2f, -0.1f), 0.06f, 0.06f);
            b.ShadedBox(new Vector3(-0.44f, 0.56f, 0.04f), new Vector3(0.1f, 0.3f, 0.34f), Pal.Leather, rng, 0.3f, 0.4f);
        }

        // ---- Gallows Market ---------------------------------------------------------------------------

        /// <summary>A hem torn into strips, round a skirt or an apron.</summary>
        static void Tatters(MeshBuilder b, System.Random rng, Vector3 centre, float radius, float depth, int count, Pal texture, Color color, float minLength, float maxLength)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                float length = MeshBuilder.Lerp(rng, minLength, maxLength);
                Paint(b, texture, color * MeshBuilder.Lerp(rng, 0.85f, 1.15f));
                b.Push(centre + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * depth), Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f), Vector3.one);
                b.Box(new Vector3(0f, -length * 0.5f, 0f), new Vector3(radius * 0.46f, length, 0.03f));
                b.Pop();
            }
        }

        /// <summary>Iron Mabb: a long apron gone to rags, a jerkin and a satchel, both hands folded on the shovel's handle.</summary>
        public static void Gravedigger(MeshBuilder b)
        {
            var rng = new System.Random(104);
            Color trousers = new Color(0.28f, 0.24f, 0.2f);
            foreach (float x in new[] { -0.14f, 0.14f })
            {
                Paint(b, Pal.ClothDark, trousers);
                b.Box(new Vector3(x, 0.66f, 0f), new Vector3(0.18f, 0.72f, 0.2f));
                Boots(b, x, 0f, Coat);
            }
            Paint(b, Pal.ClothDark, Linen * 0.8f);
            b.Box(new Vector3(0f, 1.24f, 0f), new Vector3(0.48f, 0.6f, 0.28f));
            b.ShadedBox(new Vector3(0f, 1.24f, 0f), new Vector3(0.5f, 0.56f, 0.3f), Pal.Leather, rng, 0.26f, 0.34f);
            b.ShadedBox(new Vector3(0f, 0.97f, 0f), new Vector3(0.52f, 0.08f, 0.32f), Pal.Leather, rng, 0.16f, 0.22f);
            b.ShadedBox(new Vector3(-0.3f, 0.82f, 0.05f), new Vector3(0.1f, 0.26f, 0.24f), Pal.Leather, rng, 0.3f, 0.38f);

            // The apron, down past the knee, and its hem.
            Paint(b, Pal.Leather, Apron * 1.25f);
            b.Prism(new Vector3(0f, 0.44f, 0.02f), 0.38f, 0.29f, 0.54f, 8, false, false, 0.2f);
            Tatters(b, rng, new Vector3(0f, 0.46f, 0.02f), 0.36f, 0.3f, 13, Pal.Leather, Apron * 1.25f, 0.12f, 0.34f);

            Vector3 handle = new Vector3(0f, 1.34f, 0.44f);
            foreach (float side in new[] { -1f, 1f })
            {
                Paint(b, Pal.ClothDark, Linen * 0.8f);
                Limb(b, new Vector3(side * 0.31f, 1.48f, 0f), new Vector3(side * 0.34f, 1.18f, 0.16f), 0.16f, 0.16f);
                Paint(b, Pal.Leather, AgedSkin);
                Limb(b, new Vector3(side * 0.34f, 1.18f, 0.16f), handle + new Vector3(side * 0.05f, 0f, -0.02f), 0.13f, 0.13f);
                Hand(b, handle + new Vector3(side * 0.05f, 0.02f, 0f), AgedSkin);
            }
            b.SetColor(Pal.Wood, 0.42f);
            Limb(b, handle + Vector3.up * 0.05f, new Vector3(0f, 0.28f, 0.54f), 0.06f, 0.06f);
            b.ShadedBox(new Vector3(0f, 0.15f, 0.55f), new Vector3(0.26f, 0.3f, 0.04f), Pal.SteelDark, rng, 0.4f, 0.5f);

            Vector3 neck = new Vector3(0f, 1.53f, 0.01f);
            Head(b, neck, AgedSkin);
            Paint(b, Pal.ClothDark, DarkHair);
            b.Box(neck + new Vector3(0f, 0.34f, -0.02f), new Vector3(0.29f, 0.08f, 0.29f));
            b.Box(neck + new Vector3(0f, 0.12f, -0.13f), new Vector3(0.3f, 0.42f, 0.06f));
            foreach (float side in new[] { -1f, 1f })
                b.Box(neck + new Vector3(side * 0.14f, 0.15f, -0.03f), new Vector3(0.05f, 0.36f, 0.17f));
            Paint(b, Pal.ClothDark, GreyHair);
            b.Box(neck + new Vector3(0f, 0.04f, 0.13f), new Vector3(0.24f, 0.22f, 0.07f));
        }

        /// <summary>Wick: a flat cap, a red scarf, a patched coat and the morning's notice held up in both hands.</summary>
        public static void Crier(MeshBuilder b)
        {
            var rng = new System.Random(105);
            foreach (float x in new[] { -0.13f, 0.13f })
            {
                Paint(b, Pal.ClothDark, Soot * 1.5f);
                b.Box(new Vector3(x, 0.6f, 0f), new Vector3(0.17f, 0.7f, 0.19f));
                Boots(b, x, 0f, Coat);
            }
            Paint(b, Pal.Leather, Coat);
            b.Prism(new Vector3(0f, 0.36f, 0f), 0.36f, 0.27f, 0.68f, 8, false, false, 0.2f);
            b.ShadedBox(new Vector3(0f, 1.24f, 0f), new Vector3(0.48f, 0.56f, 0.28f), Pal.Leather, rng, 0.3f, 0.36f);
            b.SetColor(Pal.ClothRed, 0.3f);
            b.Prism(new Vector3(0f, 1.46f, 0f), 0.24f, 0.17f, 0.12f, 8, false, true, 0.2f);
            b.Box(new Vector3(0.08f, 1.26f, 0.16f), new Vector3(0.1f, 0.34f, 0.04f));

            Vector3 paper = new Vector3(0f, 1.24f, 0.4f);
            foreach (float side in new[] { -1f, 1f })
            {
                Paint(b, Pal.Leather, Coat * 1.1f);
                Limb(b, new Vector3(side * 0.3f, 1.46f, 0f), new Vector3(side * 0.34f, 1.14f, 0.2f), 0.15f, 0.15f);
                Limb(b, new Vector3(side * 0.34f, 1.14f, 0.2f), paper + new Vector3(side * 0.2f, -0.1f, -0.03f), 0.12f, 0.12f);
                Hand(b, paper + new Vector3(side * 0.19f, -0.1f, 0f), AgedSkin);
            }
            b.Push(paper, Quaternion.Euler(-12f, 0f, 0f), Vector3.one);
            Paint(b, Pal.Plaster, new Color(0.78f, 0.66f, 0.44f));
            b.Box(Vector3.zero, new Vector3(0.38f, 0.5f, 0.02f));
            b.Pop();

            Vector3 neck = new Vector3(0f, 1.53f, 0.01f);
            Head(b, neck, AgedSkin);
            Paint(b, Pal.ClothDark, GreyHair);
            b.Box(neck + new Vector3(0f, 0.09f, 0.14f), new Vector3(0.14f, 0.06f, 0.03f));
            foreach (float side in new[] { -1f, 1f })
                b.Box(neck + new Vector3(side * 0.14f, 0.19f, -0.04f), new Vector3(0.04f, 0.2f, 0.16f));
            Paint(b, Pal.Leather, Coat * 0.95f);
            b.Prism(neck + new Vector3(0f, 0.32f, 0.02f), 0.2f, 0.22f, 0.1f, 8);
            b.Box(neck + new Vector3(0f, 0.34f, 0.19f), new Vector3(0.26f, 0.03f, 0.12f));

            // The bag on its strap, the torn hem, and the patches.
            Paint(b, Pal.Leather, Coat * 0.85f);
            b.Box(new Vector3(-0.3f, 0.88f, 0.05f), new Vector3(0.09f, 0.3f, 0.3f));
            Limb(b, new Vector3(0.2f, 1.5f, 0.15f), new Vector3(-0.28f, 1.02f, 0.15f), 0.05f, 0.02f);
            Tatters(b, rng, new Vector3(0f, 0.36f, 0f), 0.35f, 0.26f, 12, Pal.Leather, Coat, 0.08f, 0.24f);
            b.SetColor(Pal.ClothRed, 0.3f);
            b.Box(new Vector3(0.14f, 0.62f, 0.3f), new Vector3(0.18f, 0.2f, 0.03f));
            Paint(b, Pal.ClothDark, Linen * 0.7f);
            b.Box(new Vector3(-0.1f, 0.84f, 0.28f), new Vector3(0.16f, 0.15f, 0.03f));
        }

        /// <summary>The Waiting Widow: a dark knitted shawl over a hood, a fringe of wool, and an old face bent over her stall.</summary>
        public static void Widow(MeshBuilder b)
        {
            var rng = new System.Random(106);
            Paint(b, Pal.Shadow, Soot * 1.5f);
            b.Prism(new Vector3(0f, 0f, 0f), 0.42f, 0.25f, 1.1f, 8, false, false, 0.2f);
            Paint(b, Pal.Shadow, Soot * 1.9f);
            b.Prism(new Vector3(0f, 0.92f, 0f), 0.42f, 0.2f, 0.62f, 8, false, false, 0.2f);
            Tatters(b, rng, new Vector3(0f, 0.94f, 0f), 0.41f, 0.41f, 16, Pal.Shadow, Soot * 1.7f, 0.1f, 0.3f);
            Vector3 neck = new Vector3(0f, 1.44f, 0.02f);
            Head(b, neck, AgedSkin);
            Paint(b, Pal.ClothDark, GreyHair);
            b.Box(neck + new Vector3(0f, 0.34f, 0.02f), new Vector3(0.26f, 0.05f, 0.25f));
            Paint(b, Pal.Shadow, Soot * 1.9f);
            b.Box(neck + new Vector3(0f, 0.39f, -0.03f), new Vector3(0.34f, 0.08f, 0.34f));
            b.Box(neck + new Vector3(0f, 0.17f, -0.17f), new Vector3(0.34f, 0.38f, 0.07f));
            foreach (float side in new[] { -1f, 1f })
                b.Box(neck + new Vector3(side * 0.165f, 0.18f, -0.02f), new Vector3(0.06f, 0.4f, 0.28f));
            foreach (float side in new[] { -1f, 1f })
            {
                Paint(b, Pal.Shadow, Soot * 1.7f);
                Limb(b, new Vector3(side * 0.27f, 1.34f, 0f), new Vector3(side * 0.14f, 0.96f, 0.28f), 0.14f, 0.14f);
                Hand(b, new Vector3(side * 0.08f, 0.94f, 0.32f), AgedSkin);
            }
        }

        // ---- The Cold Forge ---------------------------------------------------------------------------

        /// <summary>The smith at the anvil: a broad back in a rolled-up shirt, a leather apron, and the hammer coming down.</summary>
        public static void Smith(MeshBuilder b)
        {
            var rng = new System.Random(107);
            Color shirt = new Color(0.46f, 0.38f, 0.3f);
            foreach (float x in new[] { -0.16f, 0.16f })
            {
                Paint(b, Pal.ClothDark, Soot * 2f);
                b.Box(new Vector3(x, 0.66f, 0f), new Vector3(0.2f, 0.72f, 0.22f));
                Boots(b, x, 0f, Coat);
            }
            Paint(b, Pal.ClothDark, shirt);
            b.Box(new Vector3(0f, 1.28f, 0f), new Vector3(0.58f, 0.62f, 0.3f));

            // The apron, with its straps and a burn or two.
            b.ShadedBox(new Vector3(0f, 1.16f, 0.17f), new Vector3(0.46f, 0.64f, 0.03f), Pal.Leather, rng, 0.2f, 0.3f);
            b.ShadedBox(new Vector3(0f, 0.6f, 0.2f), new Vector3(0.56f, 0.56f, 0.04f), Pal.Leather, rng, 0.18f, 0.28f);
            foreach (float x in new[] { -0.17f, 0.17f })
                Limb(b, new Vector3(x, 1.48f, 0.17f), new Vector3(x * 0.9f, 1.6f, -0.15f), 0.06f, 0.03f);
            b.ShadedBox(new Vector3(0f, 0.97f, 0f), new Vector3(0.6f, 0.08f, 0.34f), Pal.Leather, rng, 0.14f, 0.2f);

            // The left hand holds the work down with the tongs; the right brings the hammer onto it.
            Paint(b, Pal.ClothDark, shirt * 0.92f);
            Limb(b, new Vector3(-0.36f, 1.54f, 0f), new Vector3(-0.38f, 1.24f, 0.2f), 0.18f, 0.18f);
            Paint(b, Pal.Leather, Skin);
            Limb(b, new Vector3(-0.38f, 1.24f, 0.2f), new Vector3(-0.24f, 1.16f, 0.58f), 0.14f, 0.14f);
            Hand(b, new Vector3(-0.22f, 1.16f, 0.62f), Skin);
            b.SetColor(Pal.SteelDark, 0.3f);
            Limb(b, new Vector3(-0.22f, 1.16f, 0.66f), new Vector3(-0.08f, 1.2f, 1.1f), 0.03f, 0.03f);

            Vector3 neck = new Vector3(0f, 1.56f, 0.05f);
            Head(b, neck, Skin);
            Paint(b, Pal.ClothDark, DarkHair);
            b.Box(neck + new Vector3(0f, 0.35f, -0.01f), new Vector3(0.29f, 0.09f, 0.29f));
            b.Box(neck + new Vector3(0f, 0.22f, -0.13f), new Vector3(0.29f, 0.24f, 0.06f));
            b.Box(neck + new Vector3(0f, 0.08f, 0.13f), new Vector3(0.25f, 0.13f, 0.07f));
            foreach (float side in new[] { -1f, 1f })
                b.Box(neck + new Vector3(side * 0.135f, 0.16f, 0.02f), new Vector3(0.04f, 0.2f, 0.16f));
        }

        /// <summary>The right arm and hammer, local to the shoulder, so the blow is independent of the breathing body.</summary>
        public static void SmithHammerArm(MeshBuilder b)
        {
            var rng = new System.Random(109);
            b.Push(-new Vector3(0.36f, 1.54f, 0f), Quaternion.identity, Vector3.one);
            Paint(b, Pal.ClothDark, new Color(0.46f, 0.38f, 0.3f) * 0.92f);
            Limb(b, new Vector3(0.36f, 1.54f, 0f), new Vector3(0.42f, 1.5f, 0.3f), 0.18f, 0.18f);
            Paint(b, Pal.Leather, Skin);
            Limb(b, new Vector3(0.42f, 1.5f, 0.3f), new Vector3(0.3f, 1.42f, 0.66f), 0.14f, 0.14f);
            Hand(b, new Vector3(0.28f, 1.42f, 0.7f), Skin);
            b.SetColor(Pal.Wood, 0.4f);
            Limb(b, new Vector3(0.28f, 1.36f, 0.68f), new Vector3(0.22f, 1.54f, 1.02f), 0.045f, 0.045f);
            b.ShadedBox(new Vector3(0.22f, 1.54f, 1.06f), new Vector3(0.14f, 0.26f, 0.16f), Pal.SteelDark, rng, 0.4f, 0.5f);
            b.Pop();
        }

        public static void CaravanDriver(MeshBuilder b)
        {
            CreatureMeshes.Figure(b, EnemyFactory.Villager("CaravanDriver", Pal.Leather, Pal.Thatch));
            b.SetColor(Pal.Leather, 0.42f);
            b.Box(new Vector3(0.3f, 0.7f, -0.1f), new Vector3(0.25f, 0.34f, 0.21f));
        }

        public static void CaravanGuard(MeshBuilder b)
        {
            CreatureMeshes.Figure(b, EnemyFactory.Villager("CaravanGuard", Pal.ClothDark, Pal.SteelDark));
            b.Push(new Vector3(0.42f, 0.95f, 0.13f), Quaternion.Euler(0f, 0f, -8f), Vector3.one);
            b.SetColor(Pal.WoodDark, 0.4f);
            b.Prism(new Vector3(0f, -0.8f, 0f), 0.025f, 0.02f, 1.85f, 6);
            b.SetColor(Pal.Steel, 0.5f);
            b.Prism(new Vector3(0f, 1.05f, 0f), 0.075f, 0f, 0.22f, 4);
            b.Pop();
        }

        // ---- The traders below ------------------------------------------------------------------------

        /// <summary>
        /// A recognisable face, broad traveller's hat, patched coat and stock ledger. Outfit zero is the city trader;
        /// the five others carry the dust, damp and tools of their own depth. The right hand works on a separate pivot.
        /// </summary>
        public static void Merchant(MeshBuilder b, int outfit = 0)
        {
            Color cloth = MerchantCloth(outfit);
            Color scarf = outfit == 2 ? new Color(0.75f, 0.65f, 0.38f) : outfit == 3 ? new Color(0.68f, 0.52f, 0.32f) :
                outfit == 5 ? new Color(0.67f, 0.58f, 0.62f) : new Color(0.65f, 0.24f, 0.18f);
            foreach (float x in new[] { -0.15f, 0.15f })
            {
                Paint(b, Pal.ClothDark, new Color(0.28f, 0.27f, 0.24f));
                b.Box(new Vector3(x, 0.56f, 0f), new Vector3(0.2f, 0.7f, 0.23f));
                Boots(b, x, 0f, new Color(0.32f, 0.2f, 0.12f));
            }
            Paint(b, Pal.ClothDark, cloth);
            b.Box(new Vector3(0f, 1.2f, 0f), new Vector3(0.54f, 0.64f, 0.33f));
            b.Box(new Vector3(-0.19f, 0.81f, 0.02f), new Vector3(0.22f, 0.34f, 0.36f));
            b.Box(new Vector3(0.19f, 0.81f, 0.02f), new Vector3(0.22f, 0.34f, 0.36f));
            Paint(b, Pal.ClothDark, scarf);
            b.Box(new Vector3(0f, 1.49f, 0.08f), new Vector3(0.44f, 0.13f, 0.35f));
            b.Box(new Vector3(-0.12f, 1.31f, 0.2f), new Vector3(0.15f, 0.34f, 0.045f));
            Paint(b, Pal.Leather, new Color(0.34f, 0.22f, 0.13f));
            b.Box(new Vector3(0f, 1f, 0f), new Vector3(0.58f, 0.08f, 0.37f));
            b.Box(new Vector3(0.31f, 0.86f, 0.02f), new Vector3(0.2f, 0.27f, 0.2f));
            b.Box(new Vector3(0f, 1.2f, -0.23f), new Vector3(0.41f, 0.53f, 0.19f));
            Paint(b, Pal.Thatch, Linen);
            Limb(b, new Vector3(-0.2f, 1.51f, -0.12f), new Vector3(-0.09f, 1.01f, 0.19f), 0.07f, 0.035f);
            b.SetColor(Pal.Brass, 0.65f);
            b.Box(new Vector3(0.02f, 1f, 0.2f), new Vector3(0.11f, 0.1f, 0.025f));

            Paint(b, Pal.ClothDark, cloth * 0.95f);
            Limb(b, new Vector3(-0.32f, 1.45f, 0f), new Vector3(-0.37f, 1.18f, 0.1f), 0.17f, 0.17f);
            Paint(b, Pal.Leather, Skin);
            Limb(b, new Vector3(-0.37f, 1.18f, 0.1f), new Vector3(-0.16f, 1.12f, 0.43f), 0.105f, 0.105f);
            Hand(b, new Vector3(-0.15f, 1.12f, 0.46f), Skin);
            Paint(b, Pal.Leather, new Color(0.32f, 0.17f, 0.12f));
            b.Box(new Vector3(-0.16f, 1.15f, 0.48f), new Vector3(0.32f, 0.045f, 0.27f));
            Paint(b, Pal.Bone, new Color(0.8f, 0.71f, 0.52f));
            b.Box(new Vector3(-0.16f, 1.18f, 0.48f), new Vector3(0.27f, 0.018f, 0.23f));

            Vector3 neck = new Vector3(0f, 1.53f, 0.01f);
            Head(b, neck, AgedSkin * 1.08f);
            Paint(b, Pal.ClothDark, GreyHair);
            b.Box(neck + new Vector3(0f, 0.1f, 0.14f), new Vector3(0.23f, 0.13f, 0.05f));
            b.Box(neck + new Vector3(0f, 0.27f, -0.13f), new Vector3(0.28f, 0.18f, 0.07f));
            Paint(b, Pal.Leather, outfit == 3 ? new Color(0.24f, 0.34f, 0.36f) : cloth * 0.82f);
            b.Prism(new Vector3(0f, 1.85f, 0.01f), 0.37f, 0.37f, 0.045f, 10);
            b.Prism(new Vector3(0f, 1.88f, 0.01f), 0.19f, 0.14f, 0.23f, 8);
            Paint(b, Pal.ClothDark, scarf);
            b.Prism(new Vector3(0f, 1.91f, 0.01f), 0.192f, 0.18f, 0.065f, 8);
            if (outfit == 2)
            {
                // The spore gatherer's hood and a pale cap pinned to the hat.
                b.SetColor(Pal.FungalCap, 0.67f);
                b.Prism(new Vector3(-0.21f, 1.92f, -0.07f), 0.16f, 0.055f, 0.12f, 8);
                b.SetColor(Pal.FungalGlow, 0.48f);
                b.Box(new Vector3(-0.21f, 1.89f, -0.07f), new Vector3(0.06f, 0.12f, 0.06f));
            }
            else if (outfit == 3)
            {
                Paint(b, Pal.Thatch, new Color(0.7f, 0.58f, 0.39f));
                b.Prism(new Vector3(0.28f, 0.87f, 0.15f), 0.14f, 0.14f, 0.055f, 10);
                b.SetColor(Pal.Steel, 0.6f);
                b.Box(new Vector3(0.33f, 0.88f, 0.2f), new Vector3(0.04f, 0.18f, 0.04f));
            }
            else if (outfit == 4)
            {
                // Ash mask below visible brass goggles, rather than a featureless black hood.
                Paint(b, Pal.ClothDark, new Color(0.61f, 0.53f, 0.39f));
                b.Box(neck + new Vector3(0f, 0.13f, 0.155f), new Vector3(0.24f, 0.1f, 0.06f));
                foreach (float x in new[] { -0.065f, 0.065f })
                {
                    b.SetColor(Pal.Brass, 0.65f);
                    b.Box(neck + new Vector3(x, 0.24f, 0.151f), new Vector3(0.105f, 0.08f, 0.035f));
                    b.SetColor(Pal.Black, 0.25f);
                    b.Box(neck + new Vector3(x, 0.24f, 0.173f), new Vector3(0.065f, 0.045f, 0.015f));
                }
            }
            else if (outfit == 5 || outfit == 1)
            {
                b.SetColor(Pal.Bone, 0.7f);
                b.Box(new Vector3(0.09f, 1.31f, 0.19f), new Vector3(0.055f, 0.19f, 0.04f));
                b.Box(new Vector3(0.09f, 1.35f, 0.19f), new Vector3(0.14f, 0.045f, 0.04f));
            }
        }

        static Color MerchantCloth(int outfit) => outfit switch
        {
            1 => new Color(0.43f, 0.32f, 0.5f),
            2 => new Color(0.28f, 0.46f, 0.38f),
            3 => new Color(0.31f, 0.43f, 0.57f),
            4 => new Color(0.55f, 0.32f, 0.23f),
            5 => new Color(0.5f, 0.4f, 0.44f),
            _ => new Color(0.48f, 0.34f, 0.21f)
        };

        public static void MerchantWorkingArm(MeshBuilder b, int outfit)
        {
            b.Push(-new Vector3(0.32f, 1.45f, 0f), Quaternion.identity, Vector3.one);
            Paint(b, Pal.ClothDark, MerchantCloth(outfit) * 0.95f);
            Limb(b, new Vector3(0.32f, 1.45f, 0f), new Vector3(0.37f, 1.21f, 0.15f), 0.17f, 0.17f);
            Paint(b, Pal.Leather, AgedSkin * 1.08f);
            Limb(b, new Vector3(0.37f, 1.21f, 0.15f), new Vector3(0.22f, 1.08f, 0.48f), 0.105f, 0.105f);
            Hand(b, new Vector3(0.21f, 1.08f, 0.5f), AgedSkin * 1.08f);
            b.SetColor(Pal.Brass, 0.7f);
            b.Box(new Vector3(0.21f, 1.04f, 0.53f), new Vector3(0.09f, 0.025f, 0.09f));
            b.Pop();
        }

        // ---- The Grey Chapel --------------------------------------------------------------------------

        /// <summary>One of the Choir: grey wool to the floor, a pointed hood with nothing to see in it, a rope at the waist.</summary>
        public static void Monk(MeshBuilder b)
        {
            var rng = new System.Random(108);
            Color wool = new Color(0.3f, 0.29f, 0.3f);
            Paint(b, Pal.ClothDark, wool);
            b.Prism(Vector3.zero, 0.42f, 0.28f, 1.02f, 8, false, false, 0.2f);
            b.Prism(new Vector3(0f, 1.02f, 0f), 0.28f, 0.32f, 0.5f, 8, false, false, 0.2f);
            Tatters(b, rng, new Vector3(0f, 0.2f, 0f), 0.41f, 0.41f, 11, Pal.ClothDark, wool * 0.9f, 0.14f, 0.2f);

            Paint(b, Pal.Thatch, new Color(0.56f, 0.48f, 0.36f));
            b.Prism(new Vector3(0f, 1f, 0f), 0.3f, 0.3f, 0.05f, 8, false, false, 0.2f);
            b.Box(new Vector3(0.1f, 0.7f, 0.29f), new Vector3(0.03f, 0.6f, 0.03f));
            b.Box(new Vector3(0.2f, 0.78f, 0.28f), new Vector3(0.03f, 0.44f, 0.03f));

            // The short cape over the shoulders, its edge cut in points.
            Paint(b, Pal.ClothDark, wool * 1.2f);
            b.Prism(new Vector3(0f, 1.28f, 0f), 0.42f, 0.2f, 0.28f, 8, false, false, 0.2f);
            Tatters(b, rng, new Vector3(0f, 1.3f, 0f), 0.41f, 0.41f, 11, Pal.ClothDark, wool * 1.15f, 0.08f, 0.16f);

            foreach (float side in new[] { -1f, 1f })
            {
                Paint(b, Pal.ClothDark, wool * 1.08f);
                Limb(b, new Vector3(side * 0.3f, 1.42f, 0.02f), new Vector3(side * 0.3f, 1.12f, 0.22f), 0.16f, 0.16f);
                Limb(b, new Vector3(side * 0.3f, 1.12f, 0.22f), new Vector3(side * 0.16f, 1.2f, 0.5f), 0.16f, 0.16f);
                Hand(b, new Vector3(side * 0.14f, 1.2f, 0.56f), new Color(0.5f, 0.48f, 0.48f));
            }

            Paint(b, Pal.ClothDark, wool * 1.15f);
            b.Prism(new Vector3(0f, 1.5f, 0.02f), 0.22f, 0.17f, 0.32f, 8, false, false, 0.2f);
            b.Prism(new Vector3(0f, 1.82f, 0f), 0.17f, 0f, 0.2f, 8, false, false, 0.2f);
            b.Box(new Vector3(0f, 1.72f, -0.14f), new Vector3(0.14f, 0.3f, 0.1f));
            b.SetColor(Pal.Black, 0.2f);
            b.Box(new Vector3(0f, 1.62f, 0.15f), new Vector3(0.15f, 0.2f, 0.05f));
        }
    }
}
