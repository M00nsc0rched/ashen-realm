using System.Collections.Generic;
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
        public Vector3 Stair;
        public float StairYaw;
        public Vector3 StairArrival;
        public readonly List<(string id, Vector3 position, float yaw, HumanoidStyle look)> People = new List<(string, Vector3, float, HumanoidStyle)>();
    }

    /// <summary>
    /// Hollowgate (Üregkapu), the last city above the Deep Dark: a walled square of half-timbered houses with the Rope and
    /// Lantern to the north, the Cold Forge, the Grey Chapel and the gibbet of Gallows Market. Reuses the valley's prop kit.
    /// </summary>
    internal sealed class CityGenerator
    {
        const float Half = 64f;
        const float WallHeight = 8f;

        readonly Material material;
        readonly Vector3 origin;
        readonly System.Random rng;
        MeshBuilder ground, buildings, foliageFree;
        Transform root, colliderRoot;

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

            BuildGround();
            BuildWalls();
            var layout = new CityLayout { Root = root };
            BuildDistricts(layout);
            BuildMountains();

            Save(ground, "City_Ground", ShadowCastingMode.Off);
            Save(buildings, "City_Buildings", ShadowCastingMode.On);
            Save(foliageFree, "City_Mountains", ShadowCastingMode.Off);
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
                House(-15f, z, 90f, 8f, 6f, 3.2f, z < -35f);
                House(15f, z, -90f, 8f, 6f, 3.0f, z > -45f);
            }
            for (float z = -56f; z <= -8f; z += 12f)
            {
                Lantern(-6.5f, z);
                Lantern(6.5f, z + 6f);
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

            // The Rope and Lantern: the big house on the north side of the square, the stair at its corner.
            House(0f, 36f, 180f, 18f, 11f, 5f, true);
            layout.Stair = origin + new Vector3(0f, 0f, 29.8f);
            layout.StairYaw = 0f;
            layout.StairArrival = origin + new Vector3(0f, 0.05f, 26f);
            Prop(new Vector3(0f, 0f, 29.8f), 0f, b => PropMeshes.StairArch(b, rng, Pal.Stone, true));
            Box("Stair Frame L", new Vector3(-2.2f, 2f, 29.8f), new Vector3(0.8f, 4f, 1.2f), Quaternion.identity);
            Box("Stair Frame R", new Vector3(2.2f, 2f, 29.8f), new Vector3(0.8f, 4f, 1.2f), Quaternion.identity);
            Prop(new Vector3(-9f, 0f, 30f), 90f, b => PropMeshes.Signpost(b, rng));
            Lantern(-7f, 28f);
            Lantern(7f, 28f);

            // The Cold Forge, east: shuttered, with a chimney and an anvil out front.
            House(33f, 6f, -90f, 12f, 9f, 4f, false);
            buildings.ShadedBox(new Vector3(37f, 6f, 10f), new Vector3(2f, 12f, 2f), Pal.Stone, rng, 0.3f, 0.42f);
            buildings.ShadedBox(new Vector3(25f, 0.45f, 6f), new Vector3(0.7f, 0.9f, 0.5f), Pal.WoodDark, rng, 0.3f, 0.4f);
            buildings.ShadedBox(new Vector3(25f, 1f, 6f), new Vector3(1.1f, 0.26f, 0.4f), Pal.SteelDark, rng, 0.3f, 0.45f);

            // The Grey Chapel, west: stone, with a tall nave and a small tower.
            buildings.ShadedBox(new Vector3(-36f, 4f, 6f), new Vector3(20f, 8f, 11f), Pal.Stone, rng, 0.36f, 0.48f);
            buildings.ShadedBox(new Vector3(-27f, 7f, 6f), new Vector3(4f, 14f, 4f), Pal.Stone, rng, 0.4f, 0.52f);
            buildings.SetColor(Pal.RoofTile, 0.35f);
            buildings.Push(new Vector3(-27f, 14f, 6f), Quaternion.identity, Vector3.one);
            buildings.Prism(Vector3.zero, 3.2f, 0f, 5f, 4, false, false, Mathf.PI * 0.25f);
            buildings.Pop();
            buildings.SetColor(Pal.Lantern, 0.7f);
            buildings.Box(new Vector3(-25.9f, 3.5f, 6f), new Vector3(0.1f, 4.4f, 1.8f));
            Box("Chapel", new Vector3(-36f, 4f, 6f), new Vector3(20f, 8f, 11f), Quaternion.identity);
            Box("Chapel Tower", new Vector3(-27f, 7f, 6f), new Vector3(4f, 14f, 4f), Quaternion.identity);

            // Gallows Market, around the square: stalls, crates and the gibbet it is named for.
            for (int i = 0; i < 5; i++)
            {
                float a = i * 1.25f + 0.4f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 15f, 0f, Mathf.Sin(a) * 13f - 2f);
                Prop(p, i * 70f, b => PropMeshes.Crate(b, rng, R(0.8f, 1.1f)));
                Prop(p + new Vector3(1.4f, 0f, 0.6f), 0f, b => PropMeshes.Barrel(b, rng));
                Box("Stall", p + Vector3.up * 0.5f, new Vector3(1.2f, 1f, 1.2f), Quaternion.identity);
            }
            buildings.ShadedBox(new Vector3(0f, 2.7f, -1f), new Vector3(0.36f, 5.4f, 0.36f), Pal.WoodDark, rng, 0.3f, 0.4f);
            buildings.ShadedBox(new Vector3(0.9f, 5.2f, -1f), new Vector3(2.4f, 0.3f, 0.3f), Pal.WoodDark, rng, 0.3f, 0.4f);
            buildings.ShadedBox(new Vector3(1.8f, 4.5f, -1f), new Vector3(0.04f, 1.2f, 0.04f), Pal.Leather, rng, 0.3f, 0.4f);
            Box("Gibbet", new Vector3(0f, 2.7f, -1f), new Vector3(0.5f, 5.4f, 0.5f), Quaternion.identity);

            layout.Entry = origin + new Vector3(0f, 0.05f, -56f);
            layout.EntryYaw = 0f;
            layout.Gate = origin + new Vector3(0f, 0f, -Half + 0.2f);
            layout.GateYaw = 180f;

            layout.People.Add(("wanderer", origin + new Vector3(5.5f, 0f, 25f), 200f, EnemyFactory.Villager("Wanderer", Pal.Shadow, Pal.Shadow, true)));
            layout.People.Add(("landlord", origin + new Vector3(-5f, 0f, 26.5f), 150f, EnemyFactory.Villager("Landlord", Pal.ClothRed)));
            layout.People.Add(("quartermaster", origin + new Vector3(23.5f, 0f, 3f), 250f, EnemyFactory.Villager("Quartermaster", Pal.SteelDark)));
            layout.People.Add(("gravedigger", origin + new Vector3(-23f, 0f, 1f), 110f, EnemyFactory.Villager("Gravedigger", Pal.Dirt)));
            layout.People.Add(("merchant", origin + new Vector3(-5f, 0f, -8f), 20f, EnemyFactory.Villager("Merchant", Pal.Robe, Pal.Bone, true)));
        }

        void BuildMountains()
        {
            for (int i = 0; i < 14; i++)
            {
                float a = i * Mathf.PI * 2f / 14f + 0.2f;
                float distance = R(190f, 240f);
                foliageFree.Push(new Vector3(Mathf.Cos(a) * distance, -2f, Mathf.Sin(a) * distance), Quaternion.Euler(0f, R(0f, 360f), 0f), Vector3.one);
                PropMeshes.Mountain(foliageFree, rng, R(50f, 80f), R(45f, 85f));
                foliageFree.Pop();
            }
        }
    }
}
