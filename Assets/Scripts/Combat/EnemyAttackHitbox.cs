using UnityEngine;
using Babel.Enemies;

namespace Babel.Combat
{
    // Espelho do PlayerAttackHitbox pro lado do inimigo: mesma técnica
    // (OverlapSphere via Animation Event, Float=dano/Int=push), mascarando
    // a layer Player em vez de Enemy. Fica em arquivo próprio em vez de
    // generalizar os dois numa classe só parametrizada por layer — só
    // existe UM inimigo concreto até agora, generalizar cedo demais é
    // exatamente a abstração prematura que o guia de Robustez já decidiu
    // evitar (item 4, sobre WeaponMoveset).
    //
    // Precisa estar no MESMO GameObject do Animator do inimigo (o filho —
    // regra já documentada em PlayerAttackHitbox/WeaponEquipController),
    // não na raiz onde mora o EnemyBase.
    public class EnemyAttackHitbox : MonoBehaviour
    {
        [Header("Swipe (Attack)")]
        [SerializeField] private float hitRange = 1.2f;
        [SerializeField] private float hitRadius = 1f;
        [SerializeField] private float hitHeight = 1f;

        [Header("Jump Attack (pouso)")]
        [SerializeField] private float jumpAttackHitRadius = 2.5f;

        [SerializeField] private LayerMask playerLayer;

        // Lido pelo EnemyBase pra dimensionar o telegraph — nunca duplicado
        // em outro campo, o que se vê é exatamente o que machuca.
        public float JumpAttackHitRadius => jumpAttackHitRadius;

        private EnemyBase owner;

        private void Awake()
        {
            // Filho -> pai: EnemyBase mora na raiz (onde ficam o
            // NavMeshAgent/Rigidbody/collider), este script no filho do
            // Animator. Resolvido sozinho, sem wiring manual no Inspector.
            owner = GetComponentInParent<EnemyBase>();
        }

        // Swipe: esfera deslocada pra frente do inimigo, mesma geometria do
        // PlayerAttackHitbox.OnAttackHit — empurra na direção do forward.
        public void OnAttackHit(AnimationEvent evt)
        {
            Vector3 origin = transform.position + transform.forward * hitRange + Vector3.up * hitHeight;
            ApplyHit(origin, hitRadius, evt, transform.forward);
        }

        // Pouso do jump attack: primeiro avisa o EnemyBase pra travar a
        // posição exatamente no landingPoint/retomar o NavMeshAgent/
        // esconder o telegraph (ver EnemyBase.HandleJumpAttackLanded) —
        // só DEPOIS lê transform.position, garantindo que a esfera de dano
        // nasce exatamente onde o disco do telegraph parou de crescer, sem
        // depender do Lerp do frame ter chegado bem certinho em t=1.
        // Empurrão é radial (pra longe do ponto de pouso), não frontal —
        // mesmo racional do OnAttackHitRadial do player.
        public void OnJumpAttackLand(AnimationEvent evt)
        {
            if (owner != null)
            {
                owner.HandleJumpAttackLanded();
            }

            Vector3 origin = transform.position + Vector3.up * hitHeight;
            ApplyHit(origin, jumpAttackHitRadius, evt, null);
        }

        private void ApplyHit(Vector3 origin, float radius, AnimationEvent evt, Vector3? pushDirectionOverride)
        {
            float damage = evt.floatParameter;
            float pushForce = evt.intParameter;

            Collider[] hits = Physics.OverlapSphere(origin, radius, playerLayer);

            foreach (Collider hit in hits)
            {
                HealthComponent health = hit.GetComponentInParent<HealthComponent>();
                if (health != null)
                {
                    health.TakeDamage(damage);
                }

                KnockbackReceiver knockback = hit.GetComponentInParent<KnockbackReceiver>();
                if (knockback != null)
                {
                    // pushDirectionOverride nulo = radial (pra longe da
                    // origem do golpe, caso do jump attack); com valor =
                    // direção fixa (o forward do swipe).
                    Vector3 pushDirection = pushDirectionOverride ?? (hit.transform.position - origin);
                    knockback.ApplyKnockback(pushDirection, pushForce);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 swipeOrigin = transform.position + transform.forward * hitRange + Vector3.up * hitHeight;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(swipeOrigin, hitRadius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * hitHeight, jumpAttackHitRadius);
        }
    }
}
