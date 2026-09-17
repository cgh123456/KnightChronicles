"""Render seven cameras from the saved model without changing the source file."""
import bpy, sys, os, math
from mathutils import Vector, Matrix
args=sys.argv[sys.argv.index('--')+1:];root=os.path.abspath(args[0]);out=os.path.join(root,'assets/models/knight_refined_v4')
bpy.ops.wm.open_mainfile(filepath=os.path.join(out,'knight_refined_v4.blend'))
s=bpy.context.scene;cam=s.camera
prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='METAL';prefs.get_devices()
for dev in prefs.devices:dev.use=dev.type=='METAL'
s.cycles.device='GPU';s.cycles.samples=128;s.cycles.use_denoising=True
s.render.resolution_x=s.render.resolution_y=1600;s.render.resolution_percentage=100
if '--draft' in args:s.render.resolution_x=s.render.resolution_y=800;s.cycles.samples=40
neutral=bpy.data.collections['KNIGHT | editable components'].objects
alternate=bpy.data.collections['ALTERNATE | drawn sword presentation'].objects
for label,yaw,elev,target,scale in [
 ('front',0,4,(0,0,1.61),3.65),('hero',24,12,(0,0,1.60),3.70),
 ('side',90,6,(0,.04,1.60),3.70),('back',180,5,(0,.09,1.61),3.67),
 ('game',28,51.34,(0,0,1.52),3.60),('details',-24,10,(0,-.04,2.28),2.20),
 ('action',24,24,(-.17,0,1.53),3.79),
 ('hand_relaxed',-28,12,(-.99,-.07,1.155),.60),
 ('hand_grip',-20,14,(-1.01,-.23,1.22),.82)]:
    if '--views' in args and label not in args[args.index('--views')+1].split(','):continue
    action=label in ('action','hand_grip')
    for obj in list(neutral)+list(alternate):
        values=obj.get('action_matrix_world' if action else 'neutral_matrix_world')
        if values:obj.matrix_world=Matrix([values[i:i+4] for i in range(0,16,4)])
    for obj in alternate:obj.hide_render=not action;obj.hide_set(not action)
    for obj in neutral:
        if obj.get('hand_side')==-1 or obj.get('reference_sheathed_sword'):
            obj.hide_render=action;obj.hide_set(action)
    a=math.radians(yaw);e=math.radians(elev);target=Vector(target)
    cam.location=target+Vector((10*math.sin(a)*math.cos(e),-10*math.cos(a)*math.cos(e),10*math.sin(e)))
    cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale
    s.render.filepath=os.path.join(out,label+'.png');bpy.ops.render.render(write_still=True)
    print('FINAL_VIEW',label,flush=True)
