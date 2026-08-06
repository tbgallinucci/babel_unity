# Como funciona a iluminação do Loop

Resumo de arquitetura — não é um "como criar prefab" (isso já existe em
[Incluindo e Editando Prefabs de Luz de Parede.md](Incluindo%20e%20Editando%20Prefabs%20de%20Luz%20de%20Parede.md)),
é o "como as peças se encaixam e por quê". Cinco scripts, cada um com um
trabalho bem separado dos outros:

| Script | Pergunta que ele responde |
|---|---|
| `TorchLight.cs` | O que é "uma tocha" (2 Lights que andam juntas)? |
| `BasicLightingPopulator.cs` | Quais paredes ganham tocha acesa, e quantas? |
| `RegionGraph.cs` + `RegionLightMask.cs` | Como a luz não atravessa parede sem custar shadow map? |
| `DynamicLightBudget.cs` | Quantas tochas podem estar acesas/com sombra ao mesmo tempo? |
| `FloorLightField.cs` | De onde vem aquele "ambiente quente" que preenche o resto da sala? |

## Ordem de execução (`FloorDirector.GoToFloorRoutine`)

```
1. WFCFloorGenerator gera o andar (grid + SpawnAnchor kind=Light em cada
   peça de parede reta — nunca em canto, nunca em parede angulada).
2. BasicLightingPopulator.Populate()   → planta as tochas, decide quais acendem
3. FloorLightField.Rebuild()           → lê a intensidade "de fábrica" das tochas
                                          ANTES do orçamento zerar tudo
4. RegionLightMask.Rebuild()           → carimba tocha + geometria com rendering layer
5. DynamicLightBudget.Rebuild()        → aplica o tier (Key/Fill/Off) a cada tocha
```

A ordem 3→4→5 importa de verdade: `FloorLightField` precisa da intensidade
original (o `DynamicLightBudget` começa tudo apagado, de propósito, pra não
haver um pico de shadow casters no primeiro frame); `RegionLightMask` precisa
rodar antes do orçamento porque é ele quem "carimba" a tocha, e o orçamento só
liga/desliga o que já está carimbado.

## 1. `TorchLight` — uma tocha, duas Lights

Cada tocha tem um **Spot** estreito (~110°) e um **Point** largo, e o
componente existe pra tratar as duas como uma coisa só (nunca "meia tocha
acesa"). A divisão existe por causa da matemática de shadow map: um cone
estreito é ~8× mais barato de sombrear que um Point (que precisa de 6 faces de
cubemap), mas sozinho não preenche a sala — daí o padrão **key + fill**:

- **Key (Spot)** — cuida da oclusão perto da tocha. Inclinado ~30° pra baixo
  no `Awake` (`keyPitchDegrees`) pra evitar acne de sombra no chão em ângulo
  rasante.
- **Fill (Point)** — cuida do preenchimento da sala. **Não tem sombra por
  padrão** — não é só custo: luz de Point casando com as **emendas dos
  prefabs do WFC** (peças encostadas exatamente na borda da célula, sem
  solda) produz uma linha de shadow acne fixa e reta bem na costura, porque a
  malha ali é tecnicamente duas superfícies coincidentes. Bias não resolve
  isso — é geometria, não precisão de shadow map.

`ShadowCasterMode` (`KeyOnly` / `FillOnly` / `Both` / `Neither`) existe pra
**testar** essa troca sem editar Animator nem prefab — só no tier `Key`
(tochas perto do jogador); no tier `Fill` nenhuma das duas projeta sombra,
sempre.

## 2. `BasicLightingPopulator` — quais anchors acendem

O WFC já filtra "nunca canto, só parede reta" de graça (só `Tile_Wall`/
`Tile_Corridor` carregam anchor). Em cima disso, o populador aplica:

- **"A cada N paredes"** (`everyNWalls`) — agrupa anchors em trechos retos
  contíguos e acende 1 a cada N, com a fase de cada trecho vindo de um hash
  determinístico da célula (não de um offset global) — sem isso, trechos em
  posição equivalente do kit acendiam sempre no mesmo lugar relativo e o
  andar lia como grade.
- **Garantia por parede** (`perWallLightBounds`, `minLitPerWall`/
  `maxLitPerWall`, padrão 1–2) — toda parede de **sala** (corredor fica de
  fora) termina com pelo menos 1 e no máximo 2 tochas acesas, promovendo/
  cortando por `cellIndex` quando a regra "a cada N" não bate exatamente
  nesse intervalo. Ligado, isso ignora o filtro de eixo do `oneAxisPerRoom`
  pras paredes de sala (as 4 paredes concorrem, não só o par mais comprido).
- **Tochas decorativas apagadas** (`unlitTorchPrefab` + `unlitTorchDensity`)
  — uma fração dos anchors que não acenderam ganha a mesma luminária, só que
  sem `Light` nenhum — dá a impressão de mais tocha no andar sem custar
  orçamento.

Cada tocha acesa recebe `torch.Region = annotated.GetRegion(anchor.cellIndex)`
— é o dado que o `RegionLightMask` usa depois; a posição sozinha da tocha
(1 m à frente da parede) é ambígua demais perto de fronteira entre salas.

## 3. `RegionGraph` + `RegionLightMask` — luz não atravessa parede

Isto existe porque a maioria das tochas do andar é **Fill sem sombra**
(orçamento) — uma luz sem sombra não sabe que existe parede, então sem
contenção ela vaza pra sala vizinha. A solução não usa shadow map nenhum:
usa **Rendering Layers** do URP.

1. `RegionGraph.BuildLightPartition` fatia o andar em partições: cada sala é
   1 partição, o corredor (que pro resto do jogo é UMA região só) é
   fatiado em pedaços de até `corridorChunkSize` células — sem isso, duas
   curvas de corredor fisicamente próximas mas logicamente "a mesma região"
   vazariam luz uma na outra.
2. `RegionGraph.AssignColors` colore as partições (guloso, 7 cores) tal que
   partições **espacialmente adjacentes nunca compartilham cor** — inclusive
   através de parede sólida sem porta (`BuildAllPartitionNeighbors`; corrigido
   recentemente — antes só considerava borda passável, e duas salas encostadas
   por parede comum podiam cair na mesma cor por coincidência e vazar luz
   bem ali, que era o caso mais comum do mapa, não uma borda rara).
3. Um grafo **separado**, só de borda passável (`BuildLightNeighbors`),
   decide quais cores VIZINHAS entram na máscara de cada tocha — é o que
   deixa luz atravessar uma **porta aberta** de propósito (correto) sem deixar
   atravessar parede (incorreto).
4. `RegionLightMask.StampGeometry`/`StampTorches` escrevem
   `UniversalAdditionalLightData.renderingLayers` (não `Light.renderingLayerMask`
   direto — isso seria sobrescrito por `SyncLightAndShadowLayers()`) em cada
   Renderer e em cada `TorchLight` (nas duas Lights, key e fill).

**Limite conhecido:** isto contém luz **entre** partições, não **dentro** de
uma — um pilar no meio de uma sala grande continua sendo atravessado pelo
Fill. Quem cobre esse caso é a Etapa 3 (`FloorLightField`, granularidade de
célula) e a sombra das poucas tochas Key.

## 4. `DynamicLightBudget` — quantas tochas custam caro

Em Forward+ (o renderer deste projeto) o teto de luzes visíveis é 256 e não
existe limite de 4-por-objeto — luz **sem sombra** é barata e cabe muita. O
que custa é **sombra** (slice de shadow atlas + pass de render extra) e
volume de luz em clustering. Por isso o orçamento não conta "quantas luzes",
conta **quantas sombras**:

- `keyLights` (padrão 2) — as N tochas mais próximas do jogador viram `Key`
  (acesas + sombra).
- `fillLights` (padrão 14) — as próximas depois dessas viram `Fill` (acesas,
  sem sombra) — barato, então pode ser generoso.
- O resto vira `Off`.

Reavaliado por distância a cada `updateInterval` (0.2s por padrão), com
**histerese dupla** pra não tremer: promover de tier é imediato, rebaixar
exige folga tanto no raio (`radiusHysteresis`) quanto na posição na fila
(`rankHysteresis`) — sem isso, duas tochas quase equidistantes trocariam de
tier a cada avaliação.

Indexa `TorchLight`, não `Light` cru — contar Lights individualmente faria o
corte do orçamento cair no meio de uma tocha (Spot ligado, Point desligado, ou
o contrário), que era o bug real de "tocha acende pela metade" antes deste
componente existir.

**Nota de custo real:** com muitas tochas `Key` simultâneas o atlas de shadow
(2048×2048 por padrão) pode não caber todo mundo em resolução cheia — a URP
reduz a resolução por luz automaticamente e avisa no Console
(`Reduced additional punctual light shadows resolution...`). Se a sombra
perto do jogador parecer granulada, é aqui: baixe `keyLights` ou suba o atlas.

## 5. `FloorLightField` — o ambiente entre as poças de luz

Etapa 3: propaga a luz de cada tocha por flood-fill no grid do WFC
(`LightFieldBaker`, no plugin) e publica o resultado como uma `Texture2D`
global, somada por cima de qualquer material via um `FullScreenPassRendererFeature`
— não precisa trocar shader de nenhum objeto da cena.

- A cor/alcance de cada "semente" vem da luz **Fill** de cada tocha (a que
  "vê a sala inteira") — não duplica tuning: subiu o Range do Fill no
  prefab, o bounce acompanha sozinho.
- `intensityMultiplier` (padrão 0.05, comece baixo) existe porque
  `Light.intensity` em URP é Candela, não 0..1 — sem atenuar, o "ambiente"
  fica mais forte que a luz direta que o originou.
- `colorSaturation` (padrão 0.5) simula o bounce perdendo saturação numa
  parede cinza — 1 tinge a cena inteira de laranja puro, 0 vira cinza e perde
  o clima.
- Formato `RGBAHalf`: cor somada passa de 1.0 fácil (Candela), 8 bits
  clamparia a cauda alta do falloff.

## Troubleshooting rápido

| Sintoma | Onde olhar |
|---|---|
| Sala inteira sem luz nenhuma | `BasicLightingPopulator` — Console avisa quantos anchors candidatos existiam; zero geralmente é tileset sem `Tile_Wall` regenerado |
| Luz atravessando parede reta entre 2 salas | `RegionLightMask`/`RegionGraph` — confira `ColorsUsed` no log; se aparecer aviso de "sem cor livre", suba `RegionGraph.ColorCount` |
| Luz atravessando pilar no meio da sala | Limite conhecido do `RegionLightMask` — ajustar `FloorLightField` (granularidade de célula), não a máscara |
| Linha de sombra reta e fixa bem na emenda de duas peças do WFC | Ligou sombra no Point (`ShadowCasterMode != KeyOnly`) — geometria da costura, não bias; volte pra `KeyOnly`/`Neither` |
| Sombra granulada perto do jogador | Atlas de shadow estourado — baixe `DynamicLightBudget.keyLights` ou suba o atlas do URP Asset |
| Tocha "acende pela metade" andando pelo mapa | Sintoma histórico já corrigido pelo `TorchLight`/`DynamicLightBudget` indexarem por tocha, não por Light — se voltar a aparecer, é regressão |
