using UnityEngine;

namespace Droply.Landscape
{
    /// <summary>Smooth value noise with an integer hash. Own implementation (not Mathf.PerlinNoise) so the check tool computes the same terrain as the player.</summary>
    public static class Noise
    {
        static float Hash(int x, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393) + (uint)(z * 668265263) + (uint)(seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFFu) / 16777216f;
            }
        }

        /// <summary>Value in [0, 1].</summary>
        public static float Value(float x, float z, int seed)
        {
            int xi = Mathf.FloorToInt(x), zi = Mathf.FloorToInt(z);
            float fx = x - xi, fz = z - zi;
            float sx = fx * fx * (3f - 2f * fx), sz = fz * fz * (3f - 2f * fz);
            float a = Hash(xi, zi, seed), b = Hash(xi + 1, zi, seed), c = Hash(xi, zi + 1, seed), d = Hash(xi + 1, zi + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, sx), Mathf.Lerp(c, d, sx), sz);
        }

        /// <summary>Value in [-1, 1].</summary>
        public static float Signed(float x, float z, int seed) { return Value(x, z, seed) * 2f - 1f; }
    }
}
