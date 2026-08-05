// ============================================================================
//  GreyboxTileGenerator.cs
//  Gera automaticamente os 10 prefabs "greybox" do tileset mínimo do WFC.
//
//  COMO USAR:
//   1. Coloque este arquivo em uma pasta chamada "Editor" no seu projeto
//      (ex.: Assets/WFC/Editor/GreyboxTileGenerator.cs).
//   2. No menu do Unity, abra:  WFC  ▸  Greybox Tile Generator.
//   3. Ajuste Cell Size / Wall Height (opcional) e clique em "Gerar 10 tiles".
//   4. Os prefabs aparecem em Assets/WFC/GreyboxTiles/ prontos para usar.
//
//  O que ele cria:  geometria + colliders + materiais (piso/parede/detalhe).
//  O que NÃO cria:  sockets, ModuleDefinition, TileSet — isso vem na fase de
//  código do plugin. Estes prefabs são só a "casca" visual para prototipar.
//
//  Convenção: pivô no CENTRO da célula, topo do piso em y = 0, footprint =
//  Cell Size. Trocar o greybox por arte depois = só apontar o prefab do
//  ModuleDefinition para o modelo novo.
// ============================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class GreyboxTileGenerator : EditorWindow
{
    // ---- Parâmetros ajustáveis na janela ----
    // ATENÇÃO: estes defaults têm que bater com os prefabs que já estão em disco.
    // Regenerar com outro Cell Size troca o footprint de TODAS as peças e o andar
    // deixa de encaixar — o FloorSpec continuaria posicionando as células a cada 6 m.
    float cellSize       = 6f;    // largura/profundidade da célula (= Cell Size do grid)
    float wallHeight     = 5f;    // altura das paredes
    float wallThickness  = 0.2f;  // espessura das paredes
    float floorThickness = 0.1f;  // espessura da laje de piso
    float doorRatio      = 0.35f; // largura do vão da porta (fração do Cell Size)
    string outputFolder  = "Assets/WFC/GreyboxTiles";

    Material matFloor, matWall, matAccent;

    [MenuItem("WFC/Greybox Tile Generator")]
    static void Open() => GetWindow<GreyboxTileGenerator>("Greybox Tiles");

    void OnGUI()
    {
        EditorGUILayout.LabelField("Tileset mínimo — greybox", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Gera os 10 prefabs básicos (piso, parede, canto, corredor, beco, porta, " +
            "porta-de-corredor, escada + 2 coringas) com cubos primitivos. Idempotente: regenerar sobrescreve.",
            MessageType.Info);

        EditorGUILayout.Space();
        cellSize       = EditorGUILayout.FloatField("Cell Size", cellSize);
        wallHeight     = EditorGUILayout.FloatField("Wall Height", wallHeight);
        wallThickness  = EditorGUILayout.FloatField("Wall Thickness", wallThickness);
        floorThickness = EditorGUILayout.FloatField("Floor Thickness", floorThickness);
        doorRatio      = EditorGUILayout.Slider("Door Opening (ratio)", doorRatio, 0.15f, 0.7f);
        outputFolder   = EditorGUILayout.TextField("Output Folder", outputFolder);

        EditorGUILayout.Space();
        if (GUILayout.Button("Gerar 10 tiles (greybox)", GUILayout.Height(32)))
            Generate();
        if (GUILayout.Button("Apagar tiles gerados"))
            DeleteGenerated();
    }

    // ------------------------------------------------------------------ Build
    void Generate()
    {
        EnsureFolder(outputFolder);
        EnsureFolder(outputFolder + "/Materials");
        BuildMaterials();

        BuildFloorOpen();
        BuildWall();
        BuildCorner();
        BuildCorridor();
        BuildDeadEnd();
        BuildDoor();
        BuildDoorCorridor();
        BuildStairs();
        BuildSolidFill();
        BuildAir();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[GreyboxTileGenerator] 10 tiles gerados em {outputFolder}");
    }

    // Atalhos de dimensão
    float S => cellSize;
    float H => wallHeight;
    float T => wallThickness;
    float F => floorThickness;

    // Peças estruturais -------------------------------------------------------
    GameObject Floor() // laje de piso, topo em y=0
        => Cube("Floor", new Vector3(0, -F * 0.5f, 0), new Vector3(S, F, S), matFloor);

    GameObject WallNorth(Transform p) => Cube("Wall_N", new Vector3(0, H * 0.5f,  S * 0.5f - T * 0.5f), new Vector3(S, H, T), matWall, p);
    GameObject WallSouth(Transform p) => Cube("Wall_S", new Vector3(0, H * 0.5f, -S * 0.5f + T * 0.5f), new Vector3(S, H, T), matWall, p);
    GameObject WallWest (Transform p) => Cube("Wall_W", new Vector3(-S * 0.5f + T * 0.5f, H * 0.5f, 0), new Vector3(T, H, S), matWall, p);
    GameObject WallEast (Transform p) => Cube("Wall_E", new Vector3( S * 0.5f - T * 0.5f, H * 0.5f, 0), new Vector3(T, H, S), matWall, p);

    void BuildFloorOpen()
    {
        var root = Root("Tile_Floor_Open");
        Floor().transform.SetParent(root.transform, false);
        Save(root);
    }
    void BuildWall()
    {
        var root = Root("Tile_Wall");
        Floor().transform.SetParent(root.transform, false);
        WallNorth(root.transform);
        Save(root);
    }
    void BuildCorner()
    {
        var root = Root("Tile_Corner");
        Floor().transform.SetParent(root.transform, false);
        WallNorth(root.transform); WallWest(root.transform);
        Save(root);
    }
    void BuildCorridor()
    {
        var root = Root("Tile_Corridor");
        Floor().transform.SetParent(root.transform, false);
        WallNorth(root.transform); WallSouth(root.transform);
        Save(root);
    }
    void BuildDeadEnd()
    {
        var root = Root("Tile_DeadEnd");
        Floor().transform.SetParent(root.transform, false);
        WallNorth(root.transform); WallWest(root.transform); WallEast(root.transform);
        Save(root);
    }
    void BuildDoor()
    {
        var root = Root("Tile_Door");
        Floor().transform.SetParent(root.transform, false);
        DoorWallNorth(root.transform);
        Save(root);
    }

    // Porta dentro de um corredor: vão ao norte, aberto ao sul, parede nos dois lados.
    // Sem esta peça o esqueleto não consegue cravar porta num corredor de 1 célula de
    // largura — o Tile_Door normal tem os outros 3 lados abertos.
    void BuildDoorCorridor()
    {
        var root = Root("Tile_Door_Corridor");
        Floor().transform.SetParent(root.transform, false);
        DoorWallNorth(root.transform);
        WallWest(root.transform);
        WallEast(root.transform);
        Save(root);
    }

    // Parede norte com vão no meio + verga por cima.
    void DoorWallNorth(Transform parent)
    {
        float gap = S * doorRatio;
        float segW = (S - gap) * 0.5f;           // largura de cada meia-parede
        float segCx = (gap + segW) * 0.5f;       // centro X de cada segmento
        float zc = S * 0.5f - T * 0.5f;          // borda norte
        Cube("Wall_N_L", new Vector3(-segCx, H * 0.5f, zc), new Vector3(segW, H, T), matWall, parent);
        Cube("Wall_N_R", new Vector3( segCx, H * 0.5f, zc), new Vector3(segW, H, T), matWall, parent);
        Cube("Lintel", new Vector3(0, H * 0.83f, zc), new Vector3(gap, H * 0.34f, T), matAccent, parent);
    }
    void BuildStairs()
    {
        var root = Root("Tile_Stairs");
        Floor().transform.SetParent(root.transform, false);
        int n = 4;
        float d = S / n;
        for (int s = 0; s < n; s++)
        {
            float h  = (s + 1) * H / n;                 // sobe em direção a +Z
            float zc = -S * 0.5f + d * s + d * 0.5f;
            Cube($"Step_{s}", new Vector3(0, h * 0.5f, zc), new Vector3(S, h, d), matAccent, root.transform);
        }
        Save(root);
    }
    void BuildSolidFill()
    {
        var root = Root("Wildcard_SolidFill");
        Cube("Solid", new Vector3(0, H * 0.5f, 0), new Vector3(S, H, S), matWall, root.transform);
        Save(root);
    }
    void BuildAir()
    {
        var root = Root("Wildcard_Air"); // vazio: sem malha, sem collider
        Save(root);
    }

    // ------------------------------------------------------------ Utilitários
    GameObject Root(string name) => new GameObject(name);

    GameObject Cube(string name, Vector3 pos, Vector3 size, Material mat, Transform parent = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); // já vem com BoxCollider
        go.name = name;
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    void Save(GameObject root)
    {
        string path = $"{outputFolder}/{root.name}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        DestroyImmediate(root);
    }

    void BuildMaterials()
    {
        matFloor  = MakeMat("M_Greybox_Floor",  new Color(0.80f, 0.80f, 0.78f));
        matWall   = MakeMat("M_Greybox_Wall",   new Color(0.55f, 0.55f, 0.55f));
        matAccent = MakeMat("M_Greybox_Accent", new Color(0.62f, 0.58f, 0.48f));
    }

    Material MakeMat(string name, Color c)
    {
        string path = $"{outputFolder}/Materials/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            // Funciona tanto em URP quanto em Built-in
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(sh);
            AssetDatabase.CreateAsset(mat, path);
        }
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_Color"))     mat.SetColor("_Color", c);
        return mat;
    }

    void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    void DeleteGenerated()
    {
        string[] names = {
            "Tile_Floor_Open","Tile_Wall","Tile_Corner","Tile_Corridor","Tile_DeadEnd",
            "Tile_Door","Tile_Door_Corridor","Tile_Stairs","Wildcard_SolidFill","Wildcard_Air"
        };
        foreach (var n in names)
            AssetDatabase.DeleteAsset($"{outputFolder}/{n}.prefab");
        AssetDatabase.Refresh();
        Debug.Log("[GreyboxTileGenerator] Tiles removidos.");
    }
}
#endif
