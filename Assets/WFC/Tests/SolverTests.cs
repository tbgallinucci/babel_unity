// ============================================================================
//  SolverTests.cs
//
//  Cobre observe / collapse / propagate, reprodutibilidade por seed, respeito às
//  pré-restrições e detecção de contradição.
//
//  A asserção que vale por todas é AssertSolucaoConsistente: percorre o
//  resultado e confere, para CADA par de células vizinhas, que a adjacência
//  permite aquela combinação. Se o propagate tiver qualquer furo, isso pega.
// ============================================================================

using NUnit.Framework;
using WFC.Core;

namespace WFC.Tests
{
    public class SolverTests
    {
        // ------------------------------------------------------------ fixtures

        /// <summary>Tileset de 1 módulo que aceita a si mesmo em todas as direções.</summary>
        private static AdjacencyTable TabelaTrivial()
        {
            var t = new AdjacencyTable(1);
            foreach (Direction d in Directions.All) t.Allow(0, d, 0);
            return t;
        }

        /// <summary>
        /// Tabuleiro de xadrez: o módulo 0 só aceita o 1 ao lado e vice-versa.
        /// Só nas 4 direções horizontais — o eixo Y fica sem uso de propósito
        /// (é o caso do tileset de um andar só).
        /// </summary>
        private static AdjacencyTable TabelaXadrez()
        {
            var t = new AdjacencyTable(2);
            foreach (Direction d in Directions.Horizontal)
            {
                t.Allow(0, d, 1);
                t.Allow(1, d, 0);
            }
            return t;
        }

        /// <summary>Dois módulos que nunca se misturam: 0 só encosta em 0, 1 só em 1.</summary>
        private static AdjacencyTable TabelaSegregada()
        {
            var t = new AdjacencyTable(2);
            foreach (Direction d in Directions.Horizontal)
            {
                t.Allow(0, d, 0);
                t.Allow(1, d, 1);
            }
            return t;
        }

        private static double[] Pesos(int n, double value = 1.0)
        {
            var w = new double[n];
            for (int i = 0; i < n; i++) w[i] = value;
            return w;
        }

        private static void AssertSolucaoConsistente(Grid3D grid, AdjacencyTable table, int[] modules)
        {
            Assert.IsNotNull(modules, "solução nula");
            Assert.AreEqual(grid.CellCount, modules.Length);

            for (int c = 0; c < grid.CellCount; c++)
            {
                Assert.GreaterOrEqual(modules[c], 0, $"célula {c} sem módulo");

                foreach (Direction d in Directions.All)
                {
                    if (!grid.TryGetNeighbor(c, d, out int n)) continue;
                    Assert.IsTrue(
                        table.IsAllowed(modules[c], d, modules[n]),
                        $"par proibido: célula {c} (módulo {modules[c]}) x célula {n} (módulo {modules[n]}) em {d}");
                }
            }
        }

        // ------------------------------------------------------------- básicos

        [Test]
        public void TilesetTrivial_ResolveTudoComOUnicoModulo()
        {
            var grid = new Grid3D(6, 1, 6);
            var solver = new WFCSolver(grid, TabelaTrivial(), Pesos(1));

            SolveResult r = solver.Solve(1, SolverSettings.Default);

            Assert.AreEqual(SolveStatus.Success, r.Status, r.Message);
            Assert.AreEqual(1, r.Attempts);
            foreach (int m in r.Modules) Assert.AreEqual(0, m);
        }

        [Test]
        public void Xadrez_ProduzTabuleiroAlternado()
        {
            var grid = new Grid3D(6, 1, 6);
            AdjacencyTable table = TabelaXadrez();
            var solver = new WFCSolver(grid, table, Pesos(2));

            SolveResult r = solver.Solve(7, SolverSettings.Default);

            Assert.AreEqual(SolveStatus.Success, r.Status, r.Message);
            AssertSolucaoConsistente(grid, table, r.Modules);

            // Com adjacência de xadrez, o módulo é função da paridade de (x+z).
            int paridadeDoZero = r.Modules[grid.Index(0, 0, 0)];
            for (int z = 0; z < 6; z++)
            for (int x = 0; x < 6; x++)
            {
                int esperado = ((x + z) % 2 == 0) ? paridadeDoZero : 1 - paridadeDoZero;
                Assert.AreEqual(esperado, r.Modules[grid.Index(x, 0, z)], $"célula ({x},{z})");
            }
        }

        [Test]
        public void GridDeAlturaMaiorQueUm_TambemResolve()
        {
            // O Core é 3D de verdade: o mesmo tileset trivial preenche 3 andares.
            var grid = new Grid3D(4, 3, 4);
            AdjacencyTable table = TabelaTrivial();
            var solver = new WFCSolver(grid, table, Pesos(1));

            SolveResult r = solver.Solve(3, SolverSettings.Default);

            Assert.AreEqual(SolveStatus.Success, r.Status, r.Message);
            AssertSolucaoConsistente(grid, table, r.Modules);
        }

        // ------------------------------------------------------ reprodutibilidade

        [Test]
        public void MesmaSeed_ProduzExatamenteOMesmoAndar()
        {
            var grid = new Grid3D(8, 1, 8);
            AdjacencyTable table = TabelaXadrez();

            var a = new WFCSolver(grid, table, Pesos(2));
            var b = new WFCSolver(grid, table, Pesos(2));

            SolveResult ra = a.Solve(20250804, SolverSettings.Default);
            SolveResult rb = b.Solve(20250804, SolverSettings.Default);

            Assert.AreEqual(SolveStatus.Success, ra.Status);
            Assert.AreEqual(SolveStatus.Success, rb.Status);
            CollectionAssert.AreEqual(ra.Modules, rb.Modules);
        }

        [Test]
        public void MesmoSolver_ReutilizadoDaOMesmoResultadoParaAMesmaSeed()
        {
            // Os buffers são reaproveitados entre chamadas; um reset incompleto
            // apareceria aqui como resultado diferente na segunda passada.
            var grid = new Grid3D(8, 1, 8);
            AdjacencyTable table = TabelaXadrez();
            var solver = new WFCSolver(grid, table, Pesos(2));

            int[] primeiro = solver.Solve(555, SolverSettings.Default).Modules;
            solver.Solve(999, SolverSettings.Default);                 // outra seed no meio
            int[] terceiro = solver.Solve(555, SolverSettings.Default).Modules;

            CollectionAssert.AreEqual(primeiro, terceiro);
        }

        [Test]
        public void SeedsDiferentes_ProduzemAndaresDiferentes()
        {
            // Pesos desiguais para que o sorteio tenha o que variar.
            var grid = new Grid3D(10, 1, 10);
            var table = new AdjacencyTable(3);
            foreach (Direction d in Directions.Horizontal)
                for (int a = 0; a < 3; a++)
                for (int b = 0; b < 3; b++)
                    table.Allow(a, d, b);

            var solver = new WFCSolver(grid, table, new[] { 3.0, 1.0, 0.5 });

            int[] r1 = solver.Solve(1, SolverSettings.Default).Modules;
            int[] r2 = solver.Solve(2, SolverSettings.Default).Modules;

            CollectionAssert.AreNotEqual(r1, r2);
        }

        // --------------------------------------------------------- restrições

        [Test]
        public void Constraint_FixaACelulaEPropaga()
        {
            var grid = new Grid3D(5, 1, 5);
            AdjacencyTable table = TabelaXadrez();
            var solver = new WFCSolver(grid, table, Pesos(2));

            int alvo = grid.Index(2, 0, 2);
            solver.SetConstraints(new[] { Constraint.Fix(alvo, 1, 2) });

            SolveResult r = solver.Solve(11, SolverSettings.Default);

            Assert.AreEqual(SolveStatus.Success, r.Status, r.Message);
            Assert.AreEqual(1, r.Modules[alvo]);
            AssertSolucaoConsistente(grid, table, r.Modules);

            // A restrição no centro decide o tabuleiro inteiro pela paridade.
            Assert.AreEqual(0, r.Modules[grid.Index(1, 0, 2)]);
            Assert.AreEqual(0, r.Modules[grid.Index(2, 0, 1)]);
        }

        [Test]
        public void Forbid_ImpedeOModuloNaCelula()
        {
            var grid = new Grid3D(4, 1, 4);
            var table = new AdjacencyTable(2);
            foreach (Direction d in Directions.Horizontal)
                for (int a = 0; a < 2; a++)
                for (int b = 0; b < 2; b++)
                    table.Allow(a, d, b); // tudo com tudo

            var solver = new WFCSolver(grid, table, Pesos(2));

            int alvo = grid.Index(1, 0, 1);
            solver.SetConstraints(new[] { Constraint.Forbid(alvo, 2, 0) });

            SolveResult r = solver.Solve(5, SolverSettings.Default);

            Assert.AreEqual(SolveStatus.Success, r.Status, r.Message);
            Assert.AreEqual(1, r.Modules[alvo]);
        }

        [Test]
        public void RestricoesIncompativeis_DevolvemInvalidConstraints()
        {
            // 0 só encosta em 0 e 1 só em 1; forçar vizinhos diferentes é impossível
            // e a consistência de arco descobre isso ANTES de sortear qualquer coisa.
            // Retry não ajuda — daí o status ser específico.
            var grid = new Grid3D(2, 1, 1);
            var solver = new WFCSolver(grid, TabelaSegregada(), Pesos(2));

            solver.SetConstraints(new[]
            {
                Constraint.Fix(grid.Index(0, 0, 0), 0, 2),
                Constraint.Fix(grid.Index(1, 0, 0), 1, 2),
            });

            SolveResult r = solver.Solve(1, SolverSettings.Default);

            Assert.AreEqual(SolveStatus.InvalidConstraints, r.Status);
            Assert.AreEqual(0, r.Attempts, "não deve gastar tentativa numa restrição impossível");
            Assert.IsNull(r.Modules);
        }

        [Test]
        public void ConstraintQueZeraODominio_EhDetectadaDeCara()
        {
            var grid = new Grid3D(3, 1, 3);
            var solver = new WFCSolver(grid, TabelaTrivial(), Pesos(1));

            // Máscara vazia = nenhum módulo permitido.
            var vazia = new ulong[Bitset.WordCount(1)];
            solver.SetConstraints(new[] { new Constraint(0, vazia) });

            SolveResult r = solver.Solve(1, SolverSettings.Default);
            Assert.AreEqual(SolveStatus.InvalidConstraints, r.Status);
            Assert.AreEqual(0, r.ContradictionCell);
        }

        [Test]
        public void SetConstraintsNulo_EquivaleASemRestricao()
        {
            var grid = new Grid3D(4, 1, 4);
            AdjacencyTable table = TabelaXadrez();

            var comNull = new WFCSolver(grid, table, Pesos(2));
            comNull.SetConstraints(null);
            SolveResult a = comNull.Solve(77, SolverSettings.Default);

            var semChamar = new WFCSolver(grid, table, Pesos(2));
            SolveResult b = semChamar.Solve(77, SolverSettings.Default);

            Assert.AreEqual(SolveStatus.Success, a.Status);
            CollectionAssert.AreEqual(a.Modules, b.Modules);
        }

        // ------------------------------------------------------ selar bordas

        [Test]
        public void SealBorders_RestringeSomenteAsCelulasDeBorda()
        {
            var grid = new Grid3D(4, 1, 4);
            const int moduleCount = 2;

            // Só o módulo 1 é aceitável virado para fora, em qualquer direção.
            ulong[] soUm = ConstraintBuilder.MaskWhere(moduleCount, m => m == 1);

            var constraints = ConstraintBuilder.SealBorders(grid, moduleCount, _ => soUm);

            // Num grid 4x4 há 16 células e apenas as 4 do miolo não tocam a borda.
            Assert.AreEqual(12, constraints.Count);
            foreach (Constraint k in constraints)
            {
                grid.Coords(k.CellIndex, out int x, out _, out int z);
                bool naBorda = x == 0 || x == 3 || z == 0 || z == 3;
                Assert.IsTrue(naBorda, $"célula ({x},{z}) não é de borda mas recebeu constraint");
            }
        }

        // -------------------------------------------------------- observações

        [Test]
        public void GetCell_RefleteODominioDepoisDasRestricoes()
        {
            var grid = new Grid3D(3, 1, 3);
            var solver = new WFCSolver(grid, TabelaXadrez(), Pesos(2));

            solver.SetConstraints(new[] { Constraint.Fix(0, 1, 2) });

            Cell c0 = solver.GetCell(0);
            Assert.IsTrue(c0.IsCollapsed);
            Assert.AreEqual(1, c0.CollapsedModule);
            Assert.IsTrue(c0.Contains(1));
            Assert.IsFalse(c0.Contains(0));
        }

        [Test]
        public void PesoZeroOuNegativo_EhRejeitadoNoConstrutor()
        {
            var grid = new Grid3D(2, 1, 2);
            Assert.Throws<System.ArgumentException>(
                () => new WFCSolver(grid, TabelaTrivial(), new[] { 0.0 }));
        }

        [Test]
        public void QuantidadeDePesosErrada_EhRejeitada()
        {
            var grid = new Grid3D(2, 1, 2);
            Assert.Throws<System.ArgumentException>(
                () => new WFCSolver(grid, TabelaXadrez(), Pesos(3)));
        }

        [Test]
        public void GridGrande_ResolveDentroDeUmOrcamentoDeLoading()
        {
            // Não é benchmark; é um alarme para regressão grosseira de performance.
            //
            // Tileset "gradiente": o módulo m encosta em m-1, m e m+1 (mod 8). É
            // simétrico, faz a propagação realmente podar domínios (ao contrário de
            // um tileset onde tudo encosta em tudo) e nunca contradiz — um campo
            // constante já é solução —, então o teste não fica instável.
            var grid = new Grid3D(48, 1, 48);
            const int n = 8;
            var table = new AdjacencyTable(n);
            foreach (Direction d in Directions.Horizontal)
                for (int a = 0; a < n; a++)
                for (int b = 0; b < n; b++)
                {
                    int delta = ((a - b) % n + n) % n;
                    if (delta == 0 || delta == 1 || delta == n - 1) table.Allow(a, d, b);
                }

            Assert.IsTrue(table.Validate(out string err), err);

            var solver = new WFCSolver(grid, table, Pesos(n));

            var sw = System.Diagnostics.Stopwatch.StartNew();
            SolveResult r = solver.Solve(2024, SolverSettings.Default);
            sw.Stop();

            Assert.AreEqual(SolveStatus.Success, r.Status, r.Message);
            AssertSolucaoConsistente(grid, table, r.Modules);
            Assert.Less(sw.ElapsedMilliseconds, 2000,
                $"2304 células levaram {sw.ElapsedMilliseconds} ms — algo regrediu no hot path");
        }
    }
}
