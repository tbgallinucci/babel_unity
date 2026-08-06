## Passo 1 — Exportar o .blend aprovado pra .glb

No Blender, com `04_modelo_bruto.blend` aberto:

1. Selecionar o objeto `model` (a adaga) na viewport ou no Outliner.
2. `File > Export > glTF 2.0 (.glb/.gltf)`.
3. Painel direito da janela de export:
   - **Format**: `glTF Binary (.glb)`.
   - **Include > Selected Objects**: marcado.
   - **Transform > +Y Up**: marcado.
   - **Geometry > Apply Modifiers**: marcado.
   - **Geometry > UVs / Normals**: marcados.
4. Nome do arquivo: `dagger_bull_horn_lapis.glb`.
5. Salvar em: `Testes\Apoio\.assets_pipeline\dagger_bull_horn_lapis\05_dagger_bull_horn_lapis.glb`.

Me avise quando salvar aí — eu copio pra `Babel/babel/assets/models/props/` e sigo com material + prefab.
