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
        // Mesmo idioma do Lunge acima: quem dirige o valor é uma curva chamada
        // "ForwardMomentum" dentro do clipe de SprintHeavyAttack1 (Import
        // Settings -> Animation -> Curves), não código. 1 no início do golpe
        // (mantém a velocidade cheia da corrida) descendo suavemente até 0 —
        // é o fator que OnAnimatorMove multiplica pela velocidade de sprint
        // CAPTURADA na entrada do golpe (ver capturedSprintMomentumSpeed).
        // Clipe sem a curva não contribui (fica 0), mesmo raciocínio de
        // segurança do Lunge.
        public const string ForwardMomentum = "ForwardMomentum";

        // Mesmo idioma do ComboQueued/StrongComboQueued: bool persistente,
        // não trigger. Attack1 dispara igual sempre (no aperto, sem
        // mudança) — segurar o botão além de chargeAttackHoldThreshold
        // enquanto ele toca marca esta fila, e a transição
        // Attack1 -> Attack1Charged no Animator (Has Exit Time, igual
        // Attack1 -> Attack2) consome ela no fim natural do golpe leve. Não
        // depende de soltar o botão — é um ENCADEAMENTO, não uma escolha
        // feita na soltura.
        public const string ChargeQueued = "ChargeQueued";
        // Bool "segurando o botão além do piso de carga" — só existe pra
        // alimentar uma pose/efeito de antecipação no Animator (ex.: um
        // brilho na arma, um leve slow-down) enquanto Attack1 ainda toca e a
        // carga já foi atingida. O C# não lê isso de volta.
        public const string IsCharging = "IsCharging";

        // Triggers das transições de embalo da locomoção (ver o comentário
        // de cabeçalho da seção em PlayerController) — disparados na BORDA de
        // início/fim de movimento e de saída do sprint, não todo frame.
        public const string RunStart = "RunStart";
        public const string RunEnd = "RunEnd";
        public const string SprintEnd = "SprintEnd";

        // Ataque pesado disparado DIRETO do sprint (não é um branch do combo
        // de chão — não passa por ComboQueued/StrongComboQueued). Trigger
        // próprio pelo mesmo motivo do Jump/JumpStart: o nome do trigger e o
        // nome do estado de destino não precisam (e aqui não devem) ser
        // iguais.
        public const string SprintHeavyAttack = "SprintHeavyAttack";

        // Plunge attack — trigger próprio, dispara a partir de qualquer
        // estado aéreo (AirLoop/JumpStart/AirAttack1/AirAttack2) pra
        // AirHeavyAttack1.
        public const string PlungeAttack = "PlungeAttack";

        // -- Tags -------------------------------------------------------------
        // "Attack" serve tanto de nome de Trigger (SetTrigger) quanto de tag
        // compartilhada por todo estado de combo — valor igual, dois usos,
        // uma constante só.
        public const string Attack = "Attack";
        public const string Dodging = "Dodging";

        // -- Nomes de estado ----------------------------------------------------
        // Só existe aqui pro reset dedicado do ataque carregado (ver o
        // comentário em HandleAttack) — o resto do código sempre falou com
        // o combo de chão só por tag ("Attack"), nunca precisou distinguir
        // Attack1 dos outros hits antes disso.
        public const string Attack1 = "Attack1";
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
        // Ataque carregado (golpe pesado a partir do neutro, ver
        // GreatSword_SPAttack2_Root). Tag "Attack" também — mesmo motivo dos
        // dois acima.
        public const string Attack1Charged = "Attack1Charged";
        // Ataque pesado a partir do sprint. Tag "Attack".
        public const string SprintHeavyAttack1 = "SprintHeavyAttack1";
        // Plunge: queda (AirHeavyAttack1, tag "Attack") e impacto
        // (AirHeavyAttack2, tag "Attack"). Por NOME, mesmo motivo dos outros
        // ataques aéreos.
        public const string AirHeavyAttack1 = "AirHeavyAttack1";
        public const string AirHeavyAttack2 = "AirHeavyAttack2";

        // -- Layers -------------------------------------------------------------
        public const string UpperBody = "UpperBody";
    }
}
