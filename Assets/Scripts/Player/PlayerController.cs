
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

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";

        private CharacterController controller;
        private Animator animator;
        private WeaponEquipController weaponEquip;
        private Transform mainCameraTransform;
        private float verticalVelocity;
        private bool comboQueued;
        private int previousStateHash;

        private InputAction moveAction;
        private InputAction attackAction;
        private InputAction jumpAction;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            animator = GetComponentInChildren<Animator>();
            weaponEquip = GetComponentInChildren<WeaponEquipController>();

            var playerMap = inputActions.FindActionMap(actionMapName, throwIfNotFound: true);
            moveAction = playerMap.FindAction("Move");
            attackAction = playerMap.FindAction("Attack");
            jumpAction = playerMap.FindAction("Jump");

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
        }

        private void OnDisable()
        {
            moveAction.Disable();
            attackAction.Disable();
            jumpAction.Disable();
        }

        private void Update()
        {
            HandleAttack();
            HandleMovement();
            HandleJump();
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
                if (!IsAttacking() && (weaponEquip == null || weaponEquip.IsWielded))
                {
                    animator.SetTrigger("Attack");
                }
                else
                {
                    comboQueued = true;
                }
            }

            animator.SetBool("ComboQueued", comboQueued);
        }

        private void HandleMovement()
        {
            Vector2 moveInput = moveAction.ReadValue<Vector2>();
            Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);
            float inputMagnitude = Mathf.Clamp01(inputDir.magnitude);

            if (IsAttacking())
            {
                return;
            }

            animator.SetFloat("Speed", inputMagnitude, 0.05f, Time.deltaTime);

            if (inputMagnitude > 0.05f && mainCameraTransform != null)
            {
                float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + mainCameraTransform.eulerAngles.y;
                Quaternion targetRotation = Quaternion.Euler(0f, targetAngle, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
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

            Vector3 localRootMotion = Quaternion.Inverse(transform.rotation) * animator.deltaPosition;
            localRootMotion.x = 0f;
            localRootMotion.y = 0f;
            Vector3 rootMotionPosition = transform.rotation * localRootMotion;

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
