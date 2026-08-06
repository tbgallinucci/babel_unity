---
name: novo-asset
description: Orquestra o pipeline completo de criação de um asset visual novo para BABEL (objeto, NPC, vegetação, arma, parede, textura) — do concept em PNG até o Prefab pronto pra usar no Unity. Use quando o Gustavo disser algo como "quero criar um asset novo", "/novo-asset", "vamos modelar tal objeto".
---

# Pipeline de criação de asset visual — BABEL

Este comando guia a criação de um asset do zero até dentro do jogo, em **3 etapas sequenciais com portão de validação manual entre cada uma**. Nunca pule uma etapa nem avance sem a aprovação explícita do Gustavo.

```
Etapa 1                Etapa 2                  Etapa 3
concept-artist    →    blender-modeler     →    unity-asset-integrator
(ideia → PNG)          (PNG → .blend)           (.blend → dentro do jogo)
     ↓ gate                  ↓ gate                    ↓ gate
"aprovado"             "aprovado"                "funcionando no jogo"
```

## Como conduzir

1. **Etapa 1 — Concept.** Invoque o agente `concept-artist` (via Agent tool, `subagent_type: concept-artist`) passando o que o Gustavo descreveu (objeto, características, referência opcional). O agente devolve um prompt de imagem + ficha técnica e pede pro Gustavo gerar o PNG no Fooocus (ou similar) e trazer de volta.
   - **NÃO avance para a Etapa 2** até o Gustavo confirmar explicitamente ("aprovado", "pode seguir", etc.) o PNG final.
   - Se ele pedir ajustes, volte a invocar o mesmo agente com o feedback — quantas vezes forem necessárias.

2. **Etapa 2 — Modelagem 3D.** Só depois do gate 1, invoque `blender-modeler`, passando o caminho do concept aprovado e da ficha técnica (`ArtSource/<slug>/`). O agente decide a rota (procedural / vegetação / IA), dá o passo a passo exato no Blender, e pede confirmação do resultado (print ou descrição).
   - **NÃO avance para a Etapa 3** sem o Gustavo confirmar o checklist técnico (escala, pivot, normais) e aprovar visualmente.

3. **Etapa 3 — Integração no Godot.** Só depois do gate 2, invoque `unity-asset-integrator`, passando o `.blend` aprovado. O agente guia o export `.glb`, a colocação nas pastas certas de `Assets/Art/`, o material URP e o Prefab, e pede pro Gustavo testar dentro do jogo (Play).
   - O pipeline só termina quando o Gustavo confirma que **viu o objeto funcionando dentro do jogo**.

## Regras gerais que valem o pipeline inteiro

- **Cada etapa exige uma confirmação explícita em palavras** do Gustavo antes de avançar — nunca assuma aprovação por silêncio ou por você achar que "ficou bom".
- Se o Gustavo pedir pra pular uma etapa (ex: já tem um PNG pronto de outro lugar), tudo bem — comece direto na etapa correspondente, mas ainda assim valide o input antes de seguir (ex: o `blender-modeler` deve conferir se a ficha técnica existe/faz sentido antes de modelar).
- Todo o trabalho intermediário fica em `ArtSource/<slug>/` — isso preserva o histórico de cada asset (prompt, concept, modelo bruto) **fora de `Assets/`** — obrigatório, porque o Unity auto-importa qualquer `.blend` que esteja sob `Assets/`.
- No fim, pergunte se o Gustavo quer que você já sugira o próximo asset da lista dele, ou encerre por aqui.
