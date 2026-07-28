# 🎮 GDD + Orquestrador de Squads

> **Como usar este documento:** ele tem duas partes. O **Orquestrador** (topo) diz *em que ordem*, *com quais dependências* e *onde você precisa validar*. Os **Squads** (embaixo) são blocos de perguntas — você não responde tudo de uma vez. A cada sessão comigo, aponte para uma seção ("vamos trabalhar o Squad de Direção") e a gente preenche junto.
>
> **Regra de ouro dos squads:** onde a entrega final é **texto, código ou raciocínio** → eu entrego direto. Onde a entrega final é **imagem, áudio ou 3D** → eu viro o *diretor* que escreve o brief/prompt, e a ferramenta externa (seu pipeline de IA, Suno, etc.) executa.

---

## 🧭 ORQUESTRADOR

### Fases do projeto e quais squads ficam ativos

| Fase | Foco | Squads ativos | Objetivo de saída |
|---|---|---|---|
| **0 · Direção** | Definir *que jogo é esse* | Direção | Pilares + escopo travados |
| **1 · Pré-produção** | Provar que a ideia funciona | Direção, Narrativa, Arte, Programação | **Vertical Slice**: 1 arma completa + 1 mini-nível jogável |
| **2 · Produção** | Construir o volume | Todos, com peso em Arte + Programação | Conteúdo completo |
| **3 · Polish** | Ajustar e corrigir | QA, Áudio, Programação | Jogo estável e balanceado |
| **4 · Lançamento** | Empacotar e publicar | Produção | Build final |

> ⏱️ **Sobre prazo:** como você está solo, não uso datas fixas — uso *proporção*. Direção é curta (dias). Pré-produção é média mas crítica (não pule). Produção é a maior fatia. Polish sempre demora mais do que você imagina — reserve ~30% do tempo total só pra ele. Defina suas datas reais na tabela de checkpoints abaixo.

### Dependências (o que precisa estar pronto antes de quê)

```
Direção (pilares)
   └─> destrava TUDO. Nada sério começa antes disso.
        ├─> Narrativa (lore, personagens)
        │      └─> alimenta Arte (design de personagem/mundo)
        ├─> Arte (concept + estilo)
        │      └─> alimenta seu pipeline imagem→3D
        └─> Design de sistemas
               └─> alimenta Programação
                      └─> quando há eventos de jogo definidos
                             └─> destrava Áudio (sons de hit, morte, ambiente)

QA/Balanceamento → roda em paralelo o tempo todo, intensifica na Fase 3.
Produção/Gestão → é você usando este orquestrador. Sempre ativo.
```

### Checkpoints de validação (onde SÓ VOCÊ decide)

Eu posso rascunhar tudo, mas a visão criativa é sua. Nestes pontos você precisa parar e aprovar antes de seguir:

| # | Checkpoint | Antes de avançar para |
|---|---|---|
| ✅ V1 | Pilares do jogo aprovados (o "coração" do projeto) | Qualquer produção |
| ✅ V2 | Estilo visual travado (paleta, referências, materiais) | Gerar assets em volume |
| ✅ V3 | Vertical slice aprovado ("isso é divertido?") | Fase de Produção |
| ✅ V4 | Escopo revisado ("cabe no meu tempo?") | Continuar produção |
| ✅ V5 | Build de teste aprovado | Lançamento |

### Cadência sugerida de trabalho comigo

1. **Escolha um squad** da lista abaixo.
2. **Responda as perguntas** (ou me peça pra propor respostas e você edita).
3. **Eu registro** as decisões e aponto a próxima dependência destravada.
4. **Você valida** nos checkpoints antes de mudar de fase.

---

## 🧭 SQUAD 1 — Direção & Pré-produção
*Entrega minha: forte 🟢. É aqui que começamos.*

1. Em **uma frase**, que jogo é esse? (o "elevator pitch")
   > **Babel** — um roguelite com elementos de RPG situado na Babilônia, onde o objetivo é escalar a Torre de Babel, desvendando mistérios e superando desafios.

2. Quais são os **3 pilares** do jogo?
   > - **Pilar 1:** Combate action (mira/movimento livre, sem tab-target — estilo Hades/Returnal)
   > - **Pilar 2:** Escalada vertical roguelike por andares instanciados
   > - **Pilar 3:** Imersão no ambiente babilônico

3. Qual é a **fantasia central** do jogador — o que ele *sente* jogando?
   > Adrenalina; vontade de evoluir e melhorar a cada segundo. Espírito explorador — descobrir o que tem em cada lugar. Despertar curiosidade constante.

4. Qual é o **loop principal** de gameplay?
   > Subir a torre → lutar com inimigos → coletar itens e equipamentos → desbloquear habilidades → enfrentar chefes → morrer → progressão permanente → cidade (jardins suspensos) → retomar a subida.

5. Qual é o **escopo realista**?
   > MVP com 10 andares (geração procedural de 10 tipos de salas diferentes) e 1 chefe. 1 classe jogável (espada de 2 mãos), 1 set de equipamentos, NPCs na cidade, tutorial de comandos e mecânicas, talentos e skills (progressão durante a subida da torre).

6. Plataforma alvo?
   > PC

7. Referências diretas — 3 jogos que definem o tom.
   > - **Ratchet & Clank** (visual — plataformas industriais/flutuantes)
   > - **Hades** (estrutura roguelite)
   > - **Returnal** (gameplay — combate corpo a corpo e à distância)

8. O que este jogo **NÃO é**?
   > Mundo aberto, realista, soulslike, puzzle, plataforma, narrativo (foco não é em história pesada/cutscenes).

---

## 📖 SQUAD 2 — Narrativa & Roteiro
*Entrega minha: forte 🟢.*

1. Existe história/enredo, ou é focado em pura jogabilidade?
   > Existe história e enredo. O jogo se passa na antiga Mesopotâmia, terra cheia de mitos e lendas. Começa com o protagonista tendo visões em uma fogueira — lendas antigas, atrito entre deuses e a promessa de uma terra perdida rica em fertilidade, riquezas e prosperidade, desolada por conflitos onde todos cobiçam suas riquezas. Ao fim da visão, ele acorda e percebe que o lado de fora de sua casa não é mais o mesmo — está em outro mundo. Assim começa o jogo.

2. Quem é o **protagonista**? Motivação, personalidade, arco.
   > **Fazuk Sins**. No início se imagina um camponês incumbido de uma missão sagrada pelos deuses (por causa da visão). Motiva-se pela ideia de uma terra rica e próspera. Na verdade, é descendente direto de **Alexandre o Grande** — revelado apenas no confronto direto com Alexandre, penúltimo chefe da torre.

3. Qual é o **mundo/lore**? (por que existe a Torre de Babel? quem a construiu?)
   > Mesopotâmia antiga e mística, repleta de seres mitológicos, semideuses e criaturas. Inicialmente a Torre parece ser a fonte do mal — onde estão os vilões a derrotar para purificar a região e restaurar o equilíbrio. Depois se revela que a torre foi construída para gerar oferendas de alma e sangue às divindades que controlam a região, alimentando seu poder e imortalidade com as lutas, cobiças e almas humanas. O Jardim Suspenso foi a forma dos humanos zelarem pelas almas perdidas ali — um oásis divino em meio ao caos.

4. Há **antagonista** ou força de oposição?
   > Deuses mitológicos antigos que desejam se alimentar de todo o caos da Mesopotâmia.

5. Como a história é **contada**?
   > Ambiente (environmental storytelling), NPCs e item lore. Sem cutscenes pesadas — consistente com o anti-escopo definido no Squad 1 (não narrativo pesado).

6. Tom narrativo — sério, cômico, sombrio, aventura leve?
   > Sombrio e enigmático.

7. As **armas** têm história?
   > Sim — armas e equipamentos têm história própria, alinhada com a história principal, revelando detalhes, histórias antigas e conexões com a trama.

---

## 🎨 SQUAD 3 — Arte & Direção Visual
*Entrega minha: parcial 🟡 — eu dirijo, escrevo briefs e busco referência; seu pipeline gera.*

1. Qual é a **paleta de cores** dominante?
   > Ocres/areia da Mesopotâmia + dourado divino + vermelho de sangue/sacrifício. Existem arquivos MD separando a paleta por seções da torre: período de **Nabucodonosor**, **Assírio** e **Alexandrino**. O **Jardim Suspenso** é dourado, verde e azul — acolhedor, um oásis de descanso. A **torre** é sombria e misteriosa, sem ver o fim (se mistura nas nuvens).

2. Estilo de renderização — realista, estilizado, cel-shaded, low-poly?
   > **Estilizado.** Referências visuais: **Blasphemous**, **Ravenswatch**, **Hades**.

3. Quais **materiais-chave** se repetem? (linguagem de material)
   > Pedra/tijolo babilônico, ouro e bronze (andares mais altos), lápis-lazúli azul (andares), vitral (salas de boss), tecidos off-white sujos. Paredes = tijolo simples; casas/muros fora da torre = barro com pedras; chão = terra batida com grama baixa. Campos de cevada e trigo, tamareiras (comuns na região). **Ideia:** tâmara como item do jogo.

4. Guia de **proporção/silhueta**?
   > Protagonista **heroico**; NPCs e objetos realistas e proporcionais ao protagonista. Chefes podem ser ligeiramente maiores — mas como característica pessoal de cada um, não regra geral.

5. Linguagem visual do **ambiente**?
   > À primeira vista: sombrio e misterioso, sem indicação clara de progressão/conteúdo. Não escuro, mas sem visibilidade total do horizonte (exploração é core). Progressão de perigo transmitida por mudança de ambiente, NPCs e dificuldade: o salão deixa de ser escuro e passa a ouro, bronze, lápis-lazúli. O **Jardim Suspenso** contrasta com visibilidade total, música suave, ambiente sem perigo, pilares brancos adornados, ouro/bronze/lápis-lazúli, artes expostas, esculturas.

6. Referências de textura por categoria?
   > Pedra/tijolo babilônico, metal/ouro, orgânico (criaturas), energia/luz divina, **+ sangue, lápis-lazúli, bronze, madeira, barro, grama, água (poça e rio), couro, fogo, pelagem animal**. → vira lista de busca no PolyHaven + brief pro pipeline.

7. Assets: pipeline de IA vs. modelagem manual?
   > Máximo possível via **pipeline imagem→3D**.

8. Fluxo de validação de asset?
   > Checklist: **Escala · Formato · Pivô · Estilo e Cores · Performance e Articulação.**

9. Identidade visual do protagonista (Fazuk Sins)?
   > Humano normal, cabelos cacheados loiro-acobreado, forte e musculoso. Visual evolui conforme equipa itens (capacetes, armaduras), mas a **base se mantém sempre a mesma**.

10. UI / HUD — direção visual?
   > **Híbrido** (alinhado à dificuldade alta): parte básica — vida, uso de recursos rápidos, troca de armamento — em **UI limpa e moderna**; mapa, upgrade de armas, atributos, puzzles e itens em **UI diegética** (integrada ao mundo).

---

## 🎵 SQUAD 4 — Áudio
*Entrega minha: parcial 🟡 — eu escrevo briefs e organizo o sistema de som; IA de música (Suno/Udio) ou compositor executa.*

1. Tom da trilha?
   > **Épico.** Deve refletir a jornada emocional do protagonista — surpresa, admiração, coragem e medo. Reforça o suspense do jogo ao mesmo tempo que entoa coragem ao personagem.

2. Temas distintos?
   > Menu/título, Jardim Suspenso (hub), exploração dentro da torre, sala de bosses, tela de morte, exploração externa, exploração próxima aos rios, menu de pause (ideia: **assovio cantarolado**). A trilha **muda conforme as eras da torre** (Nabucodonosor / Assírio / Alexandrino).

3. Música adaptativa?
   > Sim — na **Esquiva Perfeita** ou num **combo elevado**, a música muda (stinger/camada).

4. Lista de SFX por evento?
   > **Movimento:** todos (passos, pulo, dash/esquiva, esquiva perfeita) + **arroto** ao consumir o item de cerveja. **Combate:** todos. **Inimigo:** morte de inimigo e morte de boss — **sem aviso sonoro de ataque** (telegraph é só visual). **Progressão:** todos. **UI:** todos + **som ao abrir o mapa**.

5. Áudio ambiente (soundscape)?
   > Sim para todos (vento na torre, tochas/fogo, ecos de pedra, água no Jardim, criaturas ao longe, maquinário da torre) + **barulho de fogueira**. Muda por andar/era.

6. Vozes / narração?
   > Apenas **linguagem inventada/cuneiforme** (idioma inexistente estilizado). Voz é opcional/desejável, não obrigatória — nada de idioma real.

7. Mixagem e prioridade?
   > Em combate intenso, prioridade no mix: **esquiva perfeita > dano recebido > acerto no inimigo > combo.**

8. Implementação no código?
   > **Nada de áudio implementado hoje — projeto de áudio começa do zero.** Avaliar opções de arquitetura (AudioManager autoload, buses de mixagem no Godot, AudioStreamPlayer por evento).

---

## 💻 SQUAD 5 — Programação
*Entrega minha: forte 🟢 — já rolando.*

> ⚠️ **Nota:** o projeto já está bem avançado — loop roguelike funcional ponta a ponta, 6 classes, meta-progressão. As respostas abaixo descrevem o **estado atual implementado**, não um plano.

1. Engine / framework / linguagem?
   > **Godot 4.7**, renderer **Forward+**, física **Jolt**, **GDScript puro**. Maior parte do jogo montada em código (root node + script em `_ready()`); exceções autoradas em cena: kit de props modulares (paredes/portas/colunas de 6m), shells de sala (`world/rooms/prefabs/`) e prefabs de atores (player, inimigos, NPCs). **15 autoloads** registrados.

2. Sistemas centrais (MVP)?
   > Movimentação/pulo/dash (esquiva); combate action (LMB segurado = chain combo em cone à frente); câmera pelo mouse; geração procedural de andares por tipos de sala pré-definidos (combate, baú, cura, desafio — puzzle é ideia futura). **Progressão temporária (run):** consumíveis, talentos (sala de desafio), skills ativas/morphs (level up por XP) — ofertados em grupos de 3 opções aleatórias, escolhe 1 por nível. **Progressão permanente:** equipamento (drop de bosses) + camada de meta via seeds. **Save:** 5 slots + save ao morrer.

3. Sistema de combate?
   > **LMB segurado:** combo de 3 ataques básicos em cone, gera recurso da classe. **RMB:** mecânica única por classe (ex. Paladino = postura de bloqueio que reduz dano e gera recurso, consumindo segmentos de barra própria). **Shift:** esquiva/dash com i-frames; **Esquiva Perfeita** (timing na janela final do telegraph) reembolsa stamina, estende invulnerabilidade, dá buff de dano temporário e câmera lenta. Todas as ações interrompem a sequência de ataque básico. Corpo-a-corpo é o foco do MVP, mas classes à distância (Ranger/arco, Mage/cajado) já existem. Skills ativas ocupam **6 slots de hotbar (1-6, remapeável)**, consomem recurso, cooldown individual ajustável por morph.
   > ⚠️ **EM ABERTO:** o modelo de mira/alvo ainda está sendo definido. Hoje há **targeting manual por Tab** (lock-on) implementado, mas se será mantido, trocado por mira livre pura ou virar híbrido é **decisão pendente** — não travar ainda.

4. IA de inimigos?
   > Máquina de estados **idle → perseguir → atacar → morte**. Idle = perambula em torno do spawn (não patrulha por rota) com **leash**. Entra em perseguição por proximidade ou ao tomar dano, contornando paredes via navmesh. Ataques: básico (melee ou projétil) + ataque **telegrafado** como forma no chão (círculo, retângulo, cone) — o telegraph é o core da esquiva e caracteriza cada inimigo. **Ainda não há:** grupos coordenados (por design, 1 confronto por vez) nem chefes multi-fase. A partir do andar 10 entra inimigo de tier "boss" no mix, sem fases.

5. Geração procedural dos andares?
   > Salas pré-fabricadas (1 cena por tipo: entrada, combate ×2 layouts, baú, cura/rest, desafio a partir do andar 3, + sala de saída). Montagem em runtime pelo **FloorAssembler**: self-avoiding random walk numa grade posiciona caminho principal + ramificações, ligados por corredores, **seedado por andar (determinístico)**. Dificuldade escala por faixa de andar (nº de salas, quantidade/tier de inimigos + multiplicadores globais). **Planejado:** sala de loja e encontros de chefe dedicados a cada 10 andares. Falta refino da curva por andar.

6. Pipeline de assets 3D?
   > Formato: sempre **.glb** (export limpo do Blender). Animações do Mixamo baixadas em FBX e rebaked/mescladas no mesmo .glb do rig via Blender (merge via script foi abandonado por bug de rotação). **Escala: 1 unidade Godot = 1 metro** desde Blender/Mixamo (cuidado: Mixamo costuma vir 100× maior — checar import scale ~0.01). **Pivô: centro** do objeto/bloco (não a base), igual aos prefabs de prop. **Colisão manual** (não gerada na importação): personagens = CapsuleShape3D codada à parte; peças estáticas = StaticBody3D + CollisionShape3D no grupo `nav_source`; greybox em código (`beveled_box.gd`) = malha e colisão paramétricas juntas.

7. Save / load e progressão permanente?
   > **MetaProgress** (autoload, save próprio independente dos slots): ganha **Seeds** matando inimigos (bônus em bosses), gasta no NPC **"Warden of the Hanging Gardens"** no hub (abas Buy = unlocks permanentes; Sell = vende materiais/consumíveis). **Reseta a cada run:** nível, XP, skills, morphs, talentos e inventário (o nó Player é recriado do zero a cada troca hub↔run). **SaveManager** (5 slots, ponto de save do hub) = único jeito de persistir nível/XP/skills/talentos/inventário entre sessões. **Pendente:** equipamento de boss sobreviver à morte, mais unlocks no Warden, preço real de venda por item.

8. Menus e UI (MVP — todos implementados)?
   > Tela de título; HUD de combate (barras vida/recurso, hotbar 6 skills + 4 itens, telegraphs, buffs/debuffs, dano flutuante com crítico, toasts); menu do personagem (Atributos / Inventário-com-Seeds / Habilidades-com-morphs-expansíveis / Talentos-árvore-radial-3-ramos: Stamina, Survival, Offender / Missões); menu de pausa (save/load 5 slots); tela de morte ("You Died" + andar + Seeds); popups de escolha de skill/morph e Soul Talent; janela de loot; UI de diálogo; loja do Garden Warden; painel de tingir armadura. **Pendente de polish:** tema visual (usa controles default do Godot), ícones de talento são placeholder.

9. Posicionamento de objetos na cena (aprendizado)?
   > Já tem experiência: importar assets, incluir colisão, alterar pivô, texturas/material. **DECIDIDO:** o jogo **terá física de colisão para abrir portas** (portas abrem naturalmente ao colidir/interagir) — a aprender/implementar.

10. Estado atual do projeto (`Babel/babel`)?
   > **Loop funcional ponta a ponta:** título → hub (`hub.tscn`) → TowerGate inicia run → FloorAssembler monta cada andar (6 tipos de sala + Swarm a partir do andar 2) → escadas → morte/conclusão (andar 10) volta ao hub. **Combate:** combo de 3 hits, Tab targeting, esquiva + Esquiva Perfeita, bloqueio RMB (Paladino com stacks de Retribuição). **6 classes** trocáveis (menu K, só teste); só o **Paladino** tem as 3 skills de hotbar + morphs completos. **Progressão em 2 camadas:** por-run (level up 3 escolhas, 15 Soul Talents, árvore radial) + meta via Seeds (10 unlocks no Warden: 5×20 + 5×40 Seeds). Equipamento e materiais persistem entre runs; só consumíveis resetam. Drop de equip: 100% bosses, 1-2% mobs. **Inimigos:** `EnemyBase` (estados/navmesh/telegraph/wander+leash); família Husk, Bandit/Brute/Spitter/Boss, Spawn (tier creep). **Dados:** classes/morphs/talentos/quests/inimigos em Resources `.tres`; itens em CSV. **Falta:** balanceamento por andar, rigs animados, grupos coordenados, chefes multi-fase, arte final (tudo greybox/placeholder). Distrito tutorial quebrado foi deletado.

---

## 🧪 SQUAD 6 — QA & Balanceamento
*Entrega minha: forte 🟢 — analiso dados, sugiro ajustes, monto checklists.*

1. O que define "**divertido**"?
   > Combate fluido, itens recebidos com bom valor, dificuldade alta, aprendizado e melhoria de gameplay, sensação de progresso, vontade de jogar de novo.

2. Como medir **dificuldade**?
   > Métricas: dano recebido de cada inimigo vs. dano de ataque (unitário e total do combate); % de mortes por andar; runs até vencer; quantidade de esquivas perfeitas. **Sensação-alvo:** o jogador deve morrer no **andar 2 na primeira run**.

3. Curva de dificuldade por andar?
   > Estrutura em **escadas por boss**: dificuldade 1 até o 1º boss, dificuldade 2 até o 2º, e assim por diante; boss final = outro patamar; "boss real" = ainda mais. Morrer é **aceitável em salas de boss, salas-desafio ou salas desafiadoras** (não em salas comuns).

4. Balanceamento de classes?
   > As 6 classes devem ser **equivalentes em poder** — mesmas regras e condições, sem variar dificuldade. Só a **gameplay/estilo do jogador** deve ser fator na escolha da classe. (Paladino = baseline.)

5. Economia e progressão?
   > Números atuais aprovados (XP por inimigo, Seeds por kill, preços 20/40 no Warden, drop 100% boss / 1-2% mob, 3 escolhas por level up). Sem ajustes por ora.

6. Bugs conhecidos (lista viva)?
   > ⏳ *Em branco — a preencher ao longo do projeto.*

7. Checklist de teste por build?
   > ⏳ *Em branco — Claude vai propor um checklist inicial.*

8. Planilha de balanceamento?
   > A definir **depois** que todos os bosses e armas estiverem definidos → então especificamos os stats. **Vai precisar de ajuda do Claude nisso.** (Dados hoje em `items.csv` + Resources `.tres`.)

9. Telemetria / logs?
   > **Sim, instrumentar.** Coletar: andar da morte, tempo por run, tempo por sala, dano tomado, dano concedido, qtd. de itens recebidos, qtd. de XP, qtd. de Seeds, qtd. de equipamentos.

---

## 📋 SQUAD 7 — Produção & Gestão
*Entrega minha: forte 🟢 — este é o orquestrador em ação.*

> 👥 **Equipe (2 pessoas):** **Gustavo** — arte + história, ~5h/semana. **Thiago** — programação, ~10h/semana. Horários variáveis. Projeto compartilhado via git (MD files + pasta de trabalho) para Thiago colaborar.

1. Tempo disponível?
   > Variável. Média: **Gustavo 5h/sem** (arte + história), **Thiago 10h/sem** (programação).

2. Próximo marco concreto?
   > **1 andar completo bonito, com o personagem jogável.** (= vertical slice)

3. Prioridades da fase atual?
   > **Arte / assets 3D** e **mais conteúdo.**

4. O que está bloqueado?
   > Dar **vazão rápida na criação de assets 3D**. E, antes disso, **definir o conteúdo do jogo**: história completa, lista de personagens, lista de armas, itens, colecionáveis. → **Precisa de uma "lista mestra do que o jogo tem que ter"** (quantidade e especificação) para pensar a arte e então gerar os assets.

5. Aprendizado vs. produção?
   > **Resolver e entregar pronto** (aprofundamento vira projeto separado, quando ele quiser). Mas em **toda etapa: explicar o quê, o porquê e por que a decisão foi tomada.** **Validar sempre** qualquer input que defina diretamente **história ou gameplay**.

6. Riscos / medos?
   > **Escopo grande demais** e **travar na criação de artes / modelos 3D.**

7. Definição de "pronto" do MVP (a mostrar)?
   > 1 classe completa pronta + entrar em 1 andar completo da torre + **áudio básico de efeitos (sem trilha ainda)** + menu pronto. *(Isto é o vertical slice; o MVP de 10 andares é meta posterior.)*

8. Checkpoints (datas)?
   > **V1** OK. Revisar **V2 (estilo visual)** e demais em **07/agosto/2026**.

9. Como trabalhar comigo?
   > **Tarefas técnicas soltas** (não um squad por vez). Programação será tocada pelo **Thiago** via git. Quer que o Claude **mantenha uma lista visual** (em andamento / backlog) + a **mesma visão em cronograma**, usando as horas úteis (5h + 10h) para **estimar a duração de cada fase**.

---

## 📌 Log de decisões (preencher ao longo do projeto)

| Data | Squad | Decisão travada |
|---|---|---|
| 2026-07-21 | Direção | Elevator pitch: **Babel** — roguelite/RPG na Babilônia, escalar a Torre de Babel |
| 2026-07-21 | Direção | 3 pilares: combate action, escalada vertical roguelike por andares instanciados, imersão babilônica |
| 2026-07-21 | Direção | **Combate confirmado como action combat** (mira/movimento livre, sem tab-target) — corrige registro anterior que citava WoW tab-target |
| 2026-07-21 | Direção | Loop: subir → lutar → coletar → desbloquear habilidades → chefe → morrer → progressão permanente → cidade (jardins suspensos) → repetir |
| 2026-07-21 | Direção | Escopo MVP: 10 andares (10 tipos de sala, geração procedural), 1 chefe, 1 classe (espada 2 mãos), 1 set de equip., NPCs na cidade, tutorial, talentos/skills |
| 2026-07-21 | Direção | Plataforma: PC |
| 2026-07-21 | Direção | Referências: Ratchet & Clank (visual), Hades (estrutura roguelite), Returnal (combate) |
| 2026-07-21 | Direção | Anti-escopo: sem mundo aberto, realismo, soulslike, puzzle, plataforma ou foco narrativo pesado |
| 2026-07-24 | Narrativa | Protagonista: **Fazuk Sins** — camponês que se crê escolhido; na verdade descendente de Alexandre o Grande (revelado no confronto com Alexandre, penúltimo boss) |
| 2026-07-24 | Narrativa | Abertura: visão na fogueira → acorda em outro mundo. Torre = fonte de oferendas de alma/sangue aos deuses antigos. Jardim Suspenso = oásis divino |
| 2026-07-24 | Narrativa | Antagonista: deuses mitológicos antigos que se alimentam do caos. Tom: **sombrio e enigmático**. História contada por ambiente + NPCs + item lore (armas têm lore próprio) |
| 2026-07-24 | Arte | Estilo **estilizado** (refs Blasphemous, Ravenswatch, Hades). Paleta: ocre/areia + dourado divino + vermelho de sangue; sub-paletas por era (Nabucodonosor, Assírio, Alexandrino) |
| 2026-07-24 | Arte | Materiais: tijolo babilônico, ouro/bronze (topo), lápis-lazúli, vitral (boss), tecido off-white. Fora da torre: barro+pedra, terra batida, cevada/trigo, tamareiras (tâmara como item) |
| 2026-07-24 | Arte | Proporção heroica (protag.), NPCs realistas; bosses ligeiramente maiores por personalidade. UI **híbrida** (limpa p/ combate, diegética p/ mapa/upgrade/itens). Assets: máximo via pipeline imagem→3D |
| 2026-07-24 | Programação | **Estado real:** loop roguelike funcional ponta a ponta, Godot 4.7 Forward+/Jolt/GDScript, 6 classes (só Paladino completo), FloorAssembler procedural seedado, meta-progressão via Seeds/Warden |
| 2026-07-24 | Programação | Combate: Esquiva Perfeita, telegraphs no chão, 6 skills na hotbar. **⚠️ EM ABERTO:** modelo de mira/alvo (Tab targeting atual vs. mira livre pura) ainda está sendo definido — não travar ainda |
| 2026-07-24 | Programação | Pipeline 3D: **.glb**, 1 un = 1 m, pivô no centro, colisão manual. Pendências: balanceamento por andar, rigs animados, chefes multi-fase, arte final (tudo greybox) |
| 2026-07-24 | Programação | **DECIDIDO:** o jogo terá **física de colisão para abrir portas** (portas abrem naturalmente ao colidir/interagir) |
| 2026-07-24 | Áudio | Trilha **épica** (surpresa/coragem/medo), muda por era da torre; adaptativa (Esquiva Perfeita e combo alto mudam a música); temas por ambiente + assovio no pause |
| 2026-07-24 | Áudio | SFX: todos os eventos + arroto (cerveja) + som de abrir mapa; **sem aviso sonoro de telegraph** (só visual). Vozes só em **idioma inventado/cuneiforme**. Prioridade no mix: esquiva perfeita > dano recebido > acerto > combo |
| 2026-07-24 | Áudio | **Áudio começa do zero** (nada implementado). Avaliar arquitetura (AudioManager autoload + buses) |
| 2026-07-24 | QA | "Divertido" = combate fluido + loot valioso + dificuldade alta + progresso + rejogar. **Alvo: morrer no andar 2 na 1ª run.** Curva em escadas por boss. 6 classes equivalentes (só estilo difere) |
| 2026-07-24 | QA | **Telemetria aprovada:** logar andar da morte, tempo/run, tempo/sala, dano tomado/dado, itens, XP, Seeds, equipamentos. Planilha de balanceamento vem depois de definir bosses+armas |
| 2026-07-24 | Produção | **EQUIPE: 2 pessoas** — Gustavo (arte+história, 5h/sem) + Thiago (programação, 10h/sem). Colaboração via git |
| 2026-07-24 | Produção | **Próximo marco = vertical slice:** 1 andar completo bonito + personagem jogável + SFX básico + menu. MVP de 10 andares = meta posterior |
| 2026-07-24 | Produção | Prioridade: **arte/assets 3D + conteúdo**. Bloqueio nº1: definir **lista mestra de conteúdo** (história, personagens, armas, itens, colecionáveis) antes de gerar arte |
| 2026-07-24 | Produção | Modo de trabalho: **tarefas soltas** (não squad-a-squad). Claude sempre explica o quê/porquê/por que decidiu e **valida inputs de história/gameplay**. Claude mantém backlog + cronograma visual |
| 2026-07-24 | Produção | **Checkpoint:** revisar V2 (estilo visual) e demais em **07/08/2026** |
| 2026-07-24 | Narrativa | **Reconciliação:** escrever uma **bíblia unificada** (funde `docs/01` + squads), aposenta o `docs/01` depois. Draft em `planejamento_squads/Biblia_Unificada_v0.1.md` |
| 2026-07-24 | Narrativa | **TRAVADO — Pedra Angular:** Fazuk Sins = "O Retornado" do repo (a visão na fogueira é a morte/colheita); Chosen/Arisen = como ele voltou (preserva as 6 classes) |
| 2026-07-24 | Narrativa | **TRAVADO — Morte Ferida:** Karru prendeu Ereshkalla (a Morte) pela metade → Torre-motor colhe almas → Fazuk é reciclado pra base; só ele lembra (sangue de Alexandre) = explicação lore do loop + meta-progressão |
| 2026-07-24 | Narrativa | **TRAVADO — Tom da base:** "a hospitalidade é a jaula" — os Jardins tratam Fazuk bem *porque* a morte dele favorece os deuses; calor sincero e sinistro (bezerro premiado) |
| 2026-07-24 | Narrativa | **TRAVADO — Estrutura/eras:** estratos ficcionais × eras históricas = "marcas d'água" dos climbers passados (Alexandre subiu mais alto → estrato alexandrino no topo). Tábuas ≡ fragmentos de Nissaba-Kel |
| 2026-07-24 | Narrativa | **TRAVADO — Clímax (3 degraus, = resposta de QA do Gustavo):** Alexandre (penúltimo) → Karru, o Selador (boss final, cume) → Tiamash/Tiamat (boss real, liberado ao quebrar Karru) |
| 2026-07-24 | Narrativa | **TRAVADO — O Final:** "O Portão Não Dá Nada" — ambíguo/cosmic horror. A subida era o ritual; Fazuk é a oferenda perfeita; não há vitória, só o loop. Final único, tint por alinhamento Chosen/Arisen. **CANON FECHADA.** |
| 2026-07-24 | Conteúdo | **TRAVADO — arma do Paladino = espada de 2 mãos** (alinha com o rig atual). ➡️ ação do Thiago: ajustar `WEAPON_CLASS` no código |
| 2026-07-24 | Conteúdo | **TRAVADO — inimigos ~4–5 por estrato + 1 mini-guardião** por estrato (reaproveitando asset do boss-portão, recolor/reescala) |
| 2026-07-24 | Conteúdo | **TRAVADO — set de equipamento do vertical slice = `guardian`** (casa com o Paladino) |
| 2026-07-24 | Narrativa | **`docs/06` (tutorial) realinhado** — "viajante de Nínive" → Fazuk (visão=morte→desperta na base); estrutura de beats e status de implementação preservados. Framing: ironia dramática (a confirmar) |
| 2026-07-24 | Narrativa | **Bíblia completa em prosa escrita** (`Biblia_Babel_Completa.md`) = canon narrativa oficial art-facing; **`docs/01` marcado SUPERSEDED**. Arte destravada |
| 2026-07-27 | Narrativa | 🔁 **REVISÃO BEAT A BEAT** — Gustavo releu e apontou densidade/confusão. Enredo quebrado em 6 beats e revisado do início ao fim. **23 decisões (D1–D23)**, várias revertendo a v1. Resultado: **`Biblia_Babel_Completa.md` v2.0**. Registro completo em `Biblia_Unificada_v0.1.md` §10 |
| 2026-07-27 | Narrativa | **D1–D3 (prólogo):** o pecado mira **só a Morte** (não "prender os deuses"); o cuneiforme-que-amarra sai do prólogo e vira recompensa de exploração; **uma** ferida central (a Morte parou) + 2 sintomas |
| 2026-07-27 | Narrativa | **D4–D6 (abertura):** a Torre isca e **Shamash assina** o chamado; **mesmo mundo** (morre olhando a Torre, acorda dentro dela — cai o "outro mundo"); linhagem plantada como objeto = **moeda com brasão de Bucéfalo** |
| 2026-07-27 | Narrativa | ⭐ **D7 — KARRU REMOVIDO. Shamash faz o pacto.** Motivo novo: Shamash se afeiçoou ao que **Alexandre** construiu (um mortal surpreendeu os deuses) e quis libertar o rebanho; não podia tocar a Morte (não morre → ela não se apresenta), então **pactuou com Alexandre como âncora mortal**. Alexandre ficou preso junto; a Mesopotâmia caiu em guerras sem fim (= história real dos Diádocos) |
| 2026-07-27 | Narrativa | **D8:** a Torre **já era motor de oferendas antes do pecado** — a ferida não criou a fome, tornou-a **infinita** (a mesma alma pra sempre). É a explicação lore definitiva do loop |
| 2026-07-27 | Narrativa | **D9:** o jogo se passa **2–3 gerações depois de Alexandre**, na guerra dos generais, com a Babilônia se esvaziando *(direção de arte: cidade grandiosa visivelmente vazia demais)* |
| 2026-07-27 | Narrativa | **D10:** aposentar "falso escolhido" → **escolhido de verdade, pro propósito errado** (o sangue dele é literalmente a chave) |
| 2026-07-27 | Narrativa | **D11–D14 (panteão):** Anshahar ≡ **Shamash** (fundidos); Shamash promete / **Tiamat revela e zomba**; **nomes reais** (Ereshkigal, Nisaba, Tiamat, Shamash); **Ningirash cortado**. Panteão de 6 → **4 deuses, todos com função** |
| 2026-07-27 | Narrativa | **Clímax reduzido a 2 bosses: Alexandre (penúltimo) → Tiamat (final).** Cai o degrau do Karru. ⚠️ **Boss do Estrato V ficou em aberto** |
| 2026-07-27 | Narrativa | ⭐ **D15 — O JARDIM É O ALÉM-VIDA LITERAL.** Toda alma termina nele e vira a seiva/água/luz do lugar. Três camadas: consolo pros vivos, verdade, e **hobby dos deuses**. **Motivo dos deuses muda de FOME para VAIDADE** — humanos são **ornamento e capricho**, não comida |
| 2026-07-27 | Narrativa | **D16:** **Garden Warden = Retornado quase-dissolvido** — perdeu a pessoa, manteve a função; não lembra o próprio nome, lembra o de Fazuk. É o que Fazuk vira se falhar |
| 2026-07-27 | Narrativa | **D17/D18:** a **meta-progressão é a Torre melhorando o espetáculo** — cada compra na loja é Fazuk concordando em ser um show melhor. *(Correção de doc: **Melhorias Permanentes** (10, persistem) ≠ **Talentos de Alma** (15, resetam por run) — a v1 confundia os dois)* |
| 2026-07-27 | Narrativa | **D19:** **Husks e Arisen = almas entaladas** — não completam o trajeto até o Jardim. A Torre está entupida de gente que morreu e nunca chegou |
| 2026-07-27 | Narrativa | **D20–D22 (subida):** *"a Torre veste o que ela toma"* (resolve estratos-por-era em 1 frase); **as tábuas são o motor da subida** (gradiente de informação, não de dificuldade); **os Arisen são a sala de espera do Jardim** — os que mais sabem e os mais amargos |
| 2026-07-27 | Narrativa | **D23:** **Chosen = vida emprestada por Shamash** (coleira bonita) · **Arisen = voltou como está**, não deve nada a ninguém. Resolve a contradição "Fazuk morreu na fogueira mas 3 classes são vivas". **Custo de código zero** (o `family` já existe no `ClassCatalog`) |
| 2026-07-27 | Narrativa | ⭐ **O FINAL (reescrito):** libertar a Morte **mata o Fazuk** (ele morreu na fogueira; só anda porque a Morte quebrou). **Aceitar** = morte real, fim do jogo · **Recusar** = matar Tiamat e voltar aos Jardins. **O loop É a recusa** — toda run jogada foi o Fazuk recusando. Inversão moral: **Tiamat está certa**, e você a mata por egoísmo |
| 2026-07-27 | Programação | ⚠️ **Flag de escopo:** a Confusão degradando a **própria UI** (mapa/interface mentindo) é caro/arriscado. MVP faz só o **diegético** (inscrições e falas de NPC virando gibberish) |
| 2026-07-27 | Narrativa | **TRAVADO — o Festival do Ato 1 honra O JARDIM** (os mortos que "foram descansar entre as flores"). Alegre, agradecido, sincero — substitui o antigo "celebrando a construção da torre", que contradizia o D8. Planta o D15 no 1º minuto e fica atroz em retrospecto. Regra: nada no Ato 1 pode insinuar que há algo errado |
| 2026-07-27 | Narrativa | **TRAVADO — Fazuk acorda NO BAIRRO**, não nos Jardins. **Os vivos não entram no Jardim** (é pra onde vão os mortos). Ele o descobre **explorando** (o portão simplesmente o deixa passar — 1ª pista de que já morreu, e deve passar despercebida) **ou** morrendo pela 1ª vez. Duas entradas pro mesmo hub, zero conteúdo extra |
| 2026-07-27 | Narrativa | ❌ **REJEITADO — boss do Estrato V como "o próprio Vínculo"** (proposta do Claude), por ser abstrato demais pra modelar e pra odiar |
| 2026-07-27 | Conteúdo | ⚖️ **TRAVADO — boss do Estrato III = O JUIZ DE HAMURABI** (ideia do Gustavo). Funcionário que aplicava as **leis de Talião**; preside a corte dos mortos prometidos ao Jardim que nunca chegaram — a maior injustiça da história — e **não pode julgar deuses, então julga quem alcança**. **Arma: o chicote de mãos** (cada tira é uma mão decepada aplicando a lei — arma e troféu). É o **espelho do Garden Warden**: os dois são pessoas reduzidas a uma função pela espera (ternura vs. julgamento). ⚙️ Gancho de design: talião pede **mecânica de reflexão de dano** |
| 2026-07-27 | Conteúdo | 🟡 **PROVISÓRIO — boss do Estrato V = Os Descartados**: as cascas que Fazuk deixou pra trás (a alma volta ao Jardim, a carne fica onde caiu), acumuladas onde as regras param de funcionar. Lutam com o moveset dele. **Custo de arte quase zero** (reaproveita o rig do jogador) e prepara o final (o loop é a recusa). Slot não lacrado |
| 2026-07-22 | Narrativa | Setting: Mesopotâmia antiga e mística; abertura com visão na fogueira → protagonista acorda em outro mundo |
| 2026-07-22 | Narrativa | Protagonista: **Fazuk Sins**, camponês que se crê escolhido pelos deuses; twist: descendente de Alexandre o Grande (revelado no penúltimo chefe) |
| 2026-07-22 | Narrativa | Twist do mundo: Torre parece a fonte do mal, mas foi construída para gerar oferendas de alma/sangue às divindades; Jardim Suspenso = oásis para almas perdidas |
| 2026-07-22 | Narrativa | Antagonista: deuses mitológicos antigos que se alimentam do caos da Mesopotâmia |
| 2026-07-22 | Narrativa | História contada por ambiente, NPCs e item lore (sem cutscenes pesadas). Tom: sombrio e enigmático |
| 2026-07-22 | Narrativa | **Alexandre o Grande = penúltimo chefe da torre**; armas/equipamentos têm lore próprio ligado à trama principal |
