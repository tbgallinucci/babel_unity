using UnityEngine;

namespace Babel.Combat
{
    // Detecção de acerto dos ataques do player: um Animation Event
    // (OnAttackHit) no frame de impacto de cada clipe de ataque (Attack1/2/3,
    // Attack2Alt, SlideAttack) dispara um OverlapSphere CENTRADO NO PLAYER,
    // filtrado por ângulo em relação ao forward — na prática, uma fatia de
    // pizza na frente do personagem, não um ponto isolado. Precisa estar no
    // MESMO GameObject do Animator — Animation Events despacham SendMessage
    // contra esse GameObject especificamente, mesma regra já documentada pro
    // WeaponEquipController.
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
        [SerializeField] private float hitRadius = 1f;
        // Abertura do arco do golpe direcional, centrada no forward do
        // personagem — 360 equivaleria ao golpe radial (mas pra isso já
        // existe OnAttackHitRadial, com raio próprio maior). Uma espada
        // grande varrendo de lado a lado pede um ângulo bem mais largo que a
        // "fatia estreita" que o valor de teste original (esfera pontual)
        // conseguia representar.
        [SerializeField, Range(0f, 360f)] private float hitAngle = 140f;
        [SerializeField] private float hitHeight = 1f;
        [SerializeField] private LayerMask enemyLayer;
        // Raio do golpe radial (OnAttackHitRadial), centrado no player. Bem
        // maior que hitRadius de propósito: um giro de 360° tem que pegar
        // quem está em volta, não só quem está no alcance do swing pra frente.
        [SerializeField] private float radialHitRadius = 3f;
        // Hit-stop, em FRAMES de 60fps (é assim que se autora peso de golpe;
        // ver HitStop.ReferenceFps), disparado só quando o golpe acerta
        // alguém — errar não congela nada.
        //
        // TÉCNICA 5 do vídeo do Sakurai: a duração escala com o dano em vez de
        // ser um valor fixo, senão um jab e um golpe carregado pesam igual.
        // Com os padrões abaixo, 5 de dano dá 4 frames (~0.067s, praticamente
        // o 0.06s fixo que existia antes) e 25 de dano dá 10 frames — a mesma
        // curva do Smash Ultimate, com coeficientes menores porque a escala de
        // dano deste projeto é outra.
        //
        // Tudo opcional: sem HitStop na cena e sem HitStopReceiver nos
        // envolvidos, o combate roda igual, só sem a trava.
        [Header("Hit stop")]
        [SerializeField] private float hitStopBaseFrames = 2.5f;
        [SerializeField] private float hitStopFramesPerDamage = 0.3f;
        [SerializeField] private float hitStopMaxFrames = 12f;
        // Multiplicador por hitbox, pro golpe radial poder pesar diferente do
        // combo normal sem mexer na curva de dano. O passo seguinte natural é
        // isto virar dado do golpe (WeaponMoveset), não do componente.
        [SerializeField] private float hitStopMultiplier = 1f;

        // Do próprio player: este script mora no filho do Animator, o receiver
        // mora na raiz (junto do HealthComponent) — daí o InParent.
        private HitStopReceiver selfHitStop;

        private void Awake()
        {
            selfHitStop = GetComponentInParent<HitStopReceiver>();
        }

        // Golpe direcional (o combo normal): esfera centrada NO PLAYER, igual
        // o golpe radial — o que diferencia os dois é o filtro de ângulo
        // aplicado em ApplyHit (radial pula esse filtro de propósito).
        public void OnAttackHit(AnimationEvent evt)
        {
            Vector3 origin = transform.position + Vector3.up * hitHeight;
            ApplyHit(origin, hitRadius, hitAngle, evt, radial: false);
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
            ApplyHit(origin, radialHitRadius, 360f, evt, radial: true);
        }

        private void ApplyHit(Vector3 origin, float radius, float angle, AnimationEvent evt, bool radial)
        {
            float damage = evt.floatParameter;
            float pushForce = evt.intParameter;

            Collider[] hits = Physics.OverlapSphere(origin, radius, enemyLayer);

            // Uma trava por swing, não uma por alvo atingido — acertar dois
            // inimigos de uma vez não deve congelar o dobro do tempo. A janela
            // é calculada aqui fora e disparada dentro do loop, por alvo que
            // sobreviver ao filtro de ângulo — travas repetidas são
            // inofensivas porque HitStop/HitStopReceiver renovam a janela pelo
            // Max em vez de somar. Chamar de dentro do loop também conserta um
            // caso antigo: a versão anterior travava com base em hits.Length,
            // ou seja, congelava a tela por um inimigo que estava dentro da
            // esfera mas fora do arco do golpe — trava sem acerto nenhum.
            float hitStopDuration = HitStop.DurationFromDamage(damage, hitStopBaseFrames,
                hitStopFramesPerDamage, hitStopMaxFrames, hitStopMultiplier);

            foreach (Collider hit in hits)
            {
                // Radial pula esse filtro de propósito (giro de 360° tem que
                // pegar todo mundo em volta, não só quem está no arco).
                if (!radial)
                {
                    Vector3 toTarget = hit.transform.position - transform.position;
                    toTarget.y = 0f;
                    if (Vector3.Angle(transform.forward, toTarget) > angle * 0.5f)
                    {
                        continue;
                    }
                }

                HealthComponent health = hit.GetComponentInParent<HealthComponent>();

                // Alvo invulnerável (i-frames de dodge) ou já morto não trava
                // nada: congelar a tela sem tirar vida lê como acerto e mata a
                // leitura do golpe. Sem HealthComponent nenhum ainda conta
                // como acerto (um objeto quebrável só empurrável, por
                // exemplo) — quem não tem vida não tem como "não levar".
                if (health == null || (!health.IsInvulnerable && health.IsAlive))
                {
                    // Antes do TakeDamage de propósito: HealthComponent.OnDamaged
                    // é o que faz o EnemyBase disparar o trigger "Hit". Com o
                    // HitStopReceiver do alvo já em UnscaledTime + speed
                    // reduzido quando esse trigger chega, a transição pro estado
                    // de dano nasce interpolando devagar — que é exatamente a
                    // TÉCNICA 6 (entrar na pose de dano, não cortar pra ela).
                    HitStop.Apply(selfHitStop, hit.GetComponentInParent<HitStopReceiver>(), hitStopDuration);
                }

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
            Vector3 origin = transform.position + Vector3.up * hitHeight;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(origin, hitRadius);

            // Duas linhas nas bordas do arco (±metade do ângulo a partir do
            // forward) — junto com a esfera acima dá pra ler a fatia de
            // pizza inteira sem precisar dar Play.
            Quaternion halfLeft = Quaternion.Euler(0f, -hitAngle * 0.5f, 0f);
            Quaternion halfRight = Quaternion.Euler(0f, hitAngle * 0.5f, 0f);
            Gizmos.DrawLine(origin, origin + (halfLeft * transform.forward) * hitRadius);
            Gizmos.DrawLine(origin, origin + (halfRight * transform.forward) * hitRadius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(origin, radialHitRadius);
        }
    }
}
