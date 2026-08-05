// ============================================================================
//  PlayerInteractor.cs  —  código do JOGO
//
//  Lê a ação "Interact" do InputSystem_Actions (já mapeada em E no teclado e em
//  <Gamepad>/buttonEast — o CÍRCULO no layout PlayStation) e aciona o
//  interagível mais próximo dentro do raio.
//
//  Segue o mesmo padrão de wiring do PlayerController: pega o action map pelo
//  nome e habilita a ação individualmente, em vez de mexer no asset inteiro.
// ============================================================================

using UnityEngine;
using UnityEngine.InputSystem;

namespace Babel.Floor
{
    [AddComponentMenu("Babel/Player Interactor")]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string interactActionName = "Interact";

        [Header("Alcance")]
        [Tooltip("Raio de busca por interagíveis, em metros.")]
        [SerializeField] private float radius = 3.5f;

        [Tooltip("Origem da busca. Vazio = o próprio transform.")]
        [SerializeField] private Transform origin;

        [Tooltip("Camadas onde procurar. Deixe tudo se não tiver camada dedicada.")]
        [SerializeField] private LayerMask layers = ~0;

        [Header("Prompt (opcional)")]
        [Tooltip("GameObject ligado/desligado conforme houver algo interagível por perto.")]
        [SerializeField] private GameObject promptObject;

        [Tooltip("Loga no console o que está ao alcance. Útil enquanto não há UI.")]
        [SerializeField] private bool logPrompt;

        private InputAction interactAction;
        private IInteractable current;
        private readonly Collider[] hits = new Collider[16];
        private string lastLogged;

        /// <summary>O interagível atualmente ao alcance, ou null.</summary>
        public IInteractable Current => current;

        private void Awake()
        {
            if (origin == null) origin = transform;

            if (inputActions == null)
            {
                Debug.LogError("[PlayerInteractor] InputActionAsset não atribuído — " +
                               "arraste o InputSystem_Actions no Inspector.", this);
                enabled = false;
                return;
            }

            InputActionMap map = inputActions.FindActionMap(actionMapName, throwIfNotFound: true);
            interactAction = map.FindAction(interactActionName, throwIfNotFound: true);
        }

        private void OnEnable() => interactAction?.Enable();

        private void OnDisable() => interactAction?.Disable();

        private void Update()
        {
            current = FindNearest();

            if (promptObject != null && promptObject.activeSelf != (current != null))
                promptObject.SetActive(current != null);

            if (logPrompt)
            {
                string now = current != null ? current.Prompt : null;
                if (now != lastLogged)
                {
                    if (now != null) Debug.Log($"[Interação] {now}  (E / ○)");
                    lastLogged = now;
                }
            }

            if (current == null) return;
            if (interactAction == null || !interactAction.WasPressedThisFrame()) return;

            current.Interact(gameObject);
        }

        private IInteractable FindNearest()
        {
            int count = Physics.OverlapSphereNonAlloc(
                origin.position, radius, hits, layers, QueryTriggerInteraction.Collide);

            IInteractable best = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                // GetComponentInParent porque o colisor costuma estar num filho.
                var candidate = hits[i].GetComponentInParent<IInteractable>();
                if (candidate == null || !candidate.CanInteract(gameObject)) continue;

                float sqr = (candidate.Transform.position - origin.position).sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                best = candidate;
            }

            return best;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.5f);
            Gizmos.DrawWireSphere((origin != null ? origin : transform).position, radius);
        }
    }
}
