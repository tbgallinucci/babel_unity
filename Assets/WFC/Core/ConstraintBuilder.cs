// ============================================================================
//  ConstraintBuilder.cs  —  camada WFC.Core
//
//  Açúcar para montar listas de Constraint sem escrever bitset na mão.
//  Continua sem saber nada de sockets ou prefabs: quem traduz "face de parede"
//  para uma máscara de módulos é a camada Data.
// ============================================================================

using System;
using System.Collections.Generic;

namespace WFC.Core
{
    public static class ConstraintBuilder
    {
        /// <summary>
        /// Sela as bordas do grid: toda célula encostada numa face externa fica
        /// restrita aos módulos cuja face virada para fora é "fechada".
        ///
        /// Sem isso, um andar termina com pisos abertos apontando para o nada na
        /// borda do grid. Uma célula de canto recebe o AND das máscaras das duas
        /// (ou três) faces externas dela.
        /// </summary>
        /// <param name="maskForOutwardFace">
        /// Dada a direção que aponta para FORA do grid, devolve a máscara de módulos
        /// aceitáveis. A máscara é lida, nunca modificada.
        /// </param>
        /// <param name="includeVertical">
        /// Se as faces PosY/NegY também devem ser seladas. Num grid de um andar só
        /// (SizeY == 1) normalmente é <c>false</c>: teto e chão não são problema do WFC.
        /// </param>
        public static List<Constraint> SealBorders(
            Grid3D grid,
            int moduleCount,
            Func<Direction, ulong[]> maskForOutwardFace,
            bool includeVertical = false)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (maskForOutwardFace == null) throw new ArgumentNullException(nameof(maskForOutwardFace));

            int wordCount = Bitset.WordCount(moduleCount);
            var result = new List<Constraint>();

            for (int cell = 0; cell < grid.CellCount; cell++)
            {
                ulong[] combined = null;

                foreach (Direction dir in Directions.All)
                {
                    if (!includeVertical && Directions.IsVertical(dir)) continue;
                    if (!grid.IsBorder(cell, dir)) continue;

                    ulong[] mask = maskForOutwardFace(dir);
                    if (mask == null) continue;

                    if (combined == null)
                    {
                        combined = new ulong[wordCount];
                        Bitset.CopyTo(mask, 0, combined, 0, wordCount);
                    }
                    else
                    {
                        Bitset.AndInPlace(combined, 0, mask, 0, wordCount);
                    }
                }

                if (combined != null)
                    result.Add(new Constraint(cell, combined));
            }

            return result;
        }

        /// <summary>Máscara vazia (nada permitido) — ponto de partida para ir ligando bits.</summary>
        public static ulong[] EmptyMask(int moduleCount) => new ulong[Bitset.WordCount(moduleCount)];

        /// <summary>Máscara cheia (tudo permitido).</summary>
        public static ulong[] FullMask(int moduleCount)
        {
            int words = Bitset.WordCount(moduleCount);
            var mask = new ulong[words];
            Bitset.FillOnes(mask, 0, words, moduleCount);
            return mask;
        }

        /// <summary>Máscara com os módulos que satisfazem um predicado.</summary>
        public static ulong[] MaskWhere(int moduleCount, Func<int, bool> predicate)
        {
            var mask = EmptyMask(moduleCount);
            for (int m = 0; m < moduleCount; m++)
                if (predicate(m)) Bitset.Set(mask, 0, m);
            return mask;
        }
    }
}
