"""Fit the downloaded standing-pose mesh to the existing gameplay skeleton.
Original download and source armature stay intact; work on duplicates.
"""
import bpy,math,json,pathlib
from mathutils import Vector,Matrix
root=pathlib.Path('C:/Users/codex/Documents/palclone/SourceArt/HazardPayJumpsuit');root.mkdir(parents=True,exist_ok=True)
for o in bpy.context.scene.objects:
 if o.type in ['MESH','ARMATURE']:
  o.hide_render=True;o.hide_set(True)
  if o.name=='PolygonSyntyCharacter':o.name='Previous fitting source'
before=set(bpy.data.objects)
bpy.ops.import_scene.gltf(filepath='C:/Users/codex/Downloads/byte engine files/synty new.glb')
rig=next(o for o in set(bpy.data.objects)-before if o.type=='ARMATURE');rig.animation_data.action=None
before=set(bpy.data.objects)
bpy.ops.import_scene.gltf(filepath='C:/Users/codex/Downloads/jumpsuit_low_poly.glb')
imported=set(bpy.data.objects)-before
for t in rig.animation_data.nla_tracks:t.mute=True
rig.data.pose_position='REST'
parts=[o for o in imported if o.type=='MESH']
collection=bpy.data.collections.new('Jumpsuit original download - preserved');bpy.context.scene.collection.children.link(collection)
working=[]
for original in parts:
 o=original.copy();o.data=original.data.copy();bpy.context.scene.collection.objects.link(o);world=original.matrix_world.copy();o.parent=None;o.matrix_world=world
 for c in list(original.users_collection):c.objects.unlink(original)
 collection.objects.link(original);original.hide_render=True;original.hide_set(True)
 working.append(o)
# Convert to the animation rig's centimetre coordinates, keeping the feet at zero.
scale=178/(4.640323638916016-.02583656646311283)
for o in working:
 for v in o.data.vertices:
  p=o.matrix_world@v.co
  v.co=Vector((p.x*scale,(p.z-.02583656646311283)*scale,-p.y*scale))
 o.matrix_world=rig.matrix_world.copy()
bpy.ops.object.select_all(action='DESELECT')
for o in working:o.select_set(True)
bpy.context.view_layer.objects.active=working[0];bpy.ops.object.join();body=bpy.context.object;body.name='HazardPay Jumpsuit Explorer'
import bmesh
bm=bmesh.new();bm.from_mesh(body.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.01);bm.to_mesh(body.data);bm.free();body.data.update()
# Temporary bind rig follows the downloaded relaxed arms. Bone names and source
# rest orientations are preserved when the result is transferred back.
fit=rig.copy();fit.data=rig.data.copy();bpy.context.scene.collection.objects.link(fit);fit.name='Jumpsuit fitting rig';fit.animation_data_clear();fit.data.pose_position='REST'
for b in fit.pose.bones:b.matrix_basis=Matrix.Identity(4)
bpy.ops.object.select_all(action='DESELECT');fit.select_set(True);bpy.context.view_layer.objects.active=fit;bpy.ops.object.mode_set(mode='EDIT')
for side,sign in [('L',1),('R',-1)]:
 shoulder=Vector((sign*18.8,148,0));elbow=Vector((sign*24,121,0));wrist=Vector((sign*26,94,1))
 values={'Shoulder_'+side:(shoulder,elbow),'Elbow_'+side:(elbow,wrist)}
 oldhand=rig.data.bones['Hand_'+side].head_local.copy()
 rot=Vector((sign,0,0)).rotation_difference(Vector((sign*.1,-1,0)).normalized())
 suffix='' if side=='L' else '_1'
 for b in fit.data.edit_bones:
  if b.name in values:
   h,t=values[b.name];old=(b.tail-b.head).normalized();q=old.rotation_difference((t-h).normalized());b.transform(q.to_matrix().to_4x4());b.head=h;b.tail=t
  elif b.name=='Hand_'+side or (b.name.startswith(('Finger_','IndexFinger_','Thumb_')) and (b.name.endswith('_1')==bool(suffix))):
   source=rig.data.bones[b.name];b.head=wrist+rot@((source.head_local-oldhand)*.54);b.tail=wrist+rot@((source.tail_local-oldhand)*.54);b.align_roll(rot@source.z_axis)
for b in fit.data.edit_bones:
 b.use_deform=b.name not in ['Root','Root_ExportRoot','Eyebrows','Eyes','Jaw']
bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.object.select_all(action='DESELECT');body.select_set(True);fit.select_set(True);bpy.context.view_layer.objects.active=fit
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
from mathutils.kdtree import KDTree
weighted=[v for v in body.data.vertices if sum(g.weight for g in v.groups)>1e-6]
if not weighted:raise RuntimeError('Automatic heat weighting failed entirely')
tree=KDTree(len(weighted))
for v in weighted:tree.insert(v.co,v.index)
tree.balance()
for v in body.data.vertices:
 total=sum(g.weight for g in v.groups)
 if total<1e-6:
  _,idx,_=tree.find(v.co)
  for g in body.data.vertices[idx].groups:body.vertex_groups[g.group].add([v.index],g.weight,'REPLACE')
  total=sum(g.weight for g in v.groups)
 for g in list(v.groups):body.vertex_groups[g.group].add([v.index],g.weight/total,'REPLACE')
# The download contains detached trim and a body under the suit. Heat weights
# alone can cross from a relaxed hand to the nearby hip, so use anatomical
# regions and the fitted bone segments for this bind conversion.
def distance(p,a,b):
 d=b-a;t=max(0,min(1,(p-a).dot(d)/max(d.length_squared,1e-8)));return (p-(a+d*t)).length
for v in body.data.vertices:
 p=v.co;side='L' if p.x>=0 else 'R'
 if p.y>153:names=['Head']
 elif abs(p.x)>(16 if p.y>138 else 21 if p.y>120 else 24) and 78<p.y<153:
  names=['Hand_'+side] if p.y<98 else ['Clavicle_'+side,'Shoulder_'+side,'Elbow_'+side,'Hand_'+side]
 elif p.y<87:names=['UpperLeg_'+side,'LowerLeg_'+side,'Ankle_'+side,'Ball_'+side]
 else:names=['Hips','Spine_01','Spine_02','Spine_03','Neck']
 heat={}
 for g in v.groups:
  n=body.vertex_groups[g.group].name
  if n.startswith(('Finger_','IndexFinger_','Thumb_')):n='Hand_'+('R' if n.endswith('_1') else 'L')
  if n in names:heat[n]=heat.get(n,0)+g.weight
 ranked=sorted([(distance(p,fit.data.bones[n].head_local,fit.data.bones[n].tail_local),n) for n in names])[:2]
 if sum(heat.values())>.05:ranked=[(0,n) for n in heat];weights=list(heat.values())
 else:weights=[1/max(d,1.5)**4 for d,n in ranked]
 total=sum(weights)
 for g in list(v.groups):body.vertex_groups[g.group].remove([v.index])
 for (d,n),w in zip(ranked,weights):(body.vertex_groups.get(n) or body.vertex_groups.new(name=n)).add([v.index],w/total,'REPLACE')
# Freeze the authored bind pose into the source T-pose without resampling clips.
fit.data.pose_position='POSE'
for b in fit.pose.bones:
 source=rig.data.bones[b.name]
 b.matrix=source.matrix_local@Matrix.Diagonal((1,source.length/max(b.bone.length,.001),1,1));bpy.context.view_layer.update()
bpy.context.view_layer.update()
bpy.context.view_layer.objects.active=body
for modifier in list(body.modifiers):
 if modifier.type=='ARMATURE':bpy.ops.object.modifier_apply(modifier=modifier.name)
world=body.matrix_world.copy();body.parent=rig;body.matrix_world=world
m=body.modifiers.new('Original gameplay animation skeleton','ARMATURE');m.object=rig
fit.hide_render=True;fit.hide_set(True)
# Face and helmet are rigid to prevent unwanted facial deformation.
head=body.vertex_groups.get('Head') or body.vertex_groups.new(name='Head')
for v in body.data.vertices:
 if v.co.y>153:
  for g in list(v.groups):body.vertex_groups[g.group].remove([v.index])
  head.add([v.index],1,'REPLACE')
for o in bpy.context.scene.objects:
 if o.type=='MESH' and o!=body:o.hide_render=True;o.hide_set(True)
body.hide_render=False;body.hide_set(False);rig.hide_set(False)
unweighted=[v.index for v in body.data.vertices if sum(g.weight for g in v.groups)<.99]
report={'vertices':len(body.data.vertices),'bones':len(rig.data.bones),'unweighted':len(unweighted),'materials':[m.name for m in body.data.materials]}
(root/'rig-inspection.json').write_text(json.dumps(report,indent=2))
if unweighted:raise RuntimeError('Unweighted vertices: '+str(unweighted[:20]))
bpy.ops.wm.save_as_mainfile(filepath=str(root/'HazardPayJumpsuit.blend'))
print(json.dumps(report))
