"""Read-only validation of the finished ART-007 editable Blender source.

Blender -b --python-exit-code 1 --python tools/validate_knight_refined.py -- REPOSITORY
The supplied source is reopened independently and never saved or modified.
"""
import bpy
import datetime
import hashlib
import json
import math
import os
import sys


root = os.path.abspath(sys.argv[sys.argv.index('--')+1])
folder = os.path.join(root, 'assets', 'models', 'knight_refined_v4')
source = os.path.join(folder, 'knight_refined_v4.blend')
destination = os.path.join(folder, 'validation.json')


def sha256(path):
    with open(path, 'rb') as handle:
        return hashlib.sha256(handle.read()).hexdigest()


source_before = {'bytes': os.path.getsize(source), 'sha256': sha256(source),
                 'mtime_ns': os.stat(source).st_mtime_ns}
bpy.ops.wm.open_mainfile(filepath=source)
depsgraph = bpy.context.evaluated_depsgraph_get()
errors = []
warnings = []


def check(condition, description):
    if not condition:
        errors.append(description)


def object_geometry(obj):
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    try:
        mesh.calc_loop_triangles()
        coords = [tuple(obj.matrix_world @ vertex.co) for vertex in mesh.vertices]
        finite = all(math.isfinite(value) for point in coords for value in point)
        invalid_faces = []
        zero_polygons = []
        bad_normals = []
        invalid_materials = []
        for polygon in mesh.polygons:
            indices = list(polygon.vertices)
            if (len(indices) < 3 or len(set(indices)) < 3 or
                    any(index < 0 or index >= len(mesh.vertices) for index in indices)):
                invalid_faces.append(polygon.index)
            if polygon.area <= 1e-12:
                zero_polygons.append(polygon.index)
            if not all(math.isfinite(value) for value in polygon.normal):
                bad_normals.append(polygon.index)
            if (polygon.material_index >= len(mesh.materials) or
                    mesh.materials[polygon.material_index] is None):
                invalid_materials.append(polygon.index)
        zero_triangles = []
        exactly_zero_triangles = 0
        for index, triangle in enumerate(mesh.loop_triangles):
            a,b,c = (mesh.vertices[i].co for i in triangle.vertices)
            area = (b-a).cross(c-a).length*.5
            exactly_zero_triangles += area == 0
            if area <= 1e-12:
                zero_triangles.append(index)
        # Boundaries on an intentionally thin trim mesh are informational;
        # disconnected parts are expected for this separately editable assembly.
        edges = {}
        for polygon in mesh.polygons:
            indices = list(polygon.vertices)
            for a,b in zip(indices, indices[1:]+indices[:1]):
                edge = tuple(sorted((a,b)))
                edges[edge] = edges.get(edge, 0)+1
        metrics = {
            'object': obj.name,
            'vertices': len(mesh.vertices),
            'polygons': len(mesh.polygons),
            'triangles': len(mesh.loop_triangles),
            'finite_coordinates': finite,
            'invalid_face_indices': invalid_faces,
            'zero_area_polygon_indices': zero_polygons,
            'zero_area_triangle_indices': zero_triangles,
            'exactly_zero_area_triangles': exactly_zero_triangles,
            'invalid_normal_polygon_indices': bad_normals,
            'invalid_material_polygon_indices': invalid_materials,
            'boundary_edges': sum(count == 1 for count in edges.values()),
            'edges_with_more_than_two_faces': sum(count > 2 for count in edges.values()),
            'bounds': {'min': [min(p[i] for p in coords) for i in range(3)],
                       'max': [max(p[i] for p in coords) for i in range(3)]},
            'materials': [material.name if material else None for material in mesh.materials],
            'hidden_render': obj.hide_render,
            'hidden_viewport': obj.hide_get(),
            'hand_side': obj.get('hand_side'),
            'gripping': obj.get('gripping'),
            'reference_sheathed_sword': bool(obj.get('reference_sheathed_sword')),
        }
        check(finite, obj.name+': non-finite coordinates')
        check(not invalid_faces, obj.name+': malformed polygon index data')
        check(not zero_polygons, obj.name+': '+str(len(zero_polygons))+' evaluated polygons at or below area 1e-12')
        check(not zero_triangles, obj.name+': '+str(len(zero_triangles))+' evaluated triangles at or below area 1e-12')
        check(not bad_normals, obj.name+': non-finite evaluated normals')
        check(not invalid_materials, obj.name+': faces refer to absent material slots')
        return metrics
    finally:
        evaluated.to_mesh_clear()


knight = bpy.data.collections.get('KNIGHT | editable components')
alternate = bpy.data.collections.get('ALTERNATE | drawn sword presentation')
check(knight is not None, 'Missing KNIGHT collection')
check(alternate is not None, 'Missing ALTERNATE collection')
neutral_objects = [obj for obj in knight.all_objects if obj.type == 'MESH'] if knight else []
alternate_objects = [obj for obj in alternate.all_objects if obj.type == 'MESH'] if alternate else []
check(len(neutral_objects) >= 100,
      'Expected the separately editable knight assembly with at least 100 neutral mesh objects; got '+str(len(neutral_objects)))
check(len(alternate_objects) >= 4,
      'Expected alternate gripping-hand and independent drawn-sword meshes; got '+str(len(alternate_objects)))
neutral_metrics = [object_geometry(obj) for obj in neutral_objects]
alternate_metrics = [object_geometry(obj) for obj in alternate_objects]

check(all(not obj.hide_render and not obj.hide_get() for obj in neutral_objects),
      'Some neutral character mesh objects are hidden in the saved default state')
check(all(obj.hide_render and obj.hide_get() for obj in alternate_objects),
      'Some ALTERNATE objects are visible in the saved default state')
sheathed = [obj for obj in neutral_objects if obj.get('reference_sheathed_sword')]
check(len(sheathed) == 6, 'Expected six tagged visible sheathed-sword components; got '+str(len(sheathed)))
check(all(not obj.hide_render and not obj.hide_get() for obj in sheathed),
      'Some tagged sheathed-sword components are hidden')
right_relaxed = [obj for obj in neutral_objects if obj.get('hand_side') == -1]
left_relaxed = [obj for obj in neutral_objects if obj.get('hand_side') == 1]
right_gripping = [obj for obj in alternate_objects if obj.get('hand_side') == -1]
check(len(right_relaxed) >= 3, 'Missing neutral right-glove assembly; got '+str(len(right_relaxed)))
check(len(left_relaxed) >= 3, 'Missing neutral left-glove assembly; got '+str(len(left_relaxed)))
check(all(obj.get('gripping') is False for obj in right_relaxed),
      'A default right-hand component is marked gripping')
check(all(obj.get('gripping') is False for obj in left_relaxed),
      'A default left-hand component is marked gripping')
check(len(right_gripping) >= 1, 'Missing alternate gripping-glove assembly')
check(all(obj.get('gripping') is True for obj in right_gripping),
      'An alternate right-hand component is not marked gripping')
blades = [obj for obj in alternate_objects if 'Diamond ground steel blade' in obj.name]
guards = [obj for obj in alternate_objects if 'Curved flat brass sword guard' in obj.name]
grips = [obj for obj in alternate_objects if 'Held leather sword grip' in obj.name]
check(bool(blades) and bool(guards) and bool(grips),
      'ALTERNATE is missing the independent drawn blade, guard or grip')

used_materials = sorted({material for obj in neutral_objects+alternate_objects
                         for material in obj.data.materials if material}, key=lambda m:m.name)
material_report = []
for material in used_materials:
    nodes = material.node_tree.nodes if material.node_tree else []
    pbr = [node for node in nodes if node.type == 'BSDF_PRINCIPLED']
    check(bool(pbr), material.name+': absent Principled shader')
    material_report.append({'name':material.name, 'nodes':len(nodes),
        'principled_bsdf':bool(pbr),
        'procedural_source':any(node.type in ('TEX_NOISE','TEX_VORONOI','TEX_WAVE') for node in nodes),
        'image_nodes':[node.image.name if node.image else None for node in nodes if node.type == 'TEX_IMAGE']})

external_files = []
for kind, datablocks in [('image',bpy.data.images),('font',bpy.data.fonts),
                         ('library',bpy.data.libraries),('sound',bpy.data.sounds),
                         ('movieclip',bpy.data.movieclips),('cache',bpy.data.cache_files)]:
    for item in datablocks:
        raw = getattr(item, 'filepath', '')
        packed = bool(getattr(item, 'packed_file', None) or getattr(item, 'packed_files', []))
        generated = kind == 'image' and item.source in ('GENERATED','VIEWER')
        builtin = raw in ('','<builtin>','<builtin font>')
        resolved = bpy.path.abspath(raw, library=getattr(item, 'library', None)) if raw else ''
        present = packed or generated or builtin or os.path.exists(resolved)
        external_files.append({'type':kind,'name':item.name,'stored_path':raw,
                               'resolved_path':resolved,'packed':packed,
                               'generated_or_builtin':generated or builtin,'present':present})
        check(present, kind+' '+item.name+': missing external file '+resolved)

source_after = {'bytes': os.path.getsize(source), 'sha256': sha256(source),
                'mtime_ns': os.stat(source).st_mtime_ns}
check(source_before == source_after, 'Source file changed during validation')
report = {
    'status': 'PASS' if not errors else 'FAIL',
    'task': 'ART-007',
    'validated_at_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
    'blender_version':bpy.app.version_string,
    'source':os.path.relpath(source,root),
    'source_before':source_before, 'source_after':source_after,
    'source_unchanged':source_before == source_after,
    'summary':{
        'neutral_mesh_objects':len(neutral_objects),
        'neutral_evaluated_triangles':sum(item['triangles'] for item in neutral_metrics),
        'alternate_mesh_objects':len(alternate_objects),
        'alternate_evaluated_triangles':sum(item['triangles'] for item in alternate_metrics),
        'finite_coordinates':all(item['finite_coordinates'] for item in neutral_metrics+alternate_metrics),
        'invalid_polygons':sum(len(item['invalid_face_indices']) for item in neutral_metrics+alternate_metrics),
        'zero_area_polygons':sum(len(item['zero_area_polygon_indices']) for item in neutral_metrics+alternate_metrics),
        'zero_area_triangles':sum(len(item['zero_area_triangle_indices']) for item in neutral_metrics+alternate_metrics),
        'exactly_zero_area_triangles':sum(item['exactly_zero_area_triangles'] for item in neutral_metrics+alternate_metrics),
        'zero_area_reporting_threshold':1e-12,
        'bounds':{'min':[min(item['bounds']['min'][i] for item in neutral_metrics) for i in range(3)],
                  'max':[max(item['bounds']['max'][i] for item in neutral_metrics) for i in range(3)]},
        'used_materials':len(used_materials),
        'missing_external_files':sum(not item['present'] for item in external_files),
        'armatures':len([obj for obj in bpy.data.objects if obj.type == 'ARMATURE']),
        'actions':len(bpy.data.actions),
    },
    'default_pose':{'neutral_all_visible':all(not obj.hide_render and not obj.hide_get() for obj in neutral_objects),
        'alternate_all_hidden':all(obj.hide_render and obj.hide_get() for obj in alternate_objects),
        'sheathed_sword_visible_components':[obj.name for obj in sheathed],
        'right_relaxed_hand_components':[obj.name for obj in right_relaxed],
        'left_relaxed_hand_components':[obj.name for obj in left_relaxed],
        'right_gripping_hand_components':[obj.name for obj in right_gripping],
        'drawn_blade_components':[obj.name for obj in blades+guards+grips],
        'natural_pose_validation_scope':'Saved geometry flags and visibility verified; aesthetic hand pose requires rendered-image review'},
    'materials':material_report,
    'external_files':external_files,
    'neutral_objects':neutral_metrics,
    'alternate_objects':alternate_metrics,
    'errors':errors,
    'warnings':warnings,
    'limits':['Geometry checks do not certify collision-free articulation, rigging, animation or exact conceptual similarity.',
              'Thin component boundary edges are recorded per object and are not automatically errors.'],
}
with open(destination,'w',encoding='utf-8') as handle:
    json.dump(report,handle,ensure_ascii=False,indent=2)
print(json.dumps({'status':report['status'],'summary':report['summary'],'errors':errors,
                  'source_unchanged':report['source_unchanged'],'report':destination},ensure_ascii=False))
if errors:
    raise RuntimeError('Knight source validation failed; see validation.json for exact objects')
