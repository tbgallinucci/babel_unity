# Guia — Dodge Roll, Ataque Forte, Lock-on e Habilidades

## Contexto

Continuação do sistema Sheathed/Wield
([Guide - Disarmed and Weapon Animation.md](./Guide%20-%20Disarmed%20and%20Weapon%20Animation.md))
e do Sprint/Dash/Slide Attack
([Log - Sprint, Dash e Slide Attack.md](./Log%20-%20Sprint%2C%20Dash%20e%20Slide%20Attack.md)).
Esta passada fecha o **core de animação de combate do player**: dodge roll
(só pra frente, root motion natural do próprio clipe — decisão revista
depois da primeira versão, ver nota no Passo 1), ataque forte, lock-on
estilo Nier (R3 + analógico direito) e 2 habilidades (L2+Triângulo = Cura,
L2+Quadrado = Magia de Ataque).

Decisões de escopo já confirmadas (não repetir a discussão):
- **I-frames**: só um stub (`PlayerController.IsDodgeInvulnerable`), sem
  sistema de dano pra consumir ainda.
- **Sem menu radial visual** — L2+botão dispara a animação direto.
- **Lock-on validado com dummies de teste** (`Targetable`), sem inimigo real.
- **Habilidades só disparam animação**, sem cura/dano funcional.

Todo o código (`PlayerController.cs`, `Targetable.cs`, `TargetingSystem.cs`,
`CameraLockOnController.cs`) e o `InputSystem_Actions.inputactions` **já
foram editados** — o que falta é 100% trabalho de Editor (Animator
Controller, import settings, cena), documentado passo a passo abaixo.

## Input (já aplicado em `InputSystem_Actions.inputactions`)

| Action                | Gamepad                                                   | KBM                                 | Observação                                                                        |
| --------------------- | --------------------------------------------------------- | ----------------------------------- | --------------------------------------------------------------------------------- |
| `Dodge`               | `buttonEast` (Círculo)                                    | `leftCtrl`                          | era `Crouch` (sem consumidor), renomeada                                          |
| `StrongAttack`        | `buttonNorth` (Triângulo)                                 | `rightButton` (mouse)               | `Interact` continua no mesmo botão, sem consumidor ainda — sem conflito real hoje |
| `LockOn`              | `rightStickPress` (R3)                                    | `Tab`                               | toggle                                                                            |
| `Heal`                | composite **One Modifier**: `leftTrigger` + `buttonNorth` | tecla `3` (direta, sem modificador) |                                                                                   |
| `AttackMagic`         | composite **One Modifier**: `leftTrigger` + `buttonWest`  | tecla `4` (direta)                  |                                                                                   |
| `AbilityModifierHeld` | `leftTrigger` (Value/Axis)                                | —                                   | só pra código checar `>= threshold`, gate anti-double-fire                        |

Nada a fazer aqui — é só conferir no Project Settings/Input Actions editor
que o asset reimportou certo depois do reload do Unity.

## Passo 1 — Import settings do `Standing Dodge Forward.fbx`

Arquivo: `Assets/Art/Animations/Greatsword/Standing Dodge Forward.fbx`.

> **Revisão**: a primeira versão deste guia mandava fazer Bake Into Pose
> (root motion baked out) + deslocamento forçado por código, com a
> velocidade do roll herdada de `lastGroundedSpeed` (igual o Jump). Decisão
> revista: o roll agora usa o **root motion natural do próprio clipe**, sem
> nenhuma força/escala por código — mesmo tratamento de import da Sprint
> (`Log - Sprint, Dash e Slide Attack.md`). Isso significa **distância/
> velocidade fixas** (o que o Mixamo autorou no clipe), iguais parado,
> andando, correndo ou sprintando — a ideia de "herdar velocidade da
> locomoção" foi abandonada de propósito pro dodge (o Jump continua com
> velocidade herdada normalmente, isso não mudou).

1. Selecionar o asset, aba **Rig**: confirmar Animation Type = Humanoid,
   Avatar Definition = "Copy From Other Avatar" apontando pro Avatar já
   usado pelos outros clipes da GreatSword.
2. Aba **Animation**, no clipe: desmarcar **Loop Time** (é um roll de
   disparo único, não um ciclo); **NÃO marcar Bake Into Pose** — deixar
   Root Transform Position e Rotation como vieram do Mixamo, pra a
   translação vir inteira do próprio clipe via `animator.deltaPosition`
   (mesmo branch de chão normal em `PlayerController.OnAnimatorMove()`, sem
   nenhum branch dedicado ao Dodge).
3. Apply.

## Passo 2 — `PlayerAnimatorController.controller`: parâmetros novos

Adicionar (aba Parameters):
- `Dodge` (Trigger)
- `StrongAttack` (Trigger)
- `Heal` (Trigger)
- `AttackMagic` (Trigger)

(`IsWielded`, `IsAttacking`, `Sprint`, `ComboQueued` já existem — não mexer.)

## Passo 3 — Estado `Dodge` (layer base, Layer 0)

1. Criar estado **`Dodge`**, Motion = clipe `Standing Dodge Forward`, tag
   `"Dodging"` (campo **Tag** no Inspector do estado — precisa bater
   exatamente com a string lida em `PlayerController.OnAnimatorMove()`).
2. Transições de entrada (Has Exit Time **off**, condição `Dodge`):
   - `Locomotion` → `Dodge` OK
   - `ArmedLocomotion` → `Dodge` OK
   - `DashToSprint` → `Dodge` OK
   - `Sprint` → `Dodge` OK
   - `Attack1` → `Dodge` ok
   - `Attack2` → `Dodge` ok
   - `Attack3` → `Dodge` OK
   - `StrongAttack` → `Dodge`
   - `SlideAttack` → `Dodge` OK

   As últimas 5 (a partir dos estados de ataque) são **dodge-cancel** —
   decisão explícita pra deixar o combate mais ágil/Nier em vez de
   "comprometido" tipo Souls (era exatamente o próximo passo sugerido no
   `Log - Sprint, Dash e Slide Attack.md`, seção "Pendente"). Sem elas, o
   trigger `Dodge` disparado em `HandleDodge()` durante um ataque fica
   pendurado sem transição pra consumir (mesmo problema de "trigger
   pendurado entre layers" já catalogado nesse log) — `HandleDodge()` não
   tem mais gate de `IsAttacking()` de propósito, então **essas transições
   não são opcionais**, sem elas o dodge simplesmente não funciona durante
   um ataque. Não precisa mexer na layer `UpperBody`: durante todos esses 5
   estados ela já está em `Empty` (ver comentário sobre `IsAttacking` no
   `Log - Sprint, Dash e Slide Attack.md`), então a transição `Empty` →
   `ArmedDodgeGrip` do Passo 4 já cobre o cancel também.
3. Transições de saída (Has Exit Time **on**, ~0.9–1.0):
   - `Dodge` → `Sprint` — condição `Sprint == true`. **Precisa ficar
     PRIMEIRO na lista** (Mecanim avalia em ordem e usa a primeira que
     bater) — sem isso, rolar enquanto sprintando volta pro
     `ArmedLocomotion`/`Locomotion` com `Sprint` ainda `true`, o que
     imediatamente re-satisfaz `Locomotion/ArmedLocomotion → DashToSprint`
     e repete o dash inteiro. Mesmo padrão que `Jump → Sprint` já usa pra
     pousar direto no sprint sem repetir o dash.
   - `Dodge` → `ArmedLocomotion` — condição `IsWielded == true`
   - `Dodge` → `Locomotion` — condição `IsWielded == false`

(Mesmo par de destinos que `Jump`/`Sprint`/`DashToSprint` já usam pra
decidir pra onde voltar conforme `IsWielded`, mais o atalho do `Sprint` que
o `Jump` também usa.)

> **Adição**: `WeaponEquipController.cs` ganhou `IsDodging()` (espelhando o
> `IsJumping()` que já existia) — bloqueia Draw/Sheath enquanto a tag
> `"Dodging"` ou o estado `"ArmedDodgeGrip"` estiverem ativos, mesmo
> racional: sem transição de `ArmedDodgeGrip` pra `GreatSwordDraw*`/
> `GreatSwordSheath*`, um trigger disparado no meio do roll ficaria
> pendurado. Os nomes default já batem com o que os Passos 3-4 mandam
> criar — nenhuma wiring extra necessária.

## Passo 4 — Estado `ArmedDodgeGrip` (layer `UpperBody`)

Reaproveita o clipe `great sword idle.fbx` (mesmo de `ArmedJumpGrip` — não
precisa importar nada novo).

1. Criar estado **`ArmedDodgeGrip`** na layer `UpperBody`, Motion = `great
   sword idle.fbx`. OK
2. Transições de entrada, Has Exit Time off:
   - `Empty` → `ArmedDodgeGrip`, condição `Dodge` AND `IsWielded == true`. OK
   - `ArmedSprintGrip` → `ArmedDodgeGrip`, condição `Dodge` — **direta**, sem
     passar por `Empty` (mesma razão documentada no log do Sprint/Dash:
     encadear 2 hops no mesmo frame não é confiável no Mecanim). OK
   - `ArmedJumpGrip` → `ArmedDodgeGrip`, condição `Dodge` — direta, mesma
     razão. OK
1. Saída: Has Exit Time on (~0.9–1.0), sem condição, volta pra `Empty`. OK

## Passo 5 — Estado `StrongAttack` (layer base)

1. Verificar visualmente `great sword power up.fbx`
   (`Assets/Art/Animations/Greatsword/`) — o nome sugere "carregar"/windup;
   confirmar se é o swing completo ou só a antecipação. Se for só windup,
   pode precisar splitar num 2º clipe de swing (mesma situação que o
   `GreatSwordDraw` virou 2 clipes em sequência) — decisão visual, não dá
   pra resolver só lendo o arquivo.
2. Criar estado **`StrongAttack`**, Motion = o(s) clipe(s) confirmado(s)
   acima, tag `"Attack"` (reusa a tag existente de propósito — é isso que
   faz `IsAttacking()`/gate de combo funcionarem sem mudança de código,
   mesmo padrão do `SlideAttack`).
3. Entrada: `ArmedLocomotion` → `StrongAttack`, condição `StrongAttack`
   (trigger), Has Exit Time off. **Só a partir de `ArmedLocomotion`** — sem
   entrada a partir do combo (`Attack1/2/3`) nem de `Locomotion` desarmado
   (código já garante isso via `weaponEquip.IsWielded`, mas a única
   transição de entrada no Animator reforça estruturalmente).
4. Saída: Has Exit Time on, volta pra `ArmedLocomotion`.

## Passo 6 — Estados `Heal` e `AttackMagic` (layer base)

Full-body, sem overlay de `UpperBody` — o corpo inteiro faz o gesto de
conjurar, sem locomoção por baixo (mesma ideia do `AttackShot` full-filter
do projeto Godot de referência, mas aqui já é layer base mesmo).

1. `AttackMagic`: Motion = `spell cast.fbx` (já importado em
   `Assets/Art/Animations/Greatsword/`), tag `"Ability"`.
2. `Heal`: **lacuna de asset** — não existe clipe de cura no projeto hoje.
   Opção rápida pra não travar o wiring: reaproveitar `spell cast.fbx`
   temporariamente (mesma pose nos dois, sem diferenciação visual até
   chegar um clipe próprio) ou sourcing de um clipe Mixamo tipo "Standing 2H
   Magic Attack 04"/gesto de invocar. Tag `"Ability"` também.
3. Entrada de cada um, de `ArmedLocomotion` **e** `Locomotion` (funciona
   armado e desarmado), condição = trigger correspondente (`Heal` /
   `AttackMagic`), Has Exit Time off.
4. Saída de cada um: Has Exit Time on, dois destinos conforme `IsWielded`
   (`→ ArmedLocomotion` se true, `→ Locomotion` se false) — mesmo padrão do
   `Dodge`/`Jump`.

## Passo 7 — Cena: `TargetingSystem` + `CameraLockOnController` no Player

No GameObject do Player (mesmo que já tem `PlayerController` e
`WeaponEquipController`):

1. Adicionar componente `TargetingSystem` (`Babel.Combat`). Campo **Input
   Actions** → mesmo asset `InputSystem_Actions` já usado no
   `PlayerController`. `actionMapName` fica no default `"Player"`.
2. Adicionar componente `CameraLockOnController` (`Babel.Combat`) — vai
   pedir os campos do passo 8 depois de criados.

## Passo 8 — Cena: `LockOnCamera` (Cinemachine)

O rig atual é a `CinemachineCamera` "FreeLook Camera" (Orbital Follow +
Rotation Composer). Em vez de mexer nela, criar uma **segunda vcam**
dedicada ao lock-on:

1. `GameObject > Cinemachine > Camera` → renomear pra `LockOnCamera`.
2. Adicionar um `CinemachineTargetGroup` (novo GameObject ou no mesmo
   `LockOnCamera`) com **2 membros**:
   - Membro 0: Player (Transform), weight 1, radius ~1.
   - Membro 1: deixar vazio por enquanto — `CameraLockOnController` adiciona
     e troca esse membro em runtime via `AddMember`/`RemoveMember`.
3. Na `LockOnCamera`: **Tracking Target** = o `CinemachineTargetGroup`
   acima (Cinemachine ajusta automaticamente o enquadramento pros membros
   do grupo). Configurar Lens/Composer a gosto pra enquadrar bem
   player+alvo.
4. **Priority** da `LockOnCamera` deve ficar **maior** que a da FreeLook
   Camera (ex.: FreeLook = 10, LockOnCamera = 20) — o script só ativa/
   desativa o componente (`enabled`), a prioridade relativa decide quem
   ganha quando as duas estão habilitadas.
5. Deixar a `LockOnCamera` **desabilitada** por padrão na cena (o script
   ativa via código; deixar habilitada desde o início faria ela ganhar da
   FreeLook assim que a prioridade for maior, mesmo sem lock).
6. Voltar no `CameraLockOnController` do Player e arrastar: **Lock On
   Camera** → o GameObject `LockOnCamera`, **Target Group** → o
   `CinemachineTargetGroup` criado.

## Passo 9 — Cena: dummies `Targetable` de teste

Criar 2-3 GameObjects simples (cápsula primitiva serve) espalhados ao redor
do player, cada um com o componente `Targetable` (`Babel.Combat`) anexado.
Não precisam de collider nem de nenhum outro componente — `TargetingSystem`
usa `FindObjectsByType<Targetable>()`, não física. Nomear algo como
"LockOnDummy1/2/3" pra ficar claro que são placeholders.

## Arquivos tocados

- `Assets/Scripts/Player/PlayerController.cs` (editado)
- `Assets/Scripts/Combat/Targetable.cs` (novo)
- `Assets/Scripts/Combat/TargetingSystem.cs` (novo)
- `Assets/Scripts/Combat/CameraLockOnController.cs` (novo)
- `Assets/InputSystem_Actions.inputactions` (editado)
- `Assets/Art/Animations/Greatsword/Standing Dodge Forward.fbx` (import
  settings — passo 1, Editor)
- `Assets/Art/Animations/PlayerAnimatorController.controller` (passos 2-6,
  Editor)
- `Assets/Scenes/SampleScene.unity` (passos 7-9, Editor)

## Verificação (Play Mode)

- **Dodge**: Círculo rola sempre pra frente com a mesma distância/
  velocidade (vem do root motion do clipe — parado, andando, correndo ou
  sprintando não muda mais nada, decisão revista no Passo 1); arma visível
  na mão durante o roll quando `Wielded` (`ArmedDodgeGrip`); aterrissa em
  `Locomotion`/`ArmedLocomotion` corretos conforme `IsWielded`; reiniciar o
  Play Mode e repetir com a arma guardada (deve rolar sem segurar nada).
- **Dodge-cancel**: apertar Círculo no meio de qualquer hit do combo
  (Attack1/2/3), do Strong Attack e do Slide Attack interrompe o golpe na
  hora e entra no roll — testar os 5 estados individualmente. Sem as
  transições do Passo 3, o trigger fica pendurado e o dodge só dispara
  depois (atrasado, quando o ataque terminar sozinho) — se isso acontecer,
  falta alguma das 5 transições de entrada.
- **Strong Attack**: Triângulo só ataca com arma sacada (`IsWielded`); não
  dispara em pleno combo nem interrompe; combo normal (Quadrado) não
  dispara em pleno Strong Attack; volta pra idle armado limpo.
- **Lock-on**: R3/Tab perto de um dummy trava (câmera troca pra
  `LockOnCamera`, reenquadrando player+dummy); R3/Tab de novo destrava
  (volta pra FreeLook); com 2+ dummies, flick do analógico direito (ou
  mouse, se testando com `Look` no KBM) cicla sem voltar ao primeiro depois
  do último (sem wraparound); atacar/ataque-forte travado gira o
  personagem de frente pro dummy antes do golpe.
- **Habilidades**: L2+Triângulo (ou tecla 3) e L2+Quadrado (ou tecla 4)
  disparam cada animação sem também disparar ataque normal/forte nem o
  outro; funciona armado e desarmado (destino de saída correto via
  `IsWielded`); segurar L2 e apertar Triângulo/Quadrado **não** deve
  disparar `StrongAttack`/combo junto.
- **Regressão**: sprint, dash, slide attack, draw/sheath, combo de 3 hits e
  pulo (desarmado e armado) continuam idênticos ao comportamento de antes
  desta passada.
