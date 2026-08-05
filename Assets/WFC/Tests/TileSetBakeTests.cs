// ============================================================================
//  TileSetBakeTests.cs
//
//  Cobre a camada Data: rotações automáticas em Y, regras de socket (simétrico,
//  assimétrico/flipped, vertical com índice) e o bake da AdjacencyTable.
//
//  O último teste é de integração: monta em memória o mesmo tileset que o
//  WFCTilesetBootstrap gera para o greybox, bakeia e resolve um andar de 12x12
//  com as bordas seladas — ou seja, exercita Data + Core juntos, sem depender
//  de nenhum asset em disco.
// ============================================================================

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WFC.Core;
using WFC.Data;

namespace WFC.Tests
{
    public class TileSetBakeTests
    {
        private GreyboxFixture _fx;

        [SetUp]
        public void SetUp() => _fx = new GreyboxFixture();

        [TearDown]
        public void TearDown() => _fx.Cleanup();

        private T New<T>(string nome) where T : ScriptableObject => _fx.New<T>(nome);

        // ------------------------------------------------------------ helpers
        private SocketLibrary BibliotecaGreybox() => _fx.Library();

        private ModuleDefinition Modulo(string nome, string posX, string negX, string posZ, string negZ,
                                        float peso, ModuleTag tags, bool[] rots)
            => _fx.Module(nome, posX, negX, posZ, negZ, peso, tags, rots);

        private static readonly bool[] Rot0 = { true, false, false, false };
        private static readonly bool[] Rot01 = { true, true, false, false };
        private static readonly bool[] RotTodas = { true, true, true, true };

        /// <summary>Constraints que exigem face `wall` virada para fora em toda célula de borda.</summary>
        private static List<Constraint> SelarBordas(TileSet ts, Grid3D grid)
        {
            var cache = new Dictionary<Direction, ulong[]>();
            return ConstraintBuilder.SealBorders(
                grid, ts.VariantCount,
                dir =>
                {
                    if (!cache.TryGetValue(dir, out ulong[] mask))
                    {
                        mask = ts.MaskWithFaceSocket(dir, "wall");
                        cache[dir] = mask;
                    }
                    return mask;
                });
        }

        /// <summary>Réplica em memória do tileset que o WFCTilesetBootstrap gera.</summary>
        private TileSet TileSetGreybox() => _fx.TileSet();

        // ------------------------------------------------------- rotação em Y

        [Test]
        public void RotatedFace_PermutaAsFacesHorizontaisNoSentidoCerto()
        {
            var lib = BibliotecaGreybox();
            lib.UpsertHorizontal("a", true, Color.white);
            lib.UpsertHorizontal("b", true, Color.white);
            lib.UpsertHorizontal("c", true, Color.white);
            lib.UpsertHorizontal("d", true, Color.white);

            ModuleDefinition m = Modulo("teste", "a", "b", "c", "d", 1f, ModuleTag.None, RotTodas);

            // Rotação 0 não mexe em nada.
            Assert.AreEqual("a", TileSet.RotatedFace(m, 0, Direction.PosX).socket);
            Assert.AreEqual("c", TileSet.RotatedFace(m, 0, Direction.PosZ).socket);

            // A face que TERMINA em `dir` é a que ESTAVA em RotateY(dir, -rot).
            for (int rot = 0; rot < 4; rot++)
            {
                foreach (Direction dir in Directions.Horizontal)
                {
                    Direction origem = Directions.RotateY(dir, -rot);
                    Assert.AreEqual(
                        m.GetFace(origem).socket,
                        TileSet.RotatedFace(m, rot, dir).socket,
                        $"rot {rot * 90}°, face {dir}");
                }
            }
        }

        [Test]
        public void RotatedFace_IncrementaOIndiceDosSocketsVerticais()
        {
            var m = New<ModuleDefinition>("vert");
            m.SetFace(Direction.PosY, new FaceSocket("sky", false, 1));
            m.SetFace(Direction.NegY, new FaceSocket("ground", false, 0));

            Assert.AreEqual(1, TileSet.RotatedFace(m, 0, Direction.PosY).verticalRotation);
            Assert.AreEqual(2, TileSet.RotatedFace(m, 1, Direction.PosY).verticalRotation);
            Assert.AreEqual(0, TileSet.RotatedFace(m, 3, Direction.PosY).verticalRotation);
            Assert.AreEqual(3, TileSet.RotatedFace(m, 3, Direction.NegY).verticalRotation);
        }

        // ------------------------------------------------ regras de socket

        [Test]
        public void SocketSimetrico_ConectaConsigoMesmo()
        {
            var lib = New<SocketLibrary>("lib");
            lib.UpsertHorizontal("s", true, Color.white);

            var a = new FaceSocket("s");
            var b = new FaceSocket("s");
            Assert.IsTrue(lib.AreCompatible(Direction.PosX, a, b));
        }

        [Test]
        public void SocketAssimetrico_SoConectaComOEspelho()
        {
            var lib = New<SocketLibrary>("lib");
            lib.UpsertHorizontal("3", false, Color.white);

            var normal = new FaceSocket("3", flipped: false);
            var espelho = new FaceSocket("3", flipped: true);

            Assert.IsTrue(lib.AreCompatible(Direction.PosX, normal, espelho));
            Assert.IsTrue(lib.AreCompatible(Direction.PosX, espelho, normal));
            Assert.IsFalse(lib.AreCompatible(Direction.PosX, normal, normal));
            Assert.IsFalse(lib.AreCompatible(Direction.PosX, espelho, espelho));
        }

        [Test]
        public void SocketVerticalComIndice_ExigeMesmaRotacao()
        {
            var lib = New<SocketLibrary>("lib");
            lib.UpsertVertical("v", false, Color.white);

            Assert.IsTrue(lib.AreCompatible(Direction.PosY, new FaceSocket("v", false, 2), new FaceSocket("v", false, 2)));
            Assert.IsFalse(lib.AreCompatible(Direction.PosY, new FaceSocket("v", false, 2), new FaceSocket("v", false, 3)));
        }

        [Test]
        public void SocketVerticalInvariante_ConectaEmQualquerRotacao()
        {
            var lib = New<SocketLibrary>("lib");
            lib.UpsertVertical("v", true, Color.white);

            Assert.IsTrue(lib.AreCompatible(Direction.PosY, new FaceSocket("v", false, 0), new FaceSocket("v", false, 3)));
        }

        [Test]
        public void FaceSemSocket_NuncaConecta()
        {
            var lib = New<SocketLibrary>("lib");
            lib.UpsertHorizontal("s", true, Color.white);

            Assert.IsFalse(lib.AreCompatible(Direction.PosX, new FaceSocket(""), new FaceSocket("")));
            Assert.IsFalse(lib.AreCompatible(Direction.PosX, new FaceSocket("s"), new FaceSocket("")));
        }

        [Test]
        public void SocketNaoDeclaradoNaBiblioteca_NuncaConecta()
        {
            var lib = New<SocketLibrary>("lib");
            Assert.IsFalse(lib.AreCompatible(Direction.PosX, new FaceSocket("fantasma"), new FaceSocket("fantasma")));
        }

        // ------------------------------------------------------------- bake

        [Test]
        public void Bake_ExpandeAsRotacoesPermitidas()
        {
            TileSet ts = TileSetGreybox();
            Assert.IsTrue(ts.Bake(out string report), report);

            // 1 (FloorOpen) + 4 (Wall) + 4 (Corner) + 2 (Corridor) + 4 (DeadEnd)
            // + 4 (Door) + 4 (Door_Corridor) + 4 (Stairs) + 1 (SolidFill) + 1 (Air) = 29
            Assert.AreEqual(GreyboxFixture.ExpectedVariantCount, ts.VariantCount, report);
            Assert.IsTrue(ts.IsBaked);
        }

        [Test]
        public void Bake_FalhaComSocketNaoDeclarado()
        {
            SocketLibrary lib = BibliotecaGreybox();
            var ts = New<TileSet>("ts");
            ts.socketLibrary = lib;
            ts.modules = new List<ModuleDefinition>
            {
                Modulo("errado", "open", "open", "inexistente", "open", 1f, ModuleTag.None, Rot0),
            };

            Assert.IsFalse(ts.Bake(out string report));
            StringAssert.Contains("inexistente", report);
        }

        [Test]
        public void Bake_FalhaSemSocketLibrary()
        {
            var ts = New<TileSet>("ts");
            ts.modules = new List<ModuleDefinition> { Modulo("x", "open", "open", "open", "open", 1f, ModuleTag.None, Rot0) };

            Assert.IsFalse(ts.Bake(out string report));
            StringAssert.Contains("SocketLibrary", report);
        }

        [Test]
        public void Bake_ProduzTabelaSimetricaEValida()
        {
            TileSet ts = TileSetGreybox();
            Assert.IsTrue(ts.Bake(out string report), report);

            AdjacencyTable table = ts.CreateAdjacencyTable();
            Assert.IsTrue(table.Validate(out string erro), erro);
        }

        [Test]
        public void Bake_NaoConectaOEixoVerticalNesteTileset()
        {
            // Os sockets verticais têm nomes diferentes ("sky" em cima, "ground"
            // embaixo) de propósito: é um tileset de UM andar. Empilhar exigiria
            // peças novas.
            TileSet ts = TileSetGreybox();
            Assert.IsTrue(ts.Bake(out string report), report);

            AdjacencyTable table = ts.CreateAdjacencyTable();
            Assert.IsFalse(table.IsAxisUsed(Direction.PosY));
            Assert.IsFalse(table.IsAxisUsed(Direction.NegY));
            Assert.IsTrue(table.IsAxisUsed(Direction.PosX));
        }

        [Test]
        public void Bake_PesosEProbabilidadesSaoPreservados()
        {
            TileSet ts = TileSetGreybox();
            Assert.IsTrue(ts.Bake(out string report), report);

            double[] pesos = ts.CreateWeights();
            Assert.AreEqual(ts.VariantCount, pesos.Length);
            foreach (double p in pesos) Assert.Greater(p, 0.0);

            // Todas as variantes de um módulo herdam o peso dele.
            for (int v = 0; v < ts.VariantCount; v++)
                Assert.AreEqual(ts.GetModule(v).weight, pesos[v], 1e-5);
        }

        [Test]
        public void MaskWithTags_FiltraPorTagObrigatoriaEProibida()
        {
            TileSet ts = TileSetGreybox();
            Assert.IsTrue(ts.Bake(out string report), report);

            ulong[] semCoringa = ts.MaskWithTags(ModuleTag.Floor, ModuleTag.Wildcard);
            int words = Bitset.WordCount(ts.VariantCount);

            for (int v = 0; v < ts.VariantCount; v++)
            {
                ModuleTag t = ts.GetTags(v);
                bool esperado = (t & ModuleTag.Floor) != 0 && (t & ModuleTag.Wildcard) == 0;
                Assert.AreEqual(esperado, Bitset.Get(semCoringa, 0, v), ts.GetVariantName(v));
            }

            Assert.Greater(Bitset.PopCount(semCoringa, 0, words), 0);
        }

        [Test]
        public void GetRotationQuaternion_BateComAConvencaoDoCore()
        {
            TileSet ts = TileSetGreybox();
            Assert.IsTrue(ts.Bake(out string report), report);

            for (int v = 0; v < ts.VariantCount; v++)
            {
                int rot = ts.GetRotation(v);
                Quaternion esperado = Quaternion.Euler(0f, 90f * rot, 0f);
                Assert.That(Quaternion.Angle(esperado, ts.GetRotationQuaternion(v)), Is.LessThan(1e-3f));
            }
        }

        // --------------------------------------------------------- integração

        [Test]
        public void TilesetGreybox_ResolveUmAndarComBordasSeladas()
        {
            TileSet ts = TileSetGreybox();
            Assert.IsTrue(ts.Bake(out string report), report);

            var grid = new Grid3D(12, 1, 12);
            AdjacencyTable table = ts.CreateAdjacencyTable();
            var solver = new WFCSolver(grid, table, ts.CreateWeights());

            // Selar: a face virada para fora do grid precisa ser `wall`.
            List<Constraint> bordas = SelarBordas(ts, grid);
            Assert.AreEqual(12 * 12 - 10 * 10, bordas.Count);
            solver.SetConstraints(bordas);

            // Várias seeds: um tileset que só funciona com sorte não serve.
            for (int seed = 0; seed < 15; seed++)
            {
                SolveResult r = solver.Solve(seed, SolverSettings.Default);
                Assert.AreEqual(SolveStatus.Success, r.Status, $"seed {seed}: {r.Message}");

                // Consistência global.
                for (int c = 0; c < grid.CellCount; c++)
                    foreach (Direction d in Directions.All)
                    {
                        if (!grid.TryGetNeighbor(c, d, out int nb)) continue;
                        Assert.IsTrue(table.IsAllowed(r.Modules[c], d, r.Modules[nb]),
                            $"seed {seed}: {ts.GetVariantName(r.Modules[c])} x {ts.GetVariantName(r.Modules[nb])} em {d}");
                    }

                // Bordas realmente seladas.
                for (int c = 0; c < grid.CellCount; c++)
                    foreach (Direction d in Directions.Horizontal)
                    {
                        if (!grid.IsBorder(c, d)) continue;
                        Assert.AreEqual("wall", ts.GetFace(r.Modules[c], d).socket,
                            $"seed {seed}: célula de borda {c} está aberta para fora em {d}");
                    }
            }
        }

        [Test]
        public void PortasNascemAosPares()
        {
            // Um vão de porta ocupa a borda entre DUAS células: cada uma entra com
            // metade. Se um `door` aparecer sozinho de um lado, a geometria fica
            // com meia porta — este teste garante que isso não acontece.
            TileSet ts = TileSetGreybox();
            Assert.IsTrue(ts.Bake(out string report), report);

            var grid = new Grid3D(10, 1, 10);
            AdjacencyTable table = ts.CreateAdjacencyTable();
            var solver = new WFCSolver(grid, table, ts.CreateWeights());

            // Com as bordas seladas, nenhuma porta pode apontar para fora do grid.
            solver.SetConstraints(SelarBordas(ts, grid));

            SolveResult r = solver.Solve(4242, SolverSettings.Default);
            Assert.AreEqual(SolveStatus.Success, r.Status, r.Message);

            for (int c = 0; c < grid.CellCount; c++)
                foreach (Direction d in Directions.Horizontal)
                {
                    if (ts.GetFace(r.Modules[c], d).socket != "door") continue;
                    Assert.IsTrue(grid.TryGetNeighbor(c, d, out int nb),
                        $"porta apontando para fora do grid na célula {c}, {d}");
                    Assert.AreEqual("door", ts.GetFace(r.Modules[nb], Directions.Opposite(d)).socket,
                        $"meia porta entre as células {c} e {nb}");
                }
        }
    }
}
