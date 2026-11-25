# Building Ashen Realm for the web (WebGL)

The game runs in desktop and mobile browsers as a Unity WebGL build (WebGL 2). Requirements: Unity **6000.6.8f1**
with the **Web Build Support** module (installed on the development PC).

## Build
1. Open the project, run **Ashen Realm > Generate Prototype** (once, or after generator changes).
2. **Ashen Realm > Build WebGL**. It applies the web player settings and builds `Prototype_Valley` into `Builds/WebGL`:
   - page template `Assets/WebGLTemplates/AshenRealm` (full-window canvas, themed loading screen, "turn your device"
     notice on phones held upright, fullscreen button on desktop, home-screen manifest and icons)
   - Gzip compression **with decompression fallback**, so the build works on any static host without special headers
   - product name "Ashen Realm", data caching on
   The first build switches assets to the Web platform and takes several minutes (IL2CPP + WebAssembly).

Command line (project must not be open in another Editor):
```
Unity.exe -batchmode -quit -buildTarget WebGL -projectPath <path>\AshenRealm ^
  -executeMethod AshenRealm.EditorTools.BatchTasks.GenerateValidateAndBuildWebGL -logFile Logs/webgl.log
```
Exit codes: 0 built, 1 generation failed, 2 validation failed, 4 build failed.

## Test locally
Browsers will not run Unity builds from `file://`. Serve the folder over HTTP:
```
powershell -ExecutionPolicy Bypass -File tools/serve-webgl.ps1
```
then open http://localhost:8080. (Any static server works, e.g. `python -m http.server` inside `Builds/WebGL`.)

To review a zone without walking to it, add `?zone=N` (1 Hollowgate, 2 the tavern, 3 the forge, 4 the chapel, 5-9 the depths,
10-12 the valley homes): after the class choice the
game starts there. Guardians above it stay alive, so its stair up still works but nothing is unlocked by skipping.

## Publish
Upload the **contents** of `Builds/WebGL` (index.html, Build/, TemplateData/) to any static host:
your own web space, GitHub Pages, Netlify, itch.io (upload a zip of the folder, choose "HTML" and tick
"This file will be played in the browser"). For best load times configure the host to send
`Content-Encoding: gzip` for `*.gz` files; without it the decompression fallback still works.

### GitHub Pages (this repository)
The live build at https://m00nsc0rched.github.io/ashen-realm/ is served from the `gh-pages` branch, which holds only
the contents of `Builds/WebGL` plus an empty `.nojekyll`. To update it after a new build, check out `gh-pages` in a
separate folder, replace its files with the new `Builds/WebGL` contents (keep `.nojekyll`), commit and push. Pages
serves the `.unityweb` files without `Content-Encoding`, so the loader's decompression fallback unpacks them.
Pushing the source branch (`menus-and-houses` during Checkpoint L) does not update the playable site; publish a newly validated build separately.

## Controls in the browser
- Desktop: click the game to capture the mouse (pointer lock). Esc releases it and opens the pause menu
  with inventory and abilities, Codex, save, settings, fullscreen and main-menu controls. WASD, Shift, Space, left/right mouse, E.
- When the touch buttons are showing (touchscreen laptops, F1), the mouse only turns the view; attack with **TÁMADÁS**.
- Phones/tablets: touch controls appear automatically. Hold the device in landscape. On iPhone, play full screen from
  the home screen (below): Safari does not allow page fullscreen there.
- The inventory draws or puts away the torch. A continuous five-second press on the top-right HUD menu button opens the
  developer stat editor and permanent class unlocks; a short press opens the normal descent menu.

## Saves in the browser
Souls and settings live in PlayerPrefs, which a WebGL build keeps in the site's IndexedDB. They belong to one browser on
one device and one address, and clearing site data erases them. `PlayerPrefs.Save` is what writes them out, so nothing that
must survive may skip it (`SaveStore` and `GameSettings.Flush` do it).
The soul includes level, stat upgrades, ability tiers, food, torch choice and the `world_evening` flag. Unlocked classes belong
to the meta record. World version 4 preserves zone ids and moves older saved positions to the matching zone's safe entry.

## iPhone home-screen app
iPhone plays the published build as a web app; there is no separate iPhone build. On the phone:
1. Open https://m00nsc0rched.github.io/ashen-realm/ in Safari. Its loading screen says how: *Megosztás, majd Főképernyőhöz adás*.
2. Tap Share (Megosztás), then Add to Home Screen (Főképernyőhöz adás), keep the name "Ashen Realm" and add it.
3. Start the game from the castle icon. It runs full screen without Safari's bars; hold the phone in landscape.

The template carries what this needs: `manifest.webmanifest` (name, full screen, landscape, dark colours, 192 and 512 px
icons) and the iOS tags in `index.html` (180 px `apple-touch-icon`, `black-translucent` status bar, `viewport-fit=cover`).
Unity copies them into the build unchanged, so publishing a build publishes them. The icons are a crop of the castle and its
lit gate from `Assets/Art/Menu/MenuBackground.jpg`. In the app the fullscreen button and the install hint are hidden.
- **Separate saves.** The home-screen app has its own site data, apart from Safari's: a soul started in Safari is not in
  the app and the other way round, and removing the icon deletes the app's souls. Safari may erase a site's data after a week
  of browsing without a visit; WebKit counts a home-screen app's own use instead, so the app is the safer place for a long soul.
- **Upright.** iOS does not lock a home-screen app's orientation (it ignores the manifest's `landscape`), so a phone held
  upright shows the same "Fordítsd el a készüléket" notice as Safari.
- **Not supported:** offline play (each launch loads from the site; Unity's data cache makes later launches faster, and a
  new publish reaches the app on its next launch); launch splash images (iOS shows a plain screen until the loading screen);
  the pause menu's *Teljes képernyő* does nothing on iPhone, the app is already full screen.
- **Notch and rounded corners.** `viewport-fit=cover` lets the world fill the whole screen, under the notch or Dynamic Island
  too, while the HUD, the touch controls and the menus stay clear of it. Unity's web runtime reports the whole canvas as
  `Screen.safeArea`, so the page supplies the insets: the empty `#safe-area-probe` in `index.html` is padded by
  `env(safe-area-inset-*)` (`TemplateData/style.css`), and `Assets/Plugins/WebGL/SafeArea.jslib` reads its computed padding and
  scales it to the canvas's backing pixels (`canvas.width` over its CSS width, since the page caps the pixel ratio). In a web build
  `SafeAreaFitter` uses those insets instead of `Screen.safeArea`, re-reading them on a resize and four times a second (Safari can
  change them on rotation without a resize). Elsewhere it still uses `Screen.safeArea`. The touch buttons, the layout editor's
  saved places and the pause button are kept relative to that fitted area, so they move with it. Desktop browsers report zero
  insets; to try it there, set the probe's padding from the console, for example
  `document.getElementById("safe-area-probe").style.padding = "0 47px 21px 47px"`.

## Performance notes
- The world renders at a fixed 360 px height (`PixelCamera.targetHeight`) and is upscaled with hard pixels, so cost
  barely depends on screen size. The HUD renders at native resolution, capped at 1.5× DPR on phones by the template.
- WebGL uses the **Mobile** quality level (`Mobile_RPAsset`): 1 shadow cascade, 50 m shadow distance.
- If a low-end phone struggles: lower `targetHeight` to 270, reduce grass clumps in `ValleyGenerator.ScatterFoliage`,
  or disable shadows on the `Trees_*` chunk renderers.
