using AshenRealm.Combat;
using AshenRealm.Story;
using UnityEngine;

namespace AshenRealm.EditorTools
{
    /// <summary>Small painted glyphs authored in pixels, sharing the menu's violet and worn-gold palette.</summary>
    internal static class AbilityIconBuilder
    {
        static Sprite[] cached;

        public static Sprite[] Create()
        {
            if (cached != null && cached.Length == System.Enum.GetValues(typeof(AbilityEffect)).Length && cached[0] != null) return cached;
            cached = new Sprite[System.Enum.GetValues(typeof(AbilityEffect)).Length];
            for (int i = 0; i < cached.Length; i++)
            {
                var canvas = new Pixels((AbilityEffect)i);
                Draw(canvas, (AbilityEffect)i);
                cached[i] = GeneratedAssets.WriteTexture<Sprite>(canvas.Texture(), GeneratedAssets.TextureFolder + "/Ability_" + (AbilityEffect)i + ".png",
                    true, FilterMode.Point);
            }
            return cached;
        }

        sealed class Pixels
        {
            readonly Color[] pixels = new Color[64 * 64];
            public readonly Color Accent;
            public readonly Color Gold = new Color(0.85f, 0.66f, 0.34f);
            public readonly Color Light = new Color(0.98f, 0.91f, 0.72f);
            public readonly Color Dark = new Color(0.14f, 0.09f, 0.2f);

            public Pixels(AbilityEffect effect)
            {
                Accent = PlayerAbilities.EffectColor(effect);
                for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    int inset = Mathf.Min(x, y, 63 - x, 63 - y);
                    if (x + y < 8 || x + 63 - y < 8 || 63 - x + y < 8 || 126 - x - y < 8) continue;
                    Color background = Color.Lerp(new Color(0.065f, 0.04f, 0.09f), new Color(0.18f, 0.105f, 0.2f), y / 63f);
                    pixels[y * 64 + x] = inset <= 1 ? Gold * 0.65f : inset == 3 ? Gold : background;
                    pixels[y * 64 + x].a = 1f;
                }
                Disc(32, 32, 25, new Color(Accent.r * 0.18f, Accent.g * 0.14f, Accent.b * 0.16f));
            }
            public void Dot(int x, int y, Color color)
            {
                if (x > 5 && x < 58 && y > 5 && y < 58) { color.a = 1f; pixels[y * 64 + x] = color; }
            }
            public void Disc(int cx, int cy, int radius, Color color)
            {
                for (int y = cy - radius; y <= cy + radius; y++)
                for (int x = cx - radius; x <= cx + radius; x++)
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius) Dot(x, y, color);
            }
            public void Line(float x1, float y1, float x2, float y2, int thickness, Color color)
            {
                int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x2 - x1), Mathf.Abs(y2 - y1)));
                for (int i = 0; i <= steps; i++) Disc(Mathf.RoundToInt(Mathf.Lerp(x1, x2, i / (float)Mathf.Max(1, steps))),
                    Mathf.RoundToInt(Mathf.Lerp(y1, y2, i / (float)Mathf.Max(1, steps))), thickness, color);
            }
            public void Arc(int x, int y, int radius, float start, float end, int width, Color color)
            {
                for (float a = start; a < end; a += 0.035f)
                    Disc(Mathf.RoundToInt(x + Mathf.Cos(a) * radius), Mathf.RoundToInt(y + Mathf.Sin(a) * radius), width, color);
            }
            public Texture2D Texture()
            {
                var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                texture.SetPixels(pixels); texture.Apply(); return texture;
            }
        }

        static void Cross(Pixels p, int x, int y, int size) { p.Line(x, y - size, x, y + size, 2, p.Light); p.Line(x - size, y + 2, x + size, y + 2, 2, p.Light); }
        static void Blade(Pixels p, int x, int y, int dx = 0)
        {
            p.Line(x - dx, y - 11, x, y - 4, 2, p.Gold);
            p.Line(x - 7, y - 3, x + 6, y - 3, 1, p.Gold);
            p.Line(x, y, x + dx, y + 22, 3, p.Light);
            p.Line(x + 1, y, x + dx + 1, y + 21, 1, p.Accent);
        }
        static void Shield(Pixels p)
        {
            p.Line(17, 47, 46, 47, 2, p.Gold); p.Line(17, 47, 19, 25, 2, p.Gold);
            p.Line(46, 47, 44, 25, 2, p.Gold); p.Line(19, 25, 32, 14, 2, p.Gold); p.Line(44, 25, 32, 14, 2, p.Gold);
            Cross(p, 32, 34, 10);
        }
        static void Heart(Pixels p, int x, int y, int size)
        {
            p.Disc(x - size / 2, y + size / 3, size / 2 + 1, p.Accent);
            p.Disc(x + size / 2, y + size / 3, size / 2 + 1, p.Accent);
            for (int i = 0; i < size; i++) p.Line(x - size + i, y - i / 2, x + size - i, y - i / 2, 1, p.Accent);
            p.Line(x - 4, y + 5, x - 1, y + 7, 1, p.Light);
        }
        static void Bottle(Pixels p, bool fire)
        {
            p.Line(28, 48, 36, 48, 3, p.Gold); p.Line(28, 43, 28, 48, 2, p.Gold); p.Line(36, 43, 36, 48, 2, p.Gold);
            p.Disc(32, 27, 15, p.Gold); p.Disc(32, 27, 12, p.Dark);
            for (int y = 18; y < 32; y++) p.Line(23, y, 41, y, 1, p.Accent);
            p.Line(25, 35, 25, 29, 1, p.Light);
            if (fire) { p.Line(32, 47, 41, 55, 1, p.Accent); p.Line(32, 50, 24, 56, 1, p.Light); }
        }
        static void Bone(Pixels p, int x, int y, float angle, int length)
        {
            int dx = Mathf.RoundToInt(Mathf.Cos(angle) * length), dy = Mathf.RoundToInt(Mathf.Sin(angle) * length);
            p.Line(x, y, x + dx, y + dy, 2, p.Light); p.Disc(x + dx, y + dy, 3, p.Gold);
        }

        static void Draw(Pixels p, AbilityEffect effect)
        {
            switch (effect)
            {
                case AbilityEffect.ShieldBash: Shield(p); p.Line(10, 28, 16, 33, 2, p.Accent); p.Line(47, 34, 54, 39, 2, p.Accent); break;
                case AbilityEffect.Rally:
                    p.Line(23, 12, 23, 52, 2, p.Gold); p.Line(25, 48, 47, 44, 5, p.Accent); p.Line(26, 37, 43, 34, 4, p.Accent); Cross(p, 34, 42, 4); break;
                case AbilityEffect.Backstab: Blade(p, 32, 27, 7); p.Arc(31, 33, 20, 0.5f, 2.9f, 2, p.Gold); break;
                case AbilityEffect.PoisonBlade: Blade(p, 26, 26, 3); p.Disc(44, 22, 5, p.Accent); p.Line(44, 25, 44, 32, 2, p.Accent); break;
                case AbilityEffect.LifeDrain: Heart(p, 35, 34, 12); p.Line(11, 15, 28, 30, 2, p.Light); p.Line(12, 16, 12, 24, 2, p.Gold); break;
                case AbilityEffect.Hex:
                    p.Arc(31, 26, 17, 0.25f, 2.9f, 2, p.Gold); p.Arc(31, 40, 17, 3.4f, 6f, 2, p.Gold); p.Disc(31, 33, 7, p.Accent); p.Line(31, 27, 31, 39, 1, p.Dark); break;
                case AbilityEffect.Smite: Blade(p, 27, 24); Cross(p, 44, 42, 9); break;
                case AbilityEffect.Mend: Heart(p, 32, 31, 13); Cross(p, 32, 32, 8); break;
                case AbilityEffect.BoneSpear: Bone(p, 18, 17, 0.8f, 36); p.Line(43, 44, 46, 53, 2, p.Accent); p.Line(36, 50, 46, 53, 2, p.Accent); break;
                case AbilityEffect.BoneArmor:
                    p.Line(32, 15, 32, 50, 2, p.Gold); for (int i = 0; i < 4; i++) { p.Arc(32, 42 - i * 7, 12 - i, 3.3f, 6.1f, 1, p.Light); } break;
                case AbilityEffect.AcidFlask: Bottle(p, false); p.Disc(46, 47, 3, p.Accent); p.Disc(18, 51, 2, p.Accent); break;
                case AbilityEffect.HealingBrew: Bottle(p, false); Cross(p, 32, 26, 7); break;
                case AbilityEffect.Whirlwind:
                    p.Arc(32, 32, 19, -0.2f, 4.9f, 2, p.Gold); p.Arc(32, 32, 11, 1.1f, 5.9f, 2, p.Light); p.Line(49, 35, 53, 25, 2, p.Accent); break;
                case AbilityEffect.IronSkin: Shield(p); p.Line(15, 17, 15, 44, 2, p.Light); p.Line(49, 17, 49, 44, 2, p.Light); break;
                case AbilityEffect.FanOfKnives: Blade(p, 18, 24, -5); Blade(p, 32, 28); Blade(p, 46, 24, 5); break;
                case AbilityEffect.Caltrops:
                    for (int i = 0; i < 3; i++) { int x = 18 + i * 14, y = 23 + (i % 2) * 11; p.Line(x - 7, y - 4, x + 7, y + 5, 2, p.Gold); p.Line(x - 6, y + 6, x + 7, y - 5, 2, p.Light); p.Line(x, y, x, y + 12, 2, p.Accent); } break;
                case AbilityEffect.FrostLance: Blade(p, 30, 26, 5); p.Line(11, 38, 23, 38, 1, p.Accent); p.Line(17, 32, 17, 44, 1, p.Accent); p.Line(42, 20, 52, 30, 1, p.Light); break;
                case AbilityEffect.ChainLightning: p.Line(43, 53, 26, 35, 3, p.Light); p.Line(26, 35, 40, 35, 3, p.Accent); p.Line(40, 35, 20, 12, 3, p.Light); p.Line(19, 46, 14, 33, 1, p.Gold); p.Line(49, 28, 52, 18, 1, p.Gold); break;
                case AbilityEffect.Sanctuary: p.Arc(32, 25, 19, 0f, Mathf.PI * 2f, 2, p.Gold); Cross(p, 32, 37, 12); break;
                case AbilityEffect.Consecration: p.Arc(32, 20, 20, 0f, Mathf.PI * 2f, 2, p.Gold); p.Disc(32, 43, 7, p.Light); for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4f; p.Line(32 + Mathf.Cos(a) * 10, 43 + Mathf.Sin(a) * 10, 32 + Mathf.Cos(a) * 15, 43 + Mathf.Sin(a) * 15, 1, p.Accent); } break;
                case AbilityEffect.BoneNova: for (int i = 0; i < 8; i++) Bone(p, 32, 32, i * Mathf.PI / 4f, 19); p.Disc(32, 32, 5, p.Accent); break;
                case AbilityEffect.SoulHarvest: p.Disc(27, 33, 10, p.Light); p.Disc(23, 35, 2, p.Dark); p.Disc(31, 35, 2, p.Dark); p.Line(24, 23, 31, 23, 2, p.Gold); p.Arc(34, 36, 18, -0.4f, 1.8f, 2, p.Accent); p.Line(48, 17, 42, 38, 2, p.Gold); break;
                case AbilityEffect.FireBomb: Bottle(p, true); p.Line(47, 12, 47, 22, 2, p.Light); p.Line(42, 17, 52, 17, 2, p.Accent); break;
                case AbilityEffect.FlashPowder: p.Disc(32, 32, 8, p.Light); for (int i = 0; i < 12; i++) { float a = i * Mathf.PI / 6f; p.Line(32 + Mathf.Cos(a) * 13, 32 + Mathf.Sin(a) * 13, 32 + Mathf.Cos(a) * 23, 32 + Mathf.Sin(a) * 23, i % 2 == 0 ? 2 : 1, p.Accent); } break;
            }
        }
    }
}
