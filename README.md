# Droply VR — Unity landscape for Meta Quest 3S

Standalone Unity **6000.6.3f1** project (URP 17.6.0, OpenXR) for a calm, composed low-poly landscape on Meta Quest 3S. 1 Unity unit = 1 metre, no downloaded art, nothing is loaded at runtime: the whole scene is generated deterministically from code, **with the light already baked into the vertex colours** (no lights, no shadow maps, no skybox on the headset). The fundamental rules are in `CLAUDE.md`, the concept image in `docs/konzept-landschaft.png`; what was repaired and improved, and why, is in `docs/landschaft-reparatur.md` (German).

## What you see

Standing on an open, gently rolling, sunlit meadow, looking along a warm sand path that starts at your feet as a narrow tongue and bends right around a mound. In the foreground and middle ground: a few stones and pebbles along the path edge, grass tufts of spiky blades (deliberate groups, tufts at the path edge, small flecks in irregular patches), two flower islands close by (a lavender island with leaf mounds and daisies) and five more further out, bushes framing the view at about 30 m on both sides. On the left, 28–45 m away, stands **one character tree group** of five unequal trees (three oaks, a spruce and a slim white birch as the colour accent) with broad, finely faceted crowns, visible trunks, limbs and gaps between them. Further back are loose forest clumps with a clear corridor where the path climbs a ridge, simplified forest groups, a rounded forest line and three blue hill layers that get paler with distance. The sun stands low on the left and slightly ahead of you (warm glow in the sky, long soft shadows fall to the right and towards you; one cloud next to the sun casts a soft shadow on the meadow). Seven soft faceted clouds, blue-green shade. No buildings, props, signs, text, fog, bloom or post-processing.

You can walk: over the meadow, between the trunks of the tree group and along the path down to the swale before the ridge (about 2,000 m², 71 m of the path). Left thumbstick walks slowly in the direction you look (1.2 m/s, 0.3 s to start and stop), right thumbstick turns in 30° snap steps. At the edge of the area and at trunks you slow down softly and glide along instead of bumping into an invisible wall. The forest clumps stay at least 15 m away; what you can walk up to is built at full detail. Nothing moves unless you hold a stick.

## How it is built

| Part | File | Role |
|---|---|---|
| The composition as data | `Assets/Droply/Scripts/Layout/Plan.cs` | where the tree group, clumps, bushes, grass groups, flower islands, stones and clouds stand |
| Geometry, deterministic, no Unity objects | `Layout/LandscapeBuilder.cs`, `TerrainModel.cs`, `PathModel.cs`, `TreeFactory.cs`, `Shapes.cs`, `MeshKit.cs`, `Rng.cs`, `Noise.cs` | builds merged, flat-shaded mesh layers (one per material), about 76,000 triangles / 159,000 vertices |
| Colour and light, baked | `Layout/Palette.cs`, `Look.cs`, `Lighting.cs`, `Sky.cs` | `Look`: colour of every surface point before light (gradients: crowns cool and dark below, warm and light on top; grass dark at the root; path light in the middle, darker worn rim; meadow in soft patches). `Lighting`: warm sun with soft shadows from sphere occluders, ambient occlusion, contact darkening, glow through leaves, haze towards the horizon colour, soft clip; the layers are baked on all processor cores at once, with exactly the colours of a single-thread bake. `Sky`: vertex-coloured dome and sun disc |
| Composition rules as checks | `Layout/LandscapeChecks.cs` | open foreground, clear path, one grouped tree group, no tree rows or carpet, open sight corridors, relief, calm palette, budget, and that walking keeps all of it intact |
| Walking, as plain maths | `Layout/WalkArea.cs`, `Layout/Walker.cs` (outline in `Plan.WalkOutline`) | where one may walk (signed distance to the edge, trunks, bushes and stones kept clear), slow head-directed walking with a short ramp, a soft cushion at the edge, snap turns |
| Unity side | `Assets/Droply/Scripts/LandscapeGenerator.cs`, `Assets/Droply/Shaders/VertexColorUnlit.shader` | meshes with vertex colours, four materials, the unlit shader that draws them, camera; the sky dome and the sun follow the eye. Falls back to a plainer lit URP look (flat colour per material, real sun, skybox) if the shader is missing or unsupported |
| Head tracking | `Assets/Droply/Scripts/HeadsetPose.cs` | follows the head (read in Update and again just before rendering), asks for the floor tracking origin; imposes no height |
| Walking on the headset | `Assets/Droply/Scripts/ViewerLocomotion.cs` | reads the thumbsticks (`InputDevices`), moves the tracking space, keeps its floor on the ground under the head |
| Editor setup | `Assets/Droply/Editor/ProjectSetup.cs` | creates the scene (tracking space with walking, tracked camera), URP asset (MSAA 4), OpenXR/Quest settings (Meta Quest Support, Touch and Touch Plus controller profiles) and validates |

Trees are a trunk, limbs and several unequal, individually rough crown lobes (never a ball on a stick, never a cone); the near oaks have finer facets; spruces are ragged, uneven tiers. Everything is flat shaded (faceted). There are no palms. Random numbers (`Rng`, a SplitMix64 of its own) only vary size, rotation and tint; the design is in `Plan.cs`.

## Open it in Unity

Open the repository root in Unity **6000.6.3f1** and let the Package Manager resolve the pinned manifest. Run **Droply → Generate and configure landscape**. It creates and opens `Assets/Droply/Scenes/Meadow.unity`, configures a Quest-friendly URP asset (4× MSAA), adds the scene to Build Settings, assigns the Android OpenXR loader, stores references to the vertex colour shader and the URP Lit/Unlit shaders in the scene (so they are part of player builds) and validates. **Droply → Validate landscape** repeats the validation, including the composition checks and whether the shader is present and supported.

The committed `Meadow.unity`, `ProjectSettings/GraphicsSettings.asset` and `Assets/XR/Settings/OpenXR Package Settings.asset` were edited **by hand**, because Unity could not be run here: the scene references the new shader (fixed GUID in `VertexColorUnlit.shader.meta`; the shader is also in the always-included list) and has a "Tracking space" root with `ViewerLocomotion` around the tracked camera; for Android, Meta Quest Support and the Oculus Touch and Meta Quest Touch Plus controller profiles were switched on (they were all off: the app would probably not have started as a Quest VR app, and the thumbsticks would have reported nothing). Running the menu command rewrites all of it properly; do it once after pulling.

In Build Profiles select **Android**, confirm **Project Settings → XR Plug-in Management → Android → OpenXR** (with Meta Quest Support and the Touch controller profiles), and configure signing for your device. Head movement follows the tracked head; the left thumbstick walks, the right one turns in snap steps (see "What you see"). There are no comfort effects such as a vignette. On start the landscape is built once (the log line `Droply landscape: …` with the build time appears in `adb logcat -s Unity`).

## Check the landscape without Unity

Needs the .NET 8 SDK. Nothing here is part of the Unity project (Unity only compiles `Assets/` and `Packages/`).

```
dotnet run --project Tools/CompositionCheck                       # build the landscape, print the numbers, run the composition checks (exit code 1 on a violation)
dotnet run --project Tools/CompositionCheck -- selftest           # prove the checks catch bad scenes (old tree rows, a tree gate, grass at the feet, a tree on the path, carpets of flecks, bad walk areas)
                                                                  # and simulate walking at 72 fps: into trunks and the edge, along the path, ten minutes of wandering
dotnet run --project Tools/CompositionCheck -- probe 26           # baked ground colour along the line z = 26 (where does a cast shadow lie?)
dotnet run --project Tools/CompositionCheck -- bench              # build time on this machine (best of 7), light bake on all cores and on one
dotnet build Tools/CompileCheck                                   # compile all runtime scripts against the real UnityEngine assemblies (NuGet UnityEngine.Modules, 2021.3 API surface)
dotnet run --project Tools/CompositionCheck -- export Tools/Preview/scene.json
cd Tools/Preview && npm install && python3 -m http.server        # then open http://localhost:8000: drag to look around, W A S D to walk (no walk-area limit in the preview)
```

The preview is three.js, not Unity's renderer. It draws exactly the baked vertex colours (unlit, the same sRGB decode as the shader), so the light, shadows, colours and composition are what the headset gets; differences are anti-aliasing, the display and the real optics. Pictures made with it are in `docs/vorschau/` (the queries that make them are listed in `docs/landschaft-reparatur.md`).

## What has not been verified

- **Nothing was run in Unity or on a Quest 3S.** The runtime scripts compile against the real UnityEngine assemblies (2021.3 surface), the geometry, the light bake and the checks run outside Unity. The shader body was syntax-checked with glslang and preprocessed with the real URP 17.6.0 shader library for the four variants the Quest can use (no stereo, instancing, multiview, single-pass instanced): every include resolves and the macros expand as expected, but it was **not compiled by Unity/URP** (glslang cannot compile the URP library itself, DXC was not available). `ProjectSetup.cs` (needs UnityEditor, URP and XR packages) was only read; its OpenXR calls and feature class names were checked against the OpenXR 1.18.0 package source. The hand-edited scene and graphics settings are untested.
- **Walking has not been tried in a headset.** The rules (speed, ramp, cushion, snap turns, the walk area) are simulated outside Unity, the Unity side compiles, and the OpenXR 1.18.0 source confirms that both Touch controller profiles report the thumbstick as `Primary2DAxis` and register their actions even with the old input manager active (as in this project). Whether the sticks really arrive on the device, whether 1.2 m/s and 30° feel comfortable and whether the floor feels steady on the slopes is untested. Teleportation (optional in the rules) is not built.
- **Build time on the headset is unknown.** On a 4-core machine the whole build now takes about 0.26 s, the light bake 0.11 s (before this round: 0.57 s / 0.43 s; warm, best of 7, `-- bench`): the bake runs on all cores and the path distance query skips far stretches, with a byte-identical result. On the Quest it is expected to be several times slower; the generator logs the real figure (`adb logcat -s Unity`).
- Frame rate, GPU time, thermal behaviour, the 4× MSAA cost, banding in the sky gradient and the look of the sun glow on the real displays are unmeasured (Meta's OVR Metrics Tool shows them in the headset). Expected load is low (76,000 triangles, 41 draw calls, a trivial shader, per frame only the walking and the sky following the eye, no GC allocation). What Phase 9 deliberately did not build (culling tiles, runtime LOD groups, instancing, drawing the sky last, foveated rendering, 90 Hz) and why is in `docs/landschaft-reparatur.md`, section 5.
- Whether the landscape feels calm and natural from the headset is a judgement only a person in the headset can make.
