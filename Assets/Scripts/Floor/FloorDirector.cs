// ============================================================================
//  FloorDirector.cs  —  código do JOGO
//
//  O loop da torre: gera o andar, põe o jogador na entrada, popula os inimigos,
//  crava a escada. Interagir com a escada incrementa o andar e recomeça.
//
//  É AQUI que mora tudo que o plugin de geração não pode saber (Decisão 4):
//  o que é um jogador, o que é um inimigo, quantos andares tem a torre. O
//  gerador entrega casco + papéis + anchors + NavMesh; o resto é jogo.
//
//  O jogador é MOVIDO, nunca reinstanciado — num roguelite ele carrega vida,
//  equipamento e estado de animação entre andares.
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using WFC.Core;
using WFC.Runtime;

namespace Babel.Floor
{
    [AddComponentMenu("Babel/Floor Director")]
    public sealed class FloorDirector : MonoBehaviour
    {
        [Header("Referências")]
        [SerializeField] private WFCFloorGenerator generator;

        [Tooltip("O Player da cena. Ele é teleportado entre andares, não recriado.")]
        [SerializeField] private Transform player;

        [Tooltip("Opcional. Sem ele, os andares saem vazios.")]
        [SerializeField] private EnemyPopulator populator;

        [Tooltip("Opcional. Sem ele, as paredes saem sem luminária nenhuma.")]
        [SerializeField] private BasicLightingPopulator lightingPopulator;

        [Tooltip("Opcional. Sem ele, salas de Combate saem sem prop nenhum.")]
        [SerializeField] private PropRoomPopulator propPopulator;

        [Tooltip("Opcional. Limita quantas luzes realtime ficam ligadas ao mesmo tempo.")]
        [SerializeField] private DynamicLightBudget lightBudget;

        [Tooltip("Opcional. Desliga a geometria das salas que o jogador não pode ver " +
                 "(substituto do Occlusion Culling, que não funciona em andar gerado em runtime).")]
        [SerializeField] private RoomStreamer roomStreamer;

        [Tooltip("Opcional. Impede que a luz de uma sala atravesse a parede e ilumine a sala " +
                 "vizinha. Sem ele as tochas sem sombra (a maioria) vazam entre salas.")]
        [SerializeField] private RegionLightMask regionLightMask;

        [Header("Run")]
        [Min(1)] [SerializeField] private int startingFloor = 1;

        [Tooltip("Sorteia a seed da run ao iniciar. Desligue para repetir uma run inteira.")]
        [SerializeField] private bool randomRunSeed = true;

        [SerializeField] private int runSeed = 12345;

        [Tooltip("Gera o primeiro andar automaticamente no Start.")]
        [SerializeField] private bool generateOnStart = true;

        [Header("Jogador")]
        [Tooltip("Altura acima do piso ao aparecer, para não nascer dentro da laje.")]
        [SerializeField] private float spawnHeightOffset = 0.1f;

        [Tooltip("Avisa a Cinemachine do teleporte, para a câmera cortar em vez de deslizar pelo mapa.")]
        [SerializeField] private bool warpCamera = true;

        [Header("Escada")]
        [Tooltip("Opcional: prefab do interagível da escada. Vazio = criado por código.")]
        [SerializeField] private GameObject stairsPrefab;

        [Min(0.5f)] [SerializeField] private float stairsTriggerRadius = 2.5f;

        [Tooltip("Trava a escada até todos os inimigos do andar morrerem.")]
        [SerializeField] private bool requireFloorCleared;

        // ---------------------------------------------------------------- estado
        public int CurrentFloor { get; private set; }
        public bool IsGenerating { get; private set; }
        public int LivingEnemyCount => populator != null ? populator.LivingCount : 0;
        public GeneratedFloor Floor => generator != null ? generator.Current : null;

        private void Reset()
        {
            generator = GetComponent<WFCFloorGenerator>();
            populator = GetComponent<EnemyPopulator>();
            lightingPopulator = GetComponent<BasicLightingPopulator>();
            propPopulator = GetComponent<PropRoomPopulator>();
            lightBudget = GetComponent<DynamicLightBudget>();
            roomStreamer = GetComponent<RoomStreamer>();
            regionLightMask = GetComponent<RegionLightMask>();
        }

        private void Start()
        {
            if (randomRunSeed) runSeed = Random.Range(int.MinValue, int.MaxValue);
            if (generateOnStart) StartCoroutine(GoToFloorRoutine(startingFloor));
        }

        // =====================================================================
        //  Transição
        // =====================================================================

        /// <summary>Chamado pela escada. Ignora repetição enquanto uma geração está em curso.</summary>
        public void RequestNextFloor()
        {
            if (IsGenerating) return;
            StartCoroutine(GoToFloorRoutine(CurrentFloor + 1));
        }

        /// <summary>Recomeça a run do andar inicial, com seed nova.</summary>
        [ContextMenu("Reiniciar run")]
        public void RestartRun()
        {
            if (IsGenerating) return;
            runSeed = Random.Range(int.MinValue, int.MaxValue);
            StartCoroutine(GoToFloorRoutine(startingFloor));
        }

        [ContextMenu("Regerar andar atual")]
        public void RegenerateCurrentFloor()
        {
            if (IsGenerating) return;
            StartCoroutine(GoToFloorRoutine(Mathf.Max(1, CurrentFloor)));
        }

        private IEnumerator GoToFloorRoutine(int floorNumber)
        {
            if (generator == null)
            {
                Debug.LogError("[FloorDirector] WFCFloorGenerator não atribuído.", this);
                yield break;
            }

            IsGenerating = true;

            // Limpa antes de gerar: os inimigos do andar velho não podem sobreviver à
            // troca, e o NavMesh antigo precisa sair de cena.
            if (populator != null) populator.Clear();
            if (lightingPopulator != null) lightingPopulator.Clear();
            if (propPopulator != null) propPopulator.Clear();

            // Antes de gerar o próximo andar: soltam as referências pro andar velho, que
            // está prestes a ser destruído.
            if (lightBudget != null) lightBudget.Clear();
            if (roomStreamer != null) roomStreamer.Clear();

            // Seed por andar derivada da seed da run: a run inteira é reproduzível a
            // partir de um número só, e o andar 7 é sempre o mesmo andar 7.
            int floorSeed = WFCSolver.DeriveSeed(runSeed, floorNumber);

            GeneratedFloor floor = null;
            yield return generator.GenerateRoutine(floorSeed, floorNumber, r => floor = r);

            if (floor == null || !floor.Success)
            {
                Debug.LogError($"[FloorDirector] Falha ao gerar o andar {floorNumber}: {floor?.Message}", this);
                IsGenerating = false;
                yield break;
            }

            CurrentFloor = floorNumber;

            PlacePlayer(floor);
            SpawnStairs(floor);

            // Índice 2: o EnemyPopulator usa 1 (abaixo). Cada consumidor de RNG precisa da
            // sua própria seed derivada, senão os sorteios de inimigo e de prop colidiriam.
            // O sorteio de arquétipo por sala roda ANTES do EnemyPopulator de propósito —
            // é o que permite RoomArchetype.allowEnemies/enemyDensityMultiplier valerem
            // (loja sem monstro, santuário com menos inimigo etc.) sem os dois populadores
            // se conhecerem: o FloorDirector é quem faz a ponte.
            var propRng = new XorShiftRandom(WFCSolver.DeriveSeed(floorSeed, 2));
            Dictionary<SkeletonGenerator.Room, RoomArchetype> archetypeByRoom = null;
            if (propPopulator != null)
                archetypeByRoom = propPopulator.RollArchetypes(floor, propRng);

            if (populator != null)
                populator.Populate(floor, new XorShiftRandom(WFCSolver.DeriveSeed(floorSeed, 1)), floor.Root, archetypeByRoom);
            else
                Debug.LogWarning("[FloorDirector] Campo 'Enemy Populator' vazio no Inspector — " +
                                 "o andar sai sem inimigos. Arraste o componente EnemyPopulator aqui.", this);

            // Determinístico por geometria, não por sorteio — não precisa de seed derivada
            // própria (ver comentário no BasicLightingPopulator).
            if (lightingPopulator != null)
                lightingPopulator.Populate(floor, floor.Root);

            if (propPopulator != null)
                propPopulator.Populate(floor, archetypeByRoom, propRng, floor.Root);

            // Performance e contenção de luz, por último: os três indexam o que JÁ existe em
            // cena, então precisam rodar depois de toda a população (senão tocha/prop plantados
            // agora ficariam de fora do orçamento, do streaming e da máscara).
            //
            // A máscara vem ANTES do orçamento de propósito: ela carimba as tochas, e o
            // orçamento é quem as apaga. Na ordem inversa, o carimbo reacenderia nada — mas
            // ficaria dependendo de o Apply() do orçamento rodar de novo para valer.
            if (regionLightMask != null) regionLightMask.Rebuild(floor, player);
            if (lightBudget != null) lightBudget.Rebuild(floor.Root, player);
            if (roomStreamer != null) roomStreamer.Rebuild(floor, player);

            IsGenerating = false;

            Debug.Log($"[FloorDirector] Andar {CurrentFloor} pronto — {floor.Rooms.Count} salas, " +
                      $"{LivingEnemyCount} inimigos, {floor.TotalMilliseconds:F0} ms.", this);
        }

        // =====================================================================
        //  Peças do andar
        // =====================================================================

        private void PlacePlayer(GeneratedFloor floor)
        {
            if (player == null) return;

            Vector3 target = floor.EntranceWorld + Vector3.up * spawnHeightOffset;
            Vector3 delta = target - player.position;

            // O CharacterController sobrescreve transform.position no mesmo frame se
            // continuar habilitado — daí o desliga/liga em volta do teleporte.
            var controller = player.GetComponent<CharacterController>();
            bool hadController = controller != null && controller.enabled;

            if (hadController) controller.enabled = false;
            player.position = target;
            if (hadController) controller.enabled = true;

            // Sem isto a câmera atravessa o mapa inteiro voando até o novo andar.
            if (warpCamera) CinemachineCore.OnTargetObjectWarped(player, delta);
        }

        private void SpawnStairs(GeneratedFloor floor)
        {
            Vector3 position = floor.StairsWorld;
            GameObject go;

            if (stairsPrefab != null)
            {
                go = Instantiate(stairsPrefab, position, Quaternion.identity, floor.Root);
            }
            else
            {
                go = new GameObject("StairsTrigger");
                go.transform.SetParent(floor.Root, false);
                go.transform.position = position;
            }

            var trigger = go.GetComponent<SphereCollider>();
            if (trigger == null) trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = stairsTriggerRadius;
            trigger.center = Vector3.up;

            var stairs = go.GetComponent<StairsInteractable>();
            if (stairs == null) stairs = go.AddComponent<StairsInteractable>();
            stairs.Bind(this, requireFloorCleared);
        }

        // =====================================================================
        //  Debug
        // =====================================================================
        private void OnDrawGizmosSelected()
        {
            GeneratedFloor floor = Floor;
            if (floor == null || !floor.Success) return;

            Gizmos.color = new Color(0.3f, 1f, 0.4f);
            Gizmos.DrawWireSphere(floor.EntranceWorld + Vector3.up, 1.5f);

            Gizmos.color = new Color(0.3f, 0.6f, 1f);
            Gizmos.DrawWireSphere(floor.StairsWorld + Vector3.up, 1.5f);
        }
    }
}
