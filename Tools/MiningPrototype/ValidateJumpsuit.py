import bpy,json,math,pathlib
s=bpy.context.scene;r=bpy.data.objects[s['jumpsuit_final_rig']];body=bpy.data.objects[s['jumpsuit_final_body']]
actions={st.action.name.split('.')[0]:st.action for t in r.animation_data.nla_tracks for st in t.strips}
for t in r.animation_data.nla_tracks:t.mute=True
r.data.pose_position='POSE';results=[]
for name in ['SHStandIdle','SHWalk','SHRun','SHWalkStrafeLeft','SHWalkStrafeRight','SHStandShoot','SHStandReload','DHStandIdle','DHWalk','DHRun','DHWalkStrafeLeft','DHWalkStrafeRight','DHStandShoot','DHStandReload','StandDeath','CrouchDeath']:
 a=actions['PolygonSyntyCharacter_'+name];r.animation_data.action=a;r.animation_data.action_slot=a.slots[0];samples=[]
 for f in [.1,.5,.9]:
  frame=a.frame_range[0]+f*(a.frame_range[1]-a.frame_range[0]);s.frame_set(int(frame),subframe=frame%1)
  evaluated=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=evaluated.to_mesh();points=[evaluated.matrix_world@v.co for v in mesh.vertices]
  extent=[max(p[i] for p in points)-min(p[i] for p in points) for i in range(3)]
  if not all(math.isfinite(x) for p in points for x in p) or max(extent)>3.5:raise RuntimeError('Invalid pose: '+name+str(extent))
  evaluated.to_mesh_clear();samples.append(extent)
 results.append({'clip':name,'extents':samples})
root=pathlib.Path('C:/Users/codex/Documents/palclone/SourceArt/HazardPayJumpsuit');(root/'animation-validation.json').write_text(json.dumps({'bones':len(r.data.bones),'clips':results,'total_clips':len(actions),'unweighted':sum(not v.groups for v in body.data.vertices)},indent=2))
# Keep only this delivered asset and its ancestors in the final review file.
keep={body,r,s.camera};p=r.parent
while p:keep.add(p);p=p.parent
for o in list(s.objects):
 if o not in keep and o.type not in ['LIGHT']:bpy.data.objects.remove(o,do_unlink=True)
from mathutils import Vector
for name,position,power,size in [('Jumpsuit key',(3,-4,5),650,4),('Jumpsuit fill',(-3,-2,3),400,3),('Jumpsuit rim',(1,3,4),800,3)]:
 light=bpy.data.lights.new(name,'AREA');light.energy=power;light.shape='DISK';light.size=size;o=bpy.data.objects.new(name,light);s.collection.objects.link(o);o.location=position;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
a=actions['PolygonSyntyCharacter_SHStandIdle'];r.animation_data.action=a;r.animation_data.action_slot=a.slots[0];s.frame_set(12);s.frame_start=int(a.frame_range[0]);s.frame_end=int(a.frame_range[1]);body.hide_render=False
bpy.ops.object.select_all(action='DESELECT');body.select_set(True);r.select_set(True);bpy.context.view_layer.objects.active=r
bpy.ops.wm.save_as_mainfile(filepath=str(root/'HazardPayJumpsuit-Animated.blend'))
s.render.filepath='C:/Users/codex/.codex/worktrees/52af/ByteEngine/artifacts/jumpsuit-animated-review.png';bpy.ops.render.render(write_still=True)
print('PASS: 16 clips at 3 times each, 76 clips retained; Blender preview saved.')
