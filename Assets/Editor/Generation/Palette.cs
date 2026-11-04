using UnityEngine;

namespace AshenRealm.EditorTools
{
    /// <summary>Colour families in the shared palette texture (one column each).</summary>
    internal enum Pal
    {
        GrassGold, GrassOlive, GrassDry, Dirt, DirtDark, Rock, RockWarm, RockDark,
        PineDark, PineMid, Bark, Wood, WoodDark, Stone, StoneMoss, Steel,
        SteelDark, Brass, Leather, ClothRed, ClothDark, FlowerRed, FlowerWhite, FlowerViolet,
        FlowerYellow, Mountain, Snow, FireOrange, FireYellow, Bone, Glow, Black
    }

    /// <summary>
    /// 32×32 palette texture: each column is a colour family, each row a shade from dark to light.
    /// Every generated mesh samples this texture through its UVs, so the whole world shares one material
    /// (SRP-batcher friendly) while keeping per-face colour variation.
    /// </summary>
    internal static class Palette
    {
        public const int Size = 32;

        static readonly Color[] Families =
        {
            C(0.74f, 0.60f, 0.29f), C(0.50f, 0.50f, 0.25f), C(0.80f, 0.68f, 0.40f), C(0.45f, 0.34f, 0.22f),
            C(0.31f, 0.24f, 0.17f), C(0.48f, 0.47f, 0.46f), C(0.53f, 0.46f, 0.39f), C(0.29f, 0.30f, 0.28f),
            C(0.11f, 0.19f, 0.15f), C(0.17f, 0.27f, 0.19f), C(0.28f, 0.20f, 0.14f), C(0.50f, 0.37f, 0.24f),
            C(0.33f, 0.24f, 0.16f), C(0.60f, 0.57f, 0.52f), C(0.43f, 0.46f, 0.35f), C(0.72f, 0.73f, 0.74f),
            C(0.22f, 0.22f, 0.25f), C(0.74f, 0.57f, 0.26f), C(0.36f, 0.22f, 0.14f), C(0.50f, 0.10f, 0.09f),
            C(0.14f, 0.12f, 0.13f), C(0.78f, 0.17f, 0.12f), C(0.90f, 0.88f, 0.80f), C(0.52f, 0.33f, 0.64f),
            C(0.93f, 0.78f, 0.30f), C(0.36f, 0.38f, 0.45f), C(0.86f, 0.87f, 0.91f), C(1.00f, 0.45f, 0.10f),
            C(1.00f, 0.80f, 0.30f), C(0.82f, 0.77f, 0.64f), C(1.00f, 0.32f, 0.10f), C(0.05f, 0.05f, 0.06f),
        };

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        /// <summary>UV of a palette cell. Shade 0 = darkest, 0.5 ≈ base colour, 1 = lightest.</summary>
        public static Vector2 UV(Pal family, float shade)
        {
            int row = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(shade) * (Size - 1)), 0, Size - 1);
            return new Vector2(((int)family + 0.5f) / Size, (row + 0.5f) / Size);
        }

        public static Texture2D CreateTexture()
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            for (int x = 0; x < Size; x++)
            {
                Color baseColor = x < Families.Length ? Families[x] : Color.magenta;
                Color.RGBToHSV(baseColor, out float h, out float s, out float v);
                for (int y = 0; y < Size; y++)
                {
                    float t = y / (float)(Size - 1);
                    // Darker shades drift slightly cooler and more saturated, lighter ones warmer: a painterly ramp.
                    float value = v * Mathf.Lerp(0.62f, 1.3f, t);
                    float hue = Mathf.Repeat(h + Mathf.Lerp(0.012f, -0.008f, t), 1f);
                    float sat = Mathf.Clamp01(s * Mathf.Lerp(1.12f, 0.9f, t));
                    Color c = Color.HSVToRGB(hue, sat, Mathf.Clamp01(value));
                    c.a = 1f;
                    texture.SetPixel(x, y, c);
                }
            }
            texture.Apply();
            return texture;
        }
    }
}
