// ============================================================================
//  CeilingBuilder.cs  —  camada WFC.Runtime
//
//  A "tampa" única (plane) que fecha o topo do andar, extraída do
//  WFCFloorGenerator (produção) pra também poder ser chamada pelo
//  WFCFloorPreview (harness de editor) — sem isto os dois divergiam: produção
//  saía com teto, o harness de preview de arte saía sem.
//
//  Por que UM plano cobrindo o footprint inteiro, e não teto peça por peça:
//  de dentro de uma sala fechada por parede, ninguém vê acima do topo dela —
//  o resultado visual de "teto por célula, encaixado no contorno exato do
//  andar" e "um retângulo só, um pouco maior que o footprint" é idêntico pra
//  quem está dentro. E um retângulo é 1 draw call pro andar inteiro contra
//  dezenas (uma peça por célula), então nem cabe debate de custo/benefício.
//  Só troque por peça de verdade se um dia precisar de variação visual real
//  (viga aparente, claraboia) — até lá isto fecha a caixa de graça.
//
//  Não participa do WFC: roda depois que o andar já foi resolvido e
//  instanciado, então nenhum socket/adjacência precisa saber que teto existe.
// ============================================================================

using UnityEngine;

namespace WFC.Runtime
{
    public static class CeilingBuilder
    {
        /// <summary>
        /// Cria o plano de teto e o retorna já parentado. Devolve null se `parent` for nulo
        /// (nada pra prender nele).
        /// </summary>
        /// <param name="parent">Raiz do andar gerado — o teto é filho dela, então some junto
        /// quando o andar é limpo/regenerado.</param>
        /// <param name="origin">Canto do grid em coordenadas de mundo — mesmo valor passado ao
        /// AnnotatedGrid (célula 0,0,0 fica em origin + meia célula).</param>
        /// <param name="gridSize">Dimensões em células (X/Y/Z) do FloorSpec.</param>
        /// <param name="cellSize">Footprint da célula, em metros.</param>
        /// <param name="cellHeight">Altura de um andar de células, em metros — define a altura Y do teto.</param>
        /// <param name="overhang">Margem além do footprint, pra sobrar um pouco além das paredes.</param>
        /// <param name="material">Vazio = material padrão do Plane primitivo (cinza).</param>
        /// <param name="layer">-1 = não mexe na layer.</param>
        public static GameObject Build(Transform parent, Vector3 origin, Vector3Int gridSize,
                                       float cellSize, float cellHeight, float overhang,
                                       Material material, int layer = -1)
        {
            if (parent == null) return null;

            float width = gridSize.x * cellSize + 2f * overhang;
            float depth = gridSize.z * cellSize + 2f * overhang;
            float height = gridSize.y * cellHeight;

            Vector3 center = origin + new Vector3(
                gridSize.x * cellSize * 0.5f,
                height,
                gridSize.z * cellSize * 0.5f);

            GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ceiling.name = "Ceiling";
            ceiling.transform.SetParent(parent, false);
            ceiling.transform.position = center;
            // Plane primitivo tem 10x10 unidades e a normal olha para +Y; giramos 180° em X
            // para a face virada olhar para BAIXO (para dentro do andar).
            ceiling.transform.rotation = Quaternion.Euler(180f, 0f, 0f);
            ceiling.transform.localScale = new Vector3(width / 10f, 1f, depth / 10f);

            var renderer = ceiling.GetComponent<MeshRenderer>();
            if (material != null) renderer.sharedMaterial = material;

            if (layer >= 0) ceiling.layer = layer;

            return ceiling;
        }
    }
}
