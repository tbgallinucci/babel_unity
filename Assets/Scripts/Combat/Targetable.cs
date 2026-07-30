using UnityEngine;

namespace Babel.Combat
{
    // Marca qualquer GameObject como travável pelo lock-on. Inimigos reais só
    // precisam anexar este componente (e opcionalmente apontar aimPoint pro
    // torso/cabeça) pra funcionar com TargetingSystem sem nenhuma mudança nele.
    [DisallowMultipleComponent]
    public class Targetable : MonoBehaviour
    {
        [SerializeField] private Transform aimPoint;

        public Transform AimPoint => aimPoint != null ? aimPoint : transform;
    }
}
