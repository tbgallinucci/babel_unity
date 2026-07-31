using UnityEngine;

namespace Babel.Combat
{
    // Empurrão físico genérico, desacoplado de HealthComponent de propósito
    // (dano e push não dependem um do outro — algo pode empurrar sem
    // ferir, ou ter vida sem ser empurrável). Pensado pro Rigidbody alvo
    // ficar Is Kinematic (é a convenção que EnemyDummy também vai usar,
    // "só trigger, sem física de verdade nele"), então o deslocamento é
    // inteiramente por código via MovePosition, não Rigidbody.AddForce —
    // mesmo princípio do movimento forçado por código já usado em
    // PlayerController.OnAnimatorMove() (Dash/SlideAttack), só que aqui é
    // Rigidbody+FixedUpdate em vez de CharacterController+Update, já que
    // não existe CharacterController nesse alvo.
    [RequireComponent(typeof(Rigidbody))]
    public class KnockbackReceiver : MonoBehaviour
    {
        [SerializeField] private float knockbackDuration = 0.2f;

        private Rigidbody rb;
        private Vector3 knockbackVelocity;
        private float remaining;

        // Pra quem mais tenta escrever transform.position no mesmo objeto
        // (ex.: EnemyBase dirigindo um NavMeshAgent) saber quando ceder o
        // controle em vez de brigar por ele. Fica genérico de propósito —
        // não sabe nada de NavMeshAgent, só expõe "ainda tenho um push
        // rolando"; a coordenação é responsabilidade de quem consome isso.
        public bool IsActive => remaining > 0f;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        public void ApplyKnockback(Vector3 direction, float force)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f || force <= 0f)
            {
                return;
            }

            // Sobrescreve em vez de somar — um push no meio de outro em
            // andamento simplesmente reinicia com a nova direção/força, sem
            // stacking. Suficiente pra essa primeira passada.
            knockbackVelocity = direction.normalized * force;
            remaining = knockbackDuration;
        }

        private void FixedUpdate()
        {
            if (remaining <= 0f)
            {
                return;
            }

            // Kinematic não sofre resposta a colisão — MovePosition atravessa
            // geometria sem ser barrado. Inofensivo pro dummy de teste
            // isolado, mas não é "física real" contra paredes/cenário.
            float t = remaining / knockbackDuration;
            rb.MovePosition(rb.position + knockbackVelocity * t * Time.fixedDeltaTime);
            remaining -= Time.fixedDeltaTime;
        }
    }
}
