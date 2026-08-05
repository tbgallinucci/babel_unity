// ============================================================================
//  BasicLightingPopulator.cs  —  código do JOGO (Decisão 4: fora do plugin)
//
//  Popula luz de parede no andar já gerado. Vale para QUALQUER RoomRole —
//  sala ou corredor —, ao contrário do EnemyPopulator (só Combate): a regra
//  aqui é de GEOMETRIA, não de papel.
//
//  Consome os SpawnAnchor(kind = Light) que o Greybox Tile Generator planta
//  nas peças de parede reta (Tile_Wall, Tile_Corridor, Tile_Door_Corridor).
//  Duas das três regras pedidas vêm de GRAÇA por causa de qual peça ganha
//  anchor:
//
//    • "nunca em canto/quina"        → Tile_Corner não tem anchor nenhum.
//    • "só parede única ou paralela" → só Tile_Wall (1 parede) e
//                                       Tile_Corridor/Tile_Door_Corridor
//                                       (2 paredes paralelas) têm anchor;
//                                       Tile_DeadEnd (3 paredes, perpendiculares
//                                       misturadas) foi deixado de fora de
//                                       propósito.
//
//  As outras duas regras são deste script:
//
//    • "a cada 3 paredes"     → agrupa os anchors em TRECHOS RETOS contíguos
//                               (mesmo eixo, mesmo SENTIDO, mesma linha) e
//                               acende 1 a cada N dentro de cada trecho.
//                               Corner/DeadEnd/Door (sem anchor) quebram o
//                               trecho sozinhos — não precisa detectar canto.
//    • "pular paredes em ângulo" → `oneAxisPerRoom`: dentro de uma sala, só o
//                               eixo com mais parede reta acende. Como as duas
//                               paredes OPOSTAS de uma sala compartilham o
//                               mesmo eixo, sai um par aceso e as outras duas
//                               no escuro, sem precisar comparar posições.
//                               Corredores ficam fora da regra (toda a malha
//                               deles é UMA região — escolher um eixo apagaria
//                               metade dos corredores do andar).
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using Babel.VFX;
using UnityEngine;
using WFC.Core;
using WFC.Runtime;

namespace Babel.Floor
{
    [AddComponentMenu("Babel/Basic Lighting Populator")]
    public sealed class BasicLightingPopulator : MonoBehaviour
    {
        [Tooltip("Prefab da luminária (Light + malha opcional). Instanciado na posição/rotação do anchor.")]
        [SerializeField] private GameObject lightPrefab;

        [Tooltip("Acende 1 a cada N anchors dentro do mesmo trecho reto de parede.")]
        [Min(1)] [SerializeField] private int everyNWalls = 3;

        [Tooltip("Deslocamento a partir do início do trecho antes da 1ª luz (0 = primeira parede do trecho).")]
        [Min(0)] [SerializeField] private int startOffset = 0;

        [Tooltip("Dentro de uma SALA, acende só as paredes de UM eixo — o resultado é um par de " +
                 "paredes opostas iluminado e as outras duas no escuro, em vez de luz nos quatro lados. " +
                 "Escolhe o eixo com mais parede reta (o lado mais comprido da sala). Não vale para " +
                 "corredores: lá os dois lados continuam podendo acender, senão metade dos corredores " +
                 "ficaria sem luz nenhuma (toda a malha de corredores é UMA região só).")]
        [SerializeField] private bool oneAxisPerRoom = true;

        [SerializeField] private bool logSummary = true;

        private readonly List<GameObject> spawned = new List<GameObject>();

        /// <summary>
        /// Popula as luzes do andar atual. Independente de seed/rng — a escolha de QUAIS
        /// anchors acendem é determinística a partir da geometria (mesmo andar = mesmas
        /// luzes sempre), não precisa consumir número nenhum do fluxo de seed.
        /// </summary>
        public int Populate(GeneratedFloor floor, Transform parent)
        {
            Clear();

            if (floor == null || !floor.Success) return 0;
            if (lightPrefab == null)
            {
                Debug.LogWarning("[BasicLightingPopulator] 'Light Prefab' vazio — nenhuma luz nasce.", this);
                return 0;
            }

            List<SpawnAnchor> lightAnchors = floor.Anchors
                .Where(a => a != null && a.kind == SpawnAnchorKind.Light)
                .ToList();

            if (lightAnchors.Count == 0)
            {
                if (logSummary)
                    Debug.Log("[BasicLightingPopulator] Nenhum anchor de luz no kit — " +
                              "tileset sem Tile_Wall/Tile_Corridor com anchor, ou andar sem parede reta.", this);
                return 0;
            }

            AnnotatedGrid annotated = floor.Grid;
            Grid3D grid = annotated.Grid;
            int chosen = 0;
            int missingTorchComponent = 0;

            int beforeAxisFilter = lightAnchors.Count;
            if (oneAxisPerRoom) lightAnchors = FilterOneAxisPerRoom(lightAnchors, annotated);

            foreach (List<SpawnAnchor> run in GroupIntoStraightRuns(lightAnchors, grid))
            {
                for (int i = startOffset; i < run.Count; i += everyNWalls)
                {
                    SpawnAnchor anchor = run[i];
                    GameObject instance = Instantiate(lightPrefab, anchor.transform.position,
                                                       anchor.transform.rotation, parent);
                    instance.name = $"{lightPrefab.name} (célula {anchor.cellIndex})";

                    // Em que sala/corredor esta tocha nasceu. É a única informação que o
                    // RegionLightMask não consegue redescobrir depois: a POSIÇÃO da tocha fica
                    // 1 m à frente da parede, e uma parede entre duas regiões deixa esse ponto
                    // ambíguo. A célula do anchor não é ambígua.
                    var torch = instance.GetComponentInChildren<TorchLight>(true);
                    if (torch != null) torch.Region = annotated.GetRegion(anchor.cellIndex);
                    else if (missingTorchComponent++ == 0)
                        Debug.LogWarning($"[BasicLightingPopulator] '{lightPrefab.name}' não tem o componente " +
                                         "TorchLight — sem ele a tocha fica fora do orçamento de luz e da " +
                                         "contenção por sala (acesa sempre, com sombra sempre, atravessando " +
                                         "parede). Adicione o componente ao prefab.", this);

                    spawned.Add(instance);
                    chosen++;
                }
            }

            if (logSummary)
                Debug.Log($"[BasicLightingPopulator] Andar {floor.FloorNumber}: {chosen} luz(es) de " +
                          $"{lightAnchors.Count} anchor(s) candidato(s) " +
                          $"({beforeAxisFilter} antes da regra de eixo por sala).", this);

            return chosen;
        }

        public void Clear()
        {
            foreach (GameObject go in spawned)
                if (go != null) Destroy(go);
            spawned.Clear();
        }

        /// <summary>Parede corre ao longo de X (faces ±Z) ou ao longo de Z (faces ±X)?</summary>
        private static bool AlongX(Vector3 forward) => Mathf.Abs(forward.z) >= Mathf.Abs(forward.x);

        /// <summary>Sentido da face ao longo do eixo dominante: +1 ou -1.</summary>
        private static int Facing(Vector3 forward, bool alongX)
            => alongX ? (forward.z >= 0f ? 1 : -1) : (forward.x >= 0f ? 1 : -1);

        // -------------------------------------------------------- eixo por sala
        /// <summary>
        /// Dentro de cada SALA, mantém só os anchors de um eixo — o que tiver mais parede reta
        /// (o lado mais comprido). Como as duas paredes opostas de uma sala compartilham o
        /// mesmo eixo, o resultado é exatamente "duas paredes opostas acesas, as outras duas
        /// no escuro", sem precisar detectar canto: quem decide é a orientação, não a posição.
        ///
        /// Corredores (região única, compartilhada por toda a malha) ficam de fora da regra:
        /// escolher um eixo só para eles apagaria metade dos corredores do andar de uma vez.
        /// </summary>
        private static List<SpawnAnchor> FilterOneAxisPerRoom(List<SpawnAnchor> anchors, AnnotatedGrid grid)
        {
            var countByRegionAxis = new Dictionary<(int region, bool alongX), int>();

            foreach (SpawnAnchor anchor in anchors)
            {
                int region = grid.GetRegion(anchor.cellIndex);
                if (region < AnnotatedGrid.FirstRoomRegion) continue;

                var key = (region, AlongX(anchor.transform.forward));
                countByRegionAxis.TryGetValue(key, out int n);
                countByRegionAxis[key] = n + 1;
            }

            // Eixo vencedor por sala. Empate cai em alongX, por consistência entre execuções
            // (o resultado precisa ser função só da geometria — mesma seed, mesmas luzes).
            var axisByRegion = new Dictionary<int, bool>();
            foreach (KeyValuePair<(int region, bool alongX), int> pair in countByRegionAxis)
            {
                int region = pair.Key.region;
                if (!axisByRegion.TryGetValue(region, out bool currentAxis))
                {
                    axisByRegion[region] = pair.Key.alongX;
                    continue;
                }

                countByRegionAxis.TryGetValue((region, currentAxis), out int currentCount);
                if (pair.Value > currentCount || (pair.Value == currentCount && pair.Key.alongX))
                    axisByRegion[region] = pair.Key.alongX;
            }

            var kept = new List<SpawnAnchor>(anchors.Count);
            foreach (SpawnAnchor anchor in anchors)
            {
                int region = grid.GetRegion(anchor.cellIndex);
                if (region < AnnotatedGrid.FirstRoomRegion) { kept.Add(anchor); continue; }
                if (!axisByRegion.TryGetValue(region, out bool axis)) { kept.Add(anchor); continue; }
                if (AlongX(anchor.transform.forward) == axis) kept.Add(anchor);
            }

            return kept;
        }

        // ------------------------------------------------------------ agrupamento
        /// <summary>
        /// Agrupa anchors em trechos retos contíguos: mesma direção de parede (aproximada
        /// pelo eixo cardinal mais próximo do <c>forward</c> do anchor, já rotacionado
        /// junto com a peça), mesma "linha" perpendicular, ordenados ao longo do trecho.
        /// Uma lacuna na sequência (célula sem anchor — canto, beco, porta) fecha o
        /// trecho ali; o próximo anchor começa um trecho novo.
        /// </summary>
        private static IEnumerable<List<SpawnAnchor>> GroupIntoStraightRuns(List<SpawnAnchor> anchors, Grid3D grid)
        {
            var groups = new Dictionary<(bool alongX, int facing, int y, int fixedCoord),
                                        List<(int coord, SpawnAnchor a)>>();

            foreach (SpawnAnchor anchor in anchors)
            {
                grid.Coords(anchor.cellIndex, out int x, out int y, out int z);

                // Parede voltada para Norte/Sul (forward ~ ±Z) forma trecho ao longo de X;
                // voltada para Leste/Oeste (forward ~ ±X) forma trecho ao longo de Z.
                //
                // `facing` (o SENTIDO, não só o eixo) faz parte da chave porque uma peça de
                // corredor tem DOIS anchors na MESMA célula, um de cada lado. Sem isso os dois
                // caíam no mesmo grupo com o mesmo `coord`, a detecção de trecho contíguo
                // (coord == prevCoord, não prevCoord+1) quebrava o trecho a cada anchor, e como
                // todo trecho sempre acende o índice 0, acabava acendendo parede nenhuma —
                // acendia TODAS.
                Vector3 f = anchor.transform.forward;
                bool alongX = AlongX(f);
                int facing = Facing(f, alongX);
                int fixedCoord = alongX ? z : x;
                int coord = alongX ? x : z;

                var key = (alongX, facing, y, fixedCoord);
                if (!groups.TryGetValue(key, out var list))
                {
                    list = new List<(int, SpawnAnchor)>();
                    groups[key] = list;
                }
                list.Add((coord, anchor));
            }

            foreach (var list in groups.Values)
            {
                list.Sort((a, b) => a.coord.CompareTo(b.coord));

                var run = new List<SpawnAnchor>();
                int? prevCoord = null;

                foreach ((int coord, SpawnAnchor anchor) in list)
                {
                    if (prevCoord.HasValue && coord != prevCoord.Value + 1)
                    {
                        if (run.Count > 0) yield return run;
                        run = new List<SpawnAnchor>();
                    }

                    run.Add(anchor);
                    prevCoord = coord;
                }

                if (run.Count > 0) yield return run;
            }
        }
    }
}
