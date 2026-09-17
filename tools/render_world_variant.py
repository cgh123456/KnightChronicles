"""Render only requested orientation variants from the existing editable model library.
Blender --background --python tools/render_world_variant.py -- <repo>
"""
import bpy,os,sys,json,math
from mathutils import Vector,Matrix
root=sys.argv[sys.argv.index('--')+1];src=root+'/assets/models/world'
bpy.ops.wm.open_mainfile(filepath=src+'/world_collection.blend')
scn=bpy.context.scene;col=bpy.data.collections['ASSET_stone_wall']
for c in bpy.data.collections:
    if c.name.startswith('ASSET_'):c.hide_render=c!=col
# Source assets are laid out as a library. Recenter this collection, then yaw.
pts=[o.matrix_world@Vector(v) for o in col.objects for v in o.bound_box]
center=Vector(((min(p.x for p in pts)+max(p.x for p in pts))/2,(min(p.y for p in pts)+max(p.y for p in pts))/2,0))
rot=Matrix.Rotation(math.pi/2,4,'Z')
for o in col.objects:
    matrix=o.matrix_world.copy();matrix.translation-=center;o.matrix_world=rot@matrix
bpy.context.view_layer.update()
pts=[o.matrix_world@Vector(v) for o in col.objects for v in o.bound_box]
target=Vector((0,0,(min(p.z for p in pts)+max(p.z for p in pts))/2))
cam=scn.camera;cam.location=target+Vector((0,-8,10));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
bpy.context.view_layer.update();inv=cam.matrix_world.inverted();pts=[inv@p for p in pts]
xs,ys=[p.x for p in pts],[p.y for p in pts];cx,cy=(min(xs)+max(xs))/2,(min(ys)+max(ys))/2
cam.location+=cam.rotation_euler.to_quaternion()@Vector((cx,cy,0));cam.data.ortho_scale=max(max(xs)-min(xs),max(ys)-min(ys))*1.10
scn.render.resolution_x=512;scn.render.resolution_y=512;scn.render.resolution_percentage=100
scn.render.filepath=root+'/unity/KnightChronicles/Assets/Resources/Art/World/stone_wall_side.png';bpy.ops.render.render(write_still=True)
path=src+'/world_manifest.json';data=json.load(open(path,encoding='utf-8'))
data=[v for v in data if v['key']!='stone_wall_side']
data.append({'key':'stone_wall_side','sourceCollection':col.name,'meshes':len(col.objects),'png':'stone_wall_side.png','pivot':[.5,.5],'cameraElevation':51.34,'modelYaw':90,'source':'world_collection.blend'})
with open(path,'w',encoding='utf-8') as fp:json.dump(data,fp,ensure_ascii=False,indent=2)
# Save a separate orientation source; keep the main model library intact.
bpy.ops.wm.save_as_mainfile(filepath=src+'/stone_wall_side.blend')
print('WALL_SIDE_DONE',flush=True)
