"""Render the project's CC0 KayKit Adventurers as four recognisable town merchants.
Blender --background --python tools/render_town_residents.py -- <repository>
"""
import bpy
import os
import sys
import math
import json
from mathutils import Vector

root = sys.argv[sys.argv.index('--') + 1]
folder = root + '/unity/KnightChronicles/Assets/Art/ThirdParty/KayKit/Characters-Adventures/Characters/gltf'
out = root + '/unity/KnightChronicles/Assets/Resources/Art/World'
source = root + '/assets/models/world'
os.makedirs(out, exist_ok=True)
os.makedirs(source, exist_ok=True)
manifest = []
for key, character in [('npc_smith', 'Barbarian'), ('npc_alchemist', 'Mage'), ('npc_guild', 'Knight'), ('npc_trader', 'Rogue')]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=folder + '/' + character + '.fbx')
    for image in bpy.data.images:
        if image.source == 'FILE':
            texture = folder + '/' + os.path.basename(image.filepath.replace('\\', '/'))
            if not os.path.isfile(texture):
                texture = folder + '/' + character.lower() + '_texture.png'
            image.filepath = texture
            image.reload()
    rig = next(obj for obj in bpy.data.objects if obj.type == 'ARMATURE')
    yaw = bpy.data.objects.new('MerchantFacing', None)
    bpy.context.scene.collection.objects.link(yaw)
    rig.parent = yaw
    # These raw Adventurer FBXs face -Y, unlike the edited knight .blend.
    yaw.rotation_euler = (0, 0, 0)
    rig.rotation_euler = (0, 0, 0)
    if rig.animation_data is None:
        rig.animation_data_create()
    for track in list(rig.animation_data.nla_tracks):
        rig.animation_data.nla_tracks.remove(track)
    idle = next(action for action in bpy.data.actions if action.name.split('|')[-1] == 'Idle')
    rig.animation_data.action = idle
    if len(idle.slots):
        rig.animation_data.action_slot = idle.slots[0]
    # FBX includes all interchangeable loadouts. Merchants should not carry all
    # swords, axes and shields at once; keep clothing and one useful role prop.
    for obj in bpy.data.objects:
        if obj.type == 'MESH':
            body_part = obj.name.startswith(character + '_') and 'Shield' not in obj.name
            role_prop = character == 'Mage' and obj.name == 'Spellbook'
            obj.hide_render = not (body_part or role_prop)
    if character == 'Barbarian':
        for loc, size, colour in [((0, .24, 0), (.06, .64, .065), (.30, .16, .075, 1)), ((0, .52, 0), (.32, .16, .16), (.32, .38, .40, 1))]:
            bpy.ops.mesh.primitive_cube_add(size=1)
            hammer = bpy.context.object
            hammer.name = 'Blacksmith_Hammer'
            material = bpy.data.materials.new('HammerOak' if size[1] > .5 else 'HammerSteel')
            material.diffuse_color = colour
            hammer.data.materials.append(material)
            hammer.parent = rig
            hammer.parent_type = 'BONE'
            hammer.parent_bone = 'handslot.r'
            hammer.location = loc
            hammer.scale = size
    scene = bpy.context.scene
    scene.frame_set(int(idle.frame_range[0]) + 6)
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.color_type = 'TEXTURE'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = 'BOTH'
    scene.display.shading.show_shadows = True
    scene.display.shading.show_specular_highlight = True
    scene.display.shading.show_object_outline = False
    scene.display.shading.background_type = 'WORLD'
    scene.render.film_transparent = True
    scene.render.resolution_x = scene.render.resolution_y = 384
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'
    scene.view_settings.view_transform = 'Standard'
    camera_data = bpy.data.cameras.new('TownMerchantBakeCamera')
    camera_data.type = 'ORTHO'
    camera_data.ortho_scale = 3.15
    camera = bpy.data.objects.new('TownMerchantBakeCamera', camera_data)
    scene.collection.objects.link(camera)
    target = Vector((0, 0, .95))
    camera.location = target + Vector((0, -8, 10))
    camera.rotation_euler = (target - camera.location).to_track_quat('-Z', 'Y').to_euler()
    scene.camera = camera
    bpy.context.view_layer.update()
    scene.render.filepath = out + '/' + key + '.png'
    bpy.ops.render.render(write_still=True)
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=source + '/' + key + '_source.blend')
    manifest.append({'key': key, 'character': character, 'source': source + '/' + key + '_source.blend', 'license': 'CC0 KayKit Adventurers', 'cameraElevation': 51.34, 'pivot': [.5, .5], 'size': [384, 384]})
    print('TOWN_MERCHANT_DONE ' + key, flush=True)
with open(source + '/town_merchants_manifest.json', 'w', encoding='utf-8') as file:
    json.dump(manifest, file, indent=2, ensure_ascii=False)
print('TOWN_MERCHANTS_DONE', flush=True)
