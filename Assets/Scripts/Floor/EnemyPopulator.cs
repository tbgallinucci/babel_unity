// ============================================================================
//  EnemyPopulator.cs  —  código do JOGO  (Decisão 4: fora do plugin)
//
//  Consome o contrato de saída do gerador (mapa de papéis + salas + NavMesh) e
//  decide o que nasce onde. O plugin não sabe o que é um inimigo; este arquivo
//  é o único lugar que sabe.
//
//  Regras de posicionamento:
//   • só salas de Combate, nunca a sala de entrada — o jogador não cai no meio
//     de uma briga sem ter tempo de olhar em volta;
//   • só células de piso totalmente aberto (4 bordas livres), para o inimigo não
//     nascer encostado ou dentro de parede;
//   • posição final amostrada na NavMesh — se não houver NavMesh ali, o ponto é
//     descartado em vez de spawnar um NavMeshAgent que nunca vai andar;
//   • espaçamento mínimo entre inimigos e distância mínima da entrada.
// ============================================================================

using System.Collections.Generic;
using Babel.Combat;
using UnityEngine;
using WFC.Core;
using WFC.Data;
using WFC.Runtime;

namespace Babel.Floor
{
    [AddComponentMenu("Babel/Enemy Populator")]
    public sealed class EnemyPopulator : MonoBehaviour
    {
        [System.Serializable]
        public sealed class EnemyEntry
        {
            [Tooltip("Prefab do inimigo. Precisa ter NavMeshAgent (EnemyBase exige).")]
            public GameObject prefab;

            [Tooltip("Peso relativo no sorteio.")]
            [Min(0.01f)] public float weight = 1f;

            [Tooltip("Só aparece a partir deste andar.")]
            [Min(1)] public int minFloor = 1;
        }

        [Header("Elenco")]
        [SerializeField] private List<EnemyEntry> enemies = new List<EnemyEntry>();

        [Header("Quantidade")]
        [Tooltip("Inimigos por sala de combate no andar 1 (mín, máx).")]
        [SerializeField] private Vector2Int perRoom = new Vector2Int(1, 3);

        [Tooltip("Quanto a média sobe por andar. 0.5 = +1 inimigo a cada 2 andares.")]
        [SerializeField] private float extraPerFloor = 0.5f;

        [Tooltip("Teto duro por sala, para o andar 20 não virar sopa.")]
        [Min(1)] [SerializeField] private int maxPerRoom = 8;

        [Header("Posicionamento")]
        [Tooltip("Distância mínima entre dois inimigos, em metros.")]
        [SerializeField] private float minSpacing = 3f;

        [Tooltip("Raio de segurança em volta da entrada onde nada nasce.")]
        [SerializeField] private float safeRadiusFromEntrance = 14f;

        [Tooltip("Quão longe procurar NavMesh a partir do centro da célula.")]
        [SerializeField] private float navMeshSampleDistance = 3f;

        [SerializeField] private bool logSummary = true;

        // O botão "+" do Inspector para List<> customizada às vezes não aplica os
        // valores padrão do C# (weight = 1f) — o elemento novo nasce com Weight 0,
        // e como PickPrefab soma os pesos e desiste se o total for 0, o inimigo
        // nunca é sorteado, sem erro nenhum. Corrige aqui, de graça, toda vez que
        // o Inspector muda.
        private void OnValidate()
        {
            foreach (EnemyEntry e in enemies)
                if (e != null && e.weight <= 0f) e.weight = 1f;
        }

        /// <summary>Inimigos vivos do andar atual.</summary>
        public int LivingCount { get; private set; }

        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Vector3> placedPositions = new List<Vector3>();

        /// <summary>
        /// Popula o andar. O <paramref name="rng"/> vem do mesmo fluxo de seed do
        /// gerador, então a mesma seed reproduz também a distribuição de inimigos.
        ///
        /// <paramref name="archetypeByRoom"/> é opcional — passe o resultado de
        /// <see cref="PropRoomPopulator.RollArchetypes"/> (chamado ANTES deste método) pra
        /// respeitar <see cref="RoomArchetype.allowEnemies"/> e
        /// <see cref="RoomArchetype.enemyDensityMultiplier"/> por sala. Sem isso (ou sala
        /// sem entrada no dicionário), o comportamento é o de sempre: toda sala de Combate
        /// é candidata, sem multiplicador.
        /// </summary>
        public int Populate(GeneratedFloor floor, IRandom rng, Transform parent,
                            IReadOnlyDictionary<SkeletonGenerator.Room, RoomArchetype> archetypeByRoom = null)
        {
            Clear();

            if (floor == null || !floor.Success) return 0;
            if (enemies.Count == 0)
            {
                Debug.LogWarning("[EnemyPopulator] Nenhum inimigo cadastrado — o andar sai vazio. " +
                                 "Arraste um prefab de inimigo na lista.", this);
                return 0;
            }

            AnnotatedGrid grid = floor.Grid;
            var candidates = new List<int>();

            // Contadores só para diagnóstico — sem eles "zero inimigos" não diz por quê.
            int roomsSkippedRole = 0, roomsSkippedArchetype = 0, roomsWithoutCandidates = 0;
            int navMeshMisses = 0, tooCloseRejections = 0, noPrefabPicked = 0;

            foreach (SkeletonGenerator.Room room in floor.Rooms)
            {
                // Só sala de Combate. Antes disto o filtro era "qualquer coisa != Entrada",
                // o que também soltava inimigo na sala da Escada e, agora que o esqueleto
                // pode sortear outros papéis (roomRoleWeights), em qualquer arquétipo novo
                // (Tesouro, etc.) — cada papel deveria ter seu próprio populador, não cair
                // aqui por omissão.
                if (room.Role != RoomRole.Combate) { roomsSkippedRole++; continue; }

                RoomArchetype archetype = null;
                archetypeByRoom?.TryGetValue(room, out archetype);

                if (archetype != null && !archetype.allowEnemies)
                { roomsSkippedArchetype++; continue; }

                // Candidatas: piso aberto dentro do retângulo da sala.
                candidates.Clear();
                for (int z = room.Rect.yMin; z < room.Rect.yMax; z++)
                for (int x = room.Rect.xMin; x < room.Rect.xMax; x++)
                {
                    int cell = grid.Grid.Index(x, 0, z);
                    if (!floor.IsOpenFloorCell(cell)) continue;
                    if ((grid.CellToWorld(cell) - floor.EntranceWorld).sqrMagnitude
                        < safeRadiusFromEntrance * safeRadiusFromEntrance) continue;
                    candidates.Add(cell);
                }

                if (candidates.Count == 0) { roomsWithoutCandidates++; continue; }

                int wanted = Mathf.Min(maxPerRoom, RollCount(floor.FloorNumber, rng));
                if (archetype != null && !Mathf.Approximately(archetype.enemyDensityMultiplier, 1f))
                    wanted = Mathf.RoundToInt(wanted * archetype.enemyDensityMultiplier);

                for (int i = 0; i < wanted; i++)
                {
                    GameObject prefab = PickPrefab(floor.FloorNumber, rng);
                    if (prefab == null) { noPrefabPicked++; continue; }

                    int cell = candidates[rng.NextInt(candidates.Count)];
                    Vector3 point = grid.CellToWorld(cell);

                    // Sem NavMesh embaixo, o NavMeshAgent nasce inválido e o inimigo
                    // fica plantado. Melhor não spawnar.
                    if (!NavMeshBuilderService.TrySnapToNavMesh(point, navMeshSampleDistance, out Vector3 snapped))
                    { navMeshMisses++; continue; }

                    if (TooClose(snapped)) { tooCloseRejections++; continue; }

                    GameObject instance = Instantiate(prefab, snapped, RandomYaw(rng), parent);
                    instance.name = $"{prefab.name} (andar {floor.FloorNumber})";

                    Track(instance);
                    placedPositions.Add(snapped);
                }
            }

            if (logSummary)
            {
                Debug.Log($"[EnemyPopulator] Andar {floor.FloorNumber}: {spawned.Count} inimigo(s) " +
                          $"em {floor.Rooms.Count} sala(s) ({roomsSkippedRole} fora do papel Combate, " +
                          $"{roomsSkippedArchetype} bloqueada(s) por arquétipo (allowEnemies=false), " +
                          $"{roomsWithoutCandidates} sem célula aberta).", this);

                // Zero spawns e algum contador suspeito != 0: é aqui que está o motivo.
                if (spawned.Count == 0 && floor.Rooms.Count > 0)
                {
                    Debug.LogWarning(
                        "[EnemyPopulator] Zero inimigos. Motivo provável: " +
                        $"{roomsWithoutCandidates} sala(s) sem célula aberta o bastante " +
                        $"(safeRadiusFromEntrance={safeRadiusFromEntrance}m pode estar cobrindo a sala inteira), " +
                        $"{navMeshMisses} amostra(s) de NavMesh falharam " +
                        "(NavMesh não bakeou ali — confira se 'Bake Nav Mesh' está ligado no WFC Floor Generator), " +
                        $"{tooCloseRejections} rejeitada(s) por espaçamento, " +
                        $"{noPrefabPicked} sem prefab sorteado (lista 'Elenco' vazia ou com Weight 0).", this);
                }
            }

            return spawned.Count;
        }

        public void Clear()
        {
            foreach (GameObject go in spawned)
                if (go != null) Destroy(go);

            spawned.Clear();
            placedPositions.Clear();
            LivingCount = 0;
        }

        // ------------------------------------------------------------ internos
        private int RollCount(int floorNumber, IRandom rng)
        {
            int min = Mathf.Min(perRoom.x, perRoom.y);
            int max = Mathf.Max(perRoom.x, perRoom.y);
            int bonus = Mathf.FloorToInt(extraPerFloor * (floorNumber - 1));
            return Mathf.Max(0, min + bonus + rng.NextInt(max - min + 1));
        }

        private GameObject PickPrefab(int floorNumber, IRandom rng)
        {
            float total = 0f;
            foreach (EnemyEntry e in enemies)
                if (e.prefab != null && floorNumber >= e.minFloor) total += e.weight;

            if (total <= 0f) return null;

            double roll = rng.NextDouble() * total;
            foreach (EnemyEntry e in enemies)
            {
                if (e.prefab == null || floorNumber < e.minFloor) continue;
                roll -= e.weight;
                if (roll <= 0.0) return e.prefab;
            }

            return null;
        }

        private bool TooClose(Vector3 point)
        {
            float sqr = minSpacing * minSpacing;
            foreach (Vector3 p in placedPositions)
                if ((p - point).sqrMagnitude < sqr) return true;
            return false;
        }

        private static Quaternion RandomYaw(IRandom rng)
            => Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);

        private void Track(GameObject instance)
        {
            spawned.Add(instance);
            LivingCount++;

            // O contador é o que destranca a escada quando "limpar o andar" estiver
            // ligado. Se o inimigo não tiver HealthComponent, ele conta como vivo para
            // sempre — por isso o aviso.
            var health = instance.GetComponentInChildren<HealthComponent>();
            if (health == null)
            {
                Debug.LogWarning($"[EnemyPopulator] '{instance.name}' não tem HealthComponent; " +
                                 "ele nunca vai contar como morto.", instance);
                return;
            }

            health.OnDeath += () => LivingCount = Mathf.Max(0, LivingCount - 1);
        }
    }
}
