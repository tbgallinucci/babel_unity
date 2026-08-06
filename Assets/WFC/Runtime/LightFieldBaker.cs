// ============================================================================
//  LightFieldBaker.cs  —  camada WFC.Runtime
//
//  Etapa 3 da investigação de iluminação: flood-fill de luz pelo GRID, célula a
//  célula. É o que garante — POR CONSTRUÇÃO, não por rendering layer nem por
//  shadow map — que luz não atravessa parede: a busca só anda por bordas que
//  não são Wall (GetBorder), então uma parede sólida é, literalmente, o limite
//  da busca. Cobre o buraco que o RegionLightMask deixa (pilar no meio de uma
//  sala, contenção grossa por sala inteira) com granularidade de célula.
//
//  Puro: só grid + geometria. Não sabe o que é tocha, Light, TorchLight — quem
//  monta a lista de seeds (célula, cor, intensidade) é o jogo (FloorLightField).
//  Devolve uma cor por célula; o consumidor decide o que fazer com isso (aqui,
//  vira Texture2D amostrada num shader de tela cheia).
//
//  Por que BFS pra ALCANCE e distância EUCLIDIANA pro FALLOFF, não os dois por
//  BFS: usar só profundidade de busca pra atenuação dá um contorno "diamante"
//  (grid 4-conectado) visível como facetamento. BFS decide só QUEM é alcançável
//  (a topologia — respeita parede), a distância real decide QUANTO cada célula
//  recebe — combina a garantia geométrica com uma queda suave.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using WFC.Core;

namespace WFC.Runtime
{
    public static class LightFieldBaker
    {
        public readonly struct Seed
        {
            public readonly int cell;
            public readonly Color color;   // cor * intensidade já combinadas — o baker não sabe de Light
            public readonly float range;   // metros; além disso a contribuição desta seed é zero

            public Seed(int cell, Color color, float range)
            {
                this.cell = cell;
                this.color = color;
                this.range = range;
            }
        }

        /// <summary>
        /// Propaga cada seed pelas células abertas alcançáveis (sem cruzar Wall), soma as
        /// contribuições de todas as seeds por célula. Devolve um array do tamanho
        /// <c>grid.Grid.CellCount</c> — índice = célula linear do Grid3D, igual a todo o resto
        /// do contrato do plugin (AnnotatedGrid, GeneratedFloor.Variants etc.).
        /// </summary>
        /// <param name="maxStepsPerSeed">Teto de passos de BFS por seed — corta o custo; o
        /// falloff por distância já deixa a contribuição desprezível bem antes disso na
        /// maioria dos casos, isto é só uma rede de segurança de custo.</param>
        public static Color[] Bake(AnnotatedGrid grid, IReadOnlyList<Seed> seeds, int maxStepsPerSeed = 12)
        {
            var result = new Color[grid.Grid.CellCount];
            if (seeds == null || seeds.Count == 0) return result;

            Grid3D g = grid.Grid;
            var visited = new HashSet<int>();
            var queue = new Queue<(int cell, int steps)>();

            foreach (Seed seed in seeds)
            {
                if (seed.cell < 0 || seed.cell >= result.Length || seed.range <= 0f) continue;

                visited.Clear();
                queue.Clear();
                visited.Add(seed.cell);
                queue.Enqueue((seed.cell, 0));

                Vector3 seedWorld = grid.CellToWorld(seed.cell);

                while (queue.Count > 0)
                {
                    (int cell, int steps) = queue.Dequeue();

                    float dist = Vector3.Distance(grid.CellToWorld(cell), seedWorld);
                    float attenuation = Falloff(dist, seed.range);
                    if (attenuation > 0f)
                        result[cell] += seed.color * attenuation;

                    if (steps >= maxStepsPerSeed) continue;

                    foreach (Direction dir in Directions.Horizontal)
                    {
                        if (!g.TryGetNeighbor(cell, dir, out int neighbor)) continue;
                        if (visited.Contains(neighbor)) continue;

                        // A trava de verdade: só atravessa borda que NÃO é parede. Um cell fora
                        // de qualquer região playable (rocha) nunca tem borda Open/Door pra fora
                        // dela mesma, então rocha já bloqueia sozinha, de graça.
                        if (grid.GetBorder(cell, dir) == BorderLabel.Wall) continue;

                        visited.Add(neighbor);
                        queue.Enqueue((neighbor, steps + 1));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Suave, sem descontinuidade na borda do range (evita um "anel" visível onde a
        /// contribuição corta abruptamente). Quadrática: cai mais rápido perto do range do
        /// que no início — combina com o feeling de luz de tocha (poça definida, não um
        /// gradiente infinito).
        /// </summary>
        private static float Falloff(float distance, float range)
        {
            float t = Mathf.Clamp01(1f - distance / range);
            return t * t;
        }
    }
}
