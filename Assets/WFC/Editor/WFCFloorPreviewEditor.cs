// ============================================================================
//  WFCFloorPreviewEditor.cs  —  camada WFC.Editor
//
//  Botões Gerar / Limpar / Nova seed no inspector do harness de demonstração,
//  e os rótulos de variante por célula na Scene View.
// ============================================================================

using UnityEditor;
using UnityEngine;
using WFC.Data;
using WFC.Runtime;

namespace WFC.EditorTools
{
    [CustomEditor(typeof(WFCFloorPreview))]
    public sealed class WFCFloorPreviewEditor : Editor
    {
        private static readonly GUIStyle LabelStyle = new GUIStyle();

        public override void OnInspectorGUI()
        {
            var preview = (WFCFloorPreview)target;

            DrawDefaultInspector();

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Gerar", GUILayout.Height(32)))
                {
                    preview.Generate();
                    SceneView.RepaintAll();
                }

                using (new EditorGUI.DisabledScope(preview.floorSpec == null))
                {
                    if (GUILayout.Button("Nova seed + Gerar", GUILayout.Height(32)))
                    {
                        Undo.RecordObject(preview.floorSpec, "Nova seed");
                        preview.floorSpec.randomSeed = false;
                        preview.floorSpec.seed = Random.Range(int.MinValue, int.MaxValue);
                        EditorUtility.SetDirty(preview.floorSpec);
                        preview.Generate();
                        SceneView.RepaintAll();
                    }
                }

                if (GUILayout.Button("Limpar", GUILayout.Height(32), GUILayout.Width(90)))
                {
                    preview.ClearGenerated();
                    SceneView.RepaintAll();
                }
            }

            if (preview.floorSpec != null && preview.floorSpec.tileSet != null && !preview.floorSpec.tileSet.IsBaked)
            {
                EditorGUILayout.HelpBox(
                    "O TileSet do FloorSpec não está bakeado. Abra o asset e clique em \"Rebuild adjacency\".",
                    MessageType.Warning);
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(preview.LastStatus, MessageType.None);
        }

        private void OnSceneGUI()
        {
            var preview = (WFCFloorPreview)target;
            if (!preview.drawVariantLabels) return;

            AnnotatedGrid grid = preview.LastGrid;
            int[] variants = preview.LastVariants;
            TileSet tileSet = preview.floorSpec != null ? preview.floorSpec.tileSet : null;

            if (grid == null || variants == null || tileSet == null || !tileSet.IsBaked) return;

            // Rótulos ficam ilegíveis (e caros) num grid grande — corta cedo.
            if (variants.Length > 1024)
            {
                Handles.BeginGUI();
                GUILayout.BeginArea(new Rect(10, 10, 320, 40));
                GUILayout.Label("Rótulos desligados: grid com mais de 1024 células.");
                GUILayout.EndArea();
                Handles.EndGUI();
                return;
            }

            LabelStyle.normal.textColor = Color.white;
            LabelStyle.fontSize = 9;
            LabelStyle.alignment = TextAnchor.MiddleCenter;

            for (int cell = 0; cell < variants.Length; cell++)
            {
                int variant = variants[cell];
                if (variant < 0) continue;
                Handles.Label(grid.CellToWorld(cell) + Vector3.up * 0.3f,
                              tileSet.GetVariantName(variant), LabelStyle);
            }
        }
    }
}
