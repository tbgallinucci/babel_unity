// ============================================================================
//  WFCFloorGenerator.cs  —  camada WFC.Runtime
//
//  O gerador de RUNTIME. Orquestra o pipeline inteiro e devolve o contrato de
//  saída (GeneratedFloor) para quem quiser consumir:
//
//      FloorSpec → SkeletonGenerator → ConnectivityValidator
//                → WFCFiller (IFloorFiller) → NavMeshSurface
//
//  Diferença para o WFCFloorPreview: aquele é harness de edição, este é o que
//  roda no jogo. Este aqui destrói o andar anterior, libera a NavMesh, tem
//  versão em corrotina (para a tela de loading) e dispara um evento no fim.
//
//  O que ele NÃO faz, de propósito (Decisão 4): posicionar jogador, spawnar
//  inimigo, contar andar. Ele entrega o casco + papéis + anchors + NavMesh e
//  acabou. Quem monta o jogo em cima disso é o FloorDirector, do lado do jogo.
// ============================================================================

using System;
using System.Collections;
using System.Diagnostics;
using Unity.AI.Navigation;
using UnityEngine;
using WFC.Core;
using WFC.Data;
using Debug = UnityEngine.Debug;

namespace WFC.Runtime
{
    [AddComponentMenu("WFC/WFC Floor Generator")]
    public sealed class WFCFloorGenerator : MonoBehaviour
    {
        [Header("Receita")]
        public FloorSpec floorSpec;

        [Header("NavMesh (Decisão 2)")]
        [Tooltip("Bakeia uma NavMeshSurface por andar. Sem isso nenhum NavMeshAgent funciona.")]
        public bool bakeNavMesh = true;

        [Tooltip("Agent Type ID da NavMesh. 0 = Humanoid (o padrão).")]
        public int agentTypeId;

        [Header("Validação")]
        [Tooltip("Confere entrada→escada antes de instanciar. Barato; deixe ligado.")]
        public bool validateConnectivity = true;

        [Tooltip("Se a conectividade falhar, tenta de novo com seed derivada em vez de devolver andar quebrado.")]
        [Min(1)] public int connectivityRetries = 4;

        [Header("Debug")]
        public bool logTimings = true;

        /// <summary>Andar atualmente em cena. Null antes da primeira geração.</summary>
        public GeneratedFloor Current { get; private set; }

        /// <summary>Disparado ao terminar uma geração bem-sucedida.</summary>
        public event Action<GeneratedFloor> FloorGenerated;

        // =====================================================================
        //  Geração
        // =====================================================================

        /// <summary>
        /// Versão síncrona. Segura a thread principal pelo bake de NavMesh — boa para
        /// teste e para andares pequenos. Na transição de verdade prefira
        /// <see cref="GenerateRoutine"/>.
        /// </summary>
        public GeneratedFloor Generate(int seed, int floorNumber = 1)
        {
            GeneratedFloor floor = null;
            IEnumerator routine = GenerateRoutine(seed, floorNumber, r => floor = r, asyncNavMesh: false);
            while (routine.MoveNext()) { }
            return floor;
        }

        /// <summary>
        /// Versão em corrotina: o bake de NavMesh não trava o frame. É a que a
        /// transição de andar deve usar, junto com a tela de loading.
        /// </summary>
        public IEnumerator GenerateRoutine(int seed, int floorNumber, Action<GeneratedFloor> onDone,
                                           bool asyncNavMesh = true)
        {
            var floor = new GeneratedFloor { Seed = seed, FloorNumber = floorNumber };

            if (floorSpec == null) { Fail(floor, "FloorSpec não atribuído.", onDone); yield break; }
            if (floorSpec.tileSet == null) { Fail(floor, "O FloorSpec não tem TileSet.", onDone); yield break; }
            if (!floorSpec.tileSet.IsBaked)
            {
                Fail(floor, $"TileSet '{floorSpec.tileSet.name}' não está bakeado — " +
                            "rode WFC ▸ Tileset Bootstrap (e antes o Greybox Tile Generator).", onDone);
                yield break;
            }

            Clear();

            floor.TileSet = floorSpec.tileSet;
            Grid3D grid = floorSpec.CreateGrid();

            // ---- 1. Esqueleto (com retry por conectividade) -----------------
            var swSkeleton = Stopwatch.StartNew();
            SkeletonGenerator.Result skeleton = null;
            IRandom rng = null;
            int usedSeed = seed;

            for (int attempt = 0; attempt < Mathf.Max(1, connectivityRetries); attempt++)
            {
                usedSeed = attempt == 0 ? seed : WFCSolver.DeriveSeed(seed, attempt);
                rng = new XorShiftRandom(usedSeed);

                skeleton = new SkeletonGenerator().Generate(
                    grid, floorSpec.cellSize, floorSpec.cellHeight, transform.position,
                    floorSpec.skeleton, rng);

                if (!skeleton.Success) continue;

                if (!validateConnectivity) break;
                if (ConnectivityValidator.IsReachable(skeleton.Grid, skeleton.EntranceCell, skeleton.StairsCell))
                    break;

                // Não deveria acontecer nunca: a MST é conexa por construção. Se cair
                // aqui, é bug no esqueleto — e é melhor gastar uma seed do que entregar
                // um andar sem saída ao jogador.
                Debug.LogWarning($"[WFCFloorGenerator] Andar {floorNumber} sem caminho entrada→escada " +
                                 $"na seed {usedSeed}. Tentando outra.", this);
                skeleton = null;
            }
            swSkeleton.Stop();

            if (skeleton == null || !skeleton.Success)
            {
                Fail(floor, skeleton?.Message ?? "Esqueleto falhou em todas as tentativas.", onDone);
                yield break;
            }

            floor.Seed = usedSeed;
            floor.Skeleton = skeleton;
            floor.Grid = skeleton.Grid;
            floor.SkeletonMilliseconds = swSkeleton.Elapsed.TotalMilliseconds;

            yield return null; // devolve o frame antes da parte pesada

            // ---- 2. Preenchimento (WFC) -------------------------------------
            var filler = new WFCFiller(floorSpec.tileSet, floorSpec, transform);
            FloorFillResult fill = filler.Fill(floor.Grid, rng);

            foreach (string w in filler.Warnings) Debug.LogWarning($"[WFCFloorGenerator] {w}", this);

            if (!fill.Success)
            {
                Fail(floor, fill.Message, onDone);
                yield break;
            }

            floor.Root = fill.Root;
            floor.Variants = fill.Variants;
            floor.Anchors = fill.Anchors;
            floor.SolveMilliseconds = fill.SolveMilliseconds;
            floor.InstantiateMilliseconds = fill.InstantiateMilliseconds;

            floor.EntranceWorld = floor.Grid.CellToWorld(skeleton.EntranceCell);
            floor.StairsWorld = floor.Grid.CellToWorld(skeleton.StairsCell);

            // ---- 3. NavMesh --------------------------------------------------
            if (bakeNavMesh && floor.Root != null)
            {
                var swNav = Stopwatch.StartNew();
                NavMeshSurface surface = NavMeshBuilderService.EnsureSurface(floor.Root, agentTypeId);
                floor.NavMesh = surface;

                if (asyncNavMesh)
                {
                    IEnumerator bake = NavMeshBuilderService.BakeAsync(surface);
                    while (bake.MoveNext()) yield return bake.Current;
                }
                else
                {
                    NavMeshBuilderService.Bake(surface);
                }

                swNav.Stop();
                floor.NavMeshMilliseconds = swNav.Elapsed.TotalMilliseconds;
            }

            floor.Success = true;
            Current = floor;

            if (logTimings) Debug.Log($"[WFCFloorGenerator] {floor}", this);

            FloorGenerated?.Invoke(floor);
            onDone?.Invoke(floor);
        }

        /// <summary>Destrói o andar atual e libera a NavMesh dele.</summary>
        public void Clear()
        {
            if (Current != null)
            {
                NavMeshBuilderService.Release(Current.NavMesh);
                Current = null;
            }

            TileInstancer.ClearGenerated(transform);
        }

        private void OnDestroy() => NavMeshBuilderService.Release(Current?.NavMesh);

        private void Fail(GeneratedFloor floor, string message, Action<GeneratedFloor> onDone)
        {
            floor.Success = false;
            floor.Message = message;
            Debug.LogError($"[WFCFloorGenerator] {floor}", this);
            onDone?.Invoke(floor);
        }
    }
}
