# 📋 Planejamento por Squads — Gustavo × Claude

> **O que é esta pasta:** documentos de planejamento/direção do jogo, construídos em sessões do Gustavo com o Claude (Claude Code) através de um processo de "squads" (blocos temáticos de perguntas: Direção, Narrativa, Arte, Áudio, Programação, QA, Produção). São o registro das **decisões de design** e do **planejamento de produção**.
>
> Adicionada em **2026-07-24**. **2026-07-25: promovida a fonte canônica de direção/lore/roadmap** — os 4 docs numerados que ela sobrepunha (`01_Game_Concept`, `02_Core_Systems`, `04_Development_Roadmap`, `06_Roteiro_Ato1`) foram **arquivados em `docs/_legacy/`** (ver reconciliação abaixo). `CLAUDE.md` segue como fonte técnica/arquitetura canônica.

---

## Arquivos

| Arquivo | O que é |
|---|---|
| **GDD_e_Orquestrador.md** | GDD principal — 7 squads preenchidos + **log de decisões** (fonte da verdade das decisões de design tomadas com o Gustavo) |
| **Roadmap_Backlog.md** | Backlog (feito / em andamento / fase 2+) + **cronograma com estimativas por fase** (baseadas em Gustavo ~5h/sem arte, Thiago ~10h/sem prog) |
| **Roadmap_Visual.html** | Versão visual do backlog + cronograma (abrir no navegador; também publicado como artifact) |
| **Lista_Mestra_Conteudo.md** | **PROPOSTA v0.1** de conteúdo (história, personagens, bosses, armas, itens, colecionáveis). ⚠️ **Direção geral adotada** (protagonista, antagonista, estrutura de 4 tiers, mitologia real) — mas os 8 ❓ numerados no fim do doc (qtd. de andares/era, tom do final, roster de NPCs/inimigos, etc.) ainda aguardam validação item-a-item do Gustavo |
| `respostas_brutas/` | As respostas cruas do Gustavo a cada squad (provençância; o conteúdo já está sintetizado no GDD) |

---

## ✅ Reconciliação com os docs antigos — RESOLVIDA (2026-07-25)

Os 4 docs numerados que se sobrepunham a estes foram **arquivados em `docs/_legacy/`** (cada um com aviso de descontinuação apontando pra cá). Como cada conflito foi resolvido:

1. **Classes:** os nomes chutados na `Lista_Mestra_Conteudo.md` ("Lanceiro/Assassino/Sacerdote") permanecem como placeholder até o Thiago confirmar — ver a Decisão 6 daquele doc. Os nomes reais no código continuam **Paladin/Ranger/Mage (`family: chosen`) + Berserker/Thief/Occultist (`family: arisen`)** — `chosen`/`arisen` aí é só uma tag de tint/flavor no código, não carrega mais a lore antiga de vivo/morto-vivo (essa foi descartada junto com o `01_Game_Concept.md`).
2. **História:** **decidido — a lore desta pasta (Fazuk Sins, Mušḫuššu→Anzu→Alexandre→Tiamat) é a oficial.** O `06_Roteiro_Ato1.md` (viajante de Nínive) foi arquivado; o tutorial vai ser reescrito do zero em cima desta história nova (ver item "Reconstruir tutorial" no `Roadmap_Backlog.md`).
3. **Tom:** adotado o enquadramento do `CLAUDE.md` (**cosmic horror mesopotâmico**) — sem mudança de ação necessária.
4. **Itens:** usar os nomes reais já no jogo (**Kurunnu Brew**, Desert Ration, Herb of War, Polished Stone — ver `11_Guia_Itens.md`) em vez de reinventar.

**Próximo passo:** o Gustavo ainda valida os 8 ❓ numerados no fim da `Lista_Mestra_Conteudo.md` (granularidade de conteúdo), mas isso não bloqueia mais nada — a direção geral já está travada e os docs antigos já não existem mais como fonte concorrente.

---

## Fluxo de trabalho
- Estes `.md` renderizam no vault Obsidian junto com o resto de `docs/`.
- Commits/branches seguem o fluxo normal do repo (`.bat` helpers na raiz, branches `gustavo`/`thiago`) — ver `08_Equipe_e_Fluxo_de_Trabalho.md`.
