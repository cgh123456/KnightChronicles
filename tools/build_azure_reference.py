"""Rebuild the supplied sword from measured image-space landmarks.

Blender 5.2: blender -b --factory-startup --python tools/build_azure_reference.py -- --quick
The front appearance uses explicitly labelled reference-projected colour, while
cross sections, shoulders, settings, gems, wrapping and borders are real geometry.
The reverse is a symmetric reconstruction; it is not observed evidence.
"""
import argparse
import bmesh
import bpy
import json
import math
import os
import random
import shutil
import sys
from pathlib import Path
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from azure_reference_materials import make_materials
from azure_reference_studio import setup_studio

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "assets/models/azure_reference_reconstruction"
SOURCE = Path("/Users/chenguanhan/.codex/generated_images/01a0a40f-f4e6-7641-9563-2911964fba5b/exec-12d993e7-b930-4c2a-a2b4-85433bc3446f.png")
S = 1.6 / 1430.0
CX, TIP = 510.0, 1444.0
M = {}
REF = {}
PARTS = []
RNG = random.Random(131)


def xyz(x, y, depth=0):
    return ((x - CX) * S, -depth * S, (TIP - y) * S)


def mesh_object(name, verts, faces, material, bevel=0, smooth=False):
    mesh = bpy.data.meshes.new(name + "_mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.data.collections["WEAPON"].objects.link(obj)
    mesh.materials.append(material)
    obj["source"] = "geometry reconstructed from the supplied single front view"
    PARTS.append(obj)
    if bevel:
        mod = obj.modifiers.new("Manufactured edge bevel", "BEVEL")
        mod.width = bevel * S
        mod.segments = 3
    for f in mesh.polygons:
        f.use_smooth = smooth
    uv = mesh.uv_layers.new(name="ReferenceProjection")
    for poly in mesh.polygons:
        for li in poly.loop_indices:
            v = mesh.vertices[mesh.loops[li].vertex_index].co
            uv.data[li].uv = ((v.x / S + CX) / 1024, 1 - (TIP - v.z / S) / 1536)
    return obj


def apply_projection(obj, key):
    """Map colour from the supplied image onto the actual front and rear surfaces.

    This preserves painted engraving and weathering. It is deliberately not
    advertised as recovered material albedo or geometry.
    """
    material = REF[key]
    idx = len(obj.data.materials)
    obj.data.materials.append(material)
    for p in obj.data.polygons:
        if abs(p.normal.y) > .33:
            p.material_index = idx
    obj["appearance_method"] = "reference-projected colour; lighting in source is retained"


def panel(name, outline, depth=7, mat="dark", bevel=.5, project=True):
    n = len(outline)
    verts = [xyz(x, y, d) for d in (depth, -depth) for x, y in outline]
    faces = [tuple(range(n - 1, -1, -1)), tuple(range(n, 2*n))]
    faces += [(i, (i+1) % n, (i+1) % n + n, i+n) for i in range(n)]
    obj = mesh_object(name, verts, faces, M[mat], bevel)
    if project:
        apply_projection(obj, mat)
    return obj


def catmull(points, subdivisions=8, closed=False):
    pts = [Vector(p) for p in points]
    ext = ([pts[-1]] + pts + [pts[0], pts[1]]) if closed else ([pts[0]] + pts + [pts[-1]])
    result = []
    for i in range(len(pts) if closed else len(pts)-1):
        a, b, c, d = ext[i:i+4]
        for j in range(subdivisions):
            t = j / subdivisions
            p = .5*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t)
            result.append(tuple(p))
    if not closed:
        result.append(tuple(pts[-1]))
    return result


def strip(name, points, width=1.2, depth=9, thickness=.7, mat="gold", curved=True, both=True):
    """Flattened inlaid metal strip with a bevelled cross section, not a round wire."""
    pts = catmull(points, 6) if curved else points
    for side in ((1, -1) if both else (1,)):
        verts, faces = [], []
        for i, (x, y) in enumerate(pts):
            before = Vector(pts[max(0, i-1)])
            after = Vector(pts[min(len(pts)-1, i+1)])
            tangent = (after - before).normalized()
            normal = Vector((-tangent.y, tangent.x))
            for lateral, height in ((-1, 0), (-.60, thickness), (.60, thickness), (1, 0)):
                pp = Vector((x, y)) + normal * width * .5 * lateral
                verts.append(xyz(*pp, side*(depth+height)))
        for i in range(len(pts)-1):
            for j in range(3):
                a = i*4+j
                faces.append((a, a+1, a+5, a+4))
            faces.append((i*4, (i+1)*4, (i+1)*4+3, i*4+3))
        faces += [(0, 1, 2, 3), tuple(range(len(verts)-4, len(verts)))]
        mesh_object(name + ("_F" if side == 1 else "_B"), verts, faces, M[mat])


def mirror(points):
    return [(1020-x, y) for x, y in points]


def paired_strip(name, points, **kw):
    strip(name+"_L", points, **kw)
    strip(name+"_R", mirror(points), **kw)


def gem(name, x, y, rx, ry, base=16, crown=26, project=True):
    """Diamond cut: girdle, corner facets, table and tapered pavilion."""
    for side in (1, -1):
        ring = [(0,-1), (.38,-.62), (1,0), (.40,.60), (0,1), (-.4,.6), (-1,0), (-.38,-.62)]
        verts = []
        for scale, d in ((1, base), (.49, crown)):
            verts.extend(xyz(x+u*rx*scale, y+v*ry*scale, side*d) for u,v in ring)
        verts.append(xyz(x, y, side*(base-5)))
        faces = []
        for i in range(8):
            j = (i+1)%8
            faces += [(i,j,8+i), (j,8+j,8+i), (i,16,j)]
        faces += [(8,9,10,14), (10,11,12,14), (12,13,14), (14,15,8)]
        obj = mesh_object(name+("_F" if side == 1 else "_B"), verts, faces, M["sapphire"])
        obj.data.materials.append(M["sapphire_light"])
        for i,p in enumerate(obj.data.polygons):
            if i%5 == 0:
                p.material_index = 1
        if project:
            apply_projection(obj,"sapphire")


def oval_loft(name, sections, mat="leather", project=True, count=64):
    verts=[]
    for y,rx,rd in sections:
        for i in range(count):
            t=2*math.pi*i/count
            verts.append(xyz(CX+rx*math.cos(t),y,rd*math.sin(t)))
    faces=[]
    for r in range(len(sections)-1):
        for i in range(count):
            a=r*count+i; b=r*count+(i+1)%count
            faces.append((a,b,b+count,a+count))
    faces += [tuple(range(count-1,-1,-1)),tuple(range((len(sections)-1)*count,len(sections)*count))]
    obj=mesh_object(name,verts,faces,M[mat],.35,True)
    if project: apply_projection(obj,mat)
    return obj


def build_blade():
    # Exact shoulder scoops and almost parallel long cutting edges.
    sections=[(399,39),(411,43),(425,44),(442,40),(451,43),(460,51),
              (473,46),(494,44),(540,43.5),(650,43),(785,42),(942,40.5),
              (1118,38),(1210,36.8),(1265,33),(1317,29),(1359,23),
              (1394,14),(1420,7),(1444,.15)]
    lateral=[-1,-.80,-.64,-.35,-.235,-.12,0,.12,.235,.35,.64,.80,1]
    heights=[.15,3.2,4.3,4.6,4.4,2.4,2.3,2.4,4.4,4.6,4.3,3.2,.15]
    verts=[]
    for side in (1,-1):
        for y,w in sections:
            factor=min(1,w/12)
            for u,d in zip(lateral,heights):
                verts.append(xyz(CX+u*w,y,side*d*factor))
    faces=[]; n=len(lateral); total=len(sections)*n
    for side in range(2):
        off=side*total
        for j in range(len(sections)-1):
            for k in range(n-1):
                a=off+j*n+k; faces.append((a,a+1,a+n+1,a+n))
    for j in range(len(sections)-1):
        for k in (0,n-1):
            a=j*n+k; faces.append((a,a+n,a+n+total,a+total))
    faces.append(tuple(list(range(n-1,-1,-1))+list(range(total,total+n))))
    start=total-n
    faces.append(tuple(list(range(start,start+n))+list(range(2*total-1,2*total-n-1,-1))))
    obj=mesh_object("01_Blade_forged_wedge_fuller_and_shoulders",verts,faces,M["steel"],.12)
    apply_projection(obj,"steel")
    # Tang remains inside the grip: a structural element visible in the clay model.
    panel("02_Hidden_full_tang",[(502,130),(518,130),(524,416),(496,416)],4,"dark",.7,False)
    # Metal inlays above and below the light channel, with their own depth.
    edge_path=[(488,467),(492,496),(492,720),(493,834),(499,859),(497,900),
               (494,935),(495,1120),(498,1188),(491,1214),(500,1238),(509,1355)]
    paired_strip("03_Fuller_antique_gold_border",edge_path,width=1.8,depth=4.7,thickness=.7)
    paired_strip("04_Fuller_inner_silver_border",[(497,520),(497,809),(501,839)],width=.8,depth=4,thickness=.45,mat="pale_gold")
    paired_strip("05_Lower_fuller_silver",[(500,929),(501,1140),(500,1180),(496,1204),(499,1225),(509,1352)],width=.85,depth=4.6,thickness=.4,mat="edge")
    # Tiny leaf etching sits flush on the metal; texture carries finer-than-geometry detail.
    for k in range(26):
        y=510+k*25
        x=487+max(0,y-810)*.009
        for side in (1,-1):
            pts=[(x,y),(x-3,y+5),(x+2,y+12),(x-2,y+21),(x,y+25)]
            if side == -1: pts=mirror(pts)
            strip("06_Engraved_vine_%02d_%s"%(k,side),pts,width=.7,depth=4.55,thickness=.22,mat="pale_gold")
    # Light is constrained to a 6-pixel channel, ending above the lower steel blade.
    panel("07_Narrow_runic_energy_inlay",[(507,537),(513,537),(513,832),(510,846),(507,832)],2.8,"energy",.12,False)
    for i in range(21):
        y=548+i*13.8
        strip("08_Arcane_rune_%02d"%i,[(507,y-4),(511,y),(507,y+4),(511,y+7)],width=.65,depth=3.2,thickness=.2,mat="energy",curved=False)
    diamond=[(510,837),(522,878),(510,925),(498,878),(510,837)]
    strip("09_Middle_diamond_setting",diamond,width=2.0,depth=5,thickness=.8,curved=False)
    gem("10_Middle_crystal",510,878,7.2,28,base=4.2,crown=7)
    strip("11_Lower_unlit_diamond",[(510,1185),(519,1214),(510,1249),(501,1214),(510,1185)],width=1.5,depth=4.5,thickness=.55,curved=False)


def build_guard():
    # Curved sickle quillons, not a straight crossbar or a boat-shaped guard.
    left=[(483,333),(473,347),(452,357),(428,363),(418,358),(414,363),
          (401,362),(388,366),(366,367),(349,365),(339,360),(334,350),
          (324,363),(314,379),(307,395),(302,415),(299,441),
          (309,425),(327,411),(348,402),(371,395),(394,392),
          (416,393),(438,399),(458,411),(476,426),(492,409)]
    # Sample only the long arcs, leaving the sharp tips intact.
    for tag,poly in (("L",left),("R",mirror(left))):
        panel("20_Crescent_quillon_"+tag,poly,8.7,"dark",.65)
    rim=[(483,337),(462,355),(438,362),(414,368),(388,372),(365,373),(347,370),
         (334,359),(321,378),(311,404),(301,437)]
    paired_strip("21_Silver_quillon_cutting_bevel",rim,width=4.5,depth=9.1,thickness=1,mat="edge")
    inner=[(302,437),(318,417),(344,404),(371,396),(395,394),(419,396),(444,404),(476,426)]
    paired_strip("22_Quillon_inner_gold_edge",inner,width=2.2,depth=9.2,thickness=.65,mat="pale_gold")
    paired_strip("23_Quillon_gilded_spine",[(332,370),(357,379),(389,376),(420,373),(450,365),(477,349)],width=1.6,depth=9.8,thickness=.5)
    # Low relief acanthus leaves: flat leaf plates and rolled stems.
    leaves=[[(401,361),(418,370),(437,379),(411,380),(397,397),(404,379),(389,375)],
            [(446,350),(458,359),(478,364),(457,369),(438,378),(446,366),(430,362)]]
    for i,outline in enumerate(leaves):
        for tag,poly in (("L",outline),("R",mirror(outline))):
            panel("24_Acanthus_leaf_%s_%s"%(i,tag),poly,10.2,"gold",.5)
    for j in range(7):
        x=334+j*18
        y=385-5*math.sin(j*.40)
        pts=[(x-6,y+5),(x-1,y+3),(x+3,y-1),(x+1,y-5),(x-3,y-4),(x-3,y),(x+4,y+3),(x+9,y)]
        paired_strip("25_Quillon_engraved_scroll_%02d"%j,pts,width=.8,depth=9.65,thickness=.28,mat="pale_gold")
    # Diamond frame and pointed finials accurately follow the reference silhouette.
    panel("26_Central_diamond_frame",[(510,317),(549,378),(510,439),(472,378)],14,"gold",1.05)
    panel("27_Central_recess",[(510,330),(539,378),(510,425),(480,378)],15.2,"recess",.5)
    strip("28_Diamond_bezel_outer",[(510,323),(545,378),(510,431),(476,378),(510,323)],width=2.5,depth=15.2,thickness=1,mat="pale_gold",curved=False)
    strip("29_Diamond_bezel_inner",[(510,337),(536,378),(510,419),(484,378),(510,337)],width=1.3,depth=16.2,thickness=.6,mat="edge",curved=False)
    gem("30_Principal_cut_sapphire",510,377,25,40,base=16.6,crown=26)
    # Four sweeping structural ribs surround the blade shoulder with real gaps.
    rib=[(478,388),(466,400),(461,416),(472,440),(477,456),(486,481),(494,508),(510,535),
         (503,490),(496,461),(487,437),(480,414),(487,400)]
    for tag,p in (("L",rib),("R",mirror(rib))):
        panel("31_Gothic_shoulder_rib_"+tag,p,10.6,"dark",.55)
    paired_strip("32_Gilded_shoulder_ogive",[(478,390),(466,408),(475,438),(481,460),(493,489),(510,533)],width=2.1,depth=11.2,thickness=.7)
    paired_strip("33_Shoulder_silver_chisel",[(464,408),(473,433),(475,449),(466,459),(479,473),(490,498)],width=2.3,depth=10.9,thickness=.7,mat="edge")
    paired_strip("34_Inner_gothic_rib",[(486,410),(494,437),(490,460),(497,485),(510,533)],width=1,depth=11.5,thickness=.4,mat="pale_gold")
    # Flaring collar under the grip meets the main stone without a round boss.
    flare=[(488,313),(479,331),(474,346),(464,354),(451,358),(471,360),(487,350),(498,332),(510,307)]
    for tag,p in (("L",flare),("R",mirror(flare))):
        panel("35_Grip_root_flaring_leaf_"+tag,p,10.5,"gold",.6)
    paired_strip("36_Grip_root_edge",[(485,316),(482,331),(471,350),(451,357)],width=2.3,depth=11.5,thickness=.8,mat="pale_gold")
    paired_strip("37_Grip_root_inner_leaf",[(494,322),(489,342),(476,354)],width=1.3,depth=12,thickness=.45,mat="edge")
    panel("38_Central_crown_spike",[(510,300),(522,326),(510,343),(498,326)],16,"dark",.45)
    paired_strip("39_Crown_spike_trim",[(510,300),(500,326),(510,341)],width=1.4,depth=16.5,thickness=.5,mat="pale_gold",curved=False)


def build_grip():
    oval_loft("40_Leather_grip_oval_core",[(134,21,16),(146,22,17),(220,23,17),(287,25.5,18),(320,27,19)])
    # Continuous wrapped ribbon; edges are raised by 0.4 mm, not thick metal rings.
    verts=[]; faces=[]; count=550
    for i in range(count):
        t=i/(count-1)
        theta=t*2*math.pi*6.1+.45
        y=137+t*179
        rx=21.3+(y-137)/179*5.4
        for j in range(4):
            offset=(-9,-8.2,8.2,9)[j]
            d=(.55 if j in (0,3) else 1.0)
            verts.append(xyz(CX+(rx+d)*math.cos(theta),y+offset,(17+d)*math.sin(theta)))
    for i in range(count-1):
        for j in range(3):
            a=i*4+j; faces.append((a,a+1,a+5,a+4))
    obj=mesh_object("41_Continuous_overlapping_leather_ribbon",verts,faces,M["leather"],0,True)
    apply_projection(obj,"leather")
    # Stitched wrap edging actually follows the oval grip in depth.
    for edge in (-8.4,8.4):
        pts=[]
        for i in range(500):
            t=i/499; a=t*math.pi*2*6.1+.45; y=137+t*179
            rx=21.3+(y-137)/179*5.4
            pts.append(xyz(CX+(rx+.9)*math.cos(a),y+edge,18*math.sin(a)))
        data=bpy.data.curves.new("Leather_seam","CURVE"); data.dimensions="3D"
        data.bevel_depth=.40*S; data.bevel_resolution=2
        spl=data.splines.new("POLY"); spl.points.add(len(pts)-1)
        for p,co in zip(spl.points,pts):p.co=(*co,1)
        obj=bpy.data.objects.new("42_Leather_wrap_seam",data); bpy.data.collections["WEAPON"].objects.link(obj)
        data.materials.append(M["dark"]); PARTS.append(obj)
    for y in (169,202,236,269):
        panel("43_Grip_diamond_rivet_%s"%y,[(510,y-6),(516,y),(510,y+6),(504,y)],19,"pale_gold",.4)
        strip("44_Rivet_chisel_%s"%y,[(510,y-4),(510,y+4)],width=.8,depth=20.1,thickness=.6,mat="edge",curved=False)
    oval_loft("45_Upper_grip_gilded_ferrule",[(120,23.5,18),(125,25.5,20),(133,25.7,20),(140,23,18)],"gold")
    oval_loft("46_Lower_grip_gilded_ferrule",[(312,26.5,19),(318,29,20.5),(329,30,21),(333,27,19)],"gold")


def build_pommel():
    outline=[(510,14),(524,38),(532,50),(539,44),(550,56),(557,70),(553,83),(547,91),
             (541,83),(536,97),(532,111),(531,128),(487,128),(484,111),(480,97),
             (474,84),(470,91),(464,84),(460,71),(466,57),(478,45),(486,49),(498,32)]
    panel("50_Point_crown_pommel",outline,11,"dark",.65)
    left=[(510,16),(498,41),(485,63),(478,70),(490,89),(495,106),(498,123)]
    paired_strip("51_Crown_silver_outer_frame",left,width=4.5,depth=12,thickness=1.2,mat="edge")
    paired_strip("52_Crown_gold_inner_frame",[(510,25),(500,48),(489,68),(500,91),(504,114),(503,125)],width=2.0,depth=13,thickness=.7)
    paired_strip("53_Crown_flared_shoulder",[(480,48),(469,66),(466,76),(471,86),(478,74),(486,84),(491,100),(494,120)],width=3,depth=11.9,thickness=1.1,mat="pale_gold")
    panel("54_Pommel_diamond_bezel",[(510,33),(530,67),(510,101),(489,67)],14.6,"gold",.7)
    panel("55_Pommel_diamond_recess",[(510,37),(527,67),(510,96),(493,67)],15.4,"recess",.2)
    gem("56_Pommel_cut_sapphire",510,66,14.5,28,base=16,crown=23)
    for side in (1,-1):
        for i in range(5):
            y=83+i*7.5; x=484+i*1.9
            coords=[(x,y),(x+3,y+2),(x+2,y+5),(x-1,y+4)]
            if side==-1:coords=mirror(coords)
            strip("57_Crown_miniature_engraving_%s_%s"%(side,i),coords,width=.8,depth=12.6,thickness=.22,mat="pale_gold")


def init():
    OUT.mkdir(parents=True,exist_ok=True)
    shutil.copy2(SOURCE,OUT/"reference_concept.png")
    for ob in list(bpy.data.objects):bpy.data.objects.remove(ob,do_unlink=True)
    col=bpy.data.collections.new("WEAPON");bpy.context.scene.collection.children.link(col)
    global M,REF
    M=make_materials()
    image=bpy.data.images.load(str(OUT/"reference_concept.png"),check_existing=True)
    image.pack()
    # Source lighting is intentionally retained in this look-development variant.
    # A separate neutral clay render demonstrates the underlying volume.
    for key in ("steel","dark","edge","gold","leather","pale_gold","recess","sapphire"):
        mat=bpy.data.materials.new("REF_COLOR_"+key)
        mat.use_nodes=True
        n=mat.node_tree.nodes; l=mat.node_tree.links
        bs=n.get("Principled BSDF")
        tex=n.new("ShaderNodeTexImage"); tex.image=image; tex.extension="EXTEND"
        tex.label="Source artwork projection: includes original light and wear"
        uv=n.new("ShaderNodeUVMap");uv.uv_map="ReferenceProjection";l.new(uv.outputs["UV"],tex.inputs["Vector"])
        l.new(tex.outputs["Color"],bs.inputs["Base Color"])
        bs.inputs["Metallic"].default_value=.68 if key not in ("leather","sapphire") else .0
        bs.inputs["Specular IOR Level"].default_value=.16 if key=="leather" else .3
        bs.inputs["Roughness"].default_value=.36 if key not in ("leather","sapphire") else (.6 if key=="leather" else .14)
        l.new(tex.outputs["Color"],bs.inputs["Emission Color"])
        bs.inputs["Emission Strength"].default_value=.55 if key!="sapphire" else .85
        mat["warning"]="Reference projection retains source lighting; not a de-lit PBR albedo."
        REF[key]=mat


def add_vfx():
    col=bpy.data.collections.new("VFX_PREVIEW_ONLY");bpy.context.scene.collection.children.link(col)
    for i in range(32):
        y=RNG.uniform(318,1000);x=CX+RNG.choice([-1,1])*RNG.uniform(52,115)
        size=RNG.uniform(.3,.85)
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=size*S,location=xyz(x,y,RNG.uniform(-12,32)))
        obj=bpy.context.object;obj.name="VFX_azure_spark_%02d"%i
        obj.scale=(.3,.3,RNG.uniform(1.1,2.5))
        for c in list(obj.users_collection):c.objects.unlink(obj)
        col.objects.link(obj);obj.data.materials.append(M["energy"]);obj["presentation_only"]=True


def stats():
    dg=bpy.context.evaluated_depsgraph_get()
    tr=ve=0;bounds=[]
    for obj in PARTS:
        ev=obj.evaluated_get(dg)
        me=ev.to_mesh();me.calc_loop_triangles()
        tr+=len(me.loop_triangles);ve+=len(me.vertices)
        bounds.extend(obj.matrix_world@Vector(p) for p in obj.bound_box)
        ev.to_mesh_clear()
    dims=[max(v[i] for v in bounds)-min(v[i] for v in bounds) for i in range(3)]
    return dict(objects=len(PARTS),evaluated_triangles=tr,evaluated_vertices=ve,dimensions_m=dims,
                source_pixels=[1024,1536],assumed_total_length_m=1.6,
                observed="single front view",inferred="reverse side, depth, tang, hidden assembly",
                texture_method="Original front concept projected as colour; original lighting remains baked in.",
                detail_method="Profile-matched closed volumes, blade wedge, cut stones, metal trims, leather wraps; fine engravings in texture.")


def save_and_render(quick=False,only=None):
    scene=setup_studio(str(OUT),quick=quick)
    add_vfx()
    scene.unit_settings.system="METRIC"
    scene.unit_settings.scale_length=1
    root=bpy.data.objects.new("AZURE_SWORD_grip_socket",None);bpy.data.collections["WEAPON"].objects.link(root)
    root.location=xyz(CX,225,0)
    bpy.context.view_layer.update()
    for obj in PARTS:
        matrix=obj.matrix_world.copy();obj.parent=root;obj.matrix_world=matrix
    bpy.context.view_layer.update()
    scene["MODEL_NOTES"]="Single-view reconstruction. Front colour uses reference projection. Reverse, thickness and hidden assembly inferred."
    # Keep camera, reference image and construction geometry available for editing.
    cameras={o.name:o for o in bpy.data.objects if o.type=="CAMERA"}
    print("CAMERAS",list(cameras))
    (OUT/"asset_report.json").write_text(json.dumps(stats(),ensure_ascii=False,indent=2))
    bpy.ops.object.select_all(action="DESELECT")
    for obj in PARTS+[root]:obj.select_set(True)
    bpy.context.view_layer.objects.active=root
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/"Azure_Reference_Reconstruction.blend"))
    bpy.ops.export_scene.gltf(filepath=str(OUT/"Azure_Reference_Reconstruction.glb"),export_format="GLB",use_selection=True,
                              export_apply=True,export_cameras=False,export_lights=False,export_extras=True)
    print("EXPORT_DONE",flush=True)
    # Explicit model renders, never substitutions with the original image.
    for label,camera in (("front",scene.camera),("detail",bpy.data.objects.get("detail_camera")),
                         ("threequarter",bpy.data.objects.get("threequarter_camera"))):
        if not camera or (only and label!=only):continue
        scene.camera=camera
        scene.render.filepath=str(OUT/(label+"_render.png"))
        print("RENDER",label,flush=True)
        bpy.ops.render.render(write_still=True)
    if only: return
    clay=bpy.data.materials.new("QA_clay_neutral");clay.use_nodes=True
    shader=clay.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value=(.26,.29,.32,1)
    shader.inputs["Roughness"].default_value=.45
    bpy.context.view_layer.material_override=clay
    bpy.data.collections["VFX_PREVIEW_ONLY"].hide_render=True
    scene.camera=bpy.data.objects.get("threequarter_camera",scene.camera)
    scene.render.filepath=str(OUT/"clay_geometry_render.png")
    bpy.ops.render.render(write_still=True)
    print("ALL_DONE",flush=True)


if __name__=="__main__":
    parser=argparse.ArgumentParser();parser.add_argument("--quick",action="store_true");parser.add_argument("--only")
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    init();build_blade();build_guard();build_grip();build_pommel()
    save_and_render(args.quick,args.only)
