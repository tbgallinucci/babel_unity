# Concept — `torch_wall_stratum1_01`

**A peça mais importante do lote, apesar de ser a menor.** Ela não é decoração: é a
fonte de luz da sala. A direção de arte do Estrato I diz que a sala é iluminada *por
objetos*, não por luz ambiente — sem a tocha, a sala-vitrine é uma caixa cinza e o
checkpoint V2 não prova nada.

## Prompt positivo (colar no Fooocus)

```
wall-mounted torch sconce for a game environment kit, stylized low-poly game asset, NOT
photorealistic, hand-painted game art in the style of Blasphemous and Hades, an ancient
Mesopotamian bracket torch fixed to a wall, a small square mounting plate of tarnished
dark gold metal, a short angled arm ending in a shallow bowl, a stubby wooden haft wrapped
in aged cloth, a bright warm flame burning at the top, small carved rosette on the mounting
plate, soot staining the wall above the flame, settled dust on the metal, dusty and in
decline — the metal tarnished not polished, color palette strictly: tarnished saffron gold
#F4C430, bright gold where the flame licks it #FFD700, aged wood #3E2E22, dry clay shadow
#7A5C3E, sand dust #C9A876, warm flame orange, orthographic side-profile view showing how
it projects from the wall, clean readable silhouette, plain neutral grey background
```

## Prompt negativo

```
photorealistic, photo, 3d render, blurry, cluttered background, perspective distortion,
text overlay, watermark, signature, modern objects, electric lamp, lantern with glass,
candle, chain, hanging chandelier, green plants, turquoise, marble, bronze, polished shiny
gold, bright daylight, sunlight, ruins, rubble
```

## Config

- Aspect ratio: **1:1**
- ⚠️ **Vista de PERFIL**, não frontal — é o único asset do lote assim. O que precisa ser
  modelado é o quanto ela **projeta da parede** (0.45 m); de frente isso não se vê.
- Mínimo 4 variações · preset de ilustração/game asset
- Salvar em `ArtSource/torch_wall_stratum1_01/03_concept.png`

## ⚠️ O que olhar na hora de escolher a variação

1. **A chama está ACIMA e à FRENTE da parede?** Se a chama estiver colada na parede, a
   luz no jogo vai ficar presa dentro da geometria e não ilumina a sala.
2. **Silhueta legível de longe.** A tocha é o ponto de referência visual do jogador numa
   sala escura — se o contorno for confuso, ela não guia.
3. **Sem corrente/lanterna de vidro.** Isso é linguagem medieval europeia, não
   mesopotâmica. Braço rígido de metal + bacia rasa.
