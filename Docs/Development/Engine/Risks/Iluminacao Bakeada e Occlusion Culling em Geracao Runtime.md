# Risco: iluminação assada e Occlusion Culling não funcionam em geração runtime

**Status:** identificado, não mitigado. Sem impacto prático hoje (kit
greybox); vira relevante quando o tileset trocar por arte de produção.

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

## Mitigação proposta (não implementada — anotar como próximo passo antes da troca de arte)

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
