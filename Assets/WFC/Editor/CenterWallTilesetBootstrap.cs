// ============================================================================
//  CenterWallTilesetBootstrap.cs  —  camada WFC.Editor
//
//  Gera os assets de DADOS do tileset de GRID DUAL — hoje o único tileset WFC
//  do projeto (o kit greybox clássico, borda-na-célula, foi removido):
//      SocketLibrary + 10 ModuleDefinition + TileSet (bakeado) + FloorSpec
//
//  Menu:  WFC ▸ Center Wall Tileset Bootstrap
//
//  ------------------------------------------------------------------------
//  O QUE MUDA EM RELAÇÃO AO BOOTSTRAP CLÁSSICO
//
//  Os SOCKETS são exatamente os mesmos (open/wall/door + ground/sky), e as
//  regras de papel também. Isso é de propósito: o solver, o DoorReconciler e o
//  FloorSpec não precisam saber que o kit mudou — a tradução borda→socket segue
//  idêntica, e o esqueleto continua mandando.
//
//  O que muda é só o PREFAB por trás de cada módulo. Como a parede saiu da peça
//  de célula e foi pro grid dual, sete dos dez módulos (aberto, parede, canto,
//  corredor, beco, porta, porta-de-corredor) apontam para o MESMO
//  `Tile_Floor_Flat`: eles diferem no socket, não na malha. Não é preguiça — é
//  a consequência direta de a peça não codificar mais formato de parede.
//
//  Efeito colateral aceito: com os sete iguais, o solver deixa de decidir
//  estrutura e passa a decidir só variação de piso. Autore mais variantes de
//  laje (mesmos sockets, malhas diferentes) e ele volta a ter o que sortear,
//  sem tocar em uma linha de código.
//
//  IMPORTANTE: o FloorSpec gerado aqui já vem com as `wallPieces` preenchidas —
//  é isso que faz o WFCFiller chamar o DualGridWallBuilder. Sem elas o andar
//  sai sem parede nenhuma, porque as peças de célula são só piso.
// ============================================================================

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WFC.Core;
using WFC.Data;

namespace WFC.EditorTools
{
    public sealed class CenterWallTilesetBootstrap : EditorWindow
    {
        private const string SocketOpen = "open";
        private const string SocketWall = "wall";
        private const string SocketDoor = "door";
        private const string SocketGround = "ground";
        private const string SocketSky = "sky";

        private string prefabFolder = "Assets/WFC/CenterWallTiles";
        private string outputFolder = "Assets/WFC/CenterWallTileset";

        private Vector3Int demoGridSize = new Vector3Int(24, 1, 24);
        private float cellSize = 6f;
        private float cellHeight = 10f;

        private Vector2 scroll;
        private string lastReport;

        [MenuItem("WFC/Center Wall Tileset Bootstrap")]
        private static void Open() => GetWindow<CenterWallTilesetBootstrap>("Center Wall Tileset");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Dados do tileset de grid dual", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Gera SocketLibrary + 10 ModuleDefinition + TileSet (bakeado) + FloorSpec apontando " +
                "para o kit de grid dual.\n\n" +
                "Rode o WFC ▸ Center Wall Tile Generator ANTES — este bootstrap só amarra os " +
                "prefabs que já existem.\n\n" +
                "Idempotente: rodar de novo atualiza os assets no lugar.",
                MessageType.Info);

            EditorGUILayout.Space();
            prefabFolder = EditorGUILayout.TextField("Pasta dos prefabs", prefabFolder);
            outputFolder = EditorGUILayout.TextField("Pasta de saída", outputFolder);

            EditorGUILayout.Space();
            cellSize = EditorGUILayout.FloatField(
                new GUIContent("Cell Size", "Tem que bater com o Cell Size usado pra gerar as peças."),
                cellSize);
            cellHeight = EditorGUILayout.FloatField("Cell Height (parede)", cellHeight);
            demoGridSize = EditorGUILayout.Vector3IntField("Grid do demo (células)", demoGridSize);

            EditorGUILayout.Space();
            if (GUILayout.Button("Gerar assets do tileset", GUILayout.Height(32)))
                Generate();

            if (!string.IsNullOrEmpty(lastReport))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Relatório", EditorStyles.boldLabel);
                scroll = EditorGUILayout.BeginScrollView(scroll);
                EditorGUILayout.TextArea(lastReport, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
        }

        // =====================================================================
        private void Generate()
        {
            EnsureFolder(outputFolder);
            EnsureFolder(outputFolder + "/Modules");

            SocketLibrary library = BuildSocketLibrary();
            List<ModuleDefinition> modules = BuildModules();
            TileSet tileSet = BuildTileSet(library, modules);

            bool ok = tileSet.Bake(out string report);
            EditorUtility.SetDirty(tileSet);

            BuildFloorSpec(tileSet);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            lastReport = report;
            if (ok) Debug.Log($"[CenterWallTilesetBootstrap] Tileset gerado em {outputFolder}.\n{report}");
            else Debug.LogError($"[CenterWallTilesetBootstrap] Bake falhou.\n{report}");
        }

        private SocketLibrary BuildSocketLibrary()
        {
            var lib = LoadOrCreate<SocketLibrary>($"{outputFolder}/SocketLibrary.asset");
            lib.ClearAll();
            lib.UpsertHorizontal(SocketOpen, true, new Color(0.35f, 0.9f, 0.45f));
            lib.UpsertHorizontal(SocketWall, true, new Color(0.60f, 0.60f, 0.62f));
            lib.UpsertHorizontal(SocketDoor, true, new Color(1.00f, 0.82f, 0.20f));
            lib.UpsertVertical(SocketGround, true, new Color(0.55f, 0.42f, 0.30f));
            lib.UpsertVertical(SocketSky, true, new Color(0.45f, 0.70f, 1.00f));
            EditorUtility.SetDirty(lib);
            return lib;
        }

        // ------------------------------------------------------------ módulos
        private List<ModuleDefinition> BuildModules()
        {
            // Os sete primeiros compartilham o MESMO prefab de laje — ver cabeçalho.
            const string Flat = "Tile_Floor_Flat";
            var list = new List<ModuleDefinition>
            {
                Module(Flat, "Module_Floor_Open",
                    SocketOpen, SocketOpen, SocketOpen, SocketOpen,
                    3.0f, ModuleTag.Floor, new[] { true, false, false, false }),

                Module(Flat, "Module_Wall",
                    SocketOpen, SocketOpen, SocketWall, SocketOpen,
                    2.0f, ModuleTag.Floor | ModuleTag.Wall, new[] { true, true, true, true }),

                Module(Flat, "Module_Corner",
                    SocketOpen, SocketWall, SocketWall, SocketOpen,
                    1.2f, ModuleTag.Floor | ModuleTag.Wall | ModuleTag.Corner, new[] { true, true, true, true }),

                Module(Flat, "Module_Corridor",
                    SocketOpen, SocketOpen, SocketWall, SocketWall,
                    1.2f, ModuleTag.Floor | ModuleTag.Wall, new[] { true, true, false, false }),

                Module(Flat, "Module_DeadEnd",
                    SocketWall, SocketWall, SocketWall, SocketOpen,
                    0.40f, ModuleTag.Floor | ModuleTag.Wall, new[] { true, true, true, true }),

                Module(Flat, "Module_Door",
                    SocketOpen, SocketOpen, SocketDoor, SocketOpen,
                    0.35f, ModuleTag.Floor | ModuleTag.Doorway, new[] { true, true, true, true }),

                Module(Flat, "Module_Door_Corridor",
                    SocketWall, SocketWall, SocketDoor, SocketOpen,
                    0.20f, ModuleTag.Floor | ModuleTag.Doorway | ModuleTag.Wall, new[] { true, true, true, true }),

                // A escada é a única peça de célula que ainda tem geometria própria: a rampa
                // não é parede, então não migrou pro grid dual.
                Module("Tile_Stairs", "Module_Stairs",
                    SocketWall, SocketWall, SocketWall, SocketOpen,
                    0.08f, ModuleTag.Floor | ModuleTag.Stairs, new[] { true, true, true, true }),

                Module("Wildcard_SolidFill", "Module_SolidFill",
                    SocketWall, SocketWall, SocketWall, SocketWall,
                    0.10f, ModuleTag.Blocking | ModuleTag.Wildcard, new[] { true, false, false, false }),

                Module("Wildcard_Air", "Module_Air",
                    SocketWall, SocketWall, SocketWall, SocketWall,
                    0.10f, ModuleTag.Empty | ModuleTag.Blocking | ModuleTag.Wildcard, new[] { true, false, false, false }),
            };

            foreach (ModuleDefinition m in list) EditorUtility.SetDirty(m);
            return list;
        }

        private ModuleDefinition Module(string prefabName, string assetName,
                                        string posX, string negX, string posZ, string negZ,
                                        float weight, ModuleTag tags, bool[] rotations)
        {
            var module = LoadOrCreate<ModuleDefinition>($"{outputFolder}/Modules/{assetName}.asset");

            module.prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{prefabFolder}/{prefabName}.prefab");
            if (module.prefab == null)
                Debug.LogWarning($"[CenterWallTilesetBootstrap] Prefab não encontrado: " +
                                 $"{prefabFolder}/{prefabName}.prefab — rode antes o " +
                                 "WFC ▸ Center Wall Tile Generator.");

            module.faces = new FaceSocket[Directions.Count];
            module.SetFace(Direction.PosX, new FaceSocket(posX));
            module.SetFace(Direction.NegX, new FaceSocket(negX));
            module.SetFace(Direction.PosZ, new FaceSocket(posZ));
            module.SetFace(Direction.NegZ, new FaceSocket(negZ));
            module.SetFace(Direction.PosY, new FaceSocket(SocketSky));
            module.SetFace(Direction.NegY, new FaceSocket(SocketGround));

            module.weight = weight;
            module.tags = tags;
            module.allowedYRotations = rotations;
            return module;
        }

        private TileSet BuildTileSet(SocketLibrary library, List<ModuleDefinition> modules)
        {
            var tileSet = LoadOrCreate<TileSet>($"{outputFolder}/TileSet_CenterWall.asset");
            tileSet.socketLibrary = library;
            tileSet.modules = new List<ModuleDefinition>(modules);
            return tileSet;
        }

        // --------------------------------------------------------- floor spec
        private void BuildFloorSpec(TileSet tileSet)
        {
            var spec = LoadOrCreate<FloorSpec>($"{outputFolder}/FloorSpec_CenterWall.asset");
            spec.tileSet = tileSet;
            spec.gridSize = demoGridSize;
            spec.cellSize = cellSize;
            spec.cellHeight = cellHeight;
            spec.randomSeed = true;
            spec.seed = 12345;
            spec.sealBorders = true;
            spec.borderSocket = SocketWall;
            spec.maxAttempts = 12;
            spec.entropyNoise = 1e-4;

            // O que liga o grid dual: sem isto o andar sai só com as lajes, sem parede
            // nenhuma, porque a parede não mora mais na peça de célula.
            spec.wallPieces = new WallPieceSet
            {
                end          = LoadPiece("Wall_End"),
                straight     = LoadPiece("Wall_Straight"),
                corner       = LoadPiece("Wall_Corner"),
                tee          = LoadPiece("Wall_Tee"),
                cross        = LoadPiece("Wall_Cross"),
                armWall      = LoadPiece("Arm_Wall"),
                armWallAtHub = LoadPiece("Arm_Wall_AtHub"),
                armDoor      = LoadPiece("Arm_Door"),
                armDoorAtHub = LoadPiece("Arm_Door_AtHub"),
                hubEnd       = LoadPiece("Hub_End"),
                hubJunction  = LoadPiece("Hub_Junction"),
            };

            if (!spec.wallPieces.IsComplete)
                Debug.LogWarning("[CenterWallTilesetBootstrap] Faltam peças de parede — o andar vai " +
                                 "sair SEM PAREDE. Rode o WFC ▸ Center Wall Tile Generator e gere de novo.");

            spec.roleRules = new List<FloorSpec.RoleRule>
            {
                new FloorSpec.RoleRule { role = RoomRole.Escada,   requiredTags = ModuleTag.Stairs, forbiddenTags = ModuleTag.None },
                new FloorSpec.RoleRule { role = RoomRole.Entrada,  requiredTags = ModuleTag.Floor,  forbiddenTags = ModuleTag.Wildcard | ModuleTag.Stairs },
                new FloorSpec.RoleRule { role = RoomRole.Combate,  requiredTags = ModuleTag.Floor,  forbiddenTags = ModuleTag.Wildcard | ModuleTag.Stairs },
                new FloorSpec.RoleRule { role = RoomRole.Corredor, requiredTags = ModuleTag.Floor,  forbiddenTags = ModuleTag.Stairs },
                new FloorSpec.RoleRule { role = RoomRole.Vazio,    requiredTags = ModuleTag.Wildcard, forbiddenTags = ModuleTag.None },
            };

            EditorUtility.SetDirty(spec);
        }

        private GameObject LoadPiece(string name)
            => AssetDatabase.LoadAssetAtPath<GameObject>($"{prefabFolder}/{name}.prefab");

        // ------------------------------------------------------------ helpers
        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
