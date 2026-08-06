// ============================================================================
//  RegionLightMask.cs  —  código do JOGO (Decisão 4: fora do plugin)
//
//  Impede que a luz de uma sala ilumine a geometria de OUTRA sala, usando
//  Rendering Layers do URP — sem shader novo e sem depender de shadow map.
//
//  Por que isso é necessário: a maior parte das tochas está no tier Fill, que
//  por definição NÃO projeta sombra (sombra é o que custa caro; ver
//  DynamicLightBudget). Uma luz sem sombra atravessa parede — ela não sabe que
//  a parede existe. Era daí que vinha "alguns pontos que não aparentam ter
//  iluminação ficam acesos": a tocha da sala do lado, através da parede.
//
//  A ideia: o WFC já entrega o andar particionado em REGIÕES (salas + a malha
//  de corredores). Colorindo o grafo de regiões de forma que vizinhas nunca
//  compartilhem cor (RegionGraph.AssignColors), cada tocha pode ser restrita a
//  iluminar só a própria região e as ligadas a ela por passagem. Luz cruzando
//  uma porta aberta é correta; o que não pode é cruzar parede.
//
//  NÃO usa a região crua do AnnotatedGrid direto — usa RegionGraph.BuildLightPartition.
//  Motivo: no AnnotatedGrid, TODO corredor do andar é UMA região só
//  (AnnotatedGrid.CorridorRegion), de propósito, para o resto do jogo. Region-based
//  containment sobre essa região crua não segura nada entre dois braços de corredor
//  que dão uma volta e ficam fisicamente perto um do outro — pra ele são a MESMA
//  região, mesma cor, mesma luz, mesmo com parede sólida entre eles (foi exatamente
//  o vazamento reportado numa curva de corredor). BuildLightPartition fatia o
//  corredor em pedaços menores só para este propósito; sala continua 1:1 com a
//  região dela (já é pequena o bastante, ver skeleton.roomWidth/roomDepth).
//
//  LIMITE conhecido 1: isto contém luz entre PARTIÇÕES, não dentro de uma. Um pilar
//  no meio de uma sala grande (ou dentro do mesmo pedaço de corredor) continua
//  sendo atravessado pelo fill — quem cobre esse caso é o FloorLightField (Etapa 3,
//  granularidade de célula) e a sombra das poucas tochas Key.
//
//  LIMITE conhecido 2: a coloração evita colisão entre partições espacialmente
//  adjacentes (RegionGraph.BuildAllPartitionNeighbors, parede sólida inclusa — foi
//  bug até este comentário existir: só usava borda passável, então sala colada em
//  sala por parede comum sem porta podia sair com a mesma cor e a luz atravessava
//  bem ali). O que SOBRA sem cobertura é o caso bem mais raro: duas partições que
//  não compartilham célula vizinha nenhuma, mas ficam perto no MUNDO porque o andar
//  dobra sobre si mesmo (ala A e ala B, sem toque direto, mas próximas no espaço).
//  Se aparecer, o RegionGraph.ColorCount (7) é o primeiro lugar pra olhar.
// ============================================================================

using System.Collections.Generic;
using Babel.VFX;
using UnityEngine;
using WFC.Runtime;

namespace Babel.Floor
{
    [AddComponentMenu("Babel/Region Light Mask")]
    public sealed class RegionLightMask : MonoBehaviour
    {
        [Tooltip("Objetos que se movem entre salas e precisam ter a máscara reavaliada — " +
                 "normalmente só o Player. Inimigos e props ficam no Default (iluminados por " +
                 "qualquer tocha), que é o comportamento antigo e nunca deixa nada preto.")]
        [SerializeField] private List<Transform> dynamicObjects = new List<Transform>();

        [Tooltip("Segundos entre reavaliações dos objetos dinâmicos. A geometria é carimbada " +
                 "uma vez por andar e não muda; só quem anda precisa de atualização.")]
        [Min(0.02f)] [SerializeField] private float updateInterval = 0.15f;

        [Tooltip("Corredor é fatiado em pedaços de até N células contíguas, só para conter luz " +
                 "(a região continua UMA só para o resto do jogo). Pedaço menor = fronteira mais " +
                 "fina, mas mais partições no grafo. 4 células (24 m com Cell Size 6) fica perto " +
                 "do alcance de uma tocha — reduzir mais raramente compensa.")]
        [Min(1)] [SerializeField] private int corridorChunkSize = 4;

        [SerializeField] private bool logSummary = true;

        private AnnotatedGrid grid;
        private Dictionary<int, int> cellToPartition;
        private Dictionary<int, List<int>> neighbors;
        private Dictionary<int, uint> colors;

        private readonly List<Renderer> rendererBuffer = new List<Renderer>();
        private readonly Dictionary<Transform, int> lastPartitionOf = new Dictionary<Transform, int>();

        private float nextUpdate;

        /// <summary>Quantas cores distintas o andar atual usou — diagnóstico.</summary>
        public int ColorsUsed { get; private set; }

        // =====================================================================
        //  Montagem
        // =====================================================================

        /// <summary>
        /// Colore as regiões e carimba geometria e tochas. Chame DEPOIS de todos os
        /// populadores — tocha plantada depois deste ponto fica sem máscara (e, por causa da
        /// rede de segurança do Default, ilumina tudo, como antes).
        /// </summary>
        public void Rebuild(GeneratedFloor floor, Transform viewerOverride = null)
        {
            Clear();

            if (viewerOverride != null && !dynamicObjects.Contains(viewerOverride))
                dynamicObjects.Add(viewerOverride);

            if (floor == null || !floor.Success || floor.PiecesByCell == null)
            {
                if (logSummary)
                    Debug.LogWarning("[RegionLightMask] Andar sem PiecesByCell — contenção de luz " +
                                     "desligada neste andar (a luz volta a atravessar parede).", this);
                return;
            }

            grid = floor.Grid;
            cellToPartition = RegionGraph.BuildLightPartition(grid, corridorChunkSize);

            // Dois grafos, dois papéis: `neighbors` (só borda passável) decide quais cores
            // VIZINHAS entram na máscara de uma tocha, pra luz atravessar porta aberta.
            // A coloração em si precisa do grafo espacial COMPLETO (parede sólida inclusa) —
            // senão duas salas coladas por parede comum, sem porta, podiam sair com a mesma
            // cor e a luz voltava a atravessar exatamente essa parede. Ver comentário em
            // RegionGraph.BuildAllPartitionNeighbors.
            neighbors = RegionGraph.BuildLightNeighbors(grid, cellToPartition);
            var colorAdjacency = RegionGraph.BuildAllPartitionNeighbors(grid, cellToPartition);
            colors = RegionGraph.AssignColors(colorAdjacency);

            var distinct = new HashSet<uint>(colors.Values);
            ColorsUsed = distinct.Count;

            StampGeometry(floor);
            int torches = StampTorches(floor.Root);

            if (logSummary)
                Debug.Log($"[RegionLightMask] Andar {floor.FloorNumber}: {colors.Count} partição(ões) " +
                          $"em {ColorsUsed} cor(es), {torches} tocha(s) contida(s).", this);

            nextUpdate = 0f;
        }

        public void Clear()
        {
            grid = null;
            cellToPartition = null;
            neighbors = null;
            colors = null;
            ColorsUsed = 0;
            lastPartitionOf.Clear();
        }

        /// <summary>
        /// Registra algo que se move entre salas depois da geração (inimigo invocado,
        /// prop arremessado). Sem registrar, o objeto fica no Default e é iluminado por
        /// qualquer tocha — feio, mas nunca preto.
        /// </summary>
        public void RegisterDynamic(Transform target)
        {
            if (target == null || dynamicObjects.Contains(target)) return;
            dynamicObjects.Add(target);
        }

        // =====================================================================
        //  Carimbo
        // =====================================================================

        private void StampGeometry(GeneratedFloor floor)
        {
            Transform[] pieces = floor.PiecesByCell;
            int cellCount = grid.Grid.CellCount;

            for (int cell = 0; cell < pieces.Length && cell < cellCount; cell++)
            {
                Transform piece = pieces[cell];
                if (piece == null) continue;

                bool hasPartition = cellToPartition.TryGetValue(cell, out int partition);

                // Preenchimento maciço (rocha) não pertence a partição nenhuma, mas é parede de
                // quem encosta nele — ver UnionOfAdjacent pra regra exata (não é uma união cega:
                // rocha tocando 2+ partições diferentes fica sem cor de tocha nenhuma, de propósito).
                uint mask = hasPartition
                    ? MaskOf(partition)
                    : UnionOfAdjacent(cell);

                rendererBuffer.Clear();
                piece.GetComponentsInChildren(true, rendererBuffer);

                foreach (Renderer r in rendererBuffer)
                    if (r != null) r.renderingLayerMask = mask;
            }
        }

        private int StampTorches(Transform floorRoot)
        {
            if (floorRoot == null) return 0;

            var torches = new List<TorchLight>();
            floorRoot.GetComponentsInChildren(true, torches);

            int stamped = 0;
            foreach (TorchLight torch in torches)
            {
                if (torch == null) continue;

                // A partição, não torch.Region: torch.Region é a região CRUA do AnnotatedGrid
                // (útil como metadado, mas não é a chave que `colors`/`neighbors` usam agora —
                // eles são indexados por partição fina, não por região).
                int cell = RegionGraph.WorldToCell(grid, torch.Anchor.position);
                if (cell < 0 || !cellToPartition.TryGetValue(cell, out int partition)) continue;

                torch.SetRenderingLayerMask(RegionGraph.LightMask(partition, neighbors, colors));
                stamped++;
            }

            return stamped;
        }

        private uint MaskOf(int partition)
            => colors != null && colors.TryGetValue(partition, out uint mask) ? mask : RegionGraph.DefaultMask;

        /// <summary>
        /// Rocha (Wildcard_SolidFill) é UM Renderer só, com até 4 faces visíveis — cada uma
        /// podendo encostar numa sala diferente. Se desse a união das cores de todas as salas
        /// vizinhas (o comportamento antigo), uma rocha fina espremida entre a sala A (com
        /// tocha) e a sala B (sem nada a ver com ela) saía com a máscara das DUAS — e como não
        /// dá pra iluminar só a face que aponta pra A, a face que aponta pra B acendia junto.
        /// Foi exatamente o vazamento reportado num trecho fino de rocha entre duas salas.
        ///
        /// Por isso: toca só UMA partição → recebe a cor dela normalmente (caso comum, seguro).
        /// Toca 2+ partições DIFERENTES, ou nenhuma → fica sem cor de tocha nenhuma (máscara 0,
        /// não Default — Default está em toda tocha do andar, então usar Default aqui reabriria
        /// o mesmo problema só que pior, com QUALQUER tocha do mapa). Ainda pega o ambiente da
        /// cena (Rendering Layers não bloqueia isso), só não pega luz de tocha específica.
        /// Rocha um pouco mais escura no caso ambíguo nunca incomodou ninguém; brilho sem fonte
        /// visível foi literalmente a queixa que começou toda esta investigação.
        /// </summary>
        private uint UnionOfAdjacent(int cell)
        {
            int found = -1;
            bool ambiguous = false;

            foreach (WFC.Core.Direction dir in WFC.Core.Directions.Horizontal)
            {
                if (!grid.Grid.TryGetNeighbor(cell, dir, out int neighbor)) continue;
                if (!cellToPartition.TryGetValue(neighbor, out int partition)) continue;

                if (found == -1) found = partition;
                else if (partition != found) ambiguous = true;
            }

            if (ambiguous || found < 0) return 0u;

            return MaskOf(found);
        }

        // =====================================================================
        //  Objetos que andam
        // =====================================================================

        private void LateUpdate()
        {
            if (grid == null || dynamicObjects.Count == 0) return;
            if (Time.time < nextUpdate) return;
            nextUpdate = Time.time + updateInterval;

            foreach (Transform target in dynamicObjects)
            {
                if (target == null) continue;

                int cell = RegionGraph.WorldToCell(grid, target.position);
                int partition = cell >= 0 && cellToPartition.TryGetValue(cell, out int p) ? p : -1;

                // Só reescreve quando a partição MUDA: percorrer a hierarquia do jogador a cada
                // 0.15 s para reescrever o mesmo valor seria puro desperdício.
                if (lastPartitionOf.TryGetValue(target, out int previous) && previous == partition) continue;
                lastPartitionOf[target] = partition;

                // Fora de qualquer partição (entre andares, em cima de rocha): volta ao Default,
                // que toda tocha ilumina. Preferível a um personagem preto no meio da tela.
                uint mask = partition >= 0 ? MaskOf(partition) : RegionGraph.DefaultMask;

                rendererBuffer.Clear();
                target.GetComponentsInChildren(true, rendererBuffer);

                foreach (Renderer r in rendererBuffer)
                    if (r != null) r.renderingLayerMask = mask;
            }
        }
    }
}
