# Guia — Combate Aéreo (Jump novo, Launch e Ataques Leves no Ar)

## Contexto

O launcher já funciona: `Attack1Alt2` levanta o inimigo via
`OnAttackHitLaunch` → `KnockbackReceiver.ApplyLaunch()` (arco balístico
integrado à mão, o alvo é kinematic). O que falta é o outro lado da equação
— o **player** subir junto e ter o que fazer lá em cima.

Este guia cobre as três frentes que faltam pra fechar o loop de juggle, na
ordem em que dependem uma da outra.

**Estado ao escrever (2026-08-03):**

- ✅ Launch implementado e testado em jogo
- ✅ Código morto do jump attack antigo removido do C#
- ⏳ Limpeza dos parâmetros órfãos no Animator — **pendente, ver seção 0**
- ❌ Nenhuma animação de smash existe no projeto (ver seção 4)

---

## 0. Limpeza pendente no Animator (fazer ANTES de qualquer coisa)

O jump attack antigo (`Attack2AltTail` / `ArmedJumpAttack2Alt`) foi deletado
em 2026-07-31 por estar ruim. O C# órfão já foi removido, mas **o
`PlayerAnimatorController` ainda tem os três parâmetros**, e um deles é uma
bomba-relógio pra este guia especificamente.

`SetTrigger("JumpAttack")` rodou por semanas sem nenhuma transição pra
consumir. Trigger não consumido no Unity **fica setado pra sempre** — ou
seja, existe um `JumpAttack` pendurado no controller agora. No instante em
que qualquer transição nova ganhar condição nesse parâmetro, ela dispara
sozinha, sem input, e o sintoma não aponta pra causa.

**A ordem importa** — três transições ainda condicionam em `IsJumpAttacking`:

| De | Para | Condição a remover |
|---|---|---|
| `Jump` | `Locomotion` | `IsJumpAttacking` = false |
| `Jump` | `ArmedLocomotion` | `IsJumpAttacking` = false |
| `Jump` | `Sprint` | `IsJumpAttacking` = false |

Como `IsJumpAttacking` nunca mais é escrito, ele fica `false` pra sempre e
essas condições sempre passam — removê-las não muda comportamento nenhum.

**Passos:**

1. Abrir `PlayerAnimatorController`, selecionar cada uma das três transições
   acima e remover **só** a linha de condição `IsJumpAttacking` (as outras —
   `Sprint`, `IsWielded`, `IsJumping` — ficam).
2. Só então, na aba **Parameters**, deletar `JumpAttack`, `JumpAttackLand` e
   `IsJumpAttacking`.
3. File → Save Project.

Deletar o parâmetro primeiro deixa as três transições apontando pra um
parâmetro inexistente.

> **Decisão pendente**: se o smash futuro for reusar o nome `JumpAttack`,
> apagar e recriar o parâmetro **não** limpa necessariamente o estado do
> trigger em runtime. Preferir nome novo (`Smash`) é a saída sem armadilha.

---

## 1. Trocar o Jump atual pelos três clipes da pasta

Hoje o pulo é um clipe só. Na pasta já existem as três peças:

- `Assets/Art/Animations/Player/Movement/Jump Start.FBX`
- `Assets/Art/Animations/Player/Movement/Air.FBX` (o miolo/loop)
- `Assets/Art/Animations/Player/Movement/Jump End.FBX`

**Por que isso vem primeiro:** o combo aéreo precisa de um estado de ar com
duração indefinida pra interromper. Com um clipe único de duração fixa não
existe "enquanto estiver no ar" — só "enquanto o clipe do Jump não acabou",
que é justamente o que gerou a família de bugs de dessincronização perna/braço
do jump attack antigo (ver o comentário deletado sobre Exit Time do Jump
roubando a base layer).

**Estrutura alvo** (três estados na Base Layer, substituindo `Jump`):

```
JumpStart --(Exit Time ~0.9)--> AirLoop --(IsGrounded)--> JumpEnd --> Locomotion
```

**Pontos de atenção:**

- **`Air.FBX` precisa de `Loop Time` ligado antes de virar estado.** O clipe é
  `GreatSword_Jump_Loop_Root` (frames 48–96) — é loop de movimento de verdade,
  mas está importado com `loopTime: 0`, igual `Jump Start` e `Jump End`. Sem a
  marcação, o estado toca os 48 frames uma vez e **congela no último frame**.
  Hoje isso passa despercebido porque o pulo é curto; com hangtime no ataque
  aéreo (seção 3) ou juggle esticado, aparece na hora. Ligar `Loop Time` e,
  provavelmente, `Loop Pose` junto (`loopBlend` também está 0) pra costurar a
  emenda do ciclo.
- A saída do `AirLoop` é **por condição** (`IsGrounded`), nunca por Exit Time —
  o tempo de ar é físico (`jumpForce`/`gravity`), não tem relação com a duração
  do clipe. Exit Time em clipe que faz loop nunca sincroniza com nada; é
  exatamente a armadilha já documentada no `IsDodging`/`ArmedDodgeGrip`.
- Vai precisar de um parâmetro `IsGrounded` novo (bool), escrito por
  `PlayerController` a partir de `controller.isGrounded`. Hoje não existe —
  o Animator só tem `Jump` (trigger) e `IsJumping`.
- As três transições `Jump -> Locomotion/ArmedLocomotion/Sprint` da seção 0
  vão ter que ser refeitas a partir de `JumpEnd`.
- Layer `UpperBody`: existe `ArmedJumpGrip` amarrado ao `Jump` — mas ele NÃO
  precisa cobrir os três estados novos. `Jump Start`/`Air`/`Jump End` são do
  animset ARMADO (corpo inteiro, braços já segurando a espada), o mesmo padrão
  de `ArmedLocomotion` e do combo de chão, nenhum dos quais toca a `UpperBody`.
  `ArmedJumpGrip` existia só porque o `Jump` antigo era um clipe genérico
  desarmado. Com os clipes novos, `ArmedJumpGrip` fica órfão e precisa ser
  **desconectado** (não recablear) — ver o guia de passo a passo, seção 6, pro
  detalhe de por que deixar como está quebraria o pulo (o trigger `Jump`
  continua disparando a transição antiga por engano).

---

## 2. Alinhamento Jump + Launch (o cancel que abre o juggle)

**O objetivo:** acertar o launcher (`Attack1Alt2`), ver o inimigo subir e
**poder pular atrás dele** pra emendar o combo aéreo.

**O problema hoje:** `HandleJump()` exige `!IsAttacking()`, e `Attack1Alt2`
é golpe comprometido (`IsInCommittedAttack()`) — ou seja, a recuperação do
launcher engole o pulo inteiro. Quando o player consegue pular, o inimigo já
está caindo.

**A regra a implementar:** a transição de saída do launcher tem que ser
**interrompível por Jump**, na janela em que o inimigo ainda está subindo.

Duas peças:

1. **No Animator**: transição `Attack1Alt2 -> JumpStart`, `Has Exit Time`
   **off**, condicionada ao trigger `Jump`. Mesmo idioma do dodge-cancel que
   já existe (transições de entrada em `Dodge` a partir de cada estado de
   ataque).
2. **No C#**: afrouxar o gate do `HandleJump()`. Não pode ser um
   `!IsAttacking()` cru — o pulo deve cancelar **só o launcher**, não
   qualquer golpe. Sugestão: uma janela igual à do `ComboWindowOpen()`, ou um
   `IsInLauncher()` específico.

**Alinhamento de timing** (o número que faz ou quebra a mecânica): a janela
de voo do inimigo é `2 × launchUpwardForce / launchGravity`. Com os padrões
atuais (6 e 18) são **0.67s**. O pulo do player precisa levar o corpo dele à
altura do inimigo **dentro** dessa janela, senão o juggle não fecha por
aritmética, não por bug.

Vale calcular os dois arcos lado a lado antes de afinar no olho:

| | Fórmula | Onde mora |
|---|---|---|
| Altura do inimigo | `f² / (2g)` | `PlayerAttackHitbox.launchUpwardForce`, `KnockbackReceiver.launchGravity` |
| Tempo de voo do inimigo | `2f / g` | idem |
| Altura do player | `jumpForce² / (2 × gravity)` | `PlayerController` |

> **Provável ajuste**: o pulo do player e o launch foram afinados
> isoladamente e não há razão pra baterem. Espere ter que mexer nos dois.

**Bloqueio conhecido**: o inimigo não tem estado aéreo no Animator dele. Voo
acima de ~0.5s já dá tempo dele voltar pra `Locomotion` **flutuando**. Isso
vai ficar MAIS visível conforme o juggle esticar. Ver seção 5.

---

## 3. Os dois ataques leves aéreos (Quadrado no ar)

Clipes já importados:

- `Assets/Art/Animations/Player/Battle/Light Attack/Air/GreatSword_Air_Attack01_Root.FBX`
- `.../GreatSword_Air_Attack02_Root.FBX`

**Estrutura**: mesmo idioma do combo de chão — dois estados encadeados por
bool persistente (`ComboQueued`), não por trigger cru, e com
`ComboWindowOpen()` gateando o encadeamento. O combo de chão já é o modelo a
copiar; não inventar um segundo idioma.

**Pontos de atenção:**

- **Entrada**: de `AirLoop`, condicionada ao trigger `Attack` + `IsWielded`.
  Como `AirLoop` é loop, a entrada é por condição pura, sem Exit Time.
- **Saída**: os dois ataques têm duração fixa, mas o player pode pousar no
  meio. Precisa de saída por `IsGrounded` para `JumpEnd` **além** da saída
  natural por Exit Time de volta pro `AirLoop`.
- **Gravidade durante o golpe**: decidir se o ataque aéreo segura o player no
  ar (hangtime, típico de character action) ou se ele continua caindo. Segurar
  é o que faz o juggle funcionar na prática — sem isso o player despenca e sai
  do alcance do inimigo no meio do combo. Se for segurar, é código em
  `OnAnimatorMove` (zerar/reduzir `verticalVelocity` durante a tag), não
  animação.
- **Hitbox**: `PlayerAttackHitbox.OnAttackHit` já serve — mesma geometria de
  cone. Só precisa dos Animation Events nos dois clipes novos (Float = dano,
  Int = push). **Não** usar `OnAttackHitLaunch` aqui; relançar no ar já
  funciona (o `groundY` é preservado), mas relançar a cada hit do combo aéreo
  faz o inimigo subir indefinidamente.

---

## 4. Smash (Triângulo no ar) — BLOQUEADO por falta de animação

Não existe clipe de smash/mergulho no projeto. O animset de greatsword
(`Assets/Art/Animations/Greatsword/GreatSword_Animset/`) tem 120+ clipes mas
nenhum ataque aéreo além dos dois `Air_Attack` já usados.

**Candidatos a improvisar** (chute pelo nome, precisa de preview):

- `GreatSword_SPAttack1_Root`, `SPAttack1_2`, `SPAttack2` — "special attack",
  tipicamente overhead pesado. Primeiro lugar a olhar.
- `GreatSword_Attack05_Root` até `Attack12_Root` — oito ataques de chão nunca
  importados (o projeto só trouxe 01–04).
- `GreatSword_Whirlwind_PowerEnd1/2_Root` — bom candidato pro **impacto do
  pouso**, não pro golpe inteiro.

**Não confundir**: `GreatSword_Falldown_*` e `GreatSword_Large_Hit` são o
personagem **levando** dano, não atacando.

**Construção padrão sem clipe dedicado** — mesma estrutura de 3 peças da
seção 1:

1. **Windup** — começo do `SPAttack1` (erguer a arma), curto
2. **Queda em loop** — pose segurada enquanto desce (sub-clipe de 1 frame)
3. **Impacto** — `Whirlwind_PowerEnd1` ou a cauda do `SPAttack1`

**Decisão de arquitetura**: pra queda usar as variantes **Inplace**, não
Root. A distância da descida depende de quão alto o player estava, coisa que
clipe nenhum sabe — o deslocamento tem que ser forçado por código, exatamente
o idioma que `Dash` e `SlideAttack` já usam (clipe in place, avanço 100% em
C#).

---

## 5. Dívida que este guia expõe (não bloqueia, mas piora com ele)

**Inimigo sem estado aéreo.** O `EnemyAnimatorController` só tem
`Locomotion`, `Attack`, `JumpAttack`, `Hit`. Durante o launch ele toca o
`Hit` e volta pra `Locomotion` no ar. Quanto mais longo o juggle, mais óbvio.
Resolver = estado `Airborne`/`Falling` novo, com entrada por condição a
partir de um bool escrito pelo `EnemyBase` lendo
`KnockbackReceiver.IsActive` + altura.

**`Hit` só é alcançável de `Locomotion`.** Não há transição de Any State
(`m_AnyStateTransitions` vazio), e `EnemyBase.HandleDamaged` não dispara o
trigger durante `Attack`/`JumpAttack`. Combo aéreo vai bater no inimigo em
estados variados — vale reavaliar.

**`agent.Warp` no pouso.** Se o launch jogar o inimigo pra fora da NavMesh, o
Warp falha calado e ele trava. Já valia pro knockback normal; o juggle torna
mais fácil de acontecer.

**Hit stop não escala por golpe.** `hitStopMultiplier` é por componente, não
por golpe — o launcher não ganha trava mais pesada automaticamente. Vira dado
do golpe quando o `WeaponMoveset` existir.

---

## Ordem sugerida

```
0. Limpar params órfãos no Animator   (bloqueia tudo — bomba-relógio)
      ↓
1. Jump em 3 estados + IsGrounded     (dá o "enquanto no ar" pro resto)
      ↓
2. Cancel do launcher pra Jump        (abre a janela do juggle)
      ↓
3. Dois ataques leves aéreos          (primeiro conteúdo aéreo jogável)
      ↓
5. Estado aéreo do inimigo            (quando o juggle esticar e ficar feio)
      ↓
4. Smash                              (depende de achar animação)
```
