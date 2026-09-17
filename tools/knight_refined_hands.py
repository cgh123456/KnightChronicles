"""ART-007 glove revision: short relaxed digits and axis-correct sword grasp.

The grasp is constructed around the actual tilted sword axis. Its fingers are
jointed open arcs, not horizontal rings. Remeshing joins the thenar pad, finger
webs and wrist into one editable sculpt without closing the handle channel.
"""
import math
import bpy
import bmesh
from mathutils import Vector, Matrix


def build(api, sign, gripping=False):
    mesh=api['mesh'];uvball=api['uvball'];tube=api['tube'];parts=api['PARTS']
    start=len(parts)
    label='11 | '+str(sign)+(' Grasp ' if gripping else ' Relaxed ')
    cx=sign*.992
    glove_mat='leather_glove' if 'leather_glove' in api['M'] else 'leather_dark'

    def ellipsoid(name,p,scale,axes=None):
        ob=uvball(label+name,p,scale,glove_mat,40,28)
        if axes is not None:
            ob.rotation_euler=Matrix([axes[0],axes[1],axes[2]]).transposed().to_euler()
        return ob

    def digit(name,controls,radii,axis_fit=None):
        controls=[Vector(p) for p in controls];points=[];rr=[]
        for i in range(len(controls)-1):
            p0=controls[max(0,i-1)];p1=controls[i];p2=controls[i+1];p3=controls[min(len(controls)-1,i+2)]
            for j in range(9):
                t=j/9
                p=.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t)
                r=radii[i]*(1-t)+radii[i+1]*t
                if axis_fit:
                    g,d=axis_fit;s=(p-g).dot(d);radial=p-(g+d*s)
                    minimum=.0335+r
                    if radial.length<minimum:p=g+d*s+radial.normalized()*minimum
                points.append(p);rr.append(r)
        points.append(controls[-1]);rr.append(radii[-1])
        if axis_fit:
            g,d=axis_fit
            for i,(p,r) in enumerate(zip(points,rr)):
                s=(p-g).dot(d);radial=p-(g+d*s)
                if radial.length<.0335+r:points[i]=g+d*s+radial.normalized()*(.0335+r)
        verts=[];faces=[];prev=None;ns=18
        for i,(p,r) in enumerate(zip(points,rr)):
            tangent=(points[min(i+1,len(points)-1)]-points[max(0,i-1)]).normalized()
            normal=tangent.cross(Vector((0,1,0))) if prev is None else prev-tangent*prev.dot(tangent)
            if normal.length<.001:normal=tangent.cross(Vector((1,0,0)))
            normal.normalize();prev=normal.copy();bit=tangent.cross(normal)
            for j in range(ns):
                angle=j*math.tau/ns
                verts.append(p+r*(normal*math.cos(angle)+bit*math.sin(angle)))
        for i in range(len(points)-1):
            for j in range(ns):faces.append((i*ns+j,i*ns+(j+1)%ns,(i+1)*ns+(j+1)%ns,(i+1)*ns+j))
        faces.extend([tuple(reversed(range(ns))),tuple((len(points)-1)*ns+j for j in range(ns))])
        mesh(label+name,verts,faces,glove_mat)
        ellipsoid(name+' fingertip',points[-1],(rr[-1],)*3)

    if not gripping:
        ellipsoid('Compact glove palm',(cx,-.024,1.170),(.080,.069,.108))
        ellipsoid('Wrist leather socket',(sign*.973,-.033,1.254),(.076,.065,.045))
        # Index to little: the little root is higher and its curl is shorter.
        for f in range(4):
            x=cx+sign*(f-1.5)*.039
            root=(1.116,1.109,1.119,1.135)[f]
            tip=(1.013,1.003,1.014,1.042)[f]
            points=[(x,-.030,root),(x+sign*.006,-.045,root-.052),
                    (x+sign*.008,-.019,tip-.001),(x+sign*.002,.013,tip+.008)]
            radius=(.0265,.028,.0265,.023)[f]
            digit('Short curled digit '+str(f),points,[radius,radius*.98,radius*.88,radius*.76])
        thumb=[(cx-sign*.052,-.010,1.191),(cx-sign*.087,-.025,1.161),
               (cx-sign*.103,-.039,1.118),(cx-sign*.078,-.026,1.092)]
        digit('Full opposing thumb',thumb,[.039,.034,.028,.024])
        # The web rises into the thumb's base; it is not a fifth dangling rod.
        ellipsoid('Thenar leather pad',(cx-sign*.047,-.003,1.147),(.039,.045,.050))
        def plate(u,t):
            x=(u*2-1)*(.076*(1-t)+.061*t)
            return Vector((cx+x,-.099+.025*(x/.08)**2,1.274-.129*t-.010*(1-abs(u*2-1))*t))
    else:
        grip=Vector((sign*.993,-.111,1.157))
        direction=Vector((sign*.49,-.18,-.855)).normalized()
        back=Vector((0,1,0));back=(back-direction*back.dot(direction)).normalized()
        inward=direction.cross(back).normalized()
        if inward.x*sign>0:inward=-inward
        # Keep a right-handed basis for ellipsoid orientation.
        across=back.cross(direction).normalized()
        def pos(s,w,b):return grip+direction*s+inward*w+back*b
        ellipsoid('Palm resting on sword handle',pos(-.012,.000,.079),(.065,.040,.108),
                  (across,back,direction))
        ellipsoid('Wrist leather socket',(sign*.973,-.033,1.254),(.075,.062,.048))
        for f in range(4):
            s=(-.080,-.028,.024,.075)[f]
            smaller=(1,1,.97,.90)[f]
            # Metacarpal knuckle, proximal hinge, middle hinge, fingertip.
            # Unequal open angular arcs leave the handle channel at r=.034.
            controls=[pos(s,-.051,.052),pos(s+.001,-.060,.010),
                      pos(s+.004,-.037,-.049),pos(s+.005,.018,-.052),
                      pos(s+.004,.048*smaller,-.014)]
            radius=(.026,.0268,.0254,.0228)[f]
            digit('Jointed wrapped digit '+str(f),controls,
                  [radius*1.08,radius,radius*.93,radius*.87,radius*.76],(grip,direction))
        thumb=[pos(-.099,.052,.077),pos(-.093,.081,.030),
               pos(-.071,.058,-.025),pos(-.053,.010,-.077)]
        digit('Thumb pressed diagonally over index',thumb,[.038,.033,.028,.025],(grip,direction))
        ellipsoid('Thumb base thenar pad',pos(-.072,.039,.071),(.042,.043,.047),
                  (across,back,direction))
        def plate(u,t):
            s=-.128+.125*t;w=(u*2-1)*(.069*(1-t)+.055*t)
            b=.091+.027*math.sin(t*math.pi*.85)-.020*(u*2-1)**2
            return pos(s,w,b)

    solids=parts[start:];bpy.ops.object.select_all(action='DESELECT')
    for ob in solids:ob.select_set(True)
    bpy.context.view_layer.objects.active=solids[0];bpy.ops.object.join()
    palm=bpy.context.object;palm.name=label+'Continuous tailored leather hand'
    del parts[start:];parts.append(palm)
    rem=palm.modifiers.new('Joined leather palm and finger webs','REMESH')
    rem.mode='VOXEL';rem.voxel_size=.0032;rem.use_smooth_shade=True
    bpy.ops.object.modifier_apply(modifier=rem.name)
    sm=palm.modifiers.new('Soft padded glove joints','SMOOTH');sm.factor=.30;sm.iterations=2
    bpy.ops.object.modifier_apply(modifier=sm.name)
    dec=palm.modifiers.new('Balanced sculpt topology','DECIMATE');dec.ratio=.72
    bpy.ops.object.modifier_apply(modifier=dec.name)
    if gripping:
        # Preserve a real handle channel through the stitched glove web.
        # This removes only compressed/contact leather, including wrist-web
        # overlap near the top of the inclined handle, never the outer hand.
        bpy.ops.mesh.primitive_cylinder_add(vertices=64,radius=.0346,depth=.253,
            location=grip+direction*.0035)
        cutter=bpy.context.object;cutter.rotation_euler=direction.to_track_quat('Z','Y').to_euler()
        bpy.context.view_layer.objects.active=palm
        mod=palm.modifiers.new('Exact sword-handle contact channel','BOOLEAN')
        mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cutter
        bpy.ops.object.modifier_apply(modifier=mod.name)
        bpy.data.objects.remove(cutter,do_unlink=True)
        bm=bmesh.new();bm.from_mesh(palm.data)
        # Boolean intersections can leave near-coincident vertices ~1.3e-6
        # apart inside otherwise valid n-gons. Collapse only this geometric
        # coincidence, rather than deleting correctly shaped small polygons.
        bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001)
        bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.00001)
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        bm.to_mesh(palm.data);bm.free();palm.data.update()
    for poly in palm.data.polygons:poly.use_smooth=True

    # The dorsal plate follows the back of each pose, so the grasp's palm and
    # fingers can approach the handle without pushing it through a metal plate.
    nu=24;nv=20;verts=[plate(i/nu,j/nv) for j in range(nv+1) for i in range(nu+1)]
    faces=[(j*(nu+1)+i,j*(nu+1)+i+1,(j+1)*(nu+1)+i+1,(j+1)*(nu+1)+i)
           for j in range(nv) for i in range(nu)]
    mesh(label+'Fitted dorsal metal plate',verts,faces,'steel',thickness=.014)
    tube(label+'Dorsal plate turned tip',verts[-nu-1:],.0045,'steel_light',sides=8)
    # Finger folds are carried by the sculpted joints and shared hide shader;
    # separate raised crease wires would become spikes in the side silhouette.
    if not gripping:
        # A mild natural turn keeps fingers stacked in depth without moving
        # the wrist socket away from the agreed forearm attachment.
        pivot=Vector((sign*.972,-.033,1.273))
        transform=Matrix.Translation(pivot)@Matrix.Rotation(sign*.18,4,'Z')@Matrix.Translation(-pivot)
        for ob in parts[start:]:ob.matrix_world=transform@ob.matrix_world
    for ob in parts[start:]:
        ob['hand_side']=sign;ob['gripping']=gripping;ob['art007_hand']=True
    return parts[start:]
