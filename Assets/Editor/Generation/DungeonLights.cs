using UnityEngine;

namespace AshenRealm.EditorTools
{
    /// <summary>
    /// What lights a depth: a fire on the wall every few strides and something hanging from the vaults, in each biome's
    /// own manner. Their flames, lava and spore light are emissive, and <see cref="DungeonGenerator"/> bakes that light
    /// into the walls and floors around them, so the halls have pools of light and dark instead of one even tint.
    /// Wall pieces stand on the wall's face with their front to -Z; hanging pieces hang from their origin at the vault.
    /// </summary>
    internal static class DungeonLights
    {
        static float R(System.Random rng, float min, float max) => MeshBuilder.Lerp(rng, min, max);

        // ---- On the walls ------------------------------------------------------------------------------------------

        public static void WallLight(MeshBuilder b, DungeonDecor decor, System.Random rng)
        {
            switch (decor)
            {
                case DungeonDecor.Fungal: ShelfFungus(b, rng); break;
                case DungeonDecor.Harbor: ShipLantern(b, rng); break;
                case DungeonDecor.Ember: LavaSeam(b, rng); break;
                case DungeonDecor.Marrow: SkullShelf(b, rng); break;
                default: Sconce(b, rng); break;
            }
        }

        /// <summary>An iron bracket with a pitch torch, and the soot it has laid on the stone above it.</summary>
        static void Sconce(MeshBuilder b, System.Random rng)
        {
            b.SetColor(Pal.Stone, 0.14f);
            b.Box(new Vector3(0f, 0.62f, -0.006f), new Vector3(0.26f, 0.6f, 0.008f));
            b.SetColor(Pal.SteelDark, 0.34f);
            b.Box(new Vector3(0f, -0.08f, -0.03f), new Vector3(0.18f, 0.34f, 0.05f));
            b.Box(new Vector3(0f, -0.12f, -0.2f), new Vector3(0.05f, 0.05f, 0.34f));
            b.Prism(new Vector3(0f, -0.06f, -0.36f), 0.06f, 0.1f, 0.1f, 6);
            b.SetColor(Pal.Bark, 0.3f);
            b.Prism(new Vector3(0f, -0.3f, -0.36f), 0.035f, 0.045f, 0.36f, 6);
            Flame(b, new Vector3(0f, 0.06f, -0.36f), R(rng, 1.4f, 1.6f));
        }

        static void Flame(MeshBuilder b, Vector3 at, float size)
        {
            b.SetColor(Pal.FireOrange, 0.72f);
            b.Prism(at, 0.08f * size, 0f, 0.28f * size, 5, false);
            b.SetColor(Pal.FireYellow, 0.85f);
            b.Prism(at + Vector3.up * 0.02f, 0.045f * size, 0f, 0.18f * size, 4, false);
        }

        /// <summary>Skulls and stubs of candle on a shelf of bone, the way the Choir kept its dead company.</summary>
        static void SkullShelf(MeshBuilder b, System.Random rng)
        {
            b.SetColor(Pal.Bone, 0.5f);
            b.Box(new Vector3(0f, -0.2f, -0.13f), new Vector3(0.8f, 0.06f, 0.26f));
            foreach (float x in new[] { -0.34f, 0.34f })
            {
                b.Push(new Vector3(x, -0.34f, -0.08f), Quaternion.Euler(-40f, 0f, 0f), Vector3.one);
                b.Box(Vector3.zero, new Vector3(0.05f, 0.26f, 0.05f));
                b.Pop();
            }
            foreach (float x in new[] { -0.22f, 0.18f })
            {
                b.SetColor(Pal.Bone, R(rng, 0.52f, 0.62f));
                b.Box(new Vector3(x, -0.1f, -0.15f), new Vector3(0.15f, 0.14f, 0.15f));
                b.SetColor(Pal.Black, 0.2f);
                b.Box(new Vector3(x - 0.035f, -0.08f, -0.228f), new Vector3(0.04f, 0.035f, 0.01f));
                b.Box(new Vector3(x + 0.035f, -0.08f, -0.228f), new Vector3(0.04f, 0.035f, 0.01f));
            }
            foreach (float x in new[] { -0.04f, 0.32f, -0.36f })
            {
                float h = R(rng, 0.1f, 0.24f);
                b.SetColor(Pal.Plaster, 0.62f);
                b.Prism(new Vector3(x, -0.17f, -0.17f), 0.035f, 0.032f, h, 5);
                b.SetColor(Pal.Lantern, 0.85f);
                b.Prism(new Vector3(x, -0.17f + h, -0.17f), 0.024f, 0f, 0.08f, 4, false);
            }
        }

        /// <summary>Bracket fungus grown in a stair up the stone, glowing underneath.</summary>
        static void ShelfFungus(MeshBuilder b, System.Random rng)
        {
            for (int i = 0; i < 5; i++)
            {
                float x = R(rng, -0.45f, 0.45f), y = -0.9f + i * 0.42f + R(rng, -0.08f, 0.08f), r = R(rng, 0.16f, 0.3f);
                b.Push(new Vector3(x, y, -0.02f), Quaternion.Euler(0f, 0f, 0f), new Vector3(1f, 1f, 0.7f));
                b.SetColor(Pal.FungalCap, R(rng, 0.38f, 0.5f));
                b.Prism(new Vector3(0f, 0.02f, -r * 0.6f), r, r * 0.4f, 0.07f, 7);
                b.SetColor(Pal.FungalGlow, 0.6f);
                b.Prism(new Vector3(0f, 0f, -r * 0.6f), r * 0.85f, r * 0.95f, 0.025f, 7, false, false);
                b.Pop();
            }
            for (int i = 0; i < 6; i++)
            {
                b.SetColor(Pal.FungalGlow, 0.7f);
                b.Box(new Vector3(R(rng, -0.6f, 0.6f), R(rng, -1.2f, 0.8f), -0.03f), Vector3.one * R(rng, 0.03f, 0.06f));
            }
        }

        /// <summary>A ship's lantern on a bent iron arm, salvaged from what sank.</summary>
        static void ShipLantern(MeshBuilder b, System.Random rng)
        {
            b.SetColor(Pal.WoodDark, 0.32f);
            b.Box(new Vector3(0f, -0.2f, -0.04f), new Vector3(0.22f, 0.9f, 0.08f));
            b.SetColor(Pal.SteelDark, 0.32f);
            b.Box(new Vector3(0f, 0.18f, -0.28f), new Vector3(0.05f, 0.05f, 0.5f));
            b.Box(new Vector3(0f, 0.06f, -0.5f), new Vector3(0.03f, 0.22f, 0.03f));
            Vector3 l = new Vector3(0f, -0.28f, -0.5f);
            b.Prism(l + Vector3.up * 0.24f, 0.13f, 0.04f, 0.1f, 6);
            b.Box(l, new Vector3(0.22f, 0.04f, 0.22f));
            foreach (float x in new[] { -0.09f, 0.09f })
            foreach (float z in new[] { -0.09f, 0.09f })
                b.Box(l + new Vector3(x, 0.12f, z), new Vector3(0.025f, 0.24f, 0.025f));
            b.SetColor(Pal.Lantern, 0.78f);
            b.Box(l + Vector3.up * 0.12f, new Vector3(0.15f, 0.2f, 0.15f));
            b.SetColor(Pal.Thatch, 0.3f);
            for (int i = 0; i < 3; i++)
                b.Prism(new Vector3(0.18f, -0.62f + i * 0.04f, -0.12f), 0.16f, 0.15f, 0.04f, 8, false, false);
        }

        /// <summary>The wall split open on the heat behind it, with a fire basket hung beside the crack.</summary>
        static void LavaSeam(MeshBuilder b, System.Random rng)
        {
            float x = 0f;
            for (int i = 0; i < 9; i++)
            {
                float y = -2.2f + i * 0.36f, next = x + R(rng, -0.12f, 0.12f);
                b.SetColor(Pal.Basalt, 0.24f);
                b.Box(new Vector3((x + next) * 0.5f, y, -0.03f), new Vector3(0.24f, 0.38f, 0.04f));
                b.SetColor(Pal.Lava, R(rng, 0.55f, 0.75f));
                b.Box(new Vector3((x + next) * 0.5f, y, -0.055f), new Vector3(R(rng, 0.05f, 0.11f), 0.37f, 0.01f));
                x = next;
            }
            b.SetColor(Pal.SteelDark, 0.3f);
            b.Box(new Vector3(0.6f, 0.1f, -0.18f), new Vector3(0.05f, 0.05f, 0.34f));
            b.Prism(new Vector3(0.6f, -0.3f, -0.34f), 0.08f, 0.2f, 0.22f, 6, true, false);
            Flame(b, new Vector3(0.6f, -0.1f, -0.34f), 1.3f);
        }

        // ---- From the vaults -------------------------------------------------------------------------------------

        /// <summary>Something hanging <paramref name="length"/> below the vault, its origin at the vault.</summary>
        public static void Hanging(MeshBuilder b, DungeonDecor decor, float length, System.Random rng)
        {
            switch (decor)
            {
                case DungeonDecor.Fungal: Roots(b, length, rng); break;
                case DungeonDecor.Harbor: Net(b, length, rng); break;
                case DungeonDecor.Ember: Cauldron(b, length, rng); break;
                case DungeonDecor.Marrow: BoneChandelier(b, length, rng); break;
                default: CageLamp(b, length, rng); break;
            }
        }

        static void Chain(MeshBuilder b, float length)
        {
            b.SetColor(Pal.SteelDark, 0.3f);
            int links = Mathf.Max(1, Mathf.RoundToInt(length / 0.16f));
            for (int i = 0; i < links; i++)
                b.Box(new Vector3(0f, -(i + 0.5f) * length / links, 0f),
                    i % 2 == 0 ? new Vector3(0.07f, 0.15f, 0.025f) : new Vector3(0.025f, 0.15f, 0.07f));
        }

        static void CageLamp(MeshBuilder b, float length, System.Random rng)
        {
            Chain(b, length);
            Vector3 c = new Vector3(0f, -length - 0.3f, 0f);
            b.SetColor(Pal.SteelDark, 0.3f);
            b.Prism(c + Vector3.up * 0.28f, 0.2f, 0.06f, 0.08f, 6);
            b.Prism(c - Vector3.up * 0.3f, 0.06f, 0.2f, 0.06f, 6);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                b.Box(c + new Vector3(Mathf.Cos(a) * 0.19f, 0f, Mathf.Sin(a) * 0.19f), new Vector3(0.025f, 0.56f, 0.025f));
            }
            Flame(b, c - Vector3.up * 0.2f, 1.3f);
        }

        static void BoneChandelier(MeshBuilder b, float length, System.Random rng)
        {
            Chain(b, length);
            Vector3 c = new Vector3(0f, -length, 0f);
            b.SetColor(Pal.Bone, 0.5f);
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI / 5f;
                b.Push(c + new Vector3(Mathf.Cos(a) * 0.55f, -0.1f, Mathf.Sin(a) * 0.55f), Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f), Vector3.one);
                b.Box(Vector3.zero, new Vector3(0.06f, 0.06f, 0.36f));
                b.Pop();
                if (i % 2 == 0)
                {
                    b.SetColor(Pal.Bone, R(rng, 0.45f, 0.6f));
                    b.Box(c + new Vector3(Mathf.Cos(a) * 0.62f, -0.4f, Mathf.Sin(a) * 0.62f), new Vector3(0.05f, R(rng, 0.3f, 0.55f), 0.05f));
                }
                else
                {
                    b.SetColor(Pal.Plaster, 0.62f);
                    Vector3 p = c + new Vector3(Mathf.Cos(a) * 0.55f, -0.07f, Mathf.Sin(a) * 0.55f);
                    b.Prism(p, 0.03f, 0.028f, 0.16f, 5);
                    b.SetColor(Pal.Lantern, 0.85f);
                    b.Prism(p + Vector3.up * 0.16f, 0.024f, 0f, 0.08f, 4, false);
                }
                b.SetColor(Pal.Bone, 0.5f);
            }
            b.Box(c + new Vector3(0f, -0.25f, 0f), new Vector3(0.18f, 0.18f, 0.18f));
        }

        static void Roots(MeshBuilder b, float length, System.Random rng)
        {
            int strands = 7;
            for (int i = 0; i < strands; i++)
            {
                float a = i * Mathf.PI * 2f / strands + R(rng, -0.3f, 0.3f), r = R(rng, 0.1f, 0.7f), l = length * R(rng, 0.5f, 1.05f);
                Vector3 top = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                b.SetColor(Pal.Bark, R(rng, 0.22f, 0.34f));
                b.Push(top, Quaternion.Euler(R(rng, -6f, 6f), 0f, R(rng, -6f, 6f)), Vector3.one);
                b.Box(new Vector3(0f, -l * 0.5f, 0f), new Vector3(0.05f, l, 0.05f));
                b.SetColor(Pal.FungalGlow, R(rng, 0.55f, 0.7f));
                b.Prism(new Vector3(0f, -l - 0.12f, 0f), 0.02f, 0.08f, 0.14f, 6);
                b.Prism(new Vector3(0f, -l + 0.02f, 0f), 0.08f, 0.02f, 0.06f, 6);
                b.Pop();
            }
        }

        static void Net(MeshBuilder b, float length, System.Random rng)
        {
            // A fishing net slung between two hooks, sagging, with a float or two still in it.
            const float span = 2.6f;
            b.SetColor(Pal.Thatch, 0.3f);
            foreach (float x in new[] { -span * 0.5f, span * 0.5f })
                b.Box(new Vector3(x, -0.6f, 0f), new Vector3(0.03f, 1.2f, 0.03f));
            for (int i = 0; i <= 8; i++)
            {
                float t = i / 8f, x = Mathf.Lerp(-span * 0.5f, span * 0.5f, t), sag = Mathf.Sin(t * Mathf.PI) * 0.9f;
                b.Box(new Vector3(x, -1.2f - sag * 0.5f, 0f), new Vector3(0.02f, sag + 0.2f, 0.02f));
            }
            for (int row = 0; row < 3; row++)
            for (int i = 0; i < 8; i++)
            {
                float t0 = i / 8f, t1 = (i + 1) / 8f;
                Vector3 a = new Vector3(Mathf.Lerp(-span * 0.5f, span * 0.5f, t0), -1.2f - Mathf.Sin(t0 * Mathf.PI) * 0.9f * (row + 1) / 3f, 0f);
                Vector3 c = new Vector3(Mathf.Lerp(-span * 0.5f, span * 0.5f, t1), -1.2f - Mathf.Sin(t1 * Mathf.PI) * 0.9f * (row + 1) / 3f, 0f);
                Vector3 span3 = c - a;
                b.Push((a + c) * 0.5f, Quaternion.FromToRotation(Vector3.right, span3.normalized), Vector3.one);
                b.Box(Vector3.zero, new Vector3(span3.magnitude, 0.02f, 0.02f));
                b.Pop();
            }
            b.SetColor(Pal.ClothRed, 0.4f);
            b.Prism(new Vector3(R(rng, -0.6f, 0.6f), -2f, 0f), 0.08f, 0.08f, 0.12f, 6);
            b.SetColor(Pal.Thatch, 0.3f);
            b.Box(new Vector3(0.5f, -length * 0.5f, 0.3f), new Vector3(0.025f, length, 0.025f));
            Vector3 l = new Vector3(0.5f, -length - 0.2f, 0.3f);
            b.SetColor(Pal.SteelDark, 0.32f);
            b.Prism(l + Vector3.up * 0.2f, 0.12f, 0.04f, 0.08f, 6);
            b.Box(l - Vector3.up * 0.05f, new Vector3(0.2f, 0.04f, 0.2f));
            b.SetColor(Pal.Lantern, 0.78f);
            b.Box(l + Vector3.up * 0.07f, new Vector3(0.14f, 0.2f, 0.14f));
        }

        static void Cauldron(MeshBuilder b, float length, System.Random rng)
        {
            foreach (float x in new[] { -0.3f, 0.3f })
            {
                b.Push(new Vector3(x, 0f, 0f), Quaternion.Euler(0f, 0f, x > 0f ? 4f : -4f), Vector3.one);
                Chain(b, length);
                b.Pop();
            }
            Vector3 c = new Vector3(0f, -length - 0.45f, 0f);
            b.SetColor(Pal.SteelDark, 0.28f);
            b.Prism(c, 0.24f, 0.46f, 0.4f, 9, true, false);
            b.Prism(c + Vector3.up * 0.4f, 0.48f, 0.5f, 0.06f, 9, false, false);
            b.SetColor(Pal.Lava, 0.7f);
            b.Prism(c + Vector3.up * 0.38f, 0.42f, 0.42f, 0.02f, 9, false, true);
            Flame(b, c + Vector3.up * 0.4f, 1.6f);
        }
    }
}
