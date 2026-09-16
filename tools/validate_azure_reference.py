"""Read-only QA of the exported concept-matched sword GLB.

Run with Blender, in an otherwise disposable background session::

    blender -b --factory-startup --python tools/validate_azure_reference.py

The GLB is parsed independently before a fresh glTF import. The script writes
``import_validation.json`` beside the asset; it never repairs or resaves the GLB.
Topology is reported both as imported and after positional seam welding on an
isolated BMesh copy. glTF splits vertices at material/UV/normal boundaries, so raw
boundary edges must not be represented as proof of holes in the physical volume.
"""

import argparse
from collections import Counter
import json
import math
from pathlib import Path
import struct
import sys

import bmesh
import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_GLB = ROOT / 'assets/models/azure_reference_reconstruction/Azure_Reference_Reconstruction.glb'
MAIN_PREFIXES = ('01_', '02_', '20_', '26_', '27_', '31_', '35_', '38_',
                 '40_', '45_', '46_', '50_', '54_', '55_')
GEM_PREFIXES = ('10_', '30_', '56_')
WELD_DISTANCE_M = 1e-7


def finite(values):
    return all(math.isfinite(float(value)) for value in values)


def parse_glb(path):
    """Inspect glTF JSON and embedded resource bounds without trusting import."""
    data = path.read_bytes()
    if len(data) < 20:
        raise ValueError('GLB is too short to contain a header and JSON chunk')
    magic, version, size = struct.unpack_from('<4sII', data, 0)
    if magic != b'glTF' or version != 2 or size != len(data):
        raise ValueError(f'Invalid GLB header: magic={magic!r}, version={version}, declared_size={size}, actual_size={len(data)}')
    offset = 12
    chunks = []
    gltf = None
    binary = b''
    while offset < len(data):
        if offset + 8 > len(data):
            raise ValueError('Truncated GLB chunk header')
        length, kind = struct.unpack_from('<II', data, offset)
        offset += 8
        end = offset + length
        if end > len(data):
            raise ValueError('GLB chunk extends beyond end of file')
        if length % 4:
            raise ValueError('GLB chunk length is not four-byte aligned')
        chunks.append({'type': hex(kind), 'bytes': length})
        if kind == 0x4E4F534A:
            gltf = json.loads(data[offset:end].decode('utf-8').rstrip(' \x00'))
        elif kind == 0x004E4942:
            binary = data[offset:end]
        offset = end
    if gltf is None:
        raise ValueError('GLB has no JSON chunk')
    images = []
    buffer_views = gltf.get('bufferViews', [])
    for index, image in enumerate(gltf.get('images', [])):
        result = {'index': index, 'name': image.get('name'), 'mime_type': image.get('mimeType')}
        if 'bufferView' in image:
            view = buffer_views[image['bufferView']]
            start = view.get('byteOffset', 0)
            length = view['byteLength']
            valid = view.get('buffer', 0) == 0 and start >= 0 and length > 0 and start + length <= len(binary)
            signature = binary[start:start + 12] if valid else b''
            detected = ('image/png' if signature.startswith(b'\x89PNG\r\n\x1a\n') else
                        'image/jpeg' if signature.startswith(b'\xff\xd8\xff') else
                        'image/webp' if signature.startswith(b'RIFF') and signature[8:12] == b'WEBP' else
                        'image/ktx2' if signature.startswith(b'\xabKTX 20') else None)
            result.update(storage='embedded_buffer_view', bytes=length, valid_resource_bounds=valid,
                          detected_mime_type=detected, signature_matches_mime=(detected == image.get('mimeType')))
        else:
            uri = image.get('uri', '')
            if uri.startswith('data:'):
                result.update(storage='data_uri', valid_resource_bounds=',' in uri and len(uri.split(',', 1)[-1]) > 0)
            else:
                result.update(storage='external_uri', uri=uri, valid_resource_bounds=bool(uri) and (path.parent / uri).is_file())
        images.append(result)

    def texture_bindings(value, prefix=''):
        bindings = []
        if isinstance(value, dict):
            for key, child in value.items():
                key_path = f'{prefix}.{key}' if prefix else key
                if key.endswith('Texture') and isinstance(child, dict) and 'index' in child:
                    texture_index = child['index']
                    textures = gltf.get('textures', [])
                    texture = textures[texture_index] if 0 <= texture_index < len(textures) else {}
                    source = texture.get('source', texture.get('extensions', {}).get('KHR_texture_basisu', {}).get('source'))
                    bindings.append({'channel': key_path, 'texture_index': texture_index, 'image_index': source,
                                     'texcoord': child.get('texCoord', 0),
                                     'valid': source is not None and 0 <= source < len(images) and images[source]['valid_resource_bounds']})
                bindings.extend(texture_bindings(child, key_path))
        return bindings

    materials = []
    for index, material in enumerate(gltf.get('materials', [])):
        name = material.get('name', f'Material {index}')
        bindings = texture_bindings(material)
        source_projection = name.startswith('REF_COLOR_') or 'projection' in name.lower()
        materials.append({'index': index, 'name': name, 'texture_bindings': bindings,
                          'is_reference_colour_projection': source_projection,
                          'extras': material.get('extras', {}),
                          'pbr_factors': {key: value for key, value in material.get('pbrMetallicRoughness', {}).items() if not key.endswith('Texture')},
                          'extensions': sorted(material.get('extensions', {}))})
    primitives = []
    accessors = gltf.get('accessors', [])
    for mesh_index, mesh in enumerate(gltf.get('meshes', [])):
        for primitive_index, primitive in enumerate(mesh.get('primitives', [])):
            attributes = primitive.get('attributes', {})
            position = accessors[attributes['POSITION']] if 'POSITION' in attributes else {}
            count = accessors[primitive['indices']]['count'] if 'indices' in primitive else position.get('count', 0)
            mode = primitive.get('mode', 4)
            primitives.append({'mesh': mesh_index, 'primitive': primitive_index, 'mode': mode,
                               'vertices': position.get('count', 0), 'indices_or_vertices': count,
                               'triangles': count // 3 if mode == 4 else None,
                               'triangle_count_integral': count % 3 == 0 if mode == 4 else None,
                               'has_normals': 'NORMAL' in attributes,
                               'has_uv0': 'TEXCOORD_0' in attributes,
                               'normal_count_matches_positions': 'NORMAL' in attributes and accessors[attributes['NORMAL']]['count'] == position.get('count', 0),
                               'material_index': primitive.get('material')})
    return {'file_bytes': len(data), 'header_version': version, 'chunks': chunks,
            'gltf_asset': gltf.get('asset', {}), 'extensions_used': gltf.get('extensionsUsed', []),
            'node_count': len(gltf.get('nodes', [])), 'mesh_count': len(gltf.get('meshes', [])),
            'material_count': len(materials), 'texture_count': len(gltf.get('textures', [])),
            'image_count': len(images), 'images': images, 'materials': materials, 'primitives': primitives,
            'triangles': sum(p['triangles'] or 0 for p in primitives)}


def topology(bm):
    edges = list(bm.edges)
    closed = bool(edges) and all(edge.is_manifold for edge in edges)
    return {
        'vertices': len(bm.verts), 'edges': len(edges), 'faces': len(bm.faces),
        'non_manifold_edges': sum(not edge.is_manifold for edge in edges),
        'boundary_edges': sum(edge.is_boundary for edge in edges),
        'wire_edges': sum(edge.is_wire for edge in edges),
        'edges_with_more_than_two_faces': sum(len(edge.link_faces) > 2 for edge in edges),
        'non_contiguous_manifold_edges': sum(edge.is_manifold and not edge.is_contiguous for edge in edges),
        'zero_area_faces': sum(face.calc_area() <= 1e-16 for face in bm.faces),
        'closed_manifold': closed,
        'signed_volume_m3': bm.calc_volume(signed=True) if closed else None,
    }


def category(name):
    if name.startswith(MAIN_PREFIXES):
        return 'closed_structural_main_part', True
    if name.startswith(GEM_PREFIXES):
        return 'closed_faceted_gemstone', True
    if name.startswith('41_'):
        return 'leather_ribbon_surface', False
    if name.startswith('42_'):
        return 'leather_seam_decoration', False
    return 'metal_inlay_or_relief_decoration', None


def inspect_import(path):
    # Import into an empty transient session. The source GLB stays untouched.
    bpy.ops.wm.read_factory_settings(use_empty=True)
    result = bpy.ops.import_scene.gltf(filepath=str(path), merge_vertices=False)
    if 'FINISHED' not in result:
        raise RuntimeError(f'glTF import did not finish: {result}')
    objects = []
    bounds = []
    for obj in sorted(bpy.context.scene.objects, key=lambda item: item.name):
        if obj.type != 'MESH':
            continue
        mesh = obj.data
        mesh.calc_loop_triangles()
        cls, require_closed = category(obj.name)
        vertex_coords = [obj.matrix_world @ vertex.co for vertex in mesh.vertices]
        bounds.extend(vertex_coords)
        bm = bmesh.new()
        bm.from_mesh(mesh)
        # Only the disposable analysis copy is transformed or welded.
        bm.transform(obj.matrix_world)
        raw = topology(bm)
        bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=WELD_DISTANCE_M)
        welded = topology(bm)
        bm.free()
        corner_normals = [normal.vector for normal in mesh.corner_normals]
        objects.append({'name': obj.name, 'category': cls, 'expected_closed_volume': require_closed,
                        'triangles': len(mesh.loop_triangles), 'vertices': len(mesh.vertices),
                        'materials': [material.name if material else None for material in mesh.materials],
                        'uv_layers': [layer.name for layer in mesh.uv_layers],
                        'non_finite_vertices': sum(not finite(vertex) for vertex in vertex_coords),
                        'non_finite_corner_normals': sum(not finite(normal) for normal in corner_normals),
                        'zero_length_corner_normals': sum(normal.length < 1e-7 for normal in corner_normals),
                        'non_unit_corner_normals': sum(abs(normal.length - 1.0) > 1e-3 for normal in corner_normals),
                        'as_imported_topology': raw, 'geometric_seam_welded_topology': welded})
    minimum = [min(vertex[axis] for vertex in bounds) for axis in range(3)] if bounds else [0, 0, 0]
    maximum = [max(vertex[axis] for vertex in bounds) for axis in range(3)] if bounds else [0, 0, 0]
    dimensions = [maximum[axis] - minimum[axis] for axis in range(3)]
    loaded_images = [{'name': image.name, 'dimensions_px': list(image.size),
                      'has_loaded_pixels': bool(image.has_data),
                      'packed': image.packed_file is not None,
                      'source': image.source}
                     for image in bpy.data.images if image.type == 'IMAGE']
    groups = {}
    for cls in sorted({item['category'] for item in objects}):
        members = [item for item in objects if item['category'] == cls]
        groups[cls] = {
            'objects': len(members),
            'closed_manifold_objects': sum(item['geometric_seam_welded_topology']['closed_manifold'] for item in members),
            'raw_non_manifold_edges': sum(item['as_imported_topology']['non_manifold_edges'] for item in members),
            'geometric_non_manifold_edges': sum(item['geometric_seam_welded_topology']['non_manifold_edges'] for item in members),
            'geometric_boundary_edges': sum(item['geometric_seam_welded_topology']['boundary_edges'] for item in members),
            'geometric_edges_with_more_than_two_faces': sum(item['geometric_seam_welded_topology']['edges_with_more_than_two_faces'] for item in members),
        }
    return {'blender_version': bpy.app.version_string, 'import_completed': True,
            'mesh_objects': len(objects), 'triangles': sum(item['triangles'] for item in objects),
            'vertices': sum(item['vertices'] for item in objects),
            'bounds_min_m': minimum, 'bounds_max_m': maximum,
            'dimensions_m': {'width_x': dimensions[0], 'depth_y': dimensions[1], 'height_z': dimensions[2]},
            'dimensions_expected_m': {'width_x': 0.473, 'height_z': 1.6},
            'dimensions_tolerance_m': {'width_x': 0.008, 'height_z': 0.008},
            'height_matches_target': abs(dimensions[2] - 1.6) <= 0.008,
            'width_matches_target': abs(dimensions[0] - 0.473) <= 0.008,
            'weld_distance_m': WELD_DISTANCE_M,
            'topology_method': 'Raw imported edges and a positional-welded BMesh analysis copy; no asset mutation.',
            'topology_groups': groups, 'loaded_images': loaded_images, 'objects': objects}


def validate(path, report_path):
    report = {
        'asset_path': str(path), 'validation_scope': 'Export parsing, independent fresh import, dimensions, textures, normals and topology; no repairs.',
        'appearance_disclosure': 'Fine visual engraving and wear use projection of the original concept image. Original lighting remains baked into that image. This is not a fully baked physically based material set.',
        'single_view_disclosure': 'The rear side, thickness and hidden construction are inferred from a single front concept view.',
        'errors': [], 'warnings': [],
    }
    try:
        container = report['glb'] = parse_glb(path)
        imported = report['blender_import'] = inspect_import(path)
        if not imported['mesh_objects']:
            report['errors'].append('No mesh objects imported.')
        if not imported['height_matches_target'] or not imported['width_matches_target']:
            report['errors'].append('Imported dimensions fall outside the documented target tolerance.')
        if container['triangles'] != imported['triangles']:
            report['errors'].append('Container and imported triangle counts differ.')
        if any(not p['has_normals'] or not p['normal_count_matches_positions'] for p in container['primitives']):
            report['errors'].append('One or more GLB primitives lack valid matched NORMAL attributes.')
        if not container['images']:
            report['errors'].append('No image textures are present in the exported asset.')
        if any(not image['valid_resource_bounds'] or image.get('signature_matches_mime') is False for image in container['images']):
            report['errors'].append('An image resource has invalid bounds or MIME signature.')
        if any(not binding['valid'] for material in container['materials'] for binding in material['texture_bindings']):
            report['errors'].append('A material texture points to a missing or invalid image.')
        if any(not image['has_loaded_pixels'] or min(image['dimensions_px']) <= 0 for image in imported['loaded_images']):
            report['errors'].append('An imported image did not decode into valid pixels.')
        for item in imported['objects']:
            if item['non_finite_vertices'] or item['non_finite_corner_normals'] or item['zero_length_corner_normals']:
                report['errors'].append(f"{item['name']}: invalid vertex or normal values.")
            topo = item['geometric_seam_welded_topology']
            if item['expected_closed_volume'] and not topo['closed_manifold']:
                report['errors'].append(f"{item['name']}: expected a closed main volume; found {topo['non_manifold_edges']} geometric non-manifold edges.")
            elif topo['non_manifold_edges']:
                report['warnings'].append(f"{item['name']}: {topo['non_manifold_edges']} geometric non-manifold edges in {item['category']} ({topo['boundary_edges']} boundary, {topo['edges_with_more_than_two_faces']} with more than two faces).")
            if topo['non_contiguous_manifold_edges']:
                report['warnings'].append(f"{item['name']}: {topo['non_contiguous_manifold_edges']} manifold edges have inconsistent face winding.")
            if topo['zero_area_faces']:
                report['warnings'].append(f"{item['name']}: {topo['zero_area_faces']} zero-area faces after positional seam welding.")
        bindings = [binding for material in container['materials'] for binding in material['texture_bindings']]
        report['texture_channel_summary'] = dict(Counter(binding['channel'] for binding in bindings))
        report['reference_projection_material_count'] = sum(material['is_reference_colour_projection'] for material in container['materials'])
        report['warnings'].append('Reference-image projection is present; this report does not certify independent albedo/normal/roughness/metalness texture baking.')
        report['passed_export_integrity_checks'] = not report['errors']
        report['status'] = 'FAIL' if report['errors'] else ('PASS_WITH_DISCLOSED_LIMITATIONS' if report['warnings'] else 'PASS')
    except Exception as exc:
        report['errors'].append(f'{type(exc).__name__}: {exc}')
        report['passed_export_integrity_checks'] = False
        report['status'] = 'FAIL'
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    compact = {key: report.get(key) for key in ('status', 'passed_export_integrity_checks', 'errors', 'reference_projection_material_count')}
    compact['report'] = str(report_path)
    compact['warning_count'] = len(report['warnings'])
    if 'blender_import' in report:
        compact['dimensions_m'] = report['blender_import']['dimensions_m']
        compact['triangles'] = report['blender_import']['triangles']
        compact['topology_groups'] = report['blender_import']['topology_groups']
    print('AZURE_IMPORT_VALIDATION ' + json.dumps(compact, ensure_ascii=False), flush=True)
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--glb', type=Path, default=DEFAULT_GLB)
    parser.add_argument('--report', type=Path)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    asset = args.glb.expanduser().resolve()
    report = validate(asset, args.report or asset.parent / 'import_validation.json')
    if not report['passed_export_integrity_checks']:
        raise SystemExit(2)
