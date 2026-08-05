# Risco: iluminação assada e Occlusion Culling não funcionam em geração runtime

**Status:** identificado e **mitigado** (implementação em 2026-08-05, ver
"Mitigações implementadas" no fim). Continua valendo como registro do risco
porque as mitigações precisam ser **calibradas e validadas** na troca do
greybox por arte de produção — é lá que o problema deixa de ser teórico.

**Origem:** relato de terceiro
([thread r/Unity3D](https://reddit.com/r/Unity3D), captura em anexo na
conversa de 2026-08-05) descrevendo exatamente os dois sintomas abaixo num
gerador tile-based recursivo parecido com o nosso, abandonado por causa
disso: "unable to recalculate light probes or bake GI into prefabs... also
Occlusion Culling is a big problem — insane overdraw tanking performance
whenever player looks through most of generated level".

## Por que isso nos atinge também

A causa raiz dos dois problemas é a mesma, e é estrutural ao projeto, não
específica do relato de terceiro: **Light Probes, GI assada (lightmap) e
Occlusion Culling nativos do Unity são todos calculados em EDIT TIME, contra
geometria ESTÁTICA.** O nosso andar é instanciado em **runtime**
(`TileInstancer.Build`, chamado a cada seed) — não existe cena fixa pra
assar nada disso contra. Ver [WFCFloorGenerator.cs](../../../../Assets/WFC/Runtime/WFCFloorGenerator.cs)
e [TileInstancer.cs](../../../../Assets/WFC/Runtime/TileInstancer.cs).

### 1. Light Probes / GI assada

Sem geometria fixa em editor, não tem como assar. Light Probes bakeados
contra o layout de UMA seed não fazem sentido pra outra seed — a posição das
salas muda. Adaptive Probe Volumes com *runtime baking* (Unity 6 / URP 17+)
existem, mas são recurso recente e complexo; não é a primeira coisa a tentar.

**Onde já estamos protegidos:** as luzes do `BasicLightingPopulator`
([BasicLightingPopulator.cs](../../../../Assets/Scripts/Floor/BasicLightingPopulator.cs))
já são 100% realtime (Point/Spot), sem depender de bake nenhum — essa decisão
foi tomada (sem querer, na origem) durante a implementação da luz de parede,
mas evita esse problema por completo. Ver também a conversa sobre a Ambient
Source do Environment Lighting (mesma raiz: sem GI assada, ambient de skybox
vaza através do teto — resolvido mudando Ambient Source pra `Color` escura,
não é bug de código).

**Regra prática:** não introduzir Light Probes nem GI assada (Baked/Mixed
Lightmapping) enquanto a geração continuar 100% runtime. Se precisar de mais
qualidade de luz indireta no futuro, pesquisar Adaptive Probe Volumes com
baking runtime antes de tentar o pipeline de bake tradicional.

### 2. Occlusion Culling

O Occlusion Culling nativo do Unity (`Window ▸ Rendering ▸ Occlusion
Culling`) também é baked contra layout fixo — inútil pra layout que muda a
cada seed. Sem ele, sobra só **Frustum Culling**, que não basta: ele descarta
o que está fora do campo de visão da câmera, mas **não** o que está atrás de
uma parede e ainda dentro do frustum. Olhar num corredor comprido pode
mandar a GPU processar salas inteiras fisicamente escondidas atrás de
parede, só porque caem dentro do cone da câmera.

**Por que não sentimos isso ainda:** o kit greybox
([GreyboxTileGenerator.cs](../../../../Assets/WFC/Editor/GreyboxTileGenerator.cs))
é cubo primitivo — poligonagem e contagem de material irrisórias, cada peça
é praticamente grátis de desenhar. O overdraw existe, mas não dói.

**Quando vira problema:** na troca do tileset greybox por arte de produção
(mesh mais pesada, mais materiais, mais draw calls por peça) — aí overdraw de
sala inteira escondida atrás de parede passa a custar de verdade.

## Mitigações implementadas

Quatro frentes, em ordem de esforço/ganho. As duas primeiras são fechadas
(não pedem calibragem); as duas últimas expõem parâmetros que precisam ser
afinados jogando.

### 1. Static batching em runtime — `WFCFloorGenerator.combineStaticBatches`

`StaticBatchingUtility.Combine` funde as peças por material depois de
instanciar. Diferente do Occlusion Culling, **o static batching funciona em
runtime** — a única exigência é que a geometria não se mova depois, o que é
verdade pro casco do andar.

Roda **depois** do bake de NavMesh de propósito: o bake lê as malhas peça a
peça, e combiná-las antes poderia mudar o que ele enxerga.

Custo: duplica dado de malha na memória. Se memória virar gargalo num andar
grande, é o primeiro item a reconsiderar.

### 2. GPU Instancing nos materiais gerados — `GreyboxTileGenerator.MakeMat()`

`mat.enableInstancing = true` nos 3 materiais do greybox. Ligado por código
porque esses materiais são **gerados** — marcar o checkbox à mão se perderia
na próxima regeração.

⚠️ **Materiais de arte autorados fora do gerador precisam do checkbox
"Enable GPU Instancing" marcado manualmente.** Item de checklist na troca de
tileset.

### 3. Orçamento de luz realtime — `DynamicLightBudget`

Mantém só as N luzes mais próximas do jogador ligadas (`Light.enabled`),
reavaliando a cada `updateInterval`. Resolve dois problemas: o limite prático
de luzes adicionais **por objeto** do URP/Built-in (passar do limite não dá
erro — o Unity descarta silenciosamente as mais fracas, e luz "some" de forma
imprevisível) e o custo de sombra, se alguma tocha tiver sombra ligada.

**Precisa de calibragem:** `maxActiveLights` e `activeRadius`. Sintoma de
valor baixo demais = buraco escuro perceptível andando pelo corredor.

### 4. Room streaming — `RoomStreamer`

O substituto caseiro do Occlusion Culling. Usa o fato de que o esqueleto já
entrega um **grafo de espaços conectados** (regiões do `AnnotatedGrid`:
corredores são a região 0, cada sala tem a sua): se o jogador está na sala A,
tudo que ele pode ver está a poucos passos de A nesse grafo, porque a única
forma de enxergar outra sala é através das portas/vãos. Então
`visível = BFS a partir da região do jogador, até N passos`.

**Sobre usar o "cone de visão" (frustum) em vez do grafo:** frustum sozinho
não adiciona nada — o Unity já faz frustum culling por `Renderer`, de graça,
e o problema é justamente o que está *dentro* do cone mas *atrás de uma
parede*. Mas frustum ajuda **por cima** do grafo, em granularidade de região:
regiões além de `alwaysVisibleDepth` também precisam intersectar o frustum
pra ficarem ligadas. Isso corta a sala que está a 2 portas de distância mas
atrás da câmera, **sem** nunca piscar a sala vizinha (que fica ligada por
profundidade, mesmo fora do cone) — que é de onde o pop-in viria.

Só mexe em `Renderer.enabled`, nunca em `SetActive`: desligar o GameObject
levaria junto os colliders, e o jogador atravessaria o chão de uma sala que
ainda não "vê" mas na qual pode estar prestes a entrar.

**Precisa de calibragem:** `visibleDepth` e `alwaysVisibleDepth`. Sintoma de
valor baixo demais = dá pra ver sala aparecendo/sumindo (pop-in) enquanto
anda. Valor alto demais = não economiza nada.

**Plumbing que isso exigiu:** `TileInstancer.Build` agora preenche um
`PiecesByCell` (peça instanciada por célula), exposto via `FloorFillResult` e
`GeneratedFloor` — sem isso o consumidor teria que redescobrir a célula de
cada peça pelo nome ou pela posição.

## Mitigação originalmente proposta (histórico)

Diferente do caso genérico, este projeto já tem a estrutura de dados certa
pra um occlusion caseiro barato, porque o esqueleto já é um grafo de
salas/corredores conectados: `AnnotatedGrid` / `SkeletonGenerator.Room` /
regiões ([SkeletonGenerator.cs](../../../../Assets/WFC/Runtime/SkeletonGenerator.cs),
[AnnotatedGrid.cs](../../../../Assets/WFC/Runtime/AnnotatedGrid.cs)).

Ideia (tipo "portal culling" de baixo esforço, sem o sistema baked do
Unity):

1. Cada peça instanciada já pode saber de qual célula/sala veio — o
   mecanismo existe hoje pra `SpawnAnchor.cellIndex`
   ([TileInstancer.cs](../../../../Assets/WFC/Runtime/TileInstancer.cs)),
   dá pra estender pra toda a geometria instanciada (`TileInstancer.Build`
   já sabe a célula de cada peça no laço de instanciação).
2. Um script de "room streaming" desliga o `Renderer` (ou o GameObject
   inteiro) das salas fora de um raio/distância de BFS a partir da sala
   atual do jogador, andando pelo grafo de regiões que o esqueleto já
   construiu.
3. Não precisa ser sofisticado: threshold simples de "N salas de distância
   pelo grafo de corredores" já corta a maior parte do overdraw de sala
   completamente fora de alcance visual possível.

## Quando revisitar

Antes de trocar o kit greybox por arte de produção (ver
[Como Criar um Tileset Alternativo.md](../Operational%20Manual/Como%20Criar%20um%20Tileset%20Alternativo.md)) —
esse é o gatilho natural pra ambos os riscos deixarem de ser teóricos.
