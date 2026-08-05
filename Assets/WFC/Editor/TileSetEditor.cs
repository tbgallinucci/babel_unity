// ============================================================================
//  TileSetEditor.cs  —  camada WFC.Editor
//
//  Botão "Rebuild adjacency" + relatório do bake.
//  O bake é edit-time por decisão de arquitetura: em runtime, na transição de
//  andar, o custo tem que ser zero.
// ============================================================================

using UnityEditor;
using UnityEngine;
using WFC.Core;
using WFC.Data;

namespace WFC.EditorTools
{
    [CustomEditor(typeof(TileSet))]
    public sealed class TileSetEditor : Editor
    {
        private Vector2 _reportScroll;
        private bool _showMatrix;

        public override void OnInspectorGUI()
        {
            var tileSet = (TileSet)target;

            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();

            // ---- Estado -----------------------------------------------------
            if (!tileSet.IsBaked)
            {
                EditorGUILayout.HelpBox(
                    "Este TileSet ainda NÃO foi bakeado. O solver não consegue usá-lo.\n" +
                    "Clique em \"Rebuild adjacency\".",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"Bakeado: {tileSet.modules.Count} módulo(s) → {tileSet.VariantCount} variante(s) " +
                    "(módulo × rotação em Y).",
                    MessageType.Info);
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rebuild adjacency", GUILayout.Height(30)))
                {
                    Undo.RecordObject(tileSet, "Rebuild adjacency");
                    bool ok = tileSet.Bake(out string report);
                    EditorUtility.SetDirty(tileSet);
                    AssetDatabase.SaveAssets();

                    if (ok) Debug.Log($"[TileSet:{tileSet.name}] bake OK.\n{report}", tileSet);
                    else Debug.LogError($"[TileSet:{tileSet.name}] bake FALHOU.\n{report}", tileSet);
                }

                if (GUILayout.Button("Selecionar módulos", GUILayout.Height(30), GUILayout.Width(150)))
                {
                    Selection.objects = tileSet.modules.ToArray();
                }
            }

            // ---- Relatório --------------------------------------------------
            if (!string.IsNullOrEmpty(tileSet.BakeReport))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Relatório do bake", EditorStyles.boldLabel);
                _reportScroll = EditorGUILayout.BeginScrollView(_reportScroll, GUILayout.Height(180));
                EditorGUILayout.TextArea(tileSet.BakeReport, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }

            // ---- Matriz de compatibilidade ----------------------------------
            if (!tileSet.IsBaked) return;

            EditorGUILayout.Space();
            _showMatrix = EditorGUILayout.Foldout(_showMatrix, "Matriz de vizinhos (+X)", true);
            if (_showMatrix) DrawMatrix(tileSet);
        }

        /// <summary>
        /// Quem pode ficar à direita (+X) de quem. É a checagem visual mais rápida
        /// quando um socket está errado: uma linha inteira vazia entrega a peça.
        /// </summary>
        private static void DrawMatrix(TileSet tileSet)
        {
            AdjacencyTable table;
            try { table = tileSet.CreateAdjacencyTable(); }
            catch (System.Exception e)
            {
                EditorGUILayout.HelpBox(e.Message, MessageType.Error);
                return;
            }

            int n = tileSet.VariantCount;
            EditorGUI.indentLevel++;
            for (int a = 0; a < n; a++)
            {
                int count = table.AllowedCount(a, Direction.PosX);
                var names = new System.Text.StringBuilder();
                for (int b = 0; b < n; b++)
                {
                    if (!table.IsAllowed(a, Direction.PosX, b)) continue;
                    if (names.Length > 0) names.Append(", ");
                    names.Append(tileSet.GetVariantName(b));
                }

                Color old = GUI.color;
                if (count == 0) GUI.color = new Color(1f, 0.5f, 0.5f);
                EditorGUILayout.LabelField($"{tileSet.GetVariantName(a)} ({count})",
                                           count == 0 ? "— ninguém —" : names.ToString());
                GUI.color = old;
            }
            EditorGUI.indentLevel--;
        }
    }
}
