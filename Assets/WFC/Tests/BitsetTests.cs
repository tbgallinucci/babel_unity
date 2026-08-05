// ============================================================================
//  BitsetTests.cs
//  O bitset é a base de tudo. Se ele estiver errado, o solver falha de formas
//  que parecem "bug de tileset" — daí testar o alicerce primeiro.
// ============================================================================

using NUnit.Framework;
using WFC.Core;

namespace WFC.Tests
{
    public class BitsetTests
    {
        [TestCase(1, 1)]
        [TestCase(64, 1)]
        [TestCase(65, 2)]
        [TestCase(128, 2)]
        [TestCase(129, 3)]
        public void WordCount_ArredondaParaCima(int bits, int expected)
        {
            Assert.AreEqual(expected, Bitset.WordCount(bits));
        }

        [Test]
        public void FillOnes_NaoLigaBitsAlemDoModuleCount()
        {
            // 70 módulos ocupam 2 palavras; os 58 bits sobrando da 2ª palavra
            // PRECISAM ficar zerados, senão o PopCount conta módulo que não existe.
            const int bits = 70;
            int words = Bitset.WordCount(bits);
            var data = new ulong[words];

            Bitset.FillOnes(data, 0, words, bits);

            Assert.AreEqual(bits, Bitset.PopCount(data, 0, words));
            Assert.IsTrue(Bitset.Get(data, 0, 69));
            Assert.IsFalse(Bitset.Get(data, 0, 70));
        }

        [Test]
        public void SetGetClear_FuncionamAtravessandoPalavras()
        {
            var data = new ulong[3];
            Bitset.Set(data, 0, 0);
            Bitset.Set(data, 0, 63);
            Bitset.Set(data, 0, 64);
            Bitset.Set(data, 0, 190);

            Assert.IsTrue(Bitset.Get(data, 0, 63));
            Assert.IsTrue(Bitset.Get(data, 0, 64));
            Assert.IsTrue(Bitset.Get(data, 0, 190));
            Assert.AreEqual(4, Bitset.PopCount(data, 0, 3));

            Bitset.Clear(data, 0, 64);
            Assert.IsFalse(Bitset.Get(data, 0, 64));
            Assert.AreEqual(3, Bitset.PopCount(data, 0, 3));
        }

        [Test]
        public void NextSetBit_IteraTodosOsBitsNaOrdem()
        {
            var data = new ulong[3];
            int[] expected = { 3, 63, 64, 127, 128, 191 };
            foreach (int b in expected) Bitset.Set(data, 0, b);

            var seen = new System.Collections.Generic.List<int>();
            for (int m = Bitset.NextSetBit(data, 0, 3, 0); m >= 0; m = Bitset.NextSetBit(data, 0, 3, m + 1))
                seen.Add(m);

            CollectionAssert.AreEqual(expected, seen);
        }

        [Test]
        public void NextSetBit_DevolveMenosUmQuandoAcaba()
        {
            var data = new ulong[1];
            Bitset.Set(data, 0, 5);
            Assert.AreEqual(5, Bitset.NextSetBit(data, 0, 1, 0));
            Assert.AreEqual(-1, Bitset.NextSetBit(data, 0, 1, 6));
        }

        [Test]
        public void AndInPlace_SinalizaMudancaSomenteQuandoEncolhe()
        {
            var a = new ulong[1];
            var b = new ulong[1];
            Bitset.Set(a, 0, 1);
            Bitset.Set(a, 0, 2);
            Bitset.Set(b, 0, 1);
            Bitset.Set(b, 0, 2);
            Bitset.Set(b, 0, 3);

            // b é superconjunto de a: o AND não muda nada.
            Assert.IsFalse(Bitset.AndInPlace(a, 0, b, 0, 1));
            Assert.AreEqual(2, Bitset.PopCount(a, 0, 1));

            var c = new ulong[1];
            Bitset.Set(c, 0, 1);
            Assert.IsTrue(Bitset.AndInPlace(a, 0, c, 0, 1));
            Assert.AreEqual(1, Bitset.PopCount(a, 0, 1));
            Assert.IsTrue(Bitset.Get(a, 0, 1));
        }

        [Test]
        public void IsEmpty_DetectaContradicao()
        {
            var data = new ulong[2];
            Assert.IsTrue(Bitset.IsEmpty(data, 0, 2));
            Bitset.Set(data, 0, 100);
            Assert.IsFalse(Bitset.IsEmpty(data, 0, 2));
        }

        [Test]
        public void PopCount64_BateComContagemIngenua()
        {
            ulong[] samples = { 0UL, 1UL, ulong.MaxValue, 0xDEADBEEFCAFEBABEUL, 0x8000000000000000UL };
            foreach (ulong v in samples)
            {
                int naive = 0;
                for (int i = 0; i < 64; i++) if ((v & (1UL << i)) != 0) naive++;
                Assert.AreEqual(naive, Bitset.PopCount64(v), $"valor 0x{v:X}");
            }
        }
    }
}
