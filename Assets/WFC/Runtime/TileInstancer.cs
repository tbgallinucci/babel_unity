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

        // Nome do filho de piso dentro de cada prefab de peça — mesma convenção que
        // GreyboxTileGenerator.Floor() usa ("Floor" sem sufixo de célula; o sufixo
        // "[cell]" só entra no nome da RAIZ instanciada, não no filho). Arte de produção
        // que substituir o greybox precisa manter esse nome pro CounterRotateFloor
        // continuar achando o piso — é o mesmo tipo de convenção de pivô/nome que o
        // prefab de tocha já respeita (ver Incluindo e Editando Prefabs de Luz de Parede.md).
        private const string FloorChildName = "Floor";

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
        /// <param name="piecesOut">
        /// Opcional. Se vier um array de tamanho <c>variants.Length</c>, cada posição recebe
        /// a peça instanciada naquela célula (null onde não houve geometria). É o que permite
        /// ao jogo agrupar/ligar/desligar geometria por sala depois — sem isso o consumidor
        /// teria que redescobrir a célula de cada peça pelo nome ou pela posição.
        /// </param>
        public static Transform Build(Transform parent, TileSet tileSet, AnnotatedGrid grid,
                                      int[] variants, List<SpawnAnchor> anchorsOut, int layer = -1,
                                      Transform[] piecesOut = null)
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
                Quaternion rotation = tileSet.GetRotationQuaternion(variant);
                instance.transform.localRotation = rotation;
                instance.name = $"{tileSet.GetVariantName(variant)} [{cell}]";

                CounterRotateFloor(instance.transform, rotation);

                // Todo o casco gerado precisa sair na MESMA layer (física/renderização
                // consistentes andar afora), independente da layer que o prefab de
                // origem tiver no asset — daí forçar em cascata em vez de confiar
                // no valor herdado.
                if (layer >= 0) SetLayerRecursively(instance, layer);

                if (piecesOut != null && cell < piecesOut.Length) piecesOut[cell] = instance.transform;

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

        /// <summary>
        /// Cancela a rotação da PEÇA só no filho de piso, mantendo a orientação dele fixa no
        /// mundo (identidade) não importa como o WFC girou a peça pra encaixar as paredes.
        ///
        /// Por que isto é seguro: o contorno do piso é um quadrado centrado na célula — girar
        /// 90/180/270 não muda footprint nem cria buraco/sobreposição com a peça vizinha,
        /// então contra-rotacionar só ele não quebra encaixe nenhum.
        ///
        /// Por que isto existe: sem isto, a MESMA textura de piso aparece virada de um jeito
        /// diferente em cada rotação de peça — e se a textura tiver qualquer sombra/luz assada
        /// nela (comum em material extraído de .blend/FBX), cada orientação lê como um bloco
        /// de brilho diferente do vizinho, um mosaico visível que não tem nada a ver com luz
        /// em tempo real (foi confirmado: zerar o FloorLightField não muda nada nele).
        /// </summary>
        private static void CounterRotateFloor(Transform piece, Quaternion pieceRotation)
        {
            Transform floor = piece.Find(FloorChildName);
            if (floor == null) return; // peça sem filho "Floor" (ex.: Wildcard_Air) — nada a fazer

            floor.localRotation = Quaternion.Inverse(pieceRotation);
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
