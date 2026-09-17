"""Build editable medieval world geometry and bake transparent orthographic sprites.

Blender --background --python tools/build_world_art.py -- <repository-root>
All geometry/materials in this script are original; generated source is world_collection.blend.
"""
import bpy, math, os, sys, json, random
from mathutils import Vector

ROOT = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else os.path.dirname(os.path.dirname(__file__))
OUT = os.path.join(ROOT, 'unity/KnightChronicles/Assets/Resources/Art/World')
SRC = os.path.join(ROOT, 'assets/models/world')
os.makedirs(OUT, exist_ok=True); os.makedirs(SRC, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
random.seed(613)
M = {}
for name, color, metal in [
    ('stone',(.37,.39,.40),0),('stoneLight',(.58,.57,.52),0),('stoneDark',(.21,.24,.27),0),
    ('plaster',(.80,.70,.53),0),('wood',(.25,.12,.065),0),('woodLight',(.50,.30,.14),0),
    ('woodDark',(.115,.07,.045),0),('roof',(.45,.13,.075),0),('roofBlue',(.15,.25,.29),0),
    ('roofGreen',(.18,.31,.22),0),('roofPurple',(.29,.20,.31),0),('steel',(.46,.52,.56),.7),
    ('gold',(.69,.45,.12),.65),('iron',(.12,.15,.17),.6),('glass',(.82,.46,.13),.1),
    ('grass',(.25,.36,.14),0),('grassLight',(.37,.43,.21),0),('leaf',(.22,.34,.12),0),
    ('leafLight',(.33,.45,.16),0),('water',(.10,.47,.60),.2),('bone',(.78,.73,.60),0),
    ('cloth',(.47,.24,.21),0),('purple',(.45,.27,.60),0),('blue',(.19,.37,.49),0),
    ('green',(.27,.56,.28),0),('red',(.65,.18,.12),0),('paper',(.81,.74,.54),0),
    ('flame', (1,.44,.04),0),('portal',(.15,.86,.69),0)]:
    mat=bpy.data.materials.new(name); mat.diffuse_color=(*color,1); mat.use_nodes=True
    p=mat.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Metallic'].default_value=metal; p.inputs['Roughness'].default_value=.72 if metal==0 else .3
    if name in ('flame','portal','glass'):
        p.inputs['Emission Color'].default_value=(*color,1);p.inputs['Emission Strength'].default_value=1.7 if name!='glass' else .35
    M[name]=mat

current=None
def attach(obj,name,mat):
    obj.name=name; obj.data.materials.append(M[mat])
    for col in list(obj.users_collection): col.objects.unlink(obj)
    current.objects.link(obj); return obj
def box(name,loc,size,mat,bevel=.02,rotation=None):
    sx,sy,sz=[v*.5 for v in size]
    mesh=bpy.data.meshes.new(name);mesh.from_pydata([(-sx,-sy,-sz),(-sx,-sy,sz),(-sx,sy,-sz),(-sx,sy,sz),(sx,-sy,-sz),(sx,-sy,sz),(sx,sy,-sz),(sx,sy,sz)],[],[(2,6,4,0),(5,7,3,1),(4,5,1,0),(3,7,6,2),(1,3,2,0),(6,7,5,4)])
    o=bpy.data.objects.new(name,mesh);current.objects.link(o);o.location=loc;mesh.materials.append(M[mat])
    if rotation:o.rotation_euler=rotation
    if bevel:
        m=o.modifiers.new('soft cut edges','BEVEL');m.width=bevel;m.segments=1
        o.modifiers.new('weighted normals','WEIGHTED_NORMAL')
    return o
def cyl(name,loc,r,depth,mat,vertices=12,rotation=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=depth,location=loc)
    o=attach(bpy.context.object,name,mat)
    if rotation:o.rotation_euler=rotation
    return o
def cone(name,loc,r1,r2,depth,mat,verts=8):
    bpy.ops.mesh.primitive_cone_add(vertices=verts,radius1=r1,radius2=r2,depth=depth,location=loc)
    return attach(bpy.context.object,name,mat)
def ico(name,loc,r,mat,scale=(1,1,1)):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=r,location=loc)
    o=attach(bpy.context.object,name,mat);o.scale=scale;return o
def beam(name,a,b,r,mat):
    a,b=Vector(a),Vector(b);o=cyl(name,(a+b)/2,r,(a-b).length,mat,8);o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();return o
def planks(name,x,y,z,w,d,h,mat='woodLight',n=5):
    for i in range(n):box(name+str(i),(x-w/2+w*(i+.5)/n,y,z),(w/n-.016,d,h),mat,.009)
def barrel(x=0,y=0,z=0,scale=1):
    for i in range(12):
        a=i*math.tau/12; box('barrel oak stave',(x+math.sin(a)*.29*scale,y+math.cos(a)*.29*scale,z+.40*scale),(.17*scale,.08*scale,.78*scale),'woodLight',.012,(0,0,-a))
    cyl('barrel lid',(x,y,z+.81*scale),.31*scale,.035*scale,'wood',12)
    for h in(.14,.65):cyl('barrel forged band',(x,y,z+h*scale),.335*scale,.045*scale,'iron',12)
def crate(x=0,y=0,z=0,scale=1):
    box('crate dark interior',(x,y,z+.36*scale),(.73*scale,.73*scale,.72*scale),'woodDark')
    for i in range(5):
        for side in(-1,1):box('crate plank',(x+(.3-i*.15)*scale,y+side*.38*scale,z+.36*scale),(.14*scale,.035*scale,.69*scale),'woodLight',.01)
        box('crate lid',(x+(.3-i*.15)*scale,y,z+.73*scale),(.14*scale,.75*scale,.03*scale),'woodLight',.01)
    for side in(-1,1):
        for h in(.08,.65):box('crate braces',(x,y+side*.40*scale,z+h*scale),(.80*scale,.06*scale,.07*scale),'wood')
        box('crate diagonal',(x,y+side*.41*scale,z+.36*scale),(.08*scale,.055*scale,.84*scale),'wood',.01,(0,.75,0))
def torch(x=0,y=0,z=0):
    beam('torch bracket',(x,y+.12,z+.15),(x,y-.15,z+.45),.045,'iron')
    cyl('torch shaft',(x,y-.2,z+.57),.065,.58,'wood')
    cone('torch cup',(x,y-.2,z+.92),.16,.10,.24,'iron')
    ico('warm flame',(x,y-.2,z+1.16),.22,'flame',(.65,.65,1.8))
def window(x,y,z,w=.48):
    box('window recess',(x,y+.02,z),(w+.1,.07,.62),'woodDark')
    box('amber glass',(x,y-.02,z),(w,.05,.53),'glass')
    for k in(-1,1):box('window jamb',(x+k*(w+.03)/2,y-.06,z),(.06,.08,.65),'wood')
    box('window cross',(x,y-.065,z),(.05,.08,.56),'wood')
    box('window mullion',(x,y-.065,z),(w,.08,.05),'wood')
    box('window sill',(x,y-.09,z-.35),(w+.18,.22,.09),'woodLight')
def sign(x,y,z,kind):
    beam('sign bracket',(x,y+.1,z+.2),(x,y-.45,z+.2),.055,'iron')
    beam('sign chain',(x,y-.40,z+.20),(x,y-.40,z-.05),.025,'iron')
    box('carved shop sign',(x,y-.43,z-.29),(.65,.10,.50),'woodDark',.04)
    # actual 3D silhouettes distinguish storefronts
    if kind in('smith','armor_shop'):
        beam('sign blade',(x,y-.50,z-.40),(x,y-.50,z-.12),.035,'gold')
        beam('sign guard',(x-.12,y-.50,z-.32),(x+.12,y-.50,z-.32),.03,'gold')
    elif kind=='alchemy':cyl('potion sign',(x,y-.50,z-.31),.12,.22,'green',8,(math.pi/2,0,0))
    elif kind=='scroll_shop':box('book sign',(x,y-.50,z-.29),(.31,.055,.32),'paper')
    else:ico('sign seal',(x,y-.50,z-.28),.14,'gold', (1,.3,1))
def roof(w,d,z,mat):
    rise=w*.36; slope=math.atan2(rise,w/2+.17); length=math.sqrt((w/2+.17)**2+rise**2)
    for side in(-1,1):
        box('roof slab',(side*w*.25,0,z+rise*.5),(length,d+.34,.12),mat,.015,(0,side*slope,0))
        for row in range(6):
            xx=side*(w/2+.17)*(row+.5)/6;zz=z+rise*(1-(row+.5)/6)+.06
            for col in range(9):
                yy=-(d+.34)/2+(d+.34)*(col+.5)/9
                box('individual roof tile',(xx,yy,zz),(length/6+.025,(d+.34)/9-.01,.048),mat,.01,(0,side*slope,0))
    cyl('roof ridge',(0,0,z+rise+.06),.075,d+.48,mat,8,(math.pi/2,0,0))
    # visible front gable triangle
    mesh=bpy.data.meshes.new('gable mesh');mesh.from_pydata([(-w/2,-d/2,z),(w/2,-d/2,z),(0,-d/2,z+rise)],[],[(0,1,2)])
    ob=bpy.data.objects.new('plaster gable',mesh);current.objects.link(ob);mesh.materials.append(M['plaster'])
    beam('gable fascia',(-w/2-.14,-d/2-.12,z),(0,-d/2-.12,z+rise+.04),.07,'wood')
    beam('gable fascia',(w/2+.14,-d/2-.12,z),(0,-d/2-.12,z+rise+.04),.07,'wood')
    beam('gable centerpost',(0,-d/2-.04,z),(0,-d/2-.04,z+rise),.055,'wood')
def building(kind):
    w,d,h=(3.2,2.5,2.1) if kind not in('guild','hut') else ((4,3,2.7) if kind=='guild' else (2.8,2.3,1.9))
    for row in range(2):
        for i in range(8):box('foundation individual stone',(-w/2+w*(i+.5)/8,-d/2-.04,.12+row*.21),(w/8-.016,.30,.20),'stoneLight' if i%3 else 'stone',.03)
    box('lime plaster walls',(0,0,h/2+.25),(w,d,h),'plaster',.03)
    for x in(-w/2+.04,0,w/2-.04):box('front timber post',(x,-d/2-.05,h/2+.25),(.12,.12,h),'wood')
    for y in(-d/2+.04,d/2-.04):box('side timber post',(w/2+.015,y,h/2+.25),(.13,.12,h),'wood')
    for z in(.54,h*.58,h+.23):box('timber belt',(0,-d/2-.06,z),(w+.14,.12,.12),'wood')
    doorx=0;doorY=-d/2-.12
    box('door recess',(doorx,doorY,.83),(.81,.1,1.38),'woodDark',.035)
    planks('door plank',doorx,doorY-.06,.85,.70,.07,1.25,n=6)
    for z in(.38,1.15):box('iron door brace',(doorx,doorY-.11,z),(.68,.05,.045),'iron')
    ico('door handle',(doorx+.24,doorY-.15,.88),.05,'gold')
    box('door stone step',(0,doorY-.19,.08),(1.05,.50,.16),'stoneLight',.035)
    for x in(-w*.30,w*.30):window(x,-d/2-.09,1.34)
    style={'smith':'roof','armor_shop':'roofBlue','alchemy':'roofGreen','scroll_shop':'roofPurple','general_shop':'roof','guild':'roofBlue','hut':'roofGreen'}[kind]
    roof(w+.15,d,h+.29,style)
    sign(-w*.35,doorY-.10,2.08,kind)
    # chimney with stone coursing and dark flue
    for i in range(6):box('chimney course',(w*.28,d*.20,h+.30+i*.19),(.45,.42,.18),'stone',.018)
    box('chimney cap',(w*.28,d*.20,h+1.40),(.58,.54,.13),'stoneDark')
    if kind=='smith':
        box('smith open lean-to',(w*.56,-.15,1.7),(1.1,1.7,.13),'wood',.02,(0,-.12,0))
        for yy in(-.85,.55):cyl('lean-to post',(w*.71,yy,.8),.075,1.6,'wood')
        box('anvil foot',(w*.58,-.6,.10),(.45,.34,.17),'iron');box('anvil body',(w*.58,-.6,.36),(.28,.30,.44),'iron')
        box('anvil top',(w*.58,-.6,.59),(.72,.34,.13),'steel');cone('anvil horn',(w*.82,-.6,.59),.14,0,.44,'steel',8).rotation_euler[1]=math.pi/2
        box('coal forge',(w*.57,.50,.40),(.8,.7,.70),'stoneDark');ico('forge coal',(w*.57,.50,.78),.30,'flame',(1,1,.30))
    elif kind=='alchemy':
        box('alchemist counter',(w*.47,-d*.52,.55),(.8,.48,.10),'woodLight')
        for i in range(4):
            xx=w*.47-.3+i*.2;cyl('glass potion',(xx,-d*.52,.70),.075,.20,'green' if i%2 else 'purple',8);cyl('potion stopper',(xx,-d*.52,.83),.04,.06,'wood')
        ico('alchemical orb',(w*.34,0,h+1.50),.28,'purple')
    elif kind=='scroll_shop':
        planks('scribe table',w*.42,-d*.53,.53,.75,.60,.07,n=5)
        for i in range(3):box('book stack',(w*.42,-d*.55,.59+i*.075),(.40,.28,.06),'blue' if i%2 else 'paper')
    elif kind=='general_shop':
        for i in range(4):box('striped canopy',(-w*.40+i*.31,-d*.68,1.30),(.32,.70,.08),'cloth' if i%2 else 'paper',.012,(.18,0,0))
        crate(-w*.33,-d*.77,0,.63);barrel(w*.45,-d*.62,0,.70)
    elif kind=='armor_shop':
        cyl('armor stand',(w*.44,-d*.63,.6),.045,1.15,'wood')
        ico('display cuirass',(w*.44,-d*.63,.90),.28,'steel',(1,.65,1.2));ico('display helmet',(w*.44,-d*.63,1.28),.19,'steel')
    elif kind=='guild':
        for x in(-w*.43,w*.43):box('guild banner',(x,-d/2-.13,1.5),(.43,.055,1.35),'blue')
        ico('guild brass crest',(0,-d/2-.13,2.26),.20,'gold',(1,.3,1))
        box('notice board',(w*.56,-d*.40,1.0),(.86,.16,.92),'wood')
        for i in range(3):box('pinned parchment',(w*.56-.26+i*.25,-d*.40-.1,1.02),(.18,.02,.50),'paper')
    else:barrel(-w*.55,-d*.28,0,.78);crate(w*.55,-d*.45,0,.65)

def floor(kind):
    if kind=='stone_floor' or kind=='cobble_tile':
        box('mortar base',(0,0,-.08),(2,2,.10),'stoneDark',0)
        n=4 if kind=='stone_floor' else 7
        for y in range(n):
            for x in range(n):
                xx=-1+(x+.5)*2/n;yy=-1+(y+.5)*2/n
                box('worn individual paving stone',(xx,yy,-.018+random.uniform(-.006,.006)),(2/n-.025,2/n-.025,.085),'stoneLight' if (x+y)%4 else 'stone',.035)
    elif kind=='wood_floor':
        for i in range(8):box('floorboard',(-.875+i*.25,0,0),(.242,2,.06),'woodLight' if i%3 else 'wood',.007)
        for i in range(8):
            for yy in(-.8,.8):cyl('floor nail',(-.875+i*.25,yy,.034),.013,.006,'iron',6)
    else:
        box('earth sod',(0,0,0),(2,2,.04),'grass',0)
        for i in range(140):ico('short grass',(random.uniform(-.98,.98),random.uniform(-.98,.98),.03),random.uniform(.015,.045),'grassLight', (1,1,.4))

def prop(kind):
    if kind in('stone_floor','cobble_tile','grass_tile','wood_floor'):floor(kind)
    elif kind=='crate':crate()
    elif kind=='barrel':barrel()
    elif kind=='stone_wall':
        for row in range(5):
            for col in range(6):
                x=-1+(col+.5)*2/6
                box('masonry block',(x,0,.15+row*.29),(2/6-.016,.46,.28),'stone' if (row+col)%3 else 'stoneLight',.025)
        box('wall coping',(0,0,1.48),(2.12,.57,.15),'stoneLight',.025)
    elif kind=='pillar':
        box('pillar plinth',(0,0,.10),(.70,.70,.20),'stoneLight')
        for i in range(6):box('pillar course',(0,0,.30+i*.28),(.47,.47,.27),'stone')
        box('pillar capital',(0,0,1.95),(.70,.70,.24),'stoneLight')
    elif kind=='chest':
        box('chest dark body',(0,0,.29),(.92,.62,.57),'woodDark',.04)
        for i in range(7):box('chest side plank',(-.4+i*.133,-.32,.29),(.12,.04,.53),'woodLight')
        # curved lid segmented across front-back axis
        for i in range(8):
            a=math.pi*(i+.5)/8;yy=math.cos(a)*.32;zz=.55+math.sin(a)*.21
            box('arched chest lid',(0,yy,zz),(.94,.13,.06),'woodLight',.01,(math.pi/2-a,0,0))
        for x in(-.33,.33):box('chest iron strap',(x,-.345,.33),(.075,.04,.58),'gold')
        box('chest lock',(0,-.37,.50),(.16,.06,.17),'gold',.02)
    elif kind=='bookshelf':
        box('shelf back',(0,.25,.72),(1.10,.09,1.44),'woodDark')
        for x in(-.55,.55):box('shelf side',(x,0,.72),(.10,.60,1.44),'wood')
        for z in(.08,.50,.93,1.38):box('shelf',(0,0,z),(1.20,.63,.07),'woodLight')
        for row in range(3):
            for i in range(7):box('bound volume',(-.44+i*.145,-.12,.13+row*.43+.14),(.09,.35,.25+random.uniform(0,.12)),['red','blue','green','paper'][i%4],.007,(0,.06*(i%3-1),0))
    elif kind in('bones','supplies'):
        if kind=='supplies':crate(-.28,.04,0,.8);barrel(.3,.18,0,.8)
        else:
            for i in range(7):
                x,y=random.uniform(-.4,.4),random.uniform(-.3,.3);beam('scattered bone',(x-.16,y,.08),(x+.16,y+.13,.08),.033,'bone')
            ico('fallen skull',(.08,-.04,.13),.16,'bone',(1,1,1.1))
            for x in(-.045,.045):ico('skull eye hollow',(x,-.17,.15),.04,'stoneDark',(1,.3,1))
    elif kind=='urn':
        cone('urn foot',(0,0,.08),.17,.24,.16,'stoneLight',12);cone('urn belly',(0,0,.32),.24,.34,.32,'stone',12)
        cone('urn neck',(0,0,.59),.34,.16,.23,'stone',12);cyl('urn rim',(0,0,.73),.20,.09,'stoneLight',12)
        cyl('urn opening',(0,0,.78),.14,.006,'stoneDark',12)
    elif kind in('stair_up','stair_down'):
        box('stairs foundation',(0,0,.04),(1.60,1.90,.10),'stoneDark')
        for i in range(7):box('individual stair',(0,-.75+i*.25,.1+i*.09),(1.34,.27,.18+i*.18),'stoneLight',.018)
        for x in(-.74,.74):box('stone stair balustrade',(x,.15,.55),(.14,1.9,.8),'stone',.015)
        if kind=='stair_down':box('dark stair opening',(0,.75,1.25),(1.29,.12,.38),'stoneDark')
    elif kind=='portal':
        for i in range(12):
            a=(i+.5)*math.pi/12;box('portal carved voussoir',(math.cos(a)*.80,0,.50+math.sin(a)*.85),(.25,.35,.26),'stoneLight',.02,(0,math.pi/2-a,0))
        for side in(-1,1):box('portal jamb',(side*.8,0,.30),(.25,.35,.6),'stone')
        box('portal threshold',(0,0,.04),(1.8,.60,.10),'stoneLight')
        ico('portal spectral interior',(0,.045,.72),.67,'portal',(1,.07,1.25))
    elif kind=='door':
        box('door back',(0,0,.76),(1.08,.17,1.52),'woodDark')
        planks('oak door',0,-.12,.74,.96,.09,1.43,n=7)
        for x in(-.60,.60):box('door carved jamb',(x,0,.77),(.19,.35,1.55),'stoneLight')
        box('door lintel',(0,0,1.58),(1.4,.38,.22),'stoneLight')
        for z in(.38,1.15):box('door iron band',(0,-.20,z),(.95,.05,.065),'iron')
        ico('door brass ring',(.28,-.21,.78),.065,'gold')
    elif kind=='torch':torch()
    elif kind=='tree':
        cone('oak trunk',(0,0,.62),.18,.11,1.24,'wood',8)
        for a in range(5):
            ang=a*math.tau/5;beam('tree bough',(0,0,.68),(.44*math.sin(ang),.44*math.cos(ang),1.36),.065,'wood')
            ico('tree canopy',(.38*math.sin(ang),.38*math.cos(ang),1.63),.72,'leaf' if a%2 else 'leafLight',(1,1,1.1))
        ico('tree crown',(0,0,2.1),.66,'leafLight')
    elif kind=='bush':
        for i in range(6):ico('bush leaf cluster',(random.uniform(-.35,.35),random.uniform(-.25,.25),.23+random.uniform(0,.1)),.27,'leaf' if i%2 else 'leafLight')
    elif kind=='rocks':
        for i in range(6):ico('angular rock',(random.uniform(-.36,.36),random.uniform(-.26,.26),.15),random.uniform(.10,.28),'stoneLight' if i%2 else 'stone', (1.2,1,.75))
    elif kind=='fountain':
        cyl('fountain stepped plinth',(0,0,.09),1.0,.18,'stone',16);cyl('fountain base',(0,0,.27),.91,.18,'stoneLight',16)
        cyl('pool water',(0,0,.39),.78,.025,'water',24)
        for i in range(16):
            a=i*math.tau/16;box('pool rim',(math.sin(a)*.86,math.cos(a)*.86,.42),(.35,.13,.22),'stoneLight',.02,(0,0,-a))
        cone('fountain column',(0,0,.78),.18,.10,.96,'stone',12);cyl('upper bowl',(0,0,1.22),.45,.15,'stoneLight',16)
        cyl('upper water',(0,0,1.30),.38,.02,'water',16);ico('finial',(0,0,1.47),.17,'gold')
        for i in range(6):
            a=i*math.tau/6;beam('falling water',(.36*math.sin(a),.36*math.cos(a),1.28),(.40*math.sin(a),.40*math.cos(a),.40),.018,'water')
    elif kind=='gate':
        for side in(-1,1):
            for i in range(5):box('gate tower course',(side*1.48,0,.25+i*.46),(.90,.90,.45),'stoneLight' if i%2 else 'stone')
            for dx in(-.28,.28):box('tower battlement',(side*1.48+dx,0,2.55),(.25,.96,.42),'stone')
            torch(side*1.48,-.50,.9)
        box('gate arch lintel',(0,0,2.18),(2.5,.68,.42),'stone')
        for i in range(9):beam('portcullis iron bar',(-.93+i*.232,0,.17),(-.93+i*.232,0,2.05),.045,'iron')
        for z in(.5,1.1,1.7):beam('portcullis brace',(-1,0,z),(1,0,z),.04,'iron')
        box('threshold',(0,0,.05),(3.8,1.35,.10),'stoneLight')
    elif kind=='fence':
        for x in(-.85,.85):box('fence post',(x,0,.43),(.11,.11,.86),'woodLight');cone('post cap',(x,0,.89),.08,0,.13,'wood',4)
        for z in(.29,.65):box('fence rail',(0,0,z),(1.85,.085,.10),'wood')
    elif kind=='signpost':
        box('wayfinding post',(0,0,.62),(.12,.12,1.24),'wood');box('upper direction plank',(.18,-.04,.98),(.78,.09,.20),'woodLight');box('lower direction plank',(-.14,-.04,.69),(.65,.09,.20),'woodLight')
    elif kind=='lantern':
        cone('lamp pedestal',(0,0,.09),.17,.10,.18,'iron');cyl('lamp post',(0,0,.85),.04,1.60,'iron',8)
        box('lantern glass',(0,0,1.69),(.22,.22,.33),'glass')
        for x in(-.13,.13):
            for y in(-.13,.13):box('lantern frame',(x,y,1.69),(.035,.035,.40),'iron')
        cone('lantern roof',(0,0,1.95),.23,.055,.20,'iron',4)
    elif kind=='bench':
        planks('bench seat',0,0,.46,1.3,.44,.07,n=5)
        for x in(-.45,.45):box('bench foot',(x,0,.23),(.11,.42,.46),'wood')
        box('bench back',(0,.24,.76),(1.35,.09,.38),'woodLight')
    elif kind=='cart':
        planks('cart bed',0,0,.48,1.05,1.5,.08,n=7)
        for x in(-.52,.52):box('cart side',(x,0,.73),(.06,1.5,.40),'woodLight')
        for x in(-.66,.66):
            cyl('cart wheel',(x,.1,.37),.37,.08,'woodDark',12,(0,math.pi/2,0));cyl('wheel hub',(x,.1,.37),.09,.12,'iron',8,(0,math.pi/2,0))
            for i in range(6):
                a=i*math.tau/6;beam('wheel spoke',(x,.1,.37),(x,.1+math.sin(a)*.32,.37+math.cos(a)*.32),.026,'woodLight')
        for x in(-.42,.42):beam('cart shafts',(x,-.5,.46),(x,-1.60,.31),.045,'wood')
        crate(0,.12,.53,.66)

KEYS=['stone_floor','stone_wall','wood_floor','grass_tile','cobble_tile','crate','chest','bookshelf','bones','supplies','urn','stair_up','stair_down','portal','door','pillar','torch','barrel','tree','bush','rocks','fountain','smith','armor_shop','alchemy','scroll_shop','general_shop','guild','hut','gate','fence','signpost','lantern','cart','bench']
collections={}
for key in KEYS:
    current=bpy.data.collections.new('ASSET_'+key);bpy.context.scene.collection.children.link(current);collections[key]=current
    building(key) if key in('smith','armor_shop','alchemy','scroll_shop','general_shop','guild','hut') else prop(key)

scn=bpy.context.scene;scn.render.engine='BLENDER_EEVEE';scn.render.film_transparent=True
scn.render.image_settings.file_format='PNG';scn.render.image_settings.color_mode='RGBA'
scn.render.resolution_x=512;scn.render.resolution_y=512;scn.render.resolution_percentage=100
scn.render.image_settings.color_depth='8';scn.view_settings.view_transform='AgX'
scn.world=bpy.data.worlds.new('ambient');scn.world.use_nodes=True;scn.world.node_tree.nodes['Background'].inputs[0].default_value=(.35,.40,.50,1)
scn.world.node_tree.nodes['Background'].inputs[1].default_value=.45
if hasattr(scn,'eevee') and hasattr(scn.eevee,'taa_render_samples'):scn.eevee.taa_render_samples=16
camdata=bpy.data.cameras.new('BakeCamera');camdata.type='ORTHO';cam=bpy.data.objects.new('BakeCamera',camdata);scn.collection.objects.link(cam);scn.camera=cam
for name,loc,energy,size in [('large warm key',(-4,-6,9),550,5),('cool rim',(4,2,7),350,4),('front fill',(1,-5,3),100,3)]:
    ld=bpy.data.lights.new(name,'AREA');ld.energy=energy;ld.shape='DISK';ld.size=size
    lo=bpy.data.objects.new(name,ld);scn.collection.objects.link(lo);lo.location=loc;lo.rotation_euler=(-Vector(loc)).to_track_quat('-Z','Y').to_euler()

manifest=[]
for key,col in collections.items():
    for other,c in collections.items():c.hide_render=other!=key
    tile=key in('stone_floor','wood_floor','grass_tile','cobble_tile')
    pts=[Vector(o.matrix_world@Vector(p)) for o in col.objects for p in o.bound_box]
    minz=min(p.z for p in pts);maxz=max(p.z for p in pts)
    target=Vector((0,0,(maxz+minz)*.5))
    cam.location=target+Vector((0,-8,10)) if not tile else Vector((0,0,12))
    cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
    bpy.context.view_layer.update()
    inv=cam.matrix_world.inverted();projected=[inv@p for p in pts]
    xs=[p.x for p in projected];ys=[p.y for p in projected]
    cx=(max(xs)+min(xs))/2;cy=(max(ys)+min(ys))/2
    cam.location+=cam.rotation_euler.to_quaternion()@Vector((cx,cy,0))
    w=max(xs)-min(xs);h=max(ys)-min(ys)
    camdata.ortho_scale=max(w,h)*1.10 if not tile else 2.0
    scn.render.resolution_x=512;scn.render.resolution_y=512
    if key in('smith','armor_shop','alchemy','scroll_shop','general_shop','guild','hut','gate'):scn.render.resolution_x=768;scn.render.resolution_y=768
    scn.render.filepath=os.path.join(OUT,key+'.png');bpy.ops.render.render(write_still=True)
    manifest.append({'key':key,'sourceCollection':col.name,'meshes':len(col.objects),'png':key+'.png','pivot':[.5,.5],'cameraElevation':51.34 if not tile else 90})
    print('ART_RENDERED '+key,flush=True)
for col in collections.values():col.hide_render=False
# Lay out the source collection as an inspectable asset library, one asset per cell.
for i,(key,col) in enumerate(collections.items()):
    for obj in col.objects:obj.location+=Vector(((i%7)*7,(i//7)*7,0))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SRC,'world_collection.blend'))
with open(os.path.join(SRC,'world_manifest.json'),'w',encoding='utf-8') as fp:json.dump(manifest,fp,ensure_ascii=False,indent=2)
print('WORLD_ART_DONE',flush=True)

