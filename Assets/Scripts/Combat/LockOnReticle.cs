using UnityEngine;
using UnityEngine.UI;

namespace Babel.Combat
{
    // Marca na tela o alvo travado no momento — segue a posição de tela do
    // AimPoint do TargetingSystem.CurrentTarget enquanto travado, some
    // quando destrava. Placeholder simples (um Image de UI), sem arte
    // dedicada ainda.
    //
    // Esconde/mostra via Image.enabled, NUNCA via gameObject.SetActive no
    // próprio objeto — como este script vive no mesmo GameObject que ele
    // controla, desativar o GameObject inteiro pararia o próprio
    // LateUpdate() de rodar (componente desativado não recebe Update/
    // LateUpdate), travando a retícula escondida pra sempre depois do
    // primeiro frame sem lock.
    [RequireComponent(typeof(Image))]
    public class LockOnReticle : MonoBehaviour
    {
        [SerializeField] private TargetingSystem targeting;
        [SerializeField] private RectTransform reticle;
        [SerializeField] private Camera trackingCamera;

        private Image image;

        private void Awake()
        {
            image = GetComponent<Image>();

            if (trackingCamera == null)
            {
                trackingCamera = Camera.main;
            }
        }

        private void LateUpdate()
        {
            if (targeting == null || reticle == null || trackingCamera == null
                || !targeting.IsLocked || targeting.CurrentTarget == null)
            {
                image.enabled = false;
                return;
            }

            Vector3 screenPoint = trackingCamera.WorldToScreenPoint(targeting.CurrentTarget.AimPoint.position);
            if (screenPoint.z <= 0f)
            {
                // Alvo atrás da câmera (não deveria acontecer com lock-on
                // ativo, mas evita a retícula "vazar" pro canto da tela
                // nesse caso).
                image.enabled = false;
                return;
            }

            image.enabled = true;
            reticle.position = screenPoint;
        }
    }
}
