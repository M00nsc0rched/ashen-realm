using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AshenRealm.AI;
using AshenRealm.Combat;
using AshenRealm.Core;
using AshenRealm.Interactions;
using AshenRealm.Story;
using AshenRealm.UI;
using AshenRealm.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

namespace AshenRealm.EditorTools
{
    /// <summary>
    /// Ashen Realm > Generate Prototype: builds Assets/Scenes/Prototype_Valley.unity and all of its generated
    /// assets from scratch. Safe to run repeatedly: the scene is recreated, assets are updated in place (GUIDs kept)
    /// and stale generated meshes are removed.
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        const string LogPrefix = "[AshenRealm]";

        [MenuItem("Ashen Realm/Generate Prototype", priority = 0)]
        public static void GenerateFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            bool ok = Generate();
            if (ok && !Application.isBatchMode)
                EditorUtility.DisplayDialog("Ashen Realm", "Prototype_Valley generated and saved.\n\nPress Play to explore the valley.", "OK");
        }

        [MenuItem("Ashen Realm/Open Prototype Scene", priority = 1)]
        public static void OpenScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(GeneratedAssets.ScenePath);
        }

        public static bool Generate()
        {
            var timer = Stopwatch.StartNew();
            try
            {
                Progress("Preparing folders", 0.02f);
                GeneratedAssets.EnsureFolders();
                GeneratedAssets.BeginMeshPass();
                EquipmentIconBuilder.Reset();

                Progress("Pixel-art textures, sky and UI sprites", 0.06f);
                var detailArray = GeneratedAssets.SaveTextureArray(PixelTextures.CreateArray(), GeneratedAssets.DetailArrayPath);
                var sky = GeneratedAssets.WriteTexture<Texture2D>(PixelTextures.PaintSky(1024, 512, -(SunRotation * Vector3.forward), out Color horizon),
                    GeneratedAssets.SkyPath, false, FilterMode.Point, true, TextureWrapMode.Repeat);
                var leaf = GeneratedAssets.WriteTexture<Texture2D>(LeafTexture(), GeneratedAssets.TextureFolder + "/T_Leaf.png", false, FilterMode.Point);
                var sprites = HudBuilder.CreateSprites();

                Progress("Materials", 0.1f);
                var materials = CreateMaterials(detailArray, leaf);
                Material skybox = CreateSkybox(sky);
                VolumeProfile volumeProfile = CreateVolumeProfile();
                ConfigureRenderPipelineAssets();

                Progress("Creating scene", 0.14f);
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                CreateSceneServices(skybox, volumeProfile, horizon);

                Progress("Generating valley (terrain, forest, ruins, hamlet)", 0.2f);
                ValleyLayout layout = new ValleyGenerator(materials.World, materials.Foliage).Generate();

                Progress("Actors", 0.5f);
                var impacts = CreateImpactEffects(materials.Sparks);
                var projectiles = ActorFactory.CreateProjectiles(materials);

                var valley = NewZone("Zone Valley", Vector3.zero);
                layout.Root.SetParent(valley.transform, true);
                var spawn = new GameObject("Player Spawn").transform;
                spawn.SetParent(valley.transform, false);
                spawn.SetPositionAndRotation(layout.PlayerSpawn, Quaternion.Euler(0f, layout.PlayerYaw, 0f));
                var gateArrival = Marker("Gate Arrival", valley.transform, layout.GateArrival, 180f);
                var gateObjective = Marker("Gate Objective", valley.transform, layout.GatePosition, 0f);

                PlayerRig player = ActorFactory.CreatePlayer(layout.PlayerSpawn, layout.PlayerYaw, materials);
                CursedKnightAI knight = ActorFactory.CreateKnight(layout.KnightSpawn, layout.KnightYaw, materials);
                var chest = ActorFactory.CreateChest(layout.ChestPosition, layout.ChestYaw, materials);
                SceneWiring.Set(chest, "saveKey", "chest_valley");
                SceneWiring.Set(chest, "goldReward", 48);
                SceneWiring.Set(chest, "potionReward", 1);

                Progress("Prefabs", 0.54f);
                SavePrefab(player.Root, "Player");
                SavePrefab(knight.gameObject, "CursedKnight");
                SavePrefab(chest.gameObject, "TreasureChest");
                knight.transform.SetParent(valley.transform, true);
                chest.transform.SetParent(valley.transform, true);

                // Scene-level references (stored as prefab instance overrides).
                SceneWiring.Set(player.Lifecycle, "spawnPoint", spawn);
                SceneWiring.Set(player.Combat, "impactEffects", impacts);
                SceneWiring.Set(player.Abilities, "visualEffects", impacts);
                SceneWiring.Set(player.Combat, "projectiles", projectiles);
                SceneWiring.Set(knight, "target", player.Root.transform);
                SceneWiring.Set(knight, "impactEffects", impacts);
                SceneWiring.Set(knight, "patrolPoints", layout.PatrolPoints);

                Progress("HUD, touch controls and pixel screen", 0.58f);
                MenuArt menuArt = MenuBuilder.LoadArt(sprites);
                var hud = HudBuilder.Build(sprites, menuArt, player.Input, player.Health, player.Stamina, player.Interactor, player.Lifecycle, player.Abilities,
                    player.Combat, player.CameraEffects.transform, layout.GatePosition);
                var pixelScreen = HudBuilder.BuildPixelScreen(materials.Composite);
                pixelScreen.enabled = false; // PixelCamera enables it at runtime; edit mode shows the world camera directly
                SceneWiring.Set(player.PixelCamera, "output", pixelScreen);
                SceneWiring.Set(player.PixelCamera, "screenCamera", CreateScreenCamera());

                var zoneManager = new GameObject("Zone Manager").AddComponent<ZoneManager>();
                SceneWiring.Set(player.Abilities, "zones", zoneManager);
                var storyGo = new GameObject("Story");
                var session = storyGo.AddComponent<GameSession>();
                var director = storyGo.AddComponent<StoryDirector>();
                var keeper = storyGo.AddComponent<SoulKeeper>();
                MenuBuilder.Build(menuArt, player.Input, player.CameraEffects.GetComponent<Camera>(), keeper, director, hud.Pause, hud.Layout);
                SceneWiring.Set(director, "keeper", keeper);
                SceneWiring.Set(keeper, "session", session);
                SceneWiring.Set(keeper, "zones", zoneManager);
                SceneWiring.Set(keeper, "player", player.Controller);
                SceneWiring.Set(keeper, "health", player.Health);
                SceneWiring.Set(keeper, "abilities", player.Abilities);
                SceneWiring.Set(director, "session", session);
                SceneWiring.Set(director, "dialogue", hud.Dialogue);
                SceneWiring.Set(director, "zones", zoneManager);
                SceneWiring.Set(director, "pauseMenu", hud.Pause);
                var portraits = storyGo.AddComponent<PortraitLibrary>();
                SceneWiring.Set(portraits, "faces", MenuBuilder.LoadPortraits());
                SceneWiring.Set(director, "portraits", portraits);
                SceneWiring.Set(director, "shop", hud.Shop);
                SceneWiring.Set(director, "classRig", player.ClassRig);
                SceneWiring.Set(director, "playerHealth", player.Health);
                SceneWiring.Set(director, "playerAbilities", player.Abilities);

                var zones = new List<Zone> { valley };
                CreateArrivalLore(layout.Arrival, valley.transform);
                foreach (var person in layout.Arrival.People)
                    ActorFactory.CreateAmbientNpc(person.name, person.position, person.yaw, person.figure, valley.transform, materials);
                ActorFactory.CreatePortal("Gate To Hollowgate", layout.GatePosition, 0f, new Vector3(6f, 9f, 1f), valley.transform, zoneManager, ZoneNumbers.City, false,
                    GameText.GateToCity, StoryDirector.DeadFlag("knight"), GameText.GateBarred);

                Progress("Hollowgate", 0.62f);
                Vector3 cityOrigin = ZoneOrigin(ZoneNumbers.City);
                var city = NewZone("Zone Hollowgate", cityOrigin);
                CityLayout cityLayout = new CityGenerator(materials.World, cityOrigin, ValleyGenerator.Seed + 1).Generate(city.transform);
                var cityEntry = Marker("City Entry", city.transform, cityLayout.Entry, cityLayout.EntryYaw);
                var cityArrival = Marker("City Tavern Arrival", city.transform, cityLayout.TavernArrival, 180f);
                var forgeArrival = Marker("City Forge Arrival", city.transform, cityLayout.ForgeArrival, 270f);
                var chapelArrival = Marker("City Chapel Arrival", city.transform, cityLayout.ChapelArrival, 90f);
                var cityObjective = Marker("City Objective", city.transform, cityLayout.TavernDoor, 0f);
                foreach (var person in cityLayout.People)
                    ActorFactory.CreateNpc(person.id, person.position, person.yaw, person.figure, city.transform, director, materials);
                CityActivitiesGenerator.Build(city.transform, cityOrigin, materials);
                ActorFactory.CreatePortal("Gate To Valley", cityLayout.Gate, 0f, new Vector3(9f, 7f, 1f), city.transform, zoneManager, 0, true,
                    GameText.GateToValley, "", "");
                ActorFactory.CreatePortal("Tavern Door", cityLayout.TavernDoor, 0f, new Vector3(2.6f, 3.4f, 0.6f), city.transform, zoneManager, ZoneNumbers.Tavern, false,
                    GameText.EnterTavern, "", "");
                ActorFactory.CreatePortal("Forge Door", cityLayout.ForgeDoor, 90f, new Vector3(2.8f, 3.4f, 0.6f), city.transform, zoneManager, ZoneNumbers.Forge, false,
                    GameText.EnterForge, "", "");
                ActorFactory.CreatePortal("Chapel Door", cityLayout.ChapelDoor, 90f, new Vector3(2.8f, 3.8f, 0.6f), city.transform, zoneManager, ZoneNumbers.Chapel, false,
                    GameText.EnterChapel, "", "");
                var gallows = new GameObject("Gallows Lore");
                gallows.transform.SetParent(city.transform, true);
                gallows.transform.position = cityLayout.Gallows;
                var gallowsBox = gallows.AddComponent<BoxCollider>();
                gallowsBox.size = new Vector3(11f, 4f, 8.6f);
                gallowsBox.isTrigger = true;
                var gallowsLore = gallows.AddComponent<LoreInspectable>();
                SceneWiring.Set(gallowsLore, "prompt", GameText.InspectGallows);
                SceneWiring.Set(gallowsLore, "loreText", GameText.GallowsLore);
                ConfigureZone(city, ZoneNumbers.City, false, new Color(0.54f, 0.5f, 0.6f), horizon, 28f, 300f, cityEntry, cityArrival, cityObjective);
                zones.Add(city);

                Progress("The Rope and Lantern", 0.64f);
                zones.Add(BuildTavern(materials, zoneManager, director));
                Progress("The Cold Forge", 0.645f);
                zones.Add(BuildForge(materials, zoneManager, director, forgeArrival));
                Progress("The Grey Chapel", 0.65f);
                zones.Add(BuildChapel(materials, zoneManager, director, chapelArrival));

                ConfigureZone(valley, 0, false, new Color(0.6f, 0.58f, 0.64f), horizon, 30f, 320f, spawn, gateArrival, gateObjective);

                var enemies = new EnemyFactory(materials);
                BiomeStyle[] biomes = Biomes();
                for (int i = 0; i < biomes.Length; i++)
                {
                    int index = ZoneNumbers.FirstDepth + i;
                    bool last = i == biomes.Length - 1;
                    Progress("Deep Dark: " + biomes[i].Name, 0.66f + 0.24f * i / biomes.Length);
                    zones.Add(BuildDepth(biomes[i], index, last, materials, enemies, player, projectiles, impacts, zoneManager, director));
                }

                for (int i = 0; i < layout.Homes.Count; i++)
                {
                    int index = ZoneNumbers.FirstHome + i;
                    var home = layout.Homes[i];
                    Transform returnPoint = Marker("Hamlet Home " + i + " Return", valley.transform, home.arrival, home.yaw + 180f);
                    ActorFactory.CreatePortal("Hamlet Home " + i + " Door", home.door, home.yaw, new Vector3(1.12f, 2.65f, 0.24f),
                        valley.transform, zoneManager, index, false, GameText.EnterHome, "", "");
                    zones.Add(BuildHamletHome(i, materials, zoneManager, returnPoint));
                }

                SceneWiring.Set(zoneManager, "zones", zones.ToArray());
                SceneWiring.Set(zoneManager, "player", player.Controller);
                SceneWiring.Set(zoneManager, "lifecycle", player.Lifecycle);
                SceneWiring.Set(zoneManager, "combat", player.Combat);
                SceneWiring.Set(zoneManager, "atmosphere", Object.FindAnyObjectByType<AtmosphereController>());
                SceneWiring.Set(zoneManager, "sun", RenderSettings.sun);
                SceneWiring.Set(zoneManager, "worldCamera", player.CameraEffects.GetComponent<Camera>());
                SceneWiring.Set(zoneManager, "compass", hud.Compass);
                SceneWiring.Set(zoneManager, "fade", hud.Fade);
                SceneWiring.Set(zoneManager, "titleGroup", hud.ZoneTitle);
                SceneWiring.Set(zoneManager, "titleText", hud.ZoneTitleText);
                SceneWiring.Set(zoneManager, "subtitleText", hud.ZoneSubtitleText);

                Progress("Saving", 0.94f);
                int stale = GeneratedAssets.DeleteStaleMeshes();
                if (!EditorSceneManager.SaveScene(scene, GeneratedAssets.ScenePath))
                {
                    Debug.LogError($"{LogPrefix} Failed to save {GeneratedAssets.ScenePath}");
                    return false;
                }
                RegisterSceneInBuildSettings();
                AssetDatabase.SaveAssets();

                Debug.Log($"{LogPrefix} Prototype generated in {timer.Elapsed.TotalSeconds:F1}s → {GeneratedAssets.ScenePath} " +
                          $"(stale meshes removed: {stale})");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"{LogPrefix} Prototype generation failed: {e}");
                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ---- Zones -----------------------------------------------------------------------------------

        const float ZoneSpacing = 1000f;

        /// <summary>Zones sit far apart along X, so they never see or touch each other even while all are active in the Editor.</summary>
        static Vector3 ZoneOrigin(int index) => new Vector3(index * ZoneSpacing, 0f, 0f);

        static Zone NewZone(string name, Vector3 origin)
        {
            var go = new GameObject(name);
            go.transform.position = origin;
            return go.AddComponent<Zone>();
        }

        static Transform Marker(string name, Transform parent, Vector3 position, float yaw)
        {
            var marker = new GameObject(name).transform;
            marker.SetParent(parent, true);
            marker.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return marker;
        }

        static void ConfigureZone(Zone zone, int index, bool underground, Color ambient, Color fog, float fogStart, float fogEnd,
            Transform entry, Transform exit, Transform objective)
        {
            SceneWiring.Set(zone, "title", StoryText.ZoneTitles[index]);
            SceneWiring.Set(zone, "subtitle", StoryText.ZoneSubtitles[index]);
            SceneWiring.Set(zone, "entry", entry);
            SceneWiring.Set(zone, "exit", exit);
            SceneWiring.Set(zone, "objective", objective);
            SceneWiring.Set(zone, "underground", underground);
            SceneWiring.Set(zone, "ambient", ambient);
            SceneWiring.Set(zone, "fog", fog);
            SceneWiring.Set(zone, "fogStart", fogStart);
            SceneWiring.Set(zone, "fogEnd", fogEnd);
        }

        /// <summary>
        /// The five depths, from Graveborne's BIOMES table. The ambient stays near neutral: each depth's own cold lives in its
        /// baked stone (`DungeonGenerator.Unlit`), so the fires on its walls can stay warm instead of turning to its tint.
        /// </summary>
        static BiomeStyle[] Biomes() => new[]
        {
            new BiomeStyle
            {
                Name = "Catacombs", Decor = DungeonDecor.Catacombs, Wall = Pal.Stone, WallBand = Pal.RockDark, Floor = Pal.Cobble, FloorPatch = Pal.Stone, Ceiling = Pal.RockDark,
                Ambient = new Color(0.84f, 0.8f, 0.9f), Fog = new Color(0.14f, 0.12f, 0.2f), FogStart = 8f, FogEnd = 52f,
                Enemies = new[] { "rat", "bat", "skeleton", "skeleton", "ooze" }, Elite = "gravemaw", Keeper = "morr", Encounters = new[] { "well", "beggar" },
            },
            new BiomeStyle
            {
                Name = "Fungal", Decor = DungeonDecor.Fungal, Wall = Pal.FungalRock, WallBand = Pal.RockDark, Floor = Pal.Dirt, FloorPatch = Pal.StoneMoss, Ceiling = Pal.FungalRock,
                Ambient = new Color(0.74f, 0.86f, 0.8f), Fog = new Color(0.09f, 0.18f, 0.14f), FogStart = 8f, FogEnd = 52f,
                Enemies = new[] { "ooze", "wolf", "ghoul", "bat" }, Elite = "sporetyrant", Keeper = "mycel", Encounters = new[] { "sporewife", "cage" },
            },
            new BiomeStyle
            {
                Name = "Harbor", Decor = DungeonDecor.Harbor, Wall = Pal.WetStone, WallBand = Pal.RockDark, Floor = Pal.Wood, FloorPatch = Pal.WetStone, Ceiling = Pal.RockDark,
                Ambient = new Color(0.74f, 0.82f, 0.86f), Fog = new Color(0.08f, 0.16f, 0.17f), FogStart = 7f, FogEnd = 48f,
                Enemies = new[] { "drownedthrall", "drownedthrall", "shade", "bat" }, Elite = "undertow", Keeper = "brine", Encounters = new[] { "ferryman", "shrine" },
            },
            new BiomeStyle
            {
                Name = "Ember", Decor = DungeonDecor.Ember, Wall = Pal.Basalt, WallBand = Pal.RockDark, Floor = Pal.Basalt, FloorPatch = Pal.DirtDark, Ceiling = Pal.Basalt,
                Ambient = new Color(0.94f, 0.8f, 0.72f), Fog = new Color(0.26f, 0.12f, 0.07f), FogStart = 9f, FogEnd = 56f,
                Enemies = new[] { "cultist", "imp", "rotpriest", "cultist" }, Elite = "cinderprophet", Keeper = "pyraxes", Encounters = new[] { "forgewidow" },
            },
            new BiomeStyle
            {
                Name = "Marrow", Decor = DungeonDecor.Marrow, Wall = Pal.BoneWall, WallBand = Pal.Stone, Floor = Pal.BoneWall, FloorPatch = Pal.Stone, Ceiling = Pal.BoneWall,
                Ambient = new Color(0.8f, 0.78f, 0.88f), Fog = new Color(0.12f, 0.1f, 0.18f), FogStart = 9f, FogEnd = 56f,
                Enemies = new[] { "skeleton", "archer", "bonehound", "shade" }, Elite = "marrowcantor", Keeper = "gloamlord", Encounters = new[] { "bonechoir" },
            },
        };

        /// <summary>The Rope and Lantern: one room between the street and the stair, lit by its own fires.</summary>
        static Zone BuildTavern(GeneratedMaterials materials, ZoneManager zoneManager, StoryDirector director)
        {
            Vector3 origin = ZoneOrigin(ZoneNumbers.Tavern);
            Zone zone = NewZone("Zone Tavern", origin);
            TavernLayout layout = new TavernGenerator(materials.World, origin, ValleyGenerator.Seed + 53).Generate(zone.transform);

            var entry = Marker("Tavern Entry", zone.transform, layout.Entry, 0f);
            var exit = Marker("Tavern Stair Arrival", zone.transform, layout.StairArrival, 180f);
            var objective = Marker("Tavern Objective", zone.transform, layout.Stair, 0f);
            foreach (var person in layout.People)
                ActorFactory.CreateNpc(person.id, person.position, person.yaw, person.figure, zone.transform, director, materials);

            ActorFactory.CreatePortal("Tavern Door Out", layout.DoorOut, 0f, new Vector3(2.4f, 3.2f, 0.5f), zone.transform, zoneManager, ZoneNumbers.City, true,
                GameText.LeaveHouse, "", "");
            ActorFactory.CreatePortal("Stair Down", layout.Stair, 0f, new Vector3(2.8f, 3.2f, 0.5f), zone.transform, zoneManager, ZoneNumbers.FirstDepth, false,
                GameText.StairDown, "", "");

            ConfigureZone(zone, ZoneNumbers.Tavern, false, new Color(0.9f, 0.84f, 0.78f), new Color(0.06f, 0.04f, 0.03f), 13f, 40f, entry, exit, objective);
            SceneWiring.Set(zone, "indoors", true);
            return zone;
        }

        static void CreateArrivalLore(ArrivalLayout layout, Transform parent)
        {
            void Lore(string name, Vector3 position, Vector3 size, string prompt, string text)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.position = position + Vector3.up * (size.y * 0.5f);
                var box = go.AddComponent<BoxCollider>();
                box.size = size;
                box.isTrigger = true;
                var lore = go.AddComponent<LoreInspectable>();
                SceneWiring.Set(lore, "prompt", prompt);
                SceneWiring.Set(lore, "loreText", text);
            }
            Lore("Arrival Tunnel Lore", layout.Tunnel, new Vector3(5.9f, 4.5f, 0.25f), GameText.InspectArrivalTunnel, GameText.ArrivalTunnelLore);
            Lore("Caravan Lore", layout.Caravan, new Vector3(0.45f, 2.2f, 2.6f), GameText.InspectCaravan, GameText.CaravanLore);
        }

        static Zone BuildHamletHome(int household, GeneratedMaterials materials, ZoneManager zoneManager, Transform returnPoint)
        {
            int index = ZoneNumbers.FirstHome + household;
            Vector3 origin = ZoneOrigin(index);
            Zone zone = NewZone("Zone Hamlet Home " + household, origin);
            RoomLayout room = new HamletGenerator(materials.World, origin, ValleyGenerator.Seed + 113 + household, household).Generate(zone.transform);
            Transform entry = Marker("Hamlet Home Entry", zone.transform, room.Entry, 0f);
            Transform objective = Marker("Hamlet Home Door", zone.transform, room.DoorOut, 0f);
            ActorFactory.CreatePortal("Hamlet Home " + household + " Door Out", room.DoorOut, 0f, new Vector3(2.15f, 3f, 0.22f),
                zone.transform, zoneManager, ZoneNumbers.Valley, false, GameText.LeaveHouse, "", "", returnPoint);
            ConfigureZone(zone, index, false, new Color(0.86f, 0.8f, 0.74f), new Color(0.055f, 0.04f, 0.035f), 9f, 24f, entry, entry, objective);
            SceneWiring.Set(zone, "indoors", true);
            return zone;
        }

        /// <summary>The Cold Forge: one room off the street, its goods on the wall.</summary>
        static Zone BuildForge(GeneratedMaterials materials, ZoneManager zoneManager, StoryDirector director, Transform streetArrival)
        {
            Vector3 origin = ZoneOrigin(ZoneNumbers.Forge);
            Zone zone = NewZone("Zone Forge", origin);
            ForgeLayout layout = new ForgeGenerator(materials.World, origin, ValleyGenerator.Seed + 57).Generate(zone.transform);
            FinishHouse(zone, layout, ZoneNumbers.Forge, new Color(0.9f, 0.84f, 0.78f), new Color(0.06f, 0.04f, 0.03f), 12f, 34f, materials, zoneManager, director, streetArrival);
            ActorFactory.CreateShopFixture("Weapon Rack", layout.Rack, layout.RackSize, zone.transform, director, ItemSlot.Weapon, GameText.BrowseRack);
            return zone;
        }

        /// <summary>The Grey Chapel: the nave, the rail, and what the Choir keeps behind it.</summary>
        static Zone BuildChapel(GeneratedMaterials materials, ZoneManager zoneManager, StoryDirector director, Transform streetArrival)
        {
            Vector3 origin = ZoneOrigin(ZoneNumbers.Chapel);
            Zone zone = NewZone("Zone Chapel", origin);
            ChapelLayout layout = new ChapelGenerator(materials.World, origin, ValleyGenerator.Seed + 59).Generate(zone.transform);
            FinishHouse(zone, layout, ZoneNumbers.Chapel, new Color(0.86f, 0.84f, 0.88f), new Color(0.05f, 0.05f, 0.07f), 16f, 46f, materials, zoneManager, director, streetArrival);
            foreach (var relics in layout.Relics)
                ActorFactory.CreateShopFixture("Relics", relics.centre, relics.size, zone.transform, director, ItemSlot.Relic, GameText.BrowseRelics);
            return zone;
        }

        /// <summary>What every house off the street has: its people, its way back out, and the air under its roof.</summary>
        static void FinishHouse(Zone zone, RoomLayout layout, int index, Color ambient, Color fog, float fogStart, float fogEnd, GeneratedMaterials materials,
            ZoneManager zoneManager, StoryDirector director, Transform streetArrival)
        {
            var entry = Marker(zone.name + " Entry", zone.transform, layout.Entry, 0f);
            var objective = Marker(zone.name + " Objective", zone.transform, layout.DoorOut, 0f);
            foreach (var person in layout.People)
                ActorFactory.CreateNpc(person.id, person.position, person.yaw, person.figure, zone.transform, director, materials);
            ActorFactory.CreatePortal(zone.name + " Door Out", layout.DoorOut, 0f, new Vector3(2.4f, 3.2f, 0.5f), zone.transform, zoneManager, ZoneNumbers.City, false,
                GameText.LeaveHouse, "", "", streetArrival);
            ConfigureZone(zone, index, false, ambient, fog, fogStart, fogEnd, entry, entry, objective);
            SceneWiring.Set(zone, "indoors", true);
        }

        static Zone BuildDepth(BiomeStyle biome, int index, bool last, GeneratedMaterials materials, EnemyFactory enemies, PlayerRig player,
            ProjectileLibrary projectiles, ImpactEffects impacts, ZoneManager zoneManager, StoryDirector director)
        {
            int depthSeed = index - ZoneNumbers.FirstDepth + 2; // the depths keep the layouts they had before the houses took zone numbers
            Vector3 origin = ZoneOrigin(index);
            Zone zone = NewZone("Zone " + biome.Name, origin);
            DungeonLayout layout = new DungeonGenerator(biome, materials.World, origin, ValleyGenerator.Seed + depthSeed * 97).Generate(zone.transform, last);
            Transform playerRoot = player.Root.transform;

            int foeNumber = 0;
            string FoeKey() => $"foe_{index}_{foeNumber++}";
            foreach (var spawn in layout.Spawns)
                SceneWiring.Set(enemies.Create(spawn.id, spawn.position + Vector3.up * 0.05f, 0f, zone.transform, playerRoot, projectiles, impacts, spawn.patrol), "saveKey", FoeKey());
            SceneWiring.Set(enemies.Create(biome.Keeper, layout.Keeper + Vector3.up * 0.05f, layout.KeeperYaw, zone.transform, playerRoot, projectiles, impacts, System.Array.Empty<Vector3>()), "saveKey", FoeKey());

            int gold = 20 + depthSeed * 12;
            int chestNumber = 0;
            foreach (Vector3 position in layout.Chests)
            {
                var chest = ActorFactory.CreateChest(position, 180f, materials);
                chest.transform.SetParent(zone.transform, true);
                SceneWiring.Set(chest, "saveKey", $"chest_{index}_{chestNumber++}");
                SceneWiring.Set(chest, "healAmount", 0f);
                SceneWiring.Set(chest, "goldReward", gold);
                SceneWiring.Set(chest, "potionReward", 1);
                SceneWiring.Set(chest, "rewardMessage", string.Format(GameText.ChestGold, gold));
            }
            foreach (var encounter in layout.Encounters)
                ActorFactory.CreateEncounter(encounter.id, encounter.position, zone.transform, director, materials);

            ActorFactory.CreateDungeonMerchant(layout, biome, zone.transform, director, materials);

            var entry = Marker("Entry", zone.transform, layout.Entry + Vector3.up * 0.05f, layout.EntryYaw);
            var exit = Marker("Stair Arrival", zone.transform, layout.StairDownArrival + Vector3.up * 0.05f, 180f);
            var objective = Marker("Objective", zone.transform, layout.StairDown, 0f);

            int above = index == ZoneNumbers.FirstDepth ? ZoneNumbers.Tavern : index - 1;
            ActorFactory.CreatePortal("Stair Up", layout.StairUp + new Vector3(0f, 0f, 0.5f), 0f, new Vector3(3.4f, 3.8f, 1f), zone.transform, zoneManager, above, true,
                GameText.StairUp, "", "");
            if (!last)
                ActorFactory.CreatePortal("Stair Down", layout.StairDown + new Vector3(0f, 0f, -0.5f), 0f, new Vector3(3.4f, 3.8f, 1f), zone.transform, zoneManager, index + 1, false,
                    GameText.StairDown, StoryDirector.DeadFlag(biome.Keeper), GameText.StairBarred);

            ConfigureZone(zone, index, true, biome.Ambient, biome.Fog, biome.FogStart, biome.FogEnd, entry, exit, objective);
            return zone;
        }

        static void Progress(string step, float t)
        {
            Debug.Log($"{LogPrefix} {step}...");
            if (!Application.isBatchMode) EditorUtility.DisplayProgressBar("Ashen Realm — Generate Prototype", step, t);
        }

        // ---- Materials & rendering -----------------------------------------------------------------------

        static GeneratedMaterials CreateMaterials(Texture2DArray detail, Texture2D leaf)
        {
            Shader pixelLit = Shader.Find("AshenRealm/PixelLit");
            Shader composite = Shader.Find("AshenRealm/PixelComposite");
            Shader particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (pixelLit == null || composite == null)
                throw new System.InvalidOperationException("AshenRealm shaders not found (Assets/Art/Shaders). Did they fail to compile?");
            if (particles == null)
                throw new System.InvalidOperationException("URP shaders not found. Is the Universal Render Pipeline package installed and active?");

            Material PixelLit(string name, bool foliage)
            {
                var m = GeneratedAssets.GetOrCreateMaterial(name, pixelLit);
                m.SetTexture("_DetailArray", detail);
                m.SetColor("_Tint", Color.white);
                m.SetFloat("_LightBands", 3f);
                m.SetFloat("_BandSoftness", 0.08f);
                m.SetColor("_ShadowColor", new Color(0.84f, 0.85f, 0.97f));
                m.SetFloat("_AlphaClip", foliage ? 1f : 0f);
                m.SetFloat("_Wind", foliage ? 1f : 0f);
                m.SetFloat("_Cull", foliage ? (float)CullMode.Off : (float)CullMode.Back);
                m.SetFloat("_Cutoff", 0.5f);
                if (foliage)
                {
                    m.EnableKeyword("_ALPHATEST_ON");
                    m.EnableKeyword("_WIND_ON");
                    m.renderQueue = (int)RenderQueue.AlphaTest;
                }
                else
                {
                    m.DisableKeyword("_ALPHATEST_ON");
                    m.DisableKeyword("_WIND_ON");
                    m.renderQueue = (int)RenderQueue.Geometry;
                }
                m.enableInstancing = true;
                return m;
            }

            var materials = new GeneratedMaterials
            {
                World = PixelLit("M_World", false),
                Foliage = PixelLit("M_Foliage", true),
            };

            materials.Composite = GeneratedAssets.GetOrCreateMaterial("M_PixelComposite", composite);
            materials.Composite.SetFloat("_Levels", 18f);
            materials.Composite.SetFloat("_Dither", 0.55f);
            materials.Composite.SetFloat("_Saturation", 1.1f);
            materials.Composite.SetFloat("_Contrast", 1.04f);

            materials.DriftingLeaves = GeneratedAssets.GetOrCreateMaterial("M_DriftingLeaves", particles);
            MakeTransparent(materials.DriftingLeaves, false);
            materials.DriftingLeaves.SetTexture("_BaseMap", leaf);
            materials.DriftingLeaves.SetColor("_BaseColor", Color.white);

            materials.Trail = GeneratedAssets.GetOrCreateMaterial("M_SwordTrail", particles);
            MakeTransparent(materials.Trail, false);
            materials.Trail.SetColor("_BaseColor", new Color(1f, 0.96f, 0.9f, 0.6f));

            materials.Sparks = GeneratedAssets.GetOrCreateMaterial("M_ImpactSparks", particles);
            MakeTransparent(materials.Sparks, true);
            materials.Sparks.SetColor("_BaseColor", Color.white);
            return materials;
        }

        static void MakeTransparent(Material m, bool additive)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetShaderPassEnabled("ShadowCaster", false);
        }

        /// <summary>8×8 pixel-art leaf for the drifting-leaves particles.</summary>
        static Texture2D LeafTexture()
        {
            string[] rows =
            {
                "........",
                "....##..",
                "..####..",
                ".#####+.",
                ".####+..",
                "..##+...",
                "..+.....",
                ".+......",
            };
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                char c = rows[7 - y][x];
                texture.SetPixel(x, y, c == '#' ? Color.white : c == '+' ? new Color(0.7f, 0.6f, 0.45f, 1f) : new Color(1f, 1f, 1f, 0f));
            }
            texture.Apply();
            return texture;
        }

        static Material CreateSkybox(Texture2D panorama)
        {
            Shader shader = Shader.Find("Skybox/Panoramic");
            var sky = GeneratedAssets.GetOrCreateMaterial("M_Skybox_Painted", shader);
            sky.SetTexture("_MainTex", panorama);
            sky.SetFloat("_Mapping", 1f);      // latitude-longitude layout
            sky.SetFloat("_ImageType", 0f);    // 360°
            sky.SetFloat("_Exposure", 1f);
            sky.SetFloat("_Rotation", 0f);
            sky.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            return sky;
        }

        /// <summary>Golden-hour sun from the west-north-west, low enough for long painterly shadows.</summary>
        static readonly Quaternion SunRotation = Quaternion.Euler(13f, 110f, 0f);

        static VolumeProfile CreateVolumeProfile()
        {
            string path = GeneratedAssets.SettingsFolder + "/VP_AshenValley.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            else
            {
                foreach (var component in profile.components.ToArray())
                {
                    AssetDatabase.RemoveObjectFromAsset(component);
                    Object.DestroyImmediate(component, true);
                }
                profile.components.Clear();
            }

            T Add<T>() where T : VolumeComponent
            {
                var component = profile.Add<T>(false);
                component.name = typeof(T).Name;
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
                return component;
            }

            var tonemapping = Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.Neutral);

            var color = Add<ColorAdjustments>();
            color.postExposure.Override(0.15f);
            color.contrast.Override(10f);
            color.saturation.Override(8f);
            color.colorFilter.Override(new Color(1f, 0.97f, 0.92f));

            var split = Add<SplitToning>();
            split.shadows.Override(new Color(0.46f, 0.47f, 0.56f));
            split.highlights.Override(new Color(0.76f, 0.64f, 0.46f));
            split.balance.Override(-10f);

            // No bloom: soft glow fights the crisp pixel look. A light vignette frames the low-res image.
            var vignette = Add<Vignette>();
            vignette.intensity.Override(0.24f);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(new Color(0.06f, 0.04f, 0.06f));

            EditorUtility.SetDirty(profile);
            return profile;
        }

        /// <summary>
        /// The pixel look comes from PixelCamera's fixed low-resolution target. URP applies its render scale even when
        /// rendering into a texture (which would blur that target), so every URP asset used by a quality level gets
        /// render scale 1. Only this field is touched; each change is logged.
        /// </summary>
        static void ConfigureRenderPipelineAssets()
        {
            var seen = new HashSet<UniversalRenderPipelineAsset>();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                if (!(QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset urp) || !seen.Add(urp)) continue;
                if (!Mathf.Approximately(urp.renderScale, 1f))
                {
                    Debug.Log($"{LogPrefix} {urp.name}: renderScale {urp.renderScale} → 1");
                    urp.renderScale = 1f;
                    EditorUtility.SetDirty(urp);
                }
            }
        }

        /// <summary>
        /// Camera that draws only the screen-space UI (including the pixel-world RawImage) to the display.
        /// Starts disabled; PixelCamera enables it at runtime.
        /// </summary>
        static Camera CreateScreenCamera()
        {
            var go = new GameObject("Screen Camera");
            var camera = go.AddComponent<Camera>();
            camera.cullingMask = 0;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.depth = 10f;
            camera.orthographic = true;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.antialiasing = AntialiasingMode.None;
            camera.enabled = false;
            return camera;
        }

        static LightingSettings GetLightingSettings()
        {
            string path = GeneratedAssets.SettingsFolder + "/LS_PrototypeValley.lighting";
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
            if (settings == null)
            {
                settings = new LightingSettings { name = "LS_PrototypeValley" };
                AssetDatabase.CreateAsset(settings, path);
            }
            // Fully realtime: nothing to bake, so the scene works straight after generation.
            settings.bakedGI = false;
            settings.realtimeGI = false;
            EditorUtility.SetDirty(settings);
            return settings;
        }

        static void CreateSceneServices(Material skybox, VolumeProfile profile, Color horizon)
        {
            new GameObject("Game Bootstrap").AddComponent<GameBootstrap>();

            var sunGo = new GameObject("Sun");
            sunGo.transform.rotation = SunRotation;
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.65f, 0.36f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;

            RenderSettings.sun = sun;
            RenderSettings.skybox = skybox;
            Lightmapping.lightingSettings = GetLightingSettings();

            // Cool ambient against the warm sun; fog matches the painted sky's horizon for atmospheric depth.
            var atmosphereGo = new GameObject("Atmosphere");
            var atmosphere = atmosphereGo.AddComponent<AtmosphereController>();
            SceneWiring.Set(atmosphere, "ambientColor", new Color(0.49f, 0.4f, 0.43f));
            SceneWiring.Set(atmosphere, "fogColor", new Color(0.56f, 0.34f, 0.3f));
            SceneWiring.Set(atmosphere, "fogStart", 30f);
            SceneWiring.Set(atmosphere, "fogEnd", 320f);
            atmosphere.Apply();

            var volume = atmosphereGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        static ImpactEffects CreateImpactEffects(Material material)
        {
            var go = new GameObject("Impact Effects");
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 1f;
            main.startLifetime = 0.4f;
            main.startSpeed = 0f;
            main.startSize = 0.05f;
            main.gravityModifier = 1.4f;
            main.maxParticles = 256;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = fade;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.04f;
            renderer.lengthScale = 1.5f;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return go.AddComponent<ImpactEffects>();
        }

        static void SavePrefab(GameObject instance, string name)
        {
            string path = $"{GeneratedAssets.PrefabFolder}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(instance, path, InteractionMode.AutomatedAction);
        }

        static void RegisterSceneInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != GeneratedAssets.ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(GeneratedAssets.ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
