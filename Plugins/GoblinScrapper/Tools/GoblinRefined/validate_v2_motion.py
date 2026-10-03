from pathlib import Path
import bpy,json,types,math
from mathutils import Vector
OUT=Path(r'C:\Users\codex\Downloads\parts\refined\standard-flight-v2')
bpy.ops.wm.open_mainfile(filepath=str(OUT/'Goblin_Parts_Redesign.blend'));SCENE=bpy.context.scene
manifest=json.loads((OUT/'parts-catalog.json').read_text());ALL=[]
for item in manifest['parts']:
 rig=bpy.data.objects[Path(item['file']).stem+'_rig'];col=rig.users_collection[0];ALL.append(types.SimpleNamespace(file=Path(item['file']).stem,rig=rig,col=col,objects=[o for o in col.objects if o.type=='MESH']))
def world_bounds(p):
 coords=[o.matrix_world@Vector(v) for o in p.objects for v in o.bound_box];return [min(v[i] for v in coords) for i in range(3)],[max(v[i] for v in coords) for i in range(3)]
lib=Path(__file__).with_name('build_refined.py').read_text();a=lib.index('# Studio for real geometry thumbnails');b=lib.index('for p in ALL:',a);exec(compile(lib[a:b],'motion_studio','exec'),globals())
SCENE.render.resolution_x=384;SCENE.render.resolution_y=288;SCENE.render.film_transparent=True;ground.hide_render=True
(OUT/'motion-checks').mkdir(exist_ok=True);records=[]
for file in ['goblin_spring','goblin_suspension','goblin_piston','goblin_slider','goblin_decoupler','goblin_metal_jaw','goblin_parachute','goblin_drill','goblin_hinge','goblin_steering_hinge','goblin_ball_joint','goblin_small_wheel','goblin_skate_wheel','goblin_fly_wheel','goblin_circular_saw','goblin_rope_winch','goblin_rope_measure','goblin_timer','goblin_speedometer','goblin_anglometer','goblin_grabber','goblin_steering_block']:
 p=next(p for p in ALL if p.file==file);p.col.hide_viewport=False;frame_part(p);camera.data.ortho_scale*=1.35
 action=next(t.strips[0].action for t in p.rig.animation_data.nla_tracks);p.rig.animation_data.action=action
 samples=[]
 for frame in [1,16,31,46,61]:
  SCENE.frame_set(frame);bpy.context.view_layer.update();r=p.rig
  def moved(bone,point):return r.pose.bones[bone].matrix@r.data.bones[bone].matrix_local.inverted()@Vector(point)
  centers={'goblin_hinge':(0,.20,0),'goblin_steering_hinge':(0,.20,0),'goblin_ball_joint':(0,.10,0),'goblin_small_wheel':(0,.18,0),'goblin_skate_wheel':(0,.18,0),'goblin_fly_wheel':(0,.16,0),'goblin_circular_saw':(0,.15,0),'goblin_rope_winch':(0,.07,.18),'goblin_rope_measure':(0,.07,.18),'goblin_timer':(0,.23,.17),'goblin_speedometer':(0,.23,.17),'goblin_anglometer':(0,.18,.18),'goblin_grabber':(.11,.13,0),'goblin_metal_jaw':(.07,-.08,0),'goblin_steering_block':(0,.22,0)}
  if file in centers and (moved('Moving',centers[file])-Vector(centers[file])).length>.0001:raise RuntimeError(file+' pivot drifts from mechanism center')
  if file in ['goblin_spring','goblin_suspension']:
   gap=(moved('Coil',(0,.85,0))-moved('Moving',(0,.85,0))).length
   if gap>.0001:raise RuntimeError(file+' coil/output mismatch '+str(gap))
  if file=='goblin_piston' and moved('Moving',(0,.03,0)).y>.4401:raise RuntimeError('Piston shaft leaves cylinder')
  samples.append({'frame':frame,'bones':{b.name:{'location':list(b.location),'scale':list(b.scale)} for b in r.pose.bones}})
  if frame in [1,31,61]:SCENE.render.filepath=str(OUT/'motion-checks'/f'{file}_{frame:02d}.png');bpy.ops.render.render(write_still=True)
 records.append({'part':file,'clip':action.name,'samples':samples,'verified':True});p.rig.animation_data.action=None
 for pb in p.rig.pose.bones:pb.location=(0,0,0);pb.scale=(1,1,1);pb.rotation_quaternion=(1,0,0,0)
print('MOTION_CHECKS_PASS',len(records),flush=True)
(OUT/'animation-validation.json').write_text(json.dumps(records,indent=2))
