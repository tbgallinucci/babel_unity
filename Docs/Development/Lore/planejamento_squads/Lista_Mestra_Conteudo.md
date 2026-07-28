# 📜 Babel — Lista Mestra de Conteúdo · v0.3 (alinhada à canon v2.0)

> **O que é este doc:** o **inventário de conteúdo** — *o que precisa existir, em que quantidade* — para planejar a arte e gerar os assets 3D. É a lista que o Gustavo pediu no Squad 7.
>
> **Fontes da verdade (NÃO duplicar aqui):**
> - **Lore/história** → **`Biblia_Babel_Completa.md` v2.0** (canon vigente).
> - **Dados/sistemas** → `docs/09` (inimigos), `docs/11` (itens/CSV), `docs/13` (skills/classes), `CLAUDE.md`.
>
> Este doc só **conta e especifica** o que a arte precisa produzir. Onde houver ❓/⚠️, é decisão/alinhamento pendente.
>
> **v0.3 (2026-07-27)** alinha à revisão de canon (23 decisões): **Karru e Ningirash
> foram cortados**, o clímax virou **2 bosses** (Alexandre → Tiamat), e os nomes dos
> deuses viraram os **reais** (Shamash, Ereshkigal, Nisaba, Tiamat). Mudanças no fim, §9.

---

## 1. 🏛️ Estrutura da Torre *(resumo — detalhe na bíblia §9)*
6 estratos × eras históricas — **"a Torre veste o que ela toma"** (ela absorve as
civilizações que a alimentam). Base = Babilônia viva em declínio (hub = Jardins
Suspensos). Clímax: **Alexandre → Tiamat** *(2 bosses; Karru foi removido da canon)*.

| # | Estrato | Era / tema visual | Boss |
|---|---|---|---|
| I | Fundação (base + primeiros andares) | Babilônia de Nabucodonosor — tijolo, ocre, lápis | *(tutorial: líder fanático)* |
| II | Terraços Inferiores | Assíria — bronze, maquinário de vínculo | Construto de Vínculo / Anzû |
| III | Muros dos Mortos | necrópole Arisen *(a "sala de espera" do Jardim)* | **O Juiz de Hamurabi** |
| IV | Alturas Ocas | Era Alexandrina — mármore, ouro | **Alexandre** *(penúltimo)* |
| V | A Confusão | não-euclidiano | **Os Descartados** *(provisório)* |
| VI | Portão do Céu | cume divino | **Tiamat** *(final)* |

---

## 2. ⚔️ Classes & Armas *(6 reais — do código)*
As 6 classes já existem (`ClassCatalog`). Só o **Paladino** tem kit completo; as outras precisam de skills/morphs (trabalho do Thiago/Claude, não seu).

| Família | Classe | Papel (título akkadiano) | Arma (mapa do código) |
|---|---|---|---|
| ☀️ Chosen | **Paladin** | Qarrādu (guerreiro) | ✅ **Espada 2 mãos** *(rig atual)* |
| ☀️ Chosen | **Ranger** | Ṣayyādu (caçador) | Bow |
| ☀️ Chosen | **Mage** | Āšipu (exorcista) | Staff |
| 🌙 Arisen | **Berserker** | Qarrādu | Axe |
| 🌙 Arisen | **Thief** | Ṣayyādu | Dagger |
| 🌙 Arisen | **Occultist** | Āšipu | Grimoire |

✅ **RESOLVIDO (2026-07-24) — arma do Paladino = ESPADA DE 2 MÃOS.** É o rig que já roda (`Player_New_Great_Sword.glb`), então zero arte nova. ➡️ **Ação do Thiago:** ajustar o `WEAPON_CLASS` no código (hoje `MACE/SHIELD → paladin`) para um `weapon_type` de espada de 2 mãos, alinhando o dado ao rig.

**Lore das armas** (bíblia: armas têm história ligada à trama) → gancho por classe fica pra depois; MVP só precisa da arma do Paladino modelada.

---

## 3. 👥 Personagens

### Protagonista *(1)*
- **Fazuk Sins** — humano, cabelos cacheados loiro-acobreado, forte. **É "O Retornado"** (bíblia §1). Base constante; visual muda por equipamento. **Tint por família:** Chosen = quente/radiante; Arisen = frio/cinza.

### NPCs do Hub — Jardins Suspensos *(~3)*
| NPC | Função | Status |
|---|---|---|
| **Garden Warden** | loja de Seeds (compra unlocks / vende) | ✅ já no jogo (greybox) |
| **A Escriba** | traduz as tábuas (lore) | a criar |
| **O Ferreiro / ArmorDyer** | tingir/forjar equipamento | ✅ painel já existe |

> 🎨 **Direção de arte do Warden (D16):** ele é um **Retornado quase-dissolvido** —
> perdeu a pessoa e manteve a função. Deve parecer **quase** normal: um jardineiro
> gentil, sorridente, e **errado por um fio** (a mesma frase de boas-vindas sempre,
> olhar que não termina de focar, mãos de terra que nunca sujam a roupa). Não é
> zumbi — é **um homem reduzido a um ofício**. É o que Fazuk vira se falhar.

> Tom do hub: **"a hospitalidade é a jaula"** (bíblia §10) — e agora com o D15: o
> lugar mais bonito do jogo **é uma vala comum bem arrumada**. Cada planta é alguém.

### Bosses *(clímax revisado + mid-strata)*
**Alexandre** *(penúltimo, Estrato IV)* → **Tiamat** *(final, cume)*. **Karru foi
removido da canon** — não modelar.
✅ **Mini-guardião: SIM** — 1 por estrato, **reaproveitando o asset do boss-portão**
(recolor/reescala) para dar pico de dificuldade no meio com custo de arte baixo.

| Estrato | Boss | Custo de arte |
|---|---|---|
| I *(tutorial)* | líder fanático | humano, baixo |
| II | Construto de Vínculo / Anzû | médio |
| III | **⚖️ O Juiz de Hamurabi** | médio-alto *(ver abaixo)* |
| IV | **Alexandre** | alto — figura histórica, precisa impor |
| V | **Os Descartados** *(provisório)* | 🟢 **quase zero** — reaproveita o rig do jogador |
| VI | **Tiamat** | 🔴 o mais alto do jogo — carrega o cume sozinha |

#### ⚖️ O Juiz de Hamurabi *(Estrato III — novo, 2026-07-27)*
O funcionário que aplicava as **leis de Talião** em nome do rei que as escreveu. Preside
a corte dos mortos que foram prometidos ao Jardim e nunca chegaram — **a maior injustiça
da história** — e não pode julgar deuses, então julga quem alcança.

🎨 **Direção de arte:**
- **Arma: o chicote de mãos.** Cada tira é uma mão decepada aplicando a lei. É a arma
  **e** o troféu. Peça-assinatura do boss — vale o esforço de modelagem.
- **Silhueta:** togado/burocrático, não guerreiro. **Uma balança** em algum lugar
  (portada, presa ao corpo, ou como parte da arena) — ela nunca para de oscilar.
- **Arena:** sala de tribunal na necrópole — pedra, **vitral** (já previsto pra salas de
  boss), bancadas de mortos assistindo. Reaproveita o kit da necrópole.
- **Espelho do Garden Warden:** os dois são pessoas reduzidas a uma função pela espera.
  O Warden guardou a ternura; o Juiz guardou o julgamento. Vale rimar visualmente.

⚙️ **Gancho de design (Thiago):** talião pede **mecânica de reflexão** — o dano que
você causa volta pra você. Força troca de ritmo em vez de "bater mais forte". Vem de
graça do conceito.

> 💰 **Economia de arte:** cortar o Karru **eliminou um boss inteiro** do orçamento
> (modelo, rig, arena, VFX, múltiplas fases), e **Os Descartados** custam quase nada
> (rig do jogador reaproveitado). Isso abre folga pra gastar no **Juiz** e na **Tiamat**,
> que são os dois que precisam impressionar.

---

## 4. 👹 Inimigos

### Já no jogo (greybox — só precisam de arte/rig) — Estrato I
| Inimigo | Tier / papel | Telegraph |
|---|---|---|
| **Husk** | básico (morto-vivo) | círculo |
| **Bandit** | humano padrão | linha/rect |
| **Brute** | elite pesado | cone |
| **Spitter** | ranged | círculo |
| **Chief** | mini-boss (bandidos) | cone |
| **Spawn** | creep (swarm) | — |

### A criar por estrato *(mitologia + taxonomia do repo)*
- **II Assíria:** Husk ranged, **Construto de Vínculo** (mini-boss).
- **III Necrópole:** mortos Arisen da corte, **Edimmu** (fantasma).
- **IV Alexandrino:** falange espectral, autômatos de bronze → **Alexandre**.
- **V Confusão:** **Os Inomináveis** (sem cuneiforme, mal obedecem à geometria).
- **VI Cúpula:** servos de **Tiamat**, os Sebitti.

📐 **Orçamento: ✅ ~4–5 tipos por estrato** (creep / padrão / ranged / elite). Estrato I **já está coberto** (6 mobs em greybox). **+ 1 mini-guardião por estrato** (asset reaproveitado do boss-portão).

---

## 5. 🎒 Itens

### Consumíveis — **já no jogo (4)** + propostas
| Item | Efeito | Status |
|---|---|---|
| **Kurunnu Brew** *(a "cerveja")* | cura | ✅ existe — **é aqui que entra o SFX de arroto** (Squad Áudio) |
| **Desert Ration** | recarrega esquiva | ✅ existe |
| **Herb of War** | buff temporário de dano | ✅ existe |
| **Polished Stone** | material (venda) | ✅ existe |
| 💡 Tâmaras, pão de cevada, óleo de nafta (arremessável)… | — | propostas (expansão; não urgente) |

> Sistema é **CSV-driven** (`items.csv`) — adicionar item = 1 linha + ícone/modelo por convenção de `id` (ver `docs/11`). Balancear = editar CSV.

### Equipamento — **3 sets já definidos (32 peças no CSV)**
- Sets: **`guardian`** · **`seer`** · **`ghoul`** *(já com flavor sombrio: "Coroa do General Caído", "Couraça da Legião Profana"…)*.
- Slots: HEAD/TORSO/LEGS/BOOTS/CAPE/GLOVES/RING/NECKLACE/WEAPON.
- **Arte por convenção:** ícone em `assets/ui/icons/items/<id>.png`, modelo em `assets/models/items/<id>.tscn`. **Nada tem modelo ainda** (greybox) — é infra pronta esperando arte.
- MVP: **1 set com arte real = `guardian`** ✅ (casa com o Paladino — set defensivo de placas/bê).

### Materiais
Polished Stone existe; expandir (fragmento de tábua, betume, lápis, bronze, couro, pelagem) conforme necessidade de venda/craft.

---

## 6. 📿 Colecionáveis
- **Tábuas Cuneiformes ≡ fragmentos de Nisaba** (bíblia §13): entregam lore (traduzidas pela Escriba) **e** clareiam a Confusão. **~3–5 por estrato** (~12–18 no total).
  > ⭐ **Promovidas a MOTOR da subida (D21):** cada estrato entrega um pedaço da
  > verdade. O jogador sobe **pra saber**, não pra ficar forte. Isso as torna
  > **conteúdo obrigatório**, não colecionável opcional — priorizar na produção.
- 🪙 **A moeda de Bucéfalo** (D6): item único do Fazuk, presente desde o Ato 1,
  reconhecida no Estrato IV. **1 asset pequeno, altíssimo retorno narrativo.**
- 💡 Secundários: ídolos/relíquias dos deuses, selos cilíndricos (troféus). Não urgente.

---

## 7. ✂️ Corte para o VERTICAL SLICE (Estrato I, Babilônia)
O mínimo pra "1 andar bonito + Paladino jogável":
- **Fazuk** (base) + **1 arma do Paladino** *(resolver a flag da §2 primeiro)* + **1 set `guardian`**.
- **2–3 inimigos do Estrato I** — Husk + Bandit (+ Spawn) — **já em greybox**, só arte/rig.
- **1 kit de ambiente Babilônia:** shells das salas (`world/rooms/prefabs/`) + props (tijolo, portas, colunas, tochas, braseiros, tamareiras, cevada, urnas) + texturas.
- **Hub amostra:** Garden Warden (existe) + 1–2 tábuas cuneiformes de prova.
- **Áudio:** SFX básico.
- **Zero bosses** no slice (mantém leve).

---

## 8. 📊 Orçamento de conteúdo (quantidades)
| Categoria | Vertical Slice | 10-andar MVP (Estrato I) | Jogo completo (6 estratos) |
|---|---|---|---|
| Classes com kit+arte | 1 (Paladino) | 1 | 6 |
| Tipos de inimigo | 2–3 *(já greybox)* | ~6 (Estrato I coberto) | ~24–30 |
| Bosses | 0 | 1 (líder fanático) | ~6 + mini-guardiões |
| Kits de ambiente | 1 (Babilônia parcial) | 1 | 6 |
| Sets de equipamento (arte) | 1 (`guardian`) | 1 | 3 (guardian/seer/ghoul) |
| Consumíveis | 3 *(já existem)* | ~6 | ~8 |
| Colecionáveis (tábuas) | 1–2 | ~3–5 | ~12–18 |
| NPCs de hub | 1–2 | ~3 | ~3 |

---

## 9. 🔧 Mudanças + pendências

### v0.3 (2026-07-27) — alinhamento à revisão de canon
| Mudança | Impacto na arte |
|---|---|
| **Karru removido** | 🟢 **–1 boss inteiro** do orçamento (modelo, rig, arena, VFX, fases). Clímax de 3 → **2 degraus** |
| **Ningirash cortado** | 🟢 –1 deus (iconografia, ídolos, culto na base) |
| **Nomes reais** (Shamash / Ereshkigal / Nisaba / Tiamat) | 🟡 renomear referências em docs e futura UI. Zero impacto em asset |
| **Tiamat = boss final único** | 🔴 sobe de prioridade: agora carrega o cume sozinha, precisa ser o asset mais forte do jogo |
| **D9 — Babilônia em declínio** | 🔴 direção nova do Estrato I: cidade grandiosa **visivelmente vazia demais** (praças largas com pouca gente, casas fechadas). Muda o dressing, não a geometria |
| **D15 — o Jardim é feito de almas** | 🟡 o hub ganha subtexto: cada planta é alguém. Vale um detalhe visual sutil (a luz vindo *de dentro* das plantas?) |
| **D16 — Warden quase-dissolvido** | 🟡 direção de personagem nova (ver §3) |
| **D21 — tábuas viram obrigatórias** | 🔴 sobem de "colecionável" para **conteúdo de caminho crítico** |
| **D6 — moeda de Bucéfalo** | 🟢 1 asset minúsculo, retorno narrativo enorme |

### ✅ Resolvido
1. ✅ Arma do Paladino = **espada 2 mãos**.
2. ✅ **~4–5 inimigos por estrato** + **mini-guardião SIM** (asset reaproveitado).
3. ✅ Set do MVP = **`guardian`**.
4. ✅ `docs/06_Roteiro_Ato1` **realinhado** (Fazuk, chamado do Shamash, moeda, Babilônia em declínio).
5. ✅ **Festival do Ato 1 = o Festival do Jardim** — honra os mortos que "foram
   descansar entre as flores". Alegre, agradecido, sincero. 🎨 *Impacto na arte:*
   dressing de festa com **flores e frutas como oferenda**, altares pequenos de rua,
   guirlandas. Nada sinistro na superfície.
6. ✅ **Fazuk acorda NO BAIRRO**, não nos Jardins. **Os vivos não entram no Jardim** —
   ele descobre o lugar explorando (o portão o deixa passar) **ou** morrendo pela
   primeira vez. 🎨 *Impacto na arte:* o Jardim precisa de um **portão/limiar
   legível** que pareça proibido e não esteja trancado.

### ❓ Aberto
7. ➡️ **Ação do Thiago (código):** ajustar `WEAPON_CLASS` — Paladino → espada de 2 mãos (alinhar dado ao rig).
8. 🟡 **Boss do Estrato V = Os Descartados**, aceito como **provisório** (27/07).
   Slot não lacrado — se algo melhor aparecer, trocar. *(A proposta anterior, "o
   próprio Vínculo", foi rejeitada.)*
9. ⚠️ **Escopo da Confusão (nota pro Thiago):** degradar a **própria UI** (mapa e
   interface mentindo) é caro e arriscado. **MVP faz só o diegético** — inscrições e
   falas de NPC virando gibberish. UI mentindo fica pra depois.
10. ❓ **"Anunnaki"** como nome coletivo do panteão (substituiu o inventado "Anzuri").
