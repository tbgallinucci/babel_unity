# 🗺️ Babel — Roadmap & Backlog

> Documento vivo. Fonte da verdade para o quadro visual (Kanban) e o cronograma. Atualizado conforme as tarefas andam.
>
> **Equipe & capacidade:** Gustavo (arte + história) ~5h/sem · Thiago (programação) ~10h/sem.
> **Data-base deste plano:** 24/07/2026. **Próxima revisão de checkpoint (V2):** 07/08/2026.

---

## 🎯 Alvo atual: VERTICAL SLICE (Fase 1)
**Definição de pronto:** 1 andar completo *bonito* + personagem jogável (Paladino) + SFX básico (sem trilha) + menu pronto.
*(O MVP de 10 andares é meta posterior — Fase 2.)*

> ⚠️ **Gargalo crítico = ARTE (5h/sem).** A programação-base já existe; o que falta pro slice é majoritariamente asset 3D. Isso valida o risco declarado (travar em arte / escopo). Mitigações no fim do doc.

---

## 📋 QUADRO (Kanban)

### ✅ FEITO (já funcional em greybox)
| Item | Área |
|---|---|
| Loop roguelike ponta a ponta (título → hub → run → andares → morte) | Prog |
| FloorAssembler procedural seedado (6 tipos de sala + Swarm) | Prog |
| Combate: combo 3 hits, esquiva + Esquiva Perfeita, bloqueio (Paladino) | Prog |
| 6 classes trocáveis (só Paladino completo) | Prog |
| Progressão por-run: level up 3-escolhas, 15 Soul Talents, árvore radial | Prog |
| Meta-progressão: Seeds, Warden, 10 unlocks | Prog |
| Save/load 5 slots + MetaProgress | Prog |
| Todas as telas de UI do MVP (greybox) | Prog |
| IA de inimigos: EnemyBase (estados, navmesh, telegraphs) | Prog |
| GDD completo — 7 squads preenchidos + log de decisões | Direção |

### 🔵 EM ANDAMENTO / PRÓXIMO (Vertical Slice)
| Item | Área | Dono | Estimativa |
|---|---|---|---|
| **Lista mestra de conteúdo** (história completa, personagens, armas, itens, colecionáveis) — *bloqueia a arte* · 📝 **rascunho v0.1 pronto em `.work/Lista_Mestra_Conteudo.md`, aguardando validação do Gustavo (8 decisões)** | Direção/História | Claude + Gustavo (validar) | ~1 sem |
| Modelo do protagonista Fazuk Sins: modelagem + rig + anims (Mixamo) | Arte | Gustavo | ~10–14h |
| Inimigos do andar 1 (2–3): modelo + rig + anims | Arte | Gustavo | ~10–14h |
| Kit de ambiente do andar 1: shells de sala + props (paredes, portas, colunas, tochas, braseiros, tamareiras, cevada/trigo, urnas) + texturas | Arte | Gustavo | ~16–22h |
| Skin/tema da UI (híbrido: limpo + diegético) | Arte | Gustavo | ~6–8h |
| Ícones da árvore de talentos (15) | Arte | Gustavo | ~4–6h |
| Física de colisão para abrir portas | Prog | Thiago | ~4h |
| Sistema de áudio do zero (AudioManager + buses + SFX por evento) | Prog | Thiago | ~10h |
| Sistema de telemetria/logs (andar da morte, tempos, dano, itens, XP, Seeds, equip.) | Prog | Thiago | ~6h |
| Integração de assets + máquina de animação (herói + inimigos) | Prog | Thiago | ~12–16h |
| Passe de balanceamento do andar (playtest — alvo: morrer no andar 2 na 1ª run) | Prog + QA | Thiago | ~6h |
| Polish do Paladino + bugfix do slice | Prog | Thiago | ~8h |
| Pacote de SFX básico (brief + execução Suno/biblioteca) | Áudio | Claude → exec | ~1 sem |

### 🗄️ BACKLOG (Fase 2+ — depois do slice)
| Item | Área |
|---|---|
| Completar as outras 5 classes (skills + morphs) | Prog + Design |
| Conteúdo dos 10 andares + boss com fases | Prog + Arte |
| Boss "Alexandre" (penúltimo) + "boss real" | Design + Arte |
| Trilha sonora completa (adaptativa, por era da torre) | Áudio |
| Sala de loja + encontros de boss dedicados a cada 10 andares | Prog |
| Planilha de balanceamento centralizada (após definir bosses + armas) | QA + Claude |
| Grupos coordenados de inimigos | Prog |
| Lore no jogo (item lore + environmental storytelling) | História + Arte |
| Reconstruir tutorial (o antigo quebrado foi deletado) | Prog + Design |
| Equipamento sobreviver à morte + mais unlocks no Warden | Prog |
| Lista de bugs conhecidos (QA — vazia, a preencher) | QA |
| Checklist de teste por build (Claude vai propor inicial) | QA |
| **Decisão em aberto:** modelo de mira/alvo (Tab-target atual vs. mira livre) | Design |

---

## 📅 CRONOGRAMA (estimativa por fase)

Estimativas em **semanas de calendário**, considerando arte 5h/sem e programação 10h/sem rodando **em paralelo**. Arte é o caminho crítico.

| Fase | Escopo | Esforço (arte / prog) | Duração estimada | Janela alvo |
|---|---|---|---|---|
| **0 · Direção** | GDD + decisões | — | ✅ concluída | — |
| **1 · Vertical Slice** | 1 andar bonito + Paladino + SFX + menu | ~46–64h arte / ~46h prog | **~10–12 semanas** (arte-bound) | jul → **out/2026** |
| **2 · Produção** | 10 andares, 5 classes, bosses, trilha | grande — reestimar após slice | **~6–9+ meses** (a refinar) | out/2026 → meados 2027 |
| **3 · Polish** | Estabilizar, balancear, corrigir | ~30% do tempo total | **~2–3 meses** | meados 2027 |
| **4 · Lançamento** | Build final, empacotar, publicar | pequeno | **~2–4 semanas** | 2027 |

> **Marco imediato (07/08/2026):** primeiros testes de estilo visual prontos para aprovar no checkpoint **V2** (~2 semanas de arte a partir de agora).
>
> **Por que a Fase 1 é ~10–12 semanas e não menos:** a programação-base termina em ~5 semanas (Thiago tem folga), mas os ~46–64h de arte a 5h/sem levam ~10–13 semanas. **O slice sai no ritmo da arte.**

### 🛡️ Mitigações do gargalo de arte
1. **Paralelizar a geração de assets** no pipeline imagem→3D (gerar em lote, não um a um).
2. **Enxugar o andar 1** — menor nº de props únicos possível pro slice; reaproveitar.
3. **Texturas do PolyHaven** em vez de autorar do zero onde der.
4. **Thiago (com folga) adianta Fase 2** ou ajuda a montar/limpar assets enquanto Gustavo modela.
5. **Definir a lista mestra de conteúdo primeiro** — evita retrabalho de arte por indefinição.
