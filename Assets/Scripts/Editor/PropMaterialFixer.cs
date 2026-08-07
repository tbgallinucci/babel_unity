// ============================================================================
//  PropMaterialFixer.cs  —  Editor do JOGO
//
//  Generaliza o conserto manual repetido hoje (obelisco Built-in Standard,
//  props Low Poly Dungeons Lite, corpo do torch): escaneia prefab(s) por
//  Renderer — inclusive dentro de prefab ANINHADO (Light_08 dentro do
//  TorchPrefab) — lista os materiais distintos encontrados e deixa escolher,
//  por material:
//
//    • Converter na FONTE — edita o asset original (shader + remapeia
//      propriedades conhecidas). Bom quando o material só é usado ali.
//    • Duplicar e repontar SÓ nos prefabs escaneados — cria uma cópia com o
//      shader novo e troca a referência apenas nos Renderers encontrados
//      nesta varredura; o material original continua intacto para qualquer
//      outro prefab que o use fora da seleção (é o que salvou o Light_08 de
//      afetar os outros 27 usuários do LowPolyDungeonsLite_MAIN hoje).
//
//  Remapeamento de propriedades é uma tabela pequena e explícita (não
//  genérica): cobre os dois padrões de shader que apareceram neste projeto
//  até agora (Built-in Standard, e o shader "estilo HDRP" de nome
//  _BaseColorMap/_NormalMap/_AOMap que o pacote de baixo poli usa). Textura
//  de rugosidade (_RoughnessMap) fica de fora de propósito — inverter
//  roughness→smoothness precisa saber o canal certo, não é 1:1, entra como
//  aviso no relatório em vez de adivinhar.
//
//  Menu:  Babel ▸ Prop Material Fixer
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Babel.Floor.EditorTools
{
    public sealed class PropMaterialFixer : EditorWindow
    {
        // ---------------------------------------------------------- remapeamento
        // Da esquerda (nome na fonte) pra direita (nome no destino, URP/Lit).
        private static readonly (string from, string to)[] TextureAliases =
        {
            ("_MainTex", "_BaseMap"),
            ("_BaseColorMap", "_BaseMap"),
            ("_BaseMap", "_BaseMap"),
            ("_BumpMap", "_BumpMap"),
            ("_NormalMap", "_BumpMap"),
            ("_MetallicGlossMap", "_MetallicGlossMap"),
            ("_EmissionMap", "_EmissionMap"),
            ("_OcclusionMap", "_OcclusionMap"),
            ("_AOMap", "_OcclusionMap"),
        };

        private static readonly (string from, string to)[] ColorAliases =
        {
            ("_Color", "_BaseColor"),
            ("_BaseColor", "_BaseColor"),
            ("_EmissionColor", "_EmissionColor"),
        };

        private static readonly (string from, string to)[] FloatAliases =
        {
            ("_Glossiness", "_Smoothness"),
            ("_Smoothness", "_Smoothness"),
            ("_Metallic", "_Metallic"),
            ("_BumpScale", "_BumpScale"),
            ("_OcclusionStrength", "_OcclusionStrength"),
            ("_Cutoff", "_Cutoff"),
        };

        // Shaders que já são "bons" — material nesses shaders não entra na lista
        // pra converter (evita reconverter algo que você já corrigiu na mão).
        private static readonly string[] AlreadyFineShaders =
        {
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Simple Lit",
            "Universal Render Pipeline/Unlit",
        };

        [System.Serializable]
        private sealed class MaterialRow
        {
            public Material material;
            public string currentShaderName;
            public bool convert = true;
            public bool duplicate = true;
            public bool overrideTint;
            public Color tint = Color.white;
            public bool overrideSmoothness;
            public float smoothness = 0.3f;
            public string status = "";
        }

        private readonly List<GameObject> scanRoots = new List<GameObject>();
        private string folderPath = "Assets/Prefabs/Props";
        private string duplicateSuffix = "_URP";
        private readonly List<MaterialRow> rows = new List<MaterialRow>();
        private Vector2 scroll;
        private string lastReport;

        [MenuItem("Babel/Prop Material Fixer")]
        private static void Open() => GetWindow<PropMaterialFixer>("Prop Material Fixer");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("1. De onde escanear", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Aponta uma pasta (escaneia todo .prefab dentro, recursivo) OU arrasta prefabs " +
                "específicos na lista abaixo. Os dois podem ser usados juntos.",
                MessageType.Info);

            folderPath = EditorGUILayout.TextField("Pasta", folderPath);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Prefabs específicos (opcional)");
            for (int i = 0; i < scanRoots.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                scanRoots[i] = (GameObject)EditorGUILayout.ObjectField(scanRoots[i], typeof(GameObject), false);
                if (GUILayout.Button("-", GUILayout.Width(24))) { scanRoots.RemoveAt(i); i--; }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("+ prefab")) scanRoots.Add(null);

            EditorGUILayout.Space();
            if (GUILayout.Button("Escanear materiais", GUILayout.Height(28)))
                Scan();

            if (rows.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("2. Materiais encontrados", EditorStyles.boldLabel);
                duplicateSuffix = EditorGUILayout.TextField("Sufixo ao duplicar", duplicateSuffix);

                scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(320));
                foreach (MaterialRow row in rows)
                    DrawRow(row);
                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space();
                if (GUILayout.Button("Aplicar tudo", GUILayout.Height(32)))
                    ApplyAll();
            }

            if (!string.IsNullOrEmpty(lastReport))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Relatório", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(lastReport, GUILayout.Height(140));
            }
        }

        private void DrawRow(MaterialRow row)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.ObjectField(row.material, typeof(Material), false);
            EditorGUILayout.LabelField("Shader atual", row.currentShaderName);

            row.convert = EditorGUILayout.Toggle("Converter pra URP/Lit", row.convert);
            using (new EditorGUI.DisabledScope(!row.convert))
            {
                row.duplicate = EditorGUILayout.Toggle(
                    new GUIContent("Duplicar (não editar a fonte)",
                        "Ligado: cria cópia e reponta só nos prefabs escaneados — o material " +
                        "original continua servindo qualquer outro prefab fora desta varredura. " +
                        "Desligado: edita o asset original direto (afeta TODO usuário dele)."),
                    row.duplicate);

                EditorGUILayout.BeginHorizontal();
                row.overrideTint = EditorGUILayout.Toggle(row.overrideTint, GUILayout.Width(16));
                using (new EditorGUI.DisabledScope(!row.overrideTint))
                    row.tint = EditorGUILayout.ColorField("Tint (Base Color)", row.tint);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                row.overrideSmoothness = EditorGUILayout.Toggle(row.overrideSmoothness, GUILayout.Width(16));
                using (new EditorGUI.DisabledScope(!row.overrideSmoothness))
                    row.smoothness = EditorGUILayout.Slider("Smoothness", row.smoothness, 0f, 1f);
                EditorGUILayout.EndHorizontal();
            }

            if (!string.IsNullOrEmpty(row.status))
                EditorGUILayout.LabelField(row.status, EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        // =====================================================================
        private void Scan()
        {
            var prefabPaths = new HashSet<string>();

            if (!string.IsNullOrEmpty(folderPath) && AssetDatabase.IsValidFolder(folderPath))
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folderPath }))
                    prefabPaths.Add(AssetDatabase.GUIDToAssetPath(guid));

            foreach (GameObject go in scanRoots)
                if (go != null)
                {
                    string path = AssetDatabase.GetAssetPath(go);
                    if (!string.IsNullOrEmpty(path)) prefabPaths.Add(path);
                }

            var found = new Dictionary<Material, MaterialRow>();
            var renderBuffer = new List<Renderer>();

            foreach (string path in prefabPaths)
            {
                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null) continue;

                renderBuffer.Clear();
                root.GetComponentsInChildren(true, renderBuffer);

                foreach (Renderer r in renderBuffer)
                foreach (Material mat in r.sharedMaterials)
                {
                    if (mat == null || mat.shader == null) continue;
                    if (found.ContainsKey(mat)) continue;
                    if (AlreadyFineShaders.Contains(mat.shader.name)) continue;

                    found[mat] = new MaterialRow
                    {
                        material = mat,
                        currentShaderName = mat.shader.name,
                    };
                }
            }

            rows.Clear();
            rows.AddRange(found.Values.OrderBy(r => r.material.name));
            lastReport = $"{prefabPaths.Count} prefab(s) escaneado(s), {rows.Count} material(is) " +
                         "fora de URP/Lit encontrado(s).";
        }

        // =====================================================================
        private void ApplyAll()
        {
            Shader target = Shader.Find("Universal Render Pipeline/Lit");
            if (target == null)
            {
                lastReport = "Shader 'Universal Render Pipeline/Lit' não encontrado no projeto — " +
                             "confirme que o pacote URP está instalado.";
                return;
            }

            var report = new System.Text.StringBuilder();
            var remap = new Dictionary<Material, Material>(); // original -> resultado (pra repontar)

            foreach (MaterialRow row in rows)
            {
                if (!row.convert || row.material == null) continue;

                // Uma linha ruim (material embutido em FBX sem 'Extract Materials', builtin,
                // pasta somente-leitura) não pode derrubar o lote inteiro — pula com aviso e
                // segue pro próximo material.
                try
                {
                    Material result = row.duplicate ? DuplicateMaterial(row) : row.material;
                    if (result == null)
                    {
                        row.status = "PULADO — não deu pra duplicar (ver relatório)";
                        continue;
                    }

                    ApplyShaderAndProperties(result, row, target, report);

                    if (row.duplicate) remap[row.material] = result;
                    row.status = row.duplicate ? $"→ duplicado: {result.name}" : "convertido na fonte";
                }
                catch (System.Exception e)
                {
                    row.status = $"ERRO — {e.Message}";
                    report.AppendLine($"{row.material.name}: falhou — {e.Message}");
                }
            }

            if (remap.Count > 0)
                RepointRenderers(remap, report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            lastReport = report.ToString();
            Debug.Log($"[PropMaterialFixer]\n{lastReport}");
        }

        private Material DuplicateMaterial(MaterialRow row)
        {
            string sourcePath = AssetDatabase.GetAssetPath(row.material);

            // Material embutido em FBX sem 'Extract Materials' (comum em pacote importado),
            // ou qualquer objeto que não seja um asset próprio no projeto: GetAssetPath volta
            // vazio, e CreateAsset num caminho vazio é a exception que travou o lote inteiro
            // antes desta blindagem existir.
            if (string.IsNullOrEmpty(sourcePath))
            {
                row.status = $"PULADO — '{row.material.name}' não tem asset .mat próprio " +
                              "(provavelmente embutido no FBX; marque 'Extract Materials' no " +
                              "importer do modelo, ou desmarque Duplicar pra editar na fonte).";
                return null;
            }

            string dir = System.IO.Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(dir)) dir = "Assets";

            // Pasta de origem fora de Assets/ (ex.: dentro de Packages/, somente-leitura) —
            // duplica pra uma pasta gerada sob Assets/ em vez de tentar escrever onde não pode.
            if (!dir.StartsWith("Assets"))
            {
                dir = "Assets/Materials_Generated";
                if (!AssetDatabase.IsValidFolder(dir))
                    AssetDatabase.CreateFolder("Assets", "Materials_Generated");
            }

            string safeName = string.Join("_", row.material.name.Split(System.IO.Path.GetInvalidFileNameChars()));
            string newPath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{safeName}{duplicateSuffix}.mat");

            if (string.IsNullOrEmpty(newPath))
            {
                row.status = $"PULADO — não deu pra gerar um caminho válido pra '{row.material.name}'.";
                return null;
            }

            var copy = new Material(row.material); // copia shader/propriedades atuais como ponto de partida
            AssetDatabase.CreateAsset(copy, newPath);
            return copy;
        }

        private static void ApplyShaderAndProperties(Material mat, MaterialRow row, Shader target,
                                                       System.Text.StringBuilder report)
        {
            // Guarda os valores ANTES de trocar o shader — trocar shader não migra
            // propriedade nenhuma sozinho, e depois da troca os getters antigos (_MainTex
            // etc.) podem não existir mais no shader novo.
            var textures = new Dictionary<string, Texture>();
            var colors = new Dictionary<string, Color>();
            var floats = new Dictionary<string, float>();

            foreach ((string from, string to) in TextureAliases)
                if (mat.HasProperty(from))
                {
                    Texture tex = mat.GetTexture(from);
                    if (tex != null) textures[to] = tex;
                }

            foreach ((string from, string to) in ColorAliases)
                if (mat.HasProperty(from)) colors[to] = mat.GetColor(from);

            foreach ((string from, string to) in FloatAliases)
                if (mat.HasProperty(from)) floats[to] = mat.GetFloat(from);

            mat.shader = target;

            foreach (KeyValuePair<string, Texture> kv in textures)
                if (mat.HasProperty(kv.Key)) mat.SetTexture(kv.Key, kv.Value);

            foreach (KeyValuePair<string, Color> kv in colors)
                if (mat.HasProperty(kv.Key)) mat.SetColor(kv.Key, kv.Value);

            foreach (KeyValuePair<string, float> kv in floats)
                if (mat.HasProperty(kv.Key)) mat.SetFloat(kv.Key, kv.Value);

            if (row.overrideTint && mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", row.tint);
            if (row.overrideSmoothness && mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", row.smoothness);

            EditorUtility.SetDirty(mat);
            report.AppendLine($"{mat.name}: shader → {target.name}, {textures.Count} textura(s) + " +
                              $"{colors.Count} cor(es) + {floats.Count} float(s) remapeado(s).");
        }

        /// <summary>
        /// Troca a referência SÓ nos Renderers dos prefabs desta varredura — o material
        /// original (se não foi editado na fonte) continua intacto pra qualquer prefab fora
        /// da seleção que o use.
        /// </summary>
        private void RepointRenderers(Dictionary<Material, Material> remap, System.Text.StringBuilder report)
        {
            var prefabPaths = new HashSet<string>();
            if (!string.IsNullOrEmpty(folderPath) && AssetDatabase.IsValidFolder(folderPath))
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folderPath }))
                    prefabPaths.Add(AssetDatabase.GUIDToAssetPath(guid));
            foreach (GameObject go in scanRoots)
                if (go != null)
                {
                    string path = AssetDatabase.GetAssetPath(go);
                    if (!string.IsNullOrEmpty(path)) prefabPaths.Add(path);
                }

            int touched = 0;
            var renderBuffer = new List<Renderer>();

            foreach (string path in prefabPaths)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                bool dirty = false;

                renderBuffer.Clear();
                root.GetComponentsInChildren(true, renderBuffer);

                foreach (Renderer r in renderBuffer)
                {
                    Material[] mats = r.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] != null && remap.TryGetValue(mats[i], out Material replacement))
                        {
                            mats[i] = replacement;
                            changed = true;
                        }
                    }
                    if (changed) { r.sharedMaterials = mats; dirty = true; touched++; }
                }

                if (dirty) PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }

            report.AppendLine($"{touched} Renderer(s) repontado(s) em {prefabPaths.Count} prefab(s).");
        }
    }
}
