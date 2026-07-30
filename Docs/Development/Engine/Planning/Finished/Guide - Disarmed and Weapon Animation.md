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

**CONCLUÍDO.** Sistema Sheathed/Wield da GreatSword implementado e verificado
em Play Mode (Passo 13 + checklist completa: spam de F, F no meio do combo,
pulo armado/desarmado, reinício do Play Mode). Inclui as 3 extensões pedidas
depois do sistema base: mover-se durante Draw/Sheath (layer `UpperBody`
mascarada), Attack com arma guardada sacando em vez de atacar
(`WeaponEquipController.RequestDraw()`), e o ajuste fino do frame de
`OnWeaponSheathed` em `GreatSwordDraw1_Reverse` (sem pop perceptível).

**Histórico (já feito):**
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

**Já feito (atualização):**
- Passo 1 concluído: clipes de Idle/Run/Jump desarmados confirmados como
  genuinamente desarmados e reimportados — locomoção desarmada rodando bem.

**Descoberta que muda os Passos 2/3/8/9/11:** o `GreatSwordDraw` do Mixamo veio
dividido em 2 clipes sequenciais, não 1 — `Draw A Great Sword 1.fbx` (alcançar
e sacar a espada da bainha) e `Draw A Great Sword 2.fbx` (ajeitar em pose de
combate). Ambos já importados e movidos para
`Assets/Art/Animations/Greatsword/` (junto dos outros clipes da arma — tinham
caído em `Unarmed/` por serem o último import). Os passos abaixo já refletem
essa estrutura de 2 clipes; ver detalhes no racional de cada passo. O código
de `WeaponEquipController.cs` **não muda** — `PollStateExit` só reage à saída
da tag `WeaponDrawing`/`WeaponSheathing`, e como os dois clipes de cada
direção compartilham a mesma tag, a transição interna entre eles é invisível
pro C#.

Os passos 2–13 abaixo (sockets, restruturação do Animator, Animation Events,
`WeaponEquipController` anexado e conectado) foram todos executados — ficam
como referência de como o sistema foi montado, incluindo a extensão da layer
`UpperBody` documentada mais abaixo.

## Passos de implementação

1. **Sourcing de animações desarmadas.** Confirmar/substituir os clipes de
   Idle/Run da Blend Tree `Locomotion` e o clipe do estado `Jump` por clipes
   Mixamo genuinamente desarmados (os atuais são suspeitos de ser placeholder).
   Reimportar como Humanoid, conferir retarget contra o Avatar do projeto.

2. **Importar `GreatSwordDraw1` e `GreatSwordDraw2`** do Mixamo como Humanoid,
   retarget no mesmo Avatar — já importados em
   `Assets/Art/Animations/Greatsword/` (`Draw A Great Sword 1.fbx` /
   `Draw A Great Sword 2.fbx`). São uma sequência: 1 termina com a espada fora
   da bainha (na mão), 2 é só o ajuste final até a pose de `ArmedLocomotion`.
   Em cada aba de import: desmarcar **Loop Time**; marcar **Bake Into Pose** em
   Root Transform Position (Y e XZ) e Root Transform Rotation, pra não deixar a
   clip com root motion residual (fica "in place", já que `OnAnimatorMove()`
   roda todo frame independente do estado).

3. **Duplicar os assets de `GreatSwordDraw1` e `GreatSwordDraw2`** em dois
   clipes independentes (`GreatSwordDraw1_Reverse`, `GreatSwordDraw2_Reverse`),
   mesmos keyframes cada. Mesmo racional de antes — Animation Events disparam
   por cruzamento de tempo dentro do *asset do clipe*, independente do estado
   do Animator que o está tocando, então Draw e Sheath não podem compartilhar
   asset se cada um precisa do seu próprio callback (`OnWeaponGrabbed` vs
   `OnWeaponSheathed`). A ordem se inverte no embainhar: o personagem sacou
   tocando 1→2, então embainha tocando 2_Reverse→1_Reverse (sai da pose de
   combate, volta pro meio do gesto de saque, aí a espada reencaixa na
   bainha).

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

8. **Adicionar os 4 estados de Draw/Sheath**: `GreatSwordDraw1` (Motion =
   clipe `GreatSwordDraw1`, tag `WeaponDrawing`), `GreatSwordDraw2` (Motion =
   clipe `GreatSwordDraw2`, tag `WeaponDrawing`), `GreatSwordSheath2` (Motion =
   clipe `GreatSwordDraw2_Reverse`, **Speed = -1**, **Cycle Offset = 1** —
   necessário para começar a amostragem no fim do clipe e andar pra trás sem
   "pop" —, Loop Time desligado, tag `WeaponSheathing`), `GreatSwordSheath1`
   (mesma config de Speed/Cycle Offset/Loop Time, Motion = clipe
   `GreatSwordDraw1_Reverse`, tag `WeaponSheathing`).

9. **Wire das transições** — ver tabela completa abaixo.

10. **Reapontar** a transição de entrada de `Attack1` de `Locomotion` para
    `ArmedLocomotion`, e reapontar o destino das transições de saída do combo
    (`ComboQueued == false` + exit time) de `Locomotion` para `ArmedLocomotion`.

11. **Adicionar Animation Events**: no clipe `GreatSwordDraw1`, um evento no
    frame em que a mão alcança/agarra a espada nas costas — deve estar perto
    do fim do clipe, já que Draw1 termina com a espada fora da bainha —,
    chamando `OnWeaponGrabbed`. No clipe `GreatSwordDraw1_Reverse`, um evento
    chamando `OnWeaponSheathed` na **mesma posição de tempo do clipe** que o
    evento de grab em `GreatSwordDraw1` — como eventos disparam por
    cruzamento de tempo do clipe independente da direção de playback, o mesmo
    tempo autorado corresponde a "mão na arma" nos dois sentidos. `Draw2` e
    `Draw2_Reverse` não precisam de evento nenhum — a troca física do socket
    já aconteceu no clipe 1, o clipe 2 é só ajuste de pose.

12. **Anexar `WeaponEquipController`** ao GameObject que carrega o `Animator`
    — Animation Events são despachados via `GameObject.SendMessage` contra o
    GameObject dono do `Animator`, não seu pai nem seus filhos. Neste projeto
    isso é o **mesmo GameObject** que já tem o `PlayerController` (não um
    filho separado do rig — a suposição original deste guia de que
    Animator/PlayerController ficariam em objetos diferentes não bateu com a
    hierarquia real da cena). Conectar no Inspector: `weapon` → GameObject da
    espada, `sheathSocket`/`wieldSocket` → os dois sockets novos, `animator`
    → o Animator do mesmo objeto, `Input Actions` → o asset
    `InputSystem_Actions` (mesmo do `PlayerController`), nomes de
    action/trigger nos defaults (`Equip`/`Draw`/`Sheath`).

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
    // projeto isso é o mesmo GameObject onde já fica o PlayerController (o
    // Animator não está num filho separado do rig).
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
        // Draw/Sheath tocam numa layer mascarada (só tronco/braços) pra deixar a
        // Locomotion da layer base livre — assim o personagem continua andando
        // normalmente enquanto saca/guarda. Ver "Extensão: mover-se durante
        // Draw/Sheath" mais abaixo pro racional completo do masking.
        [SerializeField] private string weaponLayerName = "UpperBody";

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string equipActionName = "Equip";

        [SerializeField] private WeaponState currentState = WeaponState.Sheathed;

        private InputAction equipAction;
        private int drawTriggerHash;
        private int sheathTriggerHash;
        private int previousStateHash;
        private int weaponLayerIndex;

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
            weaponLayerIndex = animator.GetLayerIndex(weaponLayerName);

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
            var stateInfo = animator.GetCurrentAnimatorStateInfo(weaponLayerIndex);

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
                TriggerDraw();
            }
            else if (currentState == WeaponState.Wielded)
            {
                TriggerSheath();
            }
        }

        // Chamado pelo PlayerController quando Attack é pressionado com a arma
        // guardada: saca em vez de atacar, sem duplicar a lógica de trigger nem
        // depender do input de Equip.
        public void RequestDraw()
        {
            if (currentState != WeaponState.Sheathed)
            {
                return;
            }

            TriggerDraw();
        }

        private void TriggerDraw()
        {
            animator.ResetTrigger(sheathTriggerHash); // defensivo: limpa trigger pendente
            animator.SetTrigger(drawTriggerHash);
            currentState = WeaponState.Drawing;
        }

        private void TriggerSheath()
        {
            animator.ResetTrigger(drawTriggerHash);
            animator.SetTrigger(sheathTriggerHash);
            currentState = WeaponState.Sheathing;
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
+            if (weaponEquip != null && weaponEquip.CurrentState == WeaponState.Sheathed)
+            {
+                weaponEquip.RequestDraw();
+            }
+            else if (!IsAttacking() && (weaponEquip == null || weaponEquip.IsWielded))
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

**Atualização:** Attack pressionado com a arma guardada agora saca em vez de
atacar (`weaponEquip.RequestDraw()`), sem embainhar de volta — só o botão
`Equip` (F) embainha. `RequestDraw()` é um método público novo em
`WeaponEquipController` que reusa a mesma lógica interna de
`HandleToggleInput()` (refatorada em `TriggerDraw()`/`TriggerSheath()`
privados) mas só age se `currentState == Sheathed`, então chamar de novo
durante `Drawing`/`Wielded`/`Sheathing` é um no-op seguro.

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
| `GreatSwordDraw1` | clipe `GreatSwordDraw1` | 1 | 0 | `WeaponDrawing` |
| `GreatSwordDraw2` | clipe `GreatSwordDraw2` | 1 | 0 | `WeaponDrawing` |
| `GreatSwordSheath2` | clipe `GreatSwordDraw2_Reverse` | **-1** | **1** | `WeaponSheathing` |
| `GreatSwordSheath1` | clipe `GreatSwordDraw1_Reverse` | **-1** | **1** | `WeaponSheathing` |

A combinação Speed=-1 + Cycle Offset=1 em cada estado de Sheath começa a
amostragem no fim do próprio clipe e anda pra trás, evitando o pop que
Speed=-1 sozinho causaria (começaria a amostrar em tempo 0 e tentaria ir
negativo) — o Cycle Offset é relativo ao tempo normalizado (0–1) de cada
clipe individualmente, então funciona igual em `GreatSwordSheath1` e
`GreatSwordSheath2` mesmo sendo assets diferentes. Loop Time precisa estar
desligado nos quatro estados de Draw/Sheath — senão o wraparound quebra a
matemática do offset e as transições por exit time. O Exit Time do Mecanim
conta o tempo decorrido no estado sempre pra cima, independente da direção de
playback, então as transições por exit time saindo dos estados de Sheath
funcionam normalmente.

**Transições a conectar:**

| De | Para | Condição | Has Exit Time | Notas |
|---|---|---|---|---|
| `Locomotion` | `GreatSwordDraw1` | `Draw` | Off | blend ~0.1s |
| `GreatSwordDraw1` | `GreatSwordDraw2` | — | On (~0.95–1.0) | blend curto ~0.05–0.1s, os dois clipes já foram autorados pra encadear |
| `GreatSwordDraw2` | `ArmedLocomotion` | — | On (~0.95–1.0) | blend fixo ~0.15s pra idle |
| `ArmedLocomotion` | `GreatSwordSheath2` | `Sheath` | Off | blend ~0.1s |
| `GreatSwordSheath2` | `GreatSwordSheath1` | — | On (~0.95–1.0) | blend curto ~0.05–0.1s |
| `GreatSwordSheath1` | `Locomotion` | — | On (~0.95–1.0) | blend fixo ~0.15s pra idle |
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

## Extensão: mover-se durante Draw/Sheath (Upper Body Layer)

**Motivação:** `PlayerController.OnAnimatorMove()` só translada o personagem a
partir do root motion do state ativo na layer 0. `GreatSwordDraw1/2` e
`GreatSwordSheath1/2` foram importados com Bake Into Pose ligado (fica in
place, Passo 2) — então assim que um desses estados fica ativo, o personagem
para fisicamente, mesmo segurando input de movimento (o trigger de
Draw/Sheath em si já dispara em qualquer velocidade, sem gate nenhum; o
congelamento é só consequência do root motion zerado desses clipes). Solução:
mover Draw/Sheath pra uma **layer separada, mascarada pra só afetar
tronco/braços**, enquanto a layer base (pernas + translação) continua livre
em `Locomotion`/`ArmedLocomotion` o tempo todo, ininterrupta.

### Mudança na Base Layer (layer 0)

- **Remover** os 4 estados `GreatSwordDraw1`/`GreatSwordDraw2`/
  `GreatSwordSheath1`/`GreatSwordSheath2` da layer base, junto das 6
  transições que os ligavam (linhas 1–6 da tabela de transições acima).
- **Adicionar 2 transições diretas** no lugar delas:

| De | Para | Condição | Has Exit Time | Notas |
|---|---|---|---|---|
| `Locomotion` | `ArmedLocomotion` | `Draw` | Off | blend mais longo (~0.5–0.8s) pra acompanhar aproximadamente a duração do saque na layer de cima |
| `ArmedLocomotion` | `Locomotion` | `Sheath` | Off | mesma lógica |

`ArmedJump`/`Jump`/`Attack1`/`Attack3` ficam exatamente como estão — sem
mudança nenhuma nessas.

### Nova Layer `UpperBody`

1. **Criar o Avatar Mask**: `Assets > Create > Avatar Mask`, nomeie
   `UpperBodyMask`. No editor do mask (diagrama do boneco humanoide), três
   grupos de toggle precisam ficar **vermelhos (excluídos)** — só desmarcar
   as pernas não é suficiente:
   - **Left Leg** e **Right Leg** (a silhueta da coxa/canela no diagrama).
   - **Left Foot IK** e **Right Foot IK** (os círculos "IK" perto dos pés) —
     é um canal separado da animação de músculo da perna; o Unity aplica IK
     *depois* da pose normal, então mesmo com as pernas excluídas, o IK do pé
     (se incluído) ainda planta/trava o pé na posição autorada do clipe de
     Draw/Sheath.
   - **Root** (o disco/oval embaixo dos pés no diagrama, separado dos
     círculos de IK) — é o canal de translação/rotação real do personagem.
     Incluído, numa layer Override com Weight 1, ele **substitui** a
     translação da layer base pelo root motion quase-zero do Draw/Sheath
     (importados "fica in place" de propósito) — sem excluir isso, o
     personagem "corre parado" enquanto os clipes de Draw/Sheath tocam.

   Deixe **Head**, **Body** (tronco/coluna) e **Left Arm**/**Right Arm**
   verdes (incluídos) — é só isso que precisa vir do Draw/Sheath. Left/Right
   **Hand** (os círculos de IK na mão) também ficam verdes, já que a troca de
   socket depende da mão seguir a pose autorada do clipe.
2. **Criar a layer**: no Animator window, aba **Layers** (canto superior
   esquerdo) → **+**. Renomeie pra `UpperBody` — precisa bater exatamente com
   o `weaponLayerName` default no `WeaponEquipController` (`"UpperBody"`), já
   que o script resolve o índice da layer por nome em `Awake()`. No ícone de
   engrenagem da layer: **Weight = 1**, **Blending = Override**, **Mask =
   UpperBodyMask**.
3. **Criar o estado padrão `Empty`**: dentro dessa layer, `Create State >
   Empty`, **Motion = None** (deixe vazio), marque como estado padrão
   (laranja). Com Motion vazio numa layer Override, ela não sobrepõe nada
   enquanto `Empty` está ativo — é assim que a layer fica "desligada" na
   maior parte do tempo, sem precisar controlar o Weight dinamicamente via
   código.
4. **Recriar os 4 estados de Draw/Sheath nessa layer**, reusando os MESMOS
   assets de clipe de antes (não duplica de novo): `GreatSwordDraw1`,
   `GreatSwordDraw2`, `GreatSwordSheath2` (Speed -1, Cycle Offset 1),
   `GreatSwordSheath1` (Speed -1, Cycle Offset 1) — mesmas tags
   `WeaponDrawing`/`WeaponSheathing` de antes, mesmos Animation Events
   (ficam associados ao asset do clipe, não precisam ser recriados).
5. **Wire das transições nessa layer**:

| De | Para | Condição | Has Exit Time |
|---|---|---|---|
| `Empty` | `GreatSwordDraw1` | `Draw` | Off |
| `GreatSwordDraw1` | `GreatSwordDraw2` | — | On (~0.95–1.0) |
| `GreatSwordDraw2` | `Empty` | — | On (~0.95–1.0) |
| `Empty` | `GreatSwordSheath2` | `Sheath` | Off |
| `GreatSwordSheath2` | `GreatSwordSheath1` | — | On (~0.95–1.0) |
| `GreatSwordSheath1` | `Empty` | — | On (~0.95–1.0) |

Os Animation Events (`OnWeaponGrabbed`/`OnWeaponSheathed`) continuam
funcionando sem nenhuma mudança — pertencem ao asset do clipe, não ao estado
nem à layer, e `SendMessage` sempre mira o GameObject dono do `Animator`
independente de qual layer está tocando o clipe no momento.

### Mudança de código (já aplicada)

`WeaponEquipController.cs` ganhou o campo `weaponLayerName` (default
`"UpperBody"`), resolvido em `Awake()` via `animator.GetLayerIndex(...)`, e
`PollStateExit()` agora lê `GetCurrentAnimatorStateInfo(weaponLayerIndex)` em
vez do `0` hardcoded — é assim que o script sabe se ainda está em
`WeaponDrawing`/`WeaponSheathing` agora que esses estados moraram pra outra
layer. `IsAttacking()` continua lendo a layer 0 (o tag `Attack` não mudou de
lugar, o combo continua só na layer base).

### Observação

O crossfade da layer base (`Locomotion`↔`ArmedLocomotion`) e o da layer de
cima (Draw/Sheath↔`Empty`) são independentes — disparados pelo mesmo trigger,
mas sem sincronia de tempo entre si. Pode sobrar um instante em que as pernas
ainda estão em transição pra pose armada enquanto os braços já terminaram o
gesto (ou vice-versa); ajuste as durações de blend das duas tabelas até isso
ficar natural — não tem valor exato aqui, é acerto visual.

## Verificação (Play Mode)

- **Sacar/guardar andando:** segurar input de movimento e apertar F — as
  pernas devem continuar no ciclo de `Locomotion`/`ArmedLocomotion` (correndo
  ou andando) sem congelar, enquanto tronco/braços tocam o gesto de
  Draw/Sheath por cima na layer `UpperBody`. `IsWielded` deve virar `true`/
  `false` no mesmo instante de antes (a troca de socket não depende de estar
  parado).
- **Caminho feliz:** início da cena → espada visível nas costas,
  `currentState == Sheathed`. F → `GreatSwordDraw1` toca, arma solta das costas
  e aparece na mão perto do fim do clipe, encadeia sem pop pra `GreatSwordDraw2`,
  termina em `ArmedLocomotion` idle, `IsWielded == true`. F de novo →
  `GreatSwordSheath2` toca, encadeia sem pop pra `GreatSwordSheath1`, arma sai
  da mão e reencaixa nas costas perto do fim desse clipe, termina em
  `Locomotion` idle, `IsWielded == false`.
- **Encadeamento 1→2 (e 2→1 no sheath):** confirmar visualmente que não há pop
  nem hesitação na transição por exit time entre os dois clipes de cada
  direção — é o ponto novo introduzido pelo split em 2 assets.
- **Janela do Animator:** confirmar que a tag do estado lê `WeaponDrawing`
  durante `GreatSwordDraw1`/`GreatSwordDraw2` e `WeaponSheathing` durante
  `GreatSwordSheath2`/`GreatSwordSheath1`; confirmar que os dois estados de
  Sheath mostram Speed -1 e tocam visivelmente para trás.
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
- `Assets/Art/Animations/Greatsword/Y Bot@Draw A Great Sword 1.fbx` (fonte de
  `GreatSwordDraw1` / `GreatSwordDraw1_Reverse`)
- `Assets/Art/Animations/Greatsword/Y Bot@Draw A Great Sword 2.fbx` (fonte de
  `GreatSwordDraw2` / `GreatSwordDraw2_Reverse`)
- `Assets/Scenes/SampleScene.unity` (sockets, wiring de componentes, parenting
  padrão)
