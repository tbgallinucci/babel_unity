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

            if (populator != null)
                populator.Populate(floor, new XorShiftRandom(WFCSolver.DeriveSeed(floorSeed, 1)), floor.Root);

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
