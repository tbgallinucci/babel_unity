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
using WFC.Runtime;

public class GreyboxTileGenerator : EditorWindow
{
    // ---- Parâmetros ajustáveis na janela ----
    // ATENÇÃO: estes defaults têm que bater com os prefabs que já estão em disco.
    // Regenerar com outro Cell Size troca o footprint de TODAS as peças e o andar
    // deixa de encaixar — o FloorSpec continuaria posicionando as células a cada 6 m.
    float cellSize       = 6f;    // largura/profundidade da célula (= Cell Size do grid)
    float wallHeight     = 10f;   // altura das paredes
    float wallThickness  = 0.6f;  // espessura das paredes
    float floorThickness = 0.6f;  // espessura da laje de piso
    float doorRatio      = 0.35f; // largura do vão da porta (fração do Cell Size)
    float doorHeight     = 3.3f;  // altura do vão, em METROS — independente de Wall Height
    float wallLightOffset = 1.0f; // distância do anchor de luz até o centro da parede (ver WallLight)
    float wallLightHeightRatio = 0.5f; // altura do anchor de luz, fração de Wall Height
    float chestAnchorInset = 1.0f; // distância do anchor de baú até a quina, na diagonal (ver CornerChestAnchor)
    float columnShaftRadius = 0.5f;  // raio da haste central da coluna
    float columnCapRadius   = 0.9f;  // raio da base/capitel (maior que a haste)
    float columnCapHeight   = 0.4f;  // altura de cada disco de base/capitel (bem menor que a haste)
    string outputFolder  = "Assets/WFC/GreyboxTiles";

    Material matFloor, matWall, matAccent;

    [MenuItem("WFC/Greybox Tile Generator")]
    static void Open() => GetWindow<GreyboxTileGenerator>("Greybox Tiles");

    void OnGUI()
    {
        EditorGUILayout.LabelField("Tileset mínimo — greybox", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Gera os 11 prefabs básicos (piso, parede, canto, corredor, beco, porta, " +
            "porta-de-corredor, escada, coluna + 2 coringas) + 1 prop (filler de quina, sem " +
            "piso — ver FloorSpec.cornerFillerPrefab) com primitivos. Idempotente: regenerar sobrescreve.",
            MessageType.Info);

        EditorGUILayout.Space();
        cellSize       = EditorGUILayout.FloatField("Cell Size", cellSize);
        wallHeight     = EditorGUILayout.FloatField("Wall Height", wallHeight);
        wallThickness  = EditorGUILayout.FloatField("Wall Thickness", wallThickness);
        floorThickness = EditorGUILayout.FloatField("Floor Thickness", floorThickness);
        doorRatio      = EditorGUILayout.Slider("Door Opening (ratio)", doorRatio, 0.15f, 0.7f);
        doorHeight     = EditorGUILayout.FloatField(
            new GUIContent("Door Opening Height", "Altura do vão em metros — fixa, NÃO acompanha Wall Height. " +
                            "Precisa ser menor que Wall Height (o resto vira verga)."),
            doorHeight);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Anchor de luz de parede", EditorStyles.boldLabel);
        wallLightOffset = EditorGUILayout.FloatField(
            new GUIContent("Wall Light Offset", "Distância do anchor até o centro da parede, em metros. " +
                            "Ajuste pra bater com o quanto o prefab da tocha precisa recuar da parede."),
            wallLightOffset);
        wallLightHeightRatio = EditorGUILayout.Slider(
            new GUIContent("Wall Light Height", "Altura do anchor, como fração de Wall Height (0 = piso, 1 = teto)."),
            wallLightHeightRatio, 0f, 1f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Anchor de baú de canto", EditorStyles.boldLabel);
        chestAnchorInset = EditorGUILayout.FloatField(
            new GUIContent("Chest Anchor Inset", "Distância do anchor até a quina da sala, na diagonal " +
                            "(afastando das duas paredes ao mesmo tempo), em metros."),
            chestAnchorInset);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Coluna (Tile_Column + Prop_ColumnFiller)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Mesmos parâmetros valem pras duas peças: Tile_Column (com piso, decorativa, o " +
            "solver do WFC escolhe onde entra) e Prop_ColumnFiller (sem piso, plantada à parte " +
            "nos vértices do grid — ver FloorSpec.cornerFillerPrefab). Se aumentar o Wall " +
            "Thickness, considere aumentar o Cap Radius junto pra continuar cobrindo a costura.",
            MessageType.None);
        columnShaftRadius = EditorGUILayout.FloatField(
            new GUIContent("Shaft Radius", "Raio da haste central do pilar, em metros."), columnShaftRadius);
        columnCapRadius = EditorGUILayout.FloatField(
            new GUIContent("Cap Radius", "Raio dos discos de base/capitel nas pontas — maior que a haste. " +
                            "No Prop_ColumnFiller é o que determina se a costura de parede fica coberta."),
            columnCapRadius);
        columnCapHeight = EditorGUILayout.FloatField(
            new GUIContent("Cap Height", "Altura de cada disco de base/capitel — bem menor que a haste."),
            columnCapHeight);

        outputFolder   = EditorGUILayout.TextField("Output Folder", outputFolder);

        EditorGUILayout.Space();
        if (GUILayout.Button("Gerar 11 tiles (greybox)", GUILayout.Height(32)))
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
        BuildColumn();
        BuildColumnFiller();
        BuildSolidFill();
        BuildAir();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[GreyboxTileGenerator] 11 tiles + 1 prop gerados em {outputFolder}");
    }

    // Atalhos de dimensão
    float S => cellSize;
    float H => wallHeight;
    float T => wallThickness;
    float F => floorThickness;

    // Peças estruturais -------------------------------------------------------
    // SEM peça de teto de propósito: o teto de produção é uma única "tampa" (plane) sobre
    // o footprint inteiro do grid, feita depois de instanciar (ver WFCFloorGenerator.
    // BuildCeiling / CeilingBuilder) — muito mais barata que uma peça de teto por célula, e
    // como o de dentro de uma sala nunca vê acima da parede, o resultado visual é idêntico.
    // O harness WFCFloorPreview não tinha essa etapa; é aí que mora o "sem teto" do preview.
    GameObject Floor() // laje de piso, topo em y=0
        => Cube("Floor", new Vector3(0, -F * 0.5f, 0), new Vector3(S, F, S), matFloor);

    GameObject WallNorth(Transform p) => Cube("Wall_N", new Vector3(0, H * 0.5f,  S * 0.5f - T * 0.5f), new Vector3(S, H, T), matWall, p);
    GameObject WallSouth(Transform p) => Cube("Wall_S", new Vector3(0, H * 0.5f, -S * 0.5f + T * 0.5f), new Vector3(S, H, T), matWall, p);
    GameObject WallWest (Transform p) => Cube("Wall_W", new Vector3(-S * 0.5f + T * 0.5f, H * 0.5f, 0), new Vector3(T, H, S), matWall, p);
    GameObject WallEast (Transform p) => Cube("Wall_E", new Vector3( S * 0.5f - T * 0.5f, H * 0.5f, 0), new Vector3(T, H, S), matWall, p);

    // Marca "cabe luz de parede aqui" para o BasicLightingPopulator (jogo). O plugin só
    // planta o anchor; quem decide se acende luz ali — respeitando espaçamento e as
    // paredes de canto/beco, que nunca ganham este anchor — é o populador.
    // `facePos` é o centro XZ da parede (mesmo valor usado no Cube da parede); `inward`
    // é a direção para dentro da sala, usada tanto para afastar o anchor da parede quanto
    // para orientar a luz.
    GameObject WallLight(Transform parent, string name, Vector3 facePos, Vector3 inward)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(facePos.x, H * wallLightHeightRatio, facePos.z) + inward * wallLightOffset;
        go.transform.localRotation = Quaternion.LookRotation(inward, Vector3.up);

        var anchor = go.AddComponent<SpawnAnchor>();
        anchor.kind = SpawnAnchorKind.Light;
        anchor.clearanceRadius = 0.3f;
        return go;
    }

    // Marca "cabe baú aqui" — só nasce em Tile_Corner, de propósito: é a peça que só existe
    // exatamente na quina de uma sala. `cornerXZ` é o ponto onde as duas paredes se encontram
    // (face interna); `inward` é a diagonal que afasta o anchor das DUAS paredes ao mesmo
    // tempo, pra dentro da sala. Mesmo idioma do WallLight, só que a peça tem só uma quina —
    // uma chamada por Tile_Corner basta, o WFC gira a peça inteira (anchor incluso) quando
    // precisa da quina oposta.
    GameObject CornerChestAnchor(Transform parent, string name, Vector3 cornerXZ, Vector3 inward)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Vector3 dir = inward.normalized;
        go.transform.localPosition = new Vector3(cornerXZ.x, 0f, cornerXZ.z) + dir * chestAnchorInset;
        go.transform.localRotation = Quaternion.LookRotation(dir, Vector3.up);

        var anchor = go.AddComponent<SpawnAnchor>();
        anchor.kind = SpawnAnchorKind.Chest;
        anchor.clearanceRadius = 0.8f;
        return go;
    }

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
        WallLight(root.transform, "Anchor_Light_N", new Vector3(0, 0, S * 0.5f - T * 0.5f), Vector3.back);
        Save(root);
    }
    void BuildCorner()
    {
        // De propósito SEM anchor de luz: parede em quina é a regra "nunca em canto"
        // do BasicLightingPopulator satisfeita de graça — se a peça não tem anchor,
        // não tem onde a luz nascer.
        var root = Root("Tile_Corner");
        Floor().transform.SetParent(root.transform, false);
        WallNorth(root.transform); WallWest(root.transform);
        // Quina Noroeste (paredes Norte + Oeste): o ponto onde as faces internas se
        // encontram é (-S/2+T, S/2-T); a diagonal que foge das duas paredes ao mesmo
        // tempo, pra dentro da sala (que fica a Sul/Leste), é (+X, -Z).
        CornerChestAnchor(root.transform, "Anchor_Chest",
            new Vector3(-S * 0.5f + T, 0, S * 0.5f - T), new Vector3(1f, 0f, -1f));
        Save(root);
    }
    void BuildCorridor()
    {
        var root = Root("Tile_Corridor");
        Floor().transform.SetParent(root.transform, false);
        WallNorth(root.transform); WallSouth(root.transform);
        // Paredes paralelas (Norte/Sul) — o caso "corredor" da regra de luz.
        WallLight(root.transform, "Anchor_Light_N", new Vector3(0, 0, S * 0.5f - T * 0.5f), Vector3.back);
        WallLight(root.transform, "Anchor_Light_S", new Vector3(0, 0, -S * 0.5f + T * 0.5f), Vector3.forward);
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
        // Paredes paralelas Oeste/Leste (a porta fica na Norte — sem anchor ali,
        // luz em cima do vão da porta não faz sentido).
        WallLight(root.transform, "Anchor_Light_W", new Vector3(-S * 0.5f + T * 0.5f, 0, 0), Vector3.right);
        WallLight(root.transform, "Anchor_Light_E", new Vector3(S * 0.5f - T * 0.5f, 0, 0), Vector3.left);
        Save(root);
    }

    // Parede norte com vão no meio + verga por cima.
    void DoorWallNorth(Transform parent)
    {
        float gap = S * doorRatio;
        float segW = (S - gap) * 0.5f;           // largura de cada meia-parede
        float segCx = (gap + segW) * 0.5f;       // centro X de cada segmento
        float zc = S * 0.5f - T * 0.5f;          // borda norte

        // Vão em metros ABSOLUTOS, não fração de H — Wall Height só decide onde a
        // verga (o que sobra até o teto) começa, não a altura de passagem em si.
        // Clamp: se doorHeight >= H (paredes baixas com vão configurado alto), a
        // verga vira uma tira mínima em vez de altura negativa/geometria invertida.
        float doorH = Mathf.Min(doorHeight, H - 0.05f);
        float lintelH = H - doorH;
        float lintelCy = doorH + lintelH * 0.5f;

        Cube("Wall_N_L", new Vector3(-segCx, H * 0.5f, zc), new Vector3(segW, H, T), matWall, parent);
        Cube("Wall_N_R", new Vector3( segCx, H * 0.5f, zc), new Vector3(segW, H, T), matWall, parent);
        Cube("Lintel", new Vector3(0, lintelCy, zc), new Vector3(gap, lintelH, T), matAccent, parent);
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
    // Pilar decorativo: haste fina + disco largo/baixo em cada ponta (base e capitel),
    // igual coluna clássica. Pivô no centro da célula, mesma convenção das outras peças —
    // a haste some dentro do disco nas pontas (a soma das alturas cobre 0..H, sem gap).
    //
    // Restrição de spawn (ficar só em cantos de corredor a 90°, tipo Tile_Corner) NÃO é
    // feita aqui: este gerador só cria a geometria do prefab, não ModuleDefinition/FloorSpec.
    // Ver "Opcional — restringir onde ele aparece" em
    // Docs/Development/Engine/Planning/Guide - Adicionar Modulo Pilar ao WFC Tileset.md
    // pra registrar a regra de sala manualmente depois de gerar este prefab.
    void BuildColumn()
    {
        var root = Root("Tile_Column");
        Floor().transform.SetParent(root.transform, false);
        BuildColumnGeometry(root.transform);
        Save(root);
    }

    // Base + haste + capitel — compartilhado entre Tile_Column (peça normal, com Floor,
    // escolhida pelo solver) e Prop_ColumnFiller (sem Floor, plantado à parte nos vértices
    // do grid). Caps não podem "comer" a coluna inteira: clamp na altura da haste.
    void BuildColumnGeometry(Transform parent)
    {
        float capH   = columnCapHeight;
        float shaftH = Mathf.Max(0.05f, H - capH * 2f);
        float shaftCy = capH + shaftH * 0.5f;

        Cylinder("Column_Base", new Vector3(0, capH * 0.5f, 0), columnCapRadius, capH, matAccent, parent);
        Cylinder("Column_Shaft", new Vector3(0, shaftCy, 0), columnShaftRadius, shaftH, matWall, parent);
        Cylinder("Column_Capital", new Vector3(0, H - capH * 0.5f, 0), columnCapRadius, capH, matAccent, parent);
    }

    // Mesma geometria do Tile_Column (base + haste + capitel), mas SEM o cubo de Floor.
    // Existe pra um uso diferente: não é escolhido pelo solver do WFC pra uma célula, é
    // plantado à PARTE (TileInstancer.PlaceCornerFillers, via FloorSpec.cornerFillerPrefab)
    // exatamente em cima dos VÉRTICES do grid onde duas paredes perpendiculares de células
    // diferentes se encontram — tampa a espessura de parede que fica exposta nessa costura.
    // Se tivesse o Floor de 6×6m do Tile_Column normal, cada vértice ganharia uma laje de
    // piso sobreposta ao piso que já existe ali — por isso este prop não tem chão nenhum.
    void BuildColumnFiller()
    {
        var root = Root("Prop_ColumnFiller");
        BuildColumnGeometry(root.transform);
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

    // Cylinder primitivo da Unity: diâmetro 1 e altura 2 por padrão (raio 0.5, meio-altura 1),
    // então scale.x/z = radius*2 e scale.y = height*0.5 dão o raio/altura finais pedidos.
    // Já vem com CapsuleCollider — mesma lógica do Cube() pro NavMesh excluir a área do pilar.
    GameObject Cylinder(string name, Vector3 pos, float radius, float height, Material mat, Transform parent = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
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

        // Fosco de propósito: o default do URP/Lit é Smoothness 0.5, que reflete o skybox
        // padrão como um espelho borrado — mais visível em ângulo raso (efeito Fresnel), que
        // é bem perto do chão/canto de sala. Sem isso, sala "escura" ainda parece ter luz
        // vazando do chão — não é luz nenhuma, é reflexo especular. Kit greybox não deveria
        // brilhar de qualquer forma; arte de produção troca isso material por material depois.
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.05f);

        // GPU Instancing: um andar instancia centenas de peças que repetem malha+material,
        // que é exatamente o caso que o instancing resolve. Ligado aqui (e não à mão no
        // Inspector) porque estes materiais são GERADOS — marcar na mão se perderia na
        // próxima regeração. Materiais de arte autorados fora daqui precisam do checkbox
        // "Enable GPU Instancing" marcado manualmente.
        mat.enableInstancing = true;

        EditorUtility.SetDirty(mat);
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
            "Tile_Door","Tile_Door_Corridor","Tile_Stairs","Tile_Column","Prop_ColumnFiller",
            "Wildcard_SolidFill","Wildcard_Air"
        };
        foreach (var n in names)
            AssetDatabase.DeleteAsset($"{outputFolder}/{n}.prefab");
        AssetDatabase.Refresh();
        Debug.Log("[GreyboxTileGenerator] Tiles removidos.");
    }
}
#endif
