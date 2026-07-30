# Status — Estado Atual e Pendências

Foto do projeto Unity levantada **contra o código e a cena**, não contra o
que os guias dizem — vários guias descrevem passos que nunca foram
executados no Editor, e um descreve como pendente algo que já foi feito.
Serve de índice: qual guia ainda vale, o que exatamente falta em cada um.

Data do levantamento: 2026-07-30.

---

## 1. O que está funcionando hoje

Combate do player fechado ponta a ponta contra um alvo real:

- **Locomoção**: idle/andar/correr com root motion, sprint (toggle), dash,
  pulo, dodge roll com i-frames calculados (sem consumidor — ver §3).
- **Arma**: draw/sheath com troca de socket por Animation Event, layer
  `UpperBody` mascarada (dá pra andar sacando/guardando).
- **Combo**: Attack1 → Attack2 → Attack3, mais os branches `Attack1Alt1`/
  `Attack1Alt2` (chutes) e `Attack2Alt` (ataque forte), slide attack a
  partir do sprint, jump attack.
- **Filas de input**: `ComboQueued`, `StrongComboQueued`, `DodgeQueued`,
  `AttackQueued` — todos bool persistente lido por condição do Animator,
  em vez de trigger cru (que se perdia quando caía num frame bloqueado).
- **Estados espelhados por código**: `IsDodging`, `IsSliding`,
  `IsAttacking`, `IsJumpAttacking` — o Animator não decide mais sozinho,
  por Exit Time, quando sair de estados cuja duração o código conhece
  melhor.
- **Lock-on**: R3/Tab trava, flick do analógico cicla, câmera dedicada.
- **Dano e feedback**: `OnAttackHit`/`OnAttackHitRadial` via Animation
  Event (Float = dano, Int = push) em **7 clipes**, `HealthComponent`,
  `KnockbackReceiver` (empurrão), `HitStop` (congelamento no acerto),
  `HitFlash` (pisca ao levar dano).

### Alvo de teste

Existe **um** `PushTestDummy` na cena (layer `Enemy`), com
`HealthComponent` + `Targetable` + `KnockbackReceiver` + `HitFlash` +
CapsuleCollider não-trigger + Rigidbody kinematic. Ele acumula os papéis
que os guias previam separados (dummy de lock-on + inimigo de teste).

---

## 2. Guias — status real

| Guia | Status |
|---|---|
| `Godot to Unity Migration` | **Mapa mestre** — nunca "termina", é o roteiro de fases. Fica. |
| `Finished/Initial Movement Animation` | ✅ concluído |
| `Finished/Disarmed and Weapon Animation` | ✅ concluído |
| `Finished/Log - Sprint, Dash e Slide Attack` | ✅ concluído |
| `Finished/Dodge, Strong Attack, Lock-on e Habilidades` | ✅ concluído **exceto o Passo 6** (estados `Heal`/`AttackMagic`), que virou o guia de Habilidades |
| `Finished/Push de Combate` | ✅ concluído — movido nesta passada |
| `Vida, Dano e Inimigo de Teste` | ⚠️ **parcial** — ver §3 |
| `Robustez e Data-Driven do Combate` | ⚠️ **parcial** — ver §4 (itens 2, 3, 6 feitos; achado e corrigido um bug novo fora da lista original) |
| `Habilidades (Heal e Magia de Ataque)` | ⚠️ **código pronto, falta wiring de Editor** — ver §5 |

---

## 3. `Vida, Dano e Inimigo de Teste` — o que falta

Feito: Layer `Enemy`, `PlayerAttackHitbox` no Player com a máscara certa,
Animation Events (7 clipes, mais que os 4 previstos).

1. **`HealthComponent` no Player — confirmar se persistiu.** Na última
   conferência direto no `SampleScene.unity`, só o `PushTestDummy` tinha
   `HealthComponent`; o Player não. Se foi adicionado em Play Mode (não
   persiste) ou sem salvar depois, ainda está faltando — **checar e
   Ctrl+S**. Enquanto não persistir: `PlayerController.health` fica
   `null`, o cálculo de `IsDodgeInvulnerable` (i-frames do dodge) não
   chega em lugar nenhum, e o player não pode levar dano nem ser curado
   por `OnHealApplied` (ver §5).
2. **`EnemyDummy` — conflito de collider resolvido por decisão do
   usuário**: em vez de um collider-filho dedicado, o
   `PushTestDummy`/`CapsuleCollider` vira **Is Trigger** (aceitando que o
   player atravessa o inimigo — não é mais prioridade poder bloquear).
   Passos: marcar Is Trigger + `Add Component` → `EnemyDummy`
   (`Babel.Enemies`) no `PushTestDummy` (já satisfaz
   `HealthComponent`/`Targetable`/`Rigidbody`, os `[RequireComponent]` do
   script). Ainda não confirmado se foi feito.
3. Verificações do guia que dependem dos itens acima: dano por contato,
   validar i-frames atravessando o inimigo no meio do roll.

---

## 4. `Robustez e Data-Driven do Combate` — o que falta

Feito: item **2** (`AnimatorStateUtil` centralizado), item **3**
(`AnimStrings`, ~30 literais em `PlayerController` convertidos), item
**6** (parcial — os dois scripts consomem o mesmo utilitário), todo o
wiring de Editor da §7 (`AttackQueued`, `IsDodging`, `IsSliding`,
`Attack2Alt -> Dodge`), e um bug novo fora da lista original (corrida de
`Update()` entre `PlayerController`/`WeaponEquipController` no
Draw/Sheath — ver §0 do guia).

Falta:

- **Item 1 — fila universal.** `DodgeQueued`/`AttackQueued` são instâncias
  do padrão, mas feitas à mão. O helper reaproveitável (`PendingAction`)
  não existe. Baixa urgência: só vale se aparecer sintoma.
- **Item 4 — data-driven por arma** (Animator Override Controller +
  `WeaponMoveset`). **Confirmado explicitamente adiado** — sem segunda
  arma real, seria abstração prematura. Existe uma skill pronta pra
  automatizar quando chegar a hora
  (`.claude/skills/unity-weapon-animator-override/`).
- **Item 5 — validação em Editor** dos nomes contra o controller real.
  Agora que o item 3 (`AnimStrings`) existe, isso é factível — mas
  continua nice-to-have, não bloqueante.

---

## 5. `Habilidades (Heal e Magia de Ataque)` — código pronto, falta Editor

Decisão tomada: **as duas viram funcionais.** `Heal` cura de verdade
(`HealthComponent.Heal(float)`, novo, + `PlayerController.OnHealApplied`
como receptor de Animation Event — auto-alvo, sem `OverlapSphere`).
`AttackMagic` causa dano de verdade reusando `OnAttackHitRadial` (já
existia, criado pro giro do slide attack) — zero código novo pra essa
parte. A lacuna de asset do `Heal` que o guia antigo apontava **não existe
mais**: `great sword power up (heal).fbx` já está no projeto.

Falta só o wiring de Editor (guia reescrito, passo a passo):
1. Estados `Heal`/`AttackMagic` no Animator (nunca foram criados — só os
   parâmetros existem).
2. Animation Event `OnHealApplied` no clipe de Heal.
3. Animation Event `OnAttackHitRadial` no clipe de AttackMagic.

---

## 6. Ordem sugerida

1. **Confirmar §3** — `HealthComponent` persistido no Player + Editor do
   `EnemyDummy`/Is Trigger no `PushTestDummy` (ver detalhes acima).
2. **Editor de §5 (habilidades)** — código pronto, só falta wirear os 3
   passos do guia reescrito.
3. **Item 1 do §4 (fila universal)** — baixa urgência, só se aparecer
   sintoma.
4. **Item 5 do §4 (validação em Editor)** — agora factível com
   `AnimStrings` pronto, nice-to-have.
5. **Item 4 do §4 (data-driven por arma)** — só quando a segunda arma
   entrar.

Fora deste doc, o próximo salto de verdade é a **Fase 3 do guia de
migração** (inimigo real com IA/NavMesh), que é o que transforma o
`PushTestDummy` em jogo.
