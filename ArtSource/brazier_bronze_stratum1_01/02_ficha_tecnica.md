NOME: brazier_bronze_stratum1_01
CATEGORIA: prop (fonte de luz diegética)
ESTRATO: I — Fundação (Babilônia de Nabucodonosor, em declínio)
DIMENSÕES REAIS (m): 1.1 altura x 0.8 diâmetro da bacia (base quadrada 0.6 x 0.6)
PALETA: ouro açafrão embaçado (#F4C430), ouro brilhante no lambido do fogo (#FFD700), tijolo cozido (#A5673C), sombra de barro (#7A5C3E), poeira (#C9A876), laranja de brasa (#FF9A3C)
PASTA DE DESTINO FINAL (Unity): Assets/Art/Props/
ESTILO DE MODELAGEM SUGERIDO: hard-surface geométrico (Rota A — script procedural bpy; bacia como corpo de revolução de 8 a 12 lados)

## Descrição do objeto

Braseiro de chão — segunda fonte de luz do Estrato I, para o **centro** da sala.

- Base: bloco de pedra quadrado escalonado, 0.6 × 0.6 m, ~0.15 m de altura.
  **Mesma linguagem do plinto da parede e da coluna** — é o que amarra o kit.
- Pernas: três, curtas e abertas, saindo da base para a bacia.
- Bacia: rasa e larga, Ø0.8 m, metal dourado fosco, com uma faixa de rosetas em relevo
  contornando a borda.
- Conteúdo: brasas e fogo baixo.
- Sujeira: fuligem e cinza manchando o metal, poeira assentada.

## ⚠ Nota técnica crítica — a luz faz parte do prefab

Mesma regra da tocha. O Prefab `Assets/Prefabs/Props/brazier_bronze_stratum1_01.prefab` precisa de:

1. Uma **`Light` (`Type = Point`)** no centro da bacia:
   - cor quente ~`#FF9A3C`, `light_energy` ~2.5, `omni_range` **10–14 m**
     (maior que a tocha — este é o que ilumina o miolo da sala)
   - `Shadow Type = Soft Shadows`
2. **Material emissivo** nas brasas (shader Lit com **Emission** ligada, cor `#FF7A1C`).
3. Flicker sutil — opcional na primeira passada.

⚠️ **Cuidado de performance:** com tocha (8–12 m) + braseiro (10–14 m), uma sala 20×20 m
pode acabar com muitas luzes com sombra sobrepostas, e sombra dinâmica é o item mais caro
do Forward+. Regra prática para a sala-vitrine: **no máximo ~4 luzes com sombra
simultâneas** no volume da sala. Se precisar de mais pontos de fogo, as extras entram com
`shadow_enabled = false` (ainda iluminam, só não projetam sombra) — visualmente quase
idêntico, custo muito menor.

## ⚠ Nota de gameplay — altura e colisão

Altura total **1.1 m** = altura de cintura do jogador (que tem ~1.9 m). Isso é
deliberado: o braseiro fica **abaixo da linha de visão** e não bloqueia a câmera de
terceira pessoa nem o telegraph de ataque no chão.

⚠️ **Correção medida (2026-07-28):** o script entrega **1.315 m de bounding box total**, não
1.1. A diferença é **só a chama** — o corpo sólido (base + pernas + bacia + aro) termina em
**1.03 m**, que é exatamente a altura de cintura pretendida. A chama subindo até 1.32 m está
certa e continua bem abaixo da linha de visão do jogador (~1.9 m).

Colisão: `CapsuleCollider` raio ~0.42, **altura 1.05** — só o corpo sólido, **a chama fica
fora do collider**. Sólido, o jogador não atravessa. Um braseiro atravessável no meio da
sala destrói a leitura de espaço no combate; um collider que engloba a chama faz o jogador
esbarrar no fogo, que é pior.

## Notas técnicas

- **Pivô:** base central — X/Z centrados, Y = 0 na base de pedra.
- **Chanfro** 0.03–0.06 m.
- **Orçamento de polígonos:** ~300–450 tris.
- **Layer `Level` (coletada pelo `NavMeshSurface`):** **sim** — é obstáculo real, o inimigo tem que contorná-lo.
- **Material:** M3 (ouro fosco) + M1/pedra na base + material emissivo das brasas.

## Checklist de validação

- [ ] **Corpo sólido ~1.03 m** (cintura do jogador — testar com o prefab do jogador ao lado).
      Com a chama o bounding box vai a ~1.32 m; isso é esperado, não é erro
- [ ] Bacia rasa e larga, não caldeirão fundo
- [ ] Base quadrada escalonada, rimando com o plinto da parede e da coluna
- [ ] `Light` (`Type = Point`) na bacia, range 10–14 m, sombra ligada
- [ ] Máximo ~4 luzes com sombra na sala (as extras sem sombra)
- [ ] Colisão sólida: `CapsuleCollider` raio ~0.42, **altura 1.05** (a chama fica FORA do
      collider), na Layer `Level`
- [ ] Pivô na base central, transforms aplicados
- [ ] ≤ 450 tris

## Medidas verificadas (Blender 5.1, 2026-07-30)

Rodando `04_gerar_modelo.py` headless:

> 246 tris | 0.840 x 0.840 x 1.315 m (corpo sólido até 1.03; o resto é chama) | pivô na
> base OK. Diâmetro 0.84 vs. Ø0.8 da ficha — o aro é levemente mais largo que a bacia,
> diferença irrelevante.
