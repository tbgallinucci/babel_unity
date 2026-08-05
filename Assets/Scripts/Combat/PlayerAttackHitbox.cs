using System;
using UnityEngine;

namespace Babel.Combat
{
    // Detecção de acerto dos ataques do player: um Animation Event
    // (OnAttackHit) no frame de impacto de cada clipe de ataque (Attack1/2/3,
    // Attack2Alt) dispara um OverlapSphere CENTRADO NO PLAYER,
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
        // Velocidade vertical inicial do launcher (OnAttackHitLaunch). Com a
        // launchGravity padrão do KnockbackReceiver (40), 18 dá ~4m de altura
        // de pico e ~0.9s de voo. Altura = f²/(2*g), voo = 2*f/g — vale ter
        // em mente que dobrar a força QUADRUPLICA a altura.
        //
        // Subiu de 12 pra 18 junto com a launchGravity (18 -> 40) só pra
        // MANTER a altura: gravidade maior come mais velocidade no mesmo
        // percurso. O que se ganha é a desaceleração visível — a "sensação de
        // launch" — sem o inimigo ir parar mais alto.
        //
        // O alvo NÃO é "o mais alto possível": é ficar um pouco acima do ápice
        // do player (~3.75m com jumpForce 15 / gravity 30), porque o combo
        // aéreo acontece no encontro dos dois arcos. Valores testados que não
        // funcionaram, e por quê:
        //   6  — voo de só 0.67s; o inimigo pousava antes do player sequer
        //        decolar (contando o tempo até a janela de cancel abrir).
        //   15 — voo longo, mas ápice de 6.25m: o inimigo passa MUITO acima do
        //        alcance do player e o encontro nunca acontece por cima.
        //
        // Já estava tunado na cena — igual jumpForce, o valor salvo no
        // Inspector do objeto real não muda sozinho, precisa editar lá
        // também.
        [SerializeField] private float launchUpwardForce = 18f;
        // Quanto tempo o alvo fica pendurado na altura em que está a cada
        // acerto do combo AÉREO (ver OnAirAttackHit e
        // KnockbackReceiver.ApplyAirHold). É o espelho, do lado do inimigo, do
        // PlayerController.airAttackGravityScale — os dois juntos é que fazem
        // "o golpe aéreo mantém os dois no ar".
        //
        // Calibrar contra a duração dos clipes aéreos: o hold precisa cobrir o
        // resto do golpe atual mais a entrada do próximo, senão o alvo retoma a
        // queda entre um hit e outro e o segundo golpe passa por cima dele.
        [SerializeField] private float airHoldTime = 0.35f;
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
            ApplyHit(origin, hitRadius, hitAngle, evt, radial: false, upwardForce: 0f);
        }

        // Mesma geometria do OnAttackHit (cone frontal), mas o alvo é JOGADO
        // PRA CIMA em vez de só empurrado — o launcher de juggle. Usado hoje
        // pelo Attack1Alt2.
        //
        // Função própria pelo mesmo motivo do OnAttackHitRadial: não é um
        // número diferente, é comportamento diferente do lado do alvo (arco
        // balístico com duração derivada da força, ver
        // KnockbackReceiver.ApplyLaunch). Também deixa óbvio na janela de
        // Animation dele qual golpe levanta e qual não, sem ter que abrir o
        // Inspector do hitbox pra descobrir.
        //
        // A força vertical vem de campo serializado e não do AnimationEvent
        // porque os dois parâmetros do evento já estão ocupados (Float = dano,
        // Int = push horizontal) e o Unity não oferece um terceiro numérico. Se
        // um dia mais de um golpe precisar levantar com alturas diferentes,
        // isso vira dado do golpe (WeaponMoveset), não mais um campo aqui.
        public void OnAttackHitLaunch(AnimationEvent evt)
        {
            Vector3 origin = transform.position + Vector3.up * hitHeight;
            ApplyHit(origin, hitRadius, hitAngle, evt, radial: false, upwardForce: launchUpwardForce);
        }

        // Os dois ataques leves aéreos. Mesma geometria de cone do OnAttackHit
        // — o que muda é o que acontece com o alvo DEPOIS do dano: ele fica
        // pendurado na altura em que está por airHoldTime, em vez de continuar
        // a queda do arco do launcher.
        //
        // Função própria e não um OnAttackHitLaunch com força menor porque a
        // diferença é de espécie, não de grau: launch DÁ velocidade pra cima e
        // empilha (dois launches seguidos sobem o dobro), hold CONGELA a altura
        // atual e não empilha. Repetir o launcher a cada hit do combo aéreo
        // faria o inimigo escalar até sair do alcance por cima — ver o
        // comentário de ApplyAirHold.
        public void OnAirAttackHit(AnimationEvent evt)
        {
            Vector3 origin = transform.position + Vector3.up * hitHeight;
            ApplyHit(origin, hitRadius, hitAngle, evt, radial: false, upwardForce: 0f,
                airHold: airHoldTime);
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
            ApplyHit(origin, radialHitRadius, 360f, evt, radial: true, upwardForce: 0f);
        }

        // Impacto do plunge attack (Air Heavy Attack 2): mesma geometria
        // radial do golpe giratório (OverlapSphere 360° centrado no player),
        // porque o impacto tem que pegar todo mundo em volta do ponto de
        // pouso, não só quem está na frente.
        //
        // Não é este script que sabe desligar a colisão jogador-inimigo nem
        // soltar o alvo carregado no socket — isso é estado do
        // PlayerController (Physics.IgnoreLayerCollision, KnockbackReceiver
        // ancorado). Repassa por evento C#, mesmo idioma do
        // WeaponEquipController.JumpTakeOff: o Animation Event tem que cair
        // neste GameObject (o do Animator), não na raiz onde mora o
        // PlayerController.
        public event Action<AnimationEvent> PlungeImpact;

        public void OnPlungeImpact(AnimationEvent evt)
        {
            Vector3 origin = transform.position + Vector3.up * hitHeight;
            ApplyHit(origin, radialHitRadius, 360f, evt, radial: true, upwardForce: 0f);
            PlungeImpact?.Invoke(evt);
        }

        private void ApplyHit(Vector3 origin, float radius, float angle, AnimationEvent evt,
            bool radial, float upwardForce, float airHold = 0f)
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

                    // ApplyLaunch com upwardForce 0 recai no empurrão normal,
                    // então não precisa de if aqui — só o launcher passa valor.
                    knockback.ApplyLaunch(pushDirection, pushForce, upwardForce);

                    // Depois do push de propósito: o ApplyKnockback embutido no
                    // ApplyLaunch (o caminho de upwardForce 0, que é o dos
                    // golpes aéreos) devolve cedo quando o alvo já está voando,
                    // preservando o arco — mas ele não sabe nada de hang. Quem
                    // segura é esta chamada, e ela ignora sozinha quem não está
                    // no ar, então acertar um inimigo em pé com um golpe aéreo
                    // continua sendo só um empurrão normal.
                    knockback.ApplyAirHold(airHold);
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
