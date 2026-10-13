import bpy
from mathutils import Vector
C=bpy.app.driver_namespace['cyber'];scene=bpy.context.scene
for g in C['guns']:
 g.rest();upper='LeftUpperArm';fore='LeftForearm';a,b,parent,connected=g.bones[upper];old_a=a.copy();old_b=b.copy();new_a=a.copy();new_b=b.copy();new_a.x=-.46;new_b.x=-.36;da=new_a-old_a;db=new_b-old_b;wrist=g.bones[fore][1]
 for mesh in g.meshes:
  names={group.index:group.name for group in mesh.vertex_groups}
  for vertex in mesh.data.vertices:
   original=vertex.co.copy();move=Vector((0,0,0))
   for group in vertex.groups:
    name=names.get(group.group)
    if name==upper:
     axis=old_b-old_a;t=max(0,min(1,(original-old_a).dot(axis)/axis.length_squared));move+=(da*(1-t)+db*t)*group.weight
    elif name==fore:
     axis=wrist-old_b;t=max(0,min(1,(original-old_b).dot(axis)/axis.length_squared));move+=db*(1-t)*group.weight
   vertex.co+=move
  mesh.data.update()
 g.bones[upper]=(new_a,new_b,parent,connected);head,tail,parent,connected=g.bones[fore];g.bones[fore]=(new_b,tail,parent,connected)
 pole='LeftElbowPole';head,tail,parent,connected=g.bones[pole];g.bones[pole]=(head+db,tail+db,parent,connected)
 bpy.ops.object.select_all(action='DESELECT');g.rig.select_set(True);bpy.context.view_layer.objects.active=g.rig;bpy.ops.object.mode_set(mode='EDIT')
 for name in [upper,fore,pole]:
  bone=g.rig.data.edit_bones[name];bone.use_connect=False;bone.head=g.bones[name][0];bone.tail=g.bones[name][1]
 g.rig.data.edit_bones[fore].use_connect=True;bpy.ops.object.mode_set(mode='OBJECT')
 for track in list(g.rig.animation_data.nla_tracks):g.rig.animation_data.nla_tracks.remove(track)
 g.actions={};g.make_actions();g.rest()
result='Left elbow/shoulder route moved out of the camera centre; hand contact positions preserved'
