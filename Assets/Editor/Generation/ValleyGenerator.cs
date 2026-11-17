using System.Collections.Generic;
using System.Linq;
using AshenRealm.Core;
using AshenRealm.Interactions;
using AshenRealm.Story;
using AshenRealm.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AshenRealm.EditorTools
{
    /// <summary>Key positions produced by the valley generator for gameplay placement.</summary>
    internal sealed class ValleyLayout
    {
        public Vector3 PlayerSpawn;
        public float PlayerYaw;
        public Vector3 KnightSpawn;
        public float KnightYaw;
        public Vector3[] PatrolPoints;
        public Vector3 ChestPosition;
        public float ChestYaw;
        public Transform Root;
        public Vector3 GatePosition;
        public Vector3 GateArrival;
        public ArrivalLayout Arrival;
        public readonly List<(Vector3 door, Vector3 arrival, float yaw)> Homes = new List<(Vector3, Vector3, float)>();
        public readonly List<(string id, Vector3 position, float yaw, System.Action<MeshBuilder> figure)> People =
            new List<(string, Vector3, float, System.Action<MeshBuilder>)>();
        /// <summary>People who only live there; the last field names a look several of them share, or null.</summary>
        public readonly List<(string name, Vector3 position, float yaw, System.Action<MeshBuilder> figure, string look)> Ambient =
            new List<(string, Vector3, float, System.Action<MeshBuilder>, string)>();
    }

    /// <summary>
    /// Deterministically generates the Prototype Valley: heightfield terrain with painted paths, rim hills,
    /// distant mountains, pine forest, boulders, grass, flowers, fences, a camp, a sword graveyard and a ruined
    /// chapel. Static decoration is merged into a 4×4 grid of chunk meshes to keep draw calls low; colliders are
    /// separate primitives.
    /// </summary>
    internal sealed partial class ValleyGenerator
    {
        public const int Seed = 1337;

        // The lower valley keeps its square of walls (±103). North of its rim the land opens into a plain 500 m wide that runs
        // to the city gate at z 590; the ridge between them is crossed only at the Old Toll Gate (0, 92). The grid keeps the
        // old 2.5 m cells on the old origin, so the lower valley's heights stay exactly what they were.
        const float MinX = -330f, MinZ = -165f;
        const float CellSize = 2.5f;
        const int CellsX = 264, CellsZ = 352, TileCells = 44;
        const float WallHalf = 103f;
        const float NorthHalf = 250f, NorthEnd = 600f, RidgeZ = 103f;
        const float ChunkSize = 66f;
        const int ChunksX = 10, ChunksZ = 14;
        internal const int ColliderCells = ChunksX * ChunksZ;

        static readonly Vector2 RuinsCenter = new Vector2(0f, 32f);
        static readonly Vector2 CampCenter = new Vector2(26f, -42f);
        static readonly Vector2 GraveyardCenter = new Vector2(-32f, -6f);
        static readonly Vector2 SpawnCenter = new Vector2(0f, -86f);
        static readonly Vector2 HamletCenter = new Vector2(34f, -14f);
        static readonly Vector2 WindmillCenter = new Vector2(54f, 8f);
        static readonly Vector2 GateCenter = new Vector2(0f, 92f);   // the Old Toll Gate
        static readonly Vector2 CityGateCenter = new Vector2(0f, 590f);
        static readonly Vector2 LakeCenter = new Vector2(185f, 360f), LakeRadii = new Vector2(55f, 85f);

        readonly Material worldMaterial;
        readonly Material foliageMaterial;
        readonly System.Random rng = new System.Random(Seed);

        /// <summary>A road: its spline, where its dirt is full (inner) and where it fades out (outer), and a box to skip it fast.</summary>
        sealed class Road
        {
            public Vector2[] Points;
            public float Inner, Outer;
            public Vector2 Min, Max;
        }

        readonly List<Road> roads = new List<Road>();
        readonly List<(Vector2 center, float radius, float falloff, float height)> flats = new List<(Vector2, float, float, float)>();
        readonly List<(Vector2 center, float radius)> clearings = new List<(Vector2, float)>();
        readonly Dictionary<Vector2Int, List<Vector2>> treeGrid = new Dictionary<Vector2Int, List<Vector2>>();
        readonly List<(int kind, Vector3 position, float yaw, float scale)> scatter = new List<(int, Vector3, float, float)>();
        readonly List<ScatterData.Kind> kinds = new List<ScatterData.Kind>();
        readonly Dictionary<Vector2Int, GameObject> treeColliderCells = new Dictionary<Vector2Int, GameObject>();

        Vector2 noiseA, noiseB, noiseC;
        float[,] heights;
        Transform root, colliderRoot;
        MeshBuilder[,] decorChunks;
        int[] pineKinds, autumnKinds, flowerKinds, wheatKinds;
        int[,] grassKinds;
        MeshBuilder[] rockVariants;
        Mesh[] rockMeshes;

        /// <param name="world">Opaque PixelLit material for all solid geometry.</param>
        /// <param name="foliage">Alpha-clipped, two-sided, wind-swayed PixelLit material for cards.</param>
        public ValleyGenerator(Material world, Material foliage)
        {
            worldMaterial = world;
            foliageMaterial = foliage;
        }

        public ValleyLayout Generate()
        {
            root = new GameObject("Environment").transform;
            colliderRoot = new GameObject("Colliders").transform;
            colliderRoot.SetParent(root, false);

            noiseA = new Vector2(R(100f, 900f), R(100f, 900f));
            noiseB = new Vector2(R(100f, 900f), R(100f, 900f));
            noiseC = new Vector2(R(100f, 900f), R(100f, 900f));

            BuildPaths();
            DefineAreas();
            BuildHeightfield();
            BuildTerrain();

            decorChunks = new MeshBuilder[ChunksX, ChunksZ];
            for (int x = 0; x < ChunksX; x++)
            for (int z = 0; z < ChunksZ; z++)
                decorChunks[x, z] = new MeshBuilder();
            BuildVariants();

            var layout = new ValleyLayout { Root = root };
            BuildRuins(layout);
            BuildGraveyard();
            BuildCamp();
            BuildHamlet(layout);
            layout.Arrival = new ArrivalGenerator(worldMaterial).Generate(root, Ground);
            BuildTollGate();
            BuildPuddles();
            BuildFences();
            BuildPilgrimRoute();
            BuildVillageCourtyard();
            DressCampAndGraves();
            BuildDesignedGroves();
            ScatterTrees();
            ScatterRocks();
            ScatterFoliage();
            BuildCityGate(layout);
            BuildNorthPlaces();
            PlaceNorthPeople(layout);
            ScatterNorth();
            BuildMountains();
            BuildFarTreeLine();
            BuildBoundary();
            FlushChunks();
            BuildScatter();
            AddScenicViews();

            layout.PlayerSpawn = Ground(SpawnCenter.x, SpawnCenter.y) + Vector3.up * 0.1f;
            layout.PlayerYaw = 0f;
            layout.KnightSpawn = Ground(0f, 13f) + Vector3.up * 0.05f;
            layout.KnightYaw = 180f;
            layout.PatrolPoints = new[] { Ground(-7f, 12f), Ground(7f, 12f), Ground(6f, 4f), Ground(-6f, 4f) };
            return layout;
        }

        // ---- Noise, paths and height ---------------------------------------------------------------

        float R(float min, float max) => MeshBuilder.Lerp(rng, min, max);

        static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Rounded-square distance from the valley centre (superellipse, p = 4).</summary>
        static float SuperDistance(float x, float z) => Mathf.Pow(Mathf.Pow(Mathf.Abs(x), 4f) + Mathf.Pow(Mathf.Abs(z), 4f), 0.25f);

        /// <summary>The road from the Old Toll Gate to the city gate, wide enough to ride. A2 builds its places along it.</summary>
        public static readonly Vector2[] MainRoad =
        {
            new Vector2(0f, 84f), new Vector2(0f, 92f), new Vector2(-4f, 120f), new Vector2(6f, 160f), new Vector2(2f, 200f), new Vector2(-8f, 240f),
            new Vector2(0f, 300f), new Vector2(10f, 350f), new Vector2(4f, 400f), new Vector2(-6f, 450f), new Vector2(0f, 500f),
            new Vector2(0f, 560f), new Vector2(0f, 586f),
        };

        void BuildPaths()
        {
            AddRoad(new[]
            {
                new Vector2(0f, -96f), new Vector2(0f, -86f), new Vector2(-3f, -70f), new Vector2(4f, -54f), new Vector2(2f, -38f),
                new Vector2(-5f, -20f), new Vector2(-2f, -4f), new Vector2(1f, 9f), new Vector2(0f, 19f), new Vector2(0f, 23f),
            });
            AddRoad(new[] { new Vector2(2f, -38f), new Vector2(12f, -41f), new Vector2(21f, -42f), new Vector2(25f, -42f) });
            AddRoad(new[] { new Vector2(-5f, -20f), new Vector2(-16f, -14f), new Vector2(-26f, -8f), new Vector2(-31f, -6f) });
            AddRoad(new[] { new Vector2(-4f, -12f), new Vector2(8f, -15f), new Vector2(20f, -13f), new Vector2(30f, -14f), HamletCenter });
            AddRoad(new[] { HamletCenter, new Vector2(42f, -6f), new Vector2(48f, 2f), new Vector2(52f, 6f) });
            AddRoad(new[] { new Vector2(1f, 9f), new Vector2(13f, 13f), new Vector2(17f, 30f), new Vector2(14f, 50f), new Vector2(5f, 70f), GateCenter });
            AddRoad(new[] { new Vector2(-3f, -65f), new Vector2(-16f, -64f), new Vector2(-24f, -58f), new Vector2(-27f, -52f),
                new Vector2(-35f, -45f), new Vector2(-44f, -30f), new Vector2(-40f, -17f), GraveyardCenter });
            AddRoad(new[] { GraveyardCenter, new Vector2(-43f, 13f), new Vector2(-46f, 32f), new Vector2(-41f, 44f),
                new Vector2(-30f, 58f), new Vector2(-6f, 70f), new Vector2(5f, 70f) });

            // The north: the main road is 3.6 m of dirt (room for a horse), the side roads to A2's places 2.8 m.
            AddRoad(MainRoad, 1.8f, 3.6f);
            foreach (Vector2[] spur in Spurs) AddRoad(spur, 1.4f, 2.8f);
        }

        /// <summary>The side roads to the farm, the lake's jetty, the watchtower, the burned farm and round Feketefenyves.</summary>
        public static readonly Vector2[][] Spurs =
        {
            new[] { new Vector2(6f, 160f), new Vector2(40f, 170f), new Vector2(80f, 185f), new Vector2(120f, 195f) },
            new[] { new Vector2(0f, 300f), new Vector2(50f, 310f), new Vector2(100f, 330f), new Vector2(140f, 350f) },
            new[] { new Vector2(4f, 400f), new Vector2(35f, 430f), new Vector2(55f, 448f) },
            new[] { new Vector2(-4f, 120f), new Vector2(-50f, 140f), new Vector2(-100f, 165f) },
            new[] { new Vector2(0f, 300f), new Vector2(-70f, 330f), new Vector2(-140f, 360f), new Vector2(-150f, 420f), new Vector2(-60f, 500f), new Vector2(0f, 500f) },
        };

        void AddRoad(Vector2[] points, float inner = 1.2f, float outer = 2.8f)
        {
            Vector2[] spline = CatmullRom(points);
            Vector2 min = spline[0], max = spline[0];
            foreach (Vector2 p in spline) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            roads.Add(new Road { Points = spline, Inner = inner, Outer = outer, Min = min, Max = max });
        }

        internal static Vector2[] CatmullRom(Vector2[] points)
        {
            var result = new List<Vector2>();
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector2 p0 = points[Mathf.Max(i - 1, 0)], p1 = points[i], p2 = points[i + 1], p3 = points[Mathf.Min(i + 2, points.Length - 1)];
                int steps = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(p1, p2)));
                for (int s = 0; s < steps; s++)
                {
                    float t = s / (float)steps, t2 = t * t, t3 = t2 * t;
                    result.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            result.Add(points[points.Length - 1]);
            return result.ToArray();
        }

        static float RoadDistance(Road road, Vector2 p)
        {
            float best = float.MaxValue;
            Vector2[] path = road.Points;
            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector2 a = path[i], ab = path[i + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-5f));
                float d = (a + ab * t - p).sqrMagnitude;
                if (d < best) best = d;
            }
            return Mathf.Sqrt(best);
        }

        // Points far outside a road's box cannot be on it; skipping those keeps the large heightfield quick.
        static bool Near(Road road, Vector2 p, float margin) =>
            p.x > road.Min.x - margin && p.x < road.Max.x + margin && p.y > road.Min.y - margin && p.y < road.Max.y + margin;

        /// <summary>Distance to the nearest road centre line (capped at 60 m: nothing asks farther).</summary>
        float PathDistance(float x, float z)
        {
            var p = new Vector2(x, z);
            float best = 60f;
            foreach (var road in roads)
                if (Near(road, p, best)) best = Mathf.Min(best, RoadDistance(road, p));
            return best;
        }

        float PathMask(float x, float z)
        {
            var p = new Vector2(x, z);
            float mask = 0f;
            foreach (var road in roads)
                if (Near(road, p, road.Outer)) mask = Mathf.Max(mask, 1f - SmoothStep(road.Inner, road.Outer, RoadDistance(road, p)));
            return mask;
        }

        void DefineAreas()
        {
            flats.Add((RuinsCenter, 15f, 8f, BaseHeight(RuinsCenter.x, RuinsCenter.y) + 0.4f));
            flats.Add((CampCenter, 8f, 6f, BaseHeight(CampCenter.x, CampCenter.y)));
            flats.Add((GraveyardCenter, 9f, 6f, BaseHeight(GraveyardCenter.x, GraveyardCenter.y)));
            flats.Add((SpawnCenter, 5f, 5f, BaseHeight(SpawnCenter.x, SpawnCenter.y)));
            flats.Add((new Vector2(0f, -95f), 9f, 5f, BaseHeight(SpawnCenter.x, SpawnCenter.y)));
            flats.Add((new Vector2(-7.4f, -84.8f), 4f, 3f, BaseHeight(SpawnCenter.x, SpawnCenter.y)));
            flats.Add((new Vector2(7.5f, -91f), 4f, 3f, BaseHeight(SpawnCenter.x, SpawnCenter.y)));
            flats.Add((HamletCenter, 14f, 8f, BaseHeight(HamletCenter.x, HamletCenter.y)));
            flats.Add((WindmillCenter, 4f, 9f, BaseHeight(WindmillCenter.x, WindmillCenter.y) + 1.8f));
            flats.Add((GateCenter, 8f, 9f, BaseHeight(GateCenter.x, GateCenter.y - 10f)));
            flats.Add((new Vector2(-34f, -53f), 6f, 6f, BaseHeight(-34f, -53f) + 0.3f));

            clearings.Add((RuinsCenter, 19f));
            clearings.Add((new Vector2(0f, 10f), 10f));   // knight patrol ground
            clearings.Add((CampCenter, 10f));
            clearings.Add((GraveyardCenter, 10f));
            clearings.Add((SpawnCenter, 7f));
            clearings.Add((new Vector2(0f, -94f), 13f));
            clearings.Add((new Vector2(-7.4f, -84.8f), 7f));
            clearings.Add((HamletCenter, 17f));
            clearings.Add((WindmillCenter, 7f));
            clearings.Add((GateCenter, 11f));
            clearings.Add((new Vector2(-34f, -53f), 9f));
            clearings.Add((new Vector2(-43f, 51f), 20f));
            clearings.Add((new Vector2(-13f, -84f), 5f));

            // The north: the city gate, and ground kept free of trees and grass for the places A2 builds (section 2 of the plan).
            flats.Add((CityGateCenter, 10f, 10f, BaseHeight(CityGateCenter.x, CityGateCenter.y - 10f)));
            // The pass runs level through the gate and out the north side, so the road climbs no rim behind the doors.
            flats.Add((new Vector2(0f, 112f), 8f, 12f, BaseHeight(GateCenter.x, GateCenter.y - 10f)));
            foreach (var (x, z, radius) in new[]
            {
                (0f, 100f, 12f), (32f, 114f, 16f), (46f, 118f, 12f), (12f, 108f, 5f),          // pass, stable, paddock, rail
                (130f, 200f, 26f), (-110f, 170f, 20f), (0f, 300f, 14f), (-60f, 300f, 8f), (-70f, 290f, 7f),
                (-110f, 340f, 9f), (-145f, 400f, 8f), (-200f, 230f, 12f), (140f, 350f, 10f), (60f, 450f, 18f),
                (-90f, 540f, 36f), (0f, 590f, 24f), (-3f, 205f, 6f), (8f, 470f, 6f), (8f, 578f, 5f), (-10f, 96f, 5f),
            })
                clearings.Add((new Vector2(x, z), radius));

            // The places stand on level ground: the stable and paddock (short of the ridge's north face), the farmyard, the burned farm.
            float stableGround = BaseHeight(40f, 120f);
            foreach (float x in new[] { 31f, 42f, 52f }) flats.Add((new Vector2(x, 119.5f), 7f, 5f, stableGround));
            flats.Add((new Vector2(134f, 199f), 20f, 10f, BaseHeight(134f, 199f)));
            flats.Add((new Vector2(-111f, 171f), 9f, 7f, BaseHeight(-111f, 171f)));
            // The toll keeper's desk and the hitching rail sit on the pass's slope; each gets a step level with the gate road.
            flats.Add((new Vector2(10f, 95.5f), 2.5f, 3f, BaseHeight(GateCenter.x, GateCenter.y - 10f)));
            flats.Add((new Vector2(12f, 109f), 3f, 3f, BaseHeight(GateCenter.x, GateCenter.y - 10f)));
            // The lake lies level with its shore's average height, so the water meets the bank all round.
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 8f;
                lakeShore += BaseHeight(LakeCenter.x + Mathf.Cos(a) * LakeRadii.x, LakeCenter.y + Mathf.Sin(a) * LakeRadii.y) / 16f;
            }
        }

        /// <summary>
        /// Inside the walls, with a margin: the lower valley's square, the north land, and the pocket behind the Old Toll Gate.
        /// </summary>
        internal static bool Walkable(float x, float z, float margin = 0f) =>
            Mathf.Abs(x) < WallHalf + margin && z > -WallHalf - margin && z < RidgeZ + margin ||
            Mathf.Abs(x) < NorthHalf + margin && z > RidgeZ - margin && z < NorthEnd + margin;

        /// <summary>Distance outside the north land's open floor; its rim hills rise over the last 25 m inside the walls.</summary>
        static float NorthOutside(float x, float z)
        {
            float dx = Mathf.Max(Mathf.Abs(x) - (NorthHalf - 25f), 0f);
            float dz = Mathf.Max(Mathf.Abs(z - (RidgeZ + NorthEnd) * 0.5f) - ((NorthEnd - RidgeZ) * 0.5f - 25f), 0f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        internal static bool InLake(float x, float z, float margin)
        {
            float u = (x - LakeCenter.x) / (LakeRadii.x + margin), v = (z - LakeCenter.y) / (LakeRadii.y + margin);
            return u * u + v * v < 1f;
        }

        /// <summary>Distance from the lake's centre as a fraction of its ellipse: 1 on the shore.</summary>
        static float LakeRatio(float x, float z)
        {
            float u = (x - LakeCenter.x) / LakeRadii.x, v = (z - LakeCenter.y) / LakeRadii.y;
            return Mathf.Sqrt(u * u + v * v);
        }

        /// <summary>Feketefenyves, the dark pine forest in the west of the north land (0..1).</summary>
        float ForestMask(float x, float z)
        {
            float edge = (Mathf.PerlinNoise(x * 0.03f + noiseC.y, z * 0.03f + noiseA.x) - 0.5f) * 24f;
            return SmoothStep(-78f + edge, -102f + edge, x) * SmoothStep(166f, 190f, z + edge) * (1f - SmoothStep(510f, 534f, z - edge));
        }

        /// <summary>The golden fields north of the pass (0..1), short of the burned farm, the farmyard and the lake.</summary>
        static float NorthFieldMask(float x, float z)
        {
            var p = new Vector2(x, z);
            float field = SmoothStep(-100f, -92f, x) * (1f - SmoothStep(192f, 200f, x)) * SmoothStep(132f, 140f, z) * (1f - SmoothStep(282f, 290f, z));
            field *= SmoothStep(26f, 32f, Vector2.Distance(p, new Vector2(-110f, 170f))) * SmoothStep(24f, 30f, Vector2.Distance(p, new Vector2(130f, 200f)));
            return InLake(x, z, 14f) ? 0f : field;
        }

        /// <summary>Grey ground: the burned farm and the ash field (0..1).</summary>
        static float AshMask(float x, float z)
        {
            var p = new Vector2(x, z);
            return Mathf.Max(1f - SmoothStep(14f, 22f, Vector2.Distance(p, new Vector2(-110f, 170f))),
                1f - SmoothStep(28f, 40f, Vector2.Distance(p, new Vector2(-90f, 540f))));
        }

        /// <summary>Trampled ground in the hamlet square.</summary>
        float YardMask(float x, float z) => 1f - SmoothStep(5f, 9f, Vector2.Distance(new Vector2(x, z), HamletCenter));

        /// <summary>Golden wheat fields east of the hamlet (0 outside).</summary>
        static float FieldMask(float x, float z)
        {
            bool fieldA = x > 40f && x < 62f && z > -36f && z < -22f;
            bool fieldB = x > 44f && x < 64f && z > -10f && z < 0f;
            return fieldA || fieldB ? 1f : 0f;
        }

        float BaseHeight(float x, float z)
        {
            float n1 = Mathf.PerlinNoise(x * 0.016f + noiseA.x, z * 0.016f + noiseA.y);
            float n2 = Mathf.PerlinNoise(x * 0.055f + noiseB.x, z * 0.055f + noiseB.y);
            float h = (n1 - 0.5f) * 8f + (n2 - 0.5f) * 1.8f;

            // The rim rises where the ground leaves both floors; inside the lower valley the north floor is always farther, so
            // its heights are the old ones.
            float rim = SmoothStep(0f, 50f, Mathf.Min(SuperDistance(x, z) - 76f, NorthOutside(x, z)));
            float n3 = Mathf.PerlinNoise(x * 0.03f + noiseC.x, z * 0.03f + noiseC.y);
            float n4 = Mathf.PerlinNoise(x * 0.11f + noiseC.y, z * 0.11f + noiseC.x);
            h += rim * rim * (24f + n3 * 26f) + rim * (n4 - 0.5f) * 6f;

            // The old northern rim stays a ridge, with a notch only for the Old Toll Gate.
            // Its north face is short and steep so the stable's ground just beyond it (z 110..125) stays level.
            h += 24f * SmoothStep(86f, 99f, z) * (1f - SmoothStep(104f, 113f, z)) * SmoothStep(13f, 28f, Mathf.Abs(x));
            // Two hills flank the approach to the city gate; a rise for the watchtower.
            h += Bump(x, z, -48f, 552f, 22f, 15f) + Bump(x, z, 48f, 552f, 22f, 15f) + Bump(x, z, 60f, 450f, 16f, 6f);
            return h;
        }

        static float Bump(float x, float z, float cx, float cz, float radius, float height)
        {
            float d2 = ((x - cx) * (x - cx) + (z - cz) * (z - cz)) / (radius * radius);
            return d2 > 25f ? 0f : height * Mathf.Exp(-0.5f * d2);
        }

        float Height(float x, float z)
        {
            float h = BaseHeight(x, z);
            foreach (var flat in flats)
            {
                float w = 1f - SmoothStep(flat.radius, flat.radius + flat.falloff, Vector2.Distance(new Vector2(x, z), flat.center));
                h = Mathf.Lerp(h, flat.height, w);
            }
            // Tükörtó: the bank eases to the shore height, the bed drops inside the shore, and an island holds the bell tower.
            float r = LakeRatio(x, z);
            if (r < 1.35f)
            {
                h = Mathf.Lerp(h, lakeShore, 1f - SmoothStep(1f, 1.35f, r)) - LakeDepth * SmoothStep(1f, 0.8f, r);
                h = Mathf.Max(h, lakeShore + 0.4f - SmoothStep(3f, 9f, Vector2.Distance(new Vector2(x, z), BellIsland)) * 4.5f);
            }
            return h - PathMask(x, z) * 0.3f;
        }

        void BuildHeightfield()
        {
            heights = new float[CellsX + 1, CellsZ + 1];
            for (int i = 0; i <= CellsX; i++)
            for (int j = 0; j <= CellsZ; j++)
                heights[i, j] = Height(MinX + i * CellSize, MinZ + j * CellSize);
        }

        /// <summary>Exact height of the terrain mesh surface (matches the triangle split used in BuildTerrain).</summary>
        public float SurfaceHeight(float x, float z)
        {
            float gx = Mathf.Clamp((x - MinX) / CellSize, 0f, CellsX - 0.0001f);
            float gz = Mathf.Clamp((z - MinZ) / CellSize, 0f, CellsZ - 0.0001f);
            int i = Mathf.FloorToInt(gx), j = Mathf.FloorToInt(gz);
            float fx = gx - i, fz = gz - j;
            float h00 = heights[i, j], h10 = heights[i + 1, j], h01 = heights[i, j + 1], h11 = heights[i + 1, j + 1];
            return fz >= fx
                ? h00 + (h11 - h01) * fx + (h01 - h00) * fz
                : h00 + (h10 - h00) * fx + (h11 - h10) * fz;
        }

        Vector3 Ground(float x, float z) => new Vector3(x, SurfaceHeight(x, z), z);

        Vector3 SurfaceNormal(float x, float z)
        {
            const float e = 0.6f;
            return new Vector3(SurfaceHeight(x - e, z) - SurfaceHeight(x + e, z), 2f * e, SurfaceHeight(x, z - e) - SurfaceHeight(x, z + e)).normalized;
        }

        bool InClearing(float x, float z, float margin)
        {
            var p = new Vector2(x, z);
            foreach (var c in clearings)
                if (Vector2.Distance(p, c.center) < c.radius + margin) return true;
            return false;
        }

        // ---- Terrain -----------------------------------------------------------------------------------

        Pal GrassFamily(float x, float z)
        {
            float patch = Mathf.PerlinNoise(x * 0.045f + noiseB.y, z * 0.045f + noiseA.x);
            return patch < 0.36f ? Pal.GrassOlive : patch > 0.64f ? Pal.GrassDry : Pal.GrassGold;
        }

        /// <summary>
        /// Smooth-shaded terrain. Per grid vertex: normal from the height gradient, grass/dirt/rock tint and blend
        /// weights. Each triangle picks one texture pair (grass→dirt or grass→rock) and blends it per vertex.
        /// </summary>
        void BuildTerrain()
        {
            int nx = CellsX + 1, nz = CellsZ + 1;
            var normal = new Vector3[nx, nz];
            var tint = new Color[nx, nz];
            var dirtWeight = new float[nx, nz];
            var rockWeight = new float[nx, nz];
            for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                float x = MinX + i * CellSize, z = MinZ + j * CellSize;
                float hl = heights[Mathf.Max(i - 1, 0), j], hr = heights[Mathf.Min(i + 1, CellsX), j];
                float hd = heights[i, Mathf.Max(j - 1, 0)], hu = heights[i, Mathf.Min(j + 1, CellsZ)];
                Vector3 nrm = new Vector3(hl - hr, 2f * CellSize, hd - hu).normalized;
                normal[i, j] = nrm;

                float slope = 1f - nrm.y;
                float rockNoise = Mathf.PerlinNoise(x * 0.08f + noiseA.y, z * 0.08f + noiseB.x);
                float rock = Mathf.Max(SmoothStep(0.2f, 0.38f, slope), heights[i, j] > 16f ? SmoothStep(0.45f, 0.65f, rockNoise) : 0f);
                float dirt = Mathf.Max(PathMask(x, z), YardMask(x, z) * 0.9f) * (1f - rock);
                rockWeight[i, j] = rock;
                dirtWeight[i, j] = dirt;

                float olive = SmoothStep(0.55f, 0.75f, 1f - Mathf.PerlinNoise(x * 0.045f + noiseB.y, z * 0.045f + noiseA.x));
                float dry = SmoothStep(0.55f, 0.75f, Mathf.PerlinNoise(x * 0.045f + noiseB.y, z * 0.045f + noiseA.x));
                Color grass = Color.Lerp(Color.Lerp(Palette.Linear(Pal.GrassGold, 0.5f), Palette.Linear(Pal.GrassOlive, 0.5f), olive),
                    Palette.Linear(Pal.GrassDry, 0.5f), dry);
                // The north's regions show in the ground colour, so a field or forest still reads where its cards are culled.
                grass = Color.Lerp(grass, Palette.Linear(Pal.Thatch, 0.5f), NorthFieldMask(x, z) * 0.8f);
                grass = Color.Lerp(grass, Palette.Linear(Pal.PineDark, 0.42f), ForestMask(x, z) * 0.6f);
                grass = Color.Lerp(grass, Palette.Linear(Pal.Stone, 0.3f), AshMask(x, z) * 0.85f);
                grass = Color.Lerp(grass, Palette.Linear(Pal.DirtDark, 0.38f), 1f - SmoothStep(1f, 1.1f, LakeRatio(x, z)));
                grass *= 0.92f + Mathf.PerlinNoise(x * 0.2f + noiseC.x, z * 0.2f) * 0.16f;
                Color dirtColor = Palette.Linear(Pal.Dirt, 0.45f + rockNoise * 0.1f);
                Color rockColor = Palette.Linear(rockNoise < 0.4f ? Pal.RockDark : rockNoise > 0.62f ? Pal.RockWarm : Pal.Rock, 0.5f);
                Color c = Color.Lerp(Color.Lerp(grass, dirtColor, dirt), rockColor, rock);
                c.a = 1f;
                tint[i, j] = c;
            }

            var p = new Vector3[3];
            var nn = new Vector3[3];
            var cc = new Color[3];
            var uv = new Vector4[3];
            void Tri(MeshBuilder b, int ia, int ja, int ib, int jb, int ic, int jc)
            {
                int[] ii = { ia, ib, ic }, jj = { ja, jb, jc };
                float maxRock = 0f, maxDirt = 0f;
                for (int k = 0; k < 3; k++)
                {
                    maxRock = Mathf.Max(maxRock, rockWeight[ii[k], jj[k]]);
                    maxDirt = Mathf.Max(maxDirt, dirtWeight[ii[k], jj[k]]);
                }
                bool useRock = maxRock > maxDirt;
                TexLayer second = useRock ? TexLayer.Rock : TexLayer.Dirt;
                for (int k = 0; k < 3; k++)
                {
                    int i = ii[k], j = jj[k];
                    float x = MinX + i * CellSize, z = MinZ + j * CellSize;
                    p[k] = new Vector3(x, heights[i, j], z);
                    nn[k] = normal[i, j];
                    Color c = tint[i, j];
                    c.a = useRock ? rockWeight[i, j] : dirtWeight[i, j];
                    cc[k] = c;
                    uv[k] = new Vector4(x / 2.5f, z / 2.5f, (int)TexLayer.Grass, (int)second);
                }
                b.RawTriangle(p, nn, cc, uv, Vector4.zero);
            }

            // 110 m tiles: each is culled on its own and stays under the 16-bit index limit.
            for (int ti = 0; ti < CellsX / TileCells; ti++)
            for (int tj = 0; tj < CellsZ / TileCells; tj++)
            {
                var b = new MeshBuilder();
                for (int i = ti * TileCells; i < (ti + 1) * TileCells; i++)
                for (int j = tj * TileCells; j < (tj + 1) * TileCells; j++)
                {
                    // Same diagonal split as SurfaceHeight(): (00, 01, 11) and (00, 11, 10), clockwise from above.
                    Tri(b, i, j, i, j + 1, i + 1, j + 1);
                    Tri(b, i, j, i + 1, j + 1, i + 1, j);
                }
                string name = $"Terrain_{ti}_{tj}";
                Mesh mesh = GeneratedAssets.SaveMesh(b, name);
                var go = new GameObject(name);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = worldMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
        }

        // ---- Variants & chunks ---------------------------------------------------------------------

        void BuildVariants()
        {
            pineKinds = new int[5];
            for (int i = 0; i < pineKinds.Length; i++)
            {
                MeshBuilder solid = new MeshBuilder(), cutout = new MeshBuilder();
                PropMeshes.Pine(solid, cutout, rng, 7f + i * 1.8f);
                pineKinds[i] = AddKind($"Pine_{i}", solid, cutout, 240f, true);
            }
            Pal[] autumn = { Pal.LeafGold, Pal.LeafOrange, Pal.LeafGold };
            autumnKinds = new int[3];
            for (int i = 0; i < autumnKinds.Length; i++)
            {
                MeshBuilder solid = new MeshBuilder(), cutout = new MeshBuilder();
                PropMeshes.AutumnTree(solid, cutout, rng, 6f + i * 1.3f, autumn[i]);
                autumnKinds[i] = AddKind($"Autumn_{i}", solid, cutout, 240f, true);
            }

            rockVariants = new MeshBuilder[6];
            rockMeshes = new Mesh[rockVariants.Length];
            for (int i = 0; i < rockVariants.Length; i++)
            {
                rockVariants[i] = new MeshBuilder();
                Pal family = i % 3 == 0 ? Pal.RockWarm : i % 3 == 1 ? Pal.Rock : Pal.RockDark;
                rockVariants[i].Rock(new Vector3(0f, 0.25f, 0f), new Vector3(1f, R(0.6f, 0.9f), R(0.85f, 1.15f)), rng, family, 0.3f, 0.6f, 0.3f);
                rockMeshes[i] = GeneratedAssets.SaveMesh(rockVariants[i], $"Rock_{i}");
            }

            Pal[] grassFamilies = { Pal.GrassGold, Pal.GrassOlive, Pal.GrassDry };
            grassKinds = new int[grassFamilies.Length, 2];
            for (int f = 0; f < grassFamilies.Length; f++)
            for (int v = 0; v < 2; v++)
            {
                var b = new MeshBuilder();
                PropMeshes.GrassClump(b, rng, grassFamilies[f]);
                grassKinds[f, v] = AddKind($"Grass_{grassFamilies[f]}_{v}", null, b, 60f, false);
            }
            flowerKinds = new int[3];
            wheatKinds = new int[3];
            for (int v = 0; v < 3; v++)
            {
                var flower = new MeshBuilder();
                PropMeshes.FlowerClump(flower, rng);
                flowerKinds[v] = AddKind($"Flowers_{v}", null, flower, 50f, false);
                var wheat = new MeshBuilder();
                PropMeshes.WheatClump(wheat, rng);
                wheatKinds[v] = AddKind($"Wheat_{v}", null, wheat, 110f, false);
            }
        }

        int AddKind(string name, MeshBuilder solid, MeshBuilder cutout, float cullDistance, bool shadows)
        {
            var meshes = new List<Mesh>();
            var materials = new List<Material>();
            if (solid != null) { meshes.Add(GeneratedAssets.SaveMesh(solid, "Scatter_" + name)); materials.Add(worldMaterial); }
            if (cutout != null) { meshes.Add(GeneratedAssets.SaveMesh(cutout, "Scatter_" + name + "_Cards")); materials.Add(foliageMaterial); }
            kinds.Add(new ScatterData.Kind { Name = name, Meshes = meshes.ToArray(), Materials = materials.ToArray(), CullDistance = cullDistance, Shadows = shadows });
            return kinds.Count - 1;
        }

        int GrassKind(float x, float z)
        {
            Pal family = GrassFamily(x, z);
            return grassKinds[family == Pal.GrassGold ? 0 : family == Pal.GrassOlive ? 1 : 2, rng.Next(2)];
        }

        void AddCard(int kind, float x, float z, float sink, float scale) =>
            scatter.Add((kind, new Vector3(x, SurfaceHeight(x, z) - sink, z), R(0f, 360f), scale));

        /// <summary>
        /// An instanced tree. Inside the walls it also gets a capsule, gathered with its cell's other trees on one object
        /// rather than one object per tree.
        /// </summary>
        void AddTree(int kind, Vector3 position, float yaw, float scale, bool collider, float radius = 0.35f)
        {
            scatter.Add((kind, position, yaw, scale));
            var key = new Vector2Int(Mathf.FloorToInt(position.x / 4f), Mathf.FloorToInt(position.z / 4f));
            if (!treeGrid.TryGetValue(key, out var list)) treeGrid[key] = list = new List<Vector2>();
            list.Add(new Vector2(position.x, position.z));
            if (!collider) return;

            var cell = new Vector2Int(ChunkX(position.x), ChunkZ(position.z));
            if (!treeColliderCells.TryGetValue(cell, out GameObject holder))
            {
                holder = new GameObject($"Tree Colliders {cell.x}_{cell.y}");
                holder.transform.SetParent(colliderRoot, false);
                holder.transform.position = new Vector3(MinX + cell.x * ChunkSize, 0f, MinZ + cell.y * ChunkSize);
                treeColliderCells[cell] = holder;
            }
            var capsule = holder.AddComponent<CapsuleCollider>();
            capsule.radius = radius * scale;
            capsule.height = 4f * scale;
            capsule.center = position - holder.transform.position + new Vector3(0f, 2f * scale, 0f);
        }

        bool Crowded(float x, float z, float spacing)
        {
            int cx = Mathf.FloorToInt(x / 4f), cz = Mathf.FloorToInt(z / 4f);
            var p = new Vector2(x, z);
            for (int i = -1; i <= 1; i++)
            for (int j = -1; j <= 1; j++)
                if (treeGrid.TryGetValue(new Vector2Int(cx + i, cz + j), out var list))
                    foreach (Vector2 t in list)
                        if ((t - p).sqrMagnitude < spacing * spacing) return true;
            return false;
        }

        static int ChunkX(float x) => Mathf.Clamp(Mathf.FloorToInt((x - MinX) / ChunkSize), 0, ChunksX - 1);
        static int ChunkZ(float z) => Mathf.Clamp(Mathf.FloorToInt((z - MinZ) / ChunkSize), 0, ChunksZ - 1);

        MeshBuilder DecorChunk(float x, float z) => decorChunks[ChunkX(x), ChunkZ(z)];

        void FlushChunks()
        {
            for (int x = 0; x < ChunksX; x++)
            for (int z = 0; z < ChunksZ; z++)
                CreateChunkObject(decorChunks[x, z], $"Decor_{x}_{z}", worldMaterial, ShadowCastingMode.On);
        }

        /// <summary>Writes the instances, grouped by 66 m cell and kind, and the renderer that draws them.</summary>
        void BuildScatter()
        {
            var bounds = kinds.Select(k =>
            {
                Bounds b = k.Meshes[0].bounds;
                foreach (Mesh m in k.Meshes) b.Encapsulate(m.bounds);
                return b;
            }).ToArray();
            var ordered = scatter.OrderBy(s => ChunkX(s.position.x) * 1000 + ChunkZ(s.position.z)).ThenBy(s => s.kind).ToList();
            var instances = new List<ScatterData.Instance>(ordered.Count);
            var groups = new List<ScatterData.Group>();
            for (int i = 0; i < ordered.Count; i++)
            {
                var s = ordered[i];
                Bounds local = bounds[s.kind];
                float reach = Mathf.Max(local.extents.x + Mathf.Abs(local.center.x), local.extents.z + Mathf.Abs(local.center.z)) * 1.42f * s.scale;
                var b = new Bounds(s.position + Vector3.up * (local.center.y * s.scale), new Vector3(reach * 2f, local.size.y * s.scale, reach * 2f));
                bool sameGroup = i > 0 && ordered[i - 1].kind == s.kind &&
                    ChunkX(ordered[i - 1].position.x) == ChunkX(s.position.x) && ChunkZ(ordered[i - 1].position.z) == ChunkZ(s.position.z);
                if (sameGroup)
                {
                    var g = groups[groups.Count - 1];
                    g.Count++;
                    g.Bounds.Encapsulate(b);
                    groups[groups.Count - 1] = g;
                }
                else groups.Add(new ScatterData.Group { Kind = s.kind, Bounds = b, Start = i, Count = 1 });
                instances.Add(new ScatterData.Instance { Position = s.position, Yaw = s.yaw, Scale = s.scale });
            }

            var data = GeneratedAssets.CreateOrReplaceScriptable<ScatterData>("Assets/Art/Generated/Scatter_Valley.asset");
            data.Kinds = kinds.ToArray();
            data.Groups = groups.ToArray();
            data.Instances = instances.ToArray();
            EditorUtility.SetDirty(data);
            var go = new GameObject("Scatter");
            go.transform.SetParent(root, false);
            SceneWiring.Set(go.AddComponent<ScatterRenderer>(), "data", data);
        }

        void CreateChunkObject(MeshBuilder builder, string name, Material material, ShadowCastingMode shadows)
        {
            if (builder.TriangleCount == 0) return;
            Mesh mesh = GeneratedAssets.SaveMesh(builder, name);
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows;
        }

        GameObject CreateMeshObject(string name, Mesh mesh, Material material, Vector3 position, Quaternion rotation, Transform parent, bool castShadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : root, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        BoxCollider AddBoxCollider(string name, Vector3 center, Quaternion rotation, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(colliderRoot, false);
            go.transform.SetPositionAndRotation(center, rotation);
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            return box;
        }

        // ---- The Old Toll Gate and the gate to Hollowgate --------------------------------------------------

        /// <summary>Pillars, walls, lintel, banners and lanterns of a gate facing south; the planks too when they never move.</summary>
        Vector3 BuildGateFrame(Vector2 at, bool fixedPlanks)
        {
            Vector3 p = Ground(at.x, at.y);
            var b = DecorChunk(p.x, p.z);
            b.Push(p, Quaternion.identity, Vector3.one);
            for (int i = -1; i <= 1; i += 2)
            {
                for (int row = 0; row < 9; row++)
                    PropMeshes.StoneBlock(b, rng, new Vector3(i * 4.2f, 0.5f + row * 1f, 0f), new Vector3(2.4f, 1f, 2.4f), row < 2);
                AddBoxCollider("Gate Pillar", p + new Vector3(i * 4.2f, 4.5f, 0f), Quaternion.identity, new Vector3(2.4f, 9f, 2.4f));
                for (int row = 0; row < 6; row++)
                    PropMeshes.StoneBlock(b, rng, new Vector3(i * 12f, 0.75f + row * 1.5f, 1f), new Vector3(14f, 1.5f, 1.6f), row < 2);
                AddBoxCollider("Gate Wall", p + new Vector3(i * 12f, 4.5f, 1f), Quaternion.identity, new Vector3(14f, 9f, 1.6f));
            }
            PropMeshes.StoneBlock(b, rng, new Vector3(0f, 9.5f, 0f), new Vector3(11.5f, 1.4f, 2.6f), false);
            if (fixedPlanks)
            {
                b.SetColor(Pal.WoodDark, 0.35f);
                for (int plank = 0; plank < 7; plank++)
                    b.ShadedBox(new Vector3(-2.55f + plank * 0.85f, 4.4f, 0.2f), new Vector3(0.8f, 8.8f, 0.3f), Pal.WoodDark, rng, 0.28f, 0.42f);
                b.ShadedBox(new Vector3(0f, 2.5f, -0.02f), new Vector3(6f, 0.4f, 0.1f), Pal.SteelDark, rng, 0.3f, 0.4f);
                b.ShadedBox(new Vector3(0f, 6.5f, -0.02f), new Vector3(6f, 0.4f, 0.1f), Pal.SteelDark, rng, 0.3f, 0.4f);
            }
            for (float x = -19f; x <= 19f; x += 2.2f)
            {
                if (Mathf.Abs(x) < 5.5f) continue;
                b.ShadedBox(new Vector3(x, 9.65f, 1f), new Vector3(1.3f, 1.3f, 1.8f), Pal.Stone, rng, 0.32f, 0.5f);
            }
            foreach (float side in new[] { -1f, 1f })
            {
                b.SetColor(Pal.WoodDark, 0.3f);
                b.Box(new Vector3(side * 4.2f, 7.7f, -1.45f), new Vector3(1.8f, 0.1f, 0.1f));
                for (int fold = 0; fold < 6; fold++)
                {
                    b.SetColor(fold % 2 == 0 ? Pal.ClothRed : Pal.ClothDark, 0.45f);
                    b.Box(new Vector3(side * 4.2f - 0.65f + fold * 0.26f, 5.8f, -1.45f - fold % 2 * 0.03f), new Vector3(0.27f, 3.7f, 0.04f));
                }
                b.SetColor(Pal.Brass, 0.6f);
                b.Box(new Vector3(side * 4.2f, 6.25f, -1.52f), new Vector3(0.12f, 1.25f, 0.035f));
                b.Box(new Vector3(side * 4.2f, 6.45f, -1.53f), new Vector3(0.7f, 0.12f, 0.035f));
                b.SetColor(Pal.SteelDark, 0.38f);
                b.Box(new Vector3(side * 3.7f, 3.2f, -1.55f), new Vector3(0.42f, 0.8f, 0.45f));
                b.SetColor(Pal.Lantern, 0.65f);
                b.Box(new Vector3(side * 3.7f, 3.45f, -1.8f), new Vector3(0.26f, 0.45f, 0.2f));
            }
            b.Pop();
            return p;
        }

        /// <summary>The old gate stays where the city portal used to be; its two leaves stay shut until the Cursed Knight falls.</summary>
        void BuildTollGate()
        {
            Vector3 p = BuildGateFrame(GateCenter, false);
            var gate = new GameObject("Old Toll Gate");
            gate.transform.SetParent(root, false);
            gate.transform.position = p;
            var barrier = gate.AddComponent<BoxCollider>();
            barrier.center = new Vector3(0f, 4.5f, 0.2f);
            barrier.size = new Vector3(6f, 9f, 0.6f);

            Transform Leaf(string name, float side)
            {
                // Built from its hinge at the pillar, so opening is one turn about the hinge.
                var leaf = new MeshBuilder();
                for (int plank = 0; plank < 4; plank++)
                    leaf.ShadedBox(new Vector3(-side * (0.37f + plank * 0.74f), 4.4f, 0.2f), new Vector3(0.7f, 8.8f, 0.3f), Pal.WoodDark, rng, 0.28f, 0.42f);
                foreach (float y in new[] { 2.5f, 6.5f })
                    leaf.ShadedBox(new Vector3(-side * 1.48f, y, -0.02f), new Vector3(2.96f, 0.4f, 0.1f), Pal.SteelDark, rng, 0.3f, 0.4f);
                var go = CreateMeshObject(name, GeneratedAssets.SaveMesh(leaf, name.Replace(' ', '_')), worldMaterial,
                    p + new Vector3(side * 2.96f, 0f, 0f), Quaternion.identity, gate.transform);
                return go.transform;
            }
            var tollGate = gate.AddComponent<TollGate>();
            SceneWiring.Set(tollGate, "requiredFlag", StoryDirector.DeadFlag("knight"));
            SceneWiring.Set(tollGate, "lockedMessage", GameText.GateBarred);
            SceneWiring.Set(tollGate, "leftDoor", Leaf("Toll Gate Left Leaf", -1f));
            SceneWiring.Set(tollGate, "rightDoor", Leaf("Toll Gate Right Leaf", 1f));
            SceneWiring.Set(tollGate, "barrier", barrier);
        }

        /// <summary>The gate to Hollowgate at the north end of the land: the portal the builder places is always open.</summary>
        void BuildCityGate(ValleyLayout layout)
        {
            layout.GatePosition = BuildGateFrame(CityGateCenter, true);
            layout.GateArrival = Ground(CityGateCenter.x, CityGateCenter.y - 5f) + Vector3.up * 0.1f;

            // Banners down the wall's south face and torches between them: the city's colours and the evening's first fires.
            // The wall's blocks lean a little, so rods and brackets reach into the stone instead of touching its face.
            Vector3 p = layout.GatePosition;
            var b = DecorChunk(p.x, p.z);
            b.Push(p, Quaternion.identity, Vector3.one);
            foreach (float side in new[] { -1f, 1f })
            {
                foreach (float x in new[] { 9.5f, 15.5f })
                {
                    b.SetColor(Pal.WoodDark, 0.3f);
                    b.Box(new Vector3(side * x, 8.6f, -0.45f), new Vector3(2f, 0.12f, 0.12f));
                    foreach (float end in new[] { -0.9f, 0.9f })
                        b.Box(new Vector3(side * x + end, 8.6f, 0.1f), new Vector3(0.08f, 0.08f, 1.1f));
                    for (int fold = 0; fold < 6; fold++)
                    {
                        b.SetColor(fold % 2 == 0 ? Pal.ClothRed : Pal.ClothDark, 0.42f);
                        b.Box(new Vector3(side * x - 0.75f + fold * 0.3f, 6.2f, -0.45f - fold % 2 * 0.03f), new Vector3(0.31f, 4.7f, 0.04f));
                    }
                    b.SetColor(Pal.Brass, 0.6f);
                    b.Box(new Vector3(side * x, 6.8f, -0.5f), new Vector3(0.7f, 0.7f, 0.04f));
                }
                foreach (float x in new[] { 7.5f, 12.5f, 17.5f })
                {
                    b.SetColor(Pal.SteelDark, 0.32f);
                    b.Box(new Vector3(side * x, 4.1f, -0.1f), new Vector3(0.1f, 0.1f, 1.4f));
                    b.Prism(new Vector3(side * x, 3.85f, -0.8f), 0.05f, 0.16f, 0.3f, 6);
                    b.SetColor(Pal.FireOrange, 0.8f);
                    b.Prism(new Vector3(side * x, 4.15f, -0.8f), 0.14f, 0.02f, 0.42f, 5, false);
                    b.SetColor(Pal.FireYellow, 0.9f);
                    b.Prism(new Vector3(side * x, 4.15f, -0.8f), 0.07f, 0f, 0.28f, 4, false);
                }
            }
            b.Pop();
        }

        // ---- Ruined chapel ---------------------------------------------------------------------------

        void BuildRuins(ValleyLayout layout)
        {
            var b = new MeshBuilder();
            float baseY = SurfaceHeight(RuinsCenter.x, RuinsCenter.y) - 0.3f;
            float floorTop = baseY + 0.45f;
            const float rowHeight = 0.55f;

            void Wall(Vector2 from, Vector2 to, int maxRows, int wallIndex)
            {
                Vector2 dir = (to - from).normalized;
                float length = Vector2.Distance(from, to);
                Quaternion rotation = Quaternion.FromToRotation(Vector3.right, new Vector3(dir.x, 0f, dir.y));
                for (int row = 0; row < maxRows; row++)
                {
                    float s = row % 2 == 0 ? 0f : -0.55f;
                    while (s < length)
                    {
                        float blockLength = R(0.9f, 1.25f);
                        float start = Mathf.Max(s, 0f), end = Mathf.Min(s + blockLength, length);
                        float t = (start + end) * 0.5f / length;
                        float silhouette = Mathf.PerlinNoise(t * 2.6f + wallIndex * 3.1f, wallIndex * 1.7f);
                        int rowsHere = Mathf.RoundToInt(Mathf.Lerp(1f, maxRows, Mathf.Clamp01(silhouette * 1.5f - 0.15f)));
                        if (end - start > 0.25f && row < rowsHere && !(row == rowsHere - 1 && R(0f, 1f) < 0.25f))
                        {
                            Vector2 mid = from + dir * ((start + end) * 0.5f);
                            b.Push(new Vector3(mid.x, baseY + row * rowHeight + rowHeight * 0.5f, mid.y), rotation, Vector3.one);
                            PropMeshes.StoneBlock(b, rng, Vector3.zero, new Vector3(end - start - 0.04f, rowHeight - 0.03f, 0.9f), row == 0 || R(0f, 1f) < 0.2f);
                            b.Pop();
                        }
                        s += blockLength;
                    }
                }
            }

            Wall(new Vector2(-8f, 20f), new Vector2(-8f, 44f), 8, 0);
            Wall(new Vector2(8f, 20f), new Vector2(8f, 44f), 7, 1);
            Wall(new Vector2(-8f, 44f), new Vector2(8f, 44f), 10, 2);
            Wall(new Vector2(-8f, 20f), new Vector2(-3.2f, 20f), 6, 3);
            Wall(new Vector2(3.2f, 20f), new Vector2(8f, 20f), 4, 4);

            // Doorway pillars and a broken lintel.
            void Pillar(float x, float z, int rows)
            {
                for (int r = 0; r < rows; r++)
                    PropMeshes.StoneBlock(b, rng, new Vector3(x, baseY + r * rowHeight + rowHeight * 0.5f, z), new Vector3(1.15f, rowHeight - 0.03f, 1.15f), r < 2);
                PropMeshes.StoneBlock(b, rng, new Vector3(x, baseY + rows * rowHeight + 0.12f, z), new Vector3(1.4f, 0.24f, 1.4f), false);
            }
            Pillar(-2.6f, 20f, 9);
            Pillar(2.6f, 20f, 6);
            float lintelY = baseY + 9 * rowHeight + 0.5f;
            PropMeshes.StoneBlock(b, rng, new Vector3(-1.9f, lintelY, 20f), new Vector3(1.6f, 0.5f, 1.0f), false);
            PropMeshes.StoneBlock(b, rng, new Vector3(-0.5f, lintelY - 0.05f, 20f), new Vector3(1.2f, 0.5f, 0.95f), true);

            // Interior columns of stacked drums; some broken, one fallen.
            int[] leftDrums = { 8, 3, 6 }, rightDrums = { 2, 8, 5 };
            float[] columnZ = { 26f, 32f, 38f };
            for (int i = 0; i < columnZ.Length; i++)
            {
                Column(b, -4.6f, columnZ[i], leftDrums[i], floorTop);
                Column(b, 4.6f, columnZ[i], rightDrums[i], floorTop);
            }
            for (int d = 0; d < 4; d++)
            {
                b.SetColor(d % 2 == 0 ? Pal.Stone : Pal.StoneMoss, R(0.35f, 0.55f));
                b.Push(new Vector3(1.2f + d * 0.68f, floorTop + 0.42f, 29.5f + d * 0.15f), Quaternion.Euler(R(-4f, 4f), 8f + R(-5f, 5f), 90f), Vector3.one);
                b.Prism(new Vector3(0f, -0.32f, 0f), 0.42f, 0.42f, 0.62f, 8);
                b.Pop();
            }

            // Flagstone floor with gaps.
            for (float x = -7f; x <= 7.01f; x += 1.4f)
            for (float z = 21f; z <= 43.01f; z += 1.4f)
            {
                if (R(0f, 1f) < 0.22f) continue;
                b.Push(new Vector3(x + R(-0.08f, 0.08f), floorTop - 0.09f + R(-0.03f, 0.02f), z + R(-0.08f, 0.08f)),
                    Quaternion.Euler(R(-1.5f, 1.5f), R(-6f, 6f), R(-1.5f, 1.5f)), Vector3.one);
                b.ShadedBox(Vector3.zero, new Vector3(1.32f, 0.18f, 1.32f), R(0f, 1f) < 0.35f ? Pal.StoneMoss : Pal.Stone, rng, 0.3f, 0.5f);
                b.Pop();
            }

            // Altar dais at the north end.
            b.ShadedBox(new Vector3(0f, floorTop + 0.15f, 42.1f), new Vector3(6.2f, 0.3f, 3.0f), Pal.Stone, rng, 0.35f, 0.45f);
            b.ShadedBox(new Vector3(0f, floorTop + 0.45f, 42.5f), new Vector3(4.4f, 0.3f, 2.2f), Pal.Stone, rng, 0.4f, 0.5f);
            b.ShadedBox(new Vector3(0f, floorTop + 1.1f, 42.9f), new Vector3(2.3f, 1.0f, 1.0f), Pal.StoneMoss, rng, 0.35f, 0.5f);
            b.SetColor(Pal.ClothRed, 0.3f);
            b.Box(new Vector3(0f, floorTop + 1.62f, 42.9f), new Vector3(1.6f, 0.04f, 0.7f));

            // Rubble.
            for (int i = 0; i < 46; i++)
            {
                float side = R(0f, 1f);
                Vector2 p = side < 0.5f
                    ? new Vector2(R(-10.5f, 10.5f), R(17f, 47f))
                    : new Vector2(R(-6.5f, 6.5f), R(22f, 41f));
                if (Mathf.Abs(p.x) < 2f && p.y < 30f) continue; // keep the aisle clear
                float s = R(0.35f, 0.85f);
                float y = side < 0.5f ? SurfaceHeight(p.x, p.y) : floorTop;
                b.Push(new Vector3(p.x, y + s * 0.25f, p.y), Quaternion.Euler(R(-25f, 25f), R(0f, 360f), R(-25f, 25f)), Vector3.one);
                b.ShadedBox(Vector3.zero, new Vector3(s * R(1f, 1.6f), s * 0.6f, s), R(0f, 1f) < 0.3f ? Pal.StoneMoss : Pal.Stone, rng, 0.25f, 0.5f);
                b.Pop();
            }

            Mesh mesh = GeneratedAssets.SaveMesh(b, "Ruins");
            var ruins = CreateMeshObject("Ruins", mesh, worldMaterial, Vector3.zero, Quaternion.identity, root);
            ruins.AddComponent<MeshCollider>().sharedMesh = mesh;

            // Banners flanking the doorway.
            var bannerBuilder = new MeshBuilder();
            foreach (float x in new[] { -4.4f, 4.4f })
            {
                Vector3 p = Ground(x, 18.4f);
                bannerBuilder.Push(p, Quaternion.Euler(0f, R(-8f, 8f), 0f), Vector3.one);
                PropMeshes.Banner(bannerBuilder, rng, 3.6f);
                bannerBuilder.Pop();
                AddBoxCollider("Banner Pole", p + Vector3.up * 1.8f, Quaternion.identity, new Vector3(0.2f, 3.6f, 0.2f));
            }
            Mesh bannerMesh = GeneratedAssets.SaveMesh(bannerBuilder, "Banners");
            CreateMeshObject("Banners", bannerMesh, worldMaterial, Vector3.zero, Quaternion.identity, root);

            layout.ChestPosition = new Vector3(0f, floorTop, 39.4f);
            layout.ChestYaw = 0f;
        }

        void Column(MeshBuilder b, float x, float z, int drums, float floorTop)
        {
            b.ShadedBox(new Vector3(x, floorTop + 0.15f, z), new Vector3(1.3f, 0.3f, 1.3f), Pal.Stone, rng, 0.3f, 0.45f);
            float y = floorTop + 0.3f;
            for (int d = 0; d < drums; d++)
            {
                b.SetColor(d < 2 || R(0f, 1f) < 0.25f ? Pal.StoneMoss : Pal.Stone, R(0.35f, 0.58f));
                b.Push(new Vector3(x + R(-0.03f, 0.03f), y, z + R(-0.03f, 0.03f)), Quaternion.Euler(0f, R(0f, 45f), 0f), Vector3.one);
                b.Prism(Vector3.zero, 0.42f, 0.4f, 0.6f, 8);
                b.Pop();
                y += 0.6f;
            }
            if (drums >= 7)
                b.ShadedBox(new Vector3(x, y + 0.15f, z), new Vector3(1.2f, 0.3f, 1.2f), Pal.Stone, rng, 0.4f, 0.55f);
        }

        // ---- Sword graveyard ------------------------------------------------------------------------

        void BuildGraveyard()
        {
            var swordBuilder = new MeshBuilder();
            PropMeshes.Sword(swordBuilder, Pal.Steel, 0.4f);
            var rustyBuilder = new MeshBuilder();
            PropMeshes.Sword(rustyBuilder, Pal.RockWarm, 0.35f);

            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 5; col++)
            {
                if (row == 1 && col == 2) continue; // centre reserved for the lore blade
                float x = GraveyardCenter.x - 5.2f + col * 2.6f + R(-0.4f, 0.4f);
                float z = GraveyardCenter.y - 3.6f + row * 3.6f + R(-0.4f, 0.4f);
                float y = SurfaceHeight(x, z);

                // Grave mound in front of each blade.
                var chunk = DecorChunk(x, z);
                chunk.Push(new Vector3(x, y - 0.2f, z - 0.9f), Quaternion.Euler(0f, R(-8f, 8f), 0f), Vector3.one);
                chunk.Rock(Vector3.zero, new Vector3(1.0f, 0.5f, 1.8f), rng, Pal.DirtDark, 0.35f, 0.55f, 0.12f);
                chunk.Pop();

                if (R(0f, 1f) < 0.25f)
                {
                    chunk.Push(new Vector3(x, y - 0.05f, z + 0.3f), Quaternion.Euler(0f, R(-10f, 10f), 0f), Vector3.one);
                    PropMeshes.Gravestone(chunk, rng);
                    chunk.Pop();
                    continue;
                }

                float scale = R(2.1f, 3.0f);   // giant ancient blades
                var rotation = Quaternion.Euler(R(-18f, 18f), R(0f, 360f), R(-14f, 14f)) * Quaternion.Euler(180f, 0f, 0f);
                var position = new Vector3(x, y + 0.75f * scale, z);
                chunk.Append(R(0f, 1f) < 0.5f ? swordBuilder : rustyBuilder, Matrix4x4.TRS(position, rotation, Vector3.one * scale));
            }

            // Dead tree watching over the graves.
            var tree = DecorChunk(GraveyardCenter.x - 7f, GraveyardCenter.y + 5f);
            tree.Push(Ground(GraveyardCenter.x - 7f, GraveyardCenter.y + 5f), Quaternion.Euler(0f, 40f, 0f), Vector3.one);
            PropMeshes.DeadTree(tree, rng, 5.5f);
            tree.Pop();
            AddBoxCollider("Dead Tree", Ground(GraveyardCenter.x - 7f, GraveyardCenter.y + 5f) + Vector3.up * 2f, Quaternion.identity, new Vector3(0.45f, 4f, 0.45f));

            // The Oathbreaker's Blade: interactive lore object.
            var loreBuilder = new MeshBuilder();
            PropMeshes.Sword(loreBuilder, Pal.Steel, 0.6f);
            Mesh loreMesh = GeneratedAssets.SaveMesh(loreBuilder, "LoreSword");
            Vector3 lorePos = Ground(GraveyardCenter.x, GraveyardCenter.y) + Vector3.up * 2.75f;
            var lore = CreateMeshObject("Oathbreaker's Blade", loreMesh, worldMaterial, lorePos,
                Quaternion.Euler(8f, 30f, 6f) * Quaternion.Euler(180f, 0f, 0f), root);
            lore.transform.localScale = Vector3.one * 3.6f;
            var loreCollider = lore.AddComponent<BoxCollider>();
            loreCollider.center = new Vector3(0f, 0.35f, 0f);
            loreCollider.size = new Vector3(0.3f, 1.3f, 0.12f);
            var inspect = lore.AddComponent<LoreInspectable>();
            SceneWiring.Set(inspect, "prompt", GameText.InspectBlade);
            SceneWiring.Set(inspect, "loreText", GameText.BladeLore);

            var stone = DecorChunk(GraveyardCenter.x, GraveyardCenter.y);
            stone.Rock(Ground(GraveyardCenter.x, GraveyardCenter.y) + Vector3.down * 0.1f, new Vector3(1.4f, 0.6f, 1.4f), rng, Pal.Stone, 0.3f, 0.5f, 0.15f);
        }

        // ---- Camp ------------------------------------------------------------------------------------

        void BuildCamp()
        {
            Vector3 fire = Ground(CampCenter.x, CampCenter.y);
            var chunk = DecorChunk(fire.x, fire.z);
            chunk.Push(fire, Quaternion.identity, Vector3.one);
            PropMeshes.Campfire(chunk, rng);
            chunk.Pop();

            // Log seats.
            foreach (var (offset, yaw) in new[] { (new Vector2(-2.3f, -1.6f), 35f), (new Vector2(2.1f, 2.0f), 215f) })
            {
                Vector3 p = Ground(CampCenter.x + offset.x, CampCenter.y + offset.y);
                chunk.SetColor(Pal.Bark, 0.35f);
                chunk.Push(p + Vector3.up * 0.22f, Quaternion.Euler(0f, yaw, 90f), Vector3.one);
                chunk.Prism(new Vector3(0f, -0.8f, 0f), 0.24f, 0.22f, 1.6f, 7);
                chunk.Pop();
                AddBoxCollider("Log Seat", p + Vector3.up * 0.22f, Quaternion.Euler(0f, yaw, 0f), new Vector3(0.45f, 0.45f, 1.6f));
            }

            Vector3 tentPos = Ground(CampCenter.x + 4.5f, CampCenter.y + 4.5f);
            chunk.Push(tentPos, Quaternion.Euler(0f, 225f, 0f), Vector3.one);
            PropMeshes.Tent(chunk, rng);
            chunk.Pop();
            AddBoxCollider("Tent", tentPos + Vector3.up * 0.8f, Quaternion.Euler(0f, 225f, 0f), new Vector3(2.0f, 1.6f, 2.8f));

            Vector3 bannerPos = Ground(CampCenter.x - 7f, CampCenter.y + 2.5f);
            chunk.Push(bannerPos, Quaternion.Euler(0f, 160f, 0f), Vector3.one);
            PropMeshes.Banner(chunk, rng, 3f);
            chunk.Pop();

            // Flames (unlit fire material) and flickering light.
            var flameBuilder = new MeshBuilder();
            PropMeshes.Flames(flameBuilder, rng);
            Mesh flames = GeneratedAssets.SaveMesh(flameBuilder, "CampfireFlames");
            var flameObject = CreateMeshObject("Campfire", flames, worldMaterial, fire, Quaternion.identity, root, false);
            var lightObject = new GameObject("Fire Light");
            lightObject.transform.SetParent(flameObject.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.55f, 0.25f);
            light.range = 11f;
            light.intensity = 2.4f;
            light.shadows = LightShadows.None;
            lightObject.AddComponent<LightFlicker>();
        }

        // ---- Fences ------------------------------------------------------------------------------------

        void BuildFences()
        {
            Vector2[] main = roads[0].Points;
            var left = new List<Vector3>();
            var right = new List<Vector3>();
            float travelled = 0f, next = 7f;
            for (int i = 1; i < main.Length && next < 38f; i++)
            {
                float segment = Vector2.Distance(main[i - 1], main[i]);
                while (travelled + segment >= next && next < 38f)
                {
                    float t = (next - travelled) / segment;
                    Vector2 p = Vector2.Lerp(main[i - 1], main[i], t);
                    Vector2 tangent = (main[i] - main[i - 1]).normalized;
                    Vector2 normal = new Vector2(-tangent.y, tangent.x);
                    Vector2 l = p + normal * 3.4f, r = p - normal * 3.4f;
                    left.Add(Ground(l.x, l.y));
                    right.Add(Ground(r.x, r.y));
                    next += 2.5f;
                }
                travelled += segment;
            }
            FenceLine(left, 0);
            FenceLine(right, 2);

            // Short paddock line behind the camp.
            var paddock = new List<Vector3>();
            for (int i = 0; i < 6; i++)
                paddock.Add(Ground(CampCenter.x - 6f + i * 2.4f, CampCenter.y + 8.5f + Mathf.Sin(i) * 0.4f));
            FenceLine(paddock, 1);
        }

        void FenceLine(List<Vector3> posts, int phase)
        {
            for (int i = 0; i < posts.Count - 1; i++)
            {
                if ((i + phase) % 5 == 4) continue; // missing section
                Vector3 a = posts[i], c = posts[i + 1];
                Vector3 delta = c - a;
                Quaternion rotation = Quaternion.FromToRotation(Vector3.right, new Vector3(delta.x, delta.y * 0.6f, delta.z).normalized);
                bool broken = (i + phase) % 3 == 1;
                var chunk = DecorChunk(a.x, a.z);
                chunk.Push(a + Vector3.down * 0.05f, rotation, Vector3.one);
                PropMeshes.FenceSection(chunk, rng, delta.magnitude, broken);
                chunk.Pop();
                AddBoxCollider("Fence", (a + c) * 0.5f + Vector3.up * 0.6f, rotation, new Vector3(delta.magnitude, 1.2f, 0.2f));
            }
        }

        // ---- Trees, rocks, foliage ------------------------------------------------------------------

        void ScatterTrees()
        {
            int placed = 0;
            for (int attempt = 0; attempt < 2200 && placed < 280; attempt++)
            {
                float x = R(-128f, 128f), z = R(-128f, 100f);
                float d = SuperDistance(x, z);
                if (d > 132f) continue;
                float edge = SmoothStep(48f, 95f, d);
                float cluster = SmoothStep(0.56f, 0.72f, Mathf.PerlinNoise(x * 0.03f + noiseB.x, z * 0.03f + noiseB.y));
                if (R(0f, 1f) > Mathf.Max(edge * 0.85f, cluster * 0.75f)) continue;
                if (PathDistance(x, z) < 5f || InClearing(x, z, 2f)) continue;
                if (SurfaceNormal(x, z).y < 0.75f || Crowded(x, z, 3.2f)) continue;

                float scale = R(0.8f, 1.3f);
                Vector3 position = new Vector3(x, SurfaceHeight(x, z) - 0.15f, z);
                // Golden broadleaf trees mix in near the hamlet and in the mid valley, pines dominate the rim.
                bool autumn = d < 80f && (Vector2.Distance(new Vector2(x, z), HamletCenter) < 45f || R(0f, 1f) < 0.18f);
                int kind = autumn ? autumnKinds[rng.Next(autumnKinds.Length)] : pineKinds[rng.Next(pineKinds.Length)];
                AddTree(kind, position, R(0f, 360f), scale, Walkable(x, z, 3f));
                placed++;
            }
        }

        void ScatterRocks()
        {
            // Boulders across the valley floor.
            int placed = 0;
            for (int attempt = 0; attempt < 600 && placed < 70; attempt++)
            {
                float x = R(-112f, 112f), z = R(-112f, 112f);
                if (PathDistance(x, z) < 3.5f || InClearing(x, z, 1f)) continue;
                float scale = Mathf.Pow(R(0f, 1f), 2f) * 2.6f + 0.6f;
                PlaceRock(x, z, scale, true);
                placed++;
            }

            // Craggy outcrops along the rim hills, short of the pass and the road through it.
            placed = 0;
            for (int attempt = 0; attempt < 500 && placed < 55; attempt++)
            {
                float x = R(-135f, 135f), z = R(-135f, 135f);
                float d = SuperDistance(x, z);
                if (d < 88f || d > 136f || z > 100f || PathDistance(x, z) < 6f || InClearing(x, z, 3f)) continue;
                PlaceRock(x, z, R(3.5f, 9f), d < WallHalf + 6f);
                placed++;
            }

            // Small stones near paths.
            for (int i = 0; i < 120; i++)
            {
                float x = R(-100f, 100f), z = R(-100f, 100f);
                float pd = PathDistance(x, z);
                if (pd < 2.2f || pd > 4.5f || PathMask(x, z) > 0.2f) continue;
                var chunk = DecorChunk(x, z);
                chunk.Append(rockVariants[rng.Next(rockVariants.Length)],
                    Matrix4x4.TRS(new Vector3(x, SurfaceHeight(x, z) - 0.05f, z), Quaternion.Euler(0f, R(0f, 360f), 0f), Vector3.one * R(0.2f, 0.45f)));
            }
        }

        void PlaceRock(float x, float z, float scale, bool collider)
        {
            int variant = rng.Next(rockVariants.Length);
            var scale3 = new Vector3(scale * R(0.85f, 1.2f), scale * R(0.8f, 1.1f), scale * R(0.85f, 1.2f));
            var position = new Vector3(x, SurfaceHeight(x, z) - 0.12f * scale, z);
            var rotation = Quaternion.Euler(R(-6f, 6f), R(0f, 360f), R(-6f, 6f));
            DecorChunk(x, z).Append(rockVariants[variant], Matrix4x4.TRS(position, rotation, scale3));
            if (!collider || scale < 0.7f) return;

            var go = new GameObject("Rock");
            go.transform.SetParent(colliderRoot, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = scale3;
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = rockMeshes[variant];
            mc.convex = true;
        }

        bool BlockedForFoliage(float x, float z)
        {
            if (Mathf.Abs(x) < 9f && z > 19f && z < 45.5f) return true;                       // ruins interior
            if (Vector2.Distance(new Vector2(x, z), HamletCenter) < 8f) return true;           // hamlet square
            foreach (var house in houseFootprints)
                if (Vector2.Distance(new Vector2(x, z), house) < 5.5f) return true;
            if (Vector2.Distance(new Vector2(x, z), new Vector2(-34f, -53f)) < 4f) return true;
            return Vector2.Distance(new Vector2(x, z), WindmillCenter) < 3f;
        }

        void ScatterFoliage()
        {
            int clumps = 0;
            for (int attempt = 0; attempt < 20000 && clumps < 6500; attempt++)
            {
                float x = R(-110f, 110f), z = R(-110f, 100f);
                if (PathMask(x, z) > 0.3f || BlockedForFoliage(x, z) || FieldMask(x, z) > 0f) continue;
                if (SurfaceNormal(x, z).y < 0.86f) continue;
                float density = 0.3f + 0.7f * Mathf.PerlinNoise(x * 0.07f + noiseC.x, z * 0.07f + noiseA.y);
                if (R(0f, 1f) > density) continue;
                AddCard(GrassKind(x, z), x, z, 0.03f, R(0.8f, 1.4f));
                clumps++;
            }

            // Flower patches across the valley, and a dense meadow around the sword graveyard.
            int flowers = 0;
            for (int attempt = 0; attempt < 14000 && flowers < 2200; attempt++)
            {
                bool meadow = attempt % 2 == 0;
                float x, z;
                if (meadow)
                {
                    float a = R(0f, Mathf.PI * 2f), r = Mathf.Sqrt(R(0f, 1f)) * 17f;
                    x = GraveyardCenter.x + Mathf.Cos(a) * r;
                    z = GraveyardCenter.y + Mathf.Sin(a) * r;
                }
                else
                {
                    x = R(-100f, 100f);
                    z = R(-100f, 100f);
                    if (Mathf.PerlinNoise(x * 0.09f + noiseA.y, z * 0.09f + noiseC.x) < 0.58f) continue;
                }
                if (PathMask(x, z) > 0.2f || BlockedForFoliage(x, z) || FieldMask(x, z) > 0f || SurfaceNormal(x, z).y < 0.88f) continue;
                AddCard(flowerKinds[rng.Next(flowerKinds.Length)], x, z, 0.02f, R(0.9f, 1.3f));
                flowers++;
            }

            // Wheat fields in loose rows.
            for (float x = 40f; x < 64f; x += 1.1f)
            for (float z = -36f; z < 0f; z += 1.0f)
            {
                float jx = x + R(-0.35f, 0.35f), jz = z + R(-0.35f, 0.35f);
                if (FieldMask(jx, jz) <= 0f || PathMask(jx, jz) > 0.2f || BlockedForFoliage(jx, jz) || R(0f, 1f) < 0.08f) continue;
                AddCard(wheatKinds[rng.Next(wheatKinds.Length)], jx, jz, 0.05f, R(0.9f, 1.15f));
            }
        }

        /// <summary>
        /// The north land's plain cover: Feketefenyves dense in the west, loose clusters and a band of pines under the rim,
        /// wheat in plots across the golden fields, grass thickest along the roads where people travel. The places A2 builds
        /// keep their clearings.
        /// </summary>
        void ScatterNorth()
        {
            int trees = 0;
            for (int attempt = 0; attempt < 40000 && trees < 2900; attempt++)
            {
                float x = R(-262f, 262f), z = R(104f, 612f);
                float outside = NorthOutside(x, z), forest = ForestMask(x, z);
                if (outside > 55f || z < 112f && Mathf.Abs(x) < 128f) continue;   // the ridge's steep north face
                float rimBand = SmoothStep(8f, 35f, outside);
                float cluster = SmoothStep(0.6f, 0.76f, Mathf.PerlinNoise(x * 0.025f + noiseB.x, z * 0.025f + noiseB.y));
                float chance = Mathf.Max(forest * 0.95f, Mathf.Max(rimBand * 0.75f, cluster * 0.5f)) * (1f - NorthFieldMask(x, z)) * (1f - AshMask(x, z));
                if (R(0f, 1f) > chance) continue;
                if (PathDistance(x, z) < 5f || InClearing(x, z, 2f) || InLake(x, z, 8f) || SurfaceNormal(x, z).y < 0.75f) continue;
                if (Crowded(x, z, forest > 0.5f ? 3f : 3.6f)) continue;
                // A belt of golden birches along the lake's east shore, a few in the open fields' margins.
                bool birchBelt = InLake(x, z, 30f) && x > LakeCenter.x;
                bool autumn = birchBelt || forest < 0.2f && R(0f, 1f) < 0.22f;
                int kind = autumn ? autumnKinds[rng.Next(autumnKinds.Length)] : pineKinds[rng.Next(pineKinds.Length)];
                AddTree(kind, new Vector3(x, SurfaceHeight(x, z) - 0.15f, z), R(0f, 360f), R(0.85f, 1.35f), Walkable(x, z, 3f));
                trees++;
            }

            bool Open(float x, float z, float pathLimit) =>
                PathMask(x, z) <= pathLimit && !InClearing(x, z, 0f) && !InLake(x, z, 2f) && AshMask(x, z) < 0.3f && SurfaceNormal(x, z).y >= 0.86f;

            int grass = 0;
            for (int attempt = 0; attempt < 40000 && grass < 8500; attempt++)
            {
                float x = R(-245f, 245f), z = R(108f, 596f);
                if (PathDistance(x, z) > 40f && R(0f, 1f) > 0.3f) continue;
                if (NorthFieldMask(x, z) > 0.3f || ForestMask(x, z) > 0.6f || !Open(x, z, 0.3f)) continue;
                AddCard(GrassKind(x, z), x, z, 0.03f, R(0.9f, 1.5f));
                grass++;
            }

            int flowers = 0;
            for (int attempt = 0; attempt < 12000 && flowers < 1300; attempt++)
            {
                float x = R(-245f, 245f), z = R(108f, 596f);
                if (Mathf.PerlinNoise(x * 0.09f + noiseA.y, z * 0.09f + noiseC.x) < 0.62f) continue;
                if (NorthFieldMask(x, z) > 0.1f || ForestMask(x, z) > 0.3f || !Open(x, z, 0.2f)) continue;
                AddCard(flowerKinds[rng.Next(flowerKinds.Length)], x, z, 0.02f, R(0.9f, 1.3f));
                flowers++;
            }

            // Plots of 34 x 24 m between grassy balks; about a third lie fallow.
            int wheat = 0;
            for (float x = -100f; x < 200f && wheat < 5800; x += 1.9f)
            for (float z = 132f; z < 290f && wheat < 5800; z += 1.9f)
            {
                float jx = x + R(-0.5f, 0.5f), jz = z + R(-0.5f, 0.5f);
                int plotX = Mathf.FloorToInt((jx + 100f) / 39f), plotZ = Mathf.FloorToInt((jz - 132f) / 29f);
                bool inPlot = (jx + 100f) - plotX * 39f < 34f && (jz - 132f) - plotZ * 29f < 24f;
                bool fallow = Mathf.PerlinNoise(plotX * 0.73f + noiseA.x, plotZ * 0.61f + noiseB.y) < 0.38f;
                if (!inPlot || fallow || NorthFieldMask(jx, jz) < 0.9f || !Open(jx, jz, 0.05f)) continue;
                AddCard(wheatKinds[rng.Next(wheatKinds.Length)], jx, jz, 0.05f, R(0.9f, 1.2f));
                wheat++;
            }
        }


        // ---- Hamlet, windmill and road details ---------------------------------------------------------

        readonly List<Vector2> houseFootprints = new List<Vector2>();

        void BuildHamlet(ValleyLayout layout)
        {
            var houses = new (Vector2 pos, float yaw, float w, float d, float h, bool tiles)[]
            {
                (new Vector2(34f, -23f), 180f, 7f, 5f, 3.0f, true),
                (new Vector2(44f, -13f), 90f, 6f, 4.6f, 2.8f, false),
                (new Vector2(26f, -5f), -37f, 8f, 5.2f, 3.2f, true),
            };
            for (int i = 0; i < houses.Length; i++)
            {
                var h = houses[i];
                houseFootprints.Add(h.pos);
                Vector3 p = Ground(h.pos.x, h.pos.y) + Vector3.down * 0.15f;
                var rotation = Quaternion.Euler(0f, h.yaw, 0f);
                var chunk = DecorChunk(p.x, p.z);
                chunk.Push(p, rotation, Vector3.one);
                PropMeshes.House(chunk, rng, h.w, h.d, h.h, h.tiles);
                chunk.Pop();
                AddBoxCollider("House", p + Vector3.up * (0.6f + h.h) * 0.5f, rotation, new Vector3(h.w + 0.3f, 0.6f + h.h + 0.4f, h.d + 0.3f));
                AddBoxCollider("House Roof", p + Vector3.up * (0.6f + h.h + h.d * 0.25f), rotation, new Vector3(h.w + 0.6f, h.d * 0.5f, h.d * 0.6f));

                // Portals sit just in front of the painted doors; the return point faces out into the yard.
                Vector3 door = p + rotation * new Vector3(-h.w * 0.12f, 0f, -h.d * 0.5f - 0.34f);
                door.y = SurfaceHeight(door.x, door.z);
                Vector3 arrival = p + rotation * new Vector3(-h.w * 0.12f, 0f, -h.d * 0.5f - 1.75f);
                arrival.y = SurfaceHeight(arrival.x, arrival.z) + 0.1f;
                layout.Homes.Add((door, arrival, h.yaw));

                // Each cottage has a sheltered threshold, a flower box and its own work left outside.
                chunk.Push(p, rotation, Vector3.one);
                float doorX = -h.w * 0.12f, front = -h.d * 0.5f;
                for (int board = 0; board < 5; board++)
                {
                    chunk.SetColor(Pal.Wood, 0.44f);
                    chunk.Box(new Vector3(doorX - 0.8f + board * 0.4f, 0.09f, front - 0.5f), new Vector3(0.38f, 0.12f, 0.85f));
                }
                foreach (float side in new[] { -1f, 1f })
                {
                    chunk.SetColor(Pal.WoodDark, 0.34f);
                    chunk.Box(new Vector3(doorX + side * 1.05f, 1.45f, front - 1.0f), new Vector3(0.1f, 2.9f, 0.1f));
                }
                for (int strip = 0; strip < 8; strip++)
                {
                    chunk.SetColor(i == 0 ? Pal.ClothRed : i == 1 ? Pal.Thatch : Pal.ClothDark, strip % 2 == 0 ? 0.46f : 0.57f);
                    chunk.SetLayer(TexLayer.Cloth, UVMode.Triplanar, 0.7f);
                    float x = doorX - 1.2f + strip * 0.3f;
                    chunk.QuadOutward(new Vector3(x, 3.03f, front + 0.08f), new Vector3(x + 0.3f, 3.03f, front + 0.08f),
                        new Vector3(x + 0.3f, 2.75f, front - 1.25f), new Vector3(x, 2.75f, front - 1.25f), new Vector3(doorX, 0f, front));
                    chunk.Box(new Vector3(x + 0.15f, 2.65f, front - 1.25f), new Vector3(0.3f, 0.24f, 0.035f));
                }
                chunk.SetColor(Pal.Wood, 0.35f);
                Vector3 planter = new Vector3(h.w * 0.22f, 1.05f, front - 0.16f);
                chunk.Box(planter, new Vector3(1.1f, 0.28f, 0.4f));
                for (int flower = 0; flower < 5; flower++)
                {
                    chunk.SetColor(flower % 2 == 0 ? Pal.FlowerRed : Pal.FlowerYellow, 0.58f);
                    chunk.Prism(planter + new Vector3(-0.4f + flower * 0.2f, 0.17f, 0f), 0.1f, 0.04f, 0.25f, 5);
                }
                chunk.Pop();
                foreach (float side in new[] { -1f, 1f })
                    AddBoxCollider("Cottage Porch Post", p + rotation * new Vector3(doorX + side * 1.05f, 1.45f, front - 1f), rotation, new Vector3(0.1f, 2.9f, 0.1f));

                // Barrels and crates by each house.
                for (int k = 0; k < 3; k++)
                {
                    Vector3 local = new Vector3(h.w * 0.5f + 0.9f, 0f, R(-h.d * 0.4f, h.d * 0.4f) + k * 0.3f);
                    Vector3 wp = p + rotation * local;
                    wp.y = SurfaceHeight(wp.x, wp.z) - 0.02f;
                    var c = DecorChunk(wp.x, wp.z);
                    c.Push(wp, Quaternion.Euler(0f, R(0f, 360f), 0f), Vector3.one);
                    if (k == 0) PropMeshes.Crate(c, rng, R(0.7f, 0.9f));
                    else PropMeshes.Barrel(c, rng);
                    c.Pop();
                    AddBoxCollider(k == 0 ? "Crate" : "Barrel", wp + Vector3.up * 0.45f, Quaternion.identity, new Vector3(0.8f, 0.9f, 0.8f));
                }
            }

            // Windmill on its rise, with rotating sails.
            Vector3 mill = Ground(WindmillCenter.x, WindmillCenter.y) + Vector3.down * 0.3f;
            var millRotation = Quaternion.Euler(0f, 200f, 0f);
            const float towerHeight = 8f;
            var millChunk = DecorChunk(mill.x, mill.z);
            millChunk.Push(mill, millRotation, Vector3.one);
            PropMeshes.WindmillTower(millChunk, rng, towerHeight);
            millChunk.Pop();
            var millCollider = new GameObject("Windmill");
            millCollider.transform.SetParent(colliderRoot, false);
            millCollider.transform.position = mill;
            var capsule = millCollider.AddComponent<CapsuleCollider>();
            capsule.radius = 2.3f;
            capsule.height = towerHeight + 2f;
            capsule.center = Vector3.up * (towerHeight * 0.5f + 0.5f);

            var sailsBuilder = new MeshBuilder();
            PropMeshes.WindmillSails(sailsBuilder, rng, 6.5f);
            Mesh sailsMesh = GeneratedAssets.SaveMesh(sailsBuilder, "WindmillSails");
            Vector3 hub = mill + millRotation * new Vector3(0f, towerHeight + 0.6f, -2.35f);
            var sails = CreateMeshObject("Windmill Sails", sailsMesh, worldMaterial, hub, millRotation, root);
            var rotator = sails.AddComponent<Rotator>();
            SceneWiring.Set(rotator, "degreesPerSecond", new Vector3(0f, 0f, -14f));

            // Lantern posts along the hamlet road; two carry real lights, the rest only glow.
            Vector2[] lanterns =
            {
                new Vector2(30f, -10.5f), new Vector2(38f, -17.5f), new Vector2(20f, -10.5f), new Vector2(9f, -12f),
                new Vector2(41f, -3f), new Vector2(-1f, -15f),
            };
            for (int i = 0; i < lanterns.Length; i++)
            {
                Vector3 p = Ground(lanterns[i].x, lanterns[i].y);
                float yaw = Mathf.Atan2(HamletCenter.x - p.x, HamletCenter.y - p.z) * Mathf.Rad2Deg - 90f;
                var chunk = DecorChunk(p.x, p.z);
                var rotation = Quaternion.Euler(0f, yaw, 0f);
                chunk.Push(p, rotation, Vector3.one);
                PropMeshes.LanternPost(chunk, rng);
                chunk.Pop();
                AddBoxCollider("Lantern Post", p + Vector3.up * 1.4f, rotation, new Vector3(0.22f, 2.9f, 0.22f));
                if (i < 2)
                {
                    var lightGo = new GameObject("Lantern Light");
                    lightGo.transform.SetParent(root, false);
                    lightGo.transform.position = p + rotation * new Vector3(0.62f, 2.1f, 0f);
                    var lamp = lightGo.AddComponent<Light>();
                    lamp.type = LightType.Point;
                    lamp.color = new Color(1f, 0.7f, 0.38f);
                    lamp.range = 7f;
                    lamp.intensity = 1.6f;
                    lamp.shadows = LightShadows.None;
                    var flicker = lightGo.AddComponent<LightFlicker>();
                    SceneWiring.Set(flicker, "baseIntensity", 1.6f);
                    SceneWiring.Set(flicker, "intensityVariation", 0.25f);
                }
            }

            // Signpost at the fork.
            Vector3 sign = Ground(-1.5f, -9f);
            var signChunk = DecorChunk(sign.x, sign.z);
            signChunk.Push(sign, Quaternion.Euler(0f, 10f, 0f), Vector3.one);
            PropMeshes.Signpost(signChunk, rng);
            signChunk.Pop();
            AddBoxCollider("Signpost", sign + Vector3.up * 1.2f, Quaternion.identity, new Vector3(0.2f, 2.4f, 0.2f));
        }

        void BuildPilgrimRoute()
        {
            Vector3 spring = Ground(-34f, -53f);
            var basin = DecorChunk(spring.x, spring.z);
            basin.Push(spring, Quaternion.identity, Vector3.one);
            basin.SetColor(Pal.Black, 0.06f);
            basin.Prism(Vector3.up * 0.02f, 2.5f, 2.5f, 0.1f, 18);
            basin.SetColor(Pal.Water, 0.58f);
            basin.Prism(Vector3.up * 0.14f, 2.28f, 2.28f, 0.035f, 18);
            for (int i = 0; i < 18; i++)
            {
                float a = i * Mathf.PI * 2f / 18f;
                basin.Push(new Vector3(Mathf.Cos(a) * 2.6f, 0.22f, Mathf.Sin(a) * 2.6f), Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f), Vector3.one);
                basin.ShadedBox(Vector3.zero, new Vector3(0.55f, 0.34f, 0.92f), Pal.StoneMoss, rng, 0.3f, 0.52f);
                basin.Pop();
            }
            foreach (float side in new[] { -1f, 1f })
            {
                basin.ShadedBox(new Vector3(side * 1.75f, 2.2f, 3.35f), new Vector3(0.85f, 4.4f, 0.95f), Pal.StoneMoss, rng, 0.3f, 0.5f);
                basin.ShadedBox(new Vector3(side * 1.75f, 4.5f, 3.35f), new Vector3(1.1f, 0.3f, 1.15f), Pal.Stone, rng, 0.4f, 0.58f);
                AddBoxCollider("Pilgrim Spring Pillar", spring + new Vector3(side * 1.75f, 2.2f, 3.35f), Quaternion.identity, new Vector3(0.85f, 4.4f, 0.95f));
            }
            basin.ShadedBox(new Vector3(0f, 4.75f, 3.35f), new Vector3(4.6f, 0.45f, 1.15f), Pal.Stone, rng, 0.35f, 0.52f);
            basin.SetColor(Pal.WoodDark, 0.31f);
            basin.Box(new Vector3(0f, 2.7f, 3.3f), new Vector3(0.04f, 3.8f, 0.04f));
            basin.SetColor(Pal.Brass, 0.52f);
            basin.Prism(new Vector3(0f, 0.7f, 3.3f), 0.48f, 0.18f, 0.8f, 10);
            basin.SetColor(Pal.FireYellow, 0.78f);
            basin.Prism(new Vector3(0f, 1.5f, 3.3f), 0.1f, 0f, 0.25f, 5);
            for (int i = 0; i < 7; i++)
            {
                basin.SetColor(i % 2 == 0 ? Pal.ClothRed : Pal.Thatch, 0.46f);
                float x = -1.4f + i * 0.46f;
                basin.DoubleSided(new Vector3(x, 3.9f, 3.28f), new Vector3(x + 0.12f, 3.9f, 3.28f), new Vector3(x + 0.18f, 2.9f - i % 3 * 0.2f, 3.24f), Vector3.back);
            }
            basin.Pop();
            WorldVista.Inspect(root, "Pilgrims Spring Lore", Ground(-31f, -53f) + Vector3.up * 1f, new Vector3(0.8f, 1.8f, 0.8f), GameText.SpringLore);

            // A ruined high road makes a distant silhouette, with open arches at ground level.
            for (int arch = 0; arch < 3; arch++)
            {
                float x = -54f + arch * 10f;
                Vector3 at = Ground(x, 51f);
                var b = DecorChunk(x, 51f);
                b.Push(at, Quaternion.identity, Vector3.one);
                foreach (float side in new[] { -1f, 1f })
                {
                    b.ShadedBox(new Vector3(side * 4.2f, 3.2f, 0f), new Vector3(1.5f, 6.4f, 1.8f), Pal.StoneMoss, rng, 0.26f, 0.45f);
                    AddBoxCollider("Old Viaduct Pier", at + new Vector3(side * 4.2f, 3.2f, 0f), Quaternion.identity, new Vector3(1.5f, 6.4f, 1.8f));
                }
                for (int stone = 0; stone <= 10; stone++)
                {
                    if (arch == 2 && stone > 5 && stone < 9) continue;
                    float a = stone * Mathf.PI / 10f;
                    b.Push(new Vector3(Mathf.Cos(a) * 3.6f, 5.7f + Mathf.Sin(a) * 3.6f, 0f), Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg), Vector3.one);
                    b.ShadedBox(Vector3.zero, new Vector3(0.9f, 1.15f, 1.65f), Pal.Stone, rng, 0.3f, 0.49f);
                    b.Pop();
                }
                if (arch < 2)
                    b.ShadedBox(new Vector3(0f, 9.4f, 0f), new Vector3(10f, 0.7f, 2.15f), Pal.StoneMoss, rng, 0.3f, 0.48f);
                b.Pop();
            }
            foreach (Vector2 spot in new[] { new Vector2(-20f, -64f), new Vector2(-42f, -37f), new Vector2(-45f, 24f), new Vector2(-23f, 63f) })
            {
                Vector3 p = Ground(spot.x, spot.y);
                var b = DecorChunk(p.x, p.z);
                b.ShadedBox(p + Vector3.up * 0.65f, new Vector3(0.6f, 1.3f, 0.6f), Pal.StoneMoss, rng, 0.3f, 0.5f);
                AddBoxCollider("Pilgrim Road Marker", p + Vector3.up * 0.65f, Quaternion.identity, new Vector3(0.6f, 1.3f, 0.6f));
                b.SetColor(Pal.Brass, 0.48f);
                b.Box(p + new Vector3(0f, 1f, -0.31f), new Vector3(0.23f, 0.07f, 0.025f));
            }
        }

        void BuildVillageCourtyard()
        {
            Vector3 well = Ground(34.3f, -10.8f);
            var b = DecorChunk(well.x, well.z);
            b.Push(well, Quaternion.identity, Vector3.one);
            WayfarerMeshes.Well(b, rng);
            b.Pop();
            AddBoxCollider("Hamlet Well", well + Vector3.up * 1.8f, Quaternion.identity, new Vector3(2.75f, 3.6f, 2.4f));
            for (int i = 0; i < 26; i++)
            {
                float a = i * Mathf.PI * 2f / 26f;
                Vector3 p = Ground(well.x + Mathf.Cos(a) * 2.2f, well.z + Mathf.Sin(a) * 2.2f);
                b.ShadedBox(p + Vector3.up * 0.025f, new Vector3(0.65f, 0.05f, 0.55f), Pal.Cobble, rng, 0.35f, 0.58f);
            }
            Vector3 lineA = Ground(27.5f, -18f), lineB = Ground(27f, -12f);
            foreach (Vector3 at in new[] { lineA, lineB })
            {
                b.SetColor(Pal.WoodDark, 0.4f);
                b.Box(at + Vector3.up * 1.55f, new Vector3(0.12f, 3.1f, 0.12f));
                AddBoxCollider("Drying Line Post", at + Vector3.up * 1.55f, Quaternion.identity, new Vector3(0.14f, 3.1f, 0.14f));
            }
            WayfarerMeshes.Bunting(b, lineA + Vector3.up * 2.9f, lineB + Vector3.up * 2.9f, 8);
            for (int garden = 0; garden < 3; garden++)
            {
                Vector3 at = Ground(39f + garden * 3f, -30f);
                b.Push(at, Quaternion.identity, Vector3.one);
                b.SetColor(Pal.DirtDark, 0.34f);
                b.Box(new Vector3(0f, 0.02f, 0f), new Vector3(2.2f, 0.08f, 3.8f));
                foreach (float side in new[] { -1f, 1f })
                {
                    WayfarerMeshes.Beam(b, new Vector3(side * 1.15f, 0.15f, -2f), new Vector3(side * 1.15f, 0.15f, 2f), 0.18f);
                    WayfarerMeshes.Beam(b, new Vector3(-1.15f, 0.15f, side * 2f), new Vector3(1.15f, 0.15f, side * 2f), 0.18f);
                }
                for (int row = 0; row < 5; row++)
                for (int plant = 0; plant < 3; plant++)
                {
                    b.SetColor(garden == 1 ? Pal.FlowerViolet : Pal.LeafGreen, 0.46f + plant * 0.07f);
                    b.Prism(new Vector3(-0.68f + plant * 0.68f, 0.08f, -1.45f + row * 0.7f), 0.19f, 0.05f, garden == 1 ? 0.6f : 0.33f, 5);
                }
                b.Pop();
            }
            Vector3 wood = Ground(21.9f, -8.1f);
            b.Push(wood, Quaternion.Euler(0f, -37f, 0f), Vector3.one);
            WayfarerMeshes.LogPile(b, rng, 2.2f, 4);
            b.Pop();
            AddBoxCollider("Cottage Woodpile", wood + Vector3.up * 0.55f, Quaternion.Euler(0f, -37f, 0f), new Vector3(1.5f, 1.1f, 2.2f));
            Vector3 hay = Ground(55f, -27f);
            b.Push(hay, Quaternion.identity, Vector3.one);
            b.SetColor(Pal.Thatch, 0.56f);
            b.Prism(Vector3.zero, 1.7f, 1.2f, 1.5f, 12);
            b.Prism(Vector3.up * 1.5f, 1.2f, 0.08f, 1.4f, 12);
            b.Pop();
            AddBoxCollider("Harvest Haystack", hay + Vector3.up * 1.1f, Quaternion.identity, new Vector3(2.8f, 2.2f, 2.8f));
        }

        void DressCampAndGraves()
        {
            Vector3 fire = Ground(CampCenter.x, CampCenter.y);
            var b = DecorChunk(fire.x, fire.z);
            foreach (float side in new[] { -1f, 1f })
                WayfarerMeshes.Beam(b, fire + new Vector3(side * 1.2f, 0f, 0.25f), fire + new Vector3(0f, 2.4f, 0.25f), 0.09f);
            b.SetColor(Pal.SteelDark, 0.3f);
            b.Box(fire + new Vector3(0f, 1.5f, 0.25f), new Vector3(0.03f, 1.1f, 0.03f));
            b.Prism(fire + new Vector3(0f, 0.8f, 0.25f), 0.26f, 0.34f, 0.36f, 10);
            Vector3 supplies = Ground(22f, -37.5f);
            b.Push(supplies, Quaternion.Euler(0f, 15f, 0f), Vector3.one);
            WayfarerMeshes.LogPile(b, rng, 1.5f);
            WayfarerMeshes.Bucket(b, new Vector3(1.1f, 0f, 0f), 1.1f);
            b.Pop();
            for (int i = 0; i < 15; i++)
            {
                float a = i * Mathf.PI * 2f / 15f;
                if (i == 4 || i == 5 || i == 11) continue;
                Vector3 at = Ground(GraveyardCenter.x + Mathf.Cos(a) * 10.5f, GraveyardCenter.y + Mathf.Sin(a) * 10.5f);
                var stones = DecorChunk(at.x, at.z);
                stones.ShadedBox(at + Vector3.up * 0.3f, new Vector3(1.1f, 0.6f, 0.8f), Pal.StoneMoss, rng, 0.3f, 0.52f);
                AddBoxCollider("Sword Garden Memorial", at + Vector3.up * 0.3f, Quaternion.identity, new Vector3(1.1f, 0.6f, 0.8f));
                stones.SetColor(Pal.Plaster, 0.58f);
                stones.Prism(at + new Vector3(0f, 0.65f, 0f), 0.06f, 0.05f, 0.22f, 6);
                stones.SetColor(Pal.FireYellow, 0.7f);
                stones.Prism(at + new Vector3(0f, 0.87f, 0f), 0.025f, 0f, 0.1f, 4);
            }
            foreach (float side in new[] { -1f, 1f })
            for (int i = 0; i < 3; i++)
            {
                Vector3 at = Ground(side * 12.5f, 25f + i * 8f);
                var stones = DecorChunk(at.x, at.z);
                stones.ShadedBox(at + Vector3.up * 0.25f, new Vector3(1.8f, 0.5f, 1.8f), Pal.StoneMoss, rng, 0.3f, 0.45f);
                int drums = 3 + (i + (side < 0f ? 1 : 0)) % 3;
                for (int drum = 0; drum < drums; drum++)
                {
                    stones.SetColor(Pal.StoneMoss, 0.38f + drum % 2 * 0.08f);
                    stones.Prism(at + Vector3.up * (0.5f + drum * 0.8f), 0.5f, 0.47f, 0.78f, 8);
                }
                float height = 0.5f + drums * 0.8f;
                AddBoxCollider("Chapel Garden Column", at + Vector3.up * (height * 0.5f), Quaternion.identity, new Vector3(1.1f, height, 1.1f));
            }
        }

        void BuildDesignedGroves()
        {
            var points = new List<Vector2>
            {
                new Vector2(-8f, -72f), new Vector2(9f, -64f), new Vector2(-11f, -47f), new Vector2(10f, -34f),
                new Vector2(-23f, -58f), new Vector2(-41f, -57f),
            };
            for (int row = 0; row < 2; row++)
            for (int col = 0; col < 3; col++) points.Add(new Vector2(15f + col * 5.4f, -34f + row * 6f));
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 at = Ground(points[i].x, points[i].y) - Vector3.up * 0.12f;
                AddTree(autumnKinds[i % autumnKinds.Length], at, i * 37f, i < 6 ? 1.1f : 0.8f, true);
            }
        }

        void AddScenicViews()
        {
            WorldVista.Add(root, "Pilgrims Spring", Ground(-25f, -58f) + Vector3.up * 1.7f, Ground(-34f, -53f) + Vector3.up * 2f);
            WorldVista.Add(root, "Hamlet Well Court", Ground(29f, -16f) + Vector3.up * 1.7f, Ground(35f, -10f) + Vector3.up * 1.7f);
            WorldVista.Add(root, "Forgotten High Road", Ground(-20f, 44f) + Vector3.up * 1.7f, Ground(-44f, 51f) + Vector3.up * 5.2f);
            WorldVista.Add(root, "Sword Garden", Ground(-23f, -14f) + Vector3.up * 1.7f, Ground(-33f, -4f) + Vector3.up * 1.8f);
            WorldVista.Add(root, "Chapel Procession", Ground(11f, 17f) + Vector3.up * 1.7f, Ground(0f, 34f) + Vector3.up * 3.3f);
            WorldVista.Add(root, "Wayfarers Hearth", Ground(18f, -48f) + Vector3.up * 1.7f, Ground(26f, -42f) + Vector3.up * 1.3f);
            WorldVista.Add(root, "Hollowgate Pilgrims Approach", Ground(0f, 79f) + Vector3.up * 1.7f, Ground(0f, 92f) + Vector3.up * 5f);
            WorldVista.Add(root, "Old Toll Gate From The North", Ground(5f, 116f) + Vector3.up * 1.7f, Ground(0f, 92f) + Vector3.up * 4f);
            WorldVista.Add(root, "Road To Hollowgate Gate", Ground(3f, 535f) + Vector3.up * 1.7f, Ground(0f, 590f) + Vector3.up * 5f);
        }

        void BuildPuddles()
        {
            Vector2[] main = roads[0].Points;
            for (int i = 6; i < main.Length - 6; i += 13)
            {
                Vector2 p = main[i] + new Vector2(R(-0.8f, 0.8f), R(-0.8f, 0.8f));
                var chunk = DecorChunk(p.x, p.y);
                chunk.Push(Ground(p.x, p.y), Quaternion.Euler(0f, R(0f, 360f), 0f), Vector3.one);
                PropMeshes.Puddle(chunk, rng, R(0.7f, 1.3f));
                chunk.Pop();
            }
        }

        // ---- Horizon & bounds --------------------------------------------------------------------------

        void BuildMountains()
        {
            var b = new MeshBuilder();
            void Peak(float x, float z)
            {
                b.Push(new Vector3(x, -2f, z), Quaternion.Euler(0f, R(0f, 360f), 0f), Vector3.one);
                PropMeshes.Mountain(b, rng, R(45f, 85f), R(55f, 140f));
                b.Pop();
            }
            const int peaks = 34;
            for (int i = 0; i < peaks; i++)
            {
                float angle = i * Mathf.PI * 2f / peaks + R(-0.05f, 0.05f);
                float distance = R(215f, 330f);
                // The old ring's northern half would stand in the north land; the peaks below replace it there.
                if (Mathf.Sin(angle) * distance > 60f) continue;
                Peak(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance);
            }
            // Fog ends at 300 m, so the north land's peaks stand close behind its rim: 110 to 190 m outside the walls.
            for (float z = 60f; z < 780f; z += 48f)
            {
                Peak(-NorthHalf - R(110f, 190f), z + R(-12f, 12f));
                Peak(NorthHalf + R(110f, 190f), z + R(-12f, 12f));
            }
            for (float x = -300f; x <= 300f; x += 48f)
                Peak(x + R(-12f, 12f), NorthEnd + R(110f, 190f));
            // Far ground plane under the horizon ring.
            b.SetColor(Pal.Mountain, 0.2f);
            b.Triangle(new Vector3(-900f, -8f, -900f), new Vector3(-900f, -8f, 900f), new Vector3(900f, -8f, 900f), Vector3.up);
            b.Triangle(new Vector3(-900f, -8f, -900f), new Vector3(900f, -8f, 900f), new Vector3(900f, -8f, -900f), Vector3.up);

            Mesh mesh = GeneratedAssets.SaveMesh(b, "Mountains");
            CreateMeshObject("Distant Mountains", mesh, worldMaterial, Vector3.zero, Quaternion.identity, root, false);
        }

        /// <summary>Dark cones on the rim hills outside the walls: a forest edge seen from afar for a few thousand vertices.</summary>
        void BuildFarTreeLine()
        {
            var b = new MeshBuilder();
            int placed = 0;
            for (int attempt = 0; attempt < 8000 && placed < 700; attempt++)
            {
                float x = R(MinX + 10f, -MinX - 10f), z = R(MinZ + 10f, 700f);
                float outside = Mathf.Min(
                    new Vector2(Mathf.Max(Mathf.Abs(x) - WallHalf, 0f), Mathf.Max(Mathf.Abs(z) - WallHalf, 0f)).magnitude,
                    new Vector2(Mathf.Max(Mathf.Abs(x) - NorthHalf, 0f), Mathf.Max(Mathf.Abs(z - (RidgeZ + NorthEnd) * 0.5f) - (NorthEnd - RidgeZ) * 0.5f, 0f)).magnitude);
                if (outside < 12f || outside > 75f || z > 85f && z < 125f && Mathf.Abs(x) < 130f) continue;   // keep the ridge bare
                if (z > 610f && Mathf.Abs(x) < 95f) continue;   // Hollowgate's silhouette stands there
                if (Mathf.PerlinNoise(x * 0.02f + noiseC.x, z * 0.02f + noiseB.y) < 0.35f) continue;
                float h = R(9f, 16f);
                b.SetColor(Pal.PineDark, R(0.16f, 0.28f));
                b.Prism(Ground(x, z) + Vector3.down * 0.5f, h * 0.2f, 0f, h, 5, false, false);
                placed++;
            }
            CreateMeshObject("Far Tree Line", GeneratedAssets.SaveMesh(b, "FarTreeLine"), worldMaterial, Vector3.zero, Quaternion.identity, root, false);
        }

        /// <summary>
        /// The walls of the inverted T: the lower valley's sides and south, the ridge between it and the north land (open only
        /// behind the Old Toll Gate, whose own walls close the rest), and the north land's sides and end.
        /// </summary>
        void BuildBoundary()
        {
            var parent = new GameObject("World Boundary").transform;
            parent.SetParent(root, false);
            const float height = 160f, t = 4f, y = 40f;
            float valleySpan = WallHalf * 2f + t * 2f;
            float crestLength = NorthHalf + t - 19f, crestCentre = (NorthHalf + t + 19f) * 0.5f;
            float northLength = NorthEnd - RidgeZ + t, northCentre = (NorthEnd + RidgeZ + t) * 0.5f;
            var walls = new (Vector3 center, Vector3 size)[]
            {
                (new Vector3(WallHalf + t * 0.5f, y, 0f), new Vector3(t, height, valleySpan)),
                (new Vector3(-WallHalf - t * 0.5f, y, 0f), new Vector3(t, height, valleySpan)),
                (new Vector3(0f, y, -WallHalf - t * 0.5f), new Vector3(valleySpan, height, t)),
                (new Vector3(crestCentre, y, RidgeZ + t * 0.5f), new Vector3(crestLength, height, t)),
                (new Vector3(-crestCentre, y, RidgeZ + t * 0.5f), new Vector3(crestLength, height, t)),
                // From the ends of the toll gate's own walls (x 19, z 92.2) up to the crest.
                (new Vector3(21f, y, 97.6f), new Vector3(t, height, 10.8f)),
                (new Vector3(-21f, y, 97.6f), new Vector3(t, height, 10.8f)),
                (new Vector3(NorthHalf + t * 0.5f, y, northCentre), new Vector3(t, height, northLength)),
                (new Vector3(-NorthHalf - t * 0.5f, y, northCentre), new Vector3(t, height, northLength)),
                (new Vector3(0f, y, NorthEnd + t * 0.5f), new Vector3((NorthHalf + t) * 2f, height, t)),
            };
            foreach (var wall in walls)
            {
                var go = new GameObject("Boundary Wall");
                go.transform.SetParent(parent, false);
                go.transform.position = wall.center;
                go.AddComponent<BoxCollider>().size = wall.size;
            }
        }
    }
}
