"""Shared real-mesh V2 rendering/export contract (Blender 5.2).

This does not synthesize an image or model. Builders provide actual meshes and
keyframed animation. PNG atlases are baked from the evaluated Blender scene.
"""
import os, math, json
import bpy
import numpy as np
from mathutils import Vector

PALETTE = {
    'stone': '#aaa79a', 'plaster': '#cbc4af', 'wood': '#806148',
    'iron': '#596978', 'leaf': '#738364', 'cloth': '#414b74',
    'gold': '#be974f', 'fire': '#dca45a', 'magic': '#58a29c',
    'bone': '#d6cbb3', 'leather': '#694b36', 'dark': '#242a30',
}
DIRECTIONS = ['S', 'E', 'N', 'W']

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def linear_color(value):
    if isinstance(value, str):
        value = value.lstrip('#')
        rgb = [int(value[i:i+2], 16) / 255 for i in (0, 2, 4)]
        return tuple(v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4 for v in rgb) + (1,)
    return tuple(value) if len(value) == 4 else tuple(value) + (1,)

def material(name, color, roughness=.72, metallic=0, emission=0, transmission=0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = linear_color(color)
    mat.use_nodes = True
    node = mat.node_tree.nodes.get('Principled BSDF')
    node.inputs['Base Color'].default_value = mat.diffuse_color
    node.inputs['Roughness'].default_value = roughness
    node.inputs['Metallic'].default_value = metallic
    node.inputs['Emission Color'].default_value = mat.diffuse_color
    node.inputs['Emission Strength'].default_value = emission
    node.inputs['Transmission Weight'].default_value = transmission
    return mat

def setup_render(cell=256, ortho_scale=3.3, target=(0, 0, 1.05), elevation=51.34, samples=16):
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.film_transparent = True
    scene.render.resolution_x = scene.render.resolution_y = cell
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'
    scene.render.image_settings.color_depth = '8'
    scene.render.threads_mode = 'FIXED'
    scene.render.threads = 2
    scene.render.fps = 24
    scene.view_settings.view_transform = 'AgX'
    scene.view_settings.look = 'AgX - Medium High Contrast'
    if hasattr(scene, 'eevee') and hasattr(scene.eevee, 'taa_render_samples'):
        scene.eevee.taa_render_samples = samples
    world = bpy.data.worlds.new('V2_cool_ambient')
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs[0].default_value = (.38, .44, .55, 1)
    world.node_tree.nodes['Background'].inputs[1].default_value = .38
    scene.world = world
    for name, loc, energy, size, color in [
        ('V2_warm_key', (-3, -4, 7), 650, 5, (1, .88, .73)),
        ('V2_cool_fill', (4, -1, 4), 320, 4, (.74, .83, 1)),
        ('V2_soft_rim', (1, 4, 6), 420, 4, (1, .93, .81)),
    ]:
        data = bpy.data.lights.new(name, 'AREA')
        data.energy, data.size, data.color = energy, size, color
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        obj.location = loc
        obj.rotation_euler = (Vector(target) - obj.location).to_track_quat('-Z', 'Y').to_euler()
    data = bpy.data.cameras.new('V2_BakeCamera')
    data.type, data.ortho_scale = 'ORTHO', ortho_scale
    camera = bpy.data.objects.new('V2_BakeCamera', data)
    scene.collection.objects.link(camera)
    angle = math.radians(elevation)
    camera.location = Vector(target) + Vector((0, -10 * math.cos(angle), 10 * math.sin(angle)))
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat('-Z', 'Y').to_euler()
    scene.camera = camera
    return camera

def fit_camera(camera, objects, margin=1.16):
    """For static assets; moving actors should use a stable multi-pose envelope."""
    bpy.context.view_layer.update()
    inv = camera.matrix_world.inverted()
    points = [inv @ (obj.matrix_world @ Vector(v)) for obj in objects
              if obj.type == 'MESH' and not obj.hide_render for v in obj.bound_box]
    if not points:
        raise ValueError('Cannot fit an empty model')
    lo = Vector((min(p.x for p in points), min(p.y for p in points), 0))
    hi = Vector((max(p.x for p in points), max(p.y for p in points), 0))
    camera.location += camera.rotation_euler.to_quaternion() @ ((lo + hi) * .5)
    camera.data.ortho_scale = max(hi.x - lo.x, hi.y - lo.y) * margin
    return camera.data.ortho_scale

def render_png(path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path

def bake_sheet(path, frame_samples=None, yaw_root=None, pose=None,
               directions=DIRECTIONS, fps=12, action='idle', metadata=None):
    """pose(frame, row) optionally switches/evaluates builder animation.

    frame_samples are Blender timeline samples; yaw root owns direction outside
    animation so F-curves cannot overwrite it. Four rows are not mirrored.
    """
    scene = bpy.context.scene
    samples = list(frame_samples if frame_samples is not None else range(1, 9))
    width, height = scene.render.resolution_x, scene.render.resolution_y
    cols, rows = len(samples), len(directions)
    atlas = np.zeros((height * rows, width * cols, 4), dtype=np.float32)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    temp_path = os.path.join(os.path.dirname(path), '.' + os.path.basename(path) + '.frame.png')
    original_yaw = yaw_root.rotation_euler.copy() if yaw_root is not None else None
    for row in range(rows):
        for col, sample in enumerate(samples):
            scene.frame_set(math.floor(sample), subframe=sample - math.floor(sample))
            if pose is not None:
                pose(sample, row)
            if yaw_root is not None:
                yaw_root.rotation_euler.z = row * math.tau / rows
            bpy.context.view_layer.update()
            render_png(temp_path)
            frame = bpy.data.images.load(temp_path, check_existing=False)
            pixels = np.array(frame.pixels[:], dtype=np.float32).reshape(height, width, 4)
            atlas[row * height:(row+1) * height, col * width:(col+1) * width] = pixels[::-1]
            bpy.data.images.remove(frame)
        print('V2_BAKE_DIRECTION', os.path.basename(path), row, flush=True)
    if yaw_root is not None:
        yaw_root.rotation_euler = original_yaw
    image = bpy.data.images.new(os.path.basename(path), cols * width, rows * height, alpha=True)
    image.pixels.foreach_set(atlas[::-1].ravel())
    image.filepath_raw, image.file_format = path, 'PNG'
    image.save()
    bpy.data.images.remove(image)
    os.remove(temp_path)  # exact generator-owned temporary file only
    info = {'action': action, 'columns': cols, 'rows': rows, 'fps': fps,
            'frameWidth': width, 'frameHeight': height, 'directions': list(directions),
            'orthoScale': scene.camera.data.ortho_scale, 'cameraElevation': 51.34,
            'pivot': [.5, .5], 'renderEngine': 'BLENDER_EEVEE', 'colorManagement': 'AgX'}
    if metadata:
        info.update(metadata)
    with open(os.path.splitext(path)[0] + '.json', 'w', encoding='utf-8') as stream:
        json.dump(info, stream, ensure_ascii=False, indent=2)
    print('V2_SHEET_DONE', path, flush=True)
    return info

def save_source(key, source_dir, objects=None):
    """Preserve actions and pack textures; export only intended model objects."""
    os.makedirs(source_dir, exist_ok=True)
    for action in bpy.data.actions:
        action.use_fake_user = True
    for image in bpy.data.images:
        if image.source == 'FILE' and os.path.isfile(bpy.path.abspath(image.filepath)):
            image.pack()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(source_dir, key + '.blend'))
    bpy.ops.object.select_all(action='DESELECT')
    selected = list(objects) if objects is not None else [o for o in bpy.context.scene.objects if o.type in ('MESH', 'ARMATURE', 'EMPTY')]
    for obj in selected:
        obj.hide_set(False)
        obj.select_set(True)
    bpy.ops.export_scene.gltf(filepath=os.path.join(source_dir, key + '.glb'),
        export_format='GLB', use_selection=True, export_animations=True,
        export_animation_mode='ACTIONS', export_force_sampling=True,
        export_materials='EXPORT', export_apply=False)
    return model_statistics(selected)

def model_statistics(objects=None):
    objects = list(objects) if objects is not None else list(bpy.context.scene.objects)
    meshes = [o for o in objects if o.type == 'MESH']
    triangles = 0
    deps = bpy.context.evaluated_depsgraph_get()
    for obj in meshes:
        evaluated = obj.evaluated_get(deps)
        mesh = evaluated.to_mesh()
        mesh.calc_loop_triangles()
        triangles += len(mesh.loop_triangles)
        evaluated.to_mesh_clear()
    return {'meshObjects': len(meshes), 'trianglesEvaluated': triangles,
            'bones': sum(len(o.data.bones) for o in objects if o.type == 'ARMATURE'),
            'actions': [a.name for a in bpy.data.actions]}

def write_manifest(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'w', encoding='utf-8') as stream:
        json.dump(data, stream, ensure_ascii=False, indent=2)
