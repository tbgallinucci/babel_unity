// ============================================================================
//  FloorSpec.cs  —  camada WFC.Data
//
//  A "receita" de um andar: qual tileset, que tamanho de grid, que seed, e as
//  regras que ligam RoomRole (papel da célula) a ModuleTag (o que a peça é).
//
//  Na Fase 2 é este asset que o SkeletonGenerator lê para decidir a composição
//  do andar. Por ora ele já serve para o demo e para selar as bordas.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using WFC.Core;

namespace WFC.Data
{
    [CreateAssetMenu(fileName = "FloorSpec", menuName = "WFC/Floor Spec", order = 3)]
    public sealed class FloorSpec : ScriptableObject
    {
        [Serializable]
        public sealed class RoleRule
        {
            public RoomRole role = RoomRole.Corredor;

            [Tooltip("A peça precisa ter TODAS estas tags para poder cair numa célula com este papel.")]
            public ModuleTag requiredTags = ModuleTag.None;

            [Tooltip("A peça não pode ter NENHUMA destas tags.")]
            public ModuleTag forbiddenTags = ModuleTag.None;
        }

        [Header("Tileset")]
        public TileSet tileSet;

        [Header("Paredes de grid dual (opcional)")]
        [Tooltip("Preenchido = as paredes vêm destas peças, plantadas nos VÉRTICES do grid depois " +
                 "de instanciar as células (ver DualGridWallBuilder). Nesse modo as peças do TileSet " +
                 "devem ser SÓ PISO — se elas trouxerem parede junto, as duas geometrias se " +
                 "sobrepõem.\n\n" +
                 "Vazio = a peça de cada célula precisa trazer a parede embutida (era o modo do " +
                 "kit Assets/WFC/GreyboxTiles, removido — nenhum tileset vivo usa mais este caminho).")]
        public WallPieceSet wallPieces;

        [Header("Grid")]
        [Tooltip("Dimensões em CÉLULAS. Y = 1 é o caso de um andar plano (é o da Fase 0/1).")]
        public Vector3Int gridSize = new Vector3Int(16, 1, 16);

        [Tooltip("Footprint do módulo do kit de arte, em metros. O greybox deste projeto usa 6.")]
        [Min(0.01f)]
        public float cellSize = 6f;

        [Tooltip("Altura de um andar de células, em metros. No greybox deste projeto a parede tem 5 " +
                 "(não é igual ao footprint). Só entra em jogo quando gridSize.y > 1.")]
        [Min(0.01f)]
        public float cellHeight = 5f;

        [Header("Seed")]
        [Tooltip("Se ligado, sorteia uma seed nova a cada geração. Desligue para reproduzir um andar.")]
        public bool randomSeed = true;

        public int seed = 12345;

        [Header("Bordas")]
        [Tooltip("Restringe as células da borda a peças cuja face virada para fora esteja fechada. " +
                 "Sem isso o andar termina com pisos apontando para o vazio.")]
        public bool sealBorders = true;

        [Tooltip("Nome do socket que representa uma face fechada (ver SocketLibrary).")]
        public string borderSocket = "wall";

        [Header("Esqueleto (Decisão 1)")]
        [Tooltip("Como o SkeletonGenerator espalha as salas e as liga. É o que define a cara do andar.")]
        public SkeletonSettings skeleton = new SkeletonSettings();

        [Header("Solver")]
        [Min(1)] public int maxAttempts = 12;

        [Tooltip("Ruído de desempate da entropia. Zero faz o andar sair 'listrado'.")]
        public double entropyNoise = 1e-4;

        [Header("Papéis (usado a partir da Fase 2)")]
        public List<RoleRule> roleRules = new List<RoleRule>();

        // ------------------------------------------------------------- helpers
        public Grid3D CreateGrid()
            => new Grid3D(Mathf.Max(1, gridSize.x), Mathf.Max(1, gridSize.y), Mathf.Max(1, gridSize.z));

        public SolverSettings CreateSolverSettings() => new SolverSettings
        {
            MaxAttempts = Mathf.Max(1, maxAttempts),
            MaxIterationsPerAttempt = 0, // 0 = número de células
            EntropyNoise = entropyNoise,
        };

        public RoleRule FindRule(RoomRole role)
        {
            for (int i = 0; i < roleRules.Count; i++)
                if (roleRules[i] != null && roleRules[i].role == role) return roleRules[i];
            return null;
        }

        /// <summary>
        /// Máscara de variantes aceitáveis numa célula com este papel, segundo as regras.
        /// Sem regra cadastrada = tudo liberado.
        /// </summary>
        public ulong[] MaskForRole(RoomRole role)
        {
            if (tileSet == null || !tileSet.IsBaked) return null;
            RoleRule rule = FindRule(role);
            if (rule == null) return null;
            return tileSet.MaskWithTags(rule.requiredTags, rule.forbiddenTags);
        }

        /// <summary>
        /// Constraints que selam as bordas do grid (ver <see cref="ConstraintBuilder.SealBorders"/>).
        /// Devolve lista vazia se o selo estiver desligado ou o tileset não estiver bakeado.
        /// </summary>
        public List<Constraint> BuildBorderConstraints(Grid3D grid)
        {
            if (!sealBorders || tileSet == null || !tileSet.IsBaked)
                return new List<Constraint>();

            // A face virada para FORA precisa ser fechada. Cacheamos por direção
            // porque SealBorders chama isto uma vez por célula de borda.
            var cache = new Dictionary<Direction, ulong[]>();

            return ConstraintBuilder.SealBorders(
                grid,
                tileSet.VariantCount,
                dir =>
                {
                    if (!cache.TryGetValue(dir, out ulong[] mask))
                    {
                        mask = tileSet.MaskWithFaceSocket(dir, borderSocket);
                        cache[dir] = mask;
                    }
                    return mask;
                },
                includeVertical: false);
        }

        public int ResolveSeed()
            => randomSeed ? Environment.TickCount : seed;
    }
}
