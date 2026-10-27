import bpy
from pathlib import Path
from mathutils import Vector
base=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine');scene=bpy.data.scenes['Refinery Reference'];bpy.context.window.scene=scene;scene.frame_set(1)
collection=bpy.data.collections['Reference Refinery'];rig=bpy.data.objects['RefineryRig'];delta=Vector((1.2,1.08,0))
prefixes=('Bulk bin floor','Bulk skip side','Yellow upper side rim','Lower structural side rail','Bulk skip end panel','Yellow upper end rim','Lower end rail','Skip yellow corner post','Corner protection plate','Skip foot pad','Skip forklift channel','Reference ingot pile')
moved=[]
for o in collection.objects:
 if o.type!='MESH':continue
 centre=sum((v.co for v in o.data.vertices),Vector())/max(1,len(o.data.vertices))
 bin_bolt=o.name.startswith('Hex mounting bolt') and abs(abs(centre.x-2.45)-1.325)<.04 and min(abs(centre.y+.49),abs(centre.y+2.65))<.04 and min(abs(centre.z-.18),abs(centre.z-1.09))<.02
 if o.name.startswith(prefixes) or bin_bolt:
  for v in o.data.vertices:v.co+=delta
  moved.append(o.name)
bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT');bone=rig.data.edit_bones['BucketFloor'];bone.head+=delta;bone.tail+=delta;bpy.ops.object.mode_set(mode='OBJECT')
scene.camera.location=(8,-10.5,7.1);scene.camera.rotation_euler=(Vector((.8,-.3,1.65))-scene.camera.location).to_track_quat('-Z','Y').to_euler();scene.camera.data.ortho_scale=8.7
# A saved top/front inspection makes the shared belt/bin centreline easy to check.
exec(compile((base/'Tools/MiningPrototype/export_reference_refinery.py').read_text(),'export_reference_refinery.py','exec'))
result={'bin_centre':(3.65,-.35),'belt_centreline_y':-.35,'belt_end_x':2.84,'bin_near_edge_x':2.325,'lip_overhang_inside_bin':.515,'moved_parts':len(moved)}
