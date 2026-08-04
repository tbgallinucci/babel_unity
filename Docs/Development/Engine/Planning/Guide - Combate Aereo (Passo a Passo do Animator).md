# Guia — Combate Aéreo: passo a passo do Animator

Companheiro de `Guide - Combate Aereo (Jump, Launch e Ataques Leves).md`
(aquele é o **porquê**, este é o **como**). Cobre as seções 1, 2 e 3 daquele
guia. O C# já está feito; o que falta é tudo GUI.

**Ordem obrigatória**: import settings → Animation Events → Animator. Fazer o
Animator antes dos clipes deixa estados apontando pra clipe sem loop e sem
event, e o sintoma (personagem congela no ar, pulo não sobe) não aponta pra
causa.

> **Estados removidos (2026-08-03)**: `SlideAttack`, `Heal`, o de magia e o
> `Dash` foram deletados do controller de propósito. O código C# deles **já foi
> removido** na mesma passada deste guia — não procure por `IsSliding`,
> `HandleAbilities` ou a tag `Dashing`, não existem mais.
>
> Os **parâmetros** correspondentes (`IsSliding`, `Heal`, `AttackMagic`,
> `StrongAttack`) continuam no controller e agora não são lidos nem escritos por
> ninguém. Podem ser deletados junto com os do passo 3 — com a mesma ordem de
> segurança: parâmetro só depois de nenhuma transição condicionar nele.

---

## 1. Import settings dos clipes

`Assets/Art/Animations/Player/Movement/`

| Clipe | Ajuste | Por quê |
|---|---|---|
| `Air.FBX` | **Loop Time ✔** e **Loop Pose ✔** | Está com `loopTime: 0`. Sem isso o estado toca os 48 frames uma vez e **congela no último frame** — e como a saída dele é por condição (não tem Exit Time), ele congela até você pousar. |
| `Jump Start.FBX` | Loop Time ✗ (já está) | — |
| `Jump End.FBX` | Loop Time ✗ (já está) | — |
### Bake Into Pose em Y: MANTER marcado (testado)

Uma versão anterior deste guia mandou desmarcar `Root Transform Position (Y)
→ Bake Into Pose` nos três clipes, com o argumento de que o código descarta
root motion vertical de qualquer jeito, então desmarcar deixaria a altura
"100% física". **Testado em jogo: ficou pior, reverter.** Desmarcar tira o
deslocamento vertical da pose e o clipe passa a depender de um root que
ninguém move — o resultado visual quebra.

Fica **marcado** (`loopBlendPositionY: 1`, como já vem). O efeito colateral é
real e conhecido: o movimento vertical do clipe fica no visual, então o modelo
tem um bob próprio somado à física. Onde isso incomodou, os consertos que
funcionaram foram outros — ver `AirLoop` no passo 4 e `airAttackLift` abaixo.

`Assets/Art/Animations/Player/Battle/Light Attack/Air/`

Os dois `GreatSword_Air_Attack0*_Root.FBX`: **não** ligar "Bake Into Pose" em
Root Transform Position (XZ). O código usa o root motion horizontal desses
clipes de propósito (branch novo em `OnAnimatorMove`) — baked out, o golpe
aéreo fica parado no lugar.

---

## 2. Animation Events

Três events novos. Todos precisam cair no GameObject que tem o Animator —
mesma regra já documentada no `WeaponEquipController`.

| Clipe                          | Função           | Quando                                    | Parâmetros               |
| ------------------------------ | ---------------- | ----------------------------------------- | ------------------------ |
| `Jump Start`                   | `OnJumpTakeOff`  | frame em que o **último pé deixa o chão** | —                        |
| `GreatSword_Air_Attack01_Root` | `OnAirAttackHit` | frame de impacto                          | Float = dano, Int = push |
| `GreatSword_Air_Attack02_Root` | `OnAirAttackHit` | frame de impacto                          | Float = dano, Int = push |

Referência de valores: os ataques de chão usam Float `10`, Int `4`.

**`OnAirAttackHit` e não `OnAttackHit`**: a função nova aplica dano igual, mas
também segura o alvo pendurado na altura em que ele está
(`airHoldTime` no `PlayerAttackHitbox`). É o que impede o inimigo de retomar a
queda entre um golpe e outro do combo aéreo.

**Não usar `OnAttackHitLaunch` aqui.** Relançar a cada hit soma velocidade pra
cima e o inimigo escala até sair do alcance por cima.

**Cuidado com o `OnJumpTakeOff`**: ele tem que cair **antes** do Exit Time da
transição `JumpStart → AirLoop` (passo 4), senão o estado troca antes do
impulso e o personagem entra no loop aéreo ainda no chão. E o campo
`jumpTakeOffTimeout` no `PlayerController` (padrão 0.75s) precisa ser maior
que o instante real do event em segundos — se um aviso `[Jump] OnJumpTakeOff
não chegou` aparecer no console em todo pulo, é esse número que está curto,
não o event que está faltando.

> **Armadilha que custou uma sessão inteira de debug (2026-08-03)**: o nome do
> event tinha sido digitado como `` OnJumpTakeOff` `` — com uma **crase no
> fim**. O Unity procura o método por esse nome exato, não acha, e reporta
> `has no receiver! Are you missing a component?` — mensagem que aponta pra
> componente faltando/no objeto errado, não pra typo. E como a mensagem
> imprime o nome entre **aspas simples** (`'OnJumpTakeOff`'`), a crase se
> disfarça de pontuação da própria mensagem.
>
> Se aparecer `has no receiver`, **antes de investigar hierarquia**: abrir o
> `.FBX.meta` do clipe e conferir o `functionName:` byte a byte. É mais rápido
> e descarta a causa mais chata.

**Onde o código do `OnJumpTakeOff` mora** (achado em teste, 2026-08-03): o
método público não fica em `PlayerController` — fica em
`WeaponEquipController`, que já vive no mesmo GameObject do Animator (o
`PlayerController` mora na raiz, um nível acima, e Animation Event nunca sobe
pra lá). `WeaponEquipController` só repassa via um evento C#
(`JumpTakeOff`) que o `PlayerController` assina. Isso é só pra quem for reler
este guia depois — a configuração do event na aba Animation do clipe não
muda, o alvo continua sendo a função chamada `OnJumpTakeOff`, só que ela
resolve num componente diferente agora.

---

## 3. Limpeza dos parâmetros órfãos

Corrige a seção 0 do guia original em um ponto importante.

1. Deletar o estado `Jump` da Base Layer (as três transições de saída dele —
   as que condicionam em `IsJumpAttacking` — vão junto, então a ordem que
   aquele guia pedia deixa de importar).
2. Em **Parameters**, deletar `JumpAttack`, `JumpAttackLand` e
   `IsJumpAttacking` — e, na mesma passada, `IsSliding`, `Heal`, `AttackMagic` e
   `StrongAttack`, que ficaram órfãos quando os estados de slide/heal/magia
   saíram e cujo C# já foi removido.
3. **NÃO deletar `IsJumping`.** O guia original pedia criar um `IsGrounded`
   novo; não é mais o caso. O `IsJumping` já existia órfão (lido por quatro
   transições, escrito por ninguém, portanto sempre `false`) e o
   `PlayerController` passou a escrever nele o valor de "está no ar". Duas das
   condições que já o liam são exatamente as que o combate aéreo precisa —
   UpperBody `Empty → GreatSwordDraw1` e `Empty → GreatSwordSheath1` com
   `IsJumping == false`, ou seja, não dá pra sacar/guardar a arma no ar — e
   passam a funcionar de verdade agora, de graça.

   Reaproveitar em vez de criar `IsGrounded` também evita ficar com um
   parâmetro morto colado num vivo, com significados opostos, que é receita
   pra ligar a condição errada daqui a três meses.

**Nenhum parâmetro novo é necessário.**

---

## 4. Base Layer: os cinco estados novos

Como filhos diretos da Base Layer, no lugar do `Jump` deletado — e não num
sub-state machine. O grafo fica mais poluído, mas toda transição fica
explícita, sem a indireção de Entry/Exit que já causou problema neste projeto.

| Estado | Clipe | Tag | Speed |
|---|---|---|---|
| `JumpStart` | `Jump Start` | `Jumping` | **calcular — ver abaixo** |
| `AirLoop` | `Air` | `Jumping` | **ver nota** |
| `JumpEnd` | `Jump End` | `Jumping` | 1 |
| `AirAttack1` | `GreatSword_Air_Attack01_Root` | `Attack` | Multiplier = `CombatSpeedMultiplier` |
| `AirAttack2` | `GreatSword_Air_Attack02_Root` | `Attack` | Multiplier = `CombatSpeedMultiplier` |

**A tag dos ataques aéreos é `Attack`, não algo novo.** É o que faz
`IsAttacking()`, a trava de rotação, o congelamento do `Speed` e o bloqueio de
Draw/Sheath valerem no ar sem nenhuma mudança. Estado do Unity só tem uma tag,
então o C# distingue os aéreos **por nome de estado** — os nomes acima têm que
bater exatamente com `AnimStrings.AirAttack1` / `AirAttack2`.

Nos dois ataques aéreos, marcar o Speed como **Parameter → `CombatSpeedMultiplier`**
(igual `Attack1`/`Attack2`/`Attack3`), senão o slider de velocidade de combate
não os alcança.

### O Speed do `AirLoop`: escolha entre desacelerar e tremer

`Air.FBX` é a seção aérea de um pulo REAL (frames 48–96 de
`GreatSword_Jump_Loop_Root`), não uma pose neutra de queda — ou seja, ele tem
sobe-e-desce próprio, que com *Bake Into Pose* em Y fica no visual e se soma à
física. Descendo rápido (gravity 35), o bob pra cima do ciclo lê como uma
**freada na descida**.

Aumentar o Speed é o remédio óbvio e tem um teto: a 48 frames/60fps o ciclo é
0.8s, então Speed 10 dá 0.08s por volta — 12.5 Hz, que não lê mais como bob e
sim como o personagem **tremendo**. Foi exatamente o que aconteceu em teste.

O ajuste de verdade não é o Speed, é **encurtar o range do clipe** no import
(`firstFrame`/`lastFrame`, aba Animation do `Air.FBX`): uma janela curta perto
do ápice do salto original, onde o corpo quase não sobe nem desce, vira uma
pose de queda praticamente estática. Com a amplitude do bob pequena, dá pra
voltar o Speed pra 1–2 e não sobra nem freada nem tremor. Vale experimentar
janelas de 8–15 frames dentro do range 48–96 até achar a mais neutra.

### O Speed do `JumpStart` é o número mais sensível do guia

É ele que decide se o pulo responde ou parece arrastado. O alvo é **0.10s a
0.15s entre o aperto e os pés saindo do chão** — acima disso o controle lê como
atrasado, e o jogador sente antes de saber explicar.

O `Jump Start` tem 50 frames. Onde o `OnJumpTakeOff` cai dentro deles é
propriedade da animação, não escolha nossa; o que dá pra escolher é o Speed:

```
tempo até decolar = (tempo normalizado do event × duração do clipe) / Speed
```

Meça a duração real no Inspector do clipe (o campo Length) e resolva pra Speed
com o alvo de 0.15s. Se o event cair em ~40% de um clipe de 0.83s, por exemplo,
`(0.4 × 0.83) / 0.15 ≈ Speed 2.2`.

O `Jump` antigo já rodava em **1.4** por esse motivo — não comece em 1.

Se o número que sair for alto o bastante pra animação ficar acelerada demais aos
olhos, a saída é recortar o windup no import (subir o `firstFrame`) em vez de
continuar aumentando o Speed. E se nada funcionar, o plano B é desistir do event
e voltar a aplicar o impulso no frame do input — perde-se o casamento entre pose
e física, ganha-se resposta. Resposta vale mais.

---

## 5. Transições

Nomenclatura: **HET** = Has Exit Time, **Dur** = Transition Duration.

### 5.1 Entrada no pulo (refazer as quatro que iam pro `Jump`)

| De | Para | HET | Exit | Dur | Condições |
|---|---|---|---|---|---|
| `Locomotion` | `JumpStart` | ✗ | — | 0.25 | `Jump` |
| `ArmedLocomotion` | `JumpStart` | ✗ | — | 0.25 | `Jump` |
| `Sprint` | `JumpStart` | ✗ | — | 0.205 | `Jump` |
| `ArmedSprint` | `JumpStart` | ✗ | — | 0.205 | `Jump` |

### 5.2 O cancel do launcher (a peça que abre o juggle)

| De | Para | HET | Exit | Dur | Condições |
|---|---|---|---|---|---|
| `Attack1Alt2` | `JumpStart` | ✗ | — | 0.1 | `Jump` |

`Attack1Alt2` é o launcher (clipe `GreatSword_Attack04_Root`, o que tem o
`OnAttackHitLaunch`). Duração curta de propósito — é um cancel, não uma
emenda.

**Ordem dentro do `Attack1Alt2`**: colocar esta transição **em primeiro**, antes
das saídas pro `Dodge` e pro Exit. Transição condicionada por trigger que fica
depois de outra já satisfeita nunca é avaliada.

A janela em que isso pode disparar é controlada **em código**
(`launcherJumpCancelStart`, padrão 0.30), não por Exit Time — mesmo idioma do
`ComboWindowOpen()`, e pelo mesmo motivo: Exit Time no Unity é um instante que
se cruza, não um piso.

### 5.3 Dentro do pulo

| De | Para | HET | Exit | Dur | Condições |
|---|---|---|---|---|---|
| `JumpStart` | `AirLoop` | ✔ | **0.6** | 0.1 | — |
| `AirLoop` | `JumpEnd` | ✗ | — | **0.2** | `IsJumping` = false |

**Por que 0.6 e não 0.9** (achado em teste, 2026-08-03): `Jump Start` são 50
frames a 60fps (0.833s), e **a partir do frame 30 o corpo desce no clipe**.
Frame 30 é exatamente `30/50 = 0.6` normalizado, então sair ali corta a descida
antes dela começar.

Preferir isso a **recortar o clipe** (subir o `lastFrame` no import): cortar
tira o material que o crossfade de 0.1 usa pra suavizar a emenda com o
`AirLoop`, e o resultado fica pior — testado.

> Este 0.6 vale como cinto de segurança mesmo depois de desmarcar *Bake Into
> Pose* em Y (passo 1), que é a correção de raiz da mesma queda. Com o bake
> desmarcado dá pra experimentar voltar pra 0.9 e ganhar um blend mais folgado
> — se a queda não voltar, fica em 0.9.

`JumpStart → AirLoop` por Exit Time é seguro: o clipe não faz loop e tem
duração fixa. `AirLoop → JumpEnd` **nunca** pode ser por Exit Time — o tempo de
ar é físico (`jumpForce`/`gravity`), não tem relação com a duração do clipe, e
Exit Time em clipe que faz loop não sincroniza com nada. É a mesma armadilha já
documentada no `IsDodging`/`ArmedDodgeGrip`.

**Por que 0.2 e não 0.1 nesta saída especificamente** (achado em teste,
2026-08-03): `Air.FBX` tem 48 frames a **60fps** = 0.8s por volta do loop. O
pouso pode acontecer em QUALQUER ponto desse ciclo — não tem como sincronizar
"a física pousa" com "a animação completou uma volta redonda", porque a
duração do voo muda com a altura do pulo (que muda com terreno, e no futuro
pode mudar com pulo variável, double jump, etc.). Um blend curto pega o
`AirLoop` no meio de uma pose qualquer (ex.: perna esticada como se ainda
estivesse subindo) e faz um corte visível pro `JumpEnd`. Um blend mais largo
absorve isso — não resolve a causa (não dá pra "resolver" de verdade, é
inerente a combinar física livre com um loop de duração fixa), mas o efeito
colateral (a "flutuada" antes do pouso) fica bem menos perceptível.
Levantar `jumpForce` (feito na seção 7) já ajuda por outro ângulo: mais tempo
total de voo dá mais chance do `AirLoop` completar pelo menos uma volta antes
do pouso, mas os dois efeitos se somam — nenhum sozinho garante a sincronia.

### 5.4 Saída do pulo

| De | Para | HET | Exit | Dur | Condições |
|---|---|---|---|---|---|
| `JumpEnd` | `Sprint` | ✔ | 0.7 | 0.2 | `Sprint` = true |
| `JumpEnd` | `ArmedLocomotion` | ✔ | 0.7 | 0.25 | `IsWielded` = true |
| `JumpEnd` | `Locomotion` | ✔ | 0.7 | 0.2 | — |

**Nesta ordem.** A última não tem condição nenhuma de propósito: é o fallback,
e garante que não existe combinação de estado que deixe o personagem preso no
`JumpEnd`. O `Jump` antigo dependia de três conjuntos de condições mutuamente
exclusivas cobrirem todo o espaço — funcionava, mas sem margem.

### 5.5 Ataques aéreos

| De | Para | HET | Exit | Dur | Condições |
|---|---|---|---|---|---|
| `AirLoop` | `AirAttack1` | ✗ | — | 0.15 | `Attack`, `IsWielded` = true |
| `JumpStart` | `AirAttack1` | ✗ | — | 0.15 | `Attack`, `IsWielded` = true, `IsJumping` = true |
| `AirAttack1` | `JumpEnd` | ✗ | — | 0.1 | `IsJumping` = false |
| `AirAttack1` | `AirAttack2` | ✗ | — | 0.1 | `ComboQueued` = true |
| `AirAttack1` | `AirLoop` | ✔ | 0.9 | 0.15 | — |
| `AirAttack2` | `JumpEnd` | ✗ | — | 0.1 | `IsJumping` = false |
| `AirAttack2` | `AirLoop` | ✔ | 0.9 | 0.15 | — |

**A ordem dentro de cada ataque importa muito**, e é a listada acima: o pouso
(`IsJumping` = false) vem **antes** do combo. Se as duas condições valerem no
mesmo frame — você pousou com o próximo golpe já enfileirado — quem tem que
ganhar é o pouso; continuar um combo aéreo com os pés no chão é justamente o
tipo de dessincronização que derrubou o jump attack antigo.

`ComboQueued` sem Exit Time é de propósito: quem decide *quando* o encadeamento
pode acontecer é o `ComboWindowOpen()` no C#, que já gateia o bool. Mesmo
idioma do combo de chão — não inventar um segundo.

Marcar **Interruption Source = Current State** nas entradas de ataque
(`AirLoop → AirAttack1`, `JumpStart → AirAttack1`, `AirAttack1 → AirAttack2`),
igual às do combo de chão.

**Por que atacar também sai do `JumpStart`**: sem essa transição, apertar
ataque logo depois de pular é engolido — o `JumpStart` inteiro tem que passar
antes do golpe existir, e o jogador lê isso como input perdido, não como regra.

**Mas com `IsJumping = true` junto**, que é o detalhe que evita um bug feio: essa
condição só fica verdadeira depois do `OnJumpTakeOff`. Sem ela, atacar nos
primeiros frames do `JumpStart` trocaria de estado **antes do event disparar** —
o impulso nunca seria aplicado, o personagem faria o combo aéreo inteiro parado
no chão, e só o watchdog (0.75s depois) o jogaria pra cima, fora de hora. Com a
condição, o ataque só pode interromper o `JumpStart` depois que ele já cumpriu a
única coisa que ele precisava cumprir.

### 5.6 Land cancel — o `JumpEnd` não pode travar o controle

As três saídas do passo 5.4 são por Exit Time 0.7, ou seja, 70% do clipe de
pouso com o jogador sem controle. Isso é exatamente o tipo de coisa que faz o
personagem parecer pesado sem que ninguém consiga apontar onde. Adicionar:

| De | Para | HET | Exit | Dur | Condições |
|---|---|---|---|---|---|
| `JumpEnd` | `Attack1` | ✗ | — | 0.15 | `Attack`, `IsWielded` = true |
| `JumpEnd` | `ArmedLocomotion` | ✗ | — | 0.15 | `Speed` Greater 0.1, `IsWielded` = true |
| `JumpEnd` | `Locomotion` | ✗ | — | 0.15 | `Speed` Greater 0.1 |

**Nesta ordem, e antes das três saídas por Exit Time do 5.4** — transição
condicionada que fica depois de uma já satisfeita não é avaliada.

**Esquiva não precisa de transição nova**: a Base Layer já tem um
`AnyState → Dodge` com o trigger `Dodge`, então o roll já cancela o pouso de
graça. As outras duas (mover e atacar) é que faltam.

---

## 6. Layer UpperBody — `ArmedJumpGrip` fica órfão, e precisa ser desconectado

Correção sobre uma versão anterior deste guia, que mandava recablear o
`ArmedJumpGrip`. Não é mais o caso: **os clipes de pulo já vêm do animset
armado** (corpo inteiro, braços já segurando a espada), o mesmo padrão que
`ArmedLocomotion` e todos os estados de ataque de chão já seguem — nenhum
deles toca a `UpperBody`, que fica parada em `Empty` o tempo todo porque o
clipe da base layer já mostra a pose certa sozinho. `ArmedJumpGrip` só existia
porque o `Jump` **antigo** era um clipe genérico desarmado (`Y Bot@Jump`) que
precisava do overlay — a pose estática "great sword idle" do Mixamo — pra
fingir a mão segurando a arma por cima. Esse motivo não existe mais.

**Isto não é "nada a fazer".** `ArmedJumpGrip` já está cabeado no trigger
`Jump` — o MESMO trigger que agora também abre o `JumpStart` na base layer:

```
Empty              -> ArmedJumpGrip   (Jump, IsWielded)
ArmedSprintGrip    -> ArmedJumpGrip   (Jump)
```

Sem desconectar, pular armado continuaria entrando nesse overlay por cima do
clipe novo — a pose estática do Mixamo brigando com o `JumpStart` dinâmico, o
tipo de coisa que lê como um travão de braço no meio do pulo sem ninguém saber
apontar a causa.

**Passos:**

1. Selecionar a transição `Empty → ArmedJumpGrip` e deletar.
2. Selecionar a transição `ArmedSprintGrip → ArmedJumpGrip` e deletar.
3. Deletar o estado `ArmedJumpGrip` (botão direito → Delete). Isso remove
   junto as transições de SAÍDA dele (`→ Empty` por Exit Time, `→ Empty` por
   `Attack`, `→ ArmedDodgeGrip` por `Dodge`) — não precisa apagar uma por uma.

**Nada entra no lugar.** A `UpperBody` fica em `Empty` durante `JumpStart` /
`AirLoop` / `JumpEnd` / `AirAttack1` / `AirAttack2`, exatamente como já fica
durante `ArmedLocomotion` e o combo de chão inteiro.

**Uma esquiva no ar continua coberta de graça**: `Empty` já tem transição
própria pra `ArmedDodgeGrip` (`Dodge`, `IsWielded`) que não depende de estado
nenhum de pulo — ela dispara igual estando no chão ou no ar, então não precisa
de nenhum ajuste aqui pra isso continuar funcionando se/quando existir dodge
aéreo.

---

## 7. Afinar a aritmética do juggle

Este é o passo que decide se a mecânica fecha. Testado em jogo (2026-08-03):
com os valores originais, **não fechava de jeito nenhum** — não era questão de
timing do jogador, era impossível por aritmética. A tabela abaixo já reflete a
correção aplicada; os valores "de antes" ficam só de registro.

**A pegadinha que gerou o cálculo errado na primeira versão deste guia**: os
clipes de pulo (`Air.FBX` etc.) são bakeados a **60fps**, mas o clipe do
launcher (`GreatSword_Attack04_Root`, o `Attack1Alt2`) é bakeado a **120fps** —
frame rates diferentes dentro do mesmo animset. 276 frames a 120fps são 2.3s de
clipe, não os ~1s que uma leitura ingênua do número de frames sugeriria. Vale
conferir o `TimeMode` bakeado em cada FBX antes de fazer conta de timing —
frame count sozinho não diz nada sem a taxa.

**Linha do tempo, medida em segundos reais (não normalizados) desde o início
do swing do `Attack1Alt2`:**

| Evento | Tempo normalizado | Tempo real |
|---|---|---|
| `OnAttackHitLaunch` (inimigo decola) | 0.26 | ~0.60s |
| `launcherJumpCancelStart` (cancel liberado) | 0.30 | ~0.69s |
| Inimigo pousa — **valores antigos** (força 6, voo 0.67s) | — | ~1.27s |
| Player chega perto do ápice — **valores antigos** (jumpForce 5) | — | ~1.45s |

Com os valores antigos o player chegava **~0.18s depois** do inimigo já ter
pousado — sempre, em toda tentativa, independente de reflexo.

**Valores atuais** (já aplicados nos defaults do C#, mas **também precisam ser
copiados pro Inspector do objeto real na cena** — mudar o default em código não
atualiza um campo que já foi salvo numa instância):

| | Fórmula | Valor |
|---|---|---|
| Altura do inimigo | `f² / (2g)` | 14² / (2·18) = **~5.44 m** |
| Tempo de voo do inimigo | `2f / g` | 2·14 / 18 = **~1.56 s** |
| Altura do player | `jumpForce² / (2·gravity)` | 10² / (2·9.81) = **~5.10 m** |
| Tempo do player até o topo | `jumpForce / gravity` | 10 / 9.81 = **~1.02 s** |

Com esses números, o player chega perto do ápice em ~0.69+0.15+0.1+1.02 ≈
**1.96s**, e o inimigo só pousa em ~0.60+1.56 ≈ **2.16s** — sobra ~0.2s de
margem. É um salto e um launch bem mais altos/dramáticos que o original (esse é
o preço), mas é o primeiro conjunto de números com que a mecânica é
fisicamente alcançável. Ajuste pra gosto a partir daqui — os dois campos são
`Range` sliders, testáveis ao vivo em Play Mode.

**Outras alavancas, se ainda precisar de mais folga depois de testar:**

- `PlayerAttackHitbox.airHoldTime` (0.35). Cobre o resto de um golpe aéreo mais
  a entrada do próximo. Se o inimigo "escorrega" pra baixo entre o primeiro e o
  segundo golpe, é este.
- `PlayerController.launcherJumpCancelStart` (0.30). Piso real 0.26 — abaixo
  disso o pulo cancela antes do golpe conectar e não levanta ninguém. Pouca
  margem de sobra aqui (só 0.04 de normalizedTime, ~0.09s reais neste clipe).
- Encurtar o windup do `JumpStart` (ver seção 4) — cada 0.01s a menos ali é
  0.01s a mais de margem no fim da conta.

Com `airAttackGravityScale` em 0 o player trava no ar durante o golpe, então o
alinhamento só precisa valer no **instante do primeiro acerto aéreo** — a partir
dali os dois estão pendurados. É por isso que vale afinar o launcher antes do
pulo.

---

## 8. Checklist de verificação

Ligar `PlayerController.logDodgeDistance`? Não — não serve aqui. Testar assim:

- [x] Pular parado: o personagem sai do chão **quando o clipe mostra os pés
      saindo**, não no frame do aperto. Se sobe antes, o event está tarde
      demais; se sobe tarde, cedo demais.
- [x] Pular e ficar olhando: o loop aéreo **cicla** em vez de congelar (se
      congelou, faltou Loop Time no `Air.FBX`).
- [ ] Pular de uma plataforma alta: o `AirLoop` segura o tempo todo da queda e o
      `JumpEnd` toca no pouso, não antes. Este é o teste que o pulo antigo não
      passava.
- [ ] Nenhum aviso `[Jump] OnJumpTakeOff não chegou` no console.
- [x] Tentar sacar/guardar a arma no ar: **não** deve acontecer (é o `IsJumping`
      novo fazendo efeito no `WeaponEquipController.IsJumping()`, que agora só
      olha a tag `Jumping` da base layer — sem o `ArmedJumpGrip` no meio).
- [x] Atacar no ar: os braços fazem o golpe normalmente (a `UpperBody` fica em
      `Empty` o tempo todo — se algo parecer travado nos braços, o suspeito é o
      `ArmedJumpGrip` não ter sido desconectado no passo 6, não o
      `IsAttacking`).
- [x] Atacar no ar duas vezes: encadeia no segundo golpe, e o player fica
      **parado na altura** durante os dois.
- [ ] Pousar no meio de um ataque aéreo: cai no `JumpEnd`, não continua o combo
      no chão.
- [ ] Apertar ataque **imediatamente** depois de pular: o golpe aéreo sai (não
      é engolido pelo `JumpStart`), e o personagem está no ar quando ele sai.
- [ ] Pousar e sair andando/atacando na hora: o `JumpEnd` é interrompido, não
      segura o controle até o fim do clipe.
- [ ] Cronometrar o aperto até os pés saírem do chão: alvo 0.10–0.15s. Se
      parecer arrastado, é o Speed do `JumpStart` (passo 4).
- [ ] Launcher → pulo: o cancel sai, e o inimigo ainda está subindo quando você
      decola.
- [x] Pular armado e olhar de perto os braços na decolagem: nenhum "pop"/
      travão — sinal de que `Empty → ArmedJumpGrip` ainda está conectado (passo
      6 não foi aplicado, ou só metade).
- [x] Rolar e apertar pulo durante o roll: **nada** deve acontecer, nem na hora
      nem no fim do roll (era um trigger fantasma; ver `HandleJump`).

---

## 9. O que este guia deixa pra depois

- **Pulo desarmado.** Os três clipes são do animset da greatsword. O `Jump`
  antigo (`Y Bot@Jump`, desarmado) é deletado no passo 3, então o pulo desarmado
  passa a mostrar pose de espada até os clipes desarmados chegarem. Quando
  chegarem: duplicar `JumpStart`/`AirLoop`/`JumpEnd` e rotear a entrada por
  `IsWieldedOrDrawing`.
- **Cair de uma borda não entra no `AirLoop`.** O `IsJumping` é aberto pelo
  `OnJumpTakeOff`, ou seja, só por pulo deliberado — andar pra fora de uma
  plataforma continua tocando `Locomotion` no ar, como já acontece hoje. Não é
  regressão, mas fica mais óbvio agora que existe um loop aéreo de verdade pra
  comparar. Conserto: abrir o latch também quando `!controller.isGrounded`
  persistir por alguns frames (o atraso é o que evita o valor cru oscilando em
  degrau/rampa).
- **Blend Tree vertical no lugar do `AirLoop` estático.** Em vez de um clipe
  só, uma Blend Tree em `verticalVelocity` misturando pose de subida (`Y > 0`)
  e de queda (`Y < 0`) — bem melhor de ler no ápice do pulo. Fica pra depois
  por falta de material: existe **um** clipe aéreo (`Air.FBX`), e blend tree com
  uma entrada só é um estado normal com passos a mais. Precisa de uma segunda
  pose antes de valer a pena, e aí também de um parâmetro `VerticalVelocity`
  novo escrito pelo `PlayerController`.
- **Ground check por `Physics.CheckSphere`.** Hoje o airborne vem de
  `CharacterController.isGrounded`, que é sabidamente instável entre frames. O
  latch no `PlayerController` já absorve isso — ele só fecha com `isGrounded` E
  velocidade vertical não-positiva, e uma vez fechado não reabre sozinho, então
  o pior caso de uma oscilação é um frame de atraso no pouso. Se aparecer
  engasgo no `JumpEnd` em rampa ou em quina de plataforma, aí sim vale trocar
  por um `CheckSphere` sob os pés (exige um Transform de referência e uma
  LayerMask de chão na cena).
- **Inimigo sem estado aéreo** — seção 5 do guia original. O `airHoldTime` só
  piora a visibilidade disso: agora ele fica pendurado por mais tempo, tocando
  `Locomotion` no ar.
- **Smash (Triângulo no ar)** — seção 4 do guia original, bloqueado por falta de
  animação.
