"""Stage 3: pose the rigged model procedurally and render frames.
blender -b rig.blend --python anim3d.py -- outdir anim[,anim] [frames] [view] [size]"""
import bpy, sys, os, math, functools
from math import sin, cos, pi, radians as rad
from mathutils import Quaternion, Vector
print=functools.partial(print,flush=True)
a=sys.argv[sys.argv.index("--")+1:]; outdir=os.path.abspath(a[0]); anims=a[1].split(","); N=int(a[2]) if len(a)>2 else 12
view=a[3] if len(a)>3 else "side"; size=int(a[4]) if len(a)>4 else 384
ao=bpy.data.objects["rig"]; pb=ao.pose.bones
for p in pb: p.rotation_mode='QUATERNION'
def rot(name,*axis_deg):
    """Rotate a bone by world-axis angles (degrees), applied in order, about its own head."""
    q=Quaternion()
    for ax,deg in axis_deg: q=Quaternion(ax,rad(deg))@q
    R=ao.data.bones[name].matrix_local.to_quaternion(); pb[name].rotation_quaternion=R.inverted()@q@R
def move(name,v):
    R=ao.data.bones[name].matrix_local.to_quaternion(); pb[name].location=R.inverted()@Vector(v)
X,Y,Z=(1,0,0),(0,1,0),(0,0,1)
def reset():
    for p in pb: p.rotation_quaternion=Quaternion(); p.location=(0,0,0)
def fwd(deg): return (Y,-deg)        # swing a hanging limb forward
def walk(t,amp=24,lift=38,speed=1):
    reset(); w=2*pi*t
    move("body",(0,0,0.008*sin(2*w))); rot("body",(Y,1.5*sin(2*w)))
    rot("spine",(Z,3*sin(w))); rot("pelvis",(Z,3*sin(w)),(X,3*sin(w))); rot("chest",(Z,-3*sin(w)))
    rot("neck",(Y,-3*sin(2*w+0.6))); rot("head",(Y,3*sin(2*w+0.6)),(X,2*sin(w)))
    for leg,ph in (("hleg","L",0.0),("fleg","L",0.25),("hleg","R",0.5),("fleg","R",0.75)) if False else ((("hleg","L"),0.0),(("fleg","L"),0.25),(("hleg","R"),0.5),(("fleg","R"),0.75)):
        k,s=leg; u=w+2*pi*ph; swing=amp*sin(u); up=max(0.0,cos(u))      # cos>0: leg travelling forward, off the ground
        rot(f"{k}1.{s}",fwd(swing)); rot(f"{k}2.{s}",fwd(-lift*up)); rot(f"{k}paw.{s}" if False else f"{k[0]}paw.{s}",fwd(-18*up+ -0.3*swing))
    for i,n in enumerate(("tail1","tail2","tail3")): rot(n,(X,(10+4*i)*sin(2*w-0.5*i)))
    for s,sg in (("L",1),("R",-1)): rot("ear1."+s,(Y,5*sin(2*w+1.0))); rot("ear2."+s,(Y,8*sin(2*w+0.3)),(X,sg*4*sin(2*w)))
def run(t): 
    reset(); w=2*pi*t
    move("body",(0,0,0.03*sin(w+0.5))); rot("body",(Y,5*sin(w)))
    rot("spine",(Y,-6*sin(w))); rot("pelvis",(Y,-8*sin(w)))
    for k,s,ph in (("hleg","L",0.0),("hleg","R",0.08),("fleg","L",0.5),("fleg","R",0.58)):
        u=w+2*pi*ph; swing=40*sin(u); up=max(0.0,cos(u))
        rot(f"{k}1.{s}",fwd(swing)); rot(f"{k}2.{s}",fwd(-60*up)); rot(f"{k[0]}paw.{s}",fwd(-20*up))
    rot("neck",(Y,-5*sin(w))); rot("head",(Y,5*sin(w)))
    for i,n in enumerate(("tail1","tail2","tail3")): rot(n,(Y,-(8+4*i)*sin(w-0.6*i)))
    for s,sg in (("L",1),("R",-1)): rot("ear1."+s,(Y,-14*sin(w+0.8)),(X,sg*-8*(1+sin(w+0.8)))); rot("ear2."+s,(Y,-20*sin(w+0.2)),(X,sg*-10*(1+sin(w+0.2))))
def wag(t):
    reset(); w=2*pi*t
    for i,n in enumerate(("tail1","tail2","tail3")): rot(n,(X,(22+6*i)*sin(2*w-0.5*i)))
    rot("pelvis",(Z,4*sin(2*w)),(X,3*sin(2*w))); rot("spine",(Z,2*sin(2*w)))
    rot("head",(X,4*sin(w)),(Y,-4)); 
    for s,sg in (("L",1),("R",-1)): rot("ear2."+s,(X,sg*3*sin(2*w)))
def sitpose(k,t=0):
    """k: 0 standing .. 1 sitting"""
    reset(); k=k*k*(3-2*k); p=38*k                     # rump drops as the body pitches up about the shoulders
    rot("body",(Y,-p)); move("body",(-0.03*k,0,-0.035*k))
    rot("chest",(Y,6*k)); rot("neck",(Y,p*0.55)); rot("head",(Y,p*0.35))
    for s,sg in (("L",1),("R",-1)):
        rot("fleg1."+s,(Y,p-6*k)); rot("fpaw."+s,(Y,0))
        rot("hleg1."+s,(Y,p),fwd(78*k),(Z,sg*14*k)); rot("hleg2."+s,fwd(4*k)); rot("hpaw."+s,fwd(-50*k))
        rot("ear1."+s,(Y,p*0.25)); rot("ear2."+s,(Y,p*0.3))
    for i,n in enumerate(("tail1","tail2","tail3")): rot(n,(Y,(p*0.45 if i==0 else 6*k)),(X,(14+5*i)*sin(4*pi*t-0.5*i)*k))
POSES={"walk":walk,"run":run,"wag":wag,"sit":lambda t: sitpose(1,t),"sitdown":lambda t: sitpose(t*N/(N-1) if N>1 else 1)}
sc=bpy.context.scene; sc.render.engine='BLENDER_WORKBENCH'; sh=sc.display.shading; sh.light='STUDIO'; sh.color_type='VERTEX'; sh.show_cavity=True
sh.show_object_outline=True; sh.object_outline_color=(0.02,0.02,0.02)
sc.render.resolution_x=size; sc.render.resolution_y=size; sc.render.film_transparent=True; sc.render.image_settings.color_mode='RGBA'
cam=bpy.data.cameras.new("c"); cam.type='ORTHO'; cam.ortho_scale=1.45; co=bpy.data.objects.new("c",cam); sc.collection.objects.link(co); sc.camera=co
d=Vector({"side":(0,-1,0),"q":(0.55,-0.8,0.22),"front":(1,0,0)}[view]).normalized(); co.location=Vector((0,0,0.47))+d*5; co.rotation_euler=(-d).to_track_quat('-Z','Y').to_euler()
import numpy as np
mesh_o=bpy.data.objects["zoe"]
def ground():
    """Shift the whole dog so its lowest point rests on z=0."""
    bpy.context.view_layer.update(); dg=bpy.context.evaluated_depsgraph_get(); ev=mesh_o.evaluated_get(dg); me=ev.to_mesh()
    z=np.empty(len(me.vertices)*3,np.float32); me.vertices.foreach_get("co",z); ev.to_mesh_clear()
    move("root",(0,0,0.005-float(z[2::3].min())))
for an in anims:
    os.makedirs(f"{outdir}/{an}_{view}",exist_ok=True)
    for f in range(N):
        POSES[an](f/N); ground(); bpy.context.view_layer.update()
        sc.render.filepath=f"{outdir}/{an}_{view}/{f:03d}.png"; bpy.ops.render.render(write_still=True)
    print("rendered",an,view,N)
