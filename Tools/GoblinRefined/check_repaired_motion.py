import bpy,sys,json,math
from pathlib import Path
from mathutils import Vector
out=Path(sys.argv[sys.argv.index('--')+1]);bpy.ops.wm.open_mainfile(filepath=str(out/'GoblinScraper_RefinedParts.blend'));scene=bpy.context.scene;results=[]
for file,bone,axis,turns in [('scrap_auger_drill','Drill',(0,1,0),2),('scrap_engine_block','Flywheel',(1,0,0),2),('scrap_engine_block','Fan',(0,1,0),3)]:
 rig=bpy.data.objects[file+'_rig'];
 for t in rig.animation_data.nla_tracks:t.mute=True
 rig.animation_data.action=next(a for a in bpy.data.actions if a.name.startswith(file+'__') and (a.name.endswith('Drill') or a.name.endswith('EngineIdle')))
 p=rig.pose.bones[bone];rest=p.bone.matrix_local.to_quaternion();prev=None;total=0
 for f in range(1,62):
  scene.frame_set(f);bpy.context.view_layer.update();q=rest@p.rotation_quaternion@rest.inverted()
  if prev:
   dq=q@prev.inverted()
   if dq.w<0:dq.negate()
   angle=2*math.atan2(Vector((dq.x,dq.y,dq.z)).dot(Vector(axis)),dq.w);assert angle>0,(file,f,angle);total+=angle
  prev=q.copy()
 assert abs(total-math.tau*turns)<1e-4,total
 results.append({'part':file,'bone':bone,'one_direction_rotation_verified_frames':61,'turns':total/math.tau})
rig=bpy.data.objects['scrap_battering_fist_rig']
for t in rig.animation_data.nla_tracks:t.mute=True
rig.animation_data.action=bpy.data.actions['scrap_battering_fist__Punch'];minimum=1
for f in range(1,32):
 scene.frame_set(f);bpy.context.view_layer.update();p=rig.pose.bones['Fist'];d=p.bone.matrix_local.to_quaternion()@p.location;overlap=.25-(.005+d.y);minimum=min(minimum,overlap);assert overlap>=.05,overlap
results.append({'part':'scrap_battering_fist','minimum_guide_overlap_m':minimum,'frames_checked':31})
(out/'mechanical-motion-checks.json').write_text(json.dumps(results,indent=2));print('DIRECTION_AND_OVERLAP_CHECKS_PASSED',results)

