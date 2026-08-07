// ============================================================================
//  DecorationPopulator.cs  —  código do JOGO (Decisão 4: fora do plugin)
//
//  Planta decoração PURAMENTE ESTÉTICA em TODO SpawnAnchor de um kind
//  configurado — sem competir com o orçamento de RoomArchetype.PropEntry (que
//  é pra interagível: baú, fonte de cura, estátua de buff, NPC de loja — coisa
//  que precisa de coerência temática por sala e escassez controlada) e sem
//  ficar restrito a sala de Combate: os anchors estruturais (Pillar, hoje)
//  nascem em Wall_Corner/Wall_Cross, que aparecem em corredor, sala qualquer,
//  em todo lugar do andar.
//
//  Um populator só pra QUALQUER quantidade de categoria de decoração: cada
//  DecorationSet mapeia UM SpawnAnchorKind pra uma lista de variantes com
//  peso (mesma UI de RoomArchetype.PropEntry, sem reinventar) + uma chance de
//  spawn por anchor daquele kind. Pra decorar um kind novo amanhã (teia de
//  aranha, rachadura de parede, o que for), não precisa de C# novo — só uma
//  entrada nova na lista do Inspector, contanto que a peça do tileset já
//  plante o SpawnAnchor daquele kind.
//
//  Antes disto existir, cada categoria de decoração pedia populator próprio
//  (era assim que o PillarPopulator nasceu) — este arquivo substitui aquele
//  de vez, generalizado.
//
//  TRÊS REGRAS DE BOM SENSO, adicionadas depois de "decoração no meio da sala
//  atrapalha combate" ter aparecido em teste:
//
//    • Só perto de parede/quina → um anchor só é candidato se a CÉLULA dele
//      encostar em pelo menos uma borda Wall (CountBorders). Pilar/WallProp já
//      nascem deslocados pra dentro da parede por construção (sempre passam);
//      o filtro é o que importa de verdade pra Prop, que sem isso podia cair
//      em qualquer célula aberta, incluindo o meio livre da sala.
//    • Prop só em SALA, nunca em corredor → corredor é onde o jogador passa
//      correndo/lutando, sem a mesma folga de uma sala; Pillar/WallProp podem
//      continuar em corredor (fazem sentido arquitetonicamente ali).
//    • Teto por SALA (corredor fica de fora — não tem "a sala dele", é a
//      malha inteira, mesmo critério do BasicLightingPopulator) → sorteia
//      quais anchors sobrevivem quando uma sala tem mais candidato que o
//      teto, em vez de cortar sempre os mesmos (ordem de varredura do grid
//      seria regular, leria como padrão).
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WFC.Core;
using WFC.Runtime;

namespace Babel.Floor
{
    [AddComponentMenu("Babel/Decoration Populator")]
    public sealed class DecorationPopulator : MonoBehaviour
    {
        [Serializable]
        public sealed class WeightedPrefab
        {
            public GameObject prefab;

            [Tooltip("Peso relativo no sorteio, entre as variantes DESTE conjunto.")]
            [Min(0.01f)] public float weight = 1f;
        }

        [Serializable]
        public sealed class DecorationSet
        {
            [Tooltip("Só pra reconhecer esta entrada no Inspector — não afeta o sorteio.")]
            public string displayName = "Nova decoração";

            [Tooltip("Qual SpawnAnchorKind este conjunto decora. Precisa bater com o kind que a " +
                     "peça do tileset planta (ex.: Pillar, gerado no Wall_Corner/Wall_Cross).")]
            public SpawnAnchorKind anchorKind = SpawnAnchorKind.Pillar;

            [Tooltip("As variantes visuais deste conjunto — sorteia UMA por anchor, por peso.")]
            public List<WeightedPrefab> variants = new List<WeightedPrefab>();

            [Tooltip("Chance de CADA anchor deste kind receber decoração. 1 = sempre (todo anchor " +
                     "do kind ganha algo). Baixe pra densidade menor sem tirar anchor do tileset.")]
            [Range(0f, 1f)] public float spawnChance = 1f;
        }

        [Tooltip("Um conjunto por kind de anchor que você quer decorar. Adicionar categoria nova " +
                 "= nova entrada aqui, sem C#.")]
        [SerializeField] private List<DecorationSet> decorations = new List<DecorationSet>();

        [Tooltip("Só planta decoração em anchor cuja célula encoste em pelo menos uma parede/quina " +
                 "— nunca no meio livre da sala. Pilar/WallProp já nascem colados na parede por " +
                 "construção (o filtro nunca os derruba); é Prop que precisa disto.")]
        [SerializeField] private bool requireWallAdjacency = true;

        [Tooltip("Anchor de kind Prop só é candidato dentro de SALA — nunca em corredor. Pillar e " +
                 "WallProp ficam de fora desta regra (fazem sentido em corredor também).")]
        [SerializeField] private bool propsOnlyInRooms = true;

        [Tooltip("Máximo de decorações (somando todos os conjuntos) por SALA. Corredor fica de fora " +
                 "— é uma malha só, não 'uma sala', tetar ali zeraria corredor comprido. 0 = sem teto.")]
        [Min(0)] [SerializeField] private int maxPerRoom = 3;

        [SerializeField] private bool logSummary = true;

        private readonly List<GameObject> spawned = new List<GameObject>();

        public int Populate(GeneratedFloor floor, IRandom rng, Transform parent)
        {
            Clear();

            if (floor == null || !floor.Success || decorations.Count == 0) return 0;

            List<SpawnAnchor> allowed = FilterAnchors(floor, rng);

            var countBySet = new int[decorations.Count];
            int candidates = 0;

            foreach (SpawnAnchor a in allowed)
            {
                for (int i = 0; i < decorations.Count; i++)
                {
                    DecorationSet set = decorations[i];
                    if (set == null || set.anchorKind != a.kind || set.variants.Count == 0) continue;

                    candidates++;

                    // spawnChance = 1 (padrão) pula direto pro sorteio de variante — não gasta
                    // RNG à toa quando "sempre" é a intenção, mantém determinístico igual o
                    // BasicLightingPopulator quando ninguém pediu variação de densidade.
                    if (set.spawnChance < 1f && rng.NextDouble() >= set.spawnChance) break;

                    GameObject prefab = PickWeighted(set.variants, rng);
                    if (prefab == null) break;

                    GameObject instance = Instantiate(prefab, a.transform.position, a.transform.rotation, parent);
                    instance.name = $"{prefab.name} ({set.displayName}) [{a.cellIndex}]";
                    spawned.Add(instance);
                    countBySet[i]++;

                    break; // um anchor só bate com UM conjunto (kind é 1:1 aqui) — já achou o dele
                }
            }

            if (logSummary)
            {
                var perSet = new System.Text.StringBuilder();
                for (int i = 0; i < decorations.Count; i++)
                    perSet.Append($"{decorations[i].displayName}={countBySet[i]} ");
                Debug.Log($"[DecorationPopulator] Andar {floor.FloorNumber}: {spawned.Count} decoração(ões) " +
                         $"de {candidates} anchor(s) candidato(s) ({allowed.Count} sobreviveram ao filtro de " +
                         $"parede/teto por sala, de {floor.Anchors.Count} anchor(s) no andar) — {perSet}", this);
            }

            return spawned.Count;
        }

        /// <summary>
        /// Aplica as três regras de bom senso ANTES do sorteio de kind/variante: adjacência de
        /// parede (nunca no meio livre da sala), Prop só em sala (nunca corredor) e teto por sala
        /// (não lota uma sala pequena). Devolve os anchors que sobrevivem, na ordem de avaliação.
        /// </summary>
        private List<SpawnAnchor> FilterAnchors(GeneratedFloor floor, IRandom rng)
        {
            AnnotatedGrid grid = floor.Grid;

            IEnumerable<SpawnAnchor> byWall = floor.Anchors.Where(a => a != null);
            if (requireWallAdjacency && grid != null)
                byWall = byWall.Where(a => grid.CountBorders(a.cellIndex, BorderLabel.Wall) > 0);

            // Prop nunca em corredor (região 0) — só dentro de sala (região >= FirstRoomRegion).
            // Pillar/WallProp ficam de fora desta regra de propósito.
            if (propsOnlyInRooms && grid != null)
                byWall = byWall.Where(a => a.kind != SpawnAnchorKind.Prop
                                        || grid.GetRegion(a.cellIndex) >= AnnotatedGrid.FirstRoomRegion);

            if (maxPerRoom <= 0 || grid == null) return byWall.ToList();

            var byRegion = new Dictionary<int, List<SpawnAnchor>>();
            foreach (SpawnAnchor a in byWall)
            {
                int region = grid.GetRegion(a.cellIndex);
                if (!byRegion.TryGetValue(region, out List<SpawnAnchor> list))
                {
                    list = new List<SpawnAnchor>();
                    byRegion[region] = list;
                }
                list.Add(a);
            }

            var result = new List<SpawnAnchor>();
            foreach (KeyValuePair<int, List<SpawnAnchor>> pair in byRegion)
            {
                List<SpawnAnchor> list = pair.Value;

                // Corredor (região 0) fica de fora do teto — é a malha inteira, não "uma sala";
                // aplicar o mesmo número ali zeraria corredor comprido. Mesmo critério do
                // BasicLightingPopulator (PromoteUnderlitRooms/EnforceWallLightBounds).
                bool isRoom = pair.Key >= AnnotatedGrid.FirstRoomRegion;
                if (!isRoom || list.Count <= maxPerRoom)
                {
                    result.AddRange(list);
                    continue;
                }

                Shuffle(list, rng);
                result.AddRange(list.Take(maxPerRoom));
            }

            return result;
        }

        /// <summary>Fisher-Yates com o IRandom do próprio populator — determinístico por seed.</summary>
        private static void Shuffle<T>(IList<T> list, IRandom rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.NextInt(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public void Clear()
        {
            foreach (GameObject go in spawned)
                if (go != null) Destroy(go);
            spawned.Clear();
        }

        private static GameObject PickWeighted(List<WeightedPrefab> variants, IRandom rng)
        {
            float total = 0f;
            foreach (WeightedPrefab v in variants)
                if (v != null && v.prefab != null) total += v.weight;

            if (total <= 0f) return null;

            double roll = rng.NextDouble() * total;
            foreach (WeightedPrefab v in variants)
            {
                if (v == null || v.prefab == null) continue;
                roll -= v.weight;
                if (roll <= 0.0) return v.prefab;
            }

            // Sobra de ponto flutuante: devolve a última válida em vez de null.
            for (int i = variants.Count - 1; i >= 0; i--)
                if (variants[i]?.prefab != null) return variants[i].prefab;
            return null;
        }
    }
}
