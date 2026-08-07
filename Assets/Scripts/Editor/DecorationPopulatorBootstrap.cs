// ============================================================================
//  DecorationPopulatorBootstrap.cs  —  Editor do JOGO
//
//  Lê um manifesto CSV (prefab_path, anchor_kind, set_display_name, weight,
//  spawn_chance) e escreve direto na lista privada 'decorations' do
//  DecorationPopulator que vive no MESMO GameObject que o FloorDirector,
//  numa cena do projeto (ver FloorDirector.Awake: GetComponent<DecorationPopulator>()).
//
//  Idempotente: casa por (set_display_name) pra achar/criar o DecorationSet, e
//  por prefab dentro do set pra achar/criar o WeightedPrefab — roda de novo,
//  atualiza peso/spawnChance no lugar em vez de duplicar.
//
//  Se o DecorationPopulator ainda não existir no GameObject do FloorDirector,
//  o botão "Adicionar componente" abaixo anexa (AddComponent) antes de tentar
//  popular — sem isso o FloorDirector.Awake carrega pillarPopulator = null.
//
//  Menu:  Babel ▸ Decoration Populator Bootstrap
//
//  IMPORTANTE (mesma regra do Animator): a cena só grava no disco depois de
//  File ▸ Save (ou Ctrl+S) / File ▸ Save Project — este script marca a cena
//  como dirty e chama EditorSceneManager.SaveOpenScenes(), mas se você tiver
//  a cena aberta com outras mudanças não salvas, dá uma olhada antes de rodar.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WFC.Runtime;

namespace Babel.Floor.EditorTools
{
    public sealed class DecorationPopulatorBootstrap : EditorWindow
    {
        private string manifestPath = "Assets/WFC/Data/DecorationPopulatorManifest.csv";
        private string scenePath = "Assets/Scenes/Loop Scene/Loop_Scene.unity";
        private Vector2 scroll;
        private string lastReport;

        [MenuItem("Babel/Decoration Populator Bootstrap")]
        private static void Open() => GetWindow<DecorationPopulatorBootstrap>("Decoration Populator Bootstrap");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Manifesto → DecorationPopulator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Lê o CSV e escreve nos 'decorations' do DecorationPopulator que vive no GameObject " +
                "do FloorDirector, na cena apontada abaixo. Idempotente: casa por nome de set e por " +
                "prefab, atualiza peso/spawnChance em vez de duplicar.\n\n" +
                "Colunas do CSV: prefab_path, anchor_kind, set_display_name, weight, spawn_chance",
                MessageType.Info);

            EditorGUILayout.Space();
            manifestPath = EditorGUILayout.TextField("Manifesto (CSV)", manifestPath);
            scenePath = EditorGUILayout.TextField("Cena", scenePath);

            EditorGUILayout.Space();
            if (GUILayout.Button("Rodar bootstrap", GUILayout.Height(32)))
                Run();

            if (!string.IsNullOrEmpty(lastReport))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Relatório", EditorStyles.boldLabel);
                scroll = EditorGUILayout.BeginScrollView(scroll);
                EditorGUILayout.TextArea(lastReport, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
        }

        // =====================================================================
        private sealed class Row
        {
            public string PrefabPath;
            public SpawnAnchorKind AnchorKind;
            public string SetName;
            public float Weight;
            public float SpawnChance;
        }

        private void Run()
        {
            var report = new System.Text.StringBuilder();

            if (!File.Exists(manifestPath))
            {
                report.AppendLine($"Manifesto não encontrado: {manifestPath}");
                lastReport = report.ToString();
                return;
            }

            List<Row> rows = ParseManifest(manifestPath, report);
            if (rows.Count == 0)
            {
                report.AppendLine("Nenhuma linha válida no manifesto — nada a fazer.");
                lastReport = report.ToString();
                return;
            }

            // Garante que a cena certa está aberta sem descartar mudanças não salvas de outra cena.
            Scene active = EditorSceneManager.GetActiveScene();
            if (active.path != scenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    report.AppendLine("Cancelado: havia mudanças não salvas na cena atual.");
                    lastReport = report.ToString();
                    return;
                }
                active = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            FloorDirector director = FindFloorDirector(active);
            if (director == null)
            {
                report.AppendLine($"Nenhum FloorDirector encontrado na cena '{scenePath}'.");
                lastReport = report.ToString();
                return;
            }

            GameObject host = director.gameObject;
            var populator = host.GetComponent<DecorationPopulator>();
            if (populator == null)
            {
                populator = Undo.AddComponent<DecorationPopulator>(host);
                report.AppendLine($"DecorationPopulator não existia em '{host.name}' — componente adicionado.");
            }

            ApplyRows(populator, rows, report);

            EditorUtility.SetDirty(populator);
            EditorSceneManager.MarkSceneDirty(active);
            EditorSceneManager.SaveScene(active);

            lastReport = report.ToString();
            Debug.Log($"[DecorationPopulatorBootstrap]\n{lastReport}");
        }

        private static FloorDirector FindFloorDirector(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<FloorDirector>(true);
                if (found != null) return found;
            }
            return null;
        }

        // ------------------------------------------------------------ CSV
        private static List<Row> ParseManifest(string path, System.Text.StringBuilder report)
        {
            var rows = new List<Row>();
            string[] lines = File.ReadAllLines(path);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                if (i == 0 && line.StartsWith("prefab_path", StringComparison.OrdinalIgnoreCase)) continue; // header

                string[] cols = line.Split(',');
                if (cols.Length < 5)
                {
                    report.AppendLine($"Linha {i + 1}: colunas insuficientes, ignorada — '{line}'");
                    continue;
                }

                string prefabPath = cols[0].Trim();
                if (!Enum.TryParse(cols[1].Trim(), true, out SpawnAnchorKind kind))
                {
                    report.AppendLine($"Linha {i + 1}: anchor_kind '{cols[1]}' inválido, ignorada.");
                    continue;
                }

                if (!float.TryParse(cols[3].Trim(), out float weight)) weight = 1f;
                if (!float.TryParse(cols[4].Trim(), out float spawnChance)) spawnChance = 1f;

                rows.Add(new Row
                {
                    PrefabPath = prefabPath,
                    AnchorKind = kind,
                    SetName = cols[2].Trim(),
                    Weight = weight,
                    SpawnChance = Mathf.Clamp01(spawnChance),
                });
            }

            return rows;
        }

        // ------------------------------------------------------- aplicação
        private static void ApplyRows(DecorationPopulator populator, List<Row> rows, System.Text.StringBuilder report)
        {
            var so = new SerializedObject(populator);
            SerializedProperty decorations = so.FindProperty("decorations");

            int setsUpdated = 0, setsCreated = 0, prefabsUpserted = 0, missing = 0;

            foreach (var group in rows.GroupBy(r => r.SetName))
            {
                string setName = group.Key;
                SpawnAnchorKind kind = group.First().AnchorKind;
                float spawnChance = group.First().SpawnChance;

                if (group.Select(r => r.AnchorKind).Distinct().Count() > 1)
                    report.AppendLine($"Aviso: set '{setName}' tem anchor_kind divergente entre linhas — usando '{kind}'.");
                if (group.Select(r => r.SpawnChance).Distinct().Count() > 1)
                    report.AppendLine($"Aviso: set '{setName}' tem spawn_chance divergente entre linhas — usando {spawnChance}.");

                int setIndex = FindSetIndex(decorations, setName);
                if (setIndex < 0)
                {
                    setIndex = decorations.arraySize;
                    decorations.InsertArrayElementAtIndex(setIndex);
                    SerializedProperty newSet = decorations.GetArrayElementAtIndex(setIndex);
                    newSet.FindPropertyRelative("displayName").stringValue = setName;
                    newSet.FindPropertyRelative("variants").ClearArray();
                    setsCreated++;
                }
                else
                {
                    setsUpdated++;
                }

                SerializedProperty set = decorations.GetArrayElementAtIndex(setIndex);
                SetEnumByName(set.FindPropertyRelative("anchorKind"), kind.ToString());
                set.FindPropertyRelative("spawnChance").floatValue = spawnChance;

                SerializedProperty variants = set.FindPropertyRelative("variants");
                foreach (Row row in group)
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(row.PrefabPath);
                    if (prefab == null)
                    {
                        report.AppendLine($"Prefab não encontrado: {row.PrefabPath} — linha ignorada.");
                        missing++;
                        continue;
                    }

                    int variantIndex = FindVariantIndex(variants, prefab);
                    if (variantIndex < 0)
                    {
                        variantIndex = variants.arraySize;
                        variants.InsertArrayElementAtIndex(variantIndex);
                    }

                    SerializedProperty variant = variants.GetArrayElementAtIndex(variantIndex);
                    variant.FindPropertyRelative("prefab").objectReferenceValue = prefab;
                    variant.FindPropertyRelative("weight").floatValue = row.Weight;
                    prefabsUpserted++;
                }
            }

            so.ApplyModifiedProperties();

            report.AppendLine($"Sets criados: {setsCreated}, sets atualizados: {setsUpdated}, " +
                               $"variantes upsertadas: {prefabsUpserted}, prefabs não encontrados: {missing}.");
        }

        private static int FindSetIndex(SerializedProperty decorations, string displayName)
        {
            for (int i = 0; i < decorations.arraySize; i++)
            {
                SerializedProperty set = decorations.GetArrayElementAtIndex(i);
                if (set.FindPropertyRelative("displayName").stringValue == displayName) return i;
            }
            return -1;
        }

        private static int FindVariantIndex(SerializedProperty variants, GameObject prefab)
        {
            for (int i = 0; i < variants.arraySize; i++)
            {
                SerializedProperty variant = variants.GetArrayElementAtIndex(i);
                if (variant.FindPropertyRelative("prefab").objectReferenceValue == prefab) return i;
            }
            return -1;
        }

        private static void SetEnumByName(SerializedProperty enumProperty, string name)
        {
            int index = Array.IndexOf(enumProperty.enumNames, name);
            if (index >= 0) enumProperty.enumValueIndex = index;
        }
    }
}
