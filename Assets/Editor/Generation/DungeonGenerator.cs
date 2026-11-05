using System.Collections.Generic;
using System.Linq;
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

        struct Room
        {
            public RectInt Rect;
            public Vector2Int Center => new Vector2Int(Rect.x + Rect.width / 2, Rect.y + Rect.height / 2);
        }

        readonly BiomeStyle style;
        readonly Material material;
        readonly Vector3 origin;
        readonly System.Random rng;
        readonly bool[,] floor = new bool[Width, Height];
        readonly List<Room> rooms = new List<Room>();
        readonly MeshBuilder[,] chunks = new MeshBuilder[ChunkGrid, ChunkGrid];
        Transform root, colliderRoot;

        public DungeonGenerator(BiomeStyle style, Material material, Vector3 origin, int seed)
        {
            this.style = style;
            this.material = material;
            this.origin = origin;
            rng = new System.Random(seed);
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
            Dress();
            Flush();
            return layout;
        }

        // ---- Layout ----------------------------------------------------------------------------------

        void PlaceRooms()
        {
            // The entry and the keeper's hall sit in opposite corners, so every depth is a real walk.
            rooms.Add(new Room { Rect = new RectInt(3, 3, 9, 8) });
            rooms.Add(new Room { Rect = new RectInt(Width - 20, Height - 18, 17, 15) });

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

            for (int i = 0; i < 2 && rooms.Count > 4; i++)
                Carve(rooms[RI(2, rooms.Count)].Center, rooms[RI(2, rooms.Count)].Center);
        }

        void Carve(Vector2Int a, Vector2Int b)
        {
            int x = a.x, z = a.y;
            bool horizontalFirst = rng.NextDouble() < 0.5;
            void Open(int cx, int cz)
            {
                for (int ox = 0; ox < 2; ox++)
                for (int oz = 0; oz < 2; oz++)
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

                bool patch = Mathf.PerlinNoise(x * 0.31f + 3.7f, z * 0.31f + 8.1f) > 0.56f;
                chunk.SetColor(patch ? style.FloorPatch : style.Floor, R(0.44f, 0.6f));
                chunk.QuadOutward(new Vector3(a.x, 0f, a.z), new Vector3(c.x, 0f, a.z), new Vector3(c.x, 0f, c.z), new Vector3(a.x, 0f, c.z), new Vector3((a.x + c.x) * 0.5f, -1f, (a.z + c.z) * 0.5f));

                chunk.SetColor(style.Ceiling, R(0.3f, 0.42f));
                chunk.QuadOutward(new Vector3(a.x, WallHeight, a.z), new Vector3(c.x, WallHeight, a.z), new Vector3(c.x, WallHeight, c.z), new Vector3(a.x, WallHeight, c.z), new Vector3((a.x + c.x) * 0.5f, WallHeight + 1f, (a.z + c.z) * 0.5f));

                if (!IsFloor(x - 1, z)) Wall(chunk, new Vector3(a.x, 0f, a.z), new Vector3(a.x, 0f, c.z), Vector3.right, x + z);
                if (!IsFloor(x + 1, z)) Wall(chunk, new Vector3(c.x, 0f, a.z), new Vector3(c.x, 0f, c.z), Vector3.left, x + z);
                if (!IsFloor(x, z - 1)) Wall(chunk, new Vector3(a.x, 0f, a.z), new Vector3(c.x, 0f, a.z), Vector3.forward, x + z);
                if (!IsFloor(x, z + 1)) Wall(chunk, new Vector3(a.x, 0f, c.z), new Vector3(c.x, 0f, c.z), Vector3.back, x + z);
            }
        }

        void Wall(MeshBuilder b, Vector3 p, Vector3 q, Vector3 inward, int index)
        {
            const float bandHeight = 0.7f;
            Vector3 up = Vector3.up;

            b.SetColor(style.WallBand, R(0.36f, 0.46f));
            b.QuadOutward(p, q, q + up * bandHeight, p + up * bandHeight, p + inward * -1f + (q - p) * 0.5f + up * 0.3f);
            b.SetColor(style.Wall, R(0.44f, 0.62f));
            b.QuadOutward(p + up * bandHeight, q + up * bandHeight, q + up * WallHeight, p + up * WallHeight, p + inward * -1f + (q - p) * 0.5f + up * 3f);

            // A buttress every few tiles breaks up the long walls.
            if (index % 4 != 0) return;
            Vector3 centre = (p + q) * 0.5f + inward * 0.22f + up * (WallHeight * 0.5f);
            Vector3 size = new Vector3(Mathf.Abs(inward.x) > 0.5f ? 0.44f : 0.8f, WallHeight, Mathf.Abs(inward.x) > 0.5f ? 0.8f : 0.44f);
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
            roof.transform.position = origin + new Vector3(Width * Cell * 0.5f, WallHeight + 0.5f, Height * Cell * 0.5f);
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
                    wall.transform.position = origin + new Vector3((start + (x - start) * 0.5f) * Cell, WallHeight * 0.5f, (z + 0.5f) * Cell);
                    wall.AddComponent<BoxCollider>().size = new Vector3(length, WallHeight, Cell);
                }
            }
        }

        // ---- Features --------------------------------------------------------------------------------

        Vector3 InRoom(Room room, float margin = 1.6f)
        {
            RectInt r = room.Rect;
            return World(R(r.xMin + margin, r.xMax - margin), R(r.yMin + margin, r.yMax - margin));
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
            int encounter = 0, elitesLeft = string.IsNullOrEmpty(style.Elite) ? 0 : 1, chests = 3;
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
                    layout.Spawns.Add((pool[RI(0, pool.Length)], InRoom(room), Patrol(room)));
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
                    PlaceProp(Local(x, z));
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
