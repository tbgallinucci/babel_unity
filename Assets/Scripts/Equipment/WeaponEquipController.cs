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

        private InputAction equipAction;
        private int drawTriggerHash;
        private int sheathTriggerHash;
        private int previousStateHash;
        private int weaponLayerIndex;
        private int isWieldedHash;
        private bool pendingDrawReset;
        private bool pendingSheathReset;

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
            // Draw/Sheath só têm transição de saída a partir de Locomotion/
            // ArmedLocomotion na layer base — disparar durante DashToSprint/Sprint
            // não tem o que consumir o trigger lá (a UpperBody consome
            // normalmente, já que não depende da layer base). Sem isso, o trigger
            // ficaria pendurado e disparava com atraso quando a layer base
            // finalmente voltasse pra Locomotion/ArmedLocomotion depois do sprint.
            // Resetar um frame depois de setar dá tempo de as duas layers
            // reagirem nesse frame (se tiverem transição válida) sem deixar nada
            // pendente pra disparar tarde.
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

            PollStateExit();
            HandleToggleInput();
            animator.SetBool(isWieldedHash, IsWielded);
        }

        private void PollStateExit()
        {
            var stateInfo = animator.GetCurrentAnimatorStateInfo(weaponLayerIndex);

            if (stateInfo.fullPathHash == previousStateHash)
            {
                return;
            }

            previousStateHash = stateInfo.fullPathHash;

            if (currentState == WeaponState.Drawing && !stateInfo.IsTag(drawingTag))
            {
                currentState = WeaponState.Wielded;
            }
            else if (currentState == WeaponState.Sheathing && !stateInfo.IsTag(sheathingTag))
            {
                currentState = WeaponState.Sheathed;
            }
        }

        private void HandleToggleInput()
        {
            if (!equipAction.WasPressedThisFrame() || IsTransitioning || IsAttacking() || IsJumping())
            {
                return;
            }

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
            if (currentState != WeaponState.Sheathed || IsJumping())
            {
                return;
            }

            TriggerDraw();
        }

        private bool IsJumping()
        {
            bool baseLayerJumping = LayerHasTagNowOrIncoming(0, jumpingTag);
            bool upperBodyStillGripping = animator.GetCurrentAnimatorStateInfo(weaponLayerIndex).IsName(armedJumpGripStateName);
            return baseLayerJumping || upperBodyStillGripping;
        }

        // GetCurrentAnimatorStateInfo só reflete o estado de ORIGEM enquanto uma
        // transição está em andamento — no crossfade de decolagem
        // (Locomotion/ArmedLocomotion -> Jump), ele continua reportando o estado
        // antigo (sem a tag) até o blend terminar de verdade, mesmo o
        // personagem já tendo saído do chão fisicamente. GetNextAnimatorStateInfo
        // dá acesso ao estado de DESTINO durante esse período, fechando a janela.
        private bool LayerHasTagNowOrIncoming(int layerIndex, string tag)
        {
            if (animator.GetCurrentAnimatorStateInfo(layerIndex).IsTag(tag))
            {
                return true;
            }

            return animator.IsInTransition(layerIndex) && animator.GetNextAnimatorStateInfo(layerIndex).IsTag(tag);
        }

        private void TriggerDraw()
        {
            animator.ResetTrigger(sheathTriggerHash); // defensivo: limpa trigger pendente
            animator.SetTrigger(drawTriggerHash);
            currentState = WeaponState.Drawing;
            pendingDrawReset = true;
        }

        private void TriggerSheath()
        {
            animator.ResetTrigger(drawTriggerHash);
            animator.SetTrigger(sheathTriggerHash);
            currentState = WeaponState.Sheathing;
            pendingSheathReset = true;
        }

        private bool IsAttacking()
        {
            return animator.GetCurrentAnimatorStateInfo(0).IsTag(attackTag);
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
