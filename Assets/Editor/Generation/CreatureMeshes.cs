using UnityEngine;

namespace AshenRealm.EditorTools
{
    /// <summary>How a humanoid of the Deep Dark is dressed. One style per enemy family; parts are authored facing +Z.</summary>
    internal sealed class HumanoidStyle
    {
        public string Name;
        public Pal Skin = Pal.Bone;
        public Pal Cloth = Pal.Robe;
        public Pal Trim = Pal.Brass;
        public Pal Eyes = Pal.Glow;
        /// <summary>Long skirt and hood instead of bare limbs.</summary>
        public bool Robed;
        /// <summary>Ribs and thin bones instead of flesh.</summary>
        public bool Skeletal;
        public bool Hooded;
        public bool Crowned;
        /// <summary>Glowing seams, for the kiln-born.</summary>
        public bool Molten;
        public HeldItem Held = HeldItem.None;
    }

    internal enum HeldItem { None, Club, Bow, Staff, Scythe, GreatSword }

    /// <summary>Procedural parts for every creature that is not the Cursed Knight, in the blocky pixel-art language of the knight.</summary>
    internal static class CreatureMeshes
    {
        static float R(System.Random rng, float min, float max) => MeshBuilder.Lerp(rng, min, max);

        // string.GetHashCode is not stable between runs; generation must be.
        static int Seed(HumanoidStyle s)
        {
            int seed = 17;
            foreach (char c in s.Name) seed = seed * 31 + c;
            return seed & 0xffff;
        }

        // ---- Humanoids: pivots match the knight rig, so KnightAnimator drives them unchanged ----

        public static void Torso(MeshBuilder b, HumanoidStyle s)
        {
            var rng = new System.Random(Seed(s));
            if (s.Skeletal)
            {
                b.SetColor(s.Skin, 0.45f);
                b.Prism(new Vector3(0f, 0f, 0f), 0.05f, 0.05f, 0.34f, 5);                 // spine
                for (int i = 0; i < 4; i++)
                {
                    float y = 0.3f + i * 0.09f;
                    b.ShadedBox(new Vector3(0f, y, 0.02f), new Vector3(0.42f - i * 0.02f, 0.05f, 0.24f), s.Skin, rng, 0.4f, 0.6f);
                }
                b.ShadedBox(new Vector3(0f, 0.68f, 0f), new Vector3(0.5f, 0.06f, 0.16f), s.Skin, rng, 0.5f, 0.65f);   // collar bones
                b.ShadedBox(new Vector3(0f, 0.02f, 0f), new Vector3(0.3f, 0.1f, 0.16f), s.Skin, rng, 0.4f, 0.55f);     // pelvis
            }
            else if (s.Robed)
            {
                b.SetColor(s.Cloth, 0.42f);
                b.Prism(new Vector3(0f, -1f, 0f), 0.42f, 0.26f, 1.02f, 7, true, false, 0.2f);   // skirt to the floor
                b.SetColor(s.Cloth, 0.5f);
                b.Prism(new Vector3(0f, 0.02f, 0f), 0.26f, 0.32f, 0.62f, 7, false, false, 0.2f);
                b.ShadedBox(new Vector3(0f, 0.04f, 0f), new Vector3(0.58f, 0.08f, 0.5f), s.Trim, rng, 0.3f, 0.4f);      // sash
                b.ShadedBox(new Vector3(0f, 0.3f, 0.24f), new Vector3(0.1f, 0.5f, 0.05f), s.Trim, rng, 0.35f, 0.5f);    // stole
            }
            else
            {
                b.ShadedBox(new Vector3(0f, 0.1f, 0f), new Vector3(0.42f, 0.24f, 0.26f), s.Skin, rng, 0.35f, 0.45f);
                b.ShadedBox(new Vector3(0f, 0.45f, 0f), new Vector3(0.56f, 0.46f, 0.32f), s.Skin, rng, 0.42f, 0.55f);
                b.ShadedBox(new Vector3(0f, 0.02f, 0f), new Vector3(0.46f, 0.1f, 0.3f), s.Cloth, rng, 0.3f, 0.4f);
                for (int i = 0; i < 3; i++)
                {
                    float x0 = -0.2f + i * 0.14f, x1 = x0 + 0.14f;
                    b.SetColor(s.Cloth, 0.3f);
                    b.DoubleSided(new Vector3(x0, 0.0f, 0.16f), new Vector3(x1, 0.0f, 0.16f), new Vector3((x0 + x1) * 0.5f, -0.4f - i * 0.05f, 0.2f), Vector3.forward);
                }
            }

            if (s.Molten)
            {
                b.SetColor(Pal.Lava, 0.7f);
                b.Box(new Vector3(0.1f, 0.42f, 0.17f), new Vector3(0.05f, 0.36f, 0.02f));
                b.Box(new Vector3(-0.14f, 0.5f, 0.17f), new Vector3(0.04f, 0.24f, 0.02f));
                b.Box(new Vector3(0f, 0.24f, 0.17f), new Vector3(0.3f, 0.04f, 0.02f));
            }

            if (s.Crowned || s.Molten)
            {
                for (int i = -1; i <= 1; i += 2)
                {
                    b.Push(new Vector3(0.36f * i, 0.68f, 0f), Quaternion.Euler(0f, 0f, -18f * i), Vector3.one);
                    b.ShadedBox(Vector3.zero, new Vector3(0.28f, 0.16f, 0.36f), s.Skin, rng, 0.5f, 0.65f);
                    b.ShadedBox(new Vector3(0f, -0.09f, 0f), new Vector3(0.26f, 0.06f, 0.34f), s.Trim, rng, 0.3f, 0.4f);
                    b.Pop();
                }
            }
        }

        public static void Head(MeshBuilder b, HumanoidStyle s)
        {
            var rng = new System.Random(Seed(s) + 1);
            if (s.Hooded)
            {
                b.SetColor(s.Cloth, 0.5f);
                b.Prism(new Vector3(0f, -0.04f, 0f), 0.24f, 0.13f, 0.36f, 7, true, true, 0.2f);
                b.SetColor(Pal.Black, 0.3f);
                b.Box(new Vector3(0f, 0.12f, 0.17f), new Vector3(0.22f, 0.2f, 0.06f));         // shadowed face
            }
            else
            {
                b.ShadedBox(new Vector3(0f, 0.14f, 0f), new Vector3(0.26f, 0.28f, 0.28f), s.Skin, rng, 0.45f, 0.6f);
                b.ShadedBox(new Vector3(0f, 0.0f, 0.02f), new Vector3(0.18f, 0.08f, 0.22f), s.Skin, rng, 0.35f, 0.45f);   // jaw
                b.SetColor(Pal.Black, 0.3f);
                b.Box(new Vector3(-0.07f, 0.16f, 0.14f), new Vector3(0.07f, 0.07f, 0.02f));
                b.Box(new Vector3(0.07f, 0.16f, 0.14f), new Vector3(0.07f, 0.07f, 0.02f));
            }

            if (s.Crowned)
            {
                for (int i = 0; i < 7; i++)
                {
                    float a = i * Mathf.PI * 2f / 7f;
                    Vector3 p = new Vector3(Mathf.Cos(a) * 0.17f, 0.28f, Mathf.Sin(a) * 0.17f);
                    b.SetColor(i % 2 == 0 ? s.Trim : Pal.Bone, 0.5f);
                    b.Push(p, Quaternion.identity, Vector3.one);
                    b.Prism(Vector3.zero, 0.04f, 0f, i % 2 == 0 ? 0.24f : 0.16f, 4, false);
                    b.Pop();
                }
            }
        }

        public static void Eyes(MeshBuilder b, HumanoidStyle s)
        {
            b.SetColor(s.Eyes, 0.8f);
            b.Box(new Vector3(-0.07f, 0.15f, 0.2f), new Vector3(0.045f, 0.03f, 0.01f));
            b.Box(new Vector3(0.07f, 0.15f, 0.2f), new Vector3(0.045f, 0.03f, 0.01f));
        }

        /// <summary>Arm hanging from a shoulder pivot (0.75 long).</summary>
        public static void Arm(MeshBuilder b, HumanoidStyle s)
        {
            var rng = new System.Random(Seed(s) + 2);
            float width = s.Skeletal ? 0.06f : 0.13f;
            Pal upper = s.Robed ? s.Cloth : s.Skin;
            b.ShadedBox(new Vector3(0f, -0.2f, 0f), new Vector3(width, 0.4f, width), upper, rng, 0.38f, 0.5f);
            b.ShadedBox(new Vector3(0f, -0.54f, 0f), new Vector3(width * 0.9f, 0.32f, width * 0.9f), upper, rng, 0.4f, 0.52f);
            b.ShadedBox(new Vector3(0f, -0.72f, 0.02f), new Vector3(width * 1.1f, 0.1f, width * 1.3f), s.Skin, rng, 0.45f, 0.55f);
        }

        /// <summary>Leg hanging from a hip pivot (≈1.0 long).</summary>
        public static void Leg(MeshBuilder b, HumanoidStyle s)
        {
            var rng = new System.Random(Seed(s) + 3);
            float width = s.Skeletal ? 0.07f : 0.17f;
            b.ShadedBox(new Vector3(0f, -0.25f, 0f), new Vector3(width, 0.48f, width), s.Skin, rng, 0.35f, 0.48f);
            b.ShadedBox(new Vector3(0f, -0.72f, 0f), new Vector3(width * 0.85f, 0.44f, width * 0.85f), s.Skin, rng, 0.38f, 0.5f);
            b.ShadedBox(new Vector3(0f, -0.96f, 0.05f), new Vector3(width * 1.1f, 0.08f, 0.26f), s.Skin, rng, 0.3f, 0.42f);
        }

        /// <summary>Whatever the right hand holds; the origin is the grip.</summary>
        public static void Held(MeshBuilder b, HeldItem item)
        {
            var rng = new System.Random(77);
            switch (item)
            {
                case HeldItem.Club:
                    b.ShadedBox(new Vector3(0f, 0.02f, 0f), new Vector3(0.05f, 0.6f, 0.05f), Pal.Bone, rng, 0.35f, 0.5f);
                    b.ShadedBox(new Vector3(0f, 0.34f, 0f), new Vector3(0.12f, 0.16f, 0.12f), Pal.Bone, rng, 0.45f, 0.6f);
                    break;
                case HeldItem.Bow:
                    for (int i = 0; i < 6; i++)
                    {
                        float a = -0.8f + i * 0.32f;
                        b.SetColor(Pal.Bone, 0.45f);
                        b.Box(new Vector3(0f, Mathf.Sin(a) * 0.6f, Mathf.Cos(a) * 0.2f - 0.1f), new Vector3(0.03f, 0.2f, 0.04f));
                    }
                    break;
                case HeldItem.Staff:
                    b.SetColor(Pal.WoodDark, 0.35f);
                    b.Prism(new Vector3(0f, -0.9f, 0f), 0.03f, 0.03f, 1.9f, 6);
                    b.SetColor(Pal.Glow, 0.7f);
                    b.Box(new Vector3(0f, 1.04f, 0f), new Vector3(0.12f, 0.12f, 0.12f));
                    break;
                case HeldItem.Scythe:
                    b.SetColor(Pal.WoodDark, 0.3f);
                    b.Prism(new Vector3(0f, -1.1f, 0f), 0.03f, 0.03f, 2.6f, 6);
                    b.SetColor(Pal.SteelDark, 0.6f);
                    b.TriangleOutward(new Vector3(0f, 1.46f, 0f), new Vector3(0f, 1.4f, 0.95f), new Vector3(0f, 1.2f, 0f), new Vector3(0.5f, 1.3f, 0.3f));
                    b.TriangleOutward(new Vector3(0f, 1.46f, 0f), new Vector3(0f, 1.2f, 0f), new Vector3(0f, 1.4f, 0.95f), new Vector3(-0.5f, 1.3f, 0.3f));
                    break;
                case HeldItem.GreatSword:
                    PropMeshes.Sword(b, Pal.Bone, 0.6f);
                    break;
            }
        }

        // ---- Beasts: body along +Z, standing on y = 0 ----

        public static void BeastBody(MeshBuilder b, Pal fur, Pal under, bool bony)
        {
            var rng = new System.Random(31);
            if (bony)
            {
                for (int i = 0; i < 5; i++)
                    b.ShadedBox(new Vector3(0f, 0.72f, -0.32f + i * 0.16f), new Vector3(0.34f, 0.3f, 0.07f), fur, rng, 0.4f, 0.6f);
                b.ShadedBox(new Vector3(0f, 0.88f, 0f), new Vector3(0.07f, 0.06f, 0.9f), fur, rng, 0.5f, 0.6f);
            }
            else
            {
                b.ShadedBox(new Vector3(0f, 0.72f, 0f), new Vector3(0.42f, 0.38f, 0.95f), fur, rng, 0.38f, 0.5f);
                b.ShadedBox(new Vector3(0f, 0.8f, 0.34f), new Vector3(0.46f, 0.42f, 0.34f), fur, rng, 0.45f, 0.55f);
                b.ShadedBox(new Vector3(0f, 0.57f, 0f), new Vector3(0.34f, 0.1f, 0.7f), under, rng, 0.45f, 0.55f);
            }
        }

        public static void BeastHead(MeshBuilder b, Pal fur, Pal eye)
        {
            var rng = new System.Random(32);
            b.ShadedBox(new Vector3(0f, 0.04f, 0.14f), new Vector3(0.26f, 0.26f, 0.3f), fur, rng, 0.42f, 0.55f);
            b.ShadedBox(new Vector3(0f, -0.02f, 0.38f), new Vector3(0.15f, 0.13f, 0.26f), fur, rng, 0.48f, 0.6f);
            for (int i = -1; i <= 1; i += 2)
            {
                b.SetColor(fur, 0.35f);
                b.Push(new Vector3(0.09f * i, 0.17f, 0.06f), Quaternion.identity, Vector3.one);
                b.Prism(Vector3.zero, 0.05f, 0f, 0.14f, 4, false);
                b.Pop();
                b.SetColor(eye, 0.8f);
                b.Box(new Vector3(0.1f * i, 0.08f, 0.285f), new Vector3(0.05f, 0.035f, 0.01f));
            }
            b.SetColor(Pal.Bone, 0.7f);
            b.Box(new Vector3(0f, -0.09f, 0.4f), new Vector3(0.12f, 0.03f, 0.16f));
        }

        public static void BeastLeg(MeshBuilder b, Pal fur)
        {
            var rng = new System.Random(33);
            b.ShadedBox(new Vector3(0f, -0.3f, 0f), new Vector3(0.11f, 0.6f, 0.13f), fur, rng, 0.35f, 0.48f);
            b.ShadedBox(new Vector3(0f, -0.56f, 0.03f), new Vector3(0.12f, 0.08f, 0.18f), fur, rng, 0.3f, 0.4f);
        }

        public static void BeastTail(MeshBuilder b, Pal fur)
        {
            b.SetColor(fur, 0.4f);
            b.Push(Vector3.zero, Quaternion.Euler(-105f, 0f, 0f), Vector3.one);
            b.Prism(Vector3.zero, 0.05f, 0.01f, 0.6f, 5, false);
            b.Pop();
        }

        // ---- Flyers: body at the origin, wings pivot at the shoulders ----

        public static void FlyerBody(MeshBuilder b, Pal skin, Pal eye, bool shrouded)
        {
            var rng = new System.Random(41);
            if (shrouded)
            {
                b.SetColor(skin, 0.42f);
                b.Prism(new Vector3(0f, -0.9f, 0f), 0.08f, 0.34f, 1.05f, 7, true, false, 0.2f);
                b.SetColor(skin, 0.5f);
                b.Prism(new Vector3(0f, 0.15f, 0f), 0.34f, 0.16f, 0.48f, 7, false, true, 0.2f);
                b.SetColor(Pal.Black, 0.3f);
                b.Box(new Vector3(0f, 0.38f, 0.17f), new Vector3(0.22f, 0.2f, 0.05f));
                b.SetColor(eye, 0.8f);
                b.Box(new Vector3(-0.06f, 0.4f, 0.2f), new Vector3(0.05f, 0.03f, 0.01f));
                b.Box(new Vector3(0.06f, 0.4f, 0.2f), new Vector3(0.05f, 0.03f, 0.01f));
                return;
            }
            b.ShadedBox(new Vector3(0f, 0f, 0f), new Vector3(0.3f, 0.34f, 0.42f), skin, rng, 0.4f, 0.55f);
            b.ShadedBox(new Vector3(0f, 0.08f, 0.28f), new Vector3(0.24f, 0.22f, 0.22f), skin, rng, 0.45f, 0.6f);
            for (int i = -1; i <= 1; i += 2)
            {
                b.SetColor(skin, 0.35f);
                b.Push(new Vector3(0.09f * i, 0.18f, 0.24f), Quaternion.identity, Vector3.one);
                b.Prism(Vector3.zero, 0.05f, 0f, 0.16f, 4, false);
                b.Pop();
                b.SetColor(eye, 0.8f);
                b.Box(new Vector3(0.07f * i, 0.1f, 0.395f), new Vector3(0.045f, 0.03f, 0.01f));
            }
        }

        public static void Wing(MeshBuilder b, Pal skin, float side)
        {
            b.SetColor(skin, 0.3f);
            Vector3 root = Vector3.zero, tip = new Vector3(0.95f * side, 0.1f, -0.05f), back = new Vector3(0.5f * side, -0.05f, -0.45f), front = new Vector3(0.4f * side, 0.1f, 0.2f);
            b.DoubleSided(root, tip, back, Vector3.up);
            b.DoubleSided(root, front, tip, Vector3.up);
        }

        // ---- Blobs: a mound on y = 0 ----

        public static void Blob(MeshBuilder b, Pal color, Pal eye, bool fungal)
        {
            var rng = new System.Random(51);
            b.Rock(new Vector3(0f, 0.42f, 0f), new Vector3(1.5f, 1.2f, 1.5f), rng, color, 0.4f, 0.6f, 0.16f);
            b.Rock(new Vector3(0.2f, 0.82f, 0.1f), new Vector3(0.9f, 0.8f, 0.9f), rng, color, 0.45f, 0.65f, 0.18f);
            b.SetColor(eye, 0.8f);
            b.Box(new Vector3(-0.17f, 0.75f, 0.6f), new Vector3(0.1f, 0.08f, 0.04f));
            b.Box(new Vector3(0.17f, 0.75f, 0.6f), new Vector3(0.1f, 0.08f, 0.04f));
            if (!fungal) return;
            for (int i = 0; i < 6; i++)
            {
                float a = i * 1.05f;
                PropMeshes.Mushroom(b, rng, new Vector3(Mathf.Cos(a) * 0.5f, 0.75f + i * 0.05f, Mathf.Sin(a) * 0.45f), R(rng, 0.35f, 0.7f));
            }
        }

        // ---- A whole standing figure, merged into one mesh, for people who do not fight ----

        public static void Figure(MeshBuilder b, HumanoidStyle s)
        {
            b.Push(new Vector3(0f, 1f, 0f), Quaternion.identity, Vector3.one);
            Torso(b, s);
            b.Push(new Vector3(0f, 0.74f, 0f), Quaternion.identity, Vector3.one);
            Head(b, s);
            b.Pop();
            b.Push(new Vector3(-0.4f, 0.62f, 0f), Quaternion.Euler(-4f, 0f, -7f), Vector3.one);
            Arm(b, s);
            b.Pop();
            b.Push(new Vector3(0.4f, 0.62f, 0f), Quaternion.Euler(-4f, 0f, 7f), Vector3.one);
            Arm(b, s);
            b.Pop();
            if (!s.Robed)
            {
                b.Push(new Vector3(-0.13f, 0f, 0f), Quaternion.identity, Vector3.one);
                Leg(b, s);
                b.Pop();
                b.Push(new Vector3(0.13f, 0f, 0f), Quaternion.identity, Vector3.one);
                Leg(b, s);
                b.Pop();
            }
            b.Pop();
        }
    }
}
