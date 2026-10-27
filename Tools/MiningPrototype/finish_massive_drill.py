import bpy,math
from pathlib import Path
from mathutils import Vector
base=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine');scene=bpy.data.scenes['Drill Reference'];bpy.context.window.scene=scene;scene.frame_set(1);collection=bpy.data.collections['Reference Mining Drill'];rig=bpy.data.objects['MiningDrillRig']
# A genuinely open discharge port, with its dark back recessed into the housing.
housing=next(o for o in collection.objects if o.type=='MESH' and o.name.startswith('Ore separator housing'))
bpy.ops.object.select_all(action='DESELECT');bpy.ops.mesh.primitive_cube_add(size=1,location=(4.05,1.30,5.59));cutter=bpy.context.object;cutter.scale=(1.2,1.15,.78);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
boolean=housing.modifiers.new('Open ore discharge tunnel','BOOLEAN');boolean.operation='DIFFERENCE';boolean.solver='EXACT';boolean.object=cutter
bpy.context.view_layer.objects.active=housing;bpy.ops.object.modifier_apply(modifier=boolean.name);bpy.data.objects.remove(cutter,do_unlink=True);housing.vertex_groups['Root'].add(list(range(len(housing.data.vertices))),1,'REPLACE')
for o in collection.objects:
 if o.type=='MESH' and o.name.startswith('Separator outlet shadow'):
  for v in o.data.vertices:v.co.x-=.65
# Slower auger rotation conveys the weight of the enlarged machinery.
for layer in rig.animation_data.action.layers:
 for strip in layer.strips:
  for bag in getattr(strip,'channelbags',[]):
   for curve in bag.fcurves:
    if curve.data_path=='pose.bones["Auger"].rotation_euler':
     for key in curve.keyframe_points:key.co.y/=3;key.handle_left.y/=3;key.handle_right.y/=3
scene.frame_set(1)
exec(compile((base/'Tools/MiningPrototype/export_reference_drill.py').read_text(),'export_reference_drill.py','exec'))
