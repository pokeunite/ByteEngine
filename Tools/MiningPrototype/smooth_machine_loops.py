"""Finalize constant-speed mechanical loops in the editable Blender sources."""
import bpy
from pathlib import Path
out=Path(__file__).resolve().parents[2]/'artifacts/mining-models'
for name in ['MiningDrill','AmmoPress','Refinery','CompanyTent']:
 bpy.ops.wm.open_mainfile(filepath=str(out/(name+'.blend')))
 count=0
 for action in bpy.data.actions:
  for layer in action.layers:
   for strip in layer.strips:
    for bag in getattr(strip,'channelbags',[]):
     for curve in bag.fcurves:
      for point in curve.keyframe_points:point.interpolation='LINEAR';count+=1
 assert count>0,(name,'no animation keys found')
 bpy.context.scene.frame_set(1);bpy.ops.object.select_all(action='DESELECT')
 rig=next(o for o in bpy.data.objects if o.type=='ARMATURE');rig.select_set(True)
 for obj in bpy.data.objects:
  if obj.type=='MESH' and obj.parent==rig:obj.select_set(True)
 bpy.context.view_layer.objects.active=rig
 bpy.ops.wm.save_as_mainfile(filepath=str(out/(name+'.blend')))
 bpy.ops.export_scene.gltf(filepath=str(out/(name+'.glb')),export_format='GLB',use_selection=True,export_animations=True,export_force_sampling=True,export_animation_mode='ACTIONS')
 print('LINEAR LOOP',name,count,flush=True)
