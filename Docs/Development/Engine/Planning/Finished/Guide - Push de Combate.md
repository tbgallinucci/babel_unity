# Guia — Push de Combate (força de knockback configurável por ataque)

## Contexto

Antes de fechar
[Guide - Vida, Dano e Inimigo de Teste.md](<./Guide - Vida, Dano e Inimigo de Teste.md>)
(vertical slice de vida/dano/inimigo — ainda não wired em Editor), decisão
de priorizar física de combate: um empurrão configurável por ataque
(Attack1/2/3, Attack2Alt), independente de dano/vida.

Push é **totalmente desacoplado** de `HealthComponent`/`EnemyDummy` — não
depende de nada daquele outro guia estar pronto, só compartilha o
pré-requisito trivial da Layer `Enemy` (que `PlayerAttackHitbox` já exige de
qualquer forma pro `OverlapSphere`). Dá pra fazer esse guia primeiro, testar
push isoladamente, e fazer o guia de vida/dano depois exatamente como já
está documentado — quando isso acontecer, só adicionar `KnockbackReceiver`
ao `EnemyDummy` real já dá push nele também, sem nenhuma mudança de código.

Todo o código (`KnockbackReceiver.cs`, e o ajuste em
`PlayerAttackHitbox.cs`) **já foi escrito** — o que falta é o wiring de
Editor documentado abaixo.

## Passo 1 — Layer "Enemy"

Se ainda não existir (mesmo passo do guia de vida/dano, não precisa repetir
se já foi feito): `Edit > Project Settings > Tags and Layers` → adicionar
uma User Layer nova chamada `Enemy`.

## Passo 2 — `PlayerAttackHitbox` no Player

No GameObject do Player, se ainda não tiver: adicionar `PlayerAttackHitbox`
(`Babel.Combat`), campo **Enemy Layer** → marcar a layer `Enemy`.

## Passo 3 — GameObject `PushTestDummy`

Objeto de teste dedicado, separado dos `LockOnDummy1/2/3` existentes (só
`Targetable`, sem collider/rigidbody) e do futuro `EnemyDummy` do outro
guia.

1. `GameObject > 3D Object > Capsule` na cena, renomear pra `PushTestDummy`,
   layer = `Enemy`.
2. Adicionar `Rigidbody` — marcar **Is Kinematic** (mesma convenção que o
   `EnemyDummy` do outro guia também vai usar).
3. O `Capsule Collider` que já vem no primitivo: manter **normal** (NÃO
   marcar Is Trigger — diferente do `EnemyDummy`, esse aqui não depende de
   trigger pra nada).
4. Adicionar `KnockbackReceiver` (`Babel.Combat`) — `Knockback Duration`
   default (0.2s) serve pro teste.
5. Posicionar num lugar alcançável a partir do spawn do Player.

## Passo 4 — Animation Events de push (e dano, se quiser fazer os dois juntos)

Nos clipes de ataque já existentes no Animator (frame de impacto de cada
um), adicionar/editar o Animation Event `OnAttackHit` — agora ele recebe o
`AnimationEvent` inteiro, então o MESMO evento carrega dois campos:
**Float** = dano e **Int** = força de push. Se quiser testar só push por
enquanto, pode deixar Float em 0 (dano fica efetivamente desligado) e
preencher só o Int — dá pra voltar depois e preencher o Float quando for
fazer o guia de vida/dano, sem criar evento novo.

| Clipe | Float (dano) | Int (push) |
|---|---|---|
| `Attack1` | 10 | 4 |
| `Attack2` | 10 | 4 |
| `Attack2Alt` | 20 | 10 |
| `Attack3` | 10 | 6 |

`SlideAttack` fica de fora dessa tabela (mesmo escopo do guia de vida/dano
original, só os 4 estados do combo) — se quiser push nele também, é um 5º
evento nesse clipe, à parte.

## Verificação (Play Mode)

- Aproximar do `PushTestDummy` e acertar Attack1/2/3 (e o branch Attack2Alt
  via Triângulo durante o Attack2) — o dummy desliza pra trás, distância
  visivelmente diferente por golpe (Attack2Alt bem mais longe/rápido que
  Attack1).
- Push não deve empilhar: hits consecutivos rápidos do combo reiniciam o
  empurrão (nova direção/força), sem acumular velocidade nem derivar
  verticalmente.
- Nenhum erro no Console ao acertar algo na layer `Enemy` que não tenha
  `KnockbackReceiver` — o null-check faz o push virar no-op ali.
- Regressão: combo, dodge, sprint, dash, slide attack continuam idênticos a
  antes desta passada (nenhuma mudança fora de `PlayerAttackHitbox`/novo
  componente).

## Arquivos tocados

- `Assets/Scripts/Combat/KnockbackReceiver.cs` (novo)
- `Assets/Scripts/Combat/PlayerAttackHitbox.cs` (editado — `OnAttackHit`
  passa a receber `AnimationEvent` em vez de `float`, mais a chamada de
  `KnockbackReceiver`)
- `Assets/Art/Animations/PlayerAnimatorController.controller` (Animation
  Events — Editor, Passo 4)
- `Assets/Scenes/SampleScene.unity` (layer, componente no Player,
  `PushTestDummy` — Editor, Passos 1-3)
