using UnityEngine;

namespace Babel.Combat
{
    // Detecção de acerto dos ataques do player: um Animation Event
    // (OnAttackHit, parâmetro float = dano daquele golpe específico) no
    // frame de impacto de cada clipe de ataque (Attack1/2/3, StrongAttack,
    // AttackMagic) dispara um OverlapSphere na frente do personagem. Precisa
    // estar no MESMO GameObject do Animator — Animation Events despacham
    // SendMessage contra esse GameObject especificamente, mesma regra já
    // documentada pro WeaponEquipController.
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

        public void OnAttackHit(float damage)
        {
            Vector3 origin = transform.position + transform.forward * hitRange + Vector3.up * hitHeight;
            Collider[] hits = Physics.OverlapSphere(origin, hitRadius, enemyLayer);

            foreach (Collider hit in hits)
            {
                HealthComponent health = hit.GetComponentInParent<HealthComponent>();
                if (health != null)
                {
                    health.TakeDamage(damage);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = transform.position + transform.forward * hitRange + Vector3.up * hitHeight;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(origin, hitRadius);
        }
    }
}
