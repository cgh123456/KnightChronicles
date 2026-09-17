"""New ART-007 body surfaces measured from the supplied concept sheet.

No meshes are read from an earlier model. Helpers are passed by the assembler.
Front is -Y, height 3.20, sole Z=0, belt Z=1.38, helmet crown Z=3.20.
"""
import math, random, bpy
from mathutils import Vector, Matrix


def build(api):
    globals().update({k:api[k] for k in ('mesh','tube','uvball','box','loft','cuff','tapered_limb','PARTS','M')})
    import knight_refined_cloth,knight_refined_armor
    knight_refined_cloth.build(api)
    crest();belt()
    for sign in (-1,1):
        leg_start=len(PARTS)
        hip=(sign*.33,.025,1.03);knee=(sign*.405,0,.655);ankle=(sign*.445,0,.244)
        tapered_limb('12 | Soft padded trouser thigh',knee,hip,.154,.177,'cloth_dark')
        tapered_limb('12 | Calf leather foundation',ankle,knee,.130,.155,'leather_dark')
        knight_refined_armor.build_leg_armor(api,sign)
        boot(sign)
        for o in PARTS[leg_start:]:o['leg_side']=sign
        knight_refined_armor.build_arm(api,sign)
        glove(sign)


def surface_panel(name,surface,nu,nv,mat):
    vs=[surface(u/nu,v/nv) for v in range(nv+1) for u in range(nu+1)]
    fs=[(v*(nu+1)+u,v*(nu+1)+u+1,(v+1)*(nu+1)+u+1,(v+1)*(nu+1)+u) for v in range(nv) for u in range(nu)]
    mesh(name,vs,fs,mat,thickness=.016)
    return vs


def edge_binding(name,vs,nu,nv,edge,width=2,mat='trim',back=False):
    arr=[];n=nv if edge in ('left','right') else nu
    for i in range(n+1):
        if edge=='left':ids=[i*(nu+1),i*(nu+1)+width]
        elif edge=='right':ids=[i*(nu+1)+nu-width,i*(nu+1)+nu]
        else:ids=[(nv-width)*(nu+1)+i,nv*(nu+1)+i]
        for idx in ids:
            x,y,z=vs[idx];arr.append((x,y+(.012 if back else -.012),z))
    mesh(name,arr,[(i*2,i*2+1,i*2+3,i*2+2) for i in range(n)],mat,thickness=.006)


def crest():
    # A vertical folded escutcheon, with two broad readable planes.
    outline=[(-.151,1.909),(0,1.882),(.151,1.909),(.140,1.686),(0,1.567),(-.140,1.686)]
    verts=[(x,-.349-.026*(1-abs(x)/.151),z) for x,z in outline]
    mesh('06 | Folded brass shield insignia',verts,[(0,1,4,5),(1,2,3,4)],'gold',False,.014,.003)
    tube('06 | Shield fine raised border',verts,.0038,'gold',True,sides=8)


def belt():
    cuff('07 | Full leather belt',(0,0,1.320),(0,0,1.438),.445,.439,'leather',depth=.73)
    for z in (1.329,1.427):
        tube('07 | Belt edge seam',[(.448*math.sin(a),-.328*math.cos(a),z) for a in [i*math.tau/100 for i in range(100)]],.0038,'leather_light',True,sides=6)
    box('07 | Buckle leather overlap',(0,-.347,1.376),(.256,.030,.117),'leather_dark',.013)
    pts=[(-.099,-.377,1.329),(.099,-.377,1.329),(.115,-.377,1.343),(.115,-.377,1.415),(.095,-.377,1.432),(-.095,-.377,1.432),(-.115,-.377,1.415),(-.115,-.377,1.344)]
    tube('07 | Rounded rectangular brass buckle',pts,.011,'gold',True,sides=10)
    tube('07 | Buckle tongue',[(.064,-.389,1.38),(-.025,-.389,1.38),(-.025,-.381,1.398)],.007,'gold')
    for sign in (-1,1):
        for x in (.17,.32):
            y=-.326*math.sqrt(1-(x/.449)**2)-.008
            box('07 | Belt keeper',(sign*x,y,1.38),(.031,.021,.119),'leather_light',.005)
    for x in (.20,.25,.29):uvball('07 | Belt punched hole',(x,-.326*math.sqrt(1-(x/.449)**2)-.01,1.383),(.007,.003,.007),'dark',12,8)
    # Reference has one visible utility pouch opposite the sword.
    for sign in (-1,):
        o=box('07 | Soft hip pouch',(sign*.481,-.102,1.211),(.188,.178,.259),'leather',.037);o.rotation_euler.y=-.15
        o=box('07 | Curved hip pouch lid',(sign*.481,-.201,1.269),(.182,.022,.105),'leather_light',.016);o.rotation_euler.y=-.15
        tube('07 | Pouch retaining tab',[(sign*.481,-.225,1.32),(sign*.475,-.228,1.211)],.014,'leather_dark')
        uvball('07 | Pouch brass clasp',(sign*.475,-.235,1.233),(.011,.008,.013),'gold',12,8)


def glove(sign,gripping=False):
    import knight_refined_hands
    return knight_refined_hands.build(globals(),sign,gripping)


def boot(sign):
    import knight_refined_boots
    knight_refined_boots.build(globals(),sign)
