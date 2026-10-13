import bpy
C=bpy.app.driver_namespace['cyber'];scene=bpy.context.scene;a=bpy.app.driver_namespace
# Render rack/insert/withdrawal poses; source camera direction stays fixed in model space.
from mathutils import Vector
jobs=[]
for g in C['guns']:
 for clip,frame in [('Fire',3),('Reload',39),('Reload',92),('Reload',119),('ReloadEmpty',150 if g.name!='Shotgun' else 65)]:jobs.append((g.name,clip,frame))
status={'remaining':len(jobs),'complete':False,'error':None};a['cyber_motion_review']=status
out=C['OUT']/'Previews';camera=scene.camera

def run():
 try:
  if not jobs:status['complete']=True;return None
  name,clip,frame=jobs.pop(0);g=next(g for g in C['guns'] if g.name==name)
  for other in C['guns']:
   for obj in other.meshes:obj.hide_render=other!=g
  g.rest();g.rig.animation_data.action=g.actions[clip];scene.frame_set(frame);bpy.context.view_layer.update();verts=[]
  for obj in g.meshes:
   evaluated=obj.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=evaluated.to_mesh();verts.extend([obj.matrix_world@v.co for v in mesh.vertices]);evaluated.to_mesh_clear()
  lo=Vector([min(v[i] for v in verts) for i in range(3)]);hi=Vector([max(v[i] for v in verts) for i in range(3)]);target=(lo+hi)/2;direction=Vector((1.3,1.5,.75));camera.data.type='ORTHO';camera.location=target+direction;camera.rotation_euler=(-direction).to_track_quat('-Z','Y').to_euler();scene.render.resolution_x=1280;scene.render.resolution_y=900;bpy.context.view_layer.update();proj=[camera.matrix_world.inverted()@v for v in verts];camera.data.ortho_scale=max(max(v.x for v in proj)-min(v.x for v in proj),(max(v.y for v in proj)-min(v.y for v in proj))*1280/900)*1.10
  scene.render.filepath=str(out/(name+'_'+clip+'_'+str(frame)+'.png'));bpy.ops.render.render(write_still=True);status['remaining']=len(jobs);return .15
 except Exception as e:status['error']=str(e);return None
bpy.app.timers.register(run,first_interval=.5);result='15 fire, withdrawal, insertion and empty-reload poses queued'
