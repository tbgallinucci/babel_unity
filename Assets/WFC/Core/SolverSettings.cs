// ============================================================================
//  SolverSettings.cs  —  camada WFC.Core
// ============================================================================

namespace WFC.Core
{
    public struct SolverSettings
    {
        /// <summary>
        /// Teto de tentativas antes de desistir e devolver Contradiction (Decisão 3).
        /// Cada tentativa reusa os buffers e recomeça de uma seed derivada — não realoca.
        /// </summary>
        public int MaxAttempts;

        /// <summary>
        /// Teto de colapsos por tentativa. Cinto de segurança contra loop infinito;
        /// no caminho normal é sempre igual ao número de células.
        /// </summary>
        public int MaxIterationsPerAttempt;

        /// <summary>
        /// Ruído aplicado à entropia para desempatar células equivalentes. Sem isso o
        /// solver percorre o grid em ordem de índice e o resultado sai "listrado".
        /// </summary>
        public double EntropyNoise;

        public static SolverSettings Default => new SolverSettings
        {
            MaxAttempts = 12,
            MaxIterationsPerAttempt = 0, // 0 = usa CellCount
            EntropyNoise = 1e-4,
        };
    }
}
