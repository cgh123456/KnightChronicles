"""ART-007: sculpted travel equipment built against the user knight concept sheet.

Public interface: build(materials, sheathed=True) -> list of MESH objects.
Character height 3.2, Z up, front -Y; the caller owns scene and materials.
Only the removable sheathed sword hilt is tagged reference_sheathed_sword.
"""
import math
import bpy
import bmesh
from mathutils import Vector

_objects = []
_materials = {}


def _gauss(value, width):
    return math.exp(-(value / width) ** 2)


def _catmull(values, t):
    """Uniform scalar Catmull-Rom profile; clamp the terminal controls."""
    q=max(0.0,min(len(values)-1.000001,t*(len(values)-1)))
    j=int(q);f=q-j
    p0=values[max(0,j-1)];p1=values[j]
    p2=values[min(len(values)-1,j+1)];p3=values[min(len(values)-1,j+2)]
    return .5*((2*p1)+(-p0+p2)*f+(2*p0-5*p1+4*p2-p3)*f*f+(-p0+3*p1-3*p2+p3)*f*f*f)


def _mesh(name, vertices, faces, material, smooth=False, bevel=0):
    data = bpy.data.meshes.new('RefinedEquipment_' + name)
    data.from_pydata(vertices, [], faces)
    data.update()
    bm = bmesh.new(); bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(data); bm.free()
    obj = bpy.data.objects.new('RefinedEquipment_' + name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.data.materials.append(_materials[material])
    for face in data.polygons: face.use_smooth = smooth
    if bevel:
        mod = obj.modifiers.new('Softened handworked edges', 'BEVEL')
        mod.width = bevel; mod.segments = 3
        obj.modifiers.new('Weighted edge normals', 'WEIGHTED_NORMAL')
    _objects.append(obj)
    return obj


def _box(name, center, size, material, bevel=.005):
    x, y, z = [v * .5 for v in size]
    vs = [(a,b,c) for a in (-x,x) for b in (-y,y) for c in (-z,z)]
    fs = [(0,4,6,2),(1,3,7,5),(0,1,5,4),(2,6,7,3),(0,2,3,1),(4,5,7,6)]
    obj = _mesh(name, vs, fs, material, bevel=bevel)
    obj.location = center
    return obj


def _tube_geometry(points, radius, sides=8):
    pts = [Vector(p) for p in points]; vs=[]; fs=[]
    for i,p in enumerate(pts):
        tangent=(pts[min(i+1,len(pts)-1)]-pts[max(0,i-1)]).normalized()
        ref=Vector((1,0,0)) if abs(tangent.x)<.85 else Vector((0,1,0))
        u=tangent.cross(ref).normalized(); v=tangent.cross(u).normalized()
        for j in range(sides):
            a=j*math.tau/sides
            vs.append(tuple(p+(u*math.cos(a)+v*math.sin(a))*radius))
    for i in range(len(pts)-1):
        for j in range(sides):
            fs.append((i*sides+j,i*sides+(j+1)%sides,(i+1)*sides+(j+1)%sides,(i+1)*sides+j))
    fs += [tuple(range(sides-1,-1,-1)),tuple((len(pts)-1)*sides+j for j in range(sides))]
    return vs,fs


def _tube(name, points, radius, material, sides=8):
    vs,fs=_tube_geometry(points,radius,sides)
    return _mesh(name,vs,fs,material,smooth=True)


def _stitches(name, points, step=.033, radius=.0016, material='thread'):
    """Separate short stitches share one mesh to keep the asset editable and light."""
    pts=[Vector(p) for p in points];vs=[];fs=[];lengths=[0.0]
    for a,b in zip(pts,pts[1:]):lengths.append(lengths[-1]+(b-a).length)
    def at_distance(distance):
        for i in range(len(pts)-1):
            if distance<=lengths[i+1]:
                return pts[i].lerp(pts[i+1],(distance-lengths[i])/max(1e-9,lengths[i+1]-lengths[i]))
        return pts[-1]
    count=max(1,round(lengths[-1]/step));spacing=lengths[-1]/count
    for j in range(count):
        p=at_distance(spacing*(j+.17));q=at_distance(spacing*(j+.64))
        vv,ff=_tube_geometry([p,q],radius,5);offset=len(vs)
        vs.extend(vv);fs.extend(tuple(i+offset for i in f) for f in ff)
    return _mesh(name,vs,fs,material,smooth=True)


def _ribbon(name, points, width=.064, thickness=.018, material='leather', taper=False):
    """A flat leather ribbon with a length-limited smooth centreline fitted to cloth."""
    raw=[Vector(p) for p in points];pts=[]
    # Length-limited Hermite tangents keep broad shoulder curves while preventing
    # the short blanket arc from folding back where it meets a long strap tail.
    for i in range(len(raw)-1):
        p0=raw[max(0,i-1)];p1=raw[i];p2=raw[i+1];p3=raw[min(len(raw)-1,i+2)]
        segment=(p2-p1).length
        v1=(p2-p0).normalized()*min((p1-p0).length or segment,segment)
        v2=(p3-p1).normalized()*min((p3-p2).length or segment,segment)
        samples=max(5,math.ceil(segment/.015))
        for sub in range(samples):
            t=sub/samples;t2=t*t;t3=t2*t
            pts.append(p1*(2*t3-3*t2+1)+v1*(t3-2*t2+t)+p2*(-2*t3+3*t2)+v2*(t3-t2))
    pts.append(raw[-1]);vs=[];fs=[]
    for i,p in enumerate(pts):
        tangent=(pts[min(i+1,len(pts)-1)]-pts[max(0,i-1)]).normalized()
        across=Vector((1,0,0));across=(across-tangent*across.dot(tangent)).normalized()
        normal=across.cross(tangent).normalized()
        w=width*(1+.010*math.sin(i*.37)+.006*math.sin(i*.83))
        if taper:
            t=i/(len(pts)-1)
            w*=1-.36*max(0,(t-.89)/.11)
        for sx,sy in ((-1,-1),(1,-1),(1,1),(-1,1)):
            vs.append(tuple(p+across*w*sx*.5+normal*thickness*sy*.5))
    for i in range(len(pts)-1):
        for j in range(4):fs.append((i*4+j,i*4+(j+1)%4,(i+1)*4+(j+1)%4,(i+1)*4+j))
    fs += [(3,2,1,0),tuple((len(pts)-1)*4+j for j in range(4))]
    return _mesh(name,vs,fs,material,bevel=.003)


def _buckle(name,x,y,z,w=.085,h=.105,front=False):
    # Closed open rectangular frame; softened bends match the concept's small brass buckles.
    d=-1 if front else 1
    pts=[(x-w*.5+.012,y,z-h*.5),(x+w*.5-.012,y,z-h*.5),
         (x+w*.5,y,z-h*.5+.012),(x+w*.5,y,z+h*.5-.012),
         (x+w*.5-.012,y,z+h*.5),(x-w*.5+.012,y,z+h*.5),
         (x-w*.5,y,z+h*.5-.012),(x-w*.5,y,z-h*.5+.012),
         (x-w*.5+.012,y,z-h*.5)]
    _tube(name+' rounded brass frame',pts,.008,'gold',8)
    _tube(name+' pin',[(x,y+d*.005,z+h*.42),(x,y+d*.009,z-h*.17)],.0048,'gold',8)


def _rivet(name,loc,radius=.008,normal=(0,1,0),material='gold'):
    axis=Vector(normal).normalized();u=axis.cross(Vector((0,0,1)))
    if u.length<.01:u=axis.cross(Vector((1,0,0)))
    u.normalize();v=axis.cross(u).normalized();vs=[tuple(Vector(loc)+axis*.003)];fs=[]
    for j in range(10):
        a=j*math.tau/10;vs.append(tuple(Vector(loc)+u*math.cos(a)*radius+v*math.sin(a)*radius))
    for j in range(10):fs.append((0,j+1,(j+1)%10+1))
    fs.append(tuple(range(10,0,-1)))
    return _mesh(name,vs,fs,material,smooth=True)


def _power(v,p):return math.copysign(abs(v)**p,v)


def _pack_body():
    # A continuous dense leather volume. The front panel stays stiff between
    # the two tension straps; corner valleys and a loaded lower gusset are
    # sculpted in the actual surface, so grazing light reads the construction.
    vs=[];fs=[];sides=128;rows=66
    for row in range(rows+1):
        t=row/rows
        for j in range(sides):
            vs.append(_pack_surface(j*math.tau/sides,t))
    for row in range(rows):
        for j in range(sides):
            a=row*sides+j;b=row*sides+(j+1)%sides
            fs.append((a,b,b+sides,a+sides))
    # The hidden caps are triangulated around an interior point, never left
    # as a concave n-gon for exporters to triangulate unpredictably.
    for row,reverse in ((0,True),(rows,False)):
        start=len(vs);z=1.328 if reverse else 1.975
        vs.append((0,.472,z))
        for j in range(sides):
            tri=(start,row*sides+j,row*sides+(j+1)%sides)
            fs.append(tuple(reversed(tri)) if reverse else tri)
    _mesh('Sculpted loaded leather satchel volume',vs,fs,'leather',smooth=True)
    # The long welt joins side gussets to the rear-facing broad panel.
    for s in (-1,1):
        a=.81 if s>0 else math.pi-.81
        seam=[_pack_surface(a,.075+i*.845/46) for i in range(47)]
        _tube('Side panel raised leather welt '+str(s),[(x,y+.003,z) for x,y,z in seam],.0042,'leather_dark',8)
        _stitches('Side panel inset saddle stitch '+str(s),[(x-s*.010,y+.005,z) for x,y,z in seam],.025,.00125,'leather_light')
        # Same sewn gusset continues around its bottom corner.
        lower=[_pack_surface((.25+i*1.10/22) if s>0 else math.pi-(.25+i*1.10/22),.076) for i in range(23)]
        _tube('Loaded lower gusset piping '+str(s),[(x,y+.002,z) for x,y,z in lower],.0043,'leather_light',8)
    bottom=[_pack_surface(.62+i*(math.pi-1.24)/64,.046) for i in range(65)]
    _tube('Leather base seam rolled lip',[(x,y+.004,z) for x,y,z in bottom],.004,'leather_dark',8)
    _stitches('Leather base saddle stitches',[(x,y+.006,z+.013) for x,y,z in bottom],.026,.0013,'leather_light')
    # Short creases radiating out of loaded corners are grooves in the volume,
    # with just a thin worn rim on their lower-facing lip.
    for s in (-1,1):
        for k in range(2):
            pts=[]
            for i in range(16):
                t=i/15;x=s*(.29+.067*t);z=1.40+k*.058+.020*t
                y=_rear_y(x,z)+.0018
                pts.append((x,y,z))
            _tube('Worn compressed corner crease '+str(s)+' '+str(k),pts,.0011,'leather_light',5)


def _pack_surface(a,t):
    z=1.328+.647*t
    # Slightly asymmetric load, squared panel middle and broad turned corners.
    rx=_catmull([.318,.387,.400,.397,.393,.381,.309],t)
    ry=_catmull([.105,.157,.169,.169,.162,.151,.072],t)
    x=rx*_power(math.cos(a),.33)
    y=.474+ry*_power(math.sin(a),.38)
    rear=max(0,math.sin(a))**1.4
    side=abs(math.cos(a))**7
    x+=.0048*math.sin(z*12+a*.7)*math.sin(a)**2
    # Accordion pressure on the side gusset is restrained and concentrated
    # beside the seams, avoiding a randomly crumpled or melted bag.
    x+=math.copysign(1,x)*side*(.008*_gauss(z-1.56,.18)*math.sin((z-1.35)*29+.7)
        -.011*_gauss(z-1.43,.028)+.008*_gauss(z-1.395,.025))
    y+=rear*(.007*_gauss(x,.28)*_gauss(z-1.54,.20))
    for s in (-1,1):
        d=x-s*.225
        y-=rear*.007*_gauss(d,.045)*_gauss(z-1.58,.32)
        # Broad pinched diagonal stress creases rising from the base corners.
        cx=s*x
        corner=_gauss(cx-.329,.058)
        for k in range(3):
            line=1.391+k*.061+.32*(cx-.29)
            y+=rear*corner*(-.0056*_gauss(z-line,.009)+.0034*_gauss(z-line+.014,.014))
    # Lower panel bends over its packed bottom and sags between belt contacts.
    y+=rear*.012*_gauss(z-1.372,.032)*(.48+.52*math.cos(x*5))
    z+=.0027*math.sin(a*5+.4)*math.sin(t*math.pi)+.002*math.cos(a*3)*_gauss(t-.05,.08)
    return (x,y,z)


def _rear_y(x,z):
    """Inverse sample of the rear panel for small attached crease details."""
    t=max(0,min(1,(z-1.328)/.647))
    rx=_catmull([.318,.387,.400,.397,.393,.381,.309],t)
    c=math.copysign(min(.99999,abs(x/rx)**(1/.33)),x)
    return _pack_surface(math.acos(c),t)[1]


def _flap_surface(u,v):
    x=(u-.5)*(.688+.063*math.sin(min(1,v/.23)*math.pi*.5))
    # Broad leather top turns onto the rear plane. The last third is an uneven
    # hanging cut edge, while its two belt contact points are visibly indented.
    z=1.986-v*.327-.015*math.sin(u*math.pi)*v*v+.005*math.sin(u*11+.3)*v**3
    y=.554+.096*(1-math.exp(-v/.18))+.025*math.sin(v*math.pi*.85)+.006*math.sin(u*math.pi)
    y+=.0028*math.sin(u*16+v*7)*math.sin(v*math.pi)
    for center in (.195,.805):
        dist=u-center
        y-=.013*_gauss(dist,.063)*(.25+.75*math.sin(v*math.pi*.87))
        for k in range(2):
            line=.43+k*.22+(dist if center<.5 else -dist)*.60
            local=_gauss(dist,.16)*math.sin(v*math.pi)
            y+=local*(.0070*_gauss(v-line,.043)-.0048*_gauss(v-line-.046,.026))
        # The tip under each strap stays taut while the interval curls out.
        z+=.007*_gauss(dist,.07)*v**5
    y+=.0075*math.sin(u*math.pi*3+.35)*v**6
    y+=.007*_gauss(u,.055)*v**4+.0045*_gauss(u-1,.07)*v**3
    return (x,y,z)


def _closing_flap():
    nx=80;ny=52;vs=[];fs=[]
    for row in range(ny+1):
        for col in range(nx+1):vs.append(_flap_surface(col/nx,row/ny))
    for row in range(ny):
        for col in range(nx):
            a=row*(nx+1)+col;fs.append((a,a+1,a+nx+2,a+nx+1))
    o=_mesh('Broad worn satchel closing flap',vs,fs,'leather',smooth=True)
    sol=o.modifiers.new('Leather flap physical thickness','SOLIDIFY');sol.thickness=.012;sol.offset=-1
    bev=o.modifiers.new('Rolled flap edge','BEVEL');bev.width=.0025;bev.segments=2
    edge=[_flap_surface(u,1) for u in [i/80 for i in range(81)]]
    _tube('Flap uneven worn cut edge',[(x,y+.002,z) for x,y,z in edge],.0032,'leather_light',8)
    for u in (.022,.978):
        pts=[_flap_surface(u,.07+i*.88/36) for i in range(37)]
        _stitches('Flap side stitching',[(x,y+.004,z) for x,y,z in pts],.026,.00125,'leather_light')
    pts=[_flap_surface(.03+i*.94/48,.956) for i in range(49)]
    _stitches('Flap lower edge stitching',[(x,y+.004,z) for x,y,z in pts],.026,.00125,'leather_light')


def _blanket():
    # One genuinely rolled sheet, with physical thickness throughout its
    # 2.65 turns. Both ends expose curved fabric sections separated by narrow
    # dark recesses; there are no flat discs with decorative spiral tubes.
    rows=54;turn_steps=280;vs=[];fs=[]
    def index(side,row,col):return side*(rows+1)*(turn_steps+1)+row*(turn_steps+1)+col
    for side in (-1,1):
        for row in range(rows+1):
            x=-.449+.898*row/rows
            for col in range(turn_steps+1):
                vs.append(_blanket_point(x,col/turn_steps,side))
    for side in (0,1):
        for row in range(rows):
            for col in range(turn_steps):
                a=index(side,row,col);quad=(a,a+1,a+turn_steps+2,a+turn_steps+1)
                fs.append(tuple(reversed(quad)) if side==0 else quad)
    # Connect the thickness around four sheet edges. The two end cross
    # sections remain real solid spiral strips rather than end caps.
    for row in (0,rows):
        for col in range(turn_steps):
            fs.append((index(0,row,col),index(0,row,col+1),index(1,row,col+1),index(1,row,col)))
    for col in (0,turn_steps):
        for row in range(rows):
            fs.append((index(0,row,col),index(0,row+1,col),index(1,row+1,col),index(1,row,col)))
    _mesh('Genuine rolled thick linen with layered ends',vs,fs,'cream',smooth=True)
    # The outer fabric cut edge has an uneven folded lip with inset thread.
    # Only the final wrap carries seams, matching a functional traveling roll.
    edge=[_blanket_point(-.449+.898*i/70,1,1) for i in range(71)]
    _tube('Blanket final wrap rolled fabric edge',edge,.0028,'cream',8)
    stitch=[_blanket_point(-.433+.866*i/66,.993,1) for i in range(67)]
    _stitches('Blanket outer hem small stitches',stitch,.021,.0009,'thread')
    for s in (-1,1):
        # Inset hem follows the outer revolution, turning with its natural
        # lobes and the sheet compression rather than a perfect circle.
        seam=[_blanket_point(s*.438,.635+i*.365/110,1) for i in range(111)]
        _stitches('Blanket end binding stitches '+str(s),seam,.022,.001,'thread')


def _blanket_point(x,t,side):
    a=.24+t*math.tau*2.65
    radius=.019+.105*t
    # Broad folds follow the sheet's rolling direction and are pulled flat
    # under each tie; the unsupported ends retain their thicker soft profile.
    compression=sum(.080*_gauss(x-s*.225,.047) for s in (-1,1))
    end_puff=.024*_gauss(abs(x)-.404,.055)
    shape=1-compression+end_puff
    broad=(.0022*math.sin(a*3+x*10)+.0015*math.sin(a*7-x*19))*t
    creases=0
    for s in (-1,1):
        dx=x-s*.225
        creases+=.0025*_gauss(abs(dx)-.055,.038)*math.sin(a*9+dx*28)*t
    thickness=.0325+.0016*math.sin(a*2+x*12)
    r=(radius+side*thickness*.5)*shape+broad+creases
    # A small axial scallop means the visible end is folded fabric, not a
    # perfectly machined spiral; it decays rapidly into the width of the roll.
    endmask=_gauss(abs(x)-.449,.024)
    xx=x+math.copysign(1,x)*endmask*(.0025*math.sin(a*2.1)+.0015*math.cos(a*5.4))
    cy=.474+.0025*math.sin(x*9)*t
    cz=2.115+.003*math.cos(x*8)*t
    return (xx,cy+math.cos(a)*r,cz+math.sin(a)*r)


def _retaining_straps():
    for s in (-1,1):
        xx=s*.225;pts=[]
        for i in range(25):
            a=math.pi-i*math.pi/24
            # Fit each belt directly to the compressed outer wrap, so its
            # contact shadow does not reveal the floating constant-radius arc.
            phase=(a-.24)%math.tau
            phase+=math.tau*math.floor((2.65*math.tau-phase)/math.tau)
            p=_blanket_point(xx,phase/(2.65*math.tau),1)
            pts.append((xx,p[1]+.010*math.cos(a),p[2]+.010*math.sin(a)))
        pts += [(xx,.650,1.988),(xx,.672,1.882),(xx,.677,1.744),
                (xx+s*.002,.680,1.633),(xx+s*.006,.665,1.568),
                (xx+s*.009,.651,1.47),(xx+s*.003,.649,1.367)]
        _ribbon('Blanket and satchel flat retaining strap '+str(s),pts,.062,.017,'leather_dark',taper=True)
        _buckle('Satchel buckle '+str(s),xx,.695,1.615,.078,.098)
        _box('Leather buckle keeper '+str(s),(xx,.690,1.735),(.078,.018,.022),'leather_light',.004)
        # Short turned belt tongue at the pin, with a tangible bend and a
        # second layer tucked into the keeper above the buckle.
        _ribbon('Turned buckle tongue '+str(s),[(xx,.684,1.762),(xx,.690,1.71),(xx,.696,1.675),(xx,.694,1.644)],.049,.010,'leather',taper=True)
        for z in (1.417,1.465,1.511):
            _box('Strap punched adjustment hole '+str(s),(xx+s*.006,.665-(1.51-z)*.045,z),(.005,.003,.008),'dark',.0007)
        for z,y in ((1.854,.684),(1.955,.668)):_rivet('Small strap brass rivet '+str(s),(xx,y,z),.005)
        # Fine long edge seams, brown thread as in worn saddle leather.
        for dx in (-.023,.023):
            _stitches('Retaining belt edge seam '+str(s),[(xx+dx,.686,1.78),(xx+dx,.683,1.87),(xx+dx,.662,1.974)],.028,.001,'leather_light')


def _side_sleeve():
    # The slim side sleeve visible in the side and three-quarter concept view.
    # It stays within x=.46 so it does not inflate the backpack silhouette.
    cy=.465;vs=[];fs=[];ns=64;rows=32
    for row in range(rows+1):
        t=row/rows;z=1.425+.40*t
        rx=_catmull([.027,.049,.052,.048,.025],t)
        ry=_catmull([.072,.103,.109,.108,.065],t)
        for j in range(ns):
            a=j*math.tau/ns
            x=.399+rx*_power(math.cos(a),.45)
            x+=max(0,math.cos(a))**3*.004*math.sin(t*14+a)*math.sin(t*math.pi)
            vs.append((x,cy+ry*_power(math.sin(a),.36),z+.002*math.sin(a*3)*math.sin(t*math.pi)))
    for i in range(rows):
        for j in range(ns):fs.append((i*ns+j,i*ns+(j+1)%ns,(i+1)*ns+(j+1)%ns,(i+1)*ns+j))
    fs += [tuple(range(ns-1,-1,-1)),tuple(rows*ns+j for j in range(ns))]
    _mesh('Single fitted backpack side sleeve',vs,fs,'leather_dark',smooth=True)
    seam=[(.441,.382,1.468),(.450,.371,1.50),(.453,.371,1.65),(.447,.378,1.768)]
    _tube('Side sleeve stitched turned seam',seam,.0031,'leather',6)
    _stitches('Side sleeve seam',[(x+.003,y+.008,z) for x,y,z in seam],.026,.0011,'leather_light')
    # A single short closure flap belongs to this existing sleeve, it is not
    # an additional pouch or an ornamental piece absent from the concept.
    vs=[];fs=[];nx=20;ny=20
    for row in range(ny+1):
        v=row/ny
        for col in range(nx+1):
            u=col/nx
            y=.465+(u-.5)*.189
            z=1.836-v*.118-.009*math.sin(math.pi*u)*v**2
            x=.443+.014*math.sin(v*math.pi*.72)+.003*math.sin(u*9+v*4)*v
            vs.append((x,y,z))
    for row in range(ny):
        for col in range(nx):
            i=row*(nx+1)+col;fs.append((i,i+1,i+nx+2,i+nx+1))
    o=_mesh('Small side sleeve hinged flap',vs,fs,'leather',smooth=True)
    mod=o.modifiers.new('Side flap leather thickness','SOLIDIFY');mod.thickness=.010
    _rivet('Side sleeve closure stud',(.463,.466,1.734),.006,normal=(1,0,0),material='gold')


def _shoulder_straps():
    for s in (-1,1):
        # Front path follows the new tunic ellipse, with a small leather stand-off.
        pts=[(s*.30,.32,1.62),(s*.29,.31,1.94),(s*.278,.22,2.08),
             (s*.278,.04,2.15),(s*.285,-.11,2.12),(s*.319,-.198,1.975),
             (s*.332,-.232,1.79),(s*.330,-.243,1.65),(s*.324,-.231,1.49),(s*.314,-.217,1.38)]
        _ribbon('Fitted flat shoulder harness '+str(s),pts,.067,.022,'leather',taper=False)
        _buckle('Shoulder harness adjustment '+str(s),s*.329,-.252,1.887,.078,.096,front=True)
        _box('Shoulder leather keeper '+str(s),(s*.331,-.248,1.798),(.082,.020,.020),'leather_dark',.003)
        for z,y in ((1.46,-.244),(1.54,-.250),(1.62,-.257)):
            _box('Harness punched slot '+str(s),(s*.326,y,z),(.006,.004,.010),'dark',.0007)
        for dx in (-.023,.023):
            _stitches('Harness leather edge seam '+str(s),[(s*.315+dx,-.233,1.415),(s*.329+dx,-.255,1.61),(s*.332+dx,-.251,1.756)],.038,.00125,'leather_light')


def _ellipse_rings(name,centers,a,b,u,v,material):
    vs=[];fs=[];ns=16
    for center,scale in centers:
        center=Vector(center)
        for j in range(ns):
            t=j*math.tau/ns;vs.append(tuple(center+u*math.cos(t)*a*scale+v*math.sin(t)*b*scale))
    for i in range(len(centers)-1):
        for j in range(ns):fs.append((i*ns+j,i*ns+(j+1)%ns,(i+1)*ns+(j+1)%ns,(i+1)*ns+j))
    fs += [tuple(range(ns-1,-1,-1)),tuple((len(centers)-1)*ns+j for j in range(ns))]
    return _mesh(name,vs,fs,material,smooth=True,bevel=.002)


def _scabbard(sheathed):
    top=Vector((.525,.017,1.328));bottom=Vector((.659,.088,.419));axis=(top-bottom).normalized()
    u=Vector((axis.z,0,-axis.x)).normalized();v=axis.cross(u).normalized()
    levels=[(0,1),(.035,1),(.20,.98),(.77,.87),(.90,.78),(.97,.49),(1,.06)]
    centers=[(top+(bottom-top)*f,sc) for f,sc in levels]
    _ellipse_rings('Flattened brown leather scabbard',centers,.060,.032,u,v,'leather_dark')
    _ellipse_rings('Scabbard cast brass mouth',[(top+axis*.016,1.04),(top-axis*.026,1.04)],.062,.035,u,v,'gold')
    _ellipse_rings('Scabbard tapered brass chape',[(top+(bottom-top)*f,sc) for f,sc in ((.90,.82),(.96,.61),(1,.10))],.061,.035,u,v,'gold')
    seam=[tuple(top+(bottom-top)*f+u*.044+v*.025) for f in (.08,.28,.49,.70,.86)]
    _stitches('Scabbard long side stitches',seam,.034,.0015,'leather_light')
    for f in (.075,.175):
        center=top+(bottom-top)*f
        _ellipse_rings('Scabbard broad leather suspension collar',[(center-axis*.020,1),(center+axis*.020,1)],.063,.036,u,v,'leather')
        # A flat diagonal tab runs to the belt and supports each collar.
        _ribbon('Scabbard belt suspension tab',[(.443,-.026,1.38-f*.28),(.46,-.005,1.365-f*.65),tuple(center-u*.028)],.037,.015,'leather')
    if not sheathed:return
    first=len(_objects)
    guard_center=top+axis*.031
    # Curved shallow golden crossguard has a rectangular forged profile.
    vs=[];fs=[]
    for i in range(13):
        q=-1+2*i/12;center=guard_center+u*(q*.141)+axis*(.019*q*q)
        for aa,bb in ((-1,-1),(1,-1),(1,1),(-1,1)):
            vs.append(tuple(center+v*(aa*.023)+axis*(bb*.015)))
    for i in range(12):
        for j in range(4):fs.append((i*4+j,i*4+(j+1)%4,(i+1)*4+(j+1)%4,(i+1)*4+j))
    fs += [(3,2,1,0),tuple(48+j for j in range(4))]
    _mesh('Sheathed sword curved golden crossguard',vs,fs,'gold',smooth=False,bevel=.006)
    gripbottom=guard_center+axis*.022;griptop=gripbottom+axis*.166
    _ellipse_rings('Sheathed sword wrapped leather grip',[(gripbottom,.93),(gripbottom+axis*.05,1),(griptop,.89)],.034,.030,u,v,'leather_dark')
    wraps=[]
    for i in range(100):
        t=i/99;a=t*math.tau*5.4;p=gripbottom+axis*(.008+t*.150)
        wraps.append(tuple(p+u*math.cos(a)*.0345+v*math.sin(a)*.0305))
    _tube('Sheathed sword diagonal leather binding',wraps,.0035,'leather_light',6)
    for center in (gripbottom+axis*.006,griptop-axis*.004):
        _ellipse_rings('Sheathed sword golden grip ferrule',[(center-axis*.011,1),(center+axis*.011,1)],.036,.032,u,v,'gold')
    c=griptop+axis*.033
    vs=[tuple(c+axis*.047),tuple(c-axis*.037),tuple(c+u*.046),tuple(c+v*.041),tuple(c-u*.046),tuple(c-v*.041)]
    fs=[(0,2,3),(0,3,4),(0,4,5),(0,5,2),(1,3,2),(1,4,3),(1,5,4),(1,2,5)]
    _mesh('Sheathed sword faceted gold pommel',vs,fs,'gold',bevel=.002)
    for obj in _objects[first:]:obj['reference_sheathed_sword']=True


def build(materials, sheathed=True):
    """Build a new reference-derived equipment set using caller-owned material keys."""
    global _objects,_materials
    required={'leather','leather_light','leather_dark','gold','cream','thread','dark'}
    missing=required-set(materials)
    if missing:raise KeyError('Missing reference equipment materials: '+', '.join(sorted(missing)))
    _objects=[];_materials=materials
    _pack_body();_closing_flap();_blanket();_retaining_straps();_side_sleeve();_shoulder_straps();_scabbard(sheathed)
    for obj in _objects:
        obj['artTask']='ART-007C'
        obj['reference']='user-knight-concept-sheet-2026-09-17'
        obj['role']='refined_backpack_blanket_harness_scabbard'
    return list(_objects)
