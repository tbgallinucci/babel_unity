// ============================================================================
//  DualGridWallPreview.cs  —  camada WFC.Runtime
//
//  Harness ISOLADO pro tileset de grid dual. Roda só:
//
//      FloorSpec → SkeletonGenerator → DualGridWallBuilder
//
//  Sem WFC, sem TileSet, sem sockets. O esqueleto já decide todos os rótulos de
//  borda, e a colocação de parede no grid dual é determinística a partir deles —
//  então não há nada pro solver fazer nesta etapa.
//
//  POR QUE ISTO É SEPARADO do WFCFloorPreview/WFCFloorGenerator: o objetivo aqui
//  é responder UMA pergunta — "as quinas fecham?" — sem risco pro andar que o
//  jogo gera hoje. Nada deste arquivo é usado pelo pipeline de produção; quando
//  o kit novo estiver aprovado, a integração é que muda o pipeline.
//
//  COMO USAR
//   1. GameObject vazio na cena → Add Component → WFC ▸ Dual Grid Wall Preview.
//   2. Aponte o FloorSpec (ex.: FloorSpec_CenterWall — ele NÃO é modificado;
//      só se lê grid/cellSize/seed/esqueleto dele).
//   3. Arraste as 5 peças de Assets/WFC/CenterWallTiles/ nos slots.
//   4. (opcional) Aponte um prefab de piso — Tile_Floor_Open serve — pra ter
//      chão de referência e conseguir julgar o visual de dentro.
//   5. Botão direito no componente → "Gerar".
//
//  ATENÇÃO ao Cell Size: as peças são geradas com um S fixo (janela do Center
//  Wall Tile Generator) e o braço mede S/2. Se o FloorSpec usar outro cellSize,
//  as peças não encostam. O Generate() avisa quando os dois não batem.
// ============================================================================

using UnityEngine;
using WFC.Core;
using WFC.Data;

namespace WFC.Runtime
{
    [ExecuteAlways]
    [AddComponentMenu("WFC/Dual Grid Wall Preview (demo)")]
    public sealed class DualGridWallPreview : MonoBehaviour
    {
        [Header("Receita")]
        [Tooltip("Só é LIDO — grid, cellSize, seed e ajustes de esqueleto. O asset não é modificado.")]
        public FloorSpec floorSpec;

        [Header("Peças de parede (Assets/WFC/CenterWallTiles)")]
        public WallPieceSet pieces;

        [Header("Piso (opcional, só pra enxergar)")]
        [Tooltip("Instanciado no centro de cada célula jogável, só como referência visual pra dar " +
                 "chão e conseguir julgar as quinas de dentro. Use um prefab que seja SÓ laje: " +
                 "Tile_Floor_Open serve (não tem parede nenhuma), mas as outras Tile_* trazem as " +
                 "paredes do kit antigo junto e poluem a leitura. Vazio = sem chão.")]
        public GameObject floorPrefab;

        [Header("Seed")]
        [Tooltip("Ligado: usa a regra de seed do próprio FloorSpec. Desligado: usa o campo abaixo, " +
                 "pra comparar a MESMA planta antes/depois de mexer nas peças.")]
        public bool useSpecSeed;

        public int seed = 12345;

        [Header("Último resultado (leitura)")]
        [SerializeField] private string lastStatus = "(nada gerado ainda)";

        public string LastStatus => lastStatus;

        [ContextMenu("Gerar")]
        public void Generate()
        {
            DualGridWallBuilder.ClearGenerated(transform);

            if (floorSpec == null) { Report("FloorSpec não atribuído."); return; }
            if (!pieces.IsComplete)
            {
                Report("Faltam peças: preencha os 5 slots (End, Straight, Corner, Tee, Cross). " +
                       "Rode WFC ▸ Center Wall Tile Generator se a pasta estiver vazia.");
                return;
            }

            int usedSeed = useSpecSeed ? floorSpec.ResolveSeed() : seed;
            var rng = new XorShiftRandom(usedSeed);
            Grid3D grid = floorSpec.CreateGrid();

            SkeletonGenerator.Result skeleton = new SkeletonGenerator().Generate(
                grid, floorSpec.cellSize, floorSpec.cellHeight, transform.position,
                floorSpec.skeleton, rng);

            if (!skeleton.Success)
            {
                Report($"Esqueleto falhou — {skeleton.Message}");
                return;
            }

            var root = new GameObject(DualGridWallBuilder.GeneratedRootName);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;

            int walls = DualGridWallBuilder.Build(root.transform, transform, skeleton.Grid, pieces);
            int floors = floorPrefab != null ? BuildFloors(root.transform, skeleton.Grid) : 0;

            Report($"OK — {skeleton.Rooms.Count} salas, {walls} peça(s) de parede" +
                   (floorPrefab != null ? $", {floors} laje(s)" : "") +
                   $", seed {usedSeed}." +
                   (skeleton.DoorCount > 0
                       ? $" {skeleton.DoorCount} porta(s) saíram como vão SEM batente (limite conhecido da v1)."
                       : ""));
        }

        [ContextMenu("Limpar")]
        public void ClearGenerated() => DualGridWallBuilder.ClearGenerated(transform);

        private int BuildFloors(Transform root, AnnotatedGrid grid)
        {
            int n = 0;
            for (int cell = 0; cell < grid.Grid.CellCount; cell++)
            {
                if (!grid.IsPlayable(cell)) continue;

                GameObject go;
#if UNITY_EDITOR
                go = Application.isPlaying
                    ? Instantiate(floorPrefab, root)
                    : (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(floorPrefab, root);
#else
                go = Instantiate(floorPrefab, root);
#endif
                go.transform.localPosition = grid.CellToWorld(cell) - transform.position;
                go.transform.localRotation = Quaternion.identity;
                go.name = $"Floor [{cell}]";
                n++;
            }
            return n;
        }

        private void Report(string message)
        {
            lastStatus = message;
            Debug.Log($"[DualGridWallPreview] {message}", this);
        }

        // Aviso barato e cedo: peça gerada com um S e FloorSpec com outro é o erro mais
        // fácil de cometer aqui, e o sintoma (vãos entre as peças) parece bug do builder.
        private void OnValidate()
        {
            if (floorSpec == null || pieces.straight == null) return;

            var mf = pieces.straight.GetComponentInChildren<MeshFilter>();
            if (mf == null) return;

            // A peça reta tem exatamente S de comprimento no eixo do braço.
            float pieceSpan = Mathf.Max(mf.transform.localScale.x, mf.transform.localScale.z);
            if (pieceSpan > 0.01f && Mathf.Abs(pieceSpan - floorSpec.cellSize) > 0.01f)
                Debug.LogWarning($"[DualGridWallPreview] Wall_Straight mede {pieceSpan} m, mas o " +
                                 $"FloorSpec usa cellSize {floorSpec.cellSize}. Regere as peças com " +
                                 "o mesmo Cell Size ou as paredes não vão encostar.", this);
        }
    }
}
