using AshenRealm.Combat;
using AshenRealm.Story;
using UnityEngine;

namespace AshenRealm.EditorTools
{
    /// <summary>Small painted glyphs authored in pixels, sharing the menu's violet and worn-gold palette.</summary>
    internal static class AbilityIconBuilder
    {
        static Sprite[] cached, hudCached;

        public static AbilityEffect[] Effects() => (AbilityEffect[])System.Enum.GetValues(typeof(AbilityEffect));

        public static Sprite[] Create()
        {
            AbilityEffect[] effects = Effects();
            if (cached != null && cached.Length == effects.Length && cached.Length > 0 && cached[0] != null) return cached;
            cached = new Sprite[effects.Length];
            for (int i = 0; i < cached.Length; i++)
            {
                var canvas = new Pixels(effects[i]);
                Draw(canvas, effects[i]);
                cached[i] = GeneratedAssets.WriteTexture<Sprite>(canvas.Texture(), GeneratedAssets.TextureFolder + "/Ability_" + effects[i] + ".png",
                    true, FilterMode.Point);
            }
            return cached;
        }

        /// <summary>The stone touch button supplies its own frame; its symbol keeps a circular transparent margin.</summary>
        public static Sprite[] CreateHud()
        {
            AbilityEffect[] effects = Effects();
            if (hudCached != null && hudCached.Length == effects.Length && hudCached.Length > 0 && hudCached[0] != null) return hudCached;
            hudCached = new Sprite[effects.Length];
            for (int i = 0; i < effects.Length; i++)
            {
                var canvas = new Pixels(effects[i], false);
                Draw(canvas, effects[i]);
                hudCached[i] = GeneratedAssets.WriteTexture<Sprite>(canvas.Texture(true),
                    GeneratedAssets.TextureFolder + "/Ability_HUD_" + effects[i] + ".png", true, FilterMode.Point);
            }
            return hudCached;
        }

        sealed class Pixels
        {
            readonly Color[] pixels = new Color[64 * 64];
            public readonly Color Accent;
            public readonly Color Gold = new Color(0.85f, 0.66f, 0.34f);
            public readonly Color Light = new Color(0.98f, 0.91f, 0.72f);
            public readonly Color Dark = new Color(0.14f, 0.09f, 0.2f);

            public Pixels(AbilityEffect effect, bool framed = true)
            {
                Accent = AccentFor(effect);
                if (!framed) return;
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
            public Texture2D Texture(bool circular = false)
            {
                Color[] output = pixels;
                if (circular)
                {
                    // Fit the whole drawing, including diagonal tips, instead of clipping a square at its corners.
                    const float centre = 31.5f, safeRadius = 26f;
                    float radius = 1f;
                    for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                        if (pixels[y * 64 + x].a > 0f)
                            radius = Mathf.Max(radius, Vector2.Distance(new Vector2(x, y), Vector2.one * centre));
                    float scale = Mathf.Min(1f, (safeRadius - 0.75f) / radius);
                    output = new Color[pixels.Length];
                    for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        if ((new Vector2(x, y) - Vector2.one * centre).sqrMagnitude > safeRadius * safeRadius) continue;
                        int sx = Mathf.RoundToInt((x - centre) / scale + centre), sy = Mathf.RoundToInt((y - centre) / scale + centre);
                        if (sx >= 0 && sx < 64 && sy >= 0 && sy < 64) output[y * 64 + x] = pixels[sy * 64 + sx];
                    }
                }
                var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                texture.SetPixels(output); texture.Apply(); return texture;
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

        static AbilityDefinition Definition(AbilityEffect effect) => System.Array.Find(AbilityBook.AllDefinitions, a => a.Effect == effect);

        static Color AccentFor(AbilityEffect effect)
        {
            AbilityDefinition ability = Definition(effect);
            if (ability == null) return PlayerAbilities.EffectColor(effect);
            string id = ability.Id;
            AbilityTierDefinition tier = ability.Tier(1);
            if (id.Contains("frost") || id.Contains("frozen") || id.Contains("blizzard") || id.Contains("glacial") || id.Contains("torpor")) return new Color(0.43f, 0.8f, 1f);
            if (id.Contains("fire") || id.Contains("inferno") || id.Contains("pyre") || id.Contains("fervor")) return new Color(1f, 0.43f, 0.2f);
            if (id.Contains("blood") || id.Contains("bleed") || id == "lacerate" || id == "puncture") return new Color(0.93f, 0.29f, 0.42f);
            if (tier.PoisonPerTurn > 0f || id.Contains("vitriol") || id.Contains("miasma") || id.Contains("solvent") || id == "starter_tonic" || id == "nourishing_stew") return new Color(0.48f, 0.84f, 0.27f);
            if (tier.Holy || ability.Owner == CharacterClass.Warden) return new Color(1f, 0.83f, 0.37f);
            if (ability.Kind == AbilityKind.Heal || tier.Lifesteal > 0f) return new Color(0.93f, 0.29f, 0.42f);
            if (ability.Kind == AbilityKind.Defense) return new Color(0.46f, 0.68f, 0.97f);
            if (ability.Owner == CharacterClass.Necromancer) return new Color(0.7f, 0.48f, 0.94f);
            if (ability.Owner == CharacterClass.Witch || ability.Kind == AbilityKind.Charm) return new Color(0.84f, 0.5f, 0.93f);
            if (ability.Kind == AbilityKind.Buff) return new Color(0.4f, 0.86f, 0.74f);
            return new Color(0.92f, 0.57f, 0.36f);
        }

        static void Star(Pixels p, float x, float y, float radius, Color color, int rays = 5)
        {
            for (int i = 0; i < rays; i++)
            {
                float angle = i * Mathf.PI * 2f / rays + Mathf.PI * 0.5f;
                p.Line(x, y, x + Mathf.Cos(angle) * radius, y + Mathf.Sin(angle) * radius, 1, color);
            }
        }
        static void Drop(Pixels p, int x, int y, int radius, Color color)
        {
            p.Disc(x, y, radius, color);
            p.Line(x, y + radius * 2f, x - radius + 1f, y, 1, color);
            p.Line(x, y + radius * 2f, x + radius - 1f, y, 1, color);
            p.Line(x - 1, y + 1, x - 1, y + radius, 1, p.Light);
        }
        static void Eye(Pixels p, int x, int y, int size, bool closed = false)
        {
            p.Arc(x, y - size / 2, size, 0.55f, 2.6f, 1, p.Gold);
            p.Arc(x, y + size / 2, size, 3.7f, 5.7f, 1, p.Gold);
            if (!closed) { p.Disc(x, y, size / 3 + 1, p.Accent); p.Line(x, y - 3, x, y + 4, 1, p.Light); }
        }
        static void Skull(Pixels p, int x = 32, int y = 34, int radius = 11)
        {
            p.Disc(x, y + 2, radius, p.Light);
            p.Line(x - radius / 2, y - radius, x + radius / 2, y - radius, 3, p.Gold);
            p.Disc(x - radius / 3, y + 3, 2, p.Dark); p.Disc(x + radius / 3, y + 3, 2, p.Dark);
            p.Line(x, y - 1, x - 1, y - 4, 1, p.Dark);
            for (int i = -1; i <= 1; i++) p.Line(x + i * 4, y - radius - 2, x + i * 4, y - radius + 1, 1, p.Dark);
        }
        static void Flame(Pixels p, int x, int y, int size, bool cold = false)
        {
            p.Line(x - size / 2, y, x - size / 2 - 3, y + size, 3, p.Accent);
            p.Line(x - size / 2 - 3, y + size, x + 2, y + size * 2, 3, p.Accent);
            p.Line(x + 2, y + size * 2, x + size / 2 + 3, y + size / 2, 3, p.Accent);
            p.Disc(x, y + 2, size / 2 + 2, p.Accent);
            p.Line(x, y, x + (cold ? -2 : 2), y + size, 2, p.Light);
        }
        static void Snow(Pixels p, int x, int y, int size)
        {
            for (int i = 0; i < 3; i++)
            {
                float angle = i * Mathf.PI / 3f;
                p.Line(x - Mathf.Cos(angle) * size, y - Mathf.Sin(angle) * size,
                    x + Mathf.Cos(angle) * size, y + Mathf.Sin(angle) * size, 1, p.Light);
            }
            p.Disc(x, y, 2, p.Accent);
        }
        static void Bolt(Pixels p, int x, int y, int height)
        {
            p.Line(x + 6, y + height, x - 6, y, 2, p.Light);
            p.Line(x - 6, y, x + 5, y + 2, 2, p.Accent);
            p.Line(x + 5, y + 2, x - 7, y - height, 2, p.Light);
        }
        static void Arrow(Pixels p, int x, int y, bool down = false)
        {
            int direction = down ? -1 : 1;
            p.Line(x, y - direction * 10, x, y + direction * 10, 2, p.Accent);
            p.Line(x - 6, y + direction * 4, x, y + direction * 10, 2, p.Light);
            p.Line(x + 6, y + direction * 4, x, y + direction * 10, 2, p.Light);
        }
        static void Hammer(Pixels p, bool holy = false)
        {
            p.Line(24, 13, 39, 42, 3, p.Gold); p.Line(25, 47, 49, 35, 6, p.Light);
            p.Line(26, 49, 50, 37, 2, p.Accent);
            if (holy) Cross(p, 16, 39, 6);
        }
        static void Axe(Pixels p)
        {
            p.Line(23, 12, 36, 49, 3, p.Gold); p.Arc(33, 43, 15, -0.6f, 1.7f, 4, p.Light);
            p.Line(36, 49, 44, 48, 2, p.Accent);
        }
        static void Fist(Pixels p, bool arm = true)
        {
            for (int i = 0; i < 4; i++) p.Line(23 + i * 6, 29, 23 + i * 6, 43 - Mathf.Abs(i - 1) * 2, 2, p.Light);
            p.Line(20, 29, 43, 29, 4, p.Gold); p.Line(20, 28, 17, 37, 3, p.Accent);
            if (arm) { p.Line(24, 14, 24, 26, 4, p.Accent); p.Line(38, 14, 38, 26, 3, p.Gold); }
        }
        static void Person(Pixels p, int x = 32, int y = 30, bool bones = false)
        {
            if (bones) Skull(p, x, y + 15, 6); else p.Disc(x, y + 15, 5, p.Gold);
            p.Line(x, y + 8, x, y - 5, 3, bones ? p.Light : p.Accent);
            p.Line(x - 9, y + 4, x + 9, y + 4, 2, p.Light);
            p.Line(x, y - 5, x - 7, y - 15, 2, p.Gold); p.Line(x, y - 5, x + 7, y - 15, 2, p.Gold);
        }
        static void Cloud(Pixels p, int y = 36)
        {
            p.Disc(20, y, 7, p.Gold); p.Disc(30, y + 6, 9, p.Gold); p.Disc(43, y + 1, 8, p.Gold);
            p.Line(17, y - 5, 47, y - 5, 4, p.Accent);
        }
        static void Leaf(Pixels p, int x, int y, int size)
        {
            for (int i = 0; i < size; i++) p.Line(x - i / 2, y + i, x + i / 2, y + i + 1, 1, p.Accent);
            p.Line(x, y - 3, x + 2, y + size, 1, p.Light);
        }
        static void Wings(Pixels p, int y = 35)
        {
            for (int i = 0; i < 4; i++)
            {
                p.Line(27, y - 4, 11 + i * 4, y + 13 - i * 2, 2, p.Gold);
                p.Line(37, y - 4, 53 - i * 4, y + 13 - i * 2, 2, p.Gold);
            }
        }
        static void Ribs(Pixels p, bool closed)
        {
            for (int i = 0; i < 4; i++)
            {
                int x = 17 + i * 10;
                Bone(p, x, 15, Mathf.PI / 2f, 31);
                if (!closed) p.Line(x, 45, x + 4, 50, 2, p.Light);
            }
            p.Arc(32, 32, 20, 3.3f, 6.1f, 2, p.Gold);
            if (closed) p.Arc(32, 32, 20, 0.15f, 3f, 2, p.Gold);
        }
        static void Recipe(Pixels p, AbilityDefinition ability)
        {
            string id = ability.Id;
            if (id == "nourishing_stew" || id == "cordial_marrow")
            {
                p.Line(17, 33, 20, 17, 3, p.Gold); p.Line(20, 17, 44, 17, 3, p.Gold); p.Line(44, 17, 47, 33, 3, p.Gold);
                p.Line(17, 33, 47, 33, 3, p.Light);
                if (id == "nourishing_stew") { Leaf(p, 26, 33, 13); p.Line(38, 35, 41, 51, 2, p.Accent); }
                else { Skull(p, 32, 39, 7); Bone(p, 46, 22, 1.25f, 28); }
                return;
            }
            bool slender = id.StartsWith("vial_") || id == "draught_fervor";
            if (slender)
            {
                p.Line(25, 48, 39, 48, 3, p.Gold); p.Line(26, 44, 26, 17, 2, p.Light);
                p.Line(38, 44, 38, 17, 2, p.Light); p.Arc(32, 17, 6, Mathf.PI, Mathf.PI * 2f, 2, p.Gold);
                p.Line(30, 19, 30, 38, 2, p.Accent); p.Line(34, 19, 34, 38, 2, p.Accent);
            }
            else Bottle(p, id == "flask_pyre");
            switch (id)
            {
                case "starter_tonic": Heart(p, 32, 26, 5); p.Line(13, 22, 18, 31, 1, p.Accent); break;
                case "flask_vitriol": Drop(p, 32, 25, 5, p.Accent); p.Disc(47, 42, 3, p.Gold); p.Disc(17, 46, 2, p.Accent); break;
                case "tonic_ward": p.Line(25, 32, 39, 32, 1, p.Light); p.Line(25, 32, 28, 20, 1, p.Light); p.Line(39, 32, 36, 20, 1, p.Light); p.Line(28, 20, 32, 17, 1, p.Light); p.Line(36, 20, 32, 17, 1, p.Light); break;
                case "flask_pyre": Flame(p, 32, 20, 9); Star(p, 49, 17, 6, p.Accent); break;
                case "vial_miasma": p.Disc(18, 32, 6, p.Accent); p.Disc(15, 43, 5, p.Gold); p.Disc(46, 34, 6, p.Accent); break;
                case "brew_vigor": p.Line(24, 25, 29, 30, 2, p.Light); p.Line(29, 30, 35, 21, 2, p.Light); p.Line(35, 21, 42, 28, 2, p.Light); break;
                case "elixir_mend": Cross(p, 32, 25, 7); Heart(p, 49, 44, 5); break;
                case "vial_torpor": p.Arc(45, 37, 9, 1.3f, 5.2f, 2, p.Accent); p.Disc(16, 30, 2, p.Light); p.Disc(46, 17, 2, p.Light); break;
                case "vial_solvent": p.Line(16, 21, 23, 28, 2, p.Gold); p.Line(42, 40, 49, 47, 2, p.Gold); p.Line(25, 36, 37, 27, 1, p.Dark); break;
                case "flask_shatter": for (int i = 0; i < 6; i++) { float a = i * Mathf.PI / 3f; p.Line(32 + Mathf.Cos(a) * 18, 30 + Mathf.Sin(a) * 18, 32 + Mathf.Cos(a) * 24, 30 + Mathf.Sin(a) * 24, 1, p.Light); } break;
                case "draught_fervor": Flame(p, 32, 19, 10); Arrow(p, 49, 33); break;
            }
        }

        static bool DrawWorkbook(Pixels p, AbilityDefinition ability)
        {
            string id = ability.Id;
            if (ability.IsRecipe) { Recipe(p, ability); return true; }
            switch (id)
            {
                case "strike": Blade(p, 31, 26, 8); Star(p, 47, 18, 7, p.Accent); break;
                case "guard": Shield(p); p.Line(23, 20, 41, 20, 1, p.Accent); break;
                case "shieldbash": Shield(p); p.Line(10, 31, 15, 36, 2, p.Accent); p.Line(47, 31, 55, 37, 2, p.Light); break;
                case "rally": p.Line(20, 13, 20, 50, 2, p.Gold); p.Line(23, 46, 45, 41, 5, p.Accent); Person(p, 35, 19); break;
                case "ironskin": Fist(p); for (int i = 0; i < 3; i++) p.Line(19, 22 + i * 7, 44, 22 + i * 7, 1, p.Accent); break;
                case "execute": Axe(p); p.Line(13, 47, 43, 14, 2, p.Accent); break;
                case "warcry": Skull(p, 27, 34, 11); p.Disc(27, 27, 4, p.Dark); p.Arc(30, 33, 17, -1f, 1f, 2, p.Accent); p.Arc(30, 33, 24, -0.7f, 0.7f, 1, p.Gold); break;
                case "zeal": Blade(p, 18, 23, 2); Blade(p, 32, 27, 2); Blade(p, 46, 23, 2); break;
                case "aelanwyr": Blade(p, 32, 25); Bone(p, 17, 35, 0f, 30); Skull(p, 32, 20, 6); break;
                case "whirlwind": p.Arc(32, 32, 20, -0.2f, 5f, 2, p.Gold); p.Arc(32, 32, 11, 1f, 5.9f, 2, p.Light); Blade(p, 35, 23, 5); break;
                case "bash": Hammer(p); Star(p, 21, 20, 8, p.Accent); break;
                case "leapslam": p.Line(22, 47, 29, 30, 4, p.Gold); p.Line(29, 30, 43, 28, 4, p.Light); Arrow(p, 45, 43, true); Star(p, 32, 16, 12, p.Accent); break;
                case "taunt": Eye(p, 27, 37, 12); p.Arc(31, 29, 13, 3.5f, 6f, 2, p.Light); p.Line(45, 34, 53, 38, 2, p.Accent); break;
                case "ignorepain": Person(p); p.Line(11, 13, 52, 51, 2, p.Gold); Drop(p, 18, 23, 3, p.Accent); break;
                case "furiouscharge": Person(p, 34, 29); p.Line(11, 43, 21, 43, 2, p.Accent); p.Line(8, 33, 20, 33, 2, p.Gold); p.Line(13, 23, 21, 23, 2, p.Accent); p.Line(44, 27, 53, 33, 2, p.Light); break;
                case "seismicslam": Fist(p, false); p.Line(10, 20, 28, 20, 2, p.Gold); p.Line(28, 20, 22, 12, 2, p.Accent); p.Line(33, 22, 39, 15, 2, p.Light); p.Line(39, 15, 53, 18, 2, p.Accent); break;
                case "revenge": Blade(p, 33, 24, -3); p.Arc(32, 33, 21, 0.7f, 4.6f, 2, p.Accent); p.Line(13, 26, 14, 37, 2, p.Gold); break;
                case "stalwart": Shield(p); p.Line(12, 13, 52, 13, 2, p.Light); p.Line(12, 15, 12, 40, 2, p.Gold); p.Line(52, 15, 52, 40, 2, p.Gold); break;
                case "undyingrage": Flame(p, 32, 18, 16); Heart(p, 32, 27, 8); break;
                case "bloodthirst": p.Arc(32, 39, 17, 3.6f, 5.8f, 3, p.Light); p.Line(20, 31, 23, 21, 2, p.Gold); p.Line(44, 31, 41, 21, 2, p.Gold); Drop(p, 32, 15, 4, p.Accent); break;
                case "shieldcharge": Shield(p); p.Line(9, 43, 16, 43, 2, p.Light); p.Line(9, 32, 16, 32, 2, p.Accent); p.Line(46, 27, 54, 34, 2, p.Accent); break;
                case "backstab": Blade(p, 32, 27, 7); p.Arc(31, 33, 20, 0.5f, 2.9f, 2, p.Gold); break;
                case "poisonblade": Blade(p, 26, 26, 3); Drop(p, 44, 20, 5, p.Accent); break;
                case "bleed": for (int i = 0; i < 3; i++) { p.Line(15 + i * 11, 47, 23 + i * 11, 28, 2, p.Light); Drop(p, 23 + i * 11, 18 + i % 2 * 4, 3, p.Accent); } break;
                case "cleave": Axe(p); p.Arc(32, 33, 22, 3.5f, 6f, 2, p.Accent); break;
                case "plaguetouch": Fist(p, false); p.Disc(17, 18, 4, p.Accent); p.Disc(31, 13, 3, p.Accent); p.Disc(47, 21, 4, p.Accent); break;
                case "siphoncut": Blade(p, 22, 24); Heart(p, 44, 28, 9); p.Line(34, 43, 44, 40, 2, p.Accent); break;
                case "fanofknives": Blade(p, 18, 24, -5); Blade(p, 32, 28); Blade(p, 46, 24, 5); break;
                case "caltrops": for (int i = 0; i < 3; i++) { int x = 18 + i * 14, y = 23 + i % 2 * 11; p.Line(x - 7, y - 4, x + 7, y + 5, 2, p.Gold); p.Line(x - 6, y + 6, x + 7, y - 5, 2, p.Light); p.Line(x, y, x, y + 12, 2, p.Accent); } break;
                case "shadowstrike": Blade(p, 32, 27, -5); p.Arc(36, 34, 20, 1.3f, 4.8f, 3, p.Accent); break;
                case "impale": Person(p, 32, 28); p.Line(30, 11, 33, 51, 2, p.Light); p.Line(33, 51, 27, 43, 2, p.Gold); p.Line(33, 51, 39, 43, 2, p.Gold); break;
                case "smokescreen": Cloud(p, 37); Person(p, 34, 17); p.Line(16, 14, 49, 48, 1, p.Dark); break;
                case "venomstrike": Blade(p, 36, 24, -5); Drop(p, 17, 34, 4, p.Accent); Drop(p, 47, 18, 3, p.Accent); break;
                case "grimharvest": p.Line(28, 12, 35, 49, 2, p.Gold); p.Arc(26, 42, 16, 0f, 2.6f, 3, p.Light); Skull(p, 44, 20, 6); break;
                case "puncture": p.Line(20, 13, 42, 51, 2, p.Light); p.Line(31, 15, 49, 46, 1, p.Gold); Drop(p, 18, 30, 4, p.Accent); break;
                case "bladefury": for (int i = 0; i < 5; i++) { float a = i * Mathf.PI * 0.4f; p.Line(32 + Mathf.Cos(a) * 7, 32 + Mathf.Sin(a) * 7, 32 + Mathf.Cos(a) * 22, 32 + Mathf.Sin(a) * 22, 2, p.Light); } p.Arc(32, 32, 15, 0f, 5.4f, 1, p.Accent); break;
                case "nightstalker": p.Arc(29, 32, 19, 0.15f, 3f, 3, p.Gold); Eye(p, 29, 31, 11); p.Arc(47, 47, 7, 1.4f, 5f, 2, p.Accent); break;
                case "exploitweakness": Shield(p); p.Line(29, 49, 37, 36, 2, p.Dark); p.Line(37, 36, 29, 27, 2, p.Dark); Star(p, 41, 23, 8, p.Accent); break;
                case "deathblossom": for (int i = 0; i < 6; i++) { float a = i * Mathf.PI / 3f; p.Disc(32 + Mathf.RoundToInt(Mathf.Cos(a) * 13), 32 + Mathf.RoundToInt(Mathf.Sin(a) * 13), 6, p.Accent); p.Line(32, 32, 32 + Mathf.Cos(a) * 24, 32 + Mathf.Sin(a) * 24, 1, p.Light); } break;
                case "shadowdance": p.Line(20, 16, 25, 25, 4, p.Light); p.Line(41, 34, 45, 45, 4, p.Gold); p.Arc(31, 32, 20, 0.2f, 2.5f, 2, p.Accent); p.Arc(31, 32, 20, 3.4f, 5.6f, 2, p.Accent); break;
                case "lacerate": for (int i = 0; i < 3; i++) p.Arc(7 + i * 12, 34, 24, -0.8f, 0.9f, 2, i % 2 == 0 ? p.Light : p.Accent); Drop(p, 43, 15, 3, p.Accent); break;
                case "firebolt": Flame(p, 29, 22, 13, true); p.Line(16, 17, 47, 47, 2, p.Light); break;
                case "hex": Eye(p, 31, 33, 19); p.Line(31, 47, 31, 53, 2, p.Accent); break;
                case "drain": Heart(p, 35, 34, 12); p.Line(11, 15, 28, 30, 2, p.Light); p.Line(12, 16, 12, 24, 2, p.Gold); break;
                case "meditate": Eye(p, 32, 37, 17, true); Star(p, 32, 47, 6, p.Gold); Drop(p, 32, 17, 4, p.Accent); break;
                case "frostlance": Blade(p, 30, 26, 5); Snow(p, 17, 40, 8); break;
                case "frozenorb": p.Disc(32, 32, 17, p.Dark); p.Arc(32, 32, 18, 0f, 6.3f, 2, p.Accent); Snow(p, 32, 32, 13); p.Arc(32, 32, 24, 0.1f, 2f, 1, p.Gold); break;
                case "chainlightning": Bolt(p, 32, 32, 19); p.Disc(15, 42, 4, p.Accent); p.Disc(49, 22, 4, p.Gold); p.Line(15, 42, 25, 36, 1, p.Gold); p.Line(39, 28, 49, 22, 1, p.Accent); break;
                case "blizzard": Cloud(p, 42); Snow(p, 20, 21, 7); Snow(p, 42, 18, 8); break;
                case "inferno": Flame(p, 32, 17, 18); p.Line(13, 13, 51, 13, 2, p.Gold); break;
                case "bonespear": Bone(p, 17, 15, 0.82f, 41); p.Line(43, 44, 48, 52, 2, p.Accent); break;
                case "corpseexplosion": Skull(p); for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4f; p.Line(32 + Mathf.Cos(a) * 17, 32 + Mathf.Sin(a) * 17, 32 + Mathf.Cos(a) * 24, 32 + Mathf.Sin(a) * 24, 1, p.Accent); } break;
                case "teeth": for (int i = 0; i < 3; i++) { int x = 18 + i * 14; p.Line(x - 3, 44, x, 19, 3, p.Light); p.Line(x, 19, x + 5, 37, 2, p.Gold); } break;
                case "amplifydamage": p.Arc(32, 32, 18, 0.2f, 2.8f, 2, p.Gold); p.Arc(32, 32, 18, 3.5f, 5.9f, 2, p.Gold); Star(p, 32, 32, 14, p.Accent); break;
                case "ironmaiden": Person(p); Ribs(p, true); p.Line(12, 47, 20, 40, 2, p.Accent); p.Line(52, 47, 44, 40, 2, p.Accent); break;
                case "energyshield": p.Disc(32, 32, 15, p.Dark); p.Arc(32, 32, 21, 0f, 6.3f, 2, p.Accent); Eye(p, 32, 32, 11); break;
                case "staticfield": p.Arc(32, 31, 21, 0f, 6.3f, 1, p.Gold); Bolt(p, 23, 33, 12); Bolt(p, 44, 31, 10); break;
                case "glacialspike": for (int i = 0; i < 3; i++) { int x = 17 + i * 15, top = i == 1 ? 53 : 43; p.Line(x - 5, 16, x, top, 2, p.Light); p.Line(x, top, x + 6, 16, 2, p.Accent); } break;
                case "firewall": for (int i = 0; i < 3; i++) Flame(p, 17 + i * 15, 20, 10 + i % 2 * 3); p.Line(11, 14, 53, 14, 2, p.Gold); break;
                case "lowerresist": p.Arc(32, 32, 20, 0.4f, 2.6f, 2, p.Accent); p.Arc(32, 32, 20, 3.6f, 5.7f, 2, p.Gold); p.Line(25, 53, 36, 39, 2, p.Light); p.Line(36, 39, 27, 27, 2, p.Light); Arrow(p, 47, 18, true); break;
                case "convince": Eye(p, 32, 38, 16); p.Arc(32, 27, 14, 3.3f, 6f, 2, p.Accent); p.Line(32, 14, 39, 20, 2, p.Gold); break;
                case "smite": Blade(p, 27, 24); Cross(p, 45, 42, 8); break;
                case "prayer": p.Line(20, 17, 31, 39, 4, p.Gold); p.Line(44, 17, 33, 39, 4, p.Light); Cross(p, 32, 49, 5); break;
                case "heal": Heart(p, 32, 31, 13); Cross(p, 32, 32, 8); break;
                case "sanctuary": p.Arc(32, 26, 21, 0f, Mathf.PI, 2, p.Gold); Person(p, 32, 23); p.Line(12, 16, 52, 16, 2, p.Accent); break;
                case "rallyalt": p.Line(21, 12, 21, 51, 2, p.Gold); p.Line(23, 47, 45, 44, 5, p.Accent); Cross(p, 34, 41, 5); p.Arc(33, 19, 14, 0f, Mathf.PI, 1, p.Light); break;
                case "holybolt": p.Line(15, 15, 47, 47, 3, p.Light); Cross(p, 43, 43, 9); p.Line(12, 26, 26, 12, 1, p.Gold); break;
                case "blessedhammer": Hammer(p, true); p.Arc(32, 33, 23, -0.8f, 2.4f, 2, p.Accent); break;
                case "fistoftheheavens": Fist(p, false); for (int i = 0; i < 4; i++) p.Line(13 + i * 12, 54, 18 + i * 9, 45, 1, p.Accent); Arrow(p, 47, 19, true); break;
                case "prayeraura": p.Arc(32, 25, 21, 0f, 6.3f, 1, p.Gold); Cross(p, 32, 39, 10); Cross(p, 16, 20, 5); Cross(p, 47, 20, 5); break;
                case "cleansing": Drop(p, 35, 29, 10, p.Gold); Leaf(p, 20, 15, 17); Star(p, 44, 48, 6, p.Light); break;
                case "vigor": Fist(p); Arrow(p, 49, 38); p.Arc(19, 29, 10, 1.5f, 4.6f, 2, p.Gold); break;
                case "redemption": Heart(p, 32, 37, 11); p.Arc(32, 26, 18, 3.4f, 6f, 3, p.Gold); Arrow(p, 49, 42); break;
                case "conviction": Eye(p, 32, 35, 18); p.Line(16, 49, 29, 43, 2, p.Light); p.Line(48, 49, 35, 43, 2, p.Light); p.Line(32, 17, 32, 24, 2, p.Accent); break;
                case "holyshield": Shield(p); Star(p, 32, 37, 10, p.Accent, 8); break;
                case "consecration": p.Arc(32, 20, 20, 0f, 6.3f, 2, p.Gold); Star(p, 32, 44, 14, p.Accent, 8); break;
                case "salvation": Wings(p); Heart(p, 32, 30, 11); Cross(p, 32, 49, 5); break;
                case "zealotsoath": p.Line(19, 16, 19, 47, 4, p.Gold); p.Line(44, 16, 44, 47, 4, p.Light); p.Line(20, 20, 44, 20, 2, p.Accent); Blade(p, 32, 25); break;
                case "divinegrace": p.Arc(29, 25, 16, 3.5f, 6f, 3, p.Gold); Star(p, 34, 42, 12, p.Light, 6); p.Line(45, 52, 45, 46, 1, p.Accent); break;
                case "lastrites": Skull(p, 28, 30, 10); Cross(p, 47, 37, 9); p.Line(15, 51, 15, 35, 2, p.Gold); Flame(p, 15, 47, 4); break;
                case "auraoflight": p.Disc(32, 32, 10, p.Light); Star(p, 32, 32, 24, p.Accent, 12); p.Arc(32, 32, 16, 0f, 6.3f, 1, p.Gold); break;
                case "n_bonespear": Bone(p, 17, 14, 0.85f, 42); Skull(p, 40, 43, 7); break;
                case "n_teeth": Skull(p, 32, 44, 8); for (int i = 0; i < 4; i++) p.Line(15 + i * 11, 30, 19 + i * 9, 12, 2, p.Light); break;
                case "n_poisonnova": p.Arc(32, 32, 20, 0f, 6.3f, 2, p.Accent); Skull(p, 32, 33, 8); Drop(p, 13, 15, 3, p.Accent); Drop(p, 50, 47, 3, p.Accent); break;
                case "n_corpseburst": Skull(p, 32, 31, 10); Bone(p, 13, 48, -0.7f, 13); Bone(p, 42, 47, 0.4f, 14); Star(p, 32, 31, 23, p.Accent, 6); break;
                case "n_bonespirit": Skull(p, 32, 41, 11); p.Arc(30, 26, 12, 3f, 5.9f, 3, p.Accent); p.Line(18, 27, 25, 13, 3, p.Gold); p.Line(25, 13, 35, 18, 2, p.Light); break;
                case "n_amplify": Skull(p, 32, 37, 10); p.Line(13, 17, 24, 28, 2, p.Accent); p.Line(51, 17, 40, 28, 2, p.Accent); p.Line(32, 14, 32, 22, 2, p.Gold); break;
                case "n_lowerresist": Ribs(p, false); p.Line(17, 51, 44, 14, 2, p.Dark); Arrow(p, 47, 35, true); break;
                case "n_bloodstar": Star(p, 32, 34, 23, p.Accent, 5); Drop(p, 32, 31, 7, p.Accent); p.Arc(32, 34, 13, 0f, 6.3f, 1, p.Light); break;
                case "n_bonearmor": p.Line(32, 15, 32, 50, 2, p.Gold); for (int i = 0; i < 4; i++) p.Arc(32, 42 - i * 7, 12 - i, 3.3f, 6.1f, 1, p.Light); break;
                case "n_bonewall": Ribs(p, false); p.Line(11, 13, 53, 13, 2, p.Gold); break;
                case "n_boneprison": Ribs(p, true); Eye(p, 32, 30, 8); break;
                case "n_claygolem": Person(p, 32, 30); p.Line(22, 34, 18, 24, 5, p.Gold); p.Line(42, 34, 46, 24, 5, p.Gold); p.Line(26, 31, 38, 31, 5, p.Accent); break;
                case "n_dimvision": Eye(p, 32, 35, 19); p.Line(14, 16, 51, 50, 3, p.Dark); p.Line(14, 14, 51, 48, 1, p.Light); break;
                case "n_weaken": Fist(p, false); p.Line(17, 24, 40, 17, 3, p.Gold); Arrow(p, 49, 33, true); break;
                case "n_lifetap": Heart(p, 32, 34, 11); p.Arc(32, 33, 21, 0f, 6.3f, 1, p.Gold); p.Line(14, 18, 22, 27, 2, p.Accent); p.Line(50, 18, 42, 27, 2, p.Accent); break;
                case "n_decrepify": Skull(p, 22, 36, 8); p.Line(36, 49, 50, 49, 2, p.Gold); p.Line(36, 17, 50, 17, 2, p.Gold); p.Line(37, 46, 49, 20, 1, p.Light); p.Line(49, 46, 37, 20, 1, p.Light); break;
                case "charm": Eye(p, 32, 41, 16); Heart(p, 32, 23, 10); p.Line(16, 47, 12, 52, 1, p.Gold); p.Line(48, 47, 52, 52, 1, p.Gold); break;
                default: return false;
            }
            return true;
        }

        static void Metadata(Pixels p, AbilityDefinition ability)
        {
            AbilityTierDefinition tier = ability.Tier(1);
            if (tier.PoisonPerTurn > 0f) Drop(p, 50, 13, 2, p.Accent);
            else if (tier.Holy) Cross(p, 50, 14, 3);
            else if (tier.ShieldDefMultiplier > 0f) p.Arc(50, 16, 4, 3.3f, 6.1f, 1, p.Gold);
            else if (tier.HealFlat > 0f || tier.HealMagMultiplier > 0f || tier.RegenerationPerTurn > 0f) Cross(p, 13, 14, 3);
            if (ability.Signature) Star(p, 50, 50, 4, p.Gold, 4);
            if (ability.Path != NecromancerPath.None)
                p.Line(11, 49, ability.Path == NecromancerPath.Attack ? 16 : 11, 54, 1, p.Accent);
        }

        static void DrawDelivery(Pixels p, AbilityDefinition ability)
        {
            // Future records get an honest delivery symbol before an illustrator adds their named motif.
            switch (ability.Delivery)
            {
                case AbilityDelivery.Melee: Blade(p, 31, 25, 6); break;
                case AbilityDelivery.Bolt: p.Line(15, 15, 48, 48, 3, p.Accent); Star(p, 44, 45, 8, p.Light); break;
                case AbilityDelivery.Fan: for (int i = 0; i < 3; i++) p.Line(32, 15, 14 + i * 18, 48, 2, p.Light); break;
                case AbilityDelivery.Chain: p.Line(15, 42, 31, 22, 2, p.Gold); p.Line(31, 22, 49, 40, 2, p.Accent); p.Disc(15, 42, 5, p.Light); p.Disc(31, 22, 5, p.Light); p.Disc(49, 40, 5, p.Light); break;
                case AbilityDelivery.Nova: Star(p, 32, 32, 22, p.Accent, 8); p.Disc(32, 32, 8, p.Light); break;
                case AbilityDelivery.Field: p.Arc(32, 21, 21, 0f, 6.3f, 2, p.Gold); Star(p, 32, 40, 11, p.Accent); break;
                case AbilityDelivery.Charge: Person(p, 36, 30); p.Line(9, 33, 22, 33, 3, p.Accent); p.Line(12, 22, 24, 22, 2, p.Gold); break;
                case AbilityDelivery.Leap: Person(p, 31, 36); p.Arc(31, 24, 20, 0.2f, 2.9f, 2, p.Accent); break;
                case AbilityDelivery.Orb: p.Disc(32, 32, 15, p.Accent); p.Arc(32, 32, 23, 0f, 6.3f, 1, p.Gold); Star(p, 32, 32, 10, p.Light); break;
                case AbilityDelivery.Target: Eye(p, 32, 32, 19); break;
                default:
                    if (ability.Kind == AbilityKind.Defense) Shield(p);
                    else if (ability.Kind == AbilityKind.Heal) { Heart(p, 32, 30, 13); Cross(p, 32, 33, 7); }
                    else { Person(p); p.Arc(32, 30, 23, 0f, 6.3f, 1, p.Accent); }
                    break;
            }
        }

        static void Draw(Pixels p, AbilityEffect effect)
        {
            AbilityDefinition ability = Definition(effect);
            if (ability != null)
            {
                if (!DrawWorkbook(p, ability)) DrawDelivery(p, ability);
                Metadata(p, ability);
                return;
            }
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
                default:
                    // New effects remain legible until they receive an authored symbol.
                    uint mark = unchecked((uint)effect * 2654435761u);
                    p.Arc(32, 32, 19, 0f, Mathf.PI * 2f, 1, p.Gold);
                    for (int i = 0; i < 6; i++)
                    {
                        float angle = i * Mathf.PI / 3f;
                        p.Line(32, 32, 32 + Mathf.Cos(angle) * ((mark & (1u << i)) != 0 ? 18f : 10f),
                            32 + Mathf.Sin(angle) * ((mark & (1u << i)) != 0 ? 18f : 10f), 2, i % 2 == 0 ? p.Accent : p.Light);
                    }
                    p.Disc(32, 32, 4, p.Light);
                    break;
            }
        }
    }
}
