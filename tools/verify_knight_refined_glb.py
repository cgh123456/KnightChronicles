"""ART-007: independently inspect and re-import the delivered neutral PBR GLB.

Blender -b --python-exit-code 1 --python tools/verify_knight_refined_glb.py -- ROOT
Optional: --render --device CPU --resolution 1000 --samples 64

No rendering occurs without --render. GPU additionally requires --device GPU;
the orchestrator must authorize its use when no other GPU job is active.
The editable source and GLB are opened read-only and never saved. Only the
verification JSON and optional glb_material_preview.png are written.
"""
import argparse
import datetime
import hashlib
import json
import math
import os
import struct
import sys

import bpy


FAMILIES = ('steel', 'fabric', 'leather', 'brass')
ATTRIBUTES = {'POSITION': ('VEC3', 3), 'NORMAL': ('VEC3', 3),
              'TANGENT': ('VEC4', 4), 'TEXCOORD_0': ('VEC2', 2)}
ERRORS = []


def check(condition, description):
    if not condition:
        ERRORS.append(description)


def sha256(path):
    digest = hashlib.sha256()
    with open(path, 'rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()


def fingerprint(path):
    return {'bytes': os.path.getsize(path), 'sha256': sha256(path),
            'mtime_ns': os.stat(path).st_mtime_ns}


def read_glb(path):
    with open(path, 'rb') as stream:
        raw = stream.read()
    if len(raw) < 20:
        raise RuntimeError('Truncated GLB header')
    magic, version, declared_size = struct.unpack_from('<4sII', raw)
    check(magic == b'glTF' and version == 2, 'Expected glTF 2 binary header')
    check(declared_size == len(raw), 'GLB declared length does not match file length')
    chunks = []
    cursor = 12
    while cursor < len(raw):
        if cursor + 8 > len(raw):
            raise RuntimeError('Truncated GLB chunk header')
        size, kind = struct.unpack_from('<I4s', raw, cursor)
        cursor += 8
        if cursor + size > len(raw):
            raise RuntimeError('GLB chunk extends beyond file')
        chunks.append((kind, raw[cursor:cursor + size]))
        cursor += size
    if not chunks or chunks[0][0] != b'JSON':
        raise RuntimeError('First GLB chunk is not JSON')
    document = json.loads(chunks[0][1].decode('utf-8').rstrip('\0 \t\r\n'))
    binaries = [chunk for kind, chunk in chunks if kind == b'BIN\0']
    if len(binaries) != 1:
        raise RuntimeError('Expected exactly one embedded BIN chunk')
    check(len(document.get('buffers', [])) == 1, 'Expected one embedded GLB buffer')
    check(all('uri' not in buffer for buffer in document.get('buffers', [])),
          'GLB references an external buffer')
    return document, binaries[0]


def view_bytes(document, binary, index):
    view = document['bufferViews'][index]
    if view.get('buffer', 0) != 0:
        raise RuntimeError('Buffer view uses an unsupported external buffer')
    start = view.get('byteOffset', 0)
    end = start + view['byteLength']
    if start < 0 or end > len(binary):
        raise RuntimeError('Invalid buffer-view byte range')
    return binary[start:end]


def accessor_values(document, binary, index):
    accessor = document['accessors'][index]
    if 'sparse' in accessor or 'bufferView' not in accessor:
        raise RuntimeError('Expected a dense explicit delivery accessor')
    formats = {5120: ('b', 1), 5121: ('B', 1), 5122: ('h', 2),
               5123: ('H', 2), 5125: ('I', 4), 5126: ('f', 4)}
    widths = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4}
    fmt, component_size = formats[accessor['componentType']]
    components = widths[accessor['type']]
    width = component_size * components
    view = document['bufferViews'][accessor['bufferView']]
    stride = view.get('byteStride', width)
    data = view_bytes(document, binary, accessor['bufferView'])
    offset = accessor.get('byteOffset', 0)
    count = accessor['count']
    if stride < width or offset < 0 or (count and offset + (count - 1) * stride + width > len(data)):
        raise RuntimeError('Accessor exceeds its buffer view')
    unpacker = struct.Struct('<' + fmt * components)
    return [unpacker.unpack_from(data, offset + row * stride) for row in range(count)]


def texture_image(document, reference):
    return document['textures'][reference['index']]['source']


def inspect_cleanup(export_report, source_validation, source_hash):
    """Accept only an explicit audited export-copy cleanup, never a tolerance
    relaxation or an unexplained difference from the immutable source."""
    expected = source_validation['summary']['neutral_evaluated_triangles']
    check(export_report.get('sourceSha256') == source_hash,
          'Export report source hash does not describe the frozen source')
    check(export_report.get('sourceUnchanged') is True,
          'Export report does not confirm an unchanged editable source')
    cleanup = export_report.get('geometryCleanup')
    if cleanup is None:
        check(export_report.get('triangles') == expected,
              'Export triangle count differs from source without an explicit cleanup record')
        return {'removedTriangles':0,'beforeTriangles':expected,'afterTriangles':expected,
                'sourceObjects':[],'areaThreshold':1e-12,'sourceSha256':source_hash}
    removed = cleanup.get('removedTriangles')
    check(isinstance(removed,int) and removed >= 0, 'Invalid audited cleanup triangle reduction')
    if not isinstance(removed,int) or removed < 0:
        removed = 0
    check(cleanup.get('sourceSha256') == source_hash, 'Cleanup record source hash mismatch')
    check(cleanup.get('areaThreshold') == 1e-12, 'Cleanup record changed the strict triangle area threshold')
    check(0 < cleanup.get('mergeDistance',0) <= 1e-8,
          'Cleanup merge distance exceeds the documented float-equality repair scope')
    check(cleanup.get('beforeTriangles') == expected, 'Cleanup before-count does not match source validation')
    check(cleanup.get('afterTriangles') == expected-removed,
          'Cleanup count does not satisfy source triangles minus removed triangles')
    check(export_report.get('triangles') == expected-removed,
          'Export report final count does not match its cleanup record')
    source_counts = {item['object']:item['triangles'] for item in source_validation['neutral_objects']}
    details = cleanup.get('sourceObjects',[])
    check(bool(details) or removed == 0, 'Cleanup removed triangles without identifying source objects')
    check(sum(item.get('removedTriangles',0) for item in details) == removed,
          'Per-object cleanup records do not sum to the declared reduction')
    check(len({item.get('name') for item in details}) == len(details), 'Duplicate per-object cleanup records')
    for item in details:
        check(item.get('name') in source_counts, 'Cleanup names an object outside the frozen source')
        check(item.get('beforeTriangles') == source_counts.get(item.get('name')),
              'Cleanup object before-count does not match the source validation object')
        check(item.get('beforeTriangles',0)-item.get('afterTriangles',0) == item.get('removedTriangles'),
              'Per-object cleanup does not have consistent triangle counts')
        check(item.get('mergedVertices',0) > 0 and bool(item.get('reason')),
              'Cleanup has no vertex-merge evidence or reason')
    return cleanup


def inspect_binary(document, binary, source_validation, cleanup):
    meshes = document.get('meshes', [])
    materials = document.get('materials', [])
    images = document.get('images', [])
    check(len(meshes) == 4, 'Expected four material-family delivery meshes')
    check(len(materials) == 4, 'Expected four PBR materials')
    check(len(images) == 12, 'Expected twelve embedded PBR images')
    check(not document.get('animations'), 'Static GLB unexpectedly contains animation')
    check(not document.get('skins'), 'Static GLB unexpectedly contains a skin')
    image_report = []
    for index, item in enumerate(images):
        check('bufferView' in item and 'uri' not in item, 'Image '+str(index)+' is not embedded')
        if 'bufferView' not in item:
            continue
        data = view_bytes(document, binary, item['bufferView'])
        check(item.get('mimeType') == 'image/png', 'Expected a lossless PNG atlas '+str(index))
        if data[:8] != b'\x89PNG\r\n\x1a\n' or data[12:16] != b'IHDR':
            raise RuntimeError('Embedded image '+str(index)+' is not a valid PNG header')
        dimensions = struct.unpack_from('>II', data, 16)
        check(dimensions == (2048, 2048), 'Image '+str(index)+' is not 2048 by 2048')
        image_report.append({'index': index, 'name': item.get('name'), 'size': list(dimensions),
                             'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()})
    material_report = []
    used_images = set()
    for material in materials:
        name = material.get('name', '')
        check(any(name.endswith(family) for family in FAMILIES), 'Unexpected delivery material '+name)
        pbr = material.get('pbrMetallicRoughness', {})
        references = {'BaseColor': pbr.get('baseColorTexture'),
                      'ORM': pbr.get('metallicRoughnessTexture'),
                      'Normal': material.get('normalTexture')}
        indices = {}
        for channel, reference in references.items():
            check(reference is not None, name+': absent '+channel+' texture')
            if reference is None:
                continue
            image = texture_image(document, reference)
            check(0 <= image < len(images), name+': invalid '+channel+' image reference')
            check(reference.get('texCoord', 0) == 0, name+': unexpected UV channel')
            indices[channel] = image
            used_images.add(image)
        check(len(set(indices.values())) == 3, name+': PBR channels do not use three independent images')
        material_report.append({'material': name, 'images': indices})
    check(used_images == set(range(12)), 'The four materials do not reference all twelve embedded images')
    primitive_report = []
    triangle_count = 0
    for mesh in meshes:
        for primitive in mesh.get('primitives', []):
            name = mesh.get('name', '<unnamed>')
            check(primitive.get('mode', 4) == 4, name+': expected triangle primitives')
            attrs = primitive.get('attributes', {})
            check(all(key in attrs for key in ATTRIBUTES), name+': missing POSITION/NORMAL/TANGENT/TEXCOORD_0')
            check('indices' in primitive, name+': expected explicit triangle indices')
            check(0 <= primitive.get('material', -1) < len(materials), name+': invalid material reference')
            values = {}
            for key, (kind, width) in ATTRIBUTES.items():
                if key not in attrs:
                    continue
                accessor = document['accessors'][attrs[key]]
                check(accessor['type'] == kind and accessor['componentType'] == 5126,
                      name+': invalid '+key+' accessor type')
                values[key] = accessor_values(document, binary, attrs[key])
                check(all(all(math.isfinite(value) for value in row) for row in values[key]),
                      name+': non-finite '+key+' values')
            count = len(values.get('POSITION', []))
            check(count > 0, name+': empty positions')
            check(all(len(rows) == count for rows in values.values()), name+': inconsistent attribute counts')
            if 'NORMAL' in values:
                check(all(sum(x*x for x in row) > .5 for row in values['NORMAL']), name+': zero or collapsed normals')
            if 'TANGENT' in values:
                check(all(sum(x*x for x in row[:3]) > .5 and abs(abs(row[3])-1) < 1e-5
                          for row in values['TANGENT']), name+': invalid tangent basis')
            indices = [row[0] for row in accessor_values(document, binary, primitive['indices'])] if 'indices' in primitive else []
            check(len(indices) % 3 == 0, name+': indices are not complete triangles')
            check(all(0 <= index < count for index in indices), name+': triangle index out of range')
            triangles = len(indices) // 3
            triangle_count += triangles
            primitive_report.append({'mesh': name, 'vertices': count, 'triangles': triangles,
                                     'attributes': sorted(attrs), 'material': primitive.get('material')})
    source_triangles = source_validation['summary']['neutral_evaluated_triangles']
    expected_triangles = source_triangles-cleanup['removedTriangles']
    check(triangle_count == expected_triangles,
          'GLB triangles '+str(triangle_count)+' differ from source-minus-audited-cleanup '+str(expected_triangles))
    return {'meshCount': len(meshes), 'materialCount': len(materials), 'embeddedImageCount': len(images),
            'triangles': triangle_count, 'sourceTriangles': source_triangles,
            'auditedRemovedTriangles':cleanup['removedTriangles'],'expectedDeliveryTriangles':expected_triangles,
            'images': image_report, 'materials': material_report, 'primitives': primitive_report}


def upstream_images(socket, visited=None):
    visited = set() if visited is None else visited
    result = set()
    for link in socket.links:
        node = link.from_node
        if node in visited:
            continue
        visited.add(node)
        if node.type == 'TEX_IMAGE' and node.image:
            result.add(node.image)
        else:
            for input_socket in node.inputs:
                result.update(upstream_images(input_socket, visited))
    return result


def inspect_import(objects, binary_report, source_validation):
    meshes = [obj for obj in objects if obj.type == 'MESH']
    check(len(meshes) == 4, 'Blender did not re-import four delivery meshes')
    materials = {material for obj in meshes for material in obj.data.materials if material}
    check(len(materials) == 4, 'Blender imported an unexpected number of mesh materials')
    coords = [];total_vertices = 0;total_triangles = 0;mesh_report = []
    for obj in meshes:
        mesh = obj.data;mesh.calc_loop_triangles()
        points = [tuple(obj.matrix_world @ vertex.co) for vertex in mesh.vertices]
        finite = all(math.isfinite(component) for point in points for component in point)
        zero = sum(triangle.area <= 1e-12 for triangle in mesh.loop_triangles)
        check(finite, obj.name+': Blender imported non-finite coordinates')
        check(zero == 0, obj.name+': Blender imported zero-area triangles')
        check(bool(mesh.uv_layers), obj.name+': Blender imported no UVs')
        check(all(face.material_index < len(mesh.materials) and mesh.materials[face.material_index]
                  for face in mesh.polygons), obj.name+': Blender imported invalid face materials')
        coords.extend(points);total_vertices += len(mesh.vertices);total_triangles += len(mesh.loop_triangles)
        mesh_report.append({'mesh': obj.name, 'vertices': len(mesh.vertices),
                            'triangles': len(mesh.loop_triangles), 'finite': finite,
                            'zeroAreaTriangles': zero, 'uvLayers': [layer.name for layer in mesh.uv_layers]})
    check(total_triangles == binary_report['triangles'], 'Blender import changed GLB triangle count')
    bounds = {'min': [min(point[i] for point in coords) for i in range(3)],
              'max': [max(point[i] for point in coords) for i in range(3)]} if coords else None
    expected = source_validation['summary']['bounds']
    if bounds:
        check(all(abs(bounds[edge][axis]-expected[edge][axis]) < 1e-4
                  for edge in ('min','max') for axis in range(3)), 'Imported bounds differ from frozen source')
    texture_report = [];material_report = [];seen_images = set()
    for material in sorted(materials, key=lambda item: item.name):
        nodes = material.node_tree.nodes if material.node_tree else []
        shaders = [node for node in nodes if node.type == 'BSDF_PRINCIPLED']
        check(len(shaders) == 1, material.name+': no unique imported Principled shader')
        if not shaders:
            continue
        shader = shaders[0]
        connections = {key: upstream_images(shader.inputs[key])
                       for key in ('Base Color','Roughness','Metallic','Normal')}
        check(all(len(images) == 1 for images in connections.values()),
              material.name+': a PBR channel is not connected to exactly one image')
        check(connections['Roughness'] == connections['Metallic'], material.name+': ORM channels use different images')
        check(bool([node for node in nodes if node.type == 'NORMAL_MAP' and node.space == 'TANGENT']),
              material.name+': missing tangent-space Normal Map node')
        material_report.append({'material': material.name,
            'connectedImages': {key: sorted(image.name for image in images) for key,images in connections.items()}})
        for image in set().union(*connections.values()):
            if image in seen_images:
                continue
            seen_images.add(image)
            check(tuple(image.size) == (2048,2048), image.name+': Blender image is not 2K')
            check(bool(image.packed_file or image.packed_files), image.name+': imported embedded image is not packed')
            check(image.has_data, image.name+': imported image has no decoded pixels')
            import numpy as np
            pixels = np.empty(len(image.pixels), dtype=np.float32)
            image.pixels.foreach_get(pixels)
            samples = pixels.reshape(-1,4)[::max(1,(len(pixels)//4)//8192),:3]
            check(bool(np.isfinite(samples).all()), image.name+': non-finite decoded pixels')
            check(float(samples.max()) > .05, image.name+': decoded RGB image is empty')
            texture_report.append({'name':image.name,'size':list(image.size),
                'colorSpace':image.colorspace_settings.name,'packed':bool(image.packed_file or image.packed_files),
                'sampledRgbMin':samples.min(axis=0).tolist(),'sampledRgbMax':samples.max(axis=0).tolist(),
                'sampledRgbStd':samples.std(axis=0).tolist()})
    check(len(seen_images) == 12, 'Blender material connections do not use twelve independent images')
    return {'importedMeshes':len(meshes),'vertices':total_vertices,'triangles':total_triangles,
            'bounds':bounds,'height':bounds['max'][2]-bounds['min'][2] if bounds else None,
            'meshes':mesh_report,'materialConnections':material_report,'materialTextures':texture_report}


def load_studio(source):
    bpy.ops.wm.open_mainfile(filepath=source)
    scene = bpy.context.scene
    # Keep only the source's real studio floor, cameras and lights. Delete all
    # source knight/alternate geometry so preview pixels can only show the GLB.
    kept = []
    for obj in list(bpy.data.objects):
        if obj.type in ('CAMERA','LIGHT') or (obj.type == 'MESH' and obj.name.startswith('STUDIO |')):
            kept.append(obj.name)
        else:
            bpy.data.objects.remove(obj, do_unlink=True)
    check(scene.camera is not None, 'Source studio is missing its camera')
    check(any(obj.type == 'LIGHT' for obj in scene.objects), 'Source studio is missing its lights')
    check(any(obj.type == 'MESH' and obj.name.startswith('STUDIO |') for obj in scene.objects),
          'Source studio is missing its floor')
    return kept


def configure_render(args, destination):
    scene = bpy.context.scene;scene.render.engine = 'CYCLES'
    scene.cycles.device = args.device
    if args.device == 'GPU':
        preferences = bpy.context.preferences.addons['cycles'].preferences
        selected = False
        for backend in ('METAL','OPTIX','CUDA','HIP','ONEAPI'):
            try:
                preferences.compute_device_type = backend;preferences.get_devices()
                usable = [device for device in preferences.devices if device.type == backend]
                if usable:
                    for device in preferences.devices:
                        device.use = device.type == backend
                    selected = True;break
            except (TypeError, RuntimeError):
                continue
        if not selected:
            raise RuntimeError('Explicit GPU rendering requested but no supported GPU is available')
    scene.cycles.samples = args.samples;scene.cycles.use_denoising = True
    scene.render.resolution_x = scene.render.resolution_y = args.resolution
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG';scene.render.image_settings.color_mode = 'RGBA'
    scene.render.filepath = destination
    bpy.ops.render.render(write_still=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('root');parser.add_argument('--render',action='store_true')
    parser.add_argument('--device',choices=('CPU','GPU'),default='CPU')
    parser.add_argument('--resolution',type=int,default=1000)
    parser.add_argument('--samples',type=int,default=64)
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    if not 128 <= args.resolution <= 4096 or args.samples < 1:
        parser.error('Invalid resolution or sample count')
    folder = os.path.join(os.path.abspath(args.root),'assets/models/knight_refined_v4')
    source = os.path.join(folder,'knight_refined_v4.blend')
    glb = os.path.join(folder,'knight_refined_v4.glb')
    validation_path = os.path.join(folder,'validation.json')
    export_report_path = os.path.join(folder,'pbr_export_report.json')
    destination = os.path.join(folder,'glb_verification.json')
    preview = os.path.join(folder,'glb_material_preview.png')
    before = fingerprint(source);glb_before = fingerprint(glb)
    with open(validation_path,encoding='utf-8') as stream:
        validation = json.load(stream)
    with open(export_report_path,encoding='utf-8') as stream:
        export_report = json.load(stream)
    check(export_report.get('glb',{}).get('bytes') == glb_before['bytes'],
          'Export report byte count does not describe the current GLB')
    check(validation.get('status') == 'PASS', 'Frozen source validation did not pass')
    check(validation.get('source_after',{}).get('sha256') == before['sha256'],
          'Source validation hash does not describe the current frozen source')
    check(validation.get('source_unchanged') is True, 'Source validation did not confirm a read-only pass')
    document,binary = read_glb(glb)
    cleanup = inspect_cleanup(export_report,validation,before['sha256'])
    binary_report = inspect_binary(document,binary,validation,cleanup)
    studio = load_studio(source) if args.render else []
    if not args.render:
        bpy.ops.wm.read_factory_settings(use_empty=True)
    previous = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=glb)
    imported = [obj for obj in bpy.data.objects if obj not in previous]
    bpy.context.view_layer.update()
    import_report = inspect_import(imported,binary_report,validation)
    if args.render and not ERRORS:
        configure_render(args,preview)
    after = fingerprint(source);glb_after = fingerprint(glb)
    check(before == after, 'Editable source was changed by verification')
    check(glb_before == glb_after, 'Delivered GLB was changed by verification')
    report = {'task':'ART-007','status':'FAIL' if ERRORS else 'PASS',
        'verifiedAtUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
        'blenderVersion':bpy.app.version_string,'file':os.path.basename(glb),'sha256':glb_before['sha256'],
        'source':os.path.relpath(source,args.root),'sourceBefore':before,'sourceAfter':after,
        'sourceUnchanged':before == after,'glbUnchanged':glb_before == glb_after,
        'sourceValidation':os.path.relpath(validation_path,args.root),
        'exportReport':os.path.relpath(export_report_path,args.root),'auditedGeometryCleanup':cleanup,
        'binary':binary_report,'imported':import_report,
        'render':{'requested':args.render,'device':args.device if args.render else None,
                  'sourceStudioObjects':studio,'containsSourceKnightGeometry':False,
                  'output':os.path.basename(preview) if args.render and not ERRORS else None},
        'errors':ERRORS,
        'limits':['Passing structural checks does not certify visual parity; inspect the imported GLB preview.',
                  'A preview is rendered only when explicitly requested; CPU is the default device.']}
    with open(destination,'w',encoding='utf-8') as stream:
        json.dump(report,stream,ensure_ascii=False,indent=2)
    print(json.dumps({'status':report['status'],'triangles':import_report['triangles'],
        'embeddedImages':binary_report['embeddedImageCount'],'sourceUnchanged':report['sourceUnchanged'],
        'render':report['render'],'errors':ERRORS,'report':destination},ensure_ascii=False))
    if ERRORS:
        raise RuntimeError('Refined GLB verification failed; see glb_verification.json')


if __name__ == '__main__':
    main()
