// ============================================================================
//  SkeletonTests.cs  —  Fase 2
//
//  A promessa da Decisão 1 é forte: "andar sem saída é inaceitável". Um teste
//  que roda uma seed só não prova nada disso. Aqui a maioria dos testes varre
//  dezenas de seeds e falha se UMA delas quebrar a promessa.
//
//  O teste mais importante é TodaCelulaTemPecaCompativel: ele pega o esqueleto
//  real e pergunta, célula por célula, se o kit greybox tem alguma peça para
//  aquele formato de bordas. É esse teste que pega lacuna de tileset — a classe
//  de bug que só apareceria como "contradição" críptica em runtime.
// ============================================================================

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WFC.Core;
using WFC.Data;
using WFC.Runtime;

namespace WFC.Tests
{
    public class SkeletonTests
    {
        private GreyboxFixture _fx;

        [SetUp]
        public void SetUp() => _fx = new GreyboxFixture();

        [TearDown]
        public void TearDown() => _fx.Cleanup();

        private static SkeletonGenerator.Result Gerar(int seed, Grid3D grid = null, SkeletonSettings s = null)
        {
            grid = grid ?? new Grid3D(24, 1, 24);
            return new SkeletonGenerator().Generate(
                grid, 6f, 5f, Vector3.zero, s ?? new SkeletonSettings(), new XorShiftRandom(seed));
        }

        // ------------------------------------------------------------ estrutura

        [Test]
        public void Gera_SalasSemSobreposicao()
        {
            for (int seed = 0; seed < 25; seed++)
            {
                SkeletonGenerator.Result r = Gerar(seed);
                Assert.IsTrue(r.Success, $"seed {seed}: {r.Message}");
                Assert.GreaterOrEqual(r.Rooms.Count, 2, $"seed {seed}");

                for (int a = 0; a < r.Rooms.Count; a++)
                for (int b = a + 1; b < r.Rooms.Count; b++)
                    Assert.IsFalse(r.Rooms[a].Rect.Overlaps(r.Rooms[b].Rect),
                        $"seed {seed}: salas {a} e {b} se sobrepõem");
            }
        }

        [Test]
        public void Salas_RespeitamAMargemDaBorda()
        {
            var grid = new Grid3D(24, 1, 24);
            var s = new SkeletonSettings { borderPadding = 2 };

            for (int seed = 0; seed < 15; seed++)
            {
                SkeletonGenerator.Result r = Gerar(seed, grid, s);
                foreach (SkeletonGenerator.Room room in r.Rooms)
                {
                    Assert.GreaterOrEqual(room.Rect.xMin, 2, $"seed {seed}");
                    Assert.GreaterOrEqual(room.Rect.yMin, 2, $"seed {seed}");
                    Assert.LessOrEqual(room.Rect.xMax, grid.SizeX - 2, $"seed {seed}");
                    Assert.LessOrEqual(room.Rect.yMax, grid.SizeZ - 2, $"seed {seed}");
                }
            }
        }

        [Test]
        public void BordaDoGrid_NuncaEhJogavel()
        {
            var grid = new Grid3D(20, 1, 20);
            for (int seed = 0; seed < 15; seed++)
            {
                AnnotatedGrid ag = Gerar(seed, grid).Grid;
                for (int cell = 0; cell < grid.CellCount; cell++)
                {
                    grid.Coords(cell, out int x, out _, out int z);
                    bool naBorda = x == 0 || z == 0 || x == grid.SizeX - 1 || z == grid.SizeZ - 1;
                    if (naBorda)
                        Assert.IsFalse(ag.IsPlayable(cell), $"seed {seed}: célula de borda {cell} é jogável");
                }
            }
        }

        // -------------------------------------------------------- conectividade

        [Test]
        public void SempreExisteCaminhoDaEntradaAteAEscada()
        {
            // A promessa central do plugin. Se este teste ficar vermelho, para tudo.
            for (int seed = 0; seed < 60; seed++)
            {
                SkeletonGenerator.Result r = Gerar(seed);
                Assert.IsTrue(r.Success, $"seed {seed}: {r.Message}");
                Assert.IsTrue(ConnectivityValidator.IsReachable(r.Grid, r.EntranceCell, r.StairsCell),
                    $"seed {seed}: ANDAR SEM SAÍDA (entrada {r.EntranceCell} → escada {r.StairsCell})");
            }
        }

        [Test]
        public void NenhumaCelulaJogavelFicaIlhada()
        {
            for (int seed = 0; seed < 40; seed++)
            {
                SkeletonGenerator.Result r = Gerar(seed);
                Assert.IsTrue(ConnectivityValidator.AllPlayableReachable(r.Grid, r.EntranceCell, out List<int> orfas),
                    $"seed {seed}: {orfas.Count} célula(s) fora do alcance da entrada");
            }
        }

        [Test]
        public void TodaSala_EhAlcancavelDaEntrada()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                SkeletonGenerator.Result r = Gerar(seed);
                int[] dist = ConnectivityValidator.Distances(r.Grid, r.EntranceCell);
                foreach (SkeletonGenerator.Room room in r.Rooms)
                    Assert.GreaterOrEqual(dist[room.CenterCell], 0,
                        $"seed {seed}: sala em {room.Rect} não é alcançável");
            }
        }

        [Test]
        public void SemCiclos_OAndarContinuaConexo()
        {
            // extraLoopChance = 0 deixa só a MST. Continua conexa por definição.
            var s = new SkeletonSettings { extraLoopChance = 0f };
            for (int seed = 0; seed < 25; seed++)
            {
                SkeletonGenerator.Result r = Gerar(seed, null, s);
                Assert.IsTrue(ConnectivityValidator.IsReachable(r.Grid, r.EntranceCell, r.StairsCell), $"seed {seed}");
            }
        }

        // ------------------------------------------------------------- bordas

        [Test]
        public void RotuloDeBorda_EhSimetrico()
        {
            // Se a borda tivesse rótulo diferente de cada lado, o filler pediria sockets
            // incompatíveis nas duas células e o solver contradiria sem explicação.
            var grid = new Grid3D(20, 1, 20);
            for (int seed = 0; seed < 20; seed++)
            {
                AnnotatedGrid ag = Gerar(seed, grid).Grid;
                for (int cell = 0; cell < grid.CellCount; cell++)
                    foreach (Direction dir in Directions.Horizontal)
                    {
                        if (!grid.TryGetNeighbor(cell, dir, out int nb)) continue;
                        Assert.AreEqual(ag.GetBorder(cell, dir), ag.GetBorder(nb, Directions.Opposite(dir)),
                            $"seed {seed}: borda {cell}↔{nb} em {dir}");
                    }
            }
        }

        [Test]
        public void CelulaNaoJogavel_TemTodasAsBordasFechadas()
        {
            var grid = new Grid3D(20, 1, 20);
            for (int seed = 0; seed < 15; seed++)
            {
                AnnotatedGrid ag = Gerar(seed, grid).Grid;
                for (int cell = 0; cell < grid.CellCount; cell++)
                {
                    if (ag.IsPlayable(cell)) continue;
                    Assert.AreEqual(4, ag.CountBorders(cell, BorderLabel.Wall),
                        $"seed {seed}: célula vazia {cell} tem borda aberta");
                }
            }
        }

        [Test]
        public void ReconcileDoors_DeixaSoPortasQueOKitRepresenta()
        {
            // O esqueleto crava porta em toda junção sem olhar o kit (é agnóstico).
            // Quem apara é o filler, contra o tileset REAL. Depois dele, nenhuma porta
            // pode ter sobrado num formato sem peça.
            TileSet ts = _fx.Baked();
            var grid = new Grid3D(24, 1, 24);
            int n = ts.VariantCount;
            int words = Bitset.WordCount(n);
            var cache = new Dictionary<(Direction, string), ulong[]>();

            for (int seed = 0; seed < 40; seed++)
            {
                AnnotatedGrid ag = Gerar(seed, grid).Grid;
                WFCFiller.ReconcileDoors(ag, ts, cache);

                for (int cell = 0; cell < grid.CellCount; cell++)
                {
                    if (ag.CountBorders(cell, BorderLabel.Door) == 0) continue;

                    ulong[] mask = ConstraintBuilder.FullMask(n);
                    foreach (Direction dir in Directions.Horizontal)
                    {
                        string socket = BorderLabels.ToSocket(ag.GetBorder(cell, dir));
                        if (!cache.TryGetValue((dir, socket), out ulong[] m))
                        {
                            m = ts.MaskWithFaceSocket(dir, socket);
                            cache[(dir, socket)] = m;
                        }
                        Bitset.AndInPlace(mask, 0, m, 0, words);
                    }

                    Assert.IsFalse(Bitset.IsEmpty(mask, 0, words),
                        $"seed {seed}: sobrou porta sem peça na célula {cell} ({Perfil(ag, cell)})");
                }
            }
        }

        [Test]
        public void ReconcileDoors_NaoQuebraAConectividade()
        {
            // Rebaixar porta → vão não pode cortar caminho: vão conecta igual.
            TileSet ts = _fx.Baked();
            var grid = new Grid3D(24, 1, 24);

            for (int seed = 0; seed < 30; seed++)
            {
                SkeletonGenerator.Result r = Gerar(seed, grid);
                int antes = ConnectivityValidator.ReachableCount(r.Grid, r.EntranceCell);

                WFCFiller.ReconcileDoors(r.Grid, ts);

                int depois = ConnectivityValidator.ReachableCount(r.Grid, r.EntranceCell);
                Assert.AreEqual(antes, depois, $"seed {seed}: a reconciliação mudou o alcance");
                Assert.IsTrue(ConnectivityValidator.IsReachable(r.Grid, r.EntranceCell, r.StairsCell),
                    $"seed {seed}: perdeu o caminho até a escada");
            }
        }

        [Test]
        public void ReconcileDoors_FuncionaAteComTilesetSemPortaDeCorredor()
        {
            // Simula exatamente o caso que quebrou na prática: tileset antigo, sem a peça
            // de porta em corredor. Nada pode explodir — as portas de corredor viram vão
            // e o andar continua fechando.
            TileSet ts = _fx.TileSet();
            ts.modules.RemoveAll(m => m != null && m.name == "Door_Corridor");
            Assert.IsTrue(ts.Bake(out string report), report);

            var grid = new Grid3D(24, 1, 24);
            int n = ts.VariantCount;
            int words = Bitset.WordCount(n);
            AdjacencyTable table = ts.CreateAdjacencyTable();

            for (int seed = 0; seed < 15; seed++)
            {
                SkeletonGenerator.Result r = Gerar(seed, grid);
                WFCFiller.ReconcileDoors(r.Grid, ts);

                Assert.IsTrue(ConnectivityValidator.IsReachable(r.Grid, r.EntranceCell, r.StairsCell),
                    $"seed {seed}: perdeu o caminho");

                var constraints = new List<Constraint>(grid.CellCount);
                var cache = new Dictionary<(Direction, string), ulong[]>();
                for (int cell = 0; cell < grid.CellCount; cell++)
                {
                    ulong[] mask = ConstraintBuilder.FullMask(n);
                    foreach (Direction dir in Directions.Horizontal)
                    {
                        string socket = BorderLabels.ToSocket(r.Grid.GetBorder(cell, dir));
                        if (!cache.TryGetValue((dir, socket), out ulong[] m))
                        {
                            m = ts.MaskWithFaceSocket(dir, socket);
                            cache[(dir, socket)] = m;
                        }
                        Bitset.AndInPlace(mask, 0, m, 0, words);
                    }
                    Assert.IsFalse(Bitset.IsEmpty(mask, 0, words),
                        $"seed {seed}: célula {cell} sem peça mesmo depois da reconciliação " +
                        $"({Perfil(r.Grid, cell)})");
                    constraints.Add(new Constraint(cell, mask));
                }

                var solver = new WFCSolver(grid, table, ts.CreateWeights());
                solver.SetConstraints(constraints);
                Assert.AreEqual(SolveStatus.Success, solver.Solve(seed, SolverSettings.Default).Status,
                    $"seed {seed}");
            }
        }

        [Test]
        public void AlcovaDaEscada_TemFormatoDeNicho()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                SkeletonGenerator.Result r = Gerar(seed);
                if (r.Grid.GetRole(r.StairsCell) != RoomRole.Escada) continue; // caiu no fallback sem alcova

                Assert.AreEqual(3, r.Grid.CountBorders(r.StairsCell, BorderLabel.Wall),
                    $"seed {seed}: alcova da escada não é nicho de 3 paredes");
                Assert.AreEqual(1, r.Grid.CountBorders(r.StairsCell, BorderLabel.Open),
                    $"seed {seed}: alcova da escada não tem exatamente um lado aberto");
            }
        }

        // ---------------------------------------------- integração com o tileset

        [Test]
        public void TodaCelulaTemPecaCompativel()
        {
            // Pega o esqueleto de verdade e pergunta ao tileset se ele consegue
            // representar cada formato de célula. É o teste que pega LACUNA DE KIT.
            TileSet ts = _fx.Baked();
            Assert.AreEqual(GreyboxFixture.ExpectedVariantCount, ts.VariantCount);

            int n = ts.VariantCount;
            int words = Bitset.WordCount(n);
            var cache = new Dictionary<(Direction, string), ulong[]>();
            var grid = new Grid3D(24, 1, 24);

            for (int seed = 0; seed < 30; seed++)
            {
                AnnotatedGrid ag = Gerar(seed, grid).Grid;

                for (int cell = 0; cell < grid.CellCount; cell++)
                {
                    ulong[] mask = ConstraintBuilder.FullMask(n);
                    foreach (Direction dir in Directions.Horizontal)
                    {
                        string socket = BorderLabels.ToSocket(ag.GetBorder(cell, dir));
                        if (!cache.TryGetValue((dir, socket), out ulong[] m))
                        {
                            m = ts.MaskWithFaceSocket(dir, socket);
                            cache[(dir, socket)] = m;
                        }
                        Bitset.AndInPlace(mask, 0, m, 0, words);
                    }

                    Assert.IsFalse(Bitset.IsEmpty(mask, 0, words),
                        $"seed {seed}: nenhuma peça serve para a célula {cell} " +
                        $"(papel {ag.GetRole(cell)}, bordas {Perfil(ag, cell)})");
                }
            }
        }

        [Test]
        public void RegraDePapel_NuncaBrigaComOFormatoDaCelula()
        {
            TileSet ts = _fx.Baked();
            FloorSpec spec = _fx.Spec(ts, new Vector3Int(24, 1, 24));

            int n = ts.VariantCount;
            int words = Bitset.WordCount(n);
            var cache = new Dictionary<(Direction, string), ulong[]>();
            var grid = new Grid3D(24, 1, 24);

            for (int seed = 0; seed < 25; seed++)
            {
                AnnotatedGrid ag = Gerar(seed, grid).Grid;

                for (int cell = 0; cell < grid.CellCount; cell++)
                {
                    ulong[] mask = ConstraintBuilder.FullMask(n);
                    foreach (Direction dir in Directions.Horizontal)
                    {
                        string socket = BorderLabels.ToSocket(ag.GetBorder(cell, dir));
                        if (!cache.TryGetValue((dir, socket), out ulong[] m))
                        {
                            m = ts.MaskWithFaceSocket(dir, socket);
                            cache[(dir, socket)] = m;
                        }
                        Bitset.AndInPlace(mask, 0, m, 0, words);
                    }

                    ulong[] roleMask = spec.MaskForRole(ag.GetRole(cell));
                    if (roleMask == null) continue;

                    Bitset.AndInPlace(mask, 0, roleMask, 0, words);
                    Assert.IsFalse(Bitset.IsEmpty(mask, 0, words),
                        $"seed {seed}: regra do papel {ag.GetRole(cell)} briga com o formato da " +
                        $"célula {cell} ({Perfil(ag, cell)})");
                }
            }
        }

        [Test]
        public void PipelineCompleto_ResolveSemContradicao()
        {
            TileSet ts = _fx.Baked();
            var grid = new Grid3D(24, 1, 24);
            AdjacencyTable table = ts.CreateAdjacencyTable();

            int n = ts.VariantCount;
            int words = Bitset.WordCount(n);
            var cache = new Dictionary<(Direction, string), ulong[]>();

            for (int seed = 0; seed < 20; seed++)
            {
                var rng = new XorShiftRandom(seed);
                SkeletonGenerator.Result skel = new SkeletonGenerator().Generate(
                    grid, 6f, 5f, Vector3.zero, new SkeletonSettings(), rng);
                Assert.IsTrue(skel.Success, $"seed {seed}: {skel.Message}");

                var constraints = new List<Constraint>(grid.CellCount);
                for (int cell = 0; cell < grid.CellCount; cell++)
                {
                    ulong[] mask = ConstraintBuilder.FullMask(n);
                    foreach (Direction dir in Directions.Horizontal)
                    {
                        string socket = BorderLabels.ToSocket(skel.Grid.GetBorder(cell, dir));
                        if (!cache.TryGetValue((dir, socket), out ulong[] m))
                        {
                            m = ts.MaskWithFaceSocket(dir, socket);
                            cache[(dir, socket)] = m;
                        }
                        Bitset.AndInPlace(mask, 0, m, 0, words);
                    }
                    constraints.Add(new Constraint(cell, mask));
                }

                var solver = new WFCSolver(grid, table, ts.CreateWeights());
                solver.SetConstraints(constraints);
                SolveResult r = solver.Solve(seed, SolverSettings.Default);

                Assert.AreEqual(SolveStatus.Success, r.Status, $"seed {seed}: {r.Message}");

                for (int c = 0; c < grid.CellCount; c++)
                    foreach (Direction d in Directions.All)
                    {
                        if (!grid.TryGetNeighbor(c, d, out int nb)) continue;
                        Assert.IsTrue(table.IsAllowed(r.Modules[c], d, r.Modules[nb]),
                            $"seed {seed}: par proibido entre {c} e {nb}");
                    }
            }
        }

        // ----------------------------------------------------- reprodutibilidade

        [Test]
        public void MesmaSeed_ProduzOMesmoEsqueleto()
        {
            var grid = new Grid3D(24, 1, 24);
            SkeletonGenerator.Result a = Gerar(4242, grid);
            SkeletonGenerator.Result b = Gerar(4242, grid);

            Assert.AreEqual(a.Rooms.Count, b.Rooms.Count);
            Assert.AreEqual(a.EntranceCell, b.EntranceCell);
            Assert.AreEqual(a.StairsCell, b.StairsCell);
            Assert.AreEqual(a.DoorCount, b.DoorCount);

            for (int cell = 0; cell < grid.CellCount; cell++)
            {
                Assert.AreEqual(a.Grid.GetRole(cell), b.Grid.GetRole(cell), $"papel da célula {cell}");
                Assert.AreEqual(a.Grid.GetRegion(cell), b.Grid.GetRegion(cell), $"região da célula {cell}");
                foreach (Direction d in Directions.Horizontal)
                    Assert.AreEqual(a.Grid.GetBorder(cell, d), b.Grid.GetBorder(cell, d), $"borda {cell}/{d}");
            }
        }

        [Test]
        public void SeedsDiferentes_ProduzemAndaresDiferentes()
        {
            SkeletonGenerator.Result a = Gerar(1);
            SkeletonGenerator.Result b = Gerar(2);

            bool algumaDiferenca = a.Rooms.Count != b.Rooms.Count;
            for (int i = 0; i < Mathf.Min(a.Rooms.Count, b.Rooms.Count) && !algumaDiferenca; i++)
                if (!a.Rooms[i].Rect.Equals(b.Rooms[i].Rect)) algumaDiferenca = true;

            Assert.IsTrue(algumaDiferenca, "duas seeds diferentes produziram o mesmo layout");
        }

        [Test]
        public void GridPequenoDemais_FalhaComMensagemUtil()
        {
            // 6x6 com salas de 3..6 e margem não cabe duas salas. Tem que falhar
            // explicando o que ajustar, não estourar exceção nem devolver andar quebrado.
            SkeletonGenerator.Result r = Gerar(1, new Grid3D(6, 1, 6));

            Assert.IsFalse(r.Success);
            Assert.IsNotNull(r.Message);
            StringAssert.Contains("sala", r.Message.ToLowerInvariant());
        }

        // ------------------------------------------------------------- ajuda
        private static string Perfil(AnnotatedGrid ag, int cell)
        {
            var sb = new System.Text.StringBuilder();
            foreach (Direction d in Directions.Horizontal) sb.Append($"{d}={ag.GetBorder(cell, d)} ");
            return sb.ToString().TrimEnd();
        }
    }
}
