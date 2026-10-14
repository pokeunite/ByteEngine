import bpy
from mathutils import Vector
from pathlib import Path
p=Path('C:/Users/codex/Downloads/byte engine files/Milk Depot Environment/MilkDepot_Environment.blend')
bpy.ops.wm.open_mainfile(filepath=str(p))
for scene in bpy.data.scenes:
 bpy.context.window.scene=scene
 bpy.ops.object.select_all(action='DESELECT')
level=bpy.data.scenes['02 - Milk Depot assembled reference'];bpy.context.window.scene=level
level.camera=bpy.data.objects['Overview'];level.render.resolution_x=1680;level.render.resolution_y=1200
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':
   area.spaces.active.region_3d.view_location=Vector((-2,0,0));area.spaces.active.region_3d.view_distance=72
   area.spaces.active.region_3d.view_rotation=level.camera.rotation_euler.to_quaternion();area.spaces.active.region_3d.view_perspective='ORTHO'
   area.spaces.active.shading.type='MATERIAL';area.spaces.active.shading.use_scene_world=False
   area.spaces.active.overlay.show_floor=False
bpy.ops.wm.save_as_mainfile(filepath=str(p))
print('Final source opens at the assembled overview with clean selection.')
