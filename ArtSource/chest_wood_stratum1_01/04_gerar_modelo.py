"""
chest_wood_stratum1_01 — script procedural de modelagem (Rota A)
BABEL — Estrato I

Como usar:
1. Abrir o Blender.
2. Ir na aba "Scripting" (topo da janela).
3. New > colar este arquivo inteiro (substituindo qualquer versão anterior).
4. Rodar com o botão Play (ou Alt+P).
5. O baú aparece na viewport 3D, corpo + tampa alinhados formando a caixa
   fechada (a tampa fica com pivot próprio na dobradiça, pronta pra ser
   rotacionada depois no Godot).

Convenções do projeto (ver docs/03_Godot_Project_Structure.md):
- 1 unidade Blender = 1 metro (igual ao Godot).
- Pivot na base central (X/Y centrados, Z=0 na base) para o CORPO.
- A TAMPA tem pivot próprio na dobradiça traseira (-Y). O MECANISMO de
  abrir/fechar (rotação animada ao interagir) NÃO é feito aqui — isso é
  comportamento de jogo (AnimationPlayer + script de interação), montado na
  próxima etapa do pipeline (godot-asset-integrator). Aqui só garantimos que
  o pivot está no lugar certo pra isso funcionar.
- Baixa contagem de polígonos, bevel pequeno nas arestas duras.
- Cores aqui são só preview — a textura final de verdade entra depois no
  Godot (regra das 3 camadas, ver docs/03).

Notas de versão (bugs corrigidos):
- v1: os filhos da tampa usavam coordenadas "locais" mas o `.parent` era
  atribuído direto em Python sem compensar a transformação
  (matrix_parent_inverse) — as peças da tampa pulavam de posição. Corrigido
  usando SEMPRE coordenadas de mundo + `parent_set(keep_transform=True)`.
- v2: `add_box` escalava o cubo base (que já tem 1m de aresta) por
  `size/2`, deixando toda peça com METADE do tamanho pretendido — por isso
  as peças pareciam flutuar sem se tocar (posições calculadas pra tamanho
  cheio, mas caixas menores). Corrigido: escala agora é `size` direto.
"""

import bpy
import math

# ---------------------------------------------------------------------------
# Limpeza da cena (remove objetos default: cubo, luz, câmera)
# ---------------------------------------------------------------------------
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

SLUG = "chest_wood_stratum1_01"

# Dimensões reais (m) — ver ficha técnica. Convenção de eixos: X = largura,
# Y = profundidade (+Y = frente, onde o jogador interage; -Y = fundo/dobradiça),
# Z = altura (0 = chão).
WIDTH = 1.00
DEPTH = 0.50
HEIGHT_TOTAL = 0.40
HEIGHT_BODY = 0.32
HEIGHT_LID = HEIGHT_TOTAL - HEIGHT_BODY  # 0.08

WALL_THICKNESS = 0.03
BEVEL_WIDTH = 0.01
BEVEL_SEGMENTS = 2

METAL_INSET = 0.015
METAL_STRAP_W = 0.06

COL_WOOD = (0.184, 0.098, 0.047, 1.0)   # #4A2E18 (escurecido — ref. madeira escura trazida pelo Gustavo)
COL_METAL = (0.169, 0.149, 0.129, 1.0)  # #2B2621


def make_material(name, color):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = color
        bsdf.inputs["Roughness"].default_value = 0.7
    return mat


mat_wood = make_material(f"{SLUG}_wood_preview", COL_WOOD)
mat_metal = make_material(f"{SLUG}_metal_preview", COL_METAL)


def add_box(name, size, world_location, material, bevel=True):
    """Cria uma caixa nas dimensões `size` (m), centrada em `world_location`
    (coordenadas de MUNDO, sempre — nunca relativas a um pai).

    primitive_cube_add(size=1.0) já cria um cubo com 1m de aresta (o
    parâmetro `size` É o comprimento da aresta, não um raio) — então a
    escala aplicada aqui deve ser `size` diretamente, e NÃO `size/2`
    (v1/v2 tinham esse bug: dividiam por 2 sem necessidade, deixando toda
    peça com metade do tamanho pretendido e abrindo gaps entre elas)."""
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=world_location)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = (size[0], size[1], size[2])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    if bevel:
        bevel_mod = obj.modifiers.new(name="Bevel", type='BEVEL')
        bevel_mod.width = BEVEL_WIDTH
        bevel_mod.segments = BEVEL_SEGMENTS
        bevel_mod.limit_method = 'ANGLE'
    return obj


def parent_keep_transform(children, parent_obj):
    """Parenteia sem mover nada (equivalente a Ctrl+P > Keep Transform)."""
    bpy.ops.object.select_all(action='DESELECT')
    for child in children:
        child.select_set(True)
    parent_obj.select_set(True)
    bpy.context.view_layer.objects.active = parent_obj
    bpy.ops.object.parent_set(type='OBJECT', keep_transform=True)


# ---------------------------------------------------------------------------
# CORPO — pivot na base central (X=0, Y=0, Z=0 na base)
# ---------------------------------------------------------------------------
# Oco: fundo + 4 paredes finas (não um bloco sólido), pra já deixar o
# interior vazio (exigência da ficha técnica: precisa estar vazio pro loot).

body_root = bpy.data.objects.new(f"{SLUG}_body", None)
bpy.context.collection.objects.link(body_root)
body_root.location = (0, 0, 0)

body_parts = []

body_parts.append(add_box(
    f"{SLUG}_body_bottom",
    (WIDTH, DEPTH, WALL_THICKNESS),
    (0, 0, WALL_THICKNESS / 2.0),
    mat_wood,
))

for sign, tag in [(-1, "back"), (1, "front")]:
    body_parts.append(add_box(
        f"{SLUG}_body_wall_{tag}",
        (WIDTH, WALL_THICKNESS, HEIGHT_BODY),
        (0, sign * (DEPTH / 2.0 - WALL_THICKNESS / 2.0), HEIGHT_BODY / 2.0),
        mat_wood,
    ))

for sign, tag in [(-1, "left"), (1, "right")]:
    body_parts.append(add_box(
        f"{SLUG}_body_wall_{tag}",
        (WALL_THICKNESS, DEPTH - 2 * WALL_THICKNESS, HEIGHT_BODY),
        (sign * (WIDTH / 2.0 - WALL_THICKNESS / 2.0), 0, HEIGHT_BODY / 2.0),
        mat_wood,
    ))

for sx in (-1, 1):
    for sy in (-1, 1):
        body_parts.append(add_box(
            f"{SLUG}_body_corner_strap_{'r' if sx > 0 else 'l'}{'f' if sy > 0 else 'b'}",
            (METAL_STRAP_W, METAL_STRAP_W, HEIGHT_BODY + 0.01),
            (sx * (WIDTH / 2.0 - METAL_STRAP_W / 2.0 + METAL_INSET),
             sy * (DEPTH / 2.0 - METAL_STRAP_W / 2.0 + METAL_INSET),
             HEIGHT_BODY / 2.0),
            mat_metal,
            bevel=False,
        ))

body_parts.append(add_box(
    f"{SLUG}_body_band_top",
    (WIDTH + 2 * METAL_INSET, METAL_STRAP_W, HEIGHT_BODY * 0.18),
    (0, DEPTH / 2.0 - WALL_THICKNESS / 2.0 + METAL_INSET, HEIGHT_BODY - HEIGHT_BODY * 0.09),
    mat_metal,
    bevel=False,
))

body_parts.append(add_box(
    f"{SLUG}_latch",
    (0.10, 0.03, 0.08),
    (0, DEPTH / 2.0 + 0.015, HEIGHT_BODY - 0.04),
    mat_metal,
    bevel=False,
))

for sx in (-1, 1):
    bpy.ops.mesh.primitive_torus_add(
        major_radius=0.06,
        minor_radius=0.012,
        location=(sx * (WIDTH / 2.0 + 0.01), 0, HEIGHT_BODY * 0.55),
        rotation=(0, math.radians(90), 0),
    )
    ring = bpy.context.active_object
    ring.name = f"{SLUG}_ring_handle_{'r' if sx > 0 else 'l'}"
    ring.data.materials.append(mat_metal)
    body_parts.append(ring)

parent_keep_transform(body_parts, body_root)

# ---------------------------------------------------------------------------
# TAMPA — objeto separado, pivot (Empty) na dobradiça traseira (-Y, topo do corpo)
# Todas as peças abaixo são posicionadas em coordenadas de MUNDO como se a
# tampa estivesse FECHADA (encaixada sobre o corpo), e só then parenteadas
# ao pivot da dobradiça com keep_transform — assim nada pula de lugar.
# ---------------------------------------------------------------------------
hinge_y = -(DEPTH / 2.0 - WALL_THICKNESS / 2.0)  # borda TRASEIRA do corpo (-Y)
hinge_z = HEIGHT_BODY

lid_root = bpy.data.objects.new(f"{SLUG}_lid_pivot", None)
bpy.context.collection.objects.link(lid_root)
lid_root.location = (0, hinge_y, hinge_z)

lid_parts = []

lid_parts.append(add_box(
    f"{SLUG}_lid_panel",
    (WIDTH, DEPTH, HEIGHT_LID),
    (0, 0, HEIGHT_BODY + HEIGHT_LID / 2.0),
    mat_wood,
))

for sign, tag in [(-1, "back"), (1, "front")]:
    lid_parts.append(add_box(
        f"{SLUG}_lid_strap_{tag}",
        (WIDTH + 2 * METAL_INSET, METAL_STRAP_W, HEIGHT_LID * 0.5),
        (0, sign * (DEPTH / 2.0 - METAL_STRAP_W / 2.0), HEIGHT_BODY + HEIGHT_LID * 0.75),
        mat_metal,
        bevel=False,
    ))

for sx in (-1, 1):
    lid_parts.append(add_box(
        f"{SLUG}_lid_corner_{'r' if sx > 0 else 'l'}",
        (METAL_STRAP_W, DEPTH + 2 * METAL_INSET, HEIGHT_LID * 0.5),
        (sx * (WIDTH / 2.0 - METAL_STRAP_W / 2.0), 0, HEIGHT_BODY + HEIGHT_LID * 0.75),
        mat_metal,
        bevel=False,
    ))

parent_keep_transform(lid_parts, lid_root)
parent_keep_transform([lid_root], body_root)

# ---------------------------------------------------------------------------
# Selecionar tudo pra facilitar a visualização
# ---------------------------------------------------------------------------
bpy.ops.object.select_all(action='SELECT')

print(f"[{SLUG}] Modelo gerado. Raiz: {body_root.name} (corpo) / {lid_root.name} (pivot da tampa, na dobradiça).")
print("Confira: 1) escala com um cubo de 1m, 2) tampa fechada encostando certinho no corpo, "
      "3) NÃO aplique Ctrl+A no lid_pivot depois — isso apagaria a posição do pivot da dobradiça.")
