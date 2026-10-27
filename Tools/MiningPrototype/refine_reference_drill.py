import bpy,math
from pathlib import Path
from mathutils import Vector
BASE=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine');__file__=str(BASE/'Tools/MiningPrototype/build_machines.py')
exec(compile(Path(__file__).read_text().split('def fittings')[0],__file__,'exec'))
_finish=finish
def finish(o,*args,**kwargs):
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);return _finish(o,*args,**kwargs)
OUT=BASE/'artifacts/reference-drill';scene=bpy.data.scenes['Drill Reference'];bpy.context.window.scene=scene;scene.frame_set(1)
collection=bpy.data.collections['Reference Mining Drill'];bpy.context.view_layer.active_layer_collection=bpy.context.view_layer.layer_collection.children[collection.name];rig=bpy.data.objects['MiningDrillRig'];parts=[]
for o in list(collection.objects):
 if o.name.startswith('Base outer yellow armour'):bpy.data.objects.remove(o,do_unlink=True)
mats={k:bpy.data.materials.get('Drill '+k)for k in palette}
# Stronger metal/paint separation brings the reference's heavy industrial feel back.
for name,color,metal,rough in [('yellow',(.55,.235,.012,1),.24,.42),('steel',(.085,.105,.13,1),.60,.38),('ingot',(.30,.33,.37,1),.8,.31),('dark',(.018,.024,.032,1),.48,.46)]:
 m=bpy.data.materials['Drill '+name];m.diffuse_color=color;p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=color;p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
# Substantial side armour, instead of a thin yellow plate on a dark ring.
for i in range(9):
 a=math.radians(-45+i*30)+.02;b=a+math.radians(30)-.04
 v=[(math.cos(t)*r,math.sin(t)*r,z)for z in [.41,.73]for r,t in [(2.18,a),(2.27,a),(2.27,b),(2.18,b)]]
 mesh('Base outer yellow armour',v,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],'yellow')
 mid=(a+b)/2
 # Fasteners are deliberately broad enough to read at gameplay distance.
 for z in [.48,.66]:
  x=math.cos(mid)*2.30;y=math.sin(mid)*2.30
  bpy.ops.mesh.primitive_cylinder_add(vertices=6,radius=.037,depth=.045,location=(x,y,z));o=bpy.context.object;o.rotation_euler=(Vector((x,y,0))).to_track_quat('Z','Y').to_euler();finish(o,'Outer armour fastener','steel')
# Mirrored front feet, braced all the way into the machine chassis.
for o in collection.objects:
 if o.type=='MESH' and o.name.startswith('Yellow stabilizer cheek'):
  centre=sum((v.co for v in o.data.vertices),Vector())/len(o.data.vertices)
  if centre.x<0:
   for v in o.data.vertices:v.co.x=2*(-1.89)-v.co.x
for sx in [-1,1]:
 beam('Stabilizer load brace',(sx*1.65,-1.08,.73),(sx*2.12,-1.68,.27),.12,'steel')
 box('Load brace mounting block',(sx*1.65,-1.10,.68),(.35,.30,.29),'dark',bevel=.035)
 # Hopper legs now have real continuous upper bearers.
for x in [2.94,4.06]:box('Hopper upper bearer',(x,.65,.44),(.14,1.12,.12),'steel',bevel=.018)
for y in [.18,1.12]:box('Hopper transverse bearer',(3.50,y,.44),(1.26,.14,.12),'steel',bevel=.018)
box('Hydraulic hose connection block',(1.73,-.25,1.73),(.20,.15,.21),'steel',bevel=.025)
for o in parts:
 mod=o.modifiers.new('Rigid mechanical part','ARMATURE');mod.object=rig;o.parent=rig
# Studio floor with an actual central opening; inspection dirt remains below it.
floor=bpy.data.materials.new('Drill studio ground');floor.diffuse_color=(.16,.17,.18,1);floor.use_nodes=True;p=floor.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=floor.diffuse_color;p.inputs['Roughness'].default_value=.88
v=[];n=96
for i in range(n):
 a=i*math.tau/n
 for r in [1.12,12]:v.append((math.cos(a)*r,math.sin(a)*r,-.025))
faces=[]
for i in range(n):j=(i+1)%n;faces.append((i*2,i*2+1,j*2+1,j*2))
data=bpy.data.meshes.new('Studio floor with drill opening');data.from_pydata(v,[],faces);data.materials.append(floor);o=bpy.data.objects.new('Presentation studio floor',data);collection.objects.link(o)
# A slightly lower front view emphasizes the squat wide body and keeps the motor visible.
scene.camera.location=(8.1,-11.6,6.0);scene.camera.rotation_euler=(Vector((.55,.05,1.40))-scene.camera.location).to_track_quat('-Z','Y').to_euler();scene.camera.data.ortho_scale=8.9
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'MiningDrill.blend'))
result={'corrections':'thicker armour, supported hopper, braced mirrored feet, connected hydraulic hose, stronger materials, ground opening'}
