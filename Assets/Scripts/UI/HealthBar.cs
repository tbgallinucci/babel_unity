using Babel.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Babel.UI
{
    // Barra de vida genérica: o mesmo script serve pro player e pro inimigo,
    // igual ao HealthComponent que ele observa. Quem decide QUAL vida é essa
    // é o Inspector (player, fixo) ou outro script chamando Bind() em runtime
    // (inimigo travado — ver TargetHealthBar).
    //
    // Orientado a evento, não a polling: acompanha OnDamaged/OnHealed em vez
    // de ler CurrentHealth todo frame. A barra só recalcula quando a vida
    // realmente muda.
    //
    // O `fill` precisa ser um Image com Image Type = Filled (Fill Method
    // Horizontal), senão fillAmount não faz nada visualmente.
    public class HealthBar : MonoBehaviour
    {
        [SerializeField] private Image fill;

        // Opcional: deixe vazio pra uma barra que é preenchida por Bind() em
        // runtime (caso do inimigo). Preenchido = liga sozinho no Start
        // (caso do player).
        [SerializeField] private HealthComponent target;

        // 0 = barra pula direto pro valor novo. Acima disso, ela corre até lá
        // — dá leitura melhor de "quanto levei nesse golpe" do que um corte
        // seco, principalmente com o hit-stop congelando a tela junto.
        [SerializeField, Range(0f, 20f)] private float fillSpeed = 8f;

        private float displayedFill = 1f;
        private float targetFill = 1f;

        public HealthComponent Target => target;

        private void Start()
        {
            // Start, não Awake: HealthComponent inicializa CurrentHealth no
            // próprio Awake, e a ordem entre Awakes de objetos diferentes não
            // é garantida. Ler aqui garante que a vida inicial já existe.
            if (target != null)
            {
                Bind(target);
            }
        }

        private void OnDisable()
        {
            Unsubscribe(target);
        }

        // Troca (ou define) qual vida esta barra mostra. Passar null
        // desconecta — a barra para de acompanhar qualquer coisa, mas não
        // esconde nada por conta própria: quem chama decide a visibilidade
        // (mesma divisão de responsabilidade do LockOnReticle).
        public void Bind(HealthComponent health)
        {
            if (target != health)
            {
                Unsubscribe(target);
                target = health;
                Subscribe(target);
            }
            else if (target != null)
            {
                // Mesmo alvo: garante que não ficou sem inscrição depois de
                // um ciclo OnDisable/OnEnable.
                Unsubscribe(target);
                Subscribe(target);
            }

            if (target == null)
            {
                return;
            }

            // Rebind não anima: ao trocar de alvo a barra tem que já aparecer
            // cheia (ou no valor real daquele inimigo), não correr desde o
            // valor do alvo anterior.
            targetFill = Ratio(target.CurrentHealth, target.MaxHealth);
            displayedFill = targetFill;
            Apply(displayedFill);
        }

        private void Update()
        {
            if (fill == null || Mathf.Approximately(displayedFill, targetFill))
            {
                return;
            }

            if (fillSpeed <= 0f)
            {
                displayedFill = targetFill;
            }
            else
            {
                // unscaledDeltaTime: o HitStop zera Time.timeScale no instante
                // do acerto, que é exatamente quando a barra está animando.
                // Com deltaTime normal ela congelaria junto e só andaria
                // depois — o oposto do que a trava de impacto quer destacar.
                displayedFill = Mathf.MoveTowards(
                    displayedFill, targetFill, fillSpeed * Time.unscaledDeltaTime);
            }

            Apply(displayedFill);
        }

        private void Subscribe(HealthComponent health)
        {
            if (health == null)
            {
                return;
            }

            health.OnDamaged += HandleChanged;
            health.OnHealed += HandleChanged;
        }

        private void Unsubscribe(HealthComponent health)
        {
            if (health == null)
            {
                return;
            }

            health.OnDamaged -= HandleChanged;
            health.OnHealed -= HandleChanged;
        }

        private void HandleChanged(float current, float max)
        {
            targetFill = Ratio(current, max);
        }

        private static float Ratio(float current, float max)
        {
            return max > 0f ? Mathf.Clamp01(current / max) : 0f;
        }

        private void Apply(float value)
        {
            if (fill != null)
            {
                fill.fillAmount = value;
            }
        }
    }
}
