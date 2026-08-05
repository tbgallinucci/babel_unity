// ============================================================================
//  RandomTests.cs
//  Reprodutibilidade por seed é requisito do projeto (pré-geração de andar,
//  reproduzir bug a partir de um número). Se o RNG escorregar, tudo escorrega.
// ============================================================================

using NUnit.Framework;
using WFC.Core;

namespace WFC.Tests
{
    public class RandomTests
    {
        [Test]
        public void MesmaSeed_ProduzMesmaSequencia()
        {
            var a = new XorShiftRandom(1234);
            var b = new XorShiftRandom(1234);

            for (int i = 0; i < 500; i++)
                Assert.AreEqual(a.NextDouble(), b.NextDouble(), 0.0, $"amostra {i}");
        }

        [Test]
        public void SeedsVizinhas_ProduzemSequenciasDiferentes()
        {
            // Sem o SplitMix64 no construtor, seeds 0/1/2 gerariam sequências
            // quase idênticas — é justamente isso que este teste protege.
            var a = new XorShiftRandom(0);
            var b = new XorShiftRandom(1);

            int iguais = 0;
            for (int i = 0; i < 100; i++)
                if (a.NextDouble() == b.NextDouble()) iguais++;

            Assert.Less(iguais, 5, "seeds vizinhas estão gerando sequências parecidas demais");
        }

        [Test]
        public void NextDouble_FicaNoIntervaloUnitario()
        {
            var rng = new XorShiftRandom(98765);
            for (int i = 0; i < 10000; i++)
            {
                double v = rng.NextDouble();
                Assert.GreaterOrEqual(v, 0.0);
                Assert.Less(v, 1.0);
            }
        }

        [Test]
        public void NextInt_RespeitaOLimite()
        {
            var rng = new XorShiftRandom(42);
            for (int i = 0; i < 10000; i++)
            {
                int v = rng.NextInt(7);
                Assert.GreaterOrEqual(v, 0);
                Assert.Less(v, 7);
            }
        }

        [Test]
        public void NextInt_CobreTodosOsValores()
        {
            var rng = new XorShiftRandom(7);
            var seen = new bool[10];
            for (int i = 0; i < 5000; i++) seen[rng.NextInt(10)] = true;
            foreach (bool s in seen) Assert.IsTrue(s);
        }

        [Test]
        public void DeriveSeed_EhDeterministicoEVariaPorTentativa()
        {
            Assert.AreEqual(WFCSolver.DeriveSeed(999, 3), WFCSolver.DeriveSeed(999, 3));
            Assert.AreNotEqual(WFCSolver.DeriveSeed(999, 3), WFCSolver.DeriveSeed(999, 4));
            Assert.AreNotEqual(WFCSolver.DeriveSeed(999, 0), WFCSolver.DeriveSeed(1000, 0));
        }
    }
}
