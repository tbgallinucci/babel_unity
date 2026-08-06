NOME: house_mudbrick_babylonian_01
CATEGORIA: estrutura / edificação (casa habitável, fora do escopo padrão de "prop" ou "parede" simples — trate como estrutura hard-surface de grande porte)
ESTRATO: Fora da torre — vila/assentamento externo na base da torre (não se aplica a nenhum dos estratos I–VI internos)
DIMENSÕES REAIS (m): 5m altura x 6m largura x 6m profundidade
PALETA: tons de barro/adobe — bege queimado (#C9A876), marrom argila (#A8845C), sombras de barro seco (#7A5C3E), madeira das portas/vigas envelhecida (#3E2E22), acentos de cerâmica das ânforas (#B5622E)
PASTA DE DESTINO FINAL (Godot): assets/models/environment/
ESTILO DE MODELAGEM SUGERIDO: hard-surface geométrico (Rota A — script procedural bpy, formas primitivas + bevel, igual ao padrão beveled_box.gd)

## Descrição do objeto
Casa térrea de barro/adobe (mudbrick) de dois níveis, estilo arquitetura mesopotâmica simples e antiga:
- Nível térreo: paredes de barro, uma porta de madeira frontal, uma pequena janela/vão quadrado.
- Escada externa de barro subindo pela lateral/frente até um terraço/telhado superior (característica típica de casas babilônicas — terraço usável).
- Nível superior: parapeito de barro, viga de madeira saliente no topo (beiral), pequena abertura/janela.
- Detalhes de cena: duas ânforas de cerâmica encostadas na parede externa, base/soleira de pedra.
- Estado de conservação: usada mas íntegra (não é ruína), superfície de barro com rachaduras superficiais naturais de secagem.

## Interior
O interior deve ter volume OCO (não sólido) — o Gustavo vai alocar objetos/mobília dentro da casa em assets futuros. Não modelar mobília interna agora, só garantir que existe espaço interno navegável nível térreo.

## ⚠ Nota técnica crítica para o blender-modeler
O TELHADO/TERRAÇO SUPERIOR (a laje/parapeito de cima que cobre o nível térreo) deve ser modelado como **objeto/mesh SEPARADO** do resto da casa — não mesclar numa única mesh de corpo único. Nomear esse objeto claramente no Outliner como `telhado` (ou `roof`), em snake_case, separado de `casa_corpo` (paredes) e demais partes.

Motivo: existe um requisito de gameplay (fora deste pipeline de asset) de esconder/ocultar o telhado quando o personagem entra na casa, pra dar visão do interior. Isso será resolvido depois por um script Godot que alterna a visibilidade do nó do telhado — só é possível se o telhado for um nó independente na cena final (`MeshInstance3D "telhado"` separado dentro do `.tscn`). Nem o blender-modeler nem o godot-asset-integrator precisam implementar a lógica do script, só garantir que a geometria/hierarquia permita a separação.

## ⚠ Nota técnica — porta articulada (mecanismo de abrir/fechar)
A PORTA (`porta_madeira`) também é objeto **SEPARADO** do corpo da casa — não fundida na parede. Ela vem dentro de um Empty pivot chamado `porta_pivot`, posicionado na aresta vertical esquerda do vão da porta (dobradiça), do chão até o topo da porta — mesmo padrão usado na tampa do baú (`chest_wood_stratum1_01`, pivot `lid_root` na dobradiça traseira).

Isso permite que o `godot-asset-integrator` (ou uma etapa de scripting posterior) implemente a rotação animada de abrir/fechar ao interagir (ex: `AnimationPlayer` rotacionando `porta_pivot` em Y de 0° a ~100° em torno do eixo Z). **Nenhum agente deste pipeline implementa a lógica de interação/animação** — só garantimos que o pivot está no lugar certo. Ao exportar/montar a cena, mantenha `porta_pivot` como nó separado (não achatar/join com o resto da casa).

## Nota sobre origem do concept
Este concept não foi gerado via prompt de imagem/Fooocus — o Gustavo trouxe um render de referência externo já aprovado (arquivo `Casa.png`), mostrando vista de conjunto (cluster de 3 estruturas, usado só como contexto de vila) e vista de detalhe da casa principal (a que está sendo modelada). Não há `01_prompt_concept.md` para este asset.

## Checklist de validação (já aprovado pelo Gustavo)
- [x] Silhueta legível — sim, formas geométricas simples de casa de barro.
- [x] Paleta bate com estrato — não se aplica a estrato I-VI, mas consistente com identidade visual mesopotâmica do projeto.
- [x] Estilo consistente — imagem é semi-realista (não é o estilo estilizado low-poly final do jogo), mas serve como referência de proporção/forma para modelagem, não como textura final.
- [x] Ângulo correto — vista 3/4 com régua de escala, suficiente para modelagem.
- [x] Escala confirmada pelo Gustavo: 6m x 6m x 5m (altura).
