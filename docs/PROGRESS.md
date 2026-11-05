# Ashen Realm — Progress

_Last updated: 2026-11-05_

## Environment (confirmed)
- Unity project: `E:\Ashen Realm\AshenRealm`, Editor **6000.6.8f1** (installed via Unity Hub), URP template.
- Packages: URP 17.6.0, Input System 1.20.1 (Active Input Handling = Input System only), uGUI 2.6.0.
  Test Framework 1.8.0 present as a transitive dependency (not used yet). No packages were added.
- Installed build modules: Windows Standalone, WebGL. **iOS Build Support is not installed.** Host OS is Windows,
  so a native iOS build (Xcode) cannot be performed on this machine.
- Git: repository on `master` (E: has no file ownership, so the path is in the global `safe.directory` list).

## Checkpoint G — Graveborne integration, first pass (2026-11-05)
- World: seven zones instead of one: the valley with a new gate north of the chapel, Hollowgate, and five generated
  depths of the Deep Dark (rooms and corridors, 135 × 105 m each). Only the current zone is active.
- Classes: Ashen Knight, Gravethief, Hollow Witch, Oathwarden, each with stats, weapons, honor, focus and two abilities.
- Foes: `EnemyAI` runs 23 bestiary entries from data (melee, bolts, healing, fury, ward, poison, stun, weaken), on humanoid,
  beast, flyer and blob rigs. The Cursed Knight keeps its own AI.
- Story: class choice with backstory, Hollowgate's people with lines by class and alignment, 8 honor encounters with
  warped variants, guardian speeches, gold and potions, forge and chapel services, whispers, class endings.
- HUD: focus bar, honor, gold and potions, ability buttons with cooldown sweep, zone title cards, enemy bar with name,
  moved to the top. Compass and pause objective follow the zone.
- Details and the list of what is not ported: [GRAVEBORNE.md](GRAVEBORNE.md).

### Checkpoint G validation (2026-11-05)
Run on the `_BatchValidation` copy because the project was open in the Editor.
| Check | Result |
|---|---|
| Compile | no errors or warnings in the generation log |
| Generate | about 90 s |
| Validator | 4820/4820 |
| Automated playtest | 66/66, including class choice, gate, merchant, stair, a sword kill, Shield Bash, Morr, the Gloamlord and the ending |
| WebGL build | success, 19.1 MB (16.6 min with the IL2CPP cache) |
| Browser (in-app Chromium, touch mode) | class choice, Gravethief kit, ability and potion buttons; `?zone=2` reaches the Catacombs with the knight's weapons; no console errors |

Bugs found by looking rather than by the tests: the depths were too dark (ambient, shades, torch), the torch burned out the
weapons, the enemy bar overlapped the touch buttons, and every class but the knight held the wrong weapons (index by class
instead of by class and hand; the playtest now checks this).

### Not yet verified (Checkpoint G)
- Nobody has played from the valley to the Gloamlord by hand. The playtest teleports between the key points and kills the
  guardians directly, so balance and the length of the descent are untested.
- Enemy steering in corridors in real play: it is the Cursed Knight's side-step, built for open ground.
- Hollow Witch and Oathwarden abilities in the browser (the Witch's bolt is covered by the playtest only).
- Depths 2-5 and Hollowgate in the browser (previews only), phones, Firefox, Safari, desktop pointer lock.
- Frame rate on mobile with the larger scene.

## Checkpoint F — attack input fix, Hungarian text, public repository (2026-11-05)
User feedback: turning the view started attacks; attacking must only happen with the attack button. Game text in Hungarian.
- [x] Cause: in touch mode (on when the browser reports a touchscreen) the left mouse button still attacked, so every
      mouse drag on the look surface swung the sword. Mouse buttons are now separate actions that only fight while the
      cursor is captured in desktop mode; gamepad triggers are unaffected; in touch mode only TÁMADÁS attacks.
- [x] All player-facing text is Hungarian and lives in `Core/GameText`; the web page template is translated too.
- [x] Measured the font the WebGL build embeds (`LegacyRuntime`, scanned from `WebGL.data`): it covers U+0020–U+007E
      and U+00A0–U+017F (ő/ű included) but not U+2212, U+2014, U+2026. The validator now checks every string against it.
- [x] Public repository https://github.com/M00nsc0rched/ashen-realm (commits use the GitHub noreply address); the web
      build is served by GitHub Pages from the `gh-pages` branch: https://m00nsc0rched.github.io/ashen-realm/ (verified:
      loads in the in-app Chromium with Hungarian HUD and no console errors).

### Checkpoint F validation (2026-11-05)
| Check | Result |
|---|---|
| Compile + generate + validate (batch, real project) | 0 errors, 0 warnings; **910/910** (adds 60 text/font checks) |
| Play-mode playtest | **27/27**; new: simulated mouse presses reach Play mode (3 seen) and cause 0 swing frames in touch mode |
| WebGL build | Succeeded, 16.6 MB (full IL2CPP rebuild 51 min) |
| Browser test (in-app Chromium, touch mode) | Hungarian labels render with accents (TÁMADÁS, VÉDÉS, ÉNY, TELJES KÉPERNYŐ); mouse drags and clicks turn the view without swinging or spending stamina; TÁMADÁS swings; the pause menu's objective line was still English, fixed in `HudBuilder` and rebuilt |

## Checkpoint E — web target and reference art direction (2026-11-04)
Goal from the user: play in a web page, look like the supplied reference screenshots (pixel-art painterly 3D).
- [x] `AshenRealm/PixelLit` URP shader (texture-array pixel textures, banded light, cool shadows, sheen, emission,
      alpha clip, wind; Forward and Forward+) and `AshenRealm/PixelComposite` (posterise + Bayer dither)
- [x] `PixelCamera`: world at 360 px height, point-upscaled; UI at native resolution via a UI-only screen camera
- [x] 20 procedural pixel-art texture layers, painted panoramic sky, smooth blended terrain (grass/dirt/rock)
- [x] New art: drooping-fringe pines, golden autumn trees, grass/flower/wheat cards, hamlet (3 half-timbered houses,
      lantern posts, barrels, crates, signpost), windmill with turning sails, puddles, giant graveyard swords in a
      flower meadow, drifting golden leaves
- [x] HUD restyled after the references: compass strip, long top-centre health/stamina bars with metal caps, round
      weapon emblem, pause button, stone-ring touch controls; pause menu (resume, fullscreen, look speed)
- [x] Browser behaviour: Esc / lost pointer lock opens the pause menu; browser-paced frame rate on WebGL
- [x] WebGL tooling: `AshenRealm` page template, `Ashen Realm > Build WebGL`, `tools/serve-webgl.ps1`, `docs/BUILD_WEBGL.md`
- URP render scale reset to 1 on both quality levels (it would blur the low-res target); logged by the generator.
- Generated outputs (`Assets/Art/Generated`, the scene, prefabs, materials, volume/lighting assets; ≈80 MB) are now
  git-ignored. Run **Ashen Realm > Generate Prototype** after cloning.

### Checkpoint E validation (2026-11-04)
| Check | Result |
|---|---|
| Compile (batch, copy and real project) | 0 errors, 0 warnings in project code; both custom shaders compile and report supported |
| Validator | 850/850 (adds shader/texture-array/material, render-scale, PixelCamera, pause, compass, rotator checks) |
| Play-mode playtest | 25/25 on the copy and on the real project; no runtime errors with the pixel camera active |
| Preview renders | 10 shots reviewed against the references; fixes: shield back texture, giant graveyard swords, denser meadow, softer blue shadows, capture warm-up |
| WebGL build | Succeeded, 16.6 MB (gzip + fallback); first build 43.6 min, rebuild 0.7 min |
| Browser test (`tools/serve-webgl.ps1`, in-app Chromium) | Loads, renders the pixel look, compass turns, touch buttons respond, pause/resume works, no console errors; missing "−" glyph found and fixed |

### Not yet verified (Checkpoint E)
- Real phones and tablets in a mobile browser (performance, touch, safe areas). Desktop browsers other than the
  in-app Chromium (Firefox, Safari).
- Desktop pointer lock + Esc-to-pause in a regular browser window (the in-app pane reports a touchscreen, so it ran
  in touch mode).
- Publishing to a public host: done in Checkpoint F (GitHub Pages).

### Incident
The Unity Editor that was open on this project crashed twice (18:47, 19:29) while auto-importing shaders: its
`UnityShaderCompiler.exe` timed out on start-up ("Failed to get ipc connection ... even after 10 retries"). Both times
coincided with heavy batch-mode Unity runs on a copy of the project, so resource contention is the likely cause; the
same shaders compile cleanly in batch mode. Avoid running batch builds while the Editor is importing.

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
