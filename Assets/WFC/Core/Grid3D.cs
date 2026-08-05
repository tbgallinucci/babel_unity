// ============================================================================
//  Grid3D.cs  —  camada WFC.Core
//
//  Grid regular 3D com indexação linear e vizinhança de 6 direções.
//  O caso "2D" do plano (Fase 0) é simplesmente um grid com SizeY == 1: a mesma
//  máquina, sem código descartável. As direções PosY/NegY apenas nunca acham
//  vizinho quando a altura é 1.
//
//  Convenção de eixos = Unity: X para a direita, Y para cima, Z para frente.
// ============================================================================

using System;

namespace WFC.Core
{
    public sealed class Grid3D
    {
        public int SizeX { get; }
        public int SizeY { get; }
        public int SizeZ { get; }

        /// <summary>Total de células (SizeX * SizeY * SizeZ).</summary>
        public int CellCount { get; }

        public Grid3D(int sizeX, int sizeY, int sizeZ)
        {
            if (sizeX <= 0) throw new ArgumentOutOfRangeException(nameof(sizeX));
            if (sizeY <= 0) throw new ArgumentOutOfRangeException(nameof(sizeY));
            if (sizeZ <= 0) throw new ArgumentOutOfRangeException(nameof(sizeZ));

            SizeX = sizeX;
            SizeY = sizeY;
            SizeZ = sizeZ;
            CellCount = sizeX * sizeY * sizeZ;
        }

        /// <summary>
        /// Índice linear. Layout: X varia mais rápido, depois Z, depois Y (andares).
        /// Manter X contíguo ajuda a localidade de cache ao varrer linhas.
        /// </summary>
        public int Index(int x, int y, int z) => x + z * SizeX + y * SizeX * SizeZ;

        public void Coords(int index, out int x, out int y, out int z)
        {
            int layer = SizeX * SizeZ;
            y = index / layer;
            int rest = index - y * layer;
            z = rest / SizeX;
            x = rest - z * SizeX;
        }

        public bool InBounds(int x, int y, int z)
            => x >= 0 && x < SizeX && y >= 0 && y < SizeY && z >= 0 && z < SizeZ;

        /// <summary>
        /// Vizinho de <paramref name="index"/> na direção <paramref name="dir"/>.
        /// Devolve <c>false</c> se cair fora do grid (borda). Fora do grid NÃO é
        /// restrição: quem quiser selar a borda usa <see cref="ConstraintBuilder"/>.
        /// </summary>
        public bool TryGetNeighbor(int index, Direction dir, out int neighborIndex)
        {
            Coords(index, out int x, out int y, out int z);
            Directions.Offset(dir, out int dx, out int dy, out int dz);
            int nx = x + dx, ny = y + dy, nz = z + dz;

            if (!InBounds(nx, ny, nz))
            {
                neighborIndex = -1;
                return false;
            }

            neighborIndex = Index(nx, ny, nz);
            return true;
        }

        /// <summary>Está numa das faces externas do grid nessa direção?</summary>
        public bool IsBorder(int index, Direction dir)
            => !TryGetNeighbor(index, dir, out _);

        public override string ToString() => $"Grid3D({SizeX}x{SizeY}x{SizeZ} = {CellCount} células)";
    }
}
