# Guia — Fase 4 do Combate: Ataque Carregado, Run/Sprint Start-End, Sprint Heavy Attack, Plunge Attack

Companheiro do C# já aplicado em `PlayerController.cs`, `AnimStrings.cs`,
`KnockbackReceiver.cs`, `PlayerAttackHitbox.cs` e `WeaponEquipController.cs`
(nesta mesma passada). O código está pronto e compila; o que falta é 100%
Animator — import settings, Animation Events, estados e transições, na mesma
ordem e no mesmo idioma dos guias anteriores (`Guide - Combate Aereo (Passo a
Passo do Animator).md` é a referência de convenções: **HET** = Has Exit Time,
**Dur** = Transition Duration).

**Ordem obrigatória, igual sempre**: import settings → Animation Events →
Animator. Fazer o Animator antes dos clipes deixa estados apontando pra clipe
sem curva/event, e o sintoma não aponta pra causa.

---

## 0. Antes de tudo — sobre o comentário do AirLoop

Você mencionou de passagem: *"talvez o air loop não esteja funcionando mto
bem... acho que juntar o jump start direto no jump end fica melhor até pro
combate aéreo"*.

**Recomendo não fazer essa fusão**, pelo menos não como "apagar o AirLoop e
ligar JumpStart direto no JumpEnd". Não é resistência a mexer — é que o
`AirLoop` hoje é o **hub** de onde saem/entram ~10 transições já calibradas a
dedo (`Guide - Combate Aereo (Passo a Passo do Animator).md`, seções 5.3, 5.5,
5.6): os dois ataques aéreos entram e voltam pra ele, o dash aéreo sai dele e
volta pra ele, o pouso normal sai dele. Fundir `JumpStart` em `JumpEnd`
removeria exatamente o estado que representa "no ar, disponível pra agir" —
`JumpStart` é decolagem (clipe fixo, curto) e `JumpEnd` é pouso (clipe fixo,
curto); nenhum dos dois é loop, e um golpe aéreo ou um dash no meio de um
clipe de pouso/decolagem não tem pra onde voltar depois.

O sintoma real de "o air loop não está funcionando bem" quase certamente é um
dos dois problemas **já documentados e com remédio conhecido** na seção 4
daquele guia:

1. **Bob/tremor do `Air.FBX`** — o clipe é a fatia aérea de um pulo capturado
   (frames 48–96 de `GreatSword_Jump_Loop_Root`), não uma pose neutra de
   queda, então ele sobe-e-desce sozinho. Se `Speed` está baixo, isso lê como
   uma "freada" estranha na queda; se subiu pra compensar, vira tremor visível.
   **Remédio**: encurtar o *range* do clipe no import (`firstFrame`/
   `lastFrame`) pra uma janela de 8-15 frames perto do ápice do salto
   original, onde o corpo quase não sobe nem desce — e só depois voltar o
   Speed pra perto de 1.
2. **O corte `JumpStart → AirLoop` em 0.6** (em vez de deixar o clipe
   terminar) — é um cinto de segurança pra não mostrar a perna descendo antes
   da hora. Se o *Bake Into Pose* em Y do `JumpStart`/`Air`/`JumpEnd` estiver
   como a seção 1 daquele guia manda (marcado), já dá pra **testar voltar esse
   Exit Time pra 0.9** e ver se o corte ainda é necessário.

Se depois de aplicar os dois remédios acima o loop ainda incomodar, o guia já
tem o plano de fundo pra isso na seção 9: substituir o `AirLoop` estático por
uma Blend Tree em velocidade vertical (subida/queda) — aí sim é uma mudança de
arquitetura, mas que mantém o hub, só melhora o que toca dentro dele.

**Se depois de tentar isso você ainda quiser seguir com a fusão**, é uma
decisão de arquitetura grande o bastante (reescrever as ~10 transições da
seção 5 daquele guia) pra valer uma conversa à parte antes — me avise e a
gente desenha isso especificamente, com a lista completa do que precisa ser
recableado.

O resto deste guia assume o `AirLoop` continua existindo como hoje.

---

## 1. Ataque Carregado (hold-to-chain)

### O que o C# já faz

`Attack1` dispara exatamente como antes — no APERTO, sem nenhuma mudança de
responsividade. O que é novo é o que acontece **enquanto ele toca**: se o
jogador segurar o botão de Ataque (o mesmo aperto, sem soltar) por pelo menos
`chargeAttackHoldThreshold` (padrão 0.35s), o combo encadeia sozinho pro
`Attack1Charged` no fim natural do `Attack1` — **dois golpes na sequência**
(leve + pesado), sem precisar soltar e apertar de novo.

Mesmo idioma do `ComboQueued`/`StrongComboQueued`, só que a fila
(`ChargeQueued`) é aberta por **segurar** em vez de **tocar de novo**:

- Soltou antes do piso → só o toque normal, `Attack1` sozinho, sem
  continuação.
- Segurou até o piso → `chargeQueued` fica marcado (não desmarca se soltar
  DEPOIS — mesma regra do `ComboQueued`: a intenção já foi "gasta"). O
  Animator consome isso na transição `Attack1 -> Attack1Charged`, com o
  MESMO idioma de `ComboWindowOpen()` (Has Exit Time no clipe + condição —
  ver a tabela de transições abaixo).
- `IsCharging` (bool) espelha `chargeQueued` — serve só pro Animator mostrar
  algum feedback (brilho na arma, leve slow-down) enquanto o `Attack1` ainda
  toca e a carga já foi atingida (ver passo 1.6).
- **Nenhum código de cancelamento dedicado precisou ser escrito**: se o
  jogador rolar pra fora do `Attack1` no meio da carga (Dodge cancela ataque
  leve na hora, sem fila — ver `HandleDodge`), o Animator já troca de estado,
  e o reset por-borda que já existia (o mesmo que zera `comboQueued`/
  `strongComboQueued`/etc. a cada troca real de estado) zera `chargeQueued`
  junto — a intenção morre com o golpe em vez de disparar um Attack1Charged
  fora de hora depois.

### 1.1 Import settings

`GreatSword_SPAttack2_Root.FBX` (`Assets/Art/Animations/Player/Battle/Light
Attack/Ground/`) — o mesmo arquivo que os ataques de chão já usam como
referência:

| Ajuste | Valor |
|---|---|
| Loop Time | ✗ |
| Root Transform Position (Y) → Bake Into Pose | **desmarcado** (mesmo grupo dos ataques — ver a tabela da seção 1 do guia de Combate Aéreo) |
| Root Transform Position (XZ) → Bake Into Pose | **desmarcado** (o golpe usa o root motion horizontal próprio) |
| Curva `Lunge` | opcional, mesma convenção dos outros ataques — dá o avanço pra frente no acerto |

### 1.2 Animation Events

| Clipe | Função | Quando | Parâmetros |
|---|---|---|---|
| `GreatSword_SPAttack2_Root` | `OnAttackHit` | frame de impacto | Float = dano (referência: 10-16, é o golpe pesado), Int = push |

Cai no GameObject do Animator, mesma regra de sempre (`PlayerAttackHitbox`
mora lá).

### 1.3 Parâmetros novos no Animator

Já existem no C# (`AnimStrings`), precisam ser criados no controller:

| Parâmetro | Tipo |
|---|---|
| `ChargeQueued` | Bool |
| `IsCharging` | Bool |

### 1.4 Estado novo

| Estado | Clipe | Tag | Speed |
|---|---|---|---|
| `Attack1Charged` | `GreatSword_SPAttack2_Root` | `Attack` | `CombatSpeedMultiplier` (Parameter, igual Attack1/2/3) |

Filho direto da Base Layer, igual `Attack1Alt1`/`Attack1Alt2`/`Attack2Alt`.

### 1.5 Transições

| De | Para | HET | Exit | Dur | Condições |
|---|---|---|---|---|---|
| `Attack1` | `Attack1Charged` | ✔ | 0.85 | 0.15 | `ChargeQueued` = true |
| `Attack1Charged` | `Locomotion` | ✔ | 0.85 | 0.2 | — |
| `Attack1Charged` | `ArmedLocomotion` | ✔ | 0.85 | 0.25 | `IsWielded` = true |

**Sai de `Attack1`, não de `Locomotion`/`ArmedLocomotion`** — o gatilho é o
FIM do golpe leve, não um trigger cru; não existe entrada direta pro
`Attack1Charged` a partir de fora do combo. Mesmo Exit Time (0.85) e mesmo
idioma de `Attack1 -> Attack2` (`ComboQueued`) — coloque esta transição
**depois** da de `Attack2`/`ComboQueued` na lista de saídas do `Attack1`: um
toque novo (que dispara `comboQueued`, não `chargeQueued`) deve ganhar se por
algum motivo os dois estiverem marcados ao mesmo tempo (não deveria
acontecer na prática — tocar de novo não seta `chargeQueued`, só segurar
seta — mas a ordem deixa a prioridade explícita no grafo).

Sem branch de combo DEPOIS de `Attack1Charged` — é um encadeamento de dois
golpes só (leve + pesado), sem terceiro elo. Se no futuro ele ganhar
continuação própria, troca pro mesmo idioma de `ComboWindowOpen()`.

**Dodge já funciona de graça, sem transição nenhuma pra desenhar.**
`Attack1Charged` NÃO está em `IsInCommittedAttack()` — é só um golpe pesado,
sem o motivo de design que justifica o launcher/chutes serem "comprometidos"
(esperar o golpe conectar antes de poder cancelar). Ele cancela pelo mesmo
caminho de `Attack1`/`Attack2`/`Attack3`: `HandleDodge()` dispara o trigger
`Dodge` cru, e o `AnyState -> Dodge` global do Base Layer (já existe, é o
mesmo que cobre o combo leve inteiro) pega de qualquer estado, sem precisar
de uma transição de saída específica pro `Attack1Charged`.

> **Armadilha encontrada em teste (2026-08-05)**: uma versão anterior deste
> guia colocava `Attack1Charged` em `IsInCommittedAttack()`, pedindo uma
> transição `Attack1Charged -> Dodge` condicionada em `DodgeQueued`. Isso
> quebrava o cancel: com o estado marcado como "comprometido",
> `HandleDodge()` para de disparar o trigger cru e passa a só marcar a fila
> `dodgeQueued` — sem uma transição de saída pra consumir esse bool
> especificamente nesse estado, o dodge nunca saía. O conserto (já aplicado)
> foi tirar `Attack1Charged` de `IsInCommittedAttack()`.

> **Armadilha encontrada em teste (2026-08-05)**: uma primeira versão deste
> reset zerava `chargingAttack`/`chargeQueued` no MESMO bloco genérico que já
> zera `comboQueued`/`strongComboQueued`/etc. por troca de hash de estado.
> Isso travava o `Attack1` no último frame sempre que o jogador segurava o
> botão — não importava por quanto tempo. Causa: `chargingAttack` é ligado no
> MESMO frame em que o trigger `Attack` dispara, mas o Animator só termina de
> registrar a transição `Locomotion -> Attack1` no frame SEGUINTE; nesse
> frame o hash "efetivo" muda (já aponta pro Attack1 de destino) e o reset
> genérico apagava a carga um frame depois de armada, antes de qualquer
> chance de acumular tempo. O conserto (já aplicado no C#) foi tirar o reset
> da carga do bloco genérico e fazer ele reagir a **sair da família
> Attack1/Attack1Charged** (`AnimatorStateUtil.HasStateNowOrIncoming`) em vez
> de a troca de hash crua — mesma classe de cuidado que `GetEffectiveBaseStateHash`
> já existe pra evitar em outros lugares deste arquivo.

### 1.6 Antecipação visual de `IsCharging` (opcional)

`IsCharging` vira `true` assim que o piso é cruzado, ainda com o `Attack1`
tocando — dá pra usar isso pra um feedback discreto (um brilho na arma via
Layer aditiva, por exemplo) sem precisar de estado novo nenhum, já que o
golpe leve continua tocando normalmente até o fim, só emenda no
`Attack1Charged` depois. Pulei isso do escopo obrigatório porque
depende de ter um clipe de windup disponível — sem ele, o jogador só vê o
`Locomotion`/`ArmedLocomotion` parado normalmente durante a carga (que já
funciona sem trabalho nenhum de Animator, é só o Speed continuar em 0).

---

## 2. Locomoção — Run Start/End e Sprint End (com Total Input Cancel)

### O que o C# já faz

`HandleLocomotionTransitions()` (chamado todo `Update()`) dispara três
triggers **na borda**, nunca todo frame:

- `RunStart` — no frame em que o input de movimento cruza de "parado" pra
  "andando" (mesmo piso `locomotionMoveThreshold` = 0.05 que já era usado).
- `RunEnd` — no frame em que cruza de volta pra "parado".
- `SprintEnd` — no frame em que `sprinting` cai de `true` pra `false`
  (chão só).

Bloqueado (não dispara) durante ataque, dodge ou pulo — esses já têm suas
próprias entradas/saídas de locomoção.

**Total Input Cancel não precisou de bool novo nenhum.** A trava mora inteira
no Animator: `HandleAttack()`, `HandleJump()` e `HandleDodge()` já disparam
seus triggers/bools TODO FRAME sem checar o nome do estado atual — então
"qualquer input cancela instantaneamente" é simplesmente **desenhar as mesmas
transições que já saem de `Locomotion`/`ArmedLocomotion` também saindo de
`RunStart`/`RunEnd`/`SprintEnd`**, sem Exit Time. No frame em que o jogador
ataca/pula/rola durante um desses três estados, a condição correspondente já
está lá (foi setada por HandleAttack/HandleJump/HandleDodge normalmente) e o
Animator troca na hora — zero frames de lag adicional, porque não existe
nenhum bool intermediário esperando o clipe "terminar de tocar" antes de
aceitar o cancelamento.

### 2.1 Import settings

Os clipes brutos já existem no projeto (Mixamo/GreatSword_Animset, ainda não
importados na pasta que o controller usa). Copiar/exportar pra
`Assets/Art/Animations/Player/Movement/` (mesma pasta de `Run.FBX`/
`Sprint.FBX`), um par por variante de arma:

| Clipe de origem | Vira |
|---|---|
| `.../Root/Common/GreatSword_Common_Run_Start_Root.FBX` | `Run Start Greatsword.FBX` |
| `.../Root/Common/GreatSword_Common_Run_End_Root.FBX` | `Run End Greatsword.FBX` |

**Sprint End armado não existe como asset ainda** — não achei um
`Sprint_End`/`Stop` dedicado dentro do `GreatSword_Animset`. Duas saídas:
reaproveitar `GreatSword_Common_Run_End_Root` também pro `SprintEnd` (a
desaceleração de correr pra parado costuma ler bem mesmo vindo de mais
rápido), ou você me indicar um clipe específico se já tiver um separado —
sem isso, a tabela abaixo assume o reaproveitamento.

**Desarmado**: não há Run Start/End nem Sprint End no `Unarmed/` do projeto
hoje (só `Y Bot@Sprint`, `Y Bot@Idle To Sprint`, `Y Bot@Running`). Mesma
situação que o guia de Combate Aéreo já registrou pro Jump desarmado (seção 9
de lá): fica pendente até os clipes chegarem — quando chegarem, é duplicar os
três estados abaixo e trocar `IsWielded` por `!IsWielded` nas condições de
origem, igual o padrão que `Locomotion`/`ArmedLocomotion` já seguem.

Ajustes de import em todos:

| Ajuste | Valor |
|---|---|
| Loop Time | ✗ (são clipes de transição, não loop) |
| Root Transform Position (Y) → Bake Into Pose | marcado (mesmo grupo dos clipes de locomoção/pulo) |
| Root Transform Position (XZ) → Bake Into Pose | **desmarcado** — o deslocamento horizontal do embalo tem que vir do root motion do próprio clipe, é ele que dá a sensação de "ainda ganhando/perdendo velocidade" |

### 2.2 Parâmetros novos

| Parâmetro | Tipo |
|---|---|
| `RunStart` | Trigger |
| `RunEnd` | Trigger |
| `SprintEnd` | Trigger |

### 2.3 Estados novos

Filhos diretos da Base Layer, ao lado de `ArmedLocomotion`/`ArmedSprint`:

| Estado | Clipe | Tag | Speed |
|---|---|---|---|
| `RunStart` | `Run Start Greatsword` | — | 1 (ajustar por teste — ver nota) |
| `RunEnd` | `Run End Greatsword` | — | 1 |
| `SprintEnd` | `Run End Greatsword` (reaproveitado — ver 2.1) | — | 1 |

Sem tag: nenhum código depende de identificar esses estados por tag/nome —
eles são só uma ponte visual, o gate de bloqueio em
`HandleLocomotionTransitions()` já é feito em C# olhando pro *input*, não pro
estado do Animator.

**Nota de Speed**: mesma lógica do `JumpStart` no guia de Combate Aéreo — o
alvo é o clipe não parecer nem arrastado nem acelerado. Comece em 1 e ajuste
ao vivo (`combatSpeedMultiplier` não afeta esses estados de propósito — eles
não são combate).

### 2.4 Transições — entrada

| De | Para | HET | Exit | Dur | Condições |
|---|---|---|---|---|---|
| `Locomotion` | `RunStart` | ✗ | — | 0.1 | `RunStart` |
| `ArmedLocomotion` | `RunStart` | ✗ | — | 0.1 | `RunStart` |
| `Locomotion` | `RunEnd` | ✗ | — | 0.1 | `RunEnd` |
| `ArmedLocomotion` | `RunEnd` | ✗ | — | 0.1 | `RunEnd` |
| `Sprint` | `SprintEnd` | ✗ | — | 0.15 | `SprintEnd` |
| `ArmedSprint` | `SprintEnd` | ✗ | — | 0.15 | `SprintEnd` |

### 2.5 Transições — saída natural (fim do embalo)

| De | Para | HET | Exit | Dur | Condições |
|---|---|---|---|---|---|
| `RunStart` | `Locomotion` | ✔ | 0.9 | 0.15 | — |
| `RunStart` | `ArmedLocomotion` | ✔ | 0.9 | 0.15 | `IsWielded` = true |
| `RunEnd` | `Locomotion` | ✔ | 0.9 | 0.15 | — |
| `RunEnd` | `ArmedLocomotion` | ✔ | 0.9 | 0.15 | `IsWielded` = true |
| `SprintEnd` | `Locomotion` | ✔ | 0.9 | 0.15 | — |
| `SprintEnd` | `ArmedLocomotion` | ✔ | 0.9 | 0.15 | `IsWielded` = true |

Estas SIM usam Exit Time — ao contrário do combo, aqui não existe fila
esperando ("a janela abre em X% do golpe"), é literalmente "deixa o clipe
terminar" quando nada mais acontece nesse meio tempo.

### 2.6 Transições — Total Input Cancel

Esta é a parte que implementa a regra pedida. **Em PRIMEIRO** na lista de
saídas de cada um dos três estados (antes das saídas por Exit Time do passo
2.5 — condicionada satisfeita depois de Exit Time já satisfeito não é
avaliada):

| De | Para | HET | Exit | Dur | Condições |
|---|---|---|---|---|---|
| `RunStart` / `RunEnd` / `SprintEnd` | `Attack1` | ✗ | — | 0.15 | `Attack`, `IsWielded` = true |
| `RunStart` / `RunEnd` / `SprintEnd` | `JumpStart` | ✗ | — | 0.15 | `Jump` |
| `RunStart` / `RunEnd` / `SprintEnd` | `Dodge` | ✗ | — | 0.1 | `Dodge` |
| `RunStart` / `RunEnd` | `Locomotion` | ✗ | — | 0.1 | `Speed` Less 0.1 |
| `RunStart` / `RunEnd` | `Sprint` | ✗ | — | 0.1 | `Sprint` = true |
| `RunEnd` | `RunStart` | ✗ | — | 0.1 | `RunStart` (mudou de direção rápido e voltou a andar) |

9 transições no total (3 estados × 3 linhas fixas, mais as 3 de
mudar-de-ideia-no-meio) — repetitivo de desenhar, mas cada uma é curta e
direta. `SprintEnd` não precisa da variante "voltou a andar" porque
`Sprint`/`Speed` já cobrem: se o jogador voltar a sprintar no meio do
`SprintEnd`, `Sprint = true` já manda pra `Sprint` direto.

**Por que isso já é "instantâneo" sem nenhum código extra**: no frame em que
o jogador aperta Attack, `HandleAttack()` já chama `animator.SetTrigger(Attack)`
não importa o estado atual (o gate ali é `!IsAttacking() && IsWielded`, e
nenhum dos três estados novos tem tag `Attack`) — o trigger fica pendurado
esperando uma transição que o consuma. Antes desta seção, `RunStart`/`RunEnd`/
`SprintEnd` não tinham transição nenhuma pra consumir esse trigger, então ele
ficaria pendurado até sair pro Locomotion/Sprint por Exit Time (o *input lag*
que a feature pede pra eliminar). Com a transição desta seção, o mesmo
trigger que já existia dispara a troca no mesmo frame.

---

## 3. Sprint/Run Heavy Attack 1 (ataque pesado em movimento)

### O que o C# já faz

`HandleStrongAttack()`: apertar Ataque Forte enquanto `sprinting && grounded
&& !IsAttacking()` dispara o trigger `SprintHeavyAttack` (cru, não é branch
de combo) em vez de enfileirar `strongComboQueued`.

`OnAnimatorMove()`: na borda de entrada do estado `SprintHeavyAttack1`,
captura `lastGroundedSpeed` do frame anterior (a velocidade real de sprint,
não o `Speed` normalizado) em `capturedSprintMomentumSpeed`. Enquanto o
estado durar, SOMA `transform.forward * capturedSprintMomentumSpeed *
animator.GetFloat("ForwardMomentum") * Time.deltaTime` ao root motion normal
do clipe — mesmo idioma aditivo que o `Lunge` já usa pros ataques parados,
só que a magnitude vem da velocidade capturada em vez de um valor fixo, e
decai pela curva em vez de ficar constante. Rotação/passo continuam 100% root
motion do clipe (nada no C# toca `transform.rotation` durante um ataque,
sempre foi assim).

### 3.1 Escolha do clipe

Você não indicou o arquivo exato — a pasta `Strong Attack/` tem quatro
candidatos:

```
Assets/Art/Animations/Player/Battle/Strong Attack/
  Atack 1 Strong Greasword.FBX
  GreatSword_Attack03_1_Root.FBX
  GreatSword_Attack04_Root.FBX   (já usado pelo Attack1Alt2, o launcher — não reusar)
  GreatSword_SPAttack2_Root.FBX  (já vai pro plunge — ver seção 4, não reusar aqui)
```

Restam `Atack 1 Strong Greasword.FBX` e `GreatSword_Attack03_1_Root.FBX` como
candidatos livres. Recomendo `Atack 1 Strong Greasword.FBX` pelo nome (sugere
ser justamente o "ataque forte 1" dedicado) — ajuste esta seção se for outro.

### 3.2 Import settings

| Ajuste | Valor |
|---|---|
| Loop Time | ✗ |
| Root Transform Position (Y) → Bake Into Pose | desmarcado (grupo dos ataques) |
| Root Transform Position (XZ) → Bake Into Pose | desmarcado |
| Curva `ForwardMomentum` | **nova** — 1.0 no frame 0, decaindo suavemente (não linear — uma easing tipo ease-out lê melhor) até 0.0 no fim do clipe. Import Settings → Animation → Curves, mesmo lugar onde `Lunge` já é autorada nos outros clipes |
| Curva `Lunge` | opcional — se quiser um empurrão extra fixo por cima do momentum, some as duas; se não, deixe de fora e o termo fica 0 |

### 3.3 Animation Events

| Clipe | Função | Quando | Parâmetros |
|---|---|---|---|
| escolhido acima | `OnAttackHit` | frame de impacto | Float = dano (14-18, é pesado), Int = push |

### 3.4 Parâmetro novo

| Parâmetro | Tipo |
|---|---|
| `SprintHeavyAttack` | Trigger |

(`ForwardMomentum` é lido via `animator.GetFloat` — não precisa existir como
parâmetro editável no Animator, só a curva dentro do clipe já basta, mesma
regra do `Lunge`.)

### 3.5 Estado novo

| Estado | Clipe | Tag | Speed |
|---|---|---|---|
| `SprintHeavyAttack1` | (escolhido acima) | `Attack` | `CombatSpeedMultiplier` |

### 3.6 Transições

| De | Para | HET | Exit | Dur | Condições |
|---|---|---|---|---|---|
| `ArmedSprint` | `SprintHeavyAttack1` | ✗ | — | 0.1 | `SprintHeavyAttack` |
| `SprintHeavyAttack1` | `ArmedSprint` | ✔ | 0.85 | 0.2 | `Sprint` = true |
| `SprintHeavyAttack1` | `ArmedLocomotion` | ✔ | 0.85 | 0.2 | — |

**Não precisa de transição pro `Dodge`.** `SprintHeavyAttack1` NÃO está em
`IsInCommittedAttack()` (mesmo ajuste que corrigiu o `Attack1Charged` — ver a
armadilha na seção 1) — é só mais um golpe pesado, sem motivo de design pra
travar o cancel. Ele já cancela pelo `AnyState -> Dodge` global do Base
Layer, igual `Attack1`/`Attack2`/`Attack3`.

**Por que só a partir de `ArmedSprint`** (não `Sprint` desarmado): o gate em
C# já é `weaponEquip.IsWielded` implícito (checado por `IsAttacking()` etc.
como os outros golpes) — sem arma sacada `strongAttackAction.WasPressedThisFrame()`
sequer entra no `if` de `HandleStrongAttack()`. Se quiser uma versão
desarmada no futuro, é replicar o mesmo padrão de `Attack1`/`ArmedLocomotion`
com um clipe próprio.

---

## 4. Plunge Attack (Air Heavy Attack 1 & 2)

Esta é a maior peça. Ordem sugerida de wiring: layer + collider check primeiro
(passo 4.0), depois clipes, depois eventos, depois estados/transições.

### O que o C# já faz

`HandleStrongAttack()`: apertar Ataque Forte no ar (`!controller.isGrounded &&
!IsPlungeFalling()`) dispara `PlungeAttack` (trigger cru).

`OnAnimatorMove()`, na borda de entrada de `AirHeavyAttack1`
(`IsPlungeFalling()`, checado por nome de estado com semântica efetiva —
mesmo idioma dos outros ataques aéreos):

- `Physics.IgnoreLayerCollision(playerLayer, enemyLayer, true)` — desliga a
  colisão física jogador↔inimigo pelo resto da queda.
- `plungeStartImpulse?.GenerateImpulse()` — screen shake de entrada
  (`CinemachineImpulseSource`, campo serializado; sem um atribuído, o plunge
  funciona igual, só sem shake).
- Escolhe o alvo a ancorar: o alvo TRAVADO no lock-on se houver um, senão o
  `Targetable` com `KnockbackReceiver` mais próximo dentro de
  `plungeMagnetRange` (padrão 12m). `KnockbackReceiver.BeginCarry(...)` prende
  ele na posição inicial.

Enquanto `AirHeavyAttack1` toca:

- `verticalVelocity` é CRAVADO em `-plungeFallSpeed` todo frame (padrão 28) —
  não integra gravidade nenhuma, é constante desde o primeiro frame.
- O alvo ancorado segue `weaponEquip.WieldSocket.position` todo frame
  (`UpdateCarryPosition`).
- Uma esfera (`plungeImpactRadius`/`plungeImpactHeight`, layer
  `plungeImpactMask`) sob o player pega mobs SECUNDÁRIOS (qualquer
  `KnockbackReceiver` que não seja o alvo ancorado) e chama
  `ForceDescend(plungeFallSpeed)` neles — mesma velocidade da queda, todo
  frame que estiverem dentro da caixa (autolimpa sozinho se saírem, ver o
  comentário do campo `forcedDescentActive` em `KnockbackReceiver`).
- Horizontal fica zerado (`rootMotionPosition = Vector3.zero`) — a queda é
  reta pra baixo, o player não persegue nada no XZ.

Na saída de `AirHeavyAttack1` (pouso normal OU cancelamento por dash aéreo no
meio — ver `HandleDodge`, um dash cancela qualquer ataque aéreo em
andamento): religa a colisão e solta o alvo ancorado.

`OnPlungeImpact` (Animation Event no clipe de `AirHeavyAttack2`, ver 4.3):
aplica dano radial 360° (reaproveita `ApplyHit`, mesmo idioma do
`OnAttackHitRadial`) e dispara `plungeImpactImpulse` (shake maior, campo
separado do de entrada). Serve de cinto de segurança redundante pra
religar colisão/soltar carry, caso o bloco de saída acima não tenha rodado
por algum motivo — na prática, na maioria das vezes ele já roda depois que
isso já foi feito no pouso.

### 4.0 Layers e Inspector

1. Confirmar que a layer `Enemy` já existe (ela existe — usada por
   `PlayerAttackHitbox.enemyLayer` e pelo `PushTestDummy`). O campo
   `enemyLayerName` do `PlayerController` já vem com o default `"Enemy"`.
2. `plungeImpactMask`, no Inspector do `PlayerController`: marcar a layer
   `Enemy` (mesma mask que `PlayerAttackHitbox.enemyLayer` já usa).
3. `plungeStartImpulse`/`plungeImpactImpulse`: dois `CinemachineImpulseSource`
   — pode ser o MESMO GameObject dos dois (um componente aceita várias
   chamadas de `GenerateImpulse()`, cada uma com a própria força se você
   configurar `DefaultVelocity` diferente por Impulse Definition, ou crie dois
   componentes separados se quiser amplitudes bem diferentes entre entrada e
   impacto). Precisa de um `CinemachineImpulseListener` na vcam ativa pra
   sentir o efeito — se a câmera do projeto ainda não tem um, adicionar antes
   de testar, senão o shake é gerado mas ninguém "escuta".

### 4.1 Escolha e corte dos clipes

Você indicou os dois arquivos-fonte:

```
Assets/Art/Animations/Player/Battle/Light Attack/Ground/GreatSword_SPAttack2_Root.FBX
Assets/Art/Animations/Player/Battle/Strong Attack/GreatSword_SPAttack2_Root.FBX
```

São dois FBX DIFERENTES com o mesmo nome (pastas diferentes) — confirme qual é
qual antes de recortar (abra os dois no Inspector e compare a prévia; o da
pasta `Light Attack/Ground` tende a ser o golpe mais curto/rápido, o de
`Strong Attack` o mais longo/pesado, mas confirme visualmente).

Sugestão de divisão, já que você descreveu "AirHeavyAttack1 (Queda)" como um
único golpe cortado dos dois arquivos:

| Estado | Fonte | Trecho (`firstFrame`/`lastFrame` no import) |
|---|---|---|
| `AirHeavyAttack1` | Um dos dois — o de windup + queda | do início até o frame ANTES do impacto visual no chão |
| `AirHeavyAttack2` | O outro (ou o mesmo, continuando) | do frame do impacto até o fim (recuperação) |

Se os dois arquivos forem, na prática, o MESMO clipe de origem (uma captura
única de "erguer a espada, cair, cravar"), a divisão mais simples é: um único
FBX, dois **Take**s diferentes recortados em Import Settings (`firstFrame`/
`lastFrame` distintos), cada um virando um clipe interno separado — mesma
técnica que o Unity já usa nativamente pra outros multi-clipe do projeto.
Ajuste o corte final assistindo o preview: o ponto de corte entre 1 e 2 é
onde o pé/espada visualmente toca o chão.

### 4.2 Import settings

| Ajuste | AirHeavyAttack1 (queda) | AirHeavyAttack2 (impacto) |
|---|---|---|
| Loop Time | ✗ | ✗ |
| Root Transform Position (Y) → Bake Into Pose | **desmarcado** (o vertical é 100% física, `verticalVelocity` cravado — se ficar marcado, a pose e a física brigam) | marcado (impacto acontece no chão, pose normal) |
| Root Transform Position (XZ) → Bake Into Pose | desmarcado | desmarcado (grupo dos ataques) |

### 4.3 Animation Events

| Clipe | Função | Quando | Parâmetros |
|---|---|---|---|
| `AirHeavyAttack2` (fonte) | `OnPlungeImpact` | frame em que a espada/pé toca o chão | Float = dano (18-25, é o golpe mais pesado do kit), Int = push |

Cai no GameObject do Animator (mesma regra sempre). **Não usar
`OnAttackHit`/`OnAttackHitRadial` aqui** — precisa ser especificamente
`OnPlungeImpact`, é o único que dispara o evento C# que religa a colisão e
solta o carry (ver o comentário em `PlayerAttackHitbox.OnPlungeImpact`).

### 4.4 Parâmetro novo

| Parâmetro | Tipo |
|---|---|
| `PlungeAttack` | Trigger |

### 4.5 Estados novos

| Estado | Clipe | Tag | Speed |
|---|---|---|---|
| `AirHeavyAttack1` | (queda, ver 4.1) | `Attack` | `CombatSpeedMultiplier` |
| `AirHeavyAttack2` | (impacto, ver 4.1) | `Attack` | `CombatSpeedMultiplier` |

### 4.6 Transições

| De | Para | HET | Exit | Dur | Condições |
|---|---|---|---|---|---|
| `AirLoop` | `AirHeavyAttack1` | ✗ | — | 0.15 | `PlungeAttack` |
| `JumpStart` | `AirHeavyAttack1` | ✗ | — | 0.15 | `PlungeAttack`, `IsJumping` = true |
| `AirAttack1` | `AirHeavyAttack1` | ✗ | — | 0.1 | `PlungeAttack` |
| `AirAttack2` | `AirHeavyAttack1` | ✗ | — | 0.1 | `PlungeAttack` |
| `AirHeavyAttack1` | `AirHeavyAttack2` | ✗ | — | 0.05 | `IsJumping` = false |
| `AirHeavyAttack2` | `Attack1` | ✗ | — | 0.15 | `Attack`, `IsWielded` = true (land-cancel, mesmo idioma da seção 5.7 do guia de Combate Aéreo) |
| `AirHeavyAttack2` | `ArmedLocomotion` | ✔ | 0.8 | 0.2 | — |

**`IsJumping` = true na entrada por `JumpStart`**, mesmo motivo documentado
no guia de Combate Aéreo pro `AirAttack1`: sem essa condição, disparar o
plunge nos primeiros frames do `JumpStart` trocaria de estado antes do
`OnJumpTakeOff` aplicar o impulso do pulo — o personagem cairia parado no
chão pensando que está no ar.

**`IsJumping` = false manda o pouso ganhar de qualquer coisa** — mesma regra
já usada em `AirAttack1 -> JumpEnd`/`AirAttack2 -> JumpEnd`: o plunge não tem
"combo" pra proteger, então essa é a única saída de `AirHeavyAttack1`, sem
concorrência de ordem.

**Não existe transição pro `JumpEnd` normal** — o plunge sempre passa pelo
`AirHeavyAttack2` de impacto, que já FAZ o papel do pouso (é onde o dano
acontece). Ele não devolve pro `JumpStart`/`AirLoop`; a única saída dele é
pra locomoção de chão.

**Dash aéreo cancela o plunge de graça** (`AnyState -> Dodge` já existe,
documentado na seção 5.6 do guia de Combate Aéreo) — não precisa de
transição nova pra isso funcionar; o C# já cobre a limpeza de collision/carry
nesse caso (ver "O que o C# já faz" acima). Só falta a saída correspondente
pro dash não voltar pra um estado de chão flutuando, que **já existe** (a
`Dodge -> AirLoop` da seção 5.6).

---

## 5. Checklist de verificação

- [ ] **Ataque carregado**: toque rápido dispara só `Attack1`, sem
      continuação. Segurar o botão por ~0.35s ou mais enquanto `Attack1`
      toca encadeia automaticamente pro `Attack1Charged` no fim do golpe
      leve, sem precisar soltar e apertar de novo. Rolar pra fora do
      `Attack1` no meio da carga cancela a continuação (sem golpe pesado
      fora de hora depois).
- [ ] **Run Start**: sair do parado mostra o clipe de embalo antes de cair na
      corrida normal. Mudar de ideia (parar de novo) no meio corta pro
      `RunEnd`/`Locomotion` na hora, sem lag.
- [ ] **Run End**: parar de andar mostra o clipe de freada antes do idle.
      Atacar/pular/rolar durante ele interrompe no MESMO frame do input —
      cronometrar com o combo de chão pra comparar a responsividade.
- [ ] **Sprint End**: soltar o sprint (chão) mostra a desaceleração antes de
      cair em corrida normal ou idle.
- [ ] **Sprint Heavy Attack**: apertar Forte durante o sprint mantém a
      velocidade no início do golpe e desacelera suavemente até o fim — sem
      um "freio" abrupto nem um "deslize" constante até o fim do clipe.
      Testar `ForwardMomentum` em Play Mode direto no clipe (Animation
      Window) se a curva não estiver batendo com o esperado.
- [ ] **Plunge — entrada**: apertar Forte no ar (`AirLoop`/`JumpStart`/
      `AirAttack1`/`AirAttack2`) dispara a queda. Screen shake na entrada (se
      um `CinemachineImpulseSource` estiver atribuído).
- [ ] **Plunge — queda**: velocidade vertical constante e rápida, sem
      aceleração perceptível. Atravessa o inimigo alvo sem ser barrado
      fisicamente (colisão desligada).
- [ ] **Plunge — magnet**: o alvo travado (ou o mais próximo, sem lock-on)
      fica visualmente preso na espada durante toda a queda.
- [ ] **Plunge — carrier box**: outro inimigo dentro do raio de impacto
      desce junto, na mesma velocidade, sem ficar pra trás.
- [ ] **Plunge — impacto**: dano em área 360° no pouso, screen shake maior
      que o de entrada, colisão jogador-inimigo volta ao normal, alvo
      ancorado é solto.
- [ ] **Plunge — cancelamento**: dash aéreo no meio da queda cancela o
      plunge, religa a colisão e solta o carry mesmo sem o impacto ter
      tocado.
- [ ] Nenhum parâmetro/estado órfão sobrando depois — todos os novos
      (`ChargeQueued`, `IsCharging`, `RunStart`, `RunEnd`, `SprintEnd`,
      `SprintHeavyAttack`, `PlungeAttack`) precisam ter pelo menos uma
      transição lendo E uma escrevendo (o C# já escreve todos; conferir só
      as transições).
