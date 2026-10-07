# Droply VR — Unity landscape for Meta Quest 3S

Standalone Unity **6000.6.3f1** project (URP 17.6.0, OpenXR) for a calm, composed low-poly landscape on Meta Quest 3S. 1 Unity unit = 1 metre, no downloaded art, nothing is loaded at runtime: the whole scene is generated deterministically from code, **with the light already baked into the vertex colours** (no lights, no shadow maps, no skybox on the headset). The fundamental rules are in `CLAUDE.md`, the concept image in `docs/konzept-landschaft.png`; what was repaired and improved, and why, is in `docs/landschaft-reparatur.md` (German).

## What you see

Standing on an open, gently rolling, sunlit meadow, looking along a warm sand path that starts at your feet as a narrow tongue and bends right around a mound. In the foreground and middle ground: a few stones and pebbles along the path edge, grass tufts of spiky blades (deliberate groups, tufts at the path edge, small flecks in irregular patches), two flower islands close by (a lavender island with leaf mounds and daisies) and five more further out, bushes framing the view at about 30 m on both sides. On the left, 28–45 m away, stands **one character tree group** of five unequal trees (three oaks, a spruce and a slim white birch as the colour accent) with broad, finely faceted crowns, visible trunks, limbs and gaps between them. Further back are loose forest clumps with a clear corridor where the path climbs a ridge, simplified forest groups, a rounded forest line and three blue hill layers that get paler with distance. The sun stands low on the left and slightly ahead of you (warm glow in the sky, long soft shadows fall to the right and towards you; one cloud next to the sun casts a soft shadow on the meadow). Seven soft faceted clouds, blue-green shade. No buildings, props, signs, text, fog, bloom or post-processing.

## How it is built

| Part | File | Role |
|---|---|---|
| The composition as data | `Assets/Droply/Scripts/Layout/Plan.cs` | where the tree group, clumps, bushes, grass groups, flower islands, stones and clouds stand |
| Geometry, deterministic, no Unity objects | `Layout/LandscapeBuilder.cs`, `TerrainModel.cs`, `PathModel.cs`, `TreeFactory.cs`, `Shapes.cs`, `MeshKit.cs`, `Rng.cs`, `Noise.cs` | builds merged, flat-shaded mesh layers (one per material), about 73,000 triangles / 150,000 vertices |
| Colour and light, baked | `Layout/Palette.cs`, `Look.cs`, `Lighting.cs`, `Sky.cs` | `Look`: colour of every surface point before light (gradients: crowns cool and dark below, warm and light on top; grass dark at the root; path light in the middle, darker worn rim; meadow in soft patches). `Lighting`: warm sun with soft shadows from sphere occluders, ambient occlusion, contact darkening, glow through leaves, haze towards the horizon colour, soft clip. `Sky`: vertex-coloured dome and sun disc |
| Composition rules as checks | `Layout/LandscapeChecks.cs` | open foreground, clear path, one grouped tree group, no tree rows or carpet, open sight corridors, relief, calm palette, budget |
| Unity side | `Assets/Droply/Scripts/LandscapeGenerator.cs`, `Assets/Droply/Shaders/VertexColorUnlit.shader` | meshes with vertex colours, four materials, the unlit shader that draws them, camera. Falls back to a plainer lit URP look (flat colour per material, real sun, skybox) if the shader is missing or unsupported |
| Head tracking | `Assets/Droply/Scripts/HeadsetPose.cs` | follows the head, asks for the floor tracking origin; imposes no height |
| Editor setup | `Assets/Droply/Editor/ProjectSetup.cs` | creates the scene, URP asset (MSAA 4), OpenXR/Quest settings and validates |

Trees are a trunk, limbs and several unequal, individually rough crown lobes (never a ball on a stick, never a cone); the near oaks have finer facets; spruces are ragged, uneven tiers. Everything is flat shaded (faceted). There are no palms. Random numbers (`Rng`, a SplitMix64 of its own) only vary size, rotation and tint; the design is in `Plan.cs`.

## Open it in Unity

Open the repository root in Unity **6000.6.3f1** and let the Package Manager resolve the pinned manifest. Run **Droply → Generate and configure landscape**. It creates and opens `Assets/Droply/Scenes/Meadow.unity`, configures a Quest-friendly URP asset (4× MSAA), adds the scene to Build Settings, assigns the Android OpenXR loader, stores references to the vertex colour shader and the URP Lit/Unlit shaders in the scene (so they are part of player builds) and validates. **Droply → Validate landscape** repeats the validation, including the composition checks and whether the shader is present and supported.

The committed `Meadow.unity` and `ProjectSettings/GraphicsSettings.asset` were edited **by hand** to reference the new shader (fixed GUID in `VertexColorUnlit.shader.meta`; the shader is also in the always-included list), because Unity could not be run here. Running the menu command rewrites the scene properly; do it once after pulling.

In Build Profiles select **Android**, confirm **Project Settings → XR Plug-in Management → Android → OpenXR**, and configure signing for your device. The app is a stationary scenic experience: head movement follows the tracked head, there is no artificial locomotion and no comfort effect. On start the landscape is built once (the log line `Droply landscape: …` with the build time appears in `adb logcat -s Unity`).

## Check the landscape without Unity

Needs the .NET 8 SDK. Nothing here is part of the Unity project (Unity only compiles `Assets/` and `Packages/`).

```
dotnet run --project Tools/CompositionCheck                       # build the landscape, print the numbers, run the composition checks (exit code 1 on a violation)
dotnet run --project Tools/CompositionCheck -- selftest           # prove the checks catch bad scenes: old tree rows, a tree gate, grass at the feet, a tree on the path, carpets of flecks
dotnet run --project Tools/CompositionCheck -- probe 26           # baked ground colour along the line z = 26 (where does a cast shadow lie?)
dotnet build Tools/CompileCheck                                   # compile all runtime scripts against the real UnityEngine assemblies (NuGet UnityEngine.Modules, 2021.3 API surface)
dotnet run --project Tools/CompositionCheck -- export Tools/Preview/scene.json
cd Tools/Preview && npm install && python3 -m http.server        # then open http://localhost:8000: drag to look around from the eye height of a standing viewer
```

The preview is three.js, not Unity's renderer. It draws exactly the baked vertex colours (unlit, the same sRGB decode as the shader), so the light, shadows, colours and composition are what the headset gets; differences are anti-aliasing, the display and the real optics. Pictures made with it are in `docs/vorschau/` (the queries that make them are listed in `docs/landschaft-reparatur.md`).

## What has not been verified

- **Nothing was run in Unity or on a Quest 3S.** The runtime scripts compile against the real UnityEngine assemblies (2021.3 surface), the geometry, the light bake and the checks run outside Unity, and the shader body was syntax-checked with glslang (HLSL front end, URP macros stubbed). The shader was **not compiled by Unity/URP**, `ProjectSetup.cs` (needs UnityEditor, URP and XR packages) was only read, and the hand-edited scene and graphics settings are untested.
- **Build time on the headset is unknown.** The light bake takes about 0.25 s on a desktop (the whole build about 0.45 s); on the Quest it is expected to be several times slower (a startup pause of one to two seconds is plausible). The generator logs the real figure. If it is too slow, the bake per layer can be run in parallel (the layers are independent).
- Frame rate, GPU time, thermal behaviour, the 4× MSAA cost, banding in the sky gradient and the look of the sun glow on the real displays are unmeasured. Expected load is low (73,000 triangles, 41 draw calls, a trivial shader, no per-frame script work, no GC allocation).
- Whether the landscape feels calm and natural from the headset is a judgement only a person in the headset can make.
