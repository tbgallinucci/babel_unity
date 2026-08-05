// ============================================================================
//  WFCFiller.cs  —  camada WFC.Runtime  (Caminho A, implementação PRIMÁRIA)
//
//  Pega o grid anotado pelo esqueleto e resolve a geometria com o WFC,
//  respeitando as âncoras. É o ponto onde a Decisão 1 se fecha: jogabilidade
//  garantida por construção, estética resolvida pelo solver.
//
//  A tradução é toda por SOCKET DE FACE, não por tag:
//
//      borda Open  →  a face daquele lado tem que usar o socket "open"
//      borda Wall  →  ...............................  "wall"
//      borda Door  →  ...............................  "door"
//
//  A máscara da célula é a interseção das 4 direções. Com o kit greybox isso
//  quase sempre deixa uma variante só — e tudo bem: é o esqueleto mandando. Se
//  amanhã existirem 5 variantes de piso aberto, todas casam com 4 bordas Open e
//  o solver volta a ter escolha, sem tocar numa linha daqui.
//
//  Por cima disso vêm as regras de PAPEL do FloorSpec (ex.: "célula Escada só
//  aceita peça com tag Stairs"), que restringem ainda mais.
// ============================================================================

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using WFC.Core;
using WFC.Data;

namespace WFC.Runtime
{
    public sealed class WFCFiller : IFloorFiller
    {
        private readonly TileSet _tileSet;
        private readonly FloorSpec _spec;
        private readonly Transform _parent;

        /// <summary>Desligue para só rodar o solver (útil para medir ou para ver só os gizmos).</summary>
        public bool InstantiateGeometry { get; set; } = true;

        public CancellationToken Cancellation { get; set; } = CancellationToken.None;

        /// <summary>Avisos do último Fill — regras de papel ignoradas, etc.</summary>
        public List<string> Warnings { get; } = new List<string>();

        public WFCFiller(TileSet tileSet, FloorSpec spec, Transform parent)
        {
            _tileSet = tileSet;
            _spec = spec;
            _parent = parent;
        }

        public FloorFillResult Fill(AnnotatedGrid grid, IRandom rng)
        {
            Warnings.Clear();
            var result = new FloorFillResult();

            if (_tileSet == null) { result.Message = "TileSet nulo."; return result; }
            if (!_tileSet.IsBaked)
            {
                result.Message = $"TileSet '{_tileSet.name}' não está bakeado — rode \"Rebuild adjacency\".";
                return result;
            }

            int variantCount = _tileSet.VariantCount;
            int wordCount = Bitset.WordCount(variantCount);
            Grid3D g = grid.Grid;

            var socketMasks = new Dictionary<(Direction, string), ulong[]>();

            // ---- 0. Reconciliar as portas com o kit que existe DE FATO -------
            int rebaixadas = ReconcileDoors(grid, _tileSet, socketMasks);
            if (rebaixadas > 0)
                Warnings.Add($"{rebaixadas} porta(s) viraram vão simples: o tileset não tem peça " +
                             "para aquele formato de célula.");

            // ---- 1. Traduzir rótulos de borda em restrições -----------------
            var constraints = new List<Constraint>(g.CellCount);

            for (int cell = 0; cell < g.CellCount; cell++)
            {
                if (Cancellation.IsCancellationRequested)
                {
                    result.Message = "Cancelado.";
                    return result;
                }

                ulong[] mask = ConstraintBuilder.FullMask(variantCount);

                foreach (Direction dir in Directions.Horizontal)
                {
                    string socket = BorderLabels.ToSocket(grid.GetBorder(cell, dir));
                    Bitset.AndInPlace(mask, 0, GetSocketMask(socketMasks, dir, socket), 0, wordCount);
                }

                if (Bitset.IsEmpty(mask, 0, wordCount))
                {
                    // O kit não tem peça para este formato de célula. É lacuna de TILESET,
                    // não bug do solver — por isso o erro nomeia o perfil exato e quantas
                    // variantes o asset tem (se o número estiver desatualizado em relação
                    // ao que você espera, o bake é velho).
                    result.Message = $"Nenhuma peça serve para a célula {cell} " +
                                     $"({DescribeBorders(grid, cell)}). " +
                                     $"O TileSet '{_tileSet.name}' tem {variantCount} variantes — " +
                                     "se esse número parecer defasado, rode o WFC ▸ Tileset Bootstrap " +
                                     "(e antes o Greybox Tile Generator, se faltar prefab).";
                    return result;
                }

                // ---- Regras de papel por cima -------------------------------
                ulong[] roleMask = _spec != null ? _spec.MaskForRole(grid.GetRole(cell)) : null;
                if (roleMask != null)
                {
                    var combined = new ulong[wordCount];
                    Bitset.CopyTo(mask, 0, combined, 0, wordCount);
                    Bitset.AndInPlace(combined, 0, roleMask, 0, wordCount);

                    if (Bitset.IsEmpty(combined, 0, wordCount))
                    {
                        // A regra de papel briga com o formato da célula. As bordas mandam:
                        // elas é que garantem conectividade. A regra vira aviso, não crash.
                        Warnings.Add($"Célula {cell}: a regra do papel {grid.GetRole(cell)} não casa com " +
                                     $"o formato ({DescribeBorders(grid, cell)}) — regra ignorada nesta célula.");
                    }
                    else
                    {
                        mask = combined;
                    }
                }

                constraints.Add(new Constraint(cell, mask));
            }

            // ---- 2. Resolver -------------------------------------------------
            AdjacencyTable adjacency = _tileSet.CreateAdjacencyTable();
            var solver = new WFCSolver(g, adjacency, _tileSet.CreateWeights());

            var sw = Stopwatch.StartNew();
            solver.SetConstraints(constraints);

            int seed = rng.NextInt(int.MaxValue);
            SolverSettings settings = _spec != null ? _spec.CreateSolverSettings() : SolverSettings.Default;
            SolveResult solve = solver.Solve(seed, settings, Cancellation);
            sw.Stop();

            result.Seed = seed;
            result.Attempts = solve.Attempts;
            result.SolveMilliseconds = sw.Elapsed.TotalMilliseconds;

            if (!solve.Succeeded)
            {
                result.Message = $"{solve.Status}: {solve.Message}" +
                                 (solve.ContradictionCell >= 0
                                     ? $" (célula {solve.ContradictionCell}, {DescribeBorders(grid, solve.ContradictionCell)})"
                                     : "");
                return result;
            }

            result.Variants = solve.Modules;

            // ---- 3. Instanciar ------------------------------------------------
            if (InstantiateGeometry && _parent != null)
            {
                var sw2 = Stopwatch.StartNew();
                result.Root = TileInstancer.Build(_parent, _tileSet, grid, result.Variants, result.Anchors);
                sw2.Stop();
                result.InstantiateMilliseconds = sw2.Elapsed.TotalMilliseconds;
            }

            result.Success = true;
            if (Warnings.Count > 0) result.Message = $"{Warnings.Count} aviso(s) de regra de papel.";
            return result;
        }

        // ================================================================= portas
        /// <summary>
        /// Atalho de <see cref="DoorReconciler.Reconcile"/> para um TileSet: apara as
        /// portas que o kit não sabe representar. Ver o comentário de cabeçalho do
        /// DoorReconciler para o porquê disto não morar no esqueleto.
        /// </summary>
        public static int ReconcileDoors(AnnotatedGrid grid, TileSet tileSet,
                                         Dictionary<(Direction, string), ulong[]> cache = null)
        {
            if (tileSet == null || !tileSet.IsBaked) return 0;

            cache = cache ?? new Dictionary<(Direction, string), ulong[]>();
            return DoorReconciler.Reconcile(grid, tileSet.VariantCount,
                                            (dir, socket) => MaskFor(tileSet, cache, dir, socket));
        }

        private static ulong[] MaskFor(TileSet tileSet, Dictionary<(Direction, string), ulong[]> cache,
                                       Direction dir, string socket)
        {
            var key = (dir, socket);
            if (!cache.TryGetValue(key, out ulong[] mask))
            {
                mask = tileSet.MaskWithFaceSocket(dir, socket);
                cache[key] = mask;
            }
            return mask;
        }

        // ---------------------------------------------------------------- ajuda
        private ulong[] GetSocketMask(Dictionary<(Direction, string), ulong[]> cache, Direction dir, string socket)
            => MaskFor(_tileSet, cache, dir, socket);

        /// <summary>Descreve o perfil de bordas de uma célula — é o que torna o erro acionável.</summary>
        private static string DescribeBorders(AnnotatedGrid grid, int cell)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("papel ").Append(grid.GetRole(cell)).Append(", bordas ");
            foreach (Direction dir in Directions.Horizontal)
                sb.Append(dir).Append('=').Append(grid.GetBorder(cell, dir)).Append(' ');
            return sb.ToString().TrimEnd();
        }
    }
}
