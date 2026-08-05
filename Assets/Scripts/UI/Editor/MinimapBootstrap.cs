// ============================================================================
//  MinimapBootstrap.cs
//  Monta a hierarquia de UI do minimapa na cena aberta e liga as referências
//  do MinimapController — a parte chata de arrastar Canvas/RectTransform/Image
//  na mão, automatizada (mesmo espírito do GreyboxTileGenerator).
//
//  COMO USAR:
//   1. Abra a cena (ex.: Level_Test).
//   2. Menu:  Babel ▸ Setup Minimap in Scene.
//   3. Pronto: aparece um "Minimap" no canto superior direito, já referenciando
//      o WFCFloorGenerator e o Player que estiverem na cena.
//
//  Idempotente: rodar de novo destrói o "Minimap" antigo (se achar um dentro
//  de um Canvas) e recria do zero — não duplica.
//
//  O que ele NÃO faz: não cria Canvas/EventSystem se já existirem um na cena
//  (reaproveita o que tiver). Não gera nenhum asset em disco — tudo fica só
//  na cena, como qualquer outro GameObject de UI.
// ============================================================================
#if UNITY_EDITOR
using Babel.Player;
using Babel.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WFC.Runtime;

namespace Babel.UI.Editor
{
    public static class MinimapBootstrap
    {
        private const float MinimapSize = 220f;
        private const float Padding = 20f;

        [MenuItem("Babel/Setup Minimap in Scene")]
        private static void Setup()
        {
            Canvas canvas = EnsureCanvas();
            EnsureEventSystem();

            GameObject minimapGO = BuildMinimap(canvas.transform);

            var controller = minimapGO.GetComponent<MinimapController>();
            WireReferences(controller);

            Selection.activeGameObject = minimapGO;
            EditorSceneManager.MarkSceneDirty(minimapGO.scene);

            Debug.Log("[MinimapBootstrap] Minimap montado. Confira as referências " +
                      "'Generator' e 'Player' no MinimapController caso a auto-detecção " +
                      "não tenha achado os objetos certos.");
        }

        // =====================================================================
        //  Canvas / EventSystem
        // =====================================================================

        private static Canvas EnsureCanvas()
        {
            var existing = Object.FindFirstObjectByType<Canvas>();
            if (existing != null) return existing;

            var canvasGO = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            Undo.RegisterCreatedObjectUndo(canvasGO, "Create HUD Canvas");
            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;

            var esGO = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(esGO, "Create EventSystem");
        }

        // =====================================================================
        //  Hierarquia do minimapa
        // =====================================================================

        private static GameObject BuildMinimap(Transform canvasTransform)
        {
            // Idempotente: se já existe um "Minimap" debaixo deste Canvas, remove
            // antes de recriar (regenerar = sobrescrever, igual ao Greybox).
            Transform old = canvasTransform.Find("Minimap");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var root = new GameObject("Minimap", typeof(RectTransform));
            var rootRT = (RectTransform)root.transform;
            rootRT.SetParent(canvasTransform, false);
            rootRT.anchorMin = rootRT.anchorMax = new Vector2(1f, 1f);
            rootRT.pivot = new Vector2(1f, 1f);
            rootRT.sizeDelta = new Vector2(MinimapSize, MinimapSize);
            rootRT.anchoredPosition = new Vector2(-Padding, -Padding);

            CreateImage("Background", rootRT, stretch: true, new Color(0f, 0f, 0f, 0.45f), raycastTarget: false);

            // RectMask2D: recorte retangular puro, sem precisar de Image/stencil —
            // é exatamente o que o viewport do minimapa precisa.
            var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            var viewportRT = (RectTransform)viewportGO.transform;
            viewportRT.SetParent(rootRT, false);
            StretchFill(viewportRT, 6f);

            var mapRootGO = new GameObject("MapRoot", typeof(RectTransform));
            var mapRootRT = (RectTransform)mapRootGO.transform;
            mapRootRT.SetParent(viewportRT, false);
            mapRootRT.anchorMin = mapRootRT.anchorMax = new Vector2(0.5f, 0.5f);
            mapRootRT.pivot = new Vector2(0f, 0f);
            mapRootRT.anchoredPosition = Vector2.zero;

            GameObject playerIconGO = BuildPlayerIcon(viewportRT);

            var controller = root.AddComponent<MinimapController>();
            var so = new SerializedObject(controller);
            so.FindProperty("mapRoot").objectReferenceValue = mapRootRT;
            so.FindProperty("playerIcon").objectReferenceValue = playerIconGO.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(root, "Create Minimap");
            return root;
        }

        private static GameObject BuildPlayerIcon(RectTransform parent)
        {
            var iconGO = new GameObject("PlayerIcon", typeof(RectTransform));
            var iconRT = (RectTransform)iconGO.transform;
            iconRT.SetParent(parent, false);
            iconRT.anchorMin = iconRT.anchorMax = new Vector2(0.5f, 0.5f);
            iconRT.pivot = new Vector2(0.5f, 0.5f);
            iconRT.anchoredPosition = Vector2.zero;
            iconRT.sizeDelta = Vector2.zero;

            // Ponto central: a posição do jogador.
            var dot = CreateImage("Dot", iconRT, stretch: false, Color.white, raycastTarget: false);
            var dotRT = (RectTransform)dot.transform;
            dotRT.anchorMin = dotRT.anchorMax = new Vector2(0.5f, 0.5f);
            dotRT.pivot = new Vector2(0.5f, 0.5f);
            dotRT.sizeDelta = new Vector2(8f, 8f);

            // Barrinha "focinho": gira junto com PlayerIcon, indica pra onde o jogador olha.
            var nose = CreateImage("Facing", iconRT, stretch: false, Color.white, raycastTarget: false);
            var noseRT = (RectTransform)nose.transform;
            noseRT.anchorMin = noseRT.anchorMax = new Vector2(0.5f, 0.5f);
            noseRT.pivot = new Vector2(0.5f, 0f);
            noseRT.anchoredPosition = new Vector2(0f, 4f);
            noseRT.sizeDelta = new Vector2(4f, 10f);

            return iconGO;
        }

        // =====================================================================
        //  Referências automáticas
        // =====================================================================

        private static void WireReferences(MinimapController controller)
        {
            var generator = Object.FindFirstObjectByType<WFCFloorGenerator>();
            var playerController = Object.FindFirstObjectByType<PlayerController>();

            var so = new SerializedObject(controller);
            if (generator != null)
                so.FindProperty("generator").objectReferenceValue = generator;
            if (playerController != null)
                so.FindProperty("player").objectReferenceValue = playerController.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (generator == null)
                Debug.LogWarning("[MinimapBootstrap] Nenhum WFCFloorGenerator achado na cena — arraste na mão.");
            if (playerController == null)
                Debug.LogWarning("[MinimapBootstrap] Nenhum PlayerController achado na cena — arraste o Player na mão.");
        }

        // =====================================================================
        //  Helpers de UI
        // =====================================================================

        private static GameObject CreateImage(string name, Transform parent, bool stretch, Color color, bool raycastTarget)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            if (stretch) StretchFill(rt, 0f);

            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = raycastTarget;
            return go;
        }

        private static void StretchFill(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
#endif
