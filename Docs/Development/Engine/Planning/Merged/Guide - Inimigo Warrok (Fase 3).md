# Guia — Inimigo Warrok com IA (Fase 3)

## Contexto

Primeiro inimigo de verdade do projeto — substitui o `PushTestDummy`
(cápsula placeholder) pelo modelo Warrok já importado e riggado
(Humanoid), com roaming, aggro, ataque corpo-a-corpo, ataque de pulo
telegrafado e reação a dano. É a Fase 3 do
`Guide - Godot to Unity Migration.md`, com as decisões de arquitetura já
travadas em conversa (não reabrir):

- **Movimento**: `NavMeshAgent` dirige a posição; sem root motion — o
  Animator só reflete via `Speed = agent.velocity.magnitude`.
- **Roaming**: centro + raio a partir do spawn.
- **Telegraph**: disco simples escalando via código, sem shader.
- **Jump attack**: chance configurável a cada decisão de ataque, com
  cooldown próprio.
- **Reação a dano** (`Hit To Body`): hyper armor durante
  Attack/JumpAttack — não interrompe golpes comprometidos.

Todo o código já foi escrito — o que falta é 100% wiring de Editor.

## Passo 1 — Layer `Player`

Não existe hoje (o Player está em `Default`).

1. `Edit > Project Settings > Tags and Layers` → nova User Layer
   `Player`.
2. Selecionar o GameObject raiz do Player → **Layer** → `Player`.

## Passo 2 — Reconstruir `PushTestDummy` → `Enemy_Warrok`

Em cima do objeto existente, não do zero:

1. Renomear `PushTestDummy` para `Enemy_Warrok`.
2. Remover `MeshRenderer`/`MeshFilter` (a cápsula) — mantém
   `HealthComponent`/`Targetable`/`KnockbackReceiver`/`HitFlash`/
   `CapsuleCollider` (não-trigger)/`Rigidbody` (kinematic) como estão.
3. Arrastar `Assets/Art/Animations/Enemies/Warrok W Kurniawan.fbx` pra
   dentro da Hierarchy como **filho** de `Enemy_Warrok`.
4. No `Animator` desse filho (recém-criado pelo import): desmarcar
   **Apply Root Motion**. (O código também força isso em `Awake()`,
   defensivamente — mas deixa marcado certo no Editor também.)
5. `Add Component` no filho → **EnemyAttackHitbox** (`Babel.Combat`).
   Campo **Player Layer** → marcar a layer `Player` do Passo 1.
6. `Add Component` na raiz (`Enemy_Warrok`) → **NavMeshAgent** e
   **EnemyBase** (`Babel.Enemies`). Preencher os campos (valores
   sugeridos abaixo, ajustáveis ao vivo em Play Mode).
7. `Telegraph Prefab` do `EnemyBase` → arrastar o prefab do Passo 4.

### Valores sugeridos pra começar

| Campo | Valor |
|---|---|
| `Roam Radius` | 6 |
| `Roam Idle Min/Max` | 2 / 5 |
| `Roam Speed` | 1.5 |
| `Aggro Radius` | 8 |
| `Leash Radius` | 20 |
| `Chase Speed` | 4 |
| `Attack Range` | 2 |
| `Attack Cooldown` | 2 |
| `Jump Attack Chance` | 0.3 |
| `Jump Attack Cooldown` | 8 |
| `Jump Attack Land Normalized Time` | 0.85 |
| `Jump Attack Arc Height` | 2 |
| `Jump Attack Max Range` | 10 |

No `NavMeshAgent`, ajustar **Speed** pra bater com `Chase Speed` (o
agent tem seu próprio campo de velocidade máxima — `EnemyBase` já seta
`agent.speed` em runtime, mas o valor default do componente vale antes
disso e nos gizmos do Editor).

## Passo 3 — Bake do NavMesh

1. `Window > AI > Navigation` (ou `Add Component > NavMeshSurface` no
   chão/geometria da cena).
2. No chão: `Add Component` → **NavMeshSurface** (`com.unity.ai.navigation`
   — já instalado, nunca usado até agora).
3. **Bake** no painel do NavMeshSurface.

(Bake em runtime é escopo da Fase 5, quando salas forem geradas
proceduralmente — fora deste guia, um bake no Editor já cobre a cena
estática atual.)

## Passo 4 — `EnemyTelegraphDisc.mat` + `JumpAttackTelegraph.prefab`

1. `Assets > Create > Material` em `Assets/Art/Materials/` →
   `EnemyTelegraphDisc`. Shader **Universal Render Pipeline/Lit** (ou
   `/Unlit`), **Surface Type = Transparent**, cor base
   vermelho/laranja translúcido (alpha ~0.35).
2. Criar um `Quad` (`GameObject > 3D Object > Quad`) — rotacionar -90° no
   eixo X pra deitar no chão. Atribuir o material do passo 1.
   (Alternativa: `Cylinder` achatado no Y — mesma ideia, `localScale.y`
   bem pequeno.)
3. `Add Component` → **JumpAttackTelegraph** (`Babel.Enemies`).
4. Arrastar esse GameObject pra `Assets/Prefabs/` (criar a pasta se não
   existir) → vira `JumpAttackTelegraph.prefab`. **Importante**: o
   objeto precisa estar **ativo** no próprio prefab (não desmarcado no
   Inspector) — `JumpAttackTelegraph.Awake()` é quem esconde ele
   (`Hide()`); se o prefab já nascer inativo, `Awake()` nunca roda.
5. Apagar a instância da cena (só o prefab importa — `EnemyBase`
   instancia sozinho em `Awake()`).
6. Arrastar o prefab pro campo `Telegraph Prefab` do `EnemyBase` (Passo
   2.7, se ainda não fez).

## Passo 5 — `EnemyAnimatorController.controller`

Criar em `Assets/Art/Animations/` (`Assets > Create > Animator
Controller`), atribuir ao `Animator` do filho do `Enemy_Warrok`. Layer
única — sem split upper-body, o inimigo não tem arma pra mascarar.

**Parâmetros**: `Speed` (Float), `Attack` (Trigger), `JumpAttack`
(Trigger), `Hit` (Trigger).

**Estados**:
1. **`Locomotion`** (default) — Blend Tree 1D em `Speed`:
   - `Warrok W Kurniawan@Mutant Idle` no threshold 0
   - `Walking` no threshold = valor de `Roam Speed` (1.5, se usar o
     sugerido)
   - `Mutant Run` no threshold = valor de `Chase Speed` (4, se usar o
     sugerido)

   ⚠️ Diferente do `PlayerAnimatorController`: o `Speed` do inimigo é
   `agent.velocity.magnitude` em unidades reais (metros/segundo), não
   0-1 normalizado — os thresholds têm que bater com os valores reais
   escolhidos pros campos acima, não 0/0.5/1.
2. **`Attack`** — Motion = `Mutant Swiping`, sem loop, tag `"Attack"`.
3. **`JumpAttack`** — Motion = `Jump Attack`, sem loop.
4. **`Hit`** — Motion = `Hit To Body`, sem loop.

**Transições** (mesmo padrão pras 3: trigger + Has Exit Time **off** na
entrada, Has Exit Time **on** ~0.9-1.0 na saída, sem condição extra):
- `Locomotion -> Attack` / `Attack -> Locomotion`
- `Locomotion -> JumpAttack` / `JumpAttack -> Locomotion` (o pouso em si
  é tratado no MEIO do clipe via Animation Event, não no fim — a
  transição de saída é só limpeza/retorno visual)
- `Locomotion -> Hit` / `Hit -> Locomotion`

**Animation Events**:

| Clipe | Função | Parâmetros |
|---|---|---|
| `Mutant Swiping` | `OnAttackHit` | Float = dano (sugestão: 10), Int = push (sugestão: 6) |
| `Jump Attack` | `OnJumpAttackLand`, no frame correspondente a **85%** do clipe (bate com `Jump Attack Land Normalized Time`) | Float = dano (sugestão: 20), Int = push (sugestão: 12) |

Os dois despacham pro `EnemyAttackHitbox` — que está no MESMO GameObject
do `Animator` (o filho), não precisa apontar nada manualmente.

## Passo 6 — Confirmar pendência de outro guia

`HealthComponent` no Player — sinalizado como possivelmente não
persistido em `Status - Estado Atual e Pendencias.md`. Sem ele, os
ataques do inimigo não têm o que ferir. Confirma antes de testar.

## Verificação (Play Mode)

- Inimigo intercala Idle/Walk dentro do `Roam Radius`, nunca sai da
  zona.
- Aproximar até `Aggro Radius` dispara corrida + perseguição; sair do
  `Leash Radius` volta pro roam.
- Dentro de `Attack Range`, ataca a cada `Attack Cooldown`
  aproximadamente; hit conecta e machuca/empurra o Player.
- De vez em quando usa o jump attack: disco cresce do centro até o
  perímetro sob a posição do Player no instante do trigger; inimigo arca
  até lá; dano só acontece se o Player ainda estiver dentro no instante
  do pouso — sair no meio do telegraph evita o dano.
- Levar um golpe do Player toca `Hit To Body`, exceto se o inimigo já
  estiver no meio do próprio Attack/JumpAttack.
- Durante o jump attack, sem jitter entre a posição manual e o
  NavMeshAgent; depois do pouso, roam/chase resume limpo.
- Ataques do Player continuam danificando/empurrando o inimigo (regressão
  do que já funcionava no `PushTestDummy`).
- Ataques do inimigo só afetam a layer `Player` — bater em cenário não dá
  erro nem dano.
- Morte: agent/collider param, `deadColor` aplica (cinza), sem mais dano
  indo ou vindo.
- Sem erros de Console de `NavMeshAgent` fora do NavMesh (indicaria bake
  incompleto).

## Arquivos tocados

- `Assets/Scripts/Enemies/EnemyBase.cs` (novo)
- `Assets/Scripts/Combat/EnemyAttackHitbox.cs` (novo)
- `Assets/Scripts/Enemies/JumpAttackTelegraph.cs` (novo)
- `Assets/Scripts/Combat/KnockbackReceiver.cs` (editado — `IsActive`)
- `Assets/Art/Animations/EnemyAnimatorController.controller` (novo,
  Editor — Passo 5)
- `Assets/Art/Materials/EnemyTelegraphDisc.mat` (novo, Editor — Passo 4)
- `Assets/Prefabs/JumpAttackTelegraph.prefab` (novo, Editor — Passo 4)
- `Assets/Scenes/SampleScene.unity` (layer, `Enemy_Warrok`, NavMeshSurface
  — Editor, Passos 1-3)
