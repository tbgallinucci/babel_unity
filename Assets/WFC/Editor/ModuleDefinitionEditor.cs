// ============================================================================
//  ModuleDefinitionEditor.cs  —  camada WFC.Editor
//
//  Inspector das peças: as 6 faces viram uma tabelinha com dropdown de socket e
//  a cor de debug ao lado, em vez de string solta. Erra-se muito menos assim.
//
//  Para ver os sockets EM 3D, use o componente ModuleSocketGizmo (WFC.Runtime):
//  arraste-o para um objeto vazio na cena e aponte este módulo.
// ============================================================================

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WFC.Core;
using WFC.Data;

namespace WFC.EditorTools
{
    [CustomEditor(typeof(ModuleDefinition))]
    [CanEditMultipleObjects]
    public sealed class ModuleDefinitionEditor : Editor
    {
        private SocketLibrary _library;
        private static readonly string[] RotationLabels = { "0°", "90°", "180°", "270°" };

        private void OnEnable() => _library = FindLibrary();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("prefab"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("weight"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("tags"));

            EditorGUILayout.Space();
            DrawRotations();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Sockets das 6 faces", EditorStyles.boldLabel);

            _library = (SocketLibrary)EditorGUILayout.ObjectField(
                new GUIContent("Socket Library", "Só para o dropdown e as cores; não é salvo no módulo."),
                _library, typeof(SocketLibrary), false);

            if (_library == null)
            {
                EditorGUILayout.HelpBox(
                    "Nenhuma SocketLibrary encontrada — as faces caem para campo de texto livre.",
                    MessageType.Warning);
            }

            DrawFaces();

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("notes"));

            serializedObject.ApplyModifiedProperties();
        }

        // ---------------------------------------------------------- rotações
        private void DrawRotations()
        {
            SerializedProperty rotations = serializedObject.FindProperty("allowedYRotations");
            if (rotations.arraySize != 4) rotations.arraySize = 4;

            EditorGUILayout.LabelField(
                new GUIContent("Rotações em Y permitidas",
                    "Desmarque as que produzem uma peça idêntica — cada rotação vira uma variante " +
                    "extra no solver."));

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUI.indentLevel * 15f);
                for (int r = 0; r < 4; r++)
                {
                    SerializedProperty element = rotations.GetArrayElementAtIndex(r);
                    element.boolValue = GUILayout.Toggle(element.boolValue, RotationLabels[r], EditorStyles.miniButton);
                }
            }
        }

        // ------------------------------------------------------------- faces
        private void DrawFaces()
        {
            SerializedProperty faces = serializedObject.FindProperty("faces");
            if (faces.arraySize != Directions.Count) faces.arraySize = Directions.Count;

            foreach (Direction dir in Directions.All)
            {
                SerializedProperty face = faces.GetArrayElementAtIndex((int)dir);
                SerializedProperty socket = face.FindPropertyRelative("socket");
                SerializedProperty flipped = face.FindPropertyRelative("flipped");
                SerializedProperty vRot = face.FindPropertyRelative("verticalRotation");

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(FaceLabel(dir), GUILayout.Width(140));

                    // Amostra de cor do socket.
                    Rect swatch = GUILayoutUtility.GetRect(16, 16, GUILayout.Width(16), GUILayout.Height(16));
                    Color color = _library != null ? _library.GetDebugColor(dir, socket.stringValue) : Color.gray;
                    EditorGUI.DrawRect(swatch, color);

                    if (_library != null)
                        DrawSocketPopup(dir, socket);
                    else
                        socket.stringValue = EditorGUILayout.TextField(socket.stringValue);

                    // Modificadores só aparecem quando fazem sentido — o resto é ruído.
                    if (Directions.IsVertical(dir))
                    {
                        var vdef = _library != null ? _library.FindVertical(socket.stringValue) : null;
                        if (vdef != null && !vdef.rotationInvariant)
                        {
                            EditorGUILayout.LabelField("rot", GUILayout.Width(25));
                            vRot.intValue = EditorGUILayout.IntPopup(vRot.intValue,
                                RotationLabels, new[] { 0, 1, 2, 3 }, GUILayout.Width(60));
                        }
                    }
                    else
                    {
                        var hdef = _library != null ? _library.FindHorizontal(socket.stringValue) : null;
                        if (_library == null || (hdef != null && !hdef.symmetric))
                        {
                            flipped.boolValue = GUILayout.Toggle(flipped.boolValue, "flip",
                                EditorStyles.miniButton, GUILayout.Width(40));
                        }
                    }
                }
            }
        }

        private void DrawSocketPopup(Direction dir, SerializedProperty socket)
        {
            List<string> options = new List<string> { "<vazio>" };
            if (Directions.IsVertical(dir))
            {
                foreach (var v in _library.Vertical) if (v != null) options.Add(v.name);
            }
            else
            {
                foreach (var h in _library.Horizontal) if (h != null) options.Add(h.name);
            }

            int current = options.IndexOf(socket.stringValue ?? string.Empty);
            if (current < 0)
            {
                // Socket que não existe na biblioteca: mostra assim mesmo, sinalizado.
                options.Add(socket.stringValue + "  (desconhecido!)");
                current = options.Count - 1;
            }

            int picked = EditorGUILayout.Popup(current, options.ToArray());
            if (picked != current)
                socket.stringValue = picked == 0 ? string.Empty : options[picked];
        }

        private static string FaceLabel(Direction dir)
        {
            switch (dir)
            {
                case Direction.PosX: return "+X  (leste)";
                case Direction.NegX: return "−X  (oeste)";
                case Direction.PosY: return "+Y  (topo)";
                case Direction.NegY: return "−Y  (base)";
                case Direction.PosZ: return "+Z  (norte)";
                default:             return "−Z  (sul)";
            }
        }

        private static SocketLibrary FindLibrary()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(SocketLibrary));
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<SocketLibrary>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
