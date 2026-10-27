"""Approved low-profile drill reconstruction, executed in the user's Blender MCP scene."""
import bpy,math,random
from pathlib import Path
from mathutils import Vector,Matrix
BASE=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine');__file__=str(BASE/'Tools/MiningPrototype/build_machines.py')
exec(compile(Path(__file__).read_text().split('def fittings')[0],__file__,'exec'))
_finish=finish
def finish(o,*args,**kwargs):
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);return _finish(o,*args,**kwargs)
OUT=BASE/'artifacts/reference-drill';OUT.mkdir(parents=True,exist_ok=True)
scene=bpy.data.scenes.new('Drill Reference');bpy.context.window.scene=scene
collection=bpy.data.collections.new('Reference Mining Drill');scene.collection.children.link(collection);bpy.context.view_layer.active_layer_collection=bpy.context.view_layer.layer_collection.children[collection.name]
palette.update(yellow=(.68,.32,.018,1),steel=(.12,.15,.18,1),dark=(.024,.031,.040,1),ingot=(.40,.43,.47,1),black=(.010,.014,.019,1),earth=(.16,.105,.065,1),rock=(.24,.18,.13,1),ochre=(.32,.20,.09,1))
mats={k:material('Drill '+k,v)for k,v in palette.items()};parts=[]
for k in ['steel','ingot','dark']:
 p=mats[k].node_tree.nodes.get('Principled BSDF');p.inputs['Metallic'].default_value=.7 if k=='ingot' else .45;p.inputs['Roughness'].default_value=.35 if k=='ingot' else .48
p=mats['cyan'].node_tree.nodes.get('Principled BSDF');p.inputs['Emission Color'].default_value=(.02,.65,.68,1);p.inputs['Emission Strength'].default_value=.8
def bolt(p,axis='Z',r=.04,bone='Root'):
 return cyl('Recessed hex fastener',p,r,.045,'steel',bone,axis,6)
def hose(name,points,r=.048,mat='dark',bone='Root'):
 # Curved industrial hose with a low-sided cross section, converted to editable mesh.
 curve=bpy.data.curves.new(name,'CURVE');curve.dimensions='3D';curve.resolution_u=8;curve.bevel_depth=r;curve.bevel_resolution=1;curve.resolution_u=6
 spline=curve.splines.new('BEZIER');spline.bezier_points.add(len(points)-1)
 for p,co in zip(spline.bezier_points,points):p.co=co;p.handle_left_type='AUTO';p.handle_right_type='AUTO'
 o=bpy.data.objects.new(name,curve);collection.objects.link(o);bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.convert(target='MESH');finish(bpy.context.object,name,mat,bone)
def prism(name,outline,y,depth,mat='yellow',bone='Root',bevel=.025):
 n=len(outline);verts=[(x,yy,z)for yy in [y-depth/2,y+depth/2]for x,z in outline];faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n)for i in range(n)]
 o=mesh(name,verts,faces,mat,bone)
 if bevel:
  bpy.context.view_layer.objects.active=o;mod=o.modifiers.new('Machined edge bevel','BEVEL');mod.width=bevel;mod.segments=1;bpy.ops.object.modifier_apply(modifier=mod.name);o.vertex_groups[bone].add(list(range(len(o.data.vertices))),1,'REPLACE')
 return o
def sleeve(name,p,outer,inner,height,mat='steel',bone='Root',n=12):
 x,y,z=p;verts=[(x+math.cos(i*math.tau/n)*r,y+math.sin(i*math.tau/n)*r,zz)for zz,r in [(z-height/2,outer),(z+height/2,outer),(z-height/2,inner),(z+height/2,inner)]for i in range(n)]
 faces=[]
 for i in range(n):
  j=(i+1)%n;faces.extend([(i,j,n+j,n+i),(i,2*n+i,2*n+j,j),(n+i,n+j,3*n+j,3*n+i),(2*n+i,3*n+i,3*n+j,2*n+j)])
 return mesh(name,verts,faces,mat,bone)
# Match the reference camera: see the auger through the open front of the wide base.
bpy.ops.object.camera_add(location=(8.6,-11.5,6.4));cam=bpy.context.object;cam.name='Drill reference camera';cam.data.type='ORTHO';cam.data.ortho_scale=9.3;cam.rotation_euler=(Vector((.55,.05,1.10))-cam.location).to_track_quat('-Z','Y').to_euler();scene.camera=cam
# Nine armoured sectors form a horseshoe, leaving a real open front to the drill.
for i in range(9):
 a=math.radians(-45+i*30);b=a+math.radians(30)
 def wedge(name,ri,ro,z0,z1,mat):
  v=[(math.cos(t)*r,math.sin(t)*r,z)for z in [z0,z1]for r,t in [(ri,a),(ro,a),(ro,b),(ri,b)]]
  return mesh(name,v,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],mat)
 wedge('Cast octagonal base sector',1.15,2.18,.30,.69,'dark');wedge('Yellow upper base armour',1.20,2.25,.69,.84,'yellow')
 wedge('Inner shaft guard liner',1.12,1.20,.35,.74,'steel')
 mid=(a+b)/2
 for t in [a+.065,b-.065]:bolt((math.cos(t)*1.94,math.sin(t)*1.94,.875),r=.045)
 for r in [1.27,2.1]:
  p=(math.cos(mid)*r,math.sin(mid)*r,.86);box('Base panel seam bracket',p,(.16,.16,.07),'steel',bevel=.012)
# Four broad splayed feet. Front pair are prominent in the concept silhouette.
for sx,sy in [(-1,-1),(1,-1),(-1,1),(1,1)]:
 x=sx*1.89;y=sy*1.45
 box('Outrigger anchor pad',(x*1.15,y*1.19,.09),(.98,.79,.18),'dark',bevel=.04)
 box('Foot steel anchor plate',(x*1.15,y*1.19,.20),(.85,.67,.10),'steel',bevel=.035)
 profile=[(x-.30,.23),(x+.30,.23),(x+.25,.73),(x+.02,.94),(x-.20,.90)]
 prism('Yellow stabilizer cheek',profile,y,.26,'yellow',bevel=.035)
 for dx in [-.38,.38]:
  for dy in [-.22,.22]:bolt((x*1.15+dx,y*1.19+dy,.27),r=.05)
 cyl('Stabilizer pivot',(x,y-.18,.65),.11,.12,'ingot',axis='Y',vertices=10)
 box('Stabilizer dark inset',(x,y-.15,.49),(.22,.035,.20),'dark',bevel=.02)
# Two low hydraulic supports and their bolted base pedestals.
for x in [-1.55,1.55]:
 box('Pillar base housing',(x,0,.93),(.78,.84,.52),'yellow',bevel=.075)
 box('Pillar mounting plinth',(x,0,.73),(.93,.98,.13),'steel',bevel=.035)
 for dx in [-.34,.34]:
  for y in [-.34,.34]:bolt((x+dx,y,.82),r=.047)
 cyl('Hydraulic lower barrel',(x,0,1.14),.23,.62,'dark',vertices=12)
 cyl('Yellow cylinder collar',(x,0,1.20),.27,.25,'yellow',vertices=12)
 cyl('Polished hydraulic piston',(x,0,1.79),.16,1.02,'ingot','Carriage',vertices=16)
 for z in [1.35,2.22]:sleeve('Hydraulic gland',(x,0,z),.225,.15,.12,'steel')
 for y in [-.26,.26]:box('Cylinder guide rail',(x,y,1.78),(.085,.10,1.10),'dark',bevel=.012)
 box('Upper piston bearing',(x,0,2.28),(.48,.58,.23),'dark','Carriage',.035)
# Angled crosshead arms with dark triangular weight-relief insets.
for sign in [-1,1]:
 outline=[(sign*x,z)for x,z in [(.42,2.28),(.42,2.73),(1.47,2.86),(1.89,2.67),(1.92,2.30),(1.35,2.30),(1.16,2.43)]]
 if sign<0:outline.reverse()
 prism('Gantry crosshead casting',outline,0,.61,'yellow','Carriage',.045)
 inset=[(sign*x,z)for x,z in [(1.17,2.52),(1.42,2.72),(1.72,2.61),(1.72,2.45)]]
 if sign<0:inset.reverse()
 prism('Crosshead weight relief',inset,-.326,.025,'dark','Carriage',.008)
 for x,z in [(1.80,2.39),(1.79,2.64),(.73,2.64),(1.12,2.48)]:bolt((sign*x,-.36,z),'Y',.045,'Carriage')
# Layered central rotary drive head, central shaft and real helical auger.
for z,r,h,mat in [(2.15,.52,.20,'yellow'),(2.31,.60,.12,'steel'),(2.53,.55,.36,'yellow'),(2.77,.60,.12,'steel'),(2.91,.57,.17,'dark'),(3.03,.53,.10,'yellow')]:cyl('Rotary drive head collar',(0,0,z),r,h,mat,'Carriage',vertices=12)
sleeve('Lower drive clamp',(0,0,2.07),.37,.19,.17,'ingot','Carriage',12)
for i in range(8):
 a=i*math.tau/8
 bolt((math.cos(a)*.46,math.sin(a)*.46,3.10),r=.042,bone='Carriage')
 for z in [2.28,2.82]:box('Head flange clamp',(math.cos(a)*.59,math.sin(a)*.59,z),(.13,.13,.16),'steel','Carriage',.015)
box('Drive head cyan panel bezel',(0,-.56,2.55),(.22,.075,.30),'dark','Carriage',.02)
box('Drive head cyan status strip',(0,-.604,2.55),(.075,.015,.23),'cyan','Carriage',.01)
box('Top service cap',(0,0,3.13),(.42,.31,.07),'steel','Carriage',.025)
hose('Top cap lifting handle',[(-.19,0,3.16),(-.13,0,3.27),(.13,0,3.27),(.19,0,3.16)],.033,'ingot','Carriage')
cyl('Exposed central auger shaft',(0,0,.62),.185,2.88,'ingot','Auger',vertices=16)
cyl('Auger drive coupling',(0,0,1.97),.275,.20,'steel','Auger',vertices=12)
for i in range(8):
 a=i*math.tau/8;bolt((math.cos(a)*.225,math.sin(a)*.225,2.09),r=.03,bone='Auger')
# Closed solid flight with an outer cutting lip, not stacked circular disks.
v=[];steps=120;turns=4.1;z0=-.82;height=2.67;thickness=.035
for i in range(steps+1):
 a=i/steps*math.tau*turns;z=z0+height*i/steps
 for r,dz in [(.19,thickness),(.57,thickness),(.19,-thickness),(.57,-thickness)]:v.append((math.cos(a)*r,math.sin(a)*r,z+dz))
faces=[]
for i in range(steps):
 j=i*4;k=j+4;faces.extend([(j,j+1,k+1,k),(j+2,k+2,k+3,j+3),(j+1,j+3,k+3,k+1),(j,j+2,k+2,k)])
faces.extend([(0,2,3,1),(steps*4,steps*4+1,steps*4+3,steps*4+2)])
mesh('Continuous four-turn helical auger',v,faces,'ingot','Auger')
# Faceted cutting nose below the last flight.
bpy.ops.mesh.primitive_cone_add(vertices=12,radius1=.02,radius2=.20,depth=.32,location=(0,0,-.91));finish(bpy.context.object,'Auger cutting tip','steel','Auger')
# Big rear-right ribbed motor and gear coupling, visibly connected to the head.
box('Motor mounting saddle',(1.30,.68,2.58),(1.78,.75,.19),'dark','Carriage',.04)
cyl('Heavy ribbed drive motor',(1.51,.70,2.97),.40,1.30,'dark','Carriage','X',16)
for x in [1.02,2.04]:
 cyl('Yellow motor end band',(x,.70,2.97),.46,.19,'yellow','Carriage','X',12)
 cyl('Steel motor clamp',(x-.12,.70,2.97),.465,.065,'steel','Carriage','X',12)
for i in range(12):
 a=i*math.tau/12;y=.70+math.cos(a)*.41;z=2.97+math.sin(a)*.41
 box('Long motor cooling fin',(1.52,y,z),(1.00,.055,.055),'steel','Carriage',.008)
box('Motor electrical junction',(1.47,.70,3.43),(.40,.35,.14),'dark','Carriage',.02)
cyl('Motor end fan',(2.20,.70,2.97),.33,.08,'steel','MotorFan','X',12)
for a in [0,math.pi/2]:
 o=box('Motor fan grille',(2.25,.70,2.97),(.025,.53,.06),'dark','MotorFan',.005)
 for vertex in o.data.vertices:vertex.co=Vector((2.25,.70,2.97))+Matrix.Rotation(a,4,'X')@(vertex.co-Vector((2.25,.70,2.97)))
cyl('Motor drive shaft',(.63,.70,2.97),.15,.42,'ingot','Carriage','X',12)
box('Rotary transfer gearbox',(.39,.55,2.95),(.47,.61,.50),'steel','Carriage',.055)
for x in [.68,.84]:cyl('Drive shaft flange',(x,.70,2.97),.25,.08,'steel','Carriage','X',10)
# Real hoses are curved and have fittings rather than disconnected pipe fragments.
for x in [-1.55,1.55]:
 hose('Upper hydraulic hose',[(x,.19,2.30),(x,.30,2.87),(x*.55,.29,2.96),(.24,.26,2.80)],.046,bone='Carriage')
 for z in [1.1,2.27]:cyl('Hydraulic hose union',(x,.30,z),.07,.11,'ingot',axis='Y',vertices=8)
hose('Motor power conduit',[(1.65,.40,2.85),(1.92,.22,2.63),(1.97,-.02,2.11),(1.71,-.29,1.72)],.055)
hose('Return hydraulic line',[(1.8,-.22,1.01),(2.00,-.39,1.14),(2.08,-.23,1.89),(1.73,.08,2.20)],.045)
# Front electrical cabinet and deliberate service-panel detailing.
box('Front control cabinet',(-1.08,-1.47,.99),(.66,.32,.83),'yellow',bevel=.055)
box('Cabinet faceplate',(-1.08,-1.65,1.10),(.44,.045,.49),'dark',bevel=.025)
for x in [-1.20,-1.02]:
 for z in [1.20,1.05]:box('Cyan control switch',(x,-1.685,z),(.075,.018,.10),'cyan',bevel=.012)
box('Control indicator',(-1.14,-1.685,.92),(.23,.018,.035),'cyan',bevel=.005)
hose('Control cabinet guard',[(-.66,-1.69,.62),(-.65,-1.69,1.39),(-.86,-1.69,1.40)],.035,'ingot')
for dx in [-.18,0,.18]:hose('Control cable loop',[(-1.08+dx,-1.51,.64),(-1.05+dx,-1.55,.43),(-.80+dx,-1.28,.40),(-.70+dx,-1.10,.66)],.032)
for sign in [-1,1]:
 box('Armoured front base cheek',(sign*1.65,-.91,.84),(.68,.59,.58),'yellow',bevel=.08)
 box('Dark service recess',(sign*1.65,-1.22,.84),(.45,.025,.28),'dark',bevel=.035)
 beam('Diagonal service brace',(sign*1.85,-1.25,.72),(sign*1.50,-1.25,.96),.034,'steel')
 box('Side status bezel',(sign*2.03,-.93,.88),(.08,.31,.31),'dark',bevel=.015)
 box('Side status lamp',(sign*2.08,-.93,.88),(.012,.095,.19),'cyan',bevel=.005)
# Right-hand ore discharge tunnel connected to a sloped open chute.
box('Ore separator housing',(1.77,.65,1.82),(.67,.81,.63),'yellow',bevel=.055)
box('Separator outlet shadow',(2.12,.65,1.77),(.035,.60,.40),'black',bevel=.015)
for y in [.30,1.0]:box('Ore port jamb',(2.15,y,1.77),(.17,.09,.52),'steel',bevel=.015)
for z in [1.50,2.04]:box('Ore port lintel',(2.15,.65,z),(.18,.80,.09),'yellow',bevel=.015)
floor=[(2.13,.30,1.53),(2.13,1.00,1.53),(3.20,1.00,1.14),(3.20,.30,1.14)]
mesh('Ore chute sloped floor',floor,[(0,1,2,3)],'steel')
for y in [.28,1.02]:
 mesh('Ore chute side wall',[(2.10,y,1.54),(2.10,y,1.97),(2.52,y,1.75),(3.23,y,1.28),(3.23,y,1.13)],[(0,1,2,3,4)],'yellow')
 beam('Chute rolled edge',(2.10,y,1.98),(3.23,y,1.29),.035,'steel')
box('Chute gate',(2.22,.65,1.60),(.07,.61,.26),'dark','FeedGate',.015)
# Collection hopper is centred on the chute; the lip lands inside its opening.
hx=3.50;hy=.65
hopper((hx,hy,.48),(1.42,1.24,.67))
box('Hopper discharge neck',(hx,hy,.37),(.49,.48,.25),'dark')
box('Hopper skid',(hx,hy,.12),(1.48,1.38,.20),'dark',bevel=.04)
for x in [hx-.56,hx+.56]:
 for y in [hy-.47,hy+.47]:
  box('Hopper support foot',(x,y,.29),(.16,.17,.40),'steel',bevel=.018);bolt((x,y,.24),r=.035)
box('Ore hopper status bezel',(hx,hy-.55,.71),(.24,.08,.32),'dark',bevel=.02)
box('Ore hopper cyan strip',(hx,hy-.60,.71),(.065,.018,.22),'cyan',bevel=.008)
rng=random.Random(73)
for i in range(15):
 x=hx+rng.uniform(-.47,.47);y=hy+rng.uniform(-.38,.38);z=.95+rng.uniform(-.01,.17)
 bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=rng.uniform(.13,.22),location=(x,y,z));o=bpy.context.object;o.scale=(1,.85,.75);o.rotation_euler=(rng.random(),rng.random(),rng.random());finish(o,'Hopper angular ore','rock' if i%3 else 'ochre')
# Rigid-part animation preserves editable subassemblies.
spec={'Carriage':((0,0,0),(0,0,.3),'Root'),'Auger':((0,0,1.85),(0,0,2.15),'Carriage'),'MotorFan':((2.20,.70,2.97),(2.5,.70,2.97),'Carriage'),'FeedGate':((2.22,.65,1.72),(2.22,.95,1.72),'Root')}
arm=bpy.data.armatures.new('MiningDrillRig');rig=bpy.data.objects.new('MiningDrillRig',arm);collection.objects.link(rig);bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
root=arm.edit_bones.new('Root');root.head=(0,0,0);root.tail=(0,0,.3)
for name,(a,b,parent) in spec.items():bone=arm.edit_bones.new(name);bone.head=a;bone.tail=b;bone.parent=arm.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT')
for o in parts:mod=o.modifiers.new('Rigid mechanical part','ARMATURE');mod.object=rig;o.parent=rig
rig.animation_data_create();action=bpy.data.actions.new('Drill Operate');rig.animation_data.action=action;scene.render.fps=30;scene.frame_start=1;scene.frame_end=91
for name in spec:
 p=rig.pose.bones[name];p.rotation_mode='XYZ'
 for f in range(1,92):
  t=(f-1)/90;p.location=(0,0,0);p.rotation_euler=(0,0,0)
  if name=='Auger':p.rotation_euler[1]=-math.tau*t*3
  if name=='Carriage':p.location[1]=.025*math.sin(t*math.tau*3)
  if name=='MotorFan':p.rotation_euler[1]=-math.tau*t*6
  if name=='FeedGate':p.rotation_euler[1]=.16*math.sin(t*math.tau*3)
  p.keyframe_insert(data_path='location',frame=f);p.keyframe_insert(data_path='rotation_euler',frame=f)
for layer in action.layers:
 for strip in layer.strips:
  for bag in getattr(strip,'channelbags',[]):
   for curve in bag.fcurves:
    for point in curve.keyframe_points:point.interpolation='LINEAR'
scene.frame_set(1)
# Source-only cutaway ground, making the below-ground auger legible like the concept.
runtime_parts=list(parts);v=[]
for i in range(17):
 a=math.radians(-45+i*270/16)
 for r,z in [(1.14,-.025),(.73,-.30),(.61,-1.16)]:v.append((math.cos(a)*r,math.sin(a)*r,z))
faces=[]
for i in range(16):
 j=i*3;faces.extend([(j,j+3,j+4,j+1),(j+1,j+4,j+5,j+2)])
mesh('Presentation cutaway ground',v,faces,'earth')
for i in range(27):
 a=rng.uniform(math.radians(-40),math.radians(225));r=rng.uniform(.85,1.30)
 bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=rng.uniform(.10,.21),location=(math.cos(a)*r,math.sin(a)*r,-.02));o=bpy.context.object;o.scale=(1,.7,.7);finish(o,'Presentation broken rock','rock' if i%2 else 'earth')
scene.world=bpy.data.worlds.new('Drill studio');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.30
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.render.resolution_x=1300;scene.render.resolution_y=1000;scene.render.resolution_percentage=100;scene.render.image_settings.color_mode='RGBA';scene.render.filepath=str(OUT/'MiningDrill-preview.png')
for pos,power,size in [((2,-5,8),1100,5),((-5,-2,5),800,4),((2,5,7),1300,4)]:
 bpy.ops.object.light_add(type='AREA',location=pos);lamp=bpy.context.object;lamp.data.energy=power;lamp.data.size=size;lamp.rotation_euler=(Vector((0,0,1.3))-lamp.location).to_track_quat('-Z','Y').to_euler()
scene.view_settings.view_transform='AgX';scene.view_settings.exposure=-.35
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'MiningDrill.blend'))
result={'parts':len(runtime_parts),'triangles':sum(len(p.vertices)-2 for o in runtime_parts for p in o.data.polygons),'bones':len(arm.bones),'source':str(OUT/'MiningDrill.blend'),'separate_scene':True}
