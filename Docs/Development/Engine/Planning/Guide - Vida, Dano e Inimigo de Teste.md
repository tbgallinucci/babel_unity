# Guia — Vida, Dano e Inimigo de Teste (vertical slice)

## Contexto

Continuação do
[Guide - Dodge, Strong Attack, Lock-on e Habilidades.md](<./Guide - Dodge, Strong Attack, Lock-on e Habilidades.md>).
Até aqui nenhum sistema de combate é testável de verdade — não existe dano,
vida nem inimigo. Esta passada fecha o menor recorte possível (vertical
slice) pra sentir o combate: um `HealthComponent` genérico (Player e
inimigo usam o mesmo), detecção de acerto via Animation Event +
`OverlapSphere` (exatamente como o `Guide - Godot to Unity Migration.md`
já planeja pra Fase 1), e um inimigo de teste parado — sem IA/NavMesh, isso
é Fase 3, fora de escopo agora.

Decisões de escopo já confirmadas:
- **Inimigo**: cápsula simples, sem rig/animação — feedback de dano por
  flash de cor no material.
- **Comportamento**: parado, só "punching bag" — sem perseguir o player.
- **Vida do player**: só o `HealthComponent`, sem UI nova (acompanhar via
  Debug.Log/Inspector em Play Mode).

Todo o código (`HealthComponent.cs`, `PlayerAttackHitbox.cs`,
`EnemyDummy.cs`, e o ajuste em `PlayerController.cs`) **já foi escrito** — o
que falta é o wiring de Editor documentado abaixo.

## Passo 1 — Layer "Enemy"

`Edit > Project Settings > Tags and Layers` → adicionar uma User Layer nova
chamada `Enemy`. É o filtro que `PlayerAttackHitbox` usa no
`Physics.OverlapSphere` pra só acertar inimigos (não a si mesmo nem cenário).

## Passo 2 — Componentes no Player

No GameObject do Player (mesmo que já tem `PlayerController`,
`WeaponEquipController`, `TargetingSystem`, `CameraLockOnController`):

1. Adicionar `HealthComponent` (`Babel.Combat`) — `Max Health` no default
   (100) serve pro teste.
2. Adicionar `PlayerAttackHitbox` (`Babel.Combat`) — campo **Enemy Layer**
   → marcar a layer `Enemy` criada no Passo 1. `hitRange`/`hitRadius`/
   `hitHeight` já vêm com defaults razoáveis (1.2/1/1); dá pra ver o
   Gizmo (esfera vermelha) selecionando o Player em Scene View com o
   personagem parado de frente pra onde o golpe deveria acertar, e ajustar
   se ficar curto/longo demais.

Nenhum campo de `PlayerController` precisa de wiring novo — `health` é
resolvido sozinho via `GetComponent<HealthComponent>()` no `Awake()`.

## Passo 3 — Animation Events de dano

> **Atualização**: a passada de combate mudou desde a versão original deste
> guia — o antigo `StrongAttack` virou `Attack2Alt` (branch do combo a
> partir do Attack2, veja o outro guia), e **Heal/AttackMagic ficaram pra
> depois** (decisão do usuário: consolidar primeiro o combo básico contra
> um alvo real, habilidades entram numa passada futura). Por enquanto, só
> os 4 estados de dano do combo físico levam evento.

Nos clipes de ataque já existentes no Animator (frame de impacto de cada
um — geralmente o frame em que a lâmina "conecta" visualmente no swing),
adicionar um Animation Event chamando `OnAttackHit`, com o parâmetro
**float** = dano daquele golpe:

| Clipe/Estado | Dano sugerido |
|---|---|
| `Attack1` | 10 |
| `Attack2` | 10 |
| `Attack2Alt` | 20 |
| `Attack3` | 10 |

`AttackMagic`/`Heal` ficam sem evento por enquanto (habilidades adiadas). O
evento despacha contra o `PlayerAttackHitbox` do Passo 2 (mesmo GameObject
do Animator).

## Passo 4 — GameObject `EnemyDummy`

1. `GameObject > 3D Object > Capsule` na cena, renomear pra `EnemyDummy`,
   layer = `Enemy`.
2. Componentes:
   - `HealthComponent` (`Babel.Combat`).
   - `Targetable` (`Babel.Combat`) — já existe do lock-on; pode inclusive
     substituir um dos dummies `LockOnDummy` já criados no guia anterior,
     já que agora tem vida/dano de verdade.
   - `EnemyDummy` (`Babel.Enemies`) — vai exigir `HealthComponent`,
     `Targetable` e `Rigidbody` automaticamente (`RequireComponent`).
   - No `Rigidbody` adicionado automaticamente: marcar **Is Kinematic**
     (não queremos física/gravidade nele, só trigger).
   - O `Capsule Collider` que já vem no primitivo: marcar **Is Trigger**
     (é o que `EnemyDummy.OnTriggerStay` escuta pro dano de contato).
3. Posicionar num lugar alcançável a partir do spawn do Player.

## Verificação (Play Mode)

- Combo (Quadrado ×3, incluindo o branch Attack2Alt via Triângulo durante o
  Attack2) tira vida do `EnemyDummy` — flash vermelho rápido a cada hit. Ao
  chegar a 0, ele fica cinza e o collider desliga (não recebe nem dá mais
  dano). Habilidades ficam de fora dessa verificação por enquanto.
- Ficar parado encostado no `EnemyDummy` tira vida do Player a cada
  `contactDamageInterval` (1s por padrão) — inspecionar
  `HealthComponent.CurrentHealth` do Player no Inspector durante Play Mode
  (ou adicionar um `Debug.Log` temporário em `HandleDamaged`, se quiser
  acompanhar no Console).
- Dar dodge roll (Círculo) atravessando o `EnemyDummy` bem no meio do roll
  não deve tirar vida do Player (janela de i-frame,
  `dodgeIFrameStart`/`dodgeIFrameEnd` em `PlayerController`); encostar fora
  dessa janela (parado do lado, sem rolar) deve tirar normalmente — valida
  a ligação `IsDodgeInvulnerable` → `HealthComponent.IsInvulnerable`.
- Lock-on (R3) continua travando no `EnemyDummy` normalmente — regressão do
  `TargetingSystem`/`Targetable` da passada anterior.
- Regressão: sprint, dash, slide attack, draw/sheath, combo e pulo
  continuam idênticos a antes desta passada.

## Arquivos tocados

- `Assets/Scripts/Combat/HealthComponent.cs` (novo)
- `Assets/Scripts/Combat/PlayerAttackHitbox.cs` (novo)
- `Assets/Scripts/Enemies/EnemyDummy.cs` (novo)
- `Assets/Scripts/Player/PlayerController.cs` (editado — campo `health` +
  sync de `IsInvulnerable` em `OnAnimatorMove`)
- `Assets/Art/Animations/PlayerAnimatorController.controller` (Animation
  Events — Editor, Passo 3)
- `Assets/Scenes/SampleScene.unity` (layer, componentes no Player,
  `EnemyDummy` — Editor, Passos 1-2 e 4)
