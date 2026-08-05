// ============================================================================
//  IInteractable.cs  —  código do JOGO (não faz parte do plugin de geração)
//
//  Contrato mínimo de "coisa que responde ao botão de interagir". A escada é a
//  primeira implementação; baú, alavanca e NPC entram depois sem tocar no
//  PlayerInteractor.
// ============================================================================

using UnityEngine;

namespace Babel.Floor
{
    public interface IInteractable
    {
        /// <summary>Texto curto para o prompt na tela ("Subir", "Abrir", ...).</summary>
        string Prompt { get; }

        /// <summary>Pode ser usado agora? Ex.: escada trancada até a sala ser limpa.</summary>
        bool CanInteract(GameObject interactor);

        void Interact(GameObject interactor);

        /// <summary>Ponto de referência para medir distância e desenhar o prompt.</summary>
        Transform Transform { get; }
    }
}
