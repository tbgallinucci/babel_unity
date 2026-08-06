// ============================================================================
//  ArtPreviewPopulator.cs  —  código do JOGO (Decisão 4: fora do plugin)
//
//  Harness de REVISÃO DE ARTE: gera um andar com o WFCFloorPreview (o harness
//  do plugin) e por cima popula tocha + prop, reaproveitando exatamente os
//  mesmos componentes que o FloorDirector usa em runtime — nenhuma lógica de
//  luz/prop é duplicada aqui.
//
//  Por que não é o WFCFloorPreview que faz isso: ele mora em WFC.Runtime, o
//  PLUGIN, e a regra do projeto (ver cabeçalho de qualquer script "código do
//  JOGO") é mão única — o jogo referencia o plugin, o plugin nunca referencia
//  o jogo. BasicLightingPopulator, RegionLightMask e PropRoomPopulator são
//  código de jogo. Por isso este componente existe: ele lê o pouco de estado
//  que o preview expõe publicamente (Grid, Anchors, Root, PiecesByCell, Seed)
//  e monta um GeneratedFloor — o mesmo contrato que o WFCFloorGenerator de
//  produção entrega — só que a partir do harness de editor.
//
//  DE PROPÓSITO NÃO chama DynamicLightBudget nem RoomStreamer: os dois são
//  sistemas de PERFORMANCE que cortam/escondem coisa baseado em distância do
//  jogador — exatamente o oposto do que uma revisão de arte quer, que é ver
//  TUDO como foi autorado. Sem o budget, toda tocha fica exatamente no estado
//  do prefab (Spot com sombra, Point sem sombra) — não precisa forçar tier
//  nenhum na mão.
//
//  Roda inteiro em Edit Mode (nada aqui depende de Update/LateUpdate) — o
//  resultado é geometria de cena de verdade, que sobrevive a File → Save.
// ============================================================================

using UnityEngine;
using WFC.Core;
using WFC.Runtime;

namespace Babel.Floor
{
    [AddComponentMenu("Babel/Art Preview Populator")]
    public sealed class ArtPreviewPopulator : MonoBehaviour
    {
        [Tooltip("O harness que gera o casco (paredes/piso). Precisa já ter rodado \"Gerar\" com sucesso.")]
        [SerializeField] private WFCFloorPreview preview;

        [Tooltip("Planta as tochas nos anchors de parede. Vazio = andar sai sem luz nenhuma.")]
        [SerializeField] private BasicLightingPopulator lightingPopulator;

        [Tooltip("Opcional. Contém a luz de cada tocha na própria sala (rendering layers), " +
                 "igual em produção — sem isto o preview mostra luz vazando de sala pra sala.")]
        [SerializeField] private RegionLightMask regionLightMask;

        [Tooltip("Opcional. Etapa 3: propaga a luz de cada tocha pelo grid e soma como " +
                 "ambiente de tela cheia — igual em produção.")]
        [SerializeField] private FloorLightField lightField;

        [Tooltip("Opcional. Sorteia arquétipo por sala de Combate e instancia os props. " +
                 "Vazio = preview sai só com o casco e a luz.")]
        [SerializeField] private PropRoomPopulator propPopulator;

        [Tooltip("Índice de derivação de seed para o RNG de props — mesmo valor que o " +
                 "FloorDirector usa (índice 2), pra reproduzir a mesma disposição de props " +
                 "que o andar real teria com esta seed.")]
        [SerializeField] private int propSeedIndex = 2;

        [SerializeField] private bool logSummary = true;

        [ContextMenu("Popular arte (luz + props)")]
        public void PopulateArt()
        {
            if (preview == null) { Debug.LogError("[ArtPreviewPopulator] 'Preview' vazio.", this); return; }
            if (!preview.LastSuccess)
            {
                Debug.LogError("[ArtPreviewPopulator] O WFCFloorPreview não tem um andar gerado com " +
                               "sucesso — clique em \"Gerar\" nele primeiro (ou use o botão combinado " +
                               "no Inspector deste componente).", this);
                return;
            }

            if (preview.GeneratedRoot == null)
            {
                Debug.LogError("[ArtPreviewPopulator] O preview não instanciou geometria " +
                               "('Instantiate Prefabs' estava desligado nele) — não há onde plantar luz/prop.", this);
                return;
            }

            GeneratedFloor floor = BuildFloorView();

            var propRng = new XorShiftRandom(WFCSolver.DeriveSeed(floor.Seed, propSeedIndex));
            System.Collections.Generic.Dictionary<SkeletonGenerator.Room, RoomArchetype> archetypeByRoom = null;

            // Mesma ordem do FloorDirector: arquétipo sorteado ANTES da luz (não que a luz
            // dependa disso — é só pra manter os dois harnesses lendo igual, caso um dia
            // BasicLightingPopulator ganhe alguma regra por arquétipo).
            if (propPopulator != null)
                archetypeByRoom = propPopulator.RollArchetypes(floor, propRng);

            int torches = lightingPopulator != null ? lightingPopulator.Populate(floor, floor.Root) : 0;

            // Precisa que as tochas já existam (StampTorches/Rebuild percorrem floor.Root
            // procurando TorchLight). Sem DynamicLightBudget neste harness, a ordem entre os
            // dois abaixo é livre — nenhum zera a intensidade da luz como o budget faria.
            if (regionLightMask != null) regionLightMask.Rebuild(floor);
            if (lightField != null) lightField.Rebuild(floor);

            int props = 0;
            if (propPopulator != null && archetypeByRoom != null)
                props = propPopulator.Populate(floor, archetypeByRoom, propRng, floor.Root);

            if (logSummary)
                Debug.Log($"[ArtPreviewPopulator] {torches} tocha(s), {props} prop(s) " +
                          $"({(archetypeByRoom?.Count ?? 0)} sala(s) com arquétipo). " +
                          $"Sem DynamicLightBudget/RoomStreamer — tudo visível, como autorado.", this);
        }

        /// <summary>Desfaz o que este componente plantou, sem tocar no casco do preview.</summary>
        [ContextMenu("Limpar luz e props")]
        public void ClearArt()
        {
            if (lightingPopulator != null) lightingPopulator.Clear();
            if (propPopulator != null) propPopulator.Clear();
            if (regionLightMask != null) regionLightMask.Clear();
            if (lightField != null) lightField.Clear();
        }

        /// <summary>
        /// Monta o mesmo contrato que WFCFloorGenerator entrega em produção, a partir do
        /// pouco de estado que o harness de editor expõe. Só preenche os campos que os
        /// populadores desta revisão realmente leem — ver os cabeçalhos de cada um.
        /// </summary>
        private GeneratedFloor BuildFloorView()
        {
            SkeletonGenerator.Result skeleton = preview.LastSkeleton;
            AnnotatedGrid grid = preview.LastGrid;

            var floor = new GeneratedFloor
            {
                Success = true,
                FloorNumber = 0,
                Seed = preview.LastSeed,
                Root = preview.GeneratedRoot,
                Grid = grid,
                Skeleton = skeleton,
                Anchors = preview.Anchors,
                PiecesByCell = preview.PiecesByCell,
            };

            // Só usado pelo PropRoomPopulator (raio de segurança em volta da entrada). Sem
            // entrada válida no esqueleto, cai em Vector3.zero — o raio de segurança erra o
            // alvo, mas nada quebra: pior caso é um prop nascendo perto de onde a entrada
            // "seria" se o grid começasse na origem.
            floor.EntranceWorld = skeleton != null && skeleton.EntranceCell >= 0
                ? grid.CellToWorld(skeleton.EntranceCell)
                : Vector3.zero;

            return floor;
        }
    }
}
