using UnityEngine;
using UnityEngine.Rendering;

namespace AshenRealm.EditorTools
{
    /// <summary>A compact nonblocking workshop by each stair, using baked lantern light instead of extra point lights.</summary>
    internal static class NpcCampGenerator
    {
        static readonly Vector3 Lantern = new Vector3(-0.83f, 1.44f, 0.67f);

        public static System.Func<Vector3, Vector3, Color> Build(DungeonLayout layout, BiomeStyle biome,
            Transform zone, GeneratedMaterials materials)
        {
            var b = new MeshBuilder();
            var rng = new System.Random(4301 + (int)biome.Decor * 71);
            var rotation = Quaternion.Euler(0f, 45f, 0f);
            var camp = new GameObject("Merchant Camp " + biome.Name).transform;
            camp.SetParent(zone, true);
            camp.SetPositionAndRotation(layout.Merchant, rotation);

            // A rug makes this a little inhabited island; it does not change the walkable floor or its collision.
            b.SetColor(biome.Decor == DungeonDecor.Harbor ? Pal.PaintBlue : Pal.ClothRed, 0.36f);
            b.Box(new Vector3(0f, 0.015f, 0.25f), new Vector3(2.25f, 0.025f, 2.2f));
            b.SetColor(Pal.Thatch, 0.53f);
            foreach (float side in new[] { -1f, 1f })
                b.Box(new Vector3(side * 1.06f, 0.032f, 0.25f), new Vector3(0.035f, 0.012f, 2.05f));
            Counter(b, rng);

            // Lantern on a hook, shaded with its own warm pool on the merchant, table and packs.
            WayfarerMeshes.Beam(b, new Vector3(-0.98f, 0f, 0.78f), new Vector3(-0.98f, 1.86f, 0.78f), 0.08f);
            WayfarerMeshes.Beam(b, new Vector3(-0.98f, 1.83f, 0.78f), new Vector3(-0.83f, 1.83f, 0.67f), 0.04f, Pal.SteelDark);
            b.SetColor(Pal.SteelDark, 0.49f);
            b.Box(Lantern + Vector3.up * 0.17f, new Vector3(0.25f, 0.07f, 0.25f));
            b.Box(Lantern + Vector3.down * 0.19f, new Vector3(0.25f, 0.06f, 0.25f));
            foreach (float x in new[] { -0.1f, 0.1f })
                b.Box(Lantern + new Vector3(x, -0.01f, 0f), new Vector3(0.025f, 0.34f, 0.23f));
            b.SetColor(Pal.Lantern, 0.78f);
            b.Box(Lantern, new Vector3(0.15f, 0.29f, 0.15f));

            b.Push(new Vector3(-0.73f, 0f, -0.45f), Quaternion.Euler(0f, -18f, 0f), Vector3.one * 0.65f);
            PropMeshes.Crate(b, rng, 0.65f);
            b.Pop();
            b.SetColor(Pal.Leather, 0.49f);
            b.Box(new Vector3(0.65f, 0.25f, -0.3f), new Vector3(0.47f, 0.5f, 0.4f));
            b.SetColor(Pal.Thatch, 0.5f);
            b.Box(new Vector3(0.65f, 0.48f, -0.3f), new Vector3(0.09f, 0.035f, 0.43f));
            Stock(b, rng, biome.Decor);
            var light = LightAt(biome.Decor);
            b.Bake(light);
            Save(b, camp, "Merchant_Camp_" + biome.Name, materials);
            WorldVista.Add(layout.Root, biome.Name + "_Merchant_Workshop",
                layout.Merchant + rotation * new Vector3(0.05f, 1.65f, 3.1f), layout.Merchant + Vector3.up * 1.05f);
            return light;
        }

        public static void CityCounter(Transform npc, GeneratedMaterials materials)
        {
            var b = new MeshBuilder();
            Counter(b, new System.Random(4300));
            b.Bake((p, n) => new Color(1.1f, 0.92f, 0.74f));
            Save(b, npc, "Merchant_City_Counter", materials);
        }

        static void Counter(MeshBuilder b, System.Random rng)
        {
            b.ShadedBox(new Vector3(0f, 0.94f, 0.77f), new Vector3(1.36f, 0.1f, 0.59f), Pal.Wood, rng, 0.42f, 0.56f);
            foreach (float x in new[] { -0.53f, 0.53f })
            foreach (float z in new[] { 0.56f, 0.97f })
                b.ShadedBox(new Vector3(x, 0.45f, z), new Vector3(0.085f, 0.9f, 0.085f), Pal.WoodDark, rng, 0.33f, 0.45f);
            b.SetColor(Pal.Bone, 0.69f);
            b.Box(new Vector3(-0.12f, 1.002f, 0.75f), new Vector3(0.38f, 0.018f, 0.26f));
            b.SetColor(Pal.Brass, 0.7f);
            for (int i = 0; i < 8; i++)
                b.Box(new Vector3(0.22f + (i % 3) * 0.065f, 1.005f + (i / 3) * 0.017f, 0.78f), new Vector3(0.055f, 0.016f, 0.055f));
        }

        static void Stock(MeshBuilder b, System.Random rng, DungeonDecor decor)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector3 at = new Vector3(-0.48f, 1f, 0.58f + i * 0.15f);
                b.SetColor(decor == DungeonDecor.Fungal ? Pal.FungalGlow : decor == DungeonDecor.Harbor ? Pal.Water : Pal.ClothRed, 0.56f);
                b.Prism(at, 0.055f, 0.045f, 0.14f, 6);
                b.SetColor(Pal.Thatch, 0.5f);
                b.Prism(at + Vector3.up * 0.14f, 0.027f, 0.027f, 0.04f, 6);
            }
            if (decor == DungeonDecor.Fungal)
            {
                for (int i = 0; i < 4; i++)
                {
                    Vector3 at = new Vector3(-0.87f + (i % 2) * 0.19f, 0.42f, -0.47f + (i / 2) * 0.17f);
                    b.SetColor(Pal.Bone, 0.58f); b.Prism(at, 0.03f, 0.025f, 0.18f, 5);
                    b.SetColor(Pal.FungalCap, 0.65f); b.Prism(at + Vector3.up * 0.17f, 0.13f, 0.03f, 0.1f, 7);
                }
            }
            else if (decor == DungeonDecor.Harbor)
            {
                WayfarerMeshes.Bucket(b, new Vector3(0.8f, 0f, 0.55f), 1f);
                b.SetColor(Pal.Steel, 0.6f);
                b.Box(new Vector3(-0.7f, 0.45f, -0.45f), new Vector3(0.38f, 0.05f, 0.1f));
            }
            else if (decor == DungeonDecor.Ember)
            {
                for (int i = 0; i < 3; i++)
                    b.ShadedBox(new Vector3(-0.7f, 0.44f + i * 0.08f, -0.45f), new Vector3(0.42f, 0.07f, 0.18f), Pal.SteelDark, rng, 0.52f, 0.7f);
                b.SetColor(Pal.Brass, 0.63f);
                b.Box(new Vector3(0.49f, 1.03f, 0.75f), new Vector3(0.07f, 0.07f, 0.26f));
            }
            else
            {
                WayfarerMeshes.Books(b, rng, new Vector3(-0.74f, 0.43f, -0.44f), 3);
                b.SetColor(Pal.Bone, 0.63f);
                b.Box(new Vector3(0.5f, 1.02f, 0.74f), new Vector3(0.05f, 0.05f, 0.26f));
                b.Box(new Vector3(0.5f, 1.02f, 0.74f), new Vector3(0.18f, 0.05f, 0.05f));
            }
        }

        static System.Func<Vector3, Vector3, Color> LightAt(DungeonDecor decor)
        {
            Color fill = decor == DungeonDecor.Fungal ? new Color(0.53f, 0.64f, 0.56f) :
                decor == DungeonDecor.Harbor ? new Color(0.5f, 0.58f, 0.7f) :
                decor == DungeonDecor.Ember ? new Color(0.67f, 0.5f, 0.43f) : new Color(0.52f, 0.5f, 0.61f);
            return (p, n) =>
            {
                Vector3 delta = Lantern - p;
                float d = delta.magnitude;
                float facing = d < 0.01f ? 1f : Mathf.Clamp01(Vector3.Dot(n, delta / d) * 0.7f + 0.3f);
                float reach = Mathf.Clamp01(1f - d / 4.5f);
                return fill + new Color(1.45f, 1.04f, 0.62f) * (reach * reach * facing);
            };
        }

        static void Save(MeshBuilder b, Transform parent, string assetName, GeneratedMaterials materials)
        {
            var model = new GameObject("Workshop");
            model.transform.SetParent(parent, false);
            model.AddComponent<MeshFilter>().sharedMesh = GeneratedAssets.SaveMesh(b, assetName);
            var renderer = model.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materials.World;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
