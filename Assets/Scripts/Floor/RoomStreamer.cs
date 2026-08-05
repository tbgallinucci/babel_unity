// ============================================================================
//  RoomStreamer.cs  —  código do JOGO (Decisão 4: fora do plugin)
//
//  O substituto caseiro do Occlusion Culling, que o Unity não consegue fazer
//  aqui porque o dele é BAKEADO em editor e o nosso andar nasce em runtime
//  (ver Docs/Development/Engine/Risks/...).
//
//  A ideia: o esqueleto já entrega um GRAFO de espaços conectados (regiões do
//  AnnotatedGrid — corredores são a região 0, cada sala tem a sua). Se o
//  jogador está na sala A, tudo que ele PODE ver está a poucos passos de A
//  nesse grafo, porque a única forma de enxergar outra sala é através das
//  portas/vãos que conectam as duas. Então:
//
//      visível = BFS a partir da região do jogador, até N passos
//
//  Por que só frustum (o "cone de visão") NÃO bastaria: o Unity já faz frustum
//  culling por Renderer, de graça. O problema é justamente o que está DENTRO do
//  cone mas ATRÁS de uma parede — frustum não sabe disso. O grafo sabe.
//
//  Mas frustum ajuda POR CIMA do grafo, em granularidade de região: para as
//  regiões mais distantes (além de `alwaysVisibleDepth`), exigimos também que a
//  caixa da região intersecte o frustum. Isso corta a sala que está a 2 portas
//  de distância mas atrás da câmera, sem nunca piscar a sala vizinha (que
//  continua ligada por profundidade, mesmo fora do cone) — que é de onde o
//  pop-in viria.
//
//  Só mexe em Renderer.enabled, nunca em SetActive: desligar o GameObject
//  levaria junto os colliders, e o jogador atravessaria o chão de uma sala que
//  ele ainda não "vê" mas na qual pode estar prestes a entrar.
//
//  ANEL DE OCLUSÃO: as regiões escondidas ENCOSTADAS nas visíveis não são
//  desligadas — vão para ShadowCastingMode.ShadowsOnly. Renderer desabilitado
//  para de projetar sombra também, e era isso que fazia a tocha da sala vizinha
//  escondida atravessar a parede que deveria bloqueá-la: o oclusor tinha sido
//  culado junto com a geometria. ShadowsOnly mantém a parede invisível e
//  bloqueando. Custa pouco porque só as poucas luzes Key projetam sombra.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WFC.Core;
using WFC.Runtime;

namespace Babel.Floor
{
    [AddComponentMenu("Babel/Room Streamer")]
    public sealed class RoomStreamer : MonoBehaviour
    {
        [Tooltip("Quem define a posição de referência. Normalmente o Player.")]
        [SerializeField] private Transform viewer;

        [Tooltip("Câmera usada no teste de cone de visão. Vazio = Camera.main.")]
        [SerializeField] private Camera viewCamera;

        [Tooltip("Quantos passos no grafo de salas/corredores ficam visíveis a partir de onde o " +
                 "jogador está. 2 costuma bastar: a sala atual, as vizinhas e as vizinhas delas. " +
                 "Subir isso reduz pop-in e reduz o ganho de performance, na mesma medida.")]
        [Min(0)] [SerializeField] private int visibleDepth = 2;

        [Tooltip("Regiões até esta profundidade ficam SEMPRE ligadas, sem passar pelo teste de cone. " +
                 "É o que impede a sala do lado de piscar quando o jogador gira a câmera. " +
                 "Mantenha >= 1.")]
        [Min(0)] [SerializeField] private int alwaysVisibleDepth = 1;

        [Tooltip("Aplica o teste de cone de visão (frustum) nas regiões além de 'Always Visible Depth'. " +
                 "Desligue para usar só a distância no grafo — mais conservador, menos ganho.")]
        [SerializeField] private bool useFrustumCulling = true;

        [Tooltip("Segundos entre reavaliações. A geometria não se move, só o jogador.")]
        [Min(0.02f)] [SerializeField] private float updateInterval = 0.15f;

        [SerializeField] private bool logSummary = true;

        // ---- Estado montado uma vez por andar (Rebuild) ---------------------
        private AnnotatedGrid grid;
        private readonly Dictionary<int, List<Renderer>> renderersByRegion = new Dictionary<int, List<Renderer>>();
        private readonly Dictionary<int, List<int>> regionNeighbors = new Dictionary<int, List<int>>();
        private readonly Dictionary<int, Bounds> boundsByRegion = new Dictionary<int, Bounds>();

        // Células fora de qualquer região (rocha/preenchimento). Não participam do grafo:
        // ficam ligadas junto com QUALQUER região vizinha delas que esteja visível, senão
        // apareceria um buraco no casco onde deveria ter parede maciça.
        private readonly List<(Renderer[] renderers, int[] adjacentRegions)> voidPieces =
            new List<(Renderer[], int[])>();

        // Modo de sombra que a peça trazia do prefab. Restaurar o valor ORIGINAL, e não um
        // `On` chutado, importa: uma peça que o kit tenha marcado como Off voltaria a projetar
        // sombra sem ninguém ter pedido.
        private readonly Dictionary<Renderer, ShadowCastingMode> originalShadowMode =
            new Dictionary<Renderer, ShadowCastingMode>();

        private readonly HashSet<int> visibleRegions = new HashSet<int>();
        private readonly HashSet<int> nearRegions = new HashSet<int>();

        /// <summary>Regiões escondidas encostadas em alguma visível: invisíveis, mas ainda oclusoras.</summary>
        private readonly HashSet<int> occluderRegions = new HashSet<int>();

        private readonly Queue<(int region, int depth)> bfs = new Queue<(int, int)>();

        // Reaproveitados entre avaliações: este script existe pra economizar frame, seria
        // contraditório ele mesmo gerar lixo de GC a cada updateInterval.
        private readonly List<int> cullScratch = new List<int>();
        private readonly Plane[] frustumPlanes = new Plane[6];

        private float nextUpdate;
        private int lastVisibleCount = -1;

        /// <summary>Regiões visíveis na última avaliação — diagnóstico.</summary>
        public int VisibleRegionCount => visibleRegions.Count;

        // =====================================================================
        //  Montagem
        // =====================================================================

        /// <summary>
        /// Indexa a geometria do andar por região e monta o grafo de conectividade.
        /// Chame uma vez por andar, depois que o gerador terminar.
        /// </summary>
        public void Rebuild(GeneratedFloor floor, Transform viewerOverride = null)
        {
            Clear();
            if (viewerOverride != null) viewer = viewerOverride;

            if (floor == null || !floor.Success || floor.PiecesByCell == null)
            {
                if (logSummary)
                    Debug.LogWarning("[RoomStreamer] Andar sem PiecesByCell — streaming desligado neste andar.", this);
                return;
            }

            grid = floor.Grid;
            Grid3D g = grid.Grid;
            Transform[] pieces = floor.PiecesByCell;

            var buffer = new List<Renderer>();

            for (int cell = 0; cell < pieces.Length && cell < g.CellCount; cell++)
            {
                Transform piece = pieces[cell];
                if (piece == null) continue;

                buffer.Clear();
                piece.GetComponentsInChildren(true, buffer);
                if (buffer.Count == 0) continue;

                foreach (Renderer r in buffer)
                    if (r != null) originalShadowMode[r] = r.shadowCastingMode;

                int region = grid.GetRegion(cell);

                if (region < 0)
                {
                    voidPieces.Add((buffer.ToArray(), CollectAdjacentRegions(g, cell)));
                    continue;
                }

                if (!renderersByRegion.TryGetValue(region, out List<Renderer> list))
                {
                    list = new List<Renderer>();
                    renderersByRegion[region] = list;
                    boundsByRegion[region] = new Bounds(grid.CellToWorld(cell), Vector3.zero);
                }

                list.AddRange(buffer);

                Bounds b = boundsByRegion[region];
                b.Encapsulate(new Bounds(grid.CellToWorld(cell),
                                         new Vector3(grid.CellSize, grid.CellHeight * 2f, grid.CellSize)));
                boundsByRegion[region] = b;
            }

            // Mesmo grafo que o RegionLightMask usa para colorir as regiões — ter as duas
            // coisas derivando da MESMA função é o que impede "visível aqui, contido ali"
            // discordarem sobre quem é vizinho de quem.
            foreach (KeyValuePair<int, List<int>> pair in RegionGraph.Build(grid))
                regionNeighbors[pair.Key] = pair.Value;

            if (logSummary)
                Debug.Log($"[RoomStreamer] Andar {floor.FloorNumber}: {renderersByRegion.Count} região(ões) " +
                          $"indexada(s), {voidPieces.Count} peça(s) de preenchimento.", this);

            nextUpdate = 0f;
        }

        public void Clear()
        {
            // Religa tudo antes de soltar as referências: se o andar for destruído com peças
            // desligadas e algo reaproveitar aquela geometria, ela voltaria invisível.
            ShowEverything();

            renderersByRegion.Clear();
            regionNeighbors.Clear();
            boundsByRegion.Clear();
            voidPieces.Clear();
            originalShadowMode.Clear();
            visibleRegions.Clear();
            nearRegions.Clear();
            occluderRegions.Clear();
            grid = null;
            lastVisibleCount = -1;
        }

        private int[] CollectAdjacentRegions(Grid3D g, int cell)
        {
            var regions = new List<int>();
            foreach (Direction dir in Directions.Horizontal)
            {
                if (!g.TryGetNeighbor(cell, dir, out int neighbor)) continue;
                int r = grid.GetRegion(neighbor);
                if (r >= 0 && !regions.Contains(r)) regions.Add(r);
            }
            return regions.ToArray();
        }

        // =====================================================================
        //  Avaliação
        // =====================================================================

        private void LateUpdate()
        {
            if (grid == null || viewer == null) return;
            if (Time.time < nextUpdate) return;
            nextUpdate = Time.time + updateInterval;

            Evaluate();
        }

        private void Evaluate()
        {
            int viewerCell = WorldToCell(viewer.position);
            if (viewerCell < 0) return;

            int origin = grid.GetRegion(viewerCell);

            // Jogador fora de qualquer região (em cima de preenchimento, ou entre andares):
            // não dá pra decidir o que é visível, então religa tudo em vez de arriscar
            // esconder a sala em que ele está.
            if (origin < 0) { ShowEverything(); return; }

            visibleRegions.Clear();
            nearRegions.Clear();
            bfs.Clear();

            bfs.Enqueue((origin, 0));
            visibleRegions.Add(origin);
            nearRegions.Add(origin);

            while (bfs.Count > 0)
            {
                (int region, int depth) = bfs.Dequeue();
                if (depth >= visibleDepth) continue;
                if (!regionNeighbors.TryGetValue(region, out List<int> neighbors)) continue;

                foreach (int next in neighbors)
                {
                    if (!visibleRegions.Add(next)) continue;
                    if (depth + 1 <= alwaysVisibleDepth) nearRegions.Add(next);
                    bfs.Enqueue((next, depth + 1));
                }
            }

            // Cone de visão só nas regiões distantes: as próximas ficam ligadas de qualquer
            // jeito, senão a sala do lado piscaria a cada giro de câmera.
            if (useFrustumCulling)
            {
                Camera cam = viewCamera != null ? viewCamera : Camera.main;
                if (cam != null)
                {
                    GeometryUtility.CalculateFrustumPlanes(cam, frustumPlanes);

                    cullScratch.Clear();
                    foreach (int region in visibleRegions)
                    {
                        if (nearRegions.Contains(region)) continue;
                        if (!boundsByRegion.TryGetValue(region, out Bounds b)) continue;
                        if (!GeometryUtility.TestPlanesAABB(frustumPlanes, b)) cullScratch.Add(region);
                    }

                    foreach (int region in cullScratch) visibleRegions.Remove(region);
                }
            }

            BuildOccluderRing();
            Apply();
        }

        /// <summary>
        /// Toda região escondida que encosta numa visível. São exatamente as paredes de onde
        /// a luz vazava: perto o bastante para as tochas Key alcançarem, mas culadas — e
        /// portanto sem projetar a sombra que as bloquearia.
        /// </summary>
        private void BuildOccluderRing()
        {
            occluderRegions.Clear();

            foreach (int region in visibleRegions)
            {
                if (!regionNeighbors.TryGetValue(region, out List<int> neighbors)) continue;

                foreach (int next in neighbors)
                    if (!visibleRegions.Contains(next)) occluderRegions.Add(next);
            }
        }

        private void Apply()
        {
            foreach (KeyValuePair<int, List<Renderer>> pair in renderersByRegion)
                ApplyState(pair.Value, StateOf(pair.Key));

            foreach ((Renderer[] renderers, int[] adjacentRegions) in voidPieces)
            {
                // Preenchimento maciço acompanha a MELHOR das regiões que encosta: visível se
                // alguma vizinha está visível, oclusor se só encosta no anel. Sem isto
                // apareceria buraco no casco onde deveria ter rocha.
                RegionState state = RegionState.Hidden;
                foreach (int region in adjacentRegions)
                {
                    RegionState candidate = StateOf(region);
                    if (candidate > state) state = candidate;
                    if (state == RegionState.Visible) break;
                }

                ApplyState(renderers, state);
            }

            if (logSummary && visibleRegions.Count != lastVisibleCount)
            {
                lastVisibleCount = visibleRegions.Count;
                Debug.Log($"[RoomStreamer] {visibleRegions.Count} de {renderersByRegion.Count} região(ões) visível(eis).", this);
            }
        }

        private void ShowEverything()
        {
            foreach (List<Renderer> list in renderersByRegion.Values)
                ApplyState(list, RegionState.Visible);

            foreach ((Renderer[] renderers, int[] _) in voidPieces)
                ApplyState(renderers, RegionState.Visible);
        }

        // =====================================================================
        //  Três estados por região
        // =====================================================================

        /// <summary>Ordenado de propósito: o preenchimento maciço fica com o MAIOR dos vizinhos.</summary>
        private enum RegionState
        {
            /// <summary>Nem se vê, nem bloqueia luz. Longe o bastante para as duas coisas.</summary>
            Hidden = 0,

            /// <summary>Invisível, mas ainda entra no shadow map — é a parede que contém a luz.</summary>
            Occluder = 1,

            /// <summary>Renderizada normalmente.</summary>
            Visible = 2,
        }

        private RegionState StateOf(int region)
        {
            if (visibleRegions.Contains(region)) return RegionState.Visible;
            return occluderRegions.Contains(region) ? RegionState.Occluder : RegionState.Hidden;
        }

        private void ApplyState(IReadOnlyList<Renderer> renderers, RegionState state)
        {
            for (int i = 0; i < renderers.Count; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;

                if (state == RegionState.Hidden)
                {
                    r.enabled = false;
                    continue;
                }

                // ShadowsOnly EXIGE o Renderer habilitado: o modo diz "renderize só no shadow
                // map", não "renderize mesmo desabilitado".
                r.enabled = true;
                r.shadowCastingMode = state == RegionState.Occluder
                    ? ShadowCastingMode.ShadowsOnly
                    : Original(r);
            }
        }

        private ShadowCastingMode Original(Renderer r)
            => originalShadowMode.TryGetValue(r, out ShadowCastingMode mode) ? mode : ShadowCastingMode.On;

        private int WorldToCell(Vector3 world) => RegionGraph.WorldToCell(grid, world);
    }
}
