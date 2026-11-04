using UnityEngine;

namespace AshenRealm.EditorTools
{
    /// <summary>Colour families. Each maps to a base colour and a default surface (texture layer, UV mode).</summary>
    internal enum Pal
    {
        GrassGold, GrassOlive, GrassDry, Dirt, DirtDark, Rock, RockWarm, RockDark,
        PineDark, PineMid, Bark, Wood, WoodDark, Stone, StoneMoss, Steel,
        SteelDark, Brass, Leather, ClothRed, ClothDark, FlowerRed, FlowerWhite, FlowerViolet,
        FlowerYellow, Mountain, Snow, FireOrange, FireYellow, Bone, Glow, Black,
        PaintBlue, Plaster, RoofTile, LeafGold, LeafOrange, LeafGreen, Water, Lantern,
        BirchBark, Cobble, Wheat, Thatch
    }

    /// <summary>Layers of the generated detail Texture2DArray (order = array index).</summary>
    internal enum TexLayer
    {
        Grass, Dirt, Rock, StoneBlock, Cobble, Planks, Bark, PineBranch, Leaves, Metal,
        Cloth, RoofTiles, Plaster, Flowers, GrassCard, Wheat, Water, Plain, Leather, Thatch
    }

    internal enum UVMode
    {
        /// <summary>Planar projection along the face's dominant axis, in metres per tile.</summary>
        Triplanar,
        /// <summary>Each quad face gets the whole tile (stone blocks), repeated to keep tiles square.</summary>
        FaceUnit,
        /// <summary>UVs supplied by the caller (foliage cards).</summary>
        Explicit,
    }

    /// <summary>Everything the shader needs to know about the faces being emitted.</summary>
    internal struct Surface
    {
        public Color Color;          // linear
        public TexLayer Layer;
        public UVMode Mode;
        public float TileSize;
        public float Direct;
        public float Emission;
        public float Sheen;
    }

    /// <summary>
    /// Painterly palette: authored in sRGB, converted to linear vertex colours. Shade 0 = darkest, 0.5 ≈ base, 1 = lightest.
    /// </summary>
    internal static class Palette
    {
        static readonly Color[] Families =
        {
            C(0.74f, 0.62f, 0.30f), C(0.50f, 0.54f, 0.26f), C(0.82f, 0.70f, 0.42f), C(0.50f, 0.38f, 0.25f),
            C(0.36f, 0.27f, 0.19f), C(0.52f, 0.51f, 0.50f), C(0.56f, 0.49f, 0.42f), C(0.33f, 0.34f, 0.33f),
            C(0.16f, 0.27f, 0.20f), C(0.23f, 0.36f, 0.24f), C(0.36f, 0.27f, 0.19f), C(0.56f, 0.42f, 0.27f),
            C(0.38f, 0.28f, 0.19f), C(0.66f, 0.62f, 0.55f), C(0.52f, 0.54f, 0.42f), C(0.74f, 0.75f, 0.77f),
            C(0.30f, 0.31f, 0.35f), C(0.80f, 0.62f, 0.28f), C(0.42f, 0.27f, 0.17f), C(0.56f, 0.13f, 0.11f),
            C(0.18f, 0.16f, 0.18f), C(0.82f, 0.20f, 0.15f), C(0.92f, 0.90f, 0.82f), C(0.55f, 0.36f, 0.68f),
            C(0.95f, 0.80f, 0.32f), C(0.46f, 0.48f, 0.58f), C(0.90f, 0.91f, 0.95f), C(1.00f, 0.50f, 0.14f),
            C(1.00f, 0.82f, 0.34f), C(0.84f, 0.79f, 0.66f), C(1.00f, 0.36f, 0.12f), C(0.06f, 0.06f, 0.07f),
            C(0.22f, 0.32f, 0.58f), C(0.86f, 0.80f, 0.68f), C(0.58f, 0.27f, 0.18f), C(0.98f, 0.76f, 0.24f),
            C(0.92f, 0.50f, 0.18f), C(0.36f, 0.52f, 0.24f), C(0.36f, 0.48f, 0.66f), C(1.00f, 0.78f, 0.40f),
            C(0.86f, 0.84f, 0.78f), C(0.54f, 0.52f, 0.48f), C(1.00f, 1.00f, 1.00f), C(0.76f, 0.64f, 0.38f),
        };

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        /// <summary>Display-space colour of a family at a shade.</summary>
        public static Color SRGB(Pal family, float shade)
        {
            Color baseColor = Families[(int)family];
            Color.RGBToHSV(baseColor, out float h, out float s, out float v);
            float t = Mathf.Clamp01(shade);
            // Darker shades drift slightly cooler and more saturated, lighter ones warmer: a painterly ramp.
            float value = Mathf.Clamp01(v * Mathf.Lerp(0.62f, 1.3f, t));
            float hue = Mathf.Repeat(h + Mathf.Lerp(0.012f, -0.008f, t), 1f);
            float sat = Mathf.Clamp01(s * Mathf.Lerp(1.12f, 0.9f, t));
            Color c = Color.HSVToRGB(hue, sat, value);
            c.a = 1f;
            return c;
        }

        public static Color Linear(Pal family, float shade) => SRGB(family, shade).linear;

        public static Surface SurfaceFor(Pal family, float shade)
        {
            var s = new Surface { Color = Linear(family, shade), Layer = TexLayer.Plain, Mode = UVMode.Triplanar, TileSize = 1f };
            switch (family)
            {
                case Pal.GrassGold: case Pal.GrassOlive: case Pal.GrassDry:
                case Pal.PineDark: case Pal.PineMid:
                    s.Layer = TexLayer.Grass; s.TileSize = family >= Pal.PineDark ? 1f : 2.5f; break;
                case Pal.Dirt: case Pal.DirtDark:
                    s.Layer = TexLayer.Dirt; s.TileSize = 2.5f; break;
                case Pal.Rock: case Pal.RockWarm: case Pal.RockDark: case Pal.Bone:
                    s.Layer = TexLayer.Rock; s.TileSize = family == Pal.Bone ? 0.5f : 2f; break;
                case Pal.Mountain: case Pal.Snow:
                    s.Layer = TexLayer.Rock; s.TileSize = 28f; break;
                case Pal.Bark: case Pal.BirchBark:
                    s.Layer = TexLayer.Bark; s.TileSize = 1f; break;
                case Pal.Wood: case Pal.WoodDark: case Pal.PaintBlue:
                    s.Layer = TexLayer.Planks; s.TileSize = 1.2f; break;
                case Pal.Stone: case Pal.StoneMoss:
                    s.Layer = TexLayer.StoneBlock; s.Mode = UVMode.FaceUnit; break;
                case Pal.Cobble:
                    s.Layer = TexLayer.Cobble; s.TileSize = 2f; break;
                case Pal.Steel: case Pal.SteelDark:
                    s.Layer = TexLayer.Metal; s.TileSize = 0.6f; s.Sheen = 1f; break;
                case Pal.Brass:
                    s.Layer = TexLayer.Metal; s.TileSize = 0.4f; s.Sheen = 1f; break;
                case Pal.Leather:
                    s.Layer = TexLayer.Leather; s.TileSize = 0.3f; break;
                case Pal.ClothRed: case Pal.ClothDark:
                    s.Layer = TexLayer.Cloth; s.TileSize = 1f; break;
                case Pal.Plaster:
                    s.Layer = TexLayer.Plaster; s.TileSize = 2f; break;
                case Pal.RoofTile:
                    s.Layer = TexLayer.RoofTiles; s.TileSize = 1.5f; break;
                case Pal.Thatch:
                    s.Layer = TexLayer.Thatch; s.TileSize = 1.5f; break;
                case Pal.Water:
                    s.Layer = TexLayer.Water; s.TileSize = 3f; s.Sheen = 1f; break;
                case Pal.LeafGold: case Pal.LeafOrange: case Pal.LeafGreen:
                    s.Layer = TexLayer.Leaves; s.Mode = UVMode.Explicit; break;
                case Pal.Wheat:
                    s.Layer = TexLayer.Wheat; s.Mode = UVMode.Explicit; s.Direct = 1f; break;
                case Pal.FireOrange: case Pal.FireYellow: case Pal.Glow: case Pal.Lantern:
                    s.Emission = 1f; break;
            }
            return s;
        }
    }
}
