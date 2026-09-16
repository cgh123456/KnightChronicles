"""KayKit 角色 -> 2D 俯视 sprite 图集（改版支持版）。

基于 /tmp/kaykit-stage/render_spritesheet.py，新增：
1. --variant <目录>: 应用部件级贴图变体（TEX-knight_cape_blue.png -> Cape，
   TEX-knight_body_silverblue.png -> Body），并隐藏多余武器挂件（仅留 1H_Sword）。
2. --name <输出前缀>: 图集文件名前缀（默认取 glb 文件名）。

用法:
  Blender --background --python render_spritesheet_variant.py -- \
      <角色.glb> <动作名片段> <输出目录> [帧数] [变体贴图目录] [输出前缀]
输出: <前缀>_<动作>_sheet.png + .json（每行一个方向，每列一帧）
"""
import bpy
import json
import math
import os
import sys
import numpy as np

FRAME = 256
COLS = 12
DIRS = ['S', 'SE', 'E', 'NE', 'N', 'NW', 'W', 'SW']
HIDE = {'2H_Sword', '1H_Sword_Offhand', 'Rectangle_Shield', 'Round_Shield',
        'Spike_Shield', 'Badge_Shield', 'Icosphere'}
CAPE_TEX = 'TEX-knight_cape_blue.png'
BODY_TEX = 'TEX-knight_body_silverblue.png'


def apply_variant(variant_dir):
    """Cape/Body 材质换变体贴图，隐藏多余挂件。"""
    cape_img = bpy.data.images.load(os.path.join(variant_dir, CAPE_TEX))
    body_img = bpy.data.images.load(os.path.join(variant_dir, BODY_TEX))
    src = bpy.data.materials['knight_texture']
    for part, img in (('Knight_Cape', cape_img), ('Knight_Body', body_img)):
        m = src.copy()
        m.name = f'MAT-variant_{part}'
        for n in m.node_tree.nodes:
            if n.type == 'TEX_IMAGE':
                n.image = img
        obj = bpy.data.objects[part]
        obj.data.materials.clear()
        obj.data.materials.append(m)
    for name in HIDE:
        o = bpy.data.objects.get(name)
        if o:
            o.hide_render = True


def main():
    argv = sys.argv[sys.argv.index('--') + 1:]
    in_path = argv[0]
    action_hint = argv[1]
    out_dir = argv[2]
    frames = int(argv[3]) if len(argv) > 3 else COLS
    variant_dir = argv[4] if len(argv) > 4 else None
    prefix = argv[5] if len(argv) > 5 else os.path.splitext(os.path.basename(in_path))[0]

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=in_path)
    if variant_dir:
        apply_variant(variant_dir)

    armature = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    action = next(a for a in bpy.data.actions if action_hint.lower() in a.name.lower())
    start, end = action.frame_range
    length = end - start
    print(f"动作 {action.name}: 帧 {start:.1f}-{end:.1f}", flush=True)

    # 关键：把目标动作挂到骨架并清空 NLA，否则 frame_set 驱动的是
    # glTF 导入后默认激活的动作（可能是攻击循环），烘出的图集内容全错。
    if armature.animation_data is None:
        armature.animation_data_create()
    for track in list(armature.animation_data.nla_tracks):
        armature.animation_data.nla_tracks.remove(track)
    armature.animation_data.action = action

    scn = bpy.context.scene
    scn.render.engine = 'BLENDER_WORKBENCH'
    scn.display.shading.color_type = 'TEXTURE'
    scn.display.shading.light = 'STUDIO'
    scn.display.shading.show_cavity = True
    scn.render.film_transparent = True
    scn.render.resolution_x = FRAME
    scn.render.resolution_y = FRAME
    scn.render.resolution_percentage = 100
    scn.render.image_settings.file_format = 'PNG'
    scn.render.image_settings.color_mode = 'RGBA'

    cam_data = bpy.data.cameras.new('Cam')
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = 2.2
    cam = bpy.data.objects.new('Cam', cam_data)
    scn.collection.objects.link(cam)
    cam.location = (0, -2.3, 2.0)
    target = bpy.data.objects.new('Target', None)
    target.location = (0, 0, 0.55)
    direction = target.location - cam.location
    cam.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
    scn.camera = cam

    tmp_dir = os.path.join(out_dir, 'frames')
    os.makedirs(tmp_dir, exist_ok=True)
    files = []
    for d, dir_name in enumerate(DIRS):
        armature.rotation_euler = (0, 0, math.radians(180 + d * 45))
        for f in range(frames):
            t = start + length * (f / frames)
            scn.frame_set(int(t), subframe=t - int(t))
            # 若动作曲线驱动骨架根节点的旋转，frame_set 会覆盖朝向；采样后强制回写。
            armature.rotation_euler = (0, 0, math.radians(180 + d * 45))
            path = os.path.join(tmp_dir, f'{d:02d}_{f:02d}.png')
            scn.render.filepath = path
            bpy.ops.render.render(write_still=True)
            files.append((d, f, path))
        print(f"方向 {dir_name} 完成 ({frames} 帧)", flush=True)

    sheet_w, sheet_h = COLS * FRAME, len(DIRS) * FRAME
    sheet = np.zeros((sheet_h, sheet_w, 4), dtype=np.float32)
    for d, f, path in files:
        img = bpy.data.images.load(path)
        w, h = img.size
        px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
        px = px[::-1]
        y0, x0 = d * FRAME, f * FRAME
        sheet[y0:y0 + h, x0:x0 + w, :] = px[:, :, :]
        bpy.data.images.remove(img)

    out_img = bpy.data.images.new('sheet', sheet_w, sheet_h, alpha=True)
    out_img.pixels.foreach_set(sheet[::-1].ravel())
    sheet_path = os.path.join(out_dir, f'{prefix}_{action.name}_sheet.png')
    out_img.filepath_raw = sheet_path
    out_img.file_format = 'PNG'
    out_img.save()

    meta = {
        'character': prefix,
        'action': action.name,
        'frameWidth': FRAME, 'frameHeight': FRAME,
        'columns': COLS, 'frames': frames,
        'fps': 12, 'loop': True,
        'directions': DIRS,
        'variant': bool(variant_dir),
        'note': '每行一个方向(行序=directions), 每列一帧(循环)',
    }
    meta_path = os.path.join(out_dir, f'{prefix}_{action.name}_sheet.json')
    with open(meta_path, 'w') as fp:
        json.dump(meta, fp, ensure_ascii=False, indent=2)
    print(f"SHEET_DONE\t{sheet_path}", flush=True)


main()
