using Babel.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Babel.Equipment
{
    public enum WeaponState
    {
        Sheathed,
        Drawing,
        Wielded,
        Sheathing
    }

    // Máquina de estados Sheathed/Wielded de uma arma: input de toggle, disparo de
    // triggers no Animator, detecção de fim de transição (mesmo idioma de
    // "previous state hash" que PlayerController usa pro combo) e a troca
    // física entre os sockets de bainha/empunhadura, disparada por Animation Events.
    //
    // Precisa estar no MESMO GameObject que o Animator que toca os clipes de
    // Draw/Sheath — Animation Events são despachados via GameObject.SendMessage
    // contra esse GameObject especificamente, não o pai nem os filhos. Neste
    // projeto isso é o mesmo GameObject onde já fica o PlayerController.
    //
    // Todas as referências de arma/socket/parâmetro são campos serializados no
    // Inspector, então este mesmo componente pode ser reaproveitado num rig de
    // cajado/adaga no futuro só reapontando referências e duplicando o sub-grafo
    // do Animator — sem mudar código.
    [RequireComponent(typeof(Animator))]
    public class WeaponEquipController : MonoBehaviour
    {
        [Header("Weapon & Sockets")]
        [SerializeField] private Transform weapon;
        [SerializeField] private Transform sheathSocket;
        [SerializeField] private Transform wieldSocket;

        [Header("Animator")]
        [SerializeField] private Animator animator;
        [SerializeField] private string drawTriggerName = "Draw";
        [SerializeField] private string sheathTriggerName = "Sheath";
        [SerializeField] private string drawingTag = "WeaponDrawing";
        [SerializeField] private string sheathingTag = "WeaponSheathing";
        [SerializeField] private string attackTag = "Attack";
        // Jump/ArmedJumpGrip não têm transição de saída pro Draw/Sheath — sem
        // esse bloqueio, o trigger dispara, currentState já muda (derrubando
        // IsWielded antes da hora), mas a UpperBody nunca sai do grip pra tocar
        // o gesto nem disparar o Animation Event que move a espada de socket.
        [SerializeField] private string jumpingTag = "Jumping";
        // A UpperBody sai de ArmedJumpGrip pelo próprio Exit Time dela, que não
        // está sincronizado com o pouso real da layer base — existe uma janela
        // onde a tag Jumping já sumiu mas a UpperBody ainda não terminou de sair
        // do grip. Checar o nome do estado da UpperBody também fecha essa
        // corrida (senão o trigger pode disparar sem ter transição pra
        // consumir bem nesse instante).
        [SerializeField] private string armedJumpGripStateName = "ArmedJumpGrip";
        // Mesmo racional do Jump/ArmedJumpGrip acima, aplicado ao Dodge —
        // sem esse bloqueio, dava pra sacar/guardar a arma no meio do roll,
        // o que também explica travamentos: o trigger Draw/Sheath dispara,
        // currentState já muda, mas não tem transição de ArmedDodgeGrip pra
        // GreatSwordDraw/Sheath pra consumir esse trigger.
        [SerializeField] private string dodgingTag = "Dodging";
        [SerializeField] private string armedDodgeGripStateName = "ArmedDodgeGrip";
        // Draw/Sheath tocam numa layer mascarada (só tronco/braços) pra deixar a
        // Locomotion da layer base livre — assim o personagem continua andando
        // normalmente enquanto saca/guarda. Ver a seção "Mover durante Draw/Sheath"
        // no guia de planejamento pro racional completo do masking.
        [SerializeField] private string weaponLayerName = "UpperBody";
        // Único bool do sistema — as demais transições usam Trigger. Existe pra
        // rotear DashToSprint/Sprint (compartilhados entre desarmado/armado, já
        // que o corpo faz o mesmo movimento nos dois casos) de volta pro
        // Locomotion ou ArmedLocomotion certo, e pra UpperBody saber quando
        // sobrepor os braços com a pose de segurar a espada durante o sprint.
        [SerializeField] private string isWieldedParamName = "IsWielded";

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string equipActionName = "Equip";

        [SerializeField] private WeaponState currentState = WeaponState.Sheathed;

        // Segurança contra trigger descartado (ver PollStateExit): se o
        // Animator nunca entrar no gesto de Draw/Sheath dentro dessa janela,
        // o estado volta pro anterior em vez de ficar travado em
        // Drawing/Sheathing pra sempre. Generoso de propósito — o crossfade
        // de entrada já é detectado na hora por HasTagNowOrIncoming, então
        // isso só dispara em falha real, nunca num gesto lento.
        [SerializeField] private float transitionTimeout = 0.5f;

        private InputAction equipAction;
        private int drawTriggerHash;
        private int sheathTriggerHash;
        private int weaponLayerIndex;
        private int isWieldedHash;
        private bool pendingDrawReset;
        private bool pendingSheathReset;
        private bool pendingToggle;
        private bool sawTransitionState;
        private float transitionWatchdog;

        public WeaponState CurrentState => currentState;
        public bool IsWielded => currentState == WeaponState.Wielded;
        public bool IsTransitioning => currentState == WeaponState.Drawing || currentState == WeaponState.Sheathing;

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            var playerMap = inputActions.FindActionMap(actionMapName, throwIfNotFound: true);
            equipAction = playerMap.FindAction(equipActionName);

            drawTriggerHash = Animator.StringToHash(drawTriggerName);
            sheathTriggerHash = Animator.StringToHash(sheathTriggerName);
            weaponLayerIndex = animator.GetLayerIndex(weaponLayerName);
            isWieldedHash = Animator.StringToHash(isWieldedParamName);

            // A cena autora a espada sob sheathSocket por padrão; forçar o snap no
            // boot garante que estado runtime e visual nunca fiquem dessincronizados.
            SnapWeaponTo(sheathSocket);
        }

        private void OnEnable()
        {
            equipAction.Enable();
        }

        private void OnDisable()
        {
            equipAction.Disable();
        }

        private void Update()
        {
            PollStateExit();
            HandleToggleInput();
            animator.SetBool(isWieldedHash, IsWielded);
        }

        // Draw/Sheath só têm transição de saída a partir de Locomotion/
        // ArmedLocomotion na layer base — disparar durante DashToSprint/Sprint
        // não tem o que consumir o trigger lá (a UpperBody consome
        // normalmente, já que não depende da layer base). Sem isso, o trigger
        // ficaria pendurado e disparava com atraso quando a layer base
        // finalmente voltasse pra Locomotion/ArmedLocomotion depois do sprint.
        //
        // O reset mora em LateUpdate() e não no início do Update() de
        // propósito — bug real encontrado: TriggerDraw() pode ser chamado de
        // FORA (PlayerController.RequestDraw(), quando Attack é apertado com
        // a arma guardada), não só de dentro do próprio HandleToggleInput().
        // A Unity NÃO garante ordem entre Update() de componentes diferentes.
        // Se PlayerController.Update() rodasse antes deste Update() no mesmo
        // frame, a sequência virava: RequestDraw() seta o trigger -> este
        // Update() roda no MESMO frame -> vê pendingDrawReset==true (setado
        // segundos atrás, mesmo frame) -> reseta o trigger ANTES do Animator
        // jamais ter avaliado ele. O trigger nascia e morria no mesmo frame,
        // sem nunca disparar a transição — silenciosamente, sem erro. Sacar
        // pelo toggle de Equip nunca mostrava o bug porque TriggerDraw()
        // roda DENTRO do próprio Update(), sempre depois do bloco de reset
        // (então só o próximo Update() resetava, dando um frame inteiro pro
        // Animator). LateUpdate() é garantido pela Unity rodar depois de
        // TODOS os Update() do frame E depois do Animator já ter avaliado —
        // elimina a dependência de ordem entre os dois scripts.
        private void LateUpdate()
        {
            if (pendingDrawReset)
            {
                animator.ResetTrigger(drawTriggerHash);
                pendingDrawReset = false;
            }

            if (pendingSheathReset)
            {
                animator.ResetTrigger(sheathTriggerHash);
                pendingSheathReset = false;
            }
        }

        // Conclui (ou aborta) uma transição Drawing/Sheathing em andamento.
        //
        // A versão anterior comparava o hash do estado da layer contra o do
        // frame anterior e retornava cedo se não tivesse mudado. Isso tinha um
        // deadlock PERMANENTE: TriggerDraw() já marca currentState = Drawing
        // otimisticamente, antes de saber se o Animator aceitou o trigger. Se o
        // trigger não fosse consumido (a layer nunca sai de Empty — acontece
        // quando ela já está em transição, ou quando a layer base não está em
        // Locomotion/ArmedLocomotion, e pendingDrawReset apaga o trigger no
        // frame seguinte), o hash nunca mudava, o early-return disparava pra
        // sempre e currentState ficava em Drawing o RESTO DA SESSÃO. A partir
        // daí IsTransitioning nunca mais liberava o toggle e IsWielded ficava
        // false pra sempre — o que também matava o ataque (PlayerController cai
        // no branch de enfileirar combo quando não está nem Sheathed nem
        // Wielded). Não havia rota de recuperação nenhuma.
        //
        // Agora o estado só é dado como concluído depois de CONFIRMAR que o
        // Animator realmente entrou no gesto, e um watchdog desfaz a intenção
        // se ele nunca entrar.
        private void PollStateExit()
        {
            if (!IsTransitioning)
            {
                return;
            }

            bool drawing = currentState == WeaponState.Drawing;
            string expectedTag = drawing ? drawingTag : sheathingTag;

            if (AnimatorStateUtil.HasTagNowOrIncoming(animator, weaponLayerIndex, expectedTag))
            {
                sawTransitionState = true;
                transitionWatchdog = 0f;
                return;
            }

            if (sawTransitionState)
            {
                // Entrou no gesto e já saiu — conclusão normal.
                currentState = drawing ? WeaponState.Wielded : WeaponState.Sheathed;
                sawTransitionState = false;
                transitionWatchdog = 0f;
                return;
            }

            // Nunca chegou a entrar: o trigger se perdeu. Volta pro estado
            // anterior (a arma não trocou de socket — OnWeaponGrabbed/
            // OnWeaponSheathed nunca dispararam) em vez de travar.
            transitionWatchdog += Time.deltaTime;
            if (transitionWatchdog >= transitionTimeout)
            {
                currentState = drawing ? WeaponState.Sheathed : WeaponState.Wielded;
                transitionWatchdog = 0f;
                pendingToggle = false;
            }
        }

        // WasPressedThisFrame() é um pulso de um frame só — sem enfileirar,
        // um aperto que caísse bem no meio de IsAttacking()/IsJumping()/
        // IsDodging()/IsTransitioning era descartado pra sempre (nada
        // reexecuta o toggle depois que o bloqueio termina). pendingToggle
        // guarda a intenção e aplica assim que todos os bloqueios liberarem,
        // não importa quantos frames isso demore.
        private void HandleToggleInput()
        {
            if (equipAction.WasPressedThisFrame())
            {
                pendingToggle = true;
            }

            if (!pendingToggle || IsTransitioning || IsAttacking() || IsJumping() || IsDodging())
            {
                return;
            }

            pendingToggle = false;

            if (currentState == WeaponState.Sheathed)
            {
                TriggerDraw();
            }
            else if (currentState == WeaponState.Wielded)
            {
                TriggerSheath();
            }
        }

        // Chamado pelo PlayerController quando Attack é pressionado com a arma
        // guardada: saca em vez de atacar, sem duplicar a lógica de trigger nem
        // depender do input de Equip.
        public void RequestDraw()
        {
            // Mesmos bloqueios do HandleToggleInput() — antes faltava
            // IsAttacking() aqui, deixando esta porta mais permissiva que a do
            // toggle sem motivo. Disparar o trigger num frame em que a layer
            // não tem transição pra consumi-lo é justamente o que o watchdog
            // do PollStateExit passou a ter que remediar; melhor não criar a
            // situação.
            if (currentState != WeaponState.Sheathed || IsAttacking() || IsJumping() || IsDodging())
            {
                return;
            }

            TriggerDraw();
        }

        // As duas checagens abaixo usavam helpers próprios, duplicados aqui —
        // agora vêm de AnimatorStateUtil (mesma proteção de crossfade, um
        // lugar só). Ver o comentário de lá pro racional completo.
        private bool IsJumping()
        {
            return AnimatorStateUtil.HasTagNowOrIncoming(animator, 0, jumpingTag)
                || AnimatorStateUtil.HasStateNowOrIncoming(animator, weaponLayerIndex, armedJumpGripStateName);
        }

        private bool IsDodging()
        {
            return AnimatorStateUtil.HasTagNowOrIncoming(animator, 0, dodgingTag)
                || AnimatorStateUtil.HasStateNowOrIncoming(animator, weaponLayerIndex, armedDodgeGripStateName);
        }

        private void TriggerDraw()
        {
            animator.ResetTrigger(sheathTriggerHash); // defensivo: limpa trigger pendente
            animator.SetTrigger(drawTriggerHash);
            currentState = WeaponState.Drawing;
            pendingDrawReset = true;
            BeginTransitionWatch();
        }

        private void TriggerSheath()
        {
            animator.ResetTrigger(drawTriggerHash);
            animator.SetTrigger(sheathTriggerHash);
            currentState = WeaponState.Sheathing;
            pendingSheathReset = true;
            BeginTransitionWatch();
        }

        private void BeginTransitionWatch()
        {
            sawTransitionState = false;
            transitionWatchdog = 0f;
        }

        private bool IsAttacking()
        {
            return AnimatorStateUtil.HasTagNowOrIncoming(animator, 0, attackTag);
        }

        // -- Callbacks de Animation Event ------------------------------------
        // Ligar OnWeaponGrabbed no clipe GreatSwordDraw e OnWeaponSheathed no
        // clipe GreatSwordDraw_Reverse, no frame em que a mão encontra a arma.

        public void OnWeaponGrabbed()
        {
            SnapWeaponTo(wieldSocket);
        }

        public void OnWeaponSheathed()
        {
            SnapWeaponTo(sheathSocket);
        }

        private void SnapWeaponTo(Transform socket)
        {
            // SetParent(socket, false) mantém a posição/rotação local EXISTENTE da
            // espada e só reinterpreta sob o novo pai — NÃO zera pra alinhar com a
            // origem do socket. Sem o zeramento explícito abaixo, a espada mantém
            // seu offset relativo ao socket antigo, agora aplicado ao novo socket,
            // o que confiavelmente produz um pop/desalinhamento visível. Zerar é o
            // que de fato encaixa a espada no offset autorado do socket.
            weapon.SetParent(socket, false);
            weapon.localPosition = Vector3.zero;
            weapon.localRotation = Quaternion.identity;
        }
    }
}
