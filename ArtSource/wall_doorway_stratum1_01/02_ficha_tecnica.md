NOME: wall_doorway_stratum1_01
CATEGORIA: parede
ESTRATO: I — Fundação (Babilônia de Nabucodonosor, em declínio)
DIMENSÕES REAIS (m): 6.0 altura x 4.0 largura x 0.6 profundidade — vão de passagem 2.8 altura x 1.6 largura, centrado em X
PALETA: idêntica a wall_brick_stratum1_01 — tijolo cozido (#A5673C), sombra de barro (#7A5C3E), poeira (#C9A876), azul Ishtar (#0047AB), azul noturno (#191970), ouro embaçado (#F4C430), marfim (#FFFFF0)
PASTA DE DESTINO FINAL (Unity): Assets/Art/Environment/
ESTILO DE MODELAGEM SUGERIDO: hard-surface geométrico (Rota A — script procedural bpy)

## Descrição do objeto

Variante do módulo de parede com vão de passagem. Substitui o antigo greybox do Godot
`doorway_6m_dark_rock.tscn`, mesmas medidas externas.

- Corpo: **idêntico** ao `wall_brick_stratum1_01` (mesma fiada de tijolo, mesmo friso azul,
  mesmo plinto) — é literalmente a mesma parede com um buraco.
- Vão: retangular, **verga reta** (não arco), 1.6 m de largura × 2.8 m de altura,
  centrado no eixo X do módulo.
- Moldura: borda em relevo de tijolo esmaltado azul contornando o vão, com uma roseta de
  ouro embaçado em cada canto superior.
- Soleira: o plinto da base é interrompido pelo vão (a passagem é rente ao chão — nada de
  degrau, o jogador atravessa correndo).

## ⚠ Nota técnica crítica — dimensão do vão

A grade herdada do Godot define altura de agente **1.9 m** e cápsula do jogador de **raio 0.4** (conferir contra o `PlayerController` do Unity).
O vão de **1.6 × 2.8 m** dá folga confortável (4× o raio na largura, ~1.5× a altura do
jogador) para atravessar em pleno movimento, inclusive durante uma esquiva/dash lateral.

🚫 **Não reduzir** para "ficar mais proporcional ao concept". Um vão apertado num jogo de
ação com dash vira um ponto de travamento — o custo de gameplay supera o ganho visual.
Se o concept vier com vão estreito, a modelagem obedece a ficha, não o concept.

## ⚠ Nota técnica — emenda entre módulos

Vale integralmente a mesma regra do `wall_brick_stratum1_01`:
o friso azul e o plinto **encostam nas bordas X = ±2.0 na mesma altura Y** que a peça lisa,
porque as duas se alternam na mesma fileira de parede. Se as duas não baterem, a emenda
aparece.

Esta peça **não precisa ser espelhável** (é simétrica em X por construção).

## Notas técnicas

- **Pivô:** base central — X/Z centrados, Y = 0 na base.
- **Chanfro** 0.05–0.10 m nas arestas duras, **incluindo as do vão** (a aresta do vão é
  a mais vista do asset — o jogador passa a 30 cm dela).
- **Orçamento de polígonos:** ~350–500 tris.
- **Colisão:** **NÃO** usar um Box único — o vão precisa ser atravessável. Montar com
  3 `BoxCollider`: painel esquerdo, painel direito e verga acima do vão. (Trimesh aqui é
  desperdício; 3 boxes resolvem exato.)
- **Layer `Level` (coletada pelo `NavMeshSurface`)** no nó raiz — e conferir no jogo que o navmesh **atravessa** o vão
  (se o inimigo não persegue pela porta, a colisão ficou fechada).
- **Material:** mesmos M1 + M2 + M3 do módulo liso, UV do Blender direto no Base Map.

## Checklist de validação

- [ ] Vão retangular de verga reta, **não** em arco
- [ ] Vão mede 1.6 × 2.8 m, centrado em X
- [ ] Friso e plinto alinham com `wall_brick_stratum1_01` (comparar lado a lado)
- [ ] Colisão em 3 boxes, vão atravessável
- [ ] Navmesh passa pelo vão (testar com um inimigo perseguindo)
- [ ] Escala externa: 4.0 × 6.0 × 0.6 m exatos
- [ ] Pivô na base central, transforms aplicados
- [ ] ≤ 500 tris

## Medidas verificadas (Blender 5.1, 2026-07-30)

Rodando `04_gerar_modelo.py` headless:

> 480 tris | 4.000 x 0.760 x 6.000 m | pivo na base OK. Chegou ao alvo com `BEVEL_SEG = 1`: com 2 segmentos custava **1120 tris** (2.2x o orcamento).

> 🔴 **O azul é ALVENARIA, não pintura** (regra de 2026-08-04, `direcao_estrato_I.md`).
> A área azul usa **M2** (`Assets/Art/Textures/brick_glazed_blue_01.png`) com **o mesmo
> tiling do barro** — mesma fiada, mesma junta, mesmo módulo. Se entrar como cor chapada
> vira tinta, e é exatamente esse o defeito que a regra existe para evitar.