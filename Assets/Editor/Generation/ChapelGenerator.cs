using System.Collections.Generic;
using AshenRealm.Core;
using AshenRealm.Interactions;
using UnityEngine;

namespace AshenRealm.EditorTools
{
    /// <summary>Key positions of the chapel for the scene builder.</summary>
    internal sealed class ChapelLayout : RoomLayout
    {
        public readonly List<(Vector3 centre, Vector3 size)> Relics = new List<(Vector3, Vector3)>();
    }

    /// <summary>
    /// The Grey Chapel from the inside, laid out after the concept painting as seen from the street door: two blocks of pews
    /// down the nave between stone columns, two steps, the iron rail, and behind it the book on its lectern and the relics on
    /// their cushions, with the Choir at its work under the tall window.
    /// </summary>
    internal sealed class ChapelGenerator : RoomGenerator
    {
        const float HalfWidth = 7f;
        const float HalfDepth = 10f;
        const float Height = 8.6f;
        const float WallThickness = 0.6f;
        const float ChoirFloor = 0.4f;
        const float ChoirStart = 2.6f;
        const float StepRun = 0.7f;
        const float RailZ = 3.5f;
        const float RailHeight = 1.15f;
        const float ColumnX = 4.4f;
        const float PewInner = 0.95f;
        const float PewLength = 2.8f;

        static readonly Color WindowLight = new Color(0.36f, 0.42f, 0.54f);
        static readonly Color WindowGlass = new Color(0.5f, 0.62f, 0.84f);
        static readonly Color BannerCloth = new Color(0.36f, 0.34f, 0.33f);

        public ChapelGenerator(Material material, Vector3 origin, int seed) : base(material, origin, seed) { }

        protected override Color Dark => new Color(0.27f, 0.26f, 0.29f);

        public ChapelLayout Generate(Transform parent)
        {
            Begin(parent);

            BuildShell();
            StreetDoor(-HalfDepth + 0.04f);
            BuildColumns();
            BuildPews();
            BuildChoir();
            BuildRail();
            BuildLectern();
            var layout = new ChapelLayout
            {
                Entry = origin + new Vector3(0f, 0.05f, -8.2f),
                DoorOut = origin + new Vector3(0f, 0f, -HalfDepth + 0.3f),
            };
            BuildRelics(layout);
            BuildWindow();
            BuildBanners();
            BuildMonks();
            BuildVotiveNave();
            Finish("Chapel_Room");

            Light("Chapel Candles", new Vector3(0f, ChoirFloor + 1.9f, 5f), new Color(1f, 0.72f, 0.42f), 8f, 0.9f, 0.2f);
            WorldVista.Add(root, "Chapel_Processional_Nave", origin + new Vector3(0f, 1.65f, -8.1f), origin + new Vector3(0f, 3.1f, 7f));
            WorldVista.Add(root, "Chapel_Votive_Alcove", origin + new Vector3(-0.3f, 1.65f, -4.9f), origin + new Vector3(-6.7f, 3f, -4.3f));
            WorldVista.Add(root, "Chapel_Rose_and_Choir", origin + new Vector3(0f, 1.65f, 0.6f), origin + new Vector3(0f, 4.1f, 8.8f));
            Person(layout, "choir", new Vector3(0f, ChoirFloor, 4.75f), 0f, PeopleMeshes.Monk);
            return layout;
        }

        // ---- Floor, walls, ceiling -------------------------------------------------------------------------

        void BuildVotiveNave()
        {
            // A pale inlaid aisle leads the eye between the dark pews and stops before the choir's steps.
            for (float z = -8.6f; z < 1.5f; z += 0.74f)
            {
                room.SetColor(Pal.Stone, 0.62f);
                room.Box(new Vector3(0f, 0.018f, z), new Vector3(1.48f, 0.025f, 0.7f));
                foreach (float side in new[] { -1f, 1f })
                {
                    room.SetColor(Pal.Brass, 0.38f);
                    room.Box(new Vector3(side * 0.68f, 0.035f, z), new Vector3(0.055f, 0.012f, 0.65f));
                }
                room.Push(new Vector3(0f, 0.036f, z), Quaternion.Euler(0f, 45f, 0f), Vector3.one);
                room.SetColor(Pal.PaintBlue, 0.35f);
                room.Box(Vector3.zero, new Vector3(0.22f, 0.015f, 0.22f));
                room.Pop();
            }
            TownDetailMeshes.CandleWheel(room, new Vector3(0f, 5.6f, -4.3f), 1.45f, 8.2f);
            AddGlow(new Vector3(0f, 4.1f, -4.3f), CandleLight * 0.75f, 10f);

            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 at = new Vector3(side * (HalfWidth - 0.3f), 0f, -4.3f);
                TownDetailMeshes.RoseWindow(room, at + new Vector3(side * 0.14f, 5.4f, 0f), Quaternion.Euler(0f, side > 0f ? 90f : -90f, 0f), 1.05f);
                AddGlow(at + new Vector3(-side * 0.8f, 4.4f, 0f), WindowLight * 1.25f, 8f);
                room.ShadedBox(at + new Vector3(0f, 0.3f, 0f), new Vector3(0.7f, 0.6f, 2.05f), Pal.StoneMoss, rng, 0.34f, 0.46f);
                room.ShadedBox(at + new Vector3(-side * 0.07f, 1.17f, 0f), new Vector3(0.83f, 0.18f, 2.25f), Pal.Stone, rng, 0.45f, 0.58f);
                Solid("Votive Shrine", at + new Vector3(-side * 0.07f, 0.6f, 0f), new Vector3(0.88f, 1.2f, 2.25f));
                room.SetColor(Pal.Stone, 0.42f);
                room.Box(at + new Vector3(side * 0.12f, 2.06f, 0f), new Vector3(0.12f, 1.65f, 1.8f));
                for (int i = 0; i < 5; i++)
                {
                    float z = -0.8f + i * 0.4f;
                    Candle(at + new Vector3(-side * 0.25f, 1.28f, z), 0.14f + i % 3 * 0.08f);
                    room.SetColor(Pal.Bone, 0.5f);
                    room.Box(at + new Vector3(-side * 0.34f, 1.48f, z), new Vector3(0.04f, 0.14f, 0.22f));
                }
                foreach (float z in new[] { -1.18f, 1.18f })
                {
                    room.ShadedBox(at + new Vector3(0f, 1.8f, z), new Vector3(0.45f, 3.6f, 0.3f), Pal.Stone, rng, 0.37f, 0.5f);
                    room.ShadedBox(at + new Vector3(0f, 3.65f, z), new Vector3(0.62f, 0.18f, 0.48f), Pal.Stone, rng, 0.45f, 0.56f);
                }
            }
            WorldVista.Inspect(root, "Chapel Nameless Votive Shrine", origin + new Vector3(-6.27f, 1.7f, -4.3f),
                new Vector3(0.2f, 1.2f, 1.75f), GameText.ChapelVotiveLore);

            // Low stacks behind the rail support the book-keeper's work without hiding the central portrait or relics.
            foreach (float side in new[] { -1f, 1f })
            for (int pile = 0; pile < 3; pile++)
            for (int book = 0; book < 4; book++)
            {
                Vector3 p = new Vector3(side * 5.45f, ChoirFloor + 0.08f + book * 0.15f, 7.1f + pile * 0.65f);
                room.SetColor(book % 2 == 0 ? Pal.ClothRed : Pal.PaintBlue, 0.26f);
                room.Push(p, Quaternion.Euler(0f, (book - 2) * 7f, 0f), Vector3.one);
                room.Box(Vector3.zero, new Vector3(0.62f, 0.13f, 0.43f));
                room.SetColor(Pal.Bone, 0.4f);
                room.Box(new Vector3(0f, 0f, -0.22f), new Vector3(0.54f, 0.09f, 0.025f));
                room.Pop();
            }
        }

        void BuildShell()
        {
            Flagstones(HalfWidth, HalfDepth, 1.7f, 0f, 0.26f, 0.42f);
            Solid("Floor", new Vector3(0f, -0.5f, 0f), new Vector3(60f, 1f, 60f));

            // Leaves the wind brought in under the door, gathered against the pews.
            for (int i = 0; i < 60; i++)
            {
                room.SetColor(rng.NextDouble() < 0.5 ? Pal.LeafOrange : Pal.Bark, R(0.2f, 0.42f));
                float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                room.Box(new Vector3(side * R(0.2f, 1.1f), 0.012f, R(-9f, 2f)), new Vector3(R(0.05f, 0.11f), 0.01f, R(0.05f, 0.11f)));
            }

            for (float x = -HalfWidth; x < HalfWidth; x += LongPiece)
            for (float z = -HalfDepth; z < HalfDepth; z += LongPiece)
                room.ShadedBox(new Vector3(x + LongPiece * 0.5f, Height + 0.1f, z + LongPiece * 0.5f), new Vector3(LongPiece, 0.2f, LongPiece), Pal.Stone, rng, 0.14f, 0.2f);
            Solid("Ceiling", new Vector3(0f, Height + 0.5f, 0f), new Vector3(60f, 1f, 60f));

            float outer = HalfWidth + WallThickness * 0.5f;
            float end = HalfDepth + WallThickness * 0.5f;
            StoneWall(new Vector3(-outer, 0f, -HalfDepth), new Vector3(-outer, 0f, HalfDepth), Height, WallThickness, 0.3f, 0.46f);
            StoneWall(new Vector3(outer, 0f, -HalfDepth), new Vector3(outer, 0f, HalfDepth), Height, WallThickness, 0.3f, 0.46f);
            StoneWall(new Vector3(-HalfWidth, 0f, -end), new Vector3(HalfWidth, 0f, -end), Height, WallThickness, 0.3f, 0.46f);
            StoneWall(new Vector3(-HalfWidth, 0f, end), new Vector3(HalfWidth, 0f, end), Height, WallThickness, 0.3f, 0.46f);
            Solid("West Wall", new Vector3(-outer, Height * 0.5f, 0f), new Vector3(WallThickness, Height, 40f));
            Solid("East Wall", new Vector3(outer, Height * 0.5f, 0f), new Vector3(WallThickness, Height, 40f));
            Solid("South Wall", new Vector3(0f, Height * 0.5f, -end), new Vector3(40f, Height, WallThickness));
            Solid("North Wall", new Vector3(0f, Height * 0.5f, end), new Vector3(40f, Height, WallThickness));
        }

        // ---- Columns and arches ------------------------------------------------------------------------------

        void BuildColumns()
        {
            float[] rows = { -6.5f, -2f, 2.5f };
            foreach (float z in rows)
            foreach (float side in new[] { -1f, 1f })
                Column(new Vector3(side * ColumnX, 0f, z));

            const float spring = 5.4f;
            foreach (float z in rows)
                Arch(new Vector3(-ColumnX, spring, z), new Vector3(ColumnX, spring, z), 2.9f);
            for (int i = 0; i + 1 < rows.Length; i++)
            foreach (float side in new[] { -1f, 1f })
                Arch(new Vector3(side * ColumnX, spring, rows[i]), new Vector3(side * ColumnX, spring, rows[i + 1]), 1.5f);

            // Sconces on the columns that face the nave, and a niche with its stub of candle in each bay.
            foreach (float z in rows)
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 at = new Vector3(side * (ColumnX - 0.62f), 2.5f, z);
                Iron(at + new Vector3(side * 0.1f, -0.04f, 0f), new Vector3(0.24f, 0.05f, 0.05f));
                Candle(at, 0.24f);
            }
            foreach (float z in new[] { -4.2f, 0.25f })
            foreach (float side in new[] { -1f, 1f })
            {
                float wall = side * (HalfWidth - 0.02f);
                room.SetColor(Pal.Stone, 0.1f);
                room.Box(new Vector3(wall, 2.1f, z), new Vector3(0.05f, 1.3f, 0.8f));
                room.ShadedBox(new Vector3(wall - side * 0.14f, 1.42f, z), new Vector3(0.3f, 0.08f, 1f), Pal.Stone, rng, 0.4f, 0.5f);
                Candle(new Vector3(wall - side * 0.14f, 1.46f, z - 0.2f), 0.26f);
                Candle(new Vector3(wall - side * 0.14f, 1.46f, z + 0.16f), 0.18f);
            }
        }

        void Column(Vector3 at)
        {
            room.ShadedBox(at + Vector3.up * 0.2f, new Vector3(1.5f, 0.4f, 1.5f), Pal.Stone, rng, 0.34f, 0.44f);
            room.SetColor(Pal.Stone, 0.44f);
            room.Prism(at + Vector3.up * 0.4f, 0.68f, 0.56f, 0.3f, 12);
            const int drums = 5;
            float drumHeight = (5.0f - 0.7f) / drums;
            for (int i = 0; i < drums; i++)
            {
                room.SetColor(Pal.Stone, R(0.36f, 0.52f));
                room.Prism(at + Vector3.up * (0.7f + i * drumHeight), 0.52f, 0.5f, drumHeight, 12, false, false);
            }
            room.SetColor(Pal.Stone, 0.5f);
            room.Prism(at + Vector3.up * 5f, 0.52f, 0.78f, 0.36f, 12);
            room.ShadedBox(at + Vector3.up * 5.42f, new Vector3(1.7f, 0.14f, 1.7f), Pal.Stone, rng, 0.42f, 0.5f);
            Solid("Column", at + Vector3.up * 2.75f, new Vector3(1.05f, 5.5f, 1.05f));
        }

        /// <summary>A pointed arch of stone blocks between two points at the same height.</summary>
        void Arch(Vector3 from, Vector3 to, float rise)
        {
            const int blocks = 9;
            Vector3 span = to - from;
            bool alongX = Mathf.Abs(span.x) > Mathf.Abs(span.z);
            for (int side = 0; side < 2; side++)
            for (int i = 0; i < blocks; i++)
            {
                float t0 = i / (float)blocks, t1 = (i + 1f) / blocks;
                Vector3 a = ArchPoint(from, to, rise, t0, side), b = ArchPoint(from, to, rise, t1, side);
                Vector3 centre = (a + b) * 0.5f;
                Vector3 along = b - a;
                float tilt = Mathf.Atan2(along.y, alongX ? along.x : along.z) * Mathf.Rad2Deg;
                room.Push(centre, alongX ? Quaternion.Euler(0f, 0f, tilt) : Quaternion.Euler(-tilt, 0f, 0f), Vector3.one);
                Vector3 size = alongX ? new Vector3(along.magnitude + 0.04f, 0.5f, 0.56f) : new Vector3(0.56f, 0.5f, along.magnitude + 0.04f);
                room.ShadedBox(Vector3.zero, size, Pal.Stone, rng, 0.3f, 0.44f);
                room.Pop();
            }
        }

        static Vector3 ArchPoint(Vector3 from, Vector3 to, float rise, float t, int side)
        {
            Vector3 foot = side == 0 ? from : to, crown = (from + to) * 0.5f;
            float a = t * Mathf.PI * 0.5f;
            return Vector3.Lerp(foot, crown, 1f - Mathf.Cos(a)) + Vector3.up * (Mathf.Sin(a) * rise);
        }

        // ---- The pews ----------------------------------------------------------------------------------------

        void BuildPews()
        {
            for (float z = -7.6f; z <= 1f; z += 1.42f)
            foreach (float side in new[] { -1f, 1f })
                Pew(new Vector3(side * (PewInner + PewLength * 0.5f), 0f, z));
        }

        void Pew(Vector3 at)
        {
            const float seat = 0.46f, top = 1.04f;
            room.ShadedBox(at + new Vector3(0f, seat, 0.06f), new Vector3(PewLength, 0.07f, 0.44f), Pal.Wood, rng, 0.3f, 0.38f);
            room.ShadedBox(at + new Vector3(0f, 0.74f, -0.2f), new Vector3(PewLength, 0.56f, 0.06f), Pal.Wood, rng, 0.26f, 0.34f);
            room.ShadedBox(at + new Vector3(0f, top - 0.02f, -0.2f), new Vector3(PewLength + 0.04f, 0.06f, 0.1f), Pal.Wood, rng, 0.36f, 0.42f);
            foreach (float s in new[] { -1f, 1f })
            {
                float x = s * (PewLength * 0.5f + 0.03f);
                room.ShadedBox(at + new Vector3(x, 0.52f, 0f), new Vector3(0.09f, 1.04f, 0.66f), Pal.Wood, rng, 0.34f, 0.42f);
                room.SetColor(Pal.Wood, 0.38f);
                room.Push(at + new Vector3(x, 1.04f, 0f), Quaternion.Euler(0f, 0f, 90f), new Vector3(1f, 1f, 1f));
                room.Prism(new Vector3(0f, -0.045f, 0f), 0.33f, 0.33f, 0.09f, 8);
                room.Pop();

                // The quatrefoil cut through the end panel.
                room.SetColor(Pal.Black, 0.22f);
                room.Box(at + new Vector3(x, 0.92f, 0f), new Vector3(0.1f, 0.16f, 0.07f));
                room.Box(at + new Vector3(x, 0.92f, 0f), new Vector3(0.1f, 0.07f, 0.16f));
            }
            Solid("Pew", at + new Vector3(0f, 0.52f, 0f), new Vector3(PewLength + 0.1f, 1.04f, 0.68f));
        }

        // ---- The choir, behind the rail ----------------------------------------------------------------------

        void BuildChoir()
        {
            float first = ChoirStart - StepRun;
            float width = ColumnX * 2f + 3f;
            for (float x = -width * 0.5f; x < width * 0.5f; x += 1.5f)
            {
                room.ShadedBox(new Vector3(x + 0.75f, 0.1f, first + StepRun * 0.5f), new Vector3(1.48f, 0.2f, StepRun - 0.03f), Pal.Stone, rng, 0.34f, 0.46f);
                room.ShadedBox(new Vector3(x + 0.75f, ChoirFloor * 0.5f, ChoirStart + 0.35f), new Vector3(1.48f, ChoirFloor, 0.68f), Pal.Stone, rng, 0.36f, 0.48f);
            }
            Solid("Lower Step", new Vector3(0f, 0.1f, first + StepRun * 0.5f), new Vector3(HalfWidth * 2f, 0.2f, StepRun));

            for (float x = -HalfWidth; x < HalfWidth; x += 1.7f)
            for (float z = ChoirStart + 0.7f; z < HalfDepth; z += 1.7f)
            {
                float right = Mathf.Min(x + 1.7f, HalfWidth), far = Mathf.Min(z + 1.7f, HalfDepth);
                room.ShadedBox(new Vector3((x + right) * 0.5f, ChoirFloor * 0.5f, (z + far) * 0.5f), new Vector3(right - x - 0.04f, ChoirFloor, far - z - 0.04f), Pal.Stone, rng, 0.3f, 0.42f);
            }
            Solid("Choir Floor", new Vector3(0f, ChoirFloor * 0.5f, ChoirStart + (HalfDepth - ChoirStart) * 0.5f), new Vector3(HalfWidth * 2f, ChoirFloor, HalfDepth - ChoirStart));
        }

        void BuildRail()
        {
            float y = ChoirFloor;
            for (float x = -HalfWidth + 0.1f; x <= HalfWidth; x += 0.22f)
                Iron(new Vector3(x, y + RailHeight * 0.5f, RailZ), new Vector3(0.03f, RailHeight, 0.03f));
            foreach (float up in new[] { 0.12f, 0.92f, RailHeight })
            {
                room.SetColor(Pal.SteelDark, 0.26f);
                room.Box(new Vector3(0f, y + up, RailZ), new Vector3(HalfWidth * 2f, 0.05f, 0.06f));
            }

            // Rust down the bars, and the taller posts with their knobs; the gate between the two middle ones stays shut.
            for (int i = 0; i < 30; i++)
            {
                room.SetColor(Pal.RoofTile, R(0.2f, 0.36f));
                room.Box(new Vector3(R(-6.8f, 6.8f), y + R(0.2f, 1.1f), RailZ - 0.02f), new Vector3(0.05f, R(0.08f, 0.26f), 0.02f));
            }
            foreach (float x in new[] { -6.2f, -3.6f, -1.1f, 1.1f, 3.6f, 6.2f })
            {
                room.SetColor(Pal.SteelDark, 0.32f);
                room.Box(new Vector3(x, y + 0.72f, RailZ), new Vector3(0.1f, 1.44f, 0.1f));
                room.Prism(new Vector3(x, y + 1.44f, RailZ), 0.05f, 0.09f, 0.08f, 6);
                room.Prism(new Vector3(x, y + 1.52f, RailZ), 0.09f, 0.02f, 0.1f, 6);
            }
            for (int i = 0; i < 9; i++)
            {
                float a = i * Mathf.PI / 8f;
                Iron(new Vector3(Mathf.Cos(a) * 1.05f, y + 1.2f + Mathf.Sin(a) * 0.3f, RailZ), new Vector3(0.05f, 0.05f, 0.05f));
            }
            // Taller than it looks, so nobody jumps it; the crosshair sees through it to the Choir and the relics.
            Solid("Rail", new Vector3(0f, y + 1.5f, RailZ), new Vector3(HalfWidth * 2f, 3f, 0.2f)).layer = PlayerInteractor.SeeThroughLayer;
        }

        void BuildLectern()
        {
            float y = ChoirFloor, z = 5.75f;
            room.ShadedBox(new Vector3(0f, y + 0.08f, z), new Vector3(1.3f, 0.16f, 1.1f), Pal.Stone, rng, 0.36f, 0.44f);
            room.ShadedBox(new Vector3(0f, y + 0.62f, z), new Vector3(0.9f, 0.94f, 0.7f), Pal.Stone, rng, 0.38f, 0.5f);
            room.SetColor(Pal.Black, 0.3f);
            room.Box(new Vector3(0f, y + 0.62f, z - 0.36f), new Vector3(0.5f, 0.62f, 0.02f));

            // The slope, and the great book of names open on it.
            room.Push(new Vector3(0f, y + 1.18f, z), Quaternion.Euler(-16f, 0f, 0f), Vector3.one);
            room.ShadedBox(Vector3.zero, new Vector3(1.3f, 0.08f, 0.9f), Pal.WoodDark, rng, 0.3f, 0.36f);
            room.SetColor(Pal.Leather, 0.3f);
            room.Box(new Vector3(0f, 0.06f, 0f), new Vector3(1.18f, 0.04f, 0.78f));
            foreach (float side in new[] { -1f, 1f })
            {
                room.SetColor(Pal.Bone, 0.56f);
                room.Box(new Vector3(side * 0.28f, 0.11f, 0f), new Vector3(0.54f, 0.07f, 0.72f));
                for (int i = 0; i < 9; i++)
                {
                    room.SetColor(Pal.Black, 0.4f);
                    room.Box(new Vector3(side * 0.28f, 0.15f, -0.28f + i * 0.07f), new Vector3(0.42f, 0.006f, 0.02f));
                }
            }
            room.SetColor(Pal.ClothRed, 0.4f);
            room.Box(new Vector3(0f, 0.06f, 0.5f), new Vector3(0.05f, 0.02f, 0.3f));
            room.Pop();
            Solid("Lectern", new Vector3(0f, y + 0.7f, z), new Vector3(1.2f, 1.4f, 0.9f));

            foreach (float side in new[] { -1f, 1f })
                Candelabrum(new Vector3(side * 1.55f, y, 5.4f));
        }

        void Candelabrum(Vector3 at)
        {
            Iron(at + Vector3.up * 0.05f, new Vector3(0.4f, 0.1f, 0.4f));
            Iron(at + Vector3.up * 0.95f, new Vector3(0.05f, 1.9f, 0.05f));
            Iron(at + Vector3.up * 1.72f, new Vector3(0.7f, 0.04f, 0.04f));
            foreach (float x in new[] { -0.33f, 0f, 0.33f })
                Candle(at + new Vector3(x, x == 0f ? 1.9f : 1.74f, 0f), 0.26f);
        }

        void BuildRelics(ChapelLayout layout)
        {
            Altar(new Vector3(-3.2f, ChoirFloor, 5f), b => TownMeshes.Bloodstone(b, rng));
            Altar(new Vector3(2.5f, ChoirFloor, 5f), TownMeshes.WitchEye);
            Altar(new Vector3(4.1f, ChoirFloor, 5f), b => TownMeshes.GraveIdol(b, rng));
            layout.Relics.Add((origin + new Vector3(-3.2f, ChoirFloor + 0.9f, 5f), new Vector3(1.5f, 1.8f, 1.4f)));
            layout.Relics.Add((origin + new Vector3(3.3f, ChoirFloor + 0.9f, 5f), new Vector3(3.1f, 1.8f, 1.4f)));
        }

        void Altar(Vector3 at, System.Action<MeshBuilder> relic)
        {
            const float top = 1.02f;
            room.ShadedBox(at + Vector3.up * (top * 0.5f), new Vector3(1.05f, top, 0.8f), Pal.Stone, rng, 0.36f, 0.48f);
            room.SetColor(Pal.Plaster, 0.42f);
            room.Box(at + Vector3.up * (top + 0.01f), new Vector3(1.2f, 0.02f, 0.95f));
            for (int i = 0; i < 8; i++)
            {
                float length = R(0.22f, 0.5f);
                room.SetColor(Pal.Plaster, R(0.36f, 0.46f));
                room.Box(at + new Vector3(-0.52f + i * 0.15f, top - length * 0.5f, -0.48f), new Vector3(0.14f, length, 0.015f));
            }
            room.SetColor(Pal.FurGrey, 0.34f);
            room.Prism(at + Vector3.up * (top + 0.02f), 0.34f, 0.28f, 0.11f, 8, true, true, 0.4f);
            room.Push(at + Vector3.up * (top + 0.12f), Quaternion.identity, Vector3.one * 1.5f);
            relic(room);
            room.Pop();
            Candle(at + new Vector3(-0.42f, top + 0.02f, 0.22f), 0.2f);
            Candle(at + new Vector3(0.44f, top + 0.02f, 0.14f), 0.14f);
            Solid("Altar", at + Vector3.up * (top * 0.5f), new Vector3(1.05f, top, 0.8f));
        }

        // ---- The window and the banners -----------------------------------------------------------------------

        void BuildWindow()
        {
            float z = HalfDepth - 0.02f;
            const float bottom = 3f, halfWidth = 1f, straight = 3.1f, point = 1.5f;
            const int slices = 18;
            float height = straight + point;
            float slice = height / slices;

            for (int i = 0; i < slices; i++)
            {
                float y = i * slice, half = HalfAt(y);
                room.SetColor(Pal.PaintBlue, 0.78f);
                room.Current.Color = WindowGlass.linear;
                room.Current.Layer = TexLayer.Plain;
                room.Current.Emission = 0.6f;
                room.Box(new Vector3(0f, bottom + y + slice * 0.5f, z), new Vector3(half * 2f, slice, 0.04f));

                foreach (float side in new[] { -1f, 1f })
                    room.ShadedBox(new Vector3(side * (half + 0.2f), bottom + y + slice * 0.5f, z - 0.1f), new Vector3(0.4f, slice + 0.02f, 0.22f), Pal.Stone, rng, 0.44f, 0.54f);
            }
            float HalfAt(float y) => y < straight ? halfWidth : halfWidth * (1f - (y - straight) / point);

            room.ShadedBox(new Vector3(0f, bottom - 0.1f, z - 0.16f), new Vector3(halfWidth * 2f + 0.9f, 0.2f, 0.36f), Pal.Stone, rng, 0.46f, 0.54f);

            // Lead in diamonds, clipped to the opening by stopping each bar where the arch closes in.
            const float pane = 0.4f;
            int row = 0;
            for (float y = 0.25f; y < height - 0.1f; y += pane * 0.5f, row++)
            for (float x = -halfWidth + (row % 2) * pane * 0.5f; x <= halfWidth; x += pane)
            {
                if (Mathf.Abs(x) + 0.18f > HalfAt(y + 0.14f)) continue;
                room.SetColor(Pal.Stone, 0.14f);
                foreach (float tilt in new[] { 45f, -45f })
                {
                    room.Push(new Vector3(x, bottom + y, z - 0.04f), Quaternion.Euler(0f, 0f, tilt), Vector3.one);
                    room.Box(Vector3.zero, new Vector3(0.32f, 0.03f, 0.02f));
                    room.Pop();
                }
            }
            room.SetColor(Pal.Stone, 0.4f);
            room.Box(new Vector3(0f, bottom + (straight + point) * 0.5f - 0.2f, z - 0.05f), new Vector3(0.07f, straight + point - 0.5f, 0.07f));

            AddGlow(new Vector3(0f, bottom + 1.4f, z - 1.6f), WindowLight, 15f);
            AddGlow(new Vector3(0f, 1.2f, 4.4f), WindowLight * 0.5f, 9f);

            // The grey light of the nave, and the warmth the candles make of it round the rail.
            AddGlow(new Vector3(0f, 4f, -4f), WindowLight * 0.7f, 16f);
            AddGlow(new Vector3(-3.2f, 2.2f, 3.9f), CandleLight, 8f);
            AddGlow(new Vector3(3.2f, 2.2f, 3.9f), CandleLight, 8f);
        }

        void BuildBanners()
        {
            float z = HalfDepth - 0.08f;
            foreach (float x in new[] { -4.1f, 4.1f })
            {
                Iron(new Vector3(x, 7f, z - 0.06f), new Vector3(2.6f, 0.06f, 0.06f));

                // Cut into pieces, so the baked light falls across the cloth and not just on its corners.
                for (int across = 0; across < 5; across++)
                for (int down = 0; down < 4; down++)
                {
                    Cloth(1f);
                    room.Box(new Vector3(x - 0.92f + across * 0.46f, 6.76f - down * 0.88f, z), new Vector3(0.46f, 0.88f, 0.03f));
                }
                for (int i = 0; i < 9; i++)
                {
                    float length = R(0.3f, 1.3f);
                    Cloth(R(0.8f, 1.1f));
                    room.Box(new Vector3(x - 1.02f + i * 0.255f, 3.45f - length * 0.5f, z), new Vector3(0.24f, length, 0.03f));
                }
            }
        }

        void Cloth(float brightness)
        {
            room.SetColor(Pal.ClothDark);
            Color linear = (BannerCloth * brightness).linear;
            linear.a = 1f;
            room.Current.Color = linear;
        }

        void BuildMonks()
        {
            // The two who do not answer: one at the candles, one bent over the relics. Only the one at the book keeps the ledger.
            Prop(new Vector3(-2.2f, ChoirFloor, 5.9f), 215f, PeopleMeshes.Monk);
            Prop(new Vector3(3.3f, ChoirFloor, 6.1f), 170f, PeopleMeshes.Monk);
        }
    }
}
