"""Repose the unrigged download before heat weighting, keeping its textures."""
exec(pathlib.Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine/Tools/MiningPrototype/RigJumpsuit.py').read_text().split('# Temporary bind rig')[0])
def smooth(a,b,x):
 t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
for v in body.data.vertices:
 p=v.co.copy();side=1 if p.x>=0 else -1;x=abs(p.x);y=p.y
 radius=14 if y>136 else 18 if y>118 else 22
 w=smooth(radius,radius+6,x)*smooth(74,89,y)*(1-smooth(153,159,y))
 # Keep rigid hip badges on the hips; elbow badges follow the sleeves.
 materials={body.data.materials[poly.material_index].name for poly in []}
 if not w:continue
 pivot=Vector((side*18.8,148,0));d=p-pivot
 q=Vector((side*.16,-1,0)).rotation_difference(Vector((side,0,0)))
 from mathutils import Quaternion
 q=Quaternion().slerp(q,w)
 transformed=pivot+q@d
 transformed.x+=side*max(0,abs(transformed.x)-18.8)*.16*w
 transformed.y-=9*w
 v.co=transformed
# Weight the already separated T-pose, where hands can no longer influence hips.
for b in rig.data.bones:b.use_deform=b.name not in ['Root','Root_ExportRoot','Eyebrows','Eyes','Jaw']
bpy.ops.object.select_all(action='DESELECT');body.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
from mathutils.kdtree import KDTree
weighted=[v for v in body.data.vertices if sum(g.weight for g in v.groups)>1e-6];tree=KDTree(len(weighted))
for v in weighted:tree.insert(v.co,v.index)
tree.balance()
for v in body.data.vertices:
 total=sum(g.weight for g in v.groups)
 if total<1e-6:
  _,idx,_=tree.find(v.co)
  for g in body.data.vertices[idx].groups:body.vertex_groups[g.group].add([v.index],g.weight,'REPLACE')
  total=sum(g.weight for g in v.groups)
 for g in list(v.groups):body.vertex_groups[g.group].add([v.index],g.weight/total,'REPLACE')
 if v.co.y>153:
  for g in list(v.groups):body.vertex_groups[g.group].remove([v.index])
  (body.vertex_groups.get('Head') or body.vertex_groups.new(name='Head')).add([v.index],1,'REPLACE')
for o in bpy.context.scene.objects:
 if o.type=='MESH' and o!=body:o.hide_render=True;o.hide_set(True)
body.hide_render=False;body.hide_set(False);rig.hide_set(False)
bpy.ops.wm.save_as_mainfile(filepath=str(root/'HazardPayJumpsuit.blend'))
print(json.dumps({'body':body.name,'rig':rig.name,'vertices':len(body.data.vertices),'bones':len(rig.data.bones)}))
