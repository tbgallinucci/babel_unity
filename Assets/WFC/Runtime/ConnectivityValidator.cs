// ============================================================================
//  ConnectivityValidator.cs  —  camada WFC.Runtime  (Decisão 1 / Decisão 3)
//
//  Flood-fill sobre os RÓTULOS DE BORDA: passa-se por Open e Door, nunca por
//  Wall. É a defesa em profundidade — o esqueleto já garante conectividade por
//  construção (MST é conexa), então este validador nunca deveria acusar nada.
//  Se acusar, tem bug no esqueleto, e é melhor descobrir num teste do que num
//  andar sem saída na mão do jogador.
//
//  Roda sobre o grid ANOTADO, não sobre a geometria: não depende do WFC nem de
//  prefab nenhum, e por isso pode rodar antes de instanciar qualquer coisa.
// ============================================================================

using System.Collections.Generic;
using WFC.Core;

namespace WFC.Runtime
{
    public static class ConnectivityValidator
    {
        /// <summary>Existe caminho de <paramref name="from"/> até <paramref name="to"/>?</summary>
        public static bool IsReachable(AnnotatedGrid grid, int from, int to)
        {
            if (from < 0 || to < 0) return false;
            if (from == to) return true;

            bool[] visited = Flood(grid, from);
            return visited[to];
        }

        /// <summary>
        /// Toda célula jogável é alcançável a partir de <paramref name="from"/>?
        /// Ilha desconexa não quebra o andar (o jogador só não a vê), mas quase sempre
        /// significa que uma sala ficou órfã — vale saber.
        /// </summary>
        public static bool AllPlayableReachable(AnnotatedGrid grid, int from, out List<int> unreachable)
        {
            unreachable = new List<int>();
            if (from < 0) return false;

            bool[] visited = Flood(grid, from);

            for (int cell = 0; cell < grid.Grid.CellCount; cell++)
                if (grid.IsPlayable(cell) && !visited[cell]) unreachable.Add(cell);

            return unreachable.Count == 0;
        }

        /// <summary>Distância em células (BFS) a partir de uma origem; -1 onde não chega.</summary>
        public static int[] Distances(AnnotatedGrid grid, int from)
        {
            int count = grid.Grid.CellCount;
            var dist = new int[count];
            for (int i = 0; i < count; i++) dist[i] = -1;

            if (from < 0 || !grid.IsPlayable(from)) return dist;

            var queue = new Queue<int>();
            dist[from] = 0;
            queue.Enqueue(from);

            while (queue.Count > 0)
            {
                int cell = queue.Dequeue();
                foreach (Direction dir in Directions.Horizontal)
                {
                    if (grid.GetBorder(cell, dir) == BorderLabel.Wall) continue;
                    if (!grid.Grid.TryGetNeighbor(cell, dir, out int neighbor)) continue;
                    if (dist[neighbor] >= 0) continue;

                    dist[neighbor] = dist[cell] + 1;
                    queue.Enqueue(neighbor);
                }
            }

            return dist;
        }

        /// <summary>Quantas células jogáveis o flood-fill alcança.</summary>
        public static int ReachableCount(AnnotatedGrid grid, int from)
        {
            bool[] visited = Flood(grid, from);
            int n = 0;
            for (int i = 0; i < visited.Length; i++) if (visited[i]) n++;
            return n;
        }

        private static bool[] Flood(AnnotatedGrid grid, int from)
        {
            int count = grid.Grid.CellCount;
            var visited = new bool[count];
            if (from < 0 || from >= count || !grid.IsPlayable(from)) return visited;

            var stack = new Stack<int>();
            visited[from] = true;
            stack.Push(from);

            while (stack.Count > 0)
            {
                int cell = stack.Pop();
                foreach (Direction dir in Directions.Horizontal)
                {
                    // A borda é a única autoridade: Open e Door passam, Wall não.
                    if (grid.GetBorder(cell, dir) == BorderLabel.Wall) continue;
                    if (!grid.Grid.TryGetNeighbor(cell, dir, out int neighbor)) continue;
                    if (visited[neighbor]) continue;

                    visited[neighbor] = true;
                    stack.Push(neighbor);
                }
            }

            return visited;
        }
    }
}
