// ============================================================================
//  RegionGraph.cs  —  código do JOGO (Decisão 4: fora do plugin)
//
//  O grafo de espaços conectados do andar: quem é vizinho de quem, onde
//  "vizinho" quer dizer "dá pra ver/passar de um pro outro".
//
//  Existe como classe própria porque DOIS consumidores precisam exatamente da
//  mesma relação, e tê-la duplicada seria um jeito garantido de dessincronizar:
//
//    • RoomStreamer  — BFS a partir da região do jogador decide o que renderizar.
//    • RegionLightMask — regiões vizinhas não podem compartilhar rendering layer,
//                        senão a contenção de luz não contém nada.
//
//  A regra de adjacência é derivada do AnnotatedGrid, não armazenada: borda
//  entre regiões diferentes que NÃO é parede (ou seja, Open ou Door) conecta.
//  Mudou a geometria, o grafo muda junto.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using WFC.Core;
using WFC.Runtime;

namespace Babel.Floor
{
    public static class RegionGraph
    {
        /// <summary>
        /// Bit 0 (Default) fica FORA da coloração de propósito: é a rede de segurança.
        /// Qualquer coisa que ninguém carimbou — um prop novo, um inimigo instanciado
        /// depois, o próprio jogador antes da primeira avaliação — permanece no Default,
        /// e como toda tocha inclui o Default na sua máscara, continua recebendo luz em vez
        /// de aparecer preta. O pior caso vira "igual a antes", nunca "objeto invisível".
        /// </summary>
        public const int FirstColorBit = 1;

        /// <summary>
        /// Quantas cores existem. Adjacência de salas num andar é praticamente planar, e
        /// grafo planar precisa de no máximo 4 cores — 7 dá folga de sobra até para o nó
        /// dos corredores, que encosta em quase todas as salas.
        /// </summary>
        public const int ColorCount = 7;

        /// <summary>Máscara que toda tocha inclui, para não apagar o que não foi carimbado.</summary>
        public const uint DefaultMask = 1u;

        // =====================================================================
        //  Grafo
        // =====================================================================

        /// <summary>
        /// Regiões adjacentes por borda transitável. Simétrico: se A lista B, B lista A.
        /// Células fora de qualquer região (rocha/preenchimento) não entram.
        /// </summary>
        public static Dictionary<int, List<int>> Build(AnnotatedGrid grid)
        {
            var neighbors = new Dictionary<int, List<int>>();
            if (grid == null) return neighbors;

            Grid3D g = grid.Grid;

            for (int cell = 0; cell < g.CellCount; cell++)
            {
                int region = grid.GetRegion(cell);
                if (region < 0) continue;

                // Registra a região mesmo sem vizinho: uma sala isolada precisa existir no
                // grafo para receber cor, senão cairia no Default e não conteria luz nenhuma.
                if (!neighbors.ContainsKey(region)) neighbors[region] = new List<int>();

                foreach (Direction dir in Directions.Horizontal)
                {
                    if (!g.TryGetNeighbor(cell, dir, out int neighbor)) continue;

                    int other = grid.GetRegion(neighbor);
                    if (other < 0 || other == region) continue;
                    if (grid.GetBorder(cell, dir) == BorderLabel.Wall) continue;

                    Connect(neighbors, region, other);
                    Connect(neighbors, other, region);
                }
            }

            return neighbors;
        }

        private static void Connect(Dictionary<int, List<int>> neighbors, int from, int to)
        {
            if (!neighbors.TryGetValue(from, out List<int> list))
            {
                list = new List<int>();
                neighbors[from] = list;
            }
            if (!list.Contains(to)) list.Add(to);
        }

        // =====================================================================
        //  Coloração
        // =====================================================================

        /// <summary>
        /// Atribui a cada região um bit de rendering layer tal que regiões VIZINHAS nunca
        /// compartilhem bit. É isso que faz a contenção de luz funcionar: como duas regiões
        /// da mesma cor são, por construção, não-adjacentes, elas estão longe o bastante para
        /// o Range da tocha (~25 m) jamais alcançar a outra.
        ///
        /// Guloso, percorrendo as regiões em ordem de id — a ordem precisa ser determinística
        /// para o mesmo andar sair sempre com a mesma coloração (mesma seed, mesma luz).
        /// </summary>
        public static Dictionary<int, uint> AssignColors(Dictionary<int, List<int>> neighbors)
        {
            var colorOf = new Dictionary<int, int>();
            var result = new Dictionary<int, uint>();
            if (neighbors == null || neighbors.Count == 0) return result;

            var regions = new List<int>(neighbors.Keys);
            regions.Sort();

            var taken = new HashSet<int>();
            int collisions = 0;

            foreach (int region in regions)
            {
                taken.Clear();
                if (neighbors.TryGetValue(region, out List<int> adjacent))
                    foreach (int other in adjacent)
                        if (colorOf.TryGetValue(other, out int c)) taken.Add(c);

                int color = 0;
                while (color < ColorCount && taken.Contains(color)) color++;

                if (color >= ColorCount)
                {
                    // Mais vizinhos distintos que cores. Não é erro fatal: cair na cor 0 só
                    // significa que ESTA fronteira volta a vazar, como vazava antes.
                    color = 0;
                    collisions++;
                }

                colorOf[region] = color;
                result[region] = 1u << (FirstColorBit + color);
            }

            if (collisions > 0)
                Debug.LogWarning($"[RegionGraph] {collisions} região(ões) sem cor livre entre {ColorCount} — " +
                                 "a luz volta a atravessar a parede nessas fronteiras. " +
                                 "Suba RegionGraph.ColorCount se aparecer com frequência.");

            return result;
        }

        /// <summary>
        /// Máscara de uma TOCHA: a própria região, mais as regiões ligadas a ela por passagem
        /// (a luz atravessando uma porta aberta é correta — o que não pode é atravessar parede),
        /// mais o Default como rede de segurança para o que ninguém carimbou.
        /// </summary>
        public static uint LightMask(int region, Dictionary<int, List<int>> neighbors, Dictionary<int, uint> colors)
        {
            uint mask = DefaultMask;
            if (colors == null || !colors.TryGetValue(region, out uint own)) return mask;

            mask |= own;

            if (neighbors != null && neighbors.TryGetValue(region, out List<int> adjacent))
                foreach (int other in adjacent)
                    if (colors.TryGetValue(other, out uint m)) mask |= m;

            return mask;
        }

        // =====================================================================
        //  Partição fina — só para contenção de luz
        // =====================================================================

        /// <summary>
        /// Mais fino que <see cref="Build"/>: sala continua 1 partição por região dela (já é
        /// pequena o bastante), mas o CORREDOR — que no AnnotatedGrid é UMA região só,
        /// <see cref="AnnotatedGrid.CorridorRegion"/>, de propósito para o resto do jogo — é
        /// fatiado em pedaços de até <paramref name="corridorChunkSize"/> células contíguas,
        /// cada um com id próprio.
        ///
        /// Por que isto precisa existir: sem fatiar, dois braços de corredor que dão uma volta
        /// e ficam fisicamente perto um do outro são, para <see cref="Build"/>, A MESMA região —
        /// mesma cor, mesma luz — mesmo com parede sólida entre eles. Region-based containment
        /// não segura essa fronteira porque, pra ele, não existe fronteira nenhuma ali.
        ///
        /// Devolve o mapa célula → id de partição. Some da chave quem não está em nenhuma
        /// região playable (vazio/rocha) — RegionLightMask trata esse caso à parte
        /// (união das partições vizinhas), igual já fazia com região crua.
        /// </summary>
        public static Dictionary<int, int> BuildLightPartition(AnnotatedGrid grid, int corridorChunkSize = 4)
        {
            var cellToPartition = new Dictionary<int, int>();
            if (grid == null) return cellToPartition;

            Grid3D g = grid.Grid;
            int nextId = 0;

            // Salas: 1 id por região, sem fatiar — já são do tamanho de uma sala.
            var roomPartitionOf = new Dictionary<int, int>();
            for (int cell = 0; cell < g.CellCount; cell++)
            {
                int region = grid.GetRegion(cell);
                if (region < AnnotatedGrid.FirstRoomRegion) continue;

                if (!roomPartitionOf.TryGetValue(region, out int id))
                {
                    id = nextId++;
                    roomPartitionOf[region] = id;
                }
                cellToPartition[cell] = id;
            }

            // Corredor: BFS fatiando em pedaços de corridorChunkSize células. `cellToPartition`
            // É a própria marca de "já visitado" — evita a armadilha clássica de marcar
            // visited no ENQUEUE: se marcasse lá, células que sobram na fila quando o chunk
            // enche ficariam "visitadas" sem partição nenhuma, perdidas para sempre (o laço
            // externo nunca mais as escolhe como semente). Marcando só no DEQUEUE, sobra de
            // fila vira exatamente a semente do próximo chunk.
            var queue = new Queue<int>();
            for (int seed = 0; seed < g.CellCount; seed++)
            {
                if (grid.GetRegion(seed) != AnnotatedGrid.CorridorRegion) continue;
                if (cellToPartition.ContainsKey(seed)) continue;

                int chunkId = nextId++;
                queue.Clear();
                queue.Enqueue(seed);
                int count = 0;

                while (queue.Count > 0 && count < corridorChunkSize)
                {
                    int cell = queue.Dequeue();
                    if (cellToPartition.ContainsKey(cell)) continue; // pode ter sido enfileirado 2x

                    cellToPartition[cell] = chunkId;
                    count++;

                    foreach (Direction dir in Directions.Horizontal)
                    {
                        if (!g.TryGetNeighbor(cell, dir, out int neighbor)) continue;
                        if (cellToPartition.ContainsKey(neighbor)) continue;
                        if (grid.GetRegion(neighbor) != AnnotatedGrid.CorridorRegion) continue;
                        if (grid.GetBorder(cell, dir) == BorderLabel.Wall) continue;

                        queue.Enqueue(neighbor);
                    }
                }
            }

            return cellToPartition;
        }

        /// <summary>
        /// O grafo de adjacência sobre a partição fina — mesma regra de <see cref="Build"/>
        /// (borda não-Wall conecta), só que por partição em vez de por região crua.
        ///
        /// USO: exclusivamente <see cref="LightMask"/>, pra decidir quais cores VIZINHAS
        /// entram na máscara de uma tocha (luz atravessando porta aberta é correto). NÃO
        /// use isto para colorir (<see cref="AssignColors"/>) — ver <see cref="BuildAllPartitionNeighbors"/>.
        /// </summary>
        public static Dictionary<int, List<int>> BuildLightNeighbors(AnnotatedGrid grid, Dictionary<int, int> cellToPartition)
            => BuildPartitionNeighbors(grid, cellToPartition, passableOnly: true);

        /// <summary>
        /// Grafo de adjacência ESPACIAL completa sobre a partição fina: conecta duas
        /// partições vizinhas mesmo quando a borda entre elas é parede sólida.
        ///
        /// USO: exclusivamente <see cref="AssignColors"/>. É o grafo que garante a invariante
        /// que todo o resto do sistema depende ("mesma cor ⇒ longe o bastante pro Range da
        /// tocha nunca alcançar"): duas salas coladas por uma parede comum estão a distância
        /// ZERO uma da outra, então elas PRECISAM de cores diferentes mesmo sem porta entre
        /// elas — senão a tocha de uma ilumina a outra através da parede, sem shadow map
        /// nenhum pra impedir (rendering layer não sabe que existe parede; só sabe se o bit
        /// bate). <see cref="BuildLightNeighbors"/> (só borda passável) SUBESTIMA essa
        /// adjacência: como não existe grafo entre duas salas separadas por parede sólida,
        /// nada impedia o guloso de dar a mesma cor pra elas — e parede reta entre duas
        /// salas vizinhas é o caso MAIS comum do andar, não uma borda rara.
        /// </summary>
        public static Dictionary<int, List<int>> BuildAllPartitionNeighbors(AnnotatedGrid grid, Dictionary<int, int> cellToPartition)
            => BuildPartitionNeighbors(grid, cellToPartition, passableOnly: false);

        private static Dictionary<int, List<int>> BuildPartitionNeighbors(
            AnnotatedGrid grid, Dictionary<int, int> cellToPartition, bool passableOnly)
        {
            var neighbors = new Dictionary<int, List<int>>();
            if (grid == null || cellToPartition == null) return neighbors;

            Grid3D g = grid.Grid;

            for (int cell = 0; cell < g.CellCount; cell++)
            {
                if (!cellToPartition.TryGetValue(cell, out int partition)) continue;
                if (!neighbors.ContainsKey(partition)) neighbors[partition] = new List<int>();

                foreach (Direction dir in Directions.Horizontal)
                {
                    if (!g.TryGetNeighbor(cell, dir, out int neighbor)) continue;
                    if (!cellToPartition.TryGetValue(neighbor, out int other) || other == partition) continue;
                    if (passableOnly && grid.GetBorder(cell, dir) == BorderLabel.Wall) continue;

                    Connect(neighbors, partition, other);
                    Connect(neighbors, other, partition);
                }
            }

            return neighbors;
        }

        // =====================================================================
        //  Mundo → célula
        // =====================================================================

        /// <summary>
        /// Inverso de <see cref="AnnotatedGrid.CellToWorld"/>. -1 se cair fora do grid.
        /// Aqui e não no plugin porque o AnnotatedGrid é contrato de saída do WFC — e aqui
        /// em vez de repetido em cada consumidor porque já estava duplicado uma vez.
        /// </summary>
        public static int WorldToCell(AnnotatedGrid grid, Vector3 world)
        {
            if (grid == null) return -1;

            Vector3 local = world - grid.Origin;
            int x = Mathf.FloorToInt(local.x / grid.CellSize);
            int z = Mathf.FloorToInt(local.z / grid.CellSize);
            int y = grid.CellHeight > 0f ? Mathf.FloorToInt(local.y / grid.CellHeight) : 0;
            y = Mathf.Clamp(y, 0, grid.Grid.SizeY - 1);

            return grid.Grid.InBounds(x, y, z) ? grid.Grid.Index(x, y, z) : -1;
        }
    }
}
