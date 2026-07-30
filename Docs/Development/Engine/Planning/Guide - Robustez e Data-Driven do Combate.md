# Guia — Robustez e Data-Driven do Combate (polimento, não é bloqueante)

## Contexto

Depois de fechar o core de combate do player (locomoção, combo com branch
Attack2Alt, dodge com cancel universal, lock-on estilo KH/Nier, câmera,
vida/dano — todos os outros guias desta pasta) ficou claro um padrão: a
maioria dos bugs caçados durante essa passada foram da MESMA classe —
corrida de 1-frame entre C# e o Animator (trigger pendurado sem transição
pra consumir, `GetCurrentAnimatorStateInfo` reportando o estado de ORIGEM
durante crossfade). Isso não é sintoma de má animação, é o preço de
orquestrar Mecanim multi-layer via trigger cru, meio ad hoc, cada
verificação reimplementada onde foi precisa.

Este guia não é bloqueante — o jogo funciona hoje. É pra quando quiser
fechar essa fundação **antes** de escalar conteúdo (segunda arma, inimigo
de verdade com IA, mais habilidades), porque cada estado/transição novo é
mais superfície pra esse tipo de corrida acontecer.

---

> **Atualização (passada de robustez do combate)**: os itens 2 e 6 deste
> guia foram implementados, mais três correções de bug que caíram na mesma
> passada. Ver as seções marcadas ✅ e a nova seção 7 (wiring de Editor
> pendente pro `AttackQueued`).

## 1. Fila em vez de descarte, pra qualquer input que possa cair num frame bloqueado

**Problema**: `equipAction.WasPressedThisFrame()` (e qualquer `WasPressedThisFrame()`
em geral) é um pulso de UM frame. Se esse frame coincidir com um gate
bloqueado (`IsAttacking()`/`IsJumping()`/`IsDodging()`/`IsTransitioning`), o
input é descartado pra sempre — nada reexecuta depois que o bloqueio
termina. Foi exatamente isso que causou o "às vezes não roda o Draw" (já
corrigido no Equip via `pendingToggle`, ver `WeaponEquipController.cs`).

**Onde mais isso vale a pena**: o combo (`comboQueued`/`strongComboQueued`)
já usa esse padrão (bool persistente lido pelo Animator via condição, em
vez de trigger cru) — é o modelo a seguir. `HandleDodge()` e
`HandleJumpAttack()` ainda disparam `SetTrigger` direto no momento do
input, sem fila. Se algum dia aparecer um "às vezes o dodge não sai", esse
é o primeiro lugar a olhar — aplicar o mesmo padrão do `pendingToggle`.

**Solução proposta**: extrair um pequeno helper reaproveitável (ex.:
`PendingAction` — bool + método `TryConsume(bool gateOpen)`) em vez de
copiar a lógica de `pendingToggle` à mão em cada `Handle*()` que precisar.

---

## 2. Checagem "estado ativo OU a caminho" centralizada — ✅ FEITO

> **Implementado** em `Assets/Scripts/Combat/AnimatorStateUtil.cs`
> (`HasTagNowOrIncoming` / `HasStateNowOrIncoming` / `EffectiveStateHash`).
> As 4 implementações duplicadas foram trocadas por chamadas a ele, e as
> checagens de `PlayerController` que NÃO tinham a proteção passaram a ter:
> `IsAttacking()`, `IsStrongAttacking()`, `IsInCommittedAttack1Alt()`,
> `IsJumpAttacking()`. Isso não foi refactor puro no fim das contas — a
> falta da proteção em `IsAttacking()` era bug real: as transições de
> entrada nos ataques têm 0.25s de crossfade e nesse intervalo ela
> respondia `false`, fazendo o spam de ataque cair no branch "começar
> ataque novo" (re-disparando o trigger e o snap de rotação do lock-on)
> em vez de "enfileirar combo".
>
> Continuam usando `GetCurrentAnimatorStateInfo` cru de propósito os três
> pontos que precisam de `normalizedTime` (só existe no estado atual):
> `IsSlideAttacking()`, o handoff em `HandleJumpAttack()` e o
> `OnAnimatorMove()`.

### Texto original (contexto)


**Problema**: `GetCurrentAnimatorStateInfo` só reflete o estado de ORIGEM
durante uma transição — isso já é sabido e tratado em dois lugares
diferentes (`WeaponEquipController.LayerHasTagNowOrIncoming`/
`LayerHasStateNowOrIncoming`, `PlayerController.GetEffectiveBaseStateHash`/
`IsUpperBodyEnteringOrIn`), cada um reimplementado à mão. A prova de que
isso é frágil: a primeira versão de `IsJumpAttacking()` não tinha essa
proteção e causou um bug real (perna dessincronizada do braço); o mesmo
buraco existia, sem ninguém notar, no `upperBodyStillGripping` do
`WeaponEquipController` até eu procurar de propósito.

**Solução proposta**: extrair uma classe estática/utilitária única (ex.:
`Assets/Scripts/Combat/AnimatorStateUtil.cs`, sem instanciar, só métodos
estáticos) com:

```csharp
public static bool HasTagNowOrIncoming(Animator animator, int layer, string tag)
public static bool HasStateNowOrIncoming(Animator animator, int layer, string stateName)
public static int EffectiveStateHash(Animator animator, int layer) // pra reset de combo-queue
```

E trocar as 4 implementações duplicadas (2 em cada script) por chamadas
pra isso. Zero mudança de comportamento, só elimina a duplicação — o
próximo lugar que precisar dessa checagem não corre o risco de esquecer a
proteção.

---

## 3. Nomes de estado/parâmetro como constantes centralizadas — ✅ FEITO

> **Implementado** em `Assets/Scripts/Combat/AnimStrings.cs`, exatamente
> como proposto abaixo. Escopo: só os literais hardcoded em
> `PlayerController` (~30 call sites). Os campos de `WeaponEquipController`
> ficaram de fora **de propósito** — são `[SerializeField]`, ou seja, já
> são um ponto de configuração único por instância (não uma cópia solta),
> e existem justamente pra permitir reapontar pra um rig de arma diferente
> sem mudar código (ver o próprio comentário de cabeçalho da classe).
> Convertê-los pra constante compile-time destruiria essa reusabilidade.
>
> Nenhum outro script no projeto fala com o Animator via string crua hoje
> (`PlayerAttackHitbox`, `CameraLockOnController`, `TargetingSystem` não
> tocam nele) — `AnimStrings` cobre 100% da superfície atual.

### Texto original (contexto)



**Problema**: cada script tem sua própria cópia de strings tipo
`"ArmedDodgeGrip"`, `"Attack2Alt"`, `"Dodging"` — e a única "verificação"
de que elas batem com o Animator é rodar o jogo e ver se quebra. Foi assim
que o "a" sobrando em `ArmedJumpAttack2Alta` passou despercebido, e foi
assim que a renomeação `StrongAttack` → `Attack2Alt` exigiu caçar cada
referência manualmente.

**Solução proposta**: uma classe estática só com constantes
(`Assets/Scripts/Combat/AnimStrings.cs`):

```csharp
public static class AnimStrings
{
    public const string Dodging = "Dodging";
    public const string Jumping = "Jumping";
    public const string Attack = "Attack";
    public const string ArmedDodgeGrip = "ArmedDodgeGrip";
    public const string ArmedJumpGrip = "ArmedJumpGrip";
    public const string Attack2Alt = "Attack2Alt";
    public const string Attack2AltTail = "Attack2AltTail";
    public const string ArmedJumpAttack2Alt = "ArmedJumpAttack2Alt";
    // ... etc, uma entrada por string hoje espalhada em [SerializeField]
    // string ou literal direto no código
}
```

Isso não impede sozinho um erro de digitação, mas concentra o ponto de
verdade num lugar só — uma renomeação futura vira "Find All References"
em vez de grep manual, e o autocomplete do editor de código já pega
digitação errada (`AnimStrings.ArmedDogdeGrip` não compila; a string solta
`"ArmedDogdeGrip"` compila e falha calado em runtime).

---

## 4. Data-driven por arma — Animator Override Controller + ScriptableObject

**Motivação**: hoje cada estado/transição foi construído à mão no grafo
pra UMA arma (espada de duas mãos). Uma segunda arma exigiria reconstruir
boa parte disso do zero — não escala. O projeto Godot de referência já
resolve isso via dado (`ClassDef.strong_attack_anim` etc.); dá pra chegar
num resultado equivalente sem reescrever o C# que já existe.

**Solução proposta** (incremental, reaproveita 100% do grafo atual):

1. **Animator Override Controller** por arma: o `PlayerAnimatorController`
   atual vira o "base" — estados/transições/parâmetros continuam
   exatamente como estão (Attack1/2/3/Attack2Alt/Dodge/Jump/etc. como
   slots genéricos). Pra cada arma nova, criar um
   `AnimatorOverrideController` (`Assets > Create > Animator Override
   Controller`) que só remapeia QUAL CLIPE toca em cada slot (ex.: Attack1
   → `DaggerSlash1` em vez de `GreatSwordTripleSlash1`). Trocar o
   Controller do `Animator` component em runtime
   (`animator.runtimeAnimatorController = weaponOverride`) na hora de
   trocar de arma.
2. **`WeaponMoveset` ScriptableObject** — um asset por arma guardando o
   que hoje está hardcoded/espalhado: `AnimatorOverrideController`
   correspondente, valores de dano por golpe (hoje só existem como
   parâmetro float direto no Animation Event — mover pra cá desacopla
   balanceamento de precisar editar clipe), talvez `hitRange`/`hitRadius`
   diferentes por arma pro `PlayerAttackHitbox`.
3. **`PlayerAttackHitbox.OnAttackHit(float damage)`** já recebe o dano
   como parâmetro do Animation Event — nesse mundo data-driven, o valor
   viria do `WeaponMoveset` atual (indexado por qual golpe está
   acontecendo) em vez de um float fixo por Animation Event, mas isso é
   opcional/incremental — o Animation Event com float direto já funciona
   e pode continuar assim se não quiser ir tão fundo agora.

**Escopo sugerido pra primeira tentativa**: não precisa fazer os 3 passos
de uma vez — só o Override Controller (passo 1) já prova o conceito e
resolve a maior dor (repetir o grafo inteiro por arma) sem tocar em nada
do que já funciona.

**Skill já criada pra automatizar o passo 1**:
`.claude/skills/unity-weapon-animator-override/` gera o Override
Controller a partir de um mapeamento clipe-original→clipe-novo, sem
precisar arrastar clipe por clipe na GUI. Ver o `SKILL.md` de lá pro
workflow completo (inclusive como achar o nome real de um sub-clipe de
FBX só com Grep, sem abrir o Unity).

### 4b. Segunda arma **coexistindo** com a primeira (troca durante o jogo)

Diferente de só *variar* a animação (4.1-4.3, que assume uma arma
substituindo a outra) — se a ideia é o personagem carregar **duas armas
e trocar entre elas em jogo** (espada E adaga, por exemplo), tem peças
físicas e de lógica que não existem ainda:

**Físico, por arma nova** (nenhum código, só Editor):
1. Importar o modelo 3D da arma nova (malha estática, mesmo tipo de asset
   que a espada de duas mãos já é).
2. Criar um par de sockets próprio (`DaggerSheathSocket`/
   `DaggerWieldSocket`, ou nome equivalente) como Transforms filhos dos
   bones certos — não necessariamente os mesmos bones/posições da
   espada (ex.: adaga costuma ficar no quadril/cinto, não nas costas).
   Ajustar posição/rotação do socket parenteando a arma nele
   temporariamente, mesma técnica já usada pra espada.
3. Adicionar uma **segunda instância** de `WeaponEquipController` no
   Player (mesma classe, já reaproveitável — o comentário original do
   script já previa isso), com `weapon`/`sheathSocket`/`wieldSocket`
   apontando pra arma nova.

**Lógico, novo (ainda não existe)**:
1. **Conceito de "arma ativa"** — hoje `PlayerController` tem um campo
   fixo `weaponEquip` apontando pra UMA instância. Com duas armas, isso
   precisa virar "qual das duas está ativa agora" (índice/enum + troca
   de referência), não um campo fixo.
2. **Input de troca de arma** — não existe ação nenhuma hoje pra isso
   (D-pad, botão dedicado, o que fizer sentido).
3. **Exclusão mútua** — só uma arma pode estar `Wielded` por vez; trocar
   com uma arma sacada devia forçar ela a embainhar primeiro (ou só
   permitir a troca com as duas `Sheathed`, mais simples e mais seguro
   contra crossfade no meio de um golpe — mesmo espírito do
   `IsAttacking()`/`IsJumping()`/`IsDodging()` já usados pra gatear
   Draw/Sheath).
4. **Trocar `animator.runtimeAnimatorController`** junto com a arma ativa
   — é aqui que o Override Controller do item 4 entra: ao trocar de
   arma, troca também qual override está tocando, pra Attack1/2/3/
   Attack2Alt/etc. tocarem os clipes certos pra arma que acabou de ser
   sacada.

Isso é bem mais escopo que só "variar animação" — só vale a pena entrar
nisso quando o jogo realmente precisar de troca de arma em jogo, não como
parte automática de toda arma nova.

---

## 5. Validação em Editor pra pegar erro de nome/estado cedo

**Ideia** (menor prioridade, mas barata): um `MenuItem` ou
`[InitializeOnLoad]` simples que confere, contra o
`PlayerAnimatorController` real, se todas as strings de `AnimStrings`
(item 3) existem como state/parameter — acusando no Console, em tempo de
Editor, exatamente o tipo de erro que hoje só aparece jogando (a "letra a"
sobrando, o parâmetro que não existe). Não precisa ser sofisticado — um
script de editor que itera `AnimatorController.parameters` e os
state machines das layers, comparando com a lista de constantes.

---

## 6. Consolidar leitura duplicada de Animator entre os dois scripts — ✅ FEITO (parcial)

> **Implementado** na medida prevista pelo próprio texto abaixo ("combinado
> com o item 2, isso já reduz bastante o risco"): os dois scripts agora
> derivam esses fatos pelo mesmo `AnimatorStateUtil`, então um fix na
> proteção de crossfade vale pros dois automaticamente. O passo mais
> ambicioso (um componente "leitor de estado" único que os dois consultam)
> continua não feito — e continua sendo nice-to-have, não bloqueante.

### Texto original (contexto)


`PlayerController` e `WeaponEquipController` cada um deriva
independentemente fatos parecidos sobre o mesmo Animator (que estado a
layer base está, que estado a UpperBody está). Um fix aplicado num
(a proteção de crossfade, por exemplo) não propaga pro outro
automaticamente — foi exatamente o que aconteceu hoje. Combinado com o
item 2 (utilitário compartilhado), isso já reduz bastante o risco; se
quiser ir além, dá pra ter um único componente "leitor de estado" que os
dois consultam em vez de cada um chamar `GetCurrentAnimatorStateInfo`
direto.

---

## 0. Bug novo encontrado e corrigido — corrida de `Update()` entre componentes

Não era nenhum dos 6 itens originais, mas é da mesma família (item 1/2):
**a Unity não garante ordem de execução entre `Update()` de scripts
diferentes.**

Sintoma: apertar Attack (Square) com a arma guardada parou de sacar —
`RequestDraw()` era chamado, passava por todos os gates, chamava
`TriggerDraw()`... e nada acontecia. O toggle de Equip dedicado continuava
funcionando normal.

Causa: `WeaponEquipController` resetava o trigger de Draw/Sheath no início
do seu próprio `Update()`, um frame depois de setar — seguro quando
`TriggerDraw()` era chamado de DENTRO do próprio `Update()`
(`HandleToggleInput()`, o caminho do Equip), porque aí o reset daquele
frame já tinha rodado antes do trigger existir. Mas `RequestDraw()` é
chamado de FORA, por `PlayerController` — um componente diferente. Se
`PlayerController.Update()` rodar antes de `WeaponEquipController.Update()`
no mesmo frame (o que pode mudar sozinho a cada recompilação de script,
sem nenhuma config explícita de ordem), a sequência vira: trigger setado →
mesmo frame, `WeaponEquipController.Update()` roda e já vê o pending reset
→ reseta o trigger antes do Animator jamais ter avaliado ele. Nasce e
morre no mesmo frame, sem erro nenhum no Console.

**Fix**: o reset saiu do início do `Update()` e virou `LateUpdate()` —
garantido pela Unity rodar depois de TODOS os `Update()` do frame e depois
do Animator já ter avaliado, independente de qual script roda primeiro.

**Lição pro resto do projeto**: qualquer padrão "seto agora, componente
resolve daqui a pouco" que dependa de UM ÚNICO `Update()` pra timing (não
só reset de trigger) é candidato ao mesmo bug se algum dia for chamado de
fora do próprio script. Vale revisar se aparecer sintoma parecido em outro
lugar (ex.: os `pending*` do combo, hoje só chamados internamente).

## 7. Wiring de Editor pendente — parâmetro `AttackQueued` (Dodge)

Correção do bug "personagem rola tremendo ao spammar ataque". O código já
enfileira (`PlayerController` seta o bool `AttackQueued`), mas o Animator
ainda precisa consumi-lo:

1. **Parameters**: criar `AttackQueued` (Bool).
2. Nova transição **`Dodge -> Attack1`**: Has Exit Time **on**, Exit Time
   **0.95** (mesmo das outras saídas do Dodge), condição
   `AttackQueued == true`. Deixar **primeira** na lista do estado `Dodge`.
3. Nas 3 transições de saída que o `Dodge` já tem, adicionar
   `AttackQueued == false` como condição extra — mesmo padrão que o
   `DodgeQueued` usa no `Attack1Alt1`/`Attack1Alt2` (deixa as saídas
   mutuamente exclusivas em vez de depender só da ordem da lista):
   - `Dodge -> Sprint` (`Sprint == true`)
   - `Dodge -> ArmedLocomotion` (`IsWielded == true`)
   - `Dodge -> Locomotion` (`IsWielded == false`)

Sem o passo 3 a transição pro Sprint/Locomotion pode vencer no mesmo
Exit Time e o ataque enfileirado é perdido.

### Parâmetro `IsDodging` — solta o grip da UpperBody em sincronia

Sintoma que apareceu assim que `Dodge -> Attack1` passou a existir: **o
personagem ataca só com o tronco, os braços ficam parados.**

Causa: `ArmedDodgeGrip` (layer `UpperBody`) só tinha saída por **Exit Time
0.95, sem condição** — mas o clipe dele (`great sword idle`) é **loop de
60 frames ≈ 2,0s**, enquanto o `Dodge` da base layer usa um clipe
não-loop de 35 frames ≈ 1,17s. "0.95" ali significa ~1,9s do loop do
idle, ~0,8s depois da base layer já ter saído do Dodge — nesse intervalo
a `UpperBody` sobrepõe os braços com a pose estática de idle por cima do
`Attack1`. **Exit Time em clipe que faz loop nunca sincroniza com outra
layer.** O desalinhamento sempre existiu; era invisível porque o destino
antigo (`ArmedLocomotion`) tem pose quase igual à do grip.

Wiring:

1. **Parameters**: criar `IsDodging` (Bool). O código já alimenta ele
   (`PlayerController.HandleDodge`), com semântica "efetiva" — vira
   `false` no instante em que o blend de saída do Dodge começa, e `true`
   no instante em que o de entrada começa, então a UpperBody troca de
   pose em sincronia com a base layer nos dois sentidos.
2. Nova transição **`ArmedDodgeGrip -> Empty`**: Has Exit Time **off**,
   condição `IsDodging == false`. Deixar **primeira** na lista.
3. Manter a transição por Exit Time que já existe como fallback (só
   dispara se o bool travar por algum motivo).

### Parâmetro `IsSliding` — saída única do SlideAttack

Substitui a ideia anterior (só baixar o Exit Time), que tratava sintoma.

O `SlideAttack` tinha **três saídas por Exit Time em instantes
diferentes**, cada uma dependendo de um bool de fila diferente:
`-> Attack1` (0.8, `ComboQueued`), `-> Attack2Alt` (0.9,
`StrongComboQueued`), `-> ArmedLocomotion` (1.0, `ComboQueued == false`).
Isso dava os dois problemas de uma vez: a cauda de ~60% do clipe sem
nada acontecer (o `OnAnimatorMove()` já zera o deslocamento depois de
`slideAttackActiveEnd = 0.4`), e janelas em que nenhuma condição fechava
— travamento ao atacar bem no fim do golpe.

Solução: **um ponto de saída só, decidido por código.** O `PlayerController`
alimenta `IsSliding`, que vira `false` em `slideAttackExitTime`
(serializado, default **0.9**, ajustável ao vivo no Inspector).

> O default é 0.9 porque é onde o swing acaba neste clipe — valor que já
> tinha sido ajustado à mão nas saídas `-> ArmedLocomotion` e
> `-> Attack2Alt`. A `-> Attack1` tinha ficado em 0.8, e era só ela que
> cortava o golpe ao emendar no combo (as outras deixavam terminar) —
> assimetria que some quando as três passam a sair pelo mesmo ponto.

> `slideAttackExitTime` ≠ `slideAttackActiveEnd` (0.4). O segundo é onde o
> DESLOCAMENTO forçado acaba; o golpe em si ainda toca depois disso, então
> sair em 0.4 cortaria o swing.

Wiring:

1. **Parameters**: criar `IsSliding` (Bool).
2. Nas **quatro** transições de saída do `SlideAttack`, tirar o
   `Has Exit Time` (deixar **off**) e somar `IsSliding == false` às
   condições que já existem:
   - `-> Attack1`: `IsSliding == false` **e** `ComboQueued == true`
   - `-> Attack2Alt`: `IsSliding == false` **e** `StrongComboQueued == true`
   - `-> ArmedLocomotion`: `IsSliding == false`, `ComboQueued == false`
     **e** `StrongComboQueued == false`
   - `-> Dodge` (trigger `Dodge`): **deixar como está** — é o dodge-cancel
     imediato, tem que poder interromper no meio do deslize.
3. Ordem na lista deixa de importar: as três primeiras são mutuamente
   exclusivas por construção.

### `Attack2Alt -> Dodge` (dodge enfileirado no ataque forte)

`Attack2Alt` passou a contar como golpe comprometido junto com
`Attack1Alt1`/`Attack1Alt2` (método `IsInCommittedAttack()`, ex-
`IsInCommittedAttack1Alt`), então Círculo durante ele **enfileira** em vez
de cancelar na hora — mesmo comportamento dos outros dois. O código já
está pronto; falta a transição:

1. Nova transição **`Attack2Alt -> Dodge`**: Has Exit Time **on** (~0.9),
   condição `DodgeQueued == true`.
2. Nas outras saídas do `Attack2Alt`, somar `DodgeQueued == false` —
   mesmo motivo de sempre: deixa as saídas mutuamente exclusivas em vez
   de depender da ordem da lista.

## Prioridade sugerida

1. Item 2 (utilitário centralizado) — maior retorno, menor risco, é
   refactor puro sem mudar comportamento.
2. Item 3 (constantes) — mecânico, baixo risco, prepara terreno pro
   item 5.
3. Item 1 (fila universal) — só vale a pena se aparecer sintoma
   (dodge/jump-attack "sumindo" às vezes); não tem evidência de bug hoje
   pra esses dois especificamente.
4. Item 4 (data-driven) — só quando a segunda arma/moveset for uma
   necessidade real, não antes.
5. Item 5 e 6 — nice-to-have, fazer junto com os itens 2/3 se estiver
   disposto.

## Arquivos afetados (quando for fazer)

- Novo: `Assets/Scripts/Combat/AnimatorStateUtil.cs`
- Novo: `Assets/Scripts/Combat/AnimStrings.cs`
- Editado: `Assets/Scripts/Player/PlayerController.cs`,
  `Assets/Scripts/Equipment/WeaponEquipController.cs` (trocar
  implementações duplicadas pelas centralizadas)
- Novo (se for pro data-driven): `Assets/Scripts/Equipment/WeaponMoveset.cs`
  (ScriptableObject), Animator Override Controllers por arma
