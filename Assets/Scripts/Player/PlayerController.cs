
using UnityEngine;
using UnityEngine.InputSystem;
using Babel.Equipment;
using Babel.Combat;

namespace Babel.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private float rotationSpeed = 15f;
        [SerializeField] private float gravity = 9.81f;
        [SerializeField] private float jumpForce = 5f;
        [SerializeField] private float dashSpeed = 12f;
        [SerializeField, Range(0f, 1f)] private float dashRampInTime = 0.15f;
        [SerializeField] private float slideAttackSpeed = 8f;
        [SerializeField, Range(0f, 1f)] private float slideAttackRampInTime = 0.1f;
        [SerializeField, Range(0f, 1f)] private float slideAttackActiveEnd = 0.4f;
        [SerializeField, Range(0f, 1f)] private float slideAttackRampOutTime = 0.1f;
        // Quando o estado SlideAttack devolve o controle — alimenta o bool
        // IsSliding do Animator, e TODAS as saídas do SlideAttack passam a
        // ser Has Exit Time OFF condicionadas nele (ver o guia de Robustez).
        //
        // Antes as saídas eram por Exit Time (ArmedLocomotion em 1.0, Attack1
        // em 0.8, Attack2Alt em 0.9), cada uma num instante diferente e cada
        // uma dependendo de um bool de fila diferente — combinação que dava
        // tanto travamento (a cauda inteira do clipe sem nada acontecer) como
        // janelas em que nenhuma condição fechava. Com um único ponto de
        // saída controlado por código isso deixa de ser possível.
        //
        // Diferente de slideAttackActiveEnd (0.4), que é só onde o
        // DESLOCAMENTO forçado termina — o golpe em si ainda toca depois
        // disso, então sair em 0.4 cortaria o swing.
        //
        // 0.9 porque é onde o swing de fato acaba neste clipe: era o Exit
        // Time já ajustado à mão nas saídas -> ArmedLocomotion e
        // -> Attack2Alt. A saída -> Attack1 estava em 0.8 e era justamente
        // ela que cortava o golpe ao emendar no combo.
        [SerializeField, Range(0f, 1f)] private float slideAttackExitTime = 0.9f;
        [SerializeField] private float sprintJumpBoost = 4f;
        [Header("Dodge")]
        // Sem movimento forçado por código — o roll usa o root motion natural
        // do clipe "Standing Dodge Forward" (mesmo branch de chão normal do
        // OnAnimatorMove), então a distância/velocidade vem inteira da
        // animação, igual Sprint/Locomotion. Nada aqui controla deslocamento,
        // só a janela de i-frame.
        // Janela (tempo normalizado do clipe) em que IsDodgeInvulnerable fica
        // true — só um stub pro futuro sistema de dano, nada consome isso
        // ainda.
        [SerializeField, Range(0f, 1f)] private float dodgeIFrameStart = 0.1f;
        [SerializeField, Range(0f, 1f)] private float dodgeIFrameEnd = 0.7f;
        [Header("Lock-on")]
        // Fração do giro pro alvo travado aplicada de uma vez no instante em
        // que um ataque/ataque forte dispara (não é contínuo por frame) —
        // perto de 1 = quase encara o alvo na hora, sem ficar um teleporte
        // 100% instantâneo.
        [SerializeField, Range(0f, 1f)] private float lockOnFacingLerp = 0.85f;
        [SerializeField] private float abilityModifierThreshold = 0.5f;
        [Header("Jump Attack (Attack2Alt aéreo)")]
        // Precisa bater com o Exit Time da transição ArmedJumpAttack2Alt ->
        // Empty na layer UpperBody (Animator) — os dois lados concordam
        // visualmente sobre onde termina o swing e começa a recuperação.
        // Sem ligação automática entre os dois, ajustar um sem o outro
        // desalinha o handoff.
        [SerializeField, Range(0f, 1f)] private float jumpAttackHandoffTime = 0.85f;
        // Janela máxima de queda (segundos) em que ainda dá pra iniciar o
        // golpe aéreo — apertar tarde demais na queda é ignorado, porque não
        // sobraria ar suficiente pro swing terminar decentemente antes do
        // pouso físico. Baseado em tempo de queda real (airTime), não no
        // normalizedTime do Jump — os dois são dissociados (Jump é uma
        // animação com duração própria, o tempo de ar real vem só da física
        // de jumpForce/gravity).
        [SerializeField] private float jumpAttackMaxAirTime = 0.4f;
        // Multiplicador de playback só pros estados de combate (Attack1/2/3,
        // SlideAttack) — ligado ao Speed desses estados no Animator via
        // "Parameter" (não afeta locomoção/idle). Testável ao vivo em Play Mode
        // arrastando o slider.
        [SerializeField, Range(0.5f, 2.5f)] private float combatSpeedMultiplier = 1f;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";

        private CharacterController controller;
        private Animator animator;
        private WeaponEquipController weaponEquip;
        private TargetingSystem targeting;
        private HealthComponent health;
        private Transform mainCameraTransform;
        private float verticalVelocity;
        private float lastGroundedSpeed;
        private bool comboQueued;
        private bool strongComboQueued;
        private bool dodgeQueued;
        private bool attackQueued;
        private bool sprinting;
        private bool pendingSprintCancel;
        private int previousStateHash;
        private int upperBodyLayerIndex;
        private float airTime;

        // Stub pro futuro sistema de dano — só a janela de invulnerabilidade
        // calculada, sem nenhum consumidor ainda.
        public bool IsDodgeInvulnerable { get; private set; }

        private InputAction moveAction;
        private InputAction attackAction;
        private InputAction jumpAction;
        private InputAction sprintAction;
        private InputAction dodgeAction;
        private InputAction strongAttackAction;
        private InputAction healAction;
        private InputAction attackMagicAction;
        private InputAction abilityModifierAction;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            animator = GetComponentInChildren<Animator>();
            weaponEquip = GetComponentInChildren<WeaponEquipController>();
            targeting = GetComponent<TargetingSystem>();
            health = GetComponent<HealthComponent>();
            upperBodyLayerIndex = animator.GetLayerIndex(AnimStrings.UpperBody);

            var playerMap = inputActions.FindActionMap(actionMapName, throwIfNotFound: true);
            moveAction = playerMap.FindAction("Move");
            attackAction = playerMap.FindAction("Attack");
            jumpAction = playerMap.FindAction("Jump");
            sprintAction = playerMap.FindAction("Sprint");
            dodgeAction = playerMap.FindAction("Dodge");
            strongAttackAction = playerMap.FindAction("StrongAttack");
            healAction = playerMap.FindAction("Heal");
            attackMagicAction = playerMap.FindAction("AttackMagic");
            abilityModifierAction = playerMap.FindAction("AbilityModifierHeld");

            if (Camera.main != null)
            {
                mainCameraTransform = Camera.main.transform;
            }
        }

        private void OnEnable()
        {
            moveAction.Enable();
            attackAction.Enable();
            jumpAction.Enable();
            sprintAction.Enable();
            dodgeAction.Enable();
            strongAttackAction.Enable();
            healAction.Enable();
            attackMagicAction.Enable();
            abilityModifierAction.Enable();
        }

        private void OnDisable()
        {
            moveAction.Disable();
            attackAction.Disable();
            jumpAction.Disable();
            sprintAction.Disable();
            dodgeAction.Disable();
            strongAttackAction.Disable();
            healAction.Disable();
            attackMagicAction.Disable();
            abilityModifierAction.Disable();
        }

        private void Update()
        {
            // Atrasado um frame de propósito — ver comentário em HandleAttack() no
            // ponto onde pendingSprintCancel é setado.
            if (pendingSprintCancel)
            {
                sprinting = false;
                pendingSprintCancel = false;
            }

            // Tempo real de queda — zera assim que toca o chão, acumula
            // enquanto no ar. É contra ISSO que HandleJumpAttack() mede a
            // janela de início do golpe aéreo, não contra o normalizedTime
            // do Jump (que não tem relação com o tempo de ar físico real).
            airTime = controller.isGrounded ? 0f : airTime + Time.deltaTime;

            HandleAttack();
            HandleStrongAttack();
            HandleJumpAttack();
            HandleAbilities();
            HandleMovement();
            HandleSprint();
            HandleJump();
            HandleDodge();
            animator.SetFloat(AnimStrings.CombatSpeedMultiplier, combatSpeedMultiplier);
            // Espelho de estado (não depende de input), por isso fica aqui e
            // não num Handle*(): é o único gatilho de saída do SlideAttack.
            animator.SetBool(AnimStrings.IsSliding, IsSliding());
        }

        // NowOrIncoming (e não GetCurrentAnimatorStateInfo cru) importa muito
        // aqui: as transições de entrada nos ataques têm 0.25s de crossfade, e
        // durante esse tempo a versão antiga respondia FALSE mesmo com o golpe
        // já a caminho. Resultado: spammar ataque nessa janela caía no branch
        // "começar ataque novo" em vez de "enfileirar combo", re-disparando o
        // trigger e chamando FaceLockedTargetIfNeeded() de novo (snap de 85%
        // na rotação, repetido) — combo errático e personagem tremendo.
        private bool IsAttacking()
        {
            return AnimatorStateUtil.HasTagNowOrIncoming(animator, 0, AnimStrings.Attack);
        }

        // Semântica "efetiva" (estado de destino durante o crossfade), não
        // NowOrIncoming, e isso importa nos dois sentidos:
        //
        // - Saindo (Dodge -> Attack1): vira false no instante em que o blend
        //   começa, então a UpperBody solta o grip em sincronia com a base
        //   layer em vez de segurar os braços na pose de idle por cima do
        //   golpe (era o bug "só o tronco se move, os braços não").
        // - Entrando (Attack1 -> Dodge, o dodge-cancel): vira true já no
        //   começo do blend, então a UpperBody veste o grip na hora e não
        //   fica um intervalo com a arma "solta" no meio do roll.
        private bool IsDodging()
        {
            return AnimatorStateUtil.EffectiveHasTag(animator, 0, AnimStrings.Dodging);
        }

        // GetCurrentAnimatorStateInfo só reflete o estado de ORIGEM enquanto
        // uma transição está em andamento (mesma pegadinha documentada em
        // WeaponEquipController.LayerHasTagNowOrIncoming) — durante o
        // crossfade Attack1->Attack2, por exemplo, isso continua reportando
        // Attack1 até o blend terminar de verdade, bem depois do que já
        // parece visualmente "Attack2" tocando. Usado só pro reset de
        // comboQueued/strongComboQueued abaixo, pra não ficar comendo input
        // apertado cedo durante o crossfade.
        private int GetEffectiveBaseStateHash()
        {
            return AnimatorStateUtil.EffectiveStateHash(animator, 0);
        }

        // Attack2Alt (ex-StrongAttack) reusa a tag "Attack" (de propósito,
        // pra IsAttacking()/combo funcionarem sem mudança), então precisa
        // checar por nome pra diferenciar do resto do combo — mesma técnica
        // já usada pro SlideAttack em OnAnimatorMove().
        private bool IsStrongAttacking()
        {
            return AnimatorStateUtil.HasStateNowOrIncoming(animator, 0, AnimStrings.Attack2Alt);
        }

        // Golpes COMPROMETIDOS: ao contrário do resto do combo (Attack1/2/3,
        // SlideAttack), Dodge não cancela na hora durante eles — só enfileira
        // (ver HandleDodge()) e dispara quando o golpe termina naturalmente,
        // pra não descartar o input só porque apertou um pouco cedo.
        //
        // Attack2Alt entra aqui pelo mesmo motivo dos Attack1Alt: é o ataque
        // forte, e já é tratado como comprometido em outro lugar
        // (IsStrongAttacking() trava a rotação durante ele).
        private bool IsInCommittedAttack()
        {
            return AnimatorStateUtil.HasStateNowOrIncoming(animator, 0, AnimStrings.Attack1Alt1)
                || AnimatorStateUtil.HasStateNowOrIncoming(animator, 0, AnimStrings.Attack1Alt2)
                || AnimatorStateUtil.HasStateNowOrIncoming(animator, 0, AnimStrings.Attack2Alt);
        }

        // Mesmo racional do StrongAttack acima: o deslize é todo root motion
        // forçado por código na direção que o personagem estava olhando
        // quando o golpe começou (ver OnAnimatorMove) — deixar o input girar
        // o personagem por cima nesse meio tempo destoa do próprio
        // deslocamento.
        //
        // Mas o lock vale SÓ enquanto o deslize está de fato acontecendo
        // (mesma janela que OnAnimatorMove usa pra forçar deslocamento:
        // normalizedTime <= slideAttackActiveEnd). A primeira versão travava
        // o estado inteiro, e como OnAnimatorMove já zera o movimento depois
        // de slideAttackActiveEnd, os ~60% finais do clipe ficavam sem
        // movimento E sem rotação — é isso que dava a sensação de "travado
        // por 1,5s" no fim do golpe. Na cauda de recuperação o controle
        // volta; a animação continua tocando normalmente.
        private bool IsSlideAttacking()
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            return state.IsName(AnimStrings.SlideAttack) && state.normalizedTime <= slideAttackActiveEnd;
        }

        // Alimenta o bool IsSliding: "o SlideAttack ainda tem que segurar o
        // estado?". Todas as saídas do SlideAttack no Animator ficam Has Exit
        // Time OFF condicionadas em IsSliding == false, então o momento da
        // saída é decidido só aqui — um ponto de verdade, sem Exit Times
        // concorrentes que possam se anular e travar.
        private bool IsSliding()
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);

            if (!state.IsName(AnimStrings.SlideAttack))
            {
                // Crossfade de ENTRADA: o estado atual ainda é o de origem
                // (Sprint), e o normalizedTime dele não diz nada sobre o
                // golpe. Segura true enquanto o SlideAttack está a caminho —
                // sem isso a saída dispararia no mesmo frame da entrada.
                return AnimatorStateUtil.HasStateNowOrIncoming(animator, 0, AnimStrings.SlideAttack);
            }

            return state.normalizedTime < slideAttackExitTime;
        }

        // Golpe aéreo (Attack2Alt no ar): braços tocando o swing via overlay
        // na UpperBody (ArmedJumpAttack2Alt) OU já entregue pra base layer
        // no trecho final (Attack2AltTail, depois do handoff em
        // HandleJumpAttack()). Os dois contam como "atacando no ar" pros
        // gates de rotação/retrigger.
        //
        // Checa GetNextAnimatorStateInfo durante o crossfade de entrada
        // (ArmedJumpGrip -> ArmedJumpAttack2Alt) — sem isso, essa checagem
        // reporta false durante toda a duração do blend (mesma pegadinha do
        // GetEffectiveBaseStateHash acima), abrindo uma janela onde
        // IsJumpAttacking==false ainda, e as transições antigas do Jump
        // (que dependem desse gate) escapam pra Locomotion/ArmedLocomotion
        // antes da hora — a perna dessincroniza do braço, que continua
        // tocando o clipe completo normalmente.
        private bool IsJumpAttacking()
        {
            if (IsUpperBodyEnteringOrIn(AnimStrings.ArmedJumpAttack2Alt))
            {
                return true;
            }

            return AnimatorStateUtil.HasStateNowOrIncoming(animator, 0, AnimStrings.Attack2AltTail);
        }

        private bool IsUpperBodyEnteringOrIn(string stateName)
        {
            return AnimatorStateUtil.HasStateNowOrIncoming(animator, upperBodyLayerIndex, stateName);
        }

        // Início do golpe aéreo: só no ar, armado, e só uma vez (não
        // retrigger enquanto IsJumpAttacking() já está rolando). O handoff
        // pra perna (JumpAttackLand) não pode depender de Exit Time do Jump
        // — Jump e o swing têm durações independentes (Jump é físico/
        // variável, o swing é fixo), então precisa de polling por código do
        // estado da UpperBody, mesma técnica que WeaponEquipController já
        // usa em vez de confiar em Exit Time pro Jump/ArmedJumpGrip.
        private void HandleJumpAttack()
        {
            // airTime <= jumpAttackMaxAirTime: apertar tarde demais na queda
            // é ignorado — não sobraria ar suficiente pro swing (mesmo com o
            // Cycle Offset pulando o windup) terminar antes do pouso físico.
            if (!controller.isGrounded && airTime <= jumpAttackMaxAirTime && !IsAbilityModifierHeld() && !IsJumpAttacking()
                && weaponEquip != null && weaponEquip.IsWielded && strongAttackAction.WasPressedThisFrame())
            {
                FaceLockedTargetIfNeeded();
                animator.SetTrigger(AnimStrings.JumpAttack);
            }

            var upperBodyState = animator.GetCurrentAnimatorStateInfo(upperBodyLayerIndex);
            if (upperBodyState.IsName(AnimStrings.ArmedJumpAttack2Alt) && upperBodyState.normalizedTime >= jumpAttackHandoffTime)
            {
                animator.SetTrigger(AnimStrings.JumpAttackLand);
            }

            // As transições antigas Jump -> Locomotion/ArmedLocomotion/Sprint
            // são só por Exit Time, sem gate nenhum — se o clipe do Jump
            // atinge o próprio Exit Time antes do JumpAttackLand disparar
            // (bem provável, o swing pode durar mais que o pouso "normal"),
            // elas roubam a base layer pra fora do Jump antes da transição
            // pro Attack2AltTail ter chance de vencer. Precisam de
            // IsJumpAttacking == false como condição extra no Animator —
            // mesmo racional do bool IsAttacking já usado pra UpperBody não
            // reentrar em ArmedSprintGrip durante o SlideAttack.
            animator.SetBool(AnimStrings.IsJumpAttacking, IsJumpAttacking());
        }

        // Segurar L2 (leftTrigger) é o modificador das habilidades (Heal/
        // AttackMagic) — sem esse gate, apertar Triângulo/Quadrado com L2
        // segurado disparava a habilidade E o ataque forte/combo normal ao
        // mesmo tempo, já que os dois compartilham o mesmo botão físico.
        private bool IsAbilityModifierHeld()
        {
            return abilityModifierAction.ReadValue<float>() >= abilityModifierThreshold;
        }

        // Gira o personagem pro alvo travado no instante em que um golpe é
        // disparado — é o que faz "os ataques ficarem mais direcionados" pro
        // inimigo lockado sem precisar de uma blend tree de strafe 8-direcional
        // (que não existe no lado Unity ainda). Fora de ataque, a locomoção
        // continua encarando a direção do movimento normalmente.
        private void FaceLockedTargetIfNeeded()
        {
            if (targeting == null || !targeting.IsLocked || targeting.CurrentTarget == null)
            {
                return;
            }

            Vector3 toTarget = targeting.CurrentTarget.AimPoint.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(toTarget);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, lockOnFacingLerp);
        }

        private void HandleAttack()
        {
            int effectiveHash = GetEffectiveBaseStateHash();

            if (effectiveHash != previousStateHash)
            {
                comboQueued = false;
                // Mesmo reset por-estado do comboQueued acima — só conta
                // enquanto o Animator ainda está no MESMO estado em que
                // Triângulo foi apertado (hoje só o Attack2 checa isso na
                // saída), não fica pendurado pros outros hits do combo.
                strongComboQueued = false;
                // Idem pro dodge enfileirado durante Attack1Alt1/Attack1Alt2
                // (ver HandleDodge()) — não deixa "vazar" pra outro estado
                // caso o combo continue em vez de terminar em Dodge.
                dodgeQueued = false;
                // Idem pro ataque enfileirado durante o roll (ver abaixo) — o
                // consumo acontece justamente na troca Dodge -> Attack1, então
                // resetar aqui é o que evita ele vazar pro estado seguinte.
                attackQueued = false;
                previousStateHash = effectiveHash;
            }

            if (attackAction.WasPressedThisFrame() && !IsAbilityModifierHeld())
            {
                // Durante o roll o ataque ENFILEIRA, não dispara. Antes esse
                // caso caía direto no branch de atacar, porque IsAttacking()
                // é false no Dodge (a tag lá é "Dodging"): cada aperto
                // chamava FaceLockedTargetIfNeeded() (snap de 85% da rotação
                // pro alvo travado) brigando com o Slerp por frame do
                // HandleMovement() — o "personagem rolando tremendo" — e
                // ainda deixava um trigger "Attack" pendurado, já que o
                // estado Dodge não tem transição nenhuma pra consumi-lo
                // (só ->Sprint/->ArmedLocomotion/->Locomotion, todas por
                // Exit Time). O bool é consumido pela transição nova
                // Dodge -> Attack1 no fim do roll.
                if (IsDodging())
                {
                    attackQueued = weaponEquip == null || weaponEquip.IsWielded;
                }
                else if (weaponEquip != null && weaponEquip.CurrentState == WeaponState.Sheathed)
                {
                    weaponEquip.RequestDraw();
                }
                else if (!IsAttacking() && (weaponEquip == null || weaponEquip.IsWielded))
                {
                    FaceLockedTargetIfNeeded();
                    animator.SetTrigger(AnimStrings.Attack);
                    // Sprint é toggle — sem cancelar, terminar o SlideAttack com o
                    // toggle ainda ligado faria a layer base (ArmedLocomotion ->
                    // DashToSprint) e a UpperBody (Empty -> ArmedSprintGrip) retomarem
                    // o sprint sozinhas em vez de assentar no idle armado. Mas não dá
                    // pra zerar `sprinting` no mesmo frame do SetTrigger: o Animator
                    // veria Attack + Sprint==false ao mesmo tempo, e como
                    // Sprint->ArmedLocomotion (Sprint==false) também fica satisfeita
                    // junto com Sprint->SlideAttack, a ordem das transições na lista
                    // decide qual vence — arriscado. Adiar um frame garante que o
                    // SlideAttack já foi resolvido com Sprint ainda true antes do
                    // toggle cair.
                    pendingSprintCancel = true;
                }
                else
                {
                    comboQueued = true;
                }
            }

            animator.SetBool(AnimStrings.ComboQueued, comboQueued);
            animator.SetBool(AnimStrings.AttackQueued, attackQueued);
            // A UpperBody usa isso pra não reentrar em ArmedSprintGrip enquanto o
            // SlideAttack está tocando na layer base — Sprint/IsWielded sozinhos
            // não bastam, já que os dois continuam true durante o ataque.
            animator.SetBool(AnimStrings.IsAttacking, IsAttacking());
        }

        private void HandleMovement()
        {
            Vector2 moveInput = moveAction.ReadValue<Vector2>();
            Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);
            float inputMagnitude = Mathf.Clamp01(inputDir.magnitude);

            // Rotação continua seguindo o input mesmo atacando (dá pra redirecionar
            // o golpe) — só o Speed (Blend Tree de locomoção) fica congelado
            // durante o ataque, senão a pose de ataque piscaria misturada com o
            // blend de correr.
            if (!IsAttacking())
            {
                animator.SetFloat(AnimStrings.Speed, inputMagnitude, 0.05f, Time.deltaTime);
            }

            // StrongAttack é o golpe pesado/comprometido — ao contrário do combo
            // (que pode redirecionar, comentário acima), a rotação trava assim
            // que o swing começa. O clipe foi baked com todos os canais de Root
            // Transform (posição e rotação), então deixar o código girar o
            // personagem por cima destoaria visivelmente do próprio root motion
            // congelado do clipe.
            if (inputMagnitude > 0.05f && mainCameraTransform != null && !IsStrongAttacking() && !IsJumpAttacking() && !IsSlideAttacking())
            {
                float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + mainCameraTransform.eulerAngles.y;
                Quaternion targetRotation = Quaternion.Euler(0f, targetAngle, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }

        private void HandleSprint()
        {
            if (sprintAction.WasPressedThisFrame())
            {
                sprinting = !sprinting;
            }

            bool hasMoveInput = moveAction.ReadValue<Vector2>().magnitude > 0.05f;
            if (!hasMoveInput)
            {
                // Soltar o analógico cancela o toggle de vez (não só pausa) — andar
                // de novo retoma corrida normal, não o sprint; precisa apertar
                // Shift/L1 de novo pra sprintar.
                sprinting = false;
            }

            animator.SetBool(AnimStrings.Sprint, sprinting);
        }

        private void HandleJump()
        {
            if (!IsAttacking() && controller.isGrounded && jumpAction.WasPressedThisFrame())
            {
                animator.SetTrigger(AnimStrings.Jump);
                verticalVelocity = jumpForce;
            }
        }

        // Mudança de design: Attack2Alt não é mais uma ação avulsa disparada
        // da locomoção — é um branch do combo a partir do Attack2 (não existe
        // mais transição de ArmedLocomotion pra ele). Triângulo durante o
        // combo só marca a fila (mesmo idioma do ComboQueued acima); é o
        // Animator que decide no fim natural do Attack2 se vai pro Attack3
        // ou pro Attack2Alt, lendo o bool StrongComboQueued. Sem gate de
        // IsAttacking() nem SetTrigger — não tem mais entrada direta pra
        // disparar.
        private void HandleStrongAttack()
        {
            if (!IsAbilityModifierHeld() && weaponEquip != null && weaponEquip.IsWielded
                && strongAttackAction.WasPressedThisFrame())
            {
                strongComboQueued = true;
            }

            animator.SetBool(AnimStrings.StrongComboQueued, strongComboQueued);
        }

        // Só dispara em pé no chão — sem gate de IsAttacking() de propósito:
        // Dodge cancela qualquer ataque em andamento (Attack1/2/3,
        // StrongAttack, SlideAttack), decisão explícita pra deixar o combate
        // mais ágil/Nier em vez de "comprometido" tipo Souls. As transições
        // de entrada em Dodge a partir de cada estado de ataque (Has Exit
        // Time off) são o que de fato torna isso possível no Animator — sem
        // elas o trigger fica pendurado sem transição pra consumir. O
        // deslocamento do roll vem do root motion natural do clipe (mesmo
        // branch de chão do OnAnimatorMove), não é calculado aqui.
        //
        // Exceção: os golpes comprometidos (Attack1Alt1/Attack1Alt2/
        // Attack2Alt, ver IsInCommittedAttack()) não usam o cancel imediato — apertar
        // Dodge durante eles só marca `dodgeQueued` (mesmo idioma do
        // comboQueued/strongComboQueued: bool persistente lido pelo Animator
        // via condição, em vez de trigger cru) e o Animator dispara o Dodge
        // sozinho via Has Exit Time quando o golpe termina naturalmente —
        // sem isso, apertar um frame cedo demais faria o trigger cru ficar
        // pendurado sem transição pra consumir (mesma classe de bug já
        // documentada no combo).
        private void HandleDodge()
        {
            if (controller.isGrounded && dodgeAction.WasPressedThisFrame())
            {
                if (IsInCommittedAttack())
                {
                    dodgeQueued = true;
                }
                else
                {
                    animator.SetTrigger(AnimStrings.Dodge);
                }
            }

            animator.SetBool(AnimStrings.DodgeQueued, dodgeQueued);
            // Quem manda a UpperBody segurar/soltar o ArmedDodgeGrip.
            //
            // Antes esse estado saía só por Exit Time — mas o clipe dele
            // (great sword idle) é LOOP de ~2,0s, enquanto o roll da base
            // layer dura ~1,17s. "Exit Time 0.95" ali significava ~1,9s do
            // loop do idle, ou seja, ~0,8s DEPOIS da base layer já ter
            // saído do Dodge. Exit Time em clipe que faz loop nunca vai
            // sincronizar com outra layer; a saída tem que ser por condição.
            animator.SetBool(AnimStrings.IsDodging, IsDodging());
        }

        // Heal dispara a animação; o efeito de cura em si vem de
        // OnHealApplied (Animation Event no clipe "great sword power up
        // (heal)", no frame em que o gesto "completa"). AttackMagic reusa
        // PlayerAttackHitbox.OnAttackHitRadial via Animation Event direto
        // no clipe "spell cast" — mesmo mecanismo já usado pelo giro do
        // slide attack, sem precisar de código novo aqui. As actions já
        // exigem L2 segurado via composite "Button With One Modifier" no
        // InputActionAsset, então não precisa checar o modificador de novo
        // aqui.
        private void HandleAbilities()
        {
            if (IsAttacking())
            {
                return;
            }

            if (healAction.WasPressedThisFrame())
            {
                animator.SetTrigger(AnimStrings.Heal);
            }

            if (attackMagicAction.WasPressedThisFrame())
            {
                animator.SetTrigger(AnimStrings.AttackMagic);
            }
        }

        // Animation Event (float = quantidade curada) no clipe de Heal.
        // Auto-alvo — cura o próprio player direto no HealthComponent, sem
        // OverlapSphere nenhum (diferente de OnAttackHit/OnAttackHitRadial,
        // que miram em quem está na frente/em volta).
        public void OnHealApplied(float amount)
        {
            if (health != null)
            {
                health.Heal(amount);
            }
        }

        private void OnAnimatorMove()
        {
            if (animator == null) return;

            Vector3 rootMotionPosition;
            var baseStateInfo = animator.GetCurrentAnimatorStateInfo(0);

            // Stub pro futuro sistema de dano — só a flag, ninguém consome
            // ainda. Calculado à parte da cadeia de movimento abaixo porque o
            // roll usa o mesmo branch de chão normal (root motion natural do
            // clipe "Standing Dodge Forward", sem deslocamento forçado por
            // código) — só a tag "Dodging" identifica a janela de i-frame.
            IsDodgeInvulnerable = baseStateInfo.IsTag(AnimStrings.Dodging)
                && baseStateInfo.normalizedTime >= dodgeIFrameStart
                && baseStateInfo.normalizedTime <= dodgeIFrameEnd;

            if (baseStateInfo.IsTag(AnimStrings.Dashing))
            {
                // O clipe do dash foi importado com root motion baked out (fica in
                // place) de propósito — o avanço rápido é inteiramente forçado aqui,
                // não vem da animação, pra não depender de quão longe o Mixamo
                // decidiu deslocar o personagem no clipe original. dashRampInTime
                // evita o personagem deslizar em velocidade máxima antes da perna
                // sair da pose de antecipação do início do clipe — é uma fração do
                // tempo normalizado do próprio estado, então acompanha automaticamente
                // se o Speed do estado no Animator mudar.
                float rampT = dashRampInTime > 0f
                    ? Mathf.Clamp01(baseStateInfo.normalizedTime / dashRampInTime)
                    : 1f;
                rootMotionPosition = transform.forward * dashSpeed * rampT * Time.deltaTime;
            }
            else if (baseStateInfo.IsName(AnimStrings.SlideAttack))
            {
                // Mesma técnica do dash — clipe importado com root motion baked out,
                // avanço forçado aqui. Não dá pra usar tag pra identificar esse
                // estado (a tag já é "Attack", precisa dela pro IsAttacking()/combo),
                // então checa por nome do estado em vez de tag. O deslize só cobre a
                // parte inicial do clipe (o avanço em si), com ramp-in no começo e
                // ramp-out terminando exatamente em slideAttackActiveEnd — depois
                // disso é só o golpe parado, sem movimento forçado nenhum.
                if (baseStateInfo.normalizedTime <= slideAttackActiveEnd)
                {
                    float rampIn = slideAttackRampInTime > 0f
                        ? Mathf.Clamp01(baseStateInfo.normalizedTime / slideAttackRampInTime)
                        : 1f;
                    float rampOut = slideAttackRampOutTime > 0f
                        ? Mathf.Clamp01((slideAttackActiveEnd - baseStateInfo.normalizedTime) / slideAttackRampOutTime)
                        : 1f;
                    rootMotionPosition = transform.forward * slideAttackSpeed * rampIn * rampOut * Time.deltaTime;
                }
                else
                {
                    rootMotionPosition = Vector3.zero;
                }
            }
            else if (controller.isGrounded)
            {
                // Também cobre o Dodge (root motion natural do clipe "Standing
                // Dodge Forward", sem deslocamento forçado por código) — o roll
                // só acontece parado no chão, então cai aqui como qualquer outro
                // estado de locomoção natural.
                Vector3 localRootMotion = Quaternion.Inverse(transform.rotation) * animator.deltaPosition;
                localRootMotion.x = 0f;
                localRootMotion.y = 0f;
                rootMotionPosition = transform.rotation * localRootMotion;

                // Guarda a velocidade real de chão (não o Speed normalizado do
                // Animator) pra usar assim que decolar — é isso que faz a decolagem
                // continuar exatamente na velocidade que você já estava indo.
                if (Time.deltaTime > 0f)
                {
                    lastGroundedSpeed = rootMotionPosition.magnitude / Time.deltaTime;
                }
            }
            else
            {
                // No ar (Jump): a velocidade horizontal vem da velocidade capturada
                // no último frame no chão, não do root motion do próprio clipe — o
                // root motion do Jump não tem relação nenhuma com o quão rápido você
                // estava correndo/sprintando antes de pular, o que causava um "pop"
                // de velocidade bem na decolagem. sprintJumpBoost soma um extra só
                // se ainda estiver sprintando, pra um salto mais longo de propósito.
                float boost = animator.GetBool(AnimStrings.Sprint) ? sprintJumpBoost : 0f;
                rootMotionPosition = transform.forward * (lastGroundedSpeed + boost) * Time.deltaTime;
            }

            if (health != null)
            {
                health.IsInvulnerable = IsDodgeInvulnerable;
            }

            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -0.5f;
            }
            else
            {
                verticalVelocity -= gravity * Time.deltaTime;
            }

            Vector3 finalMovement = rootMotionPosition + Vector3.up * verticalVelocity * Time.deltaTime;
            controller.Move(finalMovement);
        }
    }
}
