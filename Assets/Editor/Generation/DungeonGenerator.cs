using System.Collections.Generic;
using System.Linq;
using AshenRealm.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace AshenRealm.EditorTools
{
    internal enum DungeonDecor { Catacombs, Fungal, Harbor, Ember, Marrow }

    /// <summary>The look and the inhabitants of one depth, from Graveborne's BIOMES table.</summary>
    internal sealed class BiomeStyle
    {
        public string Name;
        public string Title;
        public string Subtitle;
        public DungeonDecor Decor;
        public Pal Wall, WallBand, Floor, FloorPatch, Ceiling;
        public Color Ambient, Fog;
        public float FogStart, FogEnd;
        public string[] Enemies;
        public string Elite;
        /// <summary>The keeper of the stair (the Gloamlord on the last depth, who has no stair).</summary>
        public string Keeper;
        public string[] Encounters;
    }

    /// <summary>Everything the scene builder needs to populate a generated depth.</summary>
    internal sealed class DungeonLayout
    {
        public Transform Root;
        public Vector3 Entry;
        public float EntryYaw;
        public Vector3 StairUp;
        public float StairUpYaw;
        public Vector3 StairDown;
        public float StairDownYaw;
        public Vector3 StairDownArrival;
        public Vector3 Keeper;
        public float KeeperYaw;
        public Vector3 Merchant;
        public readonly List<(string id, Vector3 position, Vector3[] patrol)> Spawns = new List<(string, Vector3, Vector3[])>();
        public readonly List<Vector3> Chests = new List<Vector3>();
        public readonly List<(string id, Vector3 position)> Encounters = new List<(string, Vector3)>();
        public int WalkableCells;
        public int ConnectedCells;
    }

    /// <summary>
    /// Rooms and corridors in the manner of Graveborne's dungeon.js, raised into 3D: a tile grid, an entry room in one
    /// corner, the keeper's hall in the far corner, and everything else strung between them. Walls come from the grid,
    /// so they always close; decoration never collides (Graveborne's rule: scenery never blocks a route), which also keeps
    /// the enemies' simple steering reliable with no navmesh.
    /// </summary>
    internal sealed class DungeonGenerator
    {
        const float Cell = 2.5f;
        const float WallHeight = 5.5f;
        const int Width = 54, Height = 42;
        const int RoomAttempts = 90;
        const int MaxRooms = 13;
        const int ChunkGrid = 4;
        const float RoofHeight = 10.5f;

        struct Room
        {
            public RectInt Rect;
            public Vector2Int Center => new Vector2Int(Rect.x + Rect.width / 2, Rect.y + Rect.height / 2);
        }

        readonly BiomeStyle style;
        readonly Material material;
        readonly Vector3 origin;
        readonly System.Random rng;
        /// <summary>For the finer cut of walls and floors only, so the layout's own random sequence stays as it was.</summary>
        readonly System.Random detailRng;
        readonly bool[,] floor = new bool[Width, Height];
        readonly List<Room> rooms = new List<Room>();
        readonly MeshBuilder[,] chunks = new MeshBuilder[ChunkGrid, ChunkGrid];
        Transform root, colliderRoot;
        readonly List<Vector3> featureClearance = new List<Vector3>();

        public DungeonGenerator(BiomeStyle style, Material material, Vector3 origin, int seed)
        {
            this.style = style;
            this.material = material;
            this.origin = origin;
            rng = new System.Random(seed);
            detailRng = new System.Random(seed ^ 0x5EED);
        }

        float R(float min, float max) => MeshBuilder.Lerp(rng, min, max);
        int RI(int min, int max) => rng.Next(min, max);

        public DungeonLayout Generate(Transform parent, bool lastDepth)
        {
            root = new GameObject("Environment").transform;
            root.SetParent(parent, false);
            colliderRoot = new GameObject("Colliders").transform;
            colliderRoot.SetParent(root, false);
            for (int x = 0; x < ChunkGrid; x++)
            for (int z = 0; z < ChunkGrid; z++)
                chunks[x, z] = new MeshBuilder();

            PlaceRooms();
            ConnectRooms();
            BuildShell();
            BuildColliders();

            var layout = new DungeonLayout { Root = root };
            PlaceFeatures(layout, lastDepth);
            ValidateConnectivity(layout);
            BuildArchitecture(layout);
            Dress();
            LightTheHalls(layout);
            BakeLight();
            Flush();
            return layout;
        }

        // ---- Layout ----------------------------------------------------------------------------------

        void PlaceRooms()
        {
            // The entry and the keeper's hall sit in opposite corners, so every depth is a real walk.
            rooms.Add(new Room { Rect = new RectInt(3, 3, 9, 8) });
            rooms.Add(new Room { Rect = new RectInt(Width - 20, Height - 18, 17, 15) });

            // A memorable heart and useful side galleries are guaranteed; random rooms fill the remaining gaps.
            RectInt heart;
            switch (style.Decor)
            {
                case DungeonDecor.Fungal: heart = new RectInt(15, 13, 17, 12); break;
                case DungeonDecor.Harbor: heart = new RectInt(16, 16, 17, 8); break;
                case DungeonDecor.Ember: heart = new RectInt(17, 12, 14, 13); break;
                case DungeonDecor.Marrow: heart = new RectInt(17, 14, 15, 11); break;
                default: heart = new RectInt(19, 13, 12, 12); break;
            }
            rooms.Add(new Room { Rect = heart });
            foreach (RectInt rect in new[]
            {
                new RectInt(3, 25, 10, 11), new RectInt(36, 4, 13, 10),
                new RectInt(3, 14, 9, 8), new RectInt(18, 29, 11, 9), new RectInt(17, 3, 13, 7),
            })
                rooms.Add(new Room { Rect = rect });

            for (int i = 0; i < RoomAttempts && rooms.Count < MaxRooms; i++)
            {
                var rect = new RectInt(RI(2, Width - 14), RI(2, Height - 12), RI(7, 13), RI(6, 11));
                var padded = new RectInt(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4);
                if (rooms.Any(r => r.Rect.Overlaps(padded))) continue;
                rooms.Add(new Room { Rect = rect });
            }

            foreach (var room in rooms)
                for (int x = room.Rect.xMin; x < room.Rect.xMax; x++)
                for (int z = room.Rect.yMin; z < room.Rect.yMax; z++)
                    floor[x, z] = true;
        }

        /// <summary>Prim's tree over room centres, plus two extra links so a depth has loops to flee around.</summary>
        void ConnectRooms()
        {
            var linked = new List<int> { 0 };
            var waiting = Enumerable.Range(1, rooms.Count - 1).ToList();
            while (waiting.Count > 0)
            {
                int bestFrom = 0, bestTo = 0;
                float best = float.MaxValue;
                foreach (int from in linked)
                foreach (int to in waiting)
                {
                    float d = Vector2Int.Distance(rooms[from].Center, rooms[to].Center);
                    if (d < best)
                    {
                        best = d;
                        bestFrom = from;
                        bestTo = to;
                    }
                }
                Carve(rooms[bestFrom].Center, rooms[bestTo].Center);
                linked.Add(bestTo);
                waiting.Remove(bestTo);
            }

            // The drowned galleries and ossuary have a processional loop; the caves have broader interconnecting runs.
            Carve(rooms[0].Center, rooms[2].Center);
            Carve(rooms[2].Center, rooms[1].Center);
            Carve(rooms[3].Center, rooms[4].Center);
        }

        void Carve(Vector2Int a, Vector2Int b)
        {
            int x = a.x, z = a.y;
            bool horizontalFirst = rng.NextDouble() < 0.5;
            void Open(int cx, int cz)
            {
                int start = style.Decor == DungeonDecor.Fungal || style.Decor == DungeonDecor.Marrow ? -1 : 0;
                for (int ox = start; ox < 2; ox++)
                for (int oz = start; oz < 2; oz++)
                {
                    int px = Mathf.Clamp(cx + ox, 1, Width - 2), pz = Mathf.Clamp(cz + oz, 1, Height - 2);
                    floor[px, pz] = true;
                }
            }
            void WalkX() { while (x != b.x) { x += x < b.x ? 1 : -1; Open(x, z); } }
            void WalkZ() { while (z != b.y) { z += z < b.y ? 1 : -1; Open(x, z); } }
            Open(x, z);
            if (horizontalFirst) { WalkX(); WalkZ(); }
            else { WalkZ(); WalkX(); }
        }

        bool IsFloor(int x, int z) => x >= 0 && z >= 0 && x < Width && z < Height && floor[x, z];

        Vector3 World(float x, float z) => origin + new Vector3(x * Cell, 0f, z * Cell);
        Vector3 Local(float x, float z) => new Vector3(x * Cell, 0f, z * Cell);

        float CeilingAt(int x, int z)
        {
            for (int i = 0; i < rooms.Count; i++)
                if (rooms[i].Rect.Contains(new Vector2Int(x, z)))
                    return RoomCeiling(i);
            return WallHeight;
        }

        float RoomCeiling(int index) => index == 1 ? (style.Decor == DungeonDecor.Marrow ? 9.5f : 8.4f) :
            index == 2 ? (style.Decor == DungeonDecor.Ember ? 9f : 8f) : index == 0 ? 6.6f : 6.8f;

        MeshBuilder ChunkAt(Vector3 localPosition)
        {
            int cx = Mathf.Clamp(Mathf.FloorToInt(localPosition.x / (Width * Cell) * ChunkGrid), 0, ChunkGrid - 1);
            int cz = Mathf.Clamp(Mathf.FloorToInt(localPosition.z / (Height * Cell) * ChunkGrid), 0, ChunkGrid - 1);
            return chunks[cx, cz];
        }

        // ---- Shell -----------------------------------------------------------------------------------

        void BuildShell()
        {
            for (int x = 0; x < Width; x++)
            for (int z = 0; z < Height; z++)
            {
                if (!floor[x, z]) continue;
                Vector3 a = Local(x, z), c = Local(x + 1, z + 1);
                var chunk = ChunkAt(a);
                float ceiling = CeilingAt(x, z);

                bool patch = Mathf.PerlinNoise(x * 0.31f + 3.7f, z * 0.31f + 8.1f) > 0.56f;
                chunk.SetColor(patch ? style.FloorPatch : style.Floor, R(0.44f, 0.6f));

                // Four pieces a tile: the baked light needs the corners to land in a pool, not a whole tile away.
                float half = Cell * 0.5f;
                for (int qx = 0; qx < 2; qx++)
                for (int qz = 0; qz < 2; qz++)
                {
                    float x0 = a.x + qx * half, z0 = a.z + qz * half;
                    chunk.QuadOutward(new Vector3(x0, 0f, z0), new Vector3(x0 + half, 0f, z0), new Vector3(x0 + half, 0f, z0 + half), new Vector3(x0, 0f, z0 + half),
                        new Vector3(x0 + half * 0.5f, -1f, z0 + half * 0.5f));
                }

                chunk.SetColor(style.Ceiling, R(0.3f, 0.42f));
                chunk.QuadOutward(new Vector3(a.x, ceiling, a.z), new Vector3(c.x, ceiling, a.z), new Vector3(c.x, ceiling, c.z), new Vector3(a.x, ceiling, c.z), new Vector3((a.x + c.x) * 0.5f, ceiling + 1f, (a.z + c.z) * 0.5f));

                Edge(chunk, x - 1, z, new Vector3(a.x, 0f, a.z), new Vector3(a.x, 0f, c.z), Vector3.right, x + z, ceiling);
                Edge(chunk, x + 1, z, new Vector3(c.x, 0f, a.z), new Vector3(c.x, 0f, c.z), Vector3.left, x + z, ceiling);
                Edge(chunk, x, z - 1, new Vector3(a.x, 0f, a.z), new Vector3(c.x, 0f, a.z), Vector3.forward, x + z, ceiling);
                Edge(chunk, x, z + 1, new Vector3(a.x, 0f, c.z), new Vector3(c.x, 0f, c.z), Vector3.back, x + z, ceiling);
            }
        }

        void Edge(MeshBuilder b, int nextX, int nextZ, Vector3 p, Vector3 q, Vector3 inward, int index, float height)
        {
            if (!IsFloor(nextX, nextZ)) { Wall(b, p, q, inward, index, height); return; }
            float next = CeilingAt(nextX, nextZ);
            if (height <= next + 0.01f) return;
            b.SetColor(style.WallBand, 0.32f);
            b.QuadOutward(p + Vector3.up * next, q + Vector3.up * next, q + Vector3.up * height, p + Vector3.up * height,
                (p + q) * 0.5f - inward + Vector3.up * ((height + next) * 0.5f));
        }

        void Wall(MeshBuilder b, Vector3 p, Vector3 q, Vector3 inward, int index, float height)
        {
            const float bandHeight = 0.7f;
            Vector3 up = Vector3.up;

            // Cut into courses of under two metres, each with its own shade: the stone reads as courses, and the baked light
            // has corners to fall on.
            const int columns = 1;          // more pieces sharpen the light but weigh on a phone's download
            float bandShade = R(0.36f, 0.46f), wallShade = R(0.44f, 0.62f);
            int rows = Mathf.Max(1, Mathf.RoundToInt((height - bandHeight) / 1.8f));
            for (int column = 0; column < columns; column++)
            {
                Vector3 p0 = Vector3.Lerp(p, q, column / (float)columns), p1 = Vector3.Lerp(p, q, (column + 1f) / columns);
                Vector3 outside = (p0 + p1) * 0.5f - inward;
                b.SetColor(style.WallBand, bandShade);
                b.QuadOutward(p0, p1, p1 + up * bandHeight, p0 + up * bandHeight, outside + up * 0.3f);
                for (int row = 0; row < rows; row++)
                {
                    float y0 = Mathf.Lerp(bandHeight, height, row / (float)rows), y1 = Mathf.Lerp(bandHeight, height, (row + 1f) / rows);
                    b.SetColor(style.Wall, Mathf.Clamp(wallShade + MeshBuilder.Lerp(detailRng, -0.06f, 0.06f), 0.3f, 0.7f));
                    b.QuadOutward(p0 + up * y0, p1 + up * y0, p1 + up * y1, p0 + up * y1, outside + up * ((y0 + y1) * 0.5f));
                }
            }

            // A buttress every few tiles breaks up the long walls.
            if (index % 4 != 0) return;
            Vector3 centre = (p + q) * 0.5f + inward * 0.22f + up * (height * 0.5f);
            Vector3 size = new Vector3(Mathf.Abs(inward.x) > 0.5f ? 0.44f : 0.8f, height, Mathf.Abs(inward.x) > 0.5f ? 0.8f : 0.44f);
            b.ShadedBox(centre, size, style.WallBand, rng, 0.38f, 0.5f);
        }

        void BuildColliders()
        {
            var ground = new GameObject("Floor");
            ground.transform.SetParent(colliderRoot, false);
            ground.transform.position = origin + new Vector3(Width * Cell * 0.5f, -0.5f, Height * Cell * 0.5f);
            ground.AddComponent<BoxCollider>().size = new Vector3(Width * Cell, 1f, Height * Cell);

            var roof = new GameObject("Roof");
            roof.transform.SetParent(colliderRoot, false);
            roof.transform.position = origin + new Vector3(Width * Cell * 0.5f, RoofHeight + 0.5f, Height * Cell * 0.5f);
            roof.AddComponent<BoxCollider>().size = new Vector3(Width * Cell, 1f, Height * Cell);

            // Runs of solid tiles merge into one box per row, which keeps a depth to a few hundred colliders.
            for (int z = 0; z < Height; z++)
            {
                int x = 0;
                while (x < Width)
                {
                    if (floor[x, z]) { x++; continue; }
                    int start = x;
                    while (x < Width && !floor[x, z]) x++;
                    var wall = new GameObject("Wall");
                    wall.transform.SetParent(colliderRoot, false);
                    float length = (x - start) * Cell;
                    wall.transform.position = origin + new Vector3((start + (x - start) * 0.5f) * Cell, RoofHeight * 0.5f, (z + 0.5f) * Cell);
                    wall.AddComponent<BoxCollider>().size = new Vector3(length, RoofHeight, Cell);
                }
            }
        }

        // ---- Features --------------------------------------------------------------------------------

        Vector3 InRoom(Room room, float margin = 1.6f)
        {
            RectInt r = room.Rect;
            float left = Mathf.Max(r.xMin + margin, room.Center.x - r.width * 0.23f);
            float right = Mathf.Min(r.xMax - margin, room.Center.x + r.width * 0.23f);
            return World(R(left, right), R(r.yMin + margin, r.yMax - margin - 0.7f));
        }

        void PlaceFeatures(DungeonLayout layout, bool lastDepth)
        {
            Room entry = rooms[0], hall = rooms[1];

            // Stair up against the entry room's south wall; arrivals face into the room.
            Vector2Int e = entry.Center;
            layout.StairUp = World(e.x, entry.Rect.yMin);
            layout.StairUpYaw = 180f;
            layout.Entry = World(e.x, entry.Rect.yMin + 2.2f);
            layout.EntryYaw = 0f;
            layout.Merchant = World(entry.Rect.xMin + 1.6f, entry.Rect.yMin + 2f);
            PlaceArch(layout.StairUp, 180f, false);

            // Keeper's hall: the keeper waits in the middle with the stair behind it.
            Vector2Int h = hall.Center;
            layout.Keeper = World(h.x, h.y);
            layout.KeeperYaw = 200f;
            layout.StairDown = World(h.x, hall.Rect.yMax);
            layout.StairDownYaw = 0f;
            layout.StairDownArrival = World(h.x, hall.Rect.yMax - 2.2f);
            if (lastDepth)
            {
                var b = ChunkAt(layout.StairDown - origin);
                b.Push(layout.StairDown - origin + new Vector3(0f, 0f, -3.2f), Quaternion.identity, Vector3.one);
                PropMeshes.BoneThrone(b, rng);
                b.Pop();
            }
            else
            {
                PlaceArch(layout.StairDown, 0f, true);
            }

            // The rooms between: an encounter or a pack, a chest here and there, one named terror.
            var middle = Enumerable.Range(2, rooms.Count - 2).OrderBy(_ => rng.Next()).ToList();
            int encounter = 0, elitesLeft = string.IsNullOrEmpty(style.Elite) ? 0 : 1, chests = 3, enemyCursor = 0;
            foreach (int index in middle)
            {
                Room room = rooms[index];
                if (encounter < style.Encounters.Length)
                {
                    layout.Encounters.Add((style.Encounters[encounter++], World(room.Center.x, room.Center.y)));
                    continue;
                }

                string[] pool = style.Enemies;
                int count = RI(2, 4);
                for (int i = 0; i < count; i++)
                    layout.Spawns.Add((pool[enemyCursor < pool.Length ? enemyCursor++ : RI(0, pool.Length)], InRoom(room), Patrol(room)));
                if (elitesLeft > 0)
                {
                    elitesLeft--;
                    layout.Spawns.Add((style.Elite, World(room.Center.x, room.Center.y), Patrol(room)));
                }
                if (chests > 0)
                {
                    chests--;
                    layout.Chests.Add(World(room.Rect.xMin + 1.2f, room.Rect.yMax - 1.4f));
                }
            }

            // Corridors between packs stay clear; two sentries watch the way into the hall.
            for (int i = 0; i < 2; i++)
                layout.Spawns.Add((style.Enemies[i % style.Enemies.Length], World(hall.Rect.xMin + 2f + i * 3f, hall.Rect.yMin + 2f), Patrol(hall)));
        }

        Vector3[] Patrol(Room room) => new[] { InRoom(room), InRoom(room), InRoom(room) };

        /// <summary>Generation fails before saving if any traversable tile or gameplay destination is cut off.</summary>
        void ValidateConnectivity(DungeonLayout layout)
        {
            var reached = new bool[Width, Height];
            var queue = new Queue<Vector2Int>();
            Vector2Int CellAt(Vector3 position) => new Vector2Int(Mathf.FloorToInt((position.x - origin.x) / Cell), Mathf.FloorToInt((position.z - origin.z) / Cell));
            Vector2Int start = CellAt(layout.Entry);
            if (!IsFloor(start.x, start.y)) throw new System.InvalidOperationException(style.Name + " entry is outside its floor.");
            queue.Enqueue(start);
            reached[start.x, start.y] = true;
            Vector2Int[] directions = { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
            while (queue.Count > 0)
            {
                Vector2Int at = queue.Dequeue();
                layout.ConnectedCells++;
                foreach (Vector2Int direction in directions)
                {
                    Vector2Int next = at + direction;
                    if (!IsFloor(next.x, next.y) || reached[next.x, next.y]) continue;
                    reached[next.x, next.y] = true;
                    queue.Enqueue(next);
                }
            }
            for (int x = 0; x < Width; x++)
            for (int z = 0; z < Height; z++)
            {
                if (!floor[x, z]) continue;
                layout.WalkableCells++;
                if (!reached[x, z]) throw new System.InvalidOperationException($"{style.Name} has an isolated floor at {x},{z}.");
            }
            var destinations = new List<Vector3> { layout.Entry, layout.Merchant, layout.Keeper, layout.StairDownArrival,
                layout.StairUp + Vector3.forward * 1.2f, layout.StairDown - Vector3.forward * 1.2f };
            destinations.AddRange(layout.Chests);
            destinations.AddRange(layout.Encounters.Select(e => e.position));
            foreach (var spawn in layout.Spawns) { destinations.Add(spawn.position); destinations.AddRange(spawn.patrol); }
            foreach (Vector3 position in destinations)
            {
                Vector2Int cell = CellAt(position);
                if (!IsFloor(cell.x, cell.y) || !reached[cell.x, cell.y])
                    throw new System.InvalidOperationException($"{style.Name} has an unreachable gameplay destination at {position}.");
                featureClearance.Add(position - origin);
            }
            Debug.Log($"[AshenRealm] {style.Name}: {layout.ConnectedCells}/{layout.WalkableCells} connected floor cells, {destinations.Count} reachable gameplay points, {rooms.Count} rooms.");
        }

        void BuildArchitecture(DungeonLayout layout)
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                RectInt rect = rooms[i].Rect;
                Vector3 min = Local(rect.xMin, rect.yMin), max = Local(rect.xMax, rect.yMax);
                DungeonArchitecture.BuildRoom(ChunkAt((min + max) * 0.5f), style.Decor, min, max, RoomCeiling(i), i, rng,
                    p => !IsFloor(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.z / Cell)));
            }

            Room heart = rooms[2], hall = rooms[1];
            Vector3 heartMin = Local(heart.Rect.xMin, heart.Rect.yMin), heartMax = Local(heart.Rect.xMax, heart.Rect.yMax);
            Vector3 focus = DungeonArchitecture.Focus(style.Decor, heartMin, heartMax);
            Vector3 hallMin = Local(hall.Rect.xMin, hall.Rect.yMin), hallMax = Local(hall.Rect.xMax, hall.Rect.yMax);
            Vector3 hallFocus = DungeonArchitecture.Focus(style.Decor, hallMin, hallMax, true);
            WorldVista.Add(root, style.Name + " Landmark", origin + new Vector3((heartMin.x + heartMax.x) * 0.5f + 1.8f, 1.7f, heartMin.z + 3.2f), origin + focus);
            WorldVista.Add(root, style.Name + " Keeper Hall", origin + new Vector3((hallMin.x + hallMax.x) * 0.5f + 3f, 1.8f, hallMin.z + 3.5f),
                origin + new Vector3((hallMin.x + hallMax.x) * 0.5f, 2.8f, hallMax.z - 2.5f));
            for (int i = 3; i <= 4; i++)
            {
                RectInt rect = rooms[i].Rect;
                Vector3 min = Local(rect.xMin, rect.yMin), max = Local(rect.xMax, rect.yMax);
                WorldVista.Add(root, style.Name + (i == 3 ? " Lost Lives" : " Side Gallery"),
                    origin + new Vector3(min.x + 8f, 1.7f, min.z + 3f), origin + new Vector3(min.x + 5f, 1.4f, max.z - 3.8f));
            }

            Vector3 inscription = focus + new Vector3(-2.4f, -focus.y, -3.2f);
            DungeonArchitecture.Inscription(ChunkAt(inscription), inscription, style.Decor);
            WorldVista.Inspect(root, style.Name + " Memory", origin + inscription + Vector3.up * 0.9f,
                new Vector3(0.9f, 1.5f, 0.45f), GameText.DepthLandmarkLore[(int)style.Decor]);

            Accent("Entry Beacon", Local(rooms[0].Center.x - 2.3f, rooms[0].Rect.yMin + 2.5f) + Vector3.up * 2.6f);
            Accent("Landmark Glow", focus + new Vector3(0f, 0.2f, -1.2f));
            Accent("Keeper Vigil", hallFocus + new Vector3(0f, 0.2f, -1.2f));
        }

        void Accent(string name, Vector3 local)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = origin + local;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = DungeonArchitecture.GlowColor(style.Decor);
            light.intensity = 3.2f;
            light.range = 12f;
            light.shadows = LightShadows.None;
        }

        void PlaceArch(Vector3 position, float yaw, bool leadsDown)
        {
            var b = ChunkAt(position - origin);
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            b.Push(position - origin + rotation * new Vector3(0f, 0f, -0.6f), rotation, Vector3.one); // stands proud of the wall
            PropMeshes.StairArch(b, rng, style.WallBand, leadsDown);
            b.Pop();
        }

        // ---- Dressing --------------------------------------------------------------------------------

        void Dress()
        {
            foreach (var room in rooms)
            {
                RectInt r = room.Rect;
                int props = r.width * r.height / 6;
                for (int i = 0; i < props; i++)
                {
                    bool againstWall = rng.NextDouble() < 0.7;
                    float x = againstWall ? (rng.NextDouble() < 0.5 ? r.xMin + 0.5f : r.xMax - 0.5f) : R(r.xMin + 1f, r.xMax - 1f);
                    float z = R(r.yMin + 0.6f, r.yMax - 0.6f);
                    if (rng.NextDouble() < 0.5)
                    {
                        z = againstWall ? (rng.NextDouble() < 0.5 ? r.yMin + 0.5f : r.yMax - 0.5f) : z;
                        x = R(r.xMin + 0.6f, r.xMax - 0.6f);
                    }
                    Vector3 point = Local(x, z);
                    if (Mathf.Abs(x - room.Center.x) < 1.1f || featureClearance.Any(p => (new Vector2(p.x - point.x, p.z - point.z)).sqrMagnitude < 5.76f)) continue;
                    Vector3 landmark = DungeonArchitecture.Focus(style.Decor, Local(r.xMin, r.yMin), Local(r.xMax, r.yMax), room.Rect.Equals(rooms[1].Rect));
                    if ((new Vector2(landmark.x - point.x, landmark.z - point.z)).sqrMagnitude < 20f) continue;
                    PlaceProp(point);
                }
            }
        }

        void PlaceProp(Vector3 p)
        {
            var b = ChunkAt(p);
            b.Push(p, Quaternion.Euler(0f, R(0f, 360f), 0f), Vector3.one);
            float roll = R(0f, 1f);
            switch (style.Decor)
            {
                case DungeonDecor.Catacombs:
                    if (roll < 0.28f) PropMeshes.Sarcophagus(b, rng);
                    else if (roll < 0.55f) PropMeshes.Candles(b, rng);
                    else if (roll < 0.78f) PropMeshes.Urn(b, rng);
                    else PropMeshes.BonePile(b, rng);
                    break;
                case DungeonDecor.Fungal:
                    if (roll < 0.62f) PropMeshes.Mushroom(b, rng, Vector3.zero, R(0.8f, 2.4f));
                    else if (roll < 0.82f) b.Rock(new Vector3(0f, 0.25f, 0f), new Vector3(R(0.7f, 1.5f), R(0.5f, 0.9f), R(0.7f, 1.4f)), rng, Pal.FungalRock, 0.3f, 0.5f);
                    else PropMeshes.Stalagmite(b, rng, Pal.FungalRock, R(0.8f, 2.2f));
                    break;
                case DungeonDecor.Harbor:
                    if (roll < 0.34f) PropMeshes.Barrel(b, rng);
                    else if (roll < 0.6f) PropMeshes.Crate(b, rng, R(0.7f, 1.1f));
                    else if (roll < 0.84f) PropMeshes.RottenPost(b, rng);
                    else PropMeshes.Puddle(b, rng, R(0.7f, 1.6f));
                    break;
                case DungeonDecor.Ember:
                    if (roll < 0.3f) PropMeshes.Brazier(b, rng);
                    else if (roll < 0.62f) PropMeshes.Stalagmite(b, rng, Pal.Basalt, R(0.8f, 2.6f));
                    else b.Rock(new Vector3(0f, 0.2f, 0f), new Vector3(R(0.6f, 1.4f), R(0.4f, 0.7f), R(0.6f, 1.4f)), rng, roll < 0.8f ? Pal.Basalt : Pal.Lava, 0.45f, 0.65f);
                    break;
                default:
                    if (roll < 0.5f) PropMeshes.BonePile(b, rng);
                    else if (roll < 0.78f) PropMeshes.Candles(b, rng);
                    else PropMeshes.Stalagmite(b, rng, Pal.BoneWall, R(0.8f, 2.2f));
                    break;
            }
            b.Pop();
        }

        // ---- Light -----------------------------------------------------------------------------------

        /// <summary>
        /// A fire on the wall every few strides of every room, and something hanging from its vault. Nothing here collides:
        /// the wall pieces lie against stone the colliders already own, and the hanging ones end above a raised hand.
        /// </summary>
        void LightTheHalls(DungeonLayout layout)
        {
            const int Spacing = 3;
            const float LightHeight = 2.5f;
            Vector3 stairUp = layout.StairUp - origin, stairDown = layout.StairDown - origin;
            bool NearStair(Vector3 p) => (new Vector2(p.x - stairUp.x, p.z - stairUp.z)).magnitude < 3.2f ||
                                         (new Vector2(p.x - stairDown.x, p.z - stairDown.z)).magnitude < 3.2f;

            void OnWall(Vector3 point, Vector3 inward)
            {
                if (NearStair(point)) return;
                var b = ChunkAt(point + inward * 0.5f);
                b.Push(point + Vector3.up * LightHeight, Quaternion.LookRotation(-inward), Vector3.one);
                DungeonLights.WallLight(b, style.Decor, rng);
                b.Pop();
            }

            foreach (var room in rooms)
            {
                RectInt r = room.Rect;
                for (int z = r.yMin + 1; z < r.yMax; z += Spacing)
                {
                    if (!IsFloor(r.xMin - 1, z)) OnWall(Local(r.xMin, z + 0.5f), Vector3.right);
                    if (!IsFloor(r.xMax, z)) OnWall(Local(r.xMax, z + 0.5f), Vector3.left);
                }
                for (int x = r.xMin + 2; x < r.xMax - 1; x += Spacing)
                {
                    if (!IsFloor(x, r.yMin - 1)) OnWall(Local(x + 0.5f, r.yMin), Vector3.forward);
                    if (!IsFloor(x, r.yMax)) OnWall(Local(x + 0.5f, r.yMax), Vector3.back);
                }

                // Hanging pieces sit off the room's middle, so they frame a landmark instead of hiding it.
                int hanging = Mathf.Clamp(r.width * r.height / 45, 1, 4);
                for (int i = 0; i < hanging; i++)
                {
                    float x = (i % 2 == 0 ? R(r.xMin + 1.5f, room.Center.x - 1.5f) : R(room.Center.x + 1.5f, r.xMax - 1.5f));
                    float z = R(r.yMin + 1.5f, r.yMax - 1.5f);
                    float ceiling = CeilingAt(Mathf.FloorToInt(x), Mathf.FloorToInt(z));
                    float drop = Mathf.Max(0.6f, ceiling - 3.6f - R(0f, 1.2f));
                    Vector3 top = Local(x, z) + Vector3.up * ceiling;
                    var b = ChunkAt(top);
                    b.Push(top, Quaternion.Euler(0f, R(0f, 360f), 0f), Vector3.one);
                    DungeonLights.Hanging(b, style.Decor, drop, rng);
                    b.Pop();
                }
            }
        }

        readonly struct Glow
        {
            public readonly Vector3 Position;
            public readonly Color Color;
            public readonly float Range;

            public Glow(Vector3 position, Color color, float range)
            {
                Position = position;
                Color = color;
                Range = range;
            }
        }

        /// <summary>
        /// Every emissive face becomes a light, and the depth's stone takes on its glow: warm pools by the fires, cold ones by
        /// the spores and the sea, darkness between. The shader's point lights reach only a metre or two, so this is what
        /// makes the halls read; the zone's three real lights and the carried torch play over it.
        /// </summary>
        void BakeLight()
        {
            const float Bucket = 6f;
            var cells = new Dictionary<Vector3Int, (Vector3 position, Color color, float area)>();
            foreach (MeshBuilder chunk in chunks) chunk.CollectGlows(2f, cells);

            var glows = new List<Glow>();
            foreach (var cell in cells.Values)
            {
                Color sum = cell.color;
                float peak = Mathf.Max(sum.r, Mathf.Max(sum.g, sum.b));
                if (peak <= 0f) continue;
                Color hue = Color.Lerp(sum / peak, new Color(1f, 0.92f, 0.82f), 0.3f);
                float size = Mathf.Sqrt(cell.area);
                // Vertex colours are linear: a fire has to double the stone near it before the eye sees a pool.
                float strength = Mathf.Clamp(1.5f + size * 1.2f, 1.5f, 2.4f) * GlowScale(style.Decor);
                float range = Mathf.Clamp(7f + size * 5f, 7f, 12f);
                glows.Add(new Glow(cell.position / cell.area, hue * strength, range));
            }

            var buckets = new Dictionary<Vector2Int, List<Glow>>();
            foreach (Glow glow in glows)
            {
                int x0 = Mathf.FloorToInt((glow.Position.x - glow.Range) / Bucket), x1 = Mathf.FloorToInt((glow.Position.x + glow.Range) / Bucket);
                int z0 = Mathf.FloorToInt((glow.Position.z - glow.Range) / Bucket), z1 = Mathf.FloorToInt((glow.Position.z + glow.Range) / Bucket);
                for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    var key = new Vector2Int(x, z);
                    if (!buckets.TryGetValue(key, out var list)) buckets[key] = list = new List<Glow>();
                    list.Add(glow);
                }
            }

            Color dark = Unlit(style.Decor);
            Color LightAt(Vector3 position, Vector3 normal)
            {
                Color light = dark;
                if (buckets.TryGetValue(new Vector2Int(Mathf.FloorToInt(position.x / Bucket), Mathf.FloorToInt(position.z / Bucket)), out var near))
                    foreach (Glow glow in near)
                    {
                        Vector3 towards = glow.Position - position;
                        float distance = towards.magnitude;
                        if (distance >= glow.Range) continue;
                        float reach = 1f - distance / glow.Range;
                        float facing = distance < 0.01f ? 1f : Mathf.Clamp01(Vector3.Dot(normal, towards / distance) * 0.75f + 0.25f);
                        light += glow.Color * (reach * reach * facing);
                    }
                return new Color(Mathf.Min(light.r, 2.1f), Mathf.Min(light.g, 1.9f), Mathf.Min(light.b, 1.8f));
            }

            foreach (MeshBuilder chunk in chunks) chunk.Bake(LightAt);
            Debug.Log($"[AshenRealm] {style.Name}: {glows.Count} baked lights.");
        }

        /// <summary>The caves glow everywhere, a little each; elsewhere every light is a fire someone keeps.</summary>
        static float GlowScale(DungeonDecor decor) => decor == DungeonDecor.Fungal ? 0.32f : 1f;

        /// <summary>What the stone keeps where no fire reaches: dim, and leaning to each biome's own cold.</summary>
        static Color Unlit(DungeonDecor decor)
        {
            switch (decor)
            {
                case DungeonDecor.Fungal: return new Color(0.32f, 0.38f, 0.36f);
                case DungeonDecor.Harbor: return new Color(0.28f, 0.33f, 0.37f);
                case DungeonDecor.Ember: return new Color(0.4f, 0.32f, 0.29f);
                case DungeonDecor.Marrow: return new Color(0.29f, 0.28f, 0.34f);
                default: return new Color(0.26f, 0.26f, 0.32f);
            }
        }

        void Flush()
        {
            for (int x = 0; x < ChunkGrid; x++)
            for (int z = 0; z < ChunkGrid; z++)
            {
                if (chunks[x, z].TriangleCount == 0) continue;
                string name = $"{style.Name}_{x}_{z}";
                Mesh mesh = GeneratedAssets.SaveMesh(chunks[x, z], name);
                var go = new GameObject(name);
                go.transform.SetParent(root, false);
                go.transform.position = origin;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off; // no sun down here, so nothing would cast
            }
        }
    }
}
