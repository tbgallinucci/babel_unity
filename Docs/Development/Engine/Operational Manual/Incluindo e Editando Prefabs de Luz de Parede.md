# Incluindo e editando prefabs de fonte de luz (ex.: tocha)

Guia pra criar/editar o prefab que o `BasicLightingPopulator` instancia nas
paredes do andar gerado, e pra ajustar onde exatamente esse anchor nasce na
parede.

## Como a luz nasce no jogo (recapitulando o mecanismo)

1. O **Greybox Tile Generator** (`WFC ▸ Greybox Tile Generator`) planta um
   `SpawnAnchor(kind = Light)` como filho de cada peça de parede reta
   (`Tile_Wall`, `Tile_Corridor`, `Tile_Door_Corridor`) — nunca em canto/beco,
   de propósito.
2. Quando o andar é gerado, o `TileInstancer` coleta esses anchors em
   `floor.Anchors`.
3. O `BasicLightingPopulator` filtra os anchors `kind == Light`, agrupa em
   trechos retos de parede e instancia o **prefab da luz** (o que você vai
   criar aqui) na posição/rotação de 1 a cada N anchors do trecho.

Ou seja: o prefab da luz nunca carrega `SpawnAnchor` — quem carrega o anchor é
a peça de parede. O prefab da luz só precisa respeitar a convenção de pivô e
orientação abaixo.

## Passo a passo: criar/editar o prefab da tocha

### 1. Convenção de pivô e orientação (a parte que não pode errar)

O `BasicLightingPopulator` instancia assim:

```csharp
Instantiate(lightPrefab, anchor.transform.position, anchor.transform.rotation, parent);
```

Ou seja, o **pivô do seu prefab é o ponto de encaixe**, e o eixo **+Z local
(forward)** precisa apontar **pra fora da parede, pra dentro da sala** — é
esse o eixo que o anchor guarda (`Quaternion.LookRotation(inward, Vector3.up)`
em `GreyboxTileGenerator.WallLight()`), e é o mesmo eixo que gira junto quando
o WFC rotaciona a peça de parede em 90°/180°/270°.

- Monte o modelo (malha do suporte + haste + chama) de forma que, com o objeto
  na rotação padrão (identidade), a base/suporte fique voltada para -Z (na
  direção "de onde veio a parede") e o corpo da tocha se estenda para +Z, pra
  dentro do ambiente.
- Não use `Up` inclinado — o anchor sempre usa `Vector3.up` do mundo, sem roll.

### 2. Componentes do prefab

- Malha da tocha (suporte + haste), com collider se fizer sentido (opcional —
  normalmente prop decorativo não precisa).
- Um `Light` (Point ou Spot):
  - **Point Light** é mais barato e mais fácil de acertar (não depende de
    mirar); bom default pra tocha.
  - Se usar **Spot**, lembre que o cone aponta no +Z local do próprio `Light`
    — geralmente alinhado ao forward do prefab, então já sai apontando pra
    dentro da sala se você seguiu o passo 1.
  - **Range**: comece em algo perto do `Cell Size` do seu tileset (6 m no
    greybox) — curto demais e cada tocha vira uma ilha de luz isolada; longo
    demais e várias tochas empilham brilho.
  - **Intensity/Color**: tocha costuma ficar bem com cor quente (laranja) e
    intensidade moderada — ajuste olhando a cena com a ambient escurecida (ver
    o manual de teto/iluminação; sem isso qualquer intensidade parece fraca
    porque a ambient está competindo).
  - **Shadows**: opcional. Sombra por tocha tem custo — se o andar tiver
    dezenas de tochas simultâneas, considere `No Shadows` ou `Soft Shadows`
    só nas mais próximas do jogador (fora do escopo deste guia; por ora, teste
    sem sombra primeiro).
- Efeito visual da chama (partícula, ou um material emissivo simples) —
  opcional, mas é o que vende a leitura de "tocha acesa" à distância antes da
  luz em si aparecer.

### 3. Salvar como prefab

Arraste o GameObject montado (malha + `Light` + partícula, se tiver) pra uma
pasta de props (ex.: `Assets/Art/Props/Torch.prefab` ou
`Assets/WFC/GreyboxTiles/Props/`) pra virar um prefab de verdade.

### 4. Ligar no populador

No GameObject que tem o `Basic Lighting Populator` (Inspector):

- Arraste o prefab da tocha pro campo **Light Prefab**.

Dê Play. Se a tocha nascer flutuando ou de costas pra parede, é o passo 1
(pivô/orientação) que está errado — não é bug no populador.

## Ajustando onde o anchor nasce na parede

Se a tocha nascer torta em relação à parede física (longe/perto demais,
alta/baixa demais), **não precisa editar prefab nenhum de novo** — ajuste os
dois campos que existem exatamente pra isso, em `WFC ▸ Greybox Tile
Generator`:

- **Wall Light Offset** (metros): distância do anchor até o centro da parede.
  Aumente se a tocha estiver enterrada na parede; diminua se estiver flutuando
  longe demais.
- **Wall Light Height** (0–1, fração de `Wall Height`): altura do anchor
  (0 = piso, 1 = teto). Tocha de corredor geralmente fica bem por volta de
  0.6–0.75.

Depois de ajustar, clique em **"Gerar 10 tiles (greybox)"** de novo (é
idempotente, sobrescreve os prefabs existentes no lugar) e rode o **WFC
Tileset Bootstrap** em seguida — os dois sempre andam juntos (ver o manual de
criação de tileset alternativo).

## Erros comuns

- **Tocha nasce olhando pra dentro da parede**: forward do prefab está
  invertido (deveria ser +Z pra fora da parede). Gire o modelo 180° dentro do
  prefab, não no anchor.
- **Zero tochas no andar, mesmo com Light Prefab preenchido**: confira o log
  `[BasicLightingPopulator]` no console — ele avisa quantos anchors candidatos
  existiam. Zero anchors normalmente é tileset sem `Tile_Wall`/`Tile_Corridor`
  regenerado (rodou o Greybox Tile Generator antigo, antes dos anchors
  existirem) — regenere e rebakeie.
- **Tochas empilhadas/uma do lado da outra**: `Every N Walls` no
  `BasicLightingPopulator` está baixo demais para o `Cell Size` do seu
  tileset. Suba o valor.
