// ============================================================================
//  Grid3DTests.cs
// ============================================================================

using NUnit.Framework;
using WFC.Core;

namespace WFC.Tests
{
    public class Grid3DTests
    {
        [Test]
        public void IndexECoords_SaoInversos()
        {
            var grid = new Grid3D(5, 3, 7);
            Assert.AreEqual(5 * 3 * 7, grid.CellCount);

            for (int i = 0; i < grid.CellCount; i++)
            {
                grid.Coords(i, out int x, out int y, out int z);
                Assert.AreEqual(i, grid.Index(x, y, z), $"índice {i} → ({x},{y},{z})");
            }
        }

        [Test]
        public void TryGetNeighbor_AtravessaOGridCorretamente()
        {
            var grid = new Grid3D(3, 1, 3);
            int center = grid.Index(1, 0, 1);

            Assert.IsTrue(grid.TryGetNeighbor(center, Direction.PosX, out int px));
            Assert.AreEqual(grid.Index(2, 0, 1), px);

            Assert.IsTrue(grid.TryGetNeighbor(center, Direction.NegZ, out int nz));
            Assert.AreEqual(grid.Index(1, 0, 0), nz);
        }

        [Test]
        public void TryGetNeighbor_FalhaNaBorda()
        {
            var grid = new Grid3D(3, 1, 3);
            int corner = grid.Index(0, 0, 0);

            Assert.IsFalse(grid.TryGetNeighbor(corner, Direction.NegX, out _));
            Assert.IsFalse(grid.TryGetNeighbor(corner, Direction.NegZ, out _));
            Assert.IsTrue(grid.TryGetNeighbor(corner, Direction.PosX, out _));
        }

        [Test]
        public void GridDeAlturaUm_NaoTemVizinhosVerticais()
        {
            // É esse o caso da "prova em 2D" da Fase 0: o mesmo solver de 6 direções,
            // com o eixo Y simplesmente sem vizinho nenhum.
            var grid = new Grid3D(4, 1, 4);
            for (int c = 0; c < grid.CellCount; c++)
            {
                Assert.IsFalse(grid.TryGetNeighbor(c, Direction.PosY, out _));
                Assert.IsFalse(grid.TryGetNeighbor(c, Direction.NegY, out _));
            }
        }

        [Test]
        public void IsBorder_ConcordaComTryGetNeighbor()
        {
            var grid = new Grid3D(4, 2, 3);
            for (int c = 0; c < grid.CellCount; c++)
                foreach (Direction d in Directions.All)
                    Assert.AreEqual(!grid.TryGetNeighbor(c, d, out _), grid.IsBorder(c, d));
        }

        [Test]
        public void DimensaoZero_EhRejeitada()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new Grid3D(0, 1, 1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new Grid3D(1, 1, -3));
        }
    }
}
