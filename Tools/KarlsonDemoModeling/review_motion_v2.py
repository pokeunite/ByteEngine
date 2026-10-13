import bpy,json,math
from pathlib import Path
from mathutils import Vector
scene=bpy.context.scene;camera=scene.camera;out=Path(r'C:/Users/codex/Downloads/byte engine files/Karlson V2/Previews');rigs=[o for o in scene.objects if o.type=='ARMATURE'];audit=[]
for rig in rigs:
 for other in rigs:
  for obj in other.children:obj.hide_render=other!=rig
 for track in rig.animation_data.nla_tracks:track.mute=True
 reload=next(t.strips[0].action for t in rig.animation_data.nla_tracks if t.name=='Reload')
 rig.animation_data.action=reload;scene.frame_set(46);bpy.context.view_layer.update()
 target=Vector((0,.02,-.1));camera.data.type='ORTHO';camera.location=target+Vector((1.4,1.5,.8));camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=.7 if rig.name.startswith('Pistol') else 1.25
 scene.render.filepath=str(out/(rig.name.split('_')[0]+'_Reload46.png'));bpy.ops.render.render(write_still=True)
 triangles=0
 for obj in rig.children:
  if obj.type!='MESH':continue
  obj.data.calc_loop_triangles();triangles+=len(obj.data.loop_triangles)
  assert all(len(v.groups)==1 and abs(v.groups[0].weight-1)<1e-6 for v in obj.data.vertices)
 audit.append({'name':rig.name,'triangles':triangles,'meshes':len(rig.children),'bones':list(rig.data.bones.keys()),'clips':[t.name for t in rig.animation_data.nla_tracks]})
 rig.animation_data.action=None
 for bone in rig.pose.bones:bone.location=(0,0,0);bone.rotation_quaternion=(1,0,0,0)
scene.frame_set(1)
for rig in rigs:
 for obj in rig.children:obj.hide_render=False
# Make source open on the current revision with rest poses.
bpy.ops.wm.save_as_mainfile(filepath=r'C:/Users/codex/Downloads/byte engine files/Karlson V2/Karlson_Guns_v2.blend')
(out.parent/'validation.json').write_text(json.dumps(audit,indent=2));result=audit
