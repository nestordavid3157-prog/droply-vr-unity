using UnityEngine;

namespace Droply.Landscape
{
    /// <summary>Every material of the landscape. Mesh layers are merged per material; the colour of a vertex comes from <see cref="Look"/> and <see cref="Lighting"/>, the entry here is its base colour.</summary>
    public enum Mat
    {
        GroundBase,
        PathCore,
        Trunk, BirchBark,
        OakTop, OakMid, OakDark,
        BeechTop, BeechMid, BeechDark,
        BirchTop, BirchMid, BirchDark,
        SpruceTop, SpruceMid, SpruceDark,
        ShrubMid, ShrubDark,
        GrassDark, GrassMid, GrassLight,
        FlowerStem, FlowerCenter, FlowerLavender, FlowerWhite, FlowerYellow,
        Stone, StoneWarm,
        CloudTop, CloudBottom,
        TreeLineNear, TreeLineFar,
        HazeTree1, HazeTree2,
        Hill1, Hill2, Hill3,
        Sky, SunDisc
    }

    /// <summary>How a material is treated by <see cref="Look"/> (which gradients) and <see cref="Lighting"/> (which light).</summary>
    public enum SurfaceKind { Ground, Path, Bark, BirchBark, Foliage, Grass, Petal, Stem, Stone, Cloud, Distance, Other }

    /// <summary>
    /// Colours (sRGB) and light settings. Not one green: warm sunlit grass, cooler shaded grass, blue-green canopy shade, warm brown bark, white birch bark, sand path;
    /// distance layers fade towards the horizon colour (no fog). Nothing is neon: vegetation saturation stays below 0.62 (checked in <see cref="LandscapeChecks"/>).
    /// </summary>
    public static class Palette
    {
        public static Color32 Color(Mat m)
        {
            switch (m)
            {
                case Mat.GroundBase: return new Color32(126, 166, 70, 255);
                case Mat.PathCore: return new Color32(224, 186, 124, 255);
                case Mat.Trunk: return new Color32(116, 82, 58, 255);
                case Mat.BirchBark: return new Color32(242, 238, 228, 255);
                case Mat.OakTop: return new Color32(138, 176, 84, 255);
                case Mat.OakMid: return new Color32(96, 148, 76, 255);
                case Mat.OakDark: return new Color32(54, 106, 84, 255);
                case Mat.BeechTop: return new Color32(152, 178, 86, 255);
                case Mat.BeechMid: return new Color32(114, 152, 76, 255);
                case Mat.BeechDark: return new Color32(72, 118, 78, 255);
                case Mat.BirchTop: return new Color32(194, 212, 104, 255);
                case Mat.BirchMid: return new Color32(152, 186, 90, 255);
                case Mat.BirchDark: return new Color32(92, 130, 80, 255);
                case Mat.SpruceTop: return new Color32(62, 120, 90, 255);
                case Mat.SpruceMid: return new Color32(44, 98, 78, 255);
                case Mat.SpruceDark: return new Color32(32, 78, 66, 255);
                case Mat.ShrubMid: return new Color32(110, 158, 72, 255);
                case Mat.ShrubDark: return new Color32(76, 128, 74, 255);
                case Mat.GrassDark: return new Color32(86, 134, 66, 255);
                case Mat.GrassMid: return new Color32(124, 170, 70, 255);
                case Mat.GrassLight: return new Color32(168, 198, 84, 255);
                case Mat.FlowerStem: return new Color32(96, 140, 66, 255);
                case Mat.FlowerCenter: return new Color32(232, 160, 58, 255);
                case Mat.FlowerLavender: return new Color32(164, 132, 218, 255);
                case Mat.FlowerWhite: return new Color32(250, 248, 240, 255);
                case Mat.FlowerYellow: return new Color32(248, 212, 92, 255);
                case Mat.Stone: return new Color32(152, 148, 140, 255);
                case Mat.StoneWarm: return new Color32(164, 150, 132, 255);
                case Mat.CloudTop: return new Color32(250, 252, 252, 255);
                case Mat.CloudBottom: return new Color32(206, 222, 236, 255);
                case Mat.TreeLineNear: return new Color32(58, 110, 100, 255);
                case Mat.TreeLineFar: return new Color32(86, 138, 134, 255);
                case Mat.HazeTree1: return new Color32(66, 118, 102, 255);
                case Mat.HazeTree2: return new Color32(100, 148, 140, 255);
                case Mat.Hill1: return new Color32(98, 148, 168, 255);
                case Mat.Hill2: return new Color32(124, 170, 194, 255);
                case Mat.Hill3: return new Color32(154, 192, 212, 255);
                case Mat.Sky: return new Color32(120, 170, 228, 255);
                case Mat.SunDisc: return new Color32(255, 248, 226, 255);
                default: return new Color32(255, 0, 255, 255);
            }
        }

        public static SurfaceKind Kind(Mat m)
        {
            switch (m)
            {
                case Mat.GroundBase: return SurfaceKind.Ground;
                case Mat.PathCore: return SurfaceKind.Path;
                case Mat.Trunk: return SurfaceKind.Bark;
                case Mat.BirchBark: return SurfaceKind.BirchBark;
                case Mat.GrassDark: case Mat.GrassMid: case Mat.GrassLight: return SurfaceKind.Grass;
                case Mat.FlowerLavender: case Mat.FlowerWhite: case Mat.FlowerYellow: case Mat.FlowerCenter: return SurfaceKind.Petal;
                case Mat.FlowerStem: return SurfaceKind.Stem;
                case Mat.Stone: case Mat.StoneWarm: return SurfaceKind.Stone;
                case Mat.CloudTop: case Mat.CloudBottom: return SurfaceKind.Cloud;
                case Mat.TreeLineNear: case Mat.TreeLineFar: case Mat.Hill1: case Mat.Hill2: case Mat.Hill3: return SurfaceKind.Distance;
                case Mat.Sky: case Mat.SunDisc: return SurfaceKind.Other;
                default: return SurfaceKind.Foliage; // crowns, tiers, bushes, distant blobs
            }
        }

        /// <summary>Thin blades and petals are single triangles that must be seen from both sides.</summary>
        public static bool DoubleSided(Mat m)
        {
            SurfaceKind k = Kind(m);
            return k == SurfaceKind.Grass || k == SurfaceKind.Petal || k == SurfaceKind.Stem;
        }

        /// <summary>Distant layers and clouds are flat shapes: almost no direction in their light, no shadows, no ambient occlusion.</summary>
        public static bool Atmospheric(Mat m)
        {
            SurfaceKind k = Kind(m);
            return k == SurfaceKind.Distance || k == SurfaceKind.Cloud;
        }

        /// <summary>Things that stand on the ground get a dark contact zone at their foot.</summary>
        public static bool Upright(Mat m)
        {
            SurfaceKind k = Kind(m);
            return k == SurfaceKind.Bark || k == SurfaceKind.BirchBark || k == SurfaceKind.Stone || k == SurfaceKind.Grass || k == SurfaceKind.Stem || k == SurfaceKind.Foliage;
        }

        /// <summary>How much of the sun shines through leaves from behind (a warm glow on the side facing away from the sun).</summary>
        public static float Translucency(Mat m)
        {
            switch (Kind(m))
            {
                case SurfaceKind.Grass: return .5f;
                case SurfaceKind.Petal: return .3f;
                case SurfaceKind.Foliage: return .16f;
                default: return 0f;
            }
        }

        /// <summary>Only the baked pipeline draws these (the sky dome and the sun disc); the lit fallback uses the skybox and a light instead.</summary>
        public static bool BakedOnly(Mat m) { return m == Mat.Sky || m == Mat.SunDisc; }

        /// <summary>
        /// Used by the lit fallback only: the forest line, the hills and the clouds are flat atmospheric shapes there (unlit, so nothing depends on shader keywords
        /// that a build could strip).
        /// </summary>
        public static bool Unlit(Mat m) { return Atmospheric(m) || BakedOnly(m); }

        /// <summary>Colour of the sky at the horizon: the far layers and the ground fade towards it.</summary>
        public static readonly Color32 HorizonColor = new Color32(184, 214, 228, 255);
        public static readonly Color32 ZenithColor = new Color32(64, 128, 212, 255);
        public static readonly Color32 SunDiscColor = new Color32(255, 248, 226, 255);

        // Warm side light from the left and slightly ahead (it travels towards +x and a little towards the viewer): long soft shadows fall across the path and the meadow
        // in front of the trees and show from the viewpoint, the sides of the crowns facing the viewer are lit on the left and glow through the leaves on the right.
        // Sky and ground bounce give blue-green shade.
        public const float SunPitch = 28f, SunYaw = 122f, SunIntensity = 1.2f, SunShadowStrength = 0.72f;
        public static readonly Color SunColor = new Color(1f, 0.89f, 0.70f);
        public static readonly Color AmbientSky = new Color(0.64f, 0.77f, 0.92f);
        public static readonly Color AmbientEquator = new Color(0.64f, 0.72f, 0.64f);
        public static readonly Color AmbientGround = new Color(0.44f, 0.46f, 0.34f);
        public static readonly Color SkyTint = new Color(0.46f, 0.66f, 0.86f);
    }
}
