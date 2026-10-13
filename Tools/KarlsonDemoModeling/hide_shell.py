import bpy
from pathlib import Path
scene=bpy.context.scene;rig=next(o for o in scene.objects if o.type=='ARMATURE' and o.name.startswith('Shotgun'))
for track in rig.animation_data.nla_tracks:
 action=track.strips[0].action;rig.animation_data.action=action;bone=rig.pose.bones['Shell']
 if track.name=='Reload':keys=[(1,.001),(27,.001),(28,1),(76,1),(78,.001),(120,.001)]
 else:keys=[(1,.001),(int(action.frame_range[1]),.001)]
 for frame,size in keys:
  bone.scale=(size,size,size);bone.keyframe_insert(data_path='scale',frame=frame)
rig.animation_data.action=None
for obj in [o for o in scene.objects if o.type=='ARMATURE']:
 obj.animation_data.action=None
 for bone in obj.pose.bones:bone.location=(0,0,0);bone.rotation_quaternion=(1,0,0,0);bone.scale=(1,1,1)
 for child in obj.children:child.hide_render=False
rig.pose.bones['Shell'].scale=(.001,.001,.001)
scene.frame_set(1);result='Shell visible only during insertion'
