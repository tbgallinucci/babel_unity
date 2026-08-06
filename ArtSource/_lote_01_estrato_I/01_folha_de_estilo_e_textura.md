# Lote 01 — Folha de estilo + textura M2

Duas gerações extras, além dos 6 concepts individuais. Rodar **na mesma sessão** de Fooocus.

---

## 🔴 REVISÃO 2026-07-31 — a folha de estilo da seção A não é gerável

Testado no Fooocus 2.5.5 / SDXL: **o plano da seção A estava errado desde que foi escrito.**

Modelos de difusão **não têm controle de composição nem de contagem de objetos**. Pedir
"seis peças distintas, enfileiradas, espaçadas, com escala relativa correta" não produz
isso — produz uma ou duas peças e o resto vira textura de fundo. Confirmado em 4 gerações
(`A_01`..`A_04`): a A_04 entregou **um** pedestal, a A_02 entregou **uma** placa azul.
Não é ajuste de prompt; é limitação de arquitetura.

### Plano correto
1. Gerar as **6 peças individualmente** (os `01_prompt_concept.md` de cada pasta).
2. Usar **os mesmos parâmetros** nas 6 (mesmo estilo, mesma seed base, mesmo boilerplate
   de paleta/luz/enquadramento). É daí que vem a coerência do kit — de parâmetros
   compartilhados, não de uma diagramação que o modelo não sabe fazer.
3. **Montar a folha de comparação por script**, juntando os 6 PNGs lado a lado. É
   determinístico, de graça, e é essa folha que decide o Gate 1.

O objetivo do Gate 1 **não muda**: julgar se as 6 peças parecem o mesmo jogo. Só o meio de
produzir o artefato muda.

### ✅ Configuração de estilo que FUNCIONA (validada em 6 gerações, 2026-07-31)

| Estilo | Marcar? |
|---|---|
| `Fooocus V2` | ✅ sim (expansão de prompt, não empurra para foto) |
| `SAI Digital Art` | ✅ **sim — obrigatório** |
| `Fooocus Sharp` | 🔴 **NÃO — desmarcar** (vem ligado por padrão) |
| `Fooocus Enhance` | 🔴 não |

`SAI Digital Art` injeta `concept art {prompt} . digital artwork, illustrative, painterly,
matte painting, highly detailed` e nega `photo, photorealistic, realism`. Sem ele o
`juggernautXL` — que é um modelo **treinado para fotorrealismo** — volta sozinho para foto
de estúdio. Ele é o que segura a direção.

> Descartado: `SAI Lowpoly`. Nega `highly detailed, ultra textured`, o que mataria as
> fiadas de tijolo e o friso — exatamente o detalhe que precisamos ler no concept.

### 🔴 A armadilha do `Fooocus Sharp` — causa de imagens EM BRANCO

`Fooocus Sharp` vem **marcado por padrão** e injeta:
- no **positivo**: `cinematic still ... shot on kodak, 35mm photo, film grain, grainy`
- no **negativo**: `anime, cartoon, graphic, ..., painting, crayon, graphite, abstract`

Nossos prompts pedem `hand-painted game art, NOT photorealistic` e negam `photorealistic,
photo`. Resultado: o modelo recebe **"foto 35mm" no positivo e "photo" no negativo**, e
**"hand-painted" no positivo e "painting" no negativo**, ao mesmo tempo. Empurrado e
puxado no mesmo conceito com sinais opostos, ele colapsa para **campo chapado creme**.

Diagnosticado por eliminação (prompt ✗, negativo ✗, resolução ✗, VRAM ✗, reiniciar ✗).
**Se aparecer imagem em branco, o primeiro suspeito é este.**

### ✅ RESOLVIDO 2026-08-04 — o azul tomando a peça inteira

**A alavanca nº 1 (encurtar o prompt) funcionou.** Com ~30 palavras em vez de ~120, o
vermelho passou a dominar e o azul ficou confinado. O SDXL perdia a amarração
`blue`→`stripe` em prompt longo e espalhava a cor pelo substantivo mais próximo.
Não foi preciso usar sintaxe de peso nem lista — a alavanca mais barata bastou.

### 🔴 Segunda armadilha isolada: `orthographic front elevation`

Para nós significa "vista de frente, sem perspectiva". Para o SDXL significa
**"amostra de textura chapada"** — ele para de desenhar um objeto e desenha um material,
preenchendo o quadro e descartando silhueta *e* friso.

Estava no prompt original desde o início. É co-responsável pela `tentativa_2`, que eu
tinha atribuído ao prompt longo — atribuição errada, agora isolada em teste controlado.

**Substituto que funciona:** `front view` + `the whole panel visible from top to bottom`
+ `plain grey background`, com `seamless texture, tiling texture, macro, zoomed in` no
negativo.

### 📋 Tokens com efeito medido (12 gerações, 2026-07-31 → 2026-08-04)

| Token / ajuste | Efeito |
|---|---|
| prompt curto (~30 palavras) | ✅ resolve o azul tomando a peça |
| `front view` + `plain grey background` | ✅ produz objeto com silhueta legível |
| `SAI Digital Art` | ✅ segura contra o fotorrealismo do juggernautXL |
| `orthographic front elevation` | 🔴 colapsa em swatch de textura |
| `Fooocus Sharp` | 🔴 imagem em branco |
| `MRE Ancient Illustration` | 🔴 estética de arte rupestre |

### ⚠️ Problema que RESTA — o friso na altura do peito

Em 12 gerações, **nenhuma** colocou a faixa azul contínua na altura do peito encostando
nas duas bordas — que é o **critério nº1** do `01_prompt_concept.md`. O azul aparece como
várias listras, como cruz, no topo feito capitel, ou na base feito plinto. Some quando o
negativo é reforçado demais.

Persiste também o enquadramento de **foto de catálogo** (painel encostado em parede de
estúdio, sombra no chão), mesmo com `product photography, leaning, drop shadow` no negativo.

> 💡 **Reenquadramento que vale considerar antes de gastar mais rodadas:** a posição do
> friso **já está travada no modelo** — `04_gerar_modelo.py` tem `FRIEZE_Z = 1.60` e
> largura 4.0 de borda a borda, exatamente o critério nº1. O `.glb` já existe e já está
> validado. Ou seja, o concept não precisa *decidir* o layout; o layout está decidido.
> O que falta ao concept é **cor e material**, não geometria.

### 🎁 Efeito colateral útil: as falhas da rodada 2 são texturas ótimas

As gerações que colapsaram em "swatch de textura" são **exatamente** o que a Etapa 3
precisa para texturizar. Salvas como
`M1_textura_tijolo_vermelho_candidata_A.png` e `_B.png`. O modo de falha de um
entregável é o sucesso de outro — vale testar o tile delas antes de gerar do zero.

> ⚠️ Essas duas foram geradas **antes** da troca de cor para `#A5673C`. Servem como
> referência de *padrão de fiada e junta*, não de cor. Regerar antes de usar.

### 🎨 Troca de cor do tijolo — 2026-08-04

`#CC4E5C` → **`#A5673C`**, decidido pelo Gustavo (direção de arte).

O hex antigo era RGB(204, 78, 92): matiz 353°, com mais azul que verde — rosa-salmão, não
cor de terra. Como ele entrava literalmente no prompt (`baked mudbrick red #CC4E5C`), o
modelo vinha acertando um alvo errado. Toda geração "laranja demais" era a paleta, não o
Fooocus.

O alvo foi cercado dos dois lados antes de escolher: a rodada 4 removeu o vermelho por
completo e caiu em bege lavado (`tentativa_12`), provando que o erro não era "menos
vermelho" e sim "matiz errado". `#A5673C` fica em 25°, mantém calor de barro, e preserva
a separação contra a poeira `#C9A876` — que a candidata mais amarela arriscava colapsar.

Trocado em 16 arquivos: paleta canon do `direcao_estrato_I.md`, material M4, `COL_BRICK` /
`COL_CLAY` dos scripts `bpy`, e o boilerplate dos prompts. Os `.glb` foram regerados.

### ✅ M4 regerada — `terracotta_clay_01.png` (2026-08-04)

Salva em `Assets/Art/Textures/terracotta_clay_01.png` · 1024×1024 · ~1.5 MB.
`direcao_estrato_I.md` §M4 e a ficha da urna já apontam para ela.

Tinha **dois** defeitos, não um:
1. **Cor** — a `terracotta_floor_tiles_diff_4k.jpg` é um vermelho-escuro quase oxblood,
   incompatível com `#A5673C`.
2. **Padrão** — é uma *grade de azulejo de piso*. A única peça que declara M4 é a **urna**,
   e grade de piso enrolada num vaso produz um vaso quadriculado. A ficha pede
   "terracota fosca", que é superfície de barro, não azulejo.

A nova é barro fosco liso, sem grade, com luz chapada.

**Teste de tile feito** (montagem 2×2 por script, não no olho): não há costura dura em
nenhum dos dois eixos. Mas existem alguns pontinhos escuros distintos que se repetem de
forma reconhecível — a ~4×4 numa parede isso viraria grade visual. Conclusão: **aprovada
para a urna** (peça única, escala pequena); **reavaliar antes de usar em chão**.

> ✅ **Decidido em 2026-08-04:** o **chão mantém a `terracotta_floor_tiles_diff_4k.jpg`**.
> Ela não sai do repo. O M4 passa a ser só cerâmica de objeto (a urna) — ver
> `direcao_estrato_I.md` §2.
>
> A `terracotta_clay_01.png` foi lida pelo Gustavo como **terra batida** e fica arquivada
> como candidata para **elementos de chão do exterior da torre**. É um uso melhor do que
> o que eu tinha imaginado para ela.

> Variante alternativa mais lisa salva como `M4_terracota_variante_lisa.png` nesta pasta,
> caso a escolhida tenha grão demais para a urna.

### ⛔ Estilos que NÃO usar
| Estilo | Por quê |
|---|---|
| `MRE Ancient Illustration` | injeta `"predating human civilization... crude and simple... made by genius primeval artist"` — é **estética de arte rupestre**, não da antiguidade mesopotâmica. Cobriu as 4 primeiras gerações de rabisco preto. O nome engana; conferir sempre o conteúdo em `Fooocus/sdxl_styles/*.json` antes de marcar um estilo. |

### ⛔ Não colocar no prompt negativo
`allover pattern`, `tangled lines`, `crude` — uma parede de tijolos **é** um *allover
pattern* (fiadas horizontais repetidas). Negar isso nega o vocabulário visual da própria
peça. Testado: gerou campo chapado.

---

## A. Folha de estilo — ⚠️ SUPERSEDIDA pela revisão acima (mantida como registro)

> **Por que existe:** os 6 concepts individuais provam cada peça. Eles **não** provam que
> as 6 juntas parecem o mesmo jogo. Coerência de kit é uma propriedade do conjunto, e é
> exatamente o que o checkpoint V2 de 07/08 tem que julgar. Se a folha de estilo estiver
> certa e um concept individual estiver errado, ajusta-se um. Se a folha estiver errada,
> o problema é a direção — e vale muito mais descobrir isso agora do que depois de 6
> modelos prontos.

### Prompt positivo

```
game asset sheet, a set of six matching ancient Mesopotamian environment props arranged in
a single row on a plain neutral grey background, stylized low-poly game art in the style of
Blasphemous and Hades, NOT photorealistic, all six sharing one consistent material language:
a tall mudbrick wall panel with a cobalt-blue glazed frieze, the same wall panel with a
rectangular doorway opening, a tall brick column with a blue band near its capital, a small
wall-mounted torch sconce with a lit flame, a waist-height standing fire brazier on a square
stepped base, and a terracotta storage urn, consistent scale relationship between all six
(the wall is by far the tallest, the urn the smallest), every piece dusty and in decline —
glaze gone matte under a film of dust, gold tarnished not polished, color palette strictly:
sun-dried mudbrick clay brown #A5673C, dry clay shadow #7A5C3E, sand dust #C9A876, Ishtar glazed blue
#0047AB, night blue #191970, tarnished saffron gold #F4C430, ivory #FFFFF0, harsh warm
torchlight raking from the left with deep shadows, orthographic front view, flat-on, clean
readable silhouettes, evenly spaced, plain neutral grey background
```

### Prompt negativo

```
photorealistic, photo, 3d render, blurry, cluttered background, perspective distortion,
angled view, text overlay, labels, watermark, signature, modern objects, green plants,
turquoise, marble, bronze, polished shiny gold, bright daylight, sunlight, ruins,
collapsed, rubble, inconsistent art styles, different lighting per object
```

### Config — valores reais da interface (conferidos no Fooocus 2.5.5, 2026-07-31)

| Campo | Valor | Onde |
|---|---|---|
| Aspect Ratio | **`1344×768 ∣ 7:4`** | Advanced → Settings |
| Image Number | **4** | Advanced → Settings |
| Performance | `Speed` (padrão) | Advanced → Settings |
| Output Format | `png` (padrão) | Advanced → Settings |
| Negative Prompt | o bloco abaixo | Advanced → Settings |
| Estilos | manter `Fooocus V2` + `Fooocus Enhance` + `Fooocus Sharp` e **marcar `MRE Ancient Illustration`** | Advanced → Styles |

> ⚠️ **16:9 exato não existe no Fooocus.** O SDXL só gera nas resoluções "bucket" em que
> foi treinado, e 16:9 não é uma delas. `1344×768` = **7:4** (1.75 vs 1.778) é a mais
> próxima. A alternativa mais larga seria `1408×704` (2:1) — vale testar se as 6 peças
> ficarem apertadas na fileira, ao custo de menos altura para a parede de 6 m.
>
> ℹ️ O checkbox **`Photograph`** que aparece marcado é do painel **Describe** (tipo de
> conteúdo para descrever uma imagem existente). **Não é um estilo** e não afeta a
> geração — não precisa desmarcar.
>
> 🟡 **Se a paleta sair diluída na 1ª rodada**, o primeiro suspeito é o `Fooocus V2`: ele
> reescreve o prompt acrescentando descritores, o que briga com uma paleta fechada em
> 7 hex. Desmarcar ele é o ajuste da rodada 2 — não mexa antes de ver o resultado.

- Salvar em `ArtSource/_lote_01_estrato_I/03_folha_de_estilo.png`

### As 3 perguntas do Gate 1
Olhando a folha, responda:
1. **Parece um kit só?** (mesma luz, mesma sujeira, mesmo nível de detalhe em todas)
2. **A escala relativa está certa?** (parede >> coluna > braseiro > urna > tocha)
3. **Você jogaria numa sala feita com isso?** — se a resposta for "quase", diga *o que*
   falta. "Quase" sem diagnóstico é o que vira retrabalho lá na frente.

---

## ✅ M2 PRONTA — `brick_glazed_blue_01.png` (2026-08-04)

Salva em `Assets/Art/Textures/brick_glazed_blue_01.png` · 1024×1024.
**Era a última textura que faltava no lote.**

### O caminho, porque a lição vale mais que o arquivo

O Fooocus **não** entregou isso pronto. Duas rodadas, ambas reprovadas:

| Rodada | Estrutura | Cor medida | Veredito |
|---|---|---|---|
| 1 | tijolo gordinho, chanfro arredondado, quase brinquedo | `#7D7DB4` · matiz 253 | 🔴 estilo destoa do kit; periwinkle lavado |
| 2 | tijolo realista, relevo raso, fosco — **boa** | `#5F98A9` · matiz 193 | 🔴 caiu em turquesa, **proibida por canon** |

A rodada 2 acertou a estrutura e errou a cor de um jeito pior que a rodada 1: matiz 193
fica entre o cobalto (215) e a turquesa (175), puxando para a turquesa — que o
`direcao_estrato_I.md` §1 proíbe por ser assinatura do Jardim Suspenso.

### 🔑 A solução não foi prompt, foi número

Textura de **matiz único** não precisa ser acertada por sorte. Peguei a estrutura da
rodada 2 e **remapei o matiz por script**: converte para HSV, força `H = 215°` onde a
saturação passa de 0.06 (o que preserva a argamassa cinza), reforça um pouco a saturação,
mantém o valor — ou seja, todo o relevo e a variação de luz ficam intactos.

Resultado medido: **matiz 218**, contra alvo 215. Script em
`scratchpad/recolor.py` (descartável, mas a técnica não).

➡️ **Regra que fica:** quando o defeito é *cor de matiz único* e a estrutura está boa,
corrigir por script é exato e instantâneo. Rodar o prompt de novo é apostar. Isso vale
para o resto do lote.

**Teste de tile feito** (2×2 por script): sem emenda dura em nenhum dos dois eixos.

> Original antes do recolor guardado como `M2_origem_antes_do_recolor_turquesa.png`,
> para o caso de a correção precisar ser refeita com outros parâmetros.

> 🟡 Ainda não conferido **dentro do Unity**: aplicar num Plane com Tiling `4,4` e olhar.
> O teste 2×2 pega emenda, mas não pega repetição de detalhe reconhecível a 4×4.

---

## B. Textura M2 — plano original (mantido como registro)

A única textura que falta no lote — as outras 4 já estão em `Assets/Art/Textures/`,
resgatadas do repo Godot na migração (ver `00_lote.md` § Texturas).

### Prompt positivo

```
seamless tileable texture of ancient Neo-Babylonian glazed brick wall, flat orthographic
top-down view, evenly lit with no directional shadows, regular horizontal courses of
rectangular glazed bricks with thin mortar joints, deep cobalt blue glaze #0047AB with
darker night-blue variation #191970 between bricks, subtle irregular crazing in the glaze,
a film of pale dust #C9A876 settled in the joints and dulling the surface, matte not glossy,
hand-painted stylized game texture, NOT photorealistic, high detail, seamless repeating
pattern, no perspective, no vignette
```

### Prompt negativo

```
photorealistic, photo, 3d render, perspective, angled view, directional shadow, vignette,
lighting gradient, glossy wet reflection, specular highlight, text, watermark, signature,
border, frame, single brick, isolated object, ruins, cracks through the wall
```

### Config e destino
- Aspect ratio **1:1**, resolução mais alta disponível · mínimo 4 variações
- Salvar em `Assets/Art/Textures/brick_glazed_blue_01.png`
- ⚠️ **Testar o tile antes de aprovar:** criar um material URP/Lit com ela, aplicar num
  Plane no Unity e subir o **Tiling** do Base Map (ex: `4, 4`) para ver as emendas. Se
  aparecer costura ou um detalhe que se repete de forma óbvia, gerar de novo — a parede vai
  repetir esta textura ~20 vezes por sala e qualquer padrão reconhecível vira grade visual.
- No Inspector de import: **Wrap Mode = Repeat** (é o padrão, mas confirme — em Clamp a
  emenda fica óbvia) e **Max Size = 2048**.
- Sem gradiente de luz assado na textura: a luz vem das tochas, em tempo real. Textura com
  sombra pintada briga com a iluminação da cena.
