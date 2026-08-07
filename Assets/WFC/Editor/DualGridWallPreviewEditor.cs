// ============================================================================
//  DualGridWallPreviewEditor.cs  —  camada WFC.Editor
//
//  Botões Gerar / Nova seed / Limpar no inspector do harness de grid dual, no
//  mesmo idioma do WFCFloorPreviewEditor — mais o atalho que preenche os 5
//  slots de peça a partir da pasta do gerador, pra não ter que arrastar prefab
//  por prefab toda vez que regerar o kit.
// ============================================================================

using UnityEditor;
using UnityEngine;
using WFC.Runtime;

namespace WFC.EditorTools
{
    [CustomEditor(typeof(DualGridWallPreview))]
    public sealed class DualGridWallPreviewEditor : Editor
    {
        // Mesmo default do CenterWallTileGenerator. Se você mudar a pasta de saída lá,
        // o botão "Carregar peças" para de achar — é só arrastar na mão nesse caso.
        private const string PieceFolder = "Assets/WFC/CenterWallTiles";

        public override void OnInspectorGUI()
        {
            var preview = (DualGridWallPreview)target;

            DrawDefaultInspector();

            EditorGUILayout.Space();

            if (GUILayout.Button($"Carregar peças de {PieceFolder}"))
                LoadPieces();

            if (!preview.pieces.IsComplete)
            {
                EditorGUILayout.HelpBox(
                    "Faltam peças de parede. Rode WFC ▸ Center Wall Tile Generator e depois clique " +
                    "em \"Carregar peças\" acima.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(preview.floorSpec == null || !preview.pieces.IsComplete))
                {
                    if (GUILayout.Button("Gerar", GUILayout.Height(32)))
                    {
                        preview.Generate();
                        SceneView.RepaintAll();
                    }

                    if (GUILayout.Button("Nova seed + Gerar", GUILayout.Height(32)))
                    {
                        // Mexe na seed do PREVIEW, não na do FloorSpec: o asset é
                        // compartilhado com o andar de produção e não deve sujar por causa
                        // de um teste de kit.
                        Undo.RecordObject(preview, "Nova seed");
                        preview.useSpecSeed = false;
                        preview.seed = Random.Range(int.MinValue, int.MaxValue);
                        EditorUtility.SetDirty(preview);
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

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(preview.LastStatus, MessageType.None);
        }

        private void LoadPieces()
        {
            serializedObject.Update();
            Assign("Wall_End",      "pieces.end");
            Assign("Wall_Straight", "pieces.straight");
            Assign("Wall_Corner",   "pieces.corner");
            Assign("Wall_Tee",      "pieces.tee");
            Assign("Wall_Cross",    "pieces.cross");
            Assign("Arm_Wall",       "pieces.armWall");
            Assign("Arm_Wall_AtHub", "pieces.armWallAtHub");
            Assign("Arm_Door",       "pieces.armDoor");
            Assign("Arm_Door_AtHub", "pieces.armDoorAtHub");
            Assign("Hub_End",       "pieces.hubEnd");
            Assign("Hub_Junction",  "pieces.hubJunction");
            serializedObject.ApplyModifiedProperties();
        }

        private void Assign(string prefabName, string propertyPath)
        {
            SerializedProperty p = serializedObject.FindProperty(propertyPath);
            if (p == null) return;

            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{PieceFolder}/{prefabName}.prefab");
            if (go == null)
                Debug.LogWarning($"[DualGridWallPreview] Não achei {PieceFolder}/{prefabName}.prefab — " +
                                 "rode WFC ▸ Center Wall Tile Generator primeiro.");

            p.objectReferenceValue = go;
        }
    }
}
