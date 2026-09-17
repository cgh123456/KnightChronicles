"""Create an immediately viewable drawn-sword source variant."""
import bpy,sys,os,math
from mathutils import Vector, Matrix
root=os.path.abspath(sys.argv[sys.argv.index('--')+1]);out=os.path.join(root,'assets/models/knight_refined_v4')
bpy.ops.wm.open_mainfile(filepath=os.path.join(out,'knight_refined_v4.blend'))
for obj in bpy.data.collections['ALTERNATE | drawn sword presentation'].objects:obj.hide_render=False;obj.hide_set(False)
for obj in bpy.data.collections['KNIGHT | editable components'].objects:
    if obj.get('hand_side')==-1 or obj.get('reference_sheathed_sword'):obj.hide_render=True;obj.hide_set(True)
for obj in list(bpy.data.collections['KNIGHT | editable components'].objects)+list(bpy.data.collections['ALTERNATE | drawn sword presentation'].objects):
    values=obj.get('action_matrix_world')
    if values:obj.matrix_world=Matrix([values[i:i+4] for i in range(0,16,4)])
s=bpy.context.scene;cam=s.camera;target=Vector((-.17,0,1.53));a=math.radians(24);e=math.radians(24)
cam.location=target+Vector((10*math.sin(a)*math.cos(e),-10*math.cos(a)*math.cos(e),10*math.sin(e)))
cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=3.79
s['presentation']='Drawn sword static variant; no animation or rig'
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out,'knight_refined_v4_drawn.blend'))
