// ============================================================================
//  TileInstancer.cs  —  camada WFC.Runtime
//
//  Instanciação dos prefabs de tile. Versão simples e síncrona: um Instantiate
//  por célula, tudo num frame.
//
//  A Fase 3 substitui isto por PrefabInstancer com object pooling e
//  InstantiateAsync/spread por frames. A separação já está aqui para que essa
//  troca não encoste no WFCFiller.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using WFC.Data;

namespace WFC.Runtime
{
    public static class TileInstancer
    {
        public const string GeneratedRootName = "__GeneratedFloor";

        /// <summary>Remove qualquer andar gerado anteriormente sob este transform.</summary>
        public static void ClearGenerated(Transform parent)
        {
            if (parent == null) return;

            Transform existing = parent.Find(GeneratedRootName);
            while (existing != null)
            {
                // Object.Destroy() em Play Mode só efetiva no FIM do frame — o objeto
                // continua no lugar até lá. Sem desanexar antes, parent.Find() acha o
                // MESMO objeto de novo na próxima iteração e o laço nunca sai: é
                // exatamente o travamento ao trocar de andar (a 2ª geração encontra o
                // andar da 1ª ainda por destruir e trava aqui para sempre).
                // Desanexar tira o objeto de baixo de `parent` na hora, então o Find()
                // seguinte não o encontra mais — Destroy() cuida da limpeza depois.
                existing.SetParent(null);

                if (Application.isPlaying) Object.Destroy(existing.gameObject);
                else Object.DestroyImmediate(existing.gameObject);

                existing = parent.Find(GeneratedRootName);
            }
        }

        /// <summary>
        /// Instancia uma peça por célula e coleta os SpawnAnchors encontrados nos prefabs.
        /// Devolve a raiz criada.
        /// </summary>
        /// <param name="layer">
        /// Layer aplicada em cascata na raiz e em cada peça instanciada (inclusive filhos).
        /// -1 = não mexe, mantém a layer que vier do prefab (comportamento antigo).
        /// </param>
        public static Transform Build(Transform parent, TileSet tileSet, AnnotatedGrid grid,
                                      int[] variants, List<SpawnAnchor> anchorsOut, int layer = -1)
        {
            ClearGenerated(parent);

            var root = new GameObject(GeneratedRootName);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            if (layer >= 0) root.layer = layer;

            var buffer = new List<SpawnAnchor>();

            for (int cell = 0; cell < variants.Length; cell++)
            {
                int variant = variants[cell];
                if (variant < 0) continue;

                GameObject prefab = tileSet.GetPrefab(variant);
                if (prefab == null) continue; // Wildcard_Air não tem geometria — nada a fazer

                GameObject instance = Instantiate(prefab, root.transform);
                instance.transform.localPosition = grid.CellToWorld(cell) - parent.position;
                instance.transform.localRotation = tileSet.GetRotationQuaternion(variant);
                instance.name = $"{tileSet.GetVariantName(variant)} [{cell}]";

                // Todo o casco gerado precisa sair na MESMA layer (física/renderização
                // consistentes andar afora), independente da layer que o prefab de
                // origem tiver no asset — daí forçar em cascata em vez de confiar
                // no valor herdado.
                if (layer >= 0) SetLayerRecursively(instance, layer);

                if (anchorsOut == null) continue;

                // O plugin apenas COLETA os anchors; quem decide o que nasce neles é o
                // populador, que é externo (Decisão 4).
                buffer.Clear();
                instance.GetComponentsInChildren(true, buffer);
                foreach (SpawnAnchor anchor in buffer)
                {
                    anchor.cellIndex = cell;
                    anchorsOut.Add(anchor);
                }
            }

            return root.transform;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            Transform t = go.transform;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursively(t.GetChild(i).gameObject, layer);
        }

        private static GameObject Instantiate(GameObject prefab, Transform parent)
        {
#if UNITY_EDITOR
            // Fora do play mode, instanciar como prefab instance deixa o andar
            // inspecionável e ligado ao asset original — ajuda a depurar o kit.
            if (!Application.isPlaying)
                return (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, parent);
#endif
            return Object.Instantiate(prefab, parent);
        }
    }
}
