# Droply VR — Unity landscape foundation

Standalone Unity **6000.6.3f1** project for a calm, stylized landscape on Meta Quest 3S. It uses 1 Unity unit per meter, URP 17.6.0, OpenXR, and no downloaded art. Open the repository root in that Unity editor; allow Package Manager to resolve the pinned manifest, then run **Droply → Generate and configure landscape**. The command creates and opens `Assets/Droply/Scenes/Meadow.unity`, configures a Quest-friendly URP asset, adds the scene to Build Settings, assigns the Android OpenXR loader, and validates the scene/composition. The landscape mesh and foliage are generated deterministically when the scene enters Play mode.

The deterministic scene has a custom rolling meadow mesh, a curved three-band sandy path, sparse individual broad-leaf grass clumps, six separate flower patches, five rounded faceted foreground stones, exactly one six-tree branching character group, irregular distant forest layers, softened far hills, a blue procedural sky, and a few low-poly clouds. Palette blocks use URP Lit materials, side-warm directional light, and soft shadows. There are no buildings, props, signs, text, logos, interfaces, fog, or post-processing. The initial tracked viewpoint starts at the meadow edge facing the path. Camera height and pose are supplied by headset tracking; no head height is imposed.

## Quest setup and controls

In Unity, select **Android** in Build Profiles, install the Android module if prompted, and confirm **Project Settings → XR Plug-in Management → Android → OpenXR** is enabled. The setup menu tries to assign the OpenXR loader and logs a warning rather than hiding a setup failure. Configure Quest-compatible OpenXR features and Android signing/build settings in Unity for your device and release workflow. The generated app is a stationary scenic experience: no artificial locomotion is included. Head movement follows the XR head device's tracked local position and rotation; there are no comfort effects.

## Verification limits

Package pins: OpenXR 1.18.0 (declares Unity 6000.0 minimum), XR Management 4.7.0, XR Core Utils 2.6.0, Input System 1.19.0, and the editor-bundled URP 17.6.0. The local Unity editor is available at `C:\Users\stone\AppData\Local\Unity\bin\unity.exe` (Editor 6000.6.3f1). Quest hardware, Android SDK/build tools, and headset runtime have not been tested or claimed. Run the project setup command and validation menu in the editor before building.
