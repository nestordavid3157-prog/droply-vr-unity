using UnityEngine;

namespace Droply.Landscape
{
    /// <summary>Every material of the landscape. One material per entry; mesh layers are merged per material.</summary>
    public enum Mat
    {
        GroundBase, GroundSun, GroundShade,
        PathCore, PathEdge,
        Trunk,
        OakTop, OakMid, OakDark,
        BeechTop, BeechMid, BeechDark,
        SpruceTop, SpruceMid, SpruceDark,
        ShrubMid, ShrubDark,
        GrassDark, GrassMid, GrassLight,
        FlowerStem, FlowerCenter, FlowerLavender, FlowerWhite, FlowerYellow,
        Stone, StoneWarm,
        CloudTop, CloudBottom,
        TreeLineNear, TreeLineFar,
        HazeTree1, HazeTree2,
        Hill1, Hill2, Hill3
    }

    /// <summary>
    /// Colours (sRGB) and light settings. Not one green: warm sunlit grass, cooler shaded grass, blue-green canopy shade, warm brown bark, sand path;
    /// distance layers fade towards the horizon colour (no fog). Nothing is neon: vegetation saturation stays below 0.62 (checked in <see cref="LandscapeChecks"/>).
    /// </summary>
    public static class Palette
    {
        public static Color32 Color(Mat m)
        {
            switch (m)
            {
                case Mat.GroundBase: return new Color32(114, 148, 78, 255);
                case Mat.GroundSun: return new Color32(130, 160, 84, 255);
                case Mat.GroundShade: return new Color32(102, 140, 80, 255);
                case Mat.PathCore: return new Color32(218, 184, 128, 255);
                case Mat.PathEdge: return new Color32(190, 160, 108, 255);
                case Mat.Trunk: return new Color32(116, 82, 58, 255);
                case Mat.OakTop: return new Color32(138, 176, 84, 255);
                case Mat.OakMid: return new Color32(96, 148, 76, 255);
                case Mat.OakDark: return new Color32(54, 106, 84, 255);
                case Mat.BeechTop: return new Color32(152, 178, 86, 255);
                case Mat.BeechMid: return new Color32(114, 152, 76, 255);
                case Mat.BeechDark: return new Color32(72, 118, 78, 255);
                case Mat.SpruceTop: return new Color32(62, 120, 90, 255);
                case Mat.SpruceMid: return new Color32(44, 98, 78, 255);
                case Mat.SpruceDark: return new Color32(32, 78, 66, 255);
                case Mat.ShrubMid: return new Color32(98, 142, 74, 255);
                case Mat.ShrubDark: return new Color32(62, 108, 76, 255);
                case Mat.GrassDark: return new Color32(98, 134, 72, 255);
                case Mat.GrassMid: return new Color32(136, 164, 80, 255);
                case Mat.GrassLight: return new Color32(176, 186, 98, 255);
                case Mat.FlowerStem: return new Color32(96, 140, 66, 255);
                case Mat.FlowerCenter: return new Color32(222, 182, 96, 255);
                case Mat.FlowerLavender: return new Color32(148, 122, 198, 255);
                case Mat.FlowerWhite: return new Color32(244, 240, 230, 255);
                case Mat.FlowerYellow: return new Color32(242, 208, 104, 255);
                case Mat.Stone: return new Color32(152, 148, 140, 255);
                case Mat.StoneWarm: return new Color32(164, 150, 132, 255);
                case Mat.CloudTop: return new Color32(250, 252, 252, 255);
                case Mat.CloudBottom: return new Color32(206, 222, 236, 255);
                case Mat.TreeLineNear: return new Color32(58, 108, 92, 255);
                case Mat.TreeLineFar: return new Color32(84, 134, 120, 255);
                case Mat.HazeTree1: return new Color32(70, 120, 100, 255);
                case Mat.HazeTree2: return new Color32(98, 146, 130, 255);
                case Mat.Hill1: return new Color32(110, 156, 134, 255);
                case Mat.Hill2: return new Color32(134, 176, 168, 255);
                case Mat.Hill3: return new Color32(160, 196, 200, 255);
                default: return new Color32(255, 0, 255, 255);
            }
        }

        /// <summary>Thin blades and petals are single triangles that must be seen from both sides.</summary>
        public static bool DoubleSided(Mat m)
        {
            return m == Mat.GrassDark || m == Mat.GrassMid || m == Mat.GrassLight ||
                   m == Mat.FlowerStem || m == Mat.FlowerCenter || m == Mat.FlowerLavender || m == Mat.FlowerWhite || m == Mat.FlowerYellow;
        }

        /// <summary>The forest line, the hills and the clouds are flat atmospheric shapes: unlit, so the haze colour stays the same all around, the shader is cheap, and nothing depends on shader keywords that a build could strip.</summary>
        public static bool Unlit(Mat m)
        {
            return m == Mat.TreeLineNear || m == Mat.TreeLineFar || m == Mat.Hill1 || m == Mat.Hill2 || m == Mat.Hill3 || m == Mat.CloudTop || m == Mat.CloudBottom;
        }

        /// <summary>Colour of the sky at the horizon: the far layers fade towards it.</summary>
        public static readonly Color32 HorizonColor = new Color32(184, 214, 228, 255);

        // Warm side light from the left, low enough for long soft shadows; sky and ground bounce give blue-green shade.
        public const float SunPitch = 26f, SunYaw = 97f, SunIntensity = 1.2f, SunShadowStrength = 0.72f;
        public static readonly Color SunColor = new Color(1f, 0.89f, 0.70f);
        public static readonly Color AmbientSky = new Color(0.64f, 0.77f, 0.92f);
        public static readonly Color AmbientEquator = new Color(0.64f, 0.72f, 0.64f);
        public static readonly Color AmbientGround = new Color(0.44f, 0.46f, 0.34f);
        public static readonly Color SkyTint = new Color(0.46f, 0.66f, 0.86f);
    }
}
