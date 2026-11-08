using System.Collections.Generic;
using AshenRealm.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace AshenRealm.EditorTools
{
    /// <summary>Key positions of Hollowgate for the scene builder.</summary>
    internal sealed class CityLayout
    {
        public Transform Root;
        public Vector3 Entry;
        public float EntryYaw;
        public Vector3 Gate;
        public float GateYaw;
        public Vector3 TavernDoor;
        public Vector3 TavernArrival;
        public Vector3 ForgeDoor;
        public Vector3 ForgeArrival;
        public Vector3 ChapelDoor;
        public Vector3 ChapelArrival;
        public Vector3 Gallows;
        public readonly List<(string id, Vector3 position, float yaw, System.Action<MeshBuilder> figure)> People = new List<(string, Vector3, float, System.Action<MeshBuilder>)>();
    }

    /// <summary>
    /// Hollowgate (Üregkapu), the last city above the Deep Dark: a walled square of half-timbered houses with the Rope and
    /// Lantern to the north, the Cold Forge to the east, the Grey Chapel to the west, and the road in from the gate lined with
    /// the two rows of empty stalls of Gallows Market, the gallows at their far end. Reuses the valley's prop kit.
    /// </summary>
    internal sealed class CityGenerator
    {
        const float Half = 64f;
        const float WallHeight = 8f;
        const float StallRow = 5.7f;      // the middle of each row of stalls, either side of the road
        const float StallWidth = 3.5f;
        const float StallSpacing = 4f;
        const float GallowsZ = -3f;
        const float GallowsHalfWidth = 5f;
        const float GallowsHalfDepth = 3.5f;
        const float GallowsDeck = 1.68f;
        const int GallowsSteps = 7;
        const float GallowsRise = 0.24f;
        const float GallowsRun = 0.32f;

        const int Seedless = 913;         // the keep's hill keeps its shape whatever the city's random numbers do

        readonly Material material;
        readonly Vector3 origin;
        readonly System.Random rng;
        MeshBuilder ground, buildings, foliageFree, details;
        Transform root, colliderRoot;
        readonly List<Vector3> windowGlows = new List<Vector3>();
        readonly List<(Vector3 position, float strength)> courtyardGlows = new List<(Vector3, float)>();

        public CityGenerator(Material material, Vector3 origin, int seed)
        {
            this.material = material;
            this.origin = origin;
            rng = new System.Random(seed);
        }

        float R(float min, float max) => MeshBuilder.Lerp(rng, min, max);

        public CityLayout Generate(Transform parent)
        {
            root = new GameObject("Environment").transform;
            root.SetParent(parent, false);
            colliderRoot = new GameObject("Colliders").transform;
            colliderRoot.SetParent(root, false);
            ground = new MeshBuilder();
            buildings = new MeshBuilder();
            foliageFree = new MeshBuilder();
            details = new MeshBuilder();
            windowGlows.Clear();
            courtyardGlows.Clear();

            BuildGround();
            BuildWalls();
            var layout = new CityLayout { Root = root };
            BuildDistricts(layout);
            BuildStreetCharacter();
            BuildCourtyards();
            BuildMountains();

            // Amber windows stain the nearest stone while the rest keeps the blue evening light.
            ground.Bake(WindowLightAt);
            buildings.Bake(WindowLightAt);
            details.Bake(WindowLightAt);

            Save(ground, "City_Ground", ShadowCastingMode.Off);
            Save(buildings, "City_Buildings", ShadowCastingMode.On);
            Save(foliageFree, "City_Mountains", ShadowCastingMode.Off);
            Save(details, "City_Street_Details", ShadowCastingMode.On);
            WorldVista.Add(root, "Hollowgate_Gate_and_Market", origin + new Vector3(0f, 1.65f, -54f), origin + new Vector3(0f, 3.6f, -8f));
            WorldVista.Add(root, "Hollowgate_Well_of_Names", origin + new Vector3(-9f, 1.65f, 11f), origin + new Vector3(-15.5f, 1.6f, 19f));
            WorldVista.Add(root, "Hollowgate_Lantern_Court", origin + new Vector3(8f, 1.65f, 12f), origin + new Vector3(15.5f, 2.4f, 19f));
            WorldVista.Add(root, "Hollowgate_Tavern_Street", origin + new Vector3(0f, 1.65f, 15f), origin + new Vector3(0f, 3f, 32f));
            return layout;
        }

        void Save(MeshBuilder b, string name, ShadowCastingMode shadows)
        {
            Mesh mesh = GeneratedAssets.SaveMesh(b, name);
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = origin;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows;
        }

        void Box(string name, Vector3 local, Vector3 size, Quaternion rotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(colliderRoot, false);
            go.transform.SetPositionAndRotation(origin + local, rotation);
            go.AddComponent<BoxCollider>().size = size;
        }

        void BuildGround()
        {
            const float tile = 6f;
            for (float x = -Half - 90f; x < Half + 90f; x += tile)
            for (float z = -Half - 90f; z < Half + 90f; z += tile)
            {
                bool inside = Mathf.Abs(x + tile * 0.5f) < Half && Mathf.Abs(z + tile * 0.5f) < Half;
                float street = Mathf.Min(Mathf.Abs(x + tile * 0.5f), Mathf.Abs(z + tile * 0.5f - 2f));
                Pal family = !inside ? Pal.GrassDry : street < 7f || Vector2.Distance(new Vector2(x, z), Vector2.zero) < 20f ? Pal.Cobble : Pal.Dirt;
                ground.SetColor(family, R(0.34f, 0.5f));
                ground.QuadOutward(new Vector3(x, 0f, z), new Vector3(x + tile, 0f, z), new Vector3(x + tile, 0f, z + tile), new Vector3(x, 0f, z + tile),
                    new Vector3(x + tile * 0.5f, -1f, z + tile * 0.5f));
            }
            Box("Ground", new Vector3(0f, -0.5f, 0f), new Vector3((Half + 90f) * 2f, 1f, (Half + 90f) * 2f), Quaternion.identity);
        }

        void BuildWalls()
        {
            const float gateHalf = 4.5f;
            void Segment(Vector3 a, Vector3 b, string name)
            {
                Vector3 centre = (a + b) * 0.5f;
                Vector3 span = b - a;
                bool alongX = Mathf.Abs(span.x) > Mathf.Abs(span.z);
                float length = span.magnitude;
                Vector3 size = alongX ? new Vector3(length, WallHeight, 2f) : new Vector3(2f, WallHeight, length);
                buildings.ShadedBox(centre + Vector3.up * (WallHeight * 0.5f), size, Pal.Stone, rng, 0.35f, 0.5f);
                Box(name, centre + Vector3.up * (WallHeight * 0.5f), size + Vector3.up * 40f, Quaternion.identity);

                // Crenellations along the top.
                int merlons = Mathf.FloorToInt(length / 2.4f);
                for (int i = 0; i < merlons; i++)
                {
                    Vector3 p = a + span * ((i + 0.5f) / merlons) + Vector3.up * (WallHeight + 0.45f);
                    buildings.ShadedBox(p, new Vector3(1.3f, 0.9f, 2f), Pal.Stone, rng, 0.42f, 0.55f);
                }
            }

            Segment(new Vector3(-Half, 0f, Half), new Vector3(Half, 0f, Half), "North Wall");
            Segment(new Vector3(-Half, 0f, -Half), new Vector3(-Half, 0f, Half), "West Wall");
            Segment(new Vector3(Half, 0f, -Half), new Vector3(Half, 0f, Half), "East Wall");
            Segment(new Vector3(-Half, 0f, -Half), new Vector3(-gateHalf, 0f, -Half), "South Wall");
            Segment(new Vector3(gateHalf, 0f, -Half), new Vector3(Half, 0f, -Half), "South Wall");

            // Gatehouse over the road in from the valley.
            for (int i = -1; i <= 1; i += 2)
            {
                Vector3 tower = new Vector3(i * (gateHalf + 1.8f), 5.5f, -Half);
                buildings.ShadedBox(tower, new Vector3(3.6f, 11f, 3.6f), Pal.Stone, rng, 0.36f, 0.48f);
                Box("Gate Tower", tower, new Vector3(3.6f, 11f, 3.6f), Quaternion.identity);
            }
            buildings.ShadedBox(new Vector3(0f, 8.6f, -Half), new Vector3(gateHalf * 2f + 1f, 2.8f, 2.4f), Pal.Stone, rng, 0.4f, 0.52f);
            buildings.SetColor(Pal.Black, 0.2f);
            buildings.Box(new Vector3(0f, 3.6f, -Half - 1f), new Vector3(gateHalf * 2f, 7.2f, 0.2f));
        }

        void House(float x, float z, float yaw, float width, float depth, float height, bool tiled)
        {
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 p = new Vector3(x, -0.1f, z);
            buildings.Push(p, rotation, Vector3.one);
            PropMeshes.House(buildings, rng, width, depth, height, tiled);
            buildings.Pop();
            details.Push(p, rotation, Vector3.one);
            foreach (float side in new[] { -1f, 1f })
            foreach (float windowX in new[] { width * 0.22f, -width * 0.36f })
            {
                if (side < 0f && windowX < 0f) continue;
                Vector3 window = new Vector3(windowX, 0.6f + height * 0.62f, side * (depth * 0.5f + 0.18f));
                TownDetailMeshes.Window(details, window, side < 0f ? 0f : 180f, true);
                windowGlows.Add(p + rotation * (window + new Vector3(0f, -0.7f, side * 0.8f)));
            }
            if (width < 12f)
            {
                TownDetailMeshes.Awning(details, new Vector3(-width * 0.12f, 3f, -depth * 0.5f - 0.12f), 2.4f,
                    tiled ? Pal.ClothRed : Pal.PaintBlue);
                details.SetColor(Pal.Stone, 0.48f);
                details.Box(new Vector3(-width * 0.12f, 0.05f, -depth * 0.5f - 0.45f), new Vector3(1.7f, 0.12f, 0.65f));
            }
            details.Pop();
            Box("House", p + Vector3.up * ((0.6f + height + depth * 0.5f) * 0.5f), new Vector3(width + 0.4f, 0.6f + height + depth * 0.5f, depth + 0.4f), rotation);
        }

        void Prop(Vector3 p, float yaw, System.Action<MeshBuilder> build)
        {
            buildings.Push(p, Quaternion.Euler(0f, yaw, 0f), Vector3.one);
            build(buildings);
            buildings.Pop();
        }

        void Lantern(float x, float z)
        {
            Vector3 p = new Vector3(x, 0f, z);
            Prop(p, R(0f, 360f), b => PropMeshes.LanternPost(b, rng));
            Box("Lantern Post", p + Vector3.up * 1.4f, new Vector3(0.22f, 2.9f, 0.22f), Quaternion.identity);
        }

        void BuildDistricts(CityLayout layout)
        {
            // The road in, lined with narrow houses.
            float[] rows = { -52f, -40f, -28f };
            foreach (float z in rows)
            {
                House(-15f, z, -90f, 8f, 6f, 3.2f, z < -35f);
                House(15f, z, 90f, 8f, 6f, 3.0f, z > -45f);
            }

            // Back streets fill the corners, seen across the rooftops.
            for (int i = 0; i < 8; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                House(side * R(36f, 54f), R(-52f, -18f) , R(0f, 3f) * 90f, 7f, 5.5f, 3f, i % 3 != 0);
            }
            for (int i = 0; i < 8; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                House(side * R(34f, 54f), R(30f, 54f), R(0f, 3f) * 90f, 7f, 5.5f, 3.2f, i % 3 == 0);
            }

            // The Rope and Lantern: the big house on the north side of the square. The stair is inside, at the back.
            House(0f, 36f, 180f, 18f, 11f, 5f, true);
            Prop(new Vector3(0f, 0f, 30.42f), 0f, b => PropMeshes.TavernFront(b, rng));
            layout.TavernDoor = origin + new Vector3(0f, 0f, 29.95f);
            layout.TavernArrival = origin + new Vector3(0f, 0.05f, 27.4f);
            Prop(new Vector3(-9f, 0f, 30f), 90f, b => PropMeshes.Signpost(b, rng));
            Lantern(-7f, 28f);
            Lantern(7f, 28f);

            // The Cold Forge, east, at the end of the street that crosses the square: a chimney, and its door to the west.
            House(33f, 6f, -90f, 12f, 9f, 4f, false);
            buildings.ShadedBox(new Vector3(37f, 6f, 10f), new Vector3(2f, 12f, 2f), Pal.Stone, rng, 0.3f, 0.42f);
            Prop(new Vector3(28.4f, 0f, 6f), 90f, b => TownMeshes.ForgeFront(b, rng));
            layout.ForgeDoor = origin + new Vector3(28.05f, 0f, 6f);
            layout.ForgeArrival = origin + new Vector3(25.6f, 0.05f, 6f);
            Vector3 forgeYard = new Vector3(26.4f, 0f, 10.4f);
            Prop(forgeYard, 20f, b => PropMeshes.Barrel(b, rng));
            Box("Forge Barrel", forgeYard + Vector3.up * 0.45f, new Vector3(0.8f, 0.9f, 0.8f), Quaternion.identity);

            // The Grey Chapel, west: stone, with a tall nave and a small tower.
            buildings.ShadedBox(new Vector3(-36f, 4f, 6f), new Vector3(20f, 8f, 11f), Pal.Stone, rng, 0.36f, 0.48f);
            buildings.ShadedBox(new Vector3(-27f, 7f, 6f), new Vector3(4f, 14f, 4f), Pal.Stone, rng, 0.4f, 0.52f);
            buildings.SetColor(Pal.RoofTile, 0.35f);
            buildings.Push(new Vector3(-27f, 14f, 6f), Quaternion.identity, Vector3.one);
            buildings.Prism(Vector3.zero, 3.2f, 0f, 5f, 4, false, false, Mathf.PI * 0.25f);
            buildings.Pop();
            Prop(new Vector3(-24.92f, 0f, 6f), -90f, b => TownMeshes.ChapelFront(b, rng));
            layout.ChapelDoor = origin + new Vector3(-24.6f, 0f, 6f);
            layout.ChapelArrival = origin + new Vector3(-22.2f, 0.05f, 6f);
            Box("Chapel", new Vector3(-36f, 4f, 6f), new Vector3(20f, 8f, 11f), Quaternion.identity);
            Box("Chapel Tower", new Vector3(-27f, 7f, 6f), new Vector3(4f, 14f, 4f), Quaternion.identity);

            BuildMarket(layout);

            layout.Entry = origin + new Vector3(0f, 0.05f, -56f);
            layout.EntryYaw = 0f;
            layout.Gate = origin + new Vector3(0f, 0f, -Half + 0.2f);
            layout.GateYaw = 180f;

            layout.People.Add(("merchant", origin + new Vector3(-4.4f, 0f, -11.5f), 120f, b => CreatureMeshes.Figure(b, EnemyFactory.Villager("Merchant", Pal.Robe, Pal.Bone, true))));
        }

        /// <summary>
        /// Gallows Market, laid out after the concept painting as seen from the gate: two rows of stalls with nothing on them
        /// down the road, Iron Mabb against the stone corner on the left, Wick at the notice board beyond, the Widow behind
        /// her swept stall on the right, and at the far end, where the road opens into the square, the gallows.
        /// </summary>
        void BuildMarket(CityLayout layout)
        {
            // The rows. West starts later: the corner and the board stand where its first stalls would.
            for (int i = 0; i < 10; i++)
            {
                float z = -50f + i * StallSpacing;
                Stall(StallRow, z, 90f, i % 3 == 0, i == 0);
                if (i >= 2) Stall(-StallRow, z, -90f, i % 3 == 1, false);
            }

            // The stone corner Iron Mabb leans by, with a lantern on a bracket.
            Vector3 corner = new Vector3(-9.6f, 0f, -52f);
            var size = new Vector3(4.6f, 7f, 7.6f);
            buildings.ShadedBox(corner + Vector3.up * (size.y * 0.5f), size, Pal.Stone, rng, 0.3f, 0.42f);
            for (int i = 0; i < 12; i++)
            {
                float y = 0.31f + i * 0.62f;
                for (float z = -3.8f + (i % 2) * 0.65f; z < 3.8f; z += 1.3f)
                    buildings.ShadedBox(new Vector3(corner.x + 2.33f, y, corner.z + Mathf.Min(z + 0.65f, 3.2f)), new Vector3(0.1f, 0.6f, 1.26f), Pal.Stone, rng, 0.3f, 0.46f);
            }
            Box("Stone Corner", corner + Vector3.up * (size.y * 0.5f), size, Quaternion.identity);
            buildings.SetColor(Pal.SteelDark, 0.28f);
            buildings.Box(new Vector3(-6.7f, 3.5f, -50.2f), new Vector3(0.9f, 0.05f, 0.05f));
            buildings.Prism(new Vector3(-6.3f, 3.16f, -50.2f), 0.17f, 0.06f, 0.16f, 6);
            buildings.Box(new Vector3(-6.3f, 2.68f, -50.2f), new Vector3(0.28f, 0.05f, 0.28f));
            buildings.SetColor(Pal.Lantern, 0.75f);
            buildings.Box(new Vector3(-6.3f, 2.92f, -50.2f), new Vector3(0.2f, 0.42f, 0.2f));

            // Halloway's notices on the street, and Wick reading them to nobody from his upturned crate.
            Vector3 board = new Vector3(-5.9f, 0f, -44.6f);
            Prop(board, -90f, b => TownMeshes.NoticeBoard(b, rng));
            Box("Notice Board", board + Vector3.up * 1.4f, new Vector3(0.3f, 2.8f, 2.1f), Quaternion.identity);
            Vector3 crate = new Vector3(-4.3f, 0f, -44f);
            Prop(crate, 25f, b => PropMeshes.Crate(b, rng, 0.3f));

            // The gallows.
            Vector3 foot = new Vector3(0f, 0f, GallowsZ);
            Prop(foot, 0f, b => TownMeshes.Gallows(b, rng, GallowsHalfWidth, GallowsHalfDepth, GallowsDeck, GallowsSteps, GallowsRise, GallowsRun));
            Box("Gallows Deck", foot + Vector3.up * (GallowsDeck * 0.5f + 0.03f), new Vector3(GallowsHalfWidth * 2f + 0.6f, GallowsDeck + 0.06f, GallowsHalfDepth * 2f + 0.6f), Quaternion.identity);
            for (int i = 0; i < GallowsSteps; i++)
            {
                float top = GallowsDeck - (i + 1) * GallowsRise + 0.06f;
                Box("Gallows Step", foot + new Vector3(0f, top * 0.5f, -GallowsHalfDepth - 0.3f - (i + 0.5f) * GallowsRun), new Vector3(2.5f, top, GallowsRun), Quaternion.identity);
            }
            const float railTop = GallowsDeck + 1.2f;
            Box("Gallows Rail", foot + new Vector3(0f, railTop - 0.6f, GallowsHalfDepth), new Vector3(GallowsHalfWidth * 2f, 1.2f, 0.2f), Quaternion.identity);
            Box("Gallows Rail", foot + new Vector3(-GallowsHalfWidth, railTop - 0.6f, 0f), new Vector3(0.2f, 1.2f, GallowsHalfDepth * 2f), Quaternion.identity);
            Box("Gallows Rail", foot + new Vector3(GallowsHalfWidth, railTop - 0.6f, 0f), new Vector3(0.2f, 1.2f, GallowsHalfDepth * 2f), Quaternion.identity);
            foreach (float side in new[] { -1f, 1f })
            {
                Box("Gallows Rail", foot + new Vector3(side * (GallowsHalfWidth + 1.3f) * 0.5f, railTop - 0.6f, -GallowsHalfDepth), new Vector3(GallowsHalfWidth - 1.3f, 1.2f, 0.2f), Quaternion.identity);
                Box("Gallows Post", foot + new Vector3(side * 2.7f, GallowsDeck + 2.5f, 0.8f), new Vector3(0.4f, 5f, 0.4f), Quaternion.identity);
            }
            layout.Gallows = origin + foot + Vector3.up * 3f;

            // Standing water on the cobbles, as in the painting.
            float[,] puddles = { { -1.8f, -48f, 1.1f }, { 2.4f, -43f, 0.8f }, { -2.6f, -37f, 1.3f }, { 1.2f, -31f, 1f }, { 3f, -24f, 0.9f }, { -1f, -19f, 1.2f } };
            for (int i = 0; i < puddles.GetLength(0); i++)
            {
                float radius = puddles[i, 2];
                Prop(new Vector3(puddles[i, 0], 0f, puddles[i, 1]), R(0f, 180f), b => PropMeshes.Puddle(b, rng, radius, Pal.WetStone, 0.3f));
            }

            layout.People.Add(("gravedigger", origin + new Vector3(-6.45f, 0f, -51.4f), 95f, PeopleMeshes.Gravedigger));
            layout.People.Add(("crier", origin + crate + Vector3.up * 0.3f, 140f, PeopleMeshes.Crier));
            layout.People.Add(("widow", origin + new Vector3(7f, 0f, -49.6f), 272f, PeopleMeshes.Widow));
        }

        /// <summary>One stall, its counter's front towards the road. The collider stops at the counter, so people behind it can be spoken to.</summary>
        void Stall(float x, float z, float yaw, bool lantern, bool swept)
        {
            Prop(new Vector3(x, 0f, z), yaw, b => TownMeshes.MarketStall(b, rng, StallWidth, lantern, swept));
            Box("Stall", new Vector3(x, 0.5f, z), new Vector3(1.6f, 1f, StallWidth + 0.2f), Quaternion.identity);
        }

        Color WindowLightAt(Vector3 at, Vector3 normal)
        {
            Color light = Color.white;
            foreach (Vector3 glow in windowGlows)
            {
                float reach = Mathf.Clamp01(1f - Vector3.Distance(at, glow) / 5.5f);
                light += new Color(1.55f, 0.68f, 0.2f) * (reach * reach);
            }
            foreach (var glow in courtyardGlows)
            {
                float reach = Mathf.Clamp01(1f - Vector3.Distance(at, glow.position) / 6.5f);
                light += new Color(1f, 0.48f, 0.12f) * (reach * reach * glow.strength);
            }
            return light;
        }

        void CourtyardLightPool(Vector3 centre, float radius)
        {
            // Small flags carry the baked gradient; a single six-metre ground quad would miss the pool's centre.
            const float flag = 0.7f;
            for (float x = -radius; x < radius; x += flag)
            for (float z = -radius; z < radius; z += flag)
            {
                if (x * x + z * z > radius * radius) continue;
                Vector3 p = centre + new Vector3(x, 0.028f, z);
                details.SetColor(Pal.Cobble, 0.44f);
                details.QuadOutward(p, p + Vector3.right * flag, p + new Vector3(flag, 0f, flag),
                    p + Vector3.forward * flag, p + Vector3.down);
            }
        }

        static void CourtyardLantern(MeshBuilder b, Vector3 p)
        {
            b.SetColor(Pal.SteelDark, 0.38f);
            b.Prism(p, 0.16f, 0.16f, 0.045f, 6);
            b.Prism(p + Vector3.up * 0.33f, 0.2f, 0.07f, 0.13f, 6);
            foreach (float x in new[] { -0.12f, 0.12f })
            foreach (float z in new[] { -0.12f, 0.12f })
                b.Box(p + new Vector3(x, 0.18f, z), new Vector3(0.025f, 0.3f, 0.025f));
            b.SetColor(Pal.Lantern, 0.43f);
            b.Current.Emission = 0.55f;
            b.Prism(p + Vector3.up * 0.045f, 0.115f, 0.115f, 0.28f, 6);
        }

        void BuildStreetCharacter()
        {
            // A clear processional street, framed above head height rather than filled with obstacles.
            foreach (float z in new[] { -36f, -20f })
                TownDetailMeshes.Pennants(details, new Vector3(-11.4f, 5.3f, z), new Vector3(11.4f, 5.5f, z + 1.2f), Pal.ClothRed, 22);
            TownDetailMeshes.Pennants(details, new Vector3(-8f, 5.4f, 29f), new Vector3(8f, 5.4f, 29f), Pal.PaintBlue, 15);

            // Stone drains and small paving courses give the broad ground plane an intentional rhythm.
            foreach (float side in new[] { -1f, 1f })
            for (float z = -56f; z < -12f; z += 1.1f)
            {
                details.SetColor(Pal.Stone, 0.35f);
                details.Box(new Vector3(side * 3.65f, 0.025f, z), new Vector3(0.2f, 0.05f, 0.96f));
            }
            TownDetailMeshes.Ring(details, new Vector3(0f, 0.025f, 16f), 4.7f, 0.12f, Pal.Stone, 0.58f, 32);
            TownDetailMeshes.Ring(details, new Vector3(0f, 0.028f, 16f), 4.3f, 0.055f, Pal.Brass, 0.32f, 32);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                Vector3 direction = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                Vector3 across = new Vector3(direction.z, 0f, -direction.x);
                Vector3 centre = new Vector3(0f, 0.032f, 16f);
                details.SetColor(i % 2 == 0 ? Pal.Stone : Pal.SteelDark, i % 2 == 0 ? 0.58f : 0.35f);
                details.Triangle(centre + direction * 4f, centre + direction * 1.2f + across * 0.34f,
                    centre + direction * 1.2f - across * 0.34f, Vector3.up);
            }

            foreach (float side in new[] { -1f, 1f })
            foreach (float z in new[] { -43f, -12f, 25f, 47f })
            {
                Vector3 p = new Vector3(side * 62.5f, 0f, z);
                details.ShadedBox(p + Vector3.up * 3.1f, new Vector3(2.3f, 6.2f, 2f), Pal.StoneMoss, rng, 0.33f, 0.45f);
                details.ShadedBox(p + Vector3.up * 6.3f, new Vector3(2.65f, 0.3f, 2.3f), Pal.Stone, rng, 0.4f, 0.52f);
            }
            foreach (float x in new[] { -Half, Half })
            foreach (float z in new[] { -Half, Half })
            {
                details.SetColor(Pal.Stone, 0.42f);
                details.Prism(new Vector3(x, 0f, z), 3.2f, 2.8f, 11.3f, 8);
                details.SetColor(Pal.RoofTile, 0.31f);
                details.Prism(new Vector3(x, 11.3f, z), 3.35f, 0f, 4.2f, 8);
            }
            foreach (Vector3 p in new[] { new Vector3(-11f, 0f, 27f), new Vector3(11f, 0f, 27f), new Vector3(22f, 0f, 11.5f) })
                TownDetailMeshes.Planter(details, p, rng);

            // The window-lit thresholds use small flags, so the warmth has vertices to land on.
            foreach (Vector3 glow in windowGlows)
            for (int x = -1; x <= 1; x++)
            for (int z = -1; z <= 1; z++)
            {
                details.SetColor(Pal.Cobble, 0.45f);
                details.Box(new Vector3(glow.x + x * 0.7f, 0.012f, glow.z + z * 0.7f), new Vector3(0.66f, 0.018f, 0.66f));
            }
        }

        void BuildCourtyards()
        {
            var well = new MeshBuilder();
            Vector3 centre = new Vector3(-15.5f, 0f, 19f);
            well.Push(centre, Quaternion.identity, Vector3.one);
            TownDetailMeshes.Ring(well, new Vector3(0f, 0.025f, 0f), 4.3f, 0.14f, Pal.Stone, 0.5f, 24);
            well.SetColor(Pal.Black, 0.16f);
            well.Prism(new Vector3(0f, 0.25f, 0f), 1.55f, 1.55f, 0.04f, 16);
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 0.125f;
                well.Push(new Vector3(Mathf.Cos(a) * 1.65f, 0.5f, Mathf.Sin(a) * 1.65f), Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f), Vector3.one);
                well.ShadedBox(Vector3.zero, new Vector3(0.6f, 1f, 0.64f), Pal.StoneMoss, rng, 0.38f, 0.58f);
                well.Pop();
            }
            TownDetailMeshes.Ring(well, new Vector3(0f, 1.04f, 0f), 1.66f, 0.25f, Pal.Stone, 0.56f);
            foreach (float side in new[] { -1f, 1f })
            {
                TownDetailMeshes.Beam(well, new Vector3(side * 1.95f, 0f, 0f), new Vector3(side * 1.95f, 3.65f, 0f), 0.2f);
                well.Push(new Vector3(0f, 3.8f, side * 0.72f), Quaternion.Euler(side * 24f, 0f, 0f), Vector3.one);
                well.SetColor(Pal.RoofTile, 0.42f);
                well.Box(Vector3.zero, new Vector3(4.65f, 0.15f, 1.7f));
                well.Pop();
            }
            TownDetailMeshes.Beam(well, new Vector3(-2.1f, 3.32f, 0f), new Vector3(2.1f, 3.32f, 0f), 0.18f);
            TownDetailMeshes.Beam(well, new Vector3(0f, 3.28f, 0f), new Vector3(0f, 0.5f, 0f), 0.035f, Pal.Thatch, 0.5f);
            well.SetColor(Pal.Wood, 0.38f);
            well.Prism(new Vector3(1.2f, 1.16f, -0.65f), 0.22f, 0.28f, 0.4f, 8);
            CourtyardLantern(well, new Vector3(0f, 2.65f, -0.78f));
            TownDetailMeshes.Beam(well, new Vector3(0f, 3.11f, -0.78f), new Vector3(0f, 3.42f, -0.78f), 0.035f, Pal.SteelDark, 0.35f);
            courtyardGlows.Add((centre + new Vector3(0f, 2.83f, -0.78f), 16f));
            foreach (float side in new[] { -1f, 1f })
            {
                well.ShadedBox(new Vector3(side * 3.6f, 0.42f, 0f), new Vector3(0.65f, 0.16f, 2.3f), Pal.Stone, rng, 0.42f, 0.54f);
                foreach (float z in new[] { -0.85f, 0.85f })
                    well.ShadedBox(new Vector3(side * 3.6f, 0.2f, z), new Vector3(0.5f, 0.4f, 0.3f), Pal.Stone, rng, 0.3f, 0.4f);
            }
            well.Pop();
            well.Bake(WindowLightAt);
            Save(well, "City_Well_of_Names", ShadowCastingMode.On);
            CourtyardLightPool(centre, 6.2f);
            Box("Well of Names", centre + Vector3.up * 0.65f, new Vector3(4.3f, 1.3f, 4.3f), Quaternion.identity);
            WorldVista.Inspect(root, "Well of Names Inscription", origin + centre + new Vector3(0f, 1.05f, -2.2f),
                new Vector3(1.75f, 1.15f, 0.18f), GameText.WellLore);

            var court = new MeshBuilder();
            Vector3 tree = new Vector3(15.5f, 0f, 19f);
            court.Push(tree, Quaternion.identity, Vector3.one);
            TownDetailMeshes.Ring(court, new Vector3(0f, 0.32f, 0f), 2.2f, 0.48f, Pal.StoneMoss, 0.47f);
            court.SetColor(Pal.DirtDark, 0.3f);
            court.Prism(Vector3.zero, 2f, 2f, 0.16f, 16);
            court.SetColor(Pal.Bark, 0.46f);
            court.Prism(Vector3.zero, 0.38f, 0.16f, 5.5f, 7);
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2f / 7f;
                Vector3 tip = new Vector3(Mathf.Cos(a) * 1.9f, 4.6f + (i % 3) * 0.45f, Mathf.Sin(a) * 1.9f);
                TownDetailMeshes.Beam(court, new Vector3(0f, 2.5f + (i % 2) * 0.4f, 0f), tip, 0.13f, Pal.Bark, 0.4f);
                court.Rock(tip, new Vector3(1.75f, 1.35f, 1.75f), rng, i % 2 == 0 ? Pal.LeafGold : Pal.LeafOrange, 0.35f, 0.57f, 0.22f);
                court.SetColor(Pal.Thatch, 0.4f);
                court.Box(tip + new Vector3(0f, -0.6f, 0f), new Vector3(0.025f, 1.1f, 0.025f));
                CourtyardLantern(court, tip + Vector3.down * 1.3f);
                courtyardGlows.Add((tree + tip + Vector3.down * 1.12f, 4f));
            }
            for (int i = 0; i < 35; i++)
            {
                court.SetColor(i % 2 == 0 ? Pal.LeafGold : Pal.LeafOrange, 0.4f);
                court.Box(new Vector3(R(-2.8f, 2.8f), 0.014f, R(-2.8f, 2.8f)), new Vector3(0.12f, 0.012f, 0.09f));
            }
            court.Pop();
            court.Bake(WindowLightAt);
            Save(court, "City_Lantern_Court", ShadowCastingMode.On);
            CourtyardLightPool(tree, 6.2f);
            Box("Lantern Court Tree", tree + Vector3.up * 0.5f, new Vector3(4.8f, 1f, 4.8f), Quaternion.identity);
        }

        void BuildMountains()
        {
            // The hill behind the gallows, and the keep on it: what the road points at, seen over the tavern's roof.
            const float hillZ = 205f, hillHeight = 56f;
            foliageFree.Push(new Vector3(0f, -2f, hillZ), Quaternion.identity, Vector3.one);
            PropMeshes.Mountain(foliageFree, new System.Random(Seedless), 70f, hillHeight);
            foliageFree.Pop();
            foliageFree.Push(new Vector3(0f, hillHeight * 0.84f, hillZ - 8f), Quaternion.identity, Vector3.one);
            TownMeshes.Keep(foliageFree, rng);
            foliageFree.Pop();

            for (int i = 0; i < 14; i++)
            {
                float a = i * Mathf.PI * 2f / 14f + 0.2f;
                if (Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 90f)) < 22f) continue; // that side is the keep's
                float distance = R(190f, 240f);
                foliageFree.Push(new Vector3(Mathf.Cos(a) * distance, -2f, Mathf.Sin(a) * distance), Quaternion.Euler(0f, R(0f, 360f), 0f), Vector3.one);
                PropMeshes.Mountain(foliageFree, rng, R(50f, 80f), R(45f, 85f));
                foliageFree.Pop();
            }
        }
    }
}
