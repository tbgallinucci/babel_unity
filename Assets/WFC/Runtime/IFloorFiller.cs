// ============================================================================
//  IFloorFiller.cs  —  camada WFC.Runtime  (Decisão 5)
//
//  O passo que transforma o grid anotado (papéis + portas) em geometria 3D
//  instanciada fica atrás desta interface. Assim o esqueleto, o contrato de
//  saída, o pooling, o NavMesh e o populador NÃO sabem qual estratégia de
//  preenchimento está em uso — e trocar/adicionar estratégia não quebra nada.
//
//  Implementações previstas:
//   • Caminho A — WFCFiller (PRIMÁRIA, Fase 2): o solver de verdade, com
//     sockets, adjacência e rotações automáticas, respeitando as âncoras do
//     esqueleto. É o objetivo do projeto.
//   • Caminho B — EdgeWallFiller (opcional/futuro): sem WFC, piso nas células
//     de chão e parede/porta nas bordas. Fallback leve, fora de foco.
//
//  Nesta fase (0/1) a interface existe, mas quem exercita o solver é o
//  WFCFloorPreview — um harness de demonstração, não uma implementação de
//  IFloorFiller. Ele será substituído pelo WFCFiller na Fase 2.
// ============================================================================

using WFC.Core;

namespace WFC.Runtime
{
    public interface IFloorFiller
    {
        /// <summary>
        /// Recebe o grid já anotado (papéis + portas) e devolve as instâncias + anchors.
        /// Deve ser determinístico em função de <paramref name="rng"/>.
        /// </summary>
        FloorFillResult Fill(AnnotatedGrid grid, IRandom rng);
    }
}
