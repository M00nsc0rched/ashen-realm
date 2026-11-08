using System.Collections.Generic;
using AshenRealm.Core;
using AshenRealm.Interactions;
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
    }

    /// <summary>
    /// Deterministically generates the Prototype Valley: heightfield terrain with painted paths, rim hills,
    /// distant mountains, pine forest, boulders, grass, flowers, fences, a camp, a sword graveyard and a ruined
    /// chapel. Static decoration is merged into a 4×4 grid of chunk meshes to keep draw calls low; colliders are
    /// separate primitives.
    /// </summary>
    internal sealed class ValleyGenerator
    {
        public const int Seed = 1337;

        const float TerrainHalf = 140f;
        const int Cells = 112;
        const float WallHalf = 103f;
        const int ChunkGrid = 4;

        static readonly Vector2 RuinsCenter = new Vector2(0f, 32f);
        static readonly Vector2 CampCenter = new Vector2(26f, -42f);
        static readonly Vector2 GraveyardCenter = new Vector2(-32f, -6f);
        static readonly Vector2 SpawnCenter = new Vector2(0f, -86f);
        static readonly Vector2 HamletCenter = new Vector2(34f, -14f);
        static readonly Vector2 WindmillCenter = new Vector2(54f, 8f);
        static readonly Vector2 GateCenter = new Vector2(0f, 92f);

        readonly Material worldMaterial;
        readonly Material foliageMaterial;
        readonly System.Random rng = new System.Random(Seed);

        readonly List<Vector2[]> paths = new List<Vector2[]>();
        readonly List<(Vector2 center, float radius, float falloff, float height)> flats = new List<(Vector2, float, float, float)>();
        readonly List<(Vector2 center, float radius)> clearings = new List<(Vector2, float)>();
        readonly List<Vector2> treePositions = new List<Vector2>();

        Vector2 noiseA, noiseB, noiseC;
        float[,] heights;
        float cellSize;
        Transform root, colliderRoot;
        MeshBuilder[,] decorChunks, treeChunks, foliageChunks;
        (MeshBuilder solid, MeshBuilder cutout)[] pineVariants;
        (MeshBuilder solid, MeshBuilder cutout)[] autumnVariants;
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

            decorChunks = new MeshBuilder[ChunkGrid, ChunkGrid];
            treeChunks = new MeshBuilder[ChunkGrid, ChunkGrid];
            foliageChunks = new MeshBuilder[ChunkGrid, ChunkGrid];
            for (int x = 0; x < ChunkGrid; x++)
            for (int z = 0; z < ChunkGrid; z++)
            {
                decorChunks[x, z] = new MeshBuilder();
                treeChunks[x, z] = new MeshBuilder();
                foliageChunks[x, z] = new MeshBuilder();
            }
            BuildVariants();

            var layout = new ValleyLayout { Root = root };
            BuildRuins(layout);
            BuildGraveyard();
            BuildCamp();
            BuildHamlet(layout);
            layout.Arrival = new ArrivalGenerator(worldMaterial).Generate(root, Ground);
            BuildGate(layout);
            BuildPuddles();
            BuildFences();
            ScatterTrees();
            ScatterRocks();
            ScatterFoliage();
            BuildMountains();
            BuildBoundary();
            FlushChunks();

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

        void BuildPaths()
        {
            paths.Add(CatmullRom(new[]
            {
                new Vector2(0f, -96f), new Vector2(0f, -86f), new Vector2(-3f, -70f), new Vector2(4f, -54f), new Vector2(2f, -38f),
                new Vector2(-5f, -20f), new Vector2(-2f, -4f), new Vector2(1f, 9f), new Vector2(0f, 19f), new Vector2(0f, 23f),
            }));
            paths.Add(CatmullRom(new[] { new Vector2(2f, -38f), new Vector2(12f, -41f), new Vector2(21f, -42f), new Vector2(25f, -42f) }));
            paths.Add(CatmullRom(new[] { new Vector2(-5f, -20f), new Vector2(-16f, -14f), new Vector2(-26f, -8f), new Vector2(-31f, -6f) }));
            paths.Add(CatmullRom(new[] { new Vector2(-4f, -12f), new Vector2(8f, -15f), new Vector2(20f, -13f), new Vector2(30f, -14f), HamletCenter }));
            paths.Add(CatmullRom(new[] { HamletCenter, new Vector2(42f, -6f), new Vector2(48f, 2f), new Vector2(52f, 6f) }));
            paths.Add(CatmullRom(new[] { new Vector2(1f, 9f), new Vector2(13f, 13f), new Vector2(17f, 30f), new Vector2(14f, 50f), new Vector2(5f, 70f), GateCenter }));
        }

        static Vector2[] CatmullRom(Vector2[] points)
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

        float PathDistance(float x, float z)
        {
            var p = new Vector2(x, z);
            float best = float.MaxValue;
            foreach (var path in paths)
            {
                for (int i = 0; i < path.Length - 1; i++)
                {
                    Vector2 a = path[i], ab = path[i + 1] - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-5f));
                    float d = (a + ab * t - p).sqrMagnitude;
                    if (d < best) best = d;
                }
            }
            return Mathf.Sqrt(best);
        }

        float PathMask(float x, float z) => 1f - SmoothStep(1.2f, 2.8f, PathDistance(x, z));

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

            float rim = SmoothStep(76f, 126f, SuperDistance(x, z));
            float n3 = Mathf.PerlinNoise(x * 0.03f + noiseC.x, z * 0.03f + noiseC.y);
            float n4 = Mathf.PerlinNoise(x * 0.11f + noiseC.y, z * 0.11f + noiseC.x);
            h += rim * rim * (24f + n3 * 26f) + rim * (n4 - 0.5f) * 6f;
            return h;
        }

        float Height(float x, float z)
        {
            float h = BaseHeight(x, z);
            foreach (var flat in flats)
            {
                float w = 1f - SmoothStep(flat.radius, flat.radius + flat.falloff, Vector2.Distance(new Vector2(x, z), flat.center));
                h = Mathf.Lerp(h, flat.height, w);
            }
            return h - PathMask(x, z) * 0.3f;
        }

        void BuildHeightfield()
        {
            cellSize = TerrainHalf * 2f / Cells;
            heights = new float[Cells + 1, Cells + 1];
            for (int i = 0; i <= Cells; i++)
            for (int j = 0; j <= Cells; j++)
                heights[i, j] = Height(-TerrainHalf + i * cellSize, -TerrainHalf + j * cellSize);
        }

        /// <summary>Exact height of the terrain mesh surface (matches the triangle split used in BuildTerrain).</summary>
        public float SurfaceHeight(float x, float z)
        {
            float gx = Mathf.Clamp((x + TerrainHalf) / cellSize, 0f, Cells - 0.0001f);
            float gz = Mathf.Clamp((z + TerrainHalf) / cellSize, 0f, Cells - 0.0001f);
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
            int n = Cells + 1;
            var normal = new Vector3[n, n];
            var tint = new Color[n, n];
            var dirtWeight = new float[n, n];
            var rockWeight = new float[n, n];
            for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
            {
                float x = -TerrainHalf + i * cellSize, z = -TerrainHalf + j * cellSize;
                float hl = heights[Mathf.Max(i - 1, 0), j], hr = heights[Mathf.Min(i + 1, Cells), j];
                float hd = heights[i, Mathf.Max(j - 1, 0)], hu = heights[i, Mathf.Min(j + 1, Cells)];
                Vector3 nrm = new Vector3(hl - hr, 2f * cellSize, hd - hu).normalized;
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
                grass *= 0.92f + Mathf.PerlinNoise(x * 0.2f + noiseC.x, z * 0.2f) * 0.16f;
                Color dirtColor = Palette.Linear(Pal.Dirt, 0.45f + rockNoise * 0.1f);
                Color rockColor = Palette.Linear(rockNoise < 0.4f ? Pal.RockDark : rockNoise > 0.62f ? Pal.RockWarm : Pal.Rock, 0.5f);
                Color c = Color.Lerp(Color.Lerp(grass, dirtColor, dirt), rockColor, rock);
                c.a = 1f;
                tint[i, j] = c;
            }

            var b = new MeshBuilder();
            var p = new Vector3[3];
            var nn = new Vector3[3];
            var cc = new Color[3];
            var uv = new Vector4[3];
            void Tri(int ia, int ja, int ib, int jb, int ic, int jc)
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
                    float x = -TerrainHalf + i * cellSize, z = -TerrainHalf + j * cellSize;
                    p[k] = new Vector3(x, heights[i, j], z);
                    nn[k] = normal[i, j];
                    Color c = tint[i, j];
                    c.a = useRock ? rockWeight[i, j] : dirtWeight[i, j];
                    cc[k] = c;
                    uv[k] = new Vector4(x / 2.5f, z / 2.5f, (int)TexLayer.Grass, (int)second);
                }
                b.RawTriangle(p, nn, cc, uv, Vector4.zero);
            }

            for (int i = 0; i < Cells; i++)
            for (int j = 0; j < Cells; j++)
            {
                // Same diagonal split as SurfaceHeight(): (00, 01, 11) and (00, 11, 10), clockwise from above.
                Tri(i, j, i, j + 1, i + 1, j + 1);
                Tri(i, j, i + 1, j + 1, i + 1, j);
            }

            Mesh mesh = GeneratedAssets.SaveMesh(b, "Terrain");
            var go = new GameObject("Terrain");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = worldMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        // ---- Variants & chunks ---------------------------------------------------------------------

        void BuildVariants()
        {
            pineVariants = new (MeshBuilder, MeshBuilder)[5];
            for (int i = 0; i < pineVariants.Length; i++)
            {
                pineVariants[i] = (new MeshBuilder(), new MeshBuilder());
                PropMeshes.Pine(pineVariants[i].solid, pineVariants[i].cutout, rng, 7f + i * 1.8f);
            }
            Pal[] autumn = { Pal.LeafGold, Pal.LeafOrange, Pal.LeafGold };
            autumnVariants = new (MeshBuilder, MeshBuilder)[3];
            for (int i = 0; i < autumnVariants.Length; i++)
            {
                autumnVariants[i] = (new MeshBuilder(), new MeshBuilder());
                PropMeshes.AutumnTree(autumnVariants[i].solid, autumnVariants[i].cutout, rng, 6f + i * 1.3f, autumn[i]);
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
        }

        MeshBuilder DecorChunk(float x, float z) => decorChunks[ChunkIndex(x), ChunkIndex(z)];
        MeshBuilder FoliageChunk(float x, float z) => foliageChunks[ChunkIndex(x), ChunkIndex(z)];
        MeshBuilder TreeChunk(float x, float z) => treeChunks[ChunkIndex(x), ChunkIndex(z)];

        /// <summary>Places a two-part tree variant: solid parts into the decor chunk, cards into the tree-foliage chunk.</summary>
        void PlaceTree((MeshBuilder solid, MeshBuilder cutout) variant, Vector3 position, Quaternion rotation, float scale)
        {
            var m = Matrix4x4.TRS(position, rotation, Vector3.one * scale);
            DecorChunk(position.x, position.z).Append(variant.solid, m);
            TreeChunk(position.x, position.z).Append(variant.cutout, m);
        }

        static int ChunkIndex(float v) => Mathf.Clamp(Mathf.FloorToInt((v + TerrainHalf) / (TerrainHalf * 2f / ChunkGrid)), 0, ChunkGrid - 1);

        void FlushChunks()
        {
            for (int x = 0; x < ChunkGrid; x++)
            for (int z = 0; z < ChunkGrid; z++)
            {
                CreateChunkObject(decorChunks[x, z], $"Decor_{x}_{z}", worldMaterial, ShadowCastingMode.On);
                CreateChunkObject(treeChunks[x, z], $"Trees_{x}_{z}", foliageMaterial, ShadowCastingMode.On);
                CreateChunkObject(foliageChunks[x, z], $"Foliage_{x}_{z}", foliageMaterial, ShadowCastingMode.Off);
            }
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

        // ---- The gate to Hollowgate -------------------------------------------------------------------

        void BuildGate(ValleyLayout layout)
        {
            Vector3 p = Ground(GateCenter.x, GateCenter.y);
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
            b.SetColor(Pal.WoodDark, 0.35f);
            for (int plank = 0; plank < 7; plank++)
                b.ShadedBox(new Vector3(-2.55f + plank * 0.85f, 4.4f, 0.2f), new Vector3(0.8f, 8.8f, 0.3f), Pal.WoodDark, rng, 0.28f, 0.42f);
            b.ShadedBox(new Vector3(0f, 2.5f, -0.02f), new Vector3(6f, 0.4f, 0.1f), Pal.SteelDark, rng, 0.3f, 0.4f);
            b.ShadedBox(new Vector3(0f, 6.5f, -0.02f), new Vector3(6f, 0.4f, 0.1f), Pal.SteelDark, rng, 0.3f, 0.4f);
            b.Pop();

            layout.GatePosition = p;
            layout.GateArrival = Ground(GateCenter.x, GateCenter.y - 5f) + Vector3.up * 0.1f;
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
            Vector2[] main = paths[0];
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
                float x = R(-128f, 128f), z = R(-128f, 128f);
                float d = SuperDistance(x, z);
                if (d > 132f) continue;
                float edge = SmoothStep(48f, 95f, d);
                float cluster = SmoothStep(0.56f, 0.72f, Mathf.PerlinNoise(x * 0.03f + noiseB.x, z * 0.03f + noiseB.y));
                if (R(0f, 1f) > Mathf.Max(edge * 0.85f, cluster * 0.75f)) continue;
                if (PathDistance(x, z) < 5f || InClearing(x, z, 2f)) continue;
                if (SurfaceNormal(x, z).y < 0.75f) continue;

                bool crowded = false;
                foreach (var t in treePositions)
                    if ((t - new Vector2(x, z)).sqrMagnitude < 3.2f * 3.2f) { crowded = true; break; }
                if (crowded) continue;
                treePositions.Add(new Vector2(x, z));

                float scale = R(0.8f, 1.3f);
                Vector3 position = new Vector3(x, SurfaceHeight(x, z) - 0.15f, z);
                var rotation = Quaternion.Euler(R(-3f, 3f), R(0f, 360f), R(-3f, 3f));
                // Golden broadleaf trees mix in near the hamlet and in the mid valley, pines dominate the rim.
                bool autumn = d < 80f && (Vector2.Distance(new Vector2(x, z), HamletCenter) < 45f || R(0f, 1f) < 0.18f);
                var variant = autumn ? autumnVariants[rng.Next(autumnVariants.Length)] : pineVariants[rng.Next(pineVariants.Length)];
                PlaceTree(variant, position, rotation, scale);

                if (d < WallHalf + 3f)
                {
                    var go = new GameObject("Tree");
                    go.transform.SetParent(colliderRoot, false);
                    go.transform.position = position;
                    var capsule = go.AddComponent<CapsuleCollider>();
                    capsule.radius = 0.35f * scale;
                    capsule.height = 4f * scale;
                    capsule.center = new Vector3(0f, 2f * scale, 0f);
                }
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

            // Craggy outcrops along the rim hills.
            placed = 0;
            for (int attempt = 0; attempt < 500 && placed < 55; attempt++)
            {
                float x = R(-135f, 135f), z = R(-135f, 135f);
                float d = SuperDistance(x, z);
                if (d < 88f || d > 136f) continue;
                PlaceRock(x, z, R(3.5f, 9f), d < WallHalf + 6f);
                placed++;
            }

            // Small stones near paths.
            for (int i = 0; i < 120; i++)
            {
                float x = R(-100f, 100f), z = R(-100f, 100f);
                float pd = PathDistance(x, z);
                if (pd < 2.2f || pd > 4.5f) continue;
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
            return Vector2.Distance(new Vector2(x, z), WindmillCenter) < 3f;
        }

        void ScatterFoliage()
        {
            int clumps = 0;
            for (int attempt = 0; attempt < 20000 && clumps < 7500; attempt++)
            {
                float x = R(-110f, 110f), z = R(-110f, 110f);
                if (PathMask(x, z) > 0.3f || BlockedForFoliage(x, z) || FieldMask(x, z) > 0f) continue;
                if (SurfaceNormal(x, z).y < 0.86f) continue;
                float density = 0.3f + 0.7f * Mathf.PerlinNoise(x * 0.07f + noiseC.x, z * 0.07f + noiseA.y);
                if (R(0f, 1f) > density) continue;

                var chunk = FoliageChunk(x, z);
                chunk.Push(new Vector3(x, SurfaceHeight(x, z) - 0.03f, z), Quaternion.identity, Vector3.one * R(0.8f, 1.4f));
                PropMeshes.GrassClump(chunk, rng, GrassFamily(x, z));
                chunk.Pop();
                clumps++;
            }

            // Flower patches across the valley, and a dense meadow around the sword graveyard.
            int flowers = 0;
            for (int attempt = 0; attempt < 14000 && flowers < 2600; attempt++)
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
                var chunk = FoliageChunk(x, z);
                chunk.Push(new Vector3(x, SurfaceHeight(x, z) - 0.02f, z), Quaternion.identity, Vector3.one * R(0.9f, 1.3f));
                PropMeshes.FlowerClump(chunk, rng);
                chunk.Pop();
                flowers++;
            }

            // Wheat fields in loose rows.
            for (float x = 40f; x < 64f; x += 1.1f)
            for (float z = -36f; z < 0f; z += 1.0f)
            {
                float jx = x + R(-0.35f, 0.35f), jz = z + R(-0.35f, 0.35f);
                if (FieldMask(jx, jz) <= 0f || PathMask(jx, jz) > 0.2f || BlockedForFoliage(jx, jz) || R(0f, 1f) < 0.08f) continue;
                var chunk = FoliageChunk(jx, jz);
                chunk.Push(new Vector3(jx, SurfaceHeight(jx, jz) - 0.05f, jz), Quaternion.identity, Vector3.one * R(0.9f, 1.15f));
                PropMeshes.WheatClump(chunk, rng);
                chunk.Pop();
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

        void BuildPuddles()
        {
            Vector2[] main = paths[0];
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
            const int peaks = 34;
            for (int i = 0; i < peaks; i++)
            {
                float angle = i * Mathf.PI * 2f / peaks + R(-0.05f, 0.05f);
                float distance = R(215f, 330f);
                var position = new Vector3(Mathf.Cos(angle) * distance, -2f, Mathf.Sin(angle) * distance);
                b.Push(position, Quaternion.Euler(0f, R(0f, 360f), 0f), Vector3.one);
                PropMeshes.Mountain(b, rng, R(45f, 85f), R(55f, 140f));
                b.Pop();
            }
            // Far ground plane under the horizon ring.
            b.SetColor(Pal.Mountain, 0.2f);
            b.Triangle(new Vector3(-900f, -8f, -900f), new Vector3(-900f, -8f, 900f), new Vector3(900f, -8f, 900f), Vector3.up);
            b.Triangle(new Vector3(-900f, -8f, -900f), new Vector3(900f, -8f, 900f), new Vector3(900f, -8f, -900f), Vector3.up);

            Mesh mesh = GeneratedAssets.SaveMesh(b, "Mountains");
            CreateMeshObject("Distant Mountains", mesh, worldMaterial, Vector3.zero, Quaternion.identity, root, false);
        }

        void BuildBoundary()
        {
            var parent = new GameObject("World Boundary").transform;
            parent.SetParent(root, false);
            const float height = 160f;
            const float thickness = 4f;
            float span = WallHalf * 2f + thickness * 2f;
            var walls = new (Vector3 center, Vector3 size)[]
            {
                (new Vector3(WallHalf + thickness * 0.5f, 40f, 0f), new Vector3(thickness, height, span)),
                (new Vector3(-WallHalf - thickness * 0.5f, 40f, 0f), new Vector3(thickness, height, span)),
                (new Vector3(0f, 40f, WallHalf + thickness * 0.5f), new Vector3(span, height, thickness)),
                (new Vector3(0f, 40f, -WallHalf - thickness * 0.5f), new Vector3(span, height, thickness)),
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
