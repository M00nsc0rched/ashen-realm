using AshenRealm.Core;
using UnityEngine;

namespace AshenRealm.EditorTools
{
    /// <summary>Key positions of the forge for the scene builder.</summary>
    internal sealed class ForgeLayout : RoomLayout
    {
        public Vector3 Rack;
        public Vector3 RackSize;
    }

    /// <summary>
    /// The Cold Forge from the inside, laid out after the concept painting as seen from the street door: the hearth under its
    /// stone hood at the back, the bellows beside it, the anvil in front of the fire with the smith at it, the bench on the
    /// left, and on the right wall the rack with the four weapons the forge sells.
    /// </summary>
    internal sealed class ForgeGenerator : RoomGenerator
    {
        const float HalfWidth = 6.5f;
        const float HalfDepth = 5.5f;
        const float Height = 4.6f;
        const float BeamY = 3.9f;
        const float WallThickness = 0.5f;
        const float HearthX = -1.6f;
        const float HearthWidth = 3.6f;
        const float HearthDepth = 1.7f;
        const float BedHeight = 0.85f;
        const float OpeningTop = 2.1f;
        const float RackStart = -2.3f;
        const float RackEnd = 3.1f;
        const float BenchHeight = 0.9f;

        public ForgeGenerator(Material material, Vector3 origin, int seed) : base(material, origin, seed) { }

        public ForgeLayout Generate(Transform parent)
        {
            Begin(parent);

            BuildShell();
            BuildTimber();
            StreetDoor(-HalfDepth + 0.04f);
            BuildHearth();
            BuildBellows();
            BuildToolWall();
            BuildAnvil();
            BuildBench();
            BuildRack();
            BuildCorners();
            BuildWindow();
            BuildWorkshopCharacter();
            Finish("Forge_Room");

            Light("Forge Fire", new Vector3(HearthX, 1.5f, HalfDepth - 1f), new Color(1f, 0.48f, 0.2f), 9f, 1.5f, 0.5f);
            WorldVista.Add(root, "Forge_Fire_and_Work", origin + new Vector3(0.2f, 1.65f, -3.5f), origin + new Vector3(-1.7f, 1.7f, 4.6f));
            WorldVista.Add(root, "Forge_Armour_and_Plans", origin + new Vector3(-2.7f, 1.65f, -3.7f), origin + new Vector3(-5.8f, 1.6f, -1.8f));
            WorldVista.Add(root, "Forge_Cold_Quench", origin + new Vector3(4.2f, 1.65f, 2.8f), origin + new Vector3(4.75f, 0.9f, 4.65f));

            var layout = new ForgeLayout
            {
                Entry = origin + new Vector3(0f, 0.05f, -4.2f),
                DoorOut = origin + new Vector3(0f, 0f, -HalfDepth + 0.3f),
                Rack = origin + new Vector3(HalfWidth - 0.3f, 1.9f, (RackStart + RackEnd) * 0.5f),
                RackSize = new Vector3(0.5f, 2.6f, RackEnd - RackStart),
            };
            Person(layout, "smith", new Vector3(-2.4f, 0f, 1.2f), 90f, PeopleMeshes.Smith);
            return layout;
        }

        // ---- Floor, walls, ceiling -------------------------------------------------------------------------

        void BuildWorkshopCharacter()
        {
            // A cooler corner balances the orange furnace: quenching water, wet flags and spare billets.
            var trough = new Vector3(4.75f, 0f, 4.65f);
            room.ShadedBox(trough + Vector3.up * 0.31f, new Vector3(1.7f, 0.62f, 0.9f), Pal.WetStone, rng, 0.3f, 0.45f);
            room.SetColor(Pal.Black, 0.18f);
            room.Box(trough + Vector3.up * 0.64f, new Vector3(1.42f, 0.035f, 0.65f));
            room.SetColor(Pal.Water, 0.35f);
            room.Current.Sheen = 0.8f;
            room.Box(trough + Vector3.up * 0.655f, new Vector3(1.27f, 0.018f, 0.52f));
            Solid("Quenching Trough", trough + Vector3.up * 0.35f, new Vector3(1.75f, 0.7f, 0.95f));
            AddGlow(new Vector3(4.7f, 2.2f, 4.4f), StreetLight * 1.4f, 6f);
            for (int i = 0; i < 7; i++)
                room.ShadedBox(new Vector3(5.4f - (i % 3) * 0.24f, 0.1f + i / 3 * 0.11f, 3.7f), new Vector3(0.2f, 0.1f, 0.5f), Pal.SteelDark, rng, 0.32f, 0.48f);

            // An unfinished harness gives the street-side corner a readable silhouette.
            Vector3 stand = new Vector3(-4.6f, 0f, -3.8f);
            Timber(stand + Vector3.up * 0.85f, new Vector3(0.14f, 1.7f, 0.14f), 0.35f);
            Timber(stand + Vector3.up * 0.06f, new Vector3(0.9f, 0.12f, 0.65f), 0.4f);
            room.SetColor(Pal.SteelDark, 0.45f);
            room.Prism(stand + Vector3.up * 0.85f, 0.25f, 0.34f, 0.6f, 6);
            room.Prism(stand + Vector3.up * 1.48f, 0.15f, 0.19f, 0.32f, 8);
            room.SetColor(Pal.Black, 0.22f);
            room.Box(stand + new Vector3(0f, 1.68f, -0.18f), new Vector3(0.24f, 0.045f, 0.05f));
            foreach (float side in new[] { -1f, 1f })
            {
                room.SetColor(Pal.Steel, 0.4f);
                room.Prism(stand + new Vector3(side * 0.38f, 1.29f, 0f), 0.19f, 0.13f, 0.16f, 6);
            }
            Solid("Unfinished Armour", stand + Vector3.up * 0.9f, new Vector3(1.02f, 1.8f, 0.68f));

            Vector3 plan = new Vector3(-HalfWidth + 0.14f, 2.15f, -1.5f);
            room.ShadedBox(plan, new Vector3(0.12f, 1.55f, 1.85f), Pal.WoodDark, rng, 0.3f, 0.44f);
            room.SetColor(Pal.Bone, 0.48f);
            room.Box(plan + Vector3.right * 0.08f, new Vector3(0.03f, 1.25f, 1.5f));
            foreach (float side in new[] { -1f, 1f })
            {
                TownDetailMeshes.Beam(room, plan + new Vector3(0.105f, 0.4f, side * 0.18f), plan + new Vector3(0.105f, -0.2f, side * 0.32f), 0.025f, Pal.PaintBlue, 0.3f);
                TownDetailMeshes.Beam(room, plan + new Vector3(0.105f, -0.2f, side * 0.32f), plan + new Vector3(0.105f, -0.49f, side * 0.2f), 0.025f, Pal.PaintBlue, 0.3f);
                TownDetailMeshes.Beam(room, plan + new Vector3(0.105f, 0.4f, side * 0.18f), plan + new Vector3(0.105f, 0.23f, side * 0.51f), 0.025f, Pal.PaintBlue, 0.3f);
            }
            TownDetailMeshes.Beam(room, plan + new Vector3(0.105f, 0.4f, -0.18f), plan + new Vector3(0.105f, 0.4f, 0.18f), 0.025f, Pal.PaintBlue, 0.3f);
            room.SetColor(Pal.Black, 0.18f);
            room.Box(plan + new Vector3(0.13f, 0.16f, -0.1f), new Vector3(0.025f, 0.15f, 0.16f));
            WorldVista.Inspect(root, "Forge Armour Plans", origin + plan + Vector3.right * 0.18f,
                new Vector3(0.18f, 1.4f, 1.65f), GameText.ForgePlanLore);

            // The path to the smith and the goods stays clear; these chains hang well above the player's head.
            TownDetailMeshes.Beam(room, new Vector3(0.75f, 2.9f, 2.8f), new Vector3(0.75f, Height, 2.8f), 0.04f, Pal.SteelDark, 0.36f);
            TownDetailMeshes.Beam(room, new Vector3(1.5f, 3f, 3.3f), new Vector3(1.5f, Height, 3.3f), 0.04f, Pal.SteelDark, 0.36f);
            TownDetailMeshes.Ring(room, new Vector3(0.75f, 2.8f, 2.8f), 0.23f, 0.045f, Pal.SteelDark, 0.4f, 8);
        }

        void BuildShell()
        {
            Flagstones(HalfWidth, HalfDepth, 1.3f, 0f, 0.18f, 0.32f);
            Solid("Floor", new Vector3(0f, -0.5f, 0f), new Vector3(60f, 1f, 60f));

            for (float x = -HalfWidth; x < HalfWidth; x += LongPiece)
            for (float z = -HalfDepth; z < HalfDepth; z += LongPiece)
                room.ShadedBox(new Vector3(x + LongPiece * 0.5f, Height + 0.1f, z + LongPiece * 0.5f), new Vector3(LongPiece, 0.2f, LongPiece), Pal.WoodDark, rng, 0.26f, 0.32f);
            Solid("Ceiling", new Vector3(0f, Height + 0.5f, 0f), new Vector3(60f, 1f, 60f));

            float outer = HalfWidth + WallThickness * 0.5f;
            float end = HalfDepth + WallThickness * 0.5f;
            StoneWall(new Vector3(-outer, 0f, -HalfDepth), new Vector3(-outer, 0f, HalfDepth), Height, WallThickness);
            StoneWall(new Vector3(outer, 0f, -HalfDepth), new Vector3(outer, 0f, HalfDepth), Height, WallThickness);
            StoneWall(new Vector3(-HalfWidth, 0f, -end), new Vector3(HalfWidth, 0f, -end), Height, WallThickness);
            StoneWall(new Vector3(-HalfWidth, 0f, end), new Vector3(HalfWidth, 0f, end), Height, WallThickness);
            Solid("West Wall", new Vector3(-outer, Height * 0.5f, 0f), new Vector3(WallThickness, Height, 40f));
            Solid("East Wall", new Vector3(outer, Height * 0.5f, 0f), new Vector3(WallThickness, Height, 40f));
            Solid("South Wall", new Vector3(0f, Height * 0.5f, -end), new Vector3(40f, Height, WallThickness));
            Solid("North Wall", new Vector3(0f, Height * 0.5f, end), new Vector3(40f, Height, WallThickness));
        }

        void BuildTimber()
        {
            foreach (float z in new[] { -3.6f, -0.8f, 2f })
            {
                Timber(new Vector3(0f, BeamY, z), new Vector3(HalfWidth * 2f, 0.42f, 0.44f), 0.38f);
                foreach (float side in new[] { -1f, 1f })
                {
                    if (side > 0f && z > RackStart) continue;   // the rack takes the east wall
                    Timber(new Vector3(side * (HalfWidth - 0.2f), BeamY * 0.5f, z), new Vector3(0.4f, BeamY, 0.44f));
                    room.SetColor(Pal.WoodDark, 0.34f);
                    room.Push(new Vector3(side * (HalfWidth - 0.75f), BeamY - 0.62f, z), Quaternion.Euler(0f, 0f, side * 45f), Vector3.one);
                    room.Box(Vector3.zero, new Vector3(0.2f, 1.5f, 0.24f));
                    room.Pop();
                }
            }
            foreach (float x in new[] { -4.4f, -2.2f, 0f, 2.2f, 4.4f })
                Timber(new Vector3(x, Height - 0.17f, 0f), new Vector3(0.3f, 0.34f, HalfDepth * 2f), 0.32f);

            // A chain and hook off the middle beam, for what is too heavy to lift.
            for (int i = 0; i < 9; i++)
                Iron(new Vector3(1.2f, BeamY - 0.3f - i * 0.16f, -0.8f), new Vector3(i % 2 == 0 ? 0.08f : 0.03f, 0.13f, i % 2 == 0 ? 0.03f : 0.08f));
            Iron(new Vector3(1.2f, BeamY - 1.8f, -0.8f), new Vector3(0.05f, 0.2f, 0.05f));
            Iron(new Vector3(1.28f, BeamY - 1.9f, -0.8f), new Vector3(0.16f, 0.05f, 0.05f));
            Iron(new Vector3(1.36f, BeamY - 1.82f, -0.8f), new Vector3(0.05f, 0.14f, 0.05f));
        }

        // ---- The hearth ------------------------------------------------------------------------------------

        void BuildHearth()
        {
            float cx = HearthX, back = HalfDepth, z = back - HearthDepth * 0.5f;

            // The bed, and the two piers that carry the hood.
            for (int i = 0; i < 4; i++)
            {
                room.ShadedBox(new Vector3(cx + (i - 1.5f) * HearthWidth * 0.25f, BedHeight * 0.25f, z), new Vector3(HearthWidth * 0.25f - 0.02f, BedHeight * 0.5f - 0.02f, HearthDepth), Pal.Stone, rng, 0.26f, 0.4f);
                room.ShadedBox(new Vector3(cx + (i - 1.5f) * HearthWidth * 0.25f, BedHeight * 0.75f, z - 0.02f), new Vector3(HearthWidth * 0.25f - 0.02f, BedHeight * 0.5f - 0.02f, HearthDepth), Pal.Stone, rng, 0.3f, 0.44f);
            }
            foreach (float side in new[] { -1f, 1f })
            for (int i = 0; i < 4; i++)
                room.ShadedBox(new Vector3(cx + side * (HearthWidth * 0.5f - 0.25f), BedHeight + 0.16f + i * 0.31f, z), new Vector3(0.5f, 0.3f, HearthDepth), Pal.Stone, rng, 0.3f, 0.46f);
            room.ShadedBox(new Vector3(cx, (BedHeight + OpeningTop) * 0.5f, back - 0.05f), new Vector3(HearthWidth, OpeningTop - BedHeight, 0.1f), Pal.RockDark, rng, 0.28f, 0.34f);

            // The hood, drawn in as it climbs to the roof.
            const int courses = 7;
            for (int i = 0; i < courses; i++)
            {
                float t = i / (courses - 1f);
                float height = (Height - OpeningTop) / courses;
                float width = Mathf.Lerp(HearthWidth + 0.5f, 1.7f, t), depth = Mathf.Lerp(HearthDepth + 0.2f, 1f, t);
                float y = OpeningTop + height * (i + 0.5f), zz = back - depth * 0.5f;
                for (int k = 0; k < 4; k++)
                    room.ShadedBox(new Vector3(cx + (k - 1.5f + (i % 2) * 0.12f) * width * 0.25f, y, zz), new Vector3(width * 0.25f - 0.02f, height - 0.02f, depth), Pal.Stone, rng, 0.24f, 0.4f);
            }
            room.ShadedBox(new Vector3(cx, OpeningTop + 0.04f, back - HearthDepth - 0.06f), new Vector3(HearthWidth + 0.6f, 0.24f, 0.3f), Pal.Stone, rng, 0.34f, 0.46f);
            Solid("Hearth", new Vector3(cx, Height * 0.5f, z), new Vector3(HearthWidth + 0.5f, Height, HearthDepth));

            // Coals, sparks and the fire itself.
            for (int i = 0; i < 26; i++)
            {
                room.SetColor(Pal.RockDark, R(0.1f, 0.3f));
                room.Box(new Vector3(cx + R(-1.2f, 1.2f), BedHeight + R(0.02f, 0.1f), z + R(-0.6f, 0.6f)), new Vector3(R(0.1f, 0.2f), R(0.07f, 0.14f), R(0.1f, 0.2f)));
            }
            for (int i = 0; i < 18; i++)
            {
                room.SetColor(Pal.Lava, R(0.45f, 0.85f));
                room.Box(new Vector3(cx + R(-0.9f, 0.9f), BedHeight + 0.14f, z + R(-0.5f, 0.5f)), new Vector3(R(0.08f, 0.16f), 0.05f, R(0.08f, 0.16f)));
            }
            room.Push(new Vector3(cx, BedHeight + 0.1f, z), Quaternion.identity, new Vector3(1.5f, 0.9f, 1.1f));
            PropMeshes.Flames(room, rng);
            room.Pop();
            AddGlow(new Vector3(cx, 1.5f, z - 1.2f), FireLight, 12f);
            AddGlow(new Vector3(cx + 2f, 1.5f, z - 1.8f), FireLight * 0.45f, 9f);

            // Tongs stood in the coals, more of them against the bed.
            for (int i = 0; i < 3; i++)
            {
                float x = cx + 1.25f + i * 0.14f;
                Iron(new Vector3(x, 0.58f, back - HearthDepth - 0.05f), new Vector3(0.03f, 1.1f, 0.03f));
                Iron(new Vector3(x + 0.05f, 0.56f, back - HearthDepth - 0.05f), new Vector3(0.03f, 1.06f, 0.03f));
            }
        }

        void BuildBellows()
        {
            float x = 1.65f, z = HalfDepth - 1.1f, y = 1.05f;

            foreach (float side in new[] { -1f, 1f })
            foreach (float along in new[] { -0.6f, 0.6f })
                Timber(new Vector3(x + along, 0.9f, z + side * 0.62f), new Vector3(0.16f, 1.8f, 0.16f), 0.4f);
            foreach (float side in new[] { -1f, 1f })
                Timber(new Vector3(x, 0.45f, z + side * 0.62f), new Vector3(1.4f, 0.12f, 0.1f), 0.36f);
            Timber(new Vector3(x, 1.72f, z), new Vector3(1.5f, 0.14f, 1.4f), 0.38f);

            // The leather wedge, widest at the back, narrowing to the nozzle.
            for (int i = 0; i < 7; i++)
            {
                float t = i / 6f;
                float height = Mathf.Lerp(0.82f, 0.2f, t), width = Mathf.Lerp(1f, 0.3f, t);
                room.SetColor(Pal.Leather, i % 2 == 0 ? 0.3f : 0.22f);
                room.Box(new Vector3(x + 0.62f - i * 0.2f, y, z), new Vector3(0.2f, height, width));
            }
            foreach (float tilt in new[] { 15f, -15f })
            {
                room.Push(new Vector3(x + 0.08f, y + (tilt > 0f ? 0.25f : -0.25f), z), Quaternion.Euler(0f, 0f, tilt), Vector3.one);
                room.ShadedBox(Vector3.zero, new Vector3(1.4f, 0.05f, 1.04f), Pal.Wood, rng, 0.32f, 0.4f);
                room.Pop();
            }
            room.SetColor(Pal.SteelDark, 0.3f);
            room.Push(new Vector3(x - 0.56f, y, z), Quaternion.Euler(0f, 0f, 90f), Vector3.one);
            room.Prism(Vector3.zero, 0.08f, 0.05f, 1.2f, 7);
            room.Pop();
            Solid("Bellows", new Vector3(x, 0.9f, z), new Vector3(1.6f, 1.8f, 1.5f));

            // The lever, off a chain from the beam.
            room.SetColor(Pal.WoodDark, 0.36f);
            room.Push(new Vector3(x + 0.8f, 2.3f, z), Quaternion.Euler(0f, 0f, -12f), Vector3.one);
            room.Box(Vector3.zero, new Vector3(1.9f, 0.12f, 0.1f));
            room.Pop();
            for (int i = 0; i < 5; i++)
                Iron(new Vector3(x + 1.5f, 2.2f + i * 0.14f + 0.1f, z), new Vector3(0.04f, 0.1f, 0.06f));
        }

        void BuildToolWall()
        {
            float z = HalfDepth - 0.1f;
            Timber(new Vector3(-5f, 2.55f, z), new Vector3(2.2f, 0.14f, 0.1f), 0.36f);
            Timber(new Vector3(-5f, 1.3f, z), new Vector3(2.2f, 0.14f, 0.1f), 0.34f);
            for (int i = 0; i < 7; i++)
            {
                float x = -5.9f + i * 0.3f, length = R(0.9f, 1.3f);
                Iron(new Vector3(x, 2.5f - length * 0.5f, z - 0.06f), new Vector3(0.03f, length, 0.03f));
                Iron(new Vector3(x + 0.07f, 2.5f - length * 0.5f, z - 0.06f), new Vector3(0.03f, length, 0.03f));
                Iron(new Vector3(x + 0.035f, 2.5f - length, z - 0.06f), new Vector3(0.13f, 0.06f, 0.04f));
                Iron(new Vector3(x + 0.035f, 2.5f, z - 0.06f), new Vector3(0.12f, 0.1f, 0.06f));
            }

            // Coal in a crate and a sack of it beside the bed.
            Vector3 crate = new Vector3(-4.8f, 0f, HalfDepth - 0.9f);
            Prop(crate, 6f, b => PropMeshes.Crate(b, rng, 0.95f));
            for (int i = 0; i < 12; i++)
            {
                room.SetColor(Pal.RockDark, R(0.08f, 0.24f));
                room.Box(crate + new Vector3(R(-0.36f, 0.36f), 0.97f + R(0f, 0.06f), R(-0.36f, 0.36f)), new Vector3(R(0.1f, 0.2f), 0.1f, R(0.1f, 0.2f)));
            }
            Solid("Coal Crate", crate + Vector3.up * 0.5f, new Vector3(1f, 1f, 1f));
            room.SetColor(Pal.Thatch, 0.3f);
            room.Prism(new Vector3(0.75f, 0f, HalfDepth - 0.6f), 0.34f, 0.26f, 0.7f, 8, true, false);
            room.Prism(new Vector3(0.75f, 0.7f, HalfDepth - 0.6f), 0.26f, 0.08f, 0.22f, 8, false, true);
        }

        // ---- The anvil --------------------------------------------------------------------------------------

        void BuildAnvil()
        {
            var at = new Vector3(-1.4f, 0f, 1.2f);
            room.SetColor(Pal.Bark, 0.3f);
            room.Prism(at, 0.54f, 0.46f, 0.74f, 9);
            room.SetColor(Pal.Wood, 0.34f);
            room.Prism(at + Vector3.up * 0.74f, 0.46f, 0.46f, 0.02f, 9);
            for (int i = 0; i < 18; i++)
            {
                float a = i * Mathf.PI / 9f;
                Iron(at + new Vector3(Mathf.Cos(a) * 0.53f, 0.34f, Mathf.Sin(a) * 0.53f), new Vector3(0.07f, 0.05f, 0.07f));
            }
            for (int i = 0; i < 6; i++)
                Iron(at + new Vector3(0.5f + i * 0.14f, 0.05f, -0.2f - i * 0.02f), new Vector3(0.12f, 0.05f, 0.06f));

            room.ShadedBox(at + new Vector3(0f, 0.82f, 0f), new Vector3(0.66f, 0.12f, 0.42f), Pal.SteelDark, rng, 0.24f, 0.32f);
            room.ShadedBox(at + new Vector3(0f, 0.98f, 0f), new Vector3(0.3f, 0.22f, 0.22f), Pal.SteelDark, rng, 0.26f, 0.34f);
            room.ShadedBox(at + new Vector3(0f, 1.15f, 0f), new Vector3(0.84f, 0.16f, 0.3f), Pal.SteelDark, rng, 0.36f, 0.46f);
            room.SetColor(Pal.SteelDark, 0.4f);
            room.Push(at + new Vector3(0.42f, 1.15f, 0f), Quaternion.Euler(0f, 0f, -90f), new Vector3(1f, 1f, 0.8f));
            room.Prism(Vector3.zero, 0.14f, 0f, 0.42f, 6);
            room.Pop();

            // The work is still hot. The smith emits moving sparks when his hammer lands.
            room.SetColor(Pal.Lava, 0.8f);
            room.Box(at + new Vector3(0.02f, 1.25f, 0.02f), new Vector3(0.5f, 0.035f, 0.06f));
            AddGlow(at + new Vector3(0.2f, 1.6f, -0.5f), FireLight * 0.8f, 4f);
            Solid("Anvil", at + Vector3.up * 0.62f, new Vector3(0.95f, 1.24f, 0.95f));

            // The slack tub at the smith's elbow, and a pail of tools.
            var tub = new Vector3(-0.1f, 0f, 0.5f);
            Prop(tub, 40f, b => PropMeshes.Barrel(b, rng));
            room.SetColor(Pal.Water, 0.3f);
            room.Prism(tub + Vector3.up * 0.84f, 0.31f, 0.31f, 0.02f, 10);
            Solid("Slack Tub", tub + Vector3.up * 0.45f, new Vector3(0.78f, 0.9f, 0.78f));
        }

        // ---- The bench, on the left -------------------------------------------------------------------------

        void BuildBench()
        {
            var at = new Vector3(-5.6f, 0f, -1.7f);
            const float width = 1.3f, length = 3.2f;
            for (float z = -length * 0.5f; z < length * 0.5f; z += 0.8f)
            {
                room.ShadedBox(at + new Vector3(0f, BenchHeight - 0.05f, z + 0.4f), new Vector3(width, 0.1f, 0.78f), Pal.Wood, rng, 0.3f, 0.42f);
            }
            foreach (float x in new[] { -0.5f, 0.5f })
            foreach (float z in new[] { -length * 0.5f + 0.2f, length * 0.5f - 0.2f })
                Timber(at + new Vector3(x, (BenchHeight - 0.1f) * 0.5f, z), new Vector3(0.16f, BenchHeight - 0.1f, 0.16f), 0.4f);
            Timber(at + new Vector3(0.5f, 0.3f, 0f), new Vector3(0.1f, 0.1f, length - 0.4f), 0.34f);
            Solid("Bench", at + Vector3.up * (BenchHeight * 0.5f), new Vector3(width, BenchHeight, length));

            float top = BenchHeight + 0.01f;

            // Three hammers, heads to the wall.
            foreach (float z in new[] { -2.8f, -2.3f, -1.8f })
            {
                room.SetColor(Pal.Wood, 0.42f);
                room.Box(new Vector3(-5.4f, top + 0.03f, z), new Vector3(0.7f, 0.05f, 0.05f));
                room.ShadedBox(new Vector3(-5.82f, top + 0.08f, z), new Vector3(0.16f, 0.15f, 0.2f), Pal.SteelDark, rng, 0.36f, 0.46f);
            }

            // Tongs, an oil can, a bowl of coal and the rag that came with the bench.
            Iron(new Vector3(-5.5f, top + 0.02f, -1.1f), new Vector3(0.9f, 0.03f, 0.03f));
            Iron(new Vector3(-5.5f, top + 0.02f, -1.03f), new Vector3(0.9f, 0.03f, 0.03f));
            room.SetColor(Pal.Brass, 0.34f);
            room.Prism(new Vector3(-5.9f, top, -0.5f), 0.13f, 0.1f, 0.2f, 8);
            room.Prism(new Vector3(-5.9f, top + 0.2f, -0.5f), 0.1f, 0.03f, 0.14f, 8);
            room.SetColor(Pal.Wood, 0.4f);
            room.Prism(new Vector3(-5.3f, top, -0.5f), 0.14f, 0.26f, 0.12f, 9);
            for (int i = 0; i < 6; i++)
            {
                room.SetColor(Pal.RockDark, R(0.1f, 0.24f));
                room.Box(new Vector3(-5.3f + R(-0.14f, 0.14f), top + 0.12f, -0.5f + R(-0.14f, 0.14f)), new Vector3(0.08f, 0.06f, 0.08f));
            }
            room.SetColor(Pal.Thatch, 0.3f);
            room.Box(new Vector3(-5.0f, top + 0.01f, -0.3f), new Vector3(0.05f, 0.02f, 0.5f));
            room.Box(new Vector3(-4.95f, top - 0.3f, -0.3f), new Vector3(0.02f, 0.62f, 0.5f));

            room.SetColor(Pal.SteelDark, 0.34f);
            room.Box(new Vector3(-4.4f, BeamY - 0.5f, -0.8f), new Vector3(0.04f, 0.8f, 0.04f));
            Lantern(new Vector3(-4.4f, BeamY - 1.34f, -0.8f), 0.8f);
            AddGlow(new Vector3(-4.2f, BeamY - 1.4f, -1.1f), LampLight, 7f);
        }

        // ---- The rack, on the right -------------------------------------------------------------------------

        void BuildRack()
        {
            float wall = HalfWidth - 0.06f;

            // Planks up the wall, a rail along the top, and pegs.
            for (float z = RackStart; z < RackEnd - 0.01f; z += 0.5f)
                room.ShadedBox(new Vector3(wall, 2.05f, z + 0.25f), new Vector3(0.08f, 2.5f, 0.48f), Pal.Wood, rng, 0.24f, 0.36f);
            Timber(new Vector3(wall - 0.08f, 3.34f, (RackStart + RackEnd) * 0.5f), new Vector3(0.16f, 0.2f, RackEnd - RackStart + 0.3f), 0.42f);
            foreach (float z in new[] { RackStart - 0.1f, RackEnd + 0.1f })
                Timber(new Vector3(wall - 0.08f, 1.9f, z), new Vector3(0.2f, 3f, 0.2f), 0.4f);

            // The goods, hilts up, in the order of the painting.
            Rack(2.25f, 3.05f, 1.3f, b => TownMeshes.RustedBlade(b));
            Rack(0.95f, 3.05f, 1.35f, b => TownMeshes.IronSword(b));
            Rack(-0.2f, 2.95f, 1.5f, b => TownMeshes.GraveDagger(b));
            HungStaff(new Vector3(wall - 0.12f, 1.95f, -1.4f));

            // The bench under the rack.
            var at = new Vector3(HalfWidth - 0.55f, 0f, (RackStart + RackEnd) * 0.5f);
            float length = RackEnd - RackStart + 0.2f;
            for (float z = -length * 0.5f; z < length * 0.5f; z += 0.9f)
                room.ShadedBox(at + new Vector3(0f, BenchHeight - 0.05f, z + 0.45f), new Vector3(0.9f, 0.1f, 0.88f), Pal.Wood, rng, 0.3f, 0.4f);
            foreach (float z in new[] { -length * 0.5f + 0.15f, 0f, length * 0.5f - 0.15f })
                Timber(at + new Vector3(-0.3f, (BenchHeight - 0.1f) * 0.5f, z), new Vector3(0.14f, BenchHeight - 0.1f, 0.14f), 0.4f);
            Solid("Rack Bench", at + Vector3.up * (BenchHeight * 0.5f), new Vector3(0.9f, BenchHeight, length));

            // Ingots, a coil of rope and a striped cloth, as painted.
            float top = BenchHeight + 0.01f;
            foreach (float z in new[] { 1.5f, 2f })
            {
                room.ShadedBox(new Vector3(at.x, top + 0.05f, z), new Vector3(0.28f, 0.1f, 0.42f), Pal.SteelDark, rng, 0.46f, 0.54f);
                room.ShadedBox(new Vector3(at.x + 0.03f, top + 0.15f, z + 0.02f), new Vector3(0.26f, 0.09f, 0.38f), Pal.SteelDark, rng, 0.5f, 0.58f);
            }
            for (int i = 0; i < 4; i++)
            {
                room.SetColor(Pal.Thatch, 0.3f + i % 2 * 0.1f);
                room.Prism(new Vector3(at.x + 0.1f, top + i * 0.05f, -0.6f), 0.22f, 0.2f, 0.05f, 9, false, false);
            }
            for (int i = 0; i < 7; i++)
            {
                room.SetColor(i % 3 == 1 ? Pal.ClothRed : Pal.Thatch, i % 3 == 1 ? 0.3f : 0.34f);
                float z = -2f + i * 0.14f;
                room.Box(new Vector3(at.x - 0.44f, top - 0.32f, z), new Vector3(0.02f, 0.66f, 0.13f));
                room.Box(new Vector3(at.x, top + 0.01f, z), new Vector3(0.9f, 0.02f, 0.13f));
            }

            // Under it, a crate of bar stock.
            Prop(at + new Vector3(0f, 0f, 1.4f), 90f, b => PropMeshes.Crate(b, rng, 0.62f));
            for (int i = 0; i < 5; i++)
                Iron(at + new Vector3(R(-0.2f, 0.2f), 0.64f, 1.4f + R(-0.2f, 0.2f)), new Vector3(0.1f, 0.06f, 0.46f));

            room.SetColor(Pal.SteelDark, 0.34f);
            room.Box(new Vector3(3.6f, BeamY - 0.5f, 0.6f), new Vector3(0.04f, 0.8f, 0.04f));
            Lantern(new Vector3(3.6f, BeamY - 1.34f, 0.6f), 0.8f);
            AddGlow(new Vector3(3.9f, BeamY - 1.4f, 0.6f), LampLight, 8f);
        }

        /// <summary>Hangs a weapon on the wall, hilt up, flat to the room.</summary>
        void Rack(float z, float y, float scale, System.Action<MeshBuilder> weapon)
        {
            float wall = HalfWidth - 0.12f;
            room.Push(new Vector3(wall, y, z), Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler(0f, 0f, 180f), Vector3.one * scale);
            weapon(room);
            room.Pop();
            Iron(new Vector3(wall - 0.03f, y + 0.02f, z), new Vector3(0.1f, 0.04f, 0.2f));
        }

        void HungStaff(Vector3 grip)
        {
            room.Push(grip, Quaternion.Euler(0f, 90f, 0f), new Vector3(1.7f, 1.05f, 1.7f));
            TownMeshes.PilgrimStaff(room);
            room.Pop();
            Iron(grip + new Vector3(-0.03f, 0.42f, 0f), new Vector3(0.1f, 0.04f, 0.14f));
            Iron(grip + new Vector3(-0.03f, -0.5f, 0f), new Vector3(0.1f, 0.04f, 0.14f));
        }

        // ---- The rest ---------------------------------------------------------------------------------------

        void BuildCorners()
        {
            // The grindstone, out in the light of the window.
            var at = new Vector3(3f, 0f, 2.6f);
            room.SetColor(Pal.WoodDark, 0.38f);
            foreach (float z in new[] { -0.3f, 0.3f })
            {
                foreach (float x in new[] { -0.46f, 0.46f })
                    Prop(at + new Vector3(x, 0.32f, z), 0f, b => b.Box(Vector3.zero, new Vector3(0.11f, 0.68f, 0.11f)));
                Timber(at + new Vector3(0f, 0.7f, z), new Vector3(1.3f, 0.12f, 0.11f), 0.38f);
            }
            room.SetColor(Pal.RockWarm, 0.5f);
            room.Current.Layer = TexLayer.Plain;
            room.Push(at + new Vector3(0f, 1.02f, -0.09f), Quaternion.Euler(90f, 0f, 0f), Vector3.one);
            room.Prism(Vector3.zero, 0.56f, 0.56f, 0.18f, 14);
            room.Pop();
            Iron(at + new Vector3(0f, 1.02f, 0f), new Vector3(0.06f, 0.06f, 0.96f));
            Iron(at + new Vector3(0f, 0.9f, 0.47f), new Vector3(0.05f, 0.3f, 0.05f));
            Iron(at + new Vector3(0f, 0.76f, 0.54f), new Vector3(0.05f, 0.05f, 0.2f));
            Solid("Grindstone", at + Vector3.up * 0.8f, new Vector3(1.3f, 1.6f, 0.95f));

            Prop(new Vector3(5.75f, 0f, -4.75f), 30f, b => PropMeshes.Barrel(b, rng));
            Prop(new Vector3(4.9f, 0f, -4.9f), 200f, b => PropMeshes.Barrel(b, rng));
            Solid("Barrels", new Vector3(5.3f, 0.45f, -4.8f), new Vector3(1.8f, 0.9f, 0.9f));
            Prop(new Vector3(-5.75f, 0f, -4.75f), 15f, b => PropMeshes.Crate(b, rng, 0.9f));
            Solid("Crate", new Vector3(-5.75f, 0.45f, -4.75f), new Vector3(1f, 0.9f, 1f));

            // Scale on the flags round the anvil, and the odd dropped nail.
            for (int i = 0; i < 40; i++)
            {
                room.SetColor(Pal.SteelDark, R(0.12f, 0.26f));
                room.Box(new Vector3(R(-3.4f, 0.8f), 0.012f, R(-0.6f, 3f)), new Vector3(R(0.03f, 0.1f), 0.012f, R(0.03f, 0.1f)));
            }
        }

        void BuildWindow()
        {
            float z = HalfDepth - 0.02f;
            const float x = 4.2f, y = 3.15f, width = 1.8f, height = 1.3f;
            room.SetColor(Pal.PaintBlue, 0.72f);
            room.Current.Layer = TexLayer.Plain;
            room.Current.Emission = 0.5f;
            room.Box(new Vector3(x, y, z), new Vector3(width, height, 0.04f));
            AddGlow(new Vector3(x - 0.3f, y - 0.2f, z - 1f), StreetLight * 1.4f, 9f);
            foreach (float along in new[] { -0.9f, -0.3f, 0.3f, 0.9f })
                Timber(new Vector3(x + along, y, z - 0.06f), new Vector3(0.1f, height + 0.1f, 0.1f), 0.3f);
            foreach (float up in new[] { -0.65f, 0f, 0.65f })
                Timber(new Vector3(x, y + up, z - 0.06f), new Vector3(width + 0.1f, 0.1f, 0.1f), 0.3f);
        }
    }
}
