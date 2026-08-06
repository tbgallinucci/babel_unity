NOME: column_lapis_stratum1_01
CATEGORIA: parede / estrutura
ESTRATO: I — Fundação (Babilônia de Nabucodonosor, em declínio)
DIMENSÕES REAIS (m): 6.0 altura x 0.9 diâmetro do fuste (base do plinto: 1.1 x 1.1 quadrada)
PALETA: tijolo cozido (#A5673C), sombra de barro (#7A5C3E), poeira (#C9A876), azul Ishtar (#0047AB), azul noturno (#191970), ouro embaçado (#F4C430)
PASTA DE DESTINO FINAL (Unity): Assets/Art/Environment/
ESTILO DE MODELAGEM SUGERIDO: hard-surface geométrico (Rota A — script procedural bpy; corpo de revolução de baixa contagem, 8 a 12 lados)

## Descrição do objeto

Coluna de tijolo do Estrato I, do chão ao teto. Substitui o antigo greybox do Godot
`column_6m_white_marble.tscn` — que era **mármore branco**, material do
Estrato IV (era alexandrina), não do I. Esta peça corrige essa dissonância.

- Plinto: base quadrada escalonada, 1.1 × 1.1 m, ~0.3 m de altura.
- Fuste: cilíndrico liso de tijolo, sem caneluras. **8 a 12 lados**, não redondo de
  verdade — o facetamento é intencional e é parte do estilo estilizado.
- Banda: faixa de tijolo esmaltado azul envolvendo o fuste **no terço superior**
  (~4.3 m do chão).
- Capitel: bloco simples, sem ornamento figurativo, coroado por uma fileira de rosetas de
  ouro embaçado.
- Inscrição cuneiforme rasa correndo verticalmente por um lado do fuste.
- Estado: **inteira**, empoeirada, rachaduras finas.

## ⚠ Nota de direção — por que a banda azul fica no alto

O friso da parede (`wall_brick_stratum1_01`) está na altura do peito (~1.6 m). Se a banda
da coluna ficasse na mesma altura, a sala inteira ganharia uma listra azul horizontal
contínua e o olho pararia de ler os volumes separados. Com a banda da coluna no alto, os
dois acentos azuis se **alternam em altura** — a sala ganha profundidade em vez de virar
papel de parede listrado.

## Notas técnicas

- **Pivô:** base central — X/Z centrados, Y = 0 na base do plinto.
- **Posicionamento:** a coluna assenta em interseções da grade de 4 m
  (4.0 m, ver direcao_estrato_I §3). Não é peça de parede — fica solta no volume da sala.
- **Chanfro** 0.05–0.10 m nas arestas do plinto e do capitel.
- **Orçamento de polígonos:** ~250–400 tris. Fuste com 8–12 lados; **não** subdividir para
  "ficar redondo" — sobe o custo e sai do estilo.
- **Colisão:** `CapsuleCollider` (ou Box) aproximando o fuste, raio ~0.5. Ignorar o plinto
  na colisão (o jogador não precisa tropeçar em 0.3 m).
- **Layer `Level` (coletada pelo `NavMeshSurface`)** no nó raiz — a coluna deve furar o navmesh, senão os inimigos
  perseguem atravessando ela.
- **Material:** M1 (tijolo) + M2 (esmalte azul) + M3 (ouro fosco). UV do
  Blender).

## Checklist de validação

- [ ] Não parece coluna grega (sem caneluras, sem acanto, capitel em bloco)
- [ ] Inteira, não quebrada
- [ ] Banda azul no terço superior (~4.3 m), não na altura do friso da parede
- [ ] Fuste facetado 8–12 lados, não liso-redondo
- [ ] Altura exata 6.0 m (encosta no teto da sala)
- [ ] Pivô na base central, transforms aplicados
- [ ] ≤ 400 tris

## Medidas verificadas (Blender 5.1, 2026-07-30)

Rodando `04_gerar_modelo.py` headless:

> 284 tris | 1.100 x 1.100 x 6.000 m (base do plinto; fuste O0.9) | pivo na base OK. Com `BEVEL_SEG = 2` custava 476 tris.

> 🔴 **O azul é ALVENARIA, não pintura** (regra de 2026-08-04, `direcao_estrato_I.md`).
> A área azul usa **M2** (`Assets/Art/Textures/brick_glazed_blue_01.png`) com **o mesmo
> tiling do barro** — mesma fiada, mesma junta, mesmo módulo. Se entrar como cor chapada
> vira tinta, e é exatamente esse o defeito que a regra existe para evitar.