using AshenRealm.Core;
using AshenRealm.Interactions;
using AshenRealm.Story;
using UnityEngine;
using UnityEngine.Rendering;

namespace AshenRealm.EditorTools
{
    /// <summary>Small working places on existing streets, with a saved purpose beyond their inscriptions.</summary>
    internal static class CityActivitiesGenerator
    {
        public static void Build(Transform city, Vector3 origin, GeneratedMaterials materials)
        {
            var root = new GameObject("City Activities").transform;
            root.SetParent(city, false);
            root.position = origin;
            // Contracts must continue while the whole city zone is inactive.
            new GameObject("City Activity Ledger").AddComponent<CityActivityLedger>();
            var b = new MeshBuilder();
            var rng = new System.Random(41967);

            // The well's east-side bucket is a separate target from the inscription on its south face.
            Vector3 water = new Vector3(-12.8f, 0f, 19f);
            b.Push(water, Quaternion.identity, Vector3.one);
            WayfarerMeshes.Bucket(b, new Vector3(0f, 0.65f, 0f), 0.75f);
            b.SetColor(Pal.Water, 0.6f);
            b.Prism(new Vector3(0f, 0.88f, 0f), 0.16f, 0.16f, 0.012f, 8);
            b.ShadedBox(new Vector3(0f, 0.32f, 0f), new Vector3(0.62f, 0.64f, 0.62f), Pal.WoodDark, rng, 0.3f, 0.43f);
            b.Pop();
            Station(root, "city_well", CityActivityKind.Well, new Vector3(-12.45f, 1f, 19f), new Vector3(0.25f, 0.85f, 0.72f));

            // A wax-sealed writ is on the board's street-facing side, beyond Wick's crate.
            b.SetColor(Pal.Bone, 0.78f);
            b.Box(new Vector3(-5.72f, 1.47f, -45.2f), new Vector3(0.028f, 0.63f, 0.44f));
            b.SetColor(Pal.ClothRed, 0.55f);
            b.Box(new Vector3(-5.69f, 1.26f, -45.2f), new Vector3(0.035f, 0.11f, 0.1f));
            Station(root, "city_watch_board", CityActivityKind.BountyBoard,
                new Vector3(-5.52f, 1.48f, -45.2f), new Vector3(0.2f, 0.8f, 0.68f));

            // A stocked road-facing counter in the right-hand market row.
            for (int i = 0; i < 5; i++)
            {
                Vector3 at = new Vector3(5.12f + (i % 2) * 0.22f, 1.03f, -26.55f + (i / 2) * 0.38f);
                b.SetColor(i % 2 == 0 ? Pal.ClothRed : Pal.PaintBlue, 0.64f);
                b.Prism(at, 0.085f, 0.06f, 0.22f, 6);
                b.SetColor(Pal.Thatch, 0.48f);
                b.Prism(at + Vector3.up * 0.22f, 0.04f, 0.04f, 0.06f, 6);
            }
            b.ShadedBox(new Vector3(5.6f, 1.09f, -26f), new Vector3(0.38f, 0.18f, 0.68f), Pal.ClothDark, rng, 0.34f, 0.5f);
            Station(root, "city_market_supplies", CityActivityKind.MarketSupplies,
                new Vector3(4.7f, 1.13f, -26f), new Vector3(0.2f, 0.8f, 1.25f));

            // A wayside memorial beside, rather than in front of, the chapel's door.
            Vector3 shrine = new Vector3(-21f, 0f, 10.2f);
            b.Push(shrine, Quaternion.identity, Vector3.one);
            b.ShadedBox(new Vector3(0f, 0.37f, 0f), new Vector3(1.05f, 0.74f, 0.8f), Pal.Stone, rng, 0.38f, 0.56f);
            b.SetColor(Pal.Bone, 0.66f);
            b.Box(new Vector3(-0.25f, 1.24f, 0.1f), new Vector3(0.15f, 0.94f, 0.16f));
            b.Box(new Vector3(-0.25f, 1.44f, 0.1f), new Vector3(0.57f, 0.14f, 0.16f));
            foreach (float z in new[] { -0.22f, 0.02f, 0.23f })
            {
                b.SetColor(Pal.Bone, 0.6f);
                b.Prism(new Vector3(0.2f, 0.76f, z), 0.045f, 0.035f, 0.24f, 6);
                b.SetColor(Pal.Lantern, 0.67f);
                b.Prism(new Vector3(0.2f, 1f, z), 0.035f, 0f, 0.12f, 5);
            }
            b.Pop();
            Station(root, "city_memorial", CityActivityKind.MemorialShrine,
                shrine + new Vector3(0.59f, 1.05f, 0f), new Vector3(0.22f, 1.1f, 0.9f));

            // Tea, a blanket and a small stool on the south side of the lantern tree's raised bed.
            Vector3 rest = new Vector3(15.5f, 0f, 15.5f);
            b.Push(rest, Quaternion.identity, Vector3.one);
            b.ShadedBox(new Vector3(0f, 0.45f, 0f), new Vector3(1.9f, 0.14f, 0.65f), Pal.Wood, rng, 0.34f, 0.5f);
            foreach (float x in new[] { -0.7f, 0.7f })
                b.ShadedBox(new Vector3(x, 0.22f, 0f), new Vector3(0.12f, 0.44f, 0.5f), Pal.WoodDark, rng, 0.3f, 0.42f);
            b.SetColor(Pal.ClothRed, 0.44f);
            b.Box(new Vector3(-0.35f, 0.58f, 0f), new Vector3(0.65f, 0.14f, 0.56f));
            b.SetColor(Pal.Brass, 0.53f);
            b.Prism(new Vector3(0.42f, 0.53f, 0f), 0.13f, 0.1f, 0.22f, 8);
            b.Prism(new Vector3(0.72f, 0.53f, -0.12f), 0.075f, 0.085f, 0.11f, 8);
            b.Pop();
            Station(root, "city_lantern_rest", CityActivityKind.LanternRest,
                rest + new Vector3(0f, 0.9f, -0.52f), new Vector3(1.5f, 1f, 0.25f));

            b.Bake((p, n) => new Color(0.84f, 0.76f, 0.65f));
            var model = new GameObject("Service Counters");
            model.transform.SetParent(root, false);
            model.AddComponent<MeshFilter>().sharedMesh = GeneratedAssets.SaveMesh(b, "City_Activities");
            var renderer = model.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materials.World;
            renderer.shadowCastingMode = ShadowCastingMode.On;
        }

        static void Station(Transform root, string id, CityActivityKind kind, Vector3 at, Vector3 size)
        {
            var go = new GameObject(id);
            go.transform.SetParent(root, false);
            go.transform.localPosition = at;
            var trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = size;
            var activity = go.AddComponent<CityActivity>();
            SceneWiring.Set(activity, "activityId", id);
            SceneWiring.Set(activity, "kind", kind);
        }
    }
}
