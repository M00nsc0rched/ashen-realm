# ASHEN REALM

**Játék a böngészőben (play in the browser): https://m00nsc0rched.github.io/ashen-realm/**

An original first-person dark fantasy action RPG with a pixel-art painterly look, playable in the web browser
(desktop and mobile) and prepared for iPhone. Four classes start unlocked; the Necromancer and Alchemist become playable
when unlocked. Cross the sunset valley past the Cursed Knight,
reach Hollowgate (Üregkapu), the last city above the Deep Dark, and go down five depths to the Gloamlord's throne. The
world, story and bestiary come from the author's game Graveborne ([docs/GRAVEBORNE.md](docs/GRAVEBORNE.md)).
World art (meshes, pixel textures, sky, HUD sprites) is generated procedurally in the Editor. The menu art is cut from the
author's design sketches. The one third-party asset is the Spectral font (SIL Open Font License, `Assets/Art/Fonts`).

- Engine: Unity **6000.6.8f1** · URP 17.6 (custom `AshenRealm/PixelLit` shader) · Input System 1.20 · uGUI
- Targets: **WebGL** (see [docs/BUILD_WEBGL.md](docs/BUILD_WEBGL.md)), iPhone (see [docs/BUILD_IOS.md](docs/BUILD_IOS.md))

## Open and generate
1. Open the `AshenRealm` folder with Unity Hub (Editor 6000.6.8f1).
2. Wait for the import and script compilation to finish (Console must show no errors).
3. Menu **Ashen Realm > Generate Prototype**. This creates/updates:
   - `Assets/Scenes/Prototype_Valley.unity` (also added as the first scene in Build Settings)
   - `Assets/Art/Generated/` (meshes, palette texture, UI sprites), `Assets/Materials/`, `Assets/Prefabs/`
   - `Assets/Settings/VP_AshenValley.asset` (post-processing) and `LS_PrototypeValley.lighting`
   - URP assets: render scale 1 (the pixel look comes from `PixelCamera`, which renders the world at 360 px)

   It is safe to run again at any time; it rebuilds the scene and updates assets in place.
   Generated files are not stored in git, so run this once after cloning.
4. Optional checks:
   - **Ashen Realm > Validate Prototype** — static checks (references, placement, combat logic)
   - **Ashen Realm > Run Automated Playtest** — plays the slice in Play mode (move, fight, loot, die, respawn)
   - **Ashen Realm > Capture Preview Shots** — renders reference images to `Screenshots/`
   - **Ashen Realm > Capture Menu Shots** — renders the menu screens to `Screenshots/`
5. For the browser: **Ashen Realm > Build WebGL**, then run `tools/serve-webgl.ps1` and open http://localhost:8080.
6. For iPhone: **Ashen Realm > Apply iOS Player Settings** (see [docs/BUILD_IOS.md](docs/BUILD_IOS.md)).
7. Press **Play**. (In edit mode the Game view shows the full-resolution camera; the pixel look appears in Play mode.)

> If you change anything under `Assets/Editor/`, regenerate. Do not hand-edit the generated scene.

## Controls
| Action | Keyboard / mouse (desktop) | Touch (phone, or touch mode) | Gamepad |
|---|---|---|---|
| Move | WASD / arrows | left-side floating joystick | left stick |
| Look | mouse (click Game view to capture, Esc to release) | drag on the right side | right stick |
| Sprint (stamina) | hold Left Shift | **FUTÁS** toggles | left stick press |
| Jump | Space | **UGRÁS** | South button |
| Attack | left mouse while the cursor is captured (hold to keep swinging) | **TÁMADÁS** (the only way in touch mode) | right trigger |
| Block | hold right mouse while the cursor is captured | hold **VÉDÉS** | left trigger |
| Equipped class abilities (cost focus) | Q, R | the two ability buttons (named after the ability) | right / left shoulder |
| Healing potion | F | **FŐZET** | North button |
| Interact, talk | E | **HASZNÁL** | West button |
| Answer in a dialogue, use a menu | click, or 1-5 in a dialogue | tap | D-pad and South button |
| Descent menu (inventory, save, settings) | Esc | ‖ button top-right | Start |
| Back in a menu | Esc | Vissza | East button |
| Toggle touch controls (testing) | F1 | – | – |

Touch controls appear automatically on devices and in the Device Simulator; press **F1** in the Game view to try the
touch layout with the mouse. In touch mode the mouse never attacks: drag to look, press **TÁMADÁS** to strike.

The game text is Hungarian. HUD strings live in `Assets/Scripts/Core/GameText.cs`, menu strings in `Assets/Scripts/Core/MenuText.cs`
and `InventoryText.cs`, and city service prompts in `CityText.cs`.

**Beállítások** (from the main menu or the pause menu): language (Hungarian for now), look speed, save and load, on-screen
buttons shown or hidden with their opacity and a layout editor that drags each one anywhere, HUD shown or hidden with its
opacity. Souls save on every arrival in a zone, after each guardian and when the page loses focus; in the browser they live in
that browser's site data.

The pause button opens **Lemerülés menüje**. **Készlet és képességek** shows the class portrait, current stats, equipment,
potions and the carried torch. Its **KÉPESSÉGEK** tab spends Souls on six stat upgrades and three tiers of each class ability.
Each class has four abilities (24 across the six classes), with its own icon for each one. Prepare any two in the Q/R slots;
changing slots is free and keeps each skill's running cooldown. Tiers, the chosen pair, growth and the torch choice are saved
with the soul; class unlocks belong to the permanent ledger. Equipment icons are rendered from the same mesh recipes as
the held weapons, including anything bought in town.

## What to try
- The game opens on the main menu: **Új lélek** starts a soul, the roster continues one, **Kódex** lists what you have met.
- Choose a class. Each has its own health, starting honor, card-matched equipment and four abilities; two are prepared at once.
  Try the knight's crescent polearm and sun shield, the witch's antler staff, the warden's twin blades, or the necromancer's
  chained sickles. The alchemist keeps the green flask visible even when carrying a torch.
- Look south at the dark, closed mountain tunnel and the waiting caravan that brought you here.
- Visit the three furnished cottages beside the valley road; each door returns to its own doorstep.
- Explore the hamlet's working yards, the pilgrims' spring, the broken viaduct and the sword garden. Look for objects with
  **MEGVIZSGÁLÁS**: the places and the people who left them have short stories to discover.
- Follow the dirt road north from the fenced spawn lane toward the ruined chapel.
- The **Cursed Knight** (Átkozott Lovag) patrols the chapel entrance. Its overhead chop, broad sweep and short rush have
  different wind-ups: block with stamina or step aside, then strike during recovery. Defeating it opens the gate behind the chapel.
- Inside the chapel, aim at the **chest** at the altar and press E / HASZNÁL ("LÁDA KINYITÁSA") for gold and a healing draught.
- In **Üregkapu**, talk to the Dark Wanderer and the townsfolk. Temper your weapon, buy potions, buy back your honor.
- Leave the market road for the Well of Names and Lantern Court; look up at the rooflines, hanging signs and window light.
  The tavern, forge, chapel and three homes each have their own furnishings and a discoverable memento.
- Use the well for health and focus, buy a two-potion travel package, light a memorial candle for honor, or rest over tea
  under the lantern tree. These paid services renew when you begin another descent. The watch board offers three saved
  contracts for 3, 5 and 7 dungeon foes; return to the board for each gold and Souls reward.
- Sunset fades along the road. By the city it is evening; returning to the valley keeps that light. The first city arrival
  takes out your torch, which the inventory can put away or draw again. The townsfolk breathe and turn; the smith works his hammer.
- The tavern door is the stair down. Every depth has packs, a named terror, chests and encounters whose text changes
  with your honor. A Legendary Guardian bars each stair; the Gloamlord waits at the bottom.
- Dungeon merchants count coins in furnished workshops and wear different clothes in each depth. Story encounters have
  dressed, working figures: a bucket-lifting woman, beggar, sporewife, ferryman, forgewidow, bone choir and an injured caged wolf.
  Dishonor changes their appearance as well as their words.
- Watch each foe's wind-up and floor warning. Wolves leap, ghouls rush, slimes burrow, archers fire volleys and casters mark
  the ground. Named terrors and guardians have different tactics and health phases; the Gloamlord adds attacks over three phases.
- The five depths have different architecture and routes: a memorial nave in the Catacombs, a luminous fungal grove,
  the drowned docks, the chained furnace, and the ribbed cathedral of Marrow. Side galleries and loops give the journey
  landmarks to find beyond its guardian and stair.
- If you fall in battle you rise again at the zone's entry after a few seconds.

## Batch mode
```
"C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Unity.exe" -batchmode -quit -projectPath "<path>\AshenRealm" ^
  -executeMethod AshenRealm.EditorTools.BatchTasks.GenerateAndValidate -logFile Logs/batch.log
```
Exit code 0 = generated and validated. The project must not be open in another Editor instance.

Developer control: hold the top-right HUD menu button continuously for five seconds to open the hidden stat editor.
It edits the current soul and can permanently unlock the Necromancer and Alchemist. A short click still opens the normal menu.

## Documentation
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — systems, data flow, generation pipeline
- [docs/PROGRESS.md](docs/PROGRESS.md) — checkpoints, what is verified, what is not
- [docs/BUILD_IOS.md](docs/BUILD_IOS.md) — iPhone build and deployment workflow
- [CLAUDE.md](CLAUDE.md) — rules for AI-assisted development sessions
