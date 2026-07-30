using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Babel.Combat
{
    // Lock-on estilo Nier: R3/Tab trava no Targetable mais próximo do centro
    // da tela dentro de um cone na frente da câmera; com o alvo travado, um
    // flick do analógico direito (mesmo eixo de Look) cicla pro próximo
    // Targetable visível por posição de tela, sem wraparound; R3/Tab de novo
    // destrava. Sem raycast contra colisor — os Targetables de teste ainda
    // não têm física de combate, só precisam existir na cena.
    public class TargetingSystem : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private float lockRange = 15f;
        [SerializeField, Range(0f, 90f)] private float lockConeAngle = 60f;
        [SerializeField, Range(0f, 90f)] private float cycleConeAngle = 85f;

        [Header("Cycle")]
        [SerializeField, Range(0f, 1f)] private float cycleStickThreshold = 0.6f;
        [SerializeField] private float cycleDebounce = 0.35f;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";

        private InputAction lockOnAction;
        private InputAction lookAction;
        private Transform mainCameraTransform;
        private float cycleCooldown;

        public bool IsLocked { get; private set; }
        public Targetable CurrentTarget { get; private set; }

        private void Awake()
        {
            var playerMap = inputActions.FindActionMap(actionMapName, throwIfNotFound: true);
            lockOnAction = playerMap.FindAction("LockOn");
            lookAction = playerMap.FindAction("Look");

            if (Camera.main != null)
            {
                mainCameraTransform = Camera.main.transform;
            }
        }

        private void OnEnable()
        {
            lockOnAction.Enable();
            lookAction.Enable();
        }

        private void OnDisable()
        {
            lockOnAction.Disable();
            lookAction.Disable();
        }

        private void Update()
        {
            if (lockOnAction.WasPressedThisFrame())
            {
                ToggleLock();
            }

            if (!IsLocked)
            {
                return;
            }

            if (CurrentTarget == null || !IsWithinRange(CurrentTarget))
            {
                ClearLock();
                return;
            }

            cycleCooldown -= Time.deltaTime;
            Vector2 look = lookAction.ReadValue<Vector2>();
            if (cycleCooldown <= 0f && Mathf.Abs(look.x) >= cycleStickThreshold)
            {
                CycleTarget(look.x > 0f ? 1 : -1);
                cycleCooldown = cycleDebounce;
            }
        }

        public void ToggleLock()
        {
            if (IsLocked)
            {
                ClearLock();
                return;
            }

            Targetable best = FindBestLockCandidate();
            if (best != null)
            {
                IsLocked = true;
                CurrentTarget = best;
            }
        }

        private void ClearLock()
        {
            IsLocked = false;
            CurrentTarget = null;
        }

        private bool IsWithinRange(Targetable target)
        {
            return Vector3.Distance(mainCameraTransform.position, target.AimPoint.position) <= lockRange * 1.5f;
        }

        private Targetable FindBestLockCandidate()
        {
            if (mainCameraTransform == null)
            {
                return null;
            }

            Targetable best = null;
            float bestAngle = lockConeAngle;

            foreach (Targetable candidate in FindObjectsByType<Targetable>(FindObjectsSortMode.None))
            {
                Vector3 toCandidate = candidate.AimPoint.position - mainCameraTransform.position;
                if (toCandidate.sqrMagnitude > lockRange * lockRange)
                {
                    continue;
                }

                float angle = Vector3.Angle(mainCameraTransform.forward, toCandidate);
                if (angle < bestAngle)
                {
                    bestAngle = angle;
                    best = candidate;
                }
            }

            return best;
        }

        // Cicla por posição de tela (mesma ideia do cycle_lock do projeto Godot
        // de referência): entre os Targetables visíveis, escolhe o próximo cuja
        // posição X de tela está estritamente além da do alvo atual na direção
        // pedida — sem wraparound, se não achar nenhum mantém o alvo atual.
        private void CycleTarget(int direction)
        {
            if (mainCameraTransform == null || CurrentTarget == null)
            {
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            Vector3 currentScreen = cam.WorldToScreenPoint(CurrentTarget.AimPoint.position);

            Targetable next = null;
            float bestScreenX = direction > 0 ? float.MaxValue : float.MinValue;

            foreach (Targetable candidate in FindObjectsByType<Targetable>(FindObjectsSortMode.None))
            {
                if (candidate == CurrentTarget)
                {
                    continue;
                }

                Vector3 toCandidate = candidate.AimPoint.position - mainCameraTransform.position;
                if (toCandidate.sqrMagnitude > lockRange * lockRange)
                {
                    continue;
                }

                if (Vector3.Angle(mainCameraTransform.forward, toCandidate) > cycleConeAngle)
                {
                    continue;
                }

                Vector3 candidateScreen = cam.WorldToScreenPoint(candidate.AimPoint.position);
                if (candidateScreen.z <= 0f)
                {
                    continue;
                }

                bool isBetter = direction > 0
                    ? candidateScreen.x > currentScreen.x && candidateScreen.x < bestScreenX
                    : candidateScreen.x < currentScreen.x && candidateScreen.x > bestScreenX;

                if (isBetter)
                {
                    bestScreenX = candidateScreen.x;
                    next = candidate;
                }
            }

            if (next != null)
            {
                CurrentTarget = next;
            }
        }
    }
}
