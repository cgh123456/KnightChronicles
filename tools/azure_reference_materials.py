"""Physical materials for the 1.6 m concept-matched Azure longsword.

This module creates materials only. It never changes scene geometry, lights,
camera, or reference pixels. Linear-space colour values below intentionally keep
the antique metal pale and the blue sapphire distinct from the runic emission.

Usage::

    from azure_reference_materials import make_materials
    materials = make_materials()
    blade.data.materials.append(materials['steel'])

Apply object scale before assigning the procedural materials: Object coordinates
are expressed in local metres, with the blade length along Z. The default
materials use no concept-image colour projection. To create an explicitly
optional projection material, pass ``reference_path`` and assign the returned
``reference_projection`` material yourself. It expects a planar UV layer named
``ReferenceProjection`` mapped to the entire original image.

glTF preserves the constant Principled fallback values, transmission, IOR and
emission; procedural polishing, pores and volume absorption need texture baking
or engine shader reconstruction. They are not silently claimed to survive GLB.
"""

from pathlib import Path

import bpy


PREFIX = 'AZURE_REF | '


def _new_material(name, base, metallic, roughness):
    material = bpy.data.materials.new(PREFIX + name)
    material.use_nodes = True
    material.diffuse_color = (*base[:3], 1.0)
    material.metallic = metallic
    material.roughness = roughness
    nodes = material.node_tree.nodes
    nodes.clear()
    shader = nodes.new('ShaderNodeBsdfPrincipled')
    shader.name = 'Principled BSDF'
    shader.label = 'Physical surface / glTF fallback'
    shader.location = (570, 80)
    shader.inputs['Base Color'].default_value = (*base[:3], 1.0)
    shader.inputs['Metallic'].default_value = metallic
    shader.inputs['Roughness'].default_value = roughness
    output = nodes.new('ShaderNodeOutputMaterial')
    output.location = (900, 80)
    material.node_tree.links.new(shader.outputs['BSDF'], output.inputs['Surface'])
    material['units'] = 'metres; apply object scale for procedural microdetail'
    material['gltf_fallback_base_color'] = list(base[:3])
    material['gltf_fallback_metallic'] = metallic
    material['gltf_fallback_roughness'] = roughness
    return material, shader, output


def _ramp(nodes, links, source, low, high, name, position):
    node = nodes.new('ShaderNodeValToRGB')
    node.label = name
    node.location = position
    node.color_ramp.elements[0].position = 0.16
    node.color_ramp.elements[0].color = (low, low, low, 1.0)
    node.color_ramp.elements[1].position = 0.84
    node.color_ramp.elements[1].color = (high, high, high, 1.0)
    links.new(source, node.inputs['Fac'])
    return node.outputs['Color']


def create_surface(name, base, metallic, roughness, micro=0.35):
    """Create a subtly worked metal; ``micro`` ranges from zero to one.

    Maximum bump distance is 28 micrometres, not centimetres. Roughness variation
    conveys most surface detail; the actual modelling must carry engravings.
    """
    material, shader, _ = _new_material(name, base, metallic, roughness)
    if micro <= 0:
        return material
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    coord = nodes.new('ShaderNodeTexCoord')
    coord.location = (-1050, 80)
    grain = nodes.new('ShaderNodeTexNoise')
    grain.label = 'Fine forged grain, roughly 0.45 mm'
    grain.location = (-650, 130)
    grain.inputs['Scale'].default_value = 2200.0
    grain.inputs['Detail'].default_value = 2.0
    grain.inputs['Roughness'].default_value = 0.60
    links.new(coord.outputs['Object'], grain.inputs['Vector'])
    grain_roughness = _ramp(
        nodes, links, grain.outputs['Fac'],
        max(0.04, roughness - 0.075 * micro),
        min(0.95, roughness + 0.11 * micro),
        'Subtle micro-roughness', (-330, 260),
    )
    links.new(grain_roughness, shader.inputs['Roughness'])
    brush_scale = nodes.new('ShaderNodeVectorMath')
    brush_scale.operation = 'MULTIPLY'
    brush_scale.label = 'Longitudinal polishing, Z axis'
    brush_scale.location = (-850, -200)
    brush_scale.inputs[1].default_value = (6500.0, 6500.0, 70.0)
    links.new(coord.outputs['Object'], brush_scale.inputs[0])
    brushing = nodes.new('ShaderNodeTexNoise')
    brushing.location = (-600, -180)
    brushing.inputs['Scale'].default_value = 1.0
    brushing.inputs['Detail'].default_value = 1.5
    brushing.inputs['Roughness'].default_value = 0.6
    links.new(brush_scale.outputs['Vector'], brushing.inputs['Vector'])
    micro_bump = nodes.new('ShaderNodeBump')
    micro_bump.location = (-80, -130)
    micro_bump.label = 'Micrometre forging; not visible dents'
    micro_bump.inputs['Strength'].default_value = 0.10 * micro
    micro_bump.inputs['Distance'].default_value = 0.000028
    links.new(grain.outputs['Fac'], micro_bump.inputs['Height'])
    brush_bump = nodes.new('ShaderNodeBump')
    brush_bump.location = (220, -120)
    brush_bump.label = 'Fine longitudinal polish'
    brush_bump.inputs['Strength'].default_value = 0.045 * micro
    brush_bump.inputs['Distance'].default_value = 0.000012
    links.new(brushing.outputs['Fac'], brush_bump.inputs['Height'])
    links.new(micro_bump.outputs['Normal'], brush_bump.inputs['Normal'])
    links.new(brush_bump.outputs['Normal'], shader.inputs['Normal'])
    material['requires_bake_for_gltf'] = 'procedural roughness and tangent normal'
    return material


def _leather():
    material, shader, _ = _new_material(
        'Black wrapped calf leather', (0.011, 0.014, 0.018), 0.0, 0.40,
    )
    shader.inputs['Specular IOR Level'].default_value = 0.33
    shader.inputs['Coat Weight'].default_value = 0.13
    shader.inputs['Coat Roughness'].default_value = 0.34
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    coord = nodes.new('ShaderNodeTexCoord')
    coord.location = (-800, 40)
    pores = nodes.new('ShaderNodeTexVoronoi')
    pores.location = (-570, -80)
    pores.feature = 'DISTANCE_TO_EDGE'
    pores.inputs['Scale'].default_value = 3400.0
    links.new(coord.outputs['Object'], pores.inputs['Vector'])
    bump = nodes.new('ShaderNodeBump')
    bump.location = (220, -100)
    bump.inputs['Strength'].default_value = 0.15
    bump.inputs['Distance'].default_value = 0.000065
    bump.invert = True
    links.new(pores.outputs['Distance'], bump.inputs['Height'])
    links.new(bump.outputs['Normal'], shader.inputs['Normal'])
    grain = nodes.new('ShaderNodeTexNoise')
    grain.location = (-580, 250)
    grain.inputs['Scale'].default_value = 1800.0
    grain.inputs['Detail'].default_value = 2.0
    links.new(coord.outputs['Object'], grain.inputs['Vector'])
    rough = _ramp(nodes, links, grain.outputs['Fac'], 0.34, 0.48,
                  'Soft worn leather sheen', (-220, 250))
    links.new(rough, shader.inputs['Roughness'])
    material['requires_bake_for_gltf'] = 'leather pore normal and roughness'
    return material


def _sapphire(name, base, roughness, transmission, glow):
    material, shader, output = _new_material(name, base, 0.0, roughness)
    shader.inputs['IOR'].default_value = 1.765
    shader.inputs['Transmission Weight'].default_value = transmission
    shader.inputs['Coat Weight'].default_value = 0.24
    shader.inputs['Coat Roughness'].default_value = 0.035
    shader.inputs['Emission Color'].default_value = (0.002, 0.095, 0.56, 1.0)
    shader.inputs['Emission Strength'].default_value = glow
    nodes = material.node_tree.nodes
    absorption = nodes.new('ShaderNodeVolumeAbsorption')
    absorption.label = 'Sapphire depth tint; use closed gem mesh'
    absorption.location = (570, -320)
    absorption.inputs['Color'].default_value = (0.065, 0.29, 0.84, 1.0)
    absorption.inputs['Density'].default_value = 8.0
    material.node_tree.links.new(absorption.outputs['Volume'], output.inputs['Volume'])
    material['closed_mesh_required'] = True
    material['facet_shading'] = 'Flat normals; assign sapphire_light sparingly to crown facets'
    material['gltf_note'] = 'Transmission/IOR exported; absorption volume may need engine reconstruction'
    return material


def make_reference_projection(reference_path, name='Optional concept projection'):
    """Optional UV-based source-image reference surface, never assigned for you.

    This is an artistic reference/look-development aid, NOT a recovered albedo:
    the original concept contains painted lighting. Its use must be disclosed in
    a deliverable. Material UVs should cover only the actual sword silhouette.
    """
    path = Path(reference_path).expanduser().resolve()
    if not path.is_file():
        raise FileNotFoundError(f'Concept reference not found: {path}')
    material, shader, _ = _new_material(name, (0.45, 0.46, 0.47), 0.70, 0.32)
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    uv = nodes.new('ShaderNodeUVMap')
    uv.uv_map = 'ReferenceProjection'
    uv.location = (-600, 100)
    texture = nodes.new('ShaderNodeTexImage')
    texture.label = 'Original concept, retains painted lighting'
    texture.location = (-300, 100)
    texture.image = bpy.data.images.load(str(path), check_existing=True)
    texture.extension = 'CLIP'
    texture.interpolation = 'Linear'
    links.new(uv.outputs['UV'], texture.inputs['Vector'])
    links.new(texture.outputs['Color'], shader.inputs['Base Color'])
    material['source_image'] = str(path)
    material['source_projection_is_optional'] = True
    material['not_delighted_albedo'] = True
    return material


def make_materials(reference_path=None):
    """Return the ten primary materials and optional unassigned projection.

    ``sapphire`` and ``sapphire_light`` are two dielectric facet variants.
    Bright rune inserts use ``energy``; gemstones should not use that shader.
    """
    result = {
        'steel': create_surface('Forged silver steel', (0.40, 0.435, 0.465), 0.98, 0.28, 0.50),
        'edge': create_surface('Sharpened silver bevel', (0.68, 0.705, 0.735), 1.0, 0.19, 0.18),
        'dark': create_surface('Blackened steel recess plates', (0.031, 0.035, 0.041), 0.91, 0.33, 0.48),
        'gold': create_surface('Patinated champagne gold inlay', (0.36, 0.26, 0.125), 0.95, 0.30, 0.36),
        'pale_gold': create_surface('Worn pale gold ridges', (0.53, 0.43, 0.275), 0.98, 0.245, 0.20),
        'leather': _leather(),
        'sapphire': _sapphire('Deep blue sapphire', (0.055, 0.18, 0.61), 0.048, 0.90, 0.055),
        'sapphire_light': _sapphire('Sapphire lighter crown facets', (0.12, 0.36, 0.83), 0.037, 0.82, 0.11),
        'energy': create_surface('Narrow azure runic light', (0.002, 0.08, 0.33), 0.05, 0.24, 0.0),
        'recess': create_surface('Deep carved shadow recess', (0.007, 0.009, 0.012), 0.76, 0.44, 0.15),
    }
    energy = result['energy'].node_tree.nodes['Principled BSDF']
    energy.inputs['Emission Color'].default_value = (0.002, 0.25, 1.0, 1.0)
    energy.inputs['Emission Strength'].default_value = 4.2
    for key, material in result.items():
        material['azure_material_key'] = key
        material['concept_target'] = 'silver/black steel, pale antique gold, black leather, refractive sapphire'
    if reference_path is not None:
        result['reference_projection'] = make_reference_projection(reference_path)
    return result


if __name__ == '__main__':
    materials = make_materials()
    assert len(materials) == 10
    for key, material in materials.items():
        assert material.node_tree.nodes['Principled BSDF'] is not None, key
    print('AZURE_REFERENCE_MATERIALS_OK: ' + ', '.join(materials))
