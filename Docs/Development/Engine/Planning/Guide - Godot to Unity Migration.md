# Guia C-2: Plano Mestre de Migração Godot → Unity (Babel)

Este NÃO é um tutorial clique a clique como o [`GUIA_C_1.MD`](GUIA_C_1.MD)
(esse cobre só a base de locomoção com root motion). Este é o **mapa geral**:
o que existe hoje no projeto Godot original (`babel/`), o que já existe aqui
no Unity, e um roteiro de fases para portar o resto — com a arquitetura alvo
proposta para cada sistema, os principais pontos de atenção na tradução
Godot→Unity, e um checklist por fase. Cada fase, quando for atacada de fato,
deve virar seu próprio `GUIA_C_N` de implementação (esse sim, mais no estilo
clique a clique/código concreto, se fizer sentido na hora).

Convenção de nomes: classes e identificadores de código em inglês (mesmo
padrão do projeto Godot), texto explicativo em PT-BR.

---

## 1. Status atual dos dois projetos

### 1.1 Godot (`babel/`) — o que já existe

O projeto Godot é um roguelike action-RPG 3D em Godot 4.7, ~26 dias de
desenvolvimento, ~163 scripts GDScript, ~41 cenas, 99 commits. Está em fase
**greybox** (cápsulas/caixas, sem arte final), mas os sistemas estão
funcionalmente interligados e jogáveis ponta a ponta. Resumo por sistema
(referências cruzadas ao `CLAUDE.md` do projeto Godot):

| Sistema | Estado no Godot | Arquivos-chave |
|---|---|---|
| Movimento/câmera | Free camera, aceleração de movimento, dodge/dash com i-frames, bullet-time no Perfect Dodge | `actors/player/movement_component.gd`, `player_camera_rig.gd` |
| Combate básico (Skill 1) | Combo de 3 hits data-driven (grafo de nós, não índice fixo), lunge por hit, mira por input de movimento + assist + facing, hit-cone configurável | `actors/player/combat_component.gd`, seções NIER-STYLE / MOVEMENT RAMP no CLAUDE.md |
| Bloqueio/Dodge/Ataque forte | RMB = skill inata por classe (Paladin: Block, 5 segmentos), Shift = dodge universal com Perfect Dodge, Y = Strong Attack (rooted, custa 1 carga de dodge) | seções COMBAT & SKILLS OVERHAUL, STRONG ATTACK |
| Classes/Habilidades | 6 classes standalone (Paladin/Ranger/Mage=Chosen, Berserker/Thief/Occultist=Arisen), 3 habilidades por classe + Skill 1 universal, dispatch via `ClassKit` por classe | `data/classes/class_db.gd`, `class_catalog.tres`, `actors/player/*_kit.gd` |
| Morphs/Talentos | Progressão por escolha (não ponto numérico): "unlock" ou morph (até 3 estágios por habilidade); 15 Soul Talents passivos | `data/classes/morph_db.gd`, `data/talents/talent_db.gd` |
| Inimigos | `EnemyBase` (template com state machine, NavigationAgent3D, telegraph dodgeable circle/rect/cone), subclasses finas (`husk`, bandidos, `spawn`=swarm) | `actors/enemies/enemy_base.gd`, `data/enemies/*.tres` |
| Itens/Inventário | Catálogo CSV (`items.csv`) lido por `ItemDatabase` autoload; 5 `ItemEffect`s (% bonus); equipamento só dropa de bosses | `data/items/`, `systems/inventory/` |
| Salas/Andares procedurais | `RoomBase`/`RoomPrefab` (cenas autoradas + `Marker3D` tipados), `FloorAssembler` monta andar por random walk em grid, NavMesh bakeada em runtime | `world/rooms/`, `world/generated/floor_assembler.gd` |
| Loop roguelike/meta-progressão | `RunManager` (start/climb/descend/morte reseta run), `MetaProgress` autoload (Seeds + 10 unlocks permanentes + itens/materiais persistentes) | `autoload/run_manager.gd`, `autoload/meta_progress.gd` |
| Quests/Diálogo | `QuestDB`/`DialogueDB` (Dictionary/`.tres`), `QuestManager`/`DialogueManager` autoloads, NPCs genéricos | `data/quests/`, `data/dialogue/`, `docs/14_Guia_Quests_e_Dialogos.md` |
| UI/HUD | HUD de combate (barras, hotbar, buffs, dano flutuante), menu de personagem (I/C/K/J), pause, loot/chest, shop | `ui/hud/`, `ui/character_menu.gd` |
| Gamepad | Dual-scheme completo: X ataque, RB lock-on, LT/RT radiais, navegação de menu por `PadNav` | seções GAMEPAD PASS / PASS 2 |
| Save | 5 slots (`SaveManager`), independente do save de meta-progressão | `autoload/save_manager.gd` |

Docs de referência no repo Godot para quando cada fase abaixo for
implementada: `docs/09_*` (inimigos/salas), `docs/11_Guia_Itens.md`,
`docs/12_Guia_Animacoes.md`, `docs/13_Guia_Skills_Morfs_Talentos.md`,
`docs/14_Guia_Quests_e_Dialogos.md`, e o `CLAUDE.md` (histórico completo de
decisões, seção "Keep-in-sync traps" — várias delas são armadilhas de
acoplamento entre dados e código que vale a pena EVITAR recriar no Unity, não
só portar).

### 1.2 Unity (`babel_unity/`) — o que já existe

- **Motor**: Unity 6 (`6000.5.5f1`), URP.
- **Locomoção**: `Assets/Scripts/SimpleNierController.cs` — Idle/Run/Jump com
  **root motion de verdade** (`OnAnimatorMove`, não root motion falso via
  código como o Godot), `CharacterController`, rotação por Slerp
  camera-relative.
- **Combo de 3 hits**: já implementado (`HandleAttack()` no mesmo script +
  `PlayerAnimatorController.controller`: estados `Attack1`→`Attack2`→`Attack3`
  encadeados por um trigger `Attack` + bool `ComboQueued`, tag `Attack` nos
  três estados usada por `IsTag("Attack")` para bloquear movimento/pulo
  durante o swing — é o análogo funcional do `is_attack_committed()` do
  Godot).
- **Câmera**: Cinemachine 3 (`CinemachineCamera` + `OrbitalFollow` +
  `RotationComposer` + `CinemachineInputAxisController`), sem script próprio.
- **Arma**: `2hand_dragon_sword.glb` posicionado manualmente como filho de um
  bone do modelo (sem sistema de attach point ainda).
- **Input**: estado híbrido — pacote New Input System instalado e um asset
  `InputSystem_Actions.inputactions` existe, mas só é consumido pela câmera
  Cinemachine; o gameplay (`SimpleNierController`) usa a API legada
  (`Input.GetAxis`/`GetButtonDown`), com `Active Input Handling = Both` no
  Project Settings.
- **Pacotes relevantes instalados e ainda não usados**: `com.unity.ai.navigation`
  (NavMesh), `com.unity.inputsystem`. **Não instalado**: Animation Rigging
  (não precisa — a locomoção usa Humanoid/Mecanim puro, sem constraints).
- **Nada além disso existe**: sem stats, sem inimigos, sem inventário, sem UI,
  sem save. Um único script no projeto inteiro.
- **Organização**: `Assets/Scripts/SimpleNierController.cs` solto, sem
  namespace, sem `.asmdef` (tudo compila em `Assembly-CSharp`).
  `Assets/Art/Animations/` (FBX Mixamo + o Animator Controller juntos),
  `Assets/Art/Weapons/` (`.glb`).
- **Git**: só existe o commit inicial (`Initial check-in`, template puro);
  todo o trabalho acima (controller, combo, câmera, arma) está **não
  commitado** ainda — vale commitar antes de começar a Fase 1.
- **Assets Mixamo já baixados mas NÃO ligados a nenhum estado do Animator**
  (achado da auditoria, prontos para a Fase 1): `Standing Dodge
  Backward/Forward/Left/Right.fbx`, `great sword power up.fbx`, `great sword
  slash.fbx` / `slash (3).fbx` / `slash (4).fbx`, `great sword slide
  attack.fbx`, `great sword jump.fbx`, `spell cast.fbx`.

---

## 2. Diferenças arquiteturais fundamentais (Godot → Unity)

Referência conceitual usada no resto deste guia — evita reexplicar em cada
fase.

| Godot | Unity | Observação |
|---|---|---|
| `Node` + `.tscn` + GDScript | `GameObject` + Prefab + `MonoBehaviour` | 1:1 direto |
| `Resource`/`.tres` (catálogos editáveis no Inspector: `ClassDef`, `AbilityDef`, `EnemyData`, `SoulTalent`, `QuestDef`) | `ScriptableObject` | Mesma motivação exata: dados versionáveis e editáveis fora de código. O Godot migrou esses catálogos de Dictionary hardcoded pra `.tres` especificamente por isso (seção "DATA MIGRATION" do CLAUDE.md) — no Unity, começar direto com `ScriptableObject` evita repetir essa migração depois. |
| Autoloads (`EventBus`, `GameManager`, `RunManager`, `MetaProgress`, `ItemDatabase`, ...) | Sem equivalente automático — usar um dos padrões: (a) singleton estático com `DontDestroyOnLoad`, ou (b) `ScriptableObject` "runtime set/event channel" (sem sequer precisar de uma cena persistente para dados puros como catálogos). | Recomendação: managers com estado de sessão (save, run, progressão) → singleton `MonoBehaviour`; catálogos de dados estáticos (itens, classes, inimigos) → não precisam de singleton nenhum, um `ScriptableObject` já é acessível globalmente sem instância. |
| Sinais (`EventBus.damage_dealt.emit(...)`) | `event Action<...>` em C#, ou `UnityEvent` se precisar ser editável no Inspector | `EventBus` do Godot vira uma classe estática com `public static event Action<...>` por sinal |
| `AnimationTree` (`BlendSpace2D` de locomoção + cadeia de `OneShot` para Attack/Block/Dodge/Ability, ver seção NIER-STYLE do CLAUDE.md) | Animator Controller: Blend Tree 1D/2D para locomoção + estados/camadas com **Avatar Mask** para ações que sobrepõem parte do corpo | Unity não tem um nó "OneShot" isolado — o equivalente funcional já usado no combo atual é uma cadeia de **estados com tag** (`Attack1/2/3` com tag `Attack`) e transições `Has Exit Time`. Ações full-body (ataque, ataque forte) = estado puro na Base Layer, como já está. Ações upper-body-only (bloqueio, no Godot com bone filter de 52 ossos sem pernas) = precisam de uma **camada extra do Animator com Avatar Mask** excluindo as pernas, não dá pra fazer só com tag. |
| Root motion **falso** — o Godot rejeitou root motion de propósito (`docs/12` §8: "root motion explicitamente rejeitado") e reconstruiu tudo via lunge/velocidade de código sincronizada ao tempo do clipe (`_start_step_lunge()`, trap #12 do CLAUDE.md: dividir a janela de tempo por CADA `AnimationNodeTimeScale` no caminho) | Root motion **real** (`OnAnimatorMove`, já em uso) | Isso é uma escolha melhor no Unity, mas significa que a lógica de "lunge"/timing do Godot **não se traduz 1:1** — ela existe pra simular o que o root motion real já dá de graça. Ao portar combate (Fase 1), não recrie os multiplicadores de tempo do Godot; leia `animator.deltaPosition` direto, do jeito que `OnAnimatorMove()` já faz. |
| `NavigationAgent3D` + bake runtime de navmesh (`RoomKit.bake_navmesh()`) | `NavMeshAgent` + `NavMeshSurface.BuildNavMesh()` em runtime (pacote `com.unity.ai.navigation`, já instalado) | Equivalente direto |
| `CharacterBody3D` + `move_and_slide()` | `CharacterController.Move()` | Já em uso |
| `group` (`"enemies"`, `"nav_source"`) | Unity `Tag` ou `Layer`, dependendo do uso (física/colisão → Layer; queries lógicas tipo "está vivo e é inimigo" → também dá pra usar uma interface `IEnemy`/componente marcador em vez de tag, mais type-safe) | Recomendação: Layer para física (`Enemy`, `Interactable`, `Loot`, mesma ideia dos 5 collision layers do Godot: world/player/enemies/loot/interactables), componente marcador (`EnemyMarker : MonoBehaviour` ou só checar `TryGetComponent<EnemyHealth>()`) para queries de gameplay |
| CSV (`items.csv`, lido por `ItemDatabase._ready()`) | Duas opções: `ScriptableObject` por item, ou `TextAsset` CSV + parser próprio | Ver decisão em aberto na Fase 4 |
| `_ready()` | `Awake()` (setup interno) / `Start()` (depende de outros objetos prontos) | |
| `_process(delta)` | `Update()` | |
| `_physics_process(delta)` | `FixedUpdate()` | Cuidado: `CharacterController.Move()` normalmente é chamado em `Update()` no Unity (não `FixedUpdate`), diferente da convenção Godot de física em `_physics_process`. O script atual já faz isso certo (`Update()` + `OnAnimatorMove()`, que a Unity chama automaticamente após o Animator avançar). |
| `class_name X` (GDScript) | `public class X` + namespace | Godot não usa namespaces; proponho adotar `Babel.*` no C# desde já (ver seção 3) |

---

## 3. Convenções a estabelecer no Unity (antes de escalar)

O projeto Unity está no estágio ideal pra fixar isso — só existe 1 script,
então não há nada de errado a migrar ainda, só a decidir pra frente.

### 3.1 Estrutura de pastas proposta

```
Assets/
  Art/
    Animations/        (existente — FBX Mixamo crus + Animator Controllers)
    Weapons/            (existente)
    Characters/          (futuro: modelos de inimigos/NPCs)
  Scripts/
    Player/              (SimpleNierController e o que ele virar; movimento, combate do jogador)
    Combat/               (hit detection, dano, CombatFX equivalente, stats)
    Classes/               (ClassDef/AbilityDef ScriptableObjects, ClassKit por classe)
    Enemies/                (EnemyBase equivalente, IA)
    Items/                   (catálogo de itens, inventário, efeitos)
    Rooms/                    (RoomBase/RoomPrefab equivalente, gerador de andar)
    Meta/                      (RunManager, MetaProgress, save)
    Dialogue/                   (quests, diálogo)
    UI/                          (HUD, menus)
    Core/                         (EventBus, utilitários compartilhados)
  Data/                    (assets de ScriptableObject instanciados: ClassCatalog, ItemCatalog, EnemyData por mob...)
  Scenes/
  Settings/
```

Isso espelha a divisão que o CLAUDE.md do Godot já documenta como
funcionando bem (`actors/`, `data/`, `systems/`, `world/`, `ui/`,
`autoload/`) — não precisa reinventar a divisão, só traduzir pra convenção
de pasta Unity.

### 3.2 Namespace

Proposta: `Babel.Player`, `Babel.Combat`, `Babel.Enemies`, `Babel.Items`,
`Babel.Rooms`, `Babel.Meta`, `Babel.Dialogue`, `Babel.UI`, `Babel.Core` —
espelhando a estrutura de pastas acima. `SimpleNierController` hoje está no
namespace global; mover para `Babel.Player` é uma tarefa de baixo risco pra
fazer logo no início da Fase 1 (evita que o problema cresça conforme mais
scripts chegam).

### 3.3 Decisão pendente: Input System

**Estado atual**: gameplay usa `Input.GetAxis`/`Input.GetButtonDown`
(legado), com `Active Input Handling = Both`. Existe um
`InputSystem_Actions.inputactions` não usado pelo gameplay (só pela câmera).

**Recomendação**: migrar para o New Input System (Action Maps) antes da
Fase 9 (gamepad) — idealmente já na Fase 1, enquanto o escopo de input ainda
é pequeno (Move/Look/Attack/Jump). Motivo: o Godot tem suporte robusto e
maduro a dual-scheme kbm+gamepad (`InputMapSetup` autoload, seções GAMEPAD
PASS / PASS 2 do CLAUDE.md — botões compartilhados contextual, radiais
LT/RT, navegação de menu por `PadNav`), e replicar esse nível de polimento
emulando com `Input.GetAxis` por cima de `Both` vai ficar cada vez mais
frágil conforme o número de ações cresce (hoje já são Move, Look, Attack,
Interact, Crouch, Jump, Previous, Next, Sprint no asset gerado, mais o que
vier de Bloqueio/Dodge/Ataque Forte/Hotbar). Migrar cedo, com poucas ações,
é bem mais barato que migrar tarde com 20+.

**Isso é uma decisão do usuário a confirmar antes da Fase 1** — o guia
assume a recomendação acima, mas sinalizar aqui explicitamente porque é uma
mudança que toca o único script existente.

### 3.4 Git

Commitar o estado atual (controller, combo, câmera, arma, mudanças de
Project Settings) antes de iniciar a Fase 1 — hoje só existe o commit
`Initial check-in` do template; todo o trabalho real ainda não está versionado.

---

## 4. Roteiro de migração faseado

Ordem por dependência: combate do jogador precisa existir antes de inimigos
terem algo pra lutar contra; classes/stats precisam existir antes de itens
darem bônus a eles; salas precisam existir antes do loop roguelike; etc.

### Fase 0 — Feito (baseline, não replanejar)
Locomoção root motion (Idle/Run), Jump, combo básico de 3 hits, câmera
Cinemachine, socket manual de arma. Descrito na seção 1.2 acima.

### Fase 1 — Fundamentos de combate do jogador
**Godot de referência**: seções COMBAT & SKILLS OVERHAUL, MELEE POINT-BLANK
FIX, NIER-STYLE COMBAT PASS, STRONG ATTACK do CLAUDE.md;
`actors/player/combat_component.gd`, `movement_component.gd`.

**O que portar**:
- Dodge/roll com i-frames — assets já importados (`Standing Dodge
  Backward/Forward/Left/Right.fbx`), faltam: estado(s) no Animator, lógica de
  invulnerabilidade por janela de tempo, escolha de clipe por direção do
  input relativa ao facing atual (mesmo critério do Godot:
  `_dodge_direction_key()` classifica em forward/backward/left/right pelo
  eixo dominante).
- Bloqueio — precisa de uma **camada de Animator com Avatar Mask**
  upper-body (ver seção 2), já que no Godot é filtro de bones sem pernas.
  Redução de dano percentual + barra de segmentos (pool de "stamina"
  reutilizável também pelo dodge, como no Godot).
- Ataque forte — clipe `great sword slide attack.fbx`/`great sword power
  up.fbx` já disponíveis; ataque parado (rooted) com dano em área circular
  tangente ao jogador (`center = player + facing_dir * radius`, mesma
  geometria do Godot).
- Hit detection: `Physics.OverlapSphere`/`OverlapBox` no frame de impacto
  (equivalente a `enemies_in_melee_cone`/`enemies_in_radius`/
  `enemies_in_rect` do Godot) — ler o timing do impacto direto do clipe
  (`AnimationEvent` no meio da animação é o equivalente Unity mais robusto
  ao `combo_segment_hit_times` do Godot, mais confiável que medir por
  `normalizedTime` a cada frame).
- Health/dano básico (`HealthComponent` simples: `CurrentHP`, `TakeDamage()`,
  evento de morte) — pré-requisito de qualquer coisa que bata em algo.

**Armadilhas a observar**:
- Não recriar o sistema de "lunge" do Godot como está — o Unity já ganha
  deslocamento de graça pelo root motion real (`animator.deltaPosition`).
  Se precisar de mais alcance que o baked no clipe, ajustar a curva/escala
  do próprio clipe ou aplicar um multiplicador simples ao root motion durante
  a janela de ataque, não recriar `_start_step_lunge()` inteiro.
- O bloqueio PRECISA de Avatar Mask (camada separada), não dá pra usar tag
  simples como o combo atual — senão as pernas ficam presas na pose de
  bloqueio e o personagem não anda enquanto segura o bloqueio (que é o
  comportamento certo do Godot: "you can keep walking while blocking but not
  while swinging").

**Checklist**:
- [ ] Decidir e migrar Input System (seção 3.3) antes ou junto desta fase
- [ ] `HealthComponent` (dano, morte, evento)
- [ ] Dodge (4 estados direcionais + i-frames)
- [ ] Bloqueio (camada Avatar Mask upper-body + redução de dano + stamina)
- [ ] Ataque forte (estado rooted + hit circle)
- [ ] Hit detection genérica via `AnimationEvent` + `OverlapSphere/Box`
- [ ] Mover `SimpleNierController` para `Assets/Scripts/Player/` + namespace `Babel.Player`

### Fase 2 — Stats, classes e habilidades
**Godot de referência**: seções RIG/MORPHS/TALENTS (R3), ABILITY-ANIM
MARSHALLING FIX; `systems/progression/character_stats.gd`,
`data/classes/class_db.gd`, `data/resource_scripts/{class_def,ability_def,
ability_morph}.gd`, `actors/player/class_kit.gd` + `*_kit.gd`.

**Arquitetura alvo**:
- `CharacterStats` (MonoBehaviour ou classe de dados pura) — atributos base +
  bônus percentuais de equipamento, espelhando `CharacterStats.total(attr) =
  base * (1 + equip_bonus_sum%)`.
- `ClassDef` : `ScriptableObject` — nome, família (cosmético, sem carga de
  lore "living/undead", ver seção do CLAUDE.md "LORE & PLANNING DOCS
  REPLACED": é só uma tag de tint hoje), lista de `AbilityDef` (3 habilidades
  + a Skill 1/combo universal já feita na Fase 0-1), flags de bloqueio/ataque
  forte por classe.
- `AbilityDef` : `ScriptableObject` — id, nome, desc, cooldown, custo, forma
  de hit-area (cone/circle/rect + range/angle/width, direto do padrão
  "Hit area" do Godot), clipe de animação, morphs (lista aninhada, igual ao
  Godot desde que ele passou a guardar morphs dentro do próprio
  `AbilityDef` em vez de catálogo separado).
- `ClassKit` : classe C# abstrata com `ExecuteAbility(int slot, ...)` — uma
  implementação concreta por classe (`PaladinKit`, `RangerKit`, ...), 100%
  análogo ao dispatch de `ClassKit` do Godot (é uma tabela de dispatch por
  id, não um switch gigante).
- Morphs/Talentos: mesma ideia de "escolha, não ponto numérico" — ao
  level-up, sortear 3 opções entre "desbloquear habilidade" ou "próximo
  morph de uma já desbloqueada".

**Armadilha a observar (trap #14 do Godot, evitar recriar)**: no Godot, um
novo campo em `AbilityDef`/`ClassDef` fica **invisível** para todo consumidor
até alguém copiar manualmente pra dois Dictionaries literais em
`class_db.gd` — isso já causou um bug real (`anim_clip` "morto" por meses).
No Unity, como `ScriptableObject` é a fonte de dados direta (sem uma camada
intermediária de Dictionary reconstruída à mão), esse bug de classe inteira
não deveria existir — **não introduza uma camada de tradução
Resource→Dictionary equivalente só por semelhança com o Godot**; deixe o
`ScriptableObject` ser lido diretamente pelos consumidores.

**Checklist**:
- [ ] `CharacterStats`
- [ ] `ClassDef`/`AbilityDef` ScriptableObjects (começar só com a classe já
      jogável no Unity, expandir pras outras 5 depois)
- [ ] `ClassKit` base + 1 implementação concreta
- [ ] Fluxo de level-up com escolha de 3 opções (unlock/morph)
- [ ] Sistema de Soul Talents (passivos)

### Fase 3 — Inimigos e IA
**Godot de referência**: `actors/enemies/enemy_base.gd`,
`data/resource_scripts/enemy_data.gd`, `docs/09_*`.

**Arquitetura alvo**:
- `EnemyData` : `ScriptableObject` — stats, telegraph (`aoeShape`
  circle/rect/cone, `aoeDelay`, `aoeChance`), velocidade, XP/Seeds. Um asset
  por mob, igual ao Godot.
- `EnemyBase` : `MonoBehaviour` — state machine (Idle/Chase/Attack/Dead) +
  `NavMeshAgent` (pacote já instalado, nunca usado ainda) + telegraph
  dodgeable (renderizar a forma no chão, dano só aplica depois do delay).
  Subclasses finas por mob só sobrescrevem/configuram `EnemyData`, mesma
  filosofia "template + `_configure()`" do Godot.
- Bake de NavMesh em runtime por sala/andar (`NavMeshSurface.BuildNavMesh()`
  chamado depois de montar a geometria da sala — equivalente a
  `RoomKit.bake_navmesh()`).

**Checklist**:
- [ ] `EnemyData` ScriptableObject
- [ ] `EnemyBase` com state machine + NavMeshAgent
- [ ] Telegraph dodgeable (pelo menos forma circle, que é o mais comum no Godot)
- [ ] 1 inimigo concreto de teste (equivalente ao Husk)
- [ ] Bake de NavMesh em runtime

### Fase 4 — Itens, inventário, equipamento
**Godot de referência**: `docs/11_Guia_Itens.md`, `data/items/items.csv`,
`systems/inventory/`.

**Decisão em aberto**: CSV (`TextAsset` + parser, paridade 1:1 de workflow
com o Godot — Gustavo edita uma planilha) vs. `ScriptableObject` por item
(mais idiomático em Unity, edição via Inspector, mas perde a edição em
massa por planilha). Recomendação: se Gustavo (responsável por conteúdo, ver
`docs/08` do Godot) já está confortável editando `items.csv`, manter CSV
reduz atrito de fluxo de trabalho; caso contrário, `ScriptableObject`
consistente com Fase 2/3.

**Arquitetura alvo** (qualquer que seja a decisão acima):
- `ItemDatabase` — catálogo carregado uma vez, análogo ao autoload
  `ItemDatabase` do Godot.
- `ItemEffect` — classe C# abstrata + implementações concretas (crit chance,
  max health, basic damage, stamina regen, lifesteal — os mesmos 5 do
  Godot), aplicadas como bônus percentual em `CharacterStats`.
- `Inventory` — componente no jogador, equipar/desequipar/usar.
- Regra de economia a preservar: equipamento só dropa de bosses; mobs comuns
  não dropam nada (decisão de design do Godot, não uma limitação técnica —
  preservar a menos que o design mude).

**Checklist**:
- [ ] Decidir CSV vs. ScriptableObject com o time
- [ ] `ItemDatabase` + schema de item
- [ ] `ItemEffect` base + as 5 implementações
- [ ] `Inventory` no jogador
- [ ] Loot drop de boss

### Fase 5 — Salas e andares procedurais
**Godot de referência**: `docs/09_*`, `world/rooms/`,
`world/generated/floor_assembler.gd`.

**Arquitetura alvo**:
- `RoomPrefab` — Prefab de sala autorado (paredes/porta por lado, seguindo a
  convenção Wall*/Door* N-S-E-W do Godot) + Transforms vazios tipados como
  markers (`SpawnMarker`, `Anchor` — um enum/componente marcador simples,
  análogo ao `Marker3D` com dropdown de tier do Godot).
- `RoomBase` — classe C# base com `Populate()`, subclasses por tipo
  (`CombatRoom`, `ChestRoom`, `RestRoom`, `ChallengeRoom`, `SwarmRoom`),
  mesma divisão do Godot.
- `FloorGenerator` — equivalente ao `FloorAssembler`: random walk
  self-avoiding em grid, conecta com corredores, posiciona entrada/saída,
  seed determinística (`runSeed * 1000 + floor`, mesma fórmula do Godot serve
  de referência).
- Regra de design a preservar: só a disposição/inimigos/loot são
  randomizados — a geometria de cada sala é sempre handcrafted (nunca gerar
  paredes proceduralmente).

**Checklist**:
- [ ] `RoomPrefab` + convenção de markers
- [ ] `RoomBase` + 1 subclasse concreta (CombatRoom)
- [ ] `FloorGenerator` (grid walk + corredores + stairs)
- [ ] Bake de NavMesh por sala/andar (reusa o que a Fase 3 já fez)

### Fase 6 — Loop roguelike e meta-progressão
**Godot de referência**: seção META-PROGRESSÃO do CLAUDE.md,
`autoload/run_manager.gd`, `autoload/meta_progress.gd`.

**Arquitetura alvo**:
- `RunManager` (singleton) — start/climb/descend, morte reseta a run.
- `MetaProgress` (singleton, persistido em arquivo próprio — `JsonUtility`+
  `File.WriteAllText` em `Application.persistentDataPath` é o equivalente
  direto do `user://babel_meta.json` do Godot) — moeda (Seeds), unlocks
  permanentes, itens/materiais persistentes entre runs.
- Cena de hub + `SaveManager` (slots de save de personagem, independente do
  save de meta-progressão — mesma separação do Godot).

**Checklist**:
- [ ] `RunManager`
- [ ] `MetaProgress` + persistência em arquivo
- [ ] Cena de hub mínima
- [ ] `SaveManager` (slots)

### Fase 7 — Quests e diálogo
**Godot de referência**: `docs/14_Guia_Quests_e_Dialogos.md`,
`data/quests/`, `data/dialogue/`.

**Arquitetura alvo**: catálogo de quests/diálogo (ScriptableObject ou JSON —
mesma decisão de trade-off da Fase 4, e o Godot documenta essa mesma tensão:
diálogo ficou de fora da migração pra `.tres` porque árvores ramificadas não
encaixam bem em Resource puro; provavelmente vale um formato próprio tipo
JSON/YAML aqui também no Unity), `QuestManager`/`DialogueManager`
singletons, NPC genérico com `npcId` (igual ao `npc.tscn` do Godot — script
único reutilizável, sem script por NPC).

**Regra a preservar**: recompensas de quest sempre vêm de uma única fonte
(`QuestDef.rewardItems`) — nunca hardcode dar item em outro lugar (essa é
literalmente a trap #1 documentada no Godot).

**Checklist**:
- [ ] Schema de quest (objetivos tipo "kill" pelo menos)
- [ ] Schema de diálogo (árvore de nós + condições + ações)
- [ ] `QuestManager`/`DialogueManager`
- [ ] NPC genérico reutilizável

### Fase 8 — UI/HUD
**Godot de referência**: `ui/hud/combat_hud.gd`, `ui/character_menu.gd`.

**Arquitetura alvo**: HUD de combate (barras de vida/stamina, hotbar,
buffs, dano flutuante) via UGUI ou UI Toolkit (decisão a tomar na hora,
fora do escopo deste guia geral), menu de personagem com abas, hotbar com
drag-and-drop.

**Checklist**:
- [ ] Decisão UGUI vs. UI Toolkit
- [ ] HUD de combate mínimo (vida/stamina/hotbar)
- [ ] Menu de personagem

### Fase 9 — Suporte a gamepad
**Godot de referência**: seções GAMEPAD PASS / PASS 2 do CLAUDE.md.

Só faz sentido depois da Fase 3 do Input System já estar migrado (seção
3.3) — Action Maps completos (kbm + gamepad), navegação de menu por
controle (componente `MenuCursor` análogo ao `PadNav` do Godot: cursor único
por janela, dirigido por `_input()` antes da camada de foco de UI, pra não
competir com ações de gameplay no D-pad).

**Checklist**:
- [ ] Action Maps completos (todas as ações de gameplay)
- [ ] Lock-on/target cycling por stick
- [ ] `MenuCursor` para navegação de UI por controle
- [ ] Rebind visual dos prompts (ícone kbm vs. gamepad, análogo ao `InputHints`)

---

## 5. Glossário rápido Godot → Unity

| Godot | Unity |
|---|---|
| `Node` | `GameObject` + `Component` |
| `.tscn` | Prefab |
| `Resource` / `.tres` | `ScriptableObject` |
| Autoload (singleton global) | Singleton `MonoBehaviour` com `DontDestroyOnLoad`, ou acesso estático direto pra dados puros |
| Signal (`signal foo; foo.emit(x)`) | `event Action<T> Foo;` |
| `_ready()` | `Awake()` / `Start()` |
| `_process(delta)` | `Update()` |
| `_physics_process(delta)` | `FixedUpdate()` (mas `CharacterController.Move()` roda em `Update()`, não `FixedUpdate()`) |
| `CharacterBody3D` + `move_and_slide()` | `CharacterController.Move()` |
| `NavigationAgent3D` | `NavMeshAgent` |
| `AnimationTree` (BlendSpace2D + OneShot chain) | Animator Controller (Blend Tree + estados com tag/Avatar Mask layers) |
| `group` (`"enemies"`) | `Tag` (física/gameplay simples) ou `Layer` (colisão) ou componente marcador |
| `Marker3D` | `Transform` vazio + componente marcador |
| `user://arquivo.json` | `Application.persistentDataPath` + `File.WriteAllText`/`JsonUtility` |
| `EventBus` autoload | Classe estática com `event Action<...>` por evento |
| `Engine.time_scale` (hit-stop) | `Time.timeScale` |
| Bone filter (bloqueio upper-body) | Camada extra do Animator + Avatar Mask |

---

## 6. Decisões em aberto / riscos a validar com o usuário

1. **Input System**: migrar para o New Input System agora (recomendado,
   seção 3.3) ou manter `Input.GetAxis` legado por mais tempo?
2. **Root motion vs. lunge de código**: confirmar que o Unity vai usar root
   motion real para TODAS as ações de combate futuras (dodge, ataque forte,
   habilidades), não só o combo básico — evita reintroduzir a complexidade
   de timing do Godot desnecessariamente.
3. **Catálogo de itens**: CSV (paridade de fluxo com Gustavo) ou
   `ScriptableObject` (mais idiomático Unity)? Mesma pergunta se aplica ao
   catálogo de quests/diálogo na Fase 7.
4. **Prioridade após este guia**: a ordem de fases acima segue dependência
   técnica (combate → classes → inimigos → itens → salas → loop → quests →
   UI → gamepad), mas o usuário pode preferir atacar em outra ordem (ex.:
   inimigos antes de classes, se quiser algo pra bater logo). Cada fase foi
   escrita pra ser relativamente independente das seguintes, então reordenar
   é viável.
5. **Escopo de classes**: o Godot tem 6 classes standalone mas só 1
   (Paladin) tem kit+morphs completos; vale decidir se o Unity replica as 6
   desde já ou foca em 1-2 primeiro (o próprio backlog do Godot já cogita
   "cortar pra 2 classes", ver `docs/00_README.md`).
