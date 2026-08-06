// ============================================================================
//  ArtPreviewPopulatorEditor.cs  —  ferramenta de EDITOR (não vai para a build)
//
//  Um botão só: gera o casco (WFCFloorPreview) e popula luz/prop em seguida.
//  Existe pra não precisar clicar em dois componentes diferentes toda vez que
//  você quer olhar uma seed nova.
// ============================================================================

using UnityEditor;
using UnityEngine;
using Babel.Floor;
using WFC.Runtime;

namespace Babel.EditorTools
{
    [CustomEditor(typeof(ArtPreviewPopulator))]
    public sealed class ArtPreviewPopulatorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var populator = (ArtPreviewPopulator)target;

            DrawDefaultInspector();

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Gerar com arte completa", GUILayout.Height(36)))
                {
                    RunFullGeneration(populator, replaceSeed: false);
                }

                if (GUILayout.Button("Nova seed + arte completa", GUILayout.Height(36)))
                {
                    RunFullGeneration(populator, replaceSeed: true);
                }
            }

            EditorGUILayout.Space(4f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Só popular luz/prop", GUILayout.Height(24)))
                    populator.PopulateArt();

                if (GUILayout.Button("Limpar luz/prop", GUILayout.Height(24)))
                    populator.ClearArt();
            }

            EditorGUILayout.HelpBox(
                "\"Gerar com arte completa\" chama o Gerar do WFCFloorPreview (campo abaixo) e, se " +
                "der certo, popula tocha e prop em seguida — sem passar por Play Mode, sem " +
                "DynamicLightBudget/RoomStreamer (a ideia é mostrar TUDO como foi autorado).\n\n" +
                "Depois de gerar: File → Save (Ctrl+S) grava a cena. Para compartilhar isolado do " +
                "resto da cena, selecione a raiz gerada na Hierarchy e arraste pro Project — vira " +
                "um prefab que seu colega abre em qualquer projeto sem precisar regenerar nada.",
                MessageType.Info);
        }

        private static void RunFullGeneration(ArtPreviewPopulator populator, bool replaceSeed)
        {
            var preview = populator.GetComponentInParent<WFCFloorPreview>(true)
                       ?? Object.FindFirstObjectByType<WFCFloorPreview>();

            if (preview == null)
            {
                Debug.LogError("[ArtPreviewPopulator] Nenhum WFCFloorPreview encontrado na cena. " +
                               "Coloque este componente no MESMO GameObject do WFCFloorPreview.");
                return;
            }

            if (replaceSeed && preview.floorSpec != null)
            {
                Undo.RecordObject(preview.floorSpec, "Nova seed");
                preview.floorSpec.randomSeed = false;
                preview.floorSpec.seed = Random.Range(int.MinValue, int.MaxValue);
                EditorUtility.SetDirty(preview.floorSpec);
            }

            preview.Generate();
            SceneView.RepaintAll();

            if (!preview.LastSuccess)
            {
                Debug.LogWarning($"[ArtPreviewPopulator] Geração do casco falhou — pulando luz/prop. " +
                                 $"{preview.LastStatus}");
                return;
            }

            populator.PopulateArt();
        }
    }
}
