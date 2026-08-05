// ============================================================================
//  FloorFillResult.cs  —  camada WFC.Runtime
//
//  O que um IFloorFiller devolve. Junto com o mapa de papéis do AnnotatedGrid,
//  é a base do CONTRATO DE SAÍDA que o populador externo consome (Decisão 4):
//  o plugin entrega casco geométrico + papéis + anchors, e a responsabilidade
//  dele termina aí.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace WFC.Runtime
{
    public sealed class FloorFillResult
    {
        public bool Success;

        /// <summary>Raiz da hierarquia instanciada do andar.</summary>
        public Transform Root;

        /// <summary>
        /// Variante escolhida por célula (índice linear do Grid3D), ou -1 se a célula
        /// ficou vazia. Índices são do TileSet — use TileSet.GetPrefab/GetRotation.
        /// </summary>
        public int[] Variants;

        /// <summary>
        /// Pontos de posicionamento coletados dos prefabs instanciados. O plugin apenas
        /// COLETA e EXPÕE; quem decide o que nasce em cada um é o populador.
        /// </summary>
        public List<SpawnAnchor> Anchors = new List<SpawnAnchor>();

        /// <summary>Seed efetivamente usada — guarde para reproduzir o andar.</summary>
        public int Seed;

        /// <summary>Quantas tentativas o solver gastou.</summary>
        public int Attempts;

        /// <summary>Milissegundos gastos só no solver (sem instanciação).</summary>
        public double SolveMilliseconds;

        /// <summary>Milissegundos gastos instanciando prefabs.</summary>
        public double InstantiateMilliseconds;

        public string Message;

        public override string ToString()
            => $"{(Success ? "OK" : "FALHOU")} | seed {Seed} | {Attempts} tentativa(s) | " +
               $"solve {SolveMilliseconds:F1} ms | instanciar {InstantiateMilliseconds:F1} ms" +
               (string.IsNullOrEmpty(Message) ? "" : $" | {Message}");
    }
}
