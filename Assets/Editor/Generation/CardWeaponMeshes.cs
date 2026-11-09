using AshenRealm.Story;
using UnityEngine;

namespace AshenRealm.EditorTools
{
    internal static partial class PlayerWeaponMeshes
    {
        static void CardBeam(MeshBuilder b, Vector3 a, Vector3 c, float width, Pal family, float shade = 0.5f)
        {
            Vector3 span = c - a;
            b.Push((a + c) * 0.5f, Quaternion.FromToRotation(Vector3.up, span.normalized), Vector3.one);
            b.SetColor(family, shade);
            b.Box(Vector3.zero, new Vector3(width, span.magnitude, width));
            b.Pop();
        }

        public static void ClassHand(MeshBuilder b, CharacterClass id, bool open = false)
        {
            bool pale = id == CharacterClass.Warden || id == CharacterClass.Necromancer;
            Pal skin = id == CharacterClass.Necromancer ? Pal.Bone : pale ? Pal.Plaster : Pal.Leather;
            Pal sleeve = id == CharacterClass.Knight ? Pal.SteelDark : id == CharacterClass.Warden ? Pal.Plaster : Pal.ClothDark;
            b.SetColor(sleeve, 0.4f);
            b.Box(new Vector3(0f, -0.23f, -0.035f), new Vector3(0.115f, 0.27f, 0.12f));
            b.SetColor(id == CharacterClass.Knight ? Pal.Steel : skin, 0.55f);
            b.Box(new Vector3(0f, -0.04f, -0.035f), new Vector3(0.125f, 0.13f, 0.11f));
            for (int i = 0; i < 4; i++)
            {
                b.Push(new Vector3(-0.043f + i * 0.028f, 0.017f, -0.015f), Quaternion.Euler(open ? -18f : 42f, 0f, open ? (1.5f - i) * 7f : 0f), Vector3.one);
                b.SetColor(id == CharacterClass.Knight ? Pal.Steel : skin, 0.49f + i * 0.03f);
                b.Box(new Vector3(0f, open ? 0.055f : 0.024f, 0f), new Vector3(0.024f, open ? 0.11f : 0.06f, 0.035f));
                b.Pop();
            }
            b.SetColor(skin, 0.5f);
            b.Box(new Vector3(-0.07f, -0.014f, 0.015f), new Vector3(0.04f, 0.065f, 0.045f));
            b.SetColor(id == CharacterClass.Gravethief || id == CharacterClass.Alchemist ? Pal.SteelDark : Pal.Brass, 0.56f);
            foreach (float y in new[] { -0.14f, -0.28f }) b.Box(new Vector3(0f, y, -0.035f), new Vector3(0.13f, 0.025f, 0.135f));
        }

        static void CardCrescent(MeshBuilder b, Vector2 centre, float outer, float inner, float start, float end)
        {
            const int sections = 20;
            const float thickness = 0.026f;
            Vector3 p = new Vector3(centre.x, centre.y, 0f);
            for (int i = 0; i < sections; i++)
            {
                float a = Mathf.Lerp(start, end, i / (float)sections) * Mathf.Deg2Rad;
                float c = Mathf.Lerp(start, end, (i + 1f) / sections) * Mathf.Deg2Rad;
                Vector3 u = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f), v = new Vector3(Mathf.Cos(c), Mathf.Sin(c), 0f);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 front = Vector3.forward * side * thickness;
                    b.SetColor(Pal.Steel, i % 3 == 0 ? 0.57f : 0.73f);
                    b.QuadOutward(p + u * outer + front, p + v * outer + front, p + v * inner + front, p + u * inner + front, p - front);
                }
                b.SetColor(Pal.Steel, 0.85f);
                b.QuadOutward(p + u * outer + Vector3.back * thickness, p + v * outer + Vector3.back * thickness,
                    p + v * outer + Vector3.forward * thickness, p + u * outer + Vector3.forward * thickness, p);
            }
        }

        public static void CrescentPolearm(MeshBuilder b)
        {
            b.SetColor(Pal.WoodDark, 0.37f);
            b.Prism(new Vector3(0f, -0.56f, 0f), 0.027f, 0.022f, 1.85f, 8);
            foreach (float y in new[] { -0.5f, -0.24f, 0.25f, 0.7f, 1.2f })
            {
                b.SetColor(Pal.Brass, 0.56f);
                b.Prism(new Vector3(0f, y, 0f), 0.036f, 0.036f, 0.045f, 8);
            }
            CardCrescent(b, new Vector2(0.1f, 1.24f), 0.38f, 0.29f, 90f, 270f);
            CardBeam(b, new Vector3(0f, 0.96f, 0f), new Vector3(0.1f, 0.89f, 0f), 0.065f, Pal.Brass);
            b.SetColor(Pal.Brass, 0.66f);
            b.Prism(new Vector3(0f, 1.29f, 0f), 0.043f, 0f, 0.14f, 6);
        }

        public static void SunShield(MeshBuilder b)
        {
            const int sides = 24;
            for (int face = -1; face <= 1; face += 2)
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides, c = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 u = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f), v = new Vector3(Mathf.Cos(c), Mathf.Sin(c), 0f);
                Vector3 p = Vector3.forward * face * 0.04f;
                b.SetColor(Pal.Brass, i % 2 == 0 ? 0.43f : 0.5f);
                b.Triangle(p, u * 0.36f + p, v * 0.36f + p, Vector3.forward * face);
                CardBeam(b, u * 0.36f + p, v * 0.36f + p, 0.028f, Pal.Brass, 0.72f);
                if (i % 2 == 0) CardBeam(b, u * 0.11f + p * 1.4f, u * 0.29f + p * 1.4f, 0.016f, Pal.Brass, 0.75f);
            }
            for (int face = -1; face <= 1; face += 2)
            {
                b.Push(new Vector3(0f, 0f, face * 0.055f), Quaternion.Euler(face * 90f, 0f, 0f), Vector3.one);
                b.SetColor(Pal.Brass, 0.72f);
                b.Prism(Vector3.zero, 0.105f, 0.07f, 0.045f, 16);
                b.Pop();
            }
        }

        public static void AntlerStaff(MeshBuilder b)
        {
            Vector3[] trunk = { new Vector3(0f, -0.4f, 0f), new Vector3(0.015f, 0.2f, 0f), new Vector3(-0.035f, 0.7f, 0f), new Vector3(0.015f, 1.06f, 0f) };
            for (int i = 1; i < trunk.Length; i++) CardBeam(b, trunk[i - 1], trunk[i], 0.055f - i * 0.006f, Pal.Bark, 0.34f);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 a = new Vector3(side * 0.04f, 0.78f, 0f), c = new Vector3(side * 0.23f, 1.09f, 0.025f);
                CardBeam(b, a, c, 0.034f, Pal.Bark, 0.42f);
                CardBeam(b, c, new Vector3(side * 0.27f, 1.34f, 0f), 0.021f, Pal.Bark, 0.51f);
                CardBeam(b, c, new Vector3(side * 0.38f, 1.21f, -0.015f), 0.018f, Pal.Bark, 0.48f);
                CardBeam(b, new Vector3(side * 0.14f, 0.95f, 0f), new Vector3(side * 0.13f, 1.22f, -0.06f), 0.021f, Pal.Bark);
            }
            CardBeam(b, new Vector3(-0.035f, 0.79f, 0.02f), new Vector3(-0.02f, 0.56f, -0.04f), 0.012f, Pal.Brass);
            b.SetColor(Pal.PaintBlue, 0.8f);
            b.Current.Emission = 0.13f;
            b.Prism(new Vector3(-0.02f, 0.44f, -0.04f), 0.055f, 0.015f, 0.15f, 5);
            b.Push(new Vector3(-0.02f, 0.44f, -0.04f), Quaternion.Euler(180f, 0f, 0f), Vector3.one);
            b.Prism(Vector3.zero, 0.055f, 0f, 0.09f, 5);
            b.Pop();
        }

        public static void WardenBlade(MeshBuilder b)
        {
            b.SetColor(Pal.Brass, 0.56f);
            b.Prism(new Vector3(0f, -0.16f, 0f), 0.033f, 0.033f, 0.27f, 8);
            CardCrescent(b, new Vector2(-0.12f, 0.27f), 0.34f, 0.265f, -40f, 155f);
            CardBeam(b, new Vector3(0f, 0.05f, 0f), new Vector3(0.14f, 0.05f, 0f), 0.045f, Pal.Brass, 0.68f);
        }

        public static void NecromancerSickle(MeshBuilder b)
        {
            b.SetColor(Pal.SteelDark, 0.5f);
            b.Prism(new Vector3(0f, -0.2f, 0f), 0.03f, 0.028f, 0.47f, 8);
            CardCrescent(b, new Vector2(-0.16f, 0.43f), 0.31f, 0.265f, -85f, 112f);
            CardBeam(b, new Vector3(0f, 0.2f, 0f), new Vector3(-0.13f, 0.14f, 0f), 0.045f, Pal.Steel, 0.64f);
            b.SetColor(Pal.Brass, 0.56f);
            foreach (float y in new[] { -0.2f, -0.1f, 0.04f, 0.2f }) b.Box(new Vector3(0f, y, 0f), new Vector3(0.072f, 0.025f, 0.065f));
            for (int i = 0; i < 7; i++)
                b.Box(new Vector3(-0.015f + Mathf.Sin(i * 0.7f) * 0.025f, -0.24f - i * 0.038f, 0f),
                    i % 2 == 0 ? new Vector3(0.045f, 0.025f, 0.016f) : new Vector3(0.016f, 0.025f, 0.045f));
        }
    }
}
