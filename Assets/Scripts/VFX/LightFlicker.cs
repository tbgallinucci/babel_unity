// ============================================================================
//  LightFlicker.cs  —  código do JOGO
//
//  Faz um Light oscilar de intensidade (e opcionalmente Range), pra imitar
//  chama de tocha. Não sabe nada de WFC/andar/populador — é um componente
//  genérico, funciona em qualquer Light da cena, plantado ou não pelo
//  BasicLightingPopulator.
//
//  Convive de graça com o DynamicLightBudget: aquele só liga/desliga
//  Light.enabled; este script continua rodando por baixo (o Update aqui é
//  irrelevante em custo, poucas dezenas de luzes bater PerlinNoise 1x por
//  frame não é o gargalo de nada) — quando o budget religa a luz, ela já
//  reaparece na fase certa da oscilação, sem "reiniciar" o efeito.
// ============================================================================

using UnityEngine;

namespace Babel.VFX
{
    [RequireComponent(typeof(Light))]
    [AddComponentMenu("Babel/Light Flicker")]
    public sealed class LightFlicker : MonoBehaviour
    {
        public enum FlickerMode
        {
            /// <summary>Oscilação suave e orgânica (Perlin noise) — bom pra tocha "calma".</summary>
            Perlin,

            /// <summary>Passos aleatórios com suavização — bom pra tocha "ventando", mais nervoso.</summary>
            Random,
        }

        [SerializeField] private FlickerMode mode = FlickerMode.Perlin;

        [Tooltip("Intensidade base (100%). 0 = usa a Intensity atual do Light no Awake, então normalmente " +
                 "não precisa mexer aqui — só se quiser um valor base diferente do que está no componente.")]
        [SerializeField] private float baseIntensity = 0f;

        [Tooltip("Quanto a intensidade oscila pra cima/baixo, em fração da base. 0.25 = varia entre 75% e 125%.")]
        [Range(0f, 1f)] [SerializeField] private float intensityVariance = 0.25f;

        [Tooltip("Também oscila o Range, na mesma proporção — dá a sensação de a chama 'respirar', não só " +
                 "clarear/escurecer no lugar. Opcional porque tem um custo extra de recalcular sombra.")]
        [SerializeField] private bool affectRange = false;
        [Range(0f, 1f)] [SerializeField] private float rangeVariance = 0.1f;

        [Tooltip("Velocidade da oscilação.")]
        [Min(0.01f)] [SerializeField] private float speed = 1.5f;

        [Tooltip("Só no modo Random: suavização entre um alvo sorteado e o próximo. 0 = troca instantânea " +
                 "(tremido nervoso), perto de 1 = quase sem variação perceptível.")]
        [Range(0f, 0.98f)] [SerializeField] private float smoothing = 0.6f;

        private Light lightComp;
        private float noiseSeed;
        private float randomTarget = 1f;
        private float smoothedFactor = 1f;
        private float baseRange;

        /// <summary>
        /// Fator externo (0..1) multiplicado por cima da oscilação — é por aqui que o
        /// DynamicLightBudget desvanece a tocha nos últimos metros antes de apagá-la.
        ///
        /// Precisa passar por este componente, e não direto no Light: o Update daqui
        /// reescreve `intensity` todo frame, então quem escrevesse direto seria
        /// sobrescrito no frame seguinte e o fade simplesmente não apareceria.
        /// </summary>
        public float IntensityScale { get; set; } = 1f;

        private void Awake()
        {
            lightComp = GetComponent<Light>();
            if (baseIntensity <= 0f) baseIntensity = lightComp.intensity;
            baseRange = lightComp.range;

            // Seed por instância, não por seed do andar: é puramente cosmético e sem isso
            // toda tocha da mesma sala piscaria em uníssono — bem mais artificial que cada
            // uma variar por conta própria. Não precisa (nem deve) ser determinístico.
            noiseSeed = UnityEngine.Random.Range(0f, 1000f);
        }

        private void Update()
        {
            float factor = mode == FlickerMode.Perlin ? PerlinFactor() : RandomFactor();

            lightComp.intensity = baseIntensity * factor * IntensityScale;
            if (affectRange) lightComp.range = baseRange * Mathf.Lerp(1f, factor, rangeVariance / Mathf.Max(0.0001f, intensityVariance));
        }

        private float PerlinFactor()
        {
            float n = Mathf.PerlinNoise(noiseSeed, Time.time * speed); // 0..1
            return 1f + (n * 2f - 1f) * intensityVariance;
        }

        private float RandomFactor()
        {
            // A cada frame, chance de sortear um novo alvo — a chance por frame (não um timer
            // fixo) é o que evita um "tique" perceptível toda vez que o intervalo estoura.
            if (UnityEngine.Random.value < speed * Time.deltaTime)
                randomTarget = 1f + UnityEngine.Random.Range(-intensityVariance, intensityVariance);

            smoothedFactor = Mathf.Lerp(smoothedFactor, randomTarget, 1f - smoothing);
            return smoothedFactor;
        }
    }
}
