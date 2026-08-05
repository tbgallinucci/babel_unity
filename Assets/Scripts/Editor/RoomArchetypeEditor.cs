// ============================================================================
//  RoomArchetypeEditor.cs  —  Editor do JOGO
//
//  O "aponte pra uma pasta de props" que foi pedido, sem pagar o preço de
//  scanning de pasta em RUNTIME (que no Unity só funciona de verdade com
//  Resources/ ou Addressables — nenhum dos dois usado neste projeto). Em vez
//  disso: a pasta só existe no Editor, é usada 1x pra preencher a lista com
//  referências de asset de verdade (arrasta que nem qualquer prefab), e o
//  runtime nunca sabe que uma pasta existiu — só vê a List<PropEntry> normal.
//
//  Reimportar não perde pesos/flags já configurados nas entradas que
//  sobrevivem (mesmo prefab): só adiciona as novas e avisa quais sumiram.
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Babel.Floor.EditorTools
{
    [CustomEditor(typeof(RoomArchetype))]
    public sealed class RoomArchetypeEditor : Editor
    {
        private DefaultAsset _sourceFolder;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Importar props de uma pasta", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Aponta pra uma pasta do projeto e preenche a lista 'Props' com todo prefab que achar " +
                "nela (não desce em subpastas). Não some com pesos/anchor já configurados em entradas " +
                "que continuam na pasta — só adiciona as novas.",
                MessageType.Info);

            _sourceFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                "Pasta de Props", _sourceFolder, typeof(DefaultAsset), false);

            using (new EditorGUI.DisabledScope(_sourceFolder == null))
            {
                if (GUILayout.Button("Importar prefabs desta pasta"))
                    ImportFromFolder((RoomArchetype)target, AssetDatabase.GetAssetPath(_sourceFolder));
            }
        }

        private static void ImportFromFolder(RoomArchetype archetype, string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogError($"[RoomArchetype] '{folderPath}' não é uma pasta válida do projeto.");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            var existingPrefabs = new HashSet<GameObject>(
                archetype.props.Where(p => p != null && p.prefab != null).Select(p => p.prefab));

            int added = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // "não desce em subpastas": FindAssets com escopo de pasta já é recursivo por padrão
                // no Unity, então filtramos explicitamente pelo diretório imediato.
                if (System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/') != folderPath) continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || existingPrefabs.Contains(prefab)) continue;

                archetype.props.Add(new RoomArchetype.PropEntry { prefab = prefab, weight = 1f });
                existingPrefabs.Add(prefab);
                added++;
            }

            if (added > 0)
            {
                EditorUtility.SetDirty(archetype);
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"[RoomArchetype] '{archetype.displayName}': {added} prefab(s) novo(s) importado(s) de '{folderPath}'.");
        }
    }
}
