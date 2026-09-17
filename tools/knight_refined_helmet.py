"""ART-006B: freshly authored helmet from the user-supplied model sheet.

Public API: build(materials) -> list[bpy.types.Object]. Coordinates are Z up,
front -Y; the complete character is approximately 3.2 units tall. Every visible
surface is mesh geometry. Eye openings are absent shell faces with a separate
recessed dark interior, never texture patches or projections of the reference.
"""
import math
import bpy
import bmesh


PREFIX = 'KnightReference_Helmet_'
SIDE_ANGLE = math.radians(74)
SIDE_PROFILE = [
    (2.365, .462, .438, -.006),
    (2.400, .447, .420, -.006),
    (2.480, .456, .432, -.008),
    (2.660, .462, .439, -.008),
]
# Forty arch sections keep the silhouette and raised crown strip continuously
# curved at close inspection. The small final circle avoids a degenerate pole.
DOME_PROFILE = []
for _i in range(41):
    _a = (_i/40)*(math.pi/2-.008)
    DOME_PROFILE.append((2.660+.518*math.sin(_a),
                         .462*math.cos(_a), .439*math.cos(_a), -.008))


def _mesh(label, vertices, faces, material, smooth=False, thickness=0,
          bevel=0, offset=-1):
    data = bpy.data.meshes.new(PREFIX + label + '_mesh')
    data.from_pydata(vertices, [], faces)
    data.update()
    bm = bmesh.new()
    bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(data)
    bm.free()
    obj = bpy.data.objects.new(PREFIX + label, data)
    bpy.context.scene.collection.objects.link(obj)
    data.materials.append(material)
    for polygon in data.polygons:
        polygon.use_smooth = smooth
    if thickness:
        wall = obj.modifiers.new('Physical metal wall', 'SOLIDIFY')
        wall.thickness = thickness
        wall.offset = offset
        wall.use_even_offset = True
    if bevel:
        edge = obj.modifiers.new('Soft forged edge', 'BEVEL')
        edge.width = bevel
        edge.segments = 3
        edge.limit_method = 'ANGLE'
        edge.angle_limit = .43
    return obj


def _section(profile, z):
    if z <= profile[0][0]:
        return profile[0][1:]
    if z >= profile[-1][0]:
        return profile[-1][1:]
    for low, high in zip(profile, profile[1:]):
        if low[0] <= z <= high[0]:
            t = (z - low[0]) / (high[0] - low[0])
            return tuple(a + (b-a)*t for a, b in zip(low[1:], high[1:]))
    raise ValueError(z)


def _shell(label, profile, material, segments=72, start=0, end=math.tau,
           thickness=.019, cap=False, bevel=0):
    cyclic = abs(end-start-math.tau) < 1e-5
    n = segments if cyclic else segments+1
    vertices = []
    for z, rx, ry, cy in profile:
        for i in range(n):
            a = start+(end-start)*i/segments
            vertices.append((rx*math.sin(a), cy-ry*math.cos(a), z))
    faces = []
    for row in range(len(profile)-1):
        for i in range(segments):
            j = (i+1) % n
            faces.append((row*n+i, row*n+j, (row+1)*n+j, (row+1)*n+i))
    if cap:
        faces.append(tuple((len(profile)-1)*n+i for i in range(n)))
    return _mesh(label, vertices, faces, material, smooth=True,
                 thickness=thickness, bevel=bevel)


def _mask_point(x, row):
    """Sheet-derived horizontal stations; outer edge meets the side shell."""
    t = abs(x)/.443
    if row == 0:
        z = 2.658
        y = -.008-.439*math.sqrt(max(0, 1-(x/.462)**2))
    elif row == 1:
        z = 2.564+.029*t
        y = -.480+.344*t**1.10
    elif row == 2:
        z = 2.421+.067*t
        y = -.480+.344*t**1.10
    elif row == 3:
        z = 2.310+.121*t**1.13
        y = -.470+.334*t**1.09
    else:
        z = 2.176+.189*t**1.23
        y = -.452+.326*t**1.08
    if t > .999:
        # All mask outer vertices coincide with the true side-shell contour.
        rx, ry, cy = _section(SIDE_PROFILE, z)
        x = math.copysign(rx*math.sin(SIDE_ANGLE), x)
        y = cy-ry*math.cos(SIDE_ANGLE)
    return (x, y, z)


def _visor(material):
    # A thin, integrated nasal bridge is part of the visor itself. There is no
    # bright decorative stripe running down the face or projecting past the chin.
    positive = [0, .051, .10, .19, .285, .343, .385, .443]
    vertices = []
    faces = []
    # Two independently smoothed cheek halves share the same central geometric
    # line, with duplicate centre vertices to preserve its deliberate fold.
    # This prevents large rectangular faceted reflections across the cheeks.
    for sign in (-1, 1):
        columns = sorted(sign*x for x in positive)
        n = len(columns)
        start = len(vertices)
        vertices.extend(_mask_point(x,row) for row in range(5) for x in columns)
        for row in range(4):
            for col in range(n-1):
                middle = (columns[col]+columns[col+1])*.5
                if row == 1 and .051 < abs(middle) < .343:
                    continue
                a = start+row*n+col
                faces.append((a, a+n, a+n+1, a+1))
    obj = _mesh('Faceted_visor_with_two_open_apertures', vertices, faces,
                material, smooth=True, thickness=.020, bevel=.004)
    obj['visorTopology'] = 'Two physical open apertures, no faces span the eyes'
    obj['apertureRangesX'] = '[-0.343,-0.051], [0.051,0.343]'
    obj['chinZ'] = 2.176
    return obj


def _interior(sign, material):
    # The reference's eyes are unlit cavities; inset .075 creates real parallax.
    values = [0, .045, .10, .19, .285, .370, .435]
    values = sorted(sign*x for x in values)
    vertices = []
    for row in (1, 2):
        for x in values:
            xx, y, z = _mask_point(x, row)
            vertices.append((xx, y+.040, z+(.055 if row==1 else -.055)))
    n = len(values)
    faces = [(i, i+n, i+n+1, i+1) for i in range(n-1)]
    return _mesh('Recessed_unlit_interior_'+str(sign), vertices, faces,
                 material, thickness=.006)


def _band(label, path, width, material, thickness=.011):
    vertices = []
    for y, z in path:
        vertices.extend([(-width*.5, y, z), (width*.5, y, z)])
    faces = [(2*i, 2*i+1, 2*i+3, 2*i+2) for i in range(len(path)-1)]
    return _mesh(label, vertices, faces, material, thickness=thickness,
                 bevel=.002)


def _ridge(material):
    front = [(cy-ry, z) for z, rx, ry, cy in DOME_PROFILE]
    rear = [(cy+ry, z) for z, rx, ry, cy in reversed(DOME_PROFILE)]
    rear += [(cy+ry, z) for z, rx, ry, cy in reversed(SIDE_PROFILE[:-1])]
    surface = front+rear
    path = []
    for i, (y, z) in enumerate(surface):
        a = surface[max(0, i-1)]
        b = surface[min(len(surface)-1, i+1)]
        dy, dz = b[0]-a[0], b[1]-a[1]
        length = math.hypot(dy, dz)
        if length < 1e-7:
            raise ValueError('Degenerate crown profile')
        offset = .018 if z > 3.16 else .011
        path.append((y-dz/length*offset, z+dy/length*offset))
    return _band('Narrow_crown_to_nape_reinforcing_strip', path, .056, material)


def _rounded_rectangle(y0, y1, z0, z1, radius, steps=5):
    result = []
    for y, z, start in [(y1-radius,z1-radius,0), (y0+radius,z1-radius,90),
                        (y0+radius,z0+radius,180), (y1-radius,z0+radius,270)]:
        for i in range(steps):
            a = math.radians(start+i*90/(steps-1))
            result.append((y+radius*math.cos(a), z+radius*math.sin(a)))
    return result


def _hinge(sign, materials):
    # Close-fitting recessed ear mechanism, visible as a slim rounded plate from
    # the front and a bordered latch from the side, as on the supplied sheet.
    contour = _rounded_rectangle(-.095,.066,2.439,2.735,.025)
    n = len(contour)
    vertices = [(sign*x,y,z) for x in (.449,.490) for y,z in contour]
    faces = [tuple(reversed(range(n))), tuple(range(n,2*n))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    objects = [_mesh('Ear_hinge_body_'+str(sign),vertices,faces,
                     materials['steel_dark'],bevel=.006)]
    outer = _rounded_rectangle(-.087,.058,2.450,2.724,.020)
    inner = _rounded_rectangle(-.067,.038,2.472,2.702,.014)
    vertices = [(sign*.494,y,z) for path in (outer,inner) for y,z in path]
    n = len(outer)
    faces = [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    if sign < 0:
        faces = [tuple(reversed(face)) for face in faces]
    objects.append(_mesh('Ear_hinge_rounded_border_'+str(sign),vertices,faces,
                         materials['steel'],thickness=.008,bevel=.002,offset=0))
    # A single inset steel latch leaves dark space around it; the two sides of
    # its chamfer catch light without turning into broad bright parallel bars.
    latch = _rounded_rectangle(-.027,-.005,2.483,2.689,.008)
    vertices = [(sign*.495,y,z) for y,z in latch]
    face = tuple(range(len(vertices)))
    if sign < 0:
        face = tuple(reversed(face))
    objects.append(_mesh('Ear_hinge_inset_latch_'+str(sign),vertices,
                         [face],materials['steel_light'],
                         thickness=.006,bevel=.002,offset=0))
    return objects


def _diamond(material):
    y, z, w, h = -.479, 2.666, .081, .088
    front = [(-w,y,z),(0,y,z+h),(w,y,z),(0,y,z-h)]
    vertices = front+[(0,y-.022,z)]+[(x,yy+.018,zz) for x,yy,zz in front]
    faces = [(0,4,1),(1,4,2),(2,4,3),(3,4,0),(5,6,7,8),
             (0,1,6,5),(1,2,7,6),(2,3,8,7),(3,0,5,8)]
    return _mesh('Four_facet_brass_brow_diamond',vertices,faces,material,
                 bevel=.0017)


def build(materials):
    """Return independently editable helmet pieces without changing the scene."""
    for name in ('steel','steel_light','steel_dark','dark','gold'):
        if name not in materials:
            raise KeyError('Helmet requires material '+name)
    objects = [
        _shell('Arched_open_dome', DOME_PROFILE, materials['steel'],
               thickness=.021, cap=True),
        _shell('Side_and_rear_shell', SIDE_PROFILE, materials['steel'],
               segments=56, start=SIDE_ANGLE, end=math.tau-SIDE_ANGLE),
        _shell('Short_flared_nape_rim', [
            (2.348,.474,.451,-.004), (2.368,.473,.449,-.004),
            (2.393,.452,.428,-.006)], materials['steel_light'],
            segments=56, start=math.radians(81), end=math.radians(279),
            thickness=.014,bevel=.0025),
        _visor(materials['steel']),
        _interior(-1,materials['dark']), _interior(1,materials['dark']),
        _shell('Thin_continuous_brow_binding', [
            (2.644,.474,.451,-.008),(2.681,.473,.450,-.008)],
            materials['steel_light'], thickness=.012,bevel=.0025),
        _ridge(materials['steel_light']),
        _diamond(materials['gold']),
    ]
    for sign in (-1,1):
        objects.extend(_hinge(sign,materials))
    for obj in objects:
        # The supplied side view is slightly deeper than its front-view width.
        # Apply the same longitudinal proportion to shells, real apertures,
        # recessed interiors and trim so all mating surfaces stay aligned.
        for vertex in obj.data.vertices:
            vertex.co.y *= 1.10
        obj['conceptReference'] = 'User supplied knight model sheet, 2026-09-17'
        obj['authorship'] = 'ART-006B shape with ART-007 enlarged recessed dark cavity coverage'
        obj['coordinateConvention'] = 'Z up, front -Y; ground 0; full character 3.2'
    return objects
