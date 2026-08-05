// ============================================================================
//  AnnotatedGrid.cs  —  camada WFC.Runtime
//
//  O CONTRATO entre o esqueleto e o preenchimento (Decisão 1):
//      esqueleto → grid anotado (papéis + portas obrigatórias) → IFloorFiller
//
//  O esqueleto NÃO desenha paredes. Ele só carimba, para cada célula, qual é o
//  papel dela, e marca em quais bordas tem que existir passagem. O filler
//  (WFCFiller na Fase 2) resolve a geometria respeitando isso — nunca pode
//  cortar um caminho que o esqueleto prometeu.
//
//  Na Fase 0/1 o grid vem todo com papel Corredor e sem portas: é só a máquina
//  do WFC solta, sem esqueleto. A estrutura já existe para a Fase 2 encaixar.
// ============================================================================

using System;
using UnityEngine;
using WFC.Core;
using WFC.Data;

namespace WFC.Runtime
{
    public sealed class AnnotatedGrid
    {
        public Grid3D Grid { get; }

        /// <summary>Footprint da célula no plano XZ, em metros (= tamanho do módulo do kit).</summary>
        public float CellSize { get; }

        /// <summary>
        /// Altura de um andar de células, em metros. NÃO é igual ao CellSize: no greybox
        /// deste projeto o footprint é 6 e a altura de parede é 5. Só importa quando
        /// SizeY &gt; 1; com um andar plano nada muda.
        /// </summary>
        public float CellHeight { get; }

        /// <summary>Canto do grid em coordenadas de mundo: a célula (0,0,0) fica centrada em Origin + (CellSize/2, 0, CellSize/2).</summary>
        public Vector3 Origin { get; }

        private readonly RoomRole[] _roles;
        private readonly Junction[] _junctions; // [célula * 6 + direção]
        private readonly int[] _regions;        // -1 = fora de qualquer região

        /// <summary>Id de região das células de corredor. Toda a malha de corredores é UMA região.</summary>
        public const int CorridorRegion = 0;

        /// <summary>Primeiro id livre para salas (corredores ficam com o 0).</summary>
        public const int FirstRoomRegion = 1;

        public AnnotatedGrid(Grid3D grid, float cellSize, float cellHeight, Vector3 origin = default)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            CellSize = cellSize;
            CellHeight = cellHeight > 0f ? cellHeight : cellSize;
            Origin = origin;

            _roles = new RoomRole[grid.CellCount];
            _junctions = new Junction[grid.CellCount * Directions.Count];
            _regions = new int[grid.CellCount];
            for (int i = 0; i < _regions.Length; i++) _regions[i] = -1;
        }

        // --------------------------------------------------------------- papéis
        public RoomRole GetRole(int cell) => _roles[cell];

        public void SetRole(int cell, RoomRole role) => _roles[cell] = role;

        /// <summary>Carimba o mesmo papel num bloco de células (a escala grossa da Decisão 6).</summary>
        public void SetRoleBlock(int minX, int minY, int minZ, int maxX, int maxY, int maxZ, RoomRole role)
        {
            for (int y = Mathf.Max(0, minY); y <= Mathf.Min(Grid.SizeY - 1, maxY); y++)
            for (int z = Mathf.Max(0, minZ); z <= Mathf.Min(Grid.SizeZ - 1, maxZ); z++)
            for (int x = Mathf.Max(0, minX); x <= Mathf.Min(Grid.SizeX - 1, maxX); x++)
                _roles[Grid.Index(x, y, z)] = role;
        }

        public void FillRoles(RoomRole role)
        {
            for (int i = 0; i < _roles.Length; i++) _roles[i] = role;
        }

        /// <summary>Cópia do mapa de papéis — parte do contrato de saída para o populador (Decisão 4).</summary>
        public RoomRole[] CopyRoles() => (RoomRole[])_roles.Clone();

        // ------------------------------------------------------------- junções
        /// <summary>
        /// Passagem cravada pelo esqueleto entre DUAS REGIÕES diferentes. Sem junção, a
        /// borda entre regiões distintas é parede — é a junção que cria a conexão.
        ///
        /// <see cref="Junction.Opening"/> e <see cref="Junction.Door"/> conectam igual;
        /// a diferença é só qual peça o filler vai procurar. Isso importa porque nem toda
        /// posição comporta uma porta (o kit não tem peça para todo formato), e nesses
        /// casos rebaixar Door → Opening preserva a conectividade prometida.
        /// </summary>
        public Junction GetJunction(int cell, Direction dir) => _junctions[cell * Directions.Count + (int)dir];

        /// <summary>Sempre simétrico: a junção pertence à BORDA entre duas células, não a uma delas.</summary>
        public void SetJunction(int cell, Direction dir, Junction value)
        {
            _junctions[cell * Directions.Count + (int)dir] = value;
            if (Grid.TryGetNeighbor(cell, dir, out int neighbor))
                _junctions[neighbor * Directions.Count + (int)Directions.Opposite(dir)] = value;
        }

        public bool HasDoor(int cell, Direction dir) => GetJunction(cell, dir) == Junction.Door;

        public int CountJunctions(Junction kind)
        {
            int n = 0;
            for (int i = 0; i < _junctions.Length; i++) if (_junctions[i] == kind) n++;
            return n / 2; // cada junção está marcada dos dois lados
        }

        // -------------------------------------------------------------- regiões
        /// <summary>
        /// A que espaço contíguo a célula pertence: <see cref="CorridorRegion"/> para a malha
        /// de corredores, um id próprio por sala, -1 para vazio.
        ///
        /// Sem isso, duas salas encostadas viravam uma só: o rótulo da borda entre elas
        /// depende de estarem ou não na MESMA região, não só de ambas serem jogáveis.
        /// </summary>
        public int GetRegion(int cell) => _regions[cell];

        public void SetRegion(int cell, int region) => _regions[cell] = region;

        public void SetRegionBlock(int minX, int minZ, int maxX, int maxZ, int y, int region)
        {
            for (int z = Mathf.Max(0, minZ); z <= Mathf.Min(Grid.SizeZ - 1, maxZ); z++)
            for (int x = Mathf.Max(0, minX); x <= Mathf.Min(Grid.SizeX - 1, maxX); x++)
                _regions[Grid.Index(x, y, z)] = region;
        }

        /// <summary>Célula faz parte do andar jogável (não é rocha/vazio)?</summary>
        public bool IsPlayable(int cell) => _regions[cell] >= 0 && _roles[cell] != RoomRole.Vazio;

        // ---------------------------------------------------------- rótulos de borda
        /// <summary>
        /// O CONTRATO que o <see cref="IFloorFiller"/> consome. Cada borda entre duas células
        /// é uma de três coisas, e é isso — não papel, não tag — que decide qual peça cabe:
        ///
        ///   Wall  — fechado. Bordas do grid, e qualquer borda que toque o vazio.
        ///   Open  — passagem livre. Só dentro de uma mesma região.
        ///   Door  — passagem marcada explicitamente pelo esqueleto (junção sala↔corredor).
        ///
        /// Derivar em vez de armazenar mantém a coisa impossível de dessincronizar: mudou o
        /// papel ou a região, o rótulo muda junto.
        /// </summary>
        public BorderLabel GetBorder(int cell, Direction dir)
        {
            if (!Grid.TryGetNeighbor(cell, dir, out int neighbor)) return BorderLabel.Wall;
            if (!IsPlayable(cell) || !IsPlayable(neighbor)) return BorderLabel.Wall;

            switch (_junctions[cell * Directions.Count + (int)dir])
            {
                case Junction.Door: return BorderLabel.Door;
                case Junction.Opening: return BorderLabel.Open;
            }

            return _regions[cell] == _regions[neighbor] ? BorderLabel.Open : BorderLabel.Wall;
        }

        /// <summary>Quantas bordas desta célula têm o rótulo pedido (só as 4 horizontais).</summary>
        public int CountBorders(int cell, BorderLabel label)
        {
            int n = 0;
            foreach (Direction d in Directions.Horizontal)
                if (GetBorder(cell, d) == label) n++;
            return n;
        }

        // ---------------------------------------------------------------- mundo
        /// <summary>Centro da célula em coordenadas de mundo (y no nível do piso).</summary>
        public Vector3 CellToWorld(int cell)
        {
            Grid.Coords(cell, out int x, out int y, out int z);
            return CellToWorld(x, y, z);
        }

        public Vector3 CellToWorld(int x, int y, int z)
            => Origin + new Vector3((x + 0.5f) * CellSize, y * CellHeight, (z + 0.5f) * CellSize);
    }
}
