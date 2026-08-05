// ============================================================================
//  WFCTilesetBootstrap.cs  —  camada WFC.Editor
//
//  Cria os assets de DADOS do tileset greybox de uma vez só:
//      SocketLibrary  +  9 ModuleDefinition  +  TileSet (bakeado)  +  FloorSpec
//
//  Mesma filosofia do GreyboxTileGenerator (que gerou os 9 prefabs): idempotente,
//  regerar sobrescreve, e depois você ajusta o que quiser no Inspector.
//
//  Menu:  WFC ▸ Tileset Bootstrap
//
//  ------------------------------------------------------------------------
//  A CONVENÇÃO DE SOCKETS DO GREYBOX (leia antes de reautorar à mão)
//
//  Os prefabs deste projeto colocam a parede DENTRO da célula, colada na borda.
//  Então o socket de uma face horizontal responde: "o que o vizinho encontra
//  quando encosta aqui?"
//
//    open  — piso aberto, passagem livre. Só conecta com `open`.
//    wall  — tem parede nesta borda. Conecta com `wall` (duas paredes costas
//            com costas separando dois cômodos: 0.2 + 0.2 = 0.4 m de espessura).
//    door  — vão de porta. Conecta com `door`: as portas nascem aos PARES, uma
//            metade de cada lado da borda, formando um vão alinhado.
//
//  Os coringas (SolidFill, Air) apresentam `wall` nas 4 laterais: são o "selo"
//  universal, o que permite encostá-los em qualquer parede sem contradição.
//
//  Verticais (`ground` embaixo, `sky` em cima) têm nomes DIFERENTES de propósito:
//  como só casam por nome igual, nenhuma peça empilha em nenhuma outra. Isto é
//  intencional — o greybox é um tileset de UM andar (gridSize.y = 1). Empilhar
//  vai exigir peças novas (poço, mezanino) na fase de arte.
//
//  Nenhum socket aqui é ASSIMÉTRICO (aquele que só conecta com o espelho, o
//  clássico "3" ↔ "3F"): nenhuma face do greybox é diferente da esquerda para a
//  direita. O suporte existe no código porque kits de arte de verdade precisam.
// ============================================================================

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WFC.Core;
using WFC.Data;

namespace WFC.EditorTools
{
    public sealed class WFCTilesetBootstrap : EditorWindow
    {
        // ---- Nomes dos sockets (mudou aqui, mude no FloorSpec.borderSocket também) ----
        private const string SocketOpen = "open";
        private const string SocketWall = "wall";
        private const string SocketDoor = "door";
        private const string SocketGround = "ground";
        private const string SocketSky = "sky";

        private string prefabFolder = "Assets/WFC/GreyboxTiles";
        private string outputFolder = "Assets/WFC/Tileset";

        private bool createFloorSpec = true;
        private Vector3Int demoGridSize = new Vector3Int(24, 1, 24);
        private float cellSize = 6f;    // medido nos prefabs greybox deste projeto
        private float cellHeight = 10f; // altura de parede do greybox

        private Vector2 scroll;
        private string lastReport;

        [MenuItem("WFC/Tileset Bootstrap")]
        private static void Open() => GetWindow<WFCTilesetBootstrap>("WFC Tileset");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Dados do tileset greybox", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Gera SocketLibrary + 9 ModuleDefinition + TileSet (já bakeado) + FloorSpec, " +
                "apontando para os prefabs greybox existentes.\n\n" +
                "Idempotente: rodar de novo atualiza os assets no lugar, preservando as referências.",
                MessageType.Info);

            EditorGUILayout.Space();
            prefabFolder = EditorGUILayout.TextField("Pasta dos prefabs", prefabFolder);
            outputFolder = EditorGUILayout.TextField("Pasta de saída", outputFolder);

            EditorGUILayout.Space();
            cellSize = EditorGUILayout.FloatField("Cell Size (footprint)", cellSize);
            cellHeight = EditorGUILayout.FloatField("Cell Height (parede)", cellHeight);

            EditorGUILayout.Space();
            createFloorSpec = EditorGUILayout.Toggle("Criar FloorSpec de demo", createFloorSpec);
            using (new EditorGUI.DisabledScope(!createFloorSpec))
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
        //  Geração
        // =====================================================================
        private void Generate()
        {
            EnsureFolder(outputFolder);
            EnsureFolder(outputFolder + "/Modules");

            SocketLibrary library = BuildSocketLibrary();
            List<ModuleDefinition> modules = BuildModules(library);
            TileSet tileSet = BuildTileSet(library, modules);

            bool ok = tileSet.Bake(out string report);
            EditorUtility.SetDirty(tileSet);

            if (createFloorSpec) BuildFloorSpec(tileSet);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            lastReport = report;
            if (ok) Debug.Log($"[WFCTilesetBootstrap] Tileset gerado em {outputFolder}.\n{report}");
            else Debug.LogError($"[WFCTilesetBootstrap] Bake falhou.\n{report}");
        }

        // ------------------------------------------------------------ sockets
        private SocketLibrary BuildSocketLibrary()
        {
            var lib = LoadOrCreate<SocketLibrary>($"{outputFolder}/SocketLibrary.asset");
            lib.ClearAll();

            // Todos simétricos: nenhuma face do greybox é diferente da esquerda p/ direita.
            lib.UpsertHorizontal(SocketOpen, true, new Color(0.35f, 0.9f, 0.45f));
            lib.UpsertHorizontal(SocketWall, true, new Color(0.60f, 0.60f, 0.62f));
            lib.UpsertHorizontal(SocketDoor, true, new Color(1.00f, 0.82f, 0.20f));

            // Verticais invariantes, mas com NOMES diferentes: nada empilha (tileset de 1 andar).
            lib.UpsertVertical(SocketGround, true, new Color(0.55f, 0.42f, 0.30f));
            lib.UpsertVertical(SocketSky, true, new Color(0.45f, 0.70f, 1.00f));

            EditorUtility.SetDirty(lib);
            return lib;
        }

        // ------------------------------------------------------------ módulos
        private List<ModuleDefinition> BuildModules(SocketLibrary library)
        {
            var list = new List<ModuleDefinition>();

            // Legenda dos parâmetros de face: (PosX, NegX, PosZ, NegZ).
            // As paredes dos prefabs greybox ficam em: N = +Z, S = -Z, W = -X, E = +X.

            // 1. Piso aberto — nenhuma parede. Girar não muda nada: só a rotação 0.
            list.Add(Module("Tile_Floor_Open", "Module_Floor_Open",
                posX: SocketOpen, negX: SocketOpen, posZ: SocketOpen, negZ: SocketOpen,
                weight: 3.0f, tags: ModuleTag.Floor,
                rotations: new[] { true, false, false, false }));

            // 2. Parede reta — uma parede, no norte.
            list.Add(Module("Tile_Wall", "Module_Wall",
                posX: SocketOpen, negX: SocketOpen, posZ: SocketWall, negZ: SocketOpen,
                weight: 2.0f, tags: ModuleTag.Floor | ModuleTag.Wall,
                rotations: new[] { true, true, true, true }));

            // 3. Canto em L — paredes no norte e no oeste (é assim que o prefab veio).
            list.Add(Module("Tile_Corner", "Module_Corner",
                posX: SocketOpen, negX: SocketWall, posZ: SocketWall, negZ: SocketOpen,
                weight: 1.2f, tags: ModuleTag.Floor | ModuleTag.Wall | ModuleTag.Corner,
                rotations: new[] { true, true, true, true }));

            // 4. Corredor — paredes opostas (norte e sul). 180° é idêntico a 0°.
            list.Add(Module("Tile_Corridor", "Module_Corridor",
                posX: SocketOpen, negX: SocketOpen, posZ: SocketWall, negZ: SocketWall,
                weight: 1.2f, tags: ModuleTag.Floor | ModuleTag.Wall,
                rotations: new[] { true, true, false, false }));

            // 5. Beco sem saída — três paredes (norte, oeste, leste), aberto ao sul.
            list.Add(Module("Tile_DeadEnd", "Module_DeadEnd",
                posX: SocketWall, negX: SocketWall, posZ: SocketWall, negZ: SocketOpen,
                weight: 0.40f, tags: ModuleTag.Floor | ModuleTag.Wall,
                rotations: new[] { true, true, true, true }));

            // 6. Porta — vão no norte. Casa só com outra porta: o vão nasce aos pares,
            //    metade em cada célula, formando uma passagem alinhada.
            list.Add(Module("Tile_Door", "Module_Door",
                posX: SocketOpen, negX: SocketOpen, posZ: SocketDoor, negZ: SocketOpen,
                weight: 0.35f, tags: ModuleTag.Floor | ModuleTag.Doorway,
                rotations: new[] { true, true, true, true }));

            // 7. Porta dentro de corredor — vão ao norte, aberto ao sul, parede nos lados.
            //    Sem ela o esqueleto não consegue cravar porta num corredor de largura 1:
            //    o Tile_Door normal tem os outros três lados abertos.
            list.Add(Module("Tile_Door_Corridor", "Module_Door_Corridor",
                posX: SocketWall, negX: SocketWall, posZ: SocketDoor, negZ: SocketOpen,
                weight: 0.20f, tags: ModuleTag.Floor | ModuleTag.Doorway | ModuleTag.Wall,
                rotations: new[] { true, true, true, true }));

            // 8. Escada — rampa subindo para o norte, entra-se pelo sul. Nicho fechado.
            list.Add(Module("Tile_Stairs", "Module_Stairs",
                posX: SocketWall, negX: SocketWall, posZ: SocketWall, negZ: SocketOpen,
                weight: 0.08f, tags: ModuleTag.Floor | ModuleTag.Stairs,
                rotations: new[] { true, true, true, true }));

            // 9. Coringa maciço — apresenta `wall` nos 4 lados: encosta em qualquer parede.
            list.Add(Module("Wildcard_SolidFill", "Module_SolidFill",
                posX: SocketWall, negX: SocketWall, posZ: SocketWall, negZ: SocketWall,
                weight: 0.10f, tags: ModuleTag.Blocking | ModuleTag.Wildcard,
                rotations: new[] { true, false, false, false }));

            // 10. Coringa vazio — idem, mas sem geometria nenhuma.
            list.Add(Module("Wildcard_Air", "Module_Air",
                posX: SocketWall, negX: SocketWall, posZ: SocketWall, negZ: SocketWall,
                weight: 0.10f, tags: ModuleTag.Empty | ModuleTag.Blocking | ModuleTag.Wildcard,
                rotations: new[] { true, false, false, false }));

            foreach (ModuleDefinition m in list) EditorUtility.SetDirty(m);
            return list;
        }

        private ModuleDefinition Module(
            string prefabName, string assetName,
            string posX, string negX, string posZ, string negZ,
            float weight, ModuleTag tags, bool[] rotations)
        {
            var module = LoadOrCreate<ModuleDefinition>($"{outputFolder}/Modules/{assetName}.asset");

            module.prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{prefabFolder}/{prefabName}.prefab");
            if (module.prefab == null)
                Debug.LogWarning($"[WFCTilesetBootstrap] Prefab não encontrado: {prefabFolder}/{prefabName}.prefab " +
                                 "— rode antes o WFC ▸ Greybox Tile Generator.");

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

        // ------------------------------------------------------------ tileset
        private TileSet BuildTileSet(SocketLibrary library, List<ModuleDefinition> modules)
        {
            var tileSet = LoadOrCreate<TileSet>($"{outputFolder}/TileSet_Greybox.asset");
            tileSet.socketLibrary = library;
            tileSet.modules = new List<ModuleDefinition>(modules);
            return tileSet;
        }

        // --------------------------------------------------------- floor spec
        private void BuildFloorSpec(TileSet tileSet)
        {
            var spec = LoadOrCreate<FloorSpec>($"{outputFolder}/FloorSpec_Demo.asset");
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

            // Regras de papel para a Fase 2 (o esqueleto ainda não existe, mas o
            // vocabulário já fica pronto).
            spec.roleRules = new List<FloorSpec.RoleRule>
            {
                new FloorSpec.RoleRule
                {
                    role = RoomRole.Escada,
                    requiredTags = ModuleTag.Stairs,
                    forbiddenTags = ModuleTag.None,
                },
                new FloorSpec.RoleRule
                {
                    role = RoomRole.Entrada,
                    requiredTags = ModuleTag.Floor,
                    forbiddenTags = ModuleTag.Wildcard | ModuleTag.Stairs,
                },
                new FloorSpec.RoleRule
                {
                    role = RoomRole.Combate,
                    requiredTags = ModuleTag.Floor,
                    forbiddenTags = ModuleTag.Wildcard | ModuleTag.Stairs,
                },
                new FloorSpec.RoleRule
                {
                    role = RoomRole.Corredor,
                    requiredTags = ModuleTag.Floor,
                    forbiddenTags = ModuleTag.Stairs,
                },
                new FloorSpec.RoleRule
                {
                    role = RoomRole.Vazio,
                    requiredTags = ModuleTag.Wildcard,
                    forbiddenTags = ModuleTag.None,
                },
            };

            EditorUtility.SetDirty(spec);
        }

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
