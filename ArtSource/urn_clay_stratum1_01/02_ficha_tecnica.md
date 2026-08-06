NOME: urn_clay_stratum1_01
CATEGORIA: prop
ESTRATO: I — Fundação (Babilônia de Nabucodonosor, em declínio)
DIMENSÕES REAIS (m): 0.9 altura x 0.55 diâmetro máximo (pé plano ~0.22 de diâmetro)
PALETA: tijolo cozido / terracota (#A5673C), sombra de barro (#7A5C3E), poeira (#C9A876), azul Ishtar desbotado na faixa pintada (#0047AB), marfim (#FFFFF0)
PASTA DE DESTINO FINAL (Unity): Assets/Art/Props/
ESTILO DE MODELAGEM SUGERIDO: hard-surface geométrico (Rota A — script procedural bpy; corpo de revolução de 8 a 12 lados)

## Descrição do objeto

Urna de armazenamento de barro. O prop "de gente" do kit — é o que transforma um corredor
monumental num lugar onde alguém viveu.

- Corpo: arredondado, afinando para um **pé plano** (fica de pé sozinha).
- Gargalo: curto, com borda grossa e enrolada.
- Alças: duas pequenas, em alça, no ombro da peça.
- Decoração: uma faixa simples de padrão geométrico pintado na barriga, azul desbotado.
- Superfície: barro poroso **não esmaltado**, fosco.
- Estado: **inteira**, borda lascada, rachaduras finas, poeira.

## ⚠ Nota de direção — por que a urna importa mais do que parece

É o único objeto do lote na escala da mão humana. Ele faz duas coisas que nenhuma das
outras 5 peças faz:

1. **Dá régua de escala.** O jogador só sente que a parede tem 6 m se houver algo de
   0.9 m perto dela para comparar.
2. **Vende o D9 ("Babilônia visivelmente vazia demais").** Uma urna intacta, empoeirada,
   ainda no lugar onde alguém a deixou, diz "as pessoas saíram" muito melhor do que
   qualquer escombro diria. **Por isso ela não pode estar quebrada.**

## ⚠ Nota — reuso futuro (não implementar agora)

Esta peça é candidata natural a **objeto destrutível** (quebrar e soltar loot/Seeds — o
padrão de Hades). O sistema não existe hoje e **não faz parte deste lote**. A única coisa
a garantir agora é que a malha seja **um objeto único e simples**, fácil de trocar por
uma versão fraturada depois. Não modelar cacos.

## Notas técnicas

- **Pivô:** base central — X/Z centrados, Y = 0 na base do pé.
- **Chanfro** 0.02–0.04 m (peça pequena e curva — chanfro mínimo).
- **Orçamento de polígonos:** ~200–300 tris. Corpo de revolução com 8–12 lados;
  **não** subdividir para ficar liso.
- **Colisão:** `CapsuleCollider` raio ~0.28, altura 0.9. Sólida.
- **Layer `Level`:** **não** — é pequena o bastante para não valer um furo no navmesh
  (`cell_size = 0.3`; um obstáculo de 0.55 m mal registra e só polui a malha de navegação).
- **Material:** M4 (terracota — textura `Assets/Art/Textures/terracotta_clay_01.png`) + a faixa
  pintada. UV vinda do Blender, direto no Base Map do material URP/Lit.
- **Variação:** se sobrar tempo, gerar 2 variações de escala/rotação no prefab
  (0.9× e 1.1×) — sai de graça e evita que 4 urnas iguais na sala pareçam clones.

## Checklist de validação

- [ ] Pé plano, fica de pé sozinha (não é ânfora grega de fundo pontiagudo)
- [ ] Barro fosco, **não** esmaltado brilhante
- [ ] Inteira, não quebrada / sem cacos
- [ ] Malha única e simples (para troca futura por versão fraturada)
- [ ] Altura 0.9 m — conferir ao lado do prefab do jogador (`PlayerController`) (deve bater na cintura)
- [ ] Colisão sólida (`CapsuleCollider`), **fora** da Layer `Level`
- [ ] Pivô na base central, transforms aplicados
- [ ] ≤ 300 tris

## Medidas verificadas (Blender 5.1, 2026-07-30)

Rodando `04_gerar_modelo.py` headless:

> 304 tris | 0.542 x 0.570 x 0.900 m | pivo na base OK. X e Y diferem entre si porque o corpo tem 10 lados (distancia entre faces != entre quinas) — normal em peca facetada.
