"""Read source libraries, packed image dependencies and actual animated foot projections."""
import bpy,sys,json,os,math
from mathutils import Vector
root=sys.argv[sys.argv.index('--')+1];folder=root+'/assets/models/world';records=[]
for name in ['world_collection','KnightVariant_animation_source','Skeleton_Minion_animation_source','Skeleton_Rogue_animation_source','Skeleton_Mage_animation_source','Skeleton_Warrior_animation_source','stone_wall_side']:
    path=folder+'/'+name+'.blend'
    if not os.path.isfile(path):continue
    bpy.ops.wm.open_mainfile(filepath=path)
    record={'file':name+'.blend','meshes':len([o for o in bpy.data.objects if o.type=='MESH']),'collections':len([c for c in bpy.data.collections if c.name.startswith('ASSET_')]),'collectionMeshes':{c.name:len([o for o in c.objects if o.type=='MESH']) for c in bpy.data.collections if c.name.startswith('ASSET_')},'actions':len(bpy.data.actions),'fileImages':[{'name':i.name,'packed':bool(i.packed_file),'path':i.filepath} for i in bpy.data.images if i.source=='FILE']}
    rig=next((o for o in bpy.data.objects if o.type=='ARMATURE'),None)
    if rig:
        yaw=bpy.data.objects.get('DirectionRoot');action=next(a for a in bpy.data.actions if a.name.split('|')[-1]=='Idle')
        rig.animation_data.action=action
        if len(action.slots):rig.animation_data.action_slot=action.slots[0]
        ys=[];faces=[]
        for d in range(8):
            yaw.rotation_euler=(0,0,math.radians(d*45));bpy.context.scene.frame_set(math.floor(action.frame_range[0]));bpy.context.view_layer.update()
            inv=bpy.context.scene.camera.matrix_world.inverted();deps=bpy.context.evaluated_depsgraph_get();er=rig.evaluated_get(deps)
            feet=[inv@(er.matrix_world@er.pose.bones[n].head) for n in ['toes.l','toes.r'] if n in er.pose.bones]
            ys.append(sum(p.y for p in feet)/len(feet))
            eyes=next((o for o in bpy.data.objects if o.type=='MESH' and o.name.endswith('_Eyes')),None)
            head=next((o for o in bpy.data.objects if o.type=='MESH' and o.name.endswith('_Head')),None)
            if eyes and head:
                def center(o):
                    eo=o.evaluated_get(deps);p=[eo.matrix_world@Vector(v) for v in eo.bound_box];return sum(p,Vector())/len(p)
                f=center(eyes)-center(head);faces.append([round(f.x,3),round(f.y,3)])
        record['footYByDirection']=ys;record['averageFootY']=sum(ys)/len(ys)
        record['suggestedFootPivotY']=.5+record['averageFootY']/bpy.context.scene.camera.data.ortho_scale
        record['faceXYByDirection']=faces;record['yawParent']=yaw.name if yaw else None
    records.append(record)
out=folder+'/qa/source-qa.json';os.makedirs(os.path.dirname(out),exist_ok=True)
with open(out,'w',encoding='utf-8') as fp:json.dump(records,fp,ensure_ascii=False,indent=2)
print('SOURCE_QA_DONE '+out,flush=True)
