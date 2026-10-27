import bpy,math
from pathlib import Path
out=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine/artifacts/reference-press')
rig=bpy.data.objects['AmmoPressRig'];length=2.0;radius=.13;perimeter=2*length+math.tau*radius
def belt_angle(s):
 s%=perimeter
 if s<length:return 0
 s-=length
 if s<math.pi*radius:return -s/radius
 s-=math.pi*radius
 if s<length:return -math.pi
 return -math.pi-(s-length)/radius
for f in range(1,92):
 t=(f-1)/90
 for i in range(28):
  p=rig.pose.bones['BeltSlat%02d'%i];distance=i*perimeter/28+t*perimeter
  p.rotation_euler[0]=belt_angle(distance)-math.floor(distance/perimeter)*math.tau-belt_angle(i*perimeter/28)
  p.keyframe_insert(data_path='rotation_euler',frame=f)
 for key in ['FrontRoller','RearRoller']:
  p=rig.pose.bones[key];p.rotation_euler[1]=-t*math.tau*6;p.keyframe_insert(data_path='rotation_euler',frame=f)
for layer in rig.animation_data.action.layers:
 for strip in layer.strips:
  for bag in getattr(strip,'channelbags',[]):
   for curve in bag.fcurves:
    for p in curve.keyframe_points:p.interpolation='LINEAR'
bpy.context.scene.frame_set(1)
exec(compile(Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine/Tools/MiningPrototype/refine_reference_press.py').read_text(),'refine_reference_press.py','exec'))
