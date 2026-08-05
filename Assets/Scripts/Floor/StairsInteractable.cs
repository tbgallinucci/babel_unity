// ============================================================================
//  StairsInteractable.cs  —  código do JOGO
//
//  A escada do andar. Apertar E / ○ perto dela dispara a geração do próximo
//  andar. É só um GATILHO: não há NavMesh contínuo entre níveis, cada andar é
//  uma geração nova e isolada.
//
//  O FloorDirector cria uma destas por andar, na célula que o esqueleto marcou
//  como Escada.
// ============================================================================

using UnityEngine;

namespace Babel.Floor
{
    [AddComponentMenu("Babel/Stairs Interactable")]
    [RequireComponent(typeof(SphereCollider))]
    public sealed class StairsInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private FloorDirector director;

        [Tooltip("Exige que todos os inimigos do andar estejam mortos para liberar a subida.")]
        [SerializeField] private bool requireFloorCleared;

        [SerializeField] private string prompt = "Subir";

        private bool used;

        public string Prompt => used ? "..." : prompt;

        public Transform Transform => transform;

        public void Bind(FloorDirector floorDirector, bool requireCleared)
        {
            director = floorDirector;
            requireFloorCleared = requireCleared;
            used = false;
        }

        public bool CanInteract(GameObject interactor)
        {
            if (used || director == null) return false;
            if (requireFloorCleared && director.LivingEnemyCount > 0) return false;
            return true;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;

            // Trava na hora: sem isso, segurar o botão dispara duas gerações.
            used = true;
            director.RequestNextFloor();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 1.2f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 3f);
        }
    }
}
