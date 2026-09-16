"""Reference-matched presentation studio for the full-scale Azure sword.

Blender 5.2 compatible. The sword is centered on X, faces -Y, and spans
Z = 0.005 .. 1.605 m. No sword geometry, rendering, or saving happens here.

    from azure_reference_studio import setup_studio
    scene = setup_studio(output_directory, quick=False)
    scene.camera = bpy.data.objects['detail_camera']  # optional alternate view

All created scene objects live in STUDIO and carry presentation_only=True.
"""

import math
from pathlib import Path

import bpy
from mathutils import Vector


def _aim(obj, point):
    obj.rotation_euler = (Vector(point) - obj.location).to_track_quat('-Z', 'Y').to_euler()


def _link(obj, collection):
    for owner in tuple(obj.users_collection):
        owner.objects.unlink(obj)
    collection.objects.link(obj)
    obj['presentation_only'] = True
    return obj


def _mat(name, color, roughness, metallic=0.0):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Roughness'].default_value = roughness
    bsdf.inputs['Metallic'].default_value = metallic
    return material, bsdf


def _stone_material():
    mat, bsdf = _mat('STUDIO_wet_charcoal_stone', (0.024, 0.032, 0.044), 0.29, 0.3)
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    coords = nodes.new('ShaderNodeTexCoord')
    mapping = nodes.new('ShaderNodeVectorMath')
    mapping.operation = 'MULTIPLY'
    mapping.inputs[1].default_value = (1.0, 1.0, 3.0)
    links.new(coords.outputs['Object'], mapping.inputs[0])

    macro = nodes.new('ShaderNodeTexNoise')
    macro.inputs['Scale'].default_value = 17.0
    macro.inputs['Detail'].default_value = 5.0
    macro.inputs['Roughness'].default_value = 0.73
    links.new(mapping.outputs[0], macro.inputs['Vector'])
    color = nodes.new('ShaderNodeValToRGB')
    color.color_ramp.elements[0].position = 0.14
    color.color_ramp.elements[0].color = (0.006, 0.009, 0.014, 1)
    color.color_ramp.elements[1].position = 0.88
    color.color_ramp.elements[1].color = (0.045, 0.052, 0.065, 1)
    links.new(macro.outputs['Fac'], color.inputs['Fac'])
    links.new(color.outputs['Color'], bsdf.inputs['Base Color'])

    roughness = nodes.new('ShaderNodeMapRange')
    roughness.inputs['To Min'].default_value = 0.21
    roughness.inputs['To Max'].default_value = 0.48
    links.new(macro.outputs['Fac'], roughness.inputs['Value'])
    links.new(roughness.outputs['Result'], bsdf.inputs['Roughness'])
    bump = nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 0.55
    bump.inputs['Distance'].default_value = 0.009
    links.new(macro.outputs['Fac'], bump.inputs['Height'])
    micro = nodes.new('ShaderNodeTexNoise')
    micro.inputs['Scale'].default_value = 380
    micro.inputs['Detail'].default_value = 2
    links.new(mapping.outputs[0], micro.inputs['Vector'])
    micro_bump = nodes.new('ShaderNodeBump')
    micro_bump.inputs['Strength'].default_value = 0.22
    micro_bump.inputs['Distance'].default_value = 0.00045
    links.new(micro.outputs['Fac'], micro_bump.inputs['Height'])
    links.new(bump.outputs['Normal'], micro_bump.inputs['Normal'])
    links.new(micro_bump.outputs['Normal'], bsdf.inputs['Normal'])
    return mat


def _background_material():
    mat, bsdf = _mat('STUDIO_dark_blue_charcoal_background', (0.015, 0.020, 0.030), 1.0)
    bsdf.inputs['Specular IOR Level'].default_value = 0.1
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    coord = nodes.new('ShaderNodeTexCoord')
    noise = nodes.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = 1.2
    noise.inputs['Detail'].default_value = 3.0
    noise.inputs['Roughness'].default_value = 0.65
    links.new(coord.outputs['Object'], noise.inputs['Vector'])
    ramp = nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].color = (0.0006, 0.0009, 0.0016, 1)
    ramp.color_ramp.elements[1].color = (0.005, 0.007, 0.011, 1)
    links.new(noise.outputs['Fac'], ramp.inputs['Fac'])
    links.new(ramp.outputs['Color'], bsdf.inputs['Base Color'])
    # A very low baseline keeps the background legible when exposure changes.
    links.new(ramp.outputs['Color'], bsdf.inputs['Emission Color'])
    bsdf.inputs['Emission Strength'].default_value = 0.12
    return mat


def _camera(collection, name, location, target, scale):
    data = bpy.data.cameras.new(name)
    data.type = 'ORTHO'
    data.ortho_scale = scale
    data.lens = 70
    data.clip_start = 0.01
    data.clip_end = 100
    obj = bpy.data.objects.new(name, data)
    collection.objects.link(obj)
    obj.location = location
    obj['presentation_only'] = True
    _aim(obj, target)
    return obj


def _area(collection, name, location, target, energy, color, size, size_y=None):
    data = bpy.data.lights.new(name, 'AREA')
    data.energy = energy
    data.color = color
    data.shape = 'RECTANGLE'
    data.size = size
    data.size_y = size_y if size_y is not None else size
    obj = bpy.data.objects.new(name, data)
    collection.objects.link(obj)
    obj.location = location
    obj['presentation_only'] = True
    _aim(obj, target)
    return obj


def _compositor(scene):
    """Use the Blender 5.2 compositor group API, with older-version fallback."""
    if hasattr(scene, 'compositing_node_group'):
        group = bpy.data.node_groups.new('STUDIO_subtle_energy_glow', 'CompositorNodeTree')
        group.interface.new_socket(name='Image', in_out='OUTPUT', socket_type='NodeSocketColor')
        output = group.nodes.new('NodeGroupOutput')
        scene.compositing_node_group = group
    else:
        scene.use_nodes = True
        group = scene.node_tree
        group.nodes.clear()
        output = group.nodes.new('CompositorNodeComposite')
    layers = group.nodes.new('CompositorNodeRLayers')
    glare = group.nodes.new('CompositorNodeGlare')
    glare.label = 'Restrained sapphire glow; preserve engraved metal'
    if 'Type' in glare.inputs:
        glare.inputs['Type'].default_value = 'Fog Glow'
        glare.inputs['Quality'].default_value = 'High'
        glare.inputs['Threshold'].default_value = 2.0
        glare.inputs['Smoothness'].default_value = 0.3
        glare.inputs['Strength'].default_value = 0.16
        glare.inputs['Size'].default_value = 0.15
    else:
        glare.glare_type = 'FOG_GLOW'
        glare.quality = 'HIGH'
        glare.threshold = 2.0
        glare.size = 7
        glare.mix = -0.84
    group.links.new(layers.outputs['Image'], glare.inputs['Image'])
    group.links.new(glare.outputs['Image'], output.inputs['Image'])
    layers.location = (-350, 0)
    glare.location = (-100, 0)
    output.location = (180, 0)


def setup_studio(out_dir, quick=False):
    """Build the studio and return the active scene without rendering/saving.

    quick=True: 1024x1536, 24 samples.
    quick=False: 1536x2304, 64 samples.
    Front/detail/25-degree camera objects are named front_camera,
    detail_camera, and threequarter_camera. All three are orthographic.
    The default render path is out_dir/azure_reference_front.png.
    """
    scene = bpy.context.scene
    collection = bpy.data.collections.get('STUDIO')
    if collection is not None:
        for obj in tuple(collection.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
    else:
        collection = bpy.data.collections.new('STUDIO')
        scene.collection.children.link(collection)
    collection['presentation_only'] = True

    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 24 if quick else 64
    scene.cycles.use_denoising = True
    scene.cycles.use_adaptive_sampling = True
    scene.cycles.adaptive_threshold = 0.04 if quick else 0.018
    scene.cycles.max_bounces = 8
    scene.cycles.diffuse_bounces = 3
    scene.cycles.glossy_bounces = 4
    scene.cycles.transmission_bounces = 6
    scene.cycles.transparent_max_bounces = 4
    scene.cycles.volume_bounces = 0
    scene.cycles.sample_clamp_indirect = 3.0
    scene.render.resolution_x = 1024 if quick else 1536
    scene.render.resolution_y = 1536 if quick else 2304
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'
    scene.render.image_settings.color_depth = '8'
    scene.render.film_transparent = False
    scene.render.filepath = str(Path(out_dir) / 'azure_reference_front.png')
    scene.render.use_file_extension = True
    scene.view_settings.view_transform = 'AgX'
    scene.view_settings.look = 'AgX - Medium High Contrast'
    scene.view_settings.exposure = 0.25
    scene.view_settings.gamma = 1.0

    world = bpy.data.worlds.new('STUDIO_charcoal_environment')
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (0.11, 0.15, 0.22, 1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.25
    scene.world = world

    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, 0))
    floor = _link(bpy.context.object, collection)
    floor.name = 'STUDIO_reflective_stone_floor'
    floor.data.materials.append(_stone_material())

    bpy.ops.mesh.primitive_plane_add(size=2, location=(0, 4, 2.7), rotation=(math.pi / 2, 0, 0))
    backdrop = _link(bpy.context.object, collection)
    backdrop.name = 'STUDIO_mottled_charcoal_backdrop'
    backdrop.scale = (6, 4, 1)
    backdrop.data.materials.append(_background_material())

    front = _camera(collection, 'front_camera', (0.00224, -4, 1.00), (0.00224, 0, 0.7564), 1.7186)
    detail = _camera(collection, 'detail_camera', (0.035, -3, 1.35), (0, 0, 1.32), 0.60)
    angle = math.radians(25)
    threequarter = _camera(collection, 'threequarter_camera',
                           (4 * math.sin(angle), -4 * math.cos(angle), 0.90),
                           (0, 0, 0.808), 1.718)
    detail['suggested_resolution'] = '1536x1536'
    threequarter['suggested_resolution'] = '1536x2304'
    scene.camera = front

    _area(collection, 'STUDIO_cool_left_softbox', (-1.25, -1.1, 1.75),
          (0, 0, 0.95), 145, (0.66, 0.79, 1), 0.75, 2.2)
    _area(collection, 'STUDIO_warm_right_strip', (1.1, -0.7, 1.30),
          (0, 0, 0.92), 105, (1, 0.82, 0.59), 0.3, 1.9)
    _area(collection, 'STUDIO_neutral_front_fill', (-0.08, -2.1, 1.05),
          (0, 0, 0.96), 48, (0.84, 0.91, 1), 1.0, 1.8)
    _area(collection, 'STUDIO_silver_rim', (0.65, 0.65, 1.80),
          (0, 0, 1.0), 155, (0.62, 0.77, 1), 0.55, 1.4)
    _area(collection, 'STUDIO_pommel_toplight', (-0.1, -0.05, 2.25),
          (0, 0, 1.35), 35, (1, 0.9, 0.76), 0.7, 0.6)
    _area(collection, 'STUDIO_background_wash', (-1.4, 1.8, 2.0),
          (0, 2.5, 1.3), 11, (0.49, 0.67, 1), 1.8, 2.5)
    _area(collection, 'STUDIO_blue_floor_bounce', (-0.1, -0.15, 0.13),
          (0, 0, 0), 0.13, (0.12, 0.48, 1), 0.055, 0.055)
    _compositor(scene)
    # Projected reference colour already carries painted illumination. Moderate
    # physical light prevents a second exposure from washing the dark steel out.
    for obj in collection.objects:
        if obj.type == 'LIGHT':
            obj.data.energy *= 0.18
    scene['studio_notes'] = ('Full-scale reference studio. Front faces -Y. '
                             'The backdrop and floor are presentation-only. '
                             'Alternate cameras: detail_camera, threequarter_camera.')
    return scene
