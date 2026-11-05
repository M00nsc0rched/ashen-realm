# Building Ashen Realm for the web (WebGL)

The game runs in desktop and mobile browsers as a Unity WebGL build (WebGL 2). Requirements: Unity **6000.6.8f1**
with the **Web Build Support** module (installed on the development PC).

## Build
1. Open the project, run **Ashen Realm > Generate Prototype** (once, or after generator changes).
2. **Ashen Realm > Build WebGL**. It applies the web player settings and builds `Prototype_Valley` into `Builds/WebGL`:
   - page template `Assets/WebGLTemplates/AshenRealm` (full-window canvas, themed loading screen, "turn your device"
     notice on phones held upright, fullscreen button on desktop)
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

To review a deep zone without playing down to it, add `?zone=N` (1 Hollowgate, 2-6 the depths): after the class choice the
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

## Controls in the browser
- Desktop: click the game to capture the mouse (pointer lock). Esc releases it and opens the pause menu
  (Resume, Fullscreen, look speed). WASD, Shift, Space, left/right mouse, E.
- When the touch buttons are showing (touchscreen laptops, F1), the mouse only turns the view; attack with **TÁMADÁS**.
- Phones/tablets: touch controls appear automatically. Hold the device in landscape. Use the browser's
  "Add to Home Screen" for a fullscreen-like experience on iOS (Safari does not allow page fullscreen on iPhone).

## Performance notes
- The world renders at a fixed 360 px height (`PixelCamera.targetHeight`) and is upscaled with hard pixels, so cost
  barely depends on screen size. The HUD renders at native resolution, capped at 1.5× DPR on phones by the template.
- WebGL uses the **Mobile** quality level (`Mobile_RPAsset`): 1 shadow cascade, 50 m shadow distance.
- If a low-end phone struggles: lower `targetHeight` to 270, reduce grass clumps in `ValleyGenerator.ScatterFoliage`,
  or disable shadows on the `Trees_*` chunk renderers.
