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
        public event Action<float, float> OnHealed; // (current, max)
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

        // Evento separado de OnDamaged de propósito — um HitFlash ligado em
        // OnDamaged não deve piscar de vermelho ao curar só porque os dois
        // mexem no mesmo CurrentHealth. Sem gate de IsInvulnerable (curar
        // não é dano, não faz sentido bloquear pela mesma janela de
        // i-frame) nem de IsAlive (curar um morto não devia reviver
        // sozinho — sem sistema de respawn ainda, deixa incrementar
        // CurrentHealth sem sentido prático, mas não é este método que
        // decide "reviver").
        public void Heal(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            OnHealed?.Invoke(CurrentHealth, maxHealth);
        }
    }
}
