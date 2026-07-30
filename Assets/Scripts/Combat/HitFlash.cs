using UnityEngine;

namespace Babel.Combat
{
    // Pisca o material ao levar dano. Só depende de HealthComponent —
    // qualquer coisa com vida ganha o feedback anexando este componente,
    // sem precisar ser um EnemyDummy (mesma ideia do KnockbackReceiver).
    //
    // A lógica saiu de EnemyDummy, que já fazia isso mas amarrado a ele:
    // EnemyDummy é placeholder de Fase 1 e tende a ser substituído pelo
    // EnemyBase de verdade na Fase 3 (ver o guia de migração), enquanto
    // este componente sobrevive à troca. Também evita ter que arrastar o
    // OnTriggerStay de dano por contato do EnemyDummy (que exige collider
    // Is Trigger) só pra conseguir o flash.
    //
    // Usa MaterialPropertyBlock em vez de mexer em Renderer.material: aquele
    // instancia uma cópia do material por objeto (vaza uma instância por
    // inimigo e quebra batching); o property block altera só a instância de
    // desenho, sem tocar no asset nem criar cópia.
    [RequireComponent(typeof(HealthComponent))]
    public class HitFlash : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Color flashColor = Color.red;
        [SerializeField] private float flashDuration = 0.15f;
        // URP/Lit expõe a cor como _BaseColor (o _Color do Built-in não
        // existe lá). Serializado pra funcionar com shader custom depois.
        [SerializeField] private string colorProperty = "_BaseColor";

        private HealthComponent health;
        private MaterialPropertyBlock block;
        private int colorId;
        private Color baseColor;
        private float remaining;

        private void Awake()
        {
            health = GetComponent<HealthComponent>();

            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<Renderer>();
            }

            colorId = Shader.PropertyToID(colorProperty);

            if (targetRenderer != null)
            {
                block = new MaterialPropertyBlock();
                // sharedMaterial (não .material) — só lendo o valor autorado,
                // sem instanciar nada.
                baseColor = targetRenderer.sharedMaterial != null
                    && targetRenderer.sharedMaterial.HasProperty(colorId)
                        ? targetRenderer.sharedMaterial.GetColor(colorId)
                        : Color.white;
            }
        }

        private void OnEnable()
        {
            health.OnDamaged += HandleDamaged;
        }

        private void OnDisable()
        {
            health.OnDamaged -= HandleDamaged;
            ApplyColor(baseColor);
        }

        private void HandleDamaged(float current, float max)
        {
            // Hit novo no meio de um flash só reinicia o timer — sem
            // corrotina pra cancelar, sem risco de uma antiga restaurar a
            // cor por cima da nova.
            remaining = flashDuration;
            ApplyColor(flashColor);
        }

        private void Update()
        {
            if (remaining <= 0f)
            {
                return;
            }

            remaining -= Time.deltaTime;
            if (remaining <= 0f)
            {
                ApplyColor(baseColor);
            }
        }

        // Deixa a cor de "morto" (ou qualquer outro estado) virar a nova cor
        // de repouso, pra um flash posterior não restaurar a cor antiga.
        public void SetBaseColor(Color color)
        {
            baseColor = color;
            if (remaining <= 0f)
            {
                ApplyColor(color);
            }
        }

        private void ApplyColor(Color color)
        {
            if (targetRenderer == null || block == null)
            {
                return;
            }

            targetRenderer.GetPropertyBlock(block);
            block.SetColor(colorId, color);
            targetRenderer.SetPropertyBlock(block);
        }
    }
}
