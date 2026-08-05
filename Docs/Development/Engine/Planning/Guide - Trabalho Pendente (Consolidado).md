# Guia — Trabalho Pendente (Consolidado)

Ponto único de entrada pro que falta wirear no Editor, levantado em
2026-08-05 contra os guias que estavam soltos em `Planning/`. Os guias
originais (com o passo a passo completo, import settings, tabelas de
transição, armadilhas já encontradas etc.) foram movidos pra
`Merged/` — este documento não repete o conteúdo deles, **aponta pra
seção exata** de cada item. Use o
[Checklist - Pendencias Gerais](Checklist%20-%20Pendencias%20Gerais.md)
pra marcar o progresso dia a dia; use este guia quando precisar do
"como fazer" de um item específico.

`Guide - Godot to Unity Migration.md` continua no lugar (é o mapa
mestre de fases, nunca "termina"). `WFC_Plugin_Plano_Prompt_4.md`
também continua no lugar — é um projeto separado (plugin de geração
procedural), não faz parte do combate.

---

## Ordem sugerida

1. **Fase 4 do combate — restante** (§1 abaixo). Já em andamento
   (Charged Attack implementado); o resto (Run Start/End, Sprint Heavy,
   Plunge) é a continuação natural.
2. **Confirmar `HealthComponent` no Player** (§3) — bloqueia teste real
   de dano em qualquer inimigo, inclusive o Warrok.
3. **Habilidades — wiring de Editor** (§4) — código pronto, só 3 passos
   curtos.
4. **Limpeza pendente no Combate Aéreo** (§2) — bomba-relógio de
   parâmetros órfãos, baixo esforço.
5. **Inimigo Warrok (Fase 3)** (§5) — maior peça, mas o próximo salto
   real de gameplay (substitui o `PushTestDummy`).
6. **Robustez — itens de baixa prioridade** (§6) — só se aparecer
   sintoma ou quando a segunda arma chegar.

---

## 1. Fase 4 do Combate — restante

Fonte completa: `Merged/Guide - Combate Fase 4 (Charged, Run Start-End,
Sprint Heavy, Plunge).md`. Seção 1 (Ataque Carregado) **já
implementada**. Falta:

- **§2 — Run Start/End e Sprint End (Total Input Cancel)**: import de
  `Run Start Greatsword`/`Run End Greatsword`, 3 parâmetros Trigger
  novos (`RunStart`/`RunEnd`/`SprintEnd`), 3 estados novos, 9 transições
  de cancelamento total. Nota pendente: **Sprint End armado não tem
  clipe dedicado ainda** — o guia assume reaproveitar
  `GreatSword_Common_Run_End_Root` até haver um clipe específico (ver
  §2.1 do guia original).
- **§3 — Sprint/Run Heavy Attack 1**: escolher entre `Atack 1 Strong
  Greasword.FBX` e `GreatSword_Attack03_1_Root.FBX` (guia recomenda o
  primeiro pelo nome — confirmar visualmente), curva `ForwardMomentum`
  nova no import, 1 parâmetro Trigger, 1 estado, 3 transições.
- **§4 — Plunge Attack (maior peça)**: layers/Inspector primeiro (§4.0
  — `plungeImpactMask`, dois `CinemachineImpulseSource`, confirmar
  `CinemachineImpulseListener` na vcam), depois corte dos dois clipes
  fonte em `AirHeavyAttack1`/`AirHeavyAttack2` (§4.1), Animation Event
  `OnPlungeImpact` (não confundir com `OnAttackHit`/`OnAttackHitRadial`
  — dispara a lógica de religar colisão/soltar carry), 1 parâmetro
  Trigger, 2 estados, 7 transições.
- **§5 do guia original**: checklist de verificação em Play Mode pros
  quatro itens acima.

---

## 2. Combate Aéreo — limpeza pendente e item bloqueado

Fonte: `Merged/Guide - Combate Aereo (Jump, Launch e Ataques
Leves).md`, seção 0 e 4; `Merged/Guide - Combate Aereo (Passo a Passo
do Animator).md`, seção 9.

- **Limpeza de parâmetros órfãos no Animator** (seção 0 do guia de
  Jump/Launch) — o jump attack antigo foi removido do C# de propósito,
  mas os parâmetros correspondentes podem ter ficado no controller sem
  ninguém lendo/escrevendo. Baixo esforço, mas é bomba-relógio: um
  parâmetro órfão hoje é confuso, mas parâmetro órfão + estado novo
  reusando o nome errado no futuro é bug de verdade — já documentado
  como pendência conhecida em passadas anteriores.
- **Smash (Triângulo no ar)** — seção 4 do guia de Jump/Launch,
  **bloqueado por falta de animação** (não é wiring, é falta de asset).
  Fica parado até um clipe chegar.
- **Itens deliberadamente adiados** (seção 9 do guia Passo a Passo, não
  bloqueantes, sem prazo): pulo desarmado (clipes ainda são só da
  greatsword), cair de borda não abre `AirLoop` (comportamento atual,
  não regressão), Blend Tree vertical no lugar do `AirLoop` estático
  (falta de material — só existe um clipe aéreo hoje), ground check por
  `Physics.CheckSphere` (só trocar se aparecer engasgo real no pouso),
  inimigo sem estado aéreo (resolve natural quando o Warrok, §5, tiver
  IA própria).

---

## 3. Vida, Dano e Inimigo de Teste — confirmar persistência

Fonte: `Merged/Guide - Vida, Dano e Inimigo de Teste.md`;
`Merged/Status - Estado Atual e Pendencias.md`, §3.

- **Confirmar se `HealthComponent` persistiu no Player** — na última
  conferência direto na cena, só o `PushTestDummy`/`Enemy_Warrok` tinha
  o componente; o Player não. Se foi adicionado em Play Mode (não
  persiste) ou sem `Ctrl+S` depois, ainda falta. Sem isso: o player não
  leva dano, não é curado (`OnHealApplied`, item §4 abaixo não tem
  onde aplicar), e o cálculo de i-frames do dodge não tem health pra
  referenciar.
- `EnemyDummy` (dano por contato) já foi decidido como **abandonado** —
  não reabrir. Dano só vem de ataques (Player→inimigo e, quando o
  Warrok existir, inimigo→Player).

---

## 4. Habilidades (Heal e Magia de Ataque) — wiring de Editor

Fonte completa: `Merged/Guide - Habilidades (Heal e Magia de
Ataque).md`. Código 100% pronto (`HealthComponent.Heal(float)`,
`PlayerController.OnHealApplied`, reuso de `OnAttackHitRadial` pro
`AttackMagic`). Falta só Editor, 3 passos curtos:

1. Estados `Heal` (`great sword power up (heal).fbx`) e `AttackMagic`
   (`spell cast.fbx`) na layer base, tag `"Ability"`, entrando de
   `Locomotion`/`ArmedLocomotion` e saindo pro correspondente conforme
   `IsWielded` — mesmo padrão do `Dodge`/`Jump`. Os parâmetros
   (`Heal`/`AttackMagic`, Trigger) **já existem** no controller.
2. Animation Event `OnHealApplied` (Float = quantidade curada, sugestão
   30) no clipe de Heal, no frame em que o gesto completa.
3. Animation Event `OnAttackHitRadial` (Float = dano, sugestão 15; Int
   = push, sugestão 0) no clipe `spell cast`, no frame do "cast".

Depende de §3 (Player precisa de `HealthComponent` pra `OnHealApplied`
ter o que curar).

---

## 5. Inimigo Warrok com IA (Fase 3)

Fonte completa: `Merged/Guide - Inimigo Warrok (Fase 3).md`. Código
100% pronto (`EnemyBase`, `EnemyAttackHitbox`, `JumpAttackTelegraph`).
Falta 100% wiring de Editor, maior peça pendente do projeto hoje:

1. Nova User Layer `Player` (Project Settings) + atribuir no root do
   Player.
2. Reconstruir `PushTestDummy` → `Enemy_Warrok`: trocar visual pelo
   Warrok riggado, adicionar `EnemyAttackHitbox` no filho do Animator e
   `NavMeshAgent`/`EnemyBase` na raiz.
3. Bake do NavMesh (`NavMeshSurface` no chão, `com.unity.ai.navigation`
   já instalado).
4. Material + prefab do telegraph do jump attack
   (`EnemyTelegraphDisc.mat`, `JumpAttackTelegraph.prefab` — atenção:
   o prefab precisa nascer **ativo**, é o próprio `Awake()` que esconde).
5. `EnemyAnimatorController.controller` novo: Blend Tree 1D em `Speed`
   (unidades reais, não normalizado — atenção ao thresholds), mais
   `Attack`/`JumpAttack`/`Hit`, com Animation Events `OnAttackHit` e
   `OnJumpAttackLand` (85% do clipe).
6. Depende de §3 confirmado — sem `HealthComponent` no Player os
   ataques do inimigo não têm o que ferir.

---

## 6. Robustez e Data-Driven do Combate — itens de baixa prioridade

Fonte: `Merged/Guide - Robustez e Data-Driven do Combate.md`;
`Merged/Status - Estado Atual e Pendencias.md`, §4. Itens 2, 3, 6 já
concluídos — não reabrir. Restam, todos nice-to-have sem urgência:

- **Item 1 — fila universal (`PendingAction`)**: generalizar o padrão
  já usado à mão em `DodgeQueued`/`AttackQueued`/`ChargeQueued`/etc.
  Só vale se aparecer sintoma de bug por duplicação desse padrão.
- **Item 4 — data-driven por arma** (Animator Override Controller +
  `WeaponMoveset`): **adiado explicitamente**, sem segunda arma real
  seria abstração prematura. Quando a hora chegar, existe skill pronta:
  `.claude/skills/unity-weapon-animator-override/`.
- **Item 5 — validação em Editor** dos nomes de parâmetro contra o
  controller real: factível agora que `AnimStrings` existe, mas
  continua nice-to-have.
