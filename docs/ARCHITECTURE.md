# Ashen Realm — Architecture

The Graveborne integration and what it left out: [GRAVEBORNE.md](GRAVEBORNE.md).

## Assemblies
| Assembly | Path | References |
|---|---|---|
| `AshenRealm.Runtime` | `Assets/Scripts/` | Unity.InputSystem, UnityEngine.UI |
| `AshenRealm.Editor` (Editor only) | `Assets/Editor/` | Runtime, Input System, uGUI, URP Core/Universal Runtime |

## Runtime modules (`Assets/Scripts`)
```
Core/          GameBootstrap (frame rate, orientation), AtmosphereController (ambient + fog at runtime),
               GameMessages (notification channel), GameText (all player-facing Hungarian strings),
               PixelCamera (low-res world render), LightFlicker, Rotator, OptionalAttribute
Input/         IPlayerInput (gameplay-facing contract), PlayerInputHub (keyboard/mouse, gamepad, touch merge)
Player/        FirstPersonController, Stamina, CameraEffects, PlayerLifecycle, FirstPersonWeaponAnimator
Combat/        DamageInfo + IDamageable + IDamageFilter, Health, PlayerCombat, HitFlash, ImpactEffects
AI/            CursedKnightAI (FSM), KnightAnimator (procedural rig)
Interactions/  IInteractable, PlayerInteractor, TreasureChest, LoreInspectable
UI/            HUDController, CompassStrip, PauseMenu, DialoguePanel, TouchJoystick, TouchLookArea, TouchButton,
               TouchControlsRoot, SafeAreaFitter, DesktopHint
Story/         StoryTypes (plain records), Catalog (classes, abilities, bestiary), StoryText (people, encounters, whispers),
               GameSession (class, honor, gold, potions, flags), StoryDirector (everything that talks and pays out)
World/         Zone, ZoneManager (one active zone, fade, atmosphere, respawn), ZonePortal
```
Later additions: `AI/` gained `EnemyAI`, `ICreatureAnimator`, `CreatureAnimator`; `Combat/` gained `StatusEffects`,
`PlayerAbilities`, `Projectile`, `ProjectileLibrary`, `CombatFocus`; `Player/PlayerClassRig` applies the chosen class.
`Interactions/` gained `NpcActor` and `EncounterSite`. The menus added `Core/GameSettings` and `Core/MenuText`; `UI/MainMenu`,
`SettingsPanel`, `CodexPanel`, `ConfirmPanel`, `ChoiceButton`, `MenuFocus`, `TouchLayout`, `TouchLayoutHandle`, `HudAppearance`;
`Story/SoulKeeper`, `SaveStore`, `SaveData`, `CodexBook`, `ISavedState`.
Checkpoint L adds `Player/PlayerProgression`, `PlayerTorch`; `AI/NpcIdleAnimator`; `UI/InventoryPanel`, `SecretStatsPanel`,
`HoldMenuButton`, `ClassPortrait`; and `Core/InventoryText`.

### Zones
Thirteen zones sit 1000 m apart along X (`World/ZoneNumbers`) and all stay active in the Editor, so the validator can raycast them.
Ids 0-9 retain their order (valley, city, tavern, forge, chapel, five depths); the three valley homes are appended at 10-12.
`ZoneManager.Awake` leaves only the first one active, and `Travel` swaps them behind a fade. Sun, fog and ambient follow
the zone; underground the sun is off and the equipped torch supplies local light. Respawning returns to the current
zone's entry, and guardians heal when the player dies. An indoor zone (the tavern, forge, chapel or valley homes) has no sun: its fires and lanterns
are baked into the vertex colours by `MeshBuilder.Bake`, with one or two real lights left on top so the fire still moves. Entering one
calls `PlayerCombat.SetArmed(false)`: the weapon pivots go down and out of sight, attacks, blocks and abilities stop, and the fighting
touch buttons fade. The independently mounted carried torch keeps the inventory choice. A door that needs its own spot on the far side
(the public houses and the valley homes) carries it as `ZonePortal.arrival`.
`PlayerInteractor` ignores Unity's Ignore Raycast layer, so a collider there stops the player but not the crosshair: the chapel's rail.

`ArrivalGenerator` places the two wagons, waiting travellers and black tunnel at the southern road. The tunnel's invisible collider
blocks the old route; `HamletGenerator` gives each roadside cottage a furnished room and its own return doorstep.
`AtmosphereController` advances sunset with northward travel. City arrival stores `world_evening`, so a loaded soul or a return to the
valley stays in evening light; `ZoneManager.Place` reapplies the atmosphere even when restoring into the current zone.

### Goods
`Catalog.Items` holds what is for sale, in Graveborne's points. `StoryDirector` is the `IShopCounter`: it takes the money (gold for steel,
Souls for relics, through `SoulKeeper`), equips the item in `GameSession` and trades in what it replaces. `UI/ShopPanel` only shows, and
reaches the same counter from the seller's dialogue or from a `ShopFixture` on the goods themselves. `PlayerClassRig` adds the points to
the class's numbers and swaps the right-hand model; `PlayerProgression` composes saved growth on top. Weapon and relic ids are saved in `SoulRecord`.

### Story flow
`MainMenu` opens the game; its `Új lélek` opens `ClassCarousel`, a row of cards with the middle one's sheet beside it (time scale 0,
gameplay input paused). The row is one float, and each card's place, size and light follow from its distance to it, so arrows, a
swipe and a half-finished drag share one code path. `Kiválasztás` turns the sheet to the class's story, `Indulás` hands the class to
`StoryDirector.StartSoul`. All six classes have starting kits; availability comes from `SoulKeeper.IsClassUnlocked`, with the last two
stored in `MetaRecord.UnlockedClasses`. `EnemyAI` does not
know about UI: a guardian raises `ConfrontationRequested`, the director shows its speech and answers with `Engage()`.
`EnemyAI.Slain` pays gold and sets `dead_<id>`, which unbars the stair.

### Menus and saving
The game opens on `MainMenu`, a canvas over the paused world in the same scene, so nothing loads between it and playing;
the world camera sleeps while it is up. `MenuBuilder` places everything in the design sketches' coordinates, and
`tools/prepare-menu-art.ps1` cuts the title, frames and painting out of the sketches into `Assets/Art/Menu`.

`SaveStore` keeps the roster and the meta record as JSON in PlayerPrefs (IndexedDB on the web, where `PlayerPrefs.Save`
is what persists). `SoulKeeper` writes the soul in play on every arrival, after each guardian and boss, when the app
loses focus, and on demand. A load always starts from a fresh scene: the main menu is one, and from inside the game the
scene reloads first. So the world only ever loses what a soul cleared: each `ISavedState` (chest, foe, encounter, knight)
has a unique key that doubles as a session flag, and loading calls `ApplyCleared` on those whose flag is set, in inactive
zones too. Positions are trusted only while `SaveStore.WorldVersion` matches; otherwise the soul arrives at its zone's entry.
World version 4 keeps the depth ids stable and adds the three valley homes.

The meta record outlives every soul: Souls (paid per rank and per Codex discovery, at Graveborne's rates), descents, the
deepest depth, class unlocks and the Codex. `CodexBook` builds the Codex from `Catalog` and `StoryText`, so its total is always true.

`ProgressRecord` belongs to one soul: level, stat bonuses, two ability tiers, food and torch choice. `PlayerProgression` spends Souls,
applies the resulting combat/health/focus values and captures the record. Loading restores the session, then growth, then zone and
current health/focus. `InventoryPanel` presents equipment and abilities; `HoldMenuButton` opens `SecretStatsPanel` after five continuous
unscaled seconds. The hidden editor changes the same saved values and unlock ledger, rather than a separate display-only copy.
Food is stored and editable; hunger upkeep is not implemented.

Settings live in `GameSettings` and apply at once through its `Changed` event: `HudAppearance` fades the status elements
(prompts and messages stay on purpose), `TouchControlsRoot` fades the buttons, and keeps the pause button faintly visible
on touch devices when the buttons are hidden, since it is the only way back. `TouchLayout` shows a `TouchLayoutHandle` ring
over each control; moving the joystick moves its whole touch zone. Every "go back" request ends up in `PauseMenu.Toggle`,
which finds what to close. `MenuFocus` gives keyboard and gamepad a selection without leaving pointers a lit button.

### Input flow
```
Keyboard/Mouse/Gamepad ──(InputAction, code-defined)──┐
                                                       ├─► PlayerInputHub ──IPlayerInput──► FirstPersonController
TouchJoystick / TouchLookArea / TouchButton ──Touch*()─┘                                    PlayerCombat
                                                                                            PlayerInteractor
```
- Presses are **buffered latches** (`ConsumeJump/Attack/Interact`) so a tap is never lost between UI event
  dispatch and gameplay `Update`, and early presses (e.g. jump just before landing) still count.
- Look input is accumulated in degrees and consumed once per frame. Touch drag is normalised by screen height
  (`touchDegreesPerScreenHeight`), so sensitivity is device-independent. `LookSensitivity` persists in PlayerPrefs.
- Multitouch: each touch control tracks one `pointerId`. The look surface lies *behind* the buttons, so a finger that
  starts on a button can never rotate the camera. Joystick, look and buttons work simultaneously.
- Desktop: click to capture the cursor (the capturing click does not attack), Esc releases, F1 toggles touch controls
  for testing touch layout with the mouse. Touch mode starts automatically on mobile, when the browser reports a
  touchscreen, or on the first real touch, unless the player chose `Gombok rejtve`: a stored choice (settings or F1)
  outranks the device. Gamepad Start pauses, B steps back through open menus.
- Mouse buttons are separate actions from the gamepad triggers and only fight while the cursor is captured in desktop
  mode. In touch mode the mouse (and browsers' tap-emulated clicks) only drives the on-screen controls, so dragging to
  look never swings the sword: the STRIKE (`TÁMADÁS`) button is the only way to attack. The playtest checks this.

### Text and language
All player-facing strings are Hungarian. HUD strings live in `Core/GameText`, menu strings in `Core/MenuText` and `Core/InventoryText` (builders and
runtime scripts reference them; the web page template is translated separately). Menus draw with Spectral (three faces,
`Assets/Art/Fonts`, SIL OFL), and the validator checks both menu string tables, the Codex and menu `Text` against the font's own glyphs. The web build's embedded `LegacyRuntime` font covers only U+0020–U+007E and
U+00A0–U+017F (á é í ó ö ő ú ü ű are in; em dash, ellipsis and the minus sign are not), so the validator checks every
`GameText` string, scene `Text` and interactable string against that range. Interaction prompts are written in upper
case in `GameText` instead of being converted at runtime.

### Combat
- `PlayerCombat.StartAttack` spends stamina and plays a slash; the hit resolves mid-strike (between
  `FirstPersonWeaponAnimator.WindupEnd` and `StrikeEnd`) with a `SphereCastNonAlloc` from the camera
  (range 2.5, radius 0.4). The nearest collider wins: an `IDamageable` takes 25 damage, solid geometry stops the blade.
- Blocking: `PlayerCombat` is an `IDamageFilter`. Frontal hits (≤75°) are reduced by 85%, paid with stamina
  (1.2 per absorbed point). Running out of stamina breaks the guard.
- `Health` raises `Damaged`, `Died`, `Changed`; `HitFlash`, `CameraEffects`, the HUD and AI subscribe.

### Cursed Knight FSM
`Idle → Patrol → Chase → Attack → Dead` on a `CharacterController` (no NavMesh). Detection = radius + FOV + line of sight.
Attacks: wind-up (telegraphed overhead raise), overlap-sphere hit at the chop, recovery; committed attacks have
hyper-armour. A blocked blow makes the knight recoil. Losing the player resets its health and returns it to patrol.
Obstacle handling is a stuck-detector side-step, enough for the open valley.

### Procedural animation
Weapons sit under `Main Camera/Weapon Rig/{Sword Pivot (Right Hand), Shield Pivot (Left Hand)}`. Swap the child meshes
for animated models later without touching gameplay. The knight rig is `Model/Hips/{Leg L, Leg R, Torso/{Head, Arm L, Arm R, Cape}}`.
All six classes have separate right/left hand models. `PlayerTorch` animates the supplied eight-frame warm/cold atlases with the
`AshenRealm/TorchStrip` shader; drawing it hides the class's left-hand item. `NpcIdleAnimator` moves the figure without moving its
interaction collider; the smith also has a hammer arm and timed particle sparks.

## Editor generation (`Assets/Editor`)
| File | Role |
|---|---|
| `PrototypeSceneBuilder` | Menu `Ashen Realm > Generate Prototype`; orchestrates everything, saves scene, build settings |
| `PrototypeValidator` | Menu `Ashen Realm > Validate Prototype`; asset/scene/reference/placement/logic checks |
| `IOSBuildConfigurator` | Menu `Ashen Realm > Apply iOS Player Settings` |
| `PlaytestRunner` | Menu `Ashen Realm > Run Automated Playtest`; Play-mode scenario driven through the input hub |
| `PreviewCapture` | Menu `Ashen Realm > Capture Preview Shots`; offscreen renders of key vantage points |
| `MenuCapture` | Menu `Ashen Realm > Capture Menu Shots`; renders the menu screens at desktop and phone aspect |
| `WebGLBuilder` | Menu `Ashen Realm > Build WebGL` → `Builds/WebGL` with the `AshenRealm` web template |
| `BatchTasks` | `-executeMethod` entry points with exit codes |
| `Generation/MeshBuilder` | Mesh accumulation for PixelLit: vertex colour, texture-layer UVs, wind weights, auto-oriented faces |
| `Generation/Palette` | Colour families (sRGB → linear vertex colours) and each family's default texture layer / UV mode |
| `Generation/PixelTextures` | 20 procedural 64×64 pixel-art layers (Texture2DArray) and the painted panoramic sky |
| `Generation/PropMeshes` | Sword, shield, pines (bough cards), autumn trees, grass/flower/wheat cards, houses, windmill, lanterns, barrels, crates, chest, camp, knight parts |
| `Generation/ValleyGenerator` | Smooth blended terrain, paths, ruins, graveyard, camp, hamlet, windmill, puddles, scattering, chunking, colliders, bounds |
| `Generation/ArrivalGenerator`, `HamletGenerator` | Southern tunnel and caravan; three furnished cottages with distinct household props |
| `Generation/ActorFactory` | Player (with pixel camera and drifting leaves), Cursed Knight and chest hierarchies |
| `Generation/HudBuilder` | HUD canvas (top bars, compass, emblem, pause), touch controls, layout rings, pause menu, pixel screen, generated UI sprites |
| `Generation/MenuBuilder` | Menu canvas after the sketches: main menu, Codex, settings, confirm box, layout bar, and the shared gold widgets |
| `Generation/InventoryMenuBuilder`, `TorchBuilder`, `PlayerWeaponMeshes` | Inventory/developer controls, supplied torch atlases and all six class kits |

### Rendering approach (pixel-art painterly look)
- **Low-resolution world, crisp UI.** `PixelCamera` renders the world camera into a 360 px-high render texture with point
  filtering; a full-screen RawImage (`Pixel Screen` canvas) shows it through `AshenRealm/PixelComposite`, which
  posterises colour with 4×4 Bayer dithering. A separate `Screen Camera` (culling mask: nothing) draws only the UI.
  URP render scale is kept at 1 because URP would otherwise down-scale (and blur) the render texture too.
- **`AshenRealm/PixelLit` shader** (`Assets/Art/Shaders`): samples a `Texture2DArray` of pixel-art layers (two layers
  blended per vertex, e.g. grass→dirt on terrain), multiplies by vertex colour, banded three-step lighting with a cool
  shadow tint, stepped metal sheen, emission (lanterns, windows, fire, knight eyes), alpha clip and wind sway for
  foliage, fog, shadows, Forward and Forward+ additional lights. Two materials: `M_World` (opaque) and `M_Foliage`
  (cutout, two-sided, wind).
- **UVs** come from the surface mode: stone blocks get one tile per face (bevelled block look), wood/terrain/metal are
  projected along the face's dominant axis in metres, cards use explicit UVs.
- Static decoration is merged into 4×4 chunk grids: `Decor_x_z` (opaque), `Trees_x_z` (bough/leaf cards, shadows on),
  `Foliage_x_z` (grass, flowers, wheat; shadows off).
- Lighting: low golden sun from the WNW, flat cool ambient, linear fog in the sky's horizon colour, painted panoramic
  sky (`Skybox/Panoramic`), global volume (neutral tonemapping, colour adjustments, split toning, vignette; no bloom).

### World layout (deterministic, seed 1337)
Spawn (0,−86) in a fenced lane → dirt road north (puddles) → ruined chapel (0,32) with the chest at the altar and the
Cursed Knight patrolling its entrance. Branches: camp with campfire (26,−42), giant-sword graveyard in a flower meadow
(−32,−6) with an inspectable blade, hamlet of three half-timbered houses with lantern posts (34,−14), windmill on a
rise (54,8) beside wheat fields. Playable area ≈ 206×206 m enclosed by rim hills and four invisible boundary walls;
distant mountain ring at 215–330 m.

### Web platform
`GameBootstrap` leaves frame pacing to the browser on WebGL. Losing pointer lock (Esc in the browser) raises
`PlayerInputHub.PauseRequested`, which opens `PauseMenu` (time scale 0, cursor released, settings, fullscreen toggle,
main menu). The page template caps device pixel ratio and shows a rotate-device notice on phones in portrait.

## Extending
- New enemy: reuse `Health`, `HitFlash`, implement an AI + animator pair, add a factory method, rerun the generator.
- New interactable: implement `IInteractable` on a collider's GameObject (or a parent).
- Replace art: drop models under the existing pivots / roots; gameplay components do not depend on mesh content.
