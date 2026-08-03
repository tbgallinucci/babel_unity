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
        public const string JumpAttack = "JumpAttack";
        public const string JumpAttackLand = "JumpAttackLand";
        public const string IsJumpAttacking = "IsJumpAttacking";
        public const string Dodge = "Dodge";
        public const string DodgeQueued = "DodgeQueued";
        public const string IsDodging = "IsDodging";
        public const string ComboQueued = "ComboQueued";
        public const string StrongComboQueued = "StrongComboQueued";
        public const string AttackQueued = "AttackQueued";
        public const string IsAttacking = "IsAttacking";
        public const string IsSliding = "IsSliding";
        public const string Heal = "Heal";
        public const string AttackMagic = "AttackMagic";
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
        public const string Dashing = "Dashing";

        // -- Nomes de estado ----------------------------------------------------
        public const string SlideAttack = "SlideAttack";
        public const string Attack1Alt1 = "Attack1Alt1";
        public const string Attack1Alt2 = "Attack1Alt2";
        public const string Attack2Alt = "Attack2Alt";
        public const string Attack2AltTail = "Attack2AltTail";
        public const string ArmedJumpAttack2Alt = "ArmedJumpAttack2Alt";

        // -- Layers -------------------------------------------------------------
        public const string UpperBody = "UpperBody";
    }
}
