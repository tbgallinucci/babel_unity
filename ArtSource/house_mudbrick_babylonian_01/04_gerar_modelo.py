"""
house_mudbrick_babylonian_01 - gerador procedural (Rota A)
Escala: 1 unidade Blender = 1 metro. Pivot final = base central (0,0,0).
Gera TRES objetos/hierarquias:
  - casa_corpo       : paredes do terreo, janela, escada (mesh unico, sempre visivel)
  - porta_pivot       : Empty na dobradica (aresta vertical esquerda do vao),
                        filho de casa_corpo, com "porta_madeira" dentro. Objeto
                        SEPARADO de proposito - nao e fundido na parede - para
                        permitir abrir/fechar a porta depois. A ROTACAO
                        animada de abrir/fechar (ao interagir) NAO e feita
                        aqui - isso e comportamento de jogo (AnimationPlayer +
                        script de interacao), montado no godot-asset-integrator.
                        Aqui so garantimos que o pivot esta no lugar certo.
  - telhado          : laje do terraco + parapeito + vao de acesso da escada
                        (objeto separado de proposito - script de gameplay
                        futuro vai esconder este objeto quando o personagem
                        entrar na casa)
"""

import bpy
import math

# ----------------------------------------------------------------------
# Utilidades
# ----------------------------------------------------------------------

def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for block in list(bpy.data.meshes):
        if block.users == 0:
            bpy.data.meshes.remove(block)


def make_material(name, color, roughness=0.9):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (*color, 1.0)
        bsdf.inputs["Roughness"].default_value = roughness
    return mat


def add_box(name, size, location, material=None, bevel=0.08):
    """Cria uma caixa nas dimensoes `size` (m), centrada em `location`.

    primitive_cube_add(size=1.0) ja cria um cubo com 1m de aresta (o
    parametro `size` E o comprimento da aresta, nao um raio) - a escala
    aplicada aqui deve ser `size` DIRETO, nao `size/2` (bug ja visto no
    script do bau chest_wood_stratum1_01: dividir por 2 deixa toda peca
    com metade do tamanho pretendido e abre gaps entre elas)."""
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = (size[0], size[1], size[2])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0:
        mod = obj.modifiers.new("Bevel", 'BEVEL')
        mod.width = bevel
        mod.segments = 2
        mod.limit_method = 'ANGLE'
        mod.angle_limit = math.radians(35)
    if material:
        obj.data.materials.append(material)
    return obj


def cut_hole(obj, cutter_size, cutter_location):
    """Corta um vao retangular em obj via boolean DIFFERENCE (aplica e remove o cutter).

    Mesmo cuidado do add_box: o cubo base ja tem 1m de aresta, entao a
    escala e `cutter_size` direto, nao `cutter_size/2`."""
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=cutter_location)
    cutter = bpy.context.active_object
    cutter.scale = (cutter_size[0], cutter_size[1], cutter_size[2])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    bpy.context.view_layer.objects.active = obj
    mod = obj.modifiers.new("Cut", 'BOOLEAN')
    mod.operation = 'DIFFERENCE'
    mod.object = cutter
    bpy.ops.object.modifier_apply(modifier=mod.name)

    bpy.data.objects.remove(cutter, do_unlink=True)


def join_objects(objects, final_name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[-1]
    bpy.ops.object.join()
    joined = bpy.context.active_object
    joined.name = final_name
    return joined


def parent_keep_transform(children, parent_obj):
    """Parenteia sem mover nada (equivalente a Ctrl+P > Keep Transform)."""
    bpy.ops.object.select_all(action='DESELECT')
    for child in children:
        child.select_set(True)
    parent_obj.select_set(True)
    bpy.context.view_layer.objects.active = parent_obj
    bpy.ops.object.parent_set(type='OBJECT', keep_transform=True)


def set_origin_world(obj, world_point=(0.0, 0.0, 0.0)):
    cursor = bpy.context.scene.cursor
    old_cursor_loc = cursor.location.copy()
    cursor.location = world_point
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR', center='MEDIAN')
    cursor.location = old_cursor_loc


# ----------------------------------------------------------------------
# Setup
# ----------------------------------------------------------------------

clear_scene()

mat_barro = make_material("mat_barro_casa", (0.66, 0.53, 0.36))
mat_barro_escuro = make_material("mat_barro_parapeito", (0.58, 0.45, 0.30))
mat_madeira = make_material("mat_madeira_porta", (0.16, 0.11, 0.07), roughness=0.7)

WIDTH = 6.0     # eixo X
DEPTH = 6.0     # eixo Y
WALL_H = 3.0    # altura do pavimento terreo
WALL_T = 0.3    # espessura das paredes

# ----------------------------------------------------------------------
# CASA_CORPO - pavimento terreo
# ----------------------------------------------------------------------

corpo_parts = []

front_y = -DEPTH / 2 + WALL_T / 2
back_y = DEPTH / 2 - WALL_T / 2

# Parede de fundo e laterais (sem vaos)
corpo_parts.append(add_box("parede_fundo", (WIDTH, WALL_T, WALL_H), (0, back_y, WALL_H / 2), mat_barro))
corpo_parts.append(add_box("parede_esquerda", (WALL_T, DEPTH, WALL_H), (-WIDTH / 2 + WALL_T / 2, 0, WALL_H / 2), mat_barro))

# Parede direita recebe o vao da janela (via boolean)
parede_direita = add_box("parede_direita", (WALL_T, DEPTH, WALL_H), (WIDTH / 2 - WALL_T / 2, 0, WALL_H / 2), mat_barro, bevel=0.0)
WIN_SIZE = 0.6
WIN_Y = 0.5
WIN_Z0 = 1.3
cut_hole(parede_direita, (WALL_T + 0.1, WIN_SIZE, WIN_SIZE), (WIDTH / 2 - WALL_T / 2, WIN_Y, WIN_Z0 + WIN_SIZE / 2))
bevel_mod = parede_direita.modifiers.new("Bevel", 'BEVEL')
bevel_mod.width = 0.06
bevel_mod.segments = 2
bevel_mod.limit_method = 'ANGLE'
bevel_mod.angle_limit = math.radians(35)
corpo_parts.append(parede_direita)

# Parede frontal com vao de porta (dois segmentos solidos + verga)
DOOR_W = 1.1
DOOR_H = 2.2
door_x = -1.3

seg_left_w = WIDTH / 2 + (door_x - DOOR_W / 2)
seg_left_center_x = -WIDTH / 2 + seg_left_w / 2
corpo_parts.append(add_box("parede_frontal_esq", (seg_left_w, WALL_T, WALL_H), (seg_left_center_x, front_y, WALL_H / 2), mat_barro))

seg_right_start = door_x + DOOR_W / 2
seg_right_w = WIDTH / 2 - seg_right_start
seg_right_center_x = seg_right_start + seg_right_w / 2
corpo_parts.append(add_box("parede_frontal_dir", (seg_right_w, WALL_T, WALL_H), (seg_right_center_x, front_y, WALL_H / 2), mat_barro))

# Verga acima da porta
corpo_parts.append(add_box("verga_porta", (DOOR_W, WALL_T, WALL_H - DOOR_H), (door_x, front_y, DOOR_H + (WALL_H - DOOR_H) / 2), mat_barro))

# Soleira de pedra na entrada
corpo_parts.append(add_box("soleira", (DOOR_W + 0.3, 0.5, 0.1), (door_x, front_y - 0.35, 0.05), mat_barro_escuro, bevel=0.02))

# Vigas de beiral (trim decorativo no topo das paredes, protuberante)
BEAM_COUNT = 7
for i in range(BEAM_COUNT):
    bx = -WIDTH / 2 + 0.6 + i * ((WIDTH - 1.2) / (BEAM_COUNT - 1))
    corpo_parts.append(
        add_box(f"viga_beiral_{i:02d}", (0.15, 0.5, 0.15), (bx, front_y - 0.15, WALL_H - 0.1), mat_madeira, bevel=0.02)
    )

# ----------------------------------------------------------------------
# Escada externa (blocos solidos empilhados, encostada na parede direita)
# ----------------------------------------------------------------------

STEP_COUNT = 12
step_rise = WALL_H / STEP_COUNT
step_run = 3.6 / STEP_COUNT
step_width = 1.1
start_y = -2.6
stair_x = WIDTH / 2 + step_width / 2

for i in range(STEP_COUNT):
    step_h = (i + 1) * step_rise
    step_y = start_y + i * step_run + step_run / 2
    corpo_parts.append(
        add_box(f"degrau_{i:02d}", (step_width, step_run, step_h), (stair_x, step_y, step_h / 2), mat_barro_escuro, bevel=0.02)
    )

# Junta tudo em casa_corpo, origem na base central (0,0,0)
casa_corpo = join_objects(corpo_parts, "casa_corpo")
set_origin_world(casa_corpo, (0.0, 0.0, 0.0))

# ----------------------------------------------------------------------
# PORTA - objeto SEPARADO com pivot na dobradica (aresta vertical esquerda
# do vao), para permitir abrir/fechar depois (mesmo padrao da tampa do bau).
# A porta e desenhada em coordenadas de MUNDO como se estivesse FECHADA
# (encaixada no vao) e so DEPOIS e parenteada ao pivot com keep_transform,
# assim nada pula de lugar.
# ----------------------------------------------------------------------

hinge_x = door_x - DOOR_W / 2  # aresta esquerda do vao (eixo de rotacao vertical)
hinge_z = 0.0                  # dobradica vai do chao ate o topo da porta

porta_pivot = bpy.data.objects.new("porta_pivot", None)
bpy.context.collection.objects.link(porta_pivot)
porta_pivot.location = (hinge_x, front_y, hinge_z)

porta_madeira = add_box(
    "porta_madeira",
    (DOOR_W - 0.1, 0.08, DOOR_H - 0.1),
    (door_x, front_y + 0.03, (DOOR_H - 0.1) / 2),
    mat_madeira,
    bevel=0.02,
)

parent_keep_transform([porta_madeira], porta_pivot)
parent_keep_transform([porta_pivot], casa_corpo)

# ----------------------------------------------------------------------
# TELHADO - terraco superior (objeto SEPARADO de proposito, ver ficha tecnica)
# ----------------------------------------------------------------------

telhado_parts = []

ROOF_T = 0.3
roof_z0 = WALL_H
roof_z1 = roof_z0 + ROOF_T

telhado_parts.append(
    add_box("laje_terraco", (WIDTH + 0.6, DEPTH + 0.6, ROOF_T), (0, 0, roof_z0 + ROOF_T / 2), mat_barro, bevel=0.04)
)

PARA_H = 0.9
PARA_T = 0.2
para_z_center = roof_z1 + PARA_H / 2

# Parapeito - 3 lados solidos + lado direito com vao de acesso da escada
telhado_parts.append(add_box("parapeito_fundo", (WIDTH, PARA_T, PARA_H), (0, DEPTH / 2 - PARA_T / 2, para_z_center), mat_barro_escuro))
telhado_parts.append(add_box("parapeito_frontal", (WIDTH, PARA_T, PARA_H), (0, -DEPTH / 2 + PARA_T / 2, para_z_center), mat_barro_escuro))
telhado_parts.append(add_box("parapeito_esquerdo", (PARA_T, DEPTH, PARA_H), (-WIDTH / 2 + PARA_T / 2, 0, para_z_center), mat_barro_escuro))

# Lado direito (onde a escada chega) com vao de acesso de 1.2m
ACCESS_W = 1.2
seg_w = (DEPTH - ACCESS_W) / 2
telhado_parts.append(add_box("parapeito_direito_a", (PARA_T, seg_w, PARA_H), (WIDTH / 2 - PARA_T / 2, -DEPTH / 2 + seg_w / 2, para_z_center), mat_barro_escuro))
telhado_parts.append(add_box("parapeito_direito_b", (PARA_T, seg_w, PARA_H), (WIDTH / 2 - PARA_T / 2, DEPTH / 2 - seg_w / 2, para_z_center), mat_barro_escuro))

telhado = join_objects(telhado_parts, "telhado")
set_origin_world(telhado, (0.0, 0.0, 0.0))

# ----------------------------------------------------------------------
print("Gerado: casa_corpo + porta_pivot (com porta_madeira, articulada na dobradica) + telhado")
print("Dimensoes totais aprox: %.1fm x %.1fm x %.1fm (LxPxA)" % (WIDTH, DEPTH, roof_z1 + PARA_H))
print("Confira: 1) escala com um cubo de 1m, 2) porta fechada encostando certinho no vao, "
      "3) NAO aplique Ctrl+A no porta_pivot depois - isso apagaria a posicao do pivot da dobradica.")
