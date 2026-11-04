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

        readonly Material worldMaterial;
        readonly Material metalMaterial;
        readonly Material fireMaterial;
        readonly System.Random rng = new System.Random(Seed);

        readonly List<Vector2[]> paths = new List<Vector2[]>();
        readonly List<(Vector2 center, float radius, float falloff, float height)> flats = new List<(Vector2, float, float, float)>();
        readonly List<(Vector2 center, float radius)> clearings = new List<(Vector2, float)>();
        readonly List<Vector2> treePositions = new List<Vector2>();

        Vector2 noiseA, noiseB, noiseC;
        float[,] heights;
        float cellSize;
        Transform root, colliderRoot;
        MeshBuilder[,] decorChunks, foliageChunks;
        MeshBuilder[] pineVariants;
        MeshBuilder[] rockVariants;
        Mesh[] rockMeshes;

        public ValleyGenerator(Material world, Material metal, Material fire)
        {
            worldMaterial = world;
            metalMaterial = metal;
            fireMaterial = fire;
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
            foliageChunks = new MeshBuilder[ChunkGrid, ChunkGrid];
            for (int x = 0; x < ChunkGrid; x++)
            for (int z = 0; z < ChunkGrid; z++)
            {
                decorChunks[x, z] = new MeshBuilder();
                foliageChunks[x, z] = new MeshBuilder();
            }
            BuildVariants();

            var layout = new ValleyLayout { Root = root };
            BuildRuins(layout);
            BuildGraveyard();
            BuildCamp();
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

            clearings.Add((RuinsCenter, 19f));
            clearings.Add((new Vector2(0f, 10f), 10f));   // knight patrol ground
            clearings.Add((CampCenter, 10f));
            clearings.Add((GraveyardCenter, 10f));
            clearings.Add((SpawnCenter, 7f));
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

        void BuildTerrain()
        {
            var b = new MeshBuilder();
            for (int i = 0; i < Cells; i++)
            for (int j = 0; j < Cells; j++)
            {
                float x0 = -TerrainHalf + i * cellSize, z0 = -TerrainHalf + j * cellSize;
                var v00 = new Vector3(x0, heights[i, j], z0);
                var v10 = new Vector3(x0 + cellSize, heights[i + 1, j], z0);
                var v01 = new Vector3(x0, heights[i, j + 1], z0 + cellSize);
                var v11 = new Vector3(x0 + cellSize, heights[i + 1, j + 1], z0 + cellSize);
                PaintTerrainTriangle(b, v00, v01, v11);
                b.Triangle(v00, v01, v11, Vector3.up);
                PaintTerrainTriangle(b, v00, v11, v10);
                b.Triangle(v00, v11, v10, Vector3.up);
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

        void PaintTerrainTriangle(MeshBuilder b, Vector3 a, Vector3 c1, Vector3 c2)
        {
            Vector3 center = (a + c1 + c2) / 3f;
            Vector3 n = Vector3.Cross(c1 - a, c2 - a).normalized;
            if (n.y < 0f) n = -n;
            float slope = 1f - n.y;
            float pathMask = PathMask(center.x, center.z);
            float jitter = R(-0.08f, 0.08f);
            float rockNoise = Mathf.PerlinNoise(center.x * 0.08f + noiseA.y, center.z * 0.08f + noiseB.x);

            if (slope > 0.3f || (center.y > 16f && rockNoise > 0.55f))
            {
                Pal rock = rockNoise < 0.4f ? Pal.RockDark : rockNoise > 0.62f ? Pal.RockWarm : Pal.Rock;
                b.SetColor(rock, 0.42f + jitter * 1.5f + n.y * 0.1f);
            }
            else if (pathMask > 0.5f)
            {
                b.SetColor(pathMask > 0.9f && R(0f, 1f) < 0.3f ? Pal.DirtDark : Pal.Dirt, 0.45f + jitter);
            }
            else
            {
                Pal grass = pathMask > 0.2f ? Pal.GrassDry : GrassFamily(center.x, center.z);
                float heightTint = Mathf.Clamp(center.y * 0.006f, -0.05f, 0.08f);
                b.SetColor(grass, 0.42f + jitter + heightTint - pathMask * 0.15f);
            }
        }

        // ---- Variants & chunks ---------------------------------------------------------------------

        void BuildVariants()
        {
            pineVariants = new MeshBuilder[4];
            for (int i = 0; i < pineVariants.Length; i++)
            {
                pineVariants[i] = new MeshBuilder();
                PropMeshes.Pine(pineVariants[i], rng, 7f + i * 2f);
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

        static int ChunkIndex(float v) => Mathf.Clamp(Mathf.FloorToInt((v + TerrainHalf) / (TerrainHalf * 2f / ChunkGrid)), 0, ChunkGrid - 1);

        void FlushChunks()
        {
            for (int x = 0; x < ChunkGrid; x++)
            for (int z = 0; z < ChunkGrid; z++)
            {
                CreateChunkObject(decorChunks[x, z], $"Decor_{x}_{z}", ShadowCastingMode.On);
                CreateChunkObject(foliageChunks[x, z], $"Foliage_{x}_{z}", ShadowCastingMode.Off);
            }
        }

        void CreateChunkObject(MeshBuilder builder, string name, ShadowCastingMode shadows)
        {
            if (builder.TriangleCount == 0) return;
            Mesh mesh = GeneratedAssets.SaveMesh(builder, name);
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = worldMaterial;
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

                float scale = R(1.05f, 1.35f);
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
            Vector3 lorePos = Ground(GraveyardCenter.x, GraveyardCenter.y) + Vector3.up * 1.05f;
            var lore = CreateMeshObject("Oathbreaker's Blade", loreMesh, metalMaterial, lorePos,
                Quaternion.Euler(8f, 30f, 6f) * Quaternion.Euler(180f, 0f, 0f), root);
            lore.transform.localScale = Vector3.one * 1.6f;
            var loreCollider = lore.AddComponent<BoxCollider>();
            loreCollider.center = new Vector3(0f, 0.35f, 0f);
            loreCollider.size = new Vector3(0.3f, 1.3f, 0.12f);
            var inspect = lore.AddComponent<LoreInspectable>();
            SceneWiring.Set(inspect, "prompt", "Inspect Blade");
            SceneWiring.Set(inspect, "loreText",
                "A knight's blade, driven into the earth to the hilt. Its pommel bears a broken sun: the sigil of the Ashen Oath.");

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
            var flameObject = CreateMeshObject("Campfire", flames, fireMaterial, fire, Quaternion.identity, root, false);
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
                DecorChunk(x, z).Append(pineVariants[rng.Next(pineVariants.Length)], Matrix4x4.TRS(position, rotation, Vector3.one * scale));

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

        void ScatterFoliage()
        {
            int tufts = 0;
            for (int attempt = 0; attempt < 16000 && tufts < 6500; attempt++)
            {
                float x = R(-110f, 110f), z = R(-110f, 110f);
                if (PathMask(x, z) > 0.3f) continue;
                if (Mathf.Abs(x) < 9f && z > 19f && z < 45.5f) continue; // ruins interior
                if (SurfaceNormal(x, z).y < 0.86f) continue;
                float density = 0.3f + 0.7f * Mathf.PerlinNoise(x * 0.07f + noiseC.x, z * 0.07f + noiseA.y);
                if (R(0f, 1f) > density) continue;

                var chunk = FoliageChunk(x, z);
                chunk.Push(new Vector3(x, SurfaceHeight(x, z) - 0.03f, z), Quaternion.Euler(0f, R(0f, 360f), 0f), Vector3.one * R(0.8f, 1.5f));
                PropMeshes.GrassTuft(chunk, rng, GrassFamily(x, z));
                chunk.Pop();
                tufts++;
            }

            Pal[] petals = { Pal.FlowerRed, Pal.FlowerWhite, Pal.FlowerViolet, Pal.FlowerYellow };
            int flowers = 0;
            for (int attempt = 0; attempt < 6000 && flowers < 650; attempt++)
            {
                float x = R(-100f, 100f), z = R(-100f, 100f);
                if (Mathf.PerlinNoise(x * 0.09f + noiseA.y, z * 0.09f + noiseC.x) < 0.6f) continue;
                if (PathMask(x, z) > 0.2f || SurfaceNormal(x, z).y < 0.88f) continue;
                if (Mathf.Abs(x) < 9f && z > 19f && z < 45.5f) continue;
                int colorIndex = Mathf.Clamp(Mathf.FloorToInt(Mathf.PerlinNoise(x * 0.02f + 40f, z * 0.02f + 70f) * 4f), 0, 3);
                var chunk = FoliageChunk(x, z);
                chunk.Push(new Vector3(x, SurfaceHeight(x, z) - 0.02f, z), Quaternion.Euler(R(-8f, 8f), R(0f, 360f), R(-8f, 8f)), Vector3.one * R(0.9f, 1.3f));
                PropMeshes.Flower(chunk, rng, petals[colorIndex]);
                chunk.Pop();
                flowers++;
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
