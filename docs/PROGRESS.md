# Ashen Realm — Progress

_Last updated: 2026-11-08_

## Environment (confirmed)
- Unity project: `E:\Ashen Realm\AshenRealm`, Editor **6000.6.8f1** (installed via Unity Hub), URP template.
- Packages: URP 17.6.0, Input System 1.20.1 (Active Input Handling = Input System only), uGUI 2.6.0.
  Test Framework 1.8.0 present as a transitive dependency (not used yet). No packages were added.
- Installed build modules: Windows Standalone, WebGL. **iOS Build Support is not installed.** Host OS is Windows,
  so a native iOS build (Xcode) cannot be performed on this machine.
- Git: current source branch `menus-and-houses`; the playable site is a separate build on `gh-pages` (E: has no file ownership,
  so the project path is in the global `safe.directory` list).

## Checkpoint M — places worth exploring (2026-11-08)
- All thirteen zones are redesigned through their generators. The valley gains a pilgrims' spring, a western loop and broken viaduct,
  a planted sword garden, village well/working yards, cottage awnings and gardens, and a caravan rest with packhorses.
- The three cottage interiors have separate household compositions: the weaver's textile loft/bed alcove, a roadside kitchen,
  and the scribe's books and brass celestial globe. Their original doors and safe arrivals remain in place.
- Hollowgate gains framed/shuttered windows, awnings, pennants, gate buttresses, the roofed Well of Names and Lantern Court.
  Amber vertex-lit pools mark inhabited places; the cooler evening light keeps street silhouettes readable.
- The tavern has a candlewheel, runner and travellers' token wall; the forge has armour plans, an unfinished suit and quenching trough;
  the chapel has a processional floor, rose windows and votive shrines. Existing NPC, service and portal positions remain.
- Every depth has a guaranteed landmark chamber, side galleries, loops and varied ceiling heights. The Catacombs' memorial nave,
  Fungal grove, Harbor cargo barge, Ember chained furnace and Marrow ribbed cathedral use separate architecture in the existing static chunks.
  A flood fill checks every floor tile and gameplay placement before generation finishes. Decorative dungeon props remain noncolliding.
- The depths are lit like the houses: every emissive face (flames, lava, spore light, lanterns) becomes a baked light, and the stone takes
  its glow, warm pools by the fires and darkness between, instead of one even tint. `DungeonLights` adds a fire on the wall every three tiles
  of every room (torch sconces, skull shelves with candles, bracket fungus, ship's lanterns, lava seams) and hanging pieces from the vaults
  (cage lamps, bone chandeliers, glowing roots, nets, cauldrons). Walls and floors are cut finer so the light has corners to land on.
  The depth ambients are near neutral; each biome's cold lives in its unlit stone (`DungeonGenerator.Unlit`). No colliders and no
  real lights were added.
- Thirteen new inspectable discoveries connect objects with the people who left them. Authored eye-level `Vistas` provide repeatable
  previews; physics checks and runtime interaction tests cover their approach and targeting. Both arrival directions are walked on all five depths.
- World version is 5; souls saved in older geometry arrive at their zone's safe entry. Zone ids, class kits, torch choice and saved stat growth stay stable.

### Checkpoint M validation
| Check | Result |
|---|---|
| C# compilation | Runtime and Editor preflight and Unity compilation passed |
| Generation and validator | final main project passed, 7506 checks; 5715/5715 dungeon floor cells and 337 gameplay points connected |
| Automated playtest | final pass: 316 checks, 0 failures, including every new discovery and both arrival aisles on all five depths |
| Visual review | 48 authored views across all 13 zones, plus existing world shots; refined the biome floor motifs, Marrow light, courtyard pools and the caravan/forge/chapel review poses |
| Build source audit | 129 C# files, three shaders, the generated scene/meta and 375 world/menu asset files are byte-identical between the verified main project and cached build copy |
| WebGL build and deployment | succeeded, 40.2 MB in all; the data file is 32.2 MB, up from 21.4 MB before the depth lighting pass. Published on gh-pages |
| Depth lighting pass | all five depths baked (222 to 826 lights each); validator 7506 checks, playtest 316 checks, depth previews reviewed |
| Download size | the lit depths took `WebGL.data` from 21.4 to 32.2 MB. The build report showed meshes at 167 MB uncompressed (130 before), and the cause was baked colours: every corner a different float, which barely compresses. `MeshBuilder.ToMesh` now welds identical corners (valley 582k to 223k vertices, meshes 167 to 98 MB) and stores colours in steps of 1/512: `WebGL.data` is 23.3 MB, 31.7 MB in all. Validator 7506 and playtest 316 passed on that build; previews show no banding |

The thirteen discoveries receive reading time based on their text length (up to twelve seconds). The world keeps its two PixelLit
materials, at most three permanent point lights per active zone, and the separate carried torch. A real phone, Safari and Firefox have
not been tested for this checkpoint.

## Checkpoint L — arrival, valley homes, evening and the carried soul (2026-11-06)
- The southern arrival road ends at a black mountain tunnel, blocked by an invisible threshold. Two wagons and three waiting travellers
  mark the caravan that brought the soul here. The tunnel and caravan can be inspected.
- The three valley cottages now have furnished interiors: the weaver's home, a roadside resting place and the letter-writer's home.
  Each door leads back to its own doorstep. Their zones are appended at 10-12, preserving the depths at 5-9; world version is 4.
- Sunset advances along the northbound road; reaching Hollowgate stores evening in `world_evening`. Loading or returning to the valley
  keeps it. The first city arrival draws the carried torch once; inventory controls it thereafter, including underground.
- The pause button opens the descent menu with inventory/abilities, Codex and save. Inventory shows class art, stats, equipment, potions
  and torch; Souls buy six stat increases and three tiers of each of the two class abilities. Levels, growth, tiers, editable food and torch
  choice are saved with the soul. Food has no hunger upkeep yet.
- A continuous five-second hold on the top-right HUD menu button opens the hidden developer sheet: editable soul stats and permanent
  Necromancer/Alchemist unlocks. The ledger gates selection and starting a soul. Both classes now have real weapons, ranged attacks and two abilities.
- All six classes have separate hand models; the supplied warm/cold torch strips animate in the hand. NPCs breathe and turn, the smith
  raises and strikes his hammer, and sparks emit at the blow.
- Scope still deferred: Graveborne's full skill/passive system, story-based class unlock quests, hunger upkeep, Sanctum, followers and limb combat.

### Checkpoint L validation
Validated in the cached copy and the main project. The main project generated and passed with the documented `-refreshImportMode InProcess` option after its parallel import workers crashed during native UDS initialization; no Library deletion or reset was needed.

| Check | Result |
|---|---|
| C# compilation | Runtime and Editor compiled with no C# errors, both standalone and in Unity |
| Generation and validator | main project passed, 6989 checks; cached copy passed, 6980 checks before final UI text polish |
| Automated playtest | passed on both projects, 262 checks, 0 failures |
| Preview/menu renders | reviewed tunnel, caravan, evening city, home interiors, warm/blue torch frames and the inventory/ability/secret sheets at desktop, phone landscape and reference sizes |
| WebGL build | succeeded, 26.2 MB, 14.3 min, 0 build errors |
| Browser startup | built game loads to the main menu in in-app Chromium, 1207x931; interactive browser checks were interrupted before input |

The deployment audit compared 124 C# files, three shaders, all menu images and the generated scene with the build copy: identical. Import metadata differs only in trailing whitespace, with unchanged GUIDs and settings. Live deployment is checked separately after pushing.

Earlier checkpoint notes record their state at the time; this checkpoint supersedes their locked-class, level and zone-count limitations.

## Checkpoint K — the Cold Forge, the Grey Chapel and Gallows Market (2026-11-06)
- Weapons stay at the door of every house: `Zone.Indoors` puts them away through `PlayerCombat.SetArmed`, the hands go empty, nothing
  swings, blocks or casts, the fighting touch buttons leave, and the street gives everything back.
- Two more walkable rooms on the tavern's pattern, sharing `RoomGenerator`: `ForgeGenerator` and `ChapelGenerator`, after their concepts seen
  from the street door. Their doors are on the street that crosses the square, east and west. Zones are now ten: 3 is the forge, 4 the
  chapel, the depths start at 5 (`World/ZoneNumbers`), keep their layouts through `depthSeed`. `SaveStore.WorldVersion` is 3.
- The road in from the gate is Gallows Market: two rows of stalls, the stone corner with Iron Mabb, the notice board with Wick, the Widow
  behind her swept stall, and the gallows at the far end with climbable steps. The keep stands on the hill behind, over the tavern's roof.
- Goods: `Catalog.Items`, Graveborne's first stock, prices by its formulas. Four weapons at the forge for gold, three relics at the chapel for Souls.
  Bought gear is saved with the soul, the weapon replaces the right-hand model, the old piece is traded in. The `ShopPanel` shows
  what a weapon would do for this class, because a sword does nothing for a witch's bolts.
- The chapel also has Graveborne's offering (coin to Souls, Souls back to coin) and its once-a-descent blessing for the Knight and the Gravethief.
  This gives Souls their first use. Halloway and Iron Mabb lost the services they were lent; both have their Graveborne words back.
- Stand-ins to review: the smith's and the book-keeper's names and intros are mine, their lines are Graveborne's, and their portraits are cut
  from the room paintings (one picture for all moods). Number scaling is mine: 1.5 damage per TÁM or MÁG, 2 % speed per GYO, 1.6 health per ÉP, 4 % ability power per MÁG.
  `A Szürke Kápolna` follows Graveborne although the user called it a monastery. Absolution and healing stay at the chapel from the earlier stand-in.
  Not ported: giving items up for Souls, and the forge's separate TÁM, VÉD, MÁG work; the existing tempering stands in for it.

### Checkpoint K validation (2026-11-06)
| Check | Result |
|---|---|
| Compilation | no `error CS` in the batch logs |
| Validator | passed, 6116 checks |
| WebGL build | succeeded, 24.0 MB |
| Playtest | passed, 194 checks, including real aim at every door, the rack, the relics and the people, and the rail stopping the player |
| Browser, 844x390 in the built-in pane | forge and chapel entered through `?zone=`; empty hands, fighting buttons gone, the door message, smith's and book-keeper's portraits, shop row, "not enough gold", shop closing, loading a soul, the rail, the blessing |
| Not verified | the market in a browser (previews and playtest only), a purchase by hand in a browser (a new soul has no gold there; the playtest covers buying), walking the street doors by hand, a real phone, a physical controller, Safari, Firefox |

The first browser pass found what the playtest had missed: the rail's collider stopped the crosshair, so the book-keeper could not be spoken to. The rail now sits on the layer the interactor ignores, and the playtest aims for real.

## Checkpoint J — the Rope and Lantern (2026-11-06)
- The tavern is a walkable room, on the user's choice over a painted scene: zone 2, between Hollowgate and the first depth, so zones
  are numbered in walking order and the depths moved up one (they keep their layouts through `depthSeed`). `SaveStore.WorldVersion` is 2.
- `TavernGenerator` lays it out after the concept seen from the street door: hearth and tables left, leaded window and banner on the
  west wall, the long bar and casks right, the rope and lantern at the far end over the iron-bound stair door.
- The square's north side now has a door with a sign and lanterns; the old stair arch is gone. `E` enters, the back door goes down, the
  street door comes back, and climbing up from the first depth arrives in the tavern.
- Aggie Voss, the Dark Wanderer and Sergeant Halloway moved inside, sitting and standing as in the concept. `PeopleMeshes` dresses all
  six portrait characters after their paintings. Wick and the Waiting Widow are ported from Graveborne to the square, because the
  portraits arrived for them.
- Dialogues show the speaker's painted face, chosen by honor: wary, warm or hostile.
- Stand-ins to review: the full-body concepts served as reference for the 3D figures and are not in the game. Front portraits are used;
  the 3/4 sets of Aggie and the Wanderer are kept in `Vázlat_Kocsma`. The stairwell is scenery behind the door.

### Checkpoint J validation (2026-11-06)
| Check | Result |
|---|---|
| Validator | passed, 5780 checks |
| WebGL build | succeeded, 22.4 MB |
| Playtest | 138 passed, 1 failed: the test itself, which took a switched-off torch for a burning one. Fixed in Checkpoint K. |

## Checkpoint I — class choice carousel (2026-11-06)
- `Új lélek` now opens a row of six cards with the middle card's sheet beside it, after the concepts in `Vázlat_Kártyák`. It turns
  by swipe, arrows, a tap on a neighbour, keyboard and D-pad; B or Esc steps back. `Kiválasztás` shows the class's story on the
  sheet (the backstory, goal and abilities the old dialogue had) with the prologue under the card, and `Indulás` starts the soul.
- Cards are normalised and shrunk by `tools/prepare-menu-art.ps1`: every frame lands on the same rectangle, 640x936, crunched.
  The originals stay in `Vázlat_Kártyák`, outside the repository. The background is the concept's own edges with the middle
  filled in and darkened, because the clean background sent along is the main menu's, a different picture.
- Stand-ins to review: the card numbers are the concepts', which equal Graveborne's table, while the real-time stats stay as
  they were (Knight ÉP 60 is 100 health, Witch FP 25 is 12 focus). The melee classes deal about three damage a second per TÁM,
  the Witch's bolts follow MÁG, block and speed rise with VÉD and GYO. The title is `Hamvas Lovag`, not the concept's English name. Necromancer and Alchemist show the sealed picture `CardLocked` with `Zárolva` on the button, but their sheets stay readable (Graveborne hides them); the
  passives are text only. At 4:3 the painted edges fall outside the crop and the screen is mostly dark.
- Also fixed: on the main menu the longest class name pushed `Szint 1` onto a second line of the soul slot.

### Checkpoint I validation (2026-11-06)
Run on the `_BatchValidation` copy, whose scripts match the project.
| Check | Result |
|---|---|
| Compile | no errors |
| Validator | 5653/5653 |
| Playtest | 127/127, including arrows, a swipe, both ends of the row, a locked card, the story and back, and a simulated gamepad |
| WebGL build | succeeded, 21.2 MB (1 MB more than before) |
| Browser (built-in Chromium pane, 1280x640, 844x390, 800x600) | arrows, a neighbour tap, a stepped mouse swipe, keyboard, Esc, a locked card, story and back, Indulás into the game as the Witch |

Found and fixed by testing: a pressed arrow kept the selection, so the keyboard stopped turning the row; the validator counted
the new safe area. Browser timing note: with the pane hidden the page only runs while a screenshot is taken, so stepped input needs
a screenshot between steps, and a click with no frame after it is lost. That was the tool, not the game.

Not verified: a real phone or tablet, real fingers on the swipe, a physical controller (simulated only), Safari, Firefox, and
how heavy six crunched cards are to decode on an old phone.

## Checkpoint H — main menu, settings, saves (2026-11-05)
- The game opens on the main menu from the sketch `Vázlat_Főképernyő`: painting, title and frames cut from the sketches, the
  Souls ledger, the roster, Új lélek, Kódex, Beállítások. The roster pages when there are several souls; a soul can be released.
- Saves: souls in PlayerPrefs (IndexedDB on the web), autosaved on arrival, after guardians and on focus loss, with Mentés and
  Betöltés in the settings. A load restores class, honor, gold, potions, zone, spot, health, focus and everything cleared.
- Meta record: Souls at Graveborne's rates (spending not ported), descents, deepest depth, a real Codex of 51 entries.
- Settings, styled after `Vázlat_Beállítások`, from the main menu and from pause: language (Magyar only for now), look speed,
  save and load, on-screen buttons on or off with opacity, a drag editor for every control, HUD on or off with opacity.
- Gamepad: Start pauses, B steps back, menus and dialogues take D-pad and South. Dialogues gained a fifth answer for "Vissza".
- Stand-ins to review: `Szint` is 1 + weapon level, because levels are not ported; the sketch's Kódex total of 94 is
  Graveborne's, here it counts what exists. The class choice is still the dialogue; `Vázlat_karakterkártya_knight` is not built yet.

### Checkpoint H validation (2026-11-05)
Run on the `_BatchValidation` copy (Unity was not open, but its Library is the one warmed for WebGL).
| Check | Result |
|---|---|
| Compile | no errors; two old CS0414 warnings on the WebGL target (`GameBootstrap` frame-rate fields used only off the web) |
| Validator | 5433/5433 |
| Playtest | 114/114, including a simulated gamepad (Start, B, selection) and a full save, load, menu and release round |
| WebGL build | succeeded, 20.2 MB |
| Browser (built-in Chromium pane, 800x600 and 844x390, no console errors from the game) | menu, settings, Codex, new soul, pause, layout editor with stepped mouse events, keyboard walking with hidden buttons, release with confirmation; soul and moved buttons survive a page reload, and also a new build at the same address |

Found and fixed by testing: Codex rows opened grey (buttons made inside a hidden panel), a fast drag lost ground in the layout
editor (it added per-frame deltas; it now works from the grab point), and Igen in the release question landed exactly under
the finger that pressed Elengedés, so a double tap would have deleted a soul (Mégse is there now). Slider handles, panel
corners and overlapping choice labels were caught in the menu shots.

Not verified:
- Mouse capture with hidden buttons. The game asks for pointer lock correctly, but the built-in pane refuses it
  (`WrongDocumentError`), so mouse look with hidden buttons still needs a try in real Chrome or Edge.
- A real phone or tablet, real fingers on the layout editor, a physical controller (simulated only), Safari, Firefox.

Worth knowing: a WebGL build reports a `Touchscreen` even on a desktop with no touch hardware (`navigator.maxTouchPoints` 0),
so in `Auto` the on-screen buttons show on desktop browsers until the player picks `Gombok rejtve`.

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
- Invert-Y option (look sensitivity is already available in settings).
- Weapon clipping into walls (overlay camera or shorter rest pose) after device profiling.
