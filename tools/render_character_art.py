"""Bake CC0 KayKit skeletal motion and the approved knight variant into eight directions.
Blender --background --python tools/render_character_art.py -- <repo> [knight|minion|rogue|mage|warrior|all]
"""
import bpy,sys,os,math,json,numpy as np
from mathutils import Vector
ROOT=sys.argv[sys.argv.index('--')+1];WHICH=sys.argv[sys.argv.index('--')+2] if len(sys.argv)>sys.argv.index('--')+2 else 'all'
SOURCE_ONLY='--sources-only' in sys.argv
CHARS=['knight','minion','rogue','mage','warrior'] if WHICH=='all' else [WHICH]
for character in CHARS:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    knight=character=='knight';name='KnightVariant' if knight else 'Skeleton_'+character.title()
    if knight:
        bpy.ops.wm.open_mainfile(filepath=ROOT+'/assets/char/knight_variant/knight_variant.blend')
        out=ROOT+'/unity/KnightChronicles/Assets/Resources/Art/Sprites'
        acts={'Attack':'1H_Melee_Attack_Slice_Horizontal','Hit':'Hit_A','Cast':'Spellcast_Shoot','Idle':'Idle','Walking_A':'Walking_A','Running_A':'Running_A','Dodge_Forward':'Dodge_Forward'}
        cell,cols=256,8
        texdir=ROOT+'/assets/char/knight_variant'
        for part,image in [('Knight_Body','TEX-knight_body_silverblue.png'),('Knight_Cape','TEX-knight_cape_blue.png')]:
            obj=bpy.data.objects.get(part)
            if obj:
                img=bpy.data.images.load(texdir+'/'+image,check_existing=True)
                for mat in obj.data.materials:
                    if mat and mat.use_nodes:
                        for n in mat.node_tree.nodes:
                            if n.type=='TEX_IMAGE':n.image=img
        for o in bpy.data.objects:
            if o.name in ['2H_Sword','1H_Sword_Offhand','Rectangle_Shield','Round_Shield','Spike_Shield','Badge_Shield','Icosphere']:o.hide_render=True
    else:
        fb=ROOT+'/unity/KnightChronicles/Assets/Art/ThirdParty/KayKit/Characters-Skeletons/Characters/gltf'
        bpy.ops.import_scene.fbx(filepath=fb+'/'+name+'.fbx')
        out=ROOT+'/unity/KnightChronicles/Assets/Resources/Art/Enemies';cell,cols=192,6
        acts={'idle':'Idle','walk':'Walking_A','attack':'Spellcast_Shoot' if character=='mage' else '1H_Melee_Attack_Chop'}
        for img in bpy.data.images:
            if img.source=='FILE':
                path=fb+'/'+os.path.basename(img.filepath.replace('\\','/'))
                if not os.path.isfile(path):path=fb+'/skeleton_texture.png'
                img.filepath=path
                try:img.reload()
                except RuntimeError:pass
        if bpy.data.objects.get('Icosphere'):bpy.data.objects['Icosphere'].hide_render=True
    os.makedirs(out,exist_ok=True)
    # Unassigned actions have zero users and Blender would silently discard them
    # when saving the editable source. Preserve the complete imported library.
    for action in bpy.data.actions:action.use_fake_user=True
    rig=next(o for o in bpy.data.objects if o.type=='ARMATURE')
    # Rendering evaluates action curves again. A non-animated parent owns yaw,
    # otherwise animation curves overwrite Rig.rotation_euler during the render.
    yaw=bpy.data.objects.new('DirectionRoot',None);bpy.context.scene.collection.objects.link(yaw)
    rig.parent=yaw;rig.rotation_euler=(0,0,0)
    if rig.animation_data is None:rig.animation_data_create()
    for track in list(rig.animation_data.nla_tracks):rig.animation_data.nla_tracks.remove(track)
    if not knight:
        # Equip distinct geometry on the actual animated right-hand attachment bone.
        mat=bpy.data.materials.new('rusted steel');mat.diffuse_color=(.38,.37,.29,1)
        wood=bpy.data.materials.new('dark oak');wood.diffuse_color=(.22,.11,.055,1)
        magic=bpy.data.materials.new('spectral crystal');magic.diffuse_color=(.38,.65,.72,1)
        def equip(loc,scale,material,sphere=False):
            if sphere:bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=1)
            else:bpy.ops.mesh.primitive_cube_add(size=1)
            o=bpy.context.object;o.name='Equipped_'+character;o.data.materials.append(material)
            o.parent=rig;o.parent_type='BONE';o.parent_bone='handslot.r';o.location=loc;o.scale=scale
        if character=='mage':equip((0,.45,0),(.055,1.25,.055),wood);equip((0,1.12,0),(.13,.20,.13),magic,True)
        elif character=='warrior':equip((0,.37,0),(.06,1.06,.07),wood);equip((.16,.72,0),(.52,.36,.09),mat)
        else:equip((0,.44,0),(.09,.85,.055),mat);equip((0,.10,0),(.30,.07,.065),mat)
    scn=bpy.context.scene;scn.render.engine='BLENDER_WORKBENCH';scn.display.shading.color_type='TEXTURE'
    scn.display.shading.light='STUDIO';scn.display.shading.show_cavity=True;scn.display.shading.cavity_type='BOTH'
    scn.display.shading.show_shadows=True;scn.display.shading.show_specular_highlight=True
    scn.display.shading.show_object_outline=False;scn.display.shading.background_type='WORLD'
    scn.render.film_transparent=True;scn.render.resolution_x=cell;scn.render.resolution_y=cell;scn.render.resolution_percentage=100
    scn.render.image_settings.file_format='PNG';scn.render.image_settings.color_mode='RGBA';scn.view_settings.view_transform='Standard'
    for o in list(scn.objects):
        if o.type in('CAMERA','LIGHT'):bpy.data.objects.remove(o,do_unlink=True)
    cd=bpy.data.cameras.new('BakeCamera');cd.type='ORTHO';cd.ortho_scale=2.85 if not knight else 2.95
    cam=bpy.data.objects.new('BakeCamera',cd);scn.collection.objects.link(cam)
    target=Vector((0,0,.95));cam.location=target+Vector((0,-8,10))
    cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();scn.camera=cam
    # One stable camera frame encloses every pose and direction, including blade
    # tips. Different framing per action would make the actor change size.
    bpy.context.view_layer.update();inv=cam.matrix_world.inverted();projected=[]
    for label,hint in acts.items():
        ncols=(10 if label=='Dodge_Forward' else 12 if label in('Idle','Walking_A','Running_A') else 8) if knight else 6
        action=next(a for a in bpy.data.actions if a.name.split('|')[-1]==hint)
        rig.animation_data.action=action
        if len(action.slots):rig.animation_data.action_slot=action.slots[0]
        a,b=action.frame_range
        for d in range(8):
            yaw.rotation_euler=(0,0,math.radians(d*45))
            for f in range(ncols):
                t=a+(b-a)*f/ncols;scn.frame_set(math.floor(t),subframe=t-math.floor(t));bpy.context.view_layer.update()
                deps=bpy.context.evaluated_depsgraph_get()
                for o in bpy.data.objects:
                    if o.type=='MESH' and not o.hide_render:
                        eo=o.evaluated_get(deps);projected.extend(inv@(eo.matrix_world@Vector(v)) for v in eo.bound_box)
    xs=[p.x for p in projected];ys=[p.y for p in projected]
    cx,cy=(max(xs)+min(xs))/2,(max(ys)+min(ys))/2
    cam.location+=cam.rotation_euler.to_quaternion()@Vector((cx,cy,0))
    cd.ortho_scale=max(max(xs)-min(xs),max(ys)-min(ys))*1.10
    print('CHAR_CAMERA '+name+' scale='+str(cd.ortho_scale),flush=True)
    for label,hint in acts.items():
        if SOURCE_ONLY:continue
        cols=(10 if label=='Dodge_Forward' else 12 if label in('Idle','Walking_A','Running_A') else 8) if knight else 6
        action=next(a for a in bpy.data.actions if a.name.split('|')[-1]==hint)
        rig.animation_data.action=action
        if len(action.slots):rig.animation_data.action_slot=action.slots[0]
        start,end=action.frame_range;sheet=np.zeros((8*cell,cols*cell,4),dtype=np.float32)
        for d in range(8):
            for f in range(cols):
                t=start+(end-start)*f/cols;scn.frame_set(math.floor(t),subframe=t-math.floor(t))
                yaw.rotation_euler=(0,0,math.radians(d*45));bpy.context.view_layer.update()
                tmp=ROOT+'/.work/art-tools/'+character+'_frame.png';scn.render.filepath=tmp
                bpy.ops.render.render(write_still=True)
                render=bpy.data.images.load(tmp,check_existing=False)
                pix=np.array(render.pixels[:],dtype=np.float32).reshape(cell,cell,4)
                sheet[d*cell:(d+1)*cell,f*cell:(f+1)*cell]=pix[::-1]
                bpy.data.images.remove(render)
            print('CHAR_DIRECTION '+name+' '+label+' '+str(d),flush=True)
        img=bpy.data.images.new(name+'_'+label,cols*cell,8*cell,alpha=True);img.pixels.foreach_set(sheet[::-1].ravel())
        img.filepath_raw=out+'/'+name+'_'+label+'_sheet.png';img.file_format='PNG';img.save();bpy.data.images.remove(img)
        with open(out+'/'+name+'_'+label+'_sheet.json','w',encoding='utf-8') as fp:json.dump({'character':name,'action':hint,'frameWidth':cell,'frameHeight':cell,'columns':cols,'fps':12,'cameraElevation':51.34,'orthoScale':cd.ortho_scale,'pivot':[.5,.5],'directions':['S','SE','E','NE','N','NW','W','SW'],'source':'Approved knight variant' if knight else 'KayKit Skeletons CC0'},fp,indent=2)
        print('CHAR_SHEET_DONE '+name+' '+label,flush=True)
    src=ROOT+'/assets/models/world';os.makedirs(src,exist_ok=True)
    for img in list(bpy.data.images):
        if img.source!='FILE':continue
        if not os.path.isfile(bpy.path.abspath(img.filepath)):
            base=os.path.basename(img.filepath.replace('\\','/'))
            candidate=ROOT+'/assets/char/knight_variant/'+base if knight else fb+'/'+base
            if os.path.isfile(candidate):img.filepath=candidate;img.reload()
            elif img.users==0:bpy.data.images.remove(img)
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=src+'/'+name+'_animation_source.blend')
print('CHARACTER_ART_DONE',flush=True)
