// ============================================================================
//  BasicLightingPopulator.cs  —  código do JOGO (Decisão 4: fora do plugin)
//
//  Popula luz de parede no andar já gerado. Vale para QUALQUER RoomRole —
//  sala ou corredor —, ao contrário do EnemyPopulator (só Combate): a regra
//  aqui é de GEOMETRIA, não de papel.
//
//  Consome os SpawnAnchor(kind = Light) que o Greybox Tile Generator planta
//  nas peças de parede reta (Tile_Wall, Tile_Corridor, Tile_Door_Corridor).
//  Duas das três regras pedidas vêm de GRAÇA por causa de qual peça ganha
//  anchor:
//
//    • "nunca em canto/quina"        → Tile_Corner não tem anchor de LUZ (tem um de Chest,
//                                       de outro kind — GreyboxTileGenerator.CornerChestAnchor —
//                                       que este script ignora por filtrar só kind == Light).
//    • "só parede única ou paralela" → só Tile_Wall (1 parede) e
//                                       Tile_Corridor/Tile_Door_Corridor
//                                       (2 paredes paralelas) têm anchor;
//                                       Tile_DeadEnd (3 paredes, perpendiculares
//                                       misturadas) foi deixado de fora de
//                                       propósito.
//
//  As outras duas regras são deste script:
//
//    • "a cada 3 paredes"     → agrupa os anchors em TRECHOS RETOS contíguos
//                               (mesmo eixo, mesmo SENTIDO, mesma linha) e
//                               acende 1 a cada N dentro de cada trecho.
//                               Corner/DeadEnd/Door (sem anchor) quebram o
//                               trecho sozinhos — não precisa detectar canto.
//    • "pular paredes em ângulo" → `oneAxisPerRoom`: dentro de uma sala, só o
//                               eixo com mais parede reta acende. Como as duas
//                               paredes OPOSTAS de uma sala compartilham o
//                               mesmo eixo, sai um par aceso e as outras duas
//                               no escuro, sem precisar comparar posições.
//                               Corredores ficam fora da regra (toda a malha
//                               deles é UMA região — escolher um eixo apagaria
//                               metade dos corredores do andar).
//
//  DUAS CAMADAS A MAIS, adicionadas depois de "algumas salas ficam sem luz
//  nenhuma" ter aparecido em teste — as duas regras acima combinadas (pouca
//  parede reta + metade descartada pelo eixo) zeram fácil numa sala pequena:
//
//    • Fase por trecho, não offset global → cada trecho reto começa a contagem
//      "a cada N" numa fase derivada de HASH da própria célula (RunPhase), não
//      sempre do mesmo startOffset. Sem isso, trechos em posição equivalente
//      do andar (comum em kit repetitivo) acendiam sempre no mesmo lugar
//      relativo — lia como grade. Continua 100% determinístico: mesma seed,
//      mesmas luzes sempre, só que sem alinhamento visual entre trechos.
//    • Piso mínimo por sala (PromoteUnderlitRooms) → sala que zerou ganha 1
//      anchor de volta, puxado de preferência do eixo que oneAxisPerRoom tinha
//      descartado. Só mexe na sala que precisa.
//
//  E uma peça de ATMOSFERA: anchor que a regra "a cada N" NÃO escolheu não
//  fica vazio — se `unlitTorchPrefab` estiver preenchido, nasce ali uma versão
//  decorativa (sem Light) do mesmo soquete. Ideia: nem toda tocha do calabouço
//  precisa estar acesa pra parecer que o lugar é habitado.
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using Babel.VFX;
using UnityEngine;
using WFC.Core;
using WFC.Runtime;

namespace Babel.Floor
{
    [AddComponentMenu("Babel/Basic Lighting Populator")]
    public sealed class BasicLightingPopulator : MonoBehaviour
    {
        [Tooltip("Prefab da luminária (Light + malha opcional). Instanciado na posição/rotação do anchor.")]
        [SerializeField] private GameObject lightPrefab;

        [Tooltip("Acende 1 a cada N anchors dentro do mesmo trecho reto de parede.")]
        [Min(1)] [SerializeField] private int everyNWalls = 3;

        [Tooltip("Somado à fase (derivada da célula) de CADA trecho antes de aplicar 'a cada N' — " +
                 "não é mais um offset global único: cada trecho já começa numa fase própria, " +
                 "isto aqui só desloca todas elas junto, se quiser afinar o resultado inteiro.")]
        [Min(0)] [SerializeField] private int startOffset = 0;

        [Tooltip("Toda SALA (corredor não conta — não tem 'a sala', é a malha inteira) precisa " +
                 "acabar com pelo menos este tanto de anchor aceso NO TOTAL (soma de todos os " +
                 "lados). SEMPRE roda, mesmo com 'Per Wall Light Bounds' ligado — aquela regra " +
                 "garante mínimo por LADO, esta garante o total da sala; uma sala com só 1-2 lados " +
                 "com candidato (dois lados sem Wall_Straight nenhum, por exemplo) podia passar no " +
                 "teto por parede e ainda sair com menos que o esperado no total. Promove do menor " +
                 "cellIndex entre os candidatos que sobraram, sem sortear nada. 0 = desliga.")]
        [Min(0)] [SerializeField] private int minLitAnchorsPerRoom = 3;

        [Tooltip("Mesma garantia do campo acima, mas por TRECHO RETO de CORREDOR (a malha de " +
                 "corredores é UMA região só, então isto não é 'por sala' — é por PEDAÇO reto " +
                 "contíguo). Sem isto um trecho curto podia cair na fase errada da regra 'a cada N' " +
                 "e ficar com zero tocha, sem ninguém prometer o mínimo de volta (a promoção por " +
                 "sala de cima explicitamente pula corredor). 0 = desliga.")]
        [Min(0)] [SerializeField] private int minLitPerCorridorRun = 2;

        [Tooltip("Garante, por TRECHO RETO de parede de SALA (corredor não conta), entre " +
                 "'Min Lit Per Wall' e 'Max Lit Per Wall' tochas acesas — não por sala inteira, " +
                 "por LADO da sala. Ligado, isto ignora o filtro de eixo do oneAxisPerRoom para " +
                 "as paredes de sala (density roda nos 4 lados, não só no eixo mais comprido). " +
                 "'Min Lit Anchors Per Room' continua rodando por cima — os dois se somam, não " +
                 "são mais alternativos.")]
        [SerializeField] private bool perWallLightBounds = true;

        [Tooltip("Mínimo de tochas acesas por trecho reto de parede de SALA, quando " +
                 "'Per Wall Light Bounds' está ligado.")]
        [Min(0)] [SerializeField] private int minLitPerWall = 1;

        [Tooltip("Máximo de tochas acesas por trecho reto de parede de SALA, quando " +
                 "'Per Wall Light Bounds' está ligado — corta o excesso que a regra 'a cada N' " +
                 "acender num trecho comprido (N pequeno + trecho longo passa fácil de 2).")]
        [Min(1)] [SerializeField] private int maxLitPerWall = 2;

        [Tooltip("Opcional. Instanciado (sem acender nada) numa FRAÇÃO dos anchors que a regra " +
                 "'a cada N' NÃO escolheu — mesmo soquete de parede, só que apagado. Puramente " +
                 "decorativo: dá a impressão de mais tocha no andar sem aumentar o orçamento de " +
                 "luz real. Vazio = nenhum anchor apagado ganha nada, como sempre foi.")]
        [SerializeField] private GameObject unlitTorchPrefab;

        [Tooltip("Fração dos anchors APAGADOS que ganham a versão decorativa — o resto continua " +
                 "vazio, senão vira 'todo soquete tem alguma coisa' (denso demais, lê como grade " +
                 "de novo, só que de tocha apagada em vez de acesa). 0.3 = ~30% dos apagados ganham " +
                 "prop; o resto do slot simplesmente não tem nada, como antes.")]
        [Range(0f, 1f)] [SerializeField] private float unlitTorchDensity = 0.3f;

        [Tooltip("Dentro de uma SALA, acende só as paredes de UM eixo — o resultado é um par de " +
                 "paredes opostas iluminado e as outras duas no escuro, em vez de luz nos quatro lados. " +
                 "Escolhe o eixo com mais parede reta (o lado mais comprido da sala). Não vale para " +
                 "corredores: lá os dois lados continuam podendo acender, senão metade dos corredores " +
                 "ficaria sem luz nenhuma (toda a malha de corredores é UMA região só).")]
        [SerializeField] private bool oneAxisPerRoom = true;

        [SerializeField] private bool logSummary = true;

        private readonly List<GameObject> spawned = new List<GameObject>();

        /// <summary>
        /// Popula as luzes do andar atual. Independente de seed/rng — a escolha de QUAIS
        /// anchors acendem é determinística a partir da geometria (mesmo andar = mesmas
        /// luzes sempre), não precisa consumir número nenhum do fluxo de seed.
        /// </summary>
        public int Populate(GeneratedFloor floor, Transform parent)
        {
            Clear();

            if (floor == null || !floor.Success) return 0;
            if (lightPrefab == null)
            {
                Debug.LogWarning("[BasicLightingPopulator] 'Light Prefab' vazio — nenhuma luz nasce.", this);
                return 0;
            }

            List<SpawnAnchor> lightAnchors = floor.Anchors
                .Where(a => a != null && a.kind == SpawnAnchorKind.Light)
                .ToList();

            if (lightAnchors.Count == 0)
            {
                if (logSummary)
                    Debug.Log("[BasicLightingPopulator] Nenhum anchor de luz no kit — " +
                              "tileset sem Tile_Wall/Tile_Corridor com anchor, ou andar sem parede reta.", this);
                return 0;
            }

            AnnotatedGrid annotated = floor.Grid;
            Grid3D grid = annotated.Grid;
            int missingTorchComponent = 0;

            // `candidates` é o pool INTEIRO (as duas regras de graça — sem quina, só parede
            // única/paralela — já filtraram isto no kit). `spacingPool` é o que sobra depois do
            // eixo por sala; é dele que a regra "a cada N" escolhe. A promoção de sala zerada
            // (abaixo) puxa do pool INTEIRO de propósito — é como ela consegue devolver um
            // anchor do eixo que o filtro descartou.
            int beforeAxisFilter = lightAnchors.Count;
            List<SpawnAnchor> candidates = lightAnchors;

            // Com perWallLightBounds ligado, a garantia por-parede abaixo já cobre "toda parede
            // de sala acende pelo menos uma" sozinha — filtrar por eixo aqui só faria a regra
            // "a cada N" ignorar dois dos quatro lados à toa, pra depois a promoção por-parede
            // reacender eles de qualquer jeito. Sem o filtro, "a cada N" já roda nos 4 lados.
            bool applyAxisFilter = oneAxisPerRoom && !perWallLightBounds;
            List<SpawnAnchor> spacingPool = applyAxisFilter ? FilterOneAxisPerRoom(candidates, annotated) : candidates;

            var chosen = new HashSet<SpawnAnchor>();
            foreach (List<SpawnAnchor> run in GroupIntoStraightRuns(spacingPool, grid))
            {
                int phase = RunPhase(run);
                for (int i = phase; i < run.Count; i += everyNWalls)
                    chosen.Add(run[i]);
            }

            int promoted = 0, demoted = 0;
            if (perWallLightBounds)
                promoted += EnforceWallLightBounds(chosen, candidates, annotated, grid, out demoted);

            // Sempre roda, independente de perWallLightBounds — aquela garante mínimo por LADO,
            // esta garante o TOTAL da sala (soma de todos os lados). Não são mais "ou".
            if (minLitAnchorsPerRoom > 0)
                promoted += PromoteUnderlitRooms(chosen, candidates, annotated);

            // Corredor fica de fora das duas garantias acima (region-based, ver seus comentários) —
            // esta é a garantia equivalente pra ele, por trecho reto contíguo.
            if (minLitPerCorridorRun > 0)
                promoted += EnforceCorridorMinimum(chosen, candidates, annotated, grid, minLitPerCorridorRun);

            int litCount = 0, unlitCount = 0;
            foreach (SpawnAnchor anchor in candidates)
            {
                bool isLit = chosen.Contains(anchor);
                if (!isLit && !ShouldDecorate(anchor)) continue; // maioria dos apagados fica vazia, de propósito

                GameObject prefab = isLit ? lightPrefab : unlitTorchPrefab;
                if (prefab == null) continue; // apagada sem prefab decorativo: fica vazia, como sempre foi

                GameObject instance = Instantiate(prefab, anchor.transform.position,
                                                   anchor.transform.rotation, parent);
                instance.name = isLit
                    ? $"{prefab.name} (célula {anchor.cellIndex})"
                    : $"{prefab.name} (célula {anchor.cellIndex}, apagada)";
                spawned.Add(instance);

                if (!isLit) { unlitCount++; continue; }
                litCount++;

                // Em que sala/corredor esta tocha nasceu. É a única informação que o
                // RegionLightMask não consegue redescobrir depois: a POSIÇÃO da tocha fica
                // 1 m à frente da parede, e uma parede entre duas regiões deixa esse ponto
                // ambíguo. A célula do anchor não é ambígua.
                var torch = instance.GetComponentInChildren<TorchLight>(true);
                if (torch != null) torch.Region = annotated.GetRegion(anchor.cellIndex);
                else if (missingTorchComponent++ == 0)
                    Debug.LogWarning($"[BasicLightingPopulator] '{lightPrefab.name}' não tem o componente " +
                                     "TorchLight — sem ele a tocha fica fora do orçamento de luz e da " +
                                     "contenção por sala (acesa sempre, com sombra sempre, atravessando " +
                                     "parede). Adicione o componente ao prefab.", this);
            }

            if (logSummary)
            {
                string boundsInfo = perWallLightBounds
                    ? $", {demoted} apagada(s) por passar do teto por parede"
                    : "";
                Debug.Log($"[BasicLightingPopulator] Andar {floor.FloorNumber}: {litCount} luz(es) " +
                          $"({promoted} promovida(s) por parede/sala sem luz{boundsInfo}), {unlitCount} " +
                          $"apagada(s) decorativa(s), de {candidates.Count} anchor(s) candidato(s) " +
                          $"({beforeAxisFilter} antes da regra de eixo por sala).", this);
            }

            return litCount;
        }

        /// <summary>
        /// Fase do "a cada N" DESTE trecho — derivada só da célula do primeiro anchor dele, via
        /// hash determinístico (nunca RNG: mesma seed tem que gerar sempre a mesma luz). Sem
        /// isto, todo trecho começava contando do mesmo `startOffset`, e trechos em posição
        /// equivalente do kit (comum, kit é repetitivo) acendiam sempre no mesmo lugar relativo
        /// — o que lia como padrão de grade no andar inteiro, mesmo a regra em si sendo por
        /// trecho. `startOffset` continua existindo, somado por cima, para afinar o conjunto todo.
        /// </summary>
        private int RunPhase(List<SpawnAnchor> run)
        {
            if (everyNWalls <= 1 || run.Count == 0) return 0;
            uint hash = MixHash(run[0].cellIndex);
            return (int)((hash + (uint)startOffset) % (uint)everyNWalls);
        }

        /// <summary>
        /// Mistura de bits determinística (não é RNG — mesma entrada sempre dá a mesma saída,
        /// sem consumir nenhum stream de seed). Só existe para não usar cellIndex cru como fase:
        /// cellIndex cru varia de forma regular pela ordem de varredura do grid, e "regular" é
        /// exatamente o padrão de grade que isto está tentando quebrar.
        /// </summary>
        private static uint MixHash(int value)
        {
            unchecked
            {
                uint x = (uint)value;
                x = ((x >> 16) ^ x) * 0x45d9f3bu;
                x = ((x >> 16) ^ x) * 0x45d9f3bu;
                x = (x >> 16) ^ x;
                return x;
            }
        }

        /// <summary>
        /// Decide, por hash determinístico (salt diferente de RunPhase, pra não correlacionar as
        /// duas decisões), se ESTE anchor apagado ganha a versão decorativa. Distribuição
        /// aproximadamente uniforme em [0,1) comparada contra <see cref="unlitTorchDensity"/> —
        /// "aproximadamente" já é o suficiente aqui, isto é atmosfera, não orçamento.
        /// </summary>
        private bool ShouldDecorate(SpawnAnchor anchor)
        {
            if (unlitTorchDensity <= 0f) return false;
            if (unlitTorchDensity >= 1f) return true;

            uint hash = MixHash(unchecked(anchor.cellIndex * 486187739 + 0x5bd1e995));
            float t = hash / (float)uint.MaxValue;
            return t < unlitTorchDensity;
        }

        /// <summary>
        /// Garante <see cref="minLitAnchorsPerRoom"/> anchor(s) aceso(s) por SALA (corredor fica
        /// de fora — não tem "a sala dele", é a malha inteira, e já não passa pelo filtro de
        /// eixo). Sem isto, a combinação "a cada N" + eixo por sala facilmente zera sala pequena.
        ///
        /// Promove do MENOR cellIndex entre os candidatos da sala que ainda não estão acesos —
        /// determinístico, sem sortear nada. Puxa de <paramref name="allCandidates"/> (o pool
        /// ANTES do filtro de eixo), não do pool já filtrado: é assim que uma sala sem nada no
        /// eixo vencedor ainda consegue recuperar um anchor do eixo perdedor.
        /// </summary>
        private int PromoteUnderlitRooms(HashSet<SpawnAnchor> chosen, List<SpawnAnchor> allCandidates, AnnotatedGrid grid)
        {
            var byRoom = new Dictionary<int, List<SpawnAnchor>>();
            foreach (SpawnAnchor anchor in allCandidates)
            {
                int region = grid.GetRegion(anchor.cellIndex);
                if (region < AnnotatedGrid.FirstRoomRegion) continue; // só sala; corredor fica de fora

                if (!byRoom.TryGetValue(region, out List<SpawnAnchor> list))
                {
                    list = new List<SpawnAnchor>();
                    byRoom[region] = list;
                }
                list.Add(anchor);
            }

            int promoted = 0;
            foreach (List<SpawnAnchor> roomAnchors in byRoom.Values)
            {
                int lit = 0;
                foreach (SpawnAnchor a in roomAnchors)
                    if (chosen.Contains(a)) lit++;

                SpawnAnchor next = null;
                while (lit < minLitAnchorsPerRoom)
                {
                    next = null;
                    foreach (SpawnAnchor a in roomAnchors)
                    {
                        if (chosen.Contains(a)) continue;
                        if (next == null || a.cellIndex < next.cellIndex) next = a;
                    }

                    if (next == null) break; // sala sem candidato suficiente pro mínimo pedido — não é erro
                    chosen.Add(next);
                    lit++;
                    promoted++;
                }
            }

            return promoted;
        }

        /// <summary>
        /// Garante entre <see cref="minLitPerWall"/> e <see cref="maxLitPerWall"/> anchors acesos
        /// por TRECHO RETO de parede de sala (corredor fica de fora — mesmo critério de
        /// <see cref="PromoteUnderlitRooms"/>). Reagrupa <paramref name="allCandidates"/> em runs
        /// do zero (não reaproveita os runs da passada "a cada N" porque aqueles vieram do
        /// spacingPool, que já pode ter sido filtrado por eixo) — é assim que uma parede que o
        /// oneAxisPerRoom teria apagado ainda aparece aqui como trecho próprio, com seu mínimo
        /// garantido.
        ///
        /// Promoção puxa o(s) anchor(s) de MENOR cellIndex do trecho; corte de excesso remove
        /// o(s) de MAIOR cellIndex entre os já acesos — as duas escolhas são determinísticas
        /// (mesma seed, mesmas luzes sempre), sem sortear nada.
        /// </summary>
        private int EnforceWallLightBounds(HashSet<SpawnAnchor> chosen, List<SpawnAnchor> allCandidates,
                                            AnnotatedGrid annotated, Grid3D grid, out int demoted)
        {
            int promoted = 0;
            demoted = 0;

            List<SpawnAnchor> roomCandidates = allCandidates
                .Where(a => annotated.GetRegion(a.cellIndex) >= AnnotatedGrid.FirstRoomRegion)
                .ToList();

            foreach (List<SpawnAnchor> run in GroupIntoStraightRuns(roomCandidates, grid))
            {
                List<SpawnAnchor> lit = run.Where(a => chosen.Contains(a))
                                            .OrderBy(a => a.cellIndex)
                                            .ToList();

                if (lit.Count > maxLitPerWall)
                {
                    for (int i = maxLitPerWall; i < lit.Count; i++)
                    {
                        chosen.Remove(lit[i]);
                        demoted++;
                    }
                }
                else if (lit.Count < minLitPerWall)
                {
                    List<SpawnAnchor> unlit = run.Where(a => !chosen.Contains(a))
                                                  .OrderBy(a => a.cellIndex)
                                                  .ToList();
                    int need = Mathf.Min(minLitPerWall - lit.Count, unlit.Count); // trecho curto demais: melhor esforço, não é erro
                    for (int i = 0; i < need; i++)
                    {
                        chosen.Add(unlit[i]);
                        promoted++;
                    }
                }
            }

            return promoted;
        }

        /// <summary>
        /// Equivalente ao <see cref="EnforceWallLightBounds"/>, só que pro lado que ele
        /// explicitamente pula: corredor. Não tem "a sala do corredor" (é uma região só pra todo
        /// o andar), então a garantia aqui é por TRECHO RETO contíguo — um corredor longo com
        /// várias curvas vira vários trechos independentes, cada um com seu próprio mínimo.
        /// Só promove (sem teto de máximo — corredor não tem o mesmo risco de "grade" que
        /// motivou o maxLitPerWall, que existe pra sala pequena com N baixo).
        /// </summary>
        private int EnforceCorridorMinimum(HashSet<SpawnAnchor> chosen, List<SpawnAnchor> allCandidates,
                                            AnnotatedGrid annotated, Grid3D grid, int minPerRun)
        {
            int promoted = 0;

            List<SpawnAnchor> corridorCandidates = allCandidates
                .Where(a => annotated.GetRegion(a.cellIndex) == AnnotatedGrid.CorridorRegion)
                .ToList();

            foreach (List<SpawnAnchor> run in GroupIntoStraightRuns(corridorCandidates, grid))
            {
                int lit = run.Count(a => chosen.Contains(a));
                if (lit >= minPerRun) continue;

                List<SpawnAnchor> unlit = run.Where(a => !chosen.Contains(a))
                                              .OrderBy(a => a.cellIndex)
                                              .ToList();
                int need = Mathf.Min(minPerRun - lit, unlit.Count); // trecho curto demais: melhor esforço, não é erro
                for (int i = 0; i < need; i++)
                {
                    chosen.Add(unlit[i]);
                    promoted++;
                }
            }

            return promoted;
        }

        public void Clear()
        {
            foreach (GameObject go in spawned)
                if (go != null) Destroy(go);
            spawned.Clear();
        }

        /// <summary>Parede corre ao longo de X (faces ±Z) ou ao longo de Z (faces ±X)?</summary>
        private static bool AlongX(Vector3 forward) => Mathf.Abs(forward.z) >= Mathf.Abs(forward.x);

        /// <summary>Sentido da face ao longo do eixo dominante: +1 ou -1.</summary>
        private static int Facing(Vector3 forward, bool alongX)
            => alongX ? (forward.z >= 0f ? 1 : -1) : (forward.x >= 0f ? 1 : -1);

        // -------------------------------------------------------- eixo por sala
        /// <summary>
        /// Dentro de cada SALA, mantém só os anchors de um eixo — o que tiver mais parede reta
        /// (o lado mais comprido). Como as duas paredes opostas de uma sala compartilham o
        /// mesmo eixo, o resultado é exatamente "duas paredes opostas acesas, as outras duas
        /// no escuro", sem precisar detectar canto: quem decide é a orientação, não a posição.
        ///
        /// Corredores (região única, compartilhada por toda a malha) ficam de fora da regra:
        /// escolher um eixo só para eles apagaria metade dos corredores do andar de uma vez.
        /// </summary>
        private static List<SpawnAnchor> FilterOneAxisPerRoom(List<SpawnAnchor> anchors, AnnotatedGrid grid)
        {
            var countByRegionAxis = new Dictionary<(int region, bool alongX), int>();

            foreach (SpawnAnchor anchor in anchors)
            {
                int region = grid.GetRegion(anchor.cellIndex);
                if (region < AnnotatedGrid.FirstRoomRegion) continue;

                var key = (region, AlongX(anchor.transform.forward));
                countByRegionAxis.TryGetValue(key, out int n);
                countByRegionAxis[key] = n + 1;
            }

            // Eixo vencedor por sala. Empate cai em alongX, por consistência entre execuções
            // (o resultado precisa ser função só da geometria — mesma seed, mesmas luzes).
            var axisByRegion = new Dictionary<int, bool>();
            foreach (KeyValuePair<(int region, bool alongX), int> pair in countByRegionAxis)
            {
                int region = pair.Key.region;
                if (!axisByRegion.TryGetValue(region, out bool currentAxis))
                {
                    axisByRegion[region] = pair.Key.alongX;
                    continue;
                }

                countByRegionAxis.TryGetValue((region, currentAxis), out int currentCount);
                if (pair.Value > currentCount || (pair.Value == currentCount && pair.Key.alongX))
                    axisByRegion[region] = pair.Key.alongX;
            }

            var kept = new List<SpawnAnchor>(anchors.Count);
            foreach (SpawnAnchor anchor in anchors)
            {
                int region = grid.GetRegion(anchor.cellIndex);
                if (region < AnnotatedGrid.FirstRoomRegion) { kept.Add(anchor); continue; }
                if (!axisByRegion.TryGetValue(region, out bool axis)) { kept.Add(anchor); continue; }
                if (AlongX(anchor.transform.forward) == axis) kept.Add(anchor);
            }

            return kept;
        }

        // ------------------------------------------------------------ agrupamento
        /// <summary>
        /// Agrupa anchors em trechos retos contíguos: mesma direção de parede (aproximada
        /// pelo eixo cardinal mais próximo do <c>forward</c> do anchor, já rotacionado
        /// junto com a peça), mesma "linha" perpendicular, ordenados ao longo do trecho.
        /// Uma lacuna na sequência (célula sem anchor — canto, beco, porta) fecha o
        /// trecho ali; o próximo anchor começa um trecho novo.
        /// </summary>
        private static IEnumerable<List<SpawnAnchor>> GroupIntoStraightRuns(List<SpawnAnchor> anchors, Grid3D grid)
        {
            var groups = new Dictionary<(bool alongX, int facing, int y, int fixedCoord),
                                        List<(int coord, SpawnAnchor a)>>();

            foreach (SpawnAnchor anchor in anchors)
            {
                grid.Coords(anchor.cellIndex, out int x, out int y, out int z);

                // Parede voltada para Norte/Sul (forward ~ ±Z) forma trecho ao longo de X;
                // voltada para Leste/Oeste (forward ~ ±X) forma trecho ao longo de Z.
                //
                // `facing` (o SENTIDO, não só o eixo) faz parte da chave porque uma peça de
                // corredor tem DOIS anchors na MESMA célula, um de cada lado. Sem isso os dois
                // caíam no mesmo grupo com o mesmo `coord`, a detecção de trecho contíguo
                // (coord == prevCoord, não prevCoord+1) quebrava o trecho a cada anchor, e como
                // todo trecho sempre acende o índice 0, acabava acendendo parede nenhuma —
                // acendia TODAS.
                Vector3 f = anchor.transform.forward;
                bool alongX = AlongX(f);
                int facing = Facing(f, alongX);
                int fixedCoord = alongX ? z : x;
                int coord = alongX ? x : z;

                var key = (alongX, facing, y, fixedCoord);
                if (!groups.TryGetValue(key, out var list))
                {
                    list = new List<(int, SpawnAnchor)>();
                    groups[key] = list;
                }
                list.Add((coord, anchor));
            }

            foreach (var list in groups.Values)
            {
                list.Sort((a, b) => a.coord.CompareTo(b.coord));

                var run = new List<SpawnAnchor>();
                int? prevCoord = null;

                foreach ((int coord, SpawnAnchor anchor) in list)
                {
                    if (prevCoord.HasValue && coord != prevCoord.Value + 1)
                    {
                        if (run.Count > 0) yield return run;
                        run = new List<SpawnAnchor>();
                    }

                    run.Add(anchor);
                    prevCoord = coord;
                }

                if (run.Count > 0) yield return run;
            }
        }
    }
}
