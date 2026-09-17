"""ART-007: physically differentiated weathered metal, cloth and leather.
All channels are procedural in the editable source, and baked for interchange.
"""
import bpy, os

PALETTE={
 'steel':('#73747b',.39,.88),'steel_light':('#97989b',.32,.91),'steel_dark':('#3e4149',.52,.82),
 'dark':('#0b0d12',.95,0),'gold':('#bb924d',.42,.78),
 'cloth':('#393951',.92,0),'cloth_tunic':('#2d3047',.93,0),'cloth_light':('#37374f',.91,0),'cloth_dark':('#242632',.92,0),
 'leather':('#513420',.73,0),'leather_light':('#67472c',.70,0),'leather_dark':('#403125',.79,0),'leather_glove':('#554034',.72,0),
 'cream':('#9b8869',.92,0),'thread':('#a58a62',.81,0),
 'mail':('#3a3d46',.57,.72),'padding':('#363035',.93,0),'trim':('#a48249',.66,.18)
}

def build(P):
    result={}
    for key,(color,rough,metal) in PALETTE.items():
        mat=P.material(key,color,rough,metal);mat['material_role']=key;result[key]=mat
        ns=mat.node_tree.nodes;lk=mat.node_tree.links;bs=ns.get('Principled BSDF')
        def node(kind,label):
            n=ns.new(kind);n.label=label;return n
        def math(op,a,b=0):
            n=node('ShaderNodeMath',op);n.operation=op
            for i,v in enumerate((a,b)):
                if isinstance(v,(int,float)):n.inputs[i].default_value=v
                else:lk.new(v,n.inputs[i])
            return n.outputs[0]
        def noise(scale,detail=3,roughness=.7,vector=None):
            n=node('ShaderNodeTexNoise','Surface variation');n.inputs['Scale'].default_value=scale;n.inputs['Detail'].default_value=detail;n.inputs['Roughness'].default_value=roughness
            lk.new(vector or coord,n.inputs['Vector']);return n.outputs['Fac']
        def ramp(f,stops):
            n=node('ShaderNodeValToRGB','Controlled surface range');r=n.color_ramp
            for p,e in zip(stops[:2],r.elements):
                e.position=p[0];e.color=(*([p[1]]*3),1)
            for position,value in stops[2:]:r.elements.new(position).color=(value,value,value,1)
            lk.new(f,n.inputs[0]);return n.outputs[0]
        tex=node('ShaderNodeTexCoord','Stable object coordinates');coord=tex.outputs['Object']
        geo=node('ShaderNodeNewGeometry','Curvature wear')
        worn=ramp(geo.outputs['Pointiness'],[(.47,0),(.54,1)])
        base=P.linear_color(color)
        def shade(factor):
            mix=node('ShaderNodeMixRGB','Pigment and wear');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;mix.inputs[1].default_value=base;lk.new(factor,mix.inputs[2]);lk.new(mix.outputs[0],bs.inputs['Base Color'])
        if key=='dark':
            bs.inputs['Base Color'].default_value=(0,0,0,1)
            bs.inputs['Specular IOR Level'].default_value=0
            bs.inputs['Emission Color'].default_value=P.linear_color('#030406')
            bs.inputs['Emission Strength'].default_value=1
        if key.startswith('steel') or key in ('mail','gold'):
            broad=noise(7,3);hammer=noise(115,3);fine=noise(550,2)
            # Long faint strokes in crossing directions break perfect chrome.
            stretch=node('ShaderNodeVectorMath','Brushed scratch direction');stretch.operation='MULTIPLY';lk.new(coord,stretch.inputs[0]);stretch.inputs[1].default_value=(75,2.0,6.0)
            streak=noise(8,2,vector=stretch.outputs[0]);scratch=ramp(streak,[(.40,0),(.60,0),(.73,1)])
            pigment=math('ADD',.92,math('MULTIPLY',broad,.16))
            pigment=math('ADD',pigment,math('MULTIPLY',worn,.07))
            texture_path=os.path.join(os.path.dirname(os.path.dirname(__file__)),'assets/models/knight_refined_v4/material_sources/steel_albedo.png')
            if key.startswith('steel') and os.path.isfile(texture_path):
                im=bpy.data.images.load(texture_path,check_existing=True);im.pack()
                image=node('ShaderNodeTexImage','Painted forged steel patina');image.image=im;image.projection='BOX';image.projection_blend=.32;image.extension='REPEAT'
                scale=node('ShaderNodeVectorMath','Forged grain size');scale.operation='SCALE';scale.inputs['Scale'].default_value=1.1;lk.new(coord,scale.inputs[0]);lk.new(scale.outputs[0],image.inputs[0])
                normalize=node('ShaderNodeVectorMath','Calibrated forged steel value');normalize.operation='DIVIDE';lk.new(image.outputs['Color'],normalize.inputs[0]);normalize.inputs[1].default_value=P.linear_color('#777981')[:3]
                mix=node('ShaderNodeMixRGB','Hand worked metal surface');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=.80;lk.new(pigment,mix.inputs[1]);lk.new(normalize.outputs[0],mix.inputs[2]);pigment=mix.outputs[0]
            shade(pigment)
            r=math('ADD',rough-.065,math('MULTIPLY',hammer,.13));r=math('SUBTRACT',r,math('MULTIPLY',scratch,.10));lk.new(r,bs.inputs['Roughness'])
            micro=math('ADD',math('MULTIPLY',fine,.20),math('MULTIPLY',hammer,.55));micro=math('SUBTRACT',micro,math('MULTIPLY',scratch,.15))
            bump=node('ShaderNodeBump','Forging and hairline marks');bump.inputs['Strength'].default_value=.16;bump.inputs['Distance'].default_value=.0012;lk.new(micro,bump.inputs['Height']);lk.new(bump.outputs[0],bs.inputs['Normal'])
            bs.inputs['Anisotropic'].default_value=.22
        elif key.startswith('leather'):
            broad=noise(12,4);mid=noise(51,3);pores=noise(350,2)
            pigment=math('ADD',.72,math('MULTIPLY',broad,.53));pigment=math('ADD',pigment,math('MULTIPLY',worn,.13))
            texture_path=os.path.join(os.path.dirname(os.path.dirname(__file__)),'assets/models/knight_refined_v4/material_sources/leather_albedo.png')
            if os.path.isfile(texture_path):
                im=bpy.data.images.load(texture_path,check_existing=True);im.pack()
                image=node('ShaderNodeTexImage','Painted reference leather albedo');image.image=im;image.projection='BOX';image.projection_blend=.28;image.extension='REPEAT'
                scale=node('ShaderNodeVectorMath','Leather grain size');scale.operation='SCALE';scale.inputs['Scale'].default_value=1.5;lk.new(coord,scale.inputs[0]);lk.new(scale.outputs[0],image.inputs[0])
                normalize=node('ShaderNodeVectorMath','Keep the calibrated leather palette');normalize.operation='DIVIDE';lk.new(image.outputs['Color'],normalize.inputs[0]);normalize.inputs[1].default_value=P.linear_color('#65422d' if key=='leather_glove' else '#725039')[:3]
                mix=node('ShaderNodeMixRGB','Hand painted tanning variation');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=.30 if key=='leather_glove' else .82;lk.new(pigment,mix.inputs[1]);lk.new(normalize.outputs[0],mix.inputs[2]);pigment=mix.outputs[0]
            shade(pigment)
            lk.new(math('ADD',rough-.085,math('MULTIPLY',mid,.17)),bs.inputs['Roughness'])
            # Fine cellular pores plus slightly larger tanning grain.
            cell=node('ShaderNodeTexVoronoi','Leather pore channels');cell.feature='DISTANCE_TO_EDGE';cell.inputs['Scale'].default_value=185;lk.new(coord,cell.inputs['Vector'])
            h=math('ADD',math('MULTIPLY',pores,.30),math('MULTIPLY',cell.outputs['Distance'],.40));h=math('ADD',h,math('MULTIPLY',mid,.25))
            bump=node('ShaderNodeBump','Supple leather grain');bump.inputs['Strength'].default_value=.12 if key=='leather_glove' else .22;bump.inputs['Distance'].default_value=.001 if key=='leather_glove' else .0018;lk.new(h,bump.inputs['Height']);lk.new(bump.outputs[0],bs.inputs['Normal'])
            bs.inputs['Coat Weight'].default_value=.025;bs.inputs['Coat Roughness'].default_value=.57
        elif key in ('cloth','cloth_tunic','cloth_light','cloth_dark','cream','padding','trim','thread'):
            broad=noise(8,3);fine=noise(300,2)
            pigment=math('ADD',.77,math('MULTIPLY',broad,.43));shade(pigment)
            weave=[]
            for axis in ('X','Z'):
                w=node('ShaderNodeTexWave','Woven '+axis);w.wave_type='BANDS';w.bands_direction=axis;w.inputs['Scale'].default_value=180;w.inputs['Distortion'].default_value=1.4;w.inputs['Detail Scale'].default_value=1.7;lk.new(coord,w.inputs['Vector']);weave.append(w.outputs['Color'])
            h=math('ADD',math('MULTIPLY',math('MULTIPLY',weave[0],weave[1]),.75),math('MULTIPLY',fine,.25))
            bump=node('ShaderNodeBump','Fine cloth fibers');bump.inputs['Strength'].default_value=.23;bump.inputs['Distance'].default_value=.0014;lk.new(h,bump.inputs['Height']);lk.new(bump.outputs[0],bs.inputs['Normal'])
            bs.inputs['Sheen Weight'].default_value=.025 if key!='padding' else .01;bs.inputs['Sheen Roughness'].default_value=.77
    return result
