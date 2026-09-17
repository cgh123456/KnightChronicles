"""ART-007: broad lasted leather boots with sewn toe caps and compressed vamps.

Construction follows a shaped shoe last, with an inset layered sole and heel.
The wrinkles are continuous surface displacement; no cylindrical 'laces' are
added.  The collar remains at the agreed ankle attachment height.
"""
import math
from mathutils import Vector, Matrix


def build(api, sign):
    mesh=api['mesh'];tube=api['tube'];parts=api['PARTS']
    start=len(parts)
    prefix='13 | '+str(sign)+' '
    rotation=Matrix.Rotation(sign*.105,3,'Z')
    cx=sign*.440

    def world(x,y,z):
        p=rotation@Vector((x,y,z))
        return (cx+p.x,p.y,p.z)

    def sgnpow(value,power):
        return math.copysign(abs(value)**power,value)

    def footprint(a,scale=1):
        x=.207*sgnpow(math.sin(a),.82)*(1-.033*math.cos(a))
        y=-.086-.278*sgnpow(math.cos(a),.82)
        return x*scale,y*scale

    # Distinct but restrained stacked outsole. A shallow waist underneath
    # leaves a real arch, while the heel and broad toe both contact the floor.
    n=112;verts=[];faces=[]
    for level,scale in ((0,.971),(1,1.014),(2,1.015),(3,1.001),(4,.968)):
        for i in range(n):
            a=i*math.tau/n;x,y=footprint(a,scale)
            arch=.015*math.exp(-((y+.025)/.079)**2)
            z=(.012+arch,.026+arch*.35,.046,.064,.074)[level]
            verts.append(world(x,y,z))
    for k in range(4):
        for i in range(n):faces.append((k*n+i,k*n+(i+1)%n,(k+1)*n+(i+1)%n,(k+1)*n+i))
    faces += [tuple(reversed(range(n))),tuple(4*n+i for i in range(n))]
    mesh(prefix+'Leather stacked sole with recessed waist',verts,faces,'leather_dark',bevel=.0015)

    # A compact heel stack belongs to the sole, rather than a separate block
    # projecting beyond the last. Its cut seam is visible from side and rear.
    heel=[(-.160,.015),(-.174,.098),(-.132,.175),(-.071,.190),
          (.071,.190),(.132,.175),(.174,.098),(.160,.015)]
    vs=[world(x,y,z) for z in (.012,.030) for x,y in heel]
    fs=[tuple(reversed(range(8))),tuple(8+i for i in range(8))]
    fs += [(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]
    mesh(prefix+'Inlaid leather heel stack',vs,fs,'leather_dark',bevel=.006)

    def upper_local(a,t):
        # The broad square-rounded toe blends continuously into a fitted
        # ankle.  Its profile is asymmetric: full toe, compact round heel.
        m=max(0,min(1,(t-.075)/.81));m=m*m*(3-2*m)
        rx=.202*(1-m)+.126*m
        ry=.270*(1-m)+.136*m
        cy=-.086+.108*m
        exp=.82+.18*m
        x=rx*sgnpow(math.sin(a),exp)
        y=cy-ry*sgnpow(math.cos(a),exp)
        z=.070+.232*t
        front=max(0,math.cos(a))
        # Three broad compression folds at the bend of the vamp, softened
        # toward the toe and gathered under the ankle plate.
        crease=.0043*math.sin(t*47+1.3*math.cos(a))
        crease*=math.exp(-((t-.715)/.15)**2)*front**.7
        crease+=.0022*math.sin(a*8+t*9)*math.exp(-((t-.915)/.11)**2)
        x+=math.sin(a)*crease
        y-=math.cos(a)*crease
        # A slight heel pull and side pressure break perfect lathe symmetry.
        z+=.0032*math.sin(a*3+.8)*math.sin(math.pi*t)**2
        return Vector((x,y,z))

    def upper(a,t,offset=0):
        p=upper_local(a,t)
        da=upper_local(a+.0005,t)-upper_local(a-.0005,t)
        dt=upper_local(a,min(1,t+.0005))-upper_local(a,max(0,t-.0005))
        normal=da.cross(dt).normalized()
        if normal.dot(Vector((math.sin(a),-math.cos(a),.4)))<0:normal=-normal
        p+=normal*offset
        return world(*p)

    nr=52;verts=[];faces=[]
    for k in range(nr+1):
        for i in range(n):verts.append(upper(i*math.tau/n,k/nr))
    for k in range(nr):
        for i in range(n):faces.append((k*n+i,k*n+(i+1)%n,(k+1)*n+(i+1)%n,(k+1)*n+i))
    faces.append(tuple(nr*n+i for i in range(n)))
    mesh(prefix+'Last-shaped boot with compressed leather vamp',verts,faces,'leather_dark',thickness=.006)

    # Actual separate toe-cap leather follows the parent last. Its scalloped
    # sewn boundary climbs across the toe and meets the sole at both sides.
    def seam_t(a):
        return .631-.460*(abs(a)/1.31)**1.62
    nu=70;nv=24;verts=[];faces=[]
    for j in range(nv+1):
        for i in range(nu+1):
            a=-1.31+2.62*i/nu
            verts.append(upper(a,.012+(seam_t(a)-.012)*j/nv,.0038))
    for j in range(nv):
        for i in range(nu):
            p=j*(nu+1)+i;faces.append((p,p+1,p+nu+2,p+nu+1))
    mesh(prefix+'Sewn rounded toe cap',verts,faces,'leather_dark',thickness=.003)
    seam=[upper(-1.31+2.62*i/100,seam_t(-1.31+2.62*i/100),.0057) for i in range(101)]
    tube(prefix+'Pressed toe-cap seam',seam,.0016,'leather_light',sides=6)
    # Small separate stitch dashes are flattened visually by their low radius.
    for k in range(29):
        a0=-1.27+2.54*k/29
        aa=[a0+j*.053/3 for j in range(4)]
        pp=[upper(a,seam_t(a)-.018,.0054) for a in aa]
        tube(prefix+'Toe-cap waxed stitch',pp,.00105,'leather_light',sides=5)

    # A narrow welt is integrated into the upper/sole junction. Dense stitches
    # are kept subtle so the broad boot shape stays dominant at game scale.
    pts=[]
    for i in range(n):
        a=i*math.tau/n;x,y=footprint(a,1.002)
        pts.append(world(x,y,.063))
    tube(prefix+'Inset stitched sole welt',pts,.0027,'leather_light',True,sides=8)
    for k in range(64):
        aa=[math.tau*(k+.12+.38*j/2)/64 for j in range(3)]
        pp=[]
        for a in aa:
            x,y=footprint(a,.998);pp.append(world(x,y,.066))
        tube(prefix+'Welt saddle stitch',pp,.00085,'leather_light',sides=5)

    # A sewn heel seam and softly rolled ankle lip, both following the leather
    # rather than floating over it as the earlier cylindrical instep strips.
    tube(prefix+'Back heel stitched seam',[upper(math.pi,.12+.80*j/50,.0027) for j in range(51)],.0017,'leather_light',sides=6)
    tube(prefix+'Soft ankle collar roll',[upper(i*math.tau/96,.980,.0045) for i in range(96)],.0040,'leather',True,sides=8)
    # Short compression lines at the vamp are recessed/dark and taper out;
    # the actual shape is already carried by the continuous surface above.
    for k,t in enumerate((.700,.776)):
        aa=[-.81+1.62*j/54 for j in range(55)]
        pp=[upper(a,t+.016*math.cos(a*2+k),.0007) for a in aa]
        tube(prefix+'Pressed vamp fold '+str(k),pp,.0012,'leather_dark',sides=6)
    for ob in parts[start:]:
        ob['leg_side']=sign
        ob['boot_side']=sign
        ob['art007_boot']=True
    return parts[start:]
