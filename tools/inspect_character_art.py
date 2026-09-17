import bpy,sys,os,json
root=sys.argv[sys.argv.index('--')+1]
paths=[root+'/assets/char/knight_variant/knight_variant.blend',root+'/unity/KnightChronicles/Assets/Art/ThirdParty/KayKit/Characters-Skeletons/Characters/gltf/Skeleton_Minion.fbx']
for path in paths:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if path.endswith('.blend'):bpy.ops.wm.open_mainfile(filepath=path)
    else:bpy.ops.import_scene.fbx(filepath=path)
    print('CHAR_SOURCE '+path,flush=True)
    print('CHAR_OBJECTS '+json.dumps([(o.name,o.type,[round(v,3) for v in o.dimensions]) for o in bpy.data.objects]),flush=True)
    print('CHAR_ACTIONS '+json.dumps([(a.name,list(a.frame_range)) for a in bpy.data.actions]),flush=True)
    print('CHAR_BONES '+json.dumps([b.name for o in bpy.data.objects if o.type=='ARMATURE' for b in o.data.bones]),flush=True)
