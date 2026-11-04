# Building Ashen Realm for iPhone

Building an iOS app requires **macOS with Xcode**. On Windows, Unity can export the Xcode project (with the iOS Build
Support module installed), but compiling, signing and installing must happen on a Mac.

## 0. Prerequisites
- Unity **6000.6.8f1** with the **iOS Build Support** module (Unity Hub → Installs → ⚙ → Add modules).
  *Status on the development PC (2026-11-04): only Windows Standalone and WebGL modules are installed.*
- A Mac with a current Xcode, and an Apple Account added in Xcode → Settings → Accounts (free or paid team).
- An iPhone with Developer Mode enabled (Settings → Privacy & Security → Developer Mode), connected by cable or Wi-Fi.

Credentials are never stored in this project. Signing is resolved by Xcode's automatic signing.

## 1. Prepare the project (any OS)
1. Open the project in Unity and run **Ashen Realm > Generate Prototype** (if the scene does not exist yet).
2. Run **Ashen Realm > Apply iOS Player Settings**. It sets:
   - landscape left/right only, full screen, hidden status bar, deferred edge gestures
   - IL2CPP, ARM64, Metal only, minimum iOS 15.0, iPhone target
   - bundle id `com.ashenrealm.prototype` (only if the project still had the default) — change it in
     *Project Settings → Player → iOS → Other Settings → Bundle Identifier* to a reverse-DNS id you own
   - automatic signing on, prebaked collision meshes, accelerometer off
3. Check **File > Build Profiles**: `Prototype_Valley` must be the first scene in the scene list (the generator adds it).

## 2. Generate the Xcode project
1. **File > Build Profiles > iOS** → *Switch Platform* (first switch reimports assets; this takes a while).
2. Optional for testing: enable **Development Build** (and *Script Debugging* if needed).
3. Click **Build**, choose an empty folder such as `Builds/iOS` (ignored by git).
   On a Mac you can use **Build And Run** to go straight to Xcode and the device.

## 3. Build and run from Xcode (macOS)
1. Open `Builds/iOS/Unity-iPhone.xcodeproj`.
2. Select the **Unity-iPhone** target → *Signing & Capabilities*:
   - tick **Automatically manage signing**
   - choose your **Team**
   - if Xcode reports the bundle id is taken, change it (and mirror the change in Unity Player Settings)
3. Pick your connected iPhone in the run-destination menu.
4. **Product > Run** (⌘R). On first install with a free team, trust the developer on the phone:
   Settings → General → VPN & Device Management.

## 4. On-device checklist
- Landscape only; HUD inside the safe area (notch / Dynamic Island / home indicator).
- Left thumb joystick + right thumb look + STRIKE at the same time.
- RUN toggles sprint and switches off when stamina is exhausted or you stop moving.
- Frame rate holds 30 FPS (Xcode → Debug Navigator → FPS / GPU). Profile before raising `GameBootstrap.mobileTargetFrameRate` to 60.
- If thermals or frame time are poor: lower `Mobile_RPAsset` render scale, shadow distance (50) or disable bloom in
  `Assets/Settings/VP_AshenValley.asset`.

## Command-line export (optional, with the iOS module installed)
Unity can export the Xcode project headlessly via a custom `BuildPipeline.BuildPlayer` call; this project does not ship one
yet. Use the Build Profiles window as described above.
