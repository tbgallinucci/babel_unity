// ============================================================================
//  DirectionTests.cs
//
//  O teste mais importante do arquivo é RotateY_BateComQuaternionEulerDaUnity:
//  ele amarra a convenção de rotação do Core ao Quaternion.Euler(0, 90*rot, 0)
//  que a instanciação usa. Se essas duas convenções divergirem, o andar sai com
//  paredes viradas para o lado errado e o bug é quase impossível de rastrear
//  olhando só para os sockets.
// ============================================================================

using NUnit.Framework;
using UnityEngine;
using WFC.Core;

namespace WFC.Tests
{
    public class DirectionTests
    {
        [Test]
        public void Opposite_EhInvolucao()
        {
            foreach (Direction d in Directions.All)
                Assert.AreEqual(d, Directions.Opposite(Directions.Opposite(d)));
        }

        [Test]
        public void Opposite_InverteOOffset()
        {
            foreach (Direction d in Directions.All)
            {
                Directions.Offset(d, out int dx, out int dy, out int dz);
                Directions.Offset(Directions.Opposite(d), out int ox, out int oy, out int oz);
                Assert.AreEqual(-dx, ox);
                Assert.AreEqual(-dy, oy);
                Assert.AreEqual(-dz, oz);
            }
        }

        [Test]
        public void RotateY_QuatroQuartosVoltaAoInicio()
        {
            foreach (Direction d in Directions.All)
                Assert.AreEqual(d, Directions.RotateY(d, 4));
        }

        [Test]
        public void RotateY_IgnoraDirecoesVerticais()
        {
            for (int r = 0; r < 4; r++)
            {
                Assert.AreEqual(Direction.PosY, Directions.RotateY(Direction.PosY, r));
                Assert.AreEqual(Direction.NegY, Directions.RotateY(Direction.NegY, r));
            }
        }

        [Test]
        public void RotateY_AceitaQuartosNegativos()
        {
            foreach (Direction d in Directions.All)
            {
                Assert.AreEqual(Directions.RotateY(d, 3), Directions.RotateY(d, -1));
                Assert.AreEqual(Directions.RotateY(d, 2), Directions.RotateY(d, -2));
            }
        }

        /// <summary>
        /// A CONVENÇÃO. Rodar a direção pelo Core tem que dar exatamente o mesmo
        /// vetor que rodar o offset pelo Quaternion da Unity.
        /// </summary>
        [Test]
        public void RotateY_BateComQuaternionEulerDaUnity()
        {
            for (int rot = 0; rot < 4; rot++)
            {
                Quaternion q = Quaternion.Euler(0f, 90f * rot, 0f);

                foreach (Direction d in Directions.All)
                {
                    Directions.Offset(d, out int dx, out int dy, out int dz);
                    Vector3 rotated = q * new Vector3(dx, dy, dz);

                    Direction expected = Directions.RotateY(d, rot);
                    Directions.Offset(expected, out int ex, out int ey, out int ez);

                    Assert.That(rotated.x, Is.EqualTo(ex).Within(1e-4f), $"{d} rot {rot * 90}° eixo X");
                    Assert.That(rotated.y, Is.EqualTo(ey).Within(1e-4f), $"{d} rot {rot * 90}° eixo Y");
                    Assert.That(rotated.z, Is.EqualTo(ez).Within(1e-4f), $"{d} rot {rot * 90}° eixo Z");
                }
            }
        }

        [Test]
        public void Horizontal_TemAsQuatroDirecoesDoPlano()
        {
            Assert.AreEqual(4, Directions.Horizontal.Length);
            foreach (Direction d in Directions.Horizontal)
                Assert.IsTrue(Directions.IsHorizontal(d));
        }
    }
}
