NOME: chest_wood_stratum1_01
CATEGORIA: prop (interativo — loot container com tampa articulada)
ESTRATO: I — The Foundation (cidade viva na base da torre, mudbrick, ocre/osso, luz dura de sol)
DIMENSÕES REAIS (m): 0.40 altura x 1.00 largura x 0.50 profundidade
PALETA:
  - Madeira base (escurecida após feedback do Gustavo — ref. madeira escura tipo nogueira/mogno): #4A2E18 aprox., variações mais escuras nas sombras/entalhes
    (v1 usava #B8813F, tom mel claro demais — ajustado)
  - Ferragens metálicas escuras (ferro envelhecido): #2B2621 / #3D372E
  - Textura de madeira REAL (veios, grão): NÃO aplicar ainda no Blender — é
    trabalho da etapa `godot-asset-integrator` (regra das 3 camadas,
    docs/03). Referência trazida pelo Gustavo: madeira escura tipo
    nogueira/mogno com veios verticais pronunciados, textura tileável.
  - Acento terracota do ambiente ao redor (Estrato I): #CC4E5C
  - Acento dourado divino sutil (rim light/detalhe opcional): #FFD700
PASTA DE DESTINO FINAL (Godot): assets/models/props/
ESTILO DE MODELAGEM SUGERIDO: hard-surface geométrico
  → Rota A recomendada pro blender-modeler: script procedural (bpy), corpo do
    baú + tampa como dois blocos com bevel pequeno (0.05–0.15m), reforços
    metálicos como boxes finos sobrepostos. Baixo custo, sem gastar crédito
    de IA. NÃO precisa da rota C (geração de IA 3D) — é geometria simples.

## Requisitos funcionais (registrar para os próximos agentes)
- **Interativo**: quando o jogador interagir (tecla E) com o baú, a **tampa
  abre e permanece aberta** (não fecha sozinha; loot de baú é padrão
  "abre uma vez"). Isso implica, no pipeline de integração
  (godot-asset-integrator):
  - Modelar TAMPA como objeto separado do CORPO (pivot da tampa na dobradiça
    traseira, não no centro do objeto inteiro) para permitir rotação/animação
    de abertura depois. **(Feito — ver `04_gerar_modelo.py`: `lid_pivot` é um
    Empty posicionado exatamente na dobradiça, -Y/topo do corpo.)**
  - O mecanismo de abrir (rotação do `lid_pivot` em torno do eixo X, algo
    como -100° a -110°, disparada uma vez ao interagir e mantida — via
    `AnimationPlayer` + Area3D/interação, seguindo o padrão de interactables
    do projeto, collision layer 16) é trabalho de **código/Godot**, não de
    modelagem 3D. Fica para a etapa `godot-asset-integrator` montar o
    prefab `.tscn` com isso. Se a lógica de interação (script GDScript) for
    além do prefab visual, é território de sistemas — avisar o Thiago
    conforme `docs/08_Equipe_e_Fluxo_de_Trabalho.md`.
  - Interior oco (o "vazio por dentro" pedido pelo Gustavo) — não precisa de
    geometria interna detalhada, só espaço suficiente pra não parecer sólido
    quando a tampa abrir (paredes internas finas ou simplesmente fundo/paredes
    visíveis).
  - Collision layer 16 (interactables) além da 1 (world), conforme
    `docs/03_Godot_Project_Structure.md` / CLAUDE.md — baú é objeto interativo
    de loot, não só obstáculo estático.
- Referência visual: foto colada na conversa pelo Gustavo (baú de madeira
  estilo "trunk", ferragens escuras, tampa curva, alças de argola, fivela
  central) — sem arquivo salvo em disco, conceitual apenas.

## Próximo passo manual (Gustavo)
1. Abrir o Fooocus.
2. Colar o prompt positivo/negativo de `01_prompt_concept.md`.
3. Estilo: preset "flat/game asset/illustration" se disponível; evitar "photographic realistic".
4. Gerar pelo menos 4 variações.
5. Escolher a melhor e salvar como `Testes/Apoio/.assets_pipeline/chest_wood_stratum1_01/03_concept.png`.
6. Voltar aqui e avisar (colar a imagem ou confirmar o caminho).
