using Babel.Combat;
using UnityEngine;

namespace Babel.UI
{
    // Aponta uma HealthBar pro inimigo travado no lock-on, e esconde a barra
    // quando não há alvo. Mesmo contrato do LockOnReticle (que já segue o
    // CurrentTarget na tela) — a diferença é que este mostra a VIDA do alvo,
    // não a posição dele.
    //
    // Polling em vez de evento porque TargetingSystem hoje só expõe as
    // propriedades IsLocked/CurrentTarget, sem evento de troca de alvo — mesma
    // escolha que o LockOnReticle já faz. Se um dia o TargetingSystem ganhar
    // um OnTargetChanged, os dois passam a poder escutar em vez de checar.
    //
    // Esconde via `container.SetActive`, NUNCA desativando o GameObject deste
    // script — pela mesma razão documentada no LockOnReticle: componente
    // desativado não recebe Update, e a barra ficaria presa escondida pra
    // sempre depois do primeiro frame sem lock.
    public class TargetHealthBar : MonoBehaviour
    {
        [SerializeField] private TargetingSystem targeting;
        [SerializeField] private HealthBar bar;

        // O objeto a ligar/desligar — tipicamente o painel que agrupa a barra
        // (moldura + preenchimento + nome). Precisa ser um objeto DIFERENTE
        // do que carrega este script.
        [SerializeField] private GameObject container;

        private Targetable boundTarget;

        private void Update()
        {
            Targetable current = (targeting != null && targeting.IsLocked)
                ? targeting.CurrentTarget
                : null;

            if (current != boundTarget)
            {
                boundTarget = current;
                Rebind(current);
            }

            if (container != null)
            {
                // Alvo morto some junto: sem sistema de respawn, uma barra
                // zerada parada na tela depois da morte é só ruído.
                bool show = boundTarget != null
                    && bar != null
                    && bar.Target != null
                    && bar.Target.IsAlive;

                if (container.activeSelf != show)
                {
                    container.SetActive(show);
                }
            }
        }

        private void Rebind(Targetable target)
        {
            if (bar == null)
            {
                return;
            }

            // GetComponentInParent, não GetComponent: o Targetable pode estar
            // no mesmo objeto do HealthComponent ou num filho (o AimPoint do
            // lock-on, por exemplo, costuma ser um osso lá no fundo do rig).
            HealthComponent health = target != null
                ? target.GetComponentInParent<HealthComponent>()
                : null;

            bar.Bind(health);
        }
    }
}
