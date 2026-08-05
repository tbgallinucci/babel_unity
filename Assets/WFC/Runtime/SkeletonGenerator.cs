// ============================================================================
//  SkeletonGenerator.cs  —  camada WFC.Runtime  (Decisão 1)
//
//  Gera o LAYOUT do andar de forma determinística, ANTES do WFC entrar. Ele não
//  desenha paredes: carimba papéis nas células, marca a que região cada uma
//  pertence e crava as junções (portas/passagens) entre regiões.
//
//  Algoritmo: SALAS SOLTAS + ÁRVORE GERADORA MÍNIMA
//    1. Espalha salas retangulares por rejection sampling (sem sobrepor, com
//       margem entre elas).
//    2. Liga os centros com uma MST (Prim) sobre a distância euclidiana —
//       conectividade GARANTIDA POR CONSTRUÇÃO: uma árvore é conexa por
//       definição, não por sorte.
//    3. Acrescenta algumas arestas extras para criar ciclos (senão o andar é uma
//       árvore pura e o jogador só faz ida e volta).
//    4. Escava corredores em L ao longo de cada aresta.
//    5. Crava junção onde o corredor encosta numa sala.
//    6. Escolhe entrada e escada (a sala mais distante da entrada), e escava uma
//       alcova 1×1 para a escada.
//
//  Por que a escada precisa de alcova: a peça Tile_Stairs é um nicho — 3 paredes
//  e 1 lado aberto. No meio de uma sala aberta ela não encaixa em lugar nenhum.
//  A alcova cria exatamente o formato de que ela precisa.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using WFC.Core;
using WFC.Data;

namespace WFC.Runtime
{
    public sealed class SkeletonGenerator
    {
        // =====================================================================
        //  Resultado
        // =====================================================================
        public sealed class Room
        {
            public RectInt Rect;       // em células, no plano XZ
            public int Region;
            public RoomRole Role;
            public int CenterCell;

            public Vector2Int Center => new Vector2Int(Rect.x + Rect.width / 2, Rect.y + Rect.height / 2);
        }

        public sealed class Result
        {
            public bool Success;
            public AnnotatedGrid Grid;
            public List<Room> Rooms = new List<Room>();

            /// <summary>Célula marcada como início do andar.</summary>
            public int EntranceCell = -1;

            /// <summary>Alcova com a escada para o próximo andar.</summary>
            public int StairsCell = -1;

            public int DoorCount;
            public int OpeningCount;
            public string Message;

            public override string ToString()
                => $"{(Success ? "OK" : "FALHOU")} | {Rooms.Count} salas | {DoorCount} portas | " +
                   $"{OpeningCount} vãos" + (string.IsNullOrEmpty(Message) ? "" : $" | {Message}");
        }

        // =====================================================================
        //  Geração
        // =====================================================================
        public Result Generate(Grid3D grid, float cellSize, float cellHeight, Vector3 origin,
                               SkeletonSettings settings, IRandom rng)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            settings = settings ?? SkeletonSettings.Default;

            var result = new Result
            {
                Grid = new AnnotatedGrid(grid, cellSize, cellHeight, origin),
            };

            // O esqueleto trabalha numa fatia horizontal. Andares empilhados vêm depois,
            // e serão gerações independentes (não há NavMesh contínuo entre níveis).
            const int y = 0;

            // ---- 1. Salas --------------------------------------------------
            result.Rooms = PlaceRooms(grid, settings, rng);
            if (result.Rooms.Count < 2)
            {
                result.Message = $"Só couberam {result.Rooms.Count} sala(s). " +
                                 "Aumente o grid, diminua roomWidth/roomDepth ou reduza roomMargin.";
                return result;
            }

            AnnotatedGrid ag = result.Grid;
            for (int i = 0; i < result.Rooms.Count; i++)
            {
                Room room = result.Rooms[i];
                room.Region = AnnotatedGrid.FirstRoomRegion + i;
                room.Role = RoomRole.Combate;
                room.CenterCell = grid.Index(room.Center.x, y, room.Center.y);

                ag.SetRegionBlock(room.Rect.xMin, room.Rect.yMin, room.Rect.xMax - 1, room.Rect.yMax - 1, y, room.Region);
                ag.SetRoleBlock(room.Rect.xMin, y, room.Rect.yMin, room.Rect.xMax - 1, y, room.Rect.yMax - 1, RoomRole.Combate);
            }

            // ---- 2. MST + ciclos extras ------------------------------------
            List<(int a, int b)> edges = BuildMinimumSpanningTree(result.Rooms);
            AddExtraLoops(result.Rooms, edges, settings, rng);

            // ---- 3. Corredores ---------------------------------------------
            foreach ((int a, int b) in edges)
                CarveCorridor(ag, grid, y, result.Rooms[a], result.Rooms[b], settings, rng);

            // ---- 4. Papéis de entrada e escada -----------------------------
            AssignEntranceAndStairs(ag, grid, y, result, rng);

            // NOTA: o esqueleto crava porta em toda junção e NÃO verifica se o kit tem
            // peça para aquele formato de célula — de propósito. Ele é agnóstico ao
            // tileset (Decisão 1). Quem confere isso é o WFCFiller, que é quem segura o
            // tileset bakeado: ele rebaixa para vão simples o que não couber
            // (WFCFiller.ReconcileDoors), preservando a conectividade prometida aqui.

            result.DoorCount = ag.CountJunctions(Junction.Door);
            result.OpeningCount = ag.CountJunctions(Junction.Opening);
            result.Success = true;
            return result;
        }

        // ---------------------------------------------------------------- salas
        private static List<Room> PlaceRooms(Grid3D grid, SkeletonSettings s, IRandom rng)
        {
            var rooms = new List<Room>();

            int minW = Mathf.Max(1, Mathf.Min(s.roomWidth.x, s.roomWidth.y));
            int maxW = Mathf.Max(minW, Mathf.Max(s.roomWidth.x, s.roomWidth.y));
            int minD = Mathf.Max(1, Mathf.Min(s.roomDepth.x, s.roomDepth.y));
            int maxD = Mathf.Max(minD, Mathf.Max(s.roomDepth.x, s.roomDepth.y));

            int pad = Mathf.Max(1, s.borderPadding);

            for (int i = 0; i < s.targetRooms; i++)
            {
                for (int attempt = 0; attempt < s.placementAttempts; attempt++)
                {
                    int w = minW + rng.NextInt(maxW - minW + 1);
                    int d = minD + rng.NextInt(maxD - minD + 1);

                    int spanX = grid.SizeX - 2 * pad - w;
                    int spanZ = grid.SizeZ - 2 * pad - d;
                    if (spanX < 0 || spanZ < 0) continue; // sala maior que o grid

                    int x = pad + rng.NextInt(spanX + 1);
                    int z = pad + rng.NextInt(spanZ + 1);

                    var rect = new RectInt(x, z, w, d);
                    if (OverlapsAny(rooms, rect, s.roomMargin)) continue;

                    rooms.Add(new Room { Rect = rect });
                    break;
                }
            }

            return rooms;
        }

        private static bool OverlapsAny(List<Room> rooms, RectInt candidate, int margin)
        {
            var inflated = new RectInt(
                candidate.x - margin, candidate.y - margin,
                candidate.width + 2 * margin, candidate.height + 2 * margin);

            foreach (Room r in rooms)
                if (inflated.Overlaps(r.Rect)) return true;

            return false;
        }

        // ------------------------------------------------------------------ MST
        /// <summary>
        /// Prim sobre o grafo completo dos centros de sala. O(n²), o que é irrelevante
        /// para a dúzia de salas de um andar — e devolve uma árvore, portanto conexa.
        /// </summary>
        private static List<(int a, int b)> BuildMinimumSpanningTree(List<Room> rooms)
        {
            var edges = new List<(int a, int b)>();
            int n = rooms.Count;
            if (n < 2) return edges;

            var inTree = new bool[n];
            var bestDist = new float[n];
            var bestFrom = new int[n];

            for (int i = 0; i < n; i++) { bestDist[i] = float.MaxValue; bestFrom[i] = -1; }

            inTree[0] = true;
            for (int i = 1; i < n; i++)
            {
                bestDist[i] = SqrDistance(rooms[0], rooms[i]);
                bestFrom[i] = 0;
            }

            for (int added = 1; added < n; added++)
            {
                int next = -1;
                float best = float.MaxValue;
                for (int i = 0; i < n; i++)
                    if (!inTree[i] && bestDist[i] < best) { best = bestDist[i]; next = i; }

                if (next < 0) break; // não deve acontecer com grafo completo
                inTree[next] = true;
                edges.Add((bestFrom[next], next));

                for (int i = 0; i < n; i++)
                {
                    if (inTree[i]) continue;
                    float d = SqrDistance(rooms[next], rooms[i]);
                    if (d < bestDist[i]) { bestDist[i] = d; bestFrom[i] = next; }
                }
            }

            return edges;
        }

        private static float SqrDistance(Room a, Room b)
        {
            Vector2Int ca = a.Center, cb = b.Center;
            float dx = ca.x - cb.x, dz = ca.y - cb.y;
            return dx * dx + dz * dz;
        }

        /// <summary>
        /// Arestas além da árvore, para o andar ter ciclos. Sem isso o jogador só
        /// consegue voltar pelo mesmo caminho, e o mapa fica com cara de corredor único.
        /// </summary>
        private static void AddExtraLoops(List<Room> rooms, List<(int a, int b)> edges, SkeletonSettings s, IRandom rng)
        {
            if (s.extraLoopChance <= 0f) return;

            var existing = new HashSet<long>();
            foreach ((int a, int b) in edges) existing.Add(EdgeKey(a, b));

            for (int a = 0; a < rooms.Count; a++)
            for (int b = a + 1; b < rooms.Count; b++)
            {
                if (existing.Contains(EdgeKey(a, b))) continue;
                if (rng.NextDouble() >= s.extraLoopChance) continue;
                edges.Add((a, b));
                existing.Add(EdgeKey(a, b));
            }
        }

        private static long EdgeKey(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        // ----------------------------------------------------------- corredores
        /// <summary>
        /// Escava um corredor em L entre os centros de duas salas. As células que já
        /// pertencem a alguma sala não são tocadas; as de fora viram corredor. Onde a
        /// região muda ao longo do caminho, nasce uma junção.
        /// </summary>
        private static void CarveCorridor(AnnotatedGrid ag, Grid3D grid, int y,
                                          Room a, Room b, SkeletonSettings s, IRandom rng)
        {
            List<int> path = BuildLPath(grid, y, a.Center, b.Center, rng.NextDouble() < 0.5);

            // Escavar
            foreach (int cell in path)
            {
                if (ag.GetRegion(cell) >= 0) continue; // já é sala ou corredor
                ag.SetRegion(cell, AnnotatedGrid.CorridorRegion);
                ag.SetRole(cell, RoomRole.Corredor);
            }

            // Junções onde a região muda de uma célula do caminho para a seguinte.
            for (int i = 0; i + 1 < path.Count; i++)
            {
                int c = path[i], n = path[i + 1];
                if (ag.GetRegion(c) == ag.GetRegion(n)) continue;
                if (!TryGetDirection(grid, c, n, out Direction dir)) continue;
                if (ag.GetJunction(c, dir) != Junction.None) continue; // já cravada por outro corredor

                ag.SetJunction(c, dir, s.useDoors ? Junction.Door : Junction.Opening);
            }
        }

        private static List<int> BuildLPath(Grid3D grid, int y, Vector2Int from, Vector2Int to, bool xFirst)
        {
            var path = new List<int>();
            int x = from.x, z = from.y;
            path.Add(grid.Index(x, y, z));

            void StepX() { while (x != to.x) { x += x < to.x ? 1 : -1; path.Add(grid.Index(x, y, z)); } }
            void StepZ() { while (z != to.y) { z += z < to.y ? 1 : -1; path.Add(grid.Index(x, y, z)); } }

            if (xFirst) { StepX(); StepZ(); }
            else        { StepZ(); StepX(); }

            return path;
        }

        private static bool TryGetDirection(Grid3D grid, int from, int to, out Direction dir)
        {
            foreach (Direction d in Directions.Horizontal)
                if (grid.TryGetNeighbor(from, d, out int n) && n == to) { dir = d; return true; }

            dir = Direction.PosX;
            return false;
        }

        // ------------------------------------------------------ entrada e escada
        private static void AssignEntranceAndStairs(AnnotatedGrid ag, Grid3D grid, int y, Result result, IRandom rng)
        {
            List<Room> rooms = result.Rooms;

            // Entrada: a sala mais perto de um canto do grid — dá sensação de "borda do mapa".
            int entranceIndex = 0;
            float bestCorner = float.MaxValue;
            for (int i = 0; i < rooms.Count; i++)
            {
                Vector2Int c = rooms[i].Center;
                float d = c.x * c.x + c.y * c.y;
                if (d < bestCorner) { bestCorner = d; entranceIndex = i; }
            }

            // Escada: a sala mais distante da entrada. Maximiza o percurso do andar.
            int stairsIndex = entranceIndex;
            float farthest = -1f;
            for (int i = 0; i < rooms.Count; i++)
            {
                if (i == entranceIndex) continue;
                float d = SqrDistance(rooms[entranceIndex], rooms[i]);
                if (d > farthest) { farthest = d; stairsIndex = i; }
            }

            rooms[entranceIndex].Role = RoomRole.Entrada;
            rooms[stairsIndex].Role = RoomRole.Escada;

            // A entrada é um marcador de célula, não muda a geometria.
            result.EntranceCell = rooms[entranceIndex].CenterCell;
            ag.SetRole(result.EntranceCell, RoomRole.Entrada);

            // A escada precisa de uma alcova 1×1 grudada na sala: 3 paredes + 1 lado
            // aberto, que é exatamente o formato da peça Tile_Stairs.
            // (CarveStairsAlcove já carimba o papel Escada na alcova.)
            result.StairsCell = CarveStairsAlcove(ag, grid, y, rooms[stairsIndex], rng);

            if (result.StairsCell < 0)
            {
                // Sala encurralada, sem lugar para a alcova. A escada vira um marcador
                // no centro da sala e o papel NÃO é carimbado: exigir a peça Tile_Stairs
                // (que é um nicho de 3 paredes) numa célula de piso aberto seria uma
                // contradição garantida no solver. Perde-se a rampa, o andar segue jogável.
                result.StairsCell = rooms[stairsIndex].CenterCell;
                result.Message = "Não coube alcova para a escada; ela virou marcador sem geometria própria.";
            }
        }

        /// <summary>
        /// Procura uma célula vazia colada na parede da sala cujos outros 3 vizinhos
        /// também sejam vazios — isto é, um nicho de verdade. Devolve -1 se não houver.
        /// </summary>
        private static int CarveStairsAlcove(AnnotatedGrid ag, Grid3D grid, int y, Room room, IRandom rng)
        {
            var candidates = new List<(int alcove, int roomCell)>();

            for (int z = room.Rect.yMin; z < room.Rect.yMax; z++)
            for (int x = room.Rect.xMin; x < room.Rect.xMax; x++)
            {
                bool onEdge = x == room.Rect.xMin || x == room.Rect.xMax - 1
                           || z == room.Rect.yMin || z == room.Rect.yMax - 1;
                if (!onEdge) continue;

                int roomCell = grid.Index(x, y, z);

                foreach (Direction dir in Directions.Horizontal)
                {
                    if (!grid.TryGetNeighbor(roomCell, dir, out int alcove)) continue;
                    if (ag.GetRegion(alcove) >= 0) continue; // já é sala ou corredor

                    // Os outros três lados da alcova têm que ser vazio, senão ela não é nicho.
                    bool isolated = true;
                    foreach (Direction other in Directions.Horizontal)
                    {
                        if (other == Directions.Opposite(dir)) continue; // é o lado da sala
                        if (grid.TryGetNeighbor(alcove, other, out int n) && ag.GetRegion(n) >= 0)
                        { isolated = false; break; }
                    }

                    if (isolated) candidates.Add((alcove, roomCell));
                }
            }

            if (candidates.Count == 0) return -1;

            (int chosenAlcove, int _) = candidates[rng.NextInt(candidates.Count)];

            // A alcova entra na MESMA região da sala: assim a borda entre as duas é
            // aberta sem precisar de junção, e os outros três lados ficam parede.
            ag.SetRegion(chosenAlcove, room.Region);
            ag.SetRole(chosenAlcove, RoomRole.Escada);
            return chosenAlcove;
        }

    }
}
