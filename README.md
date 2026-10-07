# Droply VR — Unity landscape for Meta Quest 3S

Standalone Unity **6000.6.3f1** project (URP 17.6.0, OpenXR) for a calm, composed low-poly landscape on Meta Quest 3S. 1 Unity unit = 1 metre, no downloaded art, nothing is loaded at runtime: the whole scene is generated deterministically from code. The fundamental rules are in `CLAUDE.md`; what was repaired and why is in `docs/landschaft-reparatur.md` (German).

## What you see

Standing on an open, gently rolling meadow, looking along a warm sand path that starts at your feet as a narrow tongue and bends right around a mound. A few tufts of grass and some stones lie in the open foreground; nothing is closer than 9 m except the path. On the left, 30–42 m away, stands **one character tree group** of five unequal trees (oak, beech, spruce) with visible trunks, limbs and gaps between them. Further back are loose forest clumps with a clear corridor where the path climbs a ridge, simplified forest groups, a low forest line, and three hill layers that get paler and bluer-green with distance. Seven small flower islands, soft faceted clouds, warm side sun from the left (long soft shadows to the right), blue-green shade. No buildings, props, signs, text, fog or post-processing.

## How it is built

| Part | File | Role |
|---|---|---|
| The composition as data | `Assets/Droply/Scripts/Layout/Plan.cs` | where the tree group, clumps, grass groups, flower islands, stones and clouds stand |
| Geometry, deterministic, no Unity objects | `Layout/LandscapeBuilder.cs`, `TerrainModel.cs`, `PathModel.cs`, `TreeFactory.cs`, `Shapes.cs`, `MeshKit.cs`, `Palette.cs`, `Rng.cs`, `Noise.cs` | builds merged, flat-shaded mesh layers (one per material), about 40,000 triangles |
| Composition rules as checks | `Layout/LandscapeChecks.cs` | open foreground, clear path, one grouped tree group, no tree rows or carpet, open sight corridors, relief, calm palette, budget |
| Unity side | `Assets/Droply/Scripts/LandscapeGenerator.cs` | turns the layers into meshes and materials, sets the sun, trilight ambient, sky and camera |
| Head tracking | `Assets/Droply/Scripts/HeadsetPose.cs` | follows the head, asks for the floor tracking origin; imposes no height |
| Editor setup | `Assets/Droply/Editor/ProjectSetup.cs` | creates the scene, URP asset, OpenXR/Quest settings and validates |

Trees are a trunk, limbs and several unequal, individually rough crown lobes (never a ball on a stick, never a cone); spruces are ragged, uneven tiers. Everything is flat shaded (faceted). There are no palms. Random numbers (`Rng`, a SplitMix64 of its own) only vary size, rotation and tint; the design is in `Plan.cs`.

## Open it in Unity

Open the repository root in Unity **6000.6.3f1** and let the Package Manager resolve the pinned manifest. Run **Droply → Generate and configure landscape**. It creates and opens `Assets/Droply/Scenes/Meadow.unity`, configures a Quest-friendly URP asset, adds the scene to Build Settings, assigns the Android OpenXR loader, stores references to the URP Lit/Unlit shaders in the scene (so they are part of player builds) and validates. **Re-run it after pulling this change**: the committed scene was generated with the previous generator and has no shader references. **Droply → Validate landscape** repeats the validation, including the composition checks.

In Build Profiles select **Android**, confirm **Project Settings → XR Plug-in Management → Android → OpenXR**, and configure signing for your device. The app is a stationary scenic experience: head movement follows the tracked head, there is no artificial locomotion and no comfort effect.

## Check the landscape without Unity

Needs the .NET 8 SDK. Nothing here is part of the Unity project (Unity only compiles `Assets/` and `Packages/`).

```
dotnet run --project Tools/CompositionCheck                       # build the landscape, print the numbers, run the composition checks (exit code 1 on a violation)
dotnet run --project Tools/CompositionCheck -- selftest           # prove the checks catch bad scenes: old tree rows, a tree gate, grass at the feet, a tree on the path
dotnet build Tools/CompileCheck                                   # compile all runtime scripts against the real UnityEngine assemblies (NuGet UnityEngine.Modules, 2021.3 API surface)
dotnet run --project Tools/CompositionCheck -- export Tools/Preview/scene.json
cd Tools/Preview && npm install && python3 -m http.server        # then open http://localhost:8000: drag to look around from the eye height of a standing viewer
```

The preview is three.js, not Unity's renderer: composition, shapes and the rough light are right, colours, shader and anti-aliasing differ from URP on the headset. Pictures made with it are in `docs/vorschau/`.

## What has not been verified

- **Nothing was run in Unity or on a Quest 3S.** The scripts compile against the real UnityEngine assemblies (2021.3 surface) and the geometry and checks run outside Unity, but the editor scripts (`ProjectSetup.cs`, which needs UnityEditor, URP and XR packages), the URP materials, the sky colour against the haze colours, soft shadows, frame rate, GPU time and comfort are untested.
- The tracking-origin request (`HeadsetPose`) and the shader references in the generated scene need a first run on the device.
- Quest 3S performance is expected to be fine (about 40,000 triangles, 36 merged meshes, about 11,000 shadow-casting triangles, no per-frame script work, no GC allocation) but not measured.
- Whether the landscape feels calm and natural from the headset is a judgement only a person in the headset can make.
