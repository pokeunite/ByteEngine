"""Company operations tent and computer desk, built through Blender MCP."""
import bpy,math,random
from pathlib import Path
from mathutils import Vector,Matrix
BASE=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine');__file__=str(BASE/'Tools/MiningPrototype/build_machines.py')
exec(compile(Path(__file__).read_text().split('def fittings')[0],__file__,'exec'))
_finish=finish
def finish(o,*args,**kwargs):
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);return _finish(o,*args,**kwargs)
OUT=BASE/'artifacts/reference-terminal';OUT.mkdir(parents=True,exist_ok=True)
scene=bpy.data.scenes.new('Company Terminal Reference');bpy.context.window.scene=scene;collection=bpy.data.collections.new('Reference Company Terminal');scene.collection.children.link(collection);bpy.context.view_layer.active_layer_collection=bpy.context.view_layer.layer_collection.children[collection.name]
palette.update(yellow=(.55,.235,.012,1),steel=(.085,.105,.13,1),dark=(.018,.024,.032,1),ingot=(.32,.35,.39,1),canvas=(.105,.108,.067,1),lining=(.14,.14,.086,1),black=(.010,.014,.019,1),screen=(.008,.025,.035,1),gold=(.9,.53,.06,1),lamp=(1,.82,.44,1))
mats={k:material('Terminal '+k,v)for k,v in palette.items()};parts=[]
for key in ['steel','dark','ingot']:
 p=mats[key].node_tree.nodes.get('Principled BSDF');p.inputs['Metallic'].default_value=.55;p.inputs['Roughness'].default_value=.4
for key in ['canvas','lining','black']:
 p=mats[key].node_tree.nodes.get('Principled BSDF');p.inputs['Metallic'].default_value=0;p.inputs['Roughness'].default_value=.87
for key,strength in [('cyan',1.4),('gold',.8),('lamp',4)]:
 p=mats[key].node_tree.nodes.get('Principled BSDF');p.inputs['Emission Color'].default_value=mats[key].diffuse_color;p.inputs['Emission Strength'].default_value=strength
def bolt(p,axis='Y',r=.028):return cyl('Terminal hex fastener',p,r,.025,'ingot',axis=axis,vertices=6)
def hose(name,points,r=.018,mat='dark',bone='Root'):
 bpy.ops.object.select_all(action='DESELECT');data=bpy.data.curves.new(name,'CURVE');data.dimensions='3D';data.bevel_depth=r;data.bevel_resolution=1;data.resolution_u=5;s=data.splines.new('BEZIER');s.bezier_points.add(len(points)-1)
 for p,co in zip(s.bezier_points,points):p.co=co;p.handle_left_type='AUTO';p.handle_right_type='AUTO'
 o=bpy.data.objects.new(name,data);collection.objects.link(o);o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.convert(target='MESH');return finish(bpy.context.object,name,mat,bone)
def folded_panel(name,corners,nx,ny,normal,mat='canvas'):
 a,b,c,d=map(Vector,corners);normal=Vector(normal);v=[]
 for j in range(ny+1):
  t=j/ny
  for i in range(nx+1):
   u=i/nx;p=a.lerp(b,u).lerp(d.lerp(c,u),t);p+=normal*(math.sin(u*math.pi)*math.sin(t*math.pi)*(.025*math.sin(i*2.1+j*.8)+.015*math.cos(j*2.7)))
   v.append(tuple(p))
 faces=[]
 for j in range(ny):
  for i in range(nx):k=j*(nx+1)+i;faces.append((k,k+1,k+nx+2,k+nx+1))
 o=mesh(name,v,faces,mat)
 # The lighter inside lining has real reversed faces, avoiding dark back-face shading.
 mesh(name+' inner lining',[tuple(Vector(p)-normal*.014)for p in v],[tuple(reversed(f))for f in faces],'lining')
 return o
# Product view, deliberately preserving the open front and readable desk.
bpy.ops.object.camera_add(location=(-7.6,-11,5.0));cam=bpy.context.object;cam.name='Company terminal inspection camera';cam.data.type='ORTHO';cam.data.ortho_scale=7.5;cam.rotation_euler=(Vector((0,0,1.65))-cam.location).to_track_quat('-Z','Y').to_euler();scene.camera=cam
# Faceted canvas tent with a supported gable and an unobstructed entry.
for side in [-1,1]:
 folded_panel('Canvas side wall',[(side*2.56,-1.86,.08),(side*2.56,1.86,.08),(side*2.14,1.86,2.96),(side*2.14,-1.86,2.96)],7,5,(side,0,.14))
 folded_panel('Canvas roof panel',[(side*2.14,-1.86,2.96),(side*2.14,1.86,2.96),(0,1.86,3.60),(0,-1.86,3.60)],7,4,(side*.30,0,1))
folded_panel('Canvas rear wall',[(-2.56,1.86,.08),(2.56,1.86,.08),(2.14,1.86,2.96),(-2.14,1.86,2.96)],7,5,(0,1,0))
mesh('Rear canvas gable',[(-2.14,1.86,2.96),(2.14,1.86,2.96),(0,1.86,3.6)],[(0,1,2)],'canvas')
mesh('Front canvas gable',[(-2.14,-1.86,2.96),(0,-1.86,3.60),(2.14,-1.86,2.96)],[(0,1,2)],'canvas')
for y in [-1.89,1.89]:
 for x in [-1,1]:
  beam('Yellow sloped tent upright',(x*2.58,y,.08),(x*2.13,y,2.98),.080,'yellow')
  beam('Yellow gable rafter',(x*2.13,y,2.98),(0,y,3.64),.075,'yellow')
  for z in [.40,1.54,2.82]:
   xx=x*(2.58-.45*(z-.08)/2.9);box('Upright clamp',(xx,y-.045,z),(.23,.17,.25),'steel',bevel=.018);bolt((xx,y-.142,z))
  box('Tent anchor shoe',(x*2.60,y,.065),(.50,.38,.13),'dark',bevel=.025)
  for dx in [-.18,.18]:bolt((x*2.60+dx,y,.145),'Z',.034)
beam('Yellow ridge beam',(0,-1.96,3.65),(0,1.96,3.65),.07,'yellow')
for x in [-2.13,2.13]:beam('Eave longitudinal beam',(x,-1.93,2.98),(x,1.93,2.98),.055,'dark')
for y in [-1.1,.85]:
 for x in [-1,1]:beam('Interior frame brace',(x*2.34,y,1.55),(x*2.13,y,2.95),.042,'dark')
# A rolled canvas door, three straps and a warm work light under the eaves.
cyl('Rolled front canvas flap',(0,-1.96,2.95),.18,4.60,'canvas','Flap','X',16)
for x in [-1.72,0,1.72]:
 cyl('Flap restraint strap',(x,-1.96,2.95),.193,.105,'black','Flap','X',16)
 box('Flap strap buckle',(x,-2.155,2.97),(.14,.04,.12),'ingot','Flap',.012)
box('Worklight backing',(0,-.98,2.86),(1.03,.19,.13),'dark',bevel=.025)
box('Warm interior worklight',(0,-.98,2.78),(.83,.13,.045),'lamp',bevel=.014)
for x in [-.48,.48]:box('Worklight end cap',(x,-.98,2.79),(.09,.20,.14),'steel')
# Layered floor mat, broad ribbed tiles and yellow corner protectors.
box('Tent floor mat',(0,-.12,.035),(4.98,4.05,.07),'black',bevel=.035)
for i in range(4):
 for j in range(3):
  x=(i-1.5)*1.20;y=-.20+(j-1)*1.27;box('Rubber floor tile',(x,y,.082),(1.17,1.23,.025),'dark',bevel=.012)
  for k in range(8):box('Floor grip rib',(x+(k-3.5)*.12,y,.100),(.024,1.08,.013),'black',bevel=.003)
for x in [-2.25,2.25]:box('Floor yellow edge guard',(x,-2.14,.095),(.30,.16,.045),'yellow',bevel=.015)
# A-frame desk with real bracing, foot shoes and yellow end trims.
box('Industrial desktop',(0,.43,1.05),(3.22,.96,.14),'steel',bevel=.035)
for x in [-1.54,1.54]:box('Yellow desktop end trim',(x,.43,1.08),(.20,1.01,.17),'yellow',bevel=.025)
for x in [-1.28,1.28]:
 for y in [.13,.74]:beam('Splayed yellow desk leg',(x*1.13,y,.10),(x,y,1.00),.075,'yellow')
 box('Desk anchor shoe',(x*1.13,.43,.10),(.40,.94,.09),'dark')
 for y in [.13,.74]:bolt((x*1.13,y,.16),'Z',.035)
 beam('Desk diagonal brace',(x*1.10,.18,.30),(x*.87,.67,.90),.035,'steel')
beam('Desk rear crossbar',(-1.38,.80,.68),(1.38,.80,.68),.035,'dark')
# Main display and its graph, modelled as separate visible parts for editing.
box('Main monitor foot',(-.48,.60,1.15),(.64,.34,.07),'dark',bevel=.025)
box('Main monitor support',(-.48,.68,1.36),(.16,.13,.44),'steel',bevel=.018)
main_start=len(parts)
box('Main monitor shell',(-.48,.63,1.85),(1.74,.15,1.12),'dark',bevel=.045)
box('Main monitor front bezel',(-.48,.544,1.85),(1.64,.035,1.03),'yellow',bevel=.03)
box('Main monitor screen',(-.48,.518,1.85),(1.49,.015,.88),'screen',bevel=.02)
for x in [-1.24,.28]:
 for z in [1.39,2.31]:bolt((x,.509,z),'Y',.023)
nodes=[(-1.05,1.85),(-.72,2.08),(-.72,1.64),(-.36,1.86),(-.03,2.08),(-.03,1.64)]
for ia,ib in [(0,1),(0,2),(1,3),(2,3),(3,4),(3,5)]:
 a=nodes[ia];b=nodes[ib];beam('Upgrade graph branch',(a[0],.499,a[1]),(b[0],.499,b[1]),.006,'cyan' if ib<4 else 'gold')
for idx,(x,z) in enumerate(nodes):
 for i in range(6):
  a=i*math.tau/6;b=(i+1)*math.tau/6;beam('Upgrade node outline',(x+math.cos(a)*.078,.489,z+math.sin(a)*.078),(x+math.cos(b)*.078,.489,z+math.sin(b)*.078),.006,'cyan' if idx<4 else 'gold')
 box('Upgrade node symbol',(x,.481,z),(.045,.008,.045),'cyan' if idx<4 else 'gold',bevel=.006)
for x in [-1.12,-.98,-.84]:box('Display header tick',(x,.49,2.23),(.09,.008,.012),'cyan',bevel=0)
# A slight backwards tilt is applied to the entire monitor assembly, including graphic geometry.
centre=Vector((-.48,.63,1.85));rotation=Matrix.Rotation(math.radians(-7),4,'X')
for o in parts[main_start:]:
 for v in o.data.vertices:v.co=centre+rotation@(v.co-centre)
# Smaller market monitor with line graph and grid.
box('Market display foot',(.90,.58,1.15),(.44,.28,.06),'steel')
box('Market display neck',(.90,.62,1.30),(.10,.10,.26),'dark')
box('Market display casing',(.90,.63,1.60),(.78,.13,.58),'yellow',bevel=.035)
box('Market display screen',(.90,.552,1.60),(.66,.018,.46),'screen',bevel=.015)
for x in [.66,.88,1.10]:beam('Market chart grid',(x,.537,1.42),(x,.537,1.78),.003,'steel')
for z in [1.47,1.60,1.73]:beam('Market chart grid',(.61,.537,z),(1.19,.537,z),.003,'steel')
chart=[(.63,1.48),(.70,1.57),(.77,1.52),(.84,1.67),(.91,1.61),(.98,1.70),(1.06,1.64),(1.16,1.74)]
for a,b in zip(chart,chart[1:]):beam('Market price trace',(a[0],.526,a[1]),(b[0],.526,b[1]),.007,'gold')
box('Market current price light',(1.16,.517,1.74),(.022,.012,.022),'gold','Ticker',.004)
# Keyboard, tactile keys, trackball and desktop radio.
box('Keyboard chassis',(-.40,.10,1.16),(1.16,.38,.06),'yellow',bevel=.02)
box('Keyboard recessed plate',(-.40,.10,1.195),(1.07,.32,.016),'dark',bevel=.012)
for row in range(4):
 for column in range(13):
  if row==3 and 4<=column<=8:continue
  box('Keyboard key',(-.88+column*.079,-.012+row*.073,1.22),(.065,.057,.025),'ingot',bevel=.006)
box('Keyboard space bar',(-.40,.207,1.22),(.35,.055,.025),'ingot',bevel=.006)
box('Trackball base',(.51,.07,1.17),(.32,.30,.08),'dark',bevel=.035)
bpy.ops.mesh.primitive_uv_sphere_add(segments=12,ring_count=6,radius=.105,location=(.51,.07,1.24));finish(bpy.context.object,'Trackball','ingot')
box('Desktop radio',(-1.30,.60,1.37),(.30,.27,.48),'dark',bevel=.025)
box('Radio cyan display',(-1.30,.45,1.47),(.21,.02,.085),'cyan',bevel=.006)
for z in [1.26,1.33]:cyl('Radio rotary knob',(-1.30,.435,z),.030,.04,'yellow',axis='Y',vertices=8)
beam('Radio antenna',(-1.39,.63,1.60),(-1.39,.63,2.11),.012,'ingot','Antenna')
box('Desk auxiliary control pad',(-1.18,.08,1.17),(.24,.25,.06),'yellow',bevel=.015)
# Under-desk computer, removable panels, ventilation and cable management.
box('Computer tower',(.65,.64,.50),(.65,.62,.87),'dark',bevel=.04)
box('Computer front panel',(.65,.31,.50),(.54,.04,.75),'steel',bevel=.025)
box('Computer service drawer',(.61,.278,.73),(.36,.025,.09),'black',bevel=.015)
box('Tower cyan status strip',(.84,.274,.45),(.035,.015,.42),'cyan',bevel=.005)
for z in [.27,.33,.39,.45,.51,.57]:box('Computer ventilation slot',(.60,.273,z),(.29,.017,.025),'black',bevel=.003)
for x in [.43,.87]:
 for z in [.18,.82]:bolt((x,.267,z),'Y',.021)
hose('Main display cable',[(-.48,.76,1.66),(-.48,.95,1.32),(-.25,.95,.75),(.40,.92,.60)],.018)
hose('Market display cable',[(.90,.72,1.47),(1.00,.87,1.12),(.90,.92,.80)],.015)
hose('Keyboard cable',[(-.40,.29,1.16),(-.55,.88,1.12),(-.15,.99,.30),(.41,.92,.32)],.012)
hose('Station power cable',[(.88,.92,.24),(1.28,1.08,.20),(1.60,1.74,.12)],.022)
# Reinforced supply crate against the back right, clear of the entry.
box('Supply crate body',(1.63,1.18,.45),(1.0,.72,.76),'dark',bevel=.07)
box('Supply crate lid',(1.63,1.18,.86),(1.03,.75,.14),'yellow',bevel=.035)
for x in [1.25,2.01]:
 box('Crate yellow corner strap',(x,1.18,.46),(.12,.77,.82),'yellow',bevel=.015)
 box('Crate latch',(x,.775,.73),(.10,.045,.18),'ingot',bevel=.012)
box('Crate carry handle',(1.63,.782,.53),(.28,.035,.10),'steel',bevel=.015)
# Flat canvas utility pocket on the visible left wall, with binding strips.
mesh('Exterior canvas pocket',[(-2.41,-.72,1.04),(-2.41,.13,1.04),(-2.34,.13,1.56),(-2.34,-.72,1.56)],[(0,1,2,3)],'canvas')
beam('Pocket upper binding',(-2.36,-.74,1.55),(-2.36,.15,1.55),.022,'black')
# Small idle mechanisms and an explicit interaction marker for the engine.
box('Interaction marker plate',(0,-.38,1.00),(.12,.05,.025),'dark','TerminalInteract',.005)
spec={'Flap':((0,-1.96,2.95),(.30,-1.96,2.95)),'Antenna':((-1.39,.63,1.60),(-1.39,.63,1.90)),'Ticker':((1.16,.517,1.74),(1.16,.517,1.84)),'TerminalInteract':((0,-1.45,.10),(0,-1.45,.30))}
arm=bpy.data.armatures.new('CompanyTerminalRig');rig=bpy.data.objects.new('CompanyTerminalRig',arm);collection.objects.link(rig);bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT');root=arm.edit_bones.new('Root');root.head=(0,0,0);root.tail=(0,0,.3)
for key,(a,b) in spec.items():bone=arm.edit_bones.new(key);bone.head=a;bone.tail=b;bone.parent=root
bpy.ops.object.mode_set(mode='OBJECT')
for o in parts:mod=o.modifiers.new('Rigid assembly animation','ARMATURE');mod.object=rig;o.parent=rig
rig.animation_data_create();action=bpy.data.actions.new('Company Terminal Operate');rig.animation_data.action=action;scene.render.fps=30;scene.frame_start=1;scene.frame_end=121
for key in spec:
 p=rig.pose.bones[key];p.rotation_mode='XYZ'
 for f in range(1,122):
  t=(f-1)/120;p.rotation_euler=(0,0,0);p.location=(0,0,0)
  if key=='Flap':p.rotation_euler[1]=.018*math.sin(t*math.tau)
  if key=='Antenna':p.rotation_euler[0]=.018*math.sin(t*math.tau)
  if key=='Ticker':p.location[1]=.008*math.sin(t*math.tau)
  p.keyframe_insert(data_path='location',frame=f);p.keyframe_insert(data_path='rotation_euler',frame=f)
for layer in action.layers:
 for strip in layer.strips:
  for bag in getattr(strip,'channelbags',[]):
   for curve in bag.fcurves:
    for p in curve.keyframe_points:p.interpolation='LINEAR'
scene.frame_set(1)
# Studio floor and lighting are source-only, not part of the game model.
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.025));floor=bpy.context.object;floor.name='Presentation studio floor';floor.data.materials.append(mats['steel'])
scene.world=bpy.data.worlds.new('Terminal studio');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.40
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.render.resolution_x=1300;scene.render.resolution_y=1000;scene.render.resolution_percentage=100;scene.render.image_settings.color_mode='RGBA';scene.render.filepath=str(OUT/'CompanyTerminal-preview.png')
for pos,power,size in [((-3,-6,7),1200,5),((5,-3,5),1000,4),((0,4,6),1100,3)]:
 bpy.ops.object.light_add(type='AREA',location=pos);lamp=bpy.context.object;lamp.data.energy=power;lamp.data.size=size;lamp.rotation_euler=(Vector((0,0,1.5))-lamp.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.light_add(type='AREA',location=(0,-.6,2.70));lamp=bpy.context.object;lamp.data.energy=65;lamp.data.color=(1,.82,.52);lamp.data.size=1.2
scene.view_settings.view_transform='AgX';scene.view_settings.exposure=-.2
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'CompanyTerminal.blend'))
result={'parts':len(parts),'triangles':sum(len(p.vertices)-2 for o in parts for p in o.data.polygons),'bones':len(arm.bones),'scene':scene.name,'source':str(OUT/'CompanyTerminal.blend')}
