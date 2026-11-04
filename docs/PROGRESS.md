# Ashen Realm — Progress

_Last updated: 2026-11-04_

## Environment (confirmed)
- Unity project: `E:\Ashen Realm\AshenRealm`, Editor **6000.6.8f1** (installed via Unity Hub), URP template.
- Packages: URP 17.6.0, Input System 1.20.1 (Active Input Handling = Input System only), uGUI 2.6.0.
  Test Framework 1.8.0 present as a transitive dependency (not used yet). No packages were added.
- Installed build modules: Windows Standalone, WebGL. **iOS Build Support is not installed.** Host OS is Windows,
  so a native iOS build (Xcode) cannot be performed on this machine.
- Git: no repository initialised yet. `.gitignore` is ready.

## Checkpoint A — foundation
- [x] Project inspected; assembly definitions `AshenRealm.Runtime` / `AshenRealm.Editor`
- [x] `CLAUDE.md`, `README.md`, `docs/ARCHITECTURE.md`, `docs/BUILD_IOS.md`, `.gitignore`
- [x] CharacterController first-person controller (walk, sprint w/ stamina, jump, gravity, ground probe, pitch/yaw clamp)
- [x] Procedural valley (terrain, paths, rim hills, mountains, forest, rocks, grass, flowers, fences, ruins, camp, graveyard)

## Checkpoint B — controls and weapons
- [x] `PlayerInputHub` input abstraction (keyboard/mouse, gamepad, touch)
- [x] Multitouch: floating joystick, look surface behind buttons, STRIKE/BLOCK/JUMP/USE/RUN buttons, safe area
- [x] Procedural sword + shield on a pivot hierarchy; idle, bob, look sway, two slash variants, block pose, impact kicks, trail

## Checkpoint C — combat, AI, interaction, HUD
- [x] Sphere-cast melee (25 dmg, 2.5 m), cooldown, stamina cost, shield block as damage filter
- [x] Cursed Knight FSM (Idle/Patrol/Chase/Attack/Dead), telegraphed attacks, flinch, recoil when blocked, death fall
- [x] Treasure chest ("Open Chest" prompt, lid animation, heal + reward notification), lore blade
- [x] HUD: health (with damage chip), stamina, crosshair, prompt, notifications, damage vignette, death overlay, enemy bar
- [x] `Ashen Realm > Generate Prototype` / `Validate Prototype` editor automation

## Checkpoint D — iOS preparation
- [x] `Ashen Realm > Apply iOS Player Settings` (landscape, IL2CPP, ARM64, Metal, bundle id, automatic signing)
- [x] Deployment workflow documented in `docs/BUILD_IOS.md`
- [ ] Install iOS Build Support, export Xcode project, build on a Mac, test on an iPhone

## Validation log — 2026-11-04
The project was open in the Editor (project lock), so validation ran in a copy at `E:\Ashen Realm\_BatchValidation`
(robocopy of the project minus Temp/Logs, scripts re-synced before each run) with Unity 6000.6.8f1 in batch mode.

| Run | Command (`-executeMethod AshenRealm.EditorTools.BatchTasks.…`) | Result |
|---|---|---|
| 1 | `GenerateValidateAndConfigureIOS` | Compiled `AshenRealm.Runtime` + `AshenRealm.Editor` with **0 errors, 0 warnings**; scene generated (27.9 s); iOS settings applied; **689/689 validator checks passed**. Found bug: template bundle id not recognised → fixed. |
| 2 | `CapturePreviews` | 8 preview renders reviewed; shield too large/dark, sword too long → weapon framing and stone shades adjusted. |
| 3 | `GenerateValidateAndConfigureIOS` | Regeneration repeatable (assets updated in place, 0 stale meshes); bundle id → `com.ashenrealm.prototype`; **689/689 passed**. |
| 4 | `Playtest` (Play mode, no `-quit`) | **25/25 runtime checks passed**: grounded spawn, walk 6.0 m/1.5 s, sprint 10.5 m/1.5 s with stamina drain, sprint toggle auto-off, jump 1.09 m and landing, knight engages, shield block (100 → 97 HP), 4 sword hits kill the knight, "Open Chest" prompt, chest opens with reward notification, death → respawn at spawn with full health, no runtime errors/exceptions. |
| 5 | `CapturePreviews` | Weapon framing confirmed. |

What the validator covers: generated assets exist; scene is first in Build Settings; exactly one of each core
component; one touch button per action; every serialized reference assigned (unless `[Optional]`); every
MeshFilter/Renderer uses persistent assets; tuning (100 HP, 100 stamina, 25 dmg, 2.5 m); player/knight/chest rest on
ground; spawn capsule free; knight far from spawn; chest aimable; 4 boundary walls; sword and knight reach geometry;
Health/Stamina/block logic tests.

## Not yet verified (needs a person or hardware)
- [ ] Generation inside the **open** Editor on `E:\Ashen Realm\AshenRealm` (run *Ashen Realm > Generate Prototype*,
      then *Apply iOS Player Settings*; output is deterministic and matches the validated copy).
- [ ] Hands-on feel in the Editor: mouse look, cursor capture, weapon sway/slash readability, pixelated presentation
      (render scale only shows in the Game view, not in preview renders).
- [ ] Real multitouch on a device or Device Simulator (the playtest drives the input hub API, not finger events).
- [ ] iOS: install iOS Build Support, export the Xcode project, build on a Mac, profile 30 FPS on an iPhone.

## Next steps (proposed, not started)
- Audio (procedural swoosh/impact/block sounds), hit-stop tuning after hands-on play.
- Settings UI for look sensitivity / invert Y (API exists on `PlayerInputHub`).
- Weapon clipping into walls (overlay camera or shorter rest pose) after device profiling.
