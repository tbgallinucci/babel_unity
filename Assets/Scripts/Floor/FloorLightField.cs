// ============================================================================
//  FloorLightField.cs  —  código do JOGO (Decisão 4: fora do plugin)
//
//  Etapa 3: propaga a luz de cada tocha pelo GRID do WFC (LightFieldBaker, no
//  plugin) e publica o resultado como uma Texture2D global — sampleada pelo
//  shader de tela cheia (FloorLightField.shader, via FullScreenPassRendererFeature
//  no Renderer) que soma essa cor em cima de QUALQUER material da cena, sem
//  precisar trocar shader de nenhum objeto.
//
//  Por que Texture2D (não Texture3D): todo FloorSpec deste projeto usa
//  gridSize.y = 1 (andar plano — ver FloorSpec_Demo.asset). O domínio do
//  flood-fill é efetivamente 2D. Se um dia existir andar com Y > 1, dá pra
//  trocar a textura por 3D sem mexer no LightFieldBaker (ele já devolve por
//  célula 3D) — só a montagem da textura aqui precisaria mudar.
//
//  Fonte de verdade da cor/alcance de cada seed: a própria Light (Fill, de
//  preferência — é a que "vê a sala inteira") de cada TorchLight. Não duplica
//  tuning num lugar segundo: subiu o Range do Fill no prefab, o bounce
//  acompanha sozinho.
// ============================================================================

using System.Collections.Generic;
using Babel.VFX;
using UnityEngine;
using WFC.Runtime;

namespace Babel.Floor
{
    [AddComponentMenu("Babel/Floor Light Field")]
    public sealed class FloorLightField : MonoBehaviour
    {
        [Tooltip("Teto de passos de BFS por tocha. O falloff por distância já deixa a " +
                 "contribuição quase zero bem antes disso na configuração padrão — isto é " +
                 "só uma rede de segurança de custo, raramente precisa mexer.")]
        [Min(1)] [SerializeField] private int maxStepsPerSeed = 12;

        [Tooltip("Multiplica o Range da luz de origem de cada tocha pra decidir até onde o " +
                 "BOUNCE (a luz propagada) alcança. 1 = mesmo alcance da luz direta que gerou.")]
        [Min(0.1f)] [SerializeField] private float rangeMultiplier = 1f;

        [Tooltip("Domina o resultado inteiro — comece BAIXO (0.02–0.08) e suba olhando a cena, " +
                 "uma tacada de cada vez. Light.intensity em URP é unidade física (Candela " +
                 "neste projeto), não 0..1: sem atenuar aqui o 'ambiente' fica mais forte que " +
                 "a luz direta que o originou.")]
        [Min(0f)] [SerializeField] private float intensityMultiplier = 0.05f;

        [Tooltip("Quanto o bounce herda a SATURAÇÃO da cor da tocha. 1 = cor crua da chama " +
                 "(o que faz a cena inteira tender pro amarelo/laranja puro, sem contraste). " +
                 "0 = bounce vira cinza (só intensidade, sem tingir nada — o oposto do problema, " +
                 "perde o clima quente). Bounce de verdade rebate numa parede cinza e perde parte " +
                 "da cor da fonte — comece por volta de 0.4–0.6, é o valor que mantém 'sala " +
                 "aquecida por tocha' sem virar monocromático.")]
        [Range(0f, 1f)] [SerializeField] private float colorSaturation = 0.5f;

        [Tooltip("DIAGNÓSTICO: Bilinear interpola entre a célula sob o pixel e as vizinhas — é o " +
                 "que dá o gradiente suave, mas SANGRA um pouco pra qualquer célula vizinha, " +
                 "parede ou não (a textura não sabe de parede depois de pronta, só guarda cor). " +
                 "Point corta a interpolação por completo: cada pixel pega só o valor puro da " +
                 "própria célula, corte duro nas bordas, sem gradiente nenhum. Troque pra Point " +
                 "e olhe o mesmo ponto que estava vazando — se sumir, a causa era esta.")]
        [SerializeField] private FilterMode textureFilterMode = FilterMode.Bilinear;

        [SerializeField] private bool logSummary = true;

        private static readonly int TextureId = Shader.PropertyToID("_FloorLightField");
        private static readonly int ParamsId = Shader.PropertyToID("_FloorLightFieldParams");

        private Texture2D texture;
        private readonly List<TorchLight> torchBuffer = new List<TorchLight>();
        private readonly List<LightFieldBaker.Seed> seedBuffer = new List<LightFieldBaker.Seed>();

        // Não [SerializeField] de propósito: é só cache de runtime pro OnValidate poder
        // recalcular, não é estado que devesse sobreviver a serialização/prefab.
        private GeneratedFloor lastFloor;

        /// <summary>
        /// Recalcula o campo a partir das tochas já plantadas no andar. Chame DEPOIS do
        /// BasicLightingPopulator — sem tocha nenhuma em cena o campo sai vazio (não é erro,
        /// só não tem o que propagar ainda).
        /// </summary>
        public void Rebuild(GeneratedFloor floor)
        {
            lastFloor = floor;

            if (floor == null || !floor.Success || floor.Root == null || floor.Grid == null)
            {
                Clear();
                return;
            }

            AnnotatedGrid grid = floor.Grid;

            torchBuffer.Clear();
            floor.Root.GetComponentsInChildren(true, torchBuffer);

            seedBuffer.Clear();
            foreach (TorchLight torch in torchBuffer)
            {
                if (torch == null) continue;

                // Fill de preferência: é a luz sem sombra que já cobre a sala inteira, o
                // alcance mais representativo de "até onde esta tocha ilumina". Key só entra
                // como fallback se a tocha não tiver fill nenhum (prefab incomum/quebrado).
                Light source = torch.FillLight != null ? torch.FillLight : torch.KeyLight;
                if (source == null) continue;

                int cell = RegionGraph.WorldToCell(grid, torch.Anchor.position);
                if (cell < 0) continue;

                Color desaturated = DesaturateTowardLuminance(source.color, colorSaturation);
                Color color = desaturated * (source.intensity * intensityMultiplier);
                float range = source.range * rangeMultiplier;

                seedBuffer.Add(new LightFieldBaker.Seed(cell, color, range));
            }

            Color[] cellColors = LightFieldBaker.Bake(grid, seedBuffer, maxStepsPerSeed);
            PublishTexture(grid, cellColors);

            if (logSummary)
                Debug.Log($"[FloorLightField] Andar {floor.FloorNumber}: {seedBuffer.Count} tocha(s) " +
                          $"propagada(s) por {grid.Grid.CellCount} célula(s).", this);
        }

        /// <summary>
        /// Zera a contribuição global. Não existe "desligar a feature" por andar — o
        /// FullScreenPassRendererFeature é global no Renderer — então isto garante que, sem
        /// andar gerado (ou entre a destruição de um e a geração do próximo), a tela não
        /// continua somando o campo do andar anterior por cima do que quer que apareça.
        /// </summary>
        public void Clear()
        {
            lastFloor = null;
            Shader.SetGlobalTexture(TextureId, Texture2D.blackTexture);
        }

        /// <summary>
        /// Dispara sozinho quando QUALQUER campo muda pelo Inspector — inclusive em Play Mode.
        /// Sem isto, arrastar o slider de Intensity/Range/Saturation só mudava o valor guardado;
        /// nada recalculava até o andar inteiro ser gerado de novo (RegenerateCurrentFloor), o
        /// que também re-rola prop/inimigo — pesado demais só pra testar um número de luz.
        ///
        /// Reentra Rebuild(lastFloor) direto — seguro porque `lastFloor` é campo puro (sem
        /// [SerializeField]), escrevê-lo dentro de Rebuild não passa pelo sistema serializado
        /// do Editor e não re-dispara este mesmo OnValidate.
        /// </summary>
        private void OnValidate()
        {
            if (lastFloor == null) return;
            Rebuild(lastFloor);
        }

        /// <summary>
        /// Mistura a cor com o próprio brilho dela em escala de cinza (Color.grayscale — a
        /// luma perceptual do Unity). saturation=1 devolve a cor original; 0 devolve cinza
        /// puro (mesmo brilho, sem matiz nenhum). É a aproximação mais barata de "essa luz
        /// rebateu numa parede cinza antes de chegar aqui" sem precisar amostrar albedo de
        /// verdade — o greybox já É quase cinza uniforme, então a aproximação é praticamente
        /// exata pra este kit.
        /// </summary>
        private static Color DesaturateTowardLuminance(Color color, float saturation)
        {
            float luminance = color.grayscale;
            var grey = new Color(luminance, luminance, luminance, color.a);
            return Color.Lerp(grey, color, saturation);
        }

        private void PublishTexture(AnnotatedGrid grid, Color[] cellColors)
        {
            int width = grid.Grid.SizeX;
            int height = grid.Grid.SizeZ;

            if (texture == null || texture.width != width || texture.height != height)
            {
                if (texture != null) Destroy(texture);

                // Half-float: as cores somadas não são 0..1 (Light.intensity em Candela pode
                // passar de 1 fácil, mesmo depois do intensityMultiplier) — um formato 8 bits
                // clamparia e cortaria a cauda alta do falloff.
                texture = new Texture2D(width, height, TextureFormat.RGBAHalf, false, true)
                {
                    name = "FloorLightField",
                    wrapMode = TextureWrapMode.Clamp,
                };
            }

            // Fora do `if` de cima de propósito: filterMode precisa reagir a mudança de Inspector
            // mesmo quando a textura já existe e não precisa ser recriada (só recalculada).
            texture.filterMode = textureFilterMode;

            // Soma as camadas Y na mesma textura XZ — ver o comentário de cabeçalho sobre por
            // que 2D basta hoje. Com SizeY=1 (o caso de sempre neste projeto) este laço interno
            // roda uma vez só; o código já sai pronto pro dia que deixar de ser 1.
            var pixels = new Color[width * height];
            for (int z = 0; z < height; z++)
            for (int x = 0; x < width; x++)
            {
                Color sum = Color.clear;
                for (int y = 0; y < grid.Grid.SizeY; y++)
                    sum += cellColors[grid.Grid.Index(x, y, z)];

                pixels[z * width + x] = sum;
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);

            Vector3 origin = grid.Origin;
            float worldSizeX = grid.Grid.SizeX * grid.CellSize;
            float worldSizeZ = grid.Grid.SizeZ * grid.CellSize;

            Shader.SetGlobalTexture(TextureId, texture);
            // xy = canto do grid em mundo, zw = 1/tamanho — o shader faz
            // (worldPos.xz - params.xy) * params.zw pra virar UV 0..1. Mesma convenção de
            // AnnotatedGrid.CellToWorld (célula 0,0,0 centrada em Origin + meia célula), então
            // o centro do texel bate com o centro da célula sem nenhum ajuste extra.
            Shader.SetGlobalVector(ParamsId, new Vector4(origin.x, origin.z, 1f / worldSizeX, 1f / worldSizeZ));
        }
    }
}
