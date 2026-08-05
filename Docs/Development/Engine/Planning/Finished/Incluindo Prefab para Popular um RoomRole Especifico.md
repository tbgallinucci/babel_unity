# Incluindo prefab para ser populado num RoomRole específico

> ⚠️ **Desatualizado pro caso comum.** Desde que o `RoomArchetype` +
> `PropRoomPopulator` existem, dar variedade de PROPS a uma sala **não exige
> mais** criar `RoomRole` nem populador novo — isso já é dado, sem código. Use
> este manual só se precisar mesmo de um papel **estrutural** novo (geometria/
> conectividade diferente). Pro caso comum, vá direto pro manual
> [Criando Room Archetypes e Atribuindo Props.md](Criando%20Room%20Archetypes%20e%20Atribuindo%20Props.md).

Guia pra fazer um prop (baú, altar, decoração) nascer só em salas de um
arquétipo específico (ex.: "sala de tesouro"), usando o mesmo mecanismo que já
distribui inimigo em sala de Combate — pelo caminho PESADO (`RoomRole` novo +
populador novo em C#), reservado pra quando o papel precisa mudar geometria
ou conectividade, não só decoração.

Antes deste manual existir o `RoomArchetype`, este era o único jeito de dar
variedade de sala. Hoje ele só continua valendo pros casos descritos na seção
"Quando isto NÃO serve" do manual novo — leia lá primeiro.

## O mecanismo (recapitulando)

- `RoomRole` ([RoomRole.cs](../../../../Assets/WFC/Data/RoomRole.cs))
  é o enum de papéis. Hoje tem `Vazio, Entrada, Corredor, Combate, Escada`.
- O `SkeletonGenerator` sorteia o papel de cada sala "normal" a partir de
  `FloorSpec.skeleton.roomRoleWeights` (lista de peso por `RoomRole`, editável
  no Inspector). Lista vazia = tudo vira `Combate`.
- `GeneratedFloor.Rooms` expõe cada `Room` já com `.Role` e `.Rect` prontos —
  é o dado que todo populador lê.
- Cada `RoomRole` deveria ter **o seu próprio** populador, filtrando só o
  papel dele — é por isso que corrigimos o `EnemyPopulator` pra só pegar
  `Combate` explicitamente (antes ele pegava "qualquer coisa != Entrada", o
  que ia vazar pra qualquer papel novo por omissão).

## Passo a passo

### 1. (Se for um papel novo) Acrescente o valor no enum RoomRole

Edite [RoomRole.cs](../../../../Assets/WFC/Data/RoomRole.cs) e
acrescente, por exemplo:

```csharp
/// <summary>Sala com prop de recompensa — o populador de tesouro decide o quê.</summary>
Tesouro = 5,
```

Se o papel que você quer já existe (ex.: reaproveitar `Escada` pra por um
prop, sem criar nada novo), pule este passo.

### 2. Dê peso a esse papel no FloorSpec

No Inspector do `FloorSpec` (ou do asset que o `WFCFloorGenerator` da cena
usa): **Esqueleto ▸ Room Role Weights**, adicione uma entrada `Tesouro` com o
peso que quiser (baixo — sala de tesouro deveria ser rara. Ex.: `Combate = 6`,
`Tesouro = 1`, pra ~1 em cada 7 salas normais virar tesouro).

Sem essa entrada, `RollRoomRole` nunca sorteia o papel novo — ele só existe no
enum, mas não é usado.

### 3. (Opcional) Regra de papel pra WFC preferir peças certas nessa sala

Se você quiser que a **geometria** da sala de tesouro também seja diferente
(ex.: só aceitar peças com uma `ModuleTag` de "sala pequena" ou "nicho"), isso
é uma `RoleRule` separada, em `FloorSpec.roleRules`
([FloorSpec.cs](../../../../Assets/WFC/Data/FloorSpec.cs)):
Role = `Tesouro`, `Required Tags` / `Forbidden Tags` conforme as tags que seu
`ModuleDefinition` já usa. Isso é regra de **tile**, não de **prop** — não
tem nada a ver com o populador que você vai escrever no passo 5. Se não
precisar de geometria diferente, pule.

### 4. Crie/prepare o prefab do prop

Igual qualquer prefab de cena: malha + collider se for físico, sem
`SpawnAnchor` nenhum (isso é só pra luz de parede — aqui o populador escolhe
célula de piso diretamente, do jeito que o `EnemyPopulator` já faz).

### 5. Escreva o populador (copie o EnemyPopulator como base)

Duplique
[EnemyPopulator.cs](../../../../Assets/Scripts/Floor/EnemyPopulator.cs)
pra, digamos, `Assets/Scripts/Floor/TreasurePopulator.cs`, e ajuste:

- **Namespace/classe**: `Babel.Floor`, `TreasurePopulator`.
- **Elenco**: troque `EnemyEntry` (que exige `NavMeshAgent`) por uma lista
  simples de prefab + peso — treasure prop não precisa de NavMesh.
- **O filtro da sala** — a linha que importa:
  ```csharp
  if (room.Role != RoomRole.Tesouro) { roomsSkippedRole++; continue; }
  ```
  (era `RoomRole.Combate` no original — só troca o papel).
- **Quantidade**: sala de tesouro provavelmente quer 1 prop, não uma faixa
  min/máx crescente por andar — simplifique `RollCount`/`maxPerRoom` conforme
  o caso, ou remova se for sempre exatamente 1.
- **Posicionamento**: reaproveite a varredura de células abertas do
  `Rect` da sala (`IsOpenFloorCell`) — não precisa mudar essa parte. Se o prop
  não precisa de `NavMeshAgent`, pode remover o `TrySnapToNavMesh` e
  instanciar direto em `grid.CellToWorld(cell)`.
- **Track/HealthComponent**: só faz sentido pro `EnemyPopulator` (contagem de
  "andar limpo"). Remova essa parte se o prop não morre.

### 6. Ligue no FloorDirector

Em [FloorDirector.cs](../../../../Assets/Scripts/Floor/FloorDirector.cs),
siga o padrão que já existe para `populator`/`lightingPopulator`:

```csharp
[Tooltip("Opcional. Sem ele, salas de Tesouro saem vazias.")]
[SerializeField] private TreasurePopulator treasurePopulator;
```

E, dentro de `GoToFloorRoutine`:

```csharp
if (treasurePopulator != null) treasurePopulator.Clear();   // junto dos outros Clear()
...
if (treasurePopulator != null)
    treasurePopulator.Populate(floor, new XorShiftRandom(WFCSolver.DeriveSeed(floorSeed, 2)), floor.Root);
```

> Use um índice de `DeriveSeed` diferente do que o `EnemyPopulator` já usa
> (ele usa `1`) — cada populador que consome RNG precisa da sua própria seed
> derivada, senão os dois sorteiam a partir da mesma sequência e um influencia
> o outro. `BasicLightingPopulator` não usa RNG (é 100% geometria), por isso
> não entra nessa conta.

Também vale adicionar em `Reset()`:
```csharp
treasurePopulator = GetComponent<TreasurePopulator>();
```

### 7. No Inspector

No GameObject do `FloorDirector`: `Add Component ▸ Treasure Populator`,
preencha a lista de prefabs, arraste o componente pro campo
`Treasure Populator` do `FloorDirector`. Dê Play e confira o log.

## Erros comuns

- **Sala nunca sai como Tesouro**: esqueceu o passo 2 (peso em
  `roomRoleWeights`) — o enum sozinho não faz nada sortear.
- **Sala de Tesouro sai com geometria de Combate igual**: normal, se você
  pulou o passo 3 — o papel muda o que o *populador* faz, não a geometria,
  a menos que você tenha configurado a `RoleRule` também.
- **Prop nasce em toda sala, não só na de Tesouro**: o filtro do passo 5 (`!=
  RoomRole.Tesouro`) foi esquecido ou ficou igual ao do `EnemyPopulator`
  (copiar e não trocar o `RoomRole` é o erro clássico aqui).
- **Zero salas de Tesouro em andares pequenos**: com poucas salas totais
  (`targetRooms` baixo) e peso baixo pro papel raro, é esperado que ele não
  saia em todo andar — isso é sorteio ponderado normal, não bug.
