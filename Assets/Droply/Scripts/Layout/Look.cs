using UnityEngine;

namespace Droply.Landscape
{
    /// <summary>
    /// The colour of every surface before light: per material, position and the vertex tag (see <see cref="MeshData.Tags"/>). All values are linear RGB in a Vector3.
    /// Gradients carry the style: leaves are cool and dark at the bottom of a crown and warm and light at the top, grass is dark at the root and bright at the tip,
    /// the path fades into the meadow, the meadow itself drifts between cool shade, fresh green and warm dry patches, stones get a little moss at their foot.
    /// </summary>
    public static class Look
    {
        const float Gamma = 2.2f;

        public static Vector3 Linear(float r, float g, float b)
        {
            return new Vector3(Mathf.Pow(r / 255f, Gamma), Mathf.Pow(g / 255f, Gamma), Mathf.Pow(b / 255f, Gamma));
        }

        public static Vector3 Linear(Color32 c) { return Linear(c.r, c.g, c.b); }

        public static Vector3 Linear(Color c) { return new Vector3(Mathf.Pow(c.r, Gamma), Mathf.Pow(c.g, Gamma), Mathf.Pow(c.b, Gamma)); }

        public static Color32 Encode(Vector3 linear)
        {
            return new Color32(Byte(linear.x), Byte(linear.y), Byte(linear.z), 255);
        }

        static byte Byte(float linear)
        {
            return (byte)Mathf.Clamp(Mathf.Pow(Mathf.Clamp01(linear), 1f / Gamma) * 255f + .5f, 0f, 255f);
        }

        public static float Smooth(float a, float b, float x) { return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((x - a) / (b - a))); }

        public static Vector3 Mix(Vector3 a, Vector3 b, float t) { t = Mathf.Clamp01(t); return a + (b - a) * t; }

        public static Vector3 Scale(Vector3 a, Vector3 b) { return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z); }

        // The meadow: cool fresh shade, a warm yellow-green in the sun, drier straw-coloured patches (all of it before light; the sun adds warmth, the sky blue to the shade).
        static readonly Vector3 GroundShade = Linear(84, 138, 76), GroundBase = Linear(122, 164, 68), GroundSun = Linear(150, 186, 70), GroundDry = Linear(182, 182, 88);
        static readonly Vector3 SandLight = Linear(234, 194, 128), Sand = Linear(222, 180, 114), SandEdge = Linear(176, 136, 86);
        static readonly Vector3 Moss = Linear(84, 104, 60), BarkDark = Linear(52, 46, 42);
        static readonly Vector3 Warm = new Vector3(1.12f, 1.04f, .8f), Cool = new Vector3(.8f, .98f, 1.12f);

        static Vector3[] baseColors;

        /// <summary>Linear base colour of a material (computed once).</summary>
        static Vector3 BaseColor(Mat mat)
        {
            if (baseColors == null)
            {
                var table = new Vector3[System.Enum.GetValues(typeof(Mat)).Length];
                for (int i = 0; i < table.Length; i++) table[i] = Linear(Palette.Color((Mat)i));
                baseColors = table;
            }
            return baseColors[(int)mat];
        }

        /// <summary>The meadow: large soft patches of cool shade, fresh green and sunlit green, drier warm patches, and a fine speckle. Smooth, no edges.</summary>
        public static Vector3 Ground(float x, float z)
        {
            float large = Noise.Value(x * .022f + 5.1f, z * .022f + 1.7f, 41), mid = Noise.Value(x * .07f, z * .07f, 43), fine = Noise.Value(x * .45f, z * .45f, 47);
            float grain = Noise.Value(x * .34f + 17f, z * .05f, 49);   // long streaks along the view direction: the lie of the grass
            float t = Mathf.Clamp01((large * .66f + mid * .34f - .17f) / .62f);
            Vector3 c = t < .5f ? Mix(GroundShade, GroundBase, Smooth(.1f, .5f, t)) : Mix(GroundBase, GroundSun, Smooth(.5f, .88f, t));
            c = Mix(c, GroundDry, Smooth(.6f, .9f, Noise.Value(x * .035f + 9f, z * .035f + 3f, 53)) * .55f);
            return c * (.95f + .08f * fine + .06f * (grain - .5f));
        }

        /// <summary>
        /// Colour of a surface point. <paramref name="tag"/>: position inside the shape (0 bottom, root or centre; 1 top, tip or rim; on the path: 0 centre, 1 outer edge).
        /// <paramref name="height"/>: metres above the ground.
        /// </summary>
        public static Vector3 Albedo(Mat mat, Vector3 p, float tag, float jitter, float height)
        {
            Vector3 baseColor = BaseColor(mat);
            float g = Mathf.Clamp01(tag);
            switch (Palette.Kind(mat))
            {
                case SurfaceKind.Ground:
                    return Ground(p.x, p.z);
                case SurfaceKind.Path:
                {
                    // worn, mottled sand: slow patches of darker soil and fine speckle; light in the middle, darker where feet and rain have worn the edge
                    float speckle = .94f + .12f * Noise.Value(p.x * .55f, p.z * .55f, 61);
                    float worn = Smooth(.55f, .85f, Noise.Value(p.x * .17f + 4f, p.z * .17f + 9f, 67)) * .38f;
                    Vector3 c = g < .3f ? Mix(SandLight, Sand, g / .3f) : g < .6f ? Mix(Sand, SandEdge, (g - .3f) / .3f) : Mix(SandEdge, Ground(p.x, p.z), Smooth(.6f, 1f, g));
                    c = Mix(c, SandEdge, worn * (1f - Smooth(.6f, 1f, g)));
                    return c * speckle;
                }
                case SurfaceKind.Bark:
                {
                    Vector3 c = baseColor * (.88f + .22f * g);
                    c = Mix(c, Moss * 1.3f, (1f - Smooth(0f, .5f, height)) * .4f);
                    return c * (1f + jitter);
                }
                case SurfaceKind.BirchBark:
                {
                    float mark = Smooth(.7f, .8f, Noise.Value(p.x * 3.1f + p.z * 2.7f, p.y * 5.5f, 59));
                    Vector3 c = Mix(baseColor * (.92f + .12f * g), BarkDark, mark * .85f);
                    return Mix(c, Moss * 1.3f, (1f - Smooth(0f, .35f, height)) * .25f);
                }
                case SurfaceKind.Foliage:
                {
                    Vector3 c = baseColor * (.84f + .3f * g);
                    c = Mix(c, Scale(c, Warm), Smooth(.65f, 1f, g) * .6f);   // sun-bleached tips
                    c = Mix(c, Scale(c, Cool), (1f - g) * .5f);              // blue-green shade at the bottom
                    return c * (1f + jitter * 1.2f);
                }
                case SurfaceKind.Grass:
                {
                    Vector3 c = baseColor * (.7f + .52f * g);
                    c = Mix(c, Scale(c, Warm), Smooth(.5f, 1f, g) * .5f);
                    c = Mix(c, Scale(c, Cool), (1f - g) * .4f);
                    return c * (1f + jitter);
                }
                case SurfaceKind.Petal:
                {
                    Vector3 c = Mix(baseColor * 1.18f + new Vector3(.05f, .05f, .05f), baseColor * .92f, g);
                    return c * (1f + jitter);
                }
                case SurfaceKind.Stem:
                    return baseColor * (.6f + .6f * g) * (1f + jitter);
                case SurfaceKind.Stone:
                {
                    Vector3 c = baseColor * (.8f + .3f * g) * (1f + jitter);
                    return Mix(c, Ground(p.x, p.z) * .8f, (1f - Smooth(0f, .3f, height)) * .5f);
                }
                case SurfaceKind.Cloud:
                    return baseColor * (.92f + .1f * g) * (1f + jitter);
                case SurfaceKind.Distance:
                {
                    // far layers are hazier at their foot and a touch deeper at the crest
                    Vector3 horizon = Linear(Palette.HorizonColor);
                    return Mix(baseColor, horizon, (1f - g) * .35f) * (1f + jitter);
                }
                default:
                    return baseColor * (1f + jitter);
            }
        }
    }
}
