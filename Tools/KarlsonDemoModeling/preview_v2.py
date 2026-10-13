import bpy, math
from pathlib import Path
from mathutils import Vector
scene=bpy.context.scene;camera=scene.camera
out=Path(r'C:/Users/codex/Downloads/byte engine files/Karlson V2/Previews');out.mkdir(exist_ok=True)
rigs=[o for o in scene.objects if o.type=='ARMATURE']
for rig in rigs:
 for other in rigs:
  for obj in other.children:obj.hide_render=other!=rig
 rig.animation_data.action=None
 for track in rig.animation_data.nla_tracks:track.mute=True
 for bone in rig.pose.bones:bone.location=(0,0,0);bone.rotation_quaternion=(1,0,0,0)
 scene.frame_set(1);bpy.context.view_layer.update();verts=[]
 for obj in rig.children:
  if obj.type=='MESH':verts.extend([obj.matrix_world@v.co for v in obj.data.vertices])
 lo=Vector([min(v[i] for v in verts) for i in range(3)]);hi=Vector([max(v[i] for v in verts) for i in range(3)]);target=(lo+hi)/2
 camera.data.type='ORTHO';camera.location=target+Vector((1.4,1.5,.8));camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();bpy.context.view_layer.update()
 projected=[camera.matrix_world.inverted()@v for v in verts];camera.data.ortho_scale=max(max(v.x for v in projected)-min(v.x for v in projected),(max(v.y for v in projected)-min(v.y for v in projected))*1100/720)*1.2
 scene.render.filepath=str(out/(rig.name.split('_')[0]+'_v2.png'));bpy.ops.render.render(write_still=True)
result=str(out)
