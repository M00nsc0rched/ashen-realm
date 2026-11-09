using AshenRealm.AI;
using UnityEngine;
using UnityEngine.Rendering;

namespace AshenRealm.EditorTools
{
    /// <summary>Each story encounter has a readable occupation and activity, even through the dishonoured view.</summary>
    internal static class EncounterPeopleGenerator
    {
        static readonly Color Skin = new Color(0.66f, 0.46f, 0.34f);
        static readonly Vector3 Shoulder = new Vector3(0.31f, 1.43f, 0f);

        public static GameObject Create(string id, Transform parent, GeneratedMaterials materials, bool warped)
        {
            var figure = new GameObject(warped ? "Warped" : "Clear");
            figure.transform.SetParent(parent, false);
            // The old preview and standing approaches are on the south side of an encounter.
            figure.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var body = new GameObject("Body").transform;
            body.SetParent(figure.transform, false);
            EncounterMotion motion = Motion(id);
            var mesh = new MeshBuilder();
            if (id == "cage") WolfBody(mesh, warped);
            else if (id == "shrine") Shrine(mesh);
            else if (id == "bonechoir") Choir(mesh);
            else Person(mesh, id, warped);
            Save(mesh, body, id + (warped ? "_Warped_Body" : "_Clear_Body"), materials, warped);
            if (id == "beggar") body.localScale = new Vector3(1f, 0.72f, 1f);

            var work = new GameObject("Work Pivot").transform;
            work.SetParent(body, false);
            work.localPosition = id == "cage" ? new Vector3(0f, 0.71f, 0.54f) : id == "shrine" ? new Vector3(0f, 1.62f, 0f) : Shoulder;
            var workMesh = new MeshBuilder();
            if (id == "cage") WolfHead(workMesh, warped);
            else if (id == "shrine") Flame(workMesh);
            else Arm(workMesh, id, warped);
            Save(workMesh, work, id + (warped ? "_Warped_Work" : "_Clear_Work"), materials, warped,
                p => p + work.localPosition);
            Transform secondary = null;
            if (id == "cage")
            {
                secondary = new GameObject("Tail Pivot").transform;
                secondary.SetParent(body, false);
                secondary.localPosition = new Vector3(0f, 0.64f, -0.49f);
                var tail = new MeshBuilder();
                Beam(tail, Vector3.zero, new Vector3(0f, -0.14f, -0.61f), 0.12f, Pal.FurGrey, 0.5f);
                Save(tail, secondary, "Cage_Wolf_Tail_" + warped, materials, warped);
            }
            var props = new MeshBuilder();
            Props(props, id, warped);
            Save(props, figure.transform, id + (warped ? "_Warped_Props" : "_Clear_Props"), materials, warped);
            var idle = figure.AddComponent<EncounterIdleAnimator>();
            SceneWiring.Set(idle, "motion", motion);
            SceneWiring.Set(idle, "body", body);
            SceneWiring.Set(idle, "workPivot", work);
            if (secondary != null) SceneWiring.Set(idle, "secondaryPivot", secondary);
            SceneWiring.Set(idle, "phase", id.Length * 0.67f);
            return figure;
        }

        static EncounterMotion Motion(string id) => id switch
        {
            "well" => EncounterMotion.DrawingWater,
            "beggar" => EncounterMotion.Begging,
            "sporewife" => EncounterMotion.Tending,
            "ferryman" => EncounterMotion.Rowing,
            "forgewidow" => EncounterMotion.Stoking,
            "bonechoir" => EncounterMotion.Chanting,
            "cage" => EncounterMotion.Wolf,
            _ => EncounterMotion.Candle
        };

        static void Paint(MeshBuilder b, Pal texture, Color color)
        {
            b.SetColor(texture, 0.5f);
            b.Current.Color = color.linear;
        }

        static Color Clothes(string id, bool warped)
        {
            Color color = id switch
            {
                "well" => new Color(0.45f, 0.5f, 0.56f),
                "beggar" => new Color(0.5f, 0.38f, 0.25f),
                "sporewife" => new Color(0.3f, 0.5f, 0.36f),
                "ferryman" => new Color(0.25f, 0.35f, 0.44f),
                _ => new Color(0.55f, 0.3f, 0.2f)
            };
            return warped ? Color.Lerp(color, new Color(0.3f, 0.31f, 0.4f), 0.55f) : color;
        }

        static void Person(MeshBuilder b, string id, bool warped)
        {
            Color cloth = Clothes(id, warped);
            Paint(b, Pal.ClothDark, cloth);
            b.Prism(new Vector3(0f, 0.18f, 0f), 0.38f, 0.26f, 0.8f, 8);
            b.Box(new Vector3(0f, 1.22f, 0f), new Vector3(0.5f, 0.62f, 0.34f));
            foreach (float side in new[] { -1f, 1f })
            {
                Paint(b, Pal.Leather, new Color(0.28f, 0.2f, 0.16f));
                b.Box(new Vector3(side * 0.16f, 0.11f, 0.06f), new Vector3(0.19f, 0.22f, 0.35f));
            }
            Paint(b, Pal.Leather, new Color(0.35f, 0.24f, 0.14f));
            b.Box(new Vector3(0f, 1f, 0f), new Vector3(0.55f, 0.075f, 0.38f));
            b.Box(new Vector3(-0.3f, 0.85f, 0f), new Vector3(0.19f, 0.3f, 0.2f));
            Paint(b, Pal.Leather, warped ? Skin * 0.8f : Skin);
            b.Box(new Vector3(0f, 1.73f, 0.01f), new Vector3(0.27f, 0.31f, 0.26f));
            b.Box(new Vector3(0f, 1.68f, 0.16f), new Vector3(0.06f, 0.1f, 0.06f));
            b.SetColor(warped ? Pal.FungalGlow : Pal.Black, warped ? 0.38f : 0.2f);
            foreach (float side in new[] { -1f, 1f })
                b.Box(new Vector3(side * 0.065f, 1.76f, 0.147f), new Vector3(0.037f, 0.027f, 0.016f));
            Paint(b, Pal.ClothDark, cloth * 0.75f);
            b.Box(new Vector3(0f, 1.91f, -0.02f), new Vector3(0.32f, 0.09f, 0.31f));
            b.Box(new Vector3(0f, 1.71f, -0.145f), new Vector3(0.32f, 0.4f, 0.07f));
            Beam(b, new Vector3(-0.3f, 1.45f, 0f), new Vector3(-0.37f, 1.12f, 0.13f), 0.16f, Pal.ClothDark, 0.44f);
            Beam(b, new Vector3(-0.37f, 1.12f, 0.13f), new Vector3(-0.21f, 1.02f, 0.43f), 0.11f, Pal.Leather, 0.5f);
            if (id == "well")
            {
                Paint(b, Pal.ClothDark, new Color(0.7f, 0.67f, 0.55f));
                b.Box(new Vector3(0f, 1.46f, 0.03f), new Vector3(0.67f, 0.2f, 0.45f));
                b.Box(new Vector3(0.12f, 1.18f, 0.205f), new Vector3(0.18f, 0.43f, 0.045f));
            }
            else if (id == "beggar")
            {
                Paint(b, Pal.ClothDark, new Color(0.69f, 0.6f, 0.42f));
                b.Box(new Vector3(-0.17f, 0.56f, 0.28f), new Vector3(0.16f, 0.19f, 0.035f));
                b.Box(new Vector3(0.12f, 1.19f, 0.19f), new Vector3(0.15f, 0.13f, 0.04f));
                Paint(b, Pal.Thatch, new Color(0.6f, 0.59f, 0.53f));
                b.Box(new Vector3(0f, 1.57f, 0.16f), new Vector3(0.23f, 0.13f, 0.06f));
            }
            else if (id == "sporewife")
            {
                for (int i = 0; i < 7; i++)
                {
                    b.SetColor(i % 2 == 0 ? Pal.LeafGreen : Pal.FungalRock, 0.5f);
                    b.Box(new Vector3(-0.25f + i * 0.083f, 1.58f, -0.08f), new Vector3(0.055f, 0.59f + i % 3 * 0.12f, 0.33f));
                }
                b.SetColor(Pal.FungalCap, 0.62f);
                b.Prism(new Vector3(0f, 1.89f, 0f), 0.43f, 0.12f, 0.23f, 10);
                b.SetColor(Pal.FungalGlow, 0.35f);
                b.Prism(new Vector3(0f, 1.9f, 0f), 0.42f, 0.4f, 0.035f, 10);
            }
            else if (id == "ferryman")
            {
                b.SetColor(Pal.ClothDark, 0.42f);
                b.Prism(new Vector3(0f, 1.51f, -0.03f), 0.22f, 0.15f, 0.51f, 8);
                Paint(b, Pal.ClothDark, cloth);
                b.Box(new Vector3(-0.19f, 1.73f, 0.02f), new Vector3(0.1f, 0.38f, 0.25f));
                b.Box(new Vector3(0.19f, 1.73f, 0.02f), new Vector3(0.1f, 0.38f, 0.25f));
            }
            else
            {
                Paint(b, Pal.Leather, new Color(0.32f, 0.23f, 0.16f));
                b.Box(new Vector3(0f, 0.84f, 0.215f), new Vector3(0.39f, 1f, 0.055f));
                b.SetColor(Pal.Steel, 0.65f);
                for (int i = 0; i < 3; i++) b.Box(new Vector3(-0.12f + i * 0.12f, 0.93f, 0.257f), new Vector3(0.04f, 0.25f, 0.025f));
            }
        }

        static void Arm(MeshBuilder b, string id, bool warped)
        {
            if (id == "bonechoir")
            {
                Beam(b, Vector3.zero, new Vector3(0.13f, -0.28f, 0.17f), 0.07f, Pal.Bone, 0.62f);
                Beam(b, new Vector3(0.13f, -0.28f, 0.17f), new Vector3(0.06f, -0.1f, 0.47f), 0.055f, Pal.Bone, 0.66f);
                return;
            }
            Paint(b, Pal.ClothDark, Clothes(id, warped));
            BeamCurrent(b, Vector3.zero, new Vector3(0.1f, -0.27f, 0.08f), 0.15f);
            Paint(b, Pal.Leather, Skin);
            BeamCurrent(b, new Vector3(0.1f, -0.27f, 0.08f), new Vector3(-0.02f, -0.32f, 0.44f), 0.105f);
            b.Box(new Vector3(-0.02f, -0.32f, 0.47f), new Vector3(0.12f, 0.08f, 0.14f));
            if (id == "well") WayfarerMeshes.Bucket(b, new Vector3(-0.02f, -0.72f, 0.49f), 0.65f);
            else if (id == "beggar")
            {
                b.SetColor(Pal.Wood, 0.43f);
                b.Prism(new Vector3(-0.02f, -0.29f, 0.49f), 0.14f, 0.17f, 0.08f, 8);
            }
            else
            {
                Beam(b, new Vector3(-0.02f, id == "ferryman" ? -1.15f : -0.5f, 0.45f), new Vector3(-0.02f, id == "ferryman" ? 0.8f : 0.35f, 0.45f),
                    0.045f, id == "forgewidow" ? Pal.SteelDark : Pal.Wood, 0.5f);
                if (id == "ferryman")
                {
                    b.SetColor(Pal.WoodDark, 0.5f);
                    b.Box(new Vector3(-0.02f, -1.28f, 0.45f), new Vector3(0.19f, 0.46f, 0.06f));
                }
            }
        }

        static void WolfBody(MeshBuilder b, bool warped)
        {
            b.SetColor(warped ? Pal.FurDark : Pal.FurGrey, 0.59f);
            b.Box(new Vector3(0f, 0.58f, 0f), new Vector3(0.48f, 0.42f, 1.13f));
            foreach (float side in new[] { -1f, 1f })
            foreach (float z in new[] { -0.37f, 0.38f })
            {
                float lift = side > 0f && z > 0f ? 0.13f : 0f;
                b.Box(new Vector3(side * 0.17f, 0.22f + lift, z), new Vector3(0.13f, 0.44f, 0.16f));
                b.Box(new Vector3(side * 0.17f, 0.06f + lift, z + 0.07f), new Vector3(0.15f, 0.12f, 0.25f));
            }
            b.SetColor(Pal.ClothRed, 0.38f);
            b.Box(new Vector3(0.17f, 0.32f, 0.38f), new Vector3(0.145f, 0.12f, 0.18f));
        }

        static void WolfHead(MeshBuilder b, bool warped)
        {
            b.SetColor(warped ? Pal.FurDark : Pal.FurGrey, 0.62f);
            b.Box(new Vector3(0f, 0.03f, 0.09f), new Vector3(0.36f, 0.32f, 0.41f));
            b.Box(new Vector3(0f, -0.04f, 0.35f), new Vector3(0.23f, 0.17f, 0.3f));
            foreach (float side in new[] { -1f, 1f })
            {
                b.Prism(new Vector3(side * 0.13f, 0.17f, 0.02f), 0.09f, 0f, 0.23f, 4);
                b.SetColor(warped ? Pal.Lantern : Pal.Black, 0.45f);
                b.Box(new Vector3(side * 0.15f, 0.09f, 0.25f), new Vector3(0.04f, 0.04f, 0.025f));
                b.SetColor(warped ? Pal.FurDark : Pal.FurGrey, 0.62f);
            }
            b.SetColor(Pal.Black, 0.25f);
            b.Box(new Vector3(0f, 0f, 0.5f), new Vector3(0.17f, 0.1f, 0.07f));
        }

        static void Choir(MeshBuilder b)
        {
            var rng = new System.Random(1903);
            for (int singer = -1; singer <= 1; singer++)
            {
                Vector3 at = new Vector3(singer * 0.68f, 0f, singer == 0 ? 0f : -0.22f);
                b.SetColor(Pal.ClothDark, 0.48f);
                b.Prism(at + Vector3.up * 0.09f, 0.29f, 0.14f, 1.3f, 8);
                b.SetColor(Pal.Bone, 0.63f);
                b.Box(at + new Vector3(0f, 1.23f, 0f), new Vector3(0.06f, 0.64f, 0.08f));
                for (int rib = 0; rib < 5; rib++) b.Box(at + new Vector3(0f, 1.09f + rib * 0.1f, 0.11f), new Vector3(0.34f, 0.035f, 0.18f));
                PropMeshes.Skull(b, rng, at + Vector3.up * 1.67f);
                b.SetColor(Pal.Brass, 0.62f);
                b.Box(at + new Vector3(0f, 1.35f, 0.18f), new Vector3(0.42f, 0.06f, 0.03f));
            }
        }

        static void Shrine(MeshBuilder b)
        {
            var rng = new System.Random(1904);
            b.SetColor(Pal.Stone, 0.45f);
            b.Box(new Vector3(0f, 0.13f, 0f), new Vector3(1.36f, 0.26f, 1.08f));
            for (int i = 0; i < 10; i++)
                PropMeshes.Skull(b, rng, new Vector3(Mathf.Cos(i * 2.4f) * (0.5f - i * 0.04f), 0.31f + i * 0.12f,
                    Mathf.Sin(i * 2.4f) * (0.5f - i * 0.04f)));
            b.SetColor(Pal.Bone, 0.62f);
            b.Prism(new Vector3(0f, 1.35f, 0f), 0.06f, 0.055f, 0.27f, 7);
        }

        static void Flame(MeshBuilder b)
        {
            b.SetColor(Pal.Lantern, 0.8f);
            b.Prism(Vector3.zero, 0.048f, 0f, 0.2f, 5);
        }

        static void Props(MeshBuilder b, string id, bool warped)
        {
            var rng = new System.Random(1901 + id.Length * 37);
            if (id == "well")
            {
                b.SetColor(Pal.StoneMoss, 0.58f);
                b.Prism(new Vector3(-1.1f, 0f, -0.1f), 0.69f, 0.69f, 0.88f, 10);
                b.SetColor(Pal.Water, 0.45f);
                b.Prism(new Vector3(-1.1f, 0.89f, -0.1f), 0.57f, 0.57f, 0.014f, 10);
                Beam(b, new Vector3(-1.72f, 0.6f, -0.1f), new Vector3(-1.72f, 1.83f, -0.1f), 0.1f, Pal.WoodDark, 0.4f);
                Beam(b, new Vector3(-0.48f, 0.6f, -0.1f), new Vector3(-0.48f, 1.83f, -0.1f), 0.1f, Pal.WoodDark, 0.4f);
                Beam(b, new Vector3(-1.72f, 1.8f, -0.1f), new Vector3(-0.48f, 1.8f, -0.1f), 0.1f, Pal.WoodDark, 0.4f);
            }
            else if (id == "beggar")
            {
                b.SetColor(Pal.ClothRed, 0.32f);
                b.Box(new Vector3(0f, 0.015f, 0.06f), new Vector3(1.5f, 0.02f, 1.3f));
                b.SetColor(Pal.Leather, 0.52f);
                b.Box(new Vector3(-0.58f, 0.18f, -0.16f), new Vector3(0.33f, 0.36f, 0.35f));
                b.SetColor(Pal.Wood, 0.47f);
                b.Prism(new Vector3(0.5f, 0.04f, 0.49f), 0.15f, 0.19f, 0.1f, 8);
            }
            else if (id == "sporewife")
            {
                b.SetColor(Pal.FungalRock, 0.5f);
                b.Box(new Vector3(0f, 0.14f, -0.44f), new Vector3(1.4f, 0.28f, 0.75f));
                for (int i = 0; i < 8; i++) PropMeshes.Mushroom(b, rng, new Vector3(-0.95f + i * 0.28f, 0f, -0.5f - (i % 3) * 0.17f), 0.35f + i % 3 * 0.15f);
                b.SetColor(Pal.WoodDark, 0.4f);
                b.Box(new Vector3(0.03f, 0.84f, 0.78f), new Vector3(1.2f, 0.09f, 0.5f));
            }
            else if (id == "ferryman")
            {
                b.SetColor(Pal.WoodDark, 0.43f);
                b.Box(new Vector3(0f, 0.04f, 0f), new Vector3(1.15f, 0.08f, 2.9f));
                foreach (float side in new[] { -1f, 1f })
                {
                    b.Box(new Vector3(side * 0.59f, 0.26f, 0f), new Vector3(0.09f, 0.43f, 2.7f));
                    Beam(b, new Vector3(side * 0.59f, 0.26f, 1.3f), new Vector3(0f, 0.34f, 1.85f), 0.09f, Pal.Wood, 0.49f);
                }
                b.SetColor(Pal.Water, 0.4f);
                b.Box(new Vector3(0f, 0.012f, 0f), new Vector3(2.7f, 0.01f, 3.6f));
            }
            else if (id == "forgewidow")
            {
                b.Push(new Vector3(-1.05f, 0f, 0.5f), Quaternion.identity, Vector3.one * 0.7f);
                PropMeshes.Brazier(b, rng);
                b.Pop();
                b.ShadedBox(new Vector3(0.06f, 0.86f, 0.82f), new Vector3(1.2f, 0.16f, 0.48f), Pal.SteelDark, rng, 0.4f, 0.58f);
                foreach (float x in new[] { -0.43f, 0.43f })
                    b.ShadedBox(new Vector3(x, 0.41f, 0.82f), new Vector3(0.11f, 0.82f, 0.3f), Pal.WoodDark, rng, 0.32f, 0.44f);
            }
            else if (id == "cage")
            {
                b.SetColor(Pal.SteelDark, 0.57f);
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI / 6f;
                    b.Box(new Vector3(Mathf.Cos(a) * 1.09f, 1.03f, Mathf.Sin(a) * 1.09f), new Vector3(0.065f, 2.06f, 0.065f));
                }
                foreach (float y in new[] { 0.12f, 1.94f })
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI / 6f, c = (i + 1) * Mathf.PI / 6f;
                    Beam(b, new Vector3(Mathf.Cos(a) * 1.09f, y, Mathf.Sin(a) * 1.09f),
                        new Vector3(Mathf.Cos(c) * 1.09f, y, Mathf.Sin(c) * 1.09f), 0.075f, Pal.SteelDark, 0.54f);
                }
                b.Rock(new Vector3(0.3f, 0.1f, 0.48f), new Vector3(0.43f, 0.23f, 0.47f), rng, Pal.Rock, 0.37f, 0.52f);
            }
            if (id == "shrine" || id == "cage") return;
            // A visible lantern belongs to the scene's baked glow; it adds no realtime light.
            b.SetColor(Pal.SteelDark, 0.48f);
            b.Box(new Vector3(-1f, 0.13f, 0.71f), new Vector3(0.24f, 0.06f, 0.24f));
            b.SetColor(Pal.Lantern, warped ? 0.45f : 0.65f);
            b.Box(new Vector3(-1f, 0.3f, 0.71f), new Vector3(0.15f, 0.29f, 0.15f));
        }

        static void Beam(MeshBuilder b, Vector3 a, Vector3 c, float width, Pal color, float shade)
        {
            b.SetColor(color, shade);
            BeamCurrent(b, a, c, width);
        }

        static void BeamCurrent(MeshBuilder b, Vector3 a, Vector3 c, float width)
        {
            Vector3 span = c - a;
            b.Push((a + c) * 0.5f, Quaternion.FromToRotation(Vector3.up, span.normalized), Vector3.one);
            b.Box(Vector3.zero, new Vector3(width, span.magnitude, width));
            b.Pop();
        }

        static void Save(MeshBuilder b, Transform parent, string name, GeneratedMaterials materials, bool warped,
            System.Func<Vector3, Vector3> toBody = null)
        {
            if (b.VertexCount == 0) return;
            b.Bake((position, normal) =>
            {
                Vector3 p = toBody != null ? toBody(position) : position;
                Vector3 towards = new Vector3(-0.8f, 1.2f, 0.6f) - p;
                float reach = Mathf.Clamp01(1f - towards.magnitude / 4.5f);
                float facing = towards.sqrMagnitude < 0.0001f ? 1f : Mathf.Clamp01(Vector3.Dot(normal, towards.normalized) * 0.7f + 0.3f);
                return (warped ? new Color(0.68f, 0.72f, 0.86f) : new Color(0.83f, 0.78f, 0.68f)) +
                    new Color(0.9f, 0.59f, 0.3f) * (reach * reach * facing);
            });
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = GeneratedAssets.SaveMesh(b, "Encounter_" + name);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materials.World;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
