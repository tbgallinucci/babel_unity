// ============================================================================
//  AdjacencyTable.cs  —  camada WFC.Core
//
//  Para cada par (módulo, direção), o bitset dos módulos que podem ficar do
//  outro lado daquela face. É a ÚNICA coisa que o solver sabe sobre o tileset:
//  sockets, prefabs e rotações já foram digeridos no bake (edit-time) e não
//  existem mais aqui. Isso é o que mantém o Core puro e testável.
//
//  Layout do array: [(módulo * 6 + direção) * WordCount + palavra]
// ============================================================================

using System;

namespace WFC.Core
{
    public sealed class AdjacencyTable
    {
        public int ModuleCount { get; }
        public int WordCount { get; }

        /// <summary>Array plano. Exposto para o hot path do solver ler direto, sem cópia.</summary>
        public ulong[] Data { get; }

        public AdjacencyTable(int moduleCount)
        {
            if (moduleCount <= 0) throw new ArgumentOutOfRangeException(nameof(moduleCount));
            ModuleCount = moduleCount;
            WordCount = Bitset.WordCount(moduleCount);
            Data = new ulong[moduleCount * Directions.Count * WordCount];
        }

        /// <summary>Offset da linha (módulo, direção) dentro de <see cref="Data"/>.</summary>
        public int OffsetOf(int module, Direction dir)
            => (module * Directions.Count + (int)dir) * WordCount;

        /// <summary>Permite que <paramref name="neighbor"/> fique na direção <paramref name="dir"/> de <paramref name="module"/>.</summary>
        public void Allow(int module, Direction dir, int neighbor)
            => Bitset.Set(Data, OffsetOf(module, dir), neighbor);

        /// <summary>Versão simétrica: registra os dois sentidos de uma vez. É o que o bake deve usar.</summary>
        public void AllowSymmetric(int module, Direction dir, int neighbor)
        {
            Allow(module, dir, neighbor);
            Allow(neighbor, Directions.Opposite(dir), module);
        }

        public bool IsAllowed(int module, Direction dir, int neighbor)
            => Bitset.Get(Data, OffsetOf(module, dir), neighbor);

        /// <summary>Quantos vizinhos o módulo aceita naquela direção (0 = módulo impossível de colocar).</summary>
        public int AllowedCount(int module, Direction dir)
            => Bitset.PopCount(Data, OffsetOf(module, dir), WordCount);

        /// <summary>
        /// Algum módulo conecta nessa direção? Se não, o tileset não usa aquele eixo —
        /// é o caso normal de PosY/NegY num tileset de andar único (grid com SizeY == 1).
        /// </summary>
        public bool IsAxisUsed(Direction dir)
        {
            for (int m = 0; m < ModuleCount; m++)
                if (AllowedCount(m, dir) > 0) return true;
            return false;
        }

        /// <summary>
        /// Verifica a invariante que mais dá dor de cabeça: a tabela precisa ser
        /// simétrica. Se A aceita B em +X, então B tem que aceitar A em -X — senão
        /// a propagação fica inconsistente e o solver "trava" de formas bizarras.
        /// Rodado pelo botão "Rebuild adjacency" e pelos testes, não no hot path.
        /// </summary>
        public bool Validate(out string error)
        {
            for (int m = 0; m < ModuleCount; m++)
            {
                foreach (Direction dir in Directions.All)
                {
                    int off = OffsetOf(m, dir);
                    Direction opp = Directions.Opposite(dir);

                    for (int n = Bitset.NextSetBit(Data, off, WordCount, 0);
                         n >= 0;
                         n = Bitset.NextSetBit(Data, off, WordCount, n + 1))
                    {
                        if (!IsAllowed(n, opp, m))
                        {
                            error = $"Adjacência assimétrica: módulo {m} aceita {n} em {dir}, " +
                                    $"mas {n} não aceita {m} em {opp}.";
                            return false;
                        }
                    }
                }
            }

            // Módulo que não aceita ninguém numa direção só pode existir na borda do
            // grid — quase sempre é erro de autoria de socket.
            //
            // Exceção: se NENHUM módulo usa aquele eixo (típico de um tileset de um
            // andar só, onde PosY/NegY não conectam com nada), o tileset simplesmente
            // não é empilhável. Isso não é erro — é só um grid de altura 1.
            foreach (Direction dir in Directions.All)
            {
                if (!IsAxisUsed(dir)) continue;

                for (int m = 0; m < ModuleCount; m++)
                {
                    if (AllowedCount(m, dir) == 0)
                    {
                        error = $"Módulo {m} não aceita nenhum vizinho na direção {dir} " +
                                $"(socket órfão — ele só poderá aparecer na borda do grid).";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }
    }
}
