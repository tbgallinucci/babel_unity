// ============================================================================
//  MinimapController.cs  —  código do JOGO
//
//  Minimapa procedural: desenha o grid do andar gerado (WFC.Runtime.GeneratedFloor)
//  como uma malha de ícones de UI — não é câmera + RenderTexture. Cada célula
//  jogável vira um quadradinho, cada borda "Wall"/"Door" vira uma barra fina.
//  Cor por RoomRole (entrada, corredor, sala de combate, escada).
//
//  Fog of war: as células começam desativadas (invisíveis) e vão sendo
//  reveladas em definitivo conforme o jogador anda — um BFS de profundidade
//  limitada (revealRadiusCells) a partir da célula atual, atravessando só
//  bordas que NÃO são parede. Isso significa que o raio de revelação respeita
//  a topologia do andar: não "vaza" revelação pra dentro de uma sala vizinha
//  através de uma parede, só através de porta/passagem aberta.
//
//  O mapa é reconstruído do zero a cada FloorGenerated (novo andar = novo
//  layout = fog reseta). A câmera do minimapa é fake: quem se move é o
//  mapRoot por baixo do ícone do jogador, que fica fixo no centro do viewport.
//
//  Hierarquia esperada (ver MinimapBootstrap, que monta isso sozinho):
//      Minimap (este componente)
//        └ Viewport (RectMask2D, recorta o que passa da borda)
//            ├ MapRoot   (RectTransform grande, filho com os tiles gerados)
//            └ PlayerIcon (fixo no centro, gira com a direção do jogador)
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WFC.Core;
using WFC.Data;
using WFC.Runtime;

namespace Babel.UI
{
    [AddComponentMenu("Babel/UI/Minimap Controller")]
    public sealed class MinimapController : MonoBehaviour
    {
        [Header("Fonte de dados")]
        [SerializeField] private WFCFloorGenerator generator;
        [SerializeField] private Transform player;

        [Header("Hierarquia (a Minimap Bootstrap preenche isso sozinha)")]
        [SerializeField] private RectTransform mapRoot;
        [SerializeField] private RectTransform playerIcon;

        [Header("Visual")]
        [Tooltip("Tamanho de uma célula do grid no minimapa, em pixels de UI.")]
        [Min(1f)] [SerializeField] private float cellPixelSize = 10f;

        [Min(0.5f)] [SerializeField] private float wallThicknessPx = 2f;

        [SerializeField] private Color corridorColor = new Color(0.55f, 0.55f, 0.6f, 1f);
        [SerializeField] private Color roomColor = new Color(0.75f, 0.75f, 0.8f, 1f);
        [SerializeField] private Color entranceColor = new Color(0.35f, 0.9f, 0.45f, 1f);
        [SerializeField] private Color stairsColor = new Color(0.35f, 0.65f, 1f, 1f);
        [SerializeField] private Color wallColor = new Color(0.05f, 0.05f, 0.07f, 1f);
        [SerializeField] private Color doorColor = new Color(0.9f, 0.75f, 0.3f, 1f);

        [Header("Fog of war")]
        [Tooltip("Quantos passos de célula em célula (atravessando só aberturas, nunca paredes) a revelação alcança a partir do jogador.")]
        [Min(0)] [SerializeField] private int revealRadiusCells = 3;

        [Header("Ícone do jogador")]
        [Tooltip("Inverte o sentido de rotação do ícone, caso ele fique espelhado no seu mapa.")]
        [SerializeField] private bool invertIconRotation;

        // -------------------------------------------------------------- estado
        private GeneratedFloor pendingFloor;
        private bool needsRebuild;

        private GeneratedFloor floor;
        private AnnotatedGrid grid;
        private int sizeX, sizeZ;
        private int currentLayerY = -1;

        private readonly Dictionary<int, GameObject> cellTiles = new Dictionary<int, GameObject>();
        private readonly HashSet<int> revealed = new HashSet<int>();
        private readonly Queue<(int cell, int depth)> bfsQueue = new Queue<(int, int)>();

        private int lastPlayerCell = int.MinValue;

        private void Reset()
        {
            generator = FindFirstObjectByType<WFCFloorGenerator>();
        }

        private void OnEnable()
        {
            if (generator != null)
                generator.FloorGenerated += HandleFloorGenerated;

            // Componente ligado depois que o andar já existe (ex.: reabrir a
            // cena, ou minimapa criado depois do primeiro Start) — não perde a
            // geração que já rolou.
            if (generator != null && generator.Current != null && generator.Current.Success)
            {
                pendingFloor = generator.Current;
                needsRebuild = true;
            }
        }

        private void OnDisable()
        {
            if (generator != null)
                generator.FloorGenerated -= HandleFloorGenerated;
        }

        private void HandleFloorGenerated(GeneratedFloor newFloor)
        {
            pendingFloor = newFloor;
            needsRebuild = true;
        }

        private void Update()
        {
            if (needsRebuild)
            {
                needsRebuild = false;
                RebuildFromPending();
            }

            if (floor == null || !floor.Success || player == null || mapRoot == null)
                return;

            WorldToCell(player.position, out int px, out int py, out int pz);
            py = Mathf.Clamp(py, 0, Mathf.Max(0, grid.Grid.SizeY - 1));

            if (py != currentLayerY)
                BuildTilesForLayer(py);

            px = Mathf.Clamp(px, 0, sizeX - 1);
            pz = Mathf.Clamp(pz, 0, sizeZ - 1);
            int playerCell = grid.Grid.Index(px, currentLayerY, pz);

            if (playerCell != lastPlayerCell)
            {
                lastPlayerCell = playerCell;
                RevealAround(playerCell);
            }

            PositionMap();
            RotateIcon();
        }

        // =====================================================================
        //  Construção do mapa
        // =====================================================================

        private void RebuildFromPending()
        {
            floor = pendingFloor;
            grid = floor?.Grid;
            currentLayerY = -1;
            lastPlayerCell = int.MinValue;

            if (grid == null || mapRoot == null)
                return;

            sizeX = grid.Grid.SizeX;
            sizeZ = grid.Grid.SizeZ;
        }

        private void BuildTilesForLayer(int layerY)
        {
            currentLayerY = layerY;
            lastPlayerCell = int.MinValue;

            ClearTiles();

            mapRoot.pivot = new Vector2(0f, 0f);
            mapRoot.anchorMin = new Vector2(0.5f, 0.5f);
            mapRoot.anchorMax = new Vector2(0.5f, 0.5f);
            mapRoot.sizeDelta = new Vector2(sizeX * cellPixelSize, sizeZ * cellPixelSize);

            for (int z = 0; z < sizeZ; z++)
            for (int x = 0; x < sizeX; x++)
            {
                int cell = grid.Grid.Index(x, layerY, z);
                if (!grid.IsPlayable(cell)) continue;
                CreateFloorTile(cell, x, z);
            }
        }

        private void ClearTiles()
        {
            cellTiles.Clear();
            revealed.Clear();

            if (mapRoot == null) return;
            for (int i = mapRoot.childCount - 1; i >= 0; i--)
                Destroy(mapRoot.GetChild(i).gameObject);
        }

        private void CreateFloorTile(int cell, int x, int z)
        {
            var go = new GameObject($"Cell_{x}_{z}", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(mapRoot, false);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(cellPixelSize, cellPixelSize);
            rt.anchoredPosition = new Vector2((x + 0.5f) * cellPixelSize, (z + 0.5f) * cellPixelSize);

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.color = ColorForRole(grid.GetRole(cell));

            foreach (Direction dir in Directions.Horizontal)
            {
                BorderLabel label = grid.GetBorder(cell, dir);
                if (label == BorderLabel.Open) continue;
                CreateBorderBar(rt, dir, label == BorderLabel.Door ? doorColor : wallColor);
            }

            go.SetActive(false); // fog: começa escondido até ser revelado
            cellTiles[cell] = go;
        }

        private void CreateBorderBar(RectTransform parent, Direction dir, Color color)
        {
            var go = new GameObject($"Border_{dir}", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);

            bool vertical = dir == Direction.PosX || dir == Direction.NegX;
            rt.sizeDelta = vertical
                ? new Vector2(wallThicknessPx, cellPixelSize)
                : new Vector2(cellPixelSize, wallThicknessPx);

            float half = cellPixelSize * 0.5f;
            rt.anchoredPosition = dir switch
            {
                Direction.PosX => new Vector2(half, 0f),
                Direction.NegX => new Vector2(-half, 0f),
                Direction.PosZ => new Vector2(0f, half),
                Direction.NegZ => new Vector2(0f, -half),
                _ => Vector2.zero,
            };

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.color = color;
        }

        private Color ColorForRole(RoomRole role)
        {
            switch (role)
            {
                case RoomRole.Entrada: return entranceColor;
                case RoomRole.Escada: return stairsColor;
                case RoomRole.Corredor: return corridorColor;
                case RoomRole.Combate: return roomColor;
                default: return roomColor;
            }
        }

        // =====================================================================
        //  Fog of war
        // =====================================================================

        private void RevealAround(int startCell)
        {
            bfsQueue.Clear();
            bfsQueue.Enqueue((startCell, 0));

            // Visited local só pra não reprocessar dentro desta chamada — revealed
            // já cobre o "pra sempre revelado" entre chamadas.
            var visited = new HashSet<int> { startCell };

            while (bfsQueue.Count > 0)
            {
                var (cell, depth) = bfsQueue.Dequeue();
                RevealCell(cell);

                if (depth >= revealRadiusCells) continue;

                foreach (Direction dir in Directions.Horizontal)
                {
                    if (grid.GetBorder(cell, dir) == BorderLabel.Wall) continue;
                    if (!grid.Grid.TryGetNeighbor(cell, dir, out int neighbor)) continue;
                    if (!grid.IsPlayable(neighbor) || visited.Contains(neighbor)) continue;

                    visited.Add(neighbor);
                    bfsQueue.Enqueue((neighbor, depth + 1));
                }
            }
        }

        private void RevealCell(int cell)
        {
            if (!revealed.Add(cell)) return;
            if (cellTiles.TryGetValue(cell, out GameObject go) && go != null)
                go.SetActive(true);
        }

        // =====================================================================
        //  Posição / rotação
        // =====================================================================

        private void PositionMap()
        {
            float u = (player.position.x - grid.Origin.x) / grid.CellSize;
            float v = (player.position.z - grid.Origin.z) / grid.CellSize;
            var mapPos = new Vector2(u * cellPixelSize, v * cellPixelSize);
            mapRoot.anchoredPosition = -mapPos;
        }

        private void RotateIcon()
        {
            if (playerIcon == null) return;
            float yaw = player.eulerAngles.y;
            float sign = invertIconRotation ? 1f : -1f;
            playerIcon.localEulerAngles = new Vector3(0f, 0f, sign * yaw);
        }

        private void WorldToCell(Vector3 world, out int x, out int y, out int z)
        {
            x = Mathf.FloorToInt((world.x - grid.Origin.x) / grid.CellSize);
            z = Mathf.FloorToInt((world.z - grid.Origin.z) / grid.CellSize);
            y = grid.CellHeight > 0f ? Mathf.RoundToInt((world.y - grid.Origin.y) / grid.CellHeight) : 0;
        }
    }
}
