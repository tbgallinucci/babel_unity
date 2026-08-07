// ============================================================================
//  TorchLight.cs  —  código do JOGO
//
//  Trata uma tocha como UMA COISA SÓ, apesar de ela ser duas Lights.
//
//  Por que são duas: um Spot estreito (~110°) é o único que consegue projetar
//  sombra decente, porque a densidade de texel do shadow map segue
//
//      texel ≈ 2 · tan(outer/2) · range / resolução
//
//  e `tan(outer/2)` explode perto de 180° (a 170° vale 11,4; a 110°, 1,43 —
//  8× mais barato por metro de alcance). Mas um cone estreito não preenche a
//  sala. Então o Spot cuida da OCLUSÃO perto da tocha e um Point largo SEM
//  sombra cuida do PREENCHIMENTO. É o padrão key + fill.
//
//  Este componente existe porque o orçamento de luz precisa raciocinar sobre
//  "tochas", não sobre "Lights". Sem ele o DynamicLightBudget coletava todo
//  Light sob o andar e ordenava por distância — e como as duas luzes da mesma
//  tocha são dois itens da lista, o corte do orçamento caía NO MEIO de uma
//  tocha: o Spot ligado e o Point desligado (ou o contrário). É essa a causa
//  de "algumas tochas acendem pela metade, de forma imprevisível conforme o
//  jogador anda".
// ============================================================================

using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Babel.VFX
{
    /// <summary>Quanto uma tocha custa neste frame. Quem decide é o DynamicLightBudget.</summary>
    public enum TorchTier
    {
        /// <summary>Apagada por completo.</summary>
        Off,

        /// <summary>Acesa, mas sem projetar sombra. É o estado da maioria das tochas.</summary>
        Fill,

        /// <summary>Acesa e projetando sombra. Reservado às poucas mais próximas do jogador.</summary>
        Key,
    }

    /// <summary>Qual das duas luzes da tocha projeta sombra quando o tier é Key.</summary>
    public enum ShadowCasterMode
    {
        /// <summary>Só o Spot (keyLight) projeta sombra. Padrão — o mais barato dos que dão oclusão.</summary>
        KeyOnly,

        /// <summary>Só o Point (fillLight) projeta sombra. Inverte o padrão — teste de vazamento por parede.</summary>
        FillOnly,

        /// <summary>As duas projetam sombra. Mais caro: soma o atlas do Spot com o do Point.</summary>
        Both,

        /// <summary>Nenhuma projeta sombra, nem no tier Key. Mais barato; sem oclusão nenhuma.</summary>
        Neither,
    }

    [AddComponentMenu("Babel/Torch Light")]
    public sealed class TorchLight : MonoBehaviour
    {
        [Tooltip("Spot estreito que projeta sombra quando a tocha está no tier Key. " +
                 "Vazio = procura um Light do tipo Spot nos filhos no Awake.")]
        [SerializeField] private Light keyLight;

        [Tooltip("Point largo que preenche a sala. NUNCA projeta sombra — sombra de Point custa " +
                 "6 slices de atlas contra 1 do Spot. Vazio = procura um Light do tipo Point nos filhos.")]
        [SerializeField] private Light fillLight;

        [Tooltip("Qualidade da sombra do Spot quando ele está projetando (KeyOnly ou Both).")]
        [SerializeField] private LightShadows keyShadows = LightShadows.Soft;

        [Tooltip("Qualidade da sombra do Point quando ele está projetando (FillOnly ou Both). " +
                 "Sombra de Point custa 6 slices de atlas contra 1 do Spot — ligar isto é " +
                 "pesado, use só pra testar se resolve o vazamento de luz por parede.")]
        [SerializeField] private LightShadows fillShadows = LightShadows.Soft;

        [Tooltip("Graus que o Spot inclina para BAIXO. Não é enfeite: o anchor de parede aponta " +
                 "na horizontal, então o chão recebe a luz em ângulo rasante — e é aí que o depth " +
                 "bias falha e a acne aparece como listras pretas no piso. Inclinar aproxima a " +
                 "direção da luz da normal do chão e a acne colapsa, permitindo baixar o bias. " +
                 "25–35 costuma bastar. 0 = mantém a horizontal do anchor.")]
        [Range(0f, 80f)] [SerializeField] private float keyPitchDegrees = 30f;

        [Tooltip("Qual(is) luz(es) da tocha projeta(m) sombra quando o tier é Key. No tier Fill " +
                 "nenhuma das duas projeta sombra, sempre — isto só controla o comportamento " +
                 "das tochas mais perto do jogador. Use pra testar o vazamento de luz do Point " +
                 "por parede: KeyOnly = padrão (Spot sombra, Point preenche sem sombra, mais " +
                 "barato); FillOnly = inverte (Point ganha sombra, Spot vira puro preenchimento); " +
                 "Both = as duas projetam (mais caro: soma o custo de atlas das duas); Neither = " +
                 "nenhuma projeta mesmo no tier Key (mais barato ainda, sem oclusão nenhuma).")]
        [SerializeField] private ShadowCasterMode shadowCasterMode = ShadowCasterMode.KeyOnly;

        private LightFlicker keyFlicker;
        private LightFlicker fillFlicker;

        private float keyBaseIntensity;
        private float fillBaseIntensity;

        private TorchTier tier = TorchTier.Off;
        private float dim = 1f;
        private bool awake;

        /// <summary>
        /// Região (sala/corredor) do AnnotatedGrid em que a tocha nasceu. Preenchida pelo
        /// BasicLightingPopulator. É o que permite conter a luz na própria sala via
        /// rendering layers — ver RegionLightMask.
        /// </summary>
        public int Region { get; set; } = -1;

        public TorchTier Tier => tier;

        /// <summary>Só para o orçamento medir distância sem pagar GetComponent toda avaliação.</summary>
        public Transform Anchor => transform;

        /// <summary>
        /// Acesso de leitura às duas luzes — usado pelo FloorLightField pra semear o flood-fill
        /// com a MESMA cor/intensidade/range que a tocha já usa, sem duplicar tuning num lugar
        /// segundo. `EnsureAwake()` aqui é defensivo: se algo ler isto antes do Awake rodar
        /// (não deveria acontecer — Instantiate() chama Awake sincronamente —, mas é grátis
        /// garantir), a auto-descoberta ainda dispara na hora.
        /// </summary>
        public Light KeyLight { get { EnsureAwake(); return keyLight; } }
        public Light FillLight { get { EnsureAwake(); return fillLight; } }

        private void Awake() => EnsureAwake();

        private void EnsureAwake()
        {
            if (awake) return;
            awake = true;

            // Auto-descoberta pelo TIPO da luz, não pela ordem dos filhos: o prefab pode ser
            // remontado (o Point já foi filho do Spot e pode virar irmão) sem quebrar isto.
            if (keyLight == null || fillLight == null)
            {
                foreach (Light l in GetComponentsInChildren<Light>(true))
                {
                    if (keyLight == null && l.type == LightType.Spot) keyLight = l;
                    else if (fillLight == null && l.type == LightType.Point) fillLight = l;
                }
            }

            if (keyLight != null)
            {
                keyBaseIntensity = keyLight.intensity;
                keyFlicker = keyLight.GetComponent<LightFlicker>();

                // Aplicado aqui, em runtime, e não assado no prefab: o Spot está na RAIZ, e a
                // rotação da raiz é sobrescrita pelo Instantiate do populador (que usa a rotação
                // do anchor). Inclinar no prefab não sobreviveria. Rodar uma vez no Awake, depois
                // do Instantiate, sobrevive — e o guard `awake` garante que é uma vez só.
                //
                // Eixo calculado no MUNDO (forward × down), não Euler em X LOCAL: os anchors
                // Anchor_Light_E/Anchor_Light_W nascem espelhados (Quaternion.LookRotation com
                // 'inward' oposto — Vector3.right vs Vector3.left), e isso inverte o eixo X local
                // de um deles em relação ao mundo. Girar por Euler(pitch,0,0) em cima de um X que
                // pode estar espelhado inclinava o Spot pro chão de um lado e pra parede/teto do
                // outro — era esse o "foco que se une" em alguns lugares (spot mirando errado
                // convergindo com o cone do vizinho).
                //
                // O sinal do ângulo é verificado e corrigido sozinho (compara o Y do forward antes
                // e depois) em vez de confiar de cabeça na convenção de sinal do AngleAxis — assim
                // o resultado é "sempre inclina pra baixo" garantido, não uma aposta de sinal.
                if (keyPitchDegrees > 0f)
                {
                    Vector3 forward = keyLight.transform.forward;
                    Vector3 axis = Vector3.Cross(forward, Vector3.down);
                    if (axis.sqrMagnitude > 0.0001f)
                    {
                        axis.Normalize();
                        Quaternion pitch = Quaternion.AngleAxis(keyPitchDegrees, axis);
                        if ((pitch * forward).y > forward.y) pitch = Quaternion.AngleAxis(-keyPitchDegrees, axis);
                        keyLight.transform.rotation = pitch * keyLight.transform.rotation;
                    }
                }
            }

            if (fillLight != null)
            {
                fillBaseIntensity = fillLight.intensity;
                fillFlicker = fillLight.GetComponent<LightFlicker>();
            }

            // Não força mais shadows=None aqui: quem decide agora é o Apply(), a cada tier,
            // olhando shadowCasterMode. Antes isto sobrescrevia o Shadow Type do prefab/Inspector
            // de volta pra None uma vez só no Awake — e o Apply nunca tocava fillLight.shadows,
            // então o Point ficava permanentemente sem sombra e mudar o campo no Editor não
            // tinha efeito nenhum visível.
        }

        // =====================================================================
        //  Orçamento
        // =====================================================================

        /// <summary>
        /// Aplica tier e atenuação. As duas luzes andam SEMPRE juntas — nunca meia tocha.
        /// </summary>
        /// <param name="newTier">Off / Fill / Key.</param>
        /// <param name="intensityScale">
        /// 0..1. Usado para desvanecer a tocha nos últimos metros antes do corte, em vez de
        /// apagá-la de uma vez: um sumiço instantâneo chama muito mais atenção que a ausência.
        /// </param>
        public void Apply(TorchTier newTier, float intensityScale)
        {
            EnsureAwake();

            tier = newTier;
            dim = Mathf.Clamp01(intensityScale);

            bool on = newTier != TorchTier.Off;

            // Sombra só existe no tier Key (o Fill nunca projeta, pra manter a maioria das
            // tochas barata). Dentro do tier Key, shadowCasterMode escolhe qual das duas —
            // ou as duas, ou nenhuma — de fato projeta.
            bool keyCasts = newTier == TorchTier.Key &&
                (shadowCasterMode == ShadowCasterMode.KeyOnly || shadowCasterMode == ShadowCasterMode.Both);
            bool fillCasts = newTier == TorchTier.Key &&
                (shadowCasterMode == ShadowCasterMode.FillOnly || shadowCasterMode == ShadowCasterMode.Both);

            if (keyLight != null)
            {
                keyLight.enabled = on;
                keyLight.shadows = keyCasts ? keyShadows : LightShadows.None;
                PushIntensity(keyLight, keyFlicker, keyBaseIntensity);
            }

            if (fillLight != null)
            {
                fillLight.enabled = on;
                fillLight.shadows = fillCasts ? fillShadows : LightShadows.None;
                PushIntensity(fillLight, fillFlicker, fillBaseIntensity);
            }
        }

        /// <summary>
        /// O LightFlicker reescreve `intensity` todo Update. Se o orçamento também escrevesse
        /// direto, um sobrescreveria o outro e o fade não apareceria (ou apareceria tremido).
        /// Por isso: onde existe flicker, o fator vai por ele; onde não existe, vai direto.
        /// </summary>
        private void PushIntensity(Light light, LightFlicker flicker, float baseIntensity)
        {
            if (flicker != null) flicker.IntensityScale = dim;
            else light.intensity = baseIntensity * dim;
        }

        // =====================================================================
        //  Contenção de luz por região
        // =====================================================================

        /// <summary>
        /// Restringe quais renderers esta tocha ilumina. Aplicado nas DUAS luzes: o vazamento
        /// de parede vem sobretudo do fill, que por definição não tem sombra para bloqueá-lo.
        /// </summary>
        public void SetRenderingLayerMask(uint mask)
        {
            EnsureAwake();
            ApplyMask(keyLight, mask);
            ApplyMask(fillLight, mask);
        }

        private static void ApplyMask(Light light, uint mask)
        {
            if (light == null) return;

            var data = light.GetComponent<UniversalAdditionalLightData>();
            if (data == null) return;

            // É `UniversalAdditionalLightData.renderingLayers` que o URP lê ao montar as
            // constantes de luz (ForwardLights.InitializeLightConstants) — escrever direto em
            // Light.renderingLayerMask seria sobrescrito por SyncLightAndShadowLayers().
            int bits = unchecked((int)mask);
            data.renderingLayers = bits;

            // A máscara de SOMBRA acompanha a de iluminação: separá-las produziria o pior dos
            // mundos — parede que recebe a luz mas não entra no shadow map dela.
            data.customShadowLayers = false;
            data.shadowRenderingLayers = bits;
        }
    }
}
