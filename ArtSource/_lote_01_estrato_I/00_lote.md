# LOTE 01 — Sala-vitrine do Estrato I

> **Objetivo:** vestir UMA sala 20×20 m do Estrato I com arte real, para o checkpoint
> **V2 (estilo visual) de 07/08/2026**. O entregável não é "meio kit pronto" — é
> **o estilo aprovado**, para só então produzir o resto do andar sem risco de retrabalho.
>
> **Criado:** 2026-07-28 · **Direção:** `.design/direcao_estrato_I.md` (ler antes de tudo)
> · **Escopo definido em:** `Lista_Mestra_Conteudo.md` §7 (corte do vertical slice)

---

## Por que estes 6 e não outros

A sala mínima que prova o estilo precisa de: **um plano** (parede), **uma abertura**
(porta), **um ritmo vertical** (coluna), **duas fontes de luz** (tocha + braseiro) e
**um objeto solto de escala humana** (urna). Com esses 6 dá pra montar uma sala que
parece um lugar. Faltando as fontes de luz, a sala é uma caixa cinza — por isso tocha e
braseiro entram no lote 01 e não em "decoração depois" (ver `direcao_estrato_I.md` §4:
a luz é diegética e é asset).

Ficaram **fora** deste lote de propósito, para depois do estilo aprovado: tamareira,
cevada/trigo, escadaria, portão, altar de rua, tábua cuneiforme.

---

## Os 6 assets

| # | Slug | Categoria | Dimensões (m) | Rota | Custo IA |
|---|---|---|---|---|---|
| 1 | `wall_brick_stratum1_01` | parede | 4.0 × 6.0 × 0.6 | A · procedural | 🟢 zero |
| 2 | `wall_doorway_stratum1_01` | parede | 4.0 × 6.0 × 0.6 (vão 1.6 × 2.8) | A · procedural | 🟢 zero |
| 3 | `column_lapis_stratum1_01` | estrutura | Ø0.9 × 6.0 | A · procedural | 🟢 zero |
| 4 | `torch_wall_stratum1_01` | prop + luz | 0.35 × 0.9 × 0.45 | A · procedural | 🟢 zero |
| 5 | `brazier_bronze_stratum1_01` | prop + luz | Ø0.8 × 1.1 | A · procedural | 🟢 zero |
| 6 | `urn_clay_stratum1_01` | prop | Ø0.55 × 0.9 | A · procedural | 🟢 zero |

> ✅ **Os 6 são Rota A (script `bpy` procedural).** Nenhum consome crédito de Meshy/Tripo
> — pela regra de roteamento do `blender-modeler`, hard-surface geométrico não justifica
> geração de IA 3D. O crédito de IA fica reservado para o que é genuinamente orgânico
> (inimigos, NPCs, bosses). Isso também significa que **eu escrevo os 6 scripts** — a sua
> parte no Blender vira "colar e rodar", não modelar.

### ✅ Scripts prontos e VERIFICADOS (2026-07-30)

Os 6 `04_gerar_modelo.py` existem e foram **rodados de verdade no Blender 5.1 headless**,
não só escritos. Resultado medido:

| Asset | Tris | X | Y | Z | Pivô |
|---|---|---|---|---|---|
| `wall_brick_stratum1_01` | 276 | 4.000 | 0.730 | 6.000 | base ✅ |
| `wall_doorway_stratum1_01` | 480 | 4.000 | 0.760 | 6.000 | base ✅ |
| `column_lapis_stratum1_01` | 284 | 1.100 | 1.100 | 6.000 | base ✅ |
| `torch_wall_stratum1_01` | 170 | 0.300 | 0.420 | 0.905 | face de montagem ✅ |
| `brazier_bronze_stratum1_01` | 246 | 0.840 | 0.840 | 1.315¹ | base ✅ |
| `urn_clay_stratum1_01` | 304 | 0.542 | 0.570 | 0.900 | base ✅ |

¹ corpo sólido até 1.03 m; o resto é a chama. O collider vai só até 1.05 — ver a ficha.

🔁 **Re-verificados em 2026-07-31**, depois da correção de caminhos pós-migração: os 6
rodam e entregam **exatamente os mesmos números** da tabela acima.

> 🟡 **Aviso de futuro (não bloqueia nada hoje):** os 6 scripts emitem
> `DeprecationWarning: 'Material.use_nodes' is expected to be removed in Blender 6.0`.
> No Blender 5.1 funciona normalmente. Quando o Blender 6.0 sair, **os 6 quebram de uma
> vez** — a correção é trocar `mat.use_nodes = True` pela API nova, num único helper
> compartilhado. Registrado aqui para não virar surpresa numa atualização automática.
> Recomendação prática: não atualizar o Blender no meio do lote.

### 🔴 O azul é alvenaria — correção de direção (Gustavo, 2026-08-04)

Regra completa em `direcao_estrato_I.md`. Resumo: **o azul de Ishtar não é pintura, é o
próprio tijolo.** Nas seções azuis o tijolo deixa de ser de barro e passa a ser esmaltado
— mesma fiada, mesma junta, mesmo módulo.

Os `03_concept.png` de `column_lapis` e `wall_doorway` foram refeitos por causa disso. Os
novos acertam o conceito (azul como peça de alvenaria, com junta) mas **não são bons
concepts**: na coluna o azul se espalha em vez de ficar no trecho definido, e na porta o
enquadramento é close. Ficam como registro da regra, não como referência de qualidade.

### ⛔ Por que eu parei de gerar coluna e porta

O que faltava já existe, e em melhor qualidade do que o Fooocus entregaria:

| O que | Onde | Estado |
|---|---|---|
| material de barro | `wall_brick_stratum1_01/03_concept.png` | ✅ aprovado |
| material azul esmaltado | `Assets/Art/Textures/brick_glazed_blue_01.png` | ✅ aprovado, tile testado |
| posição da faixa / do vão | `04_gerar_modelo.py` | ✅ medido no `.glb` |

Pedir ao SDXL que combine os dois materiais **na proporção certa** é exatamente o que ele
não sabe fazer — é o mesmo limite de *attribute binding* que custou 15 gerações na parede
e que reapareceu aqui na primeira tentativa. Insistir seria repetir um erro já catalogado.

➡️ **A validação real do azul-alvenaria é na Etapa 3**, montando no Unity com a M2
aplicada. Checagem obrigatória lá: a M2 tem que entrar com **o mesmo tiling do barro**,
senão a faixa vira cor chapada e volta a parecer tinta — que é justamente o defeito que
esta regra existe para corrigir.

### ✅ ETAPA 1 COMPLETA — os 6 `03_concept.png` existem (2026-08-04)

Os 5 restantes saíram em uma única sessão, aplicando a regra do Gate 1 (concept decide
cor e material, não layout). Custo: **12 gerações para 5 peças** — contra 15 gerações
para 1 peça antes da regra.

| Asset | Rodadas | Observação |
|---|---|---|
| `wall_doorway` | 1 | vão lido como nicho; layout vem do script, cor/material OK |
| `urn_clay` | 1 | ✅ acertou a ficha inteira de primeira |
| `brazier_bronze` | 1 | barro e chama OK; **bacia deveria ser M3 (ouro fosco)**, veio barro |
| `column_lapis` | 2 | 1ª: azul total + ouro polido. 2ª OK |
| `torch_wall` | 2 | 1ª: arandela vitoriana de latão. 2ª OK; **suporte veio ferro, não M3** |

### 🔑 A lição desta leva: o substantivo manda, o adjetivo não corrige

As 3 peças que saíram de primeira foram descritas com **vocabulário concreto de
material** — "barro cozido", "corpo arredondado", "faixa pintada desbotada". As 2 que
falharam usaram **palavras de categoria com bagagem cultural**:

- `sconce` → em inglês é *arandela*. Trouxe latão vitoriano com cúpula de vidro, apesar de
  `glass, candle, chain` estarem no negativo. **Negativo não vence substantivo errado.**
  Resolvido descrevendo o objeto: "tocha de madeira presa à parede por um suporte de ferro".
- `column` + `gold` → pedestal de museu com ouro polido, violando a regra de
  "ouro embaçado, nunca brilhante como cor-base". Resolvido pondo o barro dominante na
  frente da frase e negando `blue column, all blue, polished gold, gilded`.

### ✅ M3 RESOLVIDO em 2026-08-04 — `tarnished_gold_01.png`

A previsão abaixo estava certa e a receita funcionou. Registro do que deu certo, porque é
generalizável:

**Não pedir a cor.** O prompt não menciona ouro em nenhum momento — pede
`hammered metal plate, dented and pitted, dull tarnished patina`. Assim o modelo entrega
**estrutura** (martelado, cova, pátina) sem trazer junto o reflexo de vitrine que a palavra
`gold` carrega. O açafrão entra depois, por script, em matiz 45°.

**Usar o colapso a favor.** `orthographic front elevation` está registrado na folha de
estilo como armadilha porque achata objeto em swatch — mas quando o entregável **é** um
swatch, esse colapso é exatamente o que se quer. A mesma linha que estraga um concept
salva uma textura.

Três passadas de script, todas mensuráveis:

| passo | operação | resultado |
|---|---|---|
| 1 | recorte fora das fiadas de rebite | 1024 × 1024 de metal puro |
| 2 | matiz 45° · sat ×1.15 · valor ×1.02 | `#AB9C70` — ouro embaçado |
| 3 | compressão de valor K=0.5 | mata a mancha de larga escala |

> 🔑 O passo 3 é o menos óbvio e o mais reaproveitável. Uma textura com manchas grandes de
> claro/escuro fica ótima como swatch e **péssima numa peça pequena** — cada faceta pega
> uma mancha diferente e o objeto vira xadrez. Comprimir o valor em torno da média mata a
> mancha e preserva o grão, porque grão é alta frequência e sobrevive à compressão.

⚠️ **Armadilha separada, de render e não de textura:** projeção `BOX` em cilindro de 12
lados faz facetas vizinhas amostrarem regiões distantes da imagem, e nenhum
`projection_blend` conserta — ele só suaviza a fronteira entre os 3 eixos. O certo é o
**UV do próprio cilindro**. Perdi duas rodadas culpando a textura por um defeito que era
de projeção.

---

<details>
<summary>Histórico — como a pendência estava registrada antes</summary>

#### 🟡 Pendência conhecida: o M3 (ouro açafrão embaçado) não sai por prompt

Duas peças precisam de M3 e nenhuma acertou:
- `brazier_bronze` — a bacia veio de barro (meus negativos `polished, shiny, gleaming`
  mataram a leitura de metal junto com o brilho)
- `torch_wall` — o suporte veio de ferro escuro (evitei a palavra "gold" para escapar do
  latão e caí no extremo oposto)

Padrão: dizer `gold` puxa para ouro polido de vitrine; evitar a palavra puxa para ferro.
**Não bloqueia a Etapa 2** — os `.glb` já têm o material de preview correto e o material
final é um `.mat` URP montado no Unity.

➡️ Quando for resolver: usar a mesma técnica que resolveu a M2 — gerar com a estrutura
certa e **corrigir o matiz por script**. Ouro fosco é alvo de matiz e valor, exatamente o
tipo de defeito que se resolve por número em vez de por sorte.

</details>

### ✅ GATE 1 DA PAREDE — FECHADO em 2026-08-04 (decisão do Gustavo)

`03_concept.png` do `wall_brick_stratum1_01` está definido. Ele aprova **cor e material**,
não layout.

**Por que o critério mudou.** O concept foi criado para validar silhueta, proporção de
detalhe e cor. Depois de 15 gerações ficou claro que o SDXL não segura quatro requisitos
ao mesmo tempo (cor específica + faixa posicionada + silhueta de objeto + estilo não
fotográfico) — cada rodada conserta um eixo e quebra outro. Mas o layout **não precisava**
vir dele: o friso já está em `FRIEZE_Z = 1.60`, de borda a borda, dentro do
`04_gerar_modelo.py`, e o `.glb` exportado põe o material lápis-lazúli no lugar certo.

➡️ **Regra que fica para os outros 5 concepts:** onde o script paramétrico já define
posição e medida, o concept **não** decide layout — decide cor, material e nível de
sujeira. Isso encurta muito as próximas rodadas.

> ⚠️ O que o `03_concept.png` **não** mostra: o friso azul e as rosetas de ouro. Para
> referência desses dois, usar `tentativa_6_rosetas_de_ouro_boas_azul_em_cruz.png` (o
> material do ouro está certo ali) na mesma pasta.

### ✅ ETAPA 2 CONCLUÍDA — `.glb` exportados e validados (2026-08-04)

Os 6 foram rodados headless no Blender 5.1 e **exportados**. Não é mais "colar e rodar" —
está feito. Config de export usada, travada pelo checklist lá embaixo:
`.glb` · `export_apply=True` (aplica o Bevel) · `export_yup=True` · normais · UVs.

Medições feitas **reimportando os `.glb`**, não a cena de origem — ou seja, é o que o
Unity vai receber de fato:

| Slug | Tris | X (larg) | Y (alt) | Z (prof) | Pivô Y=0 | Mats | Destino |
|---|---|---|---|---|---|---|---|
| `wall_brick_stratum1_01` | 320² | 4.000 | 6.000 | 0.730 | ✅ | 5² | `Assets/Art/Environment/` |
| `wall_doorway_stratum1_01` | 480 | 4.000 | 6.000 | 0.760 | ✅ | 4 | `Assets/Art/Environment/` |
| `column_lapis_stratum1_01` | 284 | 1.100 | 6.000 | 1.100 | ✅ | 4 | `Assets/Art/Environment/` |
| `torch_wall_stratum1_01` | 170 | 0.300 | 0.905 | 0.420 | −0.125¹ | 3 | `Assets/Art/Props/` |
| `brazier_bronze_stratum1_01` | 246 | 0.840 | 1.315 | 0.840 | ✅ | 3 | `Assets/Art/Props/` |
| `urn_clay_stratum1_01` | 304 | 0.542 | 0.900 | 0.570 | ✅ | 3 | `Assets/Art/Props/` |

¹ **Não é erro** — é a exceção declarada na ficha da tocha: o pivô dela é a *face de
montagem*, não a base. A geometria descer 0.125 m abaixo do pivô é exatamente o esperado.

² **Reexportada em 2026-08-04** com o painel de inscrição em pedra clara (M6) emoldurado
por M2 — ver a seção do ornamento cuneiforme abaixo. Era 276 tris / 4 materiais. As
dimensões e o pivô **não mudaram**, então a emenda na grade de 4 m segue valendo.

**A conversão de eixo saiu certa:** a altura caiu no **Y** nos 6 (parede e coluna com
6.000 em Y, não em Z). É o sinal de que `export_yup` funcionou e as peças não vão entrar
deitadas no Unity.

Números idênticos aos da tabela do Blender acima — o export não distorceu nada.

> 🟡 **Pendente de Etapa 3, não de Etapa 2:** os `.glb` carregam os materiais de
> **preview** (4 ou 3 por peça, cores chapadas da paleta). Eles servem para conferir
> silhueta e leitura, **não** são o material final — o final é um `.mat` URP/Lit montado
> no Unity, conforme `Docs/Development/Art/README.md`.
>
> ⚠️ Os `.meta` do Unity ainda não existem para estes 6 arquivos: só nascem quando o
> Editor abrir o projeto e importar. Isso é normal.

**As 3 peças grandes começaram estourando o orçamento** (parede 532, porta 1120, coluna
476). A causa era `BEVEL_SEG = 2`. Baixando para **1** todas entraram no alvo, com perda
visual mínima: um segmento já mata a aresta viva de 90°, que era o objetivo declarado na
direção de arte — o segundo só arredonda mais. Numa peça instanciada ~20× por sala isso
não paga. Já aplicado nos 3 scripts.

> ⚠️ Isso é **pré-trabalho da Etapa 2**, não pula o Gate 1. Os scripts derivam das
> dimensões da ficha, não do concept — o concept valida silhueta, proporção de detalhe e
> cor, e pode muito bem mandar ajustar qualquer um deles. Se isso acontecer, mexer no
> script é minutos, porque ele é paramétrico (as medidas estão todas em constantes no
> topo do arquivo).

### ✅ ORNAMENTO CUNEIFORME — resolvido em 2026-08-04

O Gustavo fixou que *"o azul deve ser um adereço"* — faixa, quadrado ou símbolo, não
tijolo espalhado — e escolheu **cuneiforme** como o símbolo. Depois perguntou se a escrita
deveria ir no azul ou numa pedra clara. **Vai na pedra clara.** A regra completa, com os
três motivos, está em `direcao_estrato_I.md` §2.

Resultado: `04_gerar_modelo.py` da parede ganhou um painel M6 de 3.20 × 0.80 m com moldura
M2 de 0.10 m, e a textura `inscription_stone_01.png` (1024 × 256) foi recortada do concept
`cuneiforme_pedra_clara_B.png` e clareada por script até `#C1B7A6`.

#### 🔑 A lição desta rodada: negativo longo mata o assunto

Foram **onze rodadas** no Fooocus, e a maioria se perdeu por um erro meu, repetido: cada
defeito que eu via, eu tentava corrigir empilhando termo no prompt negativo. O negativo
chegou a ficar maior que o positivo — e apagou justamente o que eu queria.

| tentativa | resultado |
|---|---|
| negar `cursive, calligraphy, handwriting` para forçar a cunha | 🔴 apagou **toda** a escrita e virou tijolo triangular literal |
| `front view` + `flat wall` + negar `perspective` | 🔴 colapso em swatch de textura, 4 imagens sem escrita |
| `cream white glazed brick` para o sinal | 🔴 virou **fiada** de tijolo branco — a cor grudou no substantivo errado |
| negativo enxuto de 9 palavras | ✅ as 3 imagens boas saíram todas depois disso |

Três regras que valem para todo concept futuro:

1. **Negar uma classe inteira mata a classe.** Proibir "cursivo" não produz cunha, produz
   ausência de escrita. Afirmar o substantivo certo é o caminho; proibir o errado não é.
2. **A cor gruda no substantivo mais próximo.** `pale limestone panel` funciona;
   `cream white glazed brick` põe a cor no tijolo, não no sinal.
3. **Composição aninhada não sai por prompt.** "Painel dentro de faixa" são dois objetos
   com materiais distintos — a fraqueza documentada do SDXL. Prender um solta o outro.
   Isso é trabalho do script, e reconfirma o enquadramento do Gate 1: **concept decide cor
   e material, script decide layout.** Eu saí dessa regra e as onze rodadas foram o preço.

> ⚠️ A escrita gerada é **decorativa e não diz nada**. Num jogo onde tábua cuneiforme é a
> mecânica-assinatura isso pode incomodar. Montar uma inscrição com sinais reais é
> pesquisa, não geração — decisão aberta com o Gustavo.

### Texturas
4 dos 5 materiais já estão em `Assets/Art/Textures/`, **resgatados do repo Godot** na
migração de 2026-07-28: `old_stone_wall_diff_4k.jpg`, `sandstone_cracks_diff_4k.jpg`,
`terracotta_floor_tiles_diff_4k.jpg`, `weathered_brown_planks_diff_4k.jpg`.

> Só os mapas **diffuse** foram trazidos (37 MB), não os normal/displacement (~280 MB no
> total). Não é corte de escopo: os materiais do Godot usavam **exclusivamente**
> `albedo_texture` — normal e displacement nunca chegaram a ser usados. É paridade exata.
>
> 🟡 **Sugestão (sua decisão):** são fontes **4K**, exagero para um kit estilizado low-poly.
> Baixar para 2K cortaria o peso do repo em ~75% sem diferença visível. Como não é
> bloqueante, deixei como está — se topar, dá pra fazer numa passada só. No mínimo,
> defina `Max Size = 2048` no Inspector de import do Unity (isso corta VRAM, mas não o
> tamanho no git).

~~**Só falta 1 textura:** o esmalte azul de Ishtar (M2).~~
✅ **RESOLVIDO 2026-08-04** — `Assets/Art/Textures/brick_glazed_blue_01.png`, 1024×1024,
tile testado. **As 5 texturas do lote estão completas.** Detalhe do processo e a técnica
de correção de matiz por script em `01_folha_de_estilo_e_textura.md`.

> A `terracotta_clay_01.png` (gerada junto) substituiu a de piso **só no M4 da urna**. O
> chão mantém `terracotta_floor_tiles_diff_4k.jpg` — decisão do Gustavo, ver
> `direcao_estrato_I.md` §2.

---

## Sequência de execução (lote, não 1-a-1)

O pipeline padrão tem 3 portões por asset (18 portões para 6 assets). Em lote são **3
portões no total**, um por etapa — é a mitigação nº1 do `Roadmap_Backlog.md`
("gerar em lote, não um a um").

```
ETAPA 1 — CONCEPTS (uma sessão de Fooocus só)
  ├─ 6 prompts individuais (01_prompt_concept.md de cada pasta)
  ├─ + 1 "folha de estilo" com os 6 juntos → é ela que prova a COERÊNCIA do kit
  └─ + 1 textura tileável de esmalte azul (M2)
        ▼ GATE 1 — você aprova o conjunto (não peça por peça)

ETAPA 2 — MODELAGEM (Blender, colar e rodar)
  └─ 6 scripts bpy prontos, um por asset
        ▼ GATE 2 — checklist técnico (escala/pivô/normais) nos 6

ETAPA 3 — INTEGRAÇÃO + SALA-VITRINE
  ├─ export .glb → Assets/Art/{Environment,Props}/
  ├─ materiais URP .mat → Assets/Art/Materials/
  ├─ Prefabs → Assets/Prefabs/{Environment,Props}/
  └─ montar a sala-vitrine + luz/ambiente (é aqui que o estilo aparece de verdade)
        ▼ GATE 3 — checkpoint V2 de 07/08
```

⚠️ **O portão continua existindo.** Lote não é "sem validação" — é validação do conjunto
em vez de validação peça a peça. Se a folha de estilo do Gate 1 estiver errada, a gente
descobre com 1 sessão de Fooocus perdida, não com 6 assets modelados.

---

## Orçamento de horas (sua capacidade: ~5h/semana → ~7h até 07/08)

| Etapa | Horas |
|---|---|
| Sessão Fooocus (6 concepts + folha de estilo + textura M2) | 1.0 – 1.5h |
| Revisão/ajuste dos concepts | 0.5h |
| Blender — rodar os 6 scripts + checklist | 2.0h |
| Export + integração no Unity (6 Prefabs + materiais URP) | 1.5h |
| Montar a sala-vitrine + tuning de luz/ambiente | 1.0 – 1.5h |
| **Total** | **6.0 – 7.0h** |

🔴 **Risco declarado:** isso ocupa a sua janela inteira até 07/08, sem folga. Se algo
travar (uma rodada extra de Fooocus, um script que não roda), o que corta primeiro é
**a urna** (item 6) — é o único dos 6 que a sala sobrevive sem. A ordem de corte
seguinte é braseiro → coluna. Parede, porta e tocha são o piso mínimo.

### 🔴 Revisão de prazo pós-migração (2026-07-30)
Duas coisas mudaram desde que este orçamento foi escrito e o checkpoint V2 de **07/08**
ficou apertado demais para ser honesto:

1. **A migração para Unity consumiu dias** que não estavam no plano.
2. ~~**Não há Unity Editor instalado na máquina** — só o Hub. Instalar o `6000.5.5f1` é um
   download de vários GB antes de qualquer coisa poder ser testada no jogo.~~
   ✅ **RESOLVIDO (2026-08-04):** o `6000.5.5f1` está instalado em
   `C:\Program Files\Unity\Hub\Editor\` e bate com o `ProjectVersion.txt`. Não é mais
   bloqueio.

O que **não** foi afetado: os 6 scripts estão prontos e testados, e as Etapas 1 e 2
(Fooocus + Blender) **não dependem do Unity**. Só a Etapa 3 depende.

➡️ **Recomendação:** manter o 07/08 como checkpoint de **estilo aprovado** (folha de
estilo + concepts + modelos em Blender) e mover a **sala-vitrine montada no Unity** para
o checkpoint seguinte. Isso preserva o objetivo real do V2 — decidir se o visual está
certo antes de produzir o resto do andar — sem fingir um prazo que não fecha.

---

### 📅 CRONOGRAMA REVISADO — 2026-08-04

A coluna **original** fica preservada de propósito, a pedido do Gustavo, para dar para
medir o quanto derrapou e onde.

| Marco | Data original | Data revisada | Status |
|---|---|---|---|
| Gate 1 — folha de estilo aprovada | 07/08 | **04/08** | ✅ fechado, **3 dias adiantado** |
| Etapa 1 — 6 concepts | 07/08 | **04/08** | ✅ feito |
| Etapa 2 — 6 `.glb` exportados e medidos | 07/08 | **04/08** | ✅ feito |
| Texturas M1–M6 | 07/08 | **04/08** | ✅ feito — M6 não existia no plano |
| Merge com os 6 commits do Thiago | *não previsto* | **05–07/08** | 🔴 **bloqueio**, e não é meu |
| Etapa 3 — materiais URP + Prefabs | 07/08 | **11–14/08** | ⬜ não começou |
| Etapa 3 — sala-vitrine + luz | 07/08 | **11–14/08** | ⬜ não começou |
| **Checkpoint V2** | **07/08** | **14/08** | 🟡 **+7 dias** |

**Horas — orçado × real:**

| Etapa | Orçado | Real | Nota |
|---|---|---|---|
| Fooocus (concepts + folha + M2) | 1.0 – 1.5h | ~4h | estourou; ver causa abaixo |
| Revisão/ajuste dos concepts | 0.5h | ~1.5h | 4 correções de direção do Gustavo |
| Blender — 6 scripts + checklist | 2.0h | ~1.5h | 🟢 abaixo do orçado (scripts paramétricos) |
| Unity — Prefabs + materiais URP | 1.5h | — | não começou |
| Sala-vitrine + tuning de luz | 1.0 – 1.5h | — | não começou |
| **Total** | **6.0 – 7.0h** | **~7h gastas, ~3h restantes** | |

**Por que o Fooocus estourou 3×.** Duas causas, e só uma delas é evitável:

1. **Evitável, e é minha:** eu corrigia defeito empilhando termo no prompt negativo até
   ele ficar maior que o positivo e apagar o assunto. Custou ~11 rodadas só no ornamento
   cuneiforme. A regra está registrada na seção do ornamento acima e não deve se repetir.
2. **Não evitável:** 4 correções de direção de arte do Gustavo (azul laranja demais, azul
   como pintura, azul espalhado, cruz anacrônica). Isso **é** o trabalho do Gate 1 — pegar
   erro de estilo antes de produzir 30 assets, não depois. O orçamento de 1.5h para essa
   etapa era otimista desde o começo.

> 🟢 **O que compensou:** o Gate 1 fechou com 12 gerações para 5 peças, contra as 15 por
> peça previstas, porque a gente reenquadrou — **concept decide cor e material, script
> decide layout**. E as Etapas 1 e 2 saíram inteiras em um dia. O atraso do V2 é de
> integração no Unity, não de arte.

---

## Checklist de validação (o do GDD Squad 3 Q8, operacionalizado)

Aplicar em cada asset no Gate 2/3:

- [ ] **Escala** — bate com a ficha e com a grade (4 m / 6 m / sala 20×20; jogador 1.9 m)
- [ ] **Formato** — `.glb` (importa nativo via `com.unity.cloud.gltfast`), exportado com Apply Modifiers + UVs + Normals, +Y Up
- [ ] **Pivô** — base central (X/Z centrados, Y=0 na base). Exceção: tocha (face de montagem)
- [ ] **Estilo e cores** — só a paleta operacional do `direcao_estrato_I.md` §1; nada de turquesa/verde/mármore/bronze polido
- [ ] **Performance** — dentro do orçamento de tris da ficha; colisão primitiva (Box/Convex), nunca trimesh em objeto pequeno
- [ ] **Articulação** — chanfro 0.05–0.15 m em toda aresta dura; peças estruturais no Layer `Level` (coletada pelo `NavMeshSurface`)
- [ ] **Leitura** — reconhecível em miniatura de 64px
