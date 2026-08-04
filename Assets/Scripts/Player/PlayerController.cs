
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
        // NÃO é 9.81. Gravidade real deixa o pulo flutuante: com 9.81, uma
        // altura de ápice jogável (3-4m) exige uma velocidade inicial tão alta
        // que a subida vira quase linear (a desaceleração é fraca demais pra
        // ler como arco) e a queda demora um tempo enorme. Foi exatamente o
        // sintoma relatado em teste — "sobe em velocidade constante, desce
        // muito devagar".
        //
        // Jogos de ação usam 2-4x a gravidade real justamente por isso. O
        // arco é definido por duas coisas juntas, não por uma:
        //   altura do ápice = jumpForce² / (2 * gravity)
        //   tempo total no ar = 2 * jumpForce / gravity
        // Com gravity 30 e jumpForce 15: ápice ~3.75m, ~1.0s de ar — subida e
        // descida com peso, e um tempo de voo que casa com o ciclo do AirLoop
        // (0.8s), em vez de sobrar meio segundo de flutuação no fim.
        [SerializeField] private float gravity = 30f;
        // Calibrado JUNTO com gravity acima e com launchUpwardForce (em
        // PlayerAttackHitbox) — os três formam um sistema só, mexer em um sem
        // recalcular os outros quebra o encontro do juggle no ar.
        //
        // Como este valor já estava tunado na cena/prefab, mudar aqui só
        // afeta instâncias NOVAS — o campo já salvo no Inspector do objeto
        // real precisa ser atualizado à mão pra valer no Play Mode.
        [SerializeField] private float jumpForce = 15f;
        [SerializeField] private float sprintJumpBoost = 4f;

        [Header("Pulo e combate aéreo")]
        // Segurança pro impulso do pulo, que saiu do frame do input e passou a
        // vir do Animation Event OnJumpTakeOff (no frame em que os pés deixam o
        // chão no clipe Jump Start). Se o event não existir no clipe — clipe
        // reimportado, event apagado sem querer — o trigger dispara, a animação
        // toca e o personagem simplesmente NÃO SOBE, sem erro nenhum no
        // console. Mesmo espírito do watchdog em
        // WeaponEquipController.PollStateExit: o modo de falha silencioso é o
        // caro, então há um prazo e um aviso.
        //
        // Tem que ser MAIOR que o instante em que o event realmente dispara,
        // contado em segundos de tempo de estado (duração do clipe dividida
        // pelo Speed do estado, vezes o tempo normalizado do event). Curto
        // demais e o watchdog vira falso positivo em todo pulo: um aviso no
        // console por salto, e o impulso aplicado antes dos pés saírem do chão.
        [SerializeField] private float jumpTakeOffTimeout = 0.75f;
        // A partir de quando, no tempo normalizado do launcher (Attack1Alt2),
        // o pulo pode cancelar a recuperação dele — é o que abre a janela pra
        // subir atrás do inimigo levantado.
        //
        // O piso real é 0.26: é onde o Animation Event OnAttackHitLaunch mora
        // no clipe (GreatSword_Attack04_Root). Cancelar antes disso pularia
        // ANTES do golpe conectar, ou seja, o launcher nunca levantaria
        // ninguém. 0.3 dá uma margem curta depois do impacto.
        [SerializeField, Range(0f, 1f)] private float launcherJumpCancelStart = 0.3f;
        // Quanta gravidade age no player durante um ataque aéreo. 0 = trava
        // total no ar enquanto o golpe toca (o padrão de character action, e o
        // que faz o juggle fechar na prática — sem isso o player despenca e sai
        // do alcance do inimigo no meio do combo); 1 = cai normalmente.
        //
        // Slider e não bool porque o meio-termo é jogável: 0.2 faz o player
        // afundar de leve durante o golpe, o que lê como peso sem perder o
        // alvo.
        [SerializeField, Range(0f, 1f)] private float airAttackGravityScale = 0f;
        // Empurrão vertical aplicado no INSTANTE em que um golpe aéreo começa,
        // em unidades de velocidade (mesma escala do jumpForce).
        //
        // Existe porque nem toda descida durante o golpe vem da física: os
        // dois clipes aéreos estão com Root Transform Position (Y) em Bake
        // Into Pose, então o movimento vertical DELES fica no visual, e o
        // corpo afunda um pouco dentro do próprio swing por mais que a
        // gravidade esteja travada em airAttackGravityScale = 0. Não dá pra
        // corrigir isso zerando velocidade nenhuma — não é velocidade, é pose.
        //
        // Um valor pequeno e positivo (2-4) compensa: o personagem ganha uma
        // subidinha na entrada do golpe que cancela visualmente o afundamento
        // do clipe. É também o idioma de vários character action games, onde
        // atacar no ar dá um "pop" pra cima e sustenta o combo.
        //
        // 0 = desligado (comportamento anterior).
        [SerializeField, Range(0f, 8f)] private float airAttackLift = 3f;

        [Header("Dodge")]
        // O roll continua sendo root motion do clipe "Standing Dodge Forward"
        // (mesmo branch de chão normal do OnAnimatorMove) — o PERFIL de
        // velocidade (o arranque, o pico no meio do rolamento, a
        // desaceleração no fim) vem inteiro da animação, como em
        // Sprint/Locomotion. Este campo só multiplica o resultado: 1 = a
        // distância autorada no clipe, 1.5 = 50% mais longe com exatamente a
        // mesma cara.
        //
        // É deliberadamente um multiplicador do root motion e não um avanço
        // forçado (o idioma do Dash/SlideAttack, que importavam o clipe com
        // root motion baked out e deslocavam 100% por código — os dois foram
        // removidos, mas o contraste continua valendo): aqueles eram deslizes
        // de velocidade constante, onde o clipe não tem deslocamento pra
        // preservar. Aqui tem, e é justamente ele que faz o roll parecer um
        // roll.
        //
        // Alternativa sem código, se preferir mexer só no clipe: a curva
        // "Lunge" (Import Settings -> Animation -> Curves) já é somada ao root
        // motion neste mesmo branch, sem checagem de tag — ver AnimStrings.Lunge
        // e o uso em OnAnimatorMove. A diferença é que ela SOMA velocidade
        // extra (podendo achatar o perfil do clipe) em vez de escalar o que já
        // existe.
        //
        // Este é o valor de TOQUE; segurar o botão sobe pro campo de baixo.
        [SerializeField, Range(0.1f, 4f)] private float dodgeRootMotionScale = 1.8f;
        // Escala alvo enquanto o botão de dodge continua PRESSIONADO.
        //
        // Não dá pra decidir toque vs. segurado no frame do aperto (nesse
        // instante os dois são idênticos — a diferença só existe no futuro), e
        // atrasar o disparo pra descobrir custaria latência num input que hoje
        // sai no MESMO frame. Então o roll começa sempre no valor de toque e
        // SOBE se o botão continuar segurado, igual pulo variável por altura.
        [SerializeField, Range(0.1f, 4f)] private float dodgeHeldRootMotionScale = 2.5f;
        // Quanto tempo a subida de toque -> segurado leva. Rampa e não troca
        // seca de propósito: 1.8 -> 2.5 é +39% de velocidade instantânea, e
        // aplicar isso de uma vez no meio do rolamento dá um pop bem visível.
        // Em ~0.1s a transição já lê como binária pro jogador, mas sem o
        // degrau — e um toque um pouco mais longo cai naturalmente num valor
        // intermediário em vez de num degrau arbitrário.
        [SerializeField] private float dodgeHoldRampTime = 0.1f;
        // Até quando, contado do começo do roll, segurar ainda pode aumentar a
        // distância. Fecha a janela pra não dar pra "empurrar" o roll segurando
        // o botão até o fim do clipe — passado esse tempo o valor congela no
        // que chegou, mesmo com o botão pressionado.
        [SerializeField] private float dodgeHoldWindow = 0.22f;
        // Diagnóstico temporário: loga, no fim de cada roll, a escala que ele
        // efetivamente usou e quanto o personagem andou de verdade. Serve pra
        // separar "a escala não está sendo aplicada" de "a escala está sendo
        // aplicada mas o clipe quase não tem root motion pra escalar" — os dois
        // se parecem exatamente igual jogando. Pode apagar junto com
        // dodgeStartPosition e o bloco no UpdateDodgeScale quando não precisar
        // mais.
        [SerializeField] private bool logDodgeDistance;
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
        // Multiplicador de playback só pros estados de combate (Attack1/2/3,
        // Attack1Alt1/Alt2, Attack2Alt, AirAttack1/2) — ligado ao Speed desses
        // estados no Animator via "Parameter" (não afeta locomoção/idle).
        // Testável ao vivo em Play Mode arrastando o slider.
        [SerializeField, Range(0.5f, 2.5f)] private float combatSpeedMultiplier = 1f;

        [Header("Lunge de ataque")]
        // Magnitude global do avanço extra dos ataques. A FORMA (quando começa,
        // onde é o pico, quando zera) vem da curva "Lunge" autorada dentro de
        // cada clipe; este slider só escala tudo junto, pra dar pra calibrar a
        // sensação ao vivo em Play Mode sem reabrir Import Settings de seis
        // clipes — mesma divisão de papéis que combatSpeedMultiplier tem com o
        // Speed de cada estado.
        //
        // Em 0 o sistema inteiro fica inerte, então também serve de kill switch
        // se o avanço extra brigar com alguma animação nova.
        [SerializeField, Range(0f, 3f)] private float lungeScale = 1f;

        [Header("Janela de combo")]
        // Fração inicial de CADA golpe em que o combo ainda não pode avançar:
        // apertar antes disso enfileira (o bool comboQueued segura o input) e
        // o golpe seguinte só entra quando a janela abre. Depois dela, o
        // encadeamento é imediato.
        //
        // Mora aqui e não como Exit Time nas transições porque Exit Time no
        // Unity NÃO é um piso: a condição vira true no frame em que o clipe
        // cruza o valor e volta a ser false depois. Com Exit Time 0.2, apertar
        // aos 50% não encadeava nunca — só valia apertar ANTES dos 20% e
        // esperar o cruzamento, exatamente o oposto do desejado. A solução é a
        // mesma aplicada ao IsJumping/AirLoop: um ponto de verdade em código,
        // com as transições em Has Exit Time OFF.
        [SerializeField, Range(0f, 1f)] private float comboWindowStart = 0.2f;

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
        // "O player está no ar?" — latch, não `!controller.isGrounded` cru.
        //
        // O valor cru não serve como fonte de verdade aqui por causa das duas
        // pontas do pulo. Na SUBIDA: entre o SetTrigger(Jump) e o Animation
        // Event de decolagem o clipe Jump Start está agachando e os pés ainda
        // tocam o chão, então o valor cru diria "no chão" com o pulo já em
        // andamento — e o Animator sairia do AirLoop pro JumpEnd na hora. No
        // POUSO: CharacterController.isGrounded oscila entre frames em contato
        // raspante, e cada oscilação seria uma ida e volta ao JumpEnd.
        //
        // Aberto por ApplyJumpImpulse() e fechado só quando o personagem está no
        // chão E já parou de subir — as duas condições juntas, pra um
        // encostão em teto ou rampa no meio da subida não contar como pouso.
        private bool airborne;
        // Borda do ataque aéreo, pra zerar a velocidade vertical uma vez na
        // entrada em vez de todo frame (ver OnAnimatorMove). Mesmo idioma do
        // wasDodging logo abaixo.
        private bool wasAirAttacking;
        // Prazo do watchdog de decolagem (ver jumpTakeOffTimeout). Negativo =
        // nenhum pulo esperando o event.
        private float takeOffWatchdog = -1f;
        // Pulo apertado durante o launcher (Attack1Alt2) antes da janela de
        // cancel abrir — ver HandleJump()/CanLauncherJumpCancel().
        private bool jumpQueued;
        private bool comboQueued;
        private bool strongComboQueued;
        private bool dodgeQueued;
        // Intenção de dodge AINDA NÃO disparada, esperando a janela abrir.
        // Separado do dodgeQueued: aquele é o dodge que sai no fim de um golpe
        // comprometido (lido por condição no Animator), este é o dodge-cancel
        // normal, que continua saindo por trigger — só que não mais no frame do
        // aperto se o golpe ainda estiver na janela inicial.
        private bool dodgePressed;
        // Estado da escala variável do roll (ver dodgeRootMotionScale e
        // companhia). Resolvido por BORDA da tag "Dodging" em OnAnimatorMove, e
        // não em HandleDodge(), porque o roll tem dois caminhos de entrada: o
        // trigger disparado por código e o DodgeQueued, que quem dispara é o
        // Animator no fim de um golpe comprometido — só o segundo já bastaria
        // pra um reset feito no lado do input não cobrir todos os casos.
        private float dodgeScale = 1f;
        private float dodgeElapsed;
        private bool wasDodging;
        private Vector3 dodgeStartPosition;
        private bool attackQueued;
        private bool sprinting;
        private bool pendingSprintCancel;
        private int previousStateHash;
        private int upperBodyLayerIndex;

        // Stub pro futuro sistema de dano — só a janela de invulnerabilidade
        // calculada, sem nenhum consumidor ainda.
        public bool IsDodgeInvulnerable { get; private set; }

        private InputAction moveAction;
        private InputAction attackAction;
        private InputAction jumpAction;
        private InputAction sprintAction;
        private InputAction dodgeAction;
        private InputAction strongAttackAction;

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

            // Ver o comentário em WeaponEquipController.JumpTakeOff: o
            // Animation Event cai lá (mesmo GameObject do Animator), não
            // aqui, então é assinatura de evento, não SendMessage direto.
            if (weaponEquip != null)
            {
                weaponEquip.JumpTakeOff += ApplyJumpImpulse;
            }
        }

        private void OnDisable()
        {
            if (weaponEquip != null)
            {
                weaponEquip.JumpTakeOff -= ApplyJumpImpulse;
            }

            moveAction.Disable();
            attackAction.Disable();
            jumpAction.Disable();
            sprintAction.Disable();
            dodgeAction.Disable();
            strongAttackAction.Disable();
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

            HandleAttack();
            HandleStrongAttack();
            HandleMovement();
            HandleSprint();
            HandleJump();
            HandleDodge();
            animator.SetFloat(AnimStrings.CombatSpeedMultiplier, combatSpeedMultiplier);

            // Pouso. Exige as DUAS condições: no chão E não subindo mais.
            // Só isGrounded bastaria pra fechar o latch cedo demais num
            // encostão de rampa durante a subida — e fechar o latch é o que
            // manda o Animator sair do AirLoop pro JumpEnd, ou seja, o
            // personagem "aterrissaria" no meio do ar.
            if (airborne && controller.isGrounded && verticalVelocity <= 0f)
            {
                airborne = false;
            }

            // Único ponto que escreve o airborne no Animator. É o que segura o
            // AirLoop (que é loop e não tem Exit Time pra sair) e o que dispara
            // o JumpEnd no pouso — o tempo de ar é físico, não tem relação
            // nenhuma com a duração de clipe nenhum.
            animator.SetBool(AnimStrings.IsJumping, airborne);
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
        // usada pelos ataques aéreos em IsAirAttacking().
        private bool IsStrongAttacking()
        {
            return AnimatorStateUtil.HasStateNowOrIncoming(animator, 0, AnimStrings.Attack2Alt);
        }

        // Golpes COMPROMETIDOS: ao contrário do resto do combo (Attack1/2/3),
        // Dodge não cancela na hora durante eles — só enfileira
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

        // Os dois ataques aéreos. Por NOME de estado e não por tag pelo motivo
        // já documentado em AnimStrings.AirAttack1: a tag deles é "Attack" (é o
        // que faz o combo, a trava de rotação e o bloqueio de Draw/Sheath
        // valerem no ar de graça), e estado do Unity só tem uma tag.
        //
        // Semântica EFETIVA (estado de destino durante o crossfade), igual ao
        // IsDodging(): quem consome isto é a suspensão de gravidade em
        // OnAnimatorMove, e ela precisa valer desde o primeiro frame do blend.
        // Esperar o crossfade terminar deixaria o player despencar durante a
        // entrada do golpe — justo o instante em que ele precisa parar no ar.
        private bool IsAirAttacking()
        {
            return AnimatorStateUtil.EffectiveIsName(animator, 0, AnimStrings.AirAttack1)
                || AnimatorStateUtil.EffectiveIsName(animator, 0, AnimStrings.AirAttack2);
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
                // Idem pro dodge-cancel esperando a janela: se o estado trocou
                // antes dela abrir (o combo emendou, o golpe terminou), a
                // intenção morre com o golpe em que foi apertada em vez de
                // disparar um roll fora de hora no estado seguinte.
                dodgePressed = false;
                // Idem pro ataque enfileirado durante o roll (ver abaixo) — o
                // consumo acontece justamente na troca Dodge -> Attack1, então
                // resetar aqui é o que evita ele vazar pro estado seguinte.
                attackQueued = false;
                // Idem pro pulo enfileirado durante o launcher (ver
                // HandleJump()/CanLauncherJumpCancel()) — se o Attack1Alt2
                // terminar sem a janela de cancel ter aberto (o combo emendou
                // pro Attack3 via ComboQueued, por exemplo), a intenção morre
                // com o golpe em vez de disparar um pulo fora de hora já
                // dentro do estado seguinte.
                jumpQueued = false;
                previousStateHash = effectiveHash;
            }

            if (attackAction.WasPressedThisFrame())
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
                    // Sprint é toggle — sem cancelar, terminar um ataque
                    // disparado a partir do sprint com o toggle ainda ligado
                    // faria a layer base e a UpperBody (Empty ->
                    // ArmedSprintGrip) retomarem o sprint sozinhas em vez de
                    // assentar no idle armado. Mas não dá pra zerar `sprinting`
                    // no mesmo frame do SetTrigger: o Animator veria Attack +
                    // Sprint==false ao mesmo tempo, e como
                    // ArmedSprint->ArmedLocomotion (Sprint==false,
                    // IsAttacking==false) fica satisfeita junto com
                    // ArmedSprint->Attack1, a ordem das transições na lista
                    // decide qual vence — arriscado. Adiar um frame garante que
                    // o ataque já foi resolvido com Sprint ainda true antes do
                    // toggle cair.
                    pendingSprintCancel = true;
                }
                else
                {
                    comboQueued = true;
                }
            }

            // O bool só chega no Animator depois que a janela abre — é isso
            // que separa "enfileirado" de "encadeia agora". comboQueued (o
            // campo) continua guardando a intenção desde o instante do aperto;
            // ComboWindowOpen() decide quando ela vale.
            animator.SetBool(AnimStrings.ComboQueued, comboQueued && ComboWindowOpen());
            animator.SetBool(AnimStrings.AttackQueued, attackQueued);
            // A UpperBody usa isso pra não reentrar em ArmedSprintGrip enquanto
            // um ataque está tocando na layer base — Sprint/IsWielded sozinhos
            // não bastam, já que continuam true durante o golpe. (O pulo não
            // tem overlay própria pra proteger — os clipes de pulo já são
            // armados, então a UpperBody fica em Empty o tempo todo, dentro ou
            // fora de ataque.)
            animator.SetBool(AnimStrings.IsAttacking, IsAttacking());
        }

        // "O golpe atual já passou da janela mínima?" — o gate que substitui o
        // Exit Time das transições de combo (ver comboWindowStart).
        private bool ComboWindowOpen()
        {
            // Crossfade de ENTRADA num golpe: o estado ATUAL ainda é a origem
            // (ArmedLocomotion, Dodge, ArmedSprint...) e o normalizedTime dele
            // não diz nada sobre o golpe que está chegando — que está em ~0%.
            // Sem isso, um aperto durante os 0,25s de entrada abriria a janela
            // na hora e o segundo hit emendaria antes do primeiro existir (o
            // "spamma 3x e cai direto no Attack3").
            if (animator.IsInTransition(0))
            {
                return !animator.GetNextAnimatorStateInfo(0).IsTag(AnimStrings.Attack);
            }

            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (!state.IsTag(AnimStrings.Attack))
            {
                return true;
            }

            return state.normalizedTime >= comboWindowStart;
        }

        private void HandleMovement()
        {
            Vector2 moveInput = moveAction.ReadValue<Vector2>();
            Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);
            float inputMagnitude = Mathf.Clamp01(inputDir.magnitude);

            // Speed (Blend Tree de locomoção) fica congelado durante o ataque,
            // senão a pose de ataque piscaria misturada com o blend de correr.
            if (!IsAttacking())
            {
                animator.SetFloat(AnimStrings.Speed, inputMagnitude, 0.05f, Time.deltaTime);
            }

            // Rotação trava assim que QUALQUER ataque começa, leve ou pesado —
            // desde a Fase 1 os clipes do combo leve também carregam root
            // motion próprio (não só o StrongAttack), então deixar o código
            // girar o personagem por cima destoaria do avanço já autorado no
            // clipe em qualquer um deles.
            if (inputMagnitude > 0.05f && mainCameraTransform != null && !IsAttacking())
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

        // O trigger é tudo que sai daqui — o IMPULSO mudou de lugar e agora vem
        // do Animation Event OnJumpTakeOff, no frame em que os pés deixam o
        // chão no clipe Jump Start.
        //
        // Antes os dois aconteciam no frame do input, e o personagem subia
        // durante o agachamento de antecipação do clipe: a pose dizia "estou
        // tomando impulso" enquanto o corpo já estava a meio metro do chão. Com
        // o pulo em três estados isso pioraria — o Jump Start inteiro tocaria
        // no ar.
        //
        // O gate de ataque deixou de ser um !IsAttacking() cru: o pulo agora
        // cancela o launcher (Attack1Alt2) na janela em que o inimigo ainda
        // está subindo, que é o que abre o juggle. Continua bloqueado durante
        // qualquer OUTRO golpe — a exceção é do launcher, não do combate
        // inteiro.
        //
        // O aperto durante o launcher ENFILEIRA (jumpQueued), mesmo idioma do
        // resto do combo (comboQueued/strongComboQueued/dodgeQueued/
        // attackQueued) — e pelo mesmo motivo: a versão anterior só checava
        // CanLauncherJumpCancel() no exato frame do aperto. Apertar um frame
        // antes da janela abrir (bem provável — a reação natural é apertar
        // assim que o golpe começa a tocar, não esperar contar até 0.30) fazia
        // o input ser descartado pra sempre, sem nenhum feedback; a única
        // saída era apertar de NOVO já dentro da janela, o que lê como "o
        // cancel não responde".
        private void HandleJump()
        {
            if (takeOffWatchdog >= 0f)
            {
                takeOffWatchdog -= Time.deltaTime;
                if (takeOffWatchdog < 0f)
                {
                    Debug.LogWarning("[Jump] OnJumpTakeOff não chegou — o Animation " +
                        "Event some do clipe Jump Start? Aplicando o impulso pelo watchdog.");
                    ApplyJumpImpulse();
                }
            }

            if (controller.isGrounded && jumpAction.WasPressedThisFrame())
            {
                // Só o launcher é cancelável — outros golpes (Attack1/2/3,
                // Attack1Alt1, Attack2Alt) não têm a transição de cancel no
                // Animator, então enfileirar ali deixaria jumpQueued preso até
                // o reset por troca de estado, sem nunca disparar. Igual ao
                // IsDodging() abaixo: o aperto nesses casos é descartado
                // mesmo, não enfileirado.
                if (AnimatorStateUtil.HasStateNowOrIncoming(animator, 0, AnimStrings.Attack1Alt2))
                {
                    jumpQueued = true;
                }
                else if (!IsAttacking() && !IsDodging())
                {
                    // Bug pré-existente, não introduzido pelo combate aéreo: o
                    // roll acontece no chão e passava por todos os gates
                    // acima (a tag do Dodge é "Dodging", então IsAttacking()
                    // é false lá), mas o estado Dodge não tem transição
                    // nenhuma pro pulo. O trigger ficava pendurado e disparava
                    // quando o roll terminasse — um pulo fantasma, atrasado,
                    // que ninguém pediu naquele instante.
                    //
                    // Bloquear (não enfileirar) é o conserto conservador:
                    // mantém o que o jogo faz hoje de fato (não pular durante
                    // o roll) sem o efeito colateral. Se roll -> pulo virar
                    // mecânica desejada, o conserto é o oposto — tirar o
                    // IsDodging() daqui e desenhar Dodge -> JumpStart no
                    // Animator, como o dodge-cancel dos ataques já faz.
                    FireJumpTrigger();
                }
            }

            // Consumo do pulo enfileirado: dispara no EXATO frame em que a
            // janela abre, não só quando o botão é apertado de novo dentro
            // dela — é isso que faz o cancel responder assim que fica
            // possível, em vez de exigir um segundo aperto cronometrado.
            if (jumpQueued && CanLauncherJumpCancel())
            {
                FireJumpTrigger();
                jumpQueued = false;
            }
        }

        private void FireJumpTrigger()
        {
            animator.SetTrigger(AnimStrings.Jump);
            takeOffWatchdog = jumpTakeOffTimeout;
        }

        // "O launcher já conectou e ainda dá pra subir atrás do inimigo?"
        //
        // Attack1Alt2 é o único golpe cancelável por pulo. O piso de tempo
        // existe porque o Animation Event que de fato levanta o inimigo
        // (OnAttackHitLaunch) mora em 0.26 do clipe: cancelar antes disso
        // trocaria o launcher por um pulo sem ninguém no ar pra perseguir.
        private bool CanLauncherJumpCancel()
        {
            if (!AnimatorStateUtil.HasStateNowOrIncoming(animator, 0, AnimStrings.Attack1Alt2))
            {
                return false;
            }

            // Crossfade de ENTRADA no launcher: o normalizedTime legível ainda
            // é o do estado de origem e não diz nada sobre o golpe que está
            // chegando — que está em ~0%, ou seja, muito antes da janela.
            // Mesma pegadinha tratada em ComboWindowOpen().
            if (animator.IsInTransition(0))
            {
                return false;
            }

            return animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= launcherJumpCancelStart;
        }

        // Chamado pelo evento WeaponEquipController.JumpTakeOff (ver lá o
        // porquê de não ser este método diretamente o alvo do Animation
        // Event) e pelo watchdog acima, se o event nunca chegar.
        private void ApplyJumpImpulse()
        {
            verticalVelocity = jumpForce;
            airborne = true;
            takeOffWatchdog = -1f;
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
            if (weaponEquip != null && weaponEquip.IsWielded
                && strongAttackAction.WasPressedThisFrame())
            {
                strongComboQueued = true;
            }

            // Mesmo gate de janela do ComboQueued (ver ComboWindowOpen) — o
            // galho pesado sofria do mesmo problema de Exit Time: apertar
            // Triângulo depois do cruzamento não fazia mais nada.
            animator.SetBool(AnimStrings.StrongComboQueued, strongComboQueued && ComboWindowOpen());
        }

        // Só dispara em pé no chão — sem gate de IsAttacking() de propósito:
        // Dodge cancela qualquer ataque em andamento (Attack1/2/3,
        // Attack2Alt), decisão explícita pra deixar o combate
        // mais ágil/Nier em vez de "comprometido" tipo Souls. As transições
        // de entrada em Dodge a partir de cada estado de ataque (Has Exit
        // Time off) são o que de fato torna isso possível no Animator — sem
        // elas o trigger fica pendurado sem transição pra consumir. O
        // deslocamento do roll vem do root motion do clipe (mesmo branch de
        // chão do OnAnimatorMove, só multiplicado por dodgeRootMotionScale
        // lá), não é calculado aqui.
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
                    dodgePressed = true;
                }
            }

            // Fora de ataque a janela já está aberta, então isto dispara no
            // MESMO frame do aperto — o dodge continua instantâneo na
            // locomoção, no sprint e no ar. Dentro de um golpe, segura a
            // intenção até a janela abrir, em vez de cancelar no windup.
            if (dodgePressed && ComboWindowOpen())
            {
                animator.SetTrigger(AnimStrings.Dodge);
                dodgePressed = false;
            }

            // Mesmo gate de janela do combo (ver ComboWindowOpen). Vale só pro
            // dodge ENFILEIRADO — o dodge normal continua sendo trigger cru
            // disparado na hora logo acima, sem janela nenhuma; a decisão de
            // cancelar na hora fora dos golpes comprometidos não mudou.
            animator.SetBool(AnimStrings.DodgeQueued, dodgeQueued && ComboWindowOpen());

            bool isDodging = IsDodging();

            // Nier-style: o roll SEMPRE emenda num sprint, esteja você andando
            // normal ou já sprintando antes de apertar — não precisa segurar o
            // botão de sprint pra sair do dodge correndo.
            //
            // Escrito todo frame que `isDodging` for true, não só na borda de
            // entrada: é só isso que HandleSprint() precisa, já que ele lê
            // `sprinting` e escreve o bool Sprint no Animator sozinho todo
            // frame — inclusive cancelando se o analógico for solto no meio ou
            // depois do roll, mesma regra que já vale pro toggle manual. Não
            // duplica lógica de cancelamento aqui.
            //
            // As saídas do Dodge pro Sprint/ArmedSprint no Animator não checam
            // mais o bool Sprint pra decidir o destino (só IsWielded) —
            // justamente pra ser ISTO aqui, e não o estado de antes do dodge, a
            // decidir se você sai correndo.
            if (isDodging)
            {
                sprinting = true;
            }

            // Quem manda a UpperBody segurar/soltar o ArmedDodgeGrip.
            //
            // Antes esse estado saía só por Exit Time — mas o clipe dele
            // (great sword idle) é LOOP de ~2,0s, enquanto o roll da base
            // layer dura ~1,17s. "Exit Time 0.95" ali significava ~1,9s do
            // loop do idle, ou seja, ~0,8s DEPOIS da base layer já ter
            // saído do Dodge. Exit Time em clipe que faz loop nunca vai
            // sincronizar com outra layer; a saída tem que ser por condição.
            animator.SetBool(AnimStrings.IsDodging, isDodging);
        }

        // Resolve a escala de root motion do roll DESTE frame e devolve se o
        // roll está em andamento.
        //
        // Tag EFETIVA (mesma semântica que IsDodging() já usa) e não
        // baseStateInfo.IsTag: durante o crossfade de entrada o estado ATUAL
        // ainda é o de origem, então a versão crua só começaria a escalar
        // quando o blend TERMINASSE — bem no meio da parte rápida do roll,
        // dando um degrau de velocidade visível. Aqui a escala vale desde o
        // primeiro frame da transição; o pouco de root motion do clipe de
        // origem que entra escalado junto é desprezível (sai de locomoção ou de
        // um ataque, que quase não saem do lugar).
        private bool UpdateDodgeScale()
        {
            bool dodging = AnimatorStateUtil.EffectiveHasTag(animator, 0, AnimStrings.Dodging);

            if (!dodging)
            {
                if (wasDodging && logDodgeDistance)
                {
                    Vector3 travelled = transform.position - dodgeStartPosition;
                    travelled.y = 0f;
                    Debug.Log($"[Dodge] escala={dodgeScale:F2} distancia={travelled.magnitude:F2}m");
                }

                wasDodging = false;
                return false;
            }

            if (!wasDodging)
            {
                // Roll novo sempre começa no valor de TOQUE. Segurar só sobe a
                // partir daqui, nunca desce: assim não existe frame nenhum em
                // que o roll saia mais curto do que o toque prometeu, que é o
                // que aconteceria se a gente chutasse "segurado" na entrada e
                // corrigisse pra baixo ao ver o botão soltar.
                wasDodging = true;
                dodgeElapsed = 0f;
                dodgeScale = dodgeRootMotionScale;
                dodgeStartPosition = transform.position;
            }

            dodgeElapsed += Time.deltaTime;

            if (!dodgeAction.IsPressed()
                || dodgeElapsed > dodgeHoldWindow
                || dodgeScale >= dodgeHeldRootMotionScale)
            {
                return true;
            }

            // MoveTowards e não Lerp: rampa de velocidade constante, então
            // dodgeHoldRampTime é literalmente "quanto tempo leva do valor de
            // toque até o de segurado", em vez de uma aproximação assintótica
            // que nunca chega no alvo.
            float rate = dodgeHoldRampTime > 0f
                ? (dodgeHeldRootMotionScale - dodgeRootMotionScale) / dodgeHoldRampTime
                : float.MaxValue;
            dodgeScale = Mathf.MoveTowards(dodgeScale, dodgeHeldRootMotionScale, rate * Time.deltaTime);

            return true;
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
            //
            // Tag CRUA aqui de propósito, ao contrário do UpdateDodgeScale()
            // logo abaixo: esta checagem depende do normalizedTime do estado, e
            // durante o crossfade de entrada o normalizedTime que se lê é o do
            // estado de ORIGEM — casar ele com a tag de destino compararia a
            // janela de i-frame contra o progresso do clipe errado.
            IsDodgeInvulnerable = baseStateInfo.IsTag(AnimStrings.Dodging)
                && baseStateInfo.normalizedTime >= dodgeIFrameStart
                && baseStateInfo.normalizedTime <= dodgeIFrameEnd;

            bool dodging = UpdateDodgeScale();

            // O `&& !controller.isGrounded` não é redundante: dá pra pousar no
            // MEIO de um ataque aéreo (o Animator tem saída por IsJumping pro
            // JumpEnd, mas o golpe pode encostar no chão um frame antes dela
            // resolver). Sem o gate, esse frame rodaria com gravidade
            // suspensa e sem o -0.5 que gruda o CharacterController no chão —
            // o suficiente pro isGrounded oscilar e o pouso engasgar.
            bool airAttacking = IsAirAttacking() && !controller.isGrounded;

            // Borda de ENTRADA do golpe aéreo: mata a velocidade de QUEDA, mas
            // preserva a de SUBIDA.
            //
            // A versão anterior zerava as duas ("pra altura do combo não
            // depender de quando você apertou"), e isso ficou ruim jogando:
            // atacar logo depois de pular, ainda subindo forte, matava o
            // impulso no meio e o personagem parava numa altura bem menor que
            // a do pulo — lido como "meu personagem deu uma descida" e "não
            // ataquei na altura que eu esperava". Ele não descia de verdade,
            // só parava de subir de repente, que é visualmente parecido.
            //
            // Preservando a subida (e deixando a gravidade CHEIA agir nela até
            // o ápice, ver o bloco de gravidade abaixo), o arco do pulo termina
            // naturalmente e só então o hover assume — atacar cedo passa a dar
            // o golpe no topo do pulo, que é onde a intuição manda.
            if (airAttacking && !wasAirAttacking)
            {
                // O lift é somado DEPOIS do clamp, não antes: somar primeiro
                // faria uma queda rápida (velocidade bem negativa) engolir o
                // empurrão inteiro e o golpe sairia sem lift nenhum justo no
                // caso em que ele é mais necessário.
                verticalVelocity = Mathf.Max(verticalVelocity, 0f) + airAttackLift;
            }

            wasAirAttacking = airAttacking;

            if (controller.isGrounded)
            {
                // Também cobre o Dodge (root motion do clipe "Standing Dodge
                // Forward", sem deslocamento forçado por código — só um
                // multiplicador logo abaixo) — o roll só acontece parado no
                // chão, então cai aqui como qualquer outro estado de locomoção
                // natural.
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

                // Escala do roll, DEPOIS do lastGroundedSpeed pelo mesmo
                // motivo que o Lunge logo abaixo: aquilo alimenta o impulso
                // horizontal do salto, e um roll mais longo não deve virar
                // velocidade de decolagem — a medição tem que continuar sendo
                // a velocidade de locomoção real do clipe.
                //
                // Multiplica o vetor inteiro (que aqui já é só o eixo forward,
                // com x/y zerados acima), então preserva a curva de velocidade
                // do clipe em vez de somar um valor constante por cima.
                //
                // dodgeScale (não o campo dodgeRootMotionScale direto) porque o
                // valor é variável: começa no de toque e sobe enquanto o botão
                // ficar segurado. Quem cuida disso é UpdateDodgeScale().
                if (dodging)
                {
                    rootMotionPosition *= dodgeScale;
                }

                // Avanço extra dos ataques, SOMADO ao root motion, não
                // substituindo ele. Aqui o clipe continua mandando no
                // deslocamento base; a curva só preenche o que falta pro golpe
                // "avançar" de verdade, porque os clipes de ataque atuais quase
                // não saem do lugar.
                //
                // Sem checagem de tag de propósito: clipe sem a curva "Lunge"
                // não contribui pro parâmetro, então isto é exatamente zero fora
                // dos ataques. Deixar o Animator decidir também resolve o
                // crossfade de graça — GetCurrentAnimatorStateInfo reportaria o
                // estado de ORIGEM durante o blend (mesma pegadinha do
                // GetEffectiveBaseStateHash), enquanto GetFloat já devolve o
                // valor blendado entre os dois clipes.
                //
                // Depois do lastGroundedSpeed de propósito: aquilo alimenta o
                // impulso horizontal do salto, e ataque não deve virar
                // velocidade de decolagem; contaminar a medição só criaria um
                // pico fantasma difícil de rastrear.
                //
                // Isso deixou de ser teórico: desde o cancel do launcher
                // (HandleJump não exige mais !IsAttacking() cru) DÁ pra decolar
                // de dentro de um golpe. O lunge do Attack1Alt2 no frame do
                // cancel viraria velocidade horizontal de pulo se estivesse
                // somado antes desta linha.
                rootMotionPosition += transform.forward
                    * animator.GetFloat(AnimStrings.Lunge) * lungeScale * Time.deltaTime;
            }
            else if (airAttacking)
            {
                // Ataque aéreo: aqui o root motion do PRÓPRIO clipe manda, ao
                // contrário do resto do voo logo abaixo. Os dois clipes são
                // _Root e o avanço deles é autorado como parte do golpe;
                // arrastar o personagem na velocidade em que ele estava
                // correndo antes de pular passaria por cima disso.
                Vector3 localRootMotion = Quaternion.Inverse(transform.rotation) * animator.deltaPosition;
                localRootMotion.x = 0f;
                localRootMotion.y = 0f;
                rootMotionPosition = transform.rotation * localRootMotion;

                // O golpe aéreo ANCORA o player: o resto da queda, depois que
                // ele terminar, é vertical. Sem isto o branch de ar lá embaixo
                // retomaria a velocidade de corrida guardada na decolagem e o
                // personagem sairia deslizando pra frente no fim do combo,
                // saindo de cima do inimigo que ele acabou de pendurar.
                lastGroundedSpeed = 0f;
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
            else if (airAttacking && verticalVelocity > 0f)
            {
                // AINDA SUBINDO: gravidade CHEIA, como num pulo normal. O
                // hangtime não começa aqui — deixa o arco do salto terminar
                // primeiro, senão atacar cedo congelaria o player no meio da
                // subida (ver o comentário da borda de entrada lá em cima).
                //
                // Clampa em 0 em vez de deixar passar pra negativo: é o ponto
                // exato em que a subida vira hover, e passar direto daria um
                // frame de queda antes do branch de baixo assumir.
                verticalVelocity = Mathf.Max(0f, verticalVelocity - gravity * Time.deltaTime);
            }
            else if (airAttacking)
            {
                // Já no ápice (ou entrou no golpe caindo, e a borda de entrada
                // zerou a queda): agora sim o hangtime. Com
                // airAttackGravityScale em 0 ele trava nessa altura até o
                // ataque acabar.
                //
                // Só integra a fração de gravidade — não zera nada aqui. Fazer
                // as duas coisas no mesmo lugar (zerar todo frame e depois
                // subtrair) daria uma velocidade CONSTANTE em vez de uma
                // aceleração: com scale 0.2 o player desceria numa taxa fixa,
                // sem nunca ganhar peso, que é o oposto de "um pouco de
                // gravidade".
                verticalVelocity -= gravity * airAttackGravityScale * Time.deltaTime;
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
