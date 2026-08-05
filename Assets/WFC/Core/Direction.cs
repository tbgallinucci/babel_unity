// ============================================================================
//  Direction.cs  —  camada WFC.Core (C# puro, zero UnityEngine)
//
//  As 6 direções de vizinhança do grid 3D e os utilitários de rotação em Y.
//  Este arquivo é a peça mais "pegadinha" do plugin inteiro: se a convenção de
//  rotação aqui não bater com o Quaternion.Euler(0, 90*rot, 0) usado na hora de
//  instanciar, o andar sai desalinhado e o bug é dificílimo de enxergar.
//  Por isso a convenção está derivada explicitamente nos comentários abaixo.
// ============================================================================

namespace WFC.Core
{
    /// <summary>
    /// As 6 faces / direções de vizinhança de uma célula.
    /// A ordem dos valores é usada como índice em arrays — não reordenar.
    /// </summary>
    public enum Direction
    {
        PosX = 0,
        NegX = 1,
        PosY = 2,
        NegY = 3,
        PosZ = 4,
        NegZ = 5,
    }

    public static class Directions
    {
        /// <summary>Quantidade de direções. Usado para dimensionar arrays.</summary>
        public const int Count = 6;

        public static readonly Direction[] All =
        {
            Direction.PosX, Direction.NegX,
            Direction.PosY, Direction.NegY,
            Direction.PosZ, Direction.NegZ,
        };

        /// <summary>Apenas as 4 direções horizontais (as que a rotação em Y permuta).</summary>
        public static readonly Direction[] Horizontal =
        {
            Direction.PosX, Direction.NegZ, Direction.NegX, Direction.PosZ,
        };

        // ------------------------------------------------------------------
        //  ORDEM DO CICLO DE ROTAÇÃO EM Y — derivação
        //
        //  A Unity é canhota (left-handed) com Y para cima. Rodar um vetor por
        //  Quaternion.Euler(0, θ, 0) mapeia:
        //        x' =  x·cosθ + z·senθ
        //        z' = -x·senθ + z·cosθ
        //
        //  Com θ = +90°:   x' = z ,  z' = -x
        //        PosX (1,0,0) → (0,0,-1) = NegZ
        //        NegZ (0,0,-1) → (-1,0,0) = NegX
        //        NegX (-1,0,0) → (0,0,1)  = PosZ
        //        PosZ (0,0,1)  → (1,0,0)  = PosX
        //
        //  Logo o ciclo de UM quarto de volta positivo é:
        //        PosX → NegZ → NegX → PosZ → PosX
        //
        //  (Atenção: o documento de plano cita "PosX→PosZ→NegX→NegZ", que é o
        //   ciclo da rotação NEGATIVA. Usamos o positivo porque é o que casa com
        //   Quaternion.Euler(0, +90*rot, 0) na instanciação.)
        // ------------------------------------------------------------------
        private static readonly Direction[] YCycle =
        {
            Direction.PosX, Direction.NegZ, Direction.NegX, Direction.PosZ,
        };

        /// <summary>Índice da direção horizontal dentro de <see cref="YCycle"/>, ou -1 se for vertical.</summary>
        private static int YCycleIndex(Direction d)
        {
            switch (d)
            {
                case Direction.PosX: return 0;
                case Direction.NegZ: return 1;
                case Direction.NegX: return 2;
                case Direction.PosZ: return 3;
                default: return -1;
            }
        }

        /// <summary>A direção oposta (a face que encosta na nossa, do lado do vizinho).</summary>
        public static Direction Opposite(Direction d)
        {
            switch (d)
            {
                case Direction.PosX: return Direction.NegX;
                case Direction.NegX: return Direction.PosX;
                case Direction.PosY: return Direction.NegY;
                case Direction.NegY: return Direction.PosY;
                case Direction.PosZ: return Direction.NegZ;
                default:             return Direction.PosZ;
            }
        }

        public static bool IsHorizontal(Direction d)
            => d != Direction.PosY && d != Direction.NegY;

        public static bool IsVertical(Direction d)
            => d == Direction.PosY || d == Direction.NegY;

        /// <summary>Deslocamento inteiro no grid correspondente à direção.</summary>
        public static void Offset(Direction d, out int dx, out int dy, out int dz)
        {
            dx = 0; dy = 0; dz = 0;
            switch (d)
            {
                case Direction.PosX: dx =  1; break;
                case Direction.NegX: dx = -1; break;
                case Direction.PosY: dy =  1; break;
                case Direction.NegY: dy = -1; break;
                case Direction.PosZ: dz =  1; break;
                case Direction.NegZ: dz = -1; break;
            }
        }

        /// <summary>
        /// Roda uma direção por <paramref name="quarterTurns"/> múltiplos de 90° em torno de Y
        /// (positivo = mesmo sentido de Quaternion.Euler(0, +90, 0)).
        /// Direções verticais são invariantes.
        /// </summary>
        public static Direction RotateY(Direction d, int quarterTurns)
        {
            if (IsVertical(d)) return d;
            int i = YCycleIndex(d);
            int r = ((quarterTurns % 4) + 4) % 4; // normaliza negativos
            return YCycle[(i + r) & 3];
        }
    }
}
