using System;
using UnityEngine;

namespace AshenRealm.EditorTools
{
    /// <summary>
    /// Procedural 64×64 pixel-art detail textures, packed into a Texture2DArray (one layer per <see cref="TexLayer"/>).
    /// "Modulate" layers are near-grey detail around 0.5 (the shader doubles them and multiplies by the vertex colour);
    /// "direct" layers (flowers, wheat) carry their own sRGB colours. Everything tiles seamlessly.
    /// </summary>
    internal static class PixelTextures
    {
        public const int Size = 64;

        public static Texture2DArray CreateArray()
        {
            int layers = Enum.GetValues(typeof(TexLayer)).Length;
            var array = new Texture2DArray(Size, Size, layers, TextureFormat.RGBA32, true, true)
            {
                name = "T_DetailArray",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                anisoLevel = 0,
            };
            for (int i = 0; i < layers; i++)
                array.SetPixels(Generate((TexLayer)i), i, 0);
            array.Apply(true, false);
            return array;
        }

        public static Color[] Generate(TexLayer layer)
        {
            var px = new Color[Size * Size];
            var rng = new System.Random(1000 + (int)layer * 77);
            switch (layer)
            {
                case TexLayer.Grass: Grass(px, rng); break;
                case TexLayer.Dirt: Dirt(px, rng); break;
                case TexLayer.Rock: Rock(px, rng); break;
                case TexLayer.StoneBlock: StoneBlock(px, rng); break;
                case TexLayer.Cobble: Cobble(px, rng); break;
                case TexLayer.Planks: Planks(px, rng); break;
                case TexLayer.Bark: Bark(px, rng); break;
                case TexLayer.PineBranch: PineBranch(px, rng); break;
                case TexLayer.Leaves: Leaves(px, rng); break;
                case TexLayer.Metal: Metal(px, rng); break;
                case TexLayer.Cloth: Cloth(px, rng); break;
                case TexLayer.RoofTiles: RoofTiles(px, rng); break;
                case TexLayer.Plaster: Plaster(px, rng); break;
                case TexLayer.Flowers: Flowers(px, rng); break;
                case TexLayer.GrassCard: GrassCard(px, rng); break;
                case TexLayer.Wheat: Wheat(px, rng); break;
                case TexLayer.Water: Water(px, rng); break;
                case TexLayer.Leather: Leather(px, rng); break;
                case TexLayer.Thatch: Thatch(px, rng); break;
                default: Fill(px, 0.5f); break;
            }
            return px;
        }

        // ---- Helpers -----------------------------------------------------------------------------------

        static float R(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
        static int W(int v) => ((v % Size) + Size) % Size;

        static void Fill(Color[] px, float v)
        {
            for (int i = 0; i < px.Length; i++) px[i] = new Color(v, v, v, 1f);
        }

        static void Set(Color[] px, int x, int y, float v, float alpha = 1f) => px[W(y) * Size + W(x)] = new Color(v, v, v, alpha);
        static void SetColor(Color[] px, int x, int y, Color c) => px[W(y) * Size + W(x)] = c;
        static float Get(Color[] px, int x, int y) => px[W(y) * Size + W(x)].r;
        static void Add(Color[] px, int x, int y, float delta)
        {
            int i = W(y) * Size + W(x);
            Color c = px[i];
            px[i] = new Color(Mathf.Clamp01(c.r + delta), Mathf.Clamp01(c.g + delta), Mathf.Clamp01(c.b + delta), c.a);
        }

        static void Tint(Color[] px, int x, int y, Color tint)
        {
            int i = W(y) * Size + W(x);
            Color c = px[i];
            px[i] = new Color(c.r * tint.r * 2f, c.g * tint.g * 2f, c.b * tint.b * 2f, c.a);
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 982451653);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        /// <summary>Tileable value noise; <paramref name="period"/> lattice cells across the texture.</summary>
        static float Noise(float u, float v, int period, int seed)
        {
            float x = u * period, y = v * period;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int Wp(int a) => ((a % period) + period) % period;
            float a00 = Hash(Wp(x0), Wp(y0), seed), a10 = Hash(Wp(x0 + 1), Wp(y0), seed);
            float a01 = Hash(Wp(x0), Wp(y0 + 1), seed), a11 = Hash(Wp(x0 + 1), Wp(y0 + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(a00, a10, fx), Mathf.Lerp(a01, a11, fx), fy);
        }

        static float Fbm(int x, int y, int period, int octaves, int seed)
        {
            float u = x / (float)Size, v = y / (float)Size, sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += Noise(u, v, period << o, seed + o * 31) * amp;
                norm += amp;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        /// <summary>Tileable Voronoi: distance to nearest and second nearest feature point, and the nearest cell id.</summary>
        static void Voronoi(int x, int y, int cells, int seed, out float d1, out float d2, out int id, out Vector2 offset)
        {
            float cellSize = Size / (float)cells;
            int cx = Mathf.FloorToInt(x / cellSize), cy = Mathf.FloorToInt(y / cellSize);
            d1 = d2 = float.MaxValue;
            id = 0;
            offset = Vector2.zero;
            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int gx = cx + ox, gy = cy + oy;
                int wx = ((gx % cells) + cells) % cells, wy = ((gy % cells) + cells) % cells;
                var feature = new Vector2((gx + 0.15f + 0.7f * Hash(wx, wy, seed)) * cellSize, (gy + 0.15f + 0.7f * Hash(wx, wy, seed + 9)) * cellSize);
                Vector2 delta = new Vector2(x + 0.5f, y + 0.5f) - feature;
                float d = delta.magnitude;
                if (d < d1)
                {
                    d2 = d1;
                    d1 = d;
                    id = wy * cells + wx;
                    offset = delta;
                }
                else if (d < d2) d2 = d;
            }
        }

        static void RandomWalk(Color[] px, System.Random rng, int steps, float value, int sx, int sy)
        {
            int x = sx, y = sy;
            for (int i = 0; i < steps; i++)
            {
                Set(px, x, y, value);
                x += rng.Next(-1, 2);
                y += rng.Next(-1, 2);
            }
        }

        // ---- Ground ------------------------------------------------------------------------------------

        static void Grass(Color[] px, System.Random rng)
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                Set(px, x, y, 0.47f + (Fbm(x, y, 4, 3, 11) - 0.5f) * 0.16f);
            for (int s = 0; s < 300; s++)
            {
                int x0 = rng.Next(Size), y0 = rng.Next(Size), length = rng.Next(3, 8);
                float lean = R(rng, -1.5f, 1.5f);
                for (int k = 0; k < length; k++)
                {
                    float t = k / (float)(length - 1);
                    Set(px, x0 + Mathf.RoundToInt(lean * t), y0 + k, Mathf.Lerp(0.34f, 0.66f, t) + R(rng, -0.02f, 0.02f));
                }
            }
            for (int i = 0; i < 40; i++) Set(px, rng.Next(Size), rng.Next(Size), 0.74f);
        }

        static void Dirt(Color[] px, System.Random rng)
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                Set(px, x, y, 0.5f + (Fbm(x, y, 4, 3, 21) - 0.5f) * 0.18f + R(rng, -0.02f, 0.02f));
            for (int i = 0; i < 46; i++)
            {
                int cx = rng.Next(Size), cy = rng.Next(Size), r = rng.Next(1, 3);
                for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                    if (dx * dx + dy * dy <= r * r) Set(px, cx + dx, cy + dy, 0.62f + R(rng, -0.04f, 0.04f));
                Set(px, cx - r + 1, cy + r - 1, 0.74f);
                for (int dx = -r; dx <= r; dx++) Set(px, cx + dx + 1, cy - r - 1, 0.32f);
            }
            for (int i = 0; i < 6; i++) RandomWalk(px, rng, rng.Next(6, 14), 0.36f, rng.Next(Size), rng.Next(Size));
        }

        static void Rock(Color[] px, System.Random rng)
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                Voronoi(x, y, 5, 31, out float d1, out float d2, out int id, out Vector2 offset);
                float shade = 0.44f + Hash(id, 7, 31) * 0.18f + (offset.y - offset.x) * 0.006f;
                shade += (Fbm(x, y, 8, 2, 33) - 0.5f) * 0.08f;
                if (d2 - d1 < 1.3f) shade = 0.26f;
                else if (d2 - d1 < 2.3f && offset.y > 0f) shade += 0.08f;
                Set(px, x, y, shade);
            }
            for (int i = 0; i < 30; i++)
                Tint(px, rng.Next(Size), rng.Next(Size), new Color(0.52f, 0.56f, 0.42f));
        }

        static void StoneBlock(Color[] px, System.Random rng)
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float v = 0.53f + (Fbm(x, y, 4, 3, 41) - 0.5f) * 0.14f + R(rng, -0.025f, 0.025f);
                int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(Size - 1 - x, Size - 1 - y));
                if (edge == 0) v = 0.2f;
                else if (edge <= 2)
                {
                    bool lit = y >= Size - 3 || x <= 2;
                    v = lit ? 0.68f + R(rng, -0.03f, 0.03f) : 0.33f;
                }
                Set(px, x, y, v);
                // Moss creeps up from the lower edge.
                if (edge > 0 && Hash(x, y, 43) < Mathf.Lerp(0.35f, 0f, y / 20f))
                    Tint(px, x, y, new Color(0.42f, 0.52f, 0.34f));
            }
            for (int i = 0; i < 14; i++)
            {
                int side = rng.Next(4), along = rng.Next(4, Size - 4);
                int x = side == 0 ? 2 : side == 1 ? Size - 3 : along;
                int y = side == 2 ? 2 : side == 3 ? Size - 3 : along;
                Set(px, x, y, 0.28f);
                Set(px, x + rng.Next(-1, 2), y + rng.Next(-1, 2), 0.3f);
            }
            RandomWalk(px, rng, 14, 0.34f, rng.Next(10, 54), rng.Next(10, 54));
        }

        static void Cobble(Color[] px, System.Random rng)
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                Voronoi(x, y, 6, 51, out float d1, out float d2, out int id, out Vector2 offset);
                float v;
                if (d2 - d1 < 1.7f) v = 0.26f + (Fbm(x, y, 8, 2, 53) - 0.5f) * 0.06f;
                else
                {
                    float dome = Mathf.Clamp((offset.y - offset.x) * 0.022f, -0.12f, 0.12f);
                    v = 0.48f + Hash(id, 3, 51) * 0.14f + dome + R(rng, -0.02f, 0.02f);
                }
                Set(px, x, y, v);
            }
        }

        // ---- Wood, bark, roofs, plaster ----------------------------------------------------------------

        static void Planks(Color[] px, System.Random rng)
        {
            const int board = 16;
            var shades = new float[Size / board];
            for (int i = 0; i < shades.Length; i++) shades[i] = R(rng, 0.46f, 0.58f);
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int b = x / board, local = x % board;
                float v = shades[b] + (Noise(x / (float)Size, y / (float)Size * 0.25f, 16, 61 + b) - 0.5f) * 0.12f;
                if ((local + Mathf.FloorToInt(Fbm(x, y, 2, 2, 63 + b) * 4f)) % 5 == 0) v -= 0.07f;
                if (local == 0) v = 0.22f;
                else if (local == 1) v += 0.06f;
                else if (local == board - 1) v -= 0.06f;
                Set(px, x, y, v);
            }
            for (int k = 0; k < 4; k++)
            {
                int cx = rng.Next(4, Size - 4), cy = rng.Next(Size);
                for (int dy = -2; dy <= 2; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    Set(px, cx + dx, cy + dy, Mathf.Abs(dx) + Mathf.Abs(dy) <= 1 ? 0.3f : 0.38f);
            }
            for (int b = 0; b < Size / board; b++)
            foreach (int ny in new[] { 5, Size - 6 })
            {
                Set(px, b * board + board / 2, ny, 0.78f);
                Set(px, b * board + board / 2, ny - 1, 0.3f);
            }
        }

        static void Bark(Color[] px, System.Random rng)
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float warp = Fbm(x, y, 2, 3, 71) * 6f;
                float ridge = Mathf.Sin((x + warp) * Mathf.PI * 2f / 8f);
                float v = 0.5f + ridge * 0.1f + (Noise(x / (float)Size, y / (float)Size, 32, 73) - 0.5f) * 0.08f;
                if (ridge < -0.65f) v = 0.27f;
                Set(px, x, y, v);
            }
            for (int i = 0; i < 8; i++)
            {
                int y = rng.Next(Size), x = rng.Next(Size), len = rng.Next(3, 7);
                for (int k = 0; k < len; k++) Set(px, x + k, y, 0.3f);
            }
        }

        static void RoofTiles(Color[] px, System.Random rng)
        {
            const int row = 8, tile = 8;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int r = y / row, ty = y % row;
                int offset = (r % 2) * (tile / 2);
                int tx = (x + offset) % tile, column = (x + offset) / tile;
                float v = Mathf.Lerp(0.38f, 0.62f, ty / (float)(row - 1)) + (Hash(column, r, 81) - 0.5f) * 0.1f;
                if (ty == 0) v = 0.24f;
                if (tx == 0) v -= 0.12f;
                if (tx == tile - 1 && ty > 0) v += 0.05f;
                Set(px, x, y, v + R(rng, -0.015f, 0.015f));
                if (Hash(x, y, 83) < 0.04f) Tint(px, x, y, new Color(0.46f, 0.52f, 0.36f));
            }
        }

        static void Thatch(Color[] px, System.Random rng)
        {
            Fill(px, 0.45f);
            for (int s = 0; s < 420; s++)
            {
                int x0 = rng.Next(Size), y0 = rng.Next(Size), length = rng.Next(5, 12);
                float v = R(rng, 0.42f, 0.66f);
                for (int k = 0; k < length; k++) Set(px, x0 + (k / 4), y0 - k, v - k * 0.008f);
            }
            for (int y = 0; y < Size; y += 12)
            for (int x = 0; x < Size; x++)
                Set(px, x, y + (x % 3 == 0 ? 1 : 0), 0.3f);
        }

        static void Plaster(Color[] px, System.Random rng)
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float v = 0.56f + (Fbm(x, y, 4, 3, 91) - 0.5f) * 0.1f + R(rng, -0.025f, 0.025f);
                if (y < 12) v -= (12 - y) * 0.004f;
                Set(px, x, y, v);
            }
            for (int i = 0; i < 3; i++) RandomWalk(px, rng, rng.Next(8, 20), 0.42f, rng.Next(Size), rng.Next(Size));
        }

        // ---- Foliage cards (alpha) -----------------------------------------------------------------------

        static void Clear(Color[] px)
        {
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0.5f, 0.5f, 0.5f, 0f);
        }

        /// <summary>Pine bough hanging from the top edge with a ragged, dripping fringe (as seen in the references).</summary>
        static void PineBranch(Color[] px, System.Random rng)
        {
            Clear(px);
            var length = new int[Size];
            for (int x = 0; x < Size; x++)
            {
                float centre = 1f - Mathf.Abs(x - 31.5f) / 32f;
                length[x] = Mathf.RoundToInt(18f + 30f * Mathf.Pow(Mathf.Clamp01(centre), 0.6f));
                if (rng.NextDouble() < 0.45) length[x] += rng.Next(2, 13);
                if (rng.NextDouble() < 0.2) length[x] -= rng.Next(2, 8);
            }
            for (int x = 0; x < Size; x++)
            {
                float streak = (Hash(x, 0, 101) - 0.5f) * 0.12f;
                for (int depth = 0; depth < length[x]; depth++)
                {
                    int y = Size - 1 - depth;
                    if (y < 0) break;
                    float t = depth / (float)Mathf.Max(1, length[x]);
                    float v = Mathf.Lerp(0.66f, 0.34f, t) + streak;
                    if (x % 3 == 0 && t > 0.6f) v += 0.06f;
                    Set(px, x, y, v);
                }
            }
        }

        static void Leaves(Color[] px, System.Random rng)
        {
            Clear(px);
            int blobs = 16;
            for (int b = 0; b < blobs; b++)
            {
                float cx = R(rng, 10f, 54f), cy = R(rng, 10f, 54f), r = R(rng, 6f, 11f);
                for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) + (Hash(x, y, 111 + b) - 0.5f) * 3f;
                    if (d > r) continue;
                    float light = (dy - dx) / r * 0.12f;
                    float v = 0.52f + light + (Hash(x, y, 113) - 0.5f) * 0.08f;
                    if (d > r - 1.2f) v -= 0.1f;
                    Set(px, x, y, v);
                }
            }
            for (int i = 0; i < 60; i++)
            {
                int x = rng.Next(Size), y = rng.Next(Size);
                if (px[y * Size + x].a > 0f) Set(px, x, y, 0.74f);
            }
        }

        static void GrassCard(Color[] px, System.Random rng)
        {
            Clear(px);
            for (int b = 0; b < 34; b++)
            {
                float x0 = R(rng, 4f, 60f), lean = R(rng, -10f, 10f);
                int height = rng.Next(20, 62);
                for (int y = 0; y < height; y++)
                {
                    float t = y / (float)height;
                    float x = x0 + lean * t * t;
                    int width = t < 0.5f ? 2 : 1;
                    for (int w = 0; w < width; w++)
                    {
                        int xi = Mathf.Clamp(Mathf.RoundToInt(x) + w, 0, Size - 1);
                        Set(px, xi, y, Mathf.Lerp(0.34f, 0.68f, t) + R(rng, -0.02f, 0.02f));
                    }
                }
            }
        }

        static void Flowers(Color[] px, System.Random rng)
        {
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0.3f, 0.45f, 0.2f, 0f);
            Color[] petals =
            {
                new Color(0.86f, 0.2f, 0.16f), new Color(0.96f, 0.94f, 0.86f), new Color(0.56f, 0.36f, 0.78f),
                new Color(0.32f, 0.48f, 0.88f), new Color(0.98f, 0.82f, 0.3f), new Color(0.92f, 0.5f, 0.64f),
            };
            for (int s = 0; s < 22; s++)
            {
                int x0 = rng.Next(3, 61), height = rng.Next(12, 40);
                float lean = R(rng, -4f, 4f);
                Color stem = rng.NextDouble() < 0.5 ? new Color(0.24f, 0.42f, 0.18f) : new Color(0.33f, 0.5f, 0.2f);
                int tipX = x0;
                for (int y = 0; y < height; y++)
                {
                    tipX = Mathf.Clamp(x0 + Mathf.RoundToInt(lean * y / height), 1, Size - 2);
                    SetColor(px, tipX, y, stem);
                    if (y == height / 3) SetColor(px, tipX + 1, y, stem);
                }
                Color petal = petals[rng.Next(petals.Length)];
                Color light = Color.Lerp(petal, Color.white, 0.35f);
                Color dark = petal * 0.75f;
                light.a = dark.a = petal.a = 1f;
                int ty = Mathf.Min(height, Size - 3);
                SetColor(px, tipX - 1, ty, petal);
                SetColor(px, tipX + 1, ty, dark);
                SetColor(px, tipX, ty + 1, light);
                SetColor(px, tipX, ty - 1, dark);
                SetColor(px, tipX, ty, new Color(0.98f, 0.84f, 0.32f, 1f));
            }
        }

        static void Wheat(Color[] px, System.Random rng)
        {
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0.8f, 0.65f, 0.3f, 0f);
            for (int s = 0; s < 24; s++)
            {
                int x0 = rng.Next(2, 62), height = rng.Next(36, 62);
                float lean = R(rng, -5f, 5f);
                for (int y = 0; y < height; y++)
                {
                    int x = Mathf.Clamp(x0 + Mathf.RoundToInt(lean * y / height), 1, Size - 2);
                    bool head = y > height - 11;
                    if (head)
                    {
                        Color a = (y % 2 == 0) ? new Color(0.95f, 0.8f, 0.44f, 1f) : new Color(0.8f, 0.62f, 0.3f, 1f);
                        SetColor(px, x, y, a);
                        SetColor(px, x + ((y % 2 == 0) ? 1 : -1), y, new Color(0.86f, 0.7f, 0.36f, 1f));
                    }
                    else
                    {
                        SetColor(px, x, y, new Color(0.74f, 0.6f, 0.3f, 1f));
                    }
                }
            }
        }

        // ---- Materials -----------------------------------------------------------------------------------

        static void Metal(Color[] px, System.Random rng)
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                Set(px, x, y, 0.56f + (Noise(x / (float)Size * 0.1f, y / (float)Size, 32, 121) - 0.5f) * 0.1f + R(rng, -0.015f, 0.015f));
            for (int s = 0; s < 12; s++)
            {
                int x0 = rng.Next(Size), y0 = rng.Next(Size), len = rng.Next(4, 12);
                for (int k = 0; k < len; k++) Set(px, x0 + k, y0 + k / 2, 0.72f);
            }
            for (int i = 0; i < 16; i++) Set(px, rng.Next(Size), rng.Next(Size), 0.38f);
        }

        static void Cloth(Color[] px, System.Random rng)
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float fold = Mathf.Sin((x + Fbm(x, y, 2, 2, 131) * 10f) * Mathf.PI * 2f / 21f) * 0.08f;
                float weave = ((x + y) % 2 == 0) ? 0.02f : -0.02f;
                Set(px, x, y, 0.5f + fold + weave + R(rng, -0.015f, 0.015f));
            }
        }

        static void Leather(Color[] px, System.Random rng)
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int band = (x + y) % 8;
                float v = 0.48f + (Fbm(x, y, 8, 2, 141) - 0.5f) * 0.12f;
                if (band == 0) v = 0.32f;
                else if (band == 1) v += 0.1f;
                Set(px, x, y, v);
            }
        }

        static void Water(Color[] px, System.Random rng)
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                Set(px, x, y, 0.5f + (Fbm(x, y, 4, 2, 151) - 0.5f) * 0.08f);
            for (int i = 0; i < 40; i++)
            {
                int y = rng.Next(Size), x = rng.Next(Size), len = rng.Next(3, 9);
                float v = rng.NextDouble() < 0.6 ? 0.74f : 0.38f;
                for (int k = 0; k < len; k++) Set(px, x + k, y, v);
            }
        }

        // ---- Painted sky ---------------------------------------------------------------------------------

        /// <summary>
        /// Equirectangular sky (Skybox/Panoramic layout): golden-hour gradient, sun glow and banded, posterised clouds
        /// lit from the sun side. Returns the colour used at the horizon away from the sun (for fog).
        /// </summary>
        public static Texture2D PaintSky(int width, int height, Vector3 sunDirection, out Color horizonColor)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            sunDirection.Normalize();
            Color zenith = new Color(0.25f, 0.36f, 0.62f), upper = new Color(0.46f, 0.58f, 0.80f);
            Color horizon = new Color(0.78f, 0.76f, 0.80f), sunHorizon = new Color(0.98f, 0.80f, 0.56f);
            Color ground = new Color(0.56f, 0.56f, 0.62f);
            Color cloudShadow = new Color(0.50f, 0.50f, 0.64f), cloudMid = new Color(0.76f, 0.72f, 0.80f);
            Color cloudLit = new Color(0.98f, 0.88f, 0.72f), cloudRim = new Color(1f, 0.96f, 0.86f);
            horizonColor = horizon;

            for (int py = 0; py < height; py++)
            for (int px = 0; px < width; px++)
            {
                float u = (px + 0.5f) / width, v = (py + 0.5f) / height;
                float longitude = (0.5f - u) * Mathf.PI * 2f, latitude = (1f - v) * Mathf.PI;
                var dir = new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude));
                float elevation = dir.y;
                float sunDot = Vector3.Dot(dir, sunDirection);
                float towardSun = Mathf.Clamp01(Vector3.Dot(new Vector3(dir.x, 0f, dir.z).normalized, new Vector3(sunDirection.x, 0f, sunDirection.z).normalized) * 0.5f + 0.5f);

                Color sky;
                if (elevation < 0f) sky = Color.Lerp(horizon, ground, Mathf.Clamp01(-elevation * 4f));
                else
                {
                    Color low = Color.Lerp(horizon, sunHorizon, towardSun * towardSun);
                    sky = elevation < 0.25f
                        ? Color.Lerp(low, upper, elevation / 0.25f)
                        : Color.Lerp(upper, zenith, Mathf.Clamp01((elevation - 0.25f) / 0.6f));
                }
                sky += new Color(1f, 0.8f, 0.5f) * (Mathf.Pow(Mathf.Clamp01(sunDot), 12f) * 0.35f);

                // Clouds on a flattened dome; banding comes from thresholding density and sun-side lighting.
                if (elevation > 0.015f)
                {
                    float k = 1f / (elevation + 0.12f);
                    float cx = dir.x * k * 1.4f, cz = dir.z * k * 1.4f;
                    float density = CloudDensity(cx, cz);
                    float horizonBand = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01((elevation - 0.08f) / 0.5f));
                    density = density * horizonBand;
                    if (density > 0.5f)
                    {
                        float ahead = CloudDensity(cx + sunDirection.x * 0.35f, cz + sunDirection.z * 0.35f) * horizonBand;
                        float lightness = Mathf.Clamp01(0.5f + (density - ahead) * 3f + towardSun * 0.25f);
                        Color cloud = lightness > 0.78f ? cloudRim : lightness > 0.58f ? cloudLit : lightness > 0.38f ? cloudMid : cloudShadow;
                        float distanceFade = Mathf.Clamp01(elevation * 6f);
                        sky = Color.Lerp(Color.Lerp(horizon, sky, 0.3f), cloud, Mathf.Lerp(0.55f, 1f, distanceFade));
                    }
                }

                // Twinkle-free film grain in pixel blocks keeps large gradients from looking flat.
                float grain = (Hash(px / 2, py / 2, 999) - 0.5f) * 0.012f;
                sky.r += grain; sky.g += grain; sky.b += grain;
                sky.a = 1f;
                texture.SetPixel(px, py, sky);
            }
            texture.Apply();
            return texture;
        }

        static float CloudDensity(float x, float z)
        {
            float d = 0f, amp = 0.55f, freq = 0.35f;
            for (int o = 0; o < 4; o++)
            {
                d += Mathf.PerlinNoise(x * freq + 37.1f + o * 11.3f, z * freq + 91.7f - o * 7.9f) * amp;
                amp *= 0.5f;
                freq *= 2.1f;
            }
            return d * 1.05f;
        }
    }
}
