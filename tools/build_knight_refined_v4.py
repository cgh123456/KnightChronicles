"""ART-007: reference-driven editable knight, static modelling approval sample.

Blender -b --python tools/build_knight_refined_v4.py -- REPO [--draft]
All pictures are rendered from meshes. No image generation/projection is used.
"""
import os, sys, math, json, random
import bpy, bmesh
from mathutils import Vector, Matrix

ARGS = sys.argv[sys.argv.index('--') + 1:]
ROOT = os.path.abspath(ARGS[0])
sys.path.insert(0, os.path.join(ROOT, 'tools'))
import concept_art_pipeline as P
import knight_refined_helmet
import knight_refined_equipment

OUT = os.path.join(ROOT, 'assets/models/knight_refined_v4')
os.makedirs(OUT, exist_ok=True)
DRAFT = '--draft' in ARGS
GEOMETRY_ONLY = '--geometry-only' in ARGS
VIEWS = ARGS[ARGS.index('--views')+1].split(',') if '--views' in ARGS else None
P.reset_scene()
M = {}
PARTS = []

import knight_refined_materials
M=knight_refined_materials.build(P)
def material(name,color,rough,metal=0,grain=0):
    return P.material(name,color,rough,metal)

def mesh(name, verts, faces, mat, smooth=True, thickness=0, bevel=0, subdiv=0):
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    data.update()
    bm = bmesh.new(); bm.from_mesh(data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(data); bm.free()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(M[mat])
    for p in obj.data.polygons: p.use_smooth = smooth
    if subdiv:
        mod = obj.modifiers.new('Surface refinement', 'SUBSURF'); mod.levels = subdiv
    if thickness:
        mod = obj.modifiers.new('Physical thickness', 'SOLIDIFY'); mod.thickness = thickness; mod.offset = 0
    if bevel:
        mod = obj.modifiers.new('Soft machined edge', 'BEVEL'); mod.width = bevel; mod.segments = 3
    PARTS.append(obj)
    return obj

def uvball(name, loc, scale, mat, seg=32, rings=20):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, location=loc)
    o = bpy.context.object; o.name = name; o.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.data.materials.append(M[mat])
    for p in o.data.polygons:p.use_smooth=True
    PARTS.append(o)
    return o

def box(name, loc, size, mat, bevel=.02):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o=bpy.context.object;o.name=name;o.scale=size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.data.materials.append(M[mat])
    b=o.modifiers.new('Rounded edges','BEVEL');b.width=bevel;b.segments=3
    n=o.modifiers.new('Weighted face normals','WEIGHTED_NORMAL')
    PARTS.append(o)
    return o

def tube(name, points, radius, mat, closed=False, sides=10):
    pts=[Vector(p) for p in points];vs=[];fs=[]
    previous_normal=None
    for i,p in enumerate(pts):
        tangent=(pts[(i+1)%len(pts)]-pts[i-1]) if closed else pts[min(i+1,len(pts)-1)]-pts[max(i-1,0)]
        tangent.normalize()
        # Transport the cross section through bends. Rebuilding it from a
        # fixed up-axis flips curling fingertips as their tangent passes Y.
        n=(previous_normal-tangent*previous_normal.dot(tangent)) if previous_normal is not None else tangent.cross(Vector((0,1,0)))
        if n.length<.01:n=tangent.cross(Vector((1,0,0)))
        if n.length<.01:n=tangent.cross(Vector((0,0,1)))
        n.normalize();previous_normal=n.copy();q=tangent.cross(n).normalized()
        for j in range(sides):vs.append(p+radius*(n*math.cos(j*math.tau/sides)+q*math.sin(j*math.tau/sides)))
    for i in range(len(pts) if closed else len(pts)-1):
        ni=(i+1)%len(pts)
        for j in range(sides):fs.append((i*sides+j,i*sides+(j+1)%sides,ni*sides+(j+1)%sides,ni*sides+j))
    if not closed:fs.extend([tuple(reversed(range(sides))),tuple((len(pts)-1)*sides+j for j in range(sides))])
    return mesh(name,vs,fs,mat)

def loft(name, rings, mat, samples=40, cap=True, folds=0):
    # ring = (z, centreX, centreY, widthRadius, depthRadius)
    vs=[];fs=[]
    for z,x,y,rx,ry in rings:
        for j in range(samples):
            a=j*math.tau/samples
            wav=1+folds*math.sin(a*7+z*3)
            vs.append((x+rx*math.sin(a)*wav,y-ry*math.cos(a)*wav,z))
    for i in range(len(rings)-1):
        for j in range(samples):fs.append((i*samples+j,i*samples+(j+1)%samples,(i+1)*samples+(j+1)%samples,(i+1)*samples+j))
    if cap:fs.extend([tuple(reversed(range(samples))),tuple((len(rings)-1)*samples+j for j in range(samples))])
    return mesh(name,vs,fs,mat)

def tapered_limb(name, a, b, r1, r2, mat):
    vec=Vector(b)-Vector(a)
    o=loft(name,[(0,0,0,r1,r1*.86),(vec.length*.25,0,0,r1*1.06,r1*.92),
        (vec.length*.80,0,0,r2*1.04,r2*.9),(vec.length,0,0,r2,r2*.85)],mat)
    o.rotation_euler=vec.to_track_quat('Z','Y').to_euler();o.location=a
    return o

def cuff(name, a,b,r1,r2,mat,depth=1,theta=(-math.pi,math.pi),point=0):
    vec=Vector(b)-Vector(a);q=vec.to_track_quat('Z','Y');vs=[];fs=[];nt=36;nz=5
    for iz in range(nz):
        t=iz/(nz-1);r=r1*(1-t)+r2*t
        for j in range(nt+1):
            angle=theta[0]+(theta[1]-theta[0])*j/nt
            z=vec.length*t+point*max(0,math.cos(angle))*(t**5)
            vs.append(Vector(a)+q@Vector((r*math.sin(angle),-r*math.cos(angle)*depth,z)))
    for iz in range(nz-1):
        for j in range(nt):
            v=iz*(nt+1)+j;fs.append((v,v+1,v+nt+2,v+nt+1))
    return mesh(name,vs,fs,mat,thickness=.024,bevel=.008)

import knight_refined_body
knight_refined_body.build(globals())
uvball('15 | Recessed continuous helmet lining',(0,.015,2.565),(.379,.420,.363),'dark',48,32)
helmet_parts=knight_refined_helmet.build(M)
for o in helmet_parts:o['helmet_part']=True
PARTS[-1]['helmet_part']=True
PARTS.extend(helmet_parts)
PARTS.extend(knight_refined_equipment.build(M, sheathed=True))

# The alternate presentation is real geometry with the same wrist attachment.
# Neutral front/side/back views keep both hands relaxed and the sword sheathed.
neutral_hand=[o for o in PARTS if o.get('hand_side')==-1]
action_start=len(PARTS)
knight_refined_body.glove(-1,gripping=True)
grip=Vector((-.993,-.111,1.157));d=Vector((-.49,-.18,-.855)).normalized()
width=(Vector((1,0,0))-d*d.x).normalized();depth=d.cross(width).normalized()
a=grip-d*.117;b=grip+d*.124
tapered_limb('14 | Held leather sword grip',a,b,.034,.034,'leather_dark')
q=(b-a).to_track_quat('Z','Y')
for i in range(9):
    p=a.lerp(b,(i+.5)/9)
    tube('14 | Wound sword grip',[p+q@Vector((math.cos(t)*.035,math.sin(t)*.035,0)) for t in [j*math.tau/24 for j in range(24)]],.004,'leather_light',True)
uvball('14 | Faceted brass pommel',a-d*.036,(.052,.048,.057),'gold',8,4)
guard=grip+d*.164
outline=[(-.201,-.008),(-.192,.028),(-.08,.003),(0,-.008),(.08,.003),(.192,.028),(.201,-.008),(.095,-.033),(0,-.041),(-.095,-.033)]
verts=[];faces=[]
for z in (-.025,.025):
    for x,y in outline:verts.append(guard+width*x+d*y+depth*z)
n=len(outline);faces=[tuple(reversed(range(n))),tuple(n+i for i in range(n))]
for i in range(n):faces.append((i,(i+1)%n,(i+1)%n+n,i+n))
mesh('14 | Curved flat brass sword guard',verts,faces,'gold',False,bevel=.009)
verts=[];faces=[]
for t,w in ((-.025,.075),(.105,.075),(.70,.063),(.87,.039)):
    p=guard+d*t
    for x,y in ((-w,0),(0,-.018),(w,0),(0,.018)):verts.append(p+width*x+depth*y)
for i in range(3):
    for j in range(4):faces.append((i*4+j,i*4+(j+1)%4,(i+1)*4+(j+1)%4,(i+1)*4+j))
tip_index=len(verts);verts.append(guard+d*1.03)
for j in range(4):faces.append((12+j,12+(j+1)%4,tip_index))
faces.append((3,2,1,0))
mesh('14 | Diamond ground steel blade',verts,faces,'steel_light',False,bevel=.0015)
action_parts=PARTS[action_start:];del PARTS[action_start:]
for o in action_parts:o['arm_side']=-1
action_col=bpy.data.collections.new('ALTERNATE | drawn sword presentation')
bpy.context.scene.collection.children.link(action_col)
for o in action_parts:
    for c in list(o.users_collection):c.objects.unlink(o)
    action_col.objects.link(o);o.hide_render=True;o.hide_set(True)

# Explicit static presentation transforms travel with the editable file.
for o in PARTS+action_parts:
    neutral=o.matrix_world.copy();delta=Matrix.Identity(4)
    arm=o.get('arm_side',o.get('hand_side',0))
    if arm:
        pivot=Vector((arm*.552,0,2.025))
        rotation=Matrix.Rotation(-.15 if arm==-1 else -.075,4,'X')@Matrix.Rotation(.025 if arm==-1 else -.035,4,'Y')
        delta=Matrix.Translation(pivot)@rotation@Matrix.Translation(-pivot)
    elif o.get('leg_side'):
        delta=Matrix.Translation((o['leg_side']*.035,0,0))
    elif o.get('helmet_part'):
        pivot=Vector((0,0,2.27));delta=Matrix.Translation(pivot)@Matrix.Rotation(.05,4,'X')@Matrix.Translation(-pivot)
    o['neutral_matrix_world']=[v for row in neutral for v in row]
    o['action_matrix_world']=[v for row in delta@neutral for v in row]

def action_pose(enabled):
    for o in PARTS+action_parts:
        values=o['action_matrix_world' if enabled else 'neutral_matrix_world']
        o.matrix_world=Matrix([values[i:i+4] for i in range(0,16,4)])
    for o in action_parts:o.hide_render=not enabled;o.hide_set(not enabled)
    for o in neutral_hand:o.hide_render=enabled;o.hide_set(enabled)
    for o in PARTS:
        if o.get('reference_sheathed_sword'):o.hide_render=enabled;o.hide_set(enabled)

# Object-level component naming keeps all parts editable for approval and rigging.
model_col=bpy.data.collections.new('KNIGHT | editable components')
bpy.context.scene.collection.children.link(model_col)
for o in PARTS:
    for c in list(o.users_collection):c.objects.unlink(o)
    model_col.objects.link(o)
    o['source']='ART-007 original mesh reconstruction from concept 02'

# The default modelling viewport opens on the finished assembly, not the floor.
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_distance=5.3
            area.spaces.active.region_3d.view_location=(0,0,1.60)
            area.spaces.active.shading.type='MATERIAL'

# Render studio: neutral warm-grey floor, broad key and narrower rim, no DOF.
scene=bpy.context.scene
scene.render.engine='CYCLES'
scene.cycles.samples=32 if DRAFT else 128
scene.cycles.use_denoising=True
try:
    device_preferences=bpy.context.preferences.addons['cycles'].preferences
    device_preferences.compute_device_type='METAL'
    device_preferences.get_devices()
    for dev in device_preferences.devices:dev.use=dev.type=='METAL'
    if any(dev.type=='METAL' for dev in device_preferences.devices):scene.cycles.device='GPU'
except Exception as exc:
    print('Metal unavailable; using CPU:',exc,flush=True)
scene.render.threads_mode='FIXED';scene.render.threads=8
scene.render.resolution_x=scene.render.resolution_y=800 if DRAFT else 1600
scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.render.image_settings.color_mode='RGBA'
scene.view_settings.view_transform='AgX'
scene.view_settings.look='AgX - Medium High Contrast'
scene.world=bpy.data.worlds.new('Soft neutral studio')
scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.55,.53,.50,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.35
floor_mat=material('Studio taupe','#827d75',.95)
floor_bsdf=floor_mat.node_tree.nodes.get('Principled BSDF')
floor_bsdf.inputs['Emission Color'].default_value=P.linear_color('#827d75')
floor_bsdf.inputs['Emission Strength'].default_value=.40
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.008))
floor=bpy.context.object;floor.name='STUDIO | ground';floor.data.materials.append(floor_mat)
for name,loc,energy,size,color in [
    ('Large softbox key',(3.6,-4.0,6),830,.85,(1,.96,.91)),
    ('Neutral fill',(-3.5,-2.5,3.5),130,4,(.91,.94,1)),
    ('Soft edge light',(2.3,4,5),400,2.4,(1,.94,.86)),
]:
    ld=bpy.data.lights.new(name,'AREA');ld.energy=energy;ld.shape='DISK';ld.size=size;ld.color=color
    if name=='Neutral fill' and hasattr(ld,'specular_factor'):ld.specular_factor=.25
    ob=bpy.data.objects.new(name,ld);scene.collection.objects.link(ob);ob.location=loc
    ob.rotation_euler=(Vector((0,0,1.65))-ob.location).to_track_quat('-Z','Y').to_euler()
cd=bpy.data.cameras.new('Approval camera');cd.type='ORTHO';cd.ortho_scale=3.85
cam=bpy.data.objects.new('Approval camera',cd);scene.collection.objects.link(cam);scene.camera=cam

def view(yaw,elevation,target=(0,0,1.58),scale=3.85):
    a=math.radians(yaw);e=math.radians(elevation);focus=Vector(target)
    cam.location=focus+Vector((10*math.sin(a)*math.cos(e),-10*math.cos(a)*math.cos(e),10*math.sin(e)))
    cam.rotation_euler=(focus-cam.location).to_track_quat('-Z','Y').to_euler();cd.ortho_scale=scale

view(24,12)
scene['task']='ART-007 static visual sample; animation authoring deferred for modelling approval'
scene['reference']='assets/concepts/art-direction-v2/02-character-model-sheet.png'
scene['revision']='ART-007 refinement of independently reconstructed v3; new armor, cloth, equipment, hands and boots'
stats=P.model_statistics(PARTS)
stats.update({'task':'ART-007','stage':'static modeling sample','heightTarget':3.2,
    'reference':scene['reference'],'renderEngine':'CYCLES','samples':scene.cycles.samples,
    'rigged':False,'animationClips':0,'meshProjectionTextureUsed':False,
    'views':['hero','front','side','back','game','details','action','hand_relaxed','hand_grip'],
    'coordinateSystem':'Z up / -Y front',
    'createdFrom':'ART-006 independent concept reconstruction refined with new ART-007 component surfaces',
    'neutralEquipment':'sheathed sword and relaxed hands',
    'alternatePresentation':'ALTERNATE | drawn sword presentation collection',
    'proceduralMaterialsInBlend':True,'paintedAlbedoSourceImages':['material_sources/steel_albedo.png','material_sources/leather_albedo.png'],
    'pbrExportStatus':'pending dedicated atlas bake'})
P.write_manifest(os.path.join(OUT,'manifest.json'),stats)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'knight_refined_v4.blend'))
if GEOMETRY_ONLY:
    print('KNIGHT_GEOMETRY_COMPLETE',flush=True)
    raise SystemExit(0)
for label,yaw,elev,focus,scale in [
    ('front',0,4,(0,0,1.61),3.65),
    ('hero',24,12,(0,0,1.60),3.70),
    ('side',90,6,(0,.04,1.60),3.70),
    ('back',180,5,(0,.09,1.61),3.67),
    ('game',28,51.34,(0,0,1.52),3.60),
    ('details',-24,10,(0,-.04,2.28),2.20),
    ('action',24,24,(-.17,0,1.53),3.79),
    ('hand_relaxed',-28,12,(-.99,-.07,1.155),.60),
    ('hand_grip',-20,14,(-1.01,-.23,1.22),.82),
]:
    if VIEWS and label not in VIEWS: continue
    action_pose(label in ('action','hand_grip'))
    view(yaw,elev,focus,scale)
    P.render_png(os.path.join(OUT,label+'.png'))
    print('KNIGHT_SAMPLE_VIEW',label,flush=True)
view(24,12)
action_pose(False)
# The editable source is saved once before rendering, so export/bake processes
# can read a stable immutable snapshot while the view images are generated.
print('KNIGHT_SAMPLE_COMPLETE',json.dumps(stats),flush=True)
