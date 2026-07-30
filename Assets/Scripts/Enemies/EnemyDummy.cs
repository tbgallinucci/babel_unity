using System.Collections;
using Babel.Combat;
using UnityEngine;

namespace Babel.Enemies
{
    // Placeholder de Fase 1 (guia de migração) — um "punching bag" parado,
    // sem NavMeshAgent nem state machine de IA. Isso é a Fase 3
    // ("Inimigos e IA", EnemyBase real). Existe só pra dar um alvo de
    // verdade (com vida e dano de contato) pro combate do player, que até
    // aqui só tinha dummies inertes de Targetable. Quando a Fase 3 chegar,
    // este script tende a ser substituído pelo EnemyBase de verdade —
    // HealthComponent e Targetable é que continuam sendo reaproveitados.
    [RequireComponent(typeof(HealthComponent))]
    [RequireComponent(typeof(Targetable))]
    [RequireComponent(typeof(Rigidbody))]
    public class EnemyDummy : MonoBehaviour
    {
        [SerializeField] private float contactDamage = 10f;
        [SerializeField] private float contactDamageInterval = 1f;
        [SerializeField] private Color hitFlashColor = Color.red;
        [SerializeField] private float hitFlashDuration = 0.15f;
        [SerializeField] private Color deadColor = Color.gray;

        private HealthComponent health;
        private Renderer bodyRenderer;
        private Color baseColor;
        private Coroutine flashRoutine;
        private float contactCooldownRemaining;

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
            bodyRenderer = GetComponentInChildren<Renderer>();

            if (bodyRenderer != null)
            {
                baseColor = bodyRenderer.material.color;
            }
        }

        private void OnEnable()
        {
            health.OnDamaged += HandleDamaged;
            health.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            health.OnDamaged -= HandleDamaged;
            health.OnDeath -= HandleDeath;
        }

        private void Update()
        {
            if (contactCooldownRemaining > 0f)
            {
                contactCooldownRemaining -= Time.deltaTime;
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (!health.IsAlive || contactCooldownRemaining > 0f)
            {
                return;
            }

            HealthComponent playerHealth = other.GetComponentInParent<HealthComponent>();
            if (playerHealth == null || playerHealth == health)
            {
                return;
            }

            playerHealth.TakeDamage(contactDamage);
            contactCooldownRemaining = contactDamageInterval;
        }

        private void HandleDamaged(float current, float max)
        {
            if (bodyRenderer == null)
            {
                return;
            }

            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
            }

            flashRoutine = StartCoroutine(FlashHit());
        }

        private void HandleDeath()
        {
            GetComponent<Collider>().enabled = false;

            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
            }

            if (bodyRenderer != null)
            {
                bodyRenderer.material.color = deadColor;
            }
        }

        private IEnumerator FlashHit()
        {
            bodyRenderer.material.color = hitFlashColor;
            yield return new WaitForSeconds(hitFlashDuration);
            bodyRenderer.material.color = baseColor;
            flashRoutine = null;
        }
    }
}
