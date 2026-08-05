# Criando arquétipos de sala e atribuindo props (sem C#)

Guia atualizado pro sistema de `RoomArchetype` — a forma normal, sem código,
de dar variedade de conteúdo (props) às salas de Combate. Se você caiu aqui
procurando "como criar um RoomRole novo", leia a seção **Quando isto NÃO
serve** no fim antes de ir editar o enum.

## Duas camadas diferentes, não confundir

| | `RoomRole` (enum, `WFC.Data`) | `RoomArchetype` (ScriptableObject, `Babel.Floor`) |
|---|---|---|
| O que é | Papel **estrutural** da sala/célula | "Sabor" de conteúdo de uma sala de Combate |
| Quem lê | `SkeletonGenerator`, `WFCFiller` (geometria/conectividade) | `PropRoomPopulator` (só props) |
| Como se cria um novo | Editando `RoomRole.cs` + recompilando | `Assets ▸ Create ▸ Babel ▸ Room Archetype` — zero código |
| Exemplos | Entrada, Corredor, Combate, Escada | "Sala com estátuas", "Sala com teias", "Sala de banquete" |

Na prática: **quase sempre você quer um `RoomArchetype` novo, não um
`RoomRole` novo.** Todo `RoomArchetype` já nasce associado a salas de
`RoomRole.Combate` — a variedade visual/de props não precisa de um papel
estrutural próprio. Só mexa em `RoomRole` se precisar mudar **geometria**
(que peças o WFC aceita ali) ou **conectividade** (ex.: uma sala que só pode
ter 1 porta) — isso é regra de layout, não de decoração.

## Passo a passo: criar um Room Archetype

### 1. Crie o asset

`Assets ▸ Create ▸ Babel ▸ Room Archetype`. Dê um nome ao arquivo (ex.:
`Archetype_Estatuas.asset`) — o nome do arquivo é só organização, quem
identifica o arquétipo nos logs é o campo **Display Name** dentro dele.

### 2. Configure o Inspector do asset

- **Display Name**: texto livre, só pra reconhecer nos logs (ex.: "Sala com Estátuas").
- **Weight**: peso relativo no sorteio **entre arquétipos**, quando uma sala
  de Combate é populada. Maior = mais comum. Não precisa somar 1 nem nada —
  é proporção, não porcentagem.
- **Prop Count** (mín, máx): quantos props essa sala recebe no total,
  somando todas as entradas da lista abaixo.
- **Props**: a lista de prefabs deste arquétipo (ver próxima seção).

### 3. Popule a lista de Props

Duas formas, pode misturar:

**A. Arrastar manualmente** — clique em `+` na lista `Props`, arraste um
prefab pro campo `Prefab`, ajuste `Weight`.

**B. Importar uma pasta inteira de uma vez** — no fim do Inspector do
`RoomArchetype` tem a seção **"Importar props de uma pasta"**:
1. Arraste a pasta (não um arquivo — a pasta em si, do Project window) pro
   campo **Pasta de Props**.
2. Clique **"Importar prefabs desta pasta"**.
3. Todo prefab dentro daquela pasta (sem descer em subpastas) vira uma
   entrada nova na lista, com `Weight = 1`.

Rodar de novo depois de adicionar mais prefabs na pasta é seguro — ele só
acrescenta as entradas novas, não duplica nem apaga o que você já ajustou
nas que continuam lá.

### 4. Cada prop: célula aberta ou anchor?

Por entrada da lista `Props`, o campo **Use Anchor** decide como ela nasce:

- **Desligado (padrão)** — sorteia uma célula de piso totalmente aberta
  dentro da sala, igual o `EnemyPopulator` já faz com inimigo. Bom pra prop
  solto: estátua, caixote, barril no meio do caminho.
- **Ligado** — só nasce em cima de um `SpawnAnchor` do **Anchor Kind**
  escolhido (`Prop`, `WallProp` ou `Chest`), plantado nas PEÇAS de parede/piso
  pelo autor do tileset. Bom pra prop que precisa de um encosto específico:
  banner na parede, prateleira num nicho, tocha (essa já usa o mecanismo
  parecido, ver manual de luz de parede). **Se a sala gerada não tiver nenhum
  anchor daquele tipo, essa entrada simplesmente não nasce ali — não é erro,
  é esperado** (ver seção de anchors abaixo pra como plantar).

### 5. Ligue no PropRoomPopulator

No GameObject do `FloorDirector` (ou onde preferir):

1. `Add Component ▸ Babel ▸ Prop Room Populator` (se ainda não tiver).
2. No campo **Archetypes**, arraste todos os `RoomArchetype` que você quer
   que concorram entre si nas salas de Combate do andar.
3. Ajuste **Safe Radius From Entrance** se quiser manter a sala de entrada
   livre de props também (mesma ideia do `EnemyPopulator`).
4. Arraste o componente `Prop Room Populator` pro campo **Prop Populator** do
   `FloorDirector` (ou clique direito no `FloorDirector` ▸ **Reset**, que
   auto-preenche todos os populadores via `GetComponent<>()` se estiverem no
   mesmo GameObject).

Dê Play. Confira o log `[PropRoomPopulator]` no console — ele diz quantos
props nasceram, em quantas salas, e por que salas ficaram de fora (papel
errado ou arquétipo sem prop utilizável).

## Acoplamento com inimigos (allowEnemies / enemyDensityMultiplier)

Por padrão, o `EnemyPopulator` e o `PropRoomPopulator` não se conhecem: os
dois rodam em toda sala de Combate, cada um com sua própria regra. Ou seja,
sem configurar nada, uma sala que sorteou o arquétipo "Loja" pode
perfeitamente também spawnar monstro nela.

Se isso não é o que você quer, cada `RoomArchetype` tem dois campos pra
resolver:

- **Allow Enemies** (bool, padrão ligado): desligado = o `EnemyPopulator`
  pula a sala inteira — zero inimigo nela, não importa o resto da
  configuração. Use pra "Loja", "Santuário", qualquer arquétipo onde monstro
  não faz sentido.
- **Enemy Density Multiplier** (float, padrão 1): multiplica a quantidade de
  inimigo que a sala receberia normalmente. `0.5` = metade, `2` = dobro.
  Ignorado se `Allow Enemies` estiver desligado. Use pra afinar densidade sem
  zerar de vez (ex.: uma sala de "Descanso" com só 30% do inimigo normal, mas
  não necessariamente zero).

Isso só funciona porque o `FloorDirector` sorteia o arquétipo de cada sala
(`PropRoomPopulator.RollArchetypes`) **antes** de chamar
`EnemyPopulator.Populate`, e passa o resultado adiante — os dois populadores
continuam sem se conhecer diretamente, quem faz a ponte é o `FloorDirector`.
Se você chamar `EnemyPopulator.Populate` de outro lugar (fora do
`FloorDirector` padrão) sem passar esse dicionário, o comportamento cai pro
de sempre: toda sala de Combate é candidata, sem multiplicador nenhum — não
quebra, só ignora o acoplamento.

## Plantando anchors pra props "de encosto" (Use Anchor = true)

Isso é trabalho no **prefab da peça de tileset** (parede/piso/canto), não no
prop nem no populador — o populador só consome o que já foi plantado.

Siga exatamente o padrão que o `Greybox Tile Generator` já usa pra luz
(`WallLight()`, em
[GreyboxTileGenerator.cs](../../../Assets/WFC/Editor/GreyboxTileGenerator.cs)):
um filho vazio na peça, com um `SpawnAnchor` component, `kind` igual ao que
você vai pedir no `RoomArchetype` (`Prop`/`WallProp`/`Chest`), pivô no ponto
de encaixe, `forward` apontando pra fora da peça (pra dentro da sala).

Se o seu tileset ainda é o greybox gerado por código, o caminho mais rápido é
duplicar o método `WallLight()` do `GreyboxTileGenerator.cs`, trocar
`SpawnAnchorKind.Light` pelo kind que você quer, e chamar nas peças certas —
igual foi feito pra luz (`Tile_Wall`, `Tile_Corridor`, `Tile_Door_Corridor`).
Se já estiver em arte de verdade (fora do gerador), é só adicionar o
`SpawnAnchor` manualmente como filho do prefab, com a mesma convenção de
pivô/forward.

> Anchor sem uso não é grátis mas também não é caro: o `TileInstancer`
> sempre coleta todos, independente de existir populador que os consuma. Não
> tem problema plantar anchors de `Prop`/`WallProp`/`Chest` numa peça mesmo
> antes de ter o `RoomArchetype` que vai usá-los.

## Quando isto NÃO serve — precisa mesmo de um RoomRole novo

Casos que **não** são resolvidos por `RoomArchetype`, porque mexem com
estrutura, não conteúdo:

- Uma sala que o WFC deve montar com **peças diferentes** (tags de módulo
  diferentes) — isso é uma `RoleRule` em `FloorSpec.roleRules`, que precisa
  de um `RoomRole` de verdade pra se pendurar.
- Uma sala que **nunca deveria virar Combate** (ex.: uma "sala secreta" que o
  `EnemyPopulator` e o `PropRoomPopulator` devem ignorar dos dois, com regras
  de spawn totalmente à parte).
- Qualquer coisa que precise mudar o **sorteio de entrada/escada** ou a
  **conectividade** do esqueleto.

Pra esses casos, siga o manual
[Incluindo Prefab para Popular um RoomRole Especifico.md](Incluindo%20Prefab%20para%20Popular%20um%20RoomRole%20Especifico.md)
— ele cobre acrescentar um valor no enum `RoomRole` e escrever um populador
dedicado do zero. É o caminho mais pesado; só vale quando o `RoomArchetype`
genuinamente não dá conta.

## Erros comuns

- **Nenhum prop nasce em lugar nenhum**: confira se o `Prop Room Populator`
  está com a lista `Archetypes` vazia (log avisa isso), ou se todo
  `RoomArchetype` da lista está com `props` vazio.
- **Prop de `Use Anchor = true` nunca nasce**: o tileset ainda não tem anchor
  daquele `Anchor Kind` plantado em nenhuma peça — normal se você ainda não
  fez a parte de "Plantando anchors" acima. Confirme testando primeiro com
  `Use Anchor = false`.
- **Sempre o mesmo arquétipo em todo andar**: os pesos (`Weight`) dos
  arquétipos estão muito desbalanceados, ou só tem 1 arquétipo na lista do
  populador.
- **Importar pasta não adiciona nada**: o botão só lê prefabs no NÍVEL da
  pasta apontada, não em subpastas — confirme que os `.prefab` estão direto
  dentro da pasta escolhida, não numa subpasta dela.
