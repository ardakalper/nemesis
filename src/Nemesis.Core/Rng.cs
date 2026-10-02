using System;
using System.Collections.Generic;

namespace Nemesis
{
    /// <summary>Deterministic randomness (xorshift128+ seeded with splitmix64). A series is fully
    /// reproducible from its seed and the player's choices, which is what the tests rely on.</summary>
    public sealed class Rng
    {
        public ulong S0, S1;

        public Rng(ulong seed)
        {
            ulong z = seed;
            S0 = SplitMix(ref z);
            S1 = SplitMix(ref z);
            if (S0 == 0 && S1 == 0) S1 = 1;
        }

        public static Rng FromString(string seed) => new Rng(Hash(seed));

        public static ulong Hash(string s)
        {
            ulong h = 1469598103934665603UL; // FNV-1a
            foreach (char c in s) { h ^= c; h *= 1099511628211UL; }
            return h;
        }

        static ulong SplitMix(ref ulong z)
        {
            z += 0x9E3779B97F4A7C15UL;
            ulong r = z;
            r = (r ^ (r >> 30)) * 0xBF58476D1CE4E5B9UL;
            r = (r ^ (r >> 27)) * 0x94D049BB133111EBUL;
            return r ^ (r >> 31);
        }

        public ulong NextU64()
        {
            ulong s1 = S0, s0 = S1;
            S0 = s0;
            s1 ^= s1 << 23;
            S1 = s1 ^ s0 ^ (s1 >> 17) ^ (s0 >> 26);
            return S1 + s0;
        }

        /// <summary>Uniform in [0, 1).</summary>
        public double NextDouble() => (NextU64() >> 11) * (1.0 / 9007199254740992.0);
        /// <summary>Uniform integer in [min, max] inclusive.</summary>
        public int Range(int min, int max) => min + (int)(NextDouble() * (max - min + 1));
        public bool Chance(double p) => NextDouble() < p;
        public T Pick<T>(IReadOnlyList<T> list) => list[Range(0, list.Count - 1)];
        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Range(0, i);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
        /// <summary>A child generator, so one part of the game can't change another part's rolls.</summary>
        public Rng Fork(string label) => new Rng(NextU64() ^ Hash(label));
    }
}
