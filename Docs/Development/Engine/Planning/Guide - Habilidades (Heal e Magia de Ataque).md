# Guia — Habilidades (Heal e Magia de Ataque) — pendente, tratar à parte

## Contexto

Separado de propósito do resto do combate (dodge, ataque forte/Attack2Alt,
lock-on, câmera, inimigo de teste — todos concluídos e testados). Na época
em que `Heal`/`AttackMagic` foram desenhados
([Guide - Dodge, Strong Attack, Lock-on e Habilidades.md](<./Guide - Dodge, Strong Attack, Lock-on e Habilidades.md>)),
a decisão foi "só disparam animação, sem cura/dano funcional" — mas isso foi
decidido **antes** de existir `HealthComponent`/`PlayerAttackHitbox`
([Guide - Vida, Dano e Inimigo de Teste.md](<./Guide - Vida, Dano e Inimigo de Teste.md>)).
Agora que vida e dano existem de verdade, vale reabrir essa decisão quando
for retomar — dá pra fazer essas habilidades terem efeito real sem muito
esforço extra, já que a infraestrutura de dano já está pronta.

## Status atual (o que já existe, não mexer sem necessidade)

- **Input**: já wireado em `InputSystem_Actions.inputactions` —
  `Heal` (composite One Modifier: `leftTrigger` + `buttonNorth` no gamepad;
  tecla `3` direta no KBM) e `AttackMagic` (composite: `leftTrigger` +
  `buttonWest`; tecla `4` direta). `AbilityModifierHeld` (lê `leftTrigger`
  puro) já gateia `HandleAttack()`/`HandleStrongAttack()` pra não disparar
  combo/Attack2Alt junto quando L2 está segurado.
- **Código**: `PlayerController.HandleAbilities()` já dispara os triggers
  `Heal`/`AttackMagic` no Animator quando pressionado e `!IsAttacking()` —
  sem efeito nenhum além da animação (comentário no código já registra
  isso).
- **Animator**: parâmetros `Heal` (Trigger) e `AttackMagic` (Trigger) já
  existem. **Os estados em si (Passo 6 do guia de Dodge/Strong
  Attack/Lock-on) podem não ter sido criados ainded** — confirmar antes de
  continuar; o foco da sessão foi pro resto do combate depois desse passo.

## Lacuna de asset

- `AttackMagic` tem clipe (`spell cast.fbx`, já importado em
  `Assets/Art/Animations/Greatsword/`).
- **`Heal` não tem clipe dedicado** — nenhuma animação de cura existe no
  projeto ainda. Precisa de sourcing (Mixamo ou similar) antes de terminar
  o wiring, ou reaproveitar `spell cast.fbx` temporariamente pros dois
  (mesma pose, sem diferenciação visual até chegar um clipe próprio).

## Decisão a reabrir: efeito funcional agora que HealthComponent existe

Antes de continuar, decidir:

1. **Heal**: cura de verdade? Se sim, `HandleAbilities()` chamaria
   `health.Heal(amount)` (método novo a adicionar em `HealthComponent.cs` —
   hoje só tem `TakeDamage`, precisa do inverso, algo como
   `public void Heal(float amount) { CurrentHealth = Mathf.Min(maxHealth,
   CurrentHealth + amount); OnDamaged?.Invoke(...); }` ou um evento
   `OnHealed` separado, pra não confundir feedback visual de dano com cura).
2. **AttackMagic**: dano de verdade? Se sim, precisa de detecção de acerto
   — mesma técnica já usada em `PlayerAttackHitbox.OnAttackHit(float
   damage)` (Animation Event no clipe, no frame do "cast"), possivelmente
   um alcance/raio maior que o ataque físico (é magia, não corpo a corpo) —
   ou até um projétil de verdade, se quiser ir além do que já existe
   (`PlayerAttackHitbox` hoje só faz `OverlapSphere` na frente do
   personagem, não tem noção de projétil/hitscan à distância).
3. Se a resposta pra ambos for "não, continua só cosmético por enquanto" —
   tudo bem, não precisa fazer nada além de completar o wiring do Passo 6
   (estados no Animator) e seguir em frente.

## Quando retomar, passos sugeridos

1. Confirmar/criar os estados `Heal` e `AttackMagic` no Animator (Passo 6
   do guia de Dodge/Strong Attack — full-body, tag `"Ability"`, entrada de
   `ArmedLocomotion`/`Locomotion`, saída conforme `IsWielded`).
2. Resolver a lacuna de asset do Heal (source ou placeholder consciente).
3. Decidir e implementar o efeito funcional (ou confirmar que fica
   cosmético mesmo).
4. Se funcional: Animation Event de cura/dano nos clipes, mesma técnica do
   `PlayerAttackHitbox`.
5. Testar contra o `EnemyDummy` (pro `AttackMagic`) e observando
   `HealthComponent.CurrentHealth` do Player (pro `Heal`).

## Arquivos relevantes

- `Assets/Scripts/Player/PlayerController.cs` (`HandleAbilities()`)
- `Assets/Scripts/Combat/HealthComponent.cs` (precisaria do método `Heal`
  se for pra frente com efeito real)
- `Assets/Scripts/Combat/PlayerAttackHitbox.cs` (referência de como
  detecção de acerto já funciona, reaproveitável pro `AttackMagic`)
- `Assets/Art/Animations/PlayerAnimatorController.controller` (parâmetros
  já existem, estados pendentes de confirmação)
- `Assets/InputSystem_Actions.inputactions` (`Heal`/`AttackMagic`/
  `AbilityModifierHeld`, já prontos)
