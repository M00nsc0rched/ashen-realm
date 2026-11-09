using AshenRealm.AI;
using AshenRealm.Combat;
using AshenRealm.Core;
using AshenRealm.Input;
using AshenRealm.Interactions;
using AshenRealm.Player;
using AshenRealm.Story;
using AshenRealm.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AshenRealm.EditorTools
{
    internal sealed class GeneratedMaterials
    {
        /// <summary>Opaque AshenRealm/PixelLit (all solid geometry; metal sheen and glow come from vertex data).</summary>
        public Material World;
        /// <summary>Alpha-clipped, two-sided, wind-swayed AshenRealm/PixelLit (foliage cards).</summary>
        public Material Foliage;
        public Material Trail;
        public Material Sparks;
        public Material DriftingLeaves;
        public Material Composite;
    }

    internal sealed class PlayerRig
    {
        public GameObject Root;
        public PlayerInputHub Input;
        public Health Health;
        public Stamina Stamina;
        public FirstPersonController Controller;
        public PlayerCombat Combat;
        public PlayerInteractor Interactor;
        public PlayerLifecycle Lifecycle;
        public CameraEffects CameraEffects;
        public AshenRealm.Core.PixelCamera PixelCamera;
        public StatusEffects Status;
        public PlayerAbilities Abilities;
        public Light Torch;
        public PlayerClassRig ClassRig;
    }

    /// <summary>
    /// Builds the player, Cursed Knight and treasure chest hierarchies with procedural meshes and wires
    /// their internal references. Scene-level references are wired by the scene builder after prefab creation.
    /// </summary>
    internal static class ActorFactory
    {
        // ---- Player -------------------------------------------------------------------------------------

        public static PlayerRig CreatePlayer(Vector3 position, float yaw, GeneratedMaterials materials)
        {
            var root = new GameObject("Player") { tag = "Player" };
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.4f;
            cc.skinWidth = 0.04f;
            cc.minMoveDistance = 0f;

            var rig = new PlayerRig
            {
                Root = root,
                Input = root.AddComponent<PlayerInputHub>(),
                Health = root.AddComponent<Health>(),
                Stamina = root.AddComponent<Stamina>(),
                Controller = root.AddComponent<FirstPersonController>(),
                Combat = root.AddComponent<PlayerCombat>(),
                Interactor = root.AddComponent<PlayerInteractor>(),
                Lifecycle = root.AddComponent<PlayerLifecycle>(),
                Status = root.AddComponent<StatusEffects>(),
                Abilities = root.AddComponent<PlayerAbilities>(),
            };
            root.AddComponent<PlayerProgression>();

            var pivot = Child("Camera Pivot", root.transform, new Vector3(0f, 1.62f, 0f), Quaternion.identity);
            var cameraGo = Child("Main Camera", pivot, Vector3.zero, Quaternion.identity).gameObject;
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = 70f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 650f;
            camera.clearFlags = CameraClearFlags.Skybox;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.None;
            cameraData.renderShadows = true;
            cameraGo.AddComponent<AudioListener>();
            rig.CameraEffects = cameraGo.AddComponent<CameraEffects>();
            rig.PixelCamera = cameraGo.AddComponent<AshenRealm.Core.PixelCamera>();
            CreateDriftingLeaves(root.transform, materials.DriftingLeaves);

            // Weapon pivots are the attachment points for future animated models.
            var weaponRig = Child("Weapon Rig", cameraGo.transform, Vector3.zero, Quaternion.identity);
            var weapons = weaponRig.gameObject.AddComponent<FirstPersonWeaponAnimator>();
            var swordPivot = Child("Sword Pivot (Right Hand)", weaponRig, new Vector3(0.36f, -0.4f, 0.55f), Quaternion.Euler(38f, -10f, 2f));
            var shieldPivot = Child("Shield Pivot (Left Hand)", weaponRig, new Vector3(-0.48f, -0.44f, 0.6f), Quaternion.Euler(8f, -40f, 8f));

            var swordBuilder = new MeshBuilder();
            PlayerWeaponMeshes.CrescentPolearm(swordBuilder);
            PlayerWeaponMeshes.ClassHand(swordBuilder, CharacterClass.Knight);
            var swordMesh = GeneratedAssets.SaveMesh(swordBuilder, "Player_Sword");
            var sword = MeshChild("Knight Crescent Polearm", swordPivot, swordMesh, materials.World, Vector3.zero, Quaternion.identity, Vector3.one * 0.82f, false);

            var tip = Child("Blade Tip", sword.transform, new Vector3(-0.18f, 1.4f, 0f), Quaternion.identity);
            var trail = tip.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = materials.Trail;
            trail.time = 0.12f;
            trail.minVertexDistance = 0.02f;
            trail.widthMultiplier = 0.22f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.85f), 0f), new GradientColorKey(new Color(1f, 0.8f, 0.6f), 1f) },
                new[] { new GradientAlphaKey(0.55f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.alignment = LineAlignment.View;
            trail.emitting = false;

            var shieldBuilder = new MeshBuilder();
            PlayerWeaponMeshes.SunShield(shieldBuilder);
            PlayerWeaponMeshes.ClassHand(shieldBuilder, CharacterClass.Knight);
            var shieldMesh = GeneratedAssets.SaveMesh(shieldBuilder, "Player_Shield");
            var shield = MeshChild("Sun Shield", shieldPivot, shieldMesh, materials.World, Vector3.zero, Quaternion.identity, Vector3.one * 0.8f, false);

            // The other classes' weapons share the same pivots; PlayerClassRig leaves one pair enabled.
            GameObject Held(string name, Transform pivot, System.Action<MeshBuilder> build, Vector3 scale,
                CharacterClass handClass = CharacterClass.Gravethief, bool openHand = false)
            {
                var builder = new MeshBuilder();
                build(builder);
                PlayerWeaponMeshes.ClassHand(builder, handClass, openHand);
                return MeshChild(name, pivot, GeneratedAssets.SaveMesh(builder, "Player_" + name.Replace(" ", "")), materials.World, Vector3.zero, Quaternion.identity, scale, false);
            }
            var daggerRight = Held("Dagger Right", swordPivot, PropMeshes.Dagger, Vector3.one);
            var daggerLeft = Held("Bracing Hand", shieldPivot, _ => { }, Vector3.one, CharacterClass.Gravethief, true);
            var staff = Held("Witch Antler Staff", swordPivot, PlayerWeaponMeshes.AntlerStaff, Vector3.one, CharacterClass.Witch);
            var castingHand = Held("Casting Hand", shieldPivot, _ => { }, Vector3.one, CharacterClass.Witch, true);
            var mace = Held("Warden Crescent Right", swordPivot, PlayerWeaponMeshes.WardenBlade, Vector3.one, CharacterClass.Warden);
            var lantern = Held("Warden Crescent Left", shieldPivot, b =>
            {
                b.Push(Matrix4x4.Scale(new Vector3(-1f, 1f, 1f)));
                PlayerWeaponMeshes.WardenBlade(b);
                b.Pop();
            }, Vector3.one, CharacterClass.Warden);
            var boneStaff = Held("Necromancer Sickle Right", swordPivot, PlayerWeaponMeshes.NecromancerSickle, Vector3.one, CharacterClass.Necromancer);
            var boneTalisman = Held("Necromancer Sickle Left", shieldPivot, b =>
            {
                b.Push(Matrix4x4.Scale(new Vector3(-1f, 1f, 1f)));
                PlayerWeaponMeshes.NecromancerSickle(b);
                b.Pop();
            }, Vector3.one, CharacterClass.Necromancer);
            var reagent = Held("Alchemist Ready Hand", swordPivot, _ => { }, Vector3.one, CharacterClass.Alchemist, true);
            var alchemistKit = Held("Reagent Bottle", shieldPivot, PlayerWeaponMeshes.ReagentBottle, Vector3.one, CharacterClass.Alchemist);
            var torchFlask = Held("Alchemist Torch Flask", swordPivot, PlayerWeaponMeshes.ReagentBottle, Vector3.one, CharacterClass.Alchemist);
            torchFlask.SetActive(false);

            // The forge's goods, in Catalog order. They take the place of the class's right-hand weapon.
            GameObject[] bought =
            {
                Held("Bought Rusted Blade", swordPivot, TownMeshes.RustedBlade, Vector3.one * 0.72f),
                Held("Bought Iron Sword", swordPivot, TownMeshes.IronSword, Vector3.one * 0.72f),
                Held("Bought Grave Dagger", swordPivot, TownMeshes.GraveDagger, Vector3.one),
                Held("Bought Pilgrim Staff", swordPivot, TownMeshes.PilgrimStaff, Vector3.one),
            };
            foreach (GameObject weapon in bought) weapon.SetActive(false);

            // The visible carried torch and its light are controlled by the inventory.
            var torchGo = Child("Torch", cameraGo.transform, new Vector3(0f, 0.6f, 1.6f), Quaternion.identity).gameObject;
            var torch = torchGo.AddComponent<Light>();
            torch.type = LightType.Point;
            torch.color = new Color(1f, 0.74f, 0.46f);
            torch.range = 22f;
            torch.intensity = 12f;
            torch.shadows = LightShadows.None;
            torch.enabled = false;
            var flicker = torchGo.AddComponent<LightFlicker>();
            SceneWiring.Set(flicker, "baseIntensity", 12f);
            SceneWiring.Set(flicker, "intensityVariation", 0.9f);
            rig.Torch = torch;

            foreach (var other in new[] { daggerRight, daggerLeft, staff, castingHand, mace, lantern, boneStaff, boneTalisman, reagent, alchemistKit }) other.SetActive(false);

            var classRig = root.AddComponent<PlayerClassRig>();
            SceneWiring.Set(rig.Status, "health", rig.Health);
            SceneWiring.Set(rig.Abilities, "inputSource", rig.Input);
            SceneWiring.Set(rig.Abilities, "combat", rig.Combat);
            SceneWiring.Set(rig.Abilities, "health", rig.Health);
            SceneWiring.Set(rig.Abilities, "status", rig.Status);
            SceneWiring.Set(rig.Abilities, "aimOrigin", cameraGo.transform);
            SceneWiring.Set(rig.Combat, "status", rig.Status);
            SceneWiring.Set(classRig, "health", rig.Health);
            SceneWiring.Set(classRig, "combat", rig.Combat);
            SceneWiring.Set(classRig, "abilities", rig.Abilities);
            SceneWiring.Set(classRig, "weaponSets", new[] { sword, shield, daggerRight, daggerLeft, staff, castingHand, mace, lantern,
                boneStaff, boneTalisman, reagent, alchemistKit });
            SceneWiring.Set(classRig, "boughtWeapons", bought);
            SceneWiring.Set(classRig, "torchFlask", torchFlask);
            rig.ClassRig = classRig;
            TorchBuilder.Build(root, cameraGo.transform, torch, classRig, rig.Controller, materials.World);

            // Internal wiring (prefab-safe).
            SceneWiring.Set(rig.Controller, "inputSource", rig.Input);
            SceneWiring.Set(rig.Controller, "cameraPivot", pivot);
            SceneWiring.Set(rig.Controller, "stamina", rig.Stamina);

            SceneWiring.Set(rig.CameraEffects, "controller", rig.Controller);
            SceneWiring.Set(rig.CameraEffects, "health", rig.Health);

            SceneWiring.Set(weapons, "controller", rig.Controller);
            SceneWiring.Set(weapons, "swordPivot", swordPivot);
            SceneWiring.Set(weapons, "shieldPivot", shieldPivot);
            SceneWiring.Set(weapons, "swordTrail", trail);
            SceneWiring.Set(weapons, "shieldBlockPosition", new Vector3(-0.14f, -0.27f, 0.48f));
            SceneWiring.Set(weapons, "shieldBlockEuler", new Vector3(-6f, 8f, 0f));

            SceneWiring.Set(rig.Combat, "inputSource", rig.Input);
            SceneWiring.Set(rig.Combat, "health", rig.Health);
            SceneWiring.Set(rig.Combat, "stamina", rig.Stamina);
            SceneWiring.Set(rig.Combat, "controller", rig.Controller);
            SceneWiring.Set(rig.Combat, "weapons", weapons);
            SceneWiring.Set(rig.Combat, "aimOrigin", cameraGo.transform);
            SceneWiring.Set(rig.Combat, "cameraEffects", rig.CameraEffects);

            SceneWiring.Set(rig.Interactor, "inputSource", rig.Input);
            SceneWiring.Set(rig.Interactor, "aimOrigin", cameraGo.transform);

            SceneWiring.Set(rig.Lifecycle, "health", rig.Health);
            SceneWiring.Set(rig.Lifecycle, "stamina", rig.Stamina);
            SceneWiring.Set(rig.Lifecycle, "controller", rig.Controller);
            SceneWiring.Set(rig.Lifecycle, "disableWhileDead", new Object[] { rig.Combat, rig.Interactor });
            return rig;
        }

        // ---- Cursed Knight --------------------------------------------------------------------------------

        public static CursedKnightAI CreateKnight(Vector3 position, float yaw, GeneratedMaterials materials)
        {
            var root = new GameObject("Cursed Knight");
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var cc = root.AddComponent<CharacterController>();
            cc.height = 2.0f;
            cc.radius = 0.42f;
            cc.center = new Vector3(0f, 1f, 0f);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.4f;
            cc.skinWidth = 0.04f;

            var health = root.AddComponent<Health>();
            var animator = root.AddComponent<KnightAnimator>();
            var ai = root.AddComponent<CursedKnightAI>();
            var flash = root.AddComponent<HitFlash>();

            // Knight part meshes are authored front = -Z and mirrored here so the model faces +Z (transform.forward).
            Mesh Part(string name, System.Action<MeshBuilder> build)
            {
                var b = new MeshBuilder();
                b.Push(Vector3.zero, Quaternion.identity, new Vector3(1f, 1f, -1f));
                build(b);
                b.Pop();
                return GeneratedAssets.SaveMesh(b, name);
            }

            Mesh torsoMesh = Part("Knight_Torso", PropMeshes.KnightTorso);
            Mesh headMesh = Part("Knight_Head", PropMeshes.KnightHead);
            Mesh eyesMesh = Part("Knight_Eyes", PropMeshes.KnightEyes);
            Mesh armMesh = Part("Knight_Arm", PropMeshes.KnightArm);
            Mesh legMesh = Part("Knight_Leg", PropMeshes.KnightLeg);
            Mesh capeMesh = Part("Knight_Cape", PropMeshes.KnightCape);
            var swordBuilder = new MeshBuilder();
            PropMeshes.Sword(swordBuilder, Pal.SteelDark, 0.7f);
            Mesh swordMesh = GeneratedAssets.SaveMesh(swordBuilder, "Knight_Sword");

            var model = Child("Model", root.transform, Vector3.zero, Quaternion.identity);
            var hips = Child("Hips", model, new Vector3(0f, 1.0f, 0f), Quaternion.identity);
            var legL = Child("Leg L", hips, new Vector3(-0.13f, 0f, 0f), Quaternion.identity);
            var legR = Child("Leg R", hips, new Vector3(0.13f, 0f, 0f), Quaternion.identity);
            var torso = Child("Torso", hips, new Vector3(0f, 0.02f, 0f), Quaternion.identity);
            var head = Child("Head", torso, new Vector3(0f, 0.72f, 0f), Quaternion.identity);
            var armL = Child("Arm L", torso, new Vector3(-0.4f, 0.6f, 0f), Quaternion.Euler(-6f, 0f, -8f));
            var armR = Child("Arm R", torso, new Vector3(0.4f, 0.6f, 0f), Quaternion.Euler(-14f, 0f, 8f));
            var cape = Child("Cape", torso, new Vector3(0f, 0.64f, -0.18f), Quaternion.identity);

            var renderers = new[]
            {
                MeshChild("Leg L Mesh", legL, legMesh, materials.World).GetComponent<Renderer>(),
                MeshChild("Leg R Mesh", legR, legMesh, materials.World).GetComponent<Renderer>(),
                MeshChild("Torso Mesh", torso, torsoMesh, materials.World).GetComponent<Renderer>(),
                MeshChild("Head Mesh", head, headMesh, materials.World).GetComponent<Renderer>(),
                MeshChild("Arm L Mesh", armL, armMesh, materials.World).GetComponent<Renderer>(),
                MeshChild("Arm R Mesh", armR, armMesh, materials.World).GetComponent<Renderer>(),
                MeshChild("Cape Mesh", cape, capeMesh, materials.World).GetComponent<Renderer>(),
                MeshChild("Greatsword", armR, swordMesh, materials.World, new Vector3(0f, -0.72f, 0.03f), Quaternion.Euler(160f, 0f, 0f), Vector3.one * 1.25f).GetComponent<Renderer>(),
            };
            var eyes = MeshChild("Eyes", head, eyesMesh, materials.World, Vector3.zero, Quaternion.identity, Vector3.one, false).GetComponent<Renderer>();

            SceneWiring.Set(animator, "model", model);
            SceneWiring.Set(animator, "torso", torso);
            SceneWiring.Set(animator, "head", head);
            SceneWiring.Set(animator, "armLeft", armL);
            SceneWiring.Set(animator, "armRight", armR);
            SceneWiring.Set(animator, "legLeft", legL);
            SceneWiring.Set(animator, "legRight", legR);
            SceneWiring.Set(animator, "cape", cape);
            SceneWiring.Set(animator, "eyes", eyes);

            SceneWiring.Set(ai, "health", health);
            SceneWiring.Set(ai, "animator", animator);

            SceneWiring.Set(flash, "health", health);
            SceneWiring.Set(flash, "renderers", renderers);
            return ai;
        }

        // ---- Treasure chest -------------------------------------------------------------------------------

        public static TreasureChest CreateChest(Vector3 position, float yaw, GeneratedMaterials materials)
        {
            var root = new GameObject("Treasure Chest");
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.36f, 0f);
            box.size = new Vector3(1.0f, 0.72f, 0.66f);
            var chest = root.AddComponent<TreasureChest>();

            var bodyBuilder = new MeshBuilder();
            PropMeshes.ChestBody(bodyBuilder);
            MeshChild("Body", root.transform, GeneratedAssets.SaveMesh(bodyBuilder, "Chest_Body"), materials.World);
            var goldBuilder = new MeshBuilder();
            PropMeshes.ChestGold(goldBuilder);
            MeshChild("Gold", root.transform, GeneratedAssets.SaveMesh(goldBuilder, "Chest_Gold"), materials.World, Vector3.zero, Quaternion.identity, Vector3.one, false);

            var lidPivot = Child("Lid Hinge", root.transform, new Vector3(0f, 0.5f, 0.3f), Quaternion.identity);
            var lidBuilder = new MeshBuilder();
            PropMeshes.ChestLid(lidBuilder);
            MeshChild("Lid", lidPivot, GeneratedAssets.SaveMesh(lidBuilder, "Chest_Lid"), materials.World);

            var glowGo = Child("Reward Glow", root.transform, new Vector3(0f, 0.95f, -0.1f), Quaternion.identity).gameObject;
            var glow = glowGo.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.74f, 0.35f);
            glow.range = 4f;
            glow.intensity = 0f;
            glow.shadows = LightShadows.None;

            SceneWiring.Set(chest, "lid", lidPivot);
            SceneWiring.Set(chest, "rewardGlow", glow);
            SceneWiring.Set(chest, "openAngle", 110f);
            SceneWiring.Set(chest, "healAmount", 40f);
            SceneWiring.Set(chest, "prompt", GameText.OpenChest);
            SceneWiring.Set(chest, "rewardMessage", GameText.ChestReward);
            return chest;
        }

        // ---- People, encounters and bolts --------------------------------------------------------------

        public static NpcActor CreateNpc(string id, Vector3 position, float yaw, HumanoidStyle look, Transform parent, StoryDirector director, GeneratedMaterials materials) =>
            CreateNpc(id, position, yaw, b => CreatureMeshes.Figure(b, look), parent, director, materials);

        public static NpcActor CreateNpc(string id, Vector3 position, float yaw, System.Action<MeshBuilder> figure, Transform parent, StoryDirector director, GeneratedMaterials materials)
            => CreateNpcBody(id, position, yaw, figure, parent, director, materials, "Person_" + id, 0, null);

        static NpcActor CreateNpcBody(string id, Vector3 position, float yaw, System.Action<MeshBuilder> figure,
            Transform parent, StoryDirector director, GeneratedMaterials materials, string assetName, int outfit,
            System.Func<Vector3, Vector3, Color> light)
        {
            var body = new MeshBuilder();
            figure(body);
            if (light != null) body.Bake(light);
            var root = new GameObject("NPC " + id);
            root.transform.SetParent(parent, true);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = 1.9f;
            capsule.radius = 0.4f;
            capsule.center = new Vector3(0f, 0.95f, 0f);
            GameObject model = MeshChild("Figure", root.transform, GeneratedAssets.SaveMesh(body, assetName), materials.World);
            AddIdleAnimation(root, model.transform, id, materials, outfit, light);
            if (id == "merchant" && light == null) NpcCampGenerator.CityCounter(root.transform, materials);
            var npc = root.AddComponent<NpcActor>();
            SceneWiring.Set(npc, "npcId", id);
            SceneWiring.Set(npc, "director", director);
            return npc;
        }

        public static NpcActor CreateDungeonMerchant(DungeonLayout layout, BiomeStyle biome, Transform zone,
            StoryDirector director, GeneratedMaterials materials)
        {
            var light = NpcCampGenerator.Build(layout, biome, zone, materials);
            int outfit = (int)biome.Decor + 1;
            return CreateNpcBody("merchant", layout.Merchant, 45f, b => PeopleMeshes.Merchant(b, outfit), zone,
                director, materials, "Person_Merchant_" + biome.Name, outfit, light);
        }

        /// <summary>A waiting traveller, with the same living pose as the people who can be spoken to.</summary>
        public static GameObject CreateAmbientNpc(string name, Vector3 position, float yaw, System.Action<MeshBuilder> figure,
            Transform parent, GeneratedMaterials materials)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, true);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = 1.9f;
            capsule.radius = 0.35f;
            capsule.center = new Vector3(0f, 0.95f, 0f);
            var body = new MeshBuilder();
            figure(body);
            string assetName = "Person_Ambient_" + name.Replace(" ", "");
            GameObject model = MeshChild("Figure", root.transform, GeneratedAssets.SaveMesh(body, assetName), materials.World);
            AddIdleAnimation(root, model.transform, name, materials);
            return root;
        }

        static void AddIdleAnimation(GameObject root, Transform model, string id, GeneratedMaterials materials,
            int outfit = 0, System.Func<Vector3, Vector3, Color> light = null)
        {
            var idle = root.AddComponent<NpcIdleAnimator>();
            SceneWiring.Set(idle, "model", model);
            float phase = 0f;
            foreach (char letter in id) phase += letter * 0.13f;
            SceneWiring.Set(idle, "phase", phase);
            NpcRoutine routine = id == "merchant" ? NpcRoutine.Trading : id == "crier" ? NpcRoutine.Reading :
                id == "widow" || id == "monk" ? NpcRoutine.Praying : id == "sergeant" || id == "wanderer" ? NpcRoutine.Seated :
                id.Contains("Guard") || id == "gravedigger" ? NpcRoutine.Watching : NpcRoutine.Attentive;
            SceneWiring.Set(idle, "routine", routine);
            if (id == "merchant")
            {
                var hand = Child("Counting Shoulder", model, new Vector3(0.32f, 1.45f, 0f), Quaternion.identity);
                var countingArm = new MeshBuilder();
                PeopleMeshes.MerchantWorkingArm(countingArm, outfit);
                // Bake with body coordinates, although the mesh is local to its shoulder.
                if (light != null) countingArm.Bake((p, n) => light(p + new Vector3(0.32f, 1.45f, 0f), n));
                else countingArm.Bake((p, n) => new Color(1.1f, 0.92f, 0.74f));
                MeshChild("Counting Hand", hand, GeneratedAssets.SaveMesh(countingArm, "Person_Merchant_Arm_" + outfit), materials.World);
                SceneWiring.Set(idle, "gestureArm", hand);
            }
            if (id != "smith") return;

            var shoulder = Child("Hammer Shoulder", model, new Vector3(0.36f, 1.54f, 0f), Quaternion.identity);
            var arm = new MeshBuilder();
            PeopleMeshes.SmithHammerArm(arm);
            arm.Bake((p, n) => new Color(0.9f, 0.64f, 0.44f));
            MeshChild("Hammer Arm", shoulder, GeneratedAssets.SaveMesh(arm, "Person_SmithHammerArm"), materials.World);
            SceneWiring.Set(idle, "hammerArm", shoulder);

            var sparksGo = Child("Hammer Sparks", root.transform, new Vector3(0f, 1.28f, 1.05f), Quaternion.identity).gameObject;
            var sparks = sparksGo.AddComponent<ParticleSystem>();
            var main = sparks.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.04f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.34f, 0.04f), new Color(1f, 0.9f, 0.35f));
            main.gravityModifier = 0.45f;
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = sparks.emission;
            emission.rateOverTime = 0f;
            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 65f;
            shape.radius = 0.06f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var renderer = sparks.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = materials.Sparks;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            SceneWiring.Set(idle, "sparks", sparks);
        }

        public static EncounterSite CreateEncounter(string id, Vector3 position, Transform parent, StoryDirector director, GeneratedMaterials materials)
        {
            var root = new GameObject("Encounter " + id);
            root.transform.SetParent(parent, true);
            root.transform.position = position;
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = 1.9f;
            capsule.radius = 0.5f;
            capsule.center = new Vector3(0f, 0.95f, 0f);

            var clear = EncounterPeopleGenerator.Create(id, root.transform, materials, false);
            GameObject warped = null;
            if (StoryText.Encounter(id).Warped != null)
            {
                warped = EncounterPeopleGenerator.Create(id, root.transform, materials, true);
            }

            var site = root.AddComponent<EncounterSite>();
            SceneWiring.Set(site, "encounterId", id);
            SceneWiring.Set(site, "director", director);
            SceneWiring.Set(site, "clearFigure", clear);
            if (warped != null)
            {
                SceneWiring.Set(site, "warpedFigure", warped);
                warped.SetActive(false);
            }
            WorldVista.Add(parent, "Encounter_" + id, position + new Vector3(0.35f, 1.6f, -3.25f), position + Vector3.up * 1.05f);
            return site;
        }

        public static ProjectileLibrary CreateProjectiles(GeneratedMaterials materials)
        {
            var kinds = new (BoltKind kind, Pal color, float length, float radius)[]
            {
                (BoltKind.Arrow, Pal.Bone, 0.9f, 0.05f), (BoltKind.Fire, Pal.Lava, 0.5f, 0.2f), (BoltKind.Shadow, Pal.FungalCap, 0.55f, 0.18f),
                (BoltKind.Holy, Pal.Lantern, 0.55f, 0.18f), (BoltKind.Venom, Pal.Slime, 0.45f, 0.18f), (BoltKind.Brine, Pal.FungalGlow, 0.5f, 0.17f),
            };

            var prefabs = new Projectile[kinds.Length];
            foreach (var k in kinds)
            {
                var b = new MeshBuilder();
                b.SetColor(k.color, 0.75f);
                b.Current.Emission = 1f;
                b.Push(new Vector3(0f, 0f, -k.length * 0.5f), Quaternion.Euler(90f, 0f, 0f), Vector3.one);
                b.Prism(Vector3.zero, k.radius, 0f, k.length, 6, true, false);
                b.Pop();
                string name = "Bolt_" + k.kind;
                var go = new GameObject(name);
                MeshChild("Bolt", go.transform, GeneratedAssets.SaveMesh(b, name), materials.World, Vector3.zero, Quaternion.identity, Vector3.one, false);
                go.AddComponent<Projectile>();
                prefabs[(int)k.kind] = UnityEditor.PrefabUtility.SaveAsPrefabAsset(go, $"{GeneratedAssets.PrefabFolder}/{name}.prefab").GetComponent<Projectile>();
                Object.DestroyImmediate(go);
            }

            var library = new GameObject("Projectiles").AddComponent<ProjectileLibrary>();
            SceneWiring.Set(library, "prefabs", prefabs);
            return library;
        }

        public static ZonePortal CreatePortal(string name, Vector3 position, float yaw, Vector3 size, Transform parent, ZoneManager zones, int target, bool arriveAtExit,
            string prompt, string requiredFlag, string lockedMessage, Transform arrival = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(position + Vector3.up * (size.y * 0.5f), Quaternion.Euler(0f, yaw, 0f));
            go.AddComponent<BoxCollider>().size = size;
            var portal = go.AddComponent<ZonePortal>();
            SceneWiring.Set(portal, "zones", zones);
            SceneWiring.Set(portal, "targetZone", target);
            SceneWiring.Set(portal, "arriveAtExit", arriveAtExit);
            SceneWiring.Set(portal, "prompt", prompt);
            SceneWiring.Set(portal, "requiredFlag", requiredFlag);
            SceneWiring.Set(portal, "lockedMessage", lockedMessage);
            if (arrival != null) SceneWiring.Set(portal, "arrival", arrival);
            return portal;
        }

        /// <summary>The goods themselves as something to walk up to: a box the crosshair finds, which nothing bumps into.</summary>
        public static ShopFixture CreateShopFixture(string name, Vector3 centre, Vector3 size, Transform parent, StoryDirector director, ItemSlot goods, string prompt)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.position = centre;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            box.isTrigger = true;
            var fixture = go.AddComponent<ShopFixture>();
            SceneWiring.Set(fixture, "goods", goods);
            SceneWiring.Set(fixture, "prompt", prompt);
            SceneWiring.Set(fixture, "director", director);
            return fixture;
        }

        /// <summary>Golden leaves and motes drifting around the player (world-space particles, follows the player).</summary>
        static void CreateDriftingLeaves(Transform player, Material material)
        {
            var go = new GameObject("Drifting Leaves");
            go.transform.SetParent(player, false);
            go.transform.localPosition = new Vector3(0f, 7f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 11f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0.35f), new Color(0.95f, 0.55f, 0.2f));
            main.gravityModifier = 0.015f;
            main.maxParticles = 160;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 14f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(36f, 10f, 36f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.25f;
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = fade;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // ---- Helpers ----------------------------------------------------------------------------------------

        static Transform Child(string name, Transform parent, Vector3 localPosition, Quaternion localRotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            return go.transform;
        }

        static GameObject MeshChild(string name, Transform parent, Mesh mesh, Material material)
            => MeshChild(name, parent, mesh, material, Vector3.zero, Quaternion.identity, Vector3.one, true);

        static GameObject MeshChild(string name, Transform parent, Mesh mesh, Material material, Vector3 localPosition, Quaternion localRotation,
            Vector3 localScale, bool castShadows = true)
        {
            var go = Child(name, parent, localPosition, localRotation).gameObject;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }
    }
}
