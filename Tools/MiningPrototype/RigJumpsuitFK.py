import pathlib
exec(pathlib.Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine/Tools/MiningPrototype/RigJumpsuit.py').read_text().split('# The download contains')[0])
# Merge finger weights into the closed glove's hand. Keep the source fingers in
# the skeleton for socket/animation compatibility, but do not stretch the glove.
from mathutils.kdtree import KDTree
cloth=set(i for p in body.data.polygons if body.data.materials[p.material_index].name.startswith('Material_25') for i in p.vertices)
tree=KDTree(len(cloth))
for i in cloth:tree.insert(body.data.vertices[i].co,i)
tree.balance()
trim=set(i for p in body.data.polygons if body.data.materials[p.material_index].name.startswith('Material_26') for i in p.vertices)
for i in trim:
 v=body.data.vertices[i];_,idx,_=tree.find(v.co);groups=[(g.group,g.weight) for g in body.data.vertices[idx].groups]
 for g in list(v.groups):body.vertex_groups[g.group].remove([i])
 for group,w in groups:body.vertex_groups[group].add([i],w,'REPLACE')
helmet=set(i for p in body.data.polygons if body.data.materials[p.material_index].name.startswith('Material_27') for i in p.vertices)
skin=set(i for p in body.data.polygons if body.data.materials[p.material_index].name.startswith('Material_29') for i in p.vertices)
for i in helmet|skin:
 v=body.data.vertices[i]
 name='Head' if i in helmet or v.co.y>153 else 'Hand_'+('L' if v.co.x>0 else 'R') if abs(v.co.x)>22 and v.co.y<105 else None
 if name:
  for g in list(v.groups):body.vertex_groups[g.group].remove([i])
  (body.vertex_groups.get(name) or body.vertex_groups.new(name=name)).add([i],1,'REPLACE')
for v in body.data.vertices:
 extra={}
 for g in list(v.groups):
  n=body.vertex_groups[g.group].name
  if n.startswith(('Finger_','IndexFinger_','Thumb_')):
   hand='Hand_'+('R' if n.endswith('_1') else 'L');extra[hand]=extra.get(hand,0)+g.weight;body.vertex_groups[g.group].remove([v.index])
 for n,w in extra.items():(body.vertex_groups.get(n) or body.vertex_groups.new(name=n)).add([v.index],w,'ADD')
fit.data.pose_position='POSE'
matrices={}
for b in fit.data.bones:
 position=(matrices[b.parent.name]@b.parent.matrix_local.inverted()@b.head_local) if b.parent else b.head_local
 rotation=rig.data.bones[b.name].matrix_local.to_quaternion().to_matrix().to_4x4()
 matrices[b.name]=Matrix.Translation(position)@rotation
 fit.pose.bones[b.name].matrix=matrices[b.name];bpy.context.view_layer.update()
bpy.context.view_layer.objects.active=body
for m in list(body.modifiers):
 if m.type=='ARMATURE':bpy.ops.object.modifier_apply(modifier=m.name)
world=body.matrix_world.copy();body.parent=None;body.matrix_world=world
# Build a clean T-pose bind with source joint orientations and fitted lengths.
bpy.ops.object.select_all(action='DESELECT');fit.select_set(True);bpy.context.view_layer.objects.active=fit
bpy.ops.object.mode_set(mode='EDIT')
for b in fit.data.edit_bones:
 mat=matrices[b.name];length=b.length;b.head=mat.translation;b.tail=b.head+mat.to_3x3()@Vector((0,length,0));b.align_roll(mat.to_3x3()@Vector((0,0,1)))
bpy.ops.object.mode_set(mode='OBJECT')
for b in fit.pose.bones:b.matrix_basis=Matrix.Identity(4)
fit.data.pose_position='REST';world=body.matrix_world.copy();body.parent=fit;body.matrix_world=world
m=body.modifiers.new('Jumpsuit fitted skeleton','ARMATURE');m.object=fit
for o in bpy.context.scene.objects:
 if o.type in ['MESH','ARMATURE'] and o not in [body,fit]:o.hide_render=True;o.hide_set(True)
body.hide_render=False;body.hide_set(False);fit.hide_render=False;fit.hide_set(False)
bpy.context.scene['jumpsuit_body']=body.name;bpy.context.scene['jumpsuit_rig']=fit.name
bpy.ops.wm.save_as_mainfile(filepath=str(root/'HazardPayJumpsuit.blend'))
print('Fitted T-pose uses connected FK positions and original joint orientations')
