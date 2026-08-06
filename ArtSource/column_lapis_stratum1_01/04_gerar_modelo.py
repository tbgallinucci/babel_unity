"""
column_lapis_stratum1_01 — script procedural de modelagem (Rota A)
BABEL — Estrato I (Babilônia de Nabucodonosor, em declínio)

Como usar:
1. Abrir o Blender > aba "Scripting" > New.
2. Colar este arquivo inteiro > Run Script (Alt+P).
3. Relatório no console do sistema (Window > Toggle System Console).

Convenções (ver Docs/Development/Art/direcao_estrato_I.md e README.md):
- 1 unidade Blender = 1 metro. Pivô na base central. Z = altura, 0 = chão.
- Cores são só PREVIEW — material final é URP/Lit, montado no Unity.

⚠ NÃO É COLUNA GREGA. Sem caneluras, sem volutas, sem acanto. A coluna
babilônica é de tijolo, fuste liso, capitel em bloco simples. O fuste é
FACETADO (10 lados) de propósito — isso é estilo, não limitação. Não
subdividir para "ficar redondo": sobe o custo e sai do estilo estilizado.

⚠ A BANDA AZUL FICA NO TERÇO SUPERIOR, não no meio. Motivo (ficha técnica):
o friso da parede está na altura do peito (1.6 m). Se a banda da coluna
ficasse na mesma altura, a sala inteira ganharia uma listra azul horizontal
contínua e o olho pararia de ler os volumes separados.
"""

import bpy

SLUG = "column_lapis_stratum1_01"

# --- Dimensões (m) -----------------------------------------------------------
HEIGHT = 6.0          # do chão ao teto (WALL_H da grade padrão)
SHAFT_R = 0.45        # raio do fuste (Ø0.9)
SHAFT_SIDES = 10      # facetamento intencional

PLINTH_S = 1.10       # base quadrada 1.1 x 1.1
PLINTH_H = 0.30

CAPITAL_S = 1.00
CAPITAL_H = 0.40

BAND_R = 0.48         # banda azul, ligeiramente saliente
BAND_H = 0.50
BAND_Z = 4.30         # terço superior

ROSETTE_R = 0.09
ROSETTE_D = 0.04

CUNEI_W = 0.030
CUNEI_H = 2.00
CUNEI_Z = 2.50

BEVEL_W = 0.06
# 1 segmento (nao 2): mata a aresta viva de 90 graus, que e o objetivo, por~metade
# dos triangulos. Medido: com SEG=2 esta peca custava mais que o dobro. Peca
# instanciada ~20x por sala — cada triangulo aqui e multiplicado.
BEVEL_SEG = 1

COL_BRICK = (0.647, 0.404, 0.235, 1.0)   # #A5673C
COL_SHADOW = (0.478, 0.361, 0.243, 1.0)  # #7A5C3E
COL_LAPIS = (0.000, 0.278, 0.671, 1.0)   # #0047AB
COL_GOLD = (0.957, 0.769, 0.188, 1.0)    # #F4C430

SHAFT_H = HEIGHT - PLINTH_H - CAPITAL_H  # 5.30


# --- Helpers ----------------------------------------------------------------
def reset_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for coll in (bpy.data.meshes, bpy.data.materials):
        for block in list(coll):
            if block.users == 0:
                coll.remove(block)


def make_material(name, color, roughness=0.85):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = color
        bsdf.inputs["Roughness"].default_value = roughness
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


def add_cyl(name, radius, depth, location, material, verts=8, rotation=(0.0, 0.0, 0.0)):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=verts, radius=radius, depth=depth,
        location=location, rotation=rotation)
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

mat_brick = make_material(f"{SLUG}_brick_preview", COL_BRICK, 0.9)
mat_shadow = make_material(f"{SLUG}_stone_preview", COL_SHADOW, 0.95)
mat_lapis = make_material(f"{SLUG}_lapis_preview", COL_LAPIS, 0.45)
mat_gold = make_material(f"{SLUG}_gold_preview", COL_GOLD, 0.55)

parts = []

# Plinto quadrado escalonado
parts.append(add_box(
    f"{SLUG}_plinth",
    (PLINTH_S, PLINTH_S, PLINTH_H),
    (0.0, 0.0, PLINTH_H / 2.0),
    mat_shadow,
))

# Fuste facetado
parts.append(add_cyl(
    f"{SLUG}_shaft",
    SHAFT_R, SHAFT_H,
    (0.0, 0.0, PLINTH_H + SHAFT_H / 2.0),
    mat_brick,
    verts=SHAFT_SIDES,
))

# Banda de esmalte azul, terço superior
parts.append(add_cyl(
    f"{SLUG}_band_lapis",
    BAND_R, BAND_H,
    (0.0, 0.0, BAND_Z),
    mat_lapis,
    verts=SHAFT_SIDES,
))

# Inscrição cuneiforme vertical (relevo raso, um lado só)
parts.append(add_box(
    f"{SLUG}_cuneiform_strip",
    (CUNEI_W, 0.22, CUNEI_H),
    (SHAFT_R, 0.0, CUNEI_Z),
    mat_shadow,
    bevel_w=0.01,
))

# Capitel em bloco simples
parts.append(add_box(
    f"{SLUG}_capital",
    (CAPITAL_S, CAPITAL_S, CAPITAL_H),
    (0.0, 0.0, HEIGHT - CAPITAL_H / 2.0),
    mat_brick,
))

# Rosetas de ouro embaçado nas 4 faces do capitel
cap_z = HEIGHT - CAPITAL_H / 2.0
face = CAPITAL_S / 2.0 + ROSETTE_D / 2.0
for i, (dx, dy, rx, rz) in enumerate([
        (0.0, face, 1.5707963, 0.0),
        (0.0, -face, 1.5707963, 0.0),
        (face, 0.0, 1.5707963, 1.5707963),
        (-face, 0.0, 1.5707963, 1.5707963)]):
    parts.append(add_cyl(
        f"{SLUG}_rosette_{i:02d}",
        ROSETTE_R, ROSETTE_D,
        (dx, dy, cap_z),
        mat_gold,
        verts=6,
        rotation=(rx, 0.0, rz),
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
