import bpy,json
from pathlib import Path
OUT=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine/artifacts/spider-models')
for kind in ['PlayerHunter','DrillBreaker']:
 source=bpy.data.scenes[kind+' spider inspection']
 scene=bpy.data.scenes.get(kind+' fitted rig preview') or bpy.data.scenes.new(kind+' fitted rig preview')
 bpy.context.window.scene=scene
 col=bpy.data.collections['RIGGED_'+kind]
 if col not in list(scene.collection.children):scene.collection.children.link(col)
 for o in source.objects:
  if o.type in ['CAMERA','LIGHT'] or 'studio floor' in o.name:
   if o.name not in scene.objects:scene.collection.objects.link(o)
 scene.camera=source.camera;scene.world=source.world;scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=720;scene.render.resolution_y=720;scene.render.resolution_percentage=100
 scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast';scene.view_settings.exposure=-.65
 arm=next(o for o in col.objects if o.type=='ARMATURE');arm.animation_data.action=bpy.data.actions[kind+'_Walk'];scene.frame_set(9)
 scene.render.filepath=str(OUT/(kind+'-fitted-walk.png'));bpy.ops.render.render(write_still=True)
print('Rendered both fitted rigs at Walk frame 9')
