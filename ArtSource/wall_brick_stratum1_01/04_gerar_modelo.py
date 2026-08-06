"""
wall_brick_stratum1_01 — script procedural de modelagem (Rota A)
BABEL — Estrato I (Babilônia de Nabucodonosor, em declínio)

Como usar:
1. Abrir o Blender.
2. Aba "Scripting" (topo da janela) > New.
3. Colar este arquivo inteiro.
4. Rodar (botão Play, ou Alt+P).
5. A parede aparece na viewport. O relatório de validação (dimensões + nº de
   triângulos) é impresso no console do sistema — se não estiver visível:
   Window > Toggle System Console.

Convenções do projeto (ver Docs/Development/Art/direcao_estrato_I.md e README.md):
- 1 unidade Blender = 1 metro (igual ao Unity e ao glTF).
- EIXOS NO BLENDER: X = largura, Y = profundidade (+Y = face voltada para a
  SALA), Z = altura (0 = chão). Na exportação glTF o Blender Z vira o Y do Unity.
- Pivô na base central: X/Y centrados, Z = 0 na base.
- Cores aqui são só PREVIEW — o material final é um URP/Lit .mat montado no
  Unity (ver Docs/Development/Art/README.md).

Medidas da grade padrão do projeto (direcao_estrato_I.md §3) — NÃO ajustar:
  largura 4.0 m (CELL) x altura 6.0 m (WALL_H) x profundidade 0.6 m.

Regra crítica da ficha técnica: o friso azul e o plinto TÊM que encostar nas
duas bordas laterais (X = +-2.0) na mesma altura, senão os 5 módulos de uma
parede de sala não emendam e aparece um degrau a cada 4 m.
"""

import bpy

SLUG = "wall_brick_stratum1_01"

# --- Dimensões (m) -----------------------------------------------------------
WIDTH = 4.0          # X — travado por room_kit.gd: CELL
HEIGHT = 6.0         # Z — travado por room_kit.gd: WALL_H
DEPTH = 0.6          # Y

PLINTH_H = 0.25      # altura do plinto da base
PLINTH_OUT = 0.10    # o quanto o plinto avança para fora da face da parede

FRIEZE_Z = 1.60      # altura do centro do friso (altura do peito)
FRIEZE_H = 0.45      # altura do friso
FRIEZE_OUT = 0.04    # saliência do friso

ROSETTE_COUNT = 5
ROSETTE_R = 0.09
ROSETTE_D = 0.04

# Painel de inscrição: pedra clara com MOLDURA de tijolo azul M2 em volta.
# A escrita fica na pedra clara, não no azul — decisão do Gustavo (2026-08-04).
# Motivo de mundo: tabuinha e estela cuneiforme eram argila/calcário; o esmalte
# azul do Portão de Ishtar carregava FIGURA (leão, mušḫuššu, touro, roseta), não
# texto corrido. Motivo de leitura: barro e azul são ambos escuros e brigam entre
# si numa sala de tocha — a pedra clara entra como terceiro valor e é o que faz a
# inscrição existir a distância.
CUNEI_Z = 0.80       # centro do painel (terço inferior)
CUNEI_W = 3.20
CUNEI_H = 0.80
CUNEI_D = 0.02       # relevo raso da pedra, à frente da moldura

# Moldura azul. FRAME_B é a faixa de azul visível em volta da pedra nos 4 lados.
# Com CUNEI_Z/H atuais a moldura ocupa Z 0.30..1.30 — folga de 0.05 para o topo
# do plinto (0.25) e de 0.075 para a base do friso (1.375). Mexer em CUNEI_Z,
# CUNEI_H ou FRAME_B sem refazer essa conta encosta a moldura no friso.
FRAME_B = 0.10
FRAME_D = 0.02       # saliência da moldura

BEVEL_W = 0.06
# 1 segmento (nao 2): mata a aresta viva de 90 graus, que e o objetivo, por~metade
# dos triangulos. Medido: com SEG=2 esta peca custava mais que o dobro. Peca
# instanciada ~20x por sala — cada triangulo aqui e multiplicado.
BEVEL_SEG = 1

# --- Cores de preview (paleta do direcao_estrato_I.md §1) --------------------
COL_BRICK = (0.647, 0.404, 0.235, 1.0)   # #A5673C tijolo cozido
COL_SHADOW = (0.478, 0.361, 0.243, 1.0)  # #7A5C3E barro na sombra
COL_LAPIS = (0.000, 0.278, 0.671, 1.0)   # #0047AB azul Ishtar
COL_GOLD = (0.957, 0.769, 0.188, 1.0)    # #F4C430 ouro açafrão embaçado
COL_STONE = (0.757, 0.718, 0.651, 1.0)   # #C1B7A6 pedra clara da inscrição


# --- Helpers -----------------------------------------------------------------
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
    """Caixa de dimensões `size` (m) centrada em `location` (mundo)."""
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


def report(slug):
    """Relatório de validação — confere a ficha técnica sem sair do Blender."""
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
    print(f"  pivo na base   : {'OK' if abs(lo[2]) < 1e-4 else 'ERRO — Z minimo deve ser 0'}")
    print("=" * 62)


# --- Construção --------------------------------------------------------------
reset_scene()

mat_brick = make_material(f"{SLUG}_brick_preview", COL_BRICK, 0.9)
mat_shadow = make_material(f"{SLUG}_mortar_preview", COL_SHADOW, 0.95)
mat_lapis = make_material(f"{SLUG}_lapis_preview", COL_LAPIS, 0.45)
mat_gold = make_material(f"{SLUG}_gold_preview", COL_GOLD, 0.55)
mat_stone = make_material(f"{SLUG}_stone_preview", COL_STONE, 0.80)

parts = []

# Plinto da base — encosta em X = +-2.0 (regra de emenda)
parts.append(add_box(
    f"{SLUG}_plinth",
    (WIDTH, DEPTH + PLINTH_OUT, PLINTH_H),
    (0.0, 0.0, PLINTH_H / 2.0),
    mat_shadow,
))

# Corpo de tijolo — do topo do plinto até 6.0 m
body_h = HEIGHT - PLINTH_H
parts.append(add_box(
    f"{SLUG}_body",
    (WIDTH, DEPTH, body_h),
    (0.0, 0.0, PLINTH_H + body_h / 2.0),
    mat_brick,
))

# Friso de tijolo esmaltado azul — contínuo de borda a borda
parts.append(add_box(
    f"{SLUG}_frieze",
    (WIDTH, DEPTH + FRIEZE_OUT * 2.0, FRIEZE_H),
    (0.0, 0.0, FRIEZE_Z),
    mat_lapis,
))

# Rosetas de ouro embaçado sobre o friso (face da sala, +Y)
rosette_y = DEPTH / 2.0 + FRIEZE_OUT + ROSETTE_D / 2.0
step = WIDTH / ROSETTE_COUNT
for i in range(ROSETTE_COUNT):
    x = -WIDTH / 2.0 + step * (i + 0.5)
    parts.append(add_cyl(
        f"{SLUG}_rosette_{i:02d}",
        ROSETTE_R, ROSETTE_D,
        (x, rosette_y, FRIEZE_Z),
        mat_gold,
        verts=6,
        rot_x=1.5707963,  # deitada, encarando +Y
    ))

# Moldura de tijolo azul esmaltado (M2) — o adereço que emoldura a inscrição
parts.append(add_box(
    f"{SLUG}_inscription_frame",
    (CUNEI_W + FRAME_B * 2.0, DEPTH + FRAME_D * 2.0, CUNEI_H + FRAME_B * 2.0),
    (0.0, 0.0, CUNEI_Z),
    mat_lapis,
    bevel_w=0.02,
))

# Painel de inscrição cuneiforme em pedra clara — sai à frente da moldura, para
# a faixa azul aparecer como borda nos 4 lados.
# Textura: Assets/Art/Textures/inscription_stone_01.png (1024x256), recortada do
# concept cuneiforme_pedra_clara_B.png e clareada por script para #C1B7A6.
parts.append(add_box(
    f"{SLUG}_cuneiform_panel",
    (CUNEI_W, DEPTH + (FRAME_D + CUNEI_D) * 2.0, CUNEI_H),
    (0.0, 0.0, CUNEI_Z),
    mat_stone,
    bevel_w=0.02,
))

# Agrupa tudo sob um Empty na origem (pivô da peça)
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
