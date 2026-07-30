using UnityEngine;

namespace Babel.Combat
{
    // Detecção de acerto dos ataques do player: um Animation Event
    // (OnAttackHit) no frame de impacto de cada clipe de ataque (Attack1/2/3,
    // Attack2Alt, SlideAttack) dispara um OverlapSphere na frente do
    // personagem. Precisa estar no MESMO GameObject do Animator — Animation
    // Events despacham SendMessage contra esse GameObject especificamente,
    // mesma regra já documentada pro WeaponEquipController.
    //
    // O evento carrega os dois valores daquele golpe específico no mesmo
    // AnimationEvent (Unity entrega a struct inteira quando a assinatura do
    // método é exatamente "AnimationEvent", em vez de desempacotar só um
    // parâmetro): floatParameter = dano, intParameter = força de push. Dano e
    // push são aplicados a componentes independentes (HealthComponent /
    // KnockbackReceiver) — um alvo pode ter só um dos dois, ou nenhum.
    //
    // Cada hit do combo já é um estado/clipe separado no Animator, então um
    // OnAttackHit por clipe já dá o comportamento certo (não precisa de
    // lógica de "só acerta uma vez por swing").
    public class PlayerAttackHitbox : MonoBehaviour
    {
        [SerializeField] private float hitRange = 1.2f;
        [SerializeField] private float hitRadius = 1f;
        [SerializeField] private float hitHeight = 1f;
        [SerializeField] private LayerMask enemyLayer;
        // Raio do golpe radial (OnAttackHitRadial), centrado no player. Bem
        // maior que hitRadius de propósito: um giro de 360° tem que pegar
        // quem está em volta, não só quem está no alcance do swing pra frente.
        [SerializeField] private float radialHitRadius = 3f;
        // Duração do hit-stop, em segundos de tempo REAL, disparado só quando
        // o golpe acerta alguém (errar não congela nada). Opcional: sem um
        // HitStop no mesmo GameObject, tudo funciona igual, só sem a trava.
        [SerializeField] private float hitStopDuration = 0.06f;

        private HitStop hitStop;

        private void Awake()
        {
            hitStop = GetComponent<HitStop>();
        }

        // Golpe direcional (o combo normal): esfera deslocada pra FRENTE do
        // personagem, só pega quem está no arco do swing.
        public void OnAttackHit(AnimationEvent evt)
        {
            Vector3 origin = transform.position + transform.forward * hitRange + Vector3.up * hitHeight;
            ApplyHit(origin, hitRadius, evt, radial: false);
        }

        // Golpe radial 360°: esfera centrada NO PLAYER e bem maior, pra
        // acertar todo mundo em volta — o giro no fim do slide attack é o
        // caso de uso. Equivale ao enemies_in_radius do projeto Godot de
        // referência (o combo normal é o enemies_in_melee_cone).
        //
        // É um Animation Event de função própria em vez de um modo do
        // OnAttackHit porque a diferença não é só o raio: o centro e a
        // direção do empurrão mudam junto (ver ApplyHit).
        public void OnAttackHitRadial(AnimationEvent evt)
        {
            Vector3 origin = transform.position + Vector3.up * hitHeight;
            ApplyHit(origin, radialHitRadius, evt, radial: true);
        }

        private void ApplyHit(Vector3 origin, float radius, AnimationEvent evt, bool radial)
        {
            float damage = evt.floatParameter;
            float pushForce = evt.intParameter;

            Collider[] hits = Physics.OverlapSphere(origin, radius, enemyLayer);

            // Uma trava por swing, não uma por alvo atingido — acertar dois
            // inimigos de uma vez não deve congelar o dobro do tempo.
            if (hits.Length > 0 && hitStop != null)
            {
                hitStop.Trigger(hitStopDuration);
            }

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
                    // No golpe radial cada alvo é arremessado pra LONGE do
                    // player, não todos pro mesmo lado: empurrar quem está
                    // atrás na direção do forward puxaria ele pra dentro do
                    // personagem em vez de afastar.
                    Vector3 pushDirection = radial
                        ? hit.transform.position - transform.position
                        : transform.forward;

                    knockback.ApplyKnockback(pushDirection, pushForce);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = transform.position + transform.forward * hitRange + Vector3.up * hitHeight;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(origin, hitRadius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * hitHeight, radialHitRadius);
        }
    }
}
