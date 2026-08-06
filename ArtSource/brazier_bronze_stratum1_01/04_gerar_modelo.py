"""
brazier_bronze_stratum1_01 — script procedural de modelagem (Rota A)
BABEL — Estrato I (Babilônia de Nabucodonosor, em declínio)

Como usar:
1. Abrir o Blender > aba "Scripting" > New.
2. Colar este arquivo inteiro > Run Script (Alt+P).
3. Relatório no console do sistema (Window > Toggle System Console).

Convenções: 1 unidade = 1 metro · pivô na base central · cores só de preview.

⚠ ALTURA ~1.1 m É DECISÃO DE GAMEPLAY, não estética. O jogador tem ~1.9 m, então
o braseiro fica na CINTURA — abaixo da linha de visão. Assim ele não bloqueia a
câmera de terceira pessoa nem esconde o telegraph de ataque desenhado no chão.
Não deixar mais alto "para ficar imponente".

⚠ BACIA RASA E LARGA, não caldeirão fundo. Caldeirão lê como cozinha/bruxaria;
bacia rasa lê como fogo ritual.

⚠ A BASE QUADRADA ESCALONADA é o que amarra o kit: ela rima com o plinto da
parede e o da coluna. É o detalhe que faz as três peças parecerem do mesmo kit.

⚠ A LUZ NÃO ESTÁ AQUI — componente Light (Type = Point) e material emissivo das
brasas são montados no PREFAB do Unity. Ver ficha técnica. Alcance maior que o
da tocha (10-14 m): este é o que ilumina o miolo da sala.
"""

import bpy
import math

SLUG = "brazier_bronze_stratum1_01"

# --- Dimensões (m) -----------------------------------------------------------
BASE_S = 0.60         # base quadrada
BASE_H = 0.15

LEG_R = 0.045
LEG_H = 0.45
LEG_RADIUS = 0.16     # distância do centro
LEG_TILT = -0.20      # ~11 graus: topo inclina para dentro, pé abre para fora
LEG_COUNT = 3

BOWL_R = 0.40
BOWL_H = 0.32
BOWL_Z = 0.76         # centro da bacia

RIM_R = 0.42
RIM_H = 0.14
RIM_Z = 0.96

EMBER_R = 0.34
EMBER_H = 0.06
EMBER_Z = 0.94

FIRE_R = 0.30
FIRE_H = 0.35
FIRE_Z = 1.14

BEVEL_W = 0.04
BEVEL_SEG = 1

# --- VARIANTE DE MATERIAL (decisão do Gustavo, 2026-08-04) -------------------
# O braseiro tem DUAS leituras, e é o mesmo objeto contando a progressão da torre:
#   "barro" → andares baixos / Estrato I   — bacia de cerâmica fosca, sem metal
#   "ouro"  → andares superiores           — bacia de M3 (metal açafrão embaçado)
#
# ⚠️ O Estrato I usa "barro". O `03_concept.png` aprovado é a versão de barro.
# Trocar esta constante é a única coisa necessária para gerar a versão de cima —
# o modelo é paramétrico, então a variante custa um export, não uma peça nova.
#
# A PEDRA DA BASE E AS PERNAS NÃO MUDAM entre as variantes: o que sobe de status
# é a bacia, que é a parte cerimonial. Base e pernas são estrutura.
VARIANT = "barro"

COL_GOLD = (0.957, 0.769, 0.188, 1.0)    # #F4C430 ouro açafrão embaçado (M3)
COL_CLAY = (0.647, 0.404, 0.235, 1.0)    # #A5673C cerâmica/terracota (M4)
COL_STONE = (0.478, 0.361, 0.243, 1.0)   # #7A5C3E pedra da base
COL_EMBER = (1.000, 0.478, 0.110, 1.0)   # #FF7A1C brasa


# --- Helpers ----------------------------------------------------------------
def reset_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for coll in (bpy.data.meshes, bpy.data.materials):
        for block in list(coll):
            if block.users == 0:
                coll.remove(block)


def make_material(name, color, roughness=0.85, emission=False):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = color
        bsdf.inputs["Roughness"].default_value = roughness
        if emission and "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = color
            if "Emission Strength" in bsdf.inputs:
                bsdf.inputs["Emission Strength"].default_value = 3.0
    return mat


def add_box(name, size, location, material, bevel_w=BEVEL_W):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    if bevel_w > 0.0:
        mod = obj.modifiers.new(name="Bevel", type='BEVEL')
        mod.width = bevel_w
        mod.segments = BEVEL_SEG
        mod.limit_method = 'ANGLE'
    return obj


def add_cyl(name, radius, depth, location, material, verts=12, rotation=(0.0, 0.0, 0.0)):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=verts, radius=radius, depth=depth,
        location=location, rotation=rotation)
    obj = bpy.context.active_object
    obj.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.data.materials.append(material)
    return obj


def add_cone(name, r1, r2, depth, location, material, verts=10):
    bpy.ops.mesh.primitive_cone_add(
        vertices=verts, radius1=r1, radius2=r2, depth=depth, location=location)
    obj = bpy.context.active_object
    obj.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.data.materials.append(material)
    return obj


def report(slug, expect_base_pivot=True):
    deps = bpy.context.evaluated_depsgraph_get()
    tris = 0
    lo = [1e9, 1e9, 1e9]
    hi = [-1e9, -1e9, -1e9]
    meshes = 0
    for ob in bpy.data.objects:
        if ob.type != 'MESH':
            continue
        meshes += 1
        ev = ob.evaluated_get(deps)
        me = ev.to_mesh()
        me.calc_loop_triangles()
        tris += len(me.loop_triangles)
        for v in me.vertices:
            w = ev.matrix_world @ v.co
            for i in range(3):
                lo[i] = min(lo[i], w[i])
                hi[i] = max(hi[i], w[i])
        ev.to_mesh_clear()
    print("")
    print("=" * 62)
    print(f"  {slug}")
    print("=" * 62)
    print(f"  objetos (mesh) : {meshes}")
    print(f"  triangulos     : {tris}")
    print(f"  largura X      : {hi[0]-lo[0]:.3f} m   [{lo[0]:+.3f} .. {hi[0]:+.3f}]")
    print(f"  profundidade Y : {hi[1]-lo[1]:.3f} m   [{lo[1]:+.3f} .. {hi[1]:+.3f}]")
    print(f"  altura Z       : {hi[2]-lo[2]:.3f} m   [{lo[2]:+.3f} .. {hi[2]:+.3f}]")
    if expect_base_pivot:
        print(f"  pivo na base   : {'OK' if abs(lo[2]) < 1e-4 else 'ERRO — Z minimo deve ser 0'}")
    print("=" * 62)


# --- Construção --------------------------------------------------------------
reset_scene()

if VARIANT not in ("barro", "ouro"):
    raise ValueError(f"VARIANT deve ser 'barro' ou 'ouro', nao {VARIANT!r}")

# A bacia e o aro seguem a variante; base e pernas nao.
if VARIANT == "ouro":
    mat_bowl = make_material(f"{SLUG}_gold_preview", COL_GOLD, 0.55)
else:
    mat_bowl = make_material(f"{SLUG}_clay_preview", COL_CLAY, 0.90)

mat_gold = make_material(f"{SLUG}_legs_preview", COL_GOLD, 0.55)
mat_stone = make_material(f"{SLUG}_stone_preview", COL_STONE, 0.95)
mat_ember = make_material(f"{SLUG}_ember_preview", COL_EMBER, 0.4, emission=True)

parts = []

# Base de pedra quadrada escalonada — rima com o plinto da parede e da coluna
parts.append(add_box(
    f"{SLUG}_base",
    (BASE_S, BASE_S, BASE_H),
    (0.0, 0.0, BASE_H / 2.0),
    mat_stone,
))

# Três pernas abertas
for i in range(LEG_COUNT):
    phi = (2.0 * math.pi / LEG_COUNT) * i
    parts.append(add_cyl(
        f"{SLUG}_leg_{i:02d}",
        LEG_R, LEG_H,
        (LEG_RADIUS * math.cos(phi), LEG_RADIUS * math.sin(phi), BASE_H + LEG_H / 2.0),
        mat_gold,
        verts=6,
        rotation=(0.0, LEG_TILT, phi),
    ))

# Bacia rasa e larga
parts.append(add_cyl(
    f"{SLUG}_bowl",
    BOWL_R, BOWL_H,
    (0.0, 0.0, BOWL_Z),
    mat_bowl,
    verts=12,
))

# Borda com faixa de rosetas em relevo (aro saliente)
parts.append(add_cyl(
    f"{SLUG}_rim",
    RIM_R, RIM_H,
    (0.0, 0.0, RIM_Z),
    mat_bowl,
    verts=12,
))

# Brasas — material EMISSIVO
parts.append(add_cyl(
    f"{SLUG}_embers",
    EMBER_R, EMBER_H,
    (0.0, 0.0, EMBER_Z),
    mat_ember,
    verts=10,
))

# Fogo baixo
parts.append(add_cone(
    f"{SLUG}_fire",
    FIRE_R, 0.0, FIRE_H,
    (0.0, 0.0, FIRE_Z),
    mat_ember,
    verts=10,
))

root = bpy.data.objects.new(f"{SLUG}_root", None)
bpy.context.collection.objects.link(root)
root.location = (0.0, 0.0, 0.0)

bpy.ops.object.select_all(action='DESELECT')
for p in parts:
    p.select_set(True)
root.select_set(True)
bpy.context.view_layer.objects.active = root
bpy.ops.object.parent_set(type='OBJECT', keep_transform=True)

report(SLUG)
