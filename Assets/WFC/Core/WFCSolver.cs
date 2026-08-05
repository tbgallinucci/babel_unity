// ============================================================================
//  WFCSolver.cs  —  camada WFC.Core (C# puro, roda em thread de background)
//
//  O laço clássico do Wave Function Collapse:
//
//      observe  → escolhe a célula de MENOR ENTROPIA ainda ambígua
//      collapse → sorteia um módulo dela (sorteio ponderado pelos pesos)
//      propagate→ derruba, em cascata, os módulos que ficaram impossíveis nos
//                 vizinhos (consistência de arco), até a fila esvaziar
//
//  Detalhes de implementação que importam:
//
//  • Domínios são bitsets num único ulong[] plano — propagar é OR/AND de
//    palavras de 64 bits, sem alocação nenhuma dentro do laço.
//  • "Entropia" é a de Shannon ponderada pelos pesos, não a contagem crua de
//    módulos. Com contagem crua o resultado fica visivelmente mais pobre.
//  • Contradição NÃO é exceção: é status. Em runtime ela acontece, e a resposta
//    é retry com seed derivada (Decisão 3), depois fallback do chamador.
//  • Todos os buffers são alocados UMA vez no construtor e reaproveitados entre
//    tentativas e entre andares. Nada de GC na transição de nível.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Threading;

namespace WFC.Core
{
    public sealed class WFCSolver
    {
        private readonly Grid3D _grid;
        private readonly AdjacencyTable _adjacency;
        private readonly int _moduleCount;
        private readonly int _wordCount;
        private readonly int _cellCount;

        // Pesos por módulo (sempre > 0) e o termo w*log(w), pré-calculado.
        private readonly double[] _weights;
        private readonly double[] _weightLogWeights;

        // ---- Estado da tentativa em curso -----------------------------------
        private readonly ulong[] _domains;              // _cellCount * _wordCount
        private readonly double[] _sumWeights;          // por célula
        private readonly double[] _sumWeightLogWeights; // por célula
        private readonly int[] _remaining;              // popcount por célula
        private readonly double[] _noise;               // desempate de entropia

        // ---- Snapshot pós-restrições (recalculado só quando as constraints mudam) ----
        private readonly ulong[] _baseDomains;
        private readonly double[] _baseSumWeights;
        private readonly double[] _baseSumWeightLogWeights;
        private readonly int[] _baseRemaining;
        private bool _baseReady;
        private bool _baseContradiction;
        private int _baseContradictionCell = -1;

        // ---- Pilha de propagação --------------------------------------------
        private readonly int[] _stack;
        private readonly bool[] _inStack;
        private int _stackCount;

        private readonly ulong[] _allowedScratch;       // _wordCount

        public Grid3D Grid => _grid;
        public AdjacencyTable Adjacency => _adjacency;
        public int ModuleCount => _moduleCount;

        /// <param name="weights">Peso por módulo. Todos precisam ser &gt; 0 (peso 0 = tire o módulo do tileset).</param>
        public WFCSolver(Grid3D grid, AdjacencyTable adjacency, double[] weights)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _adjacency = adjacency ?? throw new ArgumentNullException(nameof(adjacency));
            if (weights == null) throw new ArgumentNullException(nameof(weights));
            if (weights.Length != adjacency.ModuleCount)
                throw new ArgumentException(
                    $"weights tem {weights.Length} entradas, mas a tabela tem {adjacency.ModuleCount} módulos.",
                    nameof(weights));

            _moduleCount = adjacency.ModuleCount;
            _wordCount = adjacency.WordCount;
            _cellCount = grid.CellCount;

            _weights = new double[_moduleCount];
            _weightLogWeights = new double[_moduleCount];
            for (int m = 0; m < _moduleCount; m++)
            {
                if (weights[m] <= 0.0)
                    throw new ArgumentException($"Peso do módulo {m} é {weights[m]}; precisa ser > 0.", nameof(weights));
                _weights[m] = weights[m];
                _weightLogWeights[m] = weights[m] * Math.Log(weights[m]);
            }

            int total = _cellCount * _wordCount;
            _domains = new ulong[total];
            _baseDomains = new ulong[total];

            _sumWeights = new double[_cellCount];
            _sumWeightLogWeights = new double[_cellCount];
            _remaining = new int[_cellCount];
            _noise = new double[_cellCount];

            _baseSumWeights = new double[_cellCount];
            _baseSumWeightLogWeights = new double[_cellCount];
            _baseRemaining = new int[_cellCount];

            _stack = new int[_cellCount];
            _inStack = new bool[_cellCount];
            _allowedScratch = new ulong[_wordCount];
        }

        // =====================================================================
        //  Restrições
        // =====================================================================

        /// <summary>
        /// Define as pré-restrições (Fase 2: as âncoras do esqueleto) e já roda uma
        /// passada de consistência de arco sobre o grid inteiro. O resultado vira o
        /// snapshot de partida de TODAS as tentativas — não se paga esse custo de novo
        /// a cada retry.
        /// Passe <c>null</c> para "sem restrições".
        /// </summary>
        public void SetConstraints(IReadOnlyList<Constraint> constraints)
        {
            // 1) Domínio cheio em todo mundo.
            for (int c = 0; c < _cellCount; c++)
                Bitset.FillOnes(_baseDomains, c * _wordCount, _wordCount, _moduleCount);

            // 2) AND das máscaras das restrições.
            _baseContradiction = false;
            _baseContradictionCell = -1;

            if (constraints != null)
            {
                for (int i = 0; i < constraints.Count; i++)
                {
                    Constraint k = constraints[i];
                    if (k.CellIndex >= _cellCount)
                        throw new ArgumentOutOfRangeException(
                            nameof(constraints), $"Constraint aponta para a célula {k.CellIndex}, fora do grid ({_cellCount}).");
                    if (k.AllowedMask.Length < _wordCount)
                        throw new ArgumentException(
                            $"Máscara da constraint da célula {k.CellIndex} tem {k.AllowedMask.Length} palavras; esperado {_wordCount}.");

                    Bitset.AndInPlace(_baseDomains, k.CellIndex * _wordCount, k.AllowedMask, 0, _wordCount);

                    if (Bitset.IsEmpty(_baseDomains, k.CellIndex * _wordCount, _wordCount))
                    {
                        _baseContradiction = true;
                        _baseContradictionCell = k.CellIndex;
                    }
                }
            }

            // 3) Consistência de arco global uma única vez.
            if (!_baseContradiction)
            {
                Array.Copy(_baseDomains, _domains, _baseDomains.Length);
                RecomputeAllCaches();

                _stackCount = 0;
                Array.Clear(_inStack, 0, _inStack.Length);
                for (int c = 0; c < _cellCount; c++) Push(c);

                if (!Propagate(CancellationToken.None, out int badCell))
                {
                    _baseContradiction = true;
                    _baseContradictionCell = badCell;
                }
                else
                {
                    Array.Copy(_domains, _baseDomains, _domains.Length);
                    Array.Copy(_sumWeights, _baseSumWeights, _cellCount);
                    Array.Copy(_sumWeightLogWeights, _baseSumWeightLogWeights, _cellCount);
                    Array.Copy(_remaining, _baseRemaining, _cellCount);
                }
            }

            _baseReady = true;
        }

        // =====================================================================
        //  Resolução
        // =====================================================================

        /// <summary>
        /// Resolve o grid. Mesma seed + mesmas restrições + mesmo tileset =&gt; mesmo
        /// resultado, sempre (é isso que permite pré-gerar andares e reproduzir bugs).
        /// </summary>
        public SolveResult Solve(int seed, SolverSettings settings, CancellationToken cancellation = default)
        {
            if (!_baseReady) SetConstraints(null);

            var result = new SolveResult { Seed = seed };

            if (_baseContradiction)
            {
                result.Status = SolveStatus.InvalidConstraints;
                result.ContradictionCell = _baseContradictionCell;
                result.Attempts = 0;
                result.Message = "As pré-restrições já são incompatíveis entre si — retry não resolve. " +
                                 "Revise as âncoras do esqueleto ou os sockets do tileset.";
                return result;
            }

            int maxAttempts = Math.Max(1, settings.MaxAttempts);
            int maxIterations = settings.MaxIterationsPerAttempt > 0
                ? settings.MaxIterationsPerAttempt
                : _cellCount;

            _noiseScale = settings.EntropyNoise > 0.0 ? settings.EntropyNoise : 1e-4;

            int lastBadCell = -1;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (cancellation.IsCancellationRequested)
                {
                    result.Status = SolveStatus.Cancelled;
                    result.Attempts = attempt;
                    return result;
                }

                // Seed derivada: as tentativas precisam ser bem diferentes entre si, mas
                // a sequência inteira ainda precisa ser função só da seed original.
                var rng = new XorShiftRandom(DeriveSeed(seed, attempt));

                ResetToBase(rng);

                SolveStatus status = RunAttempt(rng, maxIterations, cancellation, out int badCell, out int iterations);
                lastBadCell = badCell;

                if (status == SolveStatus.Success)
                {
                    result.Status = SolveStatus.Success;
                    result.Attempts = attempt + 1;
                    result.Seed = seed;
                    result.Modules = ExtractModules();
                    return result;
                }

                if (status == SolveStatus.Cancelled)
                {
                    result.Status = SolveStatus.Cancelled;
                    result.Attempts = attempt + 1;
                    return result;
                }

                if (status == SolveStatus.IterationLimit)
                {
                    result.Status = SolveStatus.IterationLimit;
                    result.Attempts = attempt + 1;
                    result.Message = $"Estourou {iterations} colapsos sem terminar — provável bug no tileset.";
                    return result;
                }
            }

            result.Status = SolveStatus.Contradiction;
            result.Attempts = maxAttempts;
            result.ContradictionCell = lastBadCell;
            result.Message = $"Contradição em {maxAttempts} tentativas. " +
                             "Afrouxe os sockets, aumente o peso dos coringas (SolidFill/Air) ou caia no layout de fallback.";
            return result;
        }

        /// <summary>Uma tentativa completa: observe → collapse → propagate até acabar.</summary>
        private SolveStatus RunAttempt(IRandom rng, int maxIterations, CancellationToken ct,
                                       out int contradictionCell, out int iterations)
        {
            contradictionCell = -1;
            iterations = 0;

            while (true)
            {
                if (ct.IsCancellationRequested) return SolveStatus.Cancelled;

                int cell = Observe();
                if (cell < 0) return SolveStatus.Success; // nada mais ambíguo

                if (++iterations > maxIterations) return SolveStatus.IterationLimit;

                Collapse(cell, rng);

                if (!Propagate(ct, out contradictionCell))
                    return SolveStatus.Contradiction;
            }
        }

        // ---------------------------------------------------------------- observe
        /// <summary>
        /// Célula ambígua de menor entropia, ou -1 se todas já colapsaram.
        /// Entropia de Shannon ponderada: H = ln(ΣW) − (ΣW·lnW)/ΣW.
        /// </summary>
        private int Observe()
        {
            double best = double.PositiveInfinity;
            int bestCell = -1;

            for (int c = 0; c < _cellCount; c++)
            {
                if (_remaining[c] <= 1) continue; // já colapsada (ou contraditória, mas aí já teríamos parado)

                double sw = _sumWeights[c];
                double entropy = Math.Log(sw) - (_sumWeightLogWeights[c] / sw) + _noise[c];

                if (entropy < best)
                {
                    best = entropy;
                    bestCell = c;
                }
            }

            return bestCell;
        }

        // --------------------------------------------------------------- collapse
        /// <summary>Sorteio ponderado de um módulo do domínio da célula; o domínio vira aquele único bit.</summary>
        private void Collapse(int cell, IRandom rng)
        {
            int offset = cell * _wordCount;

            double roll = rng.NextDouble() * _sumWeights[cell];
            int chosen = -1;

            for (int m = Bitset.NextSetBit(_domains, offset, _wordCount, 0);
                 m >= 0;
                 m = Bitset.NextSetBit(_domains, offset, _wordCount, m + 1))
            {
                roll -= _weights[m];
                if (roll <= 0.0) { chosen = m; break; }
                chosen = m; // guarda o último válido: protege contra erro de arredondamento no fim da soma
            }

            Bitset.ClearAll(_domains, offset, _wordCount);
            Bitset.Set(_domains, offset, chosen);

            _remaining[cell] = 1;
            _sumWeights[cell] = _weights[chosen];
            _sumWeightLogWeights[cell] = _weightLogWeights[chosen];
            _noise[cell] = 0.0;

            Push(cell);
        }

        // -------------------------------------------------------------- propagate
        /// <summary>
        /// Consistência de arco. Para cada célula na fila e cada direção, o vizinho só
        /// pode conter módulos aceitos por ALGUM módulo ainda possível na célula:
        ///     permitido = ⋃ (m ∈ domínio(c)) adjacência[m][dir]
        ///     domínio(vizinho) &amp;= permitido
        /// Se o domínio do vizinho encolher, ele entra na fila. Se zerar, contradição.
        /// </summary>
        /// <returns><c>false</c> em caso de contradição.</returns>
        private bool Propagate(CancellationToken ct, out int contradictionCell)
        {
            contradictionCell = -1;
            ulong[] adjData = _adjacency.Data;

            while (_stackCount > 0)
            {
                if (ct.IsCancellationRequested) { contradictionCell = -1; return true; }

                int cell = Pop();
                int cellOffset = cell * _wordCount;

                foreach (Direction dir in Directions.All)
                {
                    if (!_grid.TryGetNeighbor(cell, dir, out int neighbor)) continue;

                    int neighborOffset = neighbor * _wordCount;

                    // União das adjacências permitidas pelos módulos que ainda restam.
                    // Atalho quando a célula já colapsou: é uma linha só da tabela.
                    if (_remaining[cell] == 1)
                    {
                        int only = Bitset.FirstSetBit(_domains, cellOffset, _wordCount);
                        Bitset.CopyTo(adjData, _adjacency.OffsetOf(only, dir), _allowedScratch, 0, _wordCount);
                    }
                    else
                    {
                        Bitset.ClearAll(_allowedScratch, 0, _wordCount);
                        for (int m = Bitset.NextSetBit(_domains, cellOffset, _wordCount, 0);
                             m >= 0;
                             m = Bitset.NextSetBit(_domains, cellOffset, _wordCount, m + 1))
                        {
                            Bitset.Or(_allowedScratch, 0, adjData, _adjacency.OffsetOf(m, dir), _wordCount);
                        }
                    }

                    if (!Bitset.AndInPlace(_domains, neighborOffset, _allowedScratch, 0, _wordCount))
                        continue; // vizinho não mudou: nada a propagar

                    RecomputeCache(neighbor);

                    if (_remaining[neighbor] == 0)
                    {
                        contradictionCell = neighbor;
                        return false;
                    }

                    Push(neighbor);
                }
            }

            return true;
        }

        // =====================================================================
        //  Estado / buffers
        // =====================================================================

        private void ResetToBase(IRandom rng)
        {
            Array.Copy(_baseDomains, _domains, _baseDomains.Length);
            Array.Copy(_baseSumWeights, _sumWeights, _cellCount);
            Array.Copy(_baseSumWeightLogWeights, _sumWeightLogWeights, _cellCount);
            Array.Copy(_baseRemaining, _remaining, _cellCount);

            _stackCount = 0;
            Array.Clear(_inStack, 0, _inStack.Length);

            // Ruído novo a cada tentativa: é o que faz o retry explorar outro caminho.
            for (int c = 0; c < _cellCount; c++)
                _noise[c] = rng.NextDouble() * _noiseScale;
        }

        private double _noiseScale = 1e-4;

        /// <summary>Escala do ruído de desempate (vem de <see cref="SolverSettings.EntropyNoise"/>).</summary>
        public void SetEntropyNoise(double scale) => _noiseScale = scale;

        private void RecomputeAllCaches()
        {
            for (int c = 0; c < _cellCount; c++) RecomputeCache(c);
        }

        /// <summary>
        /// Recalcula popcount e as somas de peso de uma célula.
        /// É O(módulos) e é o ponto óbvio de otimização se algum dia o tileset passar
        /// de algumas centenas de módulos (aí vale manter contadores de suporte
        /// incrementais em vez de recalcular).
        /// </summary>
        private void RecomputeCache(int cell)
        {
            int offset = cell * _wordCount;
            int count = 0;
            double sw = 0.0;
            double swlw = 0.0;

            for (int m = Bitset.NextSetBit(_domains, offset, _wordCount, 0);
                 m >= 0;
                 m = Bitset.NextSetBit(_domains, offset, _wordCount, m + 1))
            {
                count++;
                sw += _weights[m];
                swlw += _weightLogWeights[m];
            }

            _remaining[cell] = count;
            _sumWeights[cell] = sw;
            _sumWeightLogWeights[cell] = swlw;
        }

        private void Push(int cell)
        {
            if (_inStack[cell]) return;
            _inStack[cell] = true;
            _stack[_stackCount++] = cell;
        }

        private int Pop()
        {
            int cell = _stack[--_stackCount];
            _inStack[cell] = false;
            return cell;
        }

        private int[] ExtractModules()
        {
            var modules = new int[_cellCount];
            for (int c = 0; c < _cellCount; c++)
                modules[c] = Bitset.FirstSetBit(_domains, c * _wordCount, _wordCount);
            return modules;
        }

        /// <summary>Vista de leitura do domínio de uma célula — para testes e gizmos de debug.</summary>
        public Cell GetCell(int index) => new Cell(_domains, index * _wordCount, _wordCount);

        /// <summary>
        /// Seed da tentativa nº <paramref name="attempt"/>. Determinística e estável entre
        /// plataformas (só aritmética inteira de 64 bits, sem GetHashCode do runtime).
        /// </summary>
        public static int DeriveSeed(int seed, int attempt)
        {
            unchecked
            {
                ulong x = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL
                        + (ulong)(uint)attempt * 0xBF58476D1CE4E5B9UL;
                x ^= x >> 33;
                x *= 0xFF51AFD7ED558CCDUL;
                x ^= x >> 33;
                return (int)(uint)(x ^ (x >> 32));
            }
        }
    }
}
