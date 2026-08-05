# Como criar um tileset alternativo (sem perder o original)

Guia prático para testar um kit de peças diferente — outra arte, outras
dimensões, ou só um teste de textura — sem sobrescrever `Assets/WFC/GreyboxTiles`
nem o `TileSet_Greybox` / `FloorSpec` que já estão em uso.

O plugin não tem "a pasta do tileset" fixa em lugar nenhum: cada `FloorSpec`
aponta pra um `TileSet` (um asset arrastado no Inspector), e cada `TileSet`
aponta pros prefabs que você escolheu quando gerou os dados. Trocar de kit é
sempre criar um conjunto novo de assets e apontar pra ele — nunca precisa mexer
em código.

## Passo a passo

### 1. Duplique a pasta de prefabs (se for reaproveitar geometria)

Se você só vai trocar textura/material, copie a pasta inteira:

```
Assets/WFC/GreyboxTiles  →  Assets/WFC/GreyboxTiles_Stone
```

Edite os prefabs copiados à vontade (materiais, meshes, o que quiser). Os
originais em `GreyboxTiles` continuam intocados.

Se for gerar geometria nova do zero (outro Cell Size, outra altura de parede):
pule pro passo 2 e aponte o gerador direto pra uma pasta nova — não precisa
duplicar nada, ele cria os prefabs lá.

### 2. (Opcional) Gere prefabs novos com o Greybox Tile Generator

Menu **WFC ▸ Greybox Tile Generator**.

- Mude **Output Folder** para a pasta nova (ex.: `Assets/WFC/GreyboxTiles_Stone`).
- Ajuste `Cell Size`, `Wall Height`, `Wall Thickness`, `Floor Thickness`,
  `Door Opening` como quiser.
- Clique em **"Gerar 10 tiles (greybox)"**.

> ⚠️ Se mudar o `Cell Size`, anote o valor — ele precisa bater com o `Cell Size`
> do `FloorSpec` que for usar esse tileset (passo 4), senão o andar gera com as
> peças fora de posição (ver nota no fim).

Isso só mexe na pasta que você apontou. `GreyboxTiles` original nunca é tocado
a menos que você aponte `Output Folder` pra ele.

### 3. Gere os dados do tileset com o WFC Tileset Bootstrap

Menu **WFC ▸ Tileset Bootstrap**.

- **Pasta dos prefabs**: aponte pra `GreyboxTiles_Stone` (ou a pasta que você
  editou/gerou no passo 1-2).
- **Pasta de saída**: um caminho novo, ex.: `Assets/WFC/Tileset_Stone`. É aqui
  que nascem `SocketLibrary`, os `ModuleDefinition`, o `TileSet` (já bakeado) e
  o `FloorSpec` de demo.
- Se você mudou `Cell Size` / `Wall Height` no passo 2, repita os mesmos
  números aqui nos campos `Cell Size` / `Cell Height` da janela.
- Clique em gerar.

Isso cria um `TileSet_Stone` (ou nome equivalente) totalmente separado do
`TileSet_Greybox` original — mesmo idempotente, só sobrescreve dentro da pasta
de saída que você apontou.

### 4. Aponte o FloorSpec que você quer testar pro tileset novo

Duas opções:

- Use o `FloorSpec` que o Bootstrap já gerou em `Tileset_Stone/` (mais rápido
  pra teste isolado).
- Ou duplique o `FloorSpec` que já está em uso na cena de jogo e troque o
  campo **Tile Set** dele pro novo asset — assim mantém todas as outras
  configurações (grid size, seed, esqueleto, regras de papel) iguais às da run
  de verdade, só trocando a estética.

No `WFCFloorGenerator` da cena (ou de uma cena de teste), arraste esse
`FloorSpec` no campo **Floor Spec** e dê Play.

### 5. Não esqueça de rebakear se mudou sockets

O bake (**Rebuild adjacency**, no Inspector do `TileSet`) só precisa rodar de
novo se você mexeu em **sockets** (que face conecta com o quê) — não por causa
de textura ou dimensão pura. O `WFC Tileset Bootstrap` já deixa o `TileSet`
bakeado ao criar; se depois você editar módulos à mão, rode o bake de novo
antes de testar.

## Nota: dimensão do prefab não é a mesma coisa que dimensão do grid

O WFC nunca mede a geometria real do prefab — ele posiciona cada peça em
`grid.CellToWorld(cell)`, que usa o `Cell Size` / `Cell Height` do `FloorSpec`,
valores digitados à mão. Se o kit novo tem dimensões diferentes do kit
original, o `FloorSpec` que usa esse kit **precisa** ter `Cell Size` /
`Cell Height` batendo com o que foi usado pra gerar os prefabs — senão as
peças saem desalinhadas (flutuando, enterradas, paredes não fechando o vão).

Isso é outro motivo pra sempre ter um `FloorSpec` por tileset em vez de
reaproveitar o mesmo asset trocando só o campo `Tile Set`: as dimensões viajam
junto com o resto da receita.

## Próximo passo (futuro): trocar tileset a cada N andares

Hoje isso não existe — é lógica de jogo, não do plugin (o gerador não sabe o
que é "andar 5" nem "torre"). O lugar natural pra isso é o `FloorDirector`:
antes de chamar `generator.GenerateRoutine(...)`, trocar
`generator.floorSpec` pelo `FloorSpec` do tileset correspondente, com base em
`floorNumber % N` (ou uma tabela de faixas de andar → FloorSpec). Não
implementado ainda.
