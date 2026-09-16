"""
验证脚本：按 blender-modeling skill 的配方生成一把剑，
渲染一张 Cycles 预览图，并导出 GLB 到项目 assets。
运行: blender --background --python tools/test_sword.py
"""
import bpy
import bmesh
import os
from mathutils import Vector

OUT_DIR = "/Users/chenguanhan/游戏项目/骑士异闻录/assets/models"
RENDER_PATH = "/Users/chenguanhan/游戏项目/骑士异闻录/assets/models/GEO-sword_preview.png"
GLB_PATH = os.path.join(OUT_DIR, "GEO-sword.glb")

# ---- 尺寸约定（skill: 长/宽/薄三轴，单位米）----
BLADE_LEN, BLADE_BROAD, BLADE_THIN = 0.78, 0.045, 0.008
GUARD_W, GUARD_H, GUARD_D = 0.20, 0.035, 0.05
GRIP_VISIBLE, GRIP_R = 0.12, 0.015
GRIP_OVERLAP_GUARD, GRIP_OVERLAP_POMMEL = 0.015, 0.010  # 重叠隐藏接缝
POMMEL_R = 0.028

# ---- 清空场景（不用 read_factory_settings，避免重置插件状态）----
scene = bpy.context.scene
for obj in list(bpy.data.objects):
    bpy.data.objects.remove(obj, do_unlink=True)

# ---- 剑身：长轴 = Z，薄 = X，宽 = Y ----
bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, BLADE_LEN / 2 + 0.05))
blade = bpy.context.active_object
blade.name = "GEO-blade"
blade.scale = (BLADE_THIN, BLADE_BROAD, BLADE_LEN)
bpy.ops.object.transform_apply(scale=True)

# 剑尖收尖：先纵向分段（否则整个剑身会变成金字塔），再捏合顶部顶点
bpy.context.view_layer.objects.active = blade
bpy.ops.object.mode_set(mode="EDIT")
bm = bmesh.from_edit_mesh(blade.data)
bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=6, use_grid_fill=True)
bm.verts.ensure_lookup_table()
max_z = max(v.co.z for v in bm.verts)
top = [v for v in bm.verts if abs(v.co.z - max_z) < 0.001]
for v in top:
    v.co.x = 0.0
    v.co.y = 0.0
bmesh.update_edit_mesh(blade.data)
bpy.ops.mesh.select_all(action="DESELECT")
for v in top:
    v.select = True
bmesh.update_edit_mesh(blade.data)
bpy.ops.mesh.remove_doubles(threshold=0.001)
bpy.ops.object.mode_set(mode="OBJECT")
print(f"tapered:{blade.name}")

# ---- 护手（十字格）：压进剑身底部以下，包住衔接处 ----
guard_z = 0.05 - GUARD_H / 2 + 0.015  # 上移 1.5cm 与剑身重叠
bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, guard_z))
guard = bpy.context.active_object
guard.name = "GEO-guard"
guard.scale = (GUARD_W, GUARD_D, GUARD_H)
bpy.ops.object.transform_apply(scale=True)

# ---- 握柄：两端伸进护手和剑首内部 ----
grip_len = GRIP_VISIBLE + GRIP_OVERLAP_GUARD + GRIP_OVERLAP_POMMEL
grip_center = 0.05 - GUARD_H - GRIP_VISIBLE / 2 + 0.005
bpy.ops.mesh.primitive_cylinder_add(radius=GRIP_R, depth=grip_len, location=(0, 0, grip_center))
grip = bpy.context.active_object
grip.name = "GEO-grip"
bpy.ops.object.shade_smooth()

# ---- 剑首（配重球）：上移 1cm 咬住握柄底端 ----
pommel_z = grip_center - grip_len / 2 - POMMEL_R + GRIP_OVERLAP_POMMEL
bpy.ops.mesh.primitive_uv_sphere_add(radius=POMMEL_R, location=(0, 0, pommel_z))
pommel = bpy.context.active_object
pommel.name = "GEO-pommel"
bpy.ops.object.shade_smooth()

# ---- 硬表面部件：Bevel → SubSurf（顺序不可反）----
for name in ("GEO-guard",):
    obj = bpy.data.objects[name]
    bevel = obj.modifiers.new("Bevel", type="BEVEL")
    bevel.width = 0.004
    bevel.segments = 3
    bevel.limit_method = "ANGLE"
    bevel.angle_limit = 0.523599
    subsurf = obj.modifiers.new("SubSurf", type="SUBSURF")
    subsurf.levels = 2
    subsurf.render_levels = 2
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.shade_smooth()

blade_bevel = blade.modifiers.new("Bevel", type="BEVEL")
blade_bevel.width = 0.002
blade_bevel.segments = 2
blade_bevel.limit_method = "ANGLE"
blade_bevel.angle_limit = 0.523599

# ---- 材质（简单 PBR，正式流程走 blender-materials skill）----
def make_mat(name, color, metallic, roughness):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    return mat

mat_steel = make_mat("MAT-steel", (0.75, 0.77, 0.80), 1.0, 0.22)
mat_gold = make_mat("MAT-gold", (0.85, 0.65, 0.25), 1.0, 0.35)
mat_leather = make_mat("MAT-leather", (0.18, 0.10, 0.06), 0.0, 0.75)

bpy.data.objects["GEO-blade"].data.materials.append(mat_steel)
bpy.data.objects["GEO-guard"].data.materials.append(mat_gold)
bpy.data.objects["GEO-pommel"].data.materials.append(mat_gold)
bpy.data.objects["GEO-grip"].data.materials.append(mat_leather)

# ---- 组合成整体并旋转：宽面朝向相机（skill: axis orientation）----
parts = [bpy.data.objects[n] for n in ("GEO-blade", "GEO-guard", "GEO-grip", "GEO-pommel")]
for p in parts:
    p.select_set(True)
bpy.context.view_layer.objects.active = blade
bpy.ops.object.join()
sword = bpy.context.active_object
sword.name = "GEO-sword"
sword.rotation_euler = (0, 0, 1.5708)  # 绕 Z 转 90°，宽面朝 -Y 方向的相机
bpy.ops.object.transform_apply(rotation=True)

# ---- 三点布光（key / fill / rim）----
def add_light(name, loc, energy, size, color=(1, 1, 1)):
    light_data = bpy.data.lights.new(name, type="AREA")
    light_data.energy = energy
    light_data.size = size
    light_data.color = color
    light = bpy.data.objects.new(name, light_data)
    light.location = loc
    bpy.context.collection.objects.link(light)
    # 朝向原点上方（剑的中心高度）
    target = (0, 0, 0.45)
    direction = Vector(target) - Vector(loc)
    light.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    return light

add_light("LGT-key", (1.2, -1.2, 1.4), 400, 1.5)
add_light("LGT-fill", (-1.5, -0.8, 0.6), 120, 2.0, color=(0.85, 0.9, 1.0))
add_light("LGT-rim", (0.6, 1.5, 1.2), 250, 1.0)

# ---- 地面 + 相机 ----
bpy.ops.mesh.primitive_plane_add(size=10, location=(0, 0, 0))
floor = bpy.context.active_object
floor.name = "GEO-floor"
floor.data.materials.append(make_mat("MAT-floor", (0.08, 0.08, 0.09), 0.0, 0.9))

cam_data = bpy.data.cameras.new("CAM-hero")
cam_data.lens = 85
cam = bpy.data.objects.new("CAM-hero", cam_data)
cam.location = (0, -2.2, 0.55)
bpy.context.collection.objects.link(cam)
direction = Vector((0, 0, 0.42)) - cam.location
cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
scene.camera = cam

# ---- 渲染设置：Cycles 低采样（无头模式可靠）----
scene.render.engine = "CYCLES"
scene.cycles.samples = 64
scene.cycles.use_denoising = True
scene.render.resolution_x = 1080
scene.render.resolution_y = 1350
scene.render.filepath = RENDER_PATH
bpy.ops.render.render(write_still=True)
print(f"rendered:{RENDER_PATH}")

# ---- 导出 GLB ----
bpy.ops.export_scene.gltf(
    filepath=GLB_PATH,
    export_format="GLB",
    export_apply=True,
    export_materials="EXPORT",
    export_yup=True,
    export_animations=False,
    export_normals=True,
)
print(f"exported:{GLB_PATH}")
print("done:1")
