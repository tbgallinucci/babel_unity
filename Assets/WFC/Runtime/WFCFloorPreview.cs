// ============================================================================
//  WFCFloorPreview.cs  —  camada WFC.Runtime
//
//  Harness de desenvolvimento: monta o pipeline inteiro num componente só.
//
//      FloorSpec → SkeletonGenerator → WFCFiller → ConnectivityValidator
//
//  Coloque num GameObject vazio, aponte um FloorSpec e clique "Gerar".
//
//  ⚠️ Continua sendo harness, não runtime de produção. O que falta para virar o
//  WFCFloorGenerator de verdade (Fase 3/4): pooling no instanciador, solve em
//  thread de background com Awaitable, e o bake do NavMeshSurface. A costura já
//  está toda aqui — é trocar as peças.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using WFC.Core;
using WFC.Data;

namespace WFC.Runtime
{
    [ExecuteAlways]
    [AddComponentMenu("WFC/WFC Floor Preview (demo)")]
    public sealed class WFCFloorPreview : MonoBehaviour
    {
        [Header("Receita")]
        public FloorSpec floorSpec;

        [Header("Opções")]
        [Tooltip("Instancia os prefabs. Desligue para só rodar esqueleto + solver e ver os gizmos.")]
        public bool instantiatePrefabs = true;

        [Tooltip("Roda o flood-fill entrada→escada depois de gerar. Barato; deixe ligado.")]
        public bool validateConnectivity = true;

        [Header("Teto")]
        [Tooltip("Mesma 'tampa' que o WFCFloorGenerator de produção cria — sem isto o preview " +
                 "sai com o topo aberto pro void (é o que diferenciava o harness da produção).")]
        public bool addCeiling = true;

        [Tooltip("Vazio = material padrão (cinza) do Plane primitivo.")]
        public Material ceilingMaterial;

        public float ceilingOverhang = 0.5f;

        [Header("Gizmos")]
        public bool drawGridGizmos = true;

        [Tooltip("Pinta o esqueleto: papéis por célula, portas e vãos. É o que mostra o que o WFC recebeu.")]
        public bool drawSkeleton = true;

        public bool drawVariantLabels;

        [Header("Último resultado (leitura)")]
        [SerializeField] private string lastStatus = "(nada gerado ainda)";
        [SerializeField] private int lastSeed;
        [SerializeField] private int lastRooms;
        [SerializeField] private int lastDoors;
        [SerializeField] private double lastSolveMs;
        [SerializeField] private double lastInstantiateMs;

        private AnnotatedGrid _grid;
        private int[] _variants;
        private SkeletonGenerator.Result _skeleton;
        private readonly List<SpawnAnchor> _anchors = new List<SpawnAnchor>();

        public string LastStatus => lastStatus;
        public int[] LastVariants => _variants;
        public AnnotatedGrid LastGrid => _grid;
        public SkeletonGenerator.Result LastSkeleton => _skeleton;
        public int LastSeed => lastSeed;

        /// <summary>
        /// True só depois de um Generate() que terminou em "OK". Existe pra quem monta em cima
        /// deste harness (ex.: um populador de preview de arte do lado do jogo) não precisar
        /// adivinhar sucesso fatiando o texto de LastStatus.
        /// </summary>
        public bool LastSuccess { get; private set; }

        /// <summary>Raiz da geometria instanciada pelo último Generate(). Null se falhou ou instantiatePrefabs=false.</summary>
        public Transform GeneratedRoot { get; private set; }

        /// <summary>Peça por célula do último Generate() — mesmo array que o TileInstancer devolveu.</summary>
        public Transform[] PiecesByCell { get; private set; }

        /// <summary>Contrato de saída para o populador externo (Decisão 4).</summary>
        public IReadOnlyList<SpawnAnchor> Anchors => _anchors;

        // =====================================================================
        //  Pipeline
        // =====================================================================
        [ContextMenu("Gerar")]
        public void Generate()
        {
            LastSuccess = false;
            GeneratedRoot = null;
            PiecesByCell = null;

            if (floorSpec == null) { Report("FloorSpec não atribuído."); return; }
            if (floorSpec.tileSet == null) { Report("O FloorSpec não tem TileSet."); return; }
            if (!floorSpec.tileSet.IsBaked)
            {
                Report($"TileSet '{floorSpec.tileSet.name}' não está bakeado. " +
                       "Abra o asset e clique em \"Rebuild adjacency\".");
                return;
            }

            int seed = floorSpec.ResolveSeed();
            lastSeed = seed;

            // Um RNG só para o andar inteiro: esqueleto e filler consomem em sequência,
            // então a seed reproduz o andar COMPLETO, não só o layout.
            var rng = new XorShiftRandom(seed);
            Grid3D grid = floorSpec.CreateGrid();

            // ---- 1. Esqueleto ----------------------------------------------
            var skeletonGen = new SkeletonGenerator();
            _skeleton = skeletonGen.Generate(grid, floorSpec.cellSize, floorSpec.cellHeight,
                                             transform.position, floorSpec.skeleton, rng);

            if (!_skeleton.Success)
            {
                _grid = _skeleton.Grid;
                _variants = null;
                TileInstancer.ClearGenerated(transform);
                Report($"Esqueleto falhou — {_skeleton.Message}");
                return;
            }

            _grid = _skeleton.Grid;
            lastRooms = _skeleton.Rooms.Count;
            lastDoors = _skeleton.DoorCount;

            // ---- 2. Conectividade (antes de gastar tempo com geometria) ----
            string connectivityNote = string.Empty;
            if (validateConnectivity)
            {
                if (!ConnectivityValidator.IsReachable(_grid, _skeleton.EntranceCell, _skeleton.StairsCell))
                {
                    Report("ANDAR SEM SAÍDA: não há caminho da entrada até a escada. " +
                           "Isso é bug do esqueleto, não do WFC.");
                    return;
                }

                if (!ConnectivityValidator.AllPlayableReachable(_grid, _skeleton.EntranceCell, out List<int> orfas))
                    connectivityNote = $" | {orfas.Count} célula(s) jogável(is) ilhada(s)";
            }

            // ---- 3. Preenchimento (WFC) ------------------------------------
            var filler = new WFCFiller(floorSpec.tileSet, floorSpec, transform)
            {
                InstantiateGeometry = instantiatePrefabs,
            };

            _anchors.Clear();
            FloorFillResult fill = filler.Fill(_grid, rng);

            lastSolveMs = fill.SolveMilliseconds;
            lastInstantiateMs = fill.InstantiateMilliseconds;

            if (!fill.Success)
            {
                _variants = null;
                TileInstancer.ClearGenerated(transform);
                Report($"Filler falhou — {fill.Message}");
                return;
            }

            _variants = fill.Variants;
            _anchors.AddRange(fill.Anchors);
            GeneratedRoot = fill.Root;
            PiecesByCell = fill.PiecesByCell;

            if (!instantiatePrefabs) TileInstancer.ClearGenerated(transform);
            else if (addCeiling && GeneratedRoot != null)
                CeilingBuilder.Build(GeneratedRoot, transform.position, floorSpec.gridSize,
                                     floorSpec.cellSize, floorSpec.cellHeight, ceilingOverhang,
                                     ceilingMaterial);

            foreach (string w in filler.Warnings) Debug.LogWarning($"[WFCFloorPreview] {w}", this);

            // Recontado DEPOIS do filler: ele rebaixa para vão as portas que o kit não
            // representa, então os números do esqueleto já estão velhos aqui.
            lastDoors = _grid.CountJunctions(Junction.Door);
            int vaos = _grid.CountJunctions(Junction.Opening);

            LastSuccess = true;
            Report($"OK — {_skeleton.Rooms.Count} salas, {lastDoors} portas, " +
                   $"{vaos} vãos, seed {seed}, {fill.Attempts} tentativa(s), " +
                   $"solve {lastSolveMs:F1} ms, instanciar {lastInstantiateMs:F1} ms" +
                   connectivityNote +
                   (string.IsNullOrEmpty(_skeleton.Message) ? "" : $" | {_skeleton.Message}"));
        }

        [ContextMenu("Limpar")]
        public void ClearGenerated()
        {
            TileInstancer.ClearGenerated(transform);
            LastSuccess = false;
            GeneratedRoot = null;
            PiecesByCell = null;
        }

        private void Report(string message)
        {
            lastStatus = message;
            Debug.Log($"[WFCFloorPreview] {message}", this);
        }

        // =====================================================================
        //  Gizmos
        // =====================================================================
        private void OnDrawGizmosSelected()
        {
            if (floorSpec == null) return;

            if (drawGridGizmos) DrawGridBounds();
            if (drawSkeleton && _grid != null) DrawSkeletonGizmos();
            else if (_grid != null && _variants != null) DrawTagGizmos();
        }

        private void DrawGridBounds()
        {
            float s = floorSpec.cellSize;
            float h = floorSpec.cellHeight;
            Vector3Int size = floorSpec.gridSize;

            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            var boundsSize = new Vector3(size.x * s, Mathf.Max(size.y, 1) * h, size.z * s);
            Gizmos.DrawWireCube(transform.position + boundsSize * 0.5f, boundsSize);
        }

        /// <summary>
        /// O desenho mais útil de todos: mostra o que o esqueleto decidiu, que é a
        /// causa de quase tudo que dá errado depois.
        /// </summary>
        private void DrawSkeletonGizmos()
        {
            float s = _grid.CellSize;

            for (int cell = 0; cell < _grid.Grid.CellCount; cell++)
            {
                Vector3 c = _grid.CellToWorld(cell) + Vector3.up * 0.05f;

                if (_grid.IsPlayable(cell))
                {
                    Gizmos.color = ColorForRole(_grid.GetRole(cell));
                    Gizmos.DrawCube(c, new Vector3(s * 0.7f, 0.02f, s * 0.7f));
                }

                // Só as bordas +X e +Z, senão cada parede é desenhada duas vezes.
                DrawBorder(cell, Direction.PosX, s);
                DrawBorder(cell, Direction.PosZ, s);
            }

            if (_skeleton == null) return;

            if (_skeleton.EntranceCell >= 0)
            {
                Gizmos.color = new Color(0.3f, 1f, 0.4f);
                Gizmos.DrawWireSphere(_grid.CellToWorld(_skeleton.EntranceCell) + Vector3.up, s * 0.3f);
            }

            if (_skeleton.StairsCell >= 0)
            {
                Gizmos.color = new Color(0.3f, 0.5f, 1f);
                Gizmos.DrawWireSphere(_grid.CellToWorld(_skeleton.StairsCell) + Vector3.up, s * 0.3f);
            }
        }

        private void DrawBorder(int cell, Direction dir, float s)
        {
            BorderLabel label = _grid.GetBorder(cell, dir);
            if (label == BorderLabel.Wall) return; // parede é o padrão; desenhar tudo vira sopa

            if (!_grid.Grid.TryGetNeighbor(cell, dir, out int neighbor)) return;

            Vector3 a = _grid.CellToWorld(cell);
            Vector3 b = _grid.CellToWorld(neighbor);
            Vector3 mid = (a + b) * 0.5f + Vector3.up * 0.4f;

            Gizmos.color = label == BorderLabel.Door
                ? new Color(1f, 0.8f, 0.2f)
                : new Color(0.4f, 1f, 0.5f, 0.5f);

            float size = label == BorderLabel.Door ? s * 0.35f : s * 0.15f;
            Gizmos.DrawCube(mid, Vector3.one * size);
        }

        private void DrawTagGizmos()
        {
            float s = _grid.CellSize;
            for (int cell = 0; cell < _variants.Length; cell++)
            {
                int variant = _variants[cell];
                if (variant < 0) continue;

                Gizmos.color = ColorForTags(floorSpec.tileSet.GetTags(variant));
                Gizmos.DrawWireCube(_grid.CellToWorld(cell) + Vector3.up * 0.05f,
                                    new Vector3(s * 0.9f, 0.02f, s * 0.9f));
            }
        }

        private static Color ColorForRole(RoomRole role)
        {
            switch (role)
            {
                case RoomRole.Entrada:  return new Color(0.3f, 1f, 0.4f, 0.55f);
                case RoomRole.Escada:   return new Color(0.3f, 0.5f, 1f, 0.75f);
                case RoomRole.Combate:  return new Color(1f, 0.45f, 0.4f, 0.35f);
                case RoomRole.Corredor: return new Color(0.85f, 0.85f, 0.5f, 0.35f);
                default:                return new Color(1f, 1f, 1f, 0.08f);
            }
        }

        private static Color ColorForTags(ModuleTag tags)
        {
            if ((tags & ModuleTag.Stairs) != 0)   return new Color(0.3f, 0.5f, 1f, 0.9f);
            if ((tags & ModuleTag.Doorway) != 0)  return new Color(1f, 0.8f, 0.2f, 0.9f);
            if ((tags & ModuleTag.Wildcard) != 0) return new Color(1f, 0.3f, 0.9f, 0.5f);
            if ((tags & ModuleTag.Wall) != 0)     return new Color(0.6f, 0.6f, 0.6f, 0.9f);
            if ((tags & ModuleTag.Floor) != 0)    return new Color(0.4f, 1f, 0.5f, 0.7f);
            return new Color(1f, 1f, 1f, 0.4f);
        }
    }
}
