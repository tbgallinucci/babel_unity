# Guia — Habilidades (Heal e Magia de Ataque)

## Contexto

Retomado depois do combate físico estar fechado ponta a ponta
(`HealthComponent`/`PlayerAttackHitbox`/`KnockbackReceiver`/`HitStop`/
`HitFlash` — ver `Status - Estado Atual e Pendencias.md`). A decisão
original ("só animação, sem efeito real") foi reaberta e revertida: as
duas agora são **funcionais**.

Decisões confirmadas nesta passada:

- **Heal**: cura de verdade, via `HealthComponent.Heal(float)` (método
  novo — antes só existia `TakeDamage`).
- **AttackMagic**: dano de verdade, reusando
  `PlayerAttackHitbox.OnAttackHitRadial` (já existia, criado pro giro do
  slide attack) — sem projétil de verdade, é um golpe radial centrado no
  player, mesma técnica.
- **Asset do Heal**: a lacuna que o guia antigo apontava não existe mais —
  `Assets/Art/Animations/Greatsword/great sword power up (heal).fbx` já
  está no projeto.

Todo o código já foi escrito — o que falta é 100% wiring de Editor.

## Status atual (confirmado contra o controller)

- **Input**: `Heal`/`AttackMagic`/`AbilityModifierHeld` já wireados
  (`InputSystem_Actions.inputactions`), sem mudança necessária.
- **Parâmetros**: `Heal` (Trigger) e `AttackMagic` (Trigger) já existem no
  `PlayerAnimatorController`.
- **Estados**: `Heal` e `AttackMagic` **não existem** — confirmado
  diretamente no controller (só os parâmetros, nenhum estado com tag
  `Ability`). É o Passo 6 do guia antigo de Dodge/Strong Attack, nunca
  feito.
- **Código**: `PlayerController.HandleAbilities()` dispara os dois
  triggers. `HealthComponent.Heal(float)` e
  `PlayerController.OnHealApplied(float)` (Animation Event receiver, cura
  o próprio player) já existem.

## Passo 1 — Estados `Heal` e `AttackMagic` (layer base)

Full-body, sem overlay de UpperBody — corpo inteiro faz o gesto de
conjurar, sem locomoção por baixo.

1. **`Heal`**: Motion = `great sword power up (heal).fbx`, tag `"Ability"`.
2. **`AttackMagic`**: Motion = `spell cast.fbx` (já importado), tag
   `"Ability"`.
3. Entrada de cada um, de `ArmedLocomotion` **e** `Locomotion` (funciona
   armado e desarmado), condição = trigger correspondente (`Heal` /
   `AttackMagic`), Has Exit Time **off**.
4. Saída de cada um: Has Exit Time **on**, dois destinos conforme
   `IsWielded` (`-> ArmedLocomotion` se true, `-> Locomotion` se false) —
   mesmo padrão do `Dodge`/`Jump`.

## Passo 2 — Animation Event de cura no clipe de Heal

No clipe `great sword power up (heal)`, no frame em que o gesto
"completa" (a mão/arma termina o movimento de canalizar):

- Function: **`OnHealApplied`**
- Float: quantidade curada (sugestão pra teste: **30**)

## Passo 3 — Animation Event de dano no clipe de AttackMagic

No clipe `spell cast`, no frame do "cast":

- Function: **`OnAttackHitRadial`**
- Float: dano (sugestão: **15** — mais que os hits físicos normais, é
  magia)
- Int: push (sugestão: **0** — cosmético decidir se magia empurra; 0
  desliga sem quebrar nada, mesmo comportamento já usado no giro do slide
  attack quando você zerou o push dele)

O raio desse hit é `radialHitRadius` (compartilhado com o golpe giratório
do slide attack, hoje **3**) — se quiser um alcance diferente pra magia
especificamente, seria um novo campo em `PlayerAttackHitbox` (não existe
ainda; avisar se quiser separar).

## Verificação (Play Mode)

- L2+tecla 3 (ou Triângulo) toca a animação de cura e
  `HealthComponent.CurrentHealth` do Player sobe (inspecionar no
  Inspector, ou dar dano antes pra ver subir de volta). Não cura acima do
  `Max Health`.
- L2+tecla 4 (ou Quadrado) toca `spell cast` e tira vida de quem estiver
  no raio ao redor do player (testável no `PushTestDummy`) — inclusive
  hit-stop e flash, já que reusa o mesmo `OnAttackHitRadial`.
- Funciona armado e desarmado (destino de saída correto via `IsWielded`).
- Segurar L2 e apertar Triângulo/Quadrado **não** deve disparar
  `StrongComboQueued`/combo normal junto (`IsAbilityModifierHeld()` já
  gateia isso — regressão, não deveria ter mudado).
- Regressão: resto do combate (combo, dodge, slide attack, lock-on)
  continua idêntico.

## Arquivos tocados

- `Assets/Scripts/Combat/HealthComponent.cs` (editado — `Heal(float)` +
  evento `OnHealed`)
- `Assets/Scripts/Player/PlayerController.cs` (editado — comentário de
  `HandleAbilities()` + `OnHealApplied(float)` novo)
- `Assets/Art/Animations/PlayerAnimatorController.controller` (Passo 1 —
  Editor)
- `Assets/Art/Animations/Greatsword/great sword power up (heal).fbx` e
  `spell cast.fbx` (Animation Events — Passos 2-3, Editor)
