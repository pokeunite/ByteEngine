import bpy,math,json
from mathutils import Vector
S=bpy.data.scenes['Hazard Pay Decorated Camp REVIEW'];bpy.context.window.scene=S
hidden=[]
for col in S.collection.children:
 if not col.name.startswith('Camp machine'):continue
 for o in col.objects:
  if o.type=='MESH' and len(o.data.materials)==0:o.hide_render=True;o.hide_set(True);hidden.append(o.name)
  if o.type=='ARMATURE':
   for bone in o.pose.bones:bone.location=(0,0,0);bone.rotation_quaternion=(1,0,0,0);bone.rotation_euler=(0,0,0);bone.scale=(1,1,1)
for o in S.objects:
 if o.instance_type=='COLLECTION' and o.instance_collection and o.instance_collection.name=='HPD 15-walkway-floor-panel':o.location.z=-.10
S.frame_set(1);bpy.context.view_layer.update()
S.render.resolution_percentage=85
print(json.dumps({'hidden_import_helpers':hidden,'scene':S.name}))
bpy.ops.render.render(write_still=True)
