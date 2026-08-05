// ============================================================================
//  RoomRole.cs  —  camada WFC.Data
//
//  O PAPEL de uma célula no andar — o que ela significa, não o que ela é
//  geometricamente (isso é ModuleTag).
//
//  Quem carimba os papéis é o SkeletonGenerator (Fase 2), na escala grossa:
//  ele decide onde ficam entrada, salas e escada como BLOCOS de células
//  (Decisão 6) e carimba o mesmo papel em todas as células do bloco.
//  Depois o mapa de papéis é exposto ao populador (Decisão 4) — ele é metade
//  do contrato de saída do plugin.
// ============================================================================

namespace WFC.Data
{
    public enum RoomRole
    {
        /// <summary>Fora do andar jogável: rocha maciça, vazio, recheio.</summary>
        Vazio = 0,

        /// <summary>Onde o jogador chega ao entrar no andar.</summary>
        Entrada = 1,

        /// <summary>Ligação entre salas.</summary>
        Corredor = 2,

        /// <summary>Sala de combate (o populador decide o encontro).</summary>
        Combate = 3,

        /// <summary>Saída para o andar de cima — o gatilho da próxima geração.</summary>
        Escada = 4,
    }
}
