// ============================================================================
//  IRandom.cs  —  camada WFC.Core
//
//  RNG injetável e seedável. O Core NUNCA usa UnityEngine.Random: reprodutibi-
//  lidade por seed é requisito (mesma seed => mesmo andar, sempre), e o solver
//  precisa rodar em thread de background.
// ============================================================================

using System;

namespace WFC.Core
{
    public interface IRandom
    {
        /// <summary>Inteiro em [0, maxExclusive).</summary>
        int NextInt(int maxExclusive);

        /// <summary>Double em [0, 1).</summary>
        double NextDouble();
    }

    /// <summary>
    /// xorshift128+ — rápido, sem alocação, determinístico e independente da
    /// implementação de System.Random (que já mudou de comportamento entre
    /// versões do .NET; não dá para confiar nela para reprodutibilidade).
    /// </summary>
    public sealed class XorShiftRandom : IRandom
    {
        private ulong _s0;
        private ulong _s1;

        public int Seed { get; }

        public XorShiftRandom(int seed)
        {
            Seed = seed;

            // SplitMix64 para espalhar a seed: seeds vizinhas (0, 1, 2...) precisam
            // gerar sequências completamente diferentes.
            ulong x = unchecked((ulong)seed + 0x9E3779B97F4A7C15UL);
            _s0 = SplitMix64(ref x);
            _s1 = SplitMix64(ref x);
            if (_s0 == 0UL && _s1 == 0UL) _s1 = 0x9E3779B97F4A7C15UL; // estado zero é absorvente
        }

        private static ulong SplitMix64(ref ulong x)
        {
            x += 0x9E3779B97F4A7C15UL;
            ulong z = x;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public ulong NextULong()
        {
            ulong s1 = _s0;
            ulong s0 = _s1;
            ulong result = s0 + s1;
            _s0 = s0;
            s1 ^= s1 << 23;
            _s1 = s1 ^ s0 ^ (s1 >> 18) ^ (s0 >> 5);
            return result;
        }

        public double NextDouble()
        {
            // 53 bits de mantissa — o padrão para gerar double uniforme em [0,1).
            return (NextULong() >> 11) * (1.0 / 9007199254740992.0);
        }

        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return (int)(NextDouble() * maxExclusive);
        }
    }
}
