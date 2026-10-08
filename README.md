# Droply VR — Unity landscape foundation

Standalone Unity **6000.6.3f1** project for a calm, stylized landscape on Meta Quest 3S. It uses 1 Unity unit per meter, URP 17.6.0, OpenXR, and no downloaded art. Open the repository root in that Unity editor; allow Package Manager to resolve the pinned manifest, then run **Droply → Generate and configure landscape**. The command creates and opens `Assets/Droply/Scenes/Meadow.unity`, configures a Quest-friendly URP asset, adds the scene to Build Settings, assigns the Android OpenXR loader, and validates the scene/composition. The landscape mesh and foliage are generated deterministically when the scene enters Play mode.

The deterministic scene has a custom rolling meadow mesh, a curved 2.36 m walkable sandy path with broad vertex-color feathering and tapered ends, sparse individual broad-leaf grass clumps, six separate flower patches, five rounded faceted foreground stones, exactly one six-tree branching character group, irregularly scattered distant forest layers, softened far hills, a blue procedural sky, and a few low-poly clouds. The path is one continuous mesh with no rectangular end seam. Palette blocks use URP Lit materials, side-warm directional light, and soft shadows. There are no buildings, props, signs, text, logos, interfaces, fog, or post-processing. The initial tracked viewpoint starts at the meadow edge facing the path. Camera height and pose are supplied by headset tracking; no head height is imposed.

## Quest setup and controls

In Unity, select **Android** in Build Profiles, install the Android module if prompted, and confirm **Project Settings → XR Plug-in Management → Android → OpenXR** is enabled. Setup configures OpenXR loader startup, Android ARM64/IL2CPP, API 29+, and landscape orientation; configure signing for your release workflow. The left controller thumbstick glides at 0.85 m/s relative to head yaw, with a dead zone and no acceleration bob. Press the right controller primary button to teleport along a short downward ballistic gaze arc to the landscape mesh. Both controls translate only the rig horizontally; headset-local tracked position and height stay untouched. No vignette, camera bob, forced eye height, or other comfort/post effects are used.

## Performance budget (estimated)

The scene groups foliage, flowers, tree lobes/branches, distant crowns, hills, and clouds by shared mesh/material and submits them with `Graphics.DrawMeshInstanced` (maximum 1,023 matrices per draw); the generated scene opts all instanced materials into GPU instancing. Tree silhouettes switch from branching multi-lobe LOD0 to a trunk plus one or two low-poly canopy lobes at 72 m; far canopy colors step toward blue-green. URP disables additional lights and HDR, uses 2x MSAA, one shadow cascade, 60 m shadows, and baked 1 m scene units. The final Edit-Mode run estimated **146,326 triangles**, **54 visible draw calls** (including static scene renderers, excluding shadow-map passes), **17 distinct materials**, and **47 instanced batches**. Tests enforce ceilings of 180,000 triangles, 80 draw calls, and 20 materials. These are generated-mesh/instance batch estimates, not GPU profiler measurements, and were not measured on Quest.

## Edit-Mode tests and visual review

The final Unity batch-mode Edit-Mode run completed **7/7 tests, 0 failures**. Tests check the tree-group/flower-island/stone/path constraints, path feather colors and tapered ends, prohibited world-object names, repeatable seeded geometry, smooth-glide/teleport height preservation, instancing opt-in, both tree LOD bands, material count, and estimated render budgets. Run from the repository root with Unity 6000.6.3f1:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe" -batchmode -nographics -quit -projectPath (Get-Location).Path -runTests -testPlatform EditMode -testResults "$env:TEMP\droply-tests.xml" -logFile "$env:TEMP\droply-tests.log"
```

**Droply → Capture start-view review images** renders temporary 1280 × 720 color and grayscale previews to `%TEMP%\droply-landscape-review` (or the directory in `DROPLY_REVIEW_OUTPUT`). The review camera is temporarily placed at a typical standing eye height to judge composition only; the saved headset rig itself remains at zero height and uses tracked pose. The final desktop captures were visually checked: the sandy path is visible, the foreground meadow stays open, the middle/far tree silhouettes are varied, and foreground, middle, and blue-green distant layers remain readable in grayscale. These are qualitative desktop-renderer authoring checks, not Quest-display or headset-runtime screenshots.

## Verification limits

Package pins: OpenXR 1.18.0 (declares Unity 6000.0 minimum), XR Management 4.7.0, XR Core Utils 2.6.0, Input System 1.19.0, Test Framework 1.4.6, and the editor-bundled URP 17.6.0. The local Unity editor is available at `C:\Users\stone\AppData\Local\Unity\bin\unity.exe` (Editor 6000.6.3f1). Edit-Mode test results and color/grayscale desktop screenshots are recorded during project development; performance figures remain mesh/batch estimates. Quest hardware, Android player build, and headset runtime have not been tested or claimed.
