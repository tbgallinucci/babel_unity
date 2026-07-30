# Log — Sprint, Dash e Slide Attack

## Contexto

Sessão de continuação depois do sistema Sheathed/Wield da GreatSword (ver
[Guide - Disarmed and Weapon Animation.md](./Guide%20-%20Disarmed%20and%20Weapon%20Animation.md)),
que já estava concluído e verificado no início desta sessão. O objetivo aqui
foi adicionar dash/sprint (estilo NieR: Shift dispara um dash que emenda num
sprint sustentado) e um ataque de transição do sprint pro idle
(`SlideAttack`), cobrindo tanto o personagem desarmado quanto empunhando a
GreatSword — reaproveitando a arquitetura de layer mascarada (`UpperBody`)
já existente pro Draw/Sheath.

## Animações novas importadas

- `Y Bot@Idle To Sprint.fbx` (`Assets/Art/Animations/Unarmed/`) — o dash.
  Root motion baked out (Bake Into Pose em Root Transform Position XZ/Y e
  Rotation), Loop Time off — o avanço é 100% forçado via código, não vem da
  animação.
- `Y Bot@Sprint.fbx` (mesma pasta) — o sprint sustentado, looping, root
  motion natural (não baked out) — a translação vem do próprio clipe, igual
  `Locomotion`.
- `Y Bot@Great Sword Slide Attack.fbx`
  (`Assets/Art/Animations/Greatsword/`) — ataque que interrompe o
  dash/sprint. Root motion natural também (mesma categoria do Sprint), Loop
  Time off.

## Arquitetura do Animator

### Layer base — estados novos

- `DashToSprint` (Motion = `Idle To Sprint`, tag `Dashing`) e `Sprint`
  (Motion = `Sprint`, looping) — **compartilhados** entre desarmado e
  armado. O corpo/pernas fazem o mesmo movimento nos dois casos; só o que
  toca na `UpperBody` muda. Isso evitou duplicar clipe ou estado (diferente
  do padrão `Locomotion`/`ArmedLocomotion` e do antigo `Jump`/`ArmedJump`).
- `SlideAttack` (Motion = `Great Sword Slide Attack`, tag `Attack` — reusa a
  tag existente de propósito, pra `IsAttacking()`/gate de combo funcionarem
  sem mudança de código).
- **`ArmedJump` foi removido.** `Jump` (o clipe desarmado) virou o único
  estado de pulo, compartilhado — mesma lógica do Dash/Sprint. O visual
  armado ruim do antigo `ArmedJump` foi trocado por reaproveitar `Jump` no
  corpo/pernas com uma pose de segurar a espada por cima, via `UpperBody`
  (ver abaixo).

### Layer base — parâmetro novo e roteamento

Novo parâmetro **`IsWielded`** (Bool), sincronizado em
`WeaponEquipController.Update()` a partir da property `IsWielded` (C#) todo
frame. É o que permite os estados compartilhados (`DashToSprint`, `Sprint`,
`Jump`) decidirem pra qual locomoção voltar:

| De | Para | Condição |
|---|---|---|
| `DashToSprint`/`Sprint` | `ArmedLocomotion` | `Sprint==false` AND `IsWielded==true` |
| `DashToSprint`/`Sprint` | `Locomotion` | `Sprint==false` AND `IsWielded==false` |
| `Jump` | `ArmedLocomotion` | `Sprint==false` AND `IsWielded==true` |
| `Jump` | `Locomotion` | `Sprint==false` AND `IsWielded==false` |
| `Jump` | `Sprint` | `Sprint==true` (pousa direto no sprint se ainda tiver segurado, sem repetir o dash) |
| `Locomotion`/`ArmedLocomotion`/`DashToSprint`/`Sprint` | `DashToSprint`/`Jump` | `Sprint`/`Jump` conforme o caso, sem IsWielded (destino é o mesmo pros dois) |
| `DashToSprint`/`Sprint` | `SlideAttack` | `Attack` (trigger) AND `IsWielded==true` |

### Layer `UpperBody` — estados novos

- `ArmedSprintGrip` (Motion = `great sword run (2).fbx`, reaproveitado —
  nenhum clipe novo) — sobrepõe braços/mãos com a pose de segurar a espada
  enquanto o corpo faz `DashToSprint`/`Sprint`.
  - Entrada: `Empty → ArmedSprintGrip`, condição `Sprint==true` AND
    `IsWielded==true` AND **`IsAttacking==false`** (parâmetro novo, ver
    abaixo — sem isso, o estado ficava saindo e voltando durante o
    SlideAttack).
  - Saída: `ArmedSprintGrip → Empty`, condição `Sprint==false`.
  - `ArmedSprintGrip → GreatSwordSheath2` (condição `Sheath`) — permite
    embainhar sem interromper o sprint em andamento.
  - `ArmedSprintGrip → ArmedJumpGrip` (condição `Jump`) — transição
    **direta**, não passa por `Empty`. Necessário porque encadear dois hops
    no mesmo frame (`ArmedSprintGrip → Empty → ArmedJumpGrip`) não é
    confiável no Mecanim.
- `ArmedJumpGrip` (Motion = `great sword idle.fbx`, reaproveitado) —
  sobrepõe braços enquanto o corpo pula com o `Jump` desarmado.
  - Entrada: `Empty → ArmedJumpGrip`, condição `Jump` AND `IsWielded==true`.
  - Entrada alternativa: `ArmedSprintGrip → ArmedJumpGrip` (acima).
  - Saída: Has Exit Time On (~0.9–1.0), sem condição.

Novo parâmetro **`IsAttacking`** (Bool), sincronizado em
`PlayerController.HandleAttack()` a partir do `IsAttacking()` já existente
(tag `Attack` na layer base) — só existe pra a `UpperBody` saber não
reentrar em `ArmedSprintGrip` enquanto o `SlideAttack` está tocando.

## Código (`PlayerController.cs`)

- `HandleSprint()`: `Sprint` virou **toggle** (clique liga, clique de novo
  desliga), não held — usa `sprintAction.WasPressedThisFrame()` invertendo
  um bool interno (`sprinting`), em vez de espelhar `IsPressed()`.
- `OnAnimatorMove()` ganhou três ramos especiais além do root motion normal:
  - **Dash** (`IsTag("Dashing")`): movimento 100% forçado
    (`dashSpeed`/`dashRampInTime`, ramp-in baseado no tempo normalizado do
    estado).
  - **SlideAttack** (`IsName("SlideAttack")` — não dá pra usar tag, já que a
    tag é `Attack`): movimento forçado só na janela inicial do clipe
    (`slideAttackActiveEnd`), com ramp-in e ramp-out
    (`slideAttackRampInTime`/`slideAttackRampOutTime`) pra não cortar seco.
  - **Sprint Jump Boost**: no ramo de root motion normal, se `Sprint==true`
    e o estado for `Jump`, soma um empurrão extra (`sprintJumpBoost`) por
    cima do root motion natural do pulo (que continua controlando o arco
    vertical) — pulo alcança mais distância durante o sprint.
- **Trigger `Attack` cancela o sprint** (`pendingSprintCancel`, atrasado um
  frame de propósito): sem isso, terminar o `SlideAttack` com o toggle de
  sprint ainda ligado fazia a layer base e a `UpperBody` retomarem o sprint
  sozinhas em vez de assentar no idle armado. O atraso de um frame evita uma
  corrida onde `Sprint==false` e o trigger `Attack` ficam true ao mesmo
  tempo, e `Sprint→ArmedLocomotion` (que também fica satisfeita nesse
  instante) rouba a prioridade de `Sprint→SlideAttack` dependendo da ordem
  das transições na lista.

## Código (`WeaponEquipController.cs`)

- Novo parâmetro sincronizado: `IsWielded` (ver acima).
- **Removido** o bloqueio antigo de Draw/Sheath durante dash/sprint
  (`IsSprintLocked`) — substituído por uma limpeza automática do trigger:
  `TriggerDraw()`/`TriggerSheath()` marcam `pendingDrawReset`/
  `pendingSheathReset`, e `Update()` chama `ResetTrigger` um frame depois de
  disparar. Isso permite sacar/embainhar **sem interromper** o sprint (a
  `UpperBody` reage ao trigger normalmente, independente da layer base),
  mas garante que o trigger nunca fica pendurado esperando a layer base
  voltar pra `Locomotion`/`ArmedLocomotion` (o que causaria um disparo
  atrasado e imprevisível).

## Input

- Bind de gamepad `<Gamepad>/leftShoulder` (L1/LB) adicionado à action
  `Equip` existente, grupo `Gamepad`, em `InputSystem_Actions.inputactions`.
  Mesmo toggle de sempre (saca se guardada, guarda se sacada) — não precisou
  de nenhuma mudança no `WeaponEquipController`.

## Bugs encontrados e corrigidos nesta sessão

Praticamente todos caíram em duas categorias — vale registrar o padrão pra
não repetir a investigação da próxima vez:

1. **Trigger pendurado entre layers**: qualquer trigger (`Draw`/`Sheath`/
   `Attack`/`Jump`) que não tem transição pra consumir na layer base a
   partir do estado atual fica pendente e dispara atrasado quando a layer
   base finalmente chega num estado que aceita ele. Resolvido caso a caso:
   adicionando a transição que faltava (Attack/Jump saindo de
   `DashToSprint`/`Sprint`), ou com reset manual atrasado um frame (Draw/
   Sheath).
2. **Corrida de um frame entre C# e Animator**: setar um parâmetro Bool no
   mesmo frame que dispara um Trigger relacionado pode fazer o Animator ver
   os dois ao mesmo tempo e escolher a transição errada entre duas que
   ficam simultaneamente satisfeitas (ordem da lista decide). Resolvido
   atrasando a mudança do bool um frame (`pendingSprintCancel`).
3. **Condição de transição com referência de parâmetro quebrada** ("Parameter
   does not exist in Controller") — aconteceu pelo menos duas vezes ao criar
   transições novas no Editor. Diagnóstico: selecionar a transição e olhar o
   Inspector por esse aviso.
4. **Encadeamento de dois hops no mesmo frame não é confiável** — descoberto
   tentando `ArmedSprintGrip → Empty → ArmedJumpGrip` pro pulo durante o
   sprint. Resolvido com uma transição direta (`ArmedSprintGrip →
   ArmedJumpGrip`) em vez de depender do Mecanim encadear duas transições
   automaticamente.

## Pendente / observações pra próxima sessão

- **Sensação de responsividade**: o personagem está mais "comprometido"
  (Monster Hunter/Souls) do que "ágil" (NieR) — a maioria das transições
  novas desta sessão usa Has Exit Time, que só libera o próximo input perto
  do fim do clipe atual. Se quiser mais responsividade, o próximo passo é
  abrir janelas de cancelamento específicas (ex.: Attack interrompendo o
  Slide Attack mais cedo) em vez de mexer só em números de blend/rotação.
- Versão armada do sistema de dash/sprint/slide attack está funcional e
  testada (parado, andando, sprintando, pulando — nos dois sentidos
  desarmado↔armado).

## Arquivos tocados

- `Assets/Scripts/Player/PlayerController.cs`
- `Assets/Scripts/Equipment/WeaponEquipController.cs`
- `Assets/InputSystem_Actions.inputactions` (bind de gamepad em `Equip`)
- `Assets/Art/Animations/PlayerAnimatorController.controller` (layers Base e
  `UpperBody`)
- `Assets/Art/Animations/Unarmed/Y Bot@Idle To Sprint.fbx`
- `Assets/Art/Animations/Unarmed/Y Bot@Sprint.fbx`
- `Assets/Art/Animations/Greatsword/Y Bot@Great Sword Slide Attack.fbx`
