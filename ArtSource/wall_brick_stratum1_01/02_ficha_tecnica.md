NOME: wall_brick_stratum1_01
CATEGORIA: parede
ESTRATO: I — Fundação (Babilônia de Nabucodonosor, em declínio)
DIMENSÕES REAIS (m): 6.0 altura x 4.0 largura x 0.6 profundidade
PALETA: tijolo cozido (#A5673C), sombra de barro seco (#7A5C3E), poeira/areia (#C9A876), azul Ishtar esmaltado (#0047AB), azul noturno (#191970), ouro açafrão embaçado (#F4C430), marfim (#FFFFF0)
PASTA DE DESTINO FINAL (Unity): Assets/Art/Environment/
ESTILO DE MODELAGEM SUGERIDO: hard-surface geométrico (Rota A — script procedural bpy, no padrão do beveled_box.gd)

## Descrição do objeto

Módulo de parede reto do kit de ambiente do Estrato I. Substitui o antigo greybox do Godot
`wall_straight_tiles_6m.tscn`, mantendo exatamente as mesmas medidas.

- Corpo: fiadas horizontais de tijolo de barro cozido, juntas de argamassa visíveis.
- **Friso**: uma faixa horizontal contínua de tijolo esmaltado azul na altura do peito
  (~1.6 m do chão), com uma fileira de rosetas de ouro embaçado.
- Terço inferior: inscrição cuneiforme rasa e gasta (relevo, não textura pintada).
- Base: plinto baixo escalonado (~0.25 m de altura, ~0.1 m de saliência).
- Estado: **íntegra, mas velha** — poeira assentada, rachaduras finas de secagem.
  **Não é ruína** (canon D9: a Babilônia está vazia demais, não destruída).

## ⚠ Nota técnica crítica — emenda entre módulos

A sala padrão tem 20 m de lado = **5 módulos em fila por parede**. Portanto:

1. O **friso azul e o plinto devem encostar exatamente nas duas bordas laterais (X = ±2.0),
   na mesma altura Y**, sem margem. Se sobrar borda lisa, aparece um degrau visível a cada
   4 m e a parede da sala não lê como uma parede só.
2. A geometria deve ser **espelhável em X sem quebrar** — assim uma fileira de 5 módulos
   alterna original/espelhado e não parece clonada. Consequência: nada de detalhe que só
   funcione num sentido (ex: uma rachadura que "escorre" para um lado só).
3. A variação de desgaste/poeira fica na **textura**, não na geometria — é o que permite
   reusar a mesma malha 20 vezes por sala.

## Notas técnicas

- **Pivô:** base central — X/Z centrados, Y = 0 na base (ver `.design/direcao_estrato_I.md` §3).
- **Grade:** largura 4.0 m e altura 6.0 m vêm da grade padrão do projeto (`Docs/Development/Art/direcao_estrato_I.md` §3).
  Nenhuma das duas é ajustável.
- **Chanfro** de 0.05–0.10 m nas arestas duras (plinto, borda do friso).
- **Orçamento de polígonos:** alvo ~200–350 tris. É a peça mais instanciada do jogo — cada
  triângulo aqui é multiplicado por ~20 por sala.
  > ✅ **Medido: 276 tris** — dentro do alvo. Chegou lá com `BEVEL_SEG = 1`: com 2
  > segmentos a peça custava **532 tris**. Um segmento já mata a aresta viva de 90°, que
  > era o objetivo declarado; o segundo só arredonda mais. Não vale 2× de geometria numa
  > peça instanciada 20× por sala.
  > Se ainda precisar cortar: `ROSETTE_COUNT = 3` em vez de 5.
- **Colisão:** `BoxCollider` simples 4.0 × 6.0 × 0.6 (não seguir a geometria do friso).
- **Layer `Level` (coletada pelo `NavMeshSurface`)** no nó raiz do prefab — é assim que o navmesh do andar é gerado.
- **Material:** M1 (tijolo, textura em `Assets/Art/Textures/`: `old_stone_wall_diff_4k.jpg` ou `sandstone_cracks_diff_4k.jpg`)
  + M2 (esmalte azul — **a única textura que falta gerar neste lote**) + M3 (ouro fosco,
  material puro sem textura). Como o modelo vem do Blender com UV própria, **não usar
  nada de projeção triplanar: usar a UV direto no Base Map do material URP/Lit.

## Checklist de validação

- [ ] Silhueta legível em miniatura de 64px
- [ ] Friso e plinto encostam nas duas bordas, mesma altura
- [ ] Espelhável em X sem quebrar a leitura
- [ ] Paleta só do `direcao_estrato_I.md` §1 (sem turquesa/verde/mármore/bronze)
- [ ] Íntegra, não em ruína
- [ ] Escala: 4.0 × 6.0 × 0.6 m exatos
- [ ] Pivô na base central, transforms aplicados
- [ ] ≤ 350 tris

> 🔴 **O azul é ALVENARIA, não pintura** (regra de 2026-08-04, `direcao_estrato_I.md`).
> A área azul usa **M2** (`Assets/Art/Textures/brick_glazed_blue_01.png`) com **o mesmo
> tiling do barro** — mesma fiada, mesma junta, mesmo módulo. Se entrar como cor chapada
> vira tinta, e é exatamente esse o defeito que a regra existe para evitar.