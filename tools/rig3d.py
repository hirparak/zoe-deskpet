"""Stage 2: give the cleaned model a skeleton and skin it.
blender -b clean.blend --python rig3d.py -- out.blend"""
import bpy, sys, functools, numpy as np
print=functools.partial(print,flush=True)
out=sys.argv[sys.argv.index("--")+1]
o=bpy.data.objects["zoe"]
arm=bpy.data.armatures.new("rig"); ao=bpy.data.objects.new("rig",arm); bpy.context.scene.collection.objects.link(ao)
bpy.context.view_layer.objects.active=ao; bpy.ops.object.mode_set(mode='EDIT')
def B(name,head,tail,parent=None,deform=True):
    b=arm.edit_bones.new(name); b.head=head; b.tail=tail; b.use_deform=deform
    if parent: b.parent=arm.edit_bones[parent]
    return b
B("root",(0,0,0),(0,0,0.1),None,False)
B("body",(0.13,0,0.42),(0.13,0,0.52),"root",False)          # pivot at the shoulders
B("chest",(0.00,0,0.40),(0.16,0,0.42),"body")
B("spine",(0.00,0,0.40),(-0.13,0,0.40),"body")
B("pelvis",(-0.13,0,0.40),(-0.29,0,0.42),"spine")
B("neck",(0.16,0,0.46),(0.20,0,0.58),"chest")
B("head",(0.20,0,0.58),(0.40,0,0.66),"neck")
B("tail1",(-0.29,0,0.48),(-0.36,0,0.55),"pelvis"); B("tail2",(-0.36,0,0.55),(-0.41,0,0.63),"tail1"); B("tail3",(-0.41,0,0.63),(-0.41,0,0.75),"tail2")
for s,sg in (("L",1),("R",-1)):
    B("ear1."+s,(0.21,sg*0.17,0.78),(0.26,sg*0.20,0.62),"head"); B("ear2."+s,(0.26,sg*0.20,0.62),(0.22,sg*0.20,0.42),"ear1."+s)
    B("fleg1."+s,(0.135,sg*0.095,0.36),(0.135,sg*0.095,0.19),"chest"); B("fleg2."+s,(0.135,sg*0.095,0.19),(0.135,sg*0.095,0.07),"fleg1."+s); B("fpaw."+s,(0.135,sg*0.095,0.07),(0.20,sg*0.095,0.02),"fleg2."+s)
    B("hleg1."+s,(-0.25,sg*0.08,0.36),(-0.25,sg*0.08,0.19),"pelvis"); B("hleg2."+s,(-0.25,sg*0.08,0.19),(-0.25,sg*0.08,0.07),"hleg1."+s); B("hpaw."+s,(-0.25,sg*0.08,0.07),(-0.19,sg*0.08,0.02),"hleg2."+s)
bpy.ops.object.mode_set(mode='OBJECT')
for x in bpy.data.objects: x.select_set(False)
o.select_set(True); ao.select_set(True); bpy.context.view_layer.objects.active=ao
r=bpy.ops.object.parent_set(type='ARMATURE_AUTO'); print("auto weights",r)
names=[g.name for g in o.vertex_groups]; cnt={n:0 for n in names}; un=0
for v in o.data.vertices:
    if not v.groups: un+=1
    for g in v.groups:
        if g.weight>0.5: cnt[names[g.group]]+=1
print("unweighted verts",un,"of",len(o.data.vertices)); print("dominant counts",cnt)
bpy.ops.wm.save_as_mainfile(filepath=out)
