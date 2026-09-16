import bpy
import math
import os
from mathutils import Vector


# -----------------------------------------------------------------------------
# Legendary Azure Longsword -- procedural production starter asset
# Units are meters. Z is up; the sword faces -Y.
# -----------------------------------------------------------------------------

OUT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "assets", "models"))
BLEND_PATH = os.path.join(OUT_DIR, "GEO-legendary_azure_sword_v3.blend")
GLB_PATH = os.path.join(OUT_DIR, "GEO-legendary_azure_sword_v3.glb")
PREVIEW_PATH = os.path.join(OUT_DIR, "GEO-legendary_azure_sword_v3_preview.png")


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.materials, bpy.data.meshes, bpy.data.curves, bpy.data.cameras, bpy.data.lights):
        for item in block:
            if item.users == 0:
                block.remove(item)


def mat_pbr(name, color, metallic=0.0, roughness=0.5, emission=None, strength=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = strength
    return mat


def add_forged_microdetail(material, scale=38.0, strength=0.09):
    """Subtle procedural bump for close-up Blender renders; base PBR survives GLB export."""
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    tex = nodes.new("ShaderNodeTexNoise")
    tex.inputs["Scale"].default_value = scale
    tex.inputs["Detail"].default_value = 3.0
    tex.inputs["Roughness"].default_value = 0.68
    bump = nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = strength
    bump.inputs["Distance"].default_value = 0.035
    bsdf = nodes.get("Principled BSDF")
    links.new(tex.outputs["Fac"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])


def add_bevel(obj, width=0.03, segments=3):
    mod = obj.modifiers.new("Edge softening", "BEVEL")
    mod.width = width
    mod.segments = segments
    mod.limit_method = "ANGLE"
    mod.harden_normals = True
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.shade_smooth_by_angle()


def cube(name, loc, scale, material, bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    if bevel:
        add_bevel(obj, bevel, 3)
    return obj


def uv_sphere(name, loc, scale, material, segments=32, rings=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    bpy.ops.object.shade_smooth()
    return obj


def cylinder(name, loc, radius, depth, material, vertices=48, rotation=(0, 0, 0), bevel=0.0):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material)
    if bevel:
        add_bevel(obj, bevel, 3)
    return obj


def prism_outline(name, points, depth, material, bevel=0.02):
    """Extrude an x,z polygon symmetrically along y."""
    n = len(points)
    verts = [(x, -depth / 2, z) for x, z in points] + [(x, depth / 2, z) for x, z in points]
    faces = [tuple(range(n)), tuple(range(n, 2 * n))]
    for i in range(n):
        j = (i + 1) % n
        faces.append((i, j, n + j, n + i))
    mesh = bpy.data.meshes.new(name + "_mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(material)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    if bevel:
        add_bevel(obj, bevel, 3)
    return obj


def diamond_gem(name, loc, size, material):
    """A double pyramid cut gem, facing the camera."""
    sx, sy, sz = size
    verts = [(-sx, 0, 0), (0, 0, sz), (sx, 0, 0), (0, 0, -sz), (0, -sy, 0), (0, sy, 0)]
    faces = [(0, 1, 4), (1, 2, 4), (2, 3, 4), (3, 0, 4), (1, 0, 5), (2, 1, 5), (3, 2, 5), (0, 3, 5)]
    mesh = bpy.data.meshes.new(name + "_mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(material)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = loc
    return obj


def curve_path(name, coords, bevel_depth, material, resolution=4):
    curve = bpy.data.curves.new(name + "_curve", "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = resolution
    curve.bevel_depth = bevel_depth
    curve.bevel_resolution = 3
    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(len(coords) - 1)
    for point, co in zip(spline.bezier_points, coords):
        point.co = co
        point.handle_left_type = "AUTO"
        point.handle_right_type = "AUTO"
    curve.materials.append(material)
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    return obj


def torus(name, loc, major, minor, material, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=40, minor_segments=10, location=loc, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material)
    bpy.ops.object.shade_smooth()
    return obj


def point_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def create_sword():
    steel = mat_pbr("Blade steel - folded silver", (0.32, 0.43, 0.56), metallic=0.97, roughness=0.20)
    edge = mat_pbr("Blade bevel - bright silver", (0.74, 0.82, 0.90), metallic=1.0, roughness=0.11)
    dark = mat_pbr("Blackened guard steel", (0.07, 0.105, 0.15), metallic=0.94, roughness=0.23)
    gold = mat_pbr("Antique gold inlay", (0.56, 0.34, 0.10), metallic=0.93, roughness=0.18)
    leather = mat_pbr("Black leather grip", (0.012, 0.015, 0.02), metallic=0.0, roughness=0.62)
    blue = mat_pbr("Azure runic energy", (0.0, 0.035, 0.12), metallic=0.28, roughness=0.15, emission=(0.0, 0.11, 0.78), strength=3.8)
    gem = mat_pbr("Sapphire core", (0.004, 0.03, 0.13), metallic=0.40, roughness=0.07, emission=(0.0, 0.08, 0.62), strength=1.9)
    bright_silver = mat_pbr("Engraved silver", (0.46, 0.57, 0.70), metallic=0.98, roughness=0.16)
    add_forged_microdetail(steel, 56.0, 0.06)
    add_forged_microdetail(dark, 44.0, 0.10)
    add_forged_microdetail(gold, 34.0, 0.045)

    # Blade core and two distinct bevel rails.  The outline establishes a credible taper and ricasso.
    blade_outline = [(-0.28, 0.18), (-0.37, 0.42), (-0.29, 5.82), (-0.10, 6.76), (0.0, 7.22), (0.10, 6.76), (0.29, 5.82), (0.37, 0.42), (0.28, 0.18)]
    blade = prism_outline("Blade_Core", blade_outline, 0.18, steel, 0.028)
    blade["part"] = "blade_core"

    left_edge = [(-0.28, 0.24), (-0.35, 0.43), (-0.27, 5.80), (-0.075, 6.92), (-0.035, 7.04), (-0.10, 6.70), (-0.21, 5.73), (-0.25, 0.39)]
    right_edge = [(-x, z) for x, z in left_edge]
    prism_outline("Blade_Edge_L", left_edge, 0.195, edge, 0.012)["part"] = "blade_edge"
    prism_outline("Blade_Edge_R", right_edge, 0.195, edge, 0.012)["part"] = "blade_edge"

    # Raised fuller frame / ancient inlay: depth deliberately proud of the blade.
    fuller_frame = [(-0.17, 0.45), (-0.24, 0.60), (-0.17, 5.54), (0.0, 6.38), (0.17, 5.54), (0.24, 0.60), (0.17, 0.45)]
    fuller = prism_outline("Fuller_Ancient_Silver", fuller_frame, 0.215, dark, 0.016)
    fuller["part"] = "fuller_frame"
    energy_outline = [(-0.06, 0.74), (-0.10, 0.91), (-0.06, 5.38), (0.0, 5.92), (0.06, 5.38), (0.10, 0.91), (0.06, 0.74)]
    energy = prism_outline("Azure_Energy_Channel", energy_outline, 0.228, blue, 0.008)
    energy["part"] = "emissive_energy_channel"

    # Inlaid borders and rune diamonds make the channel read as magic etched into forged metal.
    for x in (-0.205, 0.205):
        curve_path("Gold_Fuller_Filigree", [(x, -0.121, 0.57), (x * 0.82, -0.123, 2.3), (x * 0.7, -0.123, 4.8), (x * 0.32, -0.123, 5.86)], 0.013, gold)
    # Fine shoulder engravings make the blade read as an heirloom, not a plain energy prop.
    for sign in (-1, 1):
        curve_path("Blade_Shoulder_Engraving", [(0.23 * sign, -0.126, 0.58), (0.33 * sign, -0.128, 0.92), (0.27 * sign, -0.128, 1.22), (0.20 * sign, -0.128, 1.38)], 0.020, gold)
        curve_path("Blade_Upper_Engraving", [(0.19 * sign, -0.126, 4.82), (0.25 * sign, -0.128, 5.12), (0.18 * sign, -0.128, 5.44), (0.10 * sign, -0.128, 5.56)], 0.014, gold)
    for z in (1.18, 2.18, 3.20, 4.20, 5.17):
        d = diamond_gem("Runic_Diamond", (0, -0.126, z), (0.075, 0.026, 0.13), blue)
        d["part"] = "rune_emissive"

    # Separate layered scrolling lets the blade carry real silhouette-preserving relief detail.
    for sign in (-1, 1):
        curve_path("Blade_Lower_Vine", [(0.18 * sign, -0.128, 0.72), (0.31 * sign, -0.130, 1.03), (0.28 * sign, -0.130, 1.35), (0.17 * sign, -0.130, 1.53), (0.25 * sign, -0.130, 1.76)], 0.012, bright_silver)
        curve_path("Blade_Mid_Vine", [(0.17 * sign, -0.128, 2.25), (0.26 * sign, -0.130, 2.45), (0.25 * sign, -0.130, 2.75), (0.15 * sign, -0.130, 2.92)], 0.010, gold)
        curve_path("Blade_Upper_Vine", [(0.13 * sign, -0.128, 4.17), (0.23 * sign, -0.130, 4.36), (0.20 * sign, -0.130, 4.62), (0.11 * sign, -0.130, 4.77)], 0.010, bright_silver)
    for z, size in ((1.72, (0.055, 0.020, 0.09)), (2.92, (0.043, 0.018, 0.072)), (4.75, (0.035, 0.016, 0.058))):
        diamond_gem("Blade_Engraved_Rune", (0, -0.130, z), size, gold)

    # Swept wing guard: layered dark steel silhouette, gold vein accents, sapphire focal jewel.
    guard_outline = [(-1.78, -0.02), (-1.91, 0.20), (-1.83, 0.43), (-1.52, 0.29), (-1.08, 0.14), (-0.59, 0.17), (-0.34, 0.38), (0, 0.57), (0.34, 0.38), (0.59, 0.17), (1.08, 0.14), (1.52, 0.29), (1.83, 0.43), (1.91, 0.20), (1.78, -0.02), (1.20, -0.16), (0.64, -0.10), (0.32, -0.25), (0, -0.37), (-0.32, -0.25), (-0.64, -0.10), (-1.20, -0.16)]
    guard = prism_outline("Winged_Crossguard", guard_outline, 0.34, dark, 0.04)
    guard["part"] = "crossguard"
    for sign in (-1, 1):
        curve_path("Guard_Gold_Engraving", [(0.31 * sign, -0.19, 0.22), (0.76 * sign, -0.20, 0.12), (1.25 * sign, -0.20, 0.18), (1.69 * sign, -0.20, 0.31)], 0.035, gold)
        curve_path("Guard_Silver_Ridge", [(0.36 * sign, -0.205, 0.38), (0.88 * sign, -0.208, 0.31), (1.42 * sign, -0.208, 0.38), (1.79 * sign, -0.208, 0.27)], 0.022, edge)
        curve_path("Guard_Inner_Filigree", [(0.49 * sign, -0.207, -0.04), (0.79 * sign, -0.210, 0.03), (1.08 * sign, -0.210, 0.13), (1.30 * sign, -0.210, 0.20)], 0.019, gold)
        curve_path("Guard_Feather_Relief_A", [(0.58 * sign, -0.213, 0.25), (0.86 * sign, -0.215, 0.21), (1.11 * sign, -0.215, 0.29), (1.32 * sign, -0.215, 0.31)], 0.012, bright_silver)
        curve_path("Guard_Feather_Relief_B", [(0.69 * sign, -0.213, 0.12), (0.93 * sign, -0.215, 0.08), (1.19 * sign, -0.215, 0.16), (1.49 * sign, -0.215, 0.24)], 0.010, gold)
        curve_path("Guard_Feather_Relief_C", [(0.84 * sign, -0.213, -0.01), (1.13 * sign, -0.215, -0.02), (1.44 * sign, -0.215, 0.07), (1.68 * sign, -0.215, 0.20)], 0.009, bright_silver)
        diamond_gem("Guard_Side_Sapphire", (1.47 * sign, -0.205, 0.19), (0.09, 0.04, 0.12), gem)
        diamond_gem("Guard_Tip_Inlay", (1.77 * sign, -0.205, 0.19), (0.045, 0.032, 0.064), gold)
    boss = prism_outline("Guard_Central_Boss", [(-0.39, 0.12), (-0.24, 0.55), (0, 0.73), (0.24, 0.55), (0.39, 0.12), (0.20, -0.14), (0, -0.26), (-0.20, -0.14)], 0.41, gold, 0.025)
    boss["part"] = "guard_boss"
    diamond_gem("Guard_Main_Sapphire", (0, -0.245, 0.25), (0.22, 0.085, 0.30), gem)["part"] = "gem_emissive"
    curve_path("Central_Quillon_Silver", [(0, -0.225, 0.56), (0, -0.228, 0.80), (0, -0.228, 1.02)], 0.035, bright_silver)
    curve_path("Central_Quillon_Gold_L", [(-0.07, -0.230, 0.52), (-0.12, -0.232, 0.76), (-0.08, -0.232, 0.94)], 0.014, gold)
    curve_path("Central_Quillon_Gold_R", [(0.07, -0.230, 0.52), (0.12, -0.232, 0.76), (0.08, -0.232, 0.94)], 0.014, gold)

    # Ricasso collar, leather grip, wire binding, and layered royal pommel.
    cylinder("Rain_Guard_Collar", (0, 0, -0.18), 0.28, 0.28, gold, bevel=0.025)
    cube("Grip_Leather_Block", (0, 0, -1.34), (0.19, 0.16, 1.06), leather, 0.045)
    for z in (-0.43, -0.67, -0.91, -1.15, -1.39, -1.63, -1.87, -2.10):
        band = torus("Gold_Grip_Binding", (0, 0, z), 0.205, 0.023, gold)
        band["part"] = "grip_binding"
    for z in (-0.58, -1.05, -1.52, -1.99):
        diamond_gem("Grip_Rivet", (0, -0.19, z), (0.052, 0.022, 0.078), gold)
    # Crossed leather cord follows the reference's diamond-laced grip rather than a simple bar handle.
    for i, z in enumerate((-0.48, -0.80, -1.12, -1.44, -1.76)):
        sign = -1 if i % 2 == 0 else 1
        curve_path("Grip_Diagonal_Lacing", [(0.145 * sign, -0.185, z), (-0.145 * sign, -0.185, z - 0.23)], 0.015, bright_silver, 2)
    cylinder("Pommel_Neck", (0, 0, -2.53), 0.25, 0.22, dark, bevel=0.02)
    uv_sphere("Royal_Pommel", (0, 0, -2.84), (0.36, 0.30, 0.43), gold)
    diamond_gem("Pommel_Sapphire", (0, -0.30, -2.84), (0.16, 0.07, 0.21), gem)["part"] = "gem_emissive"
    for sign in (-1, 1):
        curve_path("Pommel_Gold_Cage", [(0.12 * sign, -0.312, -2.52), (0.30 * sign, -0.314, -2.72), (0.28 * sign, -0.314, -3.02), (0.10 * sign, -0.314, -3.18)], 0.024, gold)
        curve_path("Pommel_Silver_Cage", [(0.07 * sign, -0.316, -2.57), (0.19 * sign, -0.318, -2.83), (0.13 * sign, -0.318, -3.10)], 0.012, bright_silver)
    bpy.ops.mesh.primitive_cone_add(vertices=6, radius1=0.13, radius2=0.31, depth=0.37, location=(0, 0, -3.22))
    pommel_tip = bpy.context.object
    pommel_tip.name = "Pommel_Crown_Tip"
    pommel_tip.data.materials.append(dark)
    add_bevel(pommel_tip, 0.018, 2)

    # Floating rune motes, disabled from shadow casting for a refined energy effect.
    for i, (x, z) in enumerate(((-0.55, 1.5), (0.42, 2.55), (-0.47, 3.70), (0.37, 4.65), (-0.33, 5.38))):
        mote = uv_sphere("Azure_Arcane_Mote", (x, -0.35 + (i % 2) * 0.15, z), (0.025, 0.025, 0.025), blue, 16, 8)
        mote.visible_shadow = False


def create_scene():
    # Ground plane receives the restrained reflection; world remains near black blue.
    floor = cube("Studio_Floor", (0, 0.4, -3.47), (7.5, 7.5, 0.08), mat_pbr("Obsidian floor", (0.008, 0.012, 0.020), metallic=0.25, roughness=0.24))
    floor["part"] = "presentation_only"
    world = bpy.context.scene.world or bpy.data.worlds.new("World")
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.003, 0.006, 0.014, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.14

    # Key, rim and blue kick lights. Large areas preserve material readability.
    def area(name, loc, energy, color, size, target):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.color = color
        data.shape = "DISK"
        data.size = size
        obj = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(obj)
        obj.location = loc
        point_at(obj, target)
    area("Cold_Key", (-4.5, -5.0, 5.5), 1350, (0.44, 0.67, 1.0), 4.0, (0, 0, 2.0))
    area("Warm_Rim", (4.2, 1.8, 4.8), 1250, (1.0, 0.46, 0.16), 3.3, (0, 0, 2.0))
    area("Azure_Fill", (0, -5.4, 2.2), 520, (0.06, 0.30, 1.0), 2.8, (0, 0, 2.2))

    cam_data = bpy.data.cameras.new("Sword_Product_Camera")
    cam = bpy.data.objects.new("Sword_Product_Camera", cam_data)
    bpy.context.collection.objects.link(cam)
    # Direct product-facing angle: show the engraved face, not the sword's side.
    cam.location = (0, -21.2, 1.9)
    cam_data.lens = 60
    point_at(cam, (0, 0, 1.75))
    bpy.context.scene.camera = cam

    scene = bpy.context.scene
    # Blender 4/5 exposes the real-time renderer through this stable enum name.
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 1536
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.filepath = PREVIEW_PATH
    scene.render.film_transparent = False
    scene.view_settings.look = "AgX - Medium High Contrast"


def save_assets():
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    # The .blend retains the studio scene for iteration and rendering.  The game
    # GLB intentionally contains only the weapon, never the floor/camera/lights.
    bpy.ops.object.select_all(action="DESELECT")
    for obj in bpy.context.scene.objects:
        if obj.type in {"MESH", "CURVE"} and obj.get("part") != "presentation_only":
            obj.select_set(True)
    bpy.ops.export_scene.gltf(filepath=GLB_PATH, export_format="GLB", use_selection=True, export_apply=True, export_materials="EXPORT", export_cameras=False, export_lights=False)
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.scene.render.filepath = PREVIEW_PATH
    bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    os.makedirs(OUT_DIR, exist_ok=True)
    clear_scene()
    create_sword()
    create_scene()
    save_assets()
    print("Created", BLEND_PATH)
    print("Created", GLB_PATH)
    print("Created", PREVIEW_PATH)
