using UnityEngine;
using UnityEngine.AI;
using Babel.Combat;
using Babel.Player;

namespace Babel.Enemies
{
    // Primeiro inimigo de verdade do projeto (Fase 3 do guia de migração).
    // NavMeshAgent dirige a posição — sem root motion, de propósito (ver
    // Awake): o Animator só reflete o que o agent já decidiu, mesmo
    // padrão-Unity-pra-IA que evita a complicação de sincronizar
    // agent.nextPosition com root motion de verdade.
    //
    // Campos ficam direto aqui (sem EnemyData ScriptableObject) pela mesma
    // razão que WeaponMoveset foi adiado no guia de Robustez, item 4: um
    // catálogo de dados não se justifica pra um inimigo concreto só —
    // vira mecânico "levantar" pra ScriptableObject quando o segundo
    // inimigo for necessidade real.
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(HealthComponent))]
    [RequireComponent(typeof(Targetable))]
    [RequireComponent(typeof(KnockbackReceiver))]
    [RequireComponent(typeof(HitFlash))]
    public class EnemyBase : MonoBehaviour
    {
        private enum State { Idle, Roam, Chase, Attack, JumpAttack, Hit, Dead }

        // Precisa bater com o m_Name do estado de locomoção no
        // EnemyAnimatorController — usado só pra saber quando Attack/Hit
        // já terminaram e devolveram a Base Layer pra lá (via Exit Time do
        // próprio Animator, não um timer duplicado em C#).
        private const string LocomotionStateName = "Locomotion";

        [Header("Roam")]
        [SerializeField] private float roamRadius = 6f;
        [SerializeField] private float roamIdleMin = 2f;
        [SerializeField] private float roamIdleMax = 5f;
        [SerializeField] private float roamSpeed = 1.5f;

        [Header("Aggro / Chase")]
        [SerializeField] private float aggroRadius = 8f;
        // Distância do spawn a partir da qual desiste de perseguir e volta
        // a roamear — bem grande efetivamente desliga a coleira.
        [SerializeField] private float leashRadius = 20f;
        [SerializeField] private float chaseSpeed = 4f;
        [SerializeField] private float attackRange = 2f;

        [Header("Attack")]
        [SerializeField] private float attackCooldown = 2f;

        [Header("Jump Attack")]
        [SerializeField, Range(0f, 1f)] private float jumpAttackChance = 0.3f;
        [SerializeField] private float jumpAttackCooldown = 8f;
        // Fração do clipe em que o pouso acontece — o Animation Event
        // OnJumpAttackLand no clipe precisa disparar exatamente aqui.
        [SerializeField, Range(0f, 1f)] private float jumpAttackLandNormalizedTime = 0.85f;
        [SerializeField] private float jumpAttackArcHeight = 2f;
        [SerializeField] private float jumpAttackMaxRange = 10f;
        [SerializeField] private JumpAttackTelegraph telegraphPrefab;

        [Header("Death")]
        [SerializeField] private Color deadColor = Color.gray;

        private NavMeshAgent agent;
        private HealthComponent health;
        private KnockbackReceiver knockback;
        private HitFlash hitFlash;
        private Animator animator;
        private EnemyAttackHitbox attackHitbox;
        private JumpAttackTelegraph telegraph;
        private Transform player;

        private State state;
        private Vector3 spawnPosition;
        private float idleTimer;
        private float attackCooldownRemaining;
        private float jumpAttackCooldownRemaining;
        private Vector3 jumpStartPos;
        private Vector3 landingPoint;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int JumpAttackHash = Animator.StringToHash("JumpAttack");
        private static readonly int HitHash = Animator.StringToHash("Hit");

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<HealthComponent>();
            knockback = GetComponent<KnockbackReceiver>();
            hitFlash = GetComponent<HitFlash>();
            // Animator/hitbox moram no filho (o modelo importado) — mesma
            // técnica GetComponentInChildren que PlayerController já usa
            // pro próprio Animator, sem precisar de wiring manual.
            animator = GetComponentInChildren<Animator>();
            attackHitbox = GetComponentInChildren<EnemyAttackHitbox>();

            if (animator != null)
            {
                // Defensivo — ver comentário de cabeçalho. Import Humanoid
                // vem com isso marcado por padrão; se alguém reimportar o
                // modelo e esquecer de desmarcar no Inspector, isso evita a
                // regressão silenciosa em vez de só documentar.
                animator.applyRootMotion = false;
            }

            var playerController = FindFirstObjectByType<PlayerController>();
            if (playerController != null)
            {
                player = playerController.transform;
            }

            spawnPosition = transform.position;

            if (telegraphPrefab != null)
            {
                telegraph = Instantiate(telegraphPrefab);
            }

            idleTimer = Random.Range(roamIdleMin, roamIdleMax);
            state = State.Idle;
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
            if (state == State.Dead)
            {
                return;
            }

            SyncAgentWithKnockback();

            // Golpes comprometidos (Attack/JumpAttack) e a reação de Hit
            // não reavaliam aggro no meio — terminam o que começaram.
            if (state != State.Attack && state != State.JumpAttack && state != State.Hit)
            {
                TickAggro();
            }

            switch (state)
            {
                case State.Idle: TickIdle(); break;
                case State.Roam: TickRoam(); break;
                case State.Chase: TickChase(); break;
                case State.Attack: TickAttack(); break;
                case State.JumpAttack: TickJumpAttack(); break;
                case State.Hit: TickHit(); break;
            }

            if (attackCooldownRemaining > 0f)
            {
                attackCooldownRemaining -= Time.deltaTime;
            }

            if (jumpAttackCooldownRemaining > 0f)
            {
                jumpAttackCooldownRemaining -= Time.deltaTime;
            }

            // Attack/JumpAttack/Hit são estados discretos no Animator que
            // nunca leem o blend tree de Speed, então não precisam de gate
            // — mesmo espírito do comentário em PlayerController.IsAttacking,
            // só que aqui nem faz diferença porque ninguém lê o parâmetro
            // fora da Locomotion.
            animator.SetFloat(SpeedHash, agent.velocity.magnitude, 0.05f, Time.deltaTime);
        }

        // NavMeshAgent e KnockbackReceiver escrevem transform.position sem
        // se conhecerem. Enquanto o push está ativo, cede o controle pro
        // Rigidbody; assim que acaba, resincroniza o agent na posição onde
        // o push deixou (Warp, não um SetDestination — não é navegação,
        // é "aceita onde você foi parar").
        private void SyncAgentWithKnockback()
        {
            if (knockback.IsActive && agent.updatePosition)
            {
                agent.updatePosition = false;
                agent.updateRotation = false;
            }
            else if (!knockback.IsActive && !agent.updatePosition
                && state != State.Attack && state != State.JumpAttack)
            {
                agent.Warp(transform.position);
                agent.updatePosition = true;
                agent.updateRotation = true;
            }
        }

        private void TickAggro()
        {
            if (player == null || state == State.Chase)
            {
                return;
            }

            if (Vector3.Distance(transform.position, player.position) <= aggroRadius)
            {
                agent.isStopped = false;
                state = State.Chase;
            }
        }

        private void TickIdle()
        {
            idleTimer -= Time.deltaTime;
            if (idleTimer > 0f)
            {
                return;
            }

            Vector2 offset = Random.insideUnitCircle * roamRadius;
            Vector3 samplePoint = spawnPosition + new Vector3(offset.x, 0f, offset.y);

            if (NavMesh.SamplePosition(samplePoint, out NavMeshHit hit, roamRadius, NavMesh.AllAreas))
            {
                agent.speed = roamSpeed;
                agent.isStopped = false;
                agent.SetDestination(hit.position);
                state = State.Roam;
            }
            // Sorteio caiu fora do NavMesh — idleTimer já zerado, tenta de
            // novo no próximo Update sem ficar preso esperando.
        }

        private void TickRoam()
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                idleTimer = Random.Range(roamIdleMin, roamIdleMax);
                state = State.Idle;
            }
        }

        private void TickChase()
        {
            if (player == null)
            {
                state = State.Idle;
                return;
            }

            agent.speed = chaseSpeed;
            agent.SetDestination(player.position);

            float distanceToPlayer = Vector3.Distance(transform.position, player.position);

            if (distanceToPlayer <= attackRange && attackCooldownRemaining <= 0f)
            {
                DecideAttack();
                return;
            }

            if (Vector3.Distance(transform.position, spawnPosition) > leashRadius)
            {
                state = State.Idle;
            }
        }

        private void DecideAttack()
        {
            if (jumpAttackCooldownRemaining <= 0f && Random.value < jumpAttackChance)
            {
                BeginJumpAttack();
            }
            else
            {
                BeginAttack();
            }
        }

        private void BeginAttack()
        {
            attackCooldownRemaining = attackCooldown;
            agent.isStopped = true;
            FaceTarget(player.position);
            animator.SetTrigger(AttackHash);
            state = State.Attack;
        }

        private void TickAttack()
        {
            if (AnimatorStateUtil.HasStateNowOrIncoming(animator, 0, LocomotionStateName))
            {
                agent.isStopped = false;
                state = State.Chase;
            }
        }

        private void BeginJumpAttack()
        {
            attackCooldownRemaining = attackCooldown;
            jumpAttackCooldownRemaining = jumpAttackCooldown;

            if (!NavMesh.SamplePosition(player.position, out NavMeshHit hit, jumpAttackMaxRange, NavMesh.AllAreas))
            {
                // Não achou onde pousar — cai pro ataque normal em vez de
                // pular pra um lugar inalcançável.
                BeginAttack();
                return;
            }

            landingPoint = hit.position;
            jumpStartPos = transform.position;
            FaceTarget(landingPoint);

            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.isStopped = true;

            animator.SetTrigger(JumpAttackHash);

            if (telegraph != null)
            {
                float radius = attackHitbox != null ? attackHitbox.JumpAttackHitRadius : 0f;
                telegraph.Show(landingPoint, radius);
            }

            state = State.JumpAttack;
        }

        private void TickJumpAttack()
        {
            var info = animator.GetCurrentAnimatorStateInfo(0);
            float t = Mathf.Clamp01(info.normalizedTime / jumpAttackLandNormalizedTime);

            transform.position = Vector3.Lerp(jumpStartPos, landingPoint, t)
                + Vector3.up * (jumpAttackArcHeight * Mathf.Sin(t * Mathf.PI));

            if (telegraph != null)
            {
                telegraph.SetProgress01(t);
            }
        }

        // Chamado por EnemyAttackHitbox.OnJumpAttackLand (Animation Event
        // no clipe, no frame correspondente a jumpAttackLandNormalizedTime)
        // — o evento só pode chamar um método no MESMO GameObject do
        // Animator (o filho), então é o EnemyAttackHitbox que recebe de
        // verdade e reencaminha pra cá. Devolve o controle de posição pro
        // NavMeshAgent e esconde o telegraph.
        public void HandleJumpAttackLanded()
        {
            transform.position = landingPoint;
            agent.Warp(landingPoint);
            agent.updatePosition = true;
            agent.updateRotation = true;
            agent.isStopped = false;

            if (telegraph != null)
            {
                telegraph.Hide();
            }

            state = State.Chase;
        }

        // Hyper armor durante golpes comprometidos: um golpe do player no
        // meio do Attack/JumpAttack do inimigo não cancela pra Hit — senão
        // o telegraph/arco do jump attack ficaria órfão a meio caminho
        // (mesmo espírito do IsInCommittedAttack() do Player).
        private void HandleDamaged(float current, float max)
        {
            if (state == State.Attack || state == State.JumpAttack || state == State.Dead)
            {
                return;
            }

            agent.isStopped = true;
            animator.SetTrigger(HitHash);
            state = State.Hit;
        }

        private void TickHit()
        {
            if (AnimatorStateUtil.HasStateNowOrIncoming(animator, 0, LocomotionStateName))
            {
                agent.isStopped = false;
                state = (player != null && Vector3.Distance(transform.position, player.position) <= aggroRadius)
                    ? State.Chase
                    : State.Idle;
            }
        }

        private void HandleDeath()
        {
            state = State.Dead;
            agent.enabled = false;

            if (attackHitbox != null)
            {
                attackHitbox.enabled = false;
            }

            hitFlash.SetBaseColor(deadColor);

            if (telegraph != null)
            {
                telegraph.Hide();
            }
        }

        private void FaceTarget(Vector3 targetPosition)
        {
            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(direction);
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = Application.isPlaying ? spawnPosition : transform.position;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(center, roamRadius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, aggroRadius);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
