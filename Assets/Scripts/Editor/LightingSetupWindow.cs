// ============================================================================
//  LightingSetupWindow.cs  —  ferramenta de EDITOR (não vai para a build)
//
//  Aplica a configuração de iluminação da Etapa 1 de uma vez: prefab da tocha,
//  URP asset, SSAO e ambiente da cena.
//
//  Por que uma JANELA e não um menu que aplica valores fixos: parte disto é
//  ajuste fino que se faz olhando a tela (ângulo, range, intensidade), e quem
//  está ajustando é você. A janela vem pré-preenchida com o valor recomendado,
//  mas nada é aplicado sem clique — e cada bloco tem seu próprio toggle, então
//  dá para consertar só o bias sem mexer no que você já afinou.
//
//  E por que via AssetDatabase e não editando o .prefab/.asset no disco: o
//  Unity mantém o estado em memória e só grava em File → Save Project. Escrever
//  no disco por fora perderia o que estivesse pendente — ou seria perdido por
//  ele. Passando pelo AssetDatabase, os dois lados enxergam a mesma coisa.
// ============================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Babel.VFX;

namespace Babel.EditorTools
{
    public sealed class LightingSetupWindow : EditorWindow
    {
        private const string TorchPrefabPath = "Assets/Prefabs/Props/TestTorchLight.prefab";
        private const string RpAssetPath = "Assets/Settings/PC_RPAsset.asset";
        private const string RendererPath = "Assets/Settings/PC_Renderer.asset";

        // ---- Tocha ----------------------------------------------------------
        private bool doTorch = true;
        private bool torchFixCorrectness = true;   // bias, Realtime, Point sem sombra, TorchLight
        private bool torchApplyTuning = true;      // ângulo, range, intensidade

        private float spotOuterAngle = 110f;
        private float spotInnerAngle = 45f;
        private float spotRange = 25f;
        private float spotIntensity = 10f;
        private float pointRange = 22f;
        private float pointIntensity = 8f;

        // ---- URP asset ------------------------------------------------------
        private bool doRpAsset = true;
        private float shadowDistance = 35f;
        private int additionalShadowAtlas = 2048;
        private int mainLightShadowmap = 1024;
        private int cascadeCount = 2;

        // ---- SSAO -----------------------------------------------------------
        private bool doSsao = true;
        private float ssaoRadius = 0.6f;
        private float ssaoIntensity = 0.7f;
        private bool ssaoDownsample = true;

        // ---- Cena -----------------------------------------------------------
        private bool doScene = true;
        private Color ambientColor = new Color(0.018f, 0.020f, 0.026f, 1f);
        private bool enableFog = true;
        private Color fogColor = new Color(0.012f, 0.013f, 0.018f, 1f);
        private float fogDensity = 0.012f;

        private Vector2 scroll;

        [MenuItem("Babel/Iluminação/Configurar iluminação (Etapa 1)")]
        private static void Open()
        {
            var window = GetWindow<LightingSetupWindow>(true, "Iluminação — Etapa 1");
            window.minSize = new Vector2(430f, 560f);
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.HelpBox(
                "Aplica a configuração de iluminação da Etapa 1. Nada é gravado até você clicar em " +
                "Aplicar, e cada bloco tem seu próprio toggle.\n\n" +
                "Depois de aplicar, salve com File → Save Project.",
                MessageType.Info);

            DrawPresets();
            DrawTorch();
            DrawRpAsset();
            DrawSsao();
            DrawScene();

            EditorGUILayout.Space(12f);
            if (GUILayout.Button("Aplicar", GUILayout.Height(32f))) Apply();

            EditorGUILayout.EndScrollView();
        }

        // =====================================================================
        //  Presets
        // =====================================================================

        private void DrawPresets()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Voltar ao ajuste que estava bom",
                        "Preenche os campos com o estado ANTERIOR a esta sessão: Spot 110°/range 15, " +
                        "Point range 3, SSAO e ambiente como estavam. Ainda precisa clicar em Aplicar."),
                        GUILayout.Height(24f)))
                    LoadRollbackPreset();

                if (GUILayout.Button(new GUIContent("Recomendado (Etapa 1)",
                        "Os valores que eu havia proposto. Range maior no Spot compra raio de sombra, " +
                        "mas o Point largo achata o contraste — foi o que deu errado."),
                        GUILayout.Height(24f)))
                    LoadRecommendedPreset();
            }

            EditorGUILayout.HelpBox(
                "O Point é o botão de contraste. Range alto nele = preenchimento sem sombra por toda " +
                "parte, e a cena chapa. Suba o Spot para ganhar raio de sombra; mantenha o Point curto.",
                MessageType.None);
        }

        /// <summary>
        /// O estado que estava bom antes desta sessão. Ponto de partida seguro para voltar a
        /// mexer numa variável de cada vez, em vez de mudar oito coisas juntas.
        /// </summary>
        private void LoadRollbackPreset()
        {
            spotOuterAngle = 110f;
            spotInnerAngle = 60f;
            spotRange = 15f;
            spotIntensity = 10f;
            pointRange = 3f;
            pointIntensity = 8f;

            shadowDistance = 50f;
            additionalShadowAtlas = 4096;
            mainLightShadowmap = 4096;
            cascadeCount = 4;

            ssaoRadius = 0.3f;
            ssaoIntensity = 0.4f;
            ssaoDownsample = false;

            ambientColor = Color.black;
            enableFog = false;

            // O bias 0.97/0.71 NÃO volta de propósito: ele era compensação do ângulo de 170°,
            // e a 110° é ele quem descola a sombra da base do objeto. Deixe "corrigir o que
            // está objetivamente errado" marcado.
            torchFixCorrectness = true;
            torchApplyTuning = true;
        }

        private void LoadRecommendedPreset()
        {
            spotOuterAngle = 110f;
            spotInnerAngle = 45f;
            spotRange = 25f;
            spotIntensity = 10f;
            pointRange = 22f;
            pointIntensity = 8f;

            shadowDistance = 35f;
            additionalShadowAtlas = 2048;
            mainLightShadowmap = 1024;
            cascadeCount = 2;

            ssaoRadius = 0.6f;
            ssaoIntensity = 0.7f;
            ssaoDownsample = true;

            ambientColor = new Color(0.018f, 0.020f, 0.026f, 1f);
            enableFog = true;

            torchFixCorrectness = true;
            torchApplyTuning = true;
        }

        // =====================================================================
        //  Interface
        // =====================================================================

        private void DrawTorch()
        {
            EditorGUILayout.Space(8f);
            doTorch = EditorGUILayout.BeginToggleGroup("Prefab da tocha", doTorch);

            EditorGUILayout.LabelField(TorchPrefabPath, EditorStyles.miniLabel);

            torchFixCorrectness = EditorGUILayout.ToggleLeft(
                new GUIContent("Corrigir o que está objetivamente errado",
                    "Bias 0.97/0.71 → padrão do pipeline (era compensação do ângulo de 170°, e a " +
                    "170° o texel de sombra tinha 0,89 m); modo Mixed → Realtime (não existe bake " +
                    "neste projeto); Point sem sombra; posição local da raiz zerada; componente " +
                    "TorchLight adicionado se faltar."),
                torchFixCorrectness);

            torchApplyTuning = EditorGUILayout.ToggleLeft(
                new GUIContent("Aplicar também ângulo / range / intensidade",
                    "DESLIGUE se você já afinou esses valores no Inspector e não quer perdê-los."),
                torchApplyTuning);

            using (new EditorGUI.DisabledScope(!torchApplyTuning))
            {
                EditorGUI.indentLevel++;
                spotOuterAngle = EditorGUILayout.Slider("Spot — Outer Angle", spotOuterAngle, 20f, 170f);
                spotInnerAngle = EditorGUILayout.Slider("Spot — Inner Angle", spotInnerAngle, 1f, spotOuterAngle);
                spotRange = EditorGUILayout.FloatField(
                    new GUIContent("Spot — Range", "É o far plane do shadow frustum: o raio em que a " +
                                                   "sombra existe é EXATAMENTE este valor."), spotRange);
                spotIntensity = EditorGUILayout.FloatField("Spot — Intensity", spotIntensity);

                EditorGUILayout.Space(4f);
                pointRange = EditorGUILayout.FloatField(
                    new GUIContent("Point — Range", "Mantenha ≤ Range do Spot. Fill mais largo que a " +
                                                    "região com sombra cria um anel aceso sem oclusão — " +
                                                    "é o que faz a sombra parecer que 'acaba cedo'."), pointRange);
                pointIntensity = EditorGUILayout.FloatField("Point — Intensity", pointIntensity);

                DrawTexelEstimate();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndToggleGroup();
        }

        /// <summary>
        /// O número que decide se a sombra fica nítida ou vira mancha. URP calcula o bias a
        /// partir de `frustumSize = tan(outer/2) * range` (ShadowUtils.ExtractSpotLightMatrix),
        /// então a densidade de texel é 2·tan(outer/2)·range / resolução.
        /// </summary>
        private void DrawTexelEstimate()
        {
            const float assumedResolution = 1024f; // tier High, o do prefab
            float texel = 2f * Mathf.Tan(spotOuterAngle * 0.5f * Mathf.Deg2Rad) * spotRange / assumedResolution;

            string verdict = texel < 0.10f ? "nítida"
                           : texel < 0.20f ? "aceitável"
                           : "borrada — feche o ângulo ou encurte o range";

            EditorGUILayout.HelpBox(
                $"Texel do shadow map ≈ {texel * 100f:F1} cm (a 1024) — {verdict}.\n" +
                "Referência: parede de 0,6 m, vão de porta de 3,3 m.",
                texel < 0.20f ? MessageType.None : MessageType.Warning);
        }

        private void DrawRpAsset()
        {
            EditorGUILayout.Space(8f);
            doRpAsset = EditorGUILayout.BeginToggleGroup("URP asset", doRpAsset);
            EditorGUILayout.LabelField(RpAssetPath, EditorStyles.miniLabel);

            shadowDistance = EditorGUILayout.FloatField(
                new GUIContent("Shadow Distance", "Distância da CÂMERA, não da luz. Os últimos ~11% " +
                                                  "desvanecem (Cascade Border), então mantenha acima do " +
                                                  "Range do Spot com folga."), shadowDistance);

            additionalShadowAtlas = EditorGUILayout.IntPopup("Atlas de luzes adicionais", additionalShadowAtlas,
                new[] { "1024", "2048", "4096" }, new[] { 1024, 2048, 4096 });

            mainLightShadowmap = EditorGUILayout.IntPopup("Shadowmap da luz principal", mainLightShadowmap,
                new[] { "1024", "2048", "4096" }, new[] { 1024, 2048, 4096 });

            cascadeCount = EditorGUILayout.IntSlider("Cascades", cascadeCount, 1, 4);

            EditorGUILayout.HelpBox(
                "Não existe Directional Light na cena do andar — shadowmap principal e cascades " +
                "são peso morto até existir uma.", MessageType.None);

            EditorGUILayout.EndToggleGroup();
        }

        private void DrawSsao()
        {
            EditorGUILayout.Space(8f);
            doSsao = EditorGUILayout.BeginToggleGroup("SSAO", doSsao);
            EditorGUILayout.LabelField(RendererPath, EditorStyles.miniLabel);

            ssaoRadius = EditorGUILayout.Slider("Radius", ssaoRadius, 0.05f, 2f);
            ssaoIntensity = EditorGUILayout.Slider("Intensity", ssaoIntensity, 0f, 2f);
            ssaoDownsample = EditorGUILayout.Toggle("Downsample", ssaoDownsample);

            EditorGUILayout.HelpBox(
                "Radius 0.3 é invisível num mundo de células de 6 m e paredes de 10 m. AO é o que " +
                "assenta a geometria nos cantos sem custar luz nenhuma.", MessageType.None);

            EditorGUILayout.EndToggleGroup();
        }

        private void DrawScene()
        {
            EditorGUILayout.Space(8f);
            doScene = EditorGUILayout.BeginToggleGroup("Ambiente da cena aberta", doScene);
            EditorGUILayout.LabelField(SceneManager.GetActiveScene().name, EditorStyles.miniLabel);

            ambientColor = EditorGUILayout.ColorField(
                new GUIContent("Ambient", "Hoje está em (0,0,0). Com preto absoluto, todo erro de " +
                                          "iluminação vira void e toda célula descoberta some."),
                ambientColor);

            enableFog = EditorGUILayout.Toggle("Fog", enableFog);
            using (new EditorGUI.DisabledScope(!enableFog))
            {
                EditorGUI.indentLevel++;
                fogColor = EditorGUILayout.ColorField("Fog Color", fogColor);
                fogDensity = EditorGUILayout.Slider("Fog Density", fogDensity, 0f, 0.06f);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndToggleGroup();
        }

        // =====================================================================
        //  Aplicação
        // =====================================================================

        private void Apply()
        {
            int changed = 0;

            if (doTorch) changed += ApplyTorch();
            if (doRpAsset) changed += ApplyRpAsset();
            if (doSsao) changed += ApplySsao();
            if (doScene) changed += ApplyScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[LightingSetup] {changed} bloco(s) aplicado(s). Salve o projeto (File → Save Project) " +
                      "para gravar tudo em disco.");
        }

        private int ApplyTorch()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(TorchPrefabPath);
            if (root == null)
            {
                Debug.LogError($"[LightingSetup] Prefab não encontrado: {TorchPrefabPath}");
                return 0;
            }

            try
            {
                Light spot = null;
                Light point = null;

                foreach (Light l in root.GetComponentsInChildren<Light>(true))
                {
                    if (l.type == LightType.Spot && spot == null) spot = l;
                    else if (l.type == LightType.Point && point == null) point = l;
                }

                if (torchApplyTuning)
                {
                    if (spot != null)
                    {
                        spot.spotAngle = spotOuterAngle;
                        spot.innerSpotAngle = spotInnerAngle;
                        spot.range = spotRange;
                        spot.intensity = spotIntensity;
                    }

                    if (point != null)
                    {
                        point.range = pointRange;
                        point.intensity = pointIntensity;
                    }
                }

                if (torchFixCorrectness)
                {
                    // A raiz é reposicionada pelo Instantiate do populador; o (0,0,5) que estava
                    // aqui não tem efeito em runtime, só confunde quem abre o prefab.
                    root.transform.localPosition = Vector3.zero;

                    if (spot != null)
                    {
                        spot.shadows = LightShadows.Soft;
                        spot.lightmapBakeType = LightmapBakeType.Realtime;
                        UsePipelineBias(spot);
                    }

                    if (point != null)
                    {
                        // Invariante do desenho: sombra de Point custa 6 slices de atlas contra 1
                        // do Spot. O fill existe justamente por ser barato.
                        point.shadows = LightShadows.None;
                        point.lightmapBakeType = LightmapBakeType.Realtime;
                    }

                    if (root.GetComponentInChildren<TorchLight>(true) == null)
                        root.AddComponent<TorchLight>();
                }

                PrefabUtility.SaveAsPrefabAsset(root, TorchPrefabPath);
                return 1;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Devolve a luz ao bias do pipeline. O 0.97/0.71 que está no prefab era compensação do
        /// ângulo de 170° — com um cone fechado ele sozinho descola a sombra da base do objeto.
        /// </summary>
        private static void UsePipelineBias(Light light)
        {
            var data = light.GetComponent<UniversalAdditionalLightData>();
            if (data == null) data = light.gameObject.AddComponent<UniversalAdditionalLightData>();

            var so = new SerializedObject(data);
            so.FindProperty("m_UsePipelineSettings").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            light.shadowBias = 0.05f;
            light.shadowNormalBias = 0.4f;
        }

        private int ApplyRpAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(RpAssetPath);
            if (asset == null)
            {
                Debug.LogError($"[LightingSetup] URP asset não encontrado: {RpAssetPath}");
                return 0;
            }

            var so = new SerializedObject(asset);
            so.FindProperty("m_ShadowDistance").floatValue = shadowDistance;
            so.FindProperty("m_AdditionalLightsShadowmapResolution").intValue = additionalShadowAtlas;
            so.FindProperty("m_MainLightShadowmapResolution").intValue = mainLightShadowmap;
            so.FindProperty("m_ShadowCascadeCount").intValue = cascadeCount;

            // Ignorado em Forward+ (ForwardLights.SetupPerObjectLightIndices faz early-out), mas
            // deixar 4 no asset faz qualquer um que o leia concluir a coisa errada sobre o teto
            // de luzes deste projeto — que é 256, não 4.
            so.FindProperty("m_AdditionalLightsPerObjectLimit").intValue = 8;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return 1;
        }

        private int ApplySsao()
        {
            Object[] all = AssetDatabase.LoadAllAssetsAtPath(RendererPath);
            foreach (Object obj in all)
            {
                if (obj == null || obj.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;

                var so = new SerializedObject(obj);
                SerializedProperty settings = so.FindProperty("m_Settings");
                if (settings == null) continue;

                settings.FindPropertyRelative("Radius").floatValue = ssaoRadius;
                settings.FindPropertyRelative("Intensity").floatValue = ssaoIntensity;
                settings.FindPropertyRelative("Downsample").boolValue = ssaoDownsample;

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(obj);
                return 1;
            }

            Debug.LogWarning($"[LightingSetup] Feature ScreenSpaceAmbientOcclusion não encontrada em {RendererPath}.");
            return 0;
        }

        private int ApplyScene()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor;

            RenderSettings.fog = enableFog;
            if (enableFog)
            {
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = fogColor;
                RenderSettings.fogDensity = fogDensity;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            return 1;
        }
    }
}
