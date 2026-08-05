// ============================================================================
//  Constraint.cs  —  camada WFC.Core
//
//  Pré-restrição: "esta célula só pode receber estes módulos".
//  É por aqui que o esqueleto determinístico (Decisão 1) vai cravar as âncoras
//  na Fase 2 — papéis de sala e portas obrigatórias viram Constraints antes do
//  solver começar. O Core não sabe o que é uma "porta": só vê um bitset.
// ============================================================================

using System;

namespace WFC.Core
{
    public readonly struct Constraint
    {
        /// <summary>Índice linear da célula no <see cref="Grid3D"/>.</summary>
        public readonly int CellIndex;

        /// <summary>Máscara de módulos permitidos (é feito AND com o domínio da célula).</summary>
        public readonly ulong[] AllowedMask;

        public Constraint(int cellIndex, ulong[] allowedMask)
        {
            if (cellIndex < 0) throw new ArgumentOutOfRangeException(nameof(cellIndex));
            CellIndex = cellIndex;
            AllowedMask = allowedMask ?? throw new ArgumentNullException(nameof(allowedMask));
        }

        /// <summary>Fixa a célula num módulo específico.</summary>
        public static Constraint Fix(int cellIndex, int module, int moduleCount)
        {
            var mask = new ulong[Bitset.WordCount(moduleCount)];
            Bitset.Set(mask, 0, module);
            return new Constraint(cellIndex, mask);
        }

        /// <summary>Restringe a célula a um conjunto de módulos.</summary>
        public static Constraint Restrict(int cellIndex, int moduleCount, params int[] modules)
        {
            var mask = new ulong[Bitset.WordCount(moduleCount)];
            foreach (int m in modules) Bitset.Set(mask, 0, m);
            return new Constraint(cellIndex, mask);
        }

        /// <summary>
        /// Proíbe um conjunto de módulos (o complemento). Útil para "aqui não pode
        /// coringa" ou "aqui não pode escada".
        /// </summary>
        public static Constraint Forbid(int cellIndex, int moduleCount, params int[] modules)
        {
            int words = Bitset.WordCount(moduleCount);
            var mask = new ulong[words];
            Bitset.FillOnes(mask, 0, words, moduleCount);
            foreach (int m in modules) Bitset.Clear(mask, 0, m);
            return new Constraint(cellIndex, mask);
        }
    }
}
