# Sistema de Sheathed/Wield para Armas (GreatSword)

## Contexto

O projeto Unity (`babel_unity/babel_unity`, Unity 6000.5.5f1, URP) tem o
`PlayerController.cs` (renomeado de `SimpleNierController`) e um Animator
Controller (`PlayerAnimatorController.controller`) com locomoção + combo de
ataque em 3 hits. A GreatSword (`2hand_dragon_sword.glb`) está hoje parentada
manualmente e de forma estática num bone do rig — sempre visível, sem sistema
de socket, sem Sheathed/Wield.

O objetivo é implementar a transição de estado Sheathed ↔ Wield: a arma começa
presa nas costas (`GreatSwordSheathSocket`), e ao pressionar F o personagem toca
`GreatSwordDraw`, troca fisicamente a arma para a mão (`GreatSwordWieldSocket`) via
Animation Event, e entra em estado de combate (pode atacar). Pressionar F de novo
faz o caminho inverso. O sistema precisa ficar modular o bastante para, no futuro,
ser reaproveitado por outras armas (cajado, adaga) com seus próprios pares de
socket e clipes — sem virar um sistema de inventário completo agora.

Decisões já confirmadas com o usuário:
- **Sem clipe dedicado de embainhar**: `GreatSwordDraw` será duplicado como asset
  (`GreatSwordDraw_Reverse`) e tocado ao contrário (Speed = -1) num estado
  `GreatSwordSheath` — evita precisar baixar uma segunda animação agora.
- **Input**: New Input System, via o asset `InputSystem_Actions` (action
  `Equip`, botão `<Keyboard>/f`, no mesmo map `Player` usado por
  Move/Attack/Jump). A migração do legacy Input pra cá já foi feita em ambos
  os scripts (ver "Status atual" abaixo) — não é mais uma decisão em aberto.

Convenções do projeto: namespaces já aplicados (`Babel.Player` no
`PlayerController`, `Babel.Equipment` no `WeaponEquipController`), sem
`.asmdef`. Idioma de código: padrão "polling de tag/hash do Animator" (ver
`IsAttacking()`/`HandleAttack()` em `PlayerController.cs`) em vez de
`StateMachineBehaviour` — o novo código segue esse mesmo idioma em vez de
introduzir um conceito novo.

## Status atual

**Já feito:**
- `PlayerController.cs` renomeado (de `SimpleNierController`) e movido para
  `Assets/Scripts/Player/`, namespace `Babel.Player`.
- `WeaponEquipController.cs` criado em `Assets/Scripts/Equipment/`, namespace
  `Babel.Equipment`, com a máquina de estados Sheathed/Drawing/Wielded/Sheathing
  e os callbacks de Animation Event (`OnWeaponGrabbed`/`OnWeaponSheathed`).
- Ambos os scripts migrados pra ler input via `InputActionAsset`/`InputAction`
  (`Move`/`Attack`/`Jump`/`Equip`) em vez de `Input.GetAxis`/`GetButtonDown`/
  `GetKeyDown`.
- Action `Equip` (Button, bind `<Keyboard>/f`) adicionada ao map `Player` em
  `InputSystem_Actions.inputactions`.
- Diff aplicado em `PlayerController.cs`: referência a `WeaponEquipController`
  e gate de `Attack` por `IsWielded`.
- Campo **Input Actions** do `PlayerController` já conectado no Inspector ao
  asset `InputSystem_Actions` (sem isso o jogo quebrava em `Awake()`).

**Ainda pendente** (trabalho manual no Editor — passos 1–13 abaixo):
sourcing das animações no Mixamo, criação dos sockets na hierarquia,
restruturação do `PlayerAnimatorController` (parâmetros/estados/transições),
Animation Events nos clipes, e anexar `WeaponEquipController` ao GameObject do
rig — incluindo, nesse momento, conectar o campo **Input Actions** dele
também ao mesmo asset `InputSystem_Actions` (mesma pendência que já foi
resolvida no `PlayerController`, mas esse componente ainda não foi anexado à
cena).

## Passos de implementação

1. **Sourcing de animações desarmadas.** Confirmar/substituir os clipes de
   Idle/Run da Blend Tree `Locomotion` e o clipe do estado `Jump` por clipes
   Mixamo genuinamente desarmados (os atuais são suspeitos de ser placeholder).
   Reimportar como Humanoid, conferir retarget contra o Avatar do projeto.

2. **Importar `GreatSwordDraw`** do Mixamo como Humanoid, retarget no mesmo
   Avatar. Na aba de import da animação: desmarcar **Loop Time**; marcar **Bake
   Into Pose** em Root Transform Position (Y e XZ) e Root Transform Rotation, pra
   não deixar a clip com root motion residual (fica "in place", já que
   `OnAnimatorMove()` roda todo frame independente do estado).

3. **Duplicar o asset de `GreatSwordDraw`** em um segundo clipe independente
   (`GreatSwordDraw_Reverse`), mesmos keyframes. Necessário porque Animation
   Events disparam por cruzamento de tempo dentro do *asset do clipe*,
   independente do estado do Animator que o está tocando — se Draw e Sheath
   compartilhassem um único clipe, não daria pra ter dois métodos de callback
   diferentes (`OnWeaponGrabbed` vs `OnWeaponSheathed`) num único clipe.

4. **Criar os sockets na hierarquia** (não existem ainda):
   `GreatSwordSheathSocket` como Transform filho vazio do bone de
   espinha/peito, `GreatSwordWieldSocket` como Transform filho vazio do bone da
   mão direita. Para ajustar cada um: parenteie a espada temporariamente nele com
   local pos/rot zerados, escrube o preview do Animator até a pose relevante
   (final do Draw / pose parada nas costas) e ajuste o transform **do socket**
   (não da espada) até encaixar — o script sempre zera o transform local da
   espada ao trocar de pai, então é o transform local autorado do socket que
   define o encaixe final.

5. **Deixar a espada parentada em `GreatSwordSheathSocket`** com transform local
   identidade como estado padrão da cena (arma começa nas costas).

6. **Adicionar parâmetros no Animator**: `Draw` (Trigger) e `Sheath` (Trigger).
   Nenhum bool é necessário — ver racional na seção do Animator abaixo.

7. **Adicionar estados `ArmedLocomotion`** (Blend Tree 1D em `Speed`, usando
   `great sword idle.fbx` / `great sword run (2).fbx`, já importados) e
   **`ArmedJump`** (clipe único `great sword jump.fbx`, já importado), espelhando
   `Locomotion`/`Jump`.

8. **Adicionar estado `GreatSwordDraw`** (Motion = clipe `GreatSwordDraw`, tag
   `WeaponDrawing`) e **estado `GreatSwordSheath`** (Motion = clipe
   `GreatSwordDraw_Reverse`, **Speed = -1**, **Cycle Offset = 1** — necessário
   para começar a amostragem no fim do clipe e andar pra trás sem "pop" —, Loop
   Time desligado, tag `WeaponSheathing`).

9. **Wire das transições** — ver tabela completa abaixo.

10. **Reapontar** a transição de entrada de `Attack1` de `Locomotion` para
    `ArmedLocomotion`, e reapontar o destino das transições de saída do combo
    (`ComboQueued == false` + exit time) de `Locomotion` para `ArmedLocomotion`.

11. **Adicionar Animation Events**: no clipe `GreatSwordDraw`, um evento no
    frame em que a mão alcança a espada nas costas, chamando `OnWeaponGrabbed`.
    No clipe `GreatSwordDraw_Reverse`, um evento chamando `OnWeaponSheathed` na
    **mesma posição de tempo do clipe** que o evento de grab — como eventos
    disparam por cruzamento de tempo do clipe independente da direção de
    playback, o mesmo tempo autorado corresponde a "mão na arma" nos dois
    sentidos.

12. **Anexar `WeaponEquipController`** ao GameObject filho do rig que já carrega
    o `Animator` (não no root do Player, onde está `PlayerController`) —
    Animation Events são despachados via `GameObject.SendMessage` contra o
    GameObject dono do `Animator`, não seu pai nem seus filhos. Conectar no
    Inspector: `weapon` → GameObject da espada, `sheathSocket`/`wieldSocket` →
    os dois sockets novos, `animator` → o Animator do mesmo objeto, `Input
    Actions` → o asset `InputSystem_Actions` (mesmo do `PlayerController`),
    nomes de action/trigger nos defaults (`Equip`/`Draw`/`Sheath`).

13. **Playtest** contra o checklist de verificação no final deste documento.

## `WeaponEquipController.cs` (já criado)

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

namespace Babel.Equipment
{
    public enum WeaponState
    {
        Sheathed,
        Drawing,
        Wielded,
        Sheathing
    }

    // Máquina de estados Sheathed/Wielded de uma arma: input de toggle, disparo de
    // triggers no Animator, detecção de fim de transição (mesmo idioma de
    // "previous state hash" que PlayerController usa pro combo) e a troca
    // física entre os sockets de bainha/empunhadura, disparada por Animation Events.
    //
    // Precisa estar no MESMO GameObject que o Animator que toca os clipes de
    // Draw/Sheath — Animation Events são despachados via GameObject.SendMessage
    // contra esse GameObject especificamente, não o pai nem os filhos. Neste
    // projeto isso é o filho do rig, não o root do Player (onde fica
    // PlayerController).
    //
    // Todas as referências de arma/socket/parâmetro são campos serializados no
    // Inspector, então este mesmo componente pode ser reaproveitado num rig de
    // cajado/adaga no futuro só reapontando referências e duplicando o sub-grafo
    // do Animator — sem mudar código.
    [RequireComponent(typeof(Animator))]
    public class WeaponEquipController : MonoBehaviour
    {
        [Header("Weapon & Sockets")]
        [SerializeField] private Transform weapon;
        [SerializeField] private Transform sheathSocket;
        [SerializeField] private Transform wieldSocket;

        [Header("Animator")]
        [SerializeField] private Animator animator;
        [SerializeField] private string drawTriggerName = "Draw";
        [SerializeField] private string sheathTriggerName = "Sheath";
        [SerializeField] private string drawingTag = "WeaponDrawing";
        [SerializeField] private string sheathingTag = "WeaponSheathing";
        [SerializeField] private string attackTag = "Attack";

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string equipActionName = "Equip";

        [SerializeField] private WeaponState currentState = WeaponState.Sheathed;

        private InputAction equipAction;
        private int drawTriggerHash;
        private int sheathTriggerHash;
        private int previousStateHash;

        public WeaponState CurrentState => currentState;
        public bool IsWielded => currentState == WeaponState.Wielded;
        public bool IsTransitioning => currentState == WeaponState.Drawing || currentState == WeaponState.Sheathing;

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            var playerMap = inputActions.FindActionMap(actionMapName, throwIfNotFound: true);
            equipAction = playerMap.FindAction(equipActionName);

            drawTriggerHash = Animator.StringToHash(drawTriggerName);
            sheathTriggerHash = Animator.StringToHash(sheathTriggerName);

            // A cena autora a espada sob sheathSocket por padrão; forçar o snap no
            // boot garante que estado runtime e visual nunca fiquem dessincronizados.
            SnapWeaponTo(sheathSocket);
        }

        private void OnEnable()
        {
            equipAction.Enable();
        }

        private void OnDisable()
        {
            equipAction.Disable();
        }

        private void Update()
        {
            PollStateExit();
            HandleToggleInput();
        }

        private void PollStateExit()
        {
            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);

            if (stateInfo.fullPathHash == previousStateHash)
            {
                return;
            }

            previousStateHash = stateInfo.fullPathHash;

            if (currentState == WeaponState.Drawing && !stateInfo.IsTag(drawingTag))
            {
                currentState = WeaponState.Wielded;
            }
            else if (currentState == WeaponState.Sheathing && !stateInfo.IsTag(sheathingTag))
            {
                currentState = WeaponState.Sheathed;
            }
        }

        private void HandleToggleInput()
        {
            if (!equipAction.WasPressedThisFrame() || IsTransitioning || IsAttacking())
            {
                return;
            }

            if (currentState == WeaponState.Sheathed)
            {
                animator.ResetTrigger(sheathTriggerHash); // defensivo: limpa trigger pendente
                animator.SetTrigger(drawTriggerHash);
                currentState = WeaponState.Drawing;
            }
            else if (currentState == WeaponState.Wielded)
            {
                animator.ResetTrigger(drawTriggerHash);
                animator.SetTrigger(sheathTriggerHash);
                currentState = WeaponState.Sheathing;
            }
        }

        private bool IsAttacking()
        {
            return animator.GetCurrentAnimatorStateInfo(0).IsTag(attackTag);
        }

        // -- Callbacks de Animation Event ------------------------------------
        // Ligar OnWeaponGrabbed no clipe GreatSwordDraw e OnWeaponSheathed no
        // clipe GreatSwordDraw_Reverse, no frame em que a mão encontra a arma.

        public void OnWeaponGrabbed()
        {
            SnapWeaponTo(wieldSocket);
        }

        public void OnWeaponSheathed()
        {
            SnapWeaponTo(sheathSocket);
        }

        private void SnapWeaponTo(Transform socket)
        {
            // SetParent(socket, false) mantém a posição/rotação local EXISTENTE da
            // espada e só reinterpreta sob o novo pai — NÃO zera pra alinhar com a
            // origem do socket. Sem o zeramento explícito abaixo, a espada mantém
            // seu offset relativo ao socket antigo, agora aplicado ao novo socket,
            // o que confiavelmente produz um pop/desalinhamento visível. Zerar é o
            // que de fato encaixa a espada no offset autorado do socket.
            weapon.SetParent(socket, false);
            weapon.localPosition = Vector3.zero;
            weapon.localRotation = Quaternion.identity;
        }
    }
}
```

## Diff já aplicado em `PlayerController.cs`

```diff
     private CharacterController controller;
     private Animator animator;
+    private WeaponEquipController weaponEquip;
     private Transform mainCameraTransform;
     private float verticalVelocity;
     private bool comboQueued;
     private int previousStateHash;

     private void Awake()
     {
         controller = GetComponent<CharacterController>();
         animator = GetComponentInChildren<Animator>();
+        weaponEquip = GetComponentInChildren<WeaponEquipController>();

         if (Camera.main != null)
         {
             mainCameraTransform = Camera.main.transform;
         }
     }
```

```diff
         if (attackAction.WasPressedThisFrame())
         {
-            if (!IsAttacking())
+            if (!IsAttacking() && (weaponEquip == null || weaponEquip.IsWielded))
             {
                 animator.SetTrigger("Attack");
             }
             else
             {
                 comboQueued = true;
             }
         }
```

`weaponEquip == null ||` é só um fallback defensivo (mantém o jogo jogável se a
referência faltar numa cena de teste) — no setup pretendido nunca deve ser null.

`HandleJump()` fica **sem alteração** — o objetivo era a animação de pulo
correta (armada vs desarmada), não bloquear o pulo em si. Isso é resolvido
inteiramente no Animator: `Locomotion` e `ArmedLocomotion` cada uma tem sua
própria transição de saída via `Jump` para seu próprio estado de pulo
(`Jump` vs `ArmedJump`), então o clipe certo é escolhido automaticamente pelo
branch de locomoção ativo — sem gating em C#. Se no futuro quiser bloquear
pulo desarmado, é o mesmo padrão de uma linha usado no Attack.

## Restruturação do Animator Controller

**Novos parâmetros:** `Draw` (Trigger), `Sheath` (Trigger). Sem bool.

**Por que não precisa de bool:** `WeaponEquipController` só chama
`SetTrigger(Draw)` com `currentState == Sheathed` e `SetTrigger(Sheath)` com
`currentState == Wielded`, e bloqueia reentrância via `IsTransitioning`. O
próprio grafo reforça isso estruturalmente: a única transição de saída para
`Draw` vem de `Locomotion`, a única para `Sheath` vem de `ArmedLocomotion` — não
existe caminho onde um trigger errante tenha de onde disparar. O risco real de
triggers do Unity (ficam pendentes até *alguma* transição consumir, podendo
disparar depois num estado errado) é fechado por dois pontos já no código: (a)
`ResetTrigger` no trigger oposto toda vez que um é setado, e (b) o trigger
`Attack` agora é gateado por `IsWielded`, que só fica true depois que
`ArmedLocomotion`/`Attack*` são de fato alcançados. Um bool só passaria a valer
a pena se Draw/Sheath precisassem ser disparáveis a partir de mais estados de
origem (ex.: interromper um ataque) — não é o caso agora.

**Novos estados:**

| Estado | Motion | Speed | Cycle Offset | Tag |
|---|---|---|---|---|
| `ArmedLocomotion` | Blend Tree 1D em `Speed` (`great sword idle.fbx` @0, `great sword run (2).fbx` @1) | 1 | 0 | — |
| `ArmedJump` | `great sword jump.fbx` | 1 | 0 | — |
| `GreatSwordDraw` | clipe `GreatSwordDraw` | 1 | 0 | `WeaponDrawing` |
| `GreatSwordSheath` | clipe `GreatSwordDraw_Reverse` | **-1** | **1** | `WeaponSheathing` |

A combinação Speed=-1 + Cycle Offset=1 em `GreatSwordSheath` começa a amostragem
no fim do clipe e anda pra trás, evitando o pop que Speed=-1 sozinho causaria
(começaria a amostrar em tempo 0 e tentaria ir negativo). Loop Time precisa
estar desligado nos dois estados de Draw/Sheath — senão o wraparound quebra a
matemática do offset e as transições por exit time. O Exit Time do Mecanim
conta o tempo decorrido no estado sempre pra cima, independente da direção de
playback, então as transições por exit time saindo de `GreatSwordSheath`
funcionam normalmente.

**Transições a conectar:**

| De | Para | Condição | Has Exit Time | Notas |
|---|---|---|---|---|
| `Locomotion` | `GreatSwordDraw` | `Draw` | Off | blend ~0.1s |
| `GreatSwordDraw` | `ArmedLocomotion` | — | On (~0.95–1.0) | blend fixo ~0.15s pra idle |
| `ArmedLocomotion` | `GreatSwordSheath` | `Sheath` | Off | blend ~0.1s |
| `GreatSwordSheath` | `Locomotion` | — | On (~0.95–1.0) | blend fixo ~0.15s pra idle |
| `ArmedLocomotion` | `ArmedJump` | `Jump` | Off | espelha `Locomotion`→`Jump` |
| `ArmedJump` | `ArmedLocomotion` | — | On | espelha `Jump`→`Locomotion` |
| `Locomotion` | `Jump` | `Jump` | Off | inalterado |
| `Jump` | `Locomotion` | — | On | inalterado |
| `ArmedLocomotion` | `Attack1` | `Attack` | Off | **reapontado** de `Locomotion` |
| `Attack3` (estado terminal do combo) | `ArmedLocomotion` | `ComboQueued == false` + exit time | On | **reapontado** de `Locomotion` |

As transições internas do combo (`ComboQueued == true`, `Attack1`→`Attack2`→
`Attack3`) ficam intocadas — só a origem de entrada e o destino de saída do
combo mudam de `Locomotion` para `ArmedLocomotion`.

## Extensão futura para outra arma (ex.: cajado)

- Duplicar o par de sockets: `StaffSheathSocket`/`StaffWieldSocket`, ajustados
  do mesmo jeito que os da espada.
- Importar `StaffDraw`, duplicar o asset pra ter a versão reversa (mesma
  técnica do passo 3).
- Duplicar o sub-grafo do Animator (`StaffDraw`/`StaffSheath` com as mesmas
  tags `WeaponDrawing`/`WeaponSheathing`, mesmos nomes de trigger `Draw`/
  `Sheath` — parâmetros são compartilhados entre tipos de arma, só o conteúdo
  de socket/clipe muda). Decidir por arma se reaproveita
  `ArmedLocomotion`/`ArmedJump` ou ganha o próprio conjunto — depende de quão
  diferente é a pose de empunhadura, não é uma questão de código.
- `WeaponEquipController` não precisa de nenhuma mudança — toda referência já é
  campo serializado. Hoje: uma instância do componente por arma equipável no
  personagem; adicionar uma segunda arma significa outra instância do
  componente (mais simples, funciona hoje, zero código) — colapsar isso num
  componente único que troca suas referências em runtime é um refactor
  propositalmente adiado, sem registry via ScriptableObject por enquanto.

## Verificação (Play Mode)

- **Caminho feliz:** início da cena → espada visível nas costas,
  `currentState == Sheathed`. F → `GreatSwordDraw` toca, arma solta das costas
  e aparece na mão no meio do clipe, termina em `ArmedLocomotion` idle,
  `IsWielded == true`. F de novo → reverso toca, arma sai da mão e reencaixa
  nas costas no meio do clipe, termina em `Locomotion` idle,
  `IsWielded == false`.
- **Janela do Animator:** confirmar que a tag do estado lê `WeaponDrawing`/
  `WeaponSheathing` durante cada transição; confirmar que `GreatSwordSheath`
  mostra Speed -1 e toca visivelmente para trás.
- **Spam de F:** nenhum re-disparo ou reinício no meio de `Drawing`/
  `Sheathing`; exatamente um ciclo completo por toggle concluído.
- **Attack enquanto Sheathed:** input de ataque não produz mudança de estado,
  Animator permanece em `Locomotion`.
- **F pressionado no meio de Attack1/2/3:** ignorado até o combo voltar pra
  `ArmedLocomotion`.
- **Attack logo depois do Draw terminar:** dispara corretamente agora que a
  origem de `Attack1` é `ArmedLocomotion`.
- **Pulo enquanto Sheathed:** clipe desarmado de `Jump` toca via
  `Locomotion`→`Jump`→`Locomotion`.
- **Pulo enquanto Wielded:** clipe `ArmedJump` toca via
  `ArmedLocomotion`→`ArmedJump`→`ArmedLocomotion`; espada permanece parentada
  corretamente durante todo o pulo (nenhum evento de reparent dispara).
- **Sem pop visual** em nenhum dos dois momentos de Animation Event — se
  houver, reajustar o transform local do socket em questão (não da arma, que
  deve sempre ler `0,0,0`/identidade uma vez parentada).
- **Checagem de estado inicial:** reiniciar o Play Mode — a espada precisa
  sempre começar sob `GreatSwordSheathSocket`, confirmando que o force-snap do
  `Awake()` funciona independente de como a cena foi salva por último.

### Arquivos críticos

- `Assets/Scripts/Equipment/WeaponEquipController.cs` (namespace `Babel.Equipment`)
- `Assets/Scripts/Player/PlayerController.cs` (namespace `Babel.Player`)
- `Assets/InputSystem_Actions.inputactions` (action `Equip`)
- `Assets/Art/Animations/PlayerAnimatorController.controller`
- `Assets/Art/Weapons/2hand_dragon_sword.glb`
- `Assets/Scenes/SampleScene.unity` (sockets, wiring de componentes, parenting
  padrão)
