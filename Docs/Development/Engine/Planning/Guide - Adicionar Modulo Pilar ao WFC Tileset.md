# Guia — Adicionar módulo "Pilar" ao WFC Tileset

Passo a passo pra registrar uma peça nova (pilar decorativo) no tileset
greybox do WFC, seguindo as mesmas convenções que as outras peças em
`Assets/WFC/GreyboxTiles/` já usam (`cellSize = 6`, `wallHeight = 5`,
pivô no centro da célula, topo do piso em y = 0).

Isso é trabalho manual no Editor de propósito — o
`WFCTilesetBootstrap` (`WFC ▸ Tileset Bootstrap`) só recria as 10 peças
greybox originais; rodar ele de novo **sobrescreve** a lista `modules`
do `TileSet` e apaga qualquer peça adicionada na mão. Peça nova = fluxo
manual abaixo.

---

## Parte 1 — Criar a geometria do pilar

1. Na **Hierarchy**, botão direito → **Create Empty**. Renomeie para
   `Tile_Pillar`. Confirme que o Transform está zerado (Position
   `0,0,0` / Rotation `0` / Scale `1`) — esse é o pivô da célula.
2. Dentro dele, crie a laje de piso (igual às outras peças):
   - Botão direito no `Tile_Pillar` → **3D Object ▸ Cube**, renomeie
     para `Floor`.
   - Transform: **Position** `(0, -0.05, 0)`, **Scale** `(6, 0.1, 6)`.
   - Arraste o material
     `Assets/WFC/GreyboxTiles/Materials/M_Greybox_Floor.mat` pro
     `MeshRenderer` dele.
3. Ainda dentro do `Tile_Pillar`, crie a coluna:
   - Botão direito no `Tile_Pillar` → **3D Object ▸ Cylinder**,
     renomeie para `Pillar`.
   - Transform: **Position** `(0, 2.5, 0)` (metade da altura da
     parede, `H/2` = 5/2), **Scale** `(1, 2.5, 1)` (o Cylinder padrão
     da Unity tem altura 2, então Scale Y = 2.5 dá altura final de 5m
     — ajuste a espessura em X/Z como quiser; `1` dá um pilar de ~1m
     de diâmetro).
   - Arraste o material `M_Greybox_Wall.mat` (ou `M_Greybox_Accent.mat`
     se quiser destacar) pro `MeshRenderer`.
   - O `CapsuleCollider`/`MeshCollider` que já vem no Cylinder
     primitivo serve — é ele que faz o `NavMeshSurface` (que bakeia
     por `PhysicsColliders`, ver
     `Assets/WFC/Runtime/NavMeshBuilderService.cs`) excluir a área do
     pilar do caminho navegável, do mesmo jeito que as paredes fazem.
     Não precisa marcar nada de "Navigation Static".

> Se já existir um modelo de coluna em `Assets/Art/Objects`, pule esse
> passo e arraste o modelo pra dentro do `Tile_Pillar` na posição
> `(0,0,0)` — só garanta que ele tem collider e que a base encosta em
> y=0.

## Parte 2 — Salvar como prefab

1. Arraste o GameObject `Tile_Pillar` da Hierarchy para a pasta
   `Assets/WFC/GreyboxTiles/` no Project window.
2. Isso cria `Tile_Pillar.prefab`. Depois de confirmar que virou
   prefab (ícone azul), **delete o `Tile_Pillar` da Hierarchy** — ele
   só precisa existir como asset, igual aos outros `Tile_*.prefab` que
   já estão lá.

## Parte 3 — Criar o `ModuleDefinition`

1. Vá em `Assets/WFC/Tileset/Modules/`.
2. Botão direito → **Create ▸ WFC ▸ Module Definition**. Renomeie para
   `Module_Pillar`.
3. Selecione o asset e preencha o Inspector:

| Campo | Valor |
|---|---|
| **Prefab** | `Tile_Pillar` (arraste da pasta GreyboxTiles) |
| **Faces** (PosX, NegX, PosY, NegY, PosZ, NegZ) | `open, open, sky, ground, open, open` |
| **Weight** | `0.4` (bem menor que o `3` do `Module_Floor_Open` — senão vira floresta de pilar) |
| **Allowed Y Rotations** | só a `0°` marcada (o cilindro é simétrico, girar não muda nada) |
| **Tags** | `Floor` |
| **Notes** | opcional, ex.: "pilar decorativo, célula continua passável" |

Pra preencher as **Faces**: expanda o array, e em cada um dos 6
elementos digite o texto exato no campo `Socket`:

- Elemento 0 (PosX): `open`
- Elemento 1 (NegX): `open`
- Elemento 2 (PosY): `sky`
- Elemento 3 (NegY): `ground`
- Elemento 4 (PosZ): `open`
- Elemento 5 (NegZ): `open`

Esses nomes têm que bater **exatamente** com os sockets já cadastrados
em `Assets/WFC/Tileset/SocketLibrary.asset` — por isso é o mesmo
padrão do `Module_Floor_Open`.

## Parte 4 — Registrar no TileSet e bakear

1. Selecione `Assets/WFC/Tileset/TileSet_Greybox.asset`.
2. No Inspector, ache o campo **Modules** (lista). Aumente o **Size**
   em `+1`.
3. Arraste `Module_Pillar` pro slot novo que apareceu.
4. Clique no botão **Rebuild adjacency** (aparece embaixo da lista —
   é o `TileSetEditor` custom).
5. Leia o **Relatório do bake**: deve dizer `BAKE OK` e mostrar a
   variante `Module_Pillar` com vizinhos nas 4 direções horizontais.
   Se aparecer erro de socket, revise o texto digitado na Parte 3 —
   tem que ser idêntico, sem espaço.

## Parte 5 — Salvar

`File ▸ Save Project` (ou Ctrl+S). Sem isso os assets novos
(`Tile_Pillar.prefab`, `Module_Pillar.asset`, o `TileSet_Greybox`
atualizado) ficam só na memória do editor.

## Parte 6 — Testar

Duas formas:

- **Play Mode** na cena `Level_Test`: o `FloorDirector` gera o andar
  no Start — se o pilar tiver peso baixo, pode levar algumas gerações
  (F5 pra recarregar a cena, ou usar o botão de contexto **Regenerar
  andar atual** no `FloorDirector`) até aparecer um.
- **Sem entrar em Play**: se o `WFCFloorPreview` estiver montado em
  alguma cena de teste, dá pra gerar direto no editor e olhar o
  resultado sem rodar o jogo — mais rápido pra iterar no peso/visual
  do pilar.

## Opcional — restringir onde ele aparece

Do jeito que está, o pilar pode nascer em corredor estreito e
atrapalhar passagem. Pra reservá-lo só pra salas de combate, seria
necessário:

1. Adicionar uma tag nova em `Assets/WFC/Data/ModuleTag.cs` (ex.:
   `Decoration = 1 << 9`).
2. Marcar `Module_Pillar` com essa tag.
3. Ajustar a regra de `Corredor` em
   `Assets/WFC/Tileset/FloorSpec_Demo.asset` (campo `Role Rules`) pra
   proibi-la (`forbiddenTags`).

Isso mexe em código (`ModuleTag.cs`), então melhor tratar como um
passo à parte, numa sessão dedicada.

---

## Anexo — Como trocar textura e material do tileset

Diferente das Partes 1-6, isto é **puramente visual**: não mexe em
socket, `ModuleDefinition` nem `TileSet`, então **não precisa rodar
"Rebuild adjacency"** depois. O projeto usa URP (shader
`Universal Render Pipeline/Lit`), e os três materiais greybox ficam em
`Assets/WFC/GreyboxTiles/Materials/`:

- `M_Greybox_Floor.mat` — usado por toda peça de piso (`Floor` em
  todo `Tile_*.prefab`, inclusive o `Tile_Pillar` deste guia).
- `M_Greybox_Wall.mat` — usado nas paredes (`Wall_N/S/L/E`, coluna do
  pilar).
- `M_Greybox_Accent.mat` — usado em detalhes (verga da porta, degraus
  da escada).

Como esses materiais são **compartilhados** (`sharedMaterial`) entre
todas as peças, trocar a textura de um deles atualiza **todo o
andar de uma vez** — não precisa editar prefab por prefab.

### 1. Importar a textura

1. Arraste os arquivos de imagem (`.png`/`.jpg`/`.tga`, mapas Albedo,
   Normal, etc.) para dentro de `Assets/Art/Texture/`.
2. Selecione cada textura importada e confira no Inspector:
   - **Texture Type**: `Default` para Albedo/Metallic/Occlusion;
     `Normal Map` especificamente para o mapa de normais.
   - **sRGB (Color Texture)**: ligado para Albedo/cor, **desligado**
     para mapas técnicos (Normal, Metallic, Occlusion, Roughness) —
     eles não são cor, e deixar sRGB ligado neles escurece errado.
   - **Wrap Mode**: `Repeat` (padrão) — necessário pro tiling do passo
     3 funcionar sem borda visível.

> O arquivo `Assets/Art/Texture/floor_tiles_04_4k.blend` é uma cena do
> Blender, não uma textura importável direto. Pra usar esse material,
> exporte os mapas (Albedo/Normal/Roughness etc.) como PNG do Blender
> primeiro e importe os PNGs — a Unity não lê texturas de dentro de um
> `.blend` sozinha.

### 2. Aplicar na Unity

Selecione o material (ex.: `M_Greybox_Floor.mat`) e no Inspector
(shader **Universal Render Pipeline/Lit**):

| Slot do Inspector | Property interna | Pra que serve |
|---|---|---|
| **Surface Inputs ▸ Base Map** | `_BaseMap` | textura de cor/albedo — arraste a textura aqui |
| (quadradinho de cor ao lado do Base Map) | `_BaseColor` | tinge a textura; deixe branco pra cor "pura" da imagem |
| **Surface Inputs ▸ Normal Map** | `_BumpMap` | mapa de normais, se tiver |
| **Surface Inputs ▸ Metallic Map** / slider | `_MetallicGlossMap` | opcional |
| **Surface Inputs ▸ Occlusion Map** | `_OcclusionMap` | opcional |

### 3. Ajustar o tiling (repetição)

As peças greybox são cubos de **6×6m** (piso) esticados por
`localScale`, então uma textura com `Tiling = (1,1)` (o padrão) estica
a imagem inteira sobre os 6 metros — geralmente fica borrada. Ajuste o
**Tiling** (campo ao lado do Base Map, é o `m_Scale` do `_BaseMap`
por baixo) pra bater com o tamanho real da textura:

```
Tiling = cellSize / tamanhoDaTexturaEmMetros
```

Exemplo: se a textura representa 2×2m no mundo real e a célula tem
6m, use **Tiling X = 3, Tiling Y = 3** (a textura repete 3× por
célula). Pra parede (altura 5m, `wallHeight`), calcule Tiling Y
separado usando 5 no lugar de 6.

### 4. Ver o resultado

Editar o `.mat` atualiza a visualização **na hora**, tanto na Scene
view quanto em Play Mode já rodando — como o material é compartilhado
(`sharedMaterial`), não precisa regenerar o andar nem rodar
"Rebuild adjacency" pra ver a textura nova.

### 5. Se quiser um material DIFERENTE só numa peça específica

Em vez de editar `M_Greybox_Wall.mat` (que é global), crie um material
novo (`Assets/Create ▸ Material`, shader `Universal Render
Pipeline/Lit`) e arraste ele só no `MeshRenderer` daquela peça dentro
do prefab (ex.: só no `Pillar` do `Tile_Pillar.prefab`, sem tocar no
`Floor` dele). Isso não afeta as outras peças que ainda usam o
material greybox padrão.
