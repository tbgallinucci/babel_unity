namespace Babel.Combat
{
    // Nomes de parâmetro/tag/estado/layer do PlayerAnimatorController, num
    // lugar só. Cada entrada aqui é uma string que hoje só existe "de
    // verdade" dentro do Animator — nada no C# valida que bate. Já causou
    // bug real (a renomeação StrongAttack -> Attack2Alt exigiu caçar cada
    // referência manualmente; um "a" sobrando em nome de estado já passou
    // despercebido em commits anteriores). Não impede erro de digitação
    // sozinho, mas concentra o ponto de verdade: uma renomeação futura vira
    // "Find All References" no editor de código em vez de grep manual, e
    // autocomplete pega `AnimStrings.SlidAttack` na hora (não compila); a
    // string solta "SlidAttack" compilava e falhava calado em runtime.
    //
    // Escopo: só os literais realmente hardcoded em PlayerController (a
    // única classe que fala com o Animator via string crua). NÃO inclui os
    // campos de WeaponEquipController — aqueles são [SerializeField] de
    // propósito (o comentário de cabeçalho da classe já explica: existem
    // pra permitir reapontar pra um rig de arma diferente sem mudar
    // código), então a "duplicação" ali é configuração por instância, não o
    // problema que esta classe resolve.
    //
    // Ver "Guide - Robustez e Data-Driven do Combate.md", item 3.
    public static class AnimStrings
    {
        // -- Parâmetros (Bool/Trigger/Float) ---------------------------------
        public const string CombatSpeedMultiplier = "CombatSpeedMultiplier";
        public const string Speed = "Speed";
        public const string Sprint = "Sprint";
        public const string Jump = "Jump";
        // "O player está no ar?" — bool, escrito todo frame pelo
        // PlayerController. NÃO é `!controller.isGrounded` cru: é um latch
        // aberto pelo Animation Event de decolagem e fechado no pouso (ver
        // PlayerController.airborne), porque durante o windup do Jump Start os
        // pés ainda estão no chão e o valor cru diria "no chão" com o pulo já
        // em andamento.
        //
        // O parâmetro já existia no controller, órfão do jump attack antigo:
        // lido por quatro transições e escrito por ninguém, então valia false
        // pra sempre e as condições `IsJumping == false` passavam sempre. Duas
        // delas são exatamente o que o combate aéreo precisa (UpperBody
        // Empty -> Draw/Sheath: não dá pra sacar/guardar a arma no ar) e
        // passam a funcionar de graça agora que alguém escreve o valor.
        public const string IsJumping = "IsJumping";
        public const string Dodge = "Dodge";
        public const string DodgeQueued = "DodgeQueued";
        public const string IsDodging = "IsDodging";
        public const string ComboQueued = "ComboQueued";
        public const string StrongComboQueued = "StrongComboQueued";
        public const string AttackQueued = "AttackQueued";
        public const string IsAttacking = "IsAttacking";
        // Único parâmetro aqui que NÃO é escrito por código: quem dirige o
        // valor é uma curva chamada "Lunge" dentro de cada clipe de ataque
        // (Import Settings -> Animation -> Curves). O C# só lê
        // (animator.GetFloat) em OnAnimatorMove. Clipe sem essa curva não
        // contribui, então o valor fica 0 e o avanço extra simplesmente não
        // acontece — é isso que deixa o parâmetro entrar no projeto antes de
        // qualquer clipe novo existir, sem mudar comportamento nenhum.
        public const string Lunge = "Lunge";

        // -- Tags -------------------------------------------------------------
        // "Attack" serve tanto de nome de Trigger (SetTrigger) quanto de tag
        // compartilhada por todo estado de combo — valor igual, dois usos,
        // uma constante só.
        public const string Attack = "Attack";
        public const string Dodging = "Dodging";

        // -- Nomes de estado ----------------------------------------------------
        public const string Attack1Alt1 = "Attack1Alt1";
        public const string Attack1Alt2 = "Attack1Alt2";
        public const string Attack2Alt = "Attack2Alt";
        // Os dois ataques aéreos. Identificados por NOME e não por tag pelo
        // mesmo motivo já documentado no Attack2Alt: a tag deles
        // precisa continuar sendo "Attack" (é o que faz IsAttacking(), a trava
        // de rotação, o congelamento do Speed e o bloqueio de Draw/Sheath
        // valerem no ar sem nenhuma mudança), e um estado do Unity só tem UMA
        // tag — não dá pra ser "Attack" e "AirAttack" ao mesmo tempo.
        public const string AirAttack1 = "AirAttack1";
        public const string AirAttack2 = "AirAttack2";

        // -- Layers -------------------------------------------------------------
        public const string UpperBody = "UpperBody";
    }
}
