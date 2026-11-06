using UnityEngine;

namespace AshenRealm.EditorTools
{
    /// <summary>Key positions of the tavern for the scene builder.</summary>
    internal sealed class TavernLayout : RoomLayout
    {
        public Vector3 Stair;
        public Vector3 StairArrival;
    }

    /// <summary>
    /// The Rope and Lantern from the inside, laid out after the concept painting as seen from the street door: the hearth
    /// and the tables on the left, the long bar on the right, and at the far end, under the rope and the lantern the
    /// house is named for, the iron-bound door to the stair. The stairwell is scenery; the door itself is the way down.
    /// </summary>
    internal sealed class TavernGenerator : RoomGenerator
    {
        const float HalfWidth = 7.5f;
        const float HalfDepth = 7.5f;
        const float Height = 4.4f;
        const float BeamY = 3.7f;
        const float WallThickness = 0.5f;
        const float HearthX = -4.6f;
        const float HearthWidth = 3f;
        const float HearthOpening = 2.2f;
        const float BarX = 3.6f;
        const float BarWidth = 1.1f;
        const float BarStart = -2.2f;
        const float BarEnd = 5.2f;
        const float BarHeight = 1.15f;
        const float TableHeight = 0.8f;

        public TavernGenerator(Material material, Vector3 origin, int seed) : base(material, origin, seed) { }

        public TavernLayout Generate(Transform parent)
        {
            Begin(parent);

            BuildShell();
            BuildTimber();
            BuildStairDoor();
            BuildStreetDoor();
            BuildHearth();
            BuildWindow();
            BuildBar();
            BuildBehindTheBar();
            BuildTables();
            BuildRopeAndLantern();
            Finish("Tavern_Room");

            // Two real lights on top of the baked ones, so the fire and the great lantern still breathe.
            Light("Hearth Light", new Vector3(HearthX, 1.05f, HalfDepth - 1.5f), new Color(1f, 0.52f, 0.22f), 9f, 1.8f, 0.5f);
            Light("Rope Lantern", new Vector3(0f, 2.3f, 5.4f), new Color(1f, 0.68f, 0.36f), 8f, 0.9f, 0.15f);

            var layout = new TavernLayout
            {
                Entry = origin + new Vector3(0f, 0.05f, -6f),
                DoorOut = origin + new Vector3(0f, 0f, -HalfDepth + 0.3f),
                Stair = origin + new Vector3(0f, 0f, HalfDepth - 0.1f),
                StairArrival = origin + new Vector3(0f, 0.05f, HalfDepth - 2.2f),
            };
            Person(layout, "wanderer", new Vector3(-5.45f, 0f, -3.1f), 90f, PeopleMeshes.Wanderer);
            Person(layout, "quartermaster", new Vector3(-2.45f, 0f, 2.9f), 270f, PeopleMeshes.Sergeant);
            Person(layout, "landlord", new Vector3(4.85f, 0f, 2.2f), 270f, PeopleMeshes.Landlord);
            return layout;
        }

        // ---- Floor, walls, ceiling -------------------------------------------------------------------------

        void BuildShell()
        {
            Flagstones(HalfWidth, HalfDepth, 1.5f, 0f, 0.2f, 0.34f);
            Solid("Floor", new Vector3(0f, -0.5f, 0f), new Vector3(60f, 1f, 60f));

            for (float x = -HalfWidth; x < HalfWidth; x += LongPiece)
            for (float z = -HalfDepth; z < HalfDepth; z += LongPiece)
                room.ShadedBox(new Vector3(x + LongPiece * 0.5f, Height + 0.1f, z + LongPiece * 0.5f), new Vector3(LongPiece, 0.2f, LongPiece), Pal.WoodDark, rng, 0.3f, 0.36f);
            Solid("Ceiling", new Vector3(0f, Height + 0.5f, 0f), new Vector3(60f, 1f, 60f));

            float outer = HalfWidth + WallThickness * 0.5f;
            float end = HalfDepth + WallThickness * 0.5f;
            Wall(new Vector3(-outer, 0f, -HalfDepth), new Vector3(-outer, 0f, HalfDepth));
            Wall(new Vector3(outer, 0f, -HalfDepth), new Vector3(outer, 0f, HalfDepth));
            Wall(new Vector3(-HalfWidth, 0f, -end), new Vector3(HalfWidth, 0f, -end));
            Wall(new Vector3(-HalfWidth, 0f, end), new Vector3(-1.6f, 0f, end));
            Wall(new Vector3(1.6f, 0f, end), new Vector3(HalfWidth, 0f, end));
            Solid("West Wall", new Vector3(-outer, Height * 0.5f, 0f), new Vector3(WallThickness, Height, 40f));
            Solid("East Wall", new Vector3(outer, Height * 0.5f, 0f), new Vector3(WallThickness, Height, 40f));
            Solid("South Wall", new Vector3(0f, Height * 0.5f, -end), new Vector3(40f, Height, WallThickness));
            Solid("North Wall West", new Vector3(-(HalfWidth + 1.6f) * 0.5f, Height * 0.5f, end), new Vector3(HalfWidth - 1.6f, Height, WallThickness));
            Solid("North Wall East", new Vector3((HalfWidth + 1.6f) * 0.5f, Height * 0.5f, end), new Vector3(HalfWidth - 1.6f, Height, WallThickness));
        }

        void Wall(Vector3 from, Vector3 to) => StoneWall(from, to, Height, WallThickness);

        void BuildTimber()
        {
            // Cross beams with a post at each end, the room's ribs.
            for (float z = -6f; z <= 6f; z += 3f)
            {
                Timber(new Vector3(0f, BeamY, z), new Vector3(HalfWidth * 2f, 0.4f, 0.42f));
                foreach (float side in new[] { -1f, 1f })
                {
                    Timber(new Vector3(side * (HalfWidth - 0.2f), BeamY * 0.5f, z), new Vector3(0.4f, BeamY, 0.42f));
                    room.SetColor(Pal.WoodDark, 0.38f);
                    room.Push(new Vector3(side * (HalfWidth - 0.75f), BeamY - 0.62f, z), Quaternion.Euler(0f, 0f, side * 45f), Vector3.one);
                    room.Box(Vector3.zero, new Vector3(0.2f, 1.5f, 0.24f));
                    room.Pop();
                }
            }

            // Long joists under the boards.
            foreach (float x in new[] { -5.2f, -2.6f, 0f, 2.6f, 5.2f })
                Timber(new Vector3(x, Height - 0.17f, 0f), new Vector3(0.3f, 0.34f, HalfDepth * 2f), 0.36f);
            foreach (float side in new[] { -1f, 1f })
                Timber(new Vector3(side * (HalfWidth - 0.15f), BeamY + 0.3f, 0f), new Vector3(0.3f, 0.3f, HalfDepth * 2f), 0.38f);
        }

        // ---- The way down --------------------------------------------------------------------------------

        void BuildStairDoor()
        {
            const float z = HalfDepth;
            const float inner = 1.4f;

            // The arch: dark oak shod with iron, on two great posts.
            foreach (float side in new[] { -1f, 1f })
            {
                Timber(new Vector3(side * 1.75f, 1.6f, z - 0.05f), new Vector3(0.5f, 3.2f, 0.7f), 0.46f);
                foreach (float y in new[] { 0.5f, 1.5f, 2.5f })
                    Iron(new Vector3(side * 1.75f, y, z - 0.42f), new Vector3(0.54f, 0.1f, 0.04f));
                Timber(new Vector3(side * 2.45f, Height * 0.5f, z - 0.2f), new Vector3(0.5f, Height, 0.5f), 0.42f);
                Solid("Stair Frame", new Vector3(side * 1.75f, 1.6f, z - 0.05f), new Vector3(0.5f, 3.2f, 0.7f));
            }
            for (int i = 0; i <= 10; i++)
            {
                float angle = i * 18f;
                float a = angle * Mathf.Deg2Rad;
                room.SetColor(Pal.WoodDark, i % 2 == 0 ? 0.5f : 0.42f);
                room.Push(new Vector3(Mathf.Cos(a) * 1.65f, 2.05f + Mathf.Sin(a) * 1.65f, z - 0.05f), Quaternion.Euler(0f, 0f, angle), Vector3.one);
                room.Box(Vector3.zero, new Vector3(0.5f, 0.54f, 0.66f));
                room.Pop();
                Iron(new Vector3(Mathf.Cos(a) * 1.65f, 2.05f + Mathf.Sin(a) * 1.65f, z - 0.4f), new Vector3(0.09f, 0.09f, 0.03f));
            }
            room.ShadedBox(new Vector3(0f, 3.62f, z + 0.2f), new Vector3(3.2f, 1.6f, 0.5f), Pal.Stone, rng, 0.3f, 0.36f);

            // Beyond it the steps go down into nothing.
            for (int i = 0; i < 5; i++)
            {
                float along = z + 0.5f + i;
                foreach (float side in new[] { -1f, 1f })
                    room.ShadedBox(new Vector3(side * (inner + 0.25f), 0.2f, along), new Vector3(0.5f, 5.6f, 1f), Pal.Stone, rng, 0.4f, 0.48f);
                room.ShadedBox(new Vector3(0f, 3.2f, along), new Vector3(inner * 2f + 0.2f, 0.4f, 1f), Pal.Stone, rng, 0.34f, 0.4f);
            }
            for (int i = 0; i < 9; i++)
                room.ShadedBox(new Vector3(0f, -0.1f - i * 0.11f, z + 0.25f + i * 0.5f), new Vector3(inner * 2f, 0.2f, 0.5f), Pal.Stone, rng, 0.46f - i * 0.03f, 0.5f - i * 0.03f);
            room.SetColor(Pal.Black, 0.1f);
            room.Box(new Vector3(0f, 0.2f, z + 5f), new Vector3(inner * 2f, 5.6f, 0.2f));
            room.SetColor(Pal.SteelDark, 0.34f);
            room.Push(new Vector3(inner - 0.04f, 1.05f, z + 0.1f), Quaternion.Euler(30f, 0f, 0f), Vector3.one);
            room.Box(new Vector3(0f, 0f, 1.6f), new Vector3(0.05f, 0.05f, 3.4f));
            room.Pop();

            // Both leaves stand open, swung into the room.
            foreach (float side in new[] { -1f, 1f })
            {
                room.Push(new Vector3(side * 1.55f, 0f, z - 0.42f), Quaternion.Euler(0f, side * 9f, 0f), Vector3.one);
                room.ShadedBox(new Vector3(0f, 1.48f, -0.7f), new Vector3(0.13f, 2.96f, 1.4f), Pal.WoodDark, rng, 0.46f, 0.52f);
                foreach (float y in new[] { 0.45f, 1.5f, 2.55f })
                    Iron(new Vector3(0f, y, -0.7f), new Vector3(0.17f, 0.13f, 1.42f));
                Iron(new Vector3(side * 0.09f, 1.32f, -1.2f), new Vector3(0.05f, 0.2f, 0.09f));
                room.Pop();
                Solid("Stair Door Leaf", new Vector3(side * 1.65f, 1.48f, z - 1.12f), new Vector3(0.24f, 2.96f, 1.4f));
            }
        }

        void BuildStreetDoor() => StreetDoor(-HalfDepth + 0.04f);

        // ---- The hearth ------------------------------------------------------------------------------------

        void BuildHearth()
        {
            float cx = HearthX;
            const float back = HalfDepth, depth = 1.2f, open = HearthOpening;
            float z = back - depth * 0.5f;

            foreach (float side in new[] { -1f, 1f })
            {
                for (int i = 0; i < 4; i++)
                    room.ShadedBox(new Vector3(cx + side * (HearthWidth * 0.5f + 0.5f), 0.275f + i * 0.55f, z), new Vector3(1f, 0.55f, depth), Pal.Stone, rng, 0.4f, 0.54f);
            }
            for (float y = open; y < Height - 0.01f; y += 0.55f)
            for (int i = 0; i < 4; i++)
                room.ShadedBox(new Vector3(cx + (i - 1.5f) * (HearthWidth + 2f) * 0.25f, y + 0.275f, z), new Vector3((HearthWidth + 2f) * 0.25f - 0.02f, 0.53f, depth), Pal.Stone, rng, 0.36f, 0.48f);
            for (int i = 0; i < 9; i++)
            {
                float x = cx + (i - 4f) * 0.34f;
                float fill = 0.2f + Mathf.Abs(i - 4f) * 0.09f;
                room.ShadedBox(new Vector3(x, open - fill * 0.5f, back - depth + 0.14f), new Vector3(0.33f, fill, 0.3f), Pal.Stone, rng, 0.44f, 0.56f);
            }
            room.SetColor(Pal.Black, 0.2f);
            room.Box(new Vector3(cx, open * 0.5f, back - 0.05f), new Vector3(HearthWidth, open, 0.1f));
            room.ShadedBox(new Vector3(cx, 0.06f, z - 0.3f), new Vector3(HearthWidth + 2.4f, 0.12f, depth + 0.8f), Pal.Stone, rng, 0.36f, 0.42f);
            Solid("Hearth", new Vector3(cx, Height * 0.5f, z), new Vector3(HearthWidth + 2f, Height, depth));

            // Logs, embers and the fire.
            foreach (float x in new[] { -0.55f, 0f, 0.5f })
            {
                room.SetColor(Pal.Bark, 0.26f);
                room.Push(new Vector3(cx + x, 0.2f, z + 0.05f - x * 0.2f), Quaternion.Euler(90f, x * 40f, 0f), Vector3.one);
                room.Prism(new Vector3(0f, -0.45f, 0f), 0.1f, 0.1f, 0.9f, 6);
                room.Pop();
            }
            for (int i = 0; i < 12; i++)
            {
                room.SetColor(Pal.Lava, R(0.4f, 0.8f));
                room.Box(new Vector3(cx + R(-1f, 1f), 0.15f, z + R(-0.3f, 0.34f)), new Vector3(R(0.08f, 0.18f), 0.05f, R(0.08f, 0.18f)));
            }
            room.Push(new Vector3(cx, 0.1f, z), Quaternion.identity, new Vector3(1.9f, 1.25f, 1.3f));
            PropMeshes.Flames(room, rng);
            room.Pop();
            AddGlow(new Vector3(cx, 1.1f, z - 1f), FireLight, 11f);

            // The pot on its chain.
            room.SetColor(Pal.SteelDark, 0.26f);
            room.Prism(new Vector3(cx, 0.9f, z - 0.05f), 0.18f, 0.34f, 0.22f, 9);
            room.Prism(new Vector3(cx, 1.12f, z - 0.05f), 0.34f, 0.28f, 0.22f, 9);
            room.Box(new Vector3(cx, 1.76f, z - 0.05f), new Vector3(0.03f, 0.9f, 0.03f));

            // The mantel and what the house keeps on it.
            Timber(new Vector3(cx, open + 0.1f, back - depth - 0.16f), new Vector3(HearthWidth + 2.3f, 0.2f, 0.42f), 0.46f);
            float y0 = open + 0.2f, shelf = back - depth - 0.14f;
            room.SetColor(Pal.Brass, 0.44f);
            room.Push(new Vector3(cx - 1.5f, y0 + 0.27f, shelf + 0.1f), Quaternion.Euler(80f, 0f, 0f), Vector3.one);
            room.Prism(Vector3.zero, 0.28f, 0.3f, 0.03f, 10);
            room.Pop();
            foreach (float x in new[] { 0.5f, 0.86f, 1.2f })
            {
                room.SetColor(Pal.RobeRot, R(0.36f, 0.5f));
                room.Prism(new Vector3(cx + x, y0, shelf), 0.07f, 0.07f, 0.24f, 6);
                room.Prism(new Vector3(cx + x, y0 + 0.24f, shelf), 0.07f, 0.025f, 0.14f, 6);
            }
            foreach (float x in new[] { -0.8f, -0.3f })
            {
                room.SetColor(Pal.Leather, R(0.5f, 0.62f));
                room.Prism(new Vector3(cx + x, y0, shelf), 0.1f, 0.14f, 0.14f, 7);
                room.Prism(new Vector3(cx + x, y0 + 0.14f, shelf), 0.14f, 0.08f, 0.1f, 7);
            }
        }

        // ---- The west wall ---------------------------------------------------------------------------------

        void BuildWindow()
        {
            float x = -HalfWidth + 0.02f;
            const float z = -0.6f, y = 2.3f, width = 2.6f, height = 2.6f;

            room.SetColor(Pal.PaintBlue, 0.7f);
            room.Current.Layer = TexLayer.Plain;
            room.Current.Emission = 0.5f;
            room.Box(new Vector3(x, y, z), new Vector3(0.04f, height, width));
            AddGlow(new Vector3(x + 0.6f, y, z), StreetLight, 7f);

            // Leaded panes: a diamond lattice over the cold light of the street.
            room.SetColor(Pal.Black, 0.3f);
            const float pane = 0.42f;
            foreach (float tilt in new[] { 45f, -45f })
            for (int i = -5; i <= 5; i++)
            {
                float offset = i * pane;
                float length = Mathf.Max(0f, width * 1.41f - Mathf.Abs(offset) * 2f);
                if (length < 0.2f) continue;
                room.Push(new Vector3(x + 0.04f, y, z), Quaternion.Euler(tilt, 0f, 0f), Vector3.one);
                room.Box(new Vector3(0f, offset, 0f), new Vector3(0.02f, 0.03f, length));
                room.Pop();
            }
            foreach (float along in new[] { -width * 0.5f, -width / 6f, width / 6f, width * 0.5f })
                Timber(new Vector3(x + 0.08f, y, z + along), new Vector3(0.16f, height + 0.2f, 0.14f), 0.44f);
            foreach (float up in new[] { -height * 0.5f, 0.1f, height * 0.5f })
                Timber(new Vector3(x + 0.08f, y + up, z), new Vector3(0.16f, 0.14f, width + 0.14f), 0.44f);
            room.ShadedBox(new Vector3(x + 0.2f, y - height * 0.5f - 0.1f, z), new Vector3(0.4f, 0.14f, width + 0.4f), Pal.Stone, rng, 0.44f, 0.52f);

            // The order's banner beside it, tattered at the foot.
            float cloth = x + 0.06f;
            Iron(new Vector3(cloth + 0.1f, 3.4f, 2.2f), new Vector3(0.06f, 0.06f, 1.2f));
            room.SetColor(Pal.ClothRed, 0.5f);
            room.Box(new Vector3(cloth, 2.5f, 2.2f), new Vector3(0.03f, 1.8f, 1.05f));
            for (int i = 0; i < 5; i++)
            {
                float length = R(0.25f, 0.6f);
                room.SetColor(Pal.ClothRed, R(0.4f, 0.5f));
                room.Box(new Vector3(cloth, 1.6f - length * 0.5f, 1.78f + i * 0.21f), new Vector3(0.03f, length, 0.2f));
            }
            room.SetColor(Pal.Bone, 0.6f);
            room.Box(new Vector3(cloth + 0.02f, 2.55f, 2.2f), new Vector3(0.02f, 1.2f, 0.1f));
            room.Box(new Vector3(cloth + 0.02f, 2.8f, 2.2f), new Vector3(0.02f, 0.1f, 0.62f));
        }

        // ---- The bar -------------------------------------------------------------------------------------

        void BuildBar()
        {
            float length = BarEnd - BarStart, mid = (BarStart + BarEnd) * 0.5f;
            for (float z = BarStart; z < BarEnd - 0.01f; z += 0.8f)
            {
                room.ShadedBox(new Vector3(BarX, (BarHeight - 0.1f) * 0.5f, z + 0.4f), new Vector3(BarWidth - 0.14f, BarHeight - 0.1f, 0.78f), Pal.Wood, rng, 0.3f, 0.44f);
                room.ShadedBox(new Vector3(BarX, BarHeight - 0.05f, z + 0.4f), new Vector3(BarWidth + 0.1f, 0.12f, 0.8f), Pal.Wood, rng, 0.42f, 0.46f);
            }

            // Iron straps and studs on the customer's side.
            float face = BarX - BarWidth * 0.5f + 0.06f;
            for (float z = BarStart + 0.8f; z < BarEnd; z += 1.6f)
            {
                Iron(new Vector3(face, 0.18f, z), new Vector3(0.03f, 0.1f, 1.6f));
                Iron(new Vector3(face, 0.92f, z), new Vector3(0.03f, 0.1f, 1.6f));
                Iron(new Vector3(face, 0.55f, z), new Vector3(0.03f, 0.84f, 0.12f));
                room.SetColor(Pal.Steel, 0.36f);
                foreach (float y in new[] { 0.32f, 0.78f })
                    room.Box(new Vector3(face - 0.03f, y, z), new Vector3(0.03f, 0.05f, 0.05f));
            }
            Solid("Bar", new Vector3(BarX, BarHeight * 0.5f, mid), new Vector3(BarWidth + 0.1f, BarHeight, length + 0.1f));

            // The south end closes against the wall, so the only way behind is past the far end.
            float returnX = (BarX + HalfWidth) * 0.5f + 0.2f, returnWidth = HalfWidth - BarX - 0.4f;
            room.ShadedBox(new Vector3(returnX, (BarHeight - 0.1f) * 0.5f, BarStart + 0.3f), new Vector3(returnWidth, BarHeight - 0.1f, 0.6f), Pal.Wood, rng, 0.3f, 0.4f);
            room.ShadedBox(new Vector3(returnX, BarHeight - 0.05f, BarStart + 0.3f), new Vector3(returnWidth, 0.12f, 0.7f), Pal.Wood, rng, 0.42f, 0.46f);
            Solid("Bar Return", new Vector3(returnX, BarHeight * 0.5f, BarStart + 0.3f), new Vector3(returnWidth, BarHeight, 0.7f));

            foreach (float z in new[] { -0.4f, 1.7f, 3.8f })
            {
                Vector3 seat = new Vector3(2.45f, 0f, z);
                Stool(seat);
                Solid("Stool", seat + Vector3.up * 0.35f, new Vector3(0.45f, 0.7f, 0.45f));
            }

            float top = BarHeight + 0.01f;
            Jug(new Vector3(BarX + 0.1f, top, 3.5f), 1.25f, Pal.Leather);
            Mug(new Vector3(BarX + 0.25f, top, 3.05f));
            Mug(new Vector3(BarX - 0.28f, top, 0.8f));
            Mug(new Vector3(BarX + 0.1f, top, -1.2f));
            Candle(new Vector3(BarX, top, 2.55f), 0.2f);

            // A bowl of apples, and the rag over the edge.
            room.SetColor(Pal.Wood, 0.46f);
            room.Prism(new Vector3(BarX + 0.05f, top, 1.8f), 0.13f, 0.24f, 0.12f, 8);
            for (int i = 0; i < 4; i++)
            {
                room.SetColor(Pal.FlowerRed, R(0.4f, 0.55f));
                room.Box(new Vector3(BarX + 0.05f + R(-0.12f, 0.12f), top + 0.14f, 1.8f + R(-0.12f, 0.12f)), new Vector3(0.1f, 0.1f, 0.1f));
            }
            float edge = BarX - BarWidth * 0.5f - 0.05f;
            room.SetColor(Pal.Plaster, 0.46f);
            room.Box(new Vector3(edge + 0.2f, top + 0.01f, -0.9f), new Vector3(0.5f, 0.02f, 0.5f));
            room.Box(new Vector3(edge - 0.02f, top - 0.32f, -0.9f), new Vector3(0.02f, 0.66f, 0.5f));
            room.SetColor(Pal.ClothRed, 0.5f);
            room.Box(new Vector3(edge - 0.04f, top - 0.5f, -0.9f), new Vector3(0.02f, 0.05f, 0.5f));
        }

        void BuildBehindTheBar()
        {
            // Two great casks on a cradle, taps to the room.
            foreach (float z in new[] { 0.8f, 2.9f })
            {
                room.Push(new Vector3(HalfWidth, 1.12f, z), Quaternion.Euler(0f, 0f, 90f), Vector3.one * 1.7f);
                PropMeshes.Barrel(room, rng);
                room.Pop();
                Iron(new Vector3(HalfWidth - 1.6f, 0.86f, z), new Vector3(0.14f, 0.08f, 0.08f));
                room.SetColor(Pal.Brass, 0.4f);
                room.Box(new Vector3(HalfWidth - 1.66f, 0.78f, z), new Vector3(0.05f, 0.14f, 0.05f));
                Timber(new Vector3(HalfWidth - 0.75f, 0.2f, z), new Vector3(1.3f, 0.4f, 1.3f), 0.38f);
                Solid("Cask", new Vector3(HalfWidth - 0.75f, 1f, z), new Vector3(1.5f, 2f, 1.4f));
            }

            // Shelves of jugs at the far end.
            foreach (float y in new[] { 1.3f, 2.05f, 2.8f })
            {
                Timber(new Vector3(HalfWidth - 0.2f, y, 5.3f), new Vector3(0.4f, 0.07f, 2f), 0.46f);
                for (float z = 4.55f; z < 6.15f; z += 0.42f)
                {
                    if (rng.NextDouble() < 0.25) continue;
                    Jug(new Vector3(HalfWidth - 0.2f, y + 0.04f, z), R(0.62f, 0.85f), rng.NextDouble() < 0.5 ? Pal.Leather : Pal.RockWarm);
                }
            }

            // Mugs on a rail above Aggie's head, and bunches of herbs from the beams.
            Iron(new Vector3(BarX + 1f, 3.22f, 2.5f), new Vector3(0.05f, 0.05f, 5.6f));
            foreach (float z in new[] { 0f, 5f })
                Iron(new Vector3(BarX + 1f, 3.46f, z), new Vector3(0.04f, 0.5f, 0.04f));
            for (float z = 0.1f; z < 5.1f; z += 0.72f)
            {
                room.SetColor(Pal.SteelDark, R(0.5f, 0.62f));
                room.Prism(new Vector3(BarX + 1f, 2.94f, z), 0.08f, 0.09f, 0.2f, 7);
                room.Box(new Vector3(BarX + 1f, 3.17f, z), new Vector3(0.02f, 0.08f, 0.02f));
            }
            foreach (Vector3 p in new[] { new Vector3(6.4f, 3.5f, 0f), new Vector3(6f, 3.5f, 3f), new Vector3(6.6f, 3.5f, 3f), new Vector3(2.4f, 3.5f, 6f), new Vector3(-1.2f, 3.5f, 3f), new Vector3(-6.3f, 3.5f, 6f), new Vector3(-5.8f, 3.5f, 0f) })
                Herbs(p);
        }

        /// <summary>A bunch of dried stalks, tied at the top.</summary>
        void Herbs(Vector3 hang)
        {
            for (int i = 0; i < 9; i++)
            {
                float a = i * 0.7f;
                float length = R(0.42f, 0.62f);
                room.SetColor(Pal.Thatch, R(0.34f, 0.52f));
                room.Push(hang + new Vector3(0f, -0.14f, 0f), Quaternion.Euler(Mathf.Cos(a) * 11f, 0f, Mathf.Sin(a) * 11f), Vector3.one);
                room.Box(new Vector3(0f, -length * 0.5f, 0f), new Vector3(0.05f, length, 0.05f));
                room.Pop();
            }
            room.SetColor(Pal.Wood, 0.4f);
            room.Box(hang + new Vector3(0f, -0.08f, 0f), new Vector3(0.03f, 0.16f, 0.03f));
        }

        // ---- Tables ---------------------------------------------------------------------------------------

        void BuildTables()
        {
            // The Wanderer's table: the long one nearest the door, a fur over his chair.
            var wanderer = new Vector3(-4f, 0f, -3.1f);
            LongTable(wanderer);
            Chair(new Vector3(-5.5f, 0f, -3.1f), 90f, true);
            Mug(new Vector3(-4.62f, TableHeight, -2.8f));
            Candle(new Vector3(-4.36f, TableHeight, -3.35f), 0.2f);
            Stool(new Vector3(-3.5f, 0f, -2.1f));
            Solid("Stool", new Vector3(-3.5f, 0.35f, -2.1f), new Vector3(0.45f, 0.7f, 0.45f));

            // Halloway's table, near the fire.
            var sergeant = new Vector3(-3.9f, 0f, 2.9f);
            RoundTable(sergeant);
            Chair(new Vector3(-2.45f, 0f, 2.9f), 270f, false);
            Chair(new Vector3(-5.3f, 0f, 3.2f), 100f, true);
            Mug(new Vector3(-3.6f, TableHeight, 3.15f));
            Candle(new Vector3(-4.2f, TableHeight, 2.7f), 0.18f);

            // His board: what he came to the tavern to keep.
            NoticeBoard(new Vector3(-1.3f, 0f, 1.6f), 105f);

            // By the street door: a third table, a rug, casks in the corner.
            var near = new Vector3(4.8f, 0f, -5f);
            RoundTable(near);
            Chair(new Vector3(3.5f, 0f, -5.2f), 80f, false);
            Candle(new Vector3(4.85f, TableHeight, -4.9f), 0.26f);
            room.SetColor(Pal.Wood, 0.44f);
            room.Prism(new Vector3(4.45f, TableHeight, -4.65f), 0.08f, 0.15f, 0.07f, 8);

            room.SetColor(Pal.ClothRed, 0.3f);
            room.Box(new Vector3(1.9f, 0.012f, -4.2f), new Vector3(1.9f, 0.02f, 3f));
            room.SetColor(Pal.ClothDark, 0.44f);
            room.Box(new Vector3(1.9f, 0.016f, -4.2f), new Vector3(1.55f, 0.02f, 2.65f));
            room.SetColor(Pal.ClothRed, 0.42f);
            room.Box(new Vector3(1.9f, 0.02f, -4.2f), new Vector3(1.3f, 0.02f, 2.4f));

            foreach (Vector3 p in new[] { new Vector3(6.6f, 0f, -6.6f), new Vector3(5.75f, 0f, -6.7f), new Vector3(6.75f, 0f, -5.7f) })
            {
                Prop(p, R(0f, 360f), b => PropMeshes.Barrel(b, rng));
                Solid("Barrel", p + Vector3.up * 0.45f, new Vector3(0.8f, 0.9f, 0.8f));
            }
            Prop(new Vector3(-6.5f, 0f, -6.5f), 12f, b => PropMeshes.Crate(b, rng, 0.95f));
            Prop(new Vector3(-6.4f, 0.95f, -6.4f), 40f, b => PropMeshes.Crate(b, rng, 0.62f));
            Solid("Crates", new Vector3(-6.5f, 0.8f, -6.5f), new Vector3(1.1f, 1.6f, 1.1f));
        }

        void LongTable(Vector3 p)
        {
            room.ShadedBox(p + new Vector3(0f, TableHeight - 0.05f, 0f), new Vector3(1.9f, 0.1f, 1f), Pal.Wood, rng, 0.4f, 0.46f);
            foreach (float x in new[] { -0.7f, 0.7f })
            {
                Timber(p + new Vector3(x, (TableHeight - 0.1f) * 0.5f, 0f), new Vector3(0.14f, TableHeight - 0.1f, 0.7f));
                Timber(p + new Vector3(x, 0.05f, 0f), new Vector3(0.2f, 0.1f, 0.92f), 0.36f);
            }
            Timber(p + new Vector3(0f, 0.3f, 0f), new Vector3(1.4f, 0.1f, 0.1f), 0.36f);
            Solid("Table", p + Vector3.up * (TableHeight * 0.5f), new Vector3(1.9f, TableHeight, 1f));
        }

        void RoundTable(Vector3 p)
        {
            room.SetColor(Pal.Wood, 0.46f);
            room.Prism(p + Vector3.up * (TableHeight - 0.08f), 0.85f, 0.85f, 0.08f, 12);
            room.SetColor(Pal.WoodDark, 0.4f);
            room.Prism(p, 0.14f, 0.12f, TableHeight - 0.08f, 7);
            foreach (float yaw in new[] { 0f, 90f })
                Prop(p, yaw, b => b.Box(new Vector3(0f, 0.05f, 0f), new Vector3(1f, 0.1f, 0.12f)));
            Solid("Table", p + Vector3.up * (TableHeight * 0.5f), new Vector3(1.3f, TableHeight, 1.3f));
        }

        void Stool(Vector3 p)
        {
            room.SetColor(Pal.Wood, 0.46f);
            room.Prism(p + Vector3.up * 0.64f, 0.21f, 0.22f, 0.08f, 9);
            room.SetColor(Pal.WoodDark, 0.42f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + 0.78f;
                room.Box(p + new Vector3(Mathf.Cos(a) * 0.15f, 0.32f, Mathf.Sin(a) * 0.15f), new Vector3(0.05f, 0.64f, 0.05f));
            }
            room.Box(p + Vector3.up * 0.28f, new Vector3(0.32f, 0.03f, 0.03f));
            room.Box(p + Vector3.up * 0.28f, new Vector3(0.03f, 0.03f, 0.32f));
        }

        /// <summary>Back to -Z before the yaw, like a person sitting on it faces +Z.</summary>
        void Chair(Vector3 p, float yaw, bool fur)
        {
            Prop(p, yaw, b =>
            {
                b.SetColor(Pal.Wood, 0.44f);
                b.Box(new Vector3(0f, 0.47f, 0f), new Vector3(0.52f, 0.05f, 0.5f));
                b.SetColor(Pal.WoodDark, 0.42f);
                foreach (float x in new[] { -0.22f, 0.22f })
                {
                    b.Box(new Vector3(x, 0.22f, 0.2f), new Vector3(0.05f, 0.44f, 0.05f));
                    b.Box(new Vector3(x, 0.58f, -0.22f), new Vector3(0.06f, 1.16f, 0.06f));
                }
                b.Box(new Vector3(0f, 1.12f, -0.22f), new Vector3(0.5f, 0.1f, 0.05f));
                b.Box(new Vector3(0f, 0.86f, -0.22f), new Vector3(0.44f, 0.07f, 0.04f));
                if (!fur) return;
                b.SetColor(Pal.FurGrey, 0.5f);
                b.Box(new Vector3(0f, 1.12f, -0.25f), new Vector3(0.56f, 0.16f, 0.14f));
                b.Box(new Vector3(0f, 0.86f, -0.3f), new Vector3(0.5f, 0.5f, 0.06f));
            });
            Solid("Chair", p + Vector3.up * 0.55f, new Vector3(0.55f, 1.1f, 0.55f), yaw);
        }

        void NoticeBoard(Vector3 p, float yaw)
        {
            Prop(p, yaw, b =>
            {
                b.SetColor(Pal.WoodDark, 0.42f);
                foreach (float x in new[] { -0.72f, 0.72f })
                    b.Box(new Vector3(x, 1.15f, 0f), new Vector3(0.1f, 2.3f, 0.1f));
                b.ShadedBox(new Vector3(0f, 1.62f, 0f), new Vector3(1.4f, 1f, 0.06f), Pal.Wood, rng, 0.38f, 0.42f);
                var notes = new System.Random(77);
                for (int i = 0; i < 7; i++)
                {
                    b.SetColor(Pal.Bone, MeshBuilder.Lerp(notes, 0.42f, 0.66f));
                    foreach (float side in new[] { -1f, 1f })
                    {
                        b.Push(new Vector3(-0.48f + (i % 4) * 0.32f, 1.86f - (i / 4) * 0.48f, side * 0.04f), Quaternion.Euler(0f, 0f, MeshBuilder.Lerp(notes, -5f, 5f)), Vector3.one);
                        b.Box(Vector3.zero, new Vector3(0.24f, 0.32f, 0.01f));
                        b.Pop();
                    }
                }
            });
            Solid("Notice Board", p + Vector3.up * 1.15f, new Vector3(1.5f, 2.3f, 0.2f), yaw);
        }

        void Mug(Vector3 p)
        {
            room.SetColor(Pal.SteelDark, R(0.52f, 0.62f));
            room.Prism(p, 0.075f, 0.075f, 0.16f, 8);
            room.SetColor(Pal.SteelDark, 0.4f);
            room.Box(p + new Vector3(0.1f, 0.08f, 0f), new Vector3(0.05f, 0.11f, 0.02f));
        }

        void Jug(Vector3 p, float scale, Pal family)
        {
            room.SetColor(family, R(0.46f, 0.6f));
            room.Prism(p, 0.08f * scale, 0.13f * scale, 0.14f * scale, 8);
            room.Prism(p + Vector3.up * (0.14f * scale), 0.13f * scale, 0.06f * scale, 0.12f * scale, 8);
            room.Prism(p + Vector3.up * (0.26f * scale), 0.06f * scale, 0.07f * scale, 0.06f * scale, 8);
        }

        // ---- The rope and the lantern ----------------------------------------------------------------------

        void BuildRopeAndLantern()
        {
            const float z = 6f;
            for (int i = 0; i < 5; i++)
            {
                room.SetColor(Pal.Thatch, 0.3f + i % 2 * 0.1f);
                room.Push(new Vector3(-0.3f + i * 0.15f, BeamY, z), Quaternion.Euler(0f, 0f, 90f), Vector3.one);
                room.Prism(new Vector3(0f, -0.05f, 0f), 0.27f, 0.27f, 0.1f, 8);
                room.Pop();
            }
            room.SetColor(Pal.Thatch, 0.34f);
            room.Prism(new Vector3(0f, 3.02f, z), 0.07f, 0.2f, 0.42f, 8);
            room.Prism(new Vector3(0f, 2.82f, z), 0.04f, 0.08f, 0.2f, 6);
            Lantern(new Vector3(0f, 2.26f, z), 1f);
            AddGlow(new Vector3(0f, 2.3f, z - 0.6f), LampLight, 11f);

            // A second, smaller one over the bar, and a third by the street door.
            Iron(new Vector3(BarX, 3.25f, 0f), new Vector3(0.04f, 0.5f, 0.04f));
            Lantern(new Vector3(BarX, 2.56f, 0f), 0.75f);
            AddGlow(new Vector3(BarX - 0.6f, 2.5f, 0f), LampLight, 11f);
            Iron(new Vector3(-1f, 3.25f, -3f), new Vector3(0.04f, 0.5f, 0.04f));
            Lantern(new Vector3(-1f, 2.56f, -3f), 0.75f);
            AddGlow(new Vector3(-1f, 2.4f, -3.4f), LampLight, 10f);
        }
    }
}
