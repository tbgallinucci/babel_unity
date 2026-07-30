using Unity.Cinemachine;
using UnityEngine;

namespace Babel.Combat
{
    // Ponte entre TargetingSystem e a vcam de lock-on — estilo KH2/3 e Nier:
    // a câmera continua fazendo exatamente o que já faz sempre (orbitar e
    // compor o PLAYER, LookAt = Player, igual a FreeLook), NÃO tenta
    // calcular um "olhar" 3D pra um ponto entre player e inimigo (abordagem
    // anterior, com focusPoint/Vector3.Lerp — trocada por essa, que ficava
    // se perdendo). Só o YAW da órbita (de qual lado a câmera fica em volta
    // do player) é puxado pro lado oposto ao inimigo, suavemente (Lerp por
    // velocidade angular, não instantâneo) — pitch e distância continuam
    // sincronizados livremente com a câmera normal. Mesma técnica do
    // PlayerCameraRig.set_lock_yaw() do projeto Godot de referência.
    [RequireComponent(typeof(TargetingSystem))]
    public class CameraLockOnController : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera lockOnCamera;
        [SerializeField] private CinemachineOrbitalFollow freeLookOrbital;
        [SerializeField] private CinemachineOrbitalFollow lockOnOrbital;
        // Graus/segundo — quão rápido o yaw da órbita gira até ficar do lado
        // oposto ao inimigo. Mais alto = mais "grudento" no alvo, mais baixo
        // = giro mais suave/lento.
        [SerializeField] private float lockYawTurnSpeed = 360f;

        private TargetingSystem targeting;

        private void Awake()
        {
            targeting = GetComponent<TargetingSystem>();

            if (lockOnCamera != null)
            {
                // Follow E LookAt fixos no player desde o início — a câmera
                // de lock-on compõe o player exatamente como a câmera livre
                // já faz. O que muda durante o lock não é PRA ONDE ela olha,
                // é DE QUE LADO ela orbita (ver LateUpdate).
                lockOnCamera.Follow = transform;
                lockOnCamera.LookAt = transform;
                // Desligada por padrão — a FreeLook (prioridade menor) fica no
                // controle até um lock acontecer.
                lockOnCamera.enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (lockOnCamera == null)
            {
                return;
            }

            if (targeting.IsLocked && targeting.CurrentTarget != null)
            {
                if (freeLookOrbital != null && lockOnOrbital != null)
                {
                    // Pitch e distância ficam livres, espelhando a câmera
                    // normal (reagem ao analógico/scroll do jeito de sempre).
                    lockOnOrbital.VerticalAxis.Value = freeLookOrbital.VerticalAxis.Value;
                    lockOnOrbital.RadialAxis.Value = freeLookOrbital.RadialAxis.Value;

                    // Yaw: gira até o lado OPOSTO ao inimigo (câmera atrás do
                    // player, inimigo do outro lado, à mostra) — não instantâneo,
                    // por velocidade angular constante.
                    //
                    // Convenção confirmada por teste: sem offset já posiciona
                    // a câmera do lado oposto ao inimigo (o "+180f" de uma
                    // versão anterior deixava o inimigo ENTRE player e
                    // câmera — o oposto do desejado — por isso foi removido).
                    Vector3 toEnemy = targeting.CurrentTarget.AimPoint.position - transform.position;
                    toEnemy.y = 0f;
                    float targetYaw = Mathf.Atan2(toEnemy.x, toEnemy.z) * Mathf.Rad2Deg;

                    lockOnOrbital.HorizontalAxis.Value = Mathf.MoveTowardsAngle(
                        lockOnOrbital.HorizontalAxis.Value,
                        targetYaw,
                        lockYawTurnSpeed * Time.deltaTime);
                }

                lockOnCamera.enabled = true;
            }
            else
            {
                lockOnCamera.enabled = false;
            }
        }
    }
}
