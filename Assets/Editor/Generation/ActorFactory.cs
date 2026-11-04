using AshenRealm.AI;
using AshenRealm.Combat;
using AshenRealm.Input;
using AshenRealm.Interactions;
using AshenRealm.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AshenRealm.EditorTools
{
    internal sealed class GeneratedMaterials
    {
        public Material World;
        public Material Metal;
        public Material Fire;
        public Material Glow;
        public Material Trail;
        public Material Sparks;
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
            };

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

            // Weapon pivots are the attachment points for future animated models.
            var weaponRig = Child("Weapon Rig", cameraGo.transform, Vector3.zero, Quaternion.identity);
            var weapons = weaponRig.gameObject.AddComponent<FirstPersonWeaponAnimator>();
            var swordPivot = Child("Sword Pivot (Right Hand)", weaponRig, new Vector3(0.36f, -0.4f, 0.55f), Quaternion.Euler(38f, -10f, 2f));
            var shieldPivot = Child("Shield Pivot (Left Hand)", weaponRig, new Vector3(-0.48f, -0.44f, 0.6f), Quaternion.Euler(8f, -40f, 8f));

            var swordBuilder = new MeshBuilder();
            PropMeshes.Sword(swordBuilder);
            var swordMesh = GeneratedAssets.SaveMesh(swordBuilder, "Player_Sword");
            var sword = MeshChild("Sword", swordPivot, swordMesh, materials.Metal, Vector3.zero, Quaternion.identity, Vector3.one * 0.7f, false);

            var tip = Child("Blade Tip", sword.transform, new Vector3(0f, 0.92f, 0f), Quaternion.identity);
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
            PropMeshes.Shield(shieldBuilder);
            var shieldMesh = GeneratedAssets.SaveMesh(shieldBuilder, "Player_Shield");
            MeshChild("Shield", shieldPivot, shieldMesh, materials.World, Vector3.zero, Quaternion.identity, Vector3.one * 0.8f, false);

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
                MeshChild("Leg L Mesh", legL, legMesh, materials.Metal).GetComponent<Renderer>(),
                MeshChild("Leg R Mesh", legR, legMesh, materials.Metal).GetComponent<Renderer>(),
                MeshChild("Torso Mesh", torso, torsoMesh, materials.Metal).GetComponent<Renderer>(),
                MeshChild("Head Mesh", head, headMesh, materials.Metal).GetComponent<Renderer>(),
                MeshChild("Arm L Mesh", armL, armMesh, materials.Metal).GetComponent<Renderer>(),
                MeshChild("Arm R Mesh", armR, armMesh, materials.Metal).GetComponent<Renderer>(),
                MeshChild("Cape Mesh", cape, capeMesh, materials.World).GetComponent<Renderer>(),
                MeshChild("Greatsword", armR, swordMesh, materials.Metal, new Vector3(0f, -0.72f, 0.03f), Quaternion.Euler(160f, 0f, 0f), Vector3.one * 1.25f).GetComponent<Renderer>(),
            };
            var eyes = MeshChild("Eyes", head, eyesMesh, materials.Glow, Vector3.zero, Quaternion.identity, Vector3.one, false).GetComponent<Renderer>();

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
            MeshChild("Gold", root.transform, GeneratedAssets.SaveMesh(goldBuilder, "Chest_Gold"), materials.Metal, Vector3.zero, Quaternion.identity, Vector3.one, false);

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
            SceneWiring.Set(chest, "rewardMessage", "Found 48 gold and a Healing Draught (+40 health).");
            return chest;
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
