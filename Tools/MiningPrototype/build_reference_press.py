"""Run through Blender MCP. Editable rigid-part model matching approved press concept."""
import bpy,math,json
from pathlib import Path
from mathutils import Vector
BASE=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine')
__file__=str(BASE/'Tools/MiningPrototype/build_machines.py')
exec(compile((BASE/'Tools/MiningPrototype/build_machines.py').read_text().split('def fittings')[0],str(BASE/'Tools/MiningPrototype/build_machines.py'),'exec'))
OUT=BASE/'artifacts/reference-press';OUT.mkdir(parents=True,exist_ok=True)
# Preserve the user's camera/light and any unrelated collections.
old=bpy.data.collections.get('Reference Ammo Press')
if old:
 for o in list(old.objects):bpy.data.objects.remove(o,do_unlink=True)
 bpy.data.collections.remove(old)
collection=bpy.data.collections.new('Reference Ammo Press');bpy.context.scene.collection.children.link(collection)
bpy.context.view_layer.active_layer_collection=bpy.context.view_layer.layer_collection.children[collection.name]
palette.update(yellow=(.78,.43,.035,1),steel=(.22,.26,.30,1),dark=(.045,.057,.07,1),ingot=(.48,.51,.54,1),black=(.025,.03,.035,1))
mats={k:material('Press '+k,v)for k,v in palette.items()};parts=[];spec={}
def bolt(p,axis='Y',r=.035):return cyl('Hex fastener',p,r,.035,'steel',axis=axis,vertices=6)
# Squat pedestal and splayed feet.
box('Pedestal frame',(0,.1,.53),(1.46,1.16,.9),'dark',bevel=.045)
box('Front service cover',(0,-.505,.55),(1.08,.06,.58),'yellow',bevel=.035)
for x in [-.48,.48]:
 for z in [.32,.78]:bolt((x,-.55,z))
for x in [-.67,.67]:
 for y in [-.43,.61]:
  box('Rubber foot',(x,y,.065),(.44,.45,.13),'dark')
  # Tapered support foot, wider at ground.
  v=[(x+a*w,y+b*w,z)for z,w in [(.13,.20),(.42,.13)]for a,b in [(-1,-1),(1,-1),(1,1),(-1,1)]]
  mesh('Cast foot gusset',v,[(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],'steel')
box('Pedestal top',(0,.1,1.02),(1.55,1.23,.16),'dark')
for x in [-.7,.7]:
 for y in [-.5,.65]:bolt((x,y,1.12),'Z',.045)
box('Rear press column',(0,.46,1.98),(1.12,.34,1.88),'yellow',bevel=.06)
for x in [-.52,.52]:
 box('Lower guide casting',(x,-.06,1.36),(.29,.5,.62),'yellow',bevel=.045)
 cyl('Polished guide',(x,-.06,2.03),.082,1.23,'ingot')
 for z in [1.64,2.51]:cyl('Guide bearing collar',(x,-.06,z),.115,.10,'dark')
 box('Upper guide casting',(x,-.02,2.72),(.34,.57,.48),'yellow',bevel=.05)
 cyl('Column crown nut',(x,-.06,3.01),.118,.15,'dark',vertices=8)
box('Upper crosshead',(0,.06,2.73),(1.24,.58,.42),'yellow',bevel=.075)
cyl('Ram polished shaft',(0,-.06,2.30),.15,.64,'ingot','Ram')
box('Moving ram die',(0,-.08,2.01),(.50,.43,.24),'ingot','Ram',.025)
box('Fixed die bed',(0,-.12,1.56),(.76,.60,.12),'ingot',bevel=.018)
box('Die insert',(0,-.14,1.635),(.36,.35,.035),'dark',bevel=.01)
spec['Ram']=((0,-.06,2.3),(0,-.06,2.6))
# Octagonal flywheel assembly at the left of the press.
cyl('Flywheel rear shroud',(-.85,.12,2.05),.64,.20,'dark',axis='X',vertices=10)
cyl('Octagonal yellow flywheel',(-1.02,.12,2.05),.61,.22,'yellow','Flywheel','X',8)
cyl('Flywheel hub',(-1.16,.12,2.05),.23,.12,'dark','Flywheel','X',10)
cyl('Hub end cap',(-1.23,.12,2.05),.17,.04,'steel','Flywheel','X',10)
for a in [0,math.pi]:
 box('Flywheel inset spoke',(-1.145,.12+math.cos(a)*.36,2.05+math.sin(a)*.36),(.016,.17,.065),'steel','Flywheel',.008)
spec['Flywheel']=((-.9,.12,2.05),(-.6,.12,2.05))
box('Hopper feed neck',(-.30,.36,2.97),(.47,.50,.35),'dark')
hopper((-.30,.36,3.10),(1.17,1.03,.57))
box('Control cabinet',(.85,.04,2.48),(.32,.32,.61),'yellow',bevel=.045)
cyl('Control lamp bezel',(.85,-.14,2.63),.07,.035,'dark',axis='Y')
cyl('Cyan operation lamp',(.85,-.164,2.63),.052,.016,'cyan',axis='Y')
box('Power switch',(.85,-.15,2.41),(.105,.055,.14),'dark',bevel=.01)
for a,b in [((.85,.05,2.15),(.85,.05,1.64)),((.85,.05,1.64),(.64,.05,1.42)),((-.95,.1,1.44),(-.95,.1,1.17)),((-.95,.1,1.17),(-.67,.1,1.10))]:beam('Heavy power conduit',a,b,.045,'dark')
# Conveyor top runs toward -Y; twenty-four individually skinned slats circulate.
front=-2.30;rear=-.30;zc=1.28;radius=.13;length=rear-front
for x in [-.48,.48]:
 box('Conveyor side beam',(x,(rear+front)/2,zc),(.12,length+.15,.22),'steel',bevel=.025)
 box('Raised belt guide',(x,(rear+front)/2,zc+.16),(.055,length+.04,.10),'ingot',bevel=.015)
 for y in [-.6,-1.5,-2.22]:
  box('Conveyor clamp',(x,y,zc+.09),(.16,.105,.29),'dark',bevel=.02)
for y,key in [(rear,'RearRoller'),(front,'FrontRoller')]:
 cyl('Belt end roller',(0,y,zc),radius,.99,'dark',key,'X',12)
 spec[key]=((0,y,zc),(.3,y,zc))
 for x in [-.55,.55]:
  cyl('Roller bearing',(x,y,zc),.15,.07,'ingot',axis='X',vertices=8)
  cyl('Bearing centre',(x*1.08,y,zc),.09,.045,'steel',axis='X',vertices=8)
for x in [-.48,.48]:
 box('Conveyor support leg',(x,-1.98,.64),(.13,.15,1.18),'dark',bevel=.02)
 box('Conveyor support shoe',(x,-1.98,.08),(.32,.34,.14),'steel')
box('Motor gearbox',(.67,-1.10,1.08),(.29,.40,.32),'yellow')
cyl('Conveyor drive motor',(.87,-1.10,1.08),.16,.32,'dark',axis='X',vertices=10)
for x in [.76,.84,.92]:cyl('Motor cooling rib',(x,-1.10,1.08),.175,.035,'steel',axis='X',vertices=10)
beam('Motor cable',(.68,-1.18,.95),(.65,-1.32,.57),.035,'dark');beam('Cable return',(.65,-1.32,.57),(.6,-.28,.48),.035,'dark')
perimeter=2*length+math.tau*radius
def belt(s):
 s%=perimeter
 if s<length:return (rear-s,zc+radius,0)
 s-=length
 if s<math.pi*radius:
  a=s/radius;return(front-radius*math.sin(a),zc+radius*math.cos(a),-a)
 s-=math.pi*radius
 if s<length:return(front+s,zc-radius,-math.pi)
 a=(s-length)/radius;return(rear+radius*math.sin(a),zc-radius*math.cos(a),-math.pi-a)
for i in range(28):
 y,z,a=belt(i*perimeter/28);key='BeltSlat%02d'%i
 o=box('Conveyor slat %02d'%i,(0,y,z),(.83,perimeter/28*.91,.035),'black',key,.009)
 # Base slats need the tangent angle baked in; bones carry relative motion.
 if a:
  # Geometry was baked by finish; rotate it about its own centre.
  from mathutils import Matrix
  for v in o.data.vertices:v.co=Vector((0,y,z))+Matrix.Rotation(a,4,'X')@(v.co-Vector((0,y,z)))
 spec[key]=((0,y,z),(0,y,z+.1))
# Open tray: actual hollow space, broad rim, weighted floor marks runtime pile anchor.
tray_y=-2.90
box('Collection tray floor',(0,tray_y,.13),(1.35,1.02,.12),'dark','BucketFloor',.025)
spec['BucketFloor']=((0,tray_y,.21),(0,tray_y,.31))
for x in [-.67,.67]:box('Tray side wall',(x,tray_y,.38),(.065,1.07,.49),'dark',bevel=.02);box('Tray side rim',(x,tray_y,.64),(.09,1.13,.065),'steel',bevel=.018)
for y in [tray_y-.51,tray_y+.51]:box('Tray end wall',(0,y,.38),(1.35,.065,.49),'dark',bevel=.02);box('Tray end rim',(0,y,.64),(1.40,.095,.065),'steel',bevel=.018)
for x in [-.67,.67]:
 for y in [tray_y-.51,tray_y+.51]:box('Tray corner guard',(x,y,.34),(.10,.10,.61),'steel',bevel=.015)
for key,p in [('BeltStart',(0,-.43,1.45)),('BeltEnd',(0,-2.32,1.45))]:
 spec[key]=(p,(p[0],p[1],p[2]+.1));box(key+' marker bracket',(0,p[1],1.19),(.18,.055,.04),'dark',key,.005)
# Keep every mechanical piece editable in the blend. GLB uses rigid bone weights.
arm=bpy.data.armatures.new('AmmoPressRig');rig=bpy.data.objects.new('AmmoPressRig',arm);collection.objects.link(rig)
bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
root=arm.edit_bones.new('Root');root.head=(0,0,0);root.tail=(0,0,.25)
for key,(a,b) in spec.items():
 bone=arm.edit_bones.new(key);bone.head=a;bone.tail=b;bone.parent=root
bpy.ops.object.mode_set(mode='OBJECT')
for o in parts:
 mod=o.modifiers.new('Rigid mechanical part','ARMATURE');mod.object=rig;o.parent=rig
rig.animation_data_create();action=bpy.data.actions.new('Operate');rig.animation_data.action=action
scene=bpy.context.scene;scene.render.fps=30;scene.frame_start=1;scene.frame_end=91
for key in spec:
 pose=rig.pose.bones[key];pose.rotation_mode='XYZ';rest=Vector(spec[key][0])
 for f in range(1,92):
  t=(f-1)/90;pose.location=(0,0,0);pose.rotation_euler=(0,0,0)
  if key=='Ram':pose.location[1]=-.26*(.5-.5*math.cos(t*math.tau*3))
  if key=='Flywheel':pose.rotation_euler[1]=t*math.tau*3
  if key.endswith('Roller'):pose.rotation_euler[1]=-t*math.tau*6
  if key.startswith('BeltSlat'):
   i=int(key[-2:]);distance=i*perimeter/28+t*perimeter;y,z,a=belt(distance);a-=math.floor(distance/perimeter)*math.tau;_,_,a0=belt(i*perimeter/28)
   # Bone local Y = world Z; local Z = -world Y.
   pose.location=(0,z-rest.z,-(y-rest.y));pose.rotation_euler[0]=a-a0
  pose.keyframe_insert(data_path='location',frame=f);pose.keyframe_insert(data_path='rotation_euler',frame=f)
for layer in action.layers:
 for strip in layer.strips:
  for bag in getattr(strip,'channelbags',[]):
   for curve in bag.fcurves:
    for point in curve.keyframe_points:point.interpolation='LINEAR'
scene.frame_set(1)
# Product camera, uncluttered studio lighting.
bpy.ops.object.camera_add(location=(6,-8,4.8));cam=bpy.context.object;cam.name='Press inspection camera';cam.rotation_euler=(Vector((-.05,-1.0,1.65))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=5.5;scene.camera=cam
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.35
for p,power,size in [((2,-4,7),800,5),((-4,-1,5),650,4),((1,4,6),900,3)]:
 bpy.ops.object.light_add(type='AREA',location=p);lamp=bpy.context.object;lamp.data.energy=power;lamp.data.size=size;lamp.rotation_euler=(Vector((0,-1,1.5))-lamp.location).to_track_quat('-Z','Y').to_euler()
scene.view_settings.view_transform='AgX';scene.render.film_transparent=False;scene.render.image_settings.color_mode='RGBA';scene.render.filepath=str(OUT/'AmmoPress-preview.png')
bpy.ops.object.select_all(action='DESELECT')
for o in parts:o.select_set(True)
rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'AmmoPress.blend'))
bpy.ops.export_scene.gltf(filepath=str(OUT/'AmmoPress.glb'),export_format='GLB',use_selection=True,export_animations=True,export_force_sampling=True,export_animation_mode='ACTIONS')
result={'parts':len(parts),'bones':len(arm.bones),'triangles':sum(len(p.vertices)-2 for o in parts for p in o.data.polygons),'output':str(OUT),'clip':'Operate, 3 seconds'}
