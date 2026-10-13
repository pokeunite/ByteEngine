import bpy,json,math
from pathlib import Path
from mathutils import Vector
scene=bpy.context.scene;camera=scene.camera;out=Path(r'C:/Users/codex/Downloads/byte engine files/Karlson V2/Previews');rigs=[o for o in scene.objects if o.type=='ARMATURE']
jobs=[]
for rig in rigs:
 for label,clip,frame in [('side',None,1),('top',None,1),('front',None,1),('Reload35','Reload',35),('Reload70','Reload',70),('Reload85','Reload',85),('Reload96','Reload',96)]:jobs.append((rig.name,label,clip,frame))
# Rendering runs on Blender's own timer, avoiding the bridge's 30 second response deadline.
bpy.app.driver_namespace['karlson_review']={'remaining':len(jobs),'complete':False,'error':None}
def render_next():
 try:
  if not jobs:
   for r in rigs:
    r.animation_data.action=None
    for b in r.pose.bones:b.location=(0,0,0);b.rotation_quaternion=(1,0,0,0)
   scene.frame_set(1);bpy.app.driver_namespace['karlson_review']['complete']=True;return None
  name,label,clip,frame=jobs.pop(0);rig=bpy.data.objects[name]
  for r in rigs:
   for o in r.children:o.hide_render=r!=rig
  rig.animation_data.action=None
  for b in rig.pose.bones:b.location=(0,0,0);b.rotation_quaternion=(1,0,0,0)
  for t in rig.animation_data.nla_tracks:t.mute=True
  if clip:rig.animation_data.action=next(t.strips[0].action for t in rig.animation_data.nla_tracks if t.name==clip)
  scene.frame_set(frame);bpy.context.view_layer.update();verts=[]
  for o in rig.children:
   if o.type=='MESH':
    evaluated=o.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=evaluated.to_mesh();verts.extend([o.matrix_world@v.co for v in mesh.vertices]);evaluated.to_mesh_clear()
  low=Vector([min(v[i] for v in verts) for i in range(3)]);high=Vector([max(v[i] for v in verts) for i in range(3)]);target=(low+high)/2
  direction={'side':Vector((2,0,0)),'top':Vector((.001,0,2)),'front':Vector((0,2,0))}.get(label,Vector((1.4,1.5,.8)))
  camera.data.type='ORTHO';camera.location=target+direction;camera.rotation_euler=(-direction).to_track_quat('-Z','Y').to_euler();bpy.context.view_layer.update();projected=[camera.matrix_world.inverted()@v for v in verts]
  camera.data.ortho_scale=max(max(v.x for v in projected)-min(v.x for v in projected),(max(v.y for v in projected)-min(v.y for v in projected))*1100/720)*1.15
  scene.render.filepath=str(out/(name.split('_')[0]+'_'+label+'.png'));bpy.ops.render.render(write_still=True)
  bpy.app.driver_namespace['karlson_review']['remaining']=len(jobs);return .15
 except Exception as e:bpy.app.driver_namespace['karlson_review']['error']=str(e);return None
bpy.app.timers.register(render_next,first_interval=.5);result='Review render queue scheduled'
