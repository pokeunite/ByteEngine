import bpy
from mathutils import Vector
C=bpy.app.driver_namespace['cyber'];scene=bpy.context.scene;max_shift={}
for g in C['guns']:
 shift=0
 for clip,action in g.actions.items():
  if not clip.startswith('Reload'):continue
  g.rest();g.rig.animation_data.action=action
  for f in range(1,int(action.frame_range[1])+1):
   scene.frame_set(f);bpy.context.view_layer.update()
   for side in ['Right','Left']:
    upper=side+'UpperArm';fore=side+'Forearm';target=g.rig.pose.bones[side+'WristControl'].matrix.translation;shoulder=g.bones[upper][0]
    length=(g.bones[upper][1]-shoulder).length+(g.bones[fore][1]-g.bones[fore][0]).length
    delta=target-shoulder;move=delta.normalized()*max(0,delta.length-length+.014);shift=max(shift,move.length)
    g.key(upper,f,move)
 max_shift[g.name]=shift;g.rest()
result={'maximum_shoulder_follow_m':max_shift}
