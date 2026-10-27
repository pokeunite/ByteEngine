"""User revision: massive rig, a much larger hopper, and a long thick auger."""
import bpy,math
from pathlib import Path
from mathutils import Vector
BASE=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine');__file__=str(BASE/'Tools/MiningPrototype/build_machines.py')
exec(compile(Path(__file__).read_text().split('def fittings')[0],__file__,'exec'))
_finish=finish
def finish(o,*args,**kwargs):
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);return _finish(o,*args,**kwargs)
OUT=BASE/'artifacts/reference-drill';scene=bpy.data.scenes['Drill Reference'];bpy.context.window.scene=scene;scene.frame_set(1);collection=bpy.data.collections['Reference Mining Drill'];bpy.context.view_layer.active_layer_collection=bpy.context.view_layer.layer_collection.children[collection.name];rig=bpy.data.objects['MiningDrillRig']
mats={k:bpy.data.materials.get('Drill '+k)for k in palette};parts=[]
hopper_prefix=('Open ore hopper','Hopper rolled rim','Hopper recessed throat','Hopper discharge neck','Hopper skid','Hopper support foot','Ore hopper status bezel','Ore hopper cyan strip','Hopper angular ore','Hopper upper bearer','Hopper transverse bearer')
head_prefix=('Rotary drive head collar','Lower drive clamp','Head flange clamp','Drive head cyan','Top service cap','Top cap lifting handle')
ore_prefix=('Ore separator housing','Separator outlet shadow','Ore port jamb','Ore port lintel','Ore chute','Chute rolled edge','Chute gate')
remove_prefix=('Exposed central auger shaft','Continuous four-turn helical auger','Auger cutting tip')
# Flag the hopper bolts before enlarging anything, avoiding unrelated chassis bolts.
hopper_bolts=set()
for o in collection.objects:
 if o.type=='MESH' and o.name.startswith('Recessed hex fastener'):
  c=sum((v.co for v in o.data.vertices),Vector())/len(o.data.vertices)
  if c.x>2.8 and abs(c.z-.24)<.01:hopper_bolts.add(o.name)
for o in list(collection.objects):
 if o.type!='MESH':continue
 if o.name.startswith('Presentation ') or o.name.startswith(remove_prefix):bpy.data.objects.remove(o,do_unlink=True);continue
 for v in o.data.vertices:v.co*=2
 movable=bool(o.vertex_groups.get('Carriage') or o.vertex_groups.get('MotorFan'))
 if movable:
  for v in o.data.vertices:v.co.z+=2
 if o.name.startswith('Polished hydraulic piston'):
  for v in o.data.vertices:v.co.z=2.65+(v.co.z-4.56)/(6.60-4.56)*(6.60-2.65)
 if o.name.startswith('Cylinder guide rail'):
  for v in o.data.vertices:v.co.z=2.46+(v.co.z-2.46)/(4.66-2.46)*(6.66-2.46)
 if o.name.startswith('Hydraulic gland'):
  centre=sum((v.co.z for v in o.data.vertices))/len(o.data.vertices)
  if centre>3:
   for v in o.data.vertices:v.co.z+=2
 if o.name.startswith(('Motor power conduit','Return hydraulic line','Hydraulic hose connection block')):
  for v in o.data.vertices:v.co.z+=max(0,min(2,v.co.z-2))
 if o.name.startswith('Hydraulic hose union'):
  centre=sum(v.co.z for v in o.data.vertices)/len(o.data.vertices)
  if centre>4:
   for v in o.data.vertices:v.co.z+=2
 if o.name.startswith(head_prefix):
  for v in o.data.vertices:v.co.x*=1.25;v.co.y*=1.25
 if o.vertex_groups.get('Auger'):
  # Remaining auger-group objects are the upper coupling and its fasteners.
  for v in o.data.vertices:v.co.z+=2;v.co.x*=1.25;v.co.y*=1.25
 if o.name.startswith(ore_prefix):
  for v in o.data.vertices:v.co.z+=2
 if o.name.startswith(hopper_prefix) or o.name in hopper_bolts:
  for v in o.data.vertices:
   v.co.x=8.30+(v.co.x-7.0)*(4.60/2.84)
   v.co.y=1.30+(v.co.y-1.30)*(3.80/2.48)
   v.co.z*=3.60/2.30
# Resize and raise the mechanical bind hierarchy to match the enlarged meshes.
bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
for bone in rig.data.edit_bones:bone.head*=2;bone.tail*=2
for name in ['Carriage','Auger','MotorFan','FeedGate']:
 bone=rig.data.edit_bones[name];bone.head.z+=2;bone.tail.z+=2
bpy.ops.object.mode_set(mode='OBJECT')
action=rig.animation_data.action
for layer in action.layers:
 for strip in layer.strips:
  for bag in getattr(strip,'channelbags',[]):
   for curve in bag.fcurves:
    if curve.data_path=='pose.bones["Carriage"].location':
     for key in curve.keyframe_points:key.co.y*=2;key.handle_left.y*=2;key.handle_right.y*=2
# A twelve-metre central shaft and nearly twelve metres of solid helical flight.
shaft_bottom=-6.40;shaft_top=6.12;radius=.50
cyl('Massive auger central shaft',(0,0,(shaft_bottom+shaft_top)/2),radius,shaft_top-shaft_bottom,'ingot','Auger',vertices=20)
v=[];steps=180;turns=4.1;z0=-6.0;height=11.70;thickness=.095
for i in range(steps+1):
 a=i/steps*math.tau*turns;z=z0+height*i/steps
 for r,dz in [(radius,thickness),(1.45,thickness),(radius,-thickness),(1.45,-thickness)]:v.append((math.cos(a)*r,math.sin(a)*r,z+dz))
faces=[]
for i in range(steps):
 j=i*4;k=j+4;faces.extend([(j,j+1,k+1,k),(j+2,k+2,k+3,j+3),(j+1,j+3,k+3,k+1),(j,j+2,k+2,k)])
faces.extend([(0,2,3,1),(steps*4,steps*4+1,steps*4+3,steps*4+2)])
mesh('Massive continuous helical auger',v,faces,'ingot','Auger')
bpy.ops.mesh.primitive_cone_add(vertices=16,radius1=.06,radius2=.50,depth=.65,location=(0,0,-6.42));finish(bpy.context.object,'Massive auger cutting nose','steel','Auger')
# Heavy intermediate support under the now-raised ore separator.
box('Separator load-bearing column',(3.54,1.30,3.20),(1.10,1.18,4.10),'dark',bevel=.10)
for x in [3.10,3.98]:box('Separator support armour',(x,1.30,3.42),(.15,1.28,3.42),'yellow',bevel=.04)
for o in parts:mod=o.modifiers.new('Rigid mechanical part','ARMATURE');mod.object=rig;o.parent=rig
# Cutaway presentation: enough room around the thicker drill and a full-depth pit.
earth=bpy.data.materials.get('Drill earth');ground=bpy.data.materials.get('Drill studio ground')
v=[];n=36
for i in range(n+1):
 a=math.radians(-45+i*270/n)
 for r,z in [(12,-.05),(2.8,-.05),(2.0,-.65),(1.8,-6.85)]:v.append((math.cos(a)*r,math.sin(a)*r,z))
faces=[]
for i in range(n):
 j=i*4;faces.extend([(j,j+4,j+5,j+1),(j+1,j+5,j+6,j+2),(j+2,j+6,j+7,j+3)])
data=bpy.data.meshes.new('Full-depth excavation cutaway');data.from_pydata(v,[],faces);data.materials.append(ground);data.materials.append(earth)
for i,p in enumerate(data.polygons):p.material_index=0 if i%3==0 else 1
o=bpy.data.objects.new('Presentation massive drill excavation',data);collection.objects.link(o)
# Camera comfortably includes the entire long bit below the ground line.
scene.camera.location=(17.5,-25,14);scene.camera.rotation_euler=(Vector((1.2,.1,1.15))-scene.camera.location).to_track_quat('-Z','Y').to_euler();scene.camera.data.ortho_scale=22.2
for o in collection.objects:
 if o.type=='LIGHT':o.location*=2;o.data.energy*=4;o.data.size*=2
scene.render.filepath=str(OUT/'MiningDrill-preview.png');scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'MiningDrill.blend'))
result={'auger_diameter_m':2.90,'shaft_length_m':12.52,'flight_length_m':11.70,'hopper_mouth_m':[4.60,3.80],'hopper_height_m':3.60,'machine_height_m':9.0,'chute_lip_x_m':6.4,'hopper_near_edge_x_m':6.0,'chute_overhang_m':.4}
