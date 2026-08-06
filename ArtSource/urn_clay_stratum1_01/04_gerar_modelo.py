"""
urn_clay_stratum1_01 — script procedural de modelagem (Rota A)
BABEL — Estrato I (Babilônia de Nabucodonosor, em declínio)

Como usar:
1. Abrir o Blender > aba "Scripting" > New.
2. Colar este arquivo inteiro > Run Script (Alt+P).
3. Relatório no console do sistema (Window > Toggle System Console).

Convenções: 1 unidade = 1 metro · pivô na base central · cores só de preview.

⚠ PÉ PLANO, não fundo pontiagudo. Ânfora grega tem fundo em ponta porque
encaixava em suporte; esta fica de pé sozinha no chão de uma sala.

⚠ BARRO FOSCO, NÃO ESMALTADO. O esmalte azul é linguagem monumental (parede,
coluna). A urna é objeto doméstico — barro cru poroso. A única cor de acento é
a faixa pintada desbotada.

⚠ INTEIRA, SEM CACOS. Contraintuitivo mas é canon (D9): uma urna intacta e
empoeirada, ainda no lugar onde alguém a deixou, diz "as pessoas saíram" muito
melhor do que escombro diria. A Babilônia do Estrato I está VAZIA, não destruída.

⚠ MALHA ÚNICA E SIMPLES: esta peça é candidata a virar objeto destrutível
depois (quebrar e soltar loot, padrão Hades). Esse sistema não existe hoje e
NÃO faz parte deste lote — só garantimos que trocar por uma versão fraturada
seja fácil. Não modelar cacos.

Nota de construção: o corpo é feito de troncos de cone empilhados (radius1 na
base, radius2 no topo de cada seção) em vez de um lathe/spin — é mais simples,
robusto e suficiente para o estilo estilizado de 8-12 lados.
"""

import bpy

SLUG = "urn_clay_stratum1_01"

# --- Dimensões (m) -----------------------------------------------------------
SIDES = 10            # facetamento intencional — não subdividir

FOOT_R1, FOOT_R2, FOOT_H, FOOT_Z = 0.110, 0.140, 0.10, 0.05
LOW_R1, LOW_R2, LOW_H, LOW_Z = 0.140, 0.275, 0.30, 0.25
UPP_R1, UPP_R2, UPP_H, UPP_Z = 0.275, 0.160, 0.32, 0.56
NECK_R, NECK_H, NECK_Z = 0.130, 0.12, 0.78
RIM_R, RIM_H, RIM_Z = 0.160, 0.06, 0.87

BAND_R, BAND_H, BAND_Z = 0.285, 0.08, 0.40   # faixa pintada desbotada

HANDLE_X = 0.20
HANDLE_Z = 0.62
HANDLE_SIZE = (0.06, 0.10, 0.14)

BEVEL_W = 0.015       # peça pequena e curva: chanfro mínimo
BEVEL_SEG = 1

COL_CLAY = (0.647, 0.404, 0.235, 1.0)    # #A5673C terracota
COL_SHADOW = (0.478, 0.361, 0.243, 1.0)  # #7A5C3E barro na sombra
COL_BAND = (0.000, 0.278, 0.671, 1.0)    # #0047AB azul desbotado


# --- Helpers ----------------------------------------------------------------
def reset_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for coll in (bpy.data.meshes, bpy.data.materials):
        for block in list(coll):
            if block.users == 0:
                coll.remove(block)


def make_material(name, color, roughness=0.95):
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


def add_cyl(name, radius, depth, location, material, verts=SIDES):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=verts, radius=radius, depth=depth, location=location)
    obj = bpy.context.active_object
    obj.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.data.materials.append(material)
    return obj


def add_section(name, r1, r2, depth, z, material, verts=SIDES):
    """Tronco de cone: r1 na base, r2 no topo. Empilhados formam o perfil."""
    bpy.ops.mesh.primitive_cone_add(
        vertices=verts, radius1=r1, radius2=r2, depth=depth, location=(0.0, 0.0, z))
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

mat_clay = make_material(f"{SLUG}_clay_preview", COL_CLAY, 0.95)
mat_shadow = make_material(f"{SLUG}_clay_dark_preview", COL_SHADOW, 0.95)
mat_band = make_material(f"{SLUG}_band_preview", COL_BAND, 0.9)

parts = []

# Perfil: pé plano -> barriga -> ombro -> gargalo -> borda
parts.append(add_section(f"{SLUG}_foot", FOOT_R1, FOOT_R2, FOOT_H, FOOT_Z, mat_shadow))
parts.append(add_section(f"{SLUG}_belly_lower", LOW_R1, LOW_R2, LOW_H, LOW_Z, mat_clay))
parts.append(add_section(f"{SLUG}_belly_upper", UPP_R1, UPP_R2, UPP_H, UPP_Z, mat_clay))
parts.append(add_cyl(f"{SLUG}_neck", NECK_R, NECK_H, (0.0, 0.0, NECK_Z), mat_clay))
parts.append(add_cyl(f"{SLUG}_rim", RIM_R, RIM_H, (0.0, 0.0, RIM_Z), mat_clay))

# Faixa geométrica pintada, azul desbotado
parts.append(add_cyl(f"{SLUG}_painted_band", BAND_R, BAND_H, (0.0, 0.0, BAND_Z), mat_band))

# Duas alças pequenas no ombro
for sign, tag in [(-1, "left"), (1, "right")]:
    parts.append(add_box(
        f"{SLUG}_handle_{tag}",
        HANDLE_SIZE,
        (sign * HANDLE_X, 0.0, HANDLE_Z),
        mat_clay,
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
