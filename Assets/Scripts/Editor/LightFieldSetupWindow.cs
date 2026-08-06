// ============================================================================
//  LightFieldSetupWindow.cs  —  ferramenta de EDITOR (não vai para a build)
//
//  Instala a Etapa 3 (luz propagada por flood-fill) na pipeline: cria/acha o
//  material do shader de tela cheia e registra o FullScreenPassRendererFeature
//  no Renderer — sem isso o FloorLightField.cs calcula a textura certinho, mas
//  nada na tela soma ela em lugar nenhum.
//
//  Por que outra janela, não uma seção a mais na "Etapa 1": aquela já tem nome
//  e escopo fechados (prefab da tocha, URP asset, SSAO, ambiente da cena);
//  aqui é instalação de pipeline nova, categoria diferente.
//
//  Por que via SerializedObject/AssetDatabase e não editando o .asset do
//  Renderer no disco: é exatamente o mesmo motivo da Etapa 1 — o Unity só
//  reflete o disco depois de File → Save Project, e editar por fora arrisca
//  perder o que estiver pendente no editor. A receita de "adicionar renderer
//  feature" abaixo é a MESMA que o botão nativo "Add Renderer Feature" do
//  Inspector usa por baixo (replicada de Editor/ScriptableRendererDataEditor.cs
//  do pacote URP) — não é um jeito alternativo, é o jeito certo.
// ============================================================================

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Babel.EditorTools
{
    public sealed class LightFieldSetupWindow : EditorWindow
    {
        private const string ShaderName = "Babel/FloorLightField";
        private const string MaterialPath = "Assets/Shaders/M_FloorLightField.mat";
        private const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        private const string FeatureName = "Floor Light Field";

        [MenuItem("Babel/Iluminação/Instalar luz propagada (Etapa 3)")]
        private static void Open()
        {
            var window = GetWindow<LightFieldSetupWindow>(true, "Luz Propagada — Etapa 3");
            window.minSize = new Vector2(430f, 260f);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Cria o material do shader de tela cheia e registra o pass no Renderer " +
                "(Assets/Settings/PC_Renderer.asset). Idempotente — clicar de novo não duplica " +
                "nada, só confirma que já está instalado.\n\n" +
                "Depois de instalar: gere um andar (WFCFloorPreview/ArtPreviewPopulator/FloorDirector, " +
                "qualquer um que tenha um componente FloorLightField) e ajuste Intensity " +
                "Multiplier nesse componente olhando o resultado — comece baixo (0.02–0.08).",
                MessageType.Info);

            EditorGUILayout.Space(12f);

            Material existingMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            bool featureInstalled = IsFeatureInstalled(out UniversalRendererData rendererData);

            EditorGUILayout.LabelField("Material", existingMaterial != null ? "OK — " + MaterialPath : "Ainda não existe");
            EditorGUILayout.LabelField("Renderer Feature", featureInstalled ? "OK — já registrada" : "Ainda não registrada");

            EditorGUILayout.Space(12f);

            if (GUILayout.Button("Instalar / verificar", GUILayout.Height(32)))
                Install();
        }

        private void Install()
        {
            Material material = EnsureMaterial();
            if (material == null) return;

            EnsureFeature(material);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[LightFieldSetupWindow] Etapa 3 instalada. Salve o projeto " +
                      "(File → Save Project) para gravar em disco.");
        }

        // =====================================================================
        //  Material
        // =====================================================================

        private static Material EnsureMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) return material;

            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[LightFieldSetupWindow] Shader '{ShaderName}' não encontrado. " +
                               "Confira se Assets/Shaders/FloorLightField.shader existe e compilou " +
                               "sem erro (veja o Console) antes de instalar.");
                return null;
            }

            material = new Material(shader) { name = "M_FloorLightField" };
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        // =====================================================================
        //  Renderer Feature
        // =====================================================================

        private static bool IsFeatureInstalled(out UniversalRendererData rendererData)
        {
            rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null) return false;

            foreach (ScriptableRendererFeature feature in rendererData.rendererFeatures)
                if (feature is FullScreenPassRendererFeature && feature.name == FeatureName)
                    return true;

            return false;
        }

        private static void EnsureFeature(Material material)
        {
            if (IsFeatureInstalled(out UniversalRendererData rendererData))
            {
                // Já instalada — só garante que aponta pro material certo, caso o material
                // tenha sido recriado (path igual, referência velha) numa sessão anterior.
                foreach (ScriptableRendererFeature f in rendererData.rendererFeatures)
                    if (f is FullScreenPassRendererFeature existing && f.name == FeatureName)
                        existing.passMaterial = material;

                EditorUtility.SetDirty(rendererData);
                return;
            }

            if (rendererData == null)
            {
                Debug.LogError($"[LightFieldSetupWindow] Renderer não encontrado em {RendererPath}.");
                return;
            }

            // Receita replicada de ScriptableRendererDataEditor.AddComponent (URP,
            // Editor/ScriptableRendererDataEditor.cs) — é o que o botão "Add Renderer
            // Feature" do Inspector chama por baixo.
            var feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            feature.name = FeatureName;
            feature.hideFlags |= HideFlags.HideInHierarchy;

            // Additive puro (Blend One One no shader): não precisa da cópia da tela — economiza
            // um pass inteiro de blit por frame. Precisa de Depth pra reconstruir posição de
            // mundo. BeforeRenderingTransparents: soma no opaco antes de qualquer transparente
            // desenhar por cima (partícula, vidro) — esses não recebem o efeito, é escopo
            // conhecido, não bug (ver cabeçalho do .shader).
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingTransparents;
            feature.fetchColorBuffer = false;
            feature.requirements = ScriptableRenderPassInput.Depth;
            feature.passMaterial = material;
            feature.passIndex = 0;
            feature.bindDepthStencilAttachment = false;

            AssetDatabase.AddObjectToAsset(feature, rendererData);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

            var serializedRenderer = new SerializedObject(rendererData);
            SerializedProperty featuresProp = serializedRenderer.FindProperty("m_RendererFeatures");
            SerializedProperty mapProp = serializedRenderer.FindProperty("m_RendererFeatureMap");

            // Cresce a lista primeiro, só depois atribui — é assim que lista serializada do
            // Unity espera ser mexida via SerializedProperty (mesma ordem do código original).
            featuresProp.arraySize++;
            featuresProp.GetArrayElementAtIndex(featuresProp.arraySize - 1).objectReferenceValue = feature;

            mapProp.arraySize++;
            mapProp.GetArrayElementAtIndex(mapProp.arraySize - 1).longValue = localId;

            serializedRenderer.ApplyModifiedProperties();
            rendererData.SetDirty();
            EditorUtility.SetDirty(rendererData);
        }
    }
}
