// ============================================================================
//  DoorReconciler.cs  —  camada WFC.Runtime
//
//  Apara as portas que o esqueleto cravou, deixando só as que o TILESET REAL
//  consegue representar. As que não couberem viram vão simples.
//
//  Por que isso existe como passo separado:
//
//   • O SkeletonGenerator é agnóstico ao kit (Decisão 1). Ele promete
//     CONECTIVIDADE, não promete batente de porta. Ensinar a ele quais formatos
//     de célula existem acopla o layout a um tileset específico — e apodrece na
//     hora em que alguém acrescenta ou remove uma peça.
//   • A verdade sobre o que o kit sabe fazer está no TileSet bakeado, não numa
//     lista escrita à mão no código.
//
//  Conectividade é preservada por construção: vão conecta igual a porta. Na pior
//  das hipóteses o andar sai sem batentes — nunca sem caminho.
//
//  A dependência aqui é um DELEGATE de máscara, não o TileSet: além de manter
//  este arquivo livre de ScriptableObject (e portanto testável fora da engine),
//  deixa qualquer outro filler futuro reusar a mesma lógica.
// ============================================================================

using System;
using WFC.Core;

namespace WFC.Runtime
{
    public static class DoorReconciler
    {
        /// <summary>Dada uma direção e um nome de socket, a máscara de variantes que servem naquela face.</summary>
        public delegate ulong[] MaskProvider(Direction dir, string socket);

        /// <summary>
        /// Rebaixa para <see cref="Junction.Opening"/> toda porta cujo formato de célula
        /// não tenha peça no tileset. Devolve quantas foram rebaixadas.
        ///
        /// O laço roda até estabilizar porque rebaixar uma porta muda o rótulo da borda
        /// dos DOIS lados, o que pode revelar (ou resolver) o problema do vizinho.
        /// </summary>
        public static int Reconcile(AnnotatedGrid grid, int variantCount, MaskProvider maskFor)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (maskFor == null) throw new ArgumentNullException(nameof(maskFor));
            if (variantCount <= 0) return 0;

            int wordCount = Bitset.WordCount(variantCount);
            Grid3D g = grid.Grid;

            int downgraded = 0;
            bool changed = true;

            while (changed)
            {
                changed = false;

                for (int cell = 0; cell < g.CellCount; cell++)
                {
                    if (grid.CountBorders(cell, BorderLabel.Door) == 0) continue;
                    if (IsRepresentable(grid, cell, variantCount, wordCount, maskFor)) continue;

                    // Derruba TODAS as portas desta célula de uma vez. Rebaixar uma a uma
                    // daria resultado dependente da ordem de iteração das direções.
                    foreach (Direction dir in Directions.Horizontal)
                    {
                        if (grid.GetJunction(cell, dir) != Junction.Door) continue;
                        grid.SetJunction(cell, dir, Junction.Opening);
                        downgraded++;
                    }

                    changed = true;
                }
            }

            return downgraded;
        }

        /// <summary>Existe alguma variante que case com as 4 bordas horizontais desta célula?</summary>
        public static bool IsRepresentable(AnnotatedGrid grid, int cell, int variantCount,
                                           int wordCount, MaskProvider maskFor)
        {
            ulong[] mask = ConstraintBuilder.FullMask(variantCount);

            foreach (Direction dir in Directions.Horizontal)
            {
                string socket = BorderLabels.ToSocket(grid.GetBorder(cell, dir));
                Bitset.AndInPlace(mask, 0, maskFor(dir, socket), 0, wordCount);
            }

            return !Bitset.IsEmpty(mask, 0, wordCount);
        }
    }
}
