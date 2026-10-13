import bpy
from mathutils import Vector,Matrix
scene=bpy.context.scene
for rig in [o for o in scene.objects if o.type=='ARMATURE' and not o.name.startswith('Shotgun')]:
 action=next(t.strips[0].action for t in rig.animation_data.nla_tracks if t.name=='Reload');rig.animation_data.action=action
 hand=rig.pose.bones['LeftHand'];basis=rig.data.bones['LeftHand'].matrix_local.to_3x3()
 poses=[(92,(0,.016,.158)),(96,(0,-.016,.158)),(102,(0,.016,.158)),(108,(0,0,0))] if rig.name.startswith('Pistol') else [(92,(.07,-.20,.083)),(96,(.07,-.232,.083)),(102,(.07,-.20,.083)),(108,(0,0,0))]
 for frame,pos in poses:
  hand.location=basis.inverted()@Vector(pos);hand.keyframe_insert(data_path='location',frame=frame)
 rig.animation_data.action=None
 for bone in rig.pose.bones:bone.location=(0,0,0);bone.rotation_quaternion=(1,0,0,0)
result='Support-hand mechanism contact added to reload finish'
