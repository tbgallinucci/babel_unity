// ============================================================================
//  SolveResult.cs  —  camada WFC.Core
//
//  Resultado de uma resolução. Note que contradição NÃO vira exceção: é um
//  status normal, porque em runtime ela é esperada de vez em quando e o
//  chamador reage com retry/fallback (Decisão 3), não com try/catch.
// ============================================================================

namespace WFC.Core
{
    public enum SolveStatus
    {
        /// <summary>Todas as células colapsadas sem contradição.</summary>
        Success = 0,

        /// <summary>Alguma célula ficou com domínio vazio em todas as tentativas.</summary>
        Contradiction = 1,

        /// <summary>As pré-restrições já se contradizem entre si — retry não adianta.</summary>
        InvalidConstraints = 2,

        /// <summary>Estourou o teto de iterações (proteção contra loop infinito).</summary>
        IterationLimit = 3,

        /// <summary>Cancelado de fora (ex.: token de cancelamento na transição de andar).</summary>
        Cancelled = 4,
    }

    public sealed class SolveResult
    {
        public SolveStatus Status { get; internal set; }

        /// <summary>
        /// Módulo escolhido por célula, indexado pelo índice linear do <see cref="Grid3D"/>.
        /// Em caso de falha vem null. Os índices são de VARIANTE (módulo já rotacionado):
        /// quem traduz variante → (prefab, rotação em Y) é a camada Data.
        /// </summary>
        public int[] Modules { get; internal set; }

        /// <summary>Quantas tentativas foram gastas (1 = acertou de primeira).</summary>
        public int Attempts { get; internal set; }

        /// <summary>Seed efetivamente usada na tentativa que deu certo — guarde para reproduzir o andar.</summary>
        public int Seed { get; internal set; }

        /// <summary>Célula onde a contradição estourou (-1 se não houve).</summary>
        public int ContradictionCell { get; internal set; } = -1;

        public string Message { get; internal set; }

        public bool Succeeded => Status == SolveStatus.Success;

        public override string ToString()
            => $"{Status} (tentativas: {Attempts}, seed: {Seed})" +
               (Message != null ? $" — {Message}" : string.Empty);
    }
}
