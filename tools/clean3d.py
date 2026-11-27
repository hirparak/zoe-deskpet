"""Stage 1: turn the raw image-to-3D mesh into something riggable.
blender -b --python clean3d.py -- hi.glb out.blend [voxel] [faces]"""
import bpy, sys, math, functools, numpy as np
print=functools.partial(print,flush=True)
from mathutils import Matrix
a=sys.argv[sys.argv.index("--")+1:]; src,out=a[0],a[1]; voxel=float(a[2]) if len(a)>2 else 0.007; GS=0.05; GL=0.05; faces=int(a[3]) if len(a)>3 else 30000
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src); print("imported")
o=[x for x in bpy.data.objects if x.type=='MESH'][0]
bpy.context.view_layer.objects.active=o; o.select_set(True)
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
me=o.data; n=len(me.vertices)
co=np.empty(n*3,np.float32); me.vertices.foreach_get("co",co); co=co.reshape(-1,3)
ca=me.color_attributes[0]; print("colour attr",ca.name,ca.domain,ca.data_type,"verts",n,"faces",len(me.polygons))
m=len(ca.data); col=np.empty(m*4,np.float32); ca.data.foreach_get("color",col); col=col.reshape(-1,4)
if ca.domain=='CORNER':   # average the corners onto vertices
    li=np.empty(m,np.int32); me.loops.foreach_get("vertex_index",li)
    vc=np.zeros((n,3),np.float64); cnt=np.zeros(n); np.add.at(vc,li,col[:,:3]); np.add.at(cnt,li,1); rgb=(vc/np.maximum(cnt,1)[:,None]).astype(np.float32)
else: rgb=col[:,:3].copy()
sat=rgb.max(1)-rgb.min(1); lum=rgb.mean(1)
print("lum pct",np.percentile(lum,[5,25,50,75,95]),"sat pct",np.percentile(sat,[5,25,50,75,95]))
# unseen side comes out grey: snap unsaturated, not-dark colours to the coat (values are linear)
dark=(sat<GS)&(lum<=GL); coat=np.median(rgb[dark],axis=0); grey=(sat<GS)&~dark; rgb[grey]=coat; print("recoloured",grey.mean(),"coat",coat)
sat=rgb.max(1)-rgb.min(1)
# yaw-align: long horizontal axis -> X
xy=co[:,:2]-co[:,:2].mean(0); w,v=np.linalg.eigh(np.cov(xy.T)); ax=v[:,1]; ang=math.atan2(ax[1],ax[0])
R=Matrix.Rotation(-ang,4,'Z'); me.transform(R); me.update()
me.vertices.foreach_get("co",co.ravel()); co=co.reshape(-1,3)
# head towards +X: tan (saturated) colour high up is the muzzle
vsat=sat
if vsat is not None:
    hi=co[:,2]>np.percentile(co[:,2],60); tan=hi&(vsat>0.12)
    print("tan-high count",tan.sum(),"mean x",co[tan,0].mean() if tan.any() else None)
    if tan.any() and co[tan,0].mean()<co[:,0].mean(): me.transform(Matrix.Rotation(math.pi,4,'Z')); me.update()
me.vertices.foreach_get("co",co.ravel()); co=co.reshape(-1,3)
# feet on the ground, centred, 1 unit long
s=1.0/(co[:,0].max()-co[:,0].min())
T=Matrix.Scale(s,4)@Matrix.Translation((-(co[:,0].max()+co[:,0].min())/2,-(co[:,1].max()+co[:,1].min())/2,-co[:,2].min()))
me.transform(T); me.update()
src_o=o; src_o.name="zoe_src"
# remeshed copy
d=src_o.copy(); d.data=src_o.data.copy(); d.name="zoe"; bpy.context.scene.collection.objects.link(d)
for x in bpy.data.objects: x.select_set(False)
d.select_set(True); bpy.context.view_layer.objects.active=d
r=d.modifiers.new("r",'REMESH'); r.mode='VOXEL'; r.voxel_size=voxel; r.adaptivity=0; bpy.ops.object.modifier_apply(modifier="r")
print("remeshed faces",len(d.data.polygons))
# the side the picture never showed is pitted: keep the good (+Y) half and mirror it
import bmesh
vv=np.array([v.co[:] for v in d.data.vertices]); nose=vv[vv[:,0]>vv[:,0].max()-0.03]; y0=float(nose[:,1].mean()); print("nose y",y0,"bbox y",vv[:,1].min(),vv[:,1].max())
bm=bmesh.new(); bm.from_mesh(d.data)
bmesh.ops.bisect_plane(bm,geom=bm.verts[:]+bm.edges[:]+bm.faces[:],plane_co=(0,y0,0),plane_no=(0,-1,0),clear_outer=True)
bm.to_mesh(d.data); bm.free()
d.data.transform(Matrix.Translation((0,-y0,0)))
mi=d.modifiers.new("m",'MIRROR'); mi.use_axis=(False,True,False); mi.use_clip=True; mi.merge_threshold=0.002; bpy.ops.object.modifier_apply(modifier="m")
r=d.modifiers.new("r2",'REMESH'); r.mode='VOXEL'; r.voxel_size=voxel; r.adaptivity=0; bpy.ops.object.modifier_apply(modifier="r2")
src_o.data.transform(Matrix.Translation((0,-y0,0)))
sm=d.modifiers.new("s",'SMOOTH'); sm.factor=0.5; sm.iterations=6; bpy.ops.object.modifier_apply(modifier="s")
# keep the biggest island only
bm=bmesh.new(); bm.from_mesh(d.data); bm.verts.ensure_lookup_table()
seen={}; isl=[]
for v in bm.verts:
    if v.index in seen: continue
    st=[v]; seen[v.index]=len(isl); k=[v]
    while st:
        x=st.pop()
        for e in x.link_edges:
            y=e.other_vert(x)
            if y.index not in seen: seen[y.index]=len(isl); st.append(y); k.append(y)
    isl.append(k)
isl.sort(key=len,reverse=True); print("islands",[len(i) for i in isl[:6]])
bmesh.ops.delete(bm,geom=[v for i in isl[1:] for v in i],context='VERTS'); bm.to_mesh(d.data); bm.free()
dc=d.modifiers.new("d",'DECIMATE'); dc.ratio=min(1,faces*1.0/max(1,len(d.data.polygons))*1.0); bpy.ops.object.modifier_apply(modifier="d")
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.quads_convert_to_tris(); bpy.ops.object.mode_set(mode='OBJECT')
print("final faces",len(d.data.polygons),"verts",len(d.data.vertices))
# colours from the original: nearest source vertex
from mathutils import kdtree
sco=np.empty(n*3,np.float32); src_o.data.vertices.foreach_get("co",sco); sco=sco.reshape(-1,3)
step=max(1,n//400000); idx=np.arange(0,n,step); kd=kdtree.KDTree(len(idx))
for j,i in enumerate(idx): kd.insert(sco[i],j)
kd.balance()
for c in list(d.data.color_attributes): d.data.color_attributes.remove(c)
nc=d.data.color_attributes.new("Color",'FLOAT_COLOR','POINT'); out_c=np.ones((len(d.data.vertices),4),np.float32)
for v in d.data.vertices:
    ns=kd.find_n((v.co.x,abs(v.co.y),v.co.z),4); out_c[v.index,:3]=np.mean([rgb[idx[j]] for _,j,_ in ns],axis=0)
nc.data.foreach_set("color",out_c.ravel()); d.data.color_attributes.active_color=nc; d.data.color_attributes.render_color_index=0
bpy.ops.object.shade_smooth()
bpy.data.objects.remove(src_o)
bpy.ops.wm.save_as_mainfile(filepath=out)
