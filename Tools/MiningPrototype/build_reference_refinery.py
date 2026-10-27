"""Build the approved hopper/drum refinery and bulk ingot skip through Blender MCP."""
import bpy,math
from pathlib import Path
from mathutils import Vector,Matrix
BASE=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine');__file__=str(BASE/'Tools/MiningPrototype/build_machines.py')
exec(compile(Path(__file__).read_text().split('def fittings')[0],__file__,'exec'))
OUT=BASE/'artifacts/reference-refinery';OUT.mkdir(parents=True,exist_ok=True)
# A separate scene preserves the approved press and any other open Blender work.
scene=bpy.data.scenes.new('Refinery Reference');bpy.context.window.scene=scene
collection=bpy.data.collections.new('Reference Refinery');scene.collection.children.link(collection)
bpy.context.view_layer.active_layer_collection=bpy.context.view_layer.layer_collection.children[collection.name]
palette.update(yellow=(.72,.34,.012,1),steel=(.12,.15,.19,1),dark=(.025,.034,.045,1),ingot=(.40,.43,.47,1),black=(.012,.016,.020,1))
mats={k:material('Refinery '+k,v)for k,v in palette.items()};parts=[];spec={}
for key in ['steel','ingot','dark']:
 p=mats[key].node_tree.nodes.get('Principled BSDF');p.inputs['Metallic'].default_value=.65;p.inputs['Roughness'].default_value=.38
def bolt(p,axis='Y',r=.038):return cyl('Hex mounting bolt',p,r,.045,'steel',axis=axis,vertices=6)
def pipe(name,points,r=.055,mat='dark',bone='Root'):
 for a,b in zip(points,points[1:]):beam(name,a,b,r,mat,bone)
def ring(name,x,y,z,r,depth,mat='yellow',bone='Root',n=12):
 # Open sleeve rather than an opaque cylinder through the processing chamber.
 verts=[(xx,y+math.cos(i*math.tau/n)*rr,z+math.sin(i*math.tau/n)*rr)for xx,rr in [(x-depth/2,r),(x+depth/2,r),(x-depth/2,r-.09),(x+depth/2,r-.09)]for i in range(n)]
 faces=[]
 for i in range(n):
  j=(i+1)%n;faces.extend([(i,j,n+j,n+i),(i,2*n+i,2*n+j,j),(n+i,n+j,3*n+j,3*n+i),(2*n+i,3*n+i,3*n+j,2*n+j)])
 return mesh(name,verts,faces,mat,bone)
# Composition established before detail.
bpy.ops.object.camera_add(location=(-7.8,-10,6.1));cam=bpy.context.object;cam.name='Refinery inspection camera';cam.data.type='ORTHO';cam.data.ortho_scale=8.7
cam.rotation_euler=(Vector((.45,-.65,1.6))-cam.location).to_track_quat('-Z','Y').to_euler();scene.camera=cam
# Heavy open skid, bolted feet and service platform.
for y in [-.80,.76]:box('Longitudinal skid',(0,y,.28),(4.5,.22,.30),'dark')
for x in [-2.12,-.65,1.32]:box('Skid crossmember',(x,0,.28),(.22,1.74,.30),'dark')
for x in [-2.12,1.32]:
 for y in [-.79,.76]:
  box('Anchor shoe',(x,y,.08),(.62,.48,.16),'steel',bevel=.025)
  box('Cast support foot',(x,y,.27),(.39,.31,.35),'dark')
  for dx in [-.21,.21]:bolt((x+dx,y,.18),'Z',.04)
box('Motor support deck',(-1.65,0,.57),(1.36,1.56,.15),'dark')
# Horizontal processing chamber, a deliberately open front for the glowing inspection slot.
cx=-.10;cz=1.52;r=.85;n=12
verts=[(x,math.cos(i*math.tau/n)*r,cz+math.sin(i*math.tau/n)*r)for x in [-1.13,1.13]for i in range(n)]
faces=[]
for i in range(n):
 if i in [5,6]:continue
 j=(i+1)%n;faces.append((i,j,n+j,n+i))
mesh('Faceted processing chamber',verts,faces,'dark')
for x in [-1.18,1.18]:
 ring('Yellow chamber armour collar',x,0,cz,.93,.27)
 cyl('Chamber end plate',(x,0,cz),.80,.08,'steel',axis='X',vertices=12)
 for i in range(10):
  a=i*math.tau/10;bolt((x+(-.16 if x<0 else .16),math.cos(a)*.77,cz+math.sin(a)*.77),'X',.043)
 box('Chamber bearing stand',(x,.08,.65),(.38,1.17,.80),'yellow',bevel=.055)
 for y in [-.5,.5]:box('Bearing gusset',(x,y,.74),(.47,.17,.52),'steel')
# Fixed viewing window, side panels and protective rails (rotor stays behind housing).
for x in [-.91,.91]:box('Inspection window cheek',(x,-.78,cz),(.40,.16,.73),'dark',bevel=.025)
box('Hot processing interior',(0,-.72,cz),(1.36,.12,.55),'orange',bevel=.018)
for z in [cz-.36,cz+.36]:box('Inspection frame',(0,-.86,z),(1.62,.16,.12),'steel',bevel=.02)
for z in [cz-.28,cz,cz+.28]:
 beam('Inspection guard bar',(-.78,-.98,z),(.78,-.98,z),.04,'ingot')
 for x in [-.79,.79]:cyl('Guard bar collar',(x,-.98,z),.065,.12,'steel',axis='X',vertices=8)
for x in [-.73,.73]:box('Inspection side rail',(x,-.94,cz),(.095,.16,.80),'dark',bevel=.02)
box('Upper service panel',(0,-.59,2.22),(1.30,.15,.38),'steel',bevel=.025)
for x in [-.4,-.23,-.06]:box('Vent louvre',(x,-.68,2.24),(.07,.035,.15),'black',bevel=.005)
box('Chamber maintenance hatch',(.14,0,2.42),(.64,.61,.10),'steel')
box('Maintenance hatch inset',(.14,0,2.48),(.48,.43,.025),'yellow',bevel=.01)
for x in [-.12,.4]:
 for y in [-.22,.22]:bolt((x,y,2.51),'Z',.032)
# Visible drive train: motor, pulley and annular rotor.
cyl('Processing rotor',(-1.38,0,cz),.66,.16,'dark','Drum','X',12)
ring('Rotor silver rim',-1.48,0,cz,.57,.07,'steel','Drum')
for a in [0,math.pi/2,math.pi,3*math.pi/2]:beam('Rotor spoke',(-1.53,0,cz),(-1.53,math.cos(a)*.51,cz+math.sin(a)*.51),.05,'steel','Drum')
cyl('Rotor drive hub',(-1.59,0,cz),.19,.24,'ingot','Drum','X',10)
spec['Drum']=((-1.4,0,cz),(-1.1,0,cz))
cyl('Ribbed drive motor',(-1.90,.12,1.14),.31,.62,'dark',axis='X',vertices=12)
for x in [-2.19,-2.10,-2.01,-1.92,-1.83,-1.74]:ring('Motor cooling fin',x,.12,1.14,.34,.032,'steel')
box('Drive gearbox',(-1.55,-.02,1.12),(.28,.73,.63),'yellow',bevel=.065)
cyl('Motor end fan',(-2.25,.12,1.14),.25,.04,'steel','MotorFan','X',10);spec['MotorFan']=((-2.25,.12,1.14),(-2.0,.12,1.14))
for a in [0,math.pi/2]:
 o=box('Motor fan cross',(-2.28,.12,1.14),(.025,.40,.05),'dark','MotorFan',.005)
 for v in o.data.vertices:v.co=Vector((-2.28,.12,1.14))+Matrix.Rotation(a,4,'X')@(v.co-Vector((-2.28,.12,1.14)))
# High left hopper with open mouth, braces and a front feed chute.
for x in [-2.26,-.99]:
 for y in [-.60,.67]:
  box('Hopper stanchion',(x,y,1.40),(.13,.13,1.68),'steel',bevel=.015)
  box('Hopper foot bracket',(x,y,.69),(.31,.25,.17),'yellow')
box('Hopper support ring',(-1.60,.13,2.31),(1.72,1.61,.20),'yellow')
hopper((-1.6,.13,2.42),(2.37,1.96,1.08))
for x in [-2.38,-.82]:
 pipe('Hopper outer rib',[(x,-.38,2.45),(x,-.76,2.90),(x,-.89,3.44)],.055,'steel')
 for z,y in [(2.48,-.41),(3.40,-.88)]:bolt((x,y,z),'Y',.045)
box('Hopper feed aperture',(-1.34,-.60,2.63),(.72,.15,.50),'black',bevel=.02)
box('Sliding feed gate',(-1.34,-.70,2.63),(.66,.04,.43),'steel','FeedGate',.015);spec['FeedGate']=((-1.34,-.70,2.63),(-1.34,-.70,2.85))
pipe('Feed chute side left',[(-1.73,-.64,2.85),(-1.65,-.95,2.57),(-1.05,-.95,2.30)],.07,'yellow')
pipe('Feed chute side right',[(-1.00,-.64,2.85),(-.93,-.95,2.57),(-.67,-.95,2.30)],.07,'yellow')
mesh('Sloped feed chute',[(-1.73,-.72,2.68),(-1.0,-.72,2.68),(-.67,-.95,2.30),(-1.05,-.95,2.30)],[(0,1,2,3)],'dark')
# A restrained ore load in the hopper, visibly angular rather than smooth stones.
for i in range(13):
 x=-1.6+((i%4)-1.5)*.46;y=.13+((i//4)-1)*.43;z=3.28+(.11 if i%3==0 else 0)
 bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=.29,location=(x,y,z));o=bpy.context.object;o.scale=(1.1,.85,.9);o.rotation_euler=(i*.7,i*.23,i*.31);finish(o,'Angular feed ore',mats and ('orange' if i%3 else 'steel'))
# Rear stacks and readable pipe routing.
for x,z in [(.44,3.00),(1.0,2.76)]:
 cyl('Exhaust stack',(x,.49,z-.43),.13,1.12,'dark',vertices=10)
 for height in [z-.83,z-.36]:cyl('Exhaust retaining collar',(x,.49,height),.175,.11,'steel',vertices=10)
 cyl('Exhaust upper sleeve',(x,.49,z+.03),.14,.43,'ingot',vertices=10)
 pipe('Stack process connection',[(x,.49,2.0),(x,.86,2.0),(x,.86,1.60)],.06,'steel')
for x in [-.95,.95]:
 pipe('Coolant return',[(x,-.5,1.0),(x,-.72,.68),(x*.6,-.72,.54)],.075,'steel')
pipe('Front pipe rail',[(-1.15,-.84,.56),(.98,-.84,.56)],.085,'dark')
box('Control cabinet',(-2.16,-.76,.83),(.54,.32,.84),'yellow',bevel=.045)
box('Cabinet status bezel',(-2.16,-.94,1.04),(.21,.04,.17),'dark',bevel=.01)
box('Status display',(-2.16,-.968,1.04),(.14,.015,.11),'cyan',bevel=.006)
for z in [.60,.69,.78]:box('Control cabinet vent',(-2.16,-.94,z),(.26,.025,.035),'dark',bevel=.003)
pipe('Control conduit',[(-2.0,-.72,.44),(-1.78,-.72,.42),(-1.72,-.38,1.0)],.035)
# Ingot discharge, with rollers rotating about Y so material advances along +X.
for y in [-.41,.41]:box('Output side rail',(1.69,y,1.35),(1.02,.13,.38),'yellow',bevel=.055)
for i in range(5):
 x=1.27+i*.19;key='OutputRoller%d'%i
 cyl('Output roller',(x,0,1.35),.09,.80,'steel',key,'Y',12);spec[key]=((x,0,1.35),(x,.3,1.35))
 for y in [-.49,.49]:cyl('Roller end bearing',(x,y,1.35),.095,.05,'dark',axis='Y',vertices=8)
box('Output drive casing',(1.72,-.54,1.18),(.43,.20,.34),'yellow')
cyl('Output motor cap',(1.72,-.68,1.18),.12,.09,'dark',axis='Y',vertices=10)
# Large bulk skip: open hollow bin with reinforced corners, not a shallow tray.
bx=2.45;by=-1.43;bw=2.65;bd=2.16;bh=.99
box('Bulk bin floor',(bx,by,.15),(bw,bd,.14),'dark','BucketFloor',.025);spec['BucketFloor']=((bx,by,.25),(bx,by,.40))
for x in [bx-bw/2,bx+bw/2]:
 box('Bulk skip side',(x,by,.65),(.09,bd,bh),'dark',bevel=.025)
 box('Yellow upper side rim',(x,by,1.15),(.12,bd+.09,.12),'yellow',bevel=.02)
 box('Lower structural side rail',(x,by,.23),(.14,bd,.13),'steel')
for y in [by-bd/2,by+bd/2]:
 box('Bulk skip end panel',(bx,y,.65),(bw,.09,bh),'dark',bevel=.025)
 box('Yellow upper end rim',(bx,y,1.15),(bw+.12,.12,.12),'yellow',bevel=.02)
 box('Lower end rail',(bx,y,.23),(bw,.14,.14),'steel')
for x in [bx-bw/2,bx+bw/2]:
 for y in [by-bd/2,by+bd/2]:
  box('Skip yellow corner post',(x,y,.63),(.19,.19,1.20),'yellow',bevel=.025)
  for z in [.18,1.09]:
   box('Corner protection plate',(x,y,z),(.26,.24,.16),'steel',bevel=.015);bolt((x,y-.14,z),'Y',.042)
  box('Skip foot pad',(x,y,.075),(.36,.32,.15),'dark')
for x in [bx-.65,bx+.65]:box('Skip forklift channel',(x,by,.075),(.24,bd,.15),'steel',bevel=.01)
# Named static markers allow game output to follow the model without hand-tuned offsets.
for key,p in [('OutputStart',(1.27,0,1.49)),('OutputEnd',(2.08,0,1.49))]:
 spec[key]=(p,(p[0],p[1],p[2]+.15));box(key+' anchor bracket',(p[0],.3,1.08),(.06,.10,.05),'dark',key,.005)
# Source-only ingot examples for the reference render, excluded from the runtime export.
runtime_parts=list(parts)
for i in range(14):
 x=bx+((i%4)-1.5)*.48;y=by+((i//4)-1.3)*.45;z=.31+(i//8)*.17
 o=box('Reference ingot pile %02d'%i,(x,y,z),(.43,.24,.16),'ingot',bevel=.04)
 angle=i*1.37
 for v in o.data.vertices:v.co=Vector((x,y,z))+Matrix.Rotation(angle,4,'Z')@(v.co-Vector((x,y,z)))
for x in [1.43,1.89]:box('Reference conveyor ingot',(x,0,1.56),(.36,.25,.15),'ingot',bevel=.035)
preview_parts=[o for o in parts if o not in runtime_parts]
arm=bpy.data.armatures.new('RefineryRig');rig=bpy.data.objects.new('RefineryRig',arm);collection.objects.link(rig);bpy.context.view_layer.objects.active=rig;rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT');root=arm.edit_bones.new('Root');root.head=(0,0,0);root.tail=(0,0,.3)
for key,(a,b) in spec.items():bone=arm.edit_bones.new(key);bone.head=a;bone.tail=b;bone.parent=root
bpy.ops.object.mode_set(mode='OBJECT')
for o in runtime_parts:mod=o.modifiers.new('Rigid mechanical part','ARMATURE');mod.object=rig;o.parent=rig
rig.animation_data_create();action=bpy.data.actions.new('Operate');rig.animation_data.action=action
scene.render.fps=30;scene.frame_start=1;scene.frame_end=91
for key in spec:
 p=rig.pose.bones[key];p.rotation_mode='XYZ'
 for f in range(1,92):
  t=(f-1)/90;p.location=(0,0,0);p.rotation_euler=(0,0,0)
  if key=='Drum':p.rotation_euler[1]=-math.tau*t
  if key=='MotorFan':p.rotation_euler[1]=math.tau*t*4
  if key=='FeedGate':p.location[1]=.15*(.5-.5*math.cos(t*math.tau*3))
  if key.startswith('OutputRoller'):p.rotation_euler[1]=-math.tau*t*4
  p.keyframe_insert(data_path='location',frame=f);p.keyframe_insert(data_path='rotation_euler',frame=f)
for layer in action.layers:
 for strip in layer.strips:
  for bag in getattr(strip,'channelbags',[]):
   for curve in bag.fcurves:
    for point in curve.keyframe_points:point.interpolation='LINEAR'
scene.frame_set(1);scene.world=bpy.data.worlds.new('Refinery studio');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.35
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.render.resolution_x=1200;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
for pos,power,size in [((-3,-5,7),1100,5),((5,-1,5),750,4),((1,5,7),1200,4)]:
 bpy.ops.object.light_add(type='AREA',location=pos);lamp=bpy.context.object;lamp.data.energy=power;lamp.data.size=size;lamp.rotation_euler=(Vector((0,-.5,1.3))-lamp.location).to_track_quat('-Z','Y').to_euler()
scene.view_settings.view_transform='AgX';scene.view_settings.exposure=-.35;scene.render.filepath=str(OUT/'Refinery-preview.png');scene.render.image_settings.color_mode='RGBA'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Refinery.blend'))
# One skinned runtime mesh; source retains individually editable parts.
bpy.ops.object.select_all(action='DESELECT');copies=[]
for o in runtime_parts:c=o.copy();c.data=o.data.copy();collection.objects.link(c);c.select_set(True);copies.append(c)
bpy.context.view_layer.objects.active=copies[0];bpy.ops.object.join();body=bpy.context.object;body.name='RefineryRuntimeMesh';rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.gltf(filepath=str(OUT/'Refinery.glb'),export_format='GLB',use_selection=True,export_animations=True,export_force_sampling=True,export_animation_mode='ACTIVE_ACTIONS')
triangles=sum(len(p.vertices)-2 for p in body.data.polygons);bpy.data.objects.remove(body,do_unlink=True)
result={'parts':len(runtime_parts),'triangles':triangles,'bones':len(arm.bones),'output':str(OUT),'preview_ingots':'source-only; game pile follows real stock'}
