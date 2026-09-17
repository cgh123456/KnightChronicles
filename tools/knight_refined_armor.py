"""ART-007A. Forged, articulated armour rebuilt against the supplied sheet.

The shoulders are short polygon-domed shells with an inward apex. Independent
silver border surfaces have physical width, and the narrower gilt line belongs
to a separate, downturned lower lame.  Arms and legs keep the ART-007 attachment
points; the assembler supplies materials and ordinary mesh helpers.
"""
import math
from mathutils import Vector


def _api(api):
    globals().update({k: api[k] for k in ('mesh', 'tube', 'uvball', 'box',
        'loft', 'cuff', 'tapered_limb', 'PARTS', 'M')})


def _grid(name, fn, nu, nv, material='steel', thickness=.016):
    verts = [fn(i/nu, j/nv) for j in range(nv+1) for i in range(nu+1)]
    faces = [(j*(nu+1)+i, j*(nu+1)+i+1,
              (j+1)*(nu+1)+i+1, (j+1)*(nu+1)+i)
             for j in range(nv) for i in range(nu)]
    ob = mesh(name, verts, faces, material, thickness=thickness)
    return ob, verts


def _offset(fn, u, v, amount, up=None):
    """Displace a strip along its parent surface, including its sloping edges."""
    p = Vector(fn(u, v))
    du = Vector(fn(min(1, u+.0005), v))-Vector(fn(max(0, u-.0005), v))
    dv = Vector(fn(u, min(1, v+.0005)))-Vector(fn(u, max(0, v-.0005)))
    n = du.cross(dv).normalized()
    if up is not None and n.dot(Vector(up)) < 0:
        n = -n
    return p+n*amount


def _frame(a, b):
    a, b = Vector(a), Vector(b)
    axis = (b-a).normalized()
    front = Vector((0, -1, 0))
    front = (front-axis*front.dot(axis)).normalized()
    side = axis.cross(front).normalized()
    return a, b-a, side, front


def _round_outline(points, steps=5, rounding=.11):
    result=[]
    points=[Vector(p) for p in points]
    for k, p in enumerate(points):
        a=p.lerp(points[k-1], rounding)
        b=p.lerp(points[(k+1)%len(points)], rounding)
        for j in range(steps):
            t=j/steps
            result.append(a*(1-t)**2 + p*2*t*(1-t) + b*t*t)
        nxt=points[(k+1)%len(points)].lerp(p, rounding)
        for j in range(steps):
            result.append(b.lerp(nxt, j/steps))
    return result


def _shoulder(sign):
    prefix='08 | '+str(sign)+' '
    # Rounded polygon dome, with its apex tucked inward. The entire front
    # cheek is metal down to the cut hem; this is not an open saddle/roof.
    def shell(r, angle, under=0):
        sn,co=math.sin(angle),math.cos(angle)
        sx=math.copysign(abs(sn)**.88,sn)
        sy=math.copysign(abs(co)**.88,co)
        bx=(.210 if sn>=0 else .182)*sx
        by=-.232*sy
        hem=1.904+.092*max(0,-sn)**1.6+.025*max(0,-co)+.009*abs(co)**6
        x=.565*(1-r)+(.596+bx)*r
        y=by*r
        profile=1-math.sqrt(max(0,1-min(r,1)**2))+max(0,r-1)*1.5
        z=2.226-(2.226-hem)*profile-under
        return Vector((sign*x,y,z))
    uvball(prefix+'Soft shoulder foundation',(sign*.573,.006,1.985),
           (.164,.183,.129),'padding',36,22)

    na=104;nr=28
    verts=[shell(0,0)];faces=[]
    for k in range(1,nr+1):
        for j in range(na):verts.append(shell(k/nr,j*math.tau/na))
    for j in range(na):faces.append((0,1+j,1+(j+1)%na))
    for k in range(nr-1):
        for j in range(na):faces.append((1+k*na+j,1+k*na+(j+1)%na,
            1+(k+1)*na+(j+1)%na,1+(k+1)*na+j))
    mesh(prefix+'Closed convex forged shoulder shell',verts,faces,'steel',thickness=.021)

    # A physically separate short lower lame lies under the outer wall. Its
    # exposure is only 35 mm, so the shoulder no longer reads as a broad eave.
    def lower(u,v):
        return shell(.83+.195*u,v*math.tau,.038)
    _grid(prefix+'Tucked articulated lower shoulder plate',lower,8,104,'steel_dark',.021)
    def lower_border(u,v):
        p=shell(.967+.059*u,v*math.tau,.029)
        return p
    _grid(prefix+'Lower shoulder silver edge',lower_border,5,104,'steel_light',.005)
    tube(prefix+'Narrow gilt shoulder inlay',[shell(1.025,j*math.tau/104,.026) for j in range(104)],.0028,'trim',True,sides=8)

    # Broad forged silver bevel strips are surfaces over the real domed plate.
    def rim(u,v):
        r=.883+.117*u;a=v*math.tau
        p=shell(r,a)
        radial=Vector((sign*math.sin(a),-math.cos(a),.70)).normalized()
        return p+radial*.012
    _grid(prefix+'Wide slanted shoulder silver border',rim,8,104,'steel_light',.005)
    tube(prefix+'Outer shoulder folded silver lip',[shell(1,j*math.tau/104)+Vector((sign*math.sin(j*math.tau/104),-math.cos(j*math.tau/104),.4))*.010 for j in range(104)],.0035,'steel_light',True,sides=8)

    # Slightly proud neck-side panel seams follow the actual dome all the way
    # to its cut border. Their width grows with radius like a forged fan.
    for angle in (-.66,math.pi+.66):
        def seam_panel(u,v,angle=angle):
            r=.12+.84*v;a=angle+(u-.5)*.12
            return shell(r,a)+Vector((0,0,.010))
        _grid(prefix+'Neck-side overlapping shoulder panel',seam_panel,4,30,'steel',.005)
        pts=[shell(.12+.84*j/40,angle)+Vector((0,0,.014)) for j in range(41)]
        tube(prefix+'Neck-side folded ridge',pts,.0043,'steel_light',sides=8)
    for angle in (-.70,.85,math.pi-.85,math.pi+.70):
        p=shell(.935,angle)+Vector((sign*math.sin(angle),-math.cos(angle),.5))*.015
        uvball(prefix+'Flush shoulder fastening rivet',p,(.006,.006,.006),'steel_light',12,8)


def _upper_sleeve(sign,shoulder,elbow):
    a,d,side,front=_frame(elbow,shoulder)
    na=144;nr=60;verts=[];faces=[]
    for i in range(nr+1):
        t=i/nr
        for j in range(na):
            angle=j*math.tau/na
            # Continuous rounded quilting, narrow pressed seams, no detached
            # chessboard squares standing away from the arm.
            puff=.0065*math.sin(t*math.pi*6)**2*math.sin(angle*6)**2
            r=.146+.021*t-.040*t**4+puff
            p=a+d*t+side*(r*math.sin(angle))+front*(r*.91*math.cos(angle))
            verts.append(p)
    for i in range(nr):
        for j in range(na):
            faces.append((i*na+j,i*na+(j+1)%na,(i+1)*na+(j+1)%na,(i+1)*na+j))
    faces += [tuple(reversed(range(na))),tuple(nr*na+j for j in range(na))]
    mesh('09 | '+str(sign)+' Compressed quilted sleeve',verts,faces,'padding')
    # Leather anchoring band, mostly sheltered under the lower pauldron.
    cuff('09 | '+str(sign)+' Upper arm leather fastening',elbow+(shoulder-elbow)*.48,
         elbow+(shoulder-elbow)*.60,.161,.164,'leather_dark',depth=.92)


def _forearm(sign,elbow,wrist):
    prefix='10 | '+str(sign)+' '
    a,d,side,front=_frame(wrist,elbow)
    axis=d.normalized()
    uvball(prefix+'Flexing elbow leather',elbow,(.142,.129,.143),'leather_dark',32,20)
    tapered_limb(prefix+'Forearm leather underlayer',wrist,elbow,.098,.142,'leather_dark')

    def surface(t,ang,extra=0):
        r=.127+.045*t-.009*math.sin(math.pi*t)
        s,c=math.sin(ang),math.cos(ang)
        # The front has two broad forged planes meeting at a shallow ridge.
        ridge=.017*(1-abs(s))**1.3 if c>0 else 0
        return a+d*t+side*((r+extra)*s)+front*((r*.90+extra)*c+ridge)

    # Fastening belts sit under the armour and show only through rear opening.
    for t in (.18,.74):
        def strap(u,v,t=t):
            return surface(t+(v-.5)*.105,-math.pi+u*math.tau,-.004)
        _grid(prefix+'Leather closure belt',strap,80,3,'leather',.011)
        # Discreet folded keeper and buckle on the rear outside quarter.
        angle=sign*2.42
        centre=surface(t,angle,.004)
        uvball(prefix+'Closure belt rivet',centre,(.006,.006,.006),'gold',12,8)

    def plate(u,v):
        angle=-2.39+u*4.78
        c=max(0,math.cos(angle))
        lo=.032+.045*(1-c)+.011*math.sin(angle)
        hi=.804+.050*c-.020*math.sin(angle)*sign
        return surface(lo+(hi-lo)*v,angle)
    _grid(prefix+'Ridge-forged long vambrace',plate,84,32,'steel',.022)
    for edge in ('wrist','elbow','left','right'):
        def rim(u,v,edge=edge):
            if edge=='wrist':su=u;sv=v*.069
            elif edge=='elbow':su=u;sv=.935+v*.065
            elif edge=='left':su=u*.026;sv=v
            else:su=.974+u*.026;sv=v
            p=Vector(plate(su,sv))
            t=(p-a).dot(axis)/d.length
            radial=(p-(a+d*t)).normalized()
            return p+radial*.014
        _grid(prefix+'Wide vambrace turned '+edge,rim,72 if edge in ('wrist','elbow') else 4,
              4 if edge in ('wrist','elbow') else 28,'steel_light',.005)
    # Short, separate elbow lame. Unequal lip and separate overlap retain a
    # breathing gap; it reads like articulated construction rather than pipe.
    def elbow_plate(u,v):
        angle=-2.48+u*4.96
        t=.804+.226*v+.014*math.cos(angle)*(v-.4)
        p=surface(t,angle,.014+.006*math.sin(math.pi*v))
        return p
    _grid(prefix+'Articulated elbow lame',elbow_plate,72,12,'steel',.023)
    for v in (0,1):
        pts=[]
        for i in range(73):
            p=Vector(elbow_plate(i/72,v));t=(p-a).dot(axis)/d.length
            pts.append(p+(p-(a+d*t)).normalized()*.012)
        tube(prefix+'Elbow rolled cut edge',pts,.0065,'steel_light',sides=10)
    # Short lateral hinge/lap gives the side view deliberate metal coverage.
    def side_lap(u,v):
        angle=sign*(1.37+.77*u)
        return surface(.18+.57*v,angle,.024)
    _grid(prefix+'Outer overlapping vambrace side wing',side_lap,18,25,'steel',.017)
    for t in (.205,.708):
        for ang in (sign*1.48,-sign*1.66):
            p=surface(t,ang,.041 if ang*sign>0 else .015)
            uvball(prefix+'Small hammered attachment rivet',p,(.0074,.0074,.0074),'steel_light',12,8)


def build_arm(api, sign):
    _api(api)
    start=len(PARTS)
    shoulder=Vector((sign*.552,0,2.025))
    elbow=Vector((sign*.805,-.005,1.613))
    wrist=Vector((sign*.972,-.033,1.273))
    _upper_sleeve(sign,shoulder,elbow)
    _shoulder(sign)
    _forearm(sign,elbow,wrist)
    for ob in PARTS[start:]:
        ob['arm_side']=sign
        ob['art007_armor']=True
    return PARTS[start:]


def build_leg_armor(api, sign):
    _api(api)
    start=len(PARTS);prefix='12 | '+str(sign)+' '
    # An asymmetrically forged shield has rounded corners, a deep central
    # convexity, and a raised centre arris.  Its rim lies against the knee.
    outline=_round_outline([(-.156,.053),(-.088,.132),(.066,.142),
        (.151,.069),(.143,-.055),(.018,-.117),(-.125,-.086)],6,.12)
    nb=len(outline);nr=20;cx=sign*.405;cz=.655
    def knee_pt(p,r,offset=0):
        x,z=p*r
        q=(x/.185)**2+(z/.169)**2
        y=-.153-.076*math.sqrt(max(.065,1-q))-.024*(1-min(1,abs(x)/.16))
        y+=.006*z/.16
        return (cx+x,y-offset,cz+z)
    vs=[(cx,-.253,cz)];fs=[]
    for ir in range(1,nr+1):
        for p in outline:vs.append(knee_pt(p,ir/nr))
    for j in range(nb):fs.append((0,1+j,1+(j+1)%nb))
    for ir in range(nr-1):
        for j in range(nb):fs.append((1+ir*nb+j,1+ir*nb+(j+1)%nb,
            1+(ir+1)*nb+(j+1)%nb,1+(ir+1)*nb+j))
    mesh(prefix+'Deep convex knee shield with arris',vs,fs,'steel',thickness=.025)
    # Wide bevel is sampled from the actual surface, not an unrelated polygon.
    vs=[];fs=[]
    for r in (.926,.966,1):
        for p in outline:vs.append(knee_pt(p,r,.014 if r<1 else .010))
    for ir in range(2):
        for j in range(nb):fs.append((ir*nb+j,ir*nb+(j+1)%nb,
            (ir+1)*nb+(j+1)%nb,(ir+1)*nb+j))
    mesh(prefix+'Knee forged silver bevel',vs,fs,'steel_light',thickness=.005)
    tube(prefix+'Knee dark underturned seam',[knee_pt(p,1,-.004) for p in outline],.004,'steel_dark',True,sides=8)
    # The padded knee remains between front plate and visible restrained straps.
    for z in (.565,.701):
        cuff(prefix+'Knee leather articulation strap',(sign*.414,.008,z-.019),
             (sign*.412,.008,z+.019),.159,.157,'leather_dark',depth=.91)
    for side in (-1,1):
        uvball(prefix+'Knee side pivot',(cx+side*.151,-.055,.659),
               (.017,.037,.034),'steel_dark',20,12)
        uvball(prefix+'Knee pivot pin',(cx+side*.163,-.073,.659),
               (.007,.008,.008),'steel_light',12,8)

    def greave(u,v):
        angle=-2.82+5.64*u
        sn,co=math.sin(angle),math.cos(angle)
        bottom=.226+.017*max(0,co)+.012*sn*sn
        top=.568-.035*sn*sn+.010*co
        z=bottom*(1-v)+top*v
        r=.133+.026*v-.023*math.sin(math.pi*v)
        x=sign*(.445-.027*v)+r*sn
        ridge=.022*(1-abs(sn))**1.35 if co>0 else 0
        y=.003-r*1.01*co-ridge
        return (x,y,z)
    _grid(prefix+'Waisted folded shin greave',greave,96,34,'steel',.023)
    # Lower reinforced lip is flared and overlaps the instep instead of
    # ending as an open straight cylinder on top of the boot.
    for edge in ('ankle','knee','left','right'):
        def border(u,v,edge=edge):
            if edge=='ankle':su=u;sv=v*.070
            elif edge=='knee':su=u;sv=.943+v*.057
            elif edge=='left':su=u*.023;sv=v
            else:su=.977+u*.023;sv=v
            p=Vector(greave(su,sv));angle=-2.82+5.64*su
            p+=Vector((math.sin(angle),-math.cos(angle),0))*.013
            return p
        _grid(prefix+'Greave flared silver '+edge,border,84 if edge in ('ankle','knee') else 4,
              4 if edge in ('ankle','knee') else 28,'steel_light',.005)
    # One broad overlapping ankle lame rises into the front of the greave.
    def ankle_lame(u,v):
        ang=-1.73+3.46*u
        r=.144-.006*v
        z=.221+.056*v+.016*math.cos(ang)
        return (sign*.445+r*math.sin(ang),.002-r*1.045*math.cos(ang),z)
    _grid(prefix+'Articulating ankle overlap',ankle_lame,56,8,'steel_dark',.018)
    tube(prefix+'Ankle polished edge',[ankle_lame(j/56,0) for j in range(57)],.006,'steel_light',sides=8)
    for t in (.20,.81):
        for ang in (-1.67,1.67):
            u=(ang+2.82)/5.64;p=Vector(greave(u,t))
            p+=Vector((math.sin(ang),-math.cos(ang),0))*.020
            uvball(prefix+'Flush greave rivet',p,(.0064,.0064,.0064),'steel_light',12,8)
    for ob in PARTS[start:]:
        ob['leg_side']=sign
        ob['art007_armor']=True
    return PARTS[start:]
