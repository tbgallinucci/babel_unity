// ============================================================================
//  GeneratedFloor.cs  —  camada WFC.Runtime  (Decisão 4: CONTRATO DE SAÍDA)
//
//  Tudo que o plugin entrega e a partir daí lava as mãos:
//
//    1. mapa de papéis por célula      (via Grid)
//    2. spawn anchors coletados        (via Anchors)
//    3. raiz do andar + NavMeshSurface
//
//  O plugin NÃO sabe o que é inimigo, baú ou jogador. Quem consome isto é o
//  código do jogo — e a dependência é de mão única: o jogo conhece o plugin, o
//  plugin nunca referencia o jogo.
//
//  Os helpers no fim (CellsWithRole, IsOpenFloorCell, ...) existem para o
//  consumidor não ter que reimplementar varredura de grid — continuam sendo
//  consulta pura, sem opinião sobre o que colocar onde.
// ============================================================================

using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using WFC.Core;
using WFC.Data;

namespace WFC.Runtime
{
    public sealed class GeneratedFloor
    {
        public bool Success;
        public string Message;

        /// <summary>Número do andar na torre. O plugin só repassa; quem conta é o jogo.</summary>
        public int FloorNumber;

        /// <summary>Seed usada. Guarde para reproduzir o andar exato.</summary>
        public int Seed;

        public Transform Root;
        public AnnotatedGrid Grid;
        public SkeletonGenerator.Result Skeleton;
        public TileSet TileSet;

        /// <summary>Variante escolhida por célula (índice do TileSet), -1 se vazia.</summary>
        public int[] Variants;

        public IReadOnlyList<SpawnAnchor> Anchors = new List<SpawnAnchor>();
        public NavMeshSurface NavMesh;

        public Vector3 EntranceWorld;
        public Vector3 StairsWorld;

        public double SkeletonMilliseconds;
        public double SolveMilliseconds;
        public double InstantiateMilliseconds;
        public double NavMeshMilliseconds;

        public double TotalMilliseconds
            => SkeletonMilliseconds + SolveMilliseconds + InstantiateMilliseconds + NavMeshMilliseconds;

        // ------------------------------------------------------------ consultas
        public IEnumerable<int> CellsWithRole(RoomRole role)
        {
            if (Grid == null) yield break;
            for (int cell = 0; cell < Grid.Grid.CellCount; cell++)
                if (Grid.GetRole(cell) == role) yield return cell;
        }

        /// <summary>
        /// Célula de piso totalmente aberta (as 4 bordas livres). É onde dá para pôr
        /// coisa sem encostar em parede — bom candidato a spawn.
        /// </summary>
        public bool IsOpenFloorCell(int cell)
            => Grid != null
            && Grid.IsPlayable(cell)
            && Grid.CountBorders(cell, BorderLabel.Wall) == 0;

        public Vector3 CellToWorld(int cell) => Grid.CellToWorld(cell);

        /// <summary>Salas do esqueleto, com retângulo, região e papel. Vazio se falhou.</summary>
        public IReadOnlyList<SkeletonGenerator.Room> Rooms
            => Skeleton != null ? (IReadOnlyList<SkeletonGenerator.Room>)Skeleton.Rooms
                                : System.Array.Empty<SkeletonGenerator.Room>();

        public override string ToString()
            => Success
                ? $"Andar {FloorNumber} | seed {Seed} | {Rooms.Count} salas | " +
                  $"esqueleto {SkeletonMilliseconds:F1} + solve {SolveMilliseconds:F1} + " +
                  $"instanciar {InstantiateMilliseconds:F1} + navmesh {NavMeshMilliseconds:F1} = {TotalMilliseconds:F1} ms"
                : $"Andar {FloorNumber} FALHOU: {Message}";
    }
}
