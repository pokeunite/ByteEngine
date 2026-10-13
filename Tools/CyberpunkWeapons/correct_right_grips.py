import bpy,math
from mathutils import Vector,Matrix
C=bpy.app.driver_namespace['cyber'];scene=bpy.context.scene;rows=[]
for g in C['guns']:
 if g.name=='Pistol':continue
 g.rest();palm=Vector(g.grip);rotation=Matrix.Rotation(math.radians(-26 if g.name=='Shotgun' else -30),3,'X')
 def rotate(p):return palm+rotation@(Vector(p)-palm)
 old_wrist=g.contacts['Right']['wrist'].copy();new_wrist=rotate(old_wrist);delta_wrist=new_wrist-old_wrist
 original_index_tip=g.bones['RightHandIndex2'][1].copy();delta_index=original_index_tip-rotate(original_index_tip)
 # Move the glove mesh with its corrected grip angle, preserving the trigger-contact fingertip.
 for mesh in g.meshes:
  names={group.index:group.name for group in mesh.vertex_groups}
  for v in mesh.data.vertices:
   hand_weight=sum(group.weight for group in v.groups if names.get(group.group,'').startswith('RightHand'))
   if hand_weight>.01:
    v.co=rotate(v.co);index_weights={names[group.group]:group.weight for group in v.groups if names.get(group.group,'').startswith('RightHandIndex')}
    factor=sum((0 if name.endswith('0') else .5 if name.endswith('1') else 1)*weight for name,weight in index_weights.items());v.co+=delta_index*factor
   else:
    fore_weight=sum(group.weight for group in v.groups if names.get(group.group)=='RightForearm')
    if fore_weight>.01:
     elbow=g.bones['RightForearm'][0];axis=old_wrist-elbow;t=(v.co-elbow).dot(axis)/axis.length_squared;v.co+=delta_wrist*max(0,min(1,(t-.40)/.60))*fore_weight
  mesh.data.update()
 # Change the matching rest skeleton, instead of twisting only the mesh around an old bind pose.
 for name,(head,tail,parent,connected) in list(g.bones.items()):
  if name.startswith('RightHand') or name=='RightWristControl':
   head=rotate(head);tail=rotate(tail)
   if name.startswith('RightHandIndex'):
    index=int(name[-1]);head+=delta_index*(0 if index==0 else .0 if index==1 else .5);tail+=delta_index*(0 if index==0 else .5 if index==1 else 1)
   g.bones[name]=(head,tail,parent,connected)
 head,tail,parent,connected=g.bones['RightForearm'];g.bones['RightForearm']=(head,new_wrist,parent,connected)
 g.contacts['Right']['wrist']=new_wrist
 bpy.ops.object.select_all(action='DESELECT');g.rig.select_set(True);bpy.context.view_layer.objects.active=g.rig;bpy.ops.object.mode_set(mode='EDIT')
 for name,(head,tail,parent,connected) in g.bones.items():
  bone=g.rig.data.edit_bones[name];bone.use_connect=False;bone.head=head;bone.tail=tail
 for name,values in g.bones.items():g.rig.data.edit_bones[name].use_connect=values[3]
 bpy.ops.object.mode_set(mode='OBJECT')
 for track in list(g.rig.animation_data.nla_tracks):g.rig.animation_data.nla_tracks.remove(track)
 g.actions={};g.make_actions();g.rest();rows.append({'gun':g.name,'grip_rotation_degrees':-26 if g.name=='Shotgun' else -30,'trigger_tip_preserved':True})
result=rows
