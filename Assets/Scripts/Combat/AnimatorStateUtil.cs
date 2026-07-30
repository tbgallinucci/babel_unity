using UnityEngine;

namespace Babel.Combat
{
    // Leitura de estado do Animator à prova de crossfade, num lugar só.
    //
    // GetCurrentAnimatorStateInfo reflete o estado de ORIGEM enquanto uma
    // transição está em andamento — durante o crossfade ArmedLocomotion ->
    // Attack1 (0.25s!), por exemplo, ele continua reportando ArmedLocomotion,
    // então qualquer checagem tipo "estou atacando?" responde FALSE mesmo com
    // o golpe já a caminho. Isso já causou bugs reais neste projeto (perna
    // dessincronizada do braço no jump attack; combo re-disparando em vez de
    // enfileirar; snap de rotação repetido ao spammar ataque) e vinha sendo
    // reimplementado à mão em 4 lugares diferentes, cada um com risco de
    // esquecer a proteção.
    //
    // Ver "Guide - Robustez e Data-Driven do Combate.md", itens 2 e 6.
    public static class AnimatorStateUtil
    {
        // "A layer está NESSE estado agora, ou entrando nele?"
        public static bool HasTagNowOrIncoming(Animator animator, int layer, string tag)
        {
            if (animator.GetCurrentAnimatorStateInfo(layer).IsTag(tag))
            {
                return true;
            }

            return animator.IsInTransition(layer)
                && animator.GetNextAnimatorStateInfo(layer).IsTag(tag);
        }

        // Mesma ideia, por NOME de estado — pra estados que compartilham tag
        // com outros de propósito (Attack2Alt/SlideAttack reusam a tag
        // "Attack" pro IsAttacking()/combo funcionarem sem mudança).
        public static bool HasStateNowOrIncoming(Animator animator, int layer, string stateName)
        {
            if (animator.GetCurrentAnimatorStateInfo(layer).IsName(stateName))
            {
                return true;
            }

            return animator.IsInTransition(layer)
                && animator.GetNextAnimatorStateInfo(layer).IsName(stateName);
        }

        // "A layer JÁ CONTA como estando nesse tag?" — durante uma transição
        // responde pelo estado de DESTINO, não pelo de origem.
        //
        // Diferente de HasTagNowOrIncoming (que responde true se QUALQUER um
        // dos dois tem o tag, ou seja, é conservador e "gruda" no tag durante
        // todo o crossfade). Use este quando o consumidor precisa virar a
        // chave no INSTANTE em que a troca começa — tipicamente um bool lido
        // por outra layer do Animator, que precisa soltar/assumir a pose em
        // sincronia com a base layer, não 0,25s depois.
        public static bool EffectiveHasTag(Animator animator, int layer, string tag)
        {
            if (animator.IsInTransition(layer))
            {
                return animator.GetNextAnimatorStateInfo(layer).IsTag(tag);
            }

            return animator.GetCurrentAnimatorStateInfo(layer).IsTag(tag);
        }

        // Hash do estado que "vale" agora: o de destino se há transição em
        // andamento, senão o atual. Usado pra resetar filas (ComboQueued,
        // StrongComboQueued, DodgeQueued, AttackQueued) exatamente uma vez por
        // troca real de estado, sem contar o crossfade duas vezes.
        public static int EffectiveStateHash(Animator animator, int layer)
        {
            if (animator.IsInTransition(layer))
            {
                return animator.GetNextAnimatorStateInfo(layer).fullPathHash;
            }

            return animator.GetCurrentAnimatorStateInfo(layer).fullPathHash;
        }
    }
}
