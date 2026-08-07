// ============================================================================
//  CenterWallMaterialOverride.cs  —  camada WFC.Editor
//
//  Sobrescreve EM MASSA os materiais do kit de grid dual — piso e parede (e
//  opcionalmente o de detalhe) — com um material de origem à sua escolha.
//
//  Menu:  WFC ▸ Apply Kit Materials
//
//  ------------------------------------------------------------------------
//  POR QUE COPIAR O MATERIAL INTEIRO, NÃO SÓ A TEXTURA
//
//  Um material "de verdade" (o que você já tem pronto, com reflexo etc.) não é
//  só uma imagem — é textura de cor + normal map + mapa de rugosidade/metálico
//  + os valores certos de Smoothness/Specular já calibrados pro shader. Editar
//  só o slot Base Map do material greybox perde tudo isso. Este utilitário usa
//  EditorUtility.CopySerialized, que copia TODOS os campos serializados de um
//  Material pro outro — shader incluso — então o resultado é uma cópia exata
//  do material de origem, só que morando dentro do arquivo M_CenterFloor.mat /
//  M_CenterWall.mat que já existe.
//
//  POR QUE ISSO CONTA COMO "EM MASSA": cada peça do kit (braço, hub, canto,
//  degrau da escada, piso de toda célula) referencia um dos 3 materiais
//  COMPARTILHADOS (matWall/matFloor/matAccent) que o CenterWallTileGenerator
//  cria — nunca um material próprio por peça. Sobrescrever o conteúdo desses 3
//  arquivos atualiza literalmente toda peça do kit de uma vez, sem tocar em
//  nenhum prefab.
//
//  O GUID do arquivo (a identidade que prefab/ModuleDefinition referenciam)
//  NÃO muda — só o conteúdo por dentro. Por isso funciona sem reatribuir nada.
// ============================================================================

using UnityEditor;
using UnityEngine;

namespace WFC.EditorTools
{
    public sealed class CenterWallMaterialOverride : EditorWindow
    {
        private string materialsFolder = "Assets/WFC/CenterWallTiles/Materials";

        private Material floorSource;
        private Material wallSource;
        private Material accentSource;

        [MenuItem("WFC/Apply Kit Materials")]
        private static void Open() => GetWindow<CenterWallMaterialOverride>("Kit Materials");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Sobrescrever materiais do kit (em massa)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Arraste um material PRONTO (com textura, normal map, reflexo — o que você já " +
                "tem) em cada slot. Ao clicar Aplicar, o conteúdo INTEIRO dele é copiado por cima " +
                "do material compartilhado do kit — toda peça de piso/parede atualiza de uma vez, " +
                "sem regenerar geometria e sem reatribuir nada em prefab nenhum.",
                MessageType.Info);

            EditorGUILayout.Space();
            materialsFolder = EditorGUILayout.TextField("Pasta de materiais do kit", materialsFolder);

            EditorGUILayout.Space();
            floorSource = (Material)EditorGUILayout.ObjectField(
                new GUIContent("Piso ← de", "Material de origem pro piso. Sobrescreve M_CenterFloor.mat."),
                floorSource, typeof(Material), false);

            wallSource = (Material)EditorGUILayout.ObjectField(
                new GUIContent("Parede ← de", "Material de origem pra parede. Sobrescreve M_CenterWall.mat."),
                wallSource, typeof(Material), false);

            accentSource = (Material)EditorGUILayout.ObjectField(
                new GUIContent("Detalhe ← de (opcional)", "Verga/degrau/etc. Sobrescreve M_CenterAccent.mat. " +
                                "Vazio = não mexe no detalhe."),
                accentSource, typeof(Material), false);

            EditorGUILayout.Space();
            bool canApply = floorSource != null || wallSource != null || accentSource != null;
            using (new EditorGUI.DisabledScope(!canApply))
            {
                if (GUILayout.Button("Aplicar", GUILayout.Height(32)))
                    Apply();
            }
        }

        private void Apply()
        {
            int n = 0;
            n += Overwrite("M_CenterFloor", floorSource);
            n += Overwrite("M_CenterWall", wallSource);
            n += Overwrite("M_CenterAccent", accentSource);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CenterWallMaterialOverride] {n} material(is) sobrescrito(s) em {materialsFolder}.");
        }

        private int Overwrite(string targetName, Material source)
        {
            if (source == null) return 0;

            string path = $"{materialsFolder}/{targetName}.mat";
            var target = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (target == null)
            {
                Debug.LogWarning($"[CenterWallMaterialOverride] Não achei {path} — rode o " +
                                 "WFC ▸ Center Wall Tile Generator primeiro pra criar os materiais do kit.");
                return 0;
            }

            // CopySerialized copia TUDO (shader, texturas, cores, keywords) — inclusive o nome
            // interno do material, que devolvemos pro original logo em seguida pra não confundir
            // o Project window (o arquivo continua se chamando M_CenterFloor.mat, só o conteúdo
            // por dentro é que virou uma cópia da origem).
            string keepName = target.name;
            EditorUtility.CopySerialized(source, target);
            target.name = keepName;

            EditorUtility.SetDirty(target);
            return 1;
        }
    }
}
