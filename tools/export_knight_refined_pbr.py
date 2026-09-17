"""ART-007D: bake the new knight's evaluated neutral meshes to embedded PBR GLB.

Usage:
  Blender -b --python tools/export_knight_refined_pbr.py -- ROOT
  Optional: --resolution 2048 --no-normal --samples 8

Reads assets/models/knight_refined_v4/knight_refined_v4.blend, never saves it.
Only visible MESH objects in 'KNIGHT | editable components' are exported.
Four material families get individual UV atlases; all shader colour/roughness/
metallic detail is baked. Tangent normal baking also captures shader bump.
Object/Generated coordinates and pre-join Cycles pointiness are retained as
mesh attributes, so Voronoi leather pores, crossing Wave weave and worn edges
keep their source-mesh appearance after atlas assembly and triangulation.
"""
import argparse
import hashlib
import json
import math
import os
import struct
import sys
import time
from array import array

import bpy
import bmesh
from mathutils import Matrix

FAMILIES = ('steel', 'fabric', 'leather', 'brass')
LOCAL_COORD = 'RefinedBake_LocalCoordinate'
GENERATED_COORD = 'RefinedBake_GeneratedCoordinate'
POINTINESS = 'RefinedBake_SourcePointiness'
POINTINESS_METHOD = 'Cycles per-source evaluated mesh / duplicate-aware two-ring curvature'
POINTINESS_SOURCE = 'https://github.com/blender/blender/blob/main/intern/cycles/blender/mesh.cpp#L462'
AREA_THRESHOLD = 1e-12
MERGE_DISTANCE = 1e-10
GEOMETRY_CLEANUP = []
SOURCE_EVALUATED_TRIANGLES = 0


def _hash(path):
    h=hashlib.sha256()
    with open(path,'rb') as f:
        for block in iter(lambda:f.read(1024*1024),b''):h.update(block)
    return h.hexdigest()


def _family(material):
    name=material.name.lower() if material else ''
    if name.startswith(('steel','mail')):return 'steel'
    if name.startswith(('gold','trim')):return 'brass'
    if name.startswith(('cloth','cream','thread','padding')):return 'fabric'
    return 'leather'


def _material_copy(material, cache):
    """Keep source coordinates and pointiness after world-transform/join.

    Source materials use Object-coordinate procedural textures. Merely joining
    meshes would change those coordinates, so store them as POINT attributes on
    each evaluated copy. The other noise/Voronoi/Wave and bump nodes are copied
    intact; only implicit coordinates and the geometry Pointiness are remapped.
    """
    if material.name in cache:return cache[material.name]
    m=material.copy();m.name='BAKE SOURCE | '+material.name
    nodes,links=m.node_tree.nodes,m.node_tree.links
    for n in list(nodes):
        if n.bl_idname == 'ShaderNodeNewGeometry':
            outgoing=list(n.outputs['Pointiness'].links)
            if outgoing:
                attr=nodes.new('ShaderNodeAttribute');attr.attribute_name=POINTINESS
                attr.label='Original evaluated part curvature before join / triangulation'
                for link in outgoing:links.new(attr.outputs['Fac'],link.to_socket)
        if n.type!='TEX_COORD' or n.object is not None:continue
        for output_name,attribute_name in (('Object',LOCAL_COORD),('Generated',GENERATED_COORD)):
            outgoing=list(n.outputs[output_name].links)
            if not outgoing:continue
            attr=nodes.new('ShaderNodeAttribute');attr.attribute_name=attribute_name
            attr.label='Preserved original '+output_name+' coordinates'
            for link in outgoing:links.new(attr.outputs['Vector'],link.to_socket)
    cache[material.name]=m
    return m


def _pointiness(mesh):
    """Precompute the Cycles Geometry Pointiness field on one source mesh.

    Mirrors Blender's attr_create_pointiness algorithm, Apache-2.0 licensed:
    https://github.com/blender/blender/blob/main/intern/cycles/blender/mesh.cpp
    Steps are duplicate identification, shared normals, edge-direction/normal
    angle, then one neighbor blur. Crucially this runs BEFORE join and before
    delivery triangulation adds edges; neither may change source edge wear.
    This is a Python/NumPy implementation, not an artist-tuned curvature proxy.
    """
    import numpy as np
    count=len(mesh.vertices)
    if not count:return []
    mesh.update()
    coordinates=np.empty(count*3,dtype=np.float32)
    normals=np.empty(count*3,dtype=np.float32)
    mesh.vertices.foreach_get('co',coordinates)
    mesh.vertices.foreach_get('normal',normals)
    coordinates=coordinates.reshape(-1,3);normals=normals.reshape(-1,3)
    # Cycles searches a 3 * FLT_EPSILON window in coordinate-sum order and
    # checks squared distance < FLT_EPSILON. Exact duplicates favor low index.
    epsilon=np.finfo(np.float32).eps
    sums=coordinates.sum(axis=1,dtype=np.float32)
    ordered=sorted(range(count),key=lambda i:(float(sums[i]),-i))
    original=np.arange(count,dtype=np.int32)
    for position,index in enumerate(ordered):
        for other_position in range(position+1,count):
            other=ordered[other_position]
            if sums[other]-sums[index] > 3*epsilon:break
            difference=coordinates[other]-coordinates[index]
            if np.dot(difference,difference)<epsilon:
                original[index]=other
                break
    for index in range(count):
        root=int(original[index])
        while root!=original[root]:root=int(original[root])
        original[index]=root
    welded_normals=np.zeros((count,3),dtype=np.float32)
    np.add.at(welded_normals,original,normals)
    lengths=np.linalg.norm(welded_normals,axis=1)
    valid=lengths>0
    welded_normals[valid]/=lengths[valid,None]
    welded_normals=welded_normals[original]
    edges=np.empty(len(mesh.edges)*2,dtype=np.int32)
    mesh.edges.foreach_get('vertices',edges)
    edges=original[edges.reshape(-1,2)]
    edges=np.unique(np.sort(edges,axis=1),axis=0)
    if not len(edges):return [0.0]*count
    a,b=edges[:,0],edges[:,1]
    directions=coordinates[b]-coordinates[a]
    lengths=np.linalg.norm(directions,axis=1)
    valid=lengths>0
    directions[valid]/=lengths[valid,None]
    directions[~valid]=0
    neighbors=np.zeros(count,dtype=np.int32)
    accumulated=np.zeros((count,3),dtype=np.float32)
    np.add.at(accumulated,a,directions);np.add.at(accumulated,b,-directions)
    np.add.at(neighbors,a,1);np.add.at(neighbors,b,1)
    average=accumulated/np.maximum(neighbors,1)[:,None]
    cosines=np.clip(np.sum(welded_normals*average,axis=1),-1,1)
    raw=np.arccos(cosines)/math.pi
    raw[neighbors==0]=0
    raw[original!=np.arange(count)]=0
    blurred=raw.copy()
    np.add.at(blurred,a,raw[b]);np.add.at(blurred,b,raw[a])
    blurred/=neighbors+1
    result=blurred[original]
    if not np.isfinite(result).all():
        raise RuntimeError('Nonfinite pointiness on '+mesh.name)
    return result.astype(np.float32)


def _scalar_attribute(mesh,name,values):
    attr=mesh.attributes.get(name) or mesh.attributes.new(name,'FLOAT','POINT')
    attr.data.foreach_set('value',values)


def _attribute(mesh,name,values):
    attr=mesh.attributes.get(name) or mesh.attributes.new(name,'FLOAT_VECTOR','POINT')
    attr.data.foreach_set('vector',array('f',(n for xyz in values for n in xyz)))


def _clean_known_world_slivers(mesh,source_name):
    """Repair only the diagnosed pouch-lid float32 seam, on the export copy.

    Its clamped bevel leaves local-space slivers whose pairs become EXACTLY
    coincident after the world-space FLOAT transform used by glTF. Joining all
    nearby geometry would damage valid seams, so only zero-length-edge endpoints
    of this one diagnosed component are candidates. No valid face is deleted.
    """
    if source_name != '07 | Curved hip pouch lid':
        return
    bm=bmesh.new();bm.from_mesh(mesh)
    bmesh.ops.triangulate(bm,faces=list(bm.faces),quad_method='FIXED',ngon_method='EAR_CLIP')
    bm.to_mesh(mesh);bm.free();mesh.update();mesh.calc_loop_triangles()
    before_triangles=len(mesh.loop_triangles);before_vertices=len(mesh.vertices)
    bad=[t for t in mesh.loop_triangles if t.area<=AREA_THRESHOLD]
    if not bad:return
    if any(t.area != 0.0 for t in bad):
        raise RuntimeError('Pouch repair expected exact float-collapsed triangles, not small valid faces')
    candidates=set()
    for triangle in bad:
        found=False
        for i in range(3):
            a,b=triangle.vertices[i],triangle.vertices[(i+1)%3]
            if mesh.vertices[a].co == mesh.vertices[b].co:
                candidates.update((a,b));found=True
        if not found:
            raise RuntimeError('Pouch sliver is not the diagnosed coincident-vertex case')
    bad_count=len(bad)
    bm=bmesh.new();bm.from_mesh(mesh);bm.verts.ensure_lookup_table()
    bmesh.ops.remove_doubles(bm,verts=[bm.verts[i] for i in sorted(candidates)],dist=MERGE_DISTANCE)
    bm.to_mesh(mesh);bm.free();mesh.update();mesh.calc_loop_triangles()
    after_triangles=len(mesh.loop_triangles)
    remaining_bad=sum(t.area<=AREA_THRESHOLD for t in mesh.loop_triangles)
    removed=before_triangles-after_triangles
    if remaining_bad or removed!=bad_count:
        raise RuntimeError('Pouch cleanup changed valid faces or left degenerate triangles')
    record={'name':source_name,'beforeTriangles':before_triangles,
            'afterTriangles':after_triangles,'removedTriangles':removed,
            'mergedVertices':before_vertices-len(mesh.vertices),
            'badTrianglesBefore':bad_count,'badTrianglesAfter':remaining_bad,
            'maxWorldVertexDisplacement':0.0,
            'reason':'Clamped thin pouch-lid bevel creates pairs of locally distinct points which round to identical world FLOAT coordinates; merge only diagnosed zero-edge endpoints before UV/bake.'}
    GEOMETRY_CLEANUP.append(record)
    print('PBR_EXPORT_COPY_CLEANUP',json.dumps(record),flush=True)


def _evaluated_copies(sources,target_collection):
    global SOURCE_EVALUATED_TRIANGLES
    GEOMETRY_CLEANUP.clear();SOURCE_EVALUATED_TRIANGLES=0
    deps=bpy.context.evaluated_depsgraph_get();cache={};groups={k:[] for k in FAMILIES}
    for source in sources:
        evaluated=source.evaluated_get(deps)
        data=bpy.data.meshes.new_from_object(evaluated,preserve_all_data_layers=True,depsgraph=deps)
        if not data.polygons:
            bpy.data.meshes.remove(data);continue
        SOURCE_EVALUATED_TRIANGLES+=sum(len(p.vertices)-2 for p in data.polygons)
        _scalar_attribute(data,POINTINESS,_pointiness(data))
        local=[tuple(v.co) for v in data.vertices]
        # Generated coordinates derive from original bounding box, not the atlas.
        lo=[min(v[i] for v in local) for i in range(3)]
        hi=[max(v[i] for v in local) for i in range(3)]
        generated=[tuple((v[i]-lo[i])/max(hi[i]-lo[i],1e-8) for i in range(3)) for v in local]
        _attribute(data,LOCAL_COORD,local);_attribute(data,GENERATED_COORD,generated)
        data.transform(source.matrix_world)
        _clean_known_world_slivers(data,source.name)
        source_materials=list(data.materials)
        if not source_materials or any(m is None for m in source_materials):
            raise RuntimeError('Unassigned source material on '+source.name)
        families={_family(source_materials[p.material_index]) for p in data.polygons}
        for family in sorted(families):
            part=data.copy() if len(families)>1 else data
            if len(families)>1:
                bm=bmesh.new();bm.from_mesh(part)
                remove=[f for f in bm.faces if _family(source_materials[f.material_index])!=family]
                bmesh.ops.delete(bm,geom=remove,context='FACES')
                loose=[v for v in bm.verts if not v.link_faces]
                if loose:bmesh.ops.delete(bm,geom=loose,context='VERTS')
                bm.to_mesh(part);bm.free();part.update()
            for i,m in enumerate(source_materials):part.materials[i]=_material_copy(m,cache)
            obj=bpy.data.objects.new('BAKE '+family+' | '+source.name,part)
            obj.matrix_world=Matrix.Identity(4);target_collection.objects.link(obj)
            groups[family].append(obj)
        if len(families)>1:bpy.data.meshes.remove(data)
    return groups,list(cache.values())


def _join_unwrap(family,objects,resolution):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    if len(objects)>1:bpy.ops.object.join()
    obj=bpy.context.object;obj.name='KNIGHT PBR | '+family
    # Bake and export the same explicit triangles. Complex cap n-gons otherwise
    # make Blender's glTF exporter omit tangents needed by the normal atlas.
    triangulate=obj.modifiers.new('Delivery triangulation','TRIANGULATE')
    triangulate.quad_method='FIXED';triangulate.ngon_method='CLIP'
    if hasattr(triangulate,'keep_custom_normals'):triangulate.keep_custom_normals=True
    bpy.ops.object.modifier_apply(modifier=triangulate.name)
    obj.data.calc_loop_triangles()
    invalid=sum(t.area<=AREA_THRESHOLD for t in obj.data.loop_triangles)
    if invalid:
        raise RuntimeError(f'{family}: {invalid} world-space triangles fail the strict {AREA_THRESHOLD} area gate')
    for name,kind in ((LOCAL_COORD,'FLOAT_VECTOR'),(GENERATED_COORD,'FLOAT_VECTOR'),
                      (POINTINESS,'FLOAT')):
        attribute=obj.data.attributes.get(name)
        if attribute is None or attribute.domain!='POINT' or attribute.data_type!=kind:
            raise RuntimeError('Lost source shader attribute after join: '+name)
    # The copied geometry has no unapplied modifiers or non-identity transform.
    while obj.data.uv_layers:obj.data.uv_layers.remove(obj.data.uv_layers[0])
    obj.data.uv_layers.new(name='PBRAtlas')
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=8/resolution,
                            area_weight=.25,correct_aspect=True,scale_to_bounds=True)
    bpy.ops.object.mode_set(mode='OBJECT')
    obj.data.uv_layers.active_index=0
    obj.data.uv_layers[0].active_render=True
    return obj


def _image(name,resolution,noncolor=False):
    image=bpy.data.images.new(name,width=resolution,height=resolution,alpha=True,float_buffer=False)
    image.colorspace_settings.name='Non-Color' if noncolor else 'sRGB'
    image.generated_color=(0,0,0,1)
    return image


def _active_target(material,image):
    nodes=material.node_tree.nodes
    node=nodes.get('PBR BAKE TARGET')
    if node is None:node=nodes.new('ShaderNodeTexImage');node.name='PBR BAKE TARGET'
    node.image=image
    for n in nodes:n.select=False
    node.select=True;nodes.active=node


def _output(material):
    outputs=[n for n in material.node_tree.nodes if n.type=='OUTPUT_MATERIAL']
    return next((n for n in outputs if n.is_active_output),outputs[0])


def _principled(material):
    nodes=[n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED']
    if len(nodes)!=1:raise RuntimeError('Expected one Principled BSDF: '+material.name)
    return nodes[0]


def _emission_channel(material,input_name):
    """Route an exact BSDF input to unlit emission for a channel-only bake."""
    nodes,links=material.node_tree.nodes,material.node_tree.links
    out=_output(material);principled=_principled(material)
    saved=[link.from_socket for link in out.inputs['Surface'].links]
    emit=nodes.new('ShaderNodeEmission');emit.inputs['Strength'].default_value=1
    src=principled.inputs[input_name]
    if src.is_linked:links.new(src.links[0].from_socket,emit.inputs['Color'])
    else:
        value=src.default_value
        emit.inputs['Color'].default_value=tuple(value) if hasattr(value,'__len__') else (value,value,value,1)
    links.new(emit.outputs[0],out.inputs['Surface'])
    return (material,emit,out,saved)


def _restore(records):
    for material,node,out,saved in records:
        material.node_tree.nodes.remove(node)
        for socket in saved:material.node_tree.links.new(socket,out.inputs['Surface'])


def _save(image,path):
    image.filepath_raw=path;image.file_format='PNG';image.save()
    return {'path':os.path.basename(path),'width':image.size[0],'height':image.size[1],
            'colorSpace':image.colorspace_settings.name,'bytes':os.path.getsize(path)}


def _bake(obj,family,args,texture_dir):
    scene=bpy.context.scene
    materials=list({m.name:m for m in obj.data.materials if m}.values())
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    maps={};files=[]
    for channel,input_name,noncolor in (('BaseColor','Base Color',False),('Roughness','Roughness',True),('Metallic','Metallic',True)):
        image=_image('Knight_'+family+'_'+channel,args.resolution,noncolor)
        for m in materials:_active_target(m,image)
        records=[_emission_channel(m,input_name) for m in materials]
        print('PBR_BAKE_START',family,channel,flush=True)
        try:bpy.ops.object.bake(type='EMIT',use_clear=True,margin=6)
        finally:_restore(records)
        maps[channel]=image
        files.append(_save(image,os.path.join(texture_dir,family+'_'+channel+'.png')))
    if not args.no_normal:
        image=_image('Knight_'+family+'_Normal',args.resolution,True)
        image.generated_color=(.5,.5,1,1)
        for m in materials:_active_target(m,image)
        print('PBR_BAKE_START',family,'Normal',flush=True)
        bpy.ops.object.bake(type='NORMAL',normal_space='TANGENT',use_clear=True,margin=6)
        maps['Normal']=image
        files.append(_save(image,os.path.join(texture_dir,family+'_Normal.png')))
    # glTF metallicRoughness uses roughness in G and metallic in B. R is 1.
    import numpy as np
    count=args.resolution*args.resolution*4
    rough=np.empty(count,dtype=np.float32);metal=np.empty(count,dtype=np.float32)
    maps['Roughness'].pixels.foreach_get(rough);maps['Metallic'].pixels.foreach_get(metal)
    packed=np.ones((args.resolution*args.resolution,4),dtype=np.float32)
    packed[:,1]=rough.reshape(-1,4)[:,0];packed[:,2]=metal.reshape(-1,4)[:,0]
    orm=_image('Knight_'+family+'_ORM',args.resolution,True)
    orm.pixels.foreach_set(packed.reshape(-1));orm.update();maps['ORM']=orm
    files.append(_save(orm,os.path.join(texture_dir,family+'_ORM.png')))
    # Simple glTF-compatible material; it no longer depends on Blender procedurals.
    m=bpy.data.materials.new('Knight baked PBR | '+family);m.use_nodes=True
    nodes,links=m.node_tree.nodes,m.node_tree.links;p=nodes.get('Principled BSDF')
    uv=nodes.new('ShaderNodeUVMap');uv.uv_map='PBRAtlas'
    def texture(key):
        n=nodes.new('ShaderNodeTexImage');n.image=maps[key];n.label=key
        links.new(uv.outputs['UV'],n.inputs['Vector']);return n
    col=texture('BaseColor');links.new(col.outputs['Color'],p.inputs['Base Color'])
    orm_node=texture('ORM');separate=nodes.new('ShaderNodeSeparateColor');separate.mode='RGB'
    links.new(orm_node.outputs['Color'],separate.inputs['Color'])
    links.new(separate.outputs['Green'],p.inputs['Roughness']);links.new(separate.outputs['Blue'],p.inputs['Metallic'])
    if 'Normal' in maps:
        tex=texture('Normal');normal=nodes.new('ShaderNodeNormalMap');normal.uv_map='PBRAtlas'
        links.new(tex.outputs['Color'],normal.inputs['Color']);links.new(normal.outputs['Normal'],p.inputs['Normal'])
    obj.data.materials.clear();obj.data.materials.append(m)
    for face in obj.data.polygons:face.material_index=0
    return files


def _glb_header(path):
    with open(path,'rb') as f:
        magic,version,size=struct.unpack('<4sII',f.read(12))
        if magic!=b'glTF' or version!=2:raise RuntimeError('Invalid GLB header')
        length,kind=struct.unpack('<I4s',f.read(8))
        if kind!=b'JSON':raise RuntimeError('Missing GLB JSON chunk')
        data=json.loads(f.read(length))
    images=data.get('images',[])
    if not images or any('bufferView' not in i for i in images):raise RuntimeError('GLB does not embed all baked textures')
    return {'bytes':size,'meshes':len(data.get('meshes',[])),'materials':len(data.get('materials',[])),
            'embeddedImages':len(images),'animations':len(data.get('animations',[])),
            'hasNormals':all('NORMAL' in p['attributes'] for m in data['meshes'] for p in m['primitives']),
            'hasUVs':all('TEXCOORD_0' in p['attributes'] for m in data['meshes'] for p in m['primitives']),
            'hasTangents':all('TANGENT' in p['attributes'] for m in data['meshes'] for p in m['primitives'])}


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('root');parser.add_argument('--resolution',type=int,default=2048)
    parser.add_argument('--no-normal',action='store_true');parser.add_argument('--samples',type=int,default=8)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    if args.resolution<256 or args.resolution>8192:parser.error('--resolution must be between 256 and 8192')
    start=time.time();out=os.path.join(os.path.abspath(args.root),'assets/models/knight_refined_v4')
    source=os.path.join(out,'knight_refined_v4.blend');source_hash=_hash(source)
    bpy.ops.wm.open_mainfile(filepath=source)
    collection=bpy.data.collections.get('KNIGHT | editable components')
    if collection is None:raise RuntimeError('Missing KNIGHT editable component collection')
    sources=[o for o in collection.all_objects if o.type=='MESH' and not o.hide_render and not o.hide_get() and o.visible_get()]
    if not sources:raise RuntimeError('No visible neutral knight meshes')
    export_collection=bpy.data.collections.new('BAKED EXPORT | neutral knight')
    bpy.context.scene.collection.children.link(export_collection)
    groups,source_materials=_evaluated_copies(sources,export_collection)
    source_names=[o.name for o in sources]
    for obj in list(bpy.context.scene.objects):
        if obj not in export_collection.objects[:]:obj.hide_render=True;obj.hide_set(True)
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU'
    scene.cycles.samples=args.samples;scene.render.threads_mode='FIXED';scene.render.threads=8
    scene.render.bake.use_selected_to_active=False;scene.render.bake.target='IMAGE_TEXTURES'
    scene.render.bake.margin=6;scene.render.bake.use_clear=True
    texture_dir=os.path.join(out,'textures');os.makedirs(texture_dir,exist_ok=True)
    baked=[];files=[]
    for family in FAMILIES:
        if not groups[family]:continue
        obj=_join_unwrap(family,groups[family],args.resolution);baked.append(obj)
        files.extend(_bake(obj,family,args,texture_dir))
    bpy.ops.object.select_all(action='DESELECT')
    for o in baked:o.hide_render=False;o.hide_set(False);o.select_set(True)
    bpy.context.view_layer.objects.active=baked[0]
    temporary=os.path.join(out,'knight_refined_v4.baking.glb')
    settings=dict(filepath=temporary,export_format='GLB',use_selection=True,
                  export_apply=True,export_texcoords=True,export_normals=True,export_tangents=True,
                  export_materials='EXPORT',export_image_format='AUTO',export_cameras=False,
                  export_lights=False,export_animations=False,export_extras=True)
    supported=set(bpy.ops.export_scene.gltf.get_rna_type().properties.keys())
    bpy.ops.export_scene.gltf(**{k:v for k,v in settings.items() if k in supported})
    result=_glb_header(temporary)
    if not result['hasUVs'] or not result['hasNormals'] or (not args.no_normal and not result['hasTangents']):
        raise RuntimeError('Export is missing UVs, normals, or tangent basis')
    if _hash(source)!=source_hash:raise RuntimeError('Editable source unexpectedly changed')
    destination=os.path.join(out,'knight_refined_v4.glb');os.replace(temporary,destination)
    stats={'task':'ART-007D','source':os.path.relpath(source,args.root),'sourceSha256':source_hash,
           'sourceUnchanged':True,'sourceObjects':len(sources),'sourceObjectNames':source_names,
           'bakedMeshes':len(baked),'resolution':args.resolution,'bakedShaderNormal':not args.no_normal,
           'vertices':sum(len(o.data.vertices) for o in baked),
           'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in baked),
           'textureFiles':files,'glb':result,'elapsedSeconds':round(time.time()-start,2),
           'sourceShaderPreservation':{'coordinates':[LOCAL_COORD,GENERATED_COORD],
                                      'pointinessAttribute':POINTINESS,'pointinessMethod':POINTINESS_METHOD,
                                      'pointinessReference':POINTINESS_SOURCE,
                                      'proceduralNodes':'Noise, Voronoi, Wave, color ramps and bump copied unchanged'},
           'limitations':['Static neutral model; no rig or animation.',
                          'UV atlases are generated for delivery; editable source geometry and shaders remain in the source blend.',
                          'Core glTF PBR preserves baked base color, roughness, metallic and tangent normals; procedural fiber sheen, anisotropy and leather clearcoat are not separate exported extensions.']}
    removed=sum(item['removedTriangles'] for item in GEOMETRY_CLEANUP)
    stats['geometryCleanup']={'sourceSha256':source_hash,
                              'beforeTriangles':SOURCE_EVALUATED_TRIANGLES,
                              'afterTriangles':stats['triangles'],'removedTriangles':removed,
                              'areaThreshold':AREA_THRESHOLD,'mergeDistance':MERGE_DISTANCE,
                              'sourceObjects':list(GEOMETRY_CLEANUP)}
    if stats['triangles']!=SOURCE_EVALUATED_TRIANGLES-removed:
        raise RuntimeError('Export triangle delta does not match documented local cleanup')
    with open(os.path.join(out,'pbr_export_report.json'),'w',encoding='utf-8') as f:json.dump(stats,f,ensure_ascii=False,indent=2)
    print('KNIGHT_PBR_EXPORT_COMPLETE',json.dumps({k:v for k,v in stats.items() if k not in ('sourceObjectNames','textureFiles')},ensure_ascii=False),flush=True)


if __name__=='__main__':main()
