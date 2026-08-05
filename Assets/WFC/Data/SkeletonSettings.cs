// ============================================================================
//  SkeletonSettings.cs  —  camada WFC.Data
//
//  Parâmetros do SkeletonGenerator. Vive em Data (e não junto do gerador, em
//  Runtime) porque é DADO autorado: o FloorSpec precisa carregá-lo, e Data não
//  pode referenciar Runtime — a dependência é de mão única.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace WFC.Data
{
    /// <summary>
    /// Um papel possível para uma sala "normal" (fora Entrada/Escada, que são
    /// reatribuídas depois pelo SkeletonGenerator) e seu peso no sorteio.
    /// </summary>
    [Serializable]
    public sealed class RoomRoleWeight
    {
        public RoomRole role = RoomRole.Combate;

        [Tooltip("Peso relativo no sorteio. Maior = mais comum.")]
        [Min(0.01f)] public float weight = 1f;
    }

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

        [Header("Papéis das salas")]
        [Tooltip("Papéis possíveis para as salas 'normais' (sorteados por peso) e a chance relativa de " +
                 "cada um. NÃO inclua Entrada/Corredor/Escada aqui — esses são atribuídos à parte, depois " +
                 "do sorteio, e uma entrada acidental nesta lista vira uma sala comum com aquele papel, o " +
                 "que o WFCFiller pode rejeitar como contradição se a regra de papel exigir uma peça " +
                 "(ex.: Tile_Stairs) que não cabe numa sala aberta. Lista vazia = todas as salas viram " +
                 "Combate, igual ao comportamento antigo. Para novos arquétipos (ex.: Tesouro, Elite), " +
                 "acrescente o valor no enum RoomRole primeiro.")]
        public List<RoomRoleWeight> roomRoleWeights = new List<RoomRoleWeight>();

        public static SkeletonSettings Default => new SkeletonSettings();
    }
}
