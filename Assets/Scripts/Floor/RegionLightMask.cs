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
//  LIMITE conhecido: isto contém luz entre REGIÕES, não dentro de uma. Um pilar
//  no meio de uma sala grande continua sendo atravessado pelo fill — para isso
//  só a Etapa 3 (grid de luz propagada por flood-fill) ou a sombra das poucas
//  tochas Key, que cobre a vizinhança imediata do jogador.
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

        [SerializeField] private bool logSummary = true;

        private AnnotatedGrid grid;
        private Dictionary<int, List<int>> neighbors;
        private Dictionary<int, uint> colors;

        private readonly List<Renderer> rendererBuffer = new List<Renderer>();
        private readonly Dictionary<Transform, int> lastRegionOf = new Dictionary<Transform, int>();

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
            neighbors = RegionGraph.Build(grid);
            colors = RegionGraph.AssignColors(neighbors);

            var distinct = new HashSet<uint>(colors.Values);
            ColorsUsed = distinct.Count;

            StampGeometry(floor);
            int torches = StampTorches(floor.Root);

            if (logSummary)
                Debug.Log($"[RegionLightMask] Andar {floor.FloorNumber}: {colors.Count} região(ões) " +
                          $"em {ColorsUsed} cor(es), {torches} tocha(s) contida(s).", this);

            nextUpdate = 0f;
        }

        public void Clear()
        {
            grid = null;
            neighbors = null;
            colors = null;
            ColorsUsed = 0;
            lastRegionOf.Clear();
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

                int region = grid.GetRegion(cell);

                // Preenchimento maciço (rocha) não pertence a região nenhuma, mas é parede de
                // quem encosta nele. Recebe a UNIÃO das cores vizinhas, senão ficaria só no
                // Default e apareceria iluminado por tochas de qualquer canto do andar.
                uint mask = region >= 0
                    ? MaskOf(region)
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
                if (torch == null || torch.Region < 0) continue;

                torch.SetRenderingLayerMask(RegionGraph.LightMask(torch.Region, neighbors, colors));
                stamped++;
            }

            return stamped;
        }

        private uint MaskOf(int region)
            => colors != null && colors.TryGetValue(region, out uint mask) ? mask : RegionGraph.DefaultMask;

        private uint UnionOfAdjacent(int cell)
        {
            uint mask = 0u;

            foreach (WFC.Core.Direction dir in WFC.Core.Directions.Horizontal)
            {
                if (!grid.Grid.TryGetNeighbor(cell, dir, out int neighbor)) continue;

                int region = grid.GetRegion(neighbor);
                if (region >= 0) mask |= MaskOf(region);
            }

            // Rocha cercada só de rocha: ninguém a vê, mas deixá-la em 0 significaria
            // "nenhuma luz jamais" — e um dia alguém abre uma passagem ali.
            return mask != 0u ? mask : RegionGraph.DefaultMask;
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
                int region = cell >= 0 ? grid.GetRegion(cell) : -1;

                // Só reescreve quando a região MUDA: percorrer a hierarquia do jogador a cada
                // 0.15 s para reescrever o mesmo valor seria puro desperdício.
                if (lastRegionOf.TryGetValue(target, out int previous) && previous == region) continue;
                lastRegionOf[target] = region;

                // Fora de qualquer região (entre andares, em cima de rocha): volta ao Default,
                // que toda tocha ilumina. Preferível a um personagem preto no meio da tela.
                uint mask = region >= 0 ? MaskOf(region) : RegionGraph.DefaultMask;

                rendererBuffer.Clear();
                target.GetComponentsInChildren(true, rendererBuffer);

                foreach (Renderer r in rendererBuffer)
                    if (r != null) r.renderingLayerMask = mask;
            }
        }
    }
}
