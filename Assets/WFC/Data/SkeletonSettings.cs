// ============================================================================
//  SkeletonSettings.cs  —  camada WFC.Data
//
//  Parâmetros do SkeletonGenerator. Vive em Data (e não junto do gerador, em
//  Runtime) porque é DADO autorado: o FloorSpec precisa carregá-lo, e Data não
//  pode referenciar Runtime — a dependência é de mão única.
// ============================================================================

using System;
using UnityEngine;

namespace WFC.Data
{
    [Serializable]
    public sealed class SkeletonSettings
    {
        [Tooltip("Quantas salas tentar colocar. Se não couberem todas, usa as que couberam.")]
        [Min(2)] public int targetRooms = 8;

        [Tooltip("Largura da sala em CÉLULAS (mín, máx). Inclui as células de parede.")]
        public Vector2Int roomWidth = new Vector2Int(3, 6);

        [Tooltip("Profundidade da sala em CÉLULAS (mín, máx).")]
        public Vector2Int roomDepth = new Vector2Int(3, 6);

        [Tooltip("Células vazias mínimas entre duas salas. Zero deixa salas encostadas.")]
        [Min(0)] public int roomMargin = 1;

        [Tooltip("Margem contra a borda do grid. Precisa ser >= 1: a borda do andar tem que ser parede.")]
        [Min(1)] public int borderPadding = 1;

        [Tooltip("Tentativas de posicionamento por sala antes de desistir dela.")]
        [Min(1)] public int placementAttempts = 60;

        [Tooltip("Chance de acrescentar cada aresta extra além da MST, criando ciclos no andar. " +
                 "Zero deixa o andar como árvore pura: só ida e volta pelo mesmo caminho.")]
        [Range(0f, 1f)] public float extraLoopChance = 0.15f;

        [Tooltip("Cravar portas nas junções. Desligado, as junções viram vãos simples.")]
        public bool useDoors = true;

        public static SkeletonSettings Default => new SkeletonSettings();
    }
}
