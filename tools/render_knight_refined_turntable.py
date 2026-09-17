"""Render a real-camera orbit of the editable source, without modifying it."""
import bpy, math, sys, os
from mathutils import Vector, Matrix
root=os.path.abspath(sys.argv[sys.argv.index('--')+1])
source=os.path.join(root,'assets/models/knight_refined_v4/knight_refined_v4.blend')
frames=os.path.join(root,'.work/knight-refined/turntable');os.makedirs(frames,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=source)
s=bpy.context.scene;s.render.resolution_x=s.render.resolution_y=720;s.render.resolution_percentage=100
s.cycles.samples=24;s.cycles.use_denoising=True
prefs=bpy.context.preferences.addons['cycles'].preferences
prefs.compute_device_type='METAL';prefs.get_devices()
for dev in prefs.devices:dev.use=dev.type=='METAL'
s.cycles.device='GPU'
s.render.image_settings.file_format='PNG';s.render.image_settings.color_mode='RGB'
cam=s.camera;cam.data.ortho_scale=3.68;target=Vector((0,.06,1.59));e=math.radians(12)
for frame in range(48):
    frame_path=os.path.join(frames,'%03d.png'%frame)
    if os.path.isfile(frame_path) and os.path.getmtime(frame_path)>os.path.getmtime(source):
        print('ORBIT_FRAME_CURRENT',frame,flush=True)
        continue
    a=frame*math.tau/48
    cam.location=target+Vector((10*math.sin(a)*math.cos(e),-10*math.cos(a)*math.cos(e),10*math.sin(e)))
    cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
    s.render.filepath=frame_path
    bpy.ops.render.render(write_still=True)
    print('ORBIT_FRAME',frame,flush=True)
print('ORBIT_COMPLETE',flush=True)
