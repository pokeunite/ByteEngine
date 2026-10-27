import bpy,json
from pathlib import Path
OUT=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine/artifacts/spider-models')
scene=bpy.data.scenes['Spider Rig Repair'];bpy.context.window.scene=scene
for kind in ['PlayerHunter','DrillBreaker']:
 col=bpy.data.collections['RIGGED_'+kind];arm=next(o for o in col.objects if o.type=='ARMATURE')
 arm.animation_data.action=None
 bpy.ops.object.select_all(action='DESELECT')
 for o in col.objects:
  if o.type=='MESH':o.select_set(True);bpy.context.view_layer.objects.active=o
 bpy.ops.object.join();mesh=bpy.context.object;mesh.name=kind+' fitted eight-legged skin'
 bpy.ops.object.select_all(action='DESELECT');mesh.select_set(True);arm.select_set(True);bpy.context.view_layer.objects.active=arm
 bpy.ops.export_scene.gltf(filepath=str(OUT/(kind+'-Animated.glb')),export_format='GLB',use_selection=True,use_active_scene=True,export_animations=True,export_force_sampling=True,export_animation_mode='ACTIONS',export_anim_single_armature=False,export_nla_strips=True,export_def_bones=False,export_skins=True,export_cameras=False,export_lights=False)
print('Exported both fitted spider GLBs')
