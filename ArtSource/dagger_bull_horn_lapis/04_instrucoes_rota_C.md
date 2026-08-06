ROTA: C — Geração de IA (Meshy/Tripo) + limpeza manual no Blender

## Passo 1 — Gerar o modelo bruto
1. Abrir Meshy.ai ou Tripo3D (o que tiver crédito disponível).
2. Modo **Image to 3D**, subir o arquivo `03_concept.png` desta pasta.
3. Configurações:
   - Topologia "quad" se disponível.
   - Densidade de polígono **média/baixa** (não "high poly").
4. Exportar como **.glb**.
5. Salvar em: `Testes/Apoio/.assets_pipeline/dagger_bull_horn_lapis/04_bruto_ia.glb`.

## Passo 2 — Importar e limpar no Blender
1. `File > Import > glTF 2.0 (.glb/.gltf)` → selecionar `04_bruto_ia.glb`.
2. Selecionar o objeto → `Object > Apply > All Transforms` (Ctrl+A → All Transforms).
3. Checar normais: `Edit Mode` → `A` (select all) → `Mesh > Normals > Recalculate Outside` (Shift+N). Se alguma face aparecer preta no Material Preview, as normais estão erradas.
4. Se a malha vier pesada: `Modifier Properties > Add Modifier > Decimate`, reduzir até ficar leve sem perder a silhueta (comparar com o concept — atenção especial à curva do chifre, não deixar "facetada" demais).
5. Corrigir escala real: `N` → aba Item → Dimensions. A adaga deve medir **0.40m de comprimento total** (ponta do chifre até a base do cabo). Escalar se necessário, depois `Ctrl+A > All Transforms` de novo.
6. Confirmar proporção interna: lâmina de chifre ≈ 0.22-0.24m, cabo ≈ 0.15m (ver ficha técnica).

## Passo 3 — Origem/pivot
- Definir a origem do objeto na **base do cabo** (ponto onde a mão empunha, extremidade oposta à ponta do chifre) — é o pivot natural para uma arma de duas mãos segurada verticalmente/diagonalmente.
- `Shift+Right-click` no ponto da base do cabo pra mover o 3D cursor → `Object > Set Origin > Origin to 3D Cursor`.

## Passo 4 — Nome e salvar
- Renomear o objeto no Outliner para `dagger_bull_horn_lapis` (snake_case, batendo com o slug).
- `File > Save As` → `Testes/Apoio/.assets_pipeline/dagger_bull_horn_lapis/04_modelo_bruto.blend`.

## Checklist final antes de aprovar
- [ ] Escala correta: 0.40m de comprimento total (comparar com cubo de 1m como referência).
- [ ] Origem na base do cabo.
- [ ] Transforms aplicados por último (Ctrl+A).
- [ ] Sem normais invertidas (nenhuma face preta no Material Preview).
- [ ] Sem geometria solta/flutuante.
- [ ] Nome do objeto = `dagger_bull_horn_lapis` no Outliner.
- [ ] Silhueta bate com o concept: curva do chifre reconhecível, amarras de couro visíveis na junção e na base, cabo de pedra com veios (se a textura vier embutida do Meshy/Tripo).

Tire um print da viewport em Material Preview, ângulo 3/4, e traga de volta (ou descreva o resultado) pra eu validar.
