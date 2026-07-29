
using UnityEngine;
using UnityEngine.InputSystem;
using Babel.Equipment;

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
        [SerializeField] private float sprintJumpBoost = 4f;
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
        private Transform mainCameraTransform;
        private float verticalVelocity;
        private float lastGroundedSpeed;
        private bool comboQueued;
        private bool sprinting;
        private bool pendingSprintCancel;
        private int previousStateHash;

        private InputAction moveAction;
        private InputAction attackAction;
        private InputAction jumpAction;
        private InputAction sprintAction;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            animator = GetComponentInChildren<Animator>();
            weaponEquip = GetComponentInChildren<WeaponEquipController>();

            var playerMap = inputActions.FindActionMap(actionMapName, throwIfNotFound: true);
            moveAction = playerMap.FindAction("Move");
            attackAction = playerMap.FindAction("Attack");
            jumpAction = playerMap.FindAction("Jump");
            sprintAction = playerMap.FindAction("Sprint");

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
        }

        private void OnDisable()
        {
            moveAction.Disable();
            attackAction.Disable();
            jumpAction.Disable();
            sprintAction.Disable();
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
            HandleMovement();
            HandleSprint();
            HandleJump();
            animator.SetFloat("CombatSpeedMultiplier", combatSpeedMultiplier);
        }

        private bool IsAttacking()
        {
            return animator.GetCurrentAnimatorStateInfo(0).IsTag("Attack");
        }

        private void HandleAttack()
        {
            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);

            if (stateInfo.fullPathHash != previousStateHash)
            {
                comboQueued = false;
                previousStateHash = stateInfo.fullPathHash;
            }

            if (attackAction.WasPressedThisFrame())
            {
                if (weaponEquip != null && weaponEquip.CurrentState == WeaponState.Sheathed)
                {
                    weaponEquip.RequestDraw();
                }
                else if (!IsAttacking() && (weaponEquip == null || weaponEquip.IsWielded))
                {
                    animator.SetTrigger("Attack");
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

            animator.SetBool("ComboQueued", comboQueued);
            // A UpperBody usa isso pra não reentrar em ArmedSprintGrip enquanto o
            // SlideAttack está tocando na layer base — Sprint/IsWielded sozinhos
            // não bastam, já que os dois continuam true durante o ataque.
            animator.SetBool("IsAttacking", IsAttacking());
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
                animator.SetFloat("Speed", inputMagnitude, 0.05f, Time.deltaTime);
            }

            if (inputMagnitude > 0.05f && mainCameraTransform != null)
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

            animator.SetBool("Sprint", sprinting);
        }

        private void HandleJump()
        {
            if (!IsAttacking() && controller.isGrounded && jumpAction.WasPressedThisFrame())
            {
                animator.SetTrigger("Jump");
                verticalVelocity = jumpForce;
            }
        }

        private void OnAnimatorMove()
        {
            if (animator == null) return;

            Vector3 rootMotionPosition;
            var baseStateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (baseStateInfo.IsTag("Dashing"))
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
            else if (baseStateInfo.IsName("SlideAttack"))
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
                float boost = animator.GetBool("Sprint") ? sprintJumpBoost : 0f;
                rootMotionPosition = transform.forward * (lastGroundedSpeed + boost) * Time.deltaTime;
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
