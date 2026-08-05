// ============================================================================
//  Bitset.cs  —  camada WFC.Core
//
//  Operações sobre domínios representados como bitset (ulong[]): 1 bit por
//  módulo. Propagar vira AND de palavras de 64 bits em vez de mexer em listas —
//  ordens de magnitude mais rápido e sem alocação no hot path.
//
//  Convenção: todos os métodos recebem (array, offset) porque na prática os
//  domínios de TODAS as células vivem num único array plano
//  (célula i ocupa [i*WordCount, i*WordCount + WordCount)).
// ============================================================================

using System;

namespace WFC.Core
{
    public static class Bitset
    {
        public const int BitsPerWord = 64;

        /// <summary>Quantas palavras de 64 bits são necessárias para <paramref name="bitCount"/> bits.</summary>
        public static int WordCount(int bitCount)
        {
            if (bitCount < 0) throw new ArgumentOutOfRangeException(nameof(bitCount));
            return (bitCount + BitsPerWord - 1) / BitsPerWord;
        }

        public static bool Get(ulong[] data, int offset, int bit)
            => (data[offset + (bit >> 6)] & (1UL << (bit & 63))) != 0UL;

        public static void Set(ulong[] data, int offset, int bit)
            => data[offset + (bit >> 6)] |= 1UL << (bit & 63);

        public static void Clear(ulong[] data, int offset, int bit)
            => data[offset + (bit >> 6)] &= ~(1UL << (bit & 63));

        /// <summary>Zera todas as palavras do bitset.</summary>
        public static void ClearAll(ulong[] data, int offset, int wordCount)
        {
            for (int w = 0; w < wordCount; w++) data[offset + w] = 0UL;
        }

        /// <summary>
        /// Liga os primeiros <paramref name="bitCount"/> bits (domínio "tudo permitido").
        /// Os bits sobrando na última palavra ficam em zero — importante, senão o
        /// PopCount conta módulos que não existem.
        /// </summary>
        public static void FillOnes(ulong[] data, int offset, int wordCount, int bitCount)
        {
            for (int w = 0; w < wordCount; w++) data[offset + w] = ulong.MaxValue;
            int tail = bitCount & 63;
            if (tail != 0)
                data[offset + wordCount - 1] = (1UL << tail) - 1UL;
        }

        public static void CopyTo(ulong[] src, int srcOffset, ulong[] dst, int dstOffset, int wordCount)
        {
            Array.Copy(src, srcOffset, dst, dstOffset, wordCount);
        }

        /// <summary>dst |= src</summary>
        public static void Or(ulong[] dst, int dstOffset, ulong[] src, int srcOffset, int wordCount)
        {
            for (int w = 0; w < wordCount; w++) dst[dstOffset + w] |= src[srcOffset + w];
        }

        /// <summary>
        /// dst &amp;= src. Devolve <c>true</c> se alguma palavra mudou (ou seja, se o
        /// domínio encolheu) — é esse retorno que decide se propagamos adiante.
        /// </summary>
        public static bool AndInPlace(ulong[] dst, int dstOffset, ulong[] src, int srcOffset, int wordCount)
        {
            bool changed = false;
            for (int w = 0; w < wordCount; w++)
            {
                ulong before = dst[dstOffset + w];
                ulong after = before & src[srcOffset + w];
                if (after != before)
                {
                    dst[dstOffset + w] = after;
                    changed = true;
                }
            }
            return changed;
        }

        public static bool IsEmpty(ulong[] data, int offset, int wordCount)
        {
            for (int w = 0; w < wordCount; w++)
                if (data[offset + w] != 0UL) return false;
            return true;
        }

        /// <summary>Quantidade de bits ligados (quantos módulos ainda são possíveis).</summary>
        public static int PopCount(ulong[] data, int offset, int wordCount)
        {
            int total = 0;
            for (int w = 0; w < wordCount; w++) total += PopCount64(data[offset + w]);
            return total;
        }

        /// <summary>Índice do primeiro bit ligado, ou -1 se o bitset estiver vazio.</summary>
        public static int FirstSetBit(ulong[] data, int offset, int wordCount)
        {
            for (int w = 0; w < wordCount; w++)
            {
                ulong word = data[offset + w];
                if (word != 0UL) return (w << 6) + TrailingZeros(word);
            }
            return -1;
        }

        /// <summary>
        /// Próximo bit ligado a partir de <paramref name="fromBit"/> (inclusive), ou -1.
        /// Serve para iterar o domínio sem alocar: <c>for (int m = NextSetBit(...,0); m >= 0; m = NextSetBit(..., m+1))</c>
        /// </summary>
        public static int NextSetBit(ulong[] data, int offset, int wordCount, int fromBit)
        {
            if (fromBit < 0) fromBit = 0;
            int w = fromBit >> 6;
            if (w >= wordCount) return -1;

            // Mascara os bits antes de fromBit dentro da primeira palavra.
            ulong word = data[offset + w] & (ulong.MaxValue << (fromBit & 63));
            while (true)
            {
                if (word != 0UL) return (w << 6) + TrailingZeros(word);
                w++;
                if (w >= wordCount) return -1;
                word = data[offset + w];
            }
        }

        // ---- Intrínsecos "na mão" -------------------------------------------
        // System.Numerics.BitOperations só existe em .NET Core 3+/netstandard2.1;
        // implementamos aqui para o Core continuar compilando em qualquer perfil.

        public static int PopCount64(ulong v)
        {
            v -= (v >> 1) & 0x5555555555555555UL;
            v = (v & 0x3333333333333333UL) + ((v >> 2) & 0x3333333333333333UL);
            v = (v + (v >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
            return (int)((v * 0x0101010101010101UL) >> 56);
        }

        public static int TrailingZeros(ulong v)
        {
            if (v == 0UL) return 64;
            int n = 0;
            if ((v & 0xFFFFFFFFUL) == 0UL) { n += 32; v >>= 32; }
            if ((v & 0x0000FFFFUL) == 0UL) { n += 16; v >>= 16; }
            if ((v & 0x000000FFUL) == 0UL) { n += 8;  v >>= 8;  }
            if ((v & 0x0000000FUL) == 0UL) { n += 4;  v >>= 4;  }
            if ((v & 0x00000003UL) == 0UL) { n += 2;  v >>= 2;  }
            if ((v & 0x00000001UL) == 0UL) { n += 1; }
            return n;
        }
    }
}
