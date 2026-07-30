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

## 2. Checagem "estado ativo OU a caminho" centralizada

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

## 3. Nomes de estado/parâmetro como constantes centralizadas

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

## 6. Consolidar leitura duplicada de Animator entre os dois scripts

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
