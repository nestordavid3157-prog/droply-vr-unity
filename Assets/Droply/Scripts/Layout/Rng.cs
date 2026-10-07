using System;

namespace Droply.Landscape
{
    /// <summary>
    /// Deterministic random numbers (SplitMix64). The same sequence in the Unity player, in the editor and in
    /// Tools/CompositionCheck, independent of System.Random and of the runtime's class library.
    /// </summary>
    public sealed class Rng
    {
        ulong state;

        public Rng(int seed)
        {
            unchecked { state = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL + 0x2545F4914F6CDD1DUL; }
        }

        ulong Next()
        {
            unchecked
            {
                ulong z = state += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>Uniform in [0, 1).</summary>
        public float Value() { return (Next() >> 40) / 16777216f; }

        public float Range(float min, float max) { return min + Value() * (max - min); }

        /// <summary>Uniform in [-1, 1).</summary>
        public float Signed() { return Value() * 2f - 1f; }

        public int Int(int maxExclusive) { return Math.Min(maxExclusive - 1, (int)(Value() * maxExclusive)); }

        /// <summary>Independent stream derived from this one, so adding objects to one group does not shift every other group.</summary>
        public Rng Fork(int salt)
        {
            unchecked { return new Rng((int)(Next() >> 33) ^ (salt * 7919)); }
        }
    }
}
