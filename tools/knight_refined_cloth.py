"""ART-007B: authored cloth drape for the concept knight.

The low-frequency shape is made from explicit compression / tension folds.
No earlier mesh is loaded. `build(api)` uses the assembler's mesh helpers and
materials. Coordinate contract: Z up, -Y front, waist 1.38, crown 3.20.
"""
import math
from mathutils import Vector


def build(api):
    globals().update({k: api[k] for k in
                    ('mesh', 'tube', 'uvball', 'box', 'loft', 'cuff',
                     'tapered_limb', 'PARTS', 'M')})
    start = len(PARTS)
    shirt()
    lower_cloth()
    neck_scarf()
    for obj in PARTS[start:]:
        obj['refinement'] = 'ART-007B authored cloth folds; no image projection'
    return PARTS[start:]


def g(x, width):
    return math.exp(-(x / width) ** 2)


def smooth(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3.0 - 2.0 * t)


def interp(controls, value):
    """Continuous cubic interpolation through hand-set silhouette sections."""
    for i in range(len(controls) - 1):
        p1, p2 = controls[i:i + 2]
        if p1[0] <= value <= p2[0] + 1e-8:
            p0 = controls[max(0, i - 1)]
            p3 = controls[min(len(controls) - 1, i + 2)]
            t = (value - p1[0]) / (p2[0] - p1[0])
            ans = []
            for j in range(1, len(p1)):
                m1 = (p2[j] - p0[j]) / (p2[0] - p0[0])
                m2 = (p3[j] - p1[j]) / (p3[0] - p1[0])
                d = p2[0] - p1[0]
                ans.append((2*t**3 - 3*t*t + 1)*p1[j]
                           + (t**3 - 2*t*t + t)*d*m1
                           + (-2*t**3 + 3*t*t)*p2[j]
                           + (t**3 - t*t)*d*m2)
            return ans
    return list(controls[-1][1:])


def panel(name, surface, nu, nv, mat='cloth', thickness=.013):
    verts = [surface(u/nu, v/nv)
             for v in range(nv + 1) for u in range(nu + 1)]
    faces = [(v*(nu+1)+u, v*(nu+1)+u+1,
              (v+1)*(nu+1)+u+1, (v+1)*(nu+1)+u)
             for v in range(nv) for u in range(nu)]
    return mesh(name, verts, faces, mat, thickness=thickness)


def binding(name, surface, side, span=.065, rear=False, stop_t=1.0):
    """Woven facing: a broad flat ribbon following the same cloth surface."""
    def face(u, t):
        if side == 'left':
            s, v = span*u, stop_t*t
        elif side == 'right':
            s, v = 1-span+span*u, stop_t*t
        else:
            s, v = u, 1-span+span*t
        point = Vector(surface(s, v))
        # Cloth is predominantly vertical: offset in the actual surface normal.
        h = .0001
        du = Vector(surface(min(1, s+h), v))-Vector(surface(max(0, s-h), v))
        dv = Vector(surface(s, min(1, v+h)))-Vector(surface(s, max(0, v-h)))
        n = du.cross(dv).normalized()
        if n.y * (1 if rear else -1) < 0:
            n = -n
        return point + .009 * n
    panel(name, face, 40 if side == 'bottom' else 3,
          3 if side == 'bottom' else 48, 'trim', thickness=.005)


def shirt_surface(a, z):
    controls = [(1.30, .402, .273), (1.38, .420, .282),
                (1.50, .449, .301), (1.68, .465, .301),
                (1.83, .460, .294), (1.96, .440, .268),
                (2.07, .386, .232), (2.18, .254, .191)]
    rx, ry = interp(controls, z)
    sa, ca = math.sin(a), math.cos(a)
    x = rx * sa
    front = max(0, ca)
    side = abs(sa)
    # Broad chest fullness supports the shield. Folds emerge around its sides,
    # not through the brass face. At shoulder straps x=+/-.326, y stays ~-.22.
    bulge = .026 * front**5 * g(z-1.72, .25)
    # Two nonperiodic asymmetric arcs from the belt to each underarm.
    side_amount = smooth(.10, .31, abs(x)) * front**.55
    sign = 1 if x >= 0 else -1
    curve = 1.468 + .47*abs(x) + .018*sign
    compression = (.035*g(z-curve, .043)
                   - .019*g(z-(curve+.053), .031))
    curve2 = 1.628 + .51*abs(x) - .014*sign
    compression += (.029*g(z-curve2, .052)
                    - .014*g(z-(curve2+.059), .034))
    # A broad belly fold sits over the belt and blends into the side arcs.
    belly_line = 1.445 + .040*(x/.44)**2 - .012*x/.44
    belly = (.026*g(z-belly_line, .047)
             - .012*g(z-(belly_line+.059), .035))*front**3
    # Wide oblique fold visible below the shield, arrested by the waist belt.
    diagonal_line=1.483+.17*x+.047*(x/.44)**2
    diagonal=(.039*g(z-diagonal_line,.055)
              -.021*g(z-(diagonal_line+.064),.038))*front**3
    # Only four explicitly placed gathering rays lead into the waist seam.
    gather = 0.0
    for center, lean, amount in [(-.31, -.11, .016), (-.165, .10, .012),
                                (.17, -.12, .012), (.315, .07, .015)]:
        line = center + lean*(z-1.38)
        gather += amount*(g(x-line, .022)-.55*g(x-line-.026, .017))
    gather *= g(z-1.438, .106)*front**1.4
    # Side cloth carries a larger, shallow diagonal hollow below each arm.
    side_fold = (.015*g(z-(1.66+.12*ca), .042)
                 - .008*g(z-(1.706+.12*ca), .028))*side**6
    displacement = bulge + side_amount*compression + belly + gather + side_fold + diagonal
    crest_mask=(1-smooth(.13,.23,abs(x)))*smooth(1.53,1.62,z)*(1-smooth(1.86,1.98,z))
    displacement=displacement*(1-crest_mask)+min(displacement,.028)*crest_mask
    # The girdle still fits around the actual cloth at 1.32..1.438.
    displacement *= smooth(1.31, 1.455, z)
    return ((rx+displacement*.55)*sa,
            -(ry+displacement)*ca, z)


def shirt():
    nr, na = 64, 128
    verts = [shirt_surface(j*math.tau/na, 1.30+.88*i/nr)
             for i in range(nr+1) for j in range(na)]
    faces = [(i*na+j, i*na+(j+1)%na,
              (i+1)*na+(j+1)%na, (i+1)*na+j)
             for i in range(nr) for j in range(na)]
    faces += [tuple(reversed(range(na))),
              tuple(nr*na+j for j in range(na))]
    mesh('01 | Tunic with underarm tension and belt compression folds',
         verts, faces, 'cloth_tunic' if 'cloth_tunic' in M else 'cloth')
    # Side assembly seams are subtle fabric welts, not raised ornamental ropes.
    for sign in (-1, 1):
        points=[]
        for j in range(55):
            z=1.35+.61*j/54
            a=sign*(1.48+.045*smooth(1.4, 1.9, z))
            p=Vector(shirt_surface(a,z));p.x+=sign*.0013
            points.append(p)
        tube('01 | Tunic side construction seam', points, .0018,
             'cloth_dark', sides=6)


def mail_skirt():
    loft('02 | Fitted dark mail underlay',
         [(.837,0,0,.584,.327), (.98,0,0,.558,.318),
          (1.16,0,0,.500,.299), (1.345,0,0,.431,.279)],
         'cloth_dark',samples=80)
    # Each curved tile has its own shallow crowned surface. Small intentional
    # offsets break the rigid brick-wall regularity while retaining scale rows.
    cols=38
    for row in range(6):
        vertices=[]; faces=[]
        z=.881+row*.075
        rx=.592-(z-.85)*.35
        ry=.337-(z-.85)*.115
        for col in range(cols):
            a=(col+.49*(row%2))*math.tau/cols
            stagger=.0018*math.sin(col*2.37+row*.73)
            origin=len(vertices)
            for k in range(3):
                v=k/2
                for j in range(3):
                    u=j/2
                    angle=a+(u-.5)*.148
                    crown=.006*math.sin(math.pi*u)*math.sin(math.pi*v)
                    # Slightly flared lower edges visibly overlap the row below.
                    depth=.006+.006*(1-v)+crown
                    vertices.append(((rx+depth)*math.sin(angle),
                                     -(ry+depth)*math.cos(angle),
                                     z+(v-.5)*.081+stagger))
            for k in range(2):
                for j in range(2):
                    p=origin+k*3+j
                    faces.append((p,p+1,p+4,p+3))
        mesh('02 | Hand fitted overlapping mail row %02d'%row,
             vertices,faces,'mail',thickness=.008,bevel=.0012)


def front_surface(u, t):
    s=2*u-1
    # A tapering, shield-ended tabard; soft off-center fall from belt tension.
    width=.290-.023*t+.004*math.sin(math.pi*t)
    x=s*width+.006*t*(1-s*s)
    tip=.793-.073*(1-abs(s))
    z=1.402*(1-t)+tip*t+.012*s*math.sin(math.pi*t)
    fall=smooth(0, .34, t)
    # One broad central hollow and two unequal ridges, with natural widening
    # away from the belt. These are constructed folds, not periodic ripples.
    folds=(.034*g(s+.54-.06*t,.21+.08*t)
           -.018*g(s+.27-.04*t,.10+.04*t)
           +.023*g(s-.52+.10*t,.27)
           -.010*g(s-.19,.13)) * fall
    billow=.036*math.sin(math.pi*t*.87)
    diagonal=.006*g(t-(.19+.28*u),.043)*(1-t)
    y=-.338-.020*t+.030*s*s-billow-folds-diagonal
    return x,y,z


def cloak_surface(sign, u, t):
    # Each half drapes from its shoulder; the bag compresses it until t~.49.
    width=.316+.228*t
    slit=.007+.030*smooth(.37,1,t)
    x=sign*(slit+u*width)
    bottom=.610+.047*u + sign*.004*(1-u)
    z=2.11*(1-t)+bottom*t + .018*math.sin(math.pi*u)*t
    release=smooth(.27,.64,t)
    # Long, broad alternating folds originate below the back straps. Their
    # wavelength expands towards the hem like hanging woven fabric.
    center=.31+.035*sign+.085*(1-t)
    ridge=.079*g(u-center,.14+.07*t)
    trough=-.061*g(u-(.66+.025*sign),.14+.045*t)
    edge=.034*g(u-.94,.14)
    fold=(ridge+trough+edge)*release
    # A gentle S profile makes the hem curl back after releasing at the bag.
    y=.253+.108*t+.035*math.sin(math.pi*t)+fold
    y+=sign*.0035*math.sin(math.pi*u)*release
    # Local fold crossing the bag's lower corner, fading before the broad hem.
    y+=.012*g(t-(.46+.17*u),.058)*smooth(.3,.7,u)
    return x,y,z


def lower_cloth():
    mail_skirt()
    panel('03 | Tabard with broad hanging folds',front_surface,48,54)
    for side in ('left','right','bottom'):
        binding('03 | Flat woven gold tabard facing '+side,
                front_surface,side,.067 if side!='bottom' else .056,
                stop_t=.944 if side!='bottom' else 1.0)
    for sign in (-1,1):
        surf=lambda u,t,sign=sign:cloak_surface(sign,u,t)
        panel('04 | Weighted split cloak '+str(sign),surf,42,72)
        # Gold on both the slit and lower edge; outer side is a folded blue hem.
        for side in ('left','bottom'):
            binding('04 | Flat cloak facing '+str(sign)+' '+side,
                    surf,side,.048 if side=='left' else .026,rear=True,
                    stop_t=.974 if side=='left' else 1.0)
        # The sewn outer hem follows the fabric but stays visually quiet.
        points=[Vector(surf(.984,j/72))+Vector((0,.006,0)) for j in range(73)]
        tube('04 | Cloak turned outer hem '+str(sign),points,.0022,
             'cloth_dark',sides=6)


def scarf_surface(a, t):
    sa,ca=math.sin(a),math.cos(a)
    front=max(0,ca);rear=max(0,-ca)
    # The scarf is tall at the nape and gathered into a visibly pointed V at
    # the throat. The asymmetry is a wrap direction, not a sinusoidal texture.
    top=2.388-.173*front**1.7+.012*sa
    # A slightly off-center V replaces the old concentric U-shaped arcs.
    vshape=max(0,1-abs(sa+.045))**.80*smooth(0,.35,front)
    lower=2.164-.182*vshape+.088*rear**2+.024*sa
    z=top*(1-t)+lower*t
    # Broad fabric plane followed by one compressed roll and an undercut.
    fold=.013*g(t-(.35+.16*sa),.23)-.007*g(t-.80,.10)
    radius=.331+.008*t+fold
    depth=.296+.019*t+fold
    # Upper rear collar fills the head-to-body transition under the brim.
    radius+=.032*(1-front)*(1-t)
    depth+=.043*rear*(1-t)
    # Tight gathering at one wrap shoulder, fading before the center V.
    local=g(sa+.80,.16)*front**.5
    radius+=local*(.009*g(t-.63,.14)-.007*g(t-.86,.08))
    depth+=local*(.008*g(t-.63,.14)-.006*g(t-.86,.08))
    return radius*sa,-depth*ca,z


def neck_scarf():
    # Seamless undercollar: no black cylindrical neck between nape and helmet.
    loft('05 | Continuous fabric nape under helmet',
         [(2.018,0,.01,.257,.217), (2.165,0,.012,.306,.274),
          (2.285,0,.015,.347,.328), (2.397,0,.014,.355,.342)],
         'cloth',samples=88)
    na,nv=120,34
    vertices=[scarf_surface(j*math.tau/na,v/nv)
              for v in range(nv+1) for j in range(na)]
    faces=[(v*na+j,v*na+(j+1)%na,
            (v+1)*na+(j+1)%na,(v+1)*na+j)
           for v in range(nv) for j in range(na)]
    mesh('05 | Draped V scarf with compressed cloth ridge',
         vertices,faces,'cloth_light',thickness=.014)
    # A narrow overlaid turned fold follows a diagonal wrap. This is a flat
    # band with a folded edge rather than another padded torus around the neck.
    def lapel(u,t):
        a=-1.67+3.25*u
        base=.255+.38*u
        width=.035+.205*math.sin(math.pi*u)**.60
        v=base+width*t
        p=Vector(scarf_surface(a,v))
        normal=Vector((math.sin(a),-math.cos(a),0))
        p+=normal*(.002+.009*math.sin(math.pi*u)**.6+.003*math.sin(math.pi*t))
        p.z+=.008*(1-t)
        return p
    panel('05 | Scarf diagonal flat overfold',lapel,80,10,
          'cloth',thickness=.009)
    # The Solidify edge supplies the turned lower thickness. No separate tube
    # traces it, which would falsely read as another concentric rubber ring.
