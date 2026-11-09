using System.Collections.Generic;
using AshenRealm.AI;
using AshenRealm.Combat;
using AshenRealm.Story;
using UnityEngine;
using UnityEngine.Rendering;

namespace AshenRealm.EditorTools
{
    /// <summary>
    /// Builds a scene object for any entry of the bestiary. Humanoids reuse the Cursed Knight's rig and animator, the rest
    /// share <see cref="CreatureAnimator"/>. Meshes are built once per look and shared by every enemy wearing it.
    /// </summary>
    internal sealed class EnemyFactory
    {
        readonly GeneratedMaterials materials;
        readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

        public EnemyFactory(GeneratedMaterials materials) => this.materials = materials;

        static readonly Dictionary<string, HumanoidStyle> Styles = new Dictionary<string, HumanoidStyle>
        {
            ["skeleton"] = new HumanoidStyle { Name = "Skeleton", Skeletal = true, Held = HeldItem.Club, Eyes = Pal.Glow },
            ["archer"] = new HumanoidStyle { Name = "Archer", Skeletal = true, Held = HeldItem.Bow, Eyes = Pal.Lantern },
            ["ghoul"] = new HumanoidStyle { Name = "Ghoul", Skin = Pal.GhoulSkin, Cloth = Pal.ClothDark, Eyes = Pal.FireYellow },
            ["drownedthrall"] = new HumanoidStyle { Name = "Drowned", Skin = Pal.DrownedSkin, Cloth = Pal.RobeRot, Eyes = Pal.FungalGlow },
            ["cultist"] = new HumanoidStyle { Name = "Cultist", Robed = true, Hooded = true, Cloth = Pal.Robe, Trim = Pal.Brass, Skin = Pal.GhoulSkin, Eyes = Pal.FireOrange, Held = HeldItem.Staff },
            ["rotpriest"] = new HumanoidStyle { Name = "RotPriest", Robed = true, Hooded = true, Cloth = Pal.RobeRot, Trim = Pal.Bone, Skin = Pal.GhoulSkin, Eyes = Pal.FungalGlow, Held = HeldItem.Staff },
            ["sporetyrant"] = new HumanoidStyle { Name = "Sporetyrant", Skin = Pal.FungalRock, Cloth = Pal.FungalCap, Eyes = Pal.FungalGlow },
            ["cinderprophet"] = new HumanoidStyle { Name = "CinderProphet", Robed = true, Hooded = true, Cloth = Pal.Basalt, Trim = Pal.Lava, Skin = Pal.GhoulSkin, Eyes = Pal.Lava, Held = HeldItem.Staff },
            ["marrowcantor"] = new HumanoidStyle { Name = "MarrowCantor", Skeletal = true, Crowned = true, Trim = Pal.Brass, Eyes = Pal.FungalGlow, Held = HeldItem.Staff },
            ["morr"] = new HumanoidStyle { Name = "Morr", Robed = true, Hooded = true, Cloth = Pal.Shadow, Trim = Pal.Brass, Skin = Pal.Bone, Eyes = Pal.Lantern, Held = HeldItem.Scythe },
            ["brine"] = new HumanoidStyle { Name = "Brine", Robed = true, Hooded = true, Cloth = Pal.DrownedSkin, Trim = Pal.Plaster, Skin = Pal.DrownedSkin, Eyes = Pal.FungalGlow, Held = HeldItem.Staff },
            ["pyraxes"] = new HumanoidStyle { Name = "Pyraxes", Skin = Pal.Basalt, Cloth = Pal.Basalt, Trim = Pal.Lava, Eyes = Pal.Lava, Molten = true, Held = HeldItem.Staff },
            ["gloamlord"] = new HumanoidStyle { Name = "Gloamlord", Skin = Pal.Bone, Cloth = Pal.Shadow, Trim = Pal.Brass, Eyes = Pal.FungalGlow, Crowned = true, Held = HeldItem.GreatSword },
        };

        public static HumanoidStyle Villager(string name, Pal cloth, Pal skin = Pal.Plaster, bool hooded = false) =>
            new HumanoidStyle { Name = name, Robed = true, Hooded = hooded, Cloth = cloth, Trim = Pal.Leather, Skin = skin };

        public EnemyAI Create(string id, Vector3 position, float yaw, Transform parent, Transform player, ProjectileLibrary projectiles,
            ImpactEffects impacts, Vector3[] patrol)
        {
            EnemyDefinition d = Catalog.Enemy(id);
            var root = new GameObject(d.Name);
            root.transform.SetParent(parent, true);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            // Capsules stay tall even for rats, so a level swing connects without staring at the floor.
            var cc = root.AddComponent<CharacterController>();
            cc.height = Mathf.Max(1.7f, 1.95f * d.Scale);
            cc.radius = Mathf.Clamp(0.42f * d.Scale, 0.32f, 1.1f);
            cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.3f;
            cc.skinWidth = 0.04f;

            var health = root.AddComponent<Health>();
            var status = root.AddComponent<StatusEffects>();
            var ai = root.AddComponent<EnemyAI>();
            var flash = root.AddComponent<HitFlash>();

            var renderers = new List<Renderer>();
            Component animator;
            switch (d.Body)
            {
                case EnemyBody.Humanoid: animator = BuildHumanoid(root.transform, id, d.Scale, renderers); break;
                case EnemyBody.Beast: animator = BuildBeast(root.transform, id, d.Scale, renderers); break;
                case EnemyBody.Flyer: animator = BuildFlyer(root.transform, id, d.Scale, renderers); break;
                default: animator = BuildBlob(root.transform, id, d.Scale, renderers); break;
            }
            if (d.Rank >= EnemyRank.Elite)
            {
                Transform model = root.transform.Find("Model");
                Transform attachment = d.Body == EnemyBody.Humanoid ? model.Find("Hips/Torso") : model;
                renderers.Add(Part("Signature Regalia", attachment, Shared("Creature_Signature_" + id, b =>
                {
                    if (d.Body == EnemyBody.Humanoid) b.Push(Vector3.down * 1.02f, Quaternion.identity, Vector3.one);
                    EnemySignatureMeshes.Regalia(b, id);
                    if (d.Body == EnemyBody.Humanoid) b.Pop();
                })));
            }
            EnemyAttackTelegraph warning = BuildTelegraph(root.transform);

            SceneWiring.Set(status, "health", health);
            SceneWiring.Set(ai, "enemyId", id);
            SceneWiring.Set(ai, "health", health);
            SceneWiring.Set(ai, "status", status);
            SceneWiring.Set(ai, "animatorComponent", animator);
            SceneWiring.Set(ai, "target", player);
            SceneWiring.Set(ai, "projectiles", projectiles);
            SceneWiring.Set(ai, "impactEffects", impacts);
            SceneWiring.Set(ai, "telegraph", warning);
            SceneWiring.Set(ai, "patrolPoints", patrol);
            SceneWiring.Set(ai, "eyeHeight", Mathf.Max(1.2f, cc.height * 0.8f));
            SceneWiring.Set(flash, "health", health);
            SceneWiring.Set(flash, "renderers", renderers.ToArray());
            return ai;
        }

        Mesh Shared(string name, System.Action<MeshBuilder> build)
        {
            if (meshes.TryGetValue(name, out Mesh mesh)) return mesh;
            var b = new MeshBuilder();
            build(b);
            mesh = GeneratedAssets.SaveMesh(b, name);
            meshes[name] = mesh;
            return mesh;
        }

        Component BuildHumanoid(Transform root, string id, float scale, List<Renderer> renderers)
        {
            HumanoidStyle s = Styles[id];
            var animator = root.gameObject.AddComponent<KnightAnimator>();
            string key = "Creature_" + s.Name;

            var model = Child("Model", root, Vector3.zero, Quaternion.identity);
            model.localScale = Vector3.one * scale;
            var hips = Child("Hips", model, new Vector3(0f, 1f, 0f), Quaternion.identity);
            var legL = Child("Leg L", hips, new Vector3(-0.13f, 0f, 0f), Quaternion.identity);
            var legR = Child("Leg R", hips, new Vector3(0.13f, 0f, 0f), Quaternion.identity);
            var torso = Child("Torso", hips, new Vector3(0f, 0.02f, 0f), Quaternion.identity);
            var head = Child("Head", torso, new Vector3(0f, 0.72f, 0f), Quaternion.identity);
            var armL = Child("Arm L", torso, new Vector3(-0.4f, 0.6f, 0f), Quaternion.Euler(-6f, 0f, -8f));
            var armR = Child("Arm R", torso, new Vector3(0.4f, 0.6f, 0f), Quaternion.Euler(-14f, 0f, 8f));

            Mesh leg = Shared(key + "_Leg", b => CreatureMeshes.Leg(b, s));
            Mesh arm = Shared(key + "_Arm", b => CreatureMeshes.Arm(b, s));
            if (!s.Robed)
            {
                renderers.Add(Part("Leg L Mesh", legL, leg));
                renderers.Add(Part("Leg R Mesh", legR, leg));
            }
            renderers.Add(Part("Torso Mesh", torso, Shared(key + "_Torso", b => CreatureMeshes.Torso(b, s))));
            renderers.Add(Part("Head Mesh", head, Shared(key + "_Head", b => CreatureMeshes.Head(b, s))));
            renderers.Add(Part("Arm L Mesh", armL, arm));
            renderers.Add(Part("Arm R Mesh", armR, arm));
            if (s.Held != HeldItem.None)
            {
                bool signature = id == "brine" || id == "pyraxes" || id == "cinderprophet";
                Mesh held = Shared(signature ? "Held_" + id : "Held_" + s.Held,
                    b => { if (signature) EnemySignatureMeshes.Weapon(b, id); else CreatureMeshes.Held(b, s.Held); });
                var grip = Child("Held", armR, new Vector3(0f, -0.72f, 0.03f), Quaternion.Euler(s.Held == HeldItem.GreatSword ? 160f : 75f, 0f, 0f));
                if (s.Held == HeldItem.GreatSword) grip.localScale = Vector3.one * 1.25f;
                renderers.Add(Part("Held Mesh", grip, held));
            }
            Renderer eyes = Part("Eyes", head, Shared(key + "_Eyes", b => CreatureMeshes.Eyes(b, s)), false);

            SceneWiring.Set(animator, "model", model);
            SceneWiring.Set(animator, "torso", torso);
            SceneWiring.Set(animator, "head", head);
            SceneWiring.Set(animator, "armLeft", armL);
            SceneWiring.Set(animator, "armRight", armR);
            SceneWiring.Set(animator, "legLeft", legL);
            SceneWiring.Set(animator, "legRight", legR);
            SceneWiring.Set(animator, "eyes", eyes);
            return animator;
        }

        Component BuildBeast(Transform root, string id, float scale, List<Renderer> renderers)
        {
            bool rat = id == "rat" || id == "gravemaw";
            bool bony = id == "bonehound";
            Pal fur = bony ? Pal.Bone : rat ? Pal.FurGrey : Pal.FurDark;
            Pal under = rat ? Pal.Plaster : Pal.FurGrey;
            Pal eye = bony ? Pal.FungalGlow : Pal.Glow;
            string key = "Creature_Beast_" + fur;

            var animator = root.gameObject.AddComponent<CreatureAnimator>();
            var model = Child("Model", root, Vector3.zero, Quaternion.identity);
            model.localScale = Vector3.one * scale;
            renderers.Add(Part("Body", model, Shared(key + "_Body", b => CreatureMeshes.BeastBody(b, fur, under, bony))));
            var head = Child("Head", model, new Vector3(0f, 0.92f, 0.5f), Quaternion.identity);
            renderers.Add(Part("Head Mesh", head, Shared(key + "_Head", b => CreatureMeshes.BeastHead(b, fur, eye))));
            var tail = Child("Tail", model, new Vector3(0f, 0.82f, -0.48f), Quaternion.identity);
            renderers.Add(Part("Tail Mesh", tail, Shared(key + "_Tail", b => CreatureMeshes.BeastTail(b, fur))));

            Mesh legMesh = Shared(key + "_Leg", b => CreatureMeshes.BeastLeg(b, fur));
            var legs = new Transform[4];
            Vector3[] hips = { new Vector3(-0.17f, 0.6f, 0.34f), new Vector3(0.17f, 0.6f, 0.34f), new Vector3(-0.17f, 0.6f, -0.34f), new Vector3(0.17f, 0.6f, -0.34f) };
            for (int i = 0; i < 4; i++)
            {
                legs[i] = Child("Leg " + i, model, hips[i], Quaternion.identity);
                renderers.Add(Part("Leg Mesh", legs[i], legMesh));
            }

            SceneWiring.Set(animator, "body", EnemyBody.Beast);
            SceneWiring.Set(animator, "model", model);
            SceneWiring.Set(animator, "head", head);
            SceneWiring.Set(animator, "tail", tail);
            SceneWiring.Set(animator, "limbs", legs);
            return animator;
        }

        Component BuildFlyer(Transform root, string id, float scale, List<Renderer> renderers)
        {
            bool shrouded = id == "shade" || id == "undertow";
            Pal skin = id == "imp" ? Pal.ImpSkin : id == "undertow" ? Pal.DrownedSkin : shrouded ? Pal.Shadow : Pal.FurDark;
            Pal eye = id == "imp" ? Pal.FireYellow : shrouded ? Pal.FungalGlow : Pal.Glow;
            string key = "Creature_Flyer_" + skin;

            var animator = root.gameObject.AddComponent<CreatureAnimator>();
            var model = Child("Model", root, Vector3.zero, Quaternion.identity);
            model.localScale = Vector3.one * scale;
            renderers.Add(Part("Body", model, Shared(key + "_Body", b => CreatureMeshes.FlyerBody(b, skin, eye, shrouded))));

            var wings = new List<Transform>();
            if (!shrouded)
            {
                for (int i = 0; i < 2; i++)
                {
                    float side = i == 0 ? -1f : 1f;
                    var wing = Child(i == 0 ? "Wing L" : "Wing R", model, new Vector3(0.13f * side, 0.1f, 0f), Quaternion.identity);
                    renderers.Add(Part("Wing Mesh", wing, Shared(key + "_Wing" + i, b => CreatureMeshes.Wing(b, skin, side))));
                    wings.Add(wing);
                }
            }

            SceneWiring.Set(animator, "body", EnemyBody.Flyer);
            SceneWiring.Set(animator, "model", model);
            SceneWiring.Set(animator, "limbs", wings.ToArray());
            SceneWiring.Set(animator, "hoverHeight", shrouded ? 1.1f : 1.5f);
            return animator;
        }

        Component BuildBlob(Transform root, string id, float scale, List<Renderer> renderers)
        {
            bool fungal = id == "mycel";
            Pal color = fungal ? Pal.FungalRock : Pal.Slime;
            var animator = root.gameObject.AddComponent<CreatureAnimator>();
            var model = Child("Model", root, Vector3.zero, Quaternion.identity);
            model.localScale = Vector3.one * scale;
            renderers.Add(Part("Body", model, Shared("Creature_Blob_" + color, b => CreatureMeshes.Blob(b, color, fungal ? Pal.FungalGlow : Pal.Glow, fungal))));
            SceneWiring.Set(animator, "body", EnemyBody.Blob);
            SceneWiring.Set(animator, "model", model);
            return animator;
        }

        EnemyAttackTelegraph BuildTelegraph(Transform root)
        {
            var marker = Child("Attack Warning", root, Vector3.zero, Quaternion.identity);
            var warning = marker.gameObject.AddComponent<EnemyAttackTelegraph>();
            foreach (string field in new[] { "area", "inner", "lane" })
            {
                var part = Child(field, marker, Vector3.zero, Quaternion.identity);
                Mesh mesh = Shared(field == "lane" ? "Warning_Lane" : "Warning_Ring", b =>
                {
                    b.SetColor(Pal.Plaster, 0.7f);
                    b.Current.Emission = 0.45f;
                    if (field == "lane")
                    {
                        b.Box(new Vector3(-0.5f, 0f, 0f), new Vector3(0.045f, 0.015f, 1f));
                        b.Box(new Vector3(0.5f, 0f, 0f), new Vector3(0.045f, 0.015f, 1f));
                        b.Box(new Vector3(0f, 0f, -0.5f), new Vector3(1f, 0.015f, 0.04f));
                        b.Box(new Vector3(0f, 0f, 0.5f), new Vector3(1f, 0.015f, 0.04f));
                    }
                    else
                    {
                        for (int i = 0; i < 48; i++)
                        {
                            float a = i * Mathf.PI / 24f, c = (i + 1) * Mathf.PI / 24f;
                            Vector3 u = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                            Vector3 v = new Vector3(Mathf.Cos(c), 0f, Mathf.Sin(c));
                            b.QuadOutward(u, v, v * 0.965f, u * 0.965f, Vector3.down);
                        }
                    }
                });
                part.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = part.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = materials.World;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                SceneWiring.Set(warning, field, part);
                part.gameObject.SetActive(false);
            }
            return warning;
        }

        Renderer Part(string name, Transform parent, Mesh mesh, bool castShadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materials.World;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return renderer;
        }

        static Transform Child(string name, Transform parent, Vector3 localPosition, Quaternion localRotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            return go.transform;
        }
    }
}
