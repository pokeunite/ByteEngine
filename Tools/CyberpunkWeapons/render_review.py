import bpy,math
from mathutils import Vector
C=bpy.app.driver_namespace['cyber'];scene=bpy.context.scene;camera=scene.camera;out=C['OUT']/'Previews';out.mkdir(exist_ok=True)
jobs=[]
for g in C['guns']:
 for view in ['side','three-quarter','fps']:jobs.append((g.name,view,None,1))
bpy.app.driver_namespace['cyber_review']={'remaining':len(jobs),'complete':False,'error':None}
def run():
 try:
  if not jobs:bpy.app.driver_namespace['cyber_review']['complete']=True;return None
  name,view,clip,frame=jobs.pop(0);g=next(x for x in C['guns'] if x.name==name)
  for other in C['guns']:
   for obj in other.meshes:obj.hide_render=other!=g
  g.rest();g.rig.location=(0,0,0)
  if clip:g.rig.animation_data.action=g.actions[clip]
  scene.frame_set(frame);bpy.context.view_layer.update();verts=[]
  for obj in g.meshes:
   evaluated=obj.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=evaluated.to_mesh();verts.extend([obj.matrix_world@v.co for v in mesh.vertices]);evaluated.to_mesh_clear()
  if view=='fps':
   camera.data.type='PERSP';camera.data.sensor_fit='VERTICAL';camera.data.sensor_height=24;camera.data.lens=15.64;camera.data.clip_start=.015;camera.location=(-.18,-.49,.20) if name=='Pistol' else (-.20,-.68,.22);camera.rotation_euler=Vector((0,1,0)).to_track_quat('-Z','Y').to_euler();scene.render.resolution_x=1280;scene.render.resolution_y=720
  else:
   scene.render.resolution_x=1280;scene.render.resolution_y=900;lo=Vector([min(v[i] for v in verts) for i in range(3)]);hi=Vector([max(v[i] for v in verts) for i in range(3)]);target=(lo+hi)/2;direction=Vector((-2,0,.04)) if view=='side' else Vector((1.3,1.5,.85));camera.data.type='ORTHO';camera.location=target+direction;camera.rotation_euler=(-direction).to_track_quat('-Z','Y').to_euler();bpy.context.view_layer.update();projected=[camera.matrix_world.inverted()@v for v in verts];camera.data.ortho_scale=max(max(v.x for v in projected)-min(v.x for v in projected),(max(v.y for v in projected)-min(v.y for v in projected))*1280/900)*1.12
  scene.render.filepath=str(out/(name+'_'+view+'.png'));bpy.ops.render.render(write_still=True);bpy.app.driver_namespace['cyber_review']['remaining']=len(jobs);return .15
 except Exception as e:bpy.app.driver_namespace['cyber_review']['error']=str(e);return None
bpy.app.timers.register(run,first_interval=.5);result='Nine reference and FPS views queued'
