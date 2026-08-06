// ============================================================================
//  PropRoomPopulator.cs  —  código do JOGO (Decisão 4: fora do plugin)
//
//  O "Prop Populator" que faltava. Roda depois do WFC, sala de Combate por
//  sala de Combate: sorteia um RoomArchetype (peso configurável no asset) e
//  instancia os props dele.
//
//  Dois jeitos de posicionar, por entrada de prop (RoomArchetype.PropEntry):
//
//   • useAnchor = false (padrão) — célula de piso totalmente aberta dentro do
//     retângulo da sala, igual o EnemyPopulator já faz. Bom pra prop solto.
//   • useAnchor = true — só nasce em cima de um SpawnAnchor do 'anchorKind'
//     escolhido, coletado pelo TileInstancer nas peças que o autor do
//     tileset marcou (mesmo mecanismo do Anchor_Light_* do
//     BasicLightingPopulator, só que kind = Prop/WallProp/Chest). Sem anchor
//     daquele tipo na sala, a entrada simplesmente não nasce ali — não é bug.
//
//  ACOPLAMENTO COM O ENEMYPOPULATOR: o sorteio de arquétipo por sala é
//  separado da instanciação (RollArchetypes vs Populate) justamente para o
//  FloorDirector conseguir rolar ANTES de chamar o EnemyPopulator e passar o
//  resultado adiante — assim RoomArchetype.allowEnemies/enemyDensityMultiplier
//  valem sem os dois populadores precisarem se conhecer. Ver FloorDirector.cs.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using WFC.Core;
using WFC.Data;
using WFC.Runtime;

namespace Babel.Floor
{
    [AddComponentMenu("Babel/Prop Room Populator")]
    public sealed class PropRoomPopulator : MonoBehaviour
    {
        [Tooltip("Arquétipos possíveis para salas de Combate. Cada um já carrega seu próprio peso e " +
                 "elenco de props — pra adicionar um sabor de sala novo, crie o asset " +
                 "(Assets ▸ Create ▸ Babel ▸ Room Archetype) e arraste aqui. Não precisa de código novo.")]
        [SerializeField] private List<RoomArchetype> archetypes = new List<RoomArchetype>();

        [Tooltip("Raio de segurança em volta da entrada onde nenhum prop nasce (mesma ideia do EnemyPopulator).")]
        [SerializeField] private float safeRadiusFromEntrance = 6f;

        [SerializeField] private bool logSummary = true;

        private readonly List<GameObject> spawned = new List<GameObject>();

        /// <summary>
        /// Sorteia (sem instanciar nada) qual RoomArchetype cada sala de Combate recebeu.
        /// Chame ISTO primeiro — antes do EnemyPopulator — pra poder repassar o resultado
        /// pra ele (allowEnemies/enemyDensityMultiplier) e só depois chamar
        /// <see cref="Populate"/> com o mesmo dicionário. Salas fora de Combate, ou sem
        /// arquétipo elegível (lista vazia/sem props), simplesmente não entram no dicionário.
        /// </summary>
        public Dictionary<SkeletonGenerator.Room, RoomArchetype> RollArchetypes(GeneratedFloor floor, IRandom rng)
        {
            var map = new Dictionary<SkeletonGenerator.Room, RoomArchetype>();
            if (floor == null || !floor.Success || archetypes.Count == 0) return map;

            foreach (SkeletonGenerator.Room room in floor.Rooms)
            {
                if (room.Role != RoomRole.Combate) continue;

                RoomArchetype archetype = PickArchetype(rng);
                if (archetype != null && archetype.props.Count > 0) map[room] = archetype;
            }

            return map;
        }

        /// <summary>
        /// Instancia os props dos arquétipos já sorteados por <see cref="RollArchetypes"/>.
        /// Não sorteia arquétipo aqui — só posição/prefab dentro do arquétipo já decidido,
        /// pra ficar consistente com o que o EnemyPopulator recebeu do mesmo dicionário.
        /// </summary>
        public int Populate(GeneratedFloor floor,
                            IReadOnlyDictionary<SkeletonGenerator.Room, RoomArchetype> archetypeByRoom,
                            IRandom rng, Transform parent)
        {
            Clear();

            if (floor == null || !floor.Success) return 0;
            if (archetypeByRoom == null || archetypeByRoom.Count == 0)
            {
                if (logSummary)
                    Debug.LogWarning("[PropRoomPopulator] Nenhuma sala com arquétipo sorteado — " +
                                     "confira se 'Archetypes' está preenchido e se RollArchetypes foi chamado antes.", this);
                return 0;
            }

            AnnotatedGrid grid = floor.Grid;

            // Anchors elegíveis (Prop/WallProp/Chest), indexados por célula — Light fica de
            // fora, é território do BasicLightingPopulator.
            var anchorsByCell = new Dictionary<int, List<SpawnAnchor>>();
            foreach (SpawnAnchor a in floor.Anchors)
            {
                if (a == null) continue;
                if (a.kind != SpawnAnchorKind.Prop && a.kind != SpawnAnchorKind.WallProp && a.kind != SpawnAnchorKind.Chest)
                    continue;

                if (!anchorsByCell.TryGetValue(a.cellIndex, out List<SpawnAnchor> list))
                {
                    list = new List<SpawnAnchor>();
                    anchorsByCell[a.cellIndex] = list;
                }
                list.Add(a);
            }

            int roomsPopulated = 0, roomsSkippedRole = 0, roomsWithoutArchetype = 0;
            var openCandidates = new List<int>();
            var anchorCandidates = new List<SpawnAnchor>();
            var countByEntry = new Dictionary<RoomArchetype.PropEntry, int>();

            foreach (SkeletonGenerator.Room room in floor.Rooms)
            {
                if (room.Role != RoomRole.Combate) { roomsSkippedRole++; continue; }

                if (!archetypeByRoom.TryGetValue(room, out RoomArchetype archetype) || archetype == null)
                { roomsWithoutArchetype++; continue; }

                openCandidates.Clear();
                anchorCandidates.Clear();
                for (int z = room.Rect.yMin; z < room.Rect.yMax; z++)
                for (int x = room.Rect.xMin; x < room.Rect.xMax; x++)
                {
                    int cell = grid.Grid.Index(x, 0, z);

                    if (floor.IsOpenFloorCell(cell)
                        && (grid.CellToWorld(cell) - floor.EntranceWorld).sqrMagnitude
                           >= safeRadiusFromEntrance * safeRadiusFromEntrance)
                        openCandidates.Add(cell);

                    if (anchorsByCell.TryGetValue(cell, out List<SpawnAnchor> list))
                        anchorCandidates.AddRange(list);
                }

                int min = Mathf.Min(archetype.propCount.x, archetype.propCount.y);
                int max = Mathf.Max(archetype.propCount.x, archetype.propCount.y);
                int wanted = min + rng.NextInt(max - min + 1);

                // Por SALA, não por andar: cada sala sorteia seu elenco do zero, então o teto
                // por entrada (RoomArchetype.PropEntry.maxCount) também reinicia por sala.
                countByEntry.Clear();

                for (int i = 0; i < wanted; i++)
                {
                    RoomArchetype.PropEntry entry = PickPropEntry(archetype, rng, countByEntry);
                    if (entry?.prefab == null) continue; // inclui "todas as entradas bateram o teto"

                    Vector3 pos;
                    Quaternion rot;

                    if (entry.useAnchor)
                    {
                        int idx = FindAnchorIndex(anchorCandidates, entry.anchorKind, rng);
                        if (idx < 0) continue; // sem anchor desse tipo sobrando na sala — não é erro

                        SpawnAnchor anchor = anchorCandidates[idx];
                        pos = anchor.transform.position;
                        rot = anchor.transform.rotation;
                        anchorCandidates.RemoveAt(idx); // não repete o mesmo ponto
                    }
                    else
                    {
                        if (openCandidates.Count == 0) continue;
                        int cell = openCandidates[rng.NextInt(openCandidates.Count)];
                        pos = grid.CellToWorld(cell);
                        rot = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
                    }

                    GameObject instance = Instantiate(entry.prefab, pos, rot, parent);
                    instance.name = $"{entry.prefab.name} ({archetype.displayName})";
                    spawned.Add(instance);

                    // Conta a INSTÂNCIA de verdade, não a escolha — se o anchor faltou e a
                    // entrada nem chegou a nascer (continue acima), não deveria consumir o teto.
                    countByEntry.TryGetValue(entry, out int n);
                    countByEntry[entry] = n + 1;
                }

                roomsPopulated++;
            }

            if (logSummary)
                Debug.Log($"[PropRoomPopulator] Andar {floor.FloorNumber}: {spawned.Count} prop(s) em " +
                          $"{roomsPopulated} sala(s) ({roomsSkippedRole} fora do papel Combate, " +
                          $"{roomsWithoutArchetype} sem arquétipo utilizável).", this);

            return spawned.Count;
        }

        public void Clear()
        {
            foreach (GameObject go in spawned)
                if (go != null) Destroy(go);
            spawned.Clear();
        }

        // ------------------------------------------------------------ sorteio
        private RoomArchetype PickArchetype(IRandom rng)
        {
            float total = 0f;
            foreach (RoomArchetype a in archetypes)
                if (a != null) total += a.weight;

            if (total <= 0f) return null;

            double roll = rng.NextDouble() * total;
            foreach (RoomArchetype a in archetypes)
            {
                if (a == null) continue;
                roll -= a.weight;
                if (roll <= 0.0) return a;
            }

            return archetypes[archetypes.Count - 1];
        }

        /// <summary>
        /// Sorteio por peso, igual sempre — só que entradas que já bateram
        /// <see cref="RoomArchetype.PropEntry.maxCount"/> nesta sala saem do pool antes de somar
        /// os pesos, como se não existissem. Devolve null quando não sobra nenhuma (todas
        /// zeradas ou todas no teto) — o chamador já trata null como "esta vaga não nasce".
        /// </summary>
        private static RoomArchetype.PropEntry PickPropEntry(RoomArchetype archetype, IRandom rng,
                                                               Dictionary<RoomArchetype.PropEntry, int> countByEntry)
        {
            float total = 0f;
            foreach (RoomArchetype.PropEntry e in archetype.props)
                if (e?.prefab != null && !AtCap(e, countByEntry)) total += e.weight;

            if (total <= 0f) return null;

            double roll = rng.NextDouble() * total;
            foreach (RoomArchetype.PropEntry e in archetype.props)
            {
                if (e?.prefab == null || AtCap(e, countByEntry)) continue;
                roll -= e.weight;
                if (roll <= 0.0) return e;
            }

            return null;
        }

        private static bool AtCap(RoomArchetype.PropEntry entry, Dictionary<RoomArchetype.PropEntry, int> countByEntry)
            => entry.maxCount > 0 && countByEntry.TryGetValue(entry, out int n) && n >= entry.maxCount;

        private static int FindAnchorIndex(List<SpawnAnchor> candidates, SpawnAnchorKind kind, IRandom rng)
        {
            var matches = new List<int>();
            for (int i = 0; i < candidates.Count; i++)
                if (candidates[i].kind == kind) matches.Add(i);

            return matches.Count == 0 ? -1 : matches[rng.NextInt(matches.Count)];
        }
    }
}
