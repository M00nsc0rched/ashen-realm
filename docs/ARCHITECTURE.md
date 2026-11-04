# Ashen Realm — Architecture (vertical slice)

## Assemblies
| Assembly | Path | References |
|---|---|---|
| `AshenRealm.Runtime` | `Assets/Scripts/` | Unity.InputSystem, UnityEngine.UI |
| `AshenRealm.Editor` (Editor only) | `Assets/Editor/` | Runtime, Input System, uGUI, URP Core/Universal Runtime |

## Runtime modules (`Assets/Scripts`)
```
Core/          GameBootstrap (frame rate, orientation), AtmosphereController (ambient + fog at runtime),
               GameMessages (notification channel), LightFlicker, OptionalAttribute
Input/         IPlayerInput (gameplay-facing contract), PlayerInputHub (keyboard/mouse, gamepad, touch merge)
Player/        FirstPersonController, Stamina, CameraEffects, PlayerLifecycle, FirstPersonWeaponAnimator
Combat/        DamageInfo + IDamageable + IDamageFilter, Health, PlayerCombat, HitFlash, ImpactEffects
AI/            CursedKnightAI (FSM), KnightAnimator (procedural rig)
Interactions/  IInteractable, PlayerInteractor, TreasureChest, LoreInspectable
UI/            HUDController, TouchJoystick, TouchLookArea, TouchButton, TouchControlsRoot, SafeAreaFitter, DesktopHint
```

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
  for testing touch layout with the mouse. Touch mode starts automatically on mobile or on the first real touch.

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

## Editor generation (`Assets/Editor`)
| File | Role |
|---|---|
| `PrototypeSceneBuilder` | Menu `Ashen Realm > Generate Prototype`; orchestrates everything, saves scene, build settings |
| `PrototypeValidator` | Menu `Ashen Realm > Validate Prototype`; asset/scene/reference/placement/logic checks |
| `IOSBuildConfigurator` | Menu `Ashen Realm > Apply iOS Player Settings` |
| `PlaytestRunner` | Menu `Ashen Realm > Run Automated Playtest`; Play-mode scenario driven through the input hub |
| `PreviewCapture` | Menu `Ashen Realm > Capture Preview Shots`; offscreen renders of key vantage points |
| `BatchTasks` | `-executeMethod` entry points with exit codes |
| `Generation/MeshBuilder` | Flat-shaded mesh accumulation with auto-oriented faces and a transform stack |
| `Generation/Palette` | 32×32 palette texture: column = colour family, row = shade |
| `Generation/PropMeshes` | Sword, shield, pines, rocks, grass, flowers, fences, chest, camp, banners, mountains, knight parts |
| `Generation/ValleyGenerator` | Heightfield, paths, ruins, graveyard, camp, scattering, chunking, colliders, bounds |
| `Generation/ActorFactory` | Player, Cursed Knight and chest hierarchies |
| `Generation/HudBuilder` | HUD canvas, touch controls, generated UI sprites |

### Rendering approach
- One URP Lit material (`M_World`) samples the palette with point filtering; per-face colour comes from UVs. All static
  geometry shares it → SRP batcher friendly. Decoration is merged into a 4×4 grid of chunk meshes
  (`Decor_x_z` casts shadows, `Foliage_x_z` does not). Colliders are separate primitives / convex rock meshes.
- Flat shading (unshared vertices) gives the low-poly painterly look; the URP assets render at 0.65 scale with point
  upscaling for the slightly pixelated presentation (UI stays native resolution).
- Fully realtime lighting: one soft-shadowed directional sun, flat ambient + linear fog applied at runtime, procedural
  dusk skybox, global volume (neutral tonemapping, colour adjustments, split toning, vignette, quarter-res bloom).

### World layout (deterministic, seed 1337)
Spawn (0,−86) in a fenced lane → dirt road north → ruined chapel (0,32) with the chest at the altar and the Cursed Knight
patrolling its entrance. Branches: camp with campfire (26,−42), sword graveyard (−32,−6) with an inspectable blade.
Playable area ≈ 206×206 m enclosed by rim hills and four invisible boundary walls; distant mountain ring at 215–330 m.

## Extending
- New enemy: reuse `Health`, `HitFlash`, implement an AI + animator pair, add a factory method, rerun the generator.
- New interactable: implement `IInteractable` on a collider's GameObject (or a parent).
- Replace art: drop models under the existing pivots / roots; gameplay components do not depend on mesh content.
