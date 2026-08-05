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
//  A terceira regra ("a cada 3 paredes") é a única que este script decide:
//  agrupa os anchors em TRECHOS RETOS contíguos (mesma direção, mesma linha)
//  e acende 1 a cada N dentro de cada trecho. Corner/DeadEnd/Door (sem anchor)
//  quebram o trecho sozinhos — não precisa detectar canto explicitamente.
// ============================================================================

using System.Collections.Generic;
using System.Linq;
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

            Grid3D grid = floor.Grid.Grid;
            int chosen = 0;

            foreach (List<SpawnAnchor> run in GroupIntoStraightRuns(lightAnchors, grid))
            {
                for (int i = startOffset; i < run.Count; i += everyNWalls)
                {
                    SpawnAnchor anchor = run[i];
                    GameObject instance = Instantiate(lightPrefab, anchor.transform.position,
                                                       anchor.transform.rotation, parent);
                    instance.name = $"{lightPrefab.name} (célula {anchor.cellIndex})";
                    spawned.Add(instance);
                    chosen++;
                }
            }

            if (logSummary)
                Debug.Log($"[BasicLightingPopulator] Andar {floor.FloorNumber}: {chosen} luz(es) de " +
                          $"{lightAnchors.Count} anchor(s) candidato(s).", this);

            return chosen;
        }

        public void Clear()
        {
            foreach (GameObject go in spawned)
                if (go != null) Destroy(go);
            spawned.Clear();
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
            var groups = new Dictionary<(bool alongX, int y, int fixedCoord), List<(int coord, SpawnAnchor a)>>();

            foreach (SpawnAnchor anchor in anchors)
            {
                grid.Coords(anchor.cellIndex, out int x, out int y, out int z);

                // Parede voltada para Norte/Sul (forward ~ ±Z) forma trecho ao longo de X;
                // voltada para Leste/Oeste (forward ~ ±X) forma trecho ao longo de Z.
                Vector3 f = anchor.transform.forward;
                bool alongX = Mathf.Abs(f.z) >= Mathf.Abs(f.x);
                int fixedCoord = alongX ? z : x;
                int coord = alongX ? x : z;

                var key = (alongX, y, fixedCoord);
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
