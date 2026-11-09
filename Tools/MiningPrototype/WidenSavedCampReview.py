import bpy
from pathlib import Path
scene=bpy.data.scenes.get('Hazard Pay Decorated Camp REVIEW')
if scene:
 for o in scene.objects:
  if o.instance_type=='COLLECTION' and o.instance_collection and o.instance_collection.get('asset_name')=='15-walkway-floor-panel' and o.scale.x<1.3:
   o.location.x*=1.35;o.location.y=o.location.y*1.5-6;o.scale=(1.35,1.5,1)
 bpy.context.window.scene=scene
bpy.ops.wm.save_as_mainfile(filepath=str(Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine/artifacts/camp-decorations/HazardPay-DecoratedCamp-Review.blend')))
print('Saved widened camp paving in editable Blender source')
