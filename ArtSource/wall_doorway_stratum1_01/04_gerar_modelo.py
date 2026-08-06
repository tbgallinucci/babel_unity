"""
wall_doorway_stratum1_01 — script procedural de modelagem (Rota A)
BABEL — Estrato I (Babilônia de Nabucodonosor, em declínio)

Como usar:
1. Abrir o Blender > aba "Scripting" > New.
2. Colar este arquivo inteiro > Run Script (Alt+P).
3. O relatório de validação sai no console do sistema
   (Window > Toggle System Console).

Convenções (ver Docs/Development/Art/direcao_estrato_I.md e README.md):
- 1 unidade Blender = 1 metro (igual ao Unity e ao glTF).
- EIXOS: X = largura, Y = profundidade (+Y = face voltada para a SALA),
  Z = altura (0 = chão). Se a peça sair virada 180 graus no Unity, corrija na
  RAIZ do Prefab, nunca aqui.
- Pivô na base central.
- Cores são só PREVIEW — o material final é URP/Lit, montado no Unity.

⚠ REGRA CRÍTICA (ficha técnica): o friso azul e o plinto TÊM que encostar nas
bordas X = +-2.0 na MESMA altura do módulo liso (wall_brick_stratum1_01), senão
a emenda entre os dois aparece na parede da sala.

⚠ O VÃO É RETANGULAR DE VERGA RETA — nunca arco. Arco redondo é linguagem
romana/islâmica; a Babilônia neo-babilônica é verga reta.

⚠ O vão mede 1.6 x 2.8 m por exigência de GAMEPLAY, não de estética: a cápsula
do jogador tem raio 0.4 e ele atravessa em movimento, inclusive esquivando.
NÃO estreitar para "ficar mais proporcional ao concept".
"""

import bpy

SLUG = "wall_doorway_stratum1_01"

# --- Dimensões (m) — idênticas ao módulo liso -------------------------------
WIDTH = 4.0
HEIGHT = 6.0
DEPTH = 0.6

DOOR_W = 1.6         # largura do vão
DOOR_H = 2.8         # altura do vão

PLINTH_H = 0.25
PLINTH_OUT = 0.10

FRIEZE_Z = 1.60      # MESMA altura do módulo liso — não alterar isolado
FRIEZE_H = 0.45
FRIEZE_OUT = 0.04

FRAME_T = 0.12       # espessura da moldura do vão
FRAME_OUT = 0.06     # saliência da moldura

ROSETTE_R = 0.09
ROSETTE_D = 0.04

BEVEL_W = 0.06
# 1 segmento (nao 2): mata a aresta viva de 90 graus, que e o objetivo, por~metade
# dos triangulos. Medido: com SEG=2 esta peca custava mais que o dobro. Peca
# instanciada ~20x por sala — cada triangulo aqui e multiplicado.
BEVEL_SEG = 1

COL_BRICK = (0.647, 0.404, 0.235, 1.0)   # #A5673C
COL_SHADOW = (0.478, 0.361, 0.243, 1.0)  # #7A5C3E
COL_LAPIS = (0.000, 0.278, 0.671, 1.0)   # #0047AB
COL_GOLD = (0.957, 0.769, 0.188, 1.0)    # #F4C430

# Geometria derivada
PANEL_W = (WIDTH - DOOR_W) / 2.0          # 1.2 m por lado
PANEL_CX = DOOR_W / 2.0 + PANEL_W / 2.0   # +-1.4 m
LINTEL_H = HEIGHT - DOOR_H                # 3.2 m


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


def add_cyl(name, radius, depth, location, material, verts=6, rot_x=0.0):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=verts, radius=radius, depth=depth,
        location=location, rotation=(rot_x, 0.0, 0.0))
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
mat_shadow = make_material(f"{SLUG}_mortar_preview", COL_SHADOW, 0.95)
mat_lapis = make_material(f"{SLUG}_lapis_preview", COL_LAPIS, 0.45)
mat_gold = make_material(f"{SLUG}_gold_preview", COL_GOLD, 0.55)

parts = []
body_h = HEIGHT - PLINTH_H

for sign, tag in [(-1, "left"), (1, "right")]:
    cx = sign * PANEL_CX

    # Plinto do lado — interrompido pelo vão (a passagem é rente ao chão)
    parts.append(add_box(
        f"{SLUG}_plinth_{tag}",
        (PANEL_W, DEPTH + PLINTH_OUT, PLINTH_H),
        (cx, 0.0, PLINTH_H / 2.0),
        mat_shadow,
    ))

    # Painel de tijolo
    parts.append(add_box(
        f"{SLUG}_panel_{tag}",
        (PANEL_W, DEPTH, body_h),
        (cx, 0.0, PLINTH_H + body_h / 2.0),
        mat_brick,
    ))

    # Friso azul — mesma altura do modulo liso
    parts.append(add_box(
        f"{SLUG}_frieze_{tag}",
        (PANEL_W, DEPTH + FRIEZE_OUT * 2.0, FRIEZE_H),
        (cx, 0.0, FRIEZE_Z),
        mat_lapis,
    ))

    # Batente vertical da moldura do vão
    parts.append(add_box(
        f"{SLUG}_frame_jamb_{tag}",
        (FRAME_T, DEPTH + FRAME_OUT * 2.0, DOOR_H),
        (sign * (DOOR_W / 2.0 + FRAME_T / 2.0), 0.0, DOOR_H / 2.0),
        mat_lapis,
        bevel_w=0.03,
    ))

    # Roseta no canto superior do vão
    parts.append(add_cyl(
        f"{SLUG}_rosette_{tag}",
        ROSETTE_R, ROSETTE_D,
        (sign * (DOOR_W / 2.0 + FRAME_T / 2.0),
         DEPTH / 2.0 + FRAME_OUT + ROSETTE_D / 2.0,
         DOOR_H + FRAME_T / 2.0),
        mat_gold,
        verts=6,
        rot_x=1.5707963,
    ))

# Verga — o trecho de parede ACIMA do vão
parts.append(add_box(
    f"{SLUG}_lintel",
    (DOOR_W, DEPTH, LINTEL_H),
    (0.0, 0.0, DOOR_H + LINTEL_H / 2.0),
    mat_brick,
))

# Travessa superior da moldura do vão
parts.append(add_box(
    f"{SLUG}_frame_top",
    (DOOR_W + FRAME_T * 2.0, DEPTH + FRAME_OUT * 2.0, FRAME_T),
    (0.0, 0.0, DOOR_H + FRAME_T / 2.0),
    mat_lapis,
    bevel_w=0.03,
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
