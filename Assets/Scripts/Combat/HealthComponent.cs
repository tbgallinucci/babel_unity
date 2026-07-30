using System;
using UnityEngine;

namespace Babel.Combat
{
    // Componente genérico de vida — o mesmo script serve pro Player e pra
    // qualquer inimigo (Targetable/EnemyDummy hoje, EnemyBase real amanhã).
    // Dano é sempre aplicado de fora pra dentro, direto neste componente
    // (PlayerAttackHitbox.OnAttackHit, EnemyDummy.OnTriggerStay) — nenhum
    // dos dois lados chama um "TakeDamage" no script do outro.
    public class HealthComponent : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 100f;

        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; }
        public bool IsAlive => CurrentHealth > 0f;

        // Setável por fora (PlayerController sincroniza isso a partir de
        // IsDodgeInvulnerable) — TakeDamage() simplesmente não faz nada
        // enquanto isso for true.
        public bool IsInvulnerable { get; set; }

        public event Action<float, float> OnDamaged; // (current, max)
        public event Action OnDeath;

        private void Awake()
        {
            CurrentHealth = maxHealth;
        }

        public void TakeDamage(float amount)
        {
            if (IsInvulnerable || !IsAlive || amount <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            OnDamaged?.Invoke(CurrentHealth, maxHealth);

            if (CurrentHealth <= 0f)
            {
                OnDeath?.Invoke();
            }
        }
    }
}
