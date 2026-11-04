# ASHEN REALM

An original first-person dark fantasy action RPG with a pixel-art painterly look, playable in the web browser
(desktop and mobile) and prepared for iPhone. This repository contains the first playable vertical slice: a golden
valley with a hamlet and windmill, a sword graveyard, a ruined chapel, a Cursed Knight to fight and a chest to loot.
All art (meshes, pixel textures, sky, UI sprites) is generated procedurally in the Editor: no paid or third-party assets.

- Engine: Unity **6000.6.8f1** · URP 17.6 (custom `AshenRealm/PixelLit` shader) · Input System 1.20 · uGUI
- Targets: **WebGL** (see [docs/BUILD_WEBGL.md](docs/BUILD_WEBGL.md)), iPhone (see [docs/BUILD_IOS.md](docs/BUILD_IOS.md))

## Open and generate
1. Open the `AshenRealm` folder with Unity Hub (Editor 6000.6.8f1).
2. Wait for the import and script compilation to finish (Console must show no errors).
3. Menu **Ashen Realm > Generate Prototype**. This creates/updates:
   - `Assets/Scenes/Prototype_Valley.unity` (also added as the first scene in Build Settings)
   - `Assets/Art/Generated/` (meshes, palette texture, UI sprites), `Assets/Materials/`, `Assets/Prefabs/`
   - `Assets/Settings/VP_AshenValley.asset` (post-processing) and `LS_PrototypeValley.lighting`
   - URP assets: render scale 0.65 with point upscaling (pixelated look)

   It is safe to run again at any time; it rebuilds the scene and updates assets in place.
   Generated files are not stored in git, so run this once after cloning.
4. Optional checks:
   - **Ashen Realm > Validate Prototype** — static checks (references, placement, combat logic)
   - **Ashen Realm > Run Automated Playtest** — plays the slice in Play mode (move, fight, loot, die, respawn)
   - **Ashen Realm > Capture Preview Shots** — renders reference images to `Screenshots/`
5. For the browser: **Ashen Realm > Build WebGL**, then run `tools/serve-webgl.ps1` and open http://localhost:8080.
6. For iPhone: **Ashen Realm > Apply iOS Player Settings** (see [docs/BUILD_IOS.md](docs/BUILD_IOS.md)).
7. Press **Play**. (In edit mode the Game view shows the full-resolution camera; the pixel look appears in Play mode.)

> If you change anything under `Assets/Editor/`, regenerate. Do not hand-edit the generated scene.

## Controls
| Action | Keyboard / mouse (Editor) | Touch (iPhone) | Gamepad |
|---|---|---|---|
| Move | WASD / arrows | left-side floating joystick | left stick |
| Look | mouse (click Game view to capture, Esc to release) | drag on the right side | right stick |
| Sprint (stamina) | hold Left Shift | **RUN** toggles | left stick press |
| Jump | Space | **JUMP** | South button |
| Attack | left mouse (hold to keep swinging) | **STRIKE** | right trigger |
| Block | hold right mouse | hold **BLOCK** | left trigger |
| Interact | E | **USE** | West button |
| Pause menu (resume, fullscreen, look speed) | Esc | ‖ button top-right | – |
| Toggle touch controls (testing) | F1 | – | – |

Touch controls appear automatically on devices and in the Device Simulator; press **F1** in the Game view to try the
touch layout with the mouse.

## What to try
- Follow the dirt road north from the fenced spawn lane toward the ruined chapel.
- The **Cursed Knight** patrols the chapel entrance. It telegraphs an overhead chop: block it (stamina!) or step back,
  then strike. 4 sword hits defeat it.
- Inside the chapel, aim at the **chest** at the altar and press E / USE ("Open Chest") for gold and a Healing Draught.
- Visit the camp (east) and the sword graveyard (west); inspect the blade at the graveyard's centre.
- If you fall in battle you rise again at the spawn after a few seconds.

## Batch mode
```
"C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Unity.exe" -batchmode -quit -projectPath "<path>\AshenRealm" ^
  -executeMethod AshenRealm.EditorTools.BatchTasks.GenerateAndValidate -logFile Logs/batch.log
```
Exit code 0 = generated and validated. The project must not be open in another Editor instance.

## Documentation
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — systems, data flow, generation pipeline
- [docs/PROGRESS.md](docs/PROGRESS.md) — checkpoints, what is verified, what is not
- [docs/BUILD_IOS.md](docs/BUILD_IOS.md) — iPhone build and deployment workflow
- [CLAUDE.md](CLAUDE.md) — rules for AI-assisted development sessions
