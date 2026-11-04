# ASHEN REALM — project rules for Claude Code

Original first-person dark fantasy RPG for iPhone (landscape). Unity **6000.6.8f1**, URP 17.6, Input System 1.20, uGUI 2.6.
Current milestone: first playable vertical slice (`Assets/Scenes/Prototype_Valley.unity`). Do not expand scope
(quests, inventory, progression, multiplayer, weather) unless asked.

## Golden rules
- The scene and its art are **generated**. Change generators in `Assets/Editor/`, then run
  `Ashen Realm > Generate Prototype`. Never hand-edit `Prototype_Valley.unity` or `Assets/Art/Generated/**`.
- Generation must stay deterministic (seed `ValleyGenerator.Seed`) and repeatable: update assets in place (keep GUIDs),
  rebuild the scene from an empty scene, delete only stale files inside generated folders.
- Gameplay reads input only through `IPlayerInput` (`PlayerInputHub`). Touch UI pushes into the hub; gameplay never
  references UI scripts or devices directly.
- Damage flows through `IDamageable.ApplyDamage(DamageInfo)`; mitigation via `IDamageFilter` on the same GameObject as `Health`.
- Animation is procedural and purely visual (`FirstPersonWeaponAnimator`, `KnightAnimator`); timing lives in gameplay code.
- Every serialized reference must be assigned unless the field has `[Optional]`. `Ashen Realm > Validate Prototype` enforces it.
- Mobile budget: 30 FPS baseline, one shared palette material (`M_World`) for the world, chunked static meshes,
  no realtime GI, one shadowed directional light, ≤1 small point light in view.
- Use only first-party Unity packages. Use the modern Input System (no `UnityEngine.Input`). UI text uses legacy
  `UnityEngine.UI.Text` with the built-in `LegacyRuntime.ttf` (no TMP essentials import needed).
- Never claim compilation, validation or an iOS build succeeded without running it and reading the log.

## Layout
- `Assets/Scripts/{Core,Input,Player,Combat,AI,Interactions,UI}` → assembly `AshenRealm.Runtime`
- `Assets/Editor/` → assembly `AshenRealm.Editor` (scene builder, validator, iOS configurator, batch tasks)
- `Assets/Editor/Generation/` → procedural mesh toolkit (`MeshBuilder`, `Palette`, `PropMeshes`, `ValleyGenerator`, `HudBuilder`, `ActorFactory`)
- Docs: `docs/ARCHITECTURE.md`, `docs/PROGRESS.md` (update after each checkpoint), `docs/BUILD_IOS.md`

## Validation (batch mode — the project must not be open in another Editor)
```
"C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Unity.exe" -batchmode -quit -projectPath . ^
  -executeMethod AshenRealm.EditorTools.BatchTasks.GenerateAndValidate -logFile Logs/batch.log
```
Exit codes: 0 ok, 1 generation failed, 2 validation failed. Search the log for `error CS`, `[AshenRealm]` and `FAIL:`.
- Runtime playtest: `-executeMethod AshenRealm.EditorTools.BatchTasks.Playtest` **without `-quit`** (exit 0 ok, 3 failed;
  results tagged `[AshenRealm][Playtest]`). Extend `PlaytestRunner` when adding gameplay.
- Visual check: `BatchTasks.CapturePreviews` writes PNGs to `Screenshots/`; read them to review art changes.
- If the project is open in the Editor, validate a copy (`robocopy` to `../_BatchValidation`, excluding Temp/Logs;
  delete `Library/EditorInstance.json`, `*.pid`, `*-lock` in the copy; re-sync `Assets/` before each run).

## Conventions
- Namespaces `AshenRealm.<Folder>`; one public type per file; private `[SerializeField]` fields, wired by the builder via `SceneWiring.Set`.
- Generated prop meshes: Y up, front faces −Z (knight parts are mirrored to face +Z at build time).
- Keep comments short and explain *why*. Match the existing style.
