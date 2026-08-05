// ============================================================================
//  Cell.cs  —  camada WFC.Core
//
//  "Vista" leve sobre o domínio de UMA célula. O armazenamento de verdade é um
//  único ulong[] plano dentro do solver (célula i em [i*WordCount, ...)), então
//  Cell é só um struct de leitura por cima dele: zero alocação, API legível.
// ============================================================================

using System.Collections.Generic;

namespace WFC.Core
{
    /// <summary>
    /// Domínio de uma célula: o conjunto de módulos ainda possíveis, como bitset.
    /// Struct de leitura — não guarda estado próprio, só aponta para o array do solver.
    /// </summary>
    public readonly struct Cell
    {
        private readonly ulong[] _data;
        private readonly int _offset;
        private readonly int _wordCount;

        public Cell(ulong[] data, int offset, int wordCount)
        {
            _data = data;
            _offset = offset;
            _wordCount = wordCount;
        }

        /// <summary>Índice da célula não é guardado aqui — quem itera o grid sabe qual é.</summary>
        public int WordCount => _wordCount;

        /// <summary>Quantos módulos ainda cabem nesta célula.</summary>
        public int Count => Bitset.PopCount(_data, _offset, _wordCount);

        /// <summary>Domínio vazio = contradição.</summary>
        public bool IsContradiction => Bitset.IsEmpty(_data, _offset, _wordCount);

        /// <summary>Colapsada = sobrou exatamente um módulo.</summary>
        public bool IsCollapsed => Count == 1;

        public bool Contains(int module) => Bitset.Get(_data, _offset, module);

        /// <summary>O único módulo restante, ou -1 se ainda houver ambiguidade (ou contradição).</summary>
        public int CollapsedModule
        {
            get
            {
                int first = Bitset.FirstSetBit(_data, _offset, _wordCount);
                if (first < 0) return -1;
                return Bitset.NextSetBit(_data, _offset, _wordCount, first + 1) < 0 ? first : -1;
            }
        }

        /// <summary>Itera os módulos possíveis sem alocar (usa NextSetBit por baixo).</summary>
        public IEnumerable<int> Modules()
        {
            for (int m = Bitset.NextSetBit(_data, _offset, _wordCount, 0);
                 m >= 0;
                 m = Bitset.NextSetBit(_data, _offset, _wordCount, m + 1))
            {
                yield return m;
            }
        }
    }
}
