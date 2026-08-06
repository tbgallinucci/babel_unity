"""
torch_wall_stratum1_01 — script procedural de modelagem (Rota A)
BABEL — Estrato I (Babilônia de Nabucodonosor, em declínio)

Como usar:
1. Abrir o Blender > aba "Scripting" > New.
2. Colar este arquivo inteiro > Run Script (Alt+P).
3. Relatório no console do sistema (Window > Toggle System Console).

⚠ ESTE É O ÚNICO ASSET DO LOTE COM PIVÔ FORA DA BASE.
Pivô = CENTRO DA FACE DE MONTAGEM (a face que encosta na parede):
  X = 0 centrado, Z = 0 no centro vertical da placa, Y = 0 no plano da parede.
Toda a geometria fica em Y NEGATIVO (para fora da parede) e a chama em Z+.

Motivo: a tocha é posicionada encostando numa superfície vertical. Com pivô na
base, cada instância exigiria compensar altura E profundidade à mão. Com pivô
na face de montagem, basta encostar na parede e escolher a altura — e ela vai
ser instanciada MUITAS vezes por andar.

⚠ A LUZ NÃO ESTÁ AQUI. O .glb é só geometria. O componente Light (Type = Point)
e o material emissivo da chama são montados no PREFAB do Unity — ver a ficha
técnica, seção "a luz faz parte do prefab". Sem isso, este asset é um enfeite
escuro e a sala continua uma caixa cinza.
"""

import bpy
import math

SLUG = "torch_wall_stratum1_01"

# --- Dimensões (m) -----------------------------------------------------------
PLATE_W = 0.30        # largura da placa de montagem
PLATE_H = 0.25        # altura da placa
PLATE_T = 0.04        # espessura da placa

ARM_T = 0.06          # seção do braço
ARM_L = 0.30          # comprimento do braço
ARM_TILT = -0.5236    # -30 graus: inclina o braço para cima ao sair da parede

OUT_Y = -0.33         # afastamento da bacia em relação à parede (Y negativo)

BOWL_R = 0.09
BOWL_H = 0.07
BOWL_Z = 0.24

HAFT_R = 0.035
HAFT_H = 0.22
HAFT_Z = 0.36

FLAME_R = 0.09
FLAME_H = 0.32
FLAME_Z = 0.62

ROSETTE_R = 0.06
ROSETTE_D = 0.03

BEVEL_W = 0.02        # peça pequena: chanfro mínimo, senão come a forma
BEVEL_SEG = 1

COL_GOLD = (0.957, 0.769, 0.188, 1.0)   # #F4C430 ouro açafrão embaçado
COL_WOOD = (0.243, 0.180, 0.133, 1.0)   # #3E2E22 madeira envelhecida
COL_FLAME = (1.000, 0.604, 0.235, 1.0)  # #FF9A3C laranja de chama


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


def add_box(name, size, location, material, rotation=(0.0, 0.0, 0.0), bevel_w=BEVEL_W):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location, rotation=rotation)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
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


def add_cone(name, r1, r2, depth, location, material, verts=8):
    bpy.ops.mesh.primitive_cone_add(
        vertices=verts, radius1=r1, radius2=r2, depth=depth, location=location)
    obj = bpy.context.active_object
    obj.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.data.materials.append(material)
    return obj


def report(slug, expect_base_pivot=True, expect_mount_pivot=False):
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
    print(f"  projecao Y     : {hi[1]-lo[1]:.3f} m   [{lo[1]:+.3f} .. {hi[1]:+.3f}]")
    print(f"  altura Z       : {hi[2]-lo[2]:.3f} m   [{lo[2]:+.3f} .. {hi[2]:+.3f}]")
    if expect_base_pivot:
        print(f"  pivo na base   : {'OK' if abs(lo[2]) < 1e-4 else 'ERRO — Z minimo deve ser 0'}")
    if expect_mount_pivot:
        ok_y = abs(hi[1]) < 1e-4          # nada pode passar da parede (Y > 0)
        ok_out = lo[1] < -0.01            # tem que projetar para fora (Y < 0)
        print(f"  face montagem  : {'OK' if (ok_y and ok_out) else 'ERRO — geometria deve ficar toda em Y <= 0'}")
    print("=" * 62)


# --- Construção --------------------------------------------------------------
reset_scene()

mat_gold = make_material(f"{SLUG}_gold_preview", COL_GOLD, 0.55)
mat_wood = make_material(f"{SLUG}_wood_preview", COL_WOOD, 0.9)
mat_flame = make_material(f"{SLUG}_flame_preview", COL_FLAME, 0.4, emission=True)

parts = []

# Placa de montagem — face traseira exatamente em Y = 0 (o plano da parede)
parts.append(add_box(
    f"{SLUG}_mount_plate",
    (PLATE_W, PLATE_T, PLATE_H),
    (0.0, -PLATE_T / 2.0, 0.0),
    mat_gold,
))

# Roseta rasa na placa
parts.append(add_cyl(
    f"{SLUG}_rosette",
    ROSETTE_R, ROSETTE_D,
    (0.0, -PLATE_T - ROSETTE_D / 2.0, 0.0),
    mat_gold,
    verts=6,
    rotation=(1.5707963, 0.0, 0.0),
))

# Braço inclinado: sai da parede (-Y) subindo (+Z)
arm_cy = -(PLATE_T + (ARM_L / 2.0) * math.cos(ARM_TILT))
arm_cz = (ARM_L / 2.0) * abs(math.sin(ARM_TILT))
parts.append(add_box(
    f"{SLUG}_arm",
    (ARM_T, ARM_L, ARM_T),
    (0.0, arm_cy, arm_cz),
    mat_gold,
    rotation=(ARM_TILT, 0.0, 0.0),
))

# Bacia rasa na ponta do braço
parts.append(add_cyl(
    f"{SLUG}_bowl",
    BOWL_R, BOWL_H,
    (0.0, OUT_Y, BOWL_Z),
    mat_gold,
    verts=8,
))

# Cabo de madeira envelhecida
parts.append(add_cyl(
    f"{SLUG}_haft",
    HAFT_R, HAFT_H,
    (0.0, OUT_Y, HAFT_Z),
    mat_wood,
    verts=6,
))

# Chama — material EMISSIVO (no Unity: shader Lit com Emission ligada)
parts.append(add_cone(
    f"{SLUG}_flame",
    FLAME_R, 0.0, FLAME_H,
    (0.0, OUT_Y, FLAME_Z),
    mat_flame,
    verts=8,
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

report(SLUG, expect_base_pivot=False, expect_mount_pivot=True)
