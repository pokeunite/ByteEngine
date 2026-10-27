"""Original low-poly mining equipment. Run with Blender 5 --background --python.
Creates editable blend sources, rigidly skinned GLBs and a rendered inspection sheet.
"""
import bpy, math, json
from pathlib import Path
from mathutils import Vector
OUT=Path(__file__).resolve().parents[2]/'artifacts/mining-models'
OUT.mkdir(parents=True,exist_ok=True)
palette={
 'yellow':(.56,.25,.018,1),'dark':(.018,.027,.037,1),'cream':(.55,.53,.40,1),
 'steel':(.12,.17,.20,1),'canvas':(.095,.14,.045,1),'cyan':(.05,.6,.55,1),
 'orange':(.95,.25,.025,1),'ingot':(.55,.57,.53,1),'black':(.025,.03,.025,1)}
parts=[];spec={};mats={}
def material(name,color):
 m=bpy.data.materials.new(name);m.diffuse_color=color;m.use_nodes=True
 p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=color;p.inputs['Roughness'].default_value=.52;p.inputs['Metallic'].default_value=.45 if name in ['steel','dark','ingot'] else .12
 if name in ['cyan','orange']:p.inputs['Emission Color'].default_value=color;p.inputs['Emission Strength'].default_value=.7
 m.use_backface_culling=False;return m
def finish(o,name,mat,bone='Root',bevel=0):
 o.name=name;o.data.materials.append(mats[mat]);bpy.context.view_layer.objects.active=o
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if bevel:
  mod=o.modifiers.new('Broad edge bevel','BEVEL');mod.width=bevel;mod.segments=1
  bpy.ops.object.modifier_apply(modifier=mod.name)
 # Bake every part into model-space; skin weights then stay rigid and exact.
 bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
 o.vertex_groups.new(name=bone).add(list(range(len(o.data.vertices))),1,'REPLACE');parts.append(o);return o
def box(name,p,size,mat='yellow',bone='Root',bevel=.035):
 bpy.ops.mesh.primitive_cube_add(size=1,location=p);o=bpy.context.object;o.scale=size;return finish(o,name,mat,bone,bevel)
def cyl(name,p,r,depth,mat='steel',bone='Root',axis='Z',vertices=12):
 bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=depth,location=p)
 o=bpy.context.object
 if axis=='X':o.rotation_euler[1]=math.pi/2
 if axis=='Y':o.rotation_euler[0]=math.pi/2
 return finish(o,name,mat,bone)
def beam(name,a,b,r,mat='dark',bone='Root'):
 a,b=Vector(a),Vector(b);bpy.ops.mesh.primitive_cylinder_add(vertices=6,radius=r,depth=(b-a).length,location=(a+b)/2)
 o=bpy.context.object;o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();return finish(o,name,mat,bone)
def mesh(name,vertices,faces,mat,bone='Root'):
 data=bpy.data.meshes.new(name);data.from_pydata(vertices,[],faces);data.update();o=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(o);return finish(o,name,mat,bone)
def hopper(p,size):
 x,y,z=p;w,d,h=size;v=[(x+a*w*s/2,y+b*d*s/2,z+c*h)for c,s in [(0,.5),(1,1)]for a,b in [(-1,-1),(1,-1),(1,1),(-1,1)]]
 mesh('Open ore hopper',v,[(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(0,3,2,1)],'yellow')
 for i in range(4):beam('Hopper rolled rim',v[4+i],v[4+(i+1)%4],.035,'dark')
 box('Hopper recessed throat',(x,y,z+.06),(w*.42,d*.42,.04),'dark',bevel=0)
def fittings(name):
 """Functional secondary forms rather than scattered decorative blocks."""
 if name=='MiningDrill':
  for x in [-.95,.95]:
   box('Sliding drive rail',(x,0,1.72),(.18,.24,1.95),'dark')
   cyl('Chrome guide piston',(x,-.2,1.4),.055,1.55,'ingot')
   box('Rail slide collar',(x,-.1,1.45),(.32,.45,.32),'cream')
   for y in [-.58,.58]:box('Gantry armour cheek',(x,y,2.63),(.44,.25,.58),'yellow',bevel=.07)
  cyl('Drive gearbox',(0,0,2.64),.42,.38,'dark',vertices=10)
  cyl('Gearbox flange',(0,0,2.4),.48,.12,'cream',vertices=12)
  box('Auxiliary motor',(.65,.22,3.1),(.65,.85,.52),'dark',bevel=.08)
  for y in [-.04,.12,.28,.44]:box('Motor cooling fin',(.98,y,3.1),(.035,.045,.4),'steel',bevel=0)
  for x in [-.43,.43]:
   box('Motor service panel',(x,-.55,3.15),(.25,.04,.46),'yellow',bevel=.02)
   for z in [2.99,3.31]:cyl('Service bolt',(x,-.59,z),.035,.035,'ingot',axis='Y',vertices=6)
  for side in [-1,1]:
   for a,b in [((side*.65,.65,3.2),(side*.8,.75,2.6)),((side*.8,.75,2.6),(side*.82,.75,1.25))]:beam('Hydraulic hose',a,b,.045,'dark')
  box('Hopper support',(1.25,.1,.85),(.4,.5,.35),'dark');cyl('Hopper feed motor',(1.6,.1,.75),.19,.58,'steel',axis='Y')
  for x in [-1.25,1.25]:
   for y in [-.95,.95]:
    box('Foot anchor pad',(x,y,.30),(.35,.35,.08),'yellow')
    cyl('Anchor pin',(x,y,.36),.065,.12,'ingot',vertices=6)
 elif name=='AmmoPress':
  box('Press left cabinet',(-.75,.35,.7),(.42,.7,1),'yellow',bevel=.06)
  box('Cabinet inset door',(-.97,.35,.7),(.03,.52,.72),'dark',bevel=.015)
  for z in [.5,.62,.74]:box('Cabinet vent',(-.99,.35,z),(.02,.35,.04),'steel',bevel=0)
  box('Press head housing',(-.25,.15,2.17),(.75,.82,.38),'dark',bevel=.05)
  for x in [-.57,.15]:cyl('Ram guide',(x,.16,1.6),.05,1.22,'ingot')
  box('Ram die',(-.22,0,.82),(.4,.4,.12),'dark','Ram',.02)
  cyl('Drive shaft',(.68,.1,1.45),.13,.75,'ingot',axis='X')
  for x in [-.95,.05]:box('Belt side rail',(x,-.7,.68),(.08,1.15,.16),'yellow')
  for y in [-1.2,-.95,-.7,-.45]:cyl('Conveyor roller',(-.5,y,.56),.09,1,'steel',axis='X',vertices=8)
  for x in [-.96,-.06]:box('Bin side',(x,-1.5,.48),(.1,.5,.38),'yellow')
  box('Bin front',(-.5,-1.77,.48),(1,.07,.38),'yellow')
  hopper((.30,.35,2.63),(.85,.85,.45))
  for x in [-.6,.2]:
   for y in [-.4,.4]:cyl('Head fastener',(x,y,2.66),.035,.04,'ingot',vertices=6)
 elif name=='Refinery':
  for x in [-.85,.85]:
   for y in [-.5,.5]:beam('Bearing bracket',(x,y,.35),(x,y,1.35),.065,'dark')
  for x in [-.7,.7]:
   cyl('Stack heat shield',(x,.7,2.12),.28,.5,'dark',vertices=8)
   for z in [1.88,2.36]:cyl('Stack retaining ring',(x,.7,z),.31,.08,'yellow',vertices=8)
  box('Furnace inspection frame',(.15,-.76,1.1),(.88,.15,.5),'dark')
  box('Furnace glow',(.15,-.845,1.1),(.62,.025,.27),'orange',bevel=.01)
  for x in [-.05,.15,.35]:box('Inspection grate',(x,-.865,1.1),(.035,.025,.28),'steel',bevel=0)
  for x in [-.84,.84]:
   for i in range(8):
    a=i*math.tau/8;cyl('Collar rivet',(x+(.13 if x>0 else -.13),math.cos(a)*.67,1.12+math.sin(a)*.67),.035,.035,'ingot',axis='X',vertices=6)
  for a,b in [((-1.4,.6,.7),(-1.4,1,.7)),((-1.4,1,.7),(.9,1,.7)),((.9,1,.7),(.9,1,1.7))]:beam('Coolant pipe',a,b,.07,'dark')
  for x in [-.5,.5]:box('Drum brace',(x,0,1.86),(.10,1.05,.12),'cream')
  for x in [-.35,.95]:box('Output tray lip',(x,-1.05,.6),(.08,.85,.18),'yellow')
 elif name=='CompanyTent':
  for x in [-1.86,1.86]:
   for y in [-1.4,1.4]:
    box('Tent anchor foot',(x,y,.08),(.3,.3,.12),'dark')
    beam('Guy cable',(x*.86,y,2.1),(x*1.2,y*1.12,.05),.012,'cream')
  for x in [-1.64,1.64]:beam('Interior rib',(x,-1.55,2.3),(x,1.55,2.3),.035,'dark')
  box('Computer tower',(.75,.7,.4),(.38,.65,.68),'dark')
  for y in [.49,.6,.71,.82]:box('Computer vent',(.96,y,.52),(.02,.045,.26),'steel',bevel=0)
  for x in [-.45,-.28,-.11,.06]:
   for y in [.43,.49,.55]:box('Keyboard key',(x,y,1.026),(.12,.04,.018),'cream',bevel=0)
  box('Communications cabinet',(1.2,1.15,.52),(.55,.6,1),'dark')
  box('Radio readout',(1.2,.835,.7),(.35,.025,.20),'cyan',bevel=.01)
  cyl('Radio dial',(1.05,.8,.45),.06,.035,'cream',axis='Y',vertices=8)
  beam('Cable from antenna',(1.25,1.25,.12),(1.9,.85,.12),.025,'dark')
  box('Interior work lamp',(-.9,.9,2.12),(.5,.12,.08),'cream')
  for z in [.22,.65]:
   for x in [-1.48,-1.02]:box('Crate reinforcement',(x,.4,z),(.045,.73,.4),'dark',bevel=0)
def drill():
 spec.update(Auger=((0,0,.35),(0,0,1.1)))
 for x in [-1.25,1.25]:
  for y in [-.95,.95]:
   box('Wide stabilizer foot',(x,y,.14),(.75,.65,.28),'dark')
   beam('Angled leg',(x,y,.28),(x*.68,y*.63,2.8),.11,'yellow')
 for y in [-.60,.60]:box('Upper gantry',(0,y,2.7),(2,.22,.28),'yellow')
 for x in [-.95,.95]:box('Gantry side',(x,0,2.7),(.22,1.5,.28),'cream')
 box('Motor casing',(0,0,3.15),(1.25,1.05,.65),'yellow',bevel=.08)
 for i in range(3):box('Motor vent',(0,-.533,3.02+i*.13),(.72,.025,.05),'dark',bevel=0)
 cyl('Drive spindle',(0,0,1.5),.18,2.55,'steel','Auger')
 verts=[];faces=[];steps=96
 for i in range(steps+1):
  t=i/steps;a=t*math.tau*4.7;z=.18+t*2.1
  for radius,dz in [(.19,0),(.57,0),(.57,.07),(.19,.07)]:verts.append((math.cos(a)*radius,math.sin(a)*radius,z+dz))
 for i in range(steps):
  for k in range(4):faces.append((i*4+k,i*4+(k+1)%4,(i+1)*4+(k+1)%4,(i+1)*4+k))
 mesh('Continuous spiral auger',verts,faces,'steel','Auger')
 hopper((1.55,.1,1.0),(1.0,1.05,.65));box('Control console',(-1.15,-.9,1.3),(.5,.23,.55),'dark');box('Screen',(-1.15,-1.025,1.34),(.34,.025,.3),'cyan')
def press():
 spec.update(Flywheel=((.84,.1,1.45),(1.34,.1,1.45)),Ram=((-.22,0,1.1),(-.22,0,1.6)))
 box('Press base',(0,0,.15),(2.3,1.45,.3),'dark');box('C frame back',(.45,.25,1.3),(.42,.7,2.4),'yellow',bevel=.075)
 box('Press upper',(-.1,.05,2.45),(1.6,1,.35),'yellow',bevel=.07);box('Anvil',(-.28,0,.57),(1.1,.85,.35),'cream')
 cyl('Piston',(-.22,0,1.65),.13,1.1,'steel','Ram');box('Press ram',(-.22,0,1.0),(.65,.62,.3),'yellow','Ram')
 # Spokes are separate geometry rigidly weighted to the flywheel bone.
 cyl('Flywheel hub',(.88,.1,1.45),.18,.32,'yellow','Flywheel','X')
 for i in range(12):
  a=i*math.tau/12;b=(i+1)*math.tau/12
  beam('Wheel rim',(.89,.1+math.cos(a)*.68,1.45+math.sin(a)*.68),(.89,.1+math.cos(b)*.68,1.45+math.sin(b)*.68),.08,'dark','Flywheel')
 for i in range(5):
  a=i*math.tau/5;beam('Wheel spoke',(.89,.1,1.45),(.89,.1+math.cos(a)*.66,1.45+math.sin(a)*.66),.045,'steel','Flywheel')
 beam('Console support',(-.45,-.05,2.25),(-.68,-.48,1.9),.055,'steel')
 box('Output belt',(-.5,-.65,.54),(1.25,1.1,.15),'dark')
 for y in [-.75,-1,-1.25]:
  cyl('Supply cartridge',(-.5,y,.71),.09,.32,'cream',axis='X',vertices=8)
 box('Collection bin',(-.5,-1.5,.28),(1.35,.5,.45),'yellow');box('Controls',(-.68,-.52,1.9),(.4,.12,.3),'dark');box('Status screen',(-.68,-.59,1.9),(.29,.02,.2),'cyan')
def refinery():
 spec.update(Drum=((0,0,1.0),(.5,0,1.0)),Output=((.4,-1,.55),(.4,-1,1.05)))
 box('Skid',(0,0,.15),(3.7,2.1,.3),'dark')
 cyl('Processing drum',(0,0,1.12),.72,1.85,'steel','Drum','X',vertices=12)
 for x in [-.85,.85]:cyl('Yellow drum collar',(x,0,1.12),.79,.22,'yellow',axis='X')
 for y in [-.28,.28]:box('Drum length rib',(0,y,1.75),(1.6,.10,.12),'cream','Drum')
 for x in [-.8,.8]:box('Bearing stand',(x,0,.66),(.25,1.5,.9),'yellow')
 hopper((-1.4,.1,1.2),(.9,1.4,.8));box('Hopper feeder',(-1.15,.1,.9),(.6,.75,.5),'dark')
 for x in [-.55,.65]:
  cyl('Exhaust stack',(x,.7,1.98),.22,1.5,'cream',vertices=8);cyl('Stack cap',(x,.7,2.72),.26,.18,'dark',vertices=8)
 box('Orange inspection port',(.15,-.723,1.1),(.65,.03,.3),'orange')
 box('Output tray',(.35,-1.05,.48),(1.4,.8,.15),'yellow')
 for x in [-.1,.35,.8]:box('Processed ingot',(x,-1.04,.65),(.35,.5,.17),'ingot','Output',.04)
 box('Refinery controls',(1.2,-.7,1.0),(.42,.2,.52),'dark');box('Control screen',(1.2,-.81,1.04),(.3,.02,.3),'cyan')
def tent():
 spec.update(Antenna=((2,.8,1.4),(2,.8,2)),Flap=((0,-1.55,2.5),(0,-1.55,3)))
 box('Tent groundsheet',(0,0,.025),(4.2,3.5,.05),'dark',bevel=0)
 # Open front faces Blender -Y. Sides slope inward to the gabled roof.
 mesh('Canvas shell',[(-2,-1.65,0),(-1.65,-1.65,2.35),(0,-1.65,3),(1.65,-1.65,2.35),(2,-1.65,0),(-2,1.65,0),(-1.65,1.65,2.35),(0,1.65,3),(1.65,1.65,2.35),(2,1.65,0)],[(0,5,6,1),(1,6,7,2),(2,7,8,3),(3,8,9,4),(5,9,8,7,6)],'canvas')
 for y in [-1.66,1.66]:
  for a,b in [((-2,y,.08),(-1.65,y,2.35)),((-1.65,y,2.35),(0,y,3)),((0,y,3),(1.65,y,2.35)),((1.65,y,2.35),(2,y,.08))]:beam('Tent support',a,b,.055,'yellow')
 cyl('Rolled front flap',(0,-1.66,2.46),.14,3.3,'canvas','Flap','X')
 box('Folding desktop',(0,.85,.92),(2.35,.8,.12),'cream')
 for x in [-1,1]:
  for y in [.55,1.1]:beam('Desk leg',(x,y,0),(x,y,.92),.035,'steel')
 box('Computer base',(-.22,.8,1.05),(.55,.36,.12),'dark');box('Monitor',(-.22,.98,1.42),(.9,.14,.67),'cream',bevel=.06);box('Live market screen',(-.22,.897,1.42),(.75,.025,.5),'black',bevel=0)
 points=[(-.55,.875,1.28),(-.43,.875,1.38),(-.29,.875,1.31),(-.16,.875,1.52),(-.03,.875,1.41),(.1,.875,1.57)]
 for a,b in zip(points,points[1:]):beam('Market chart trace',a,b,.012,'cyan')
 box('Keyboard',(-.22,.48,1.005),(.65,.23,.035),'dark');box('Radio',(.72,.88,1.18),(.42,.34,.4),'dark');beam('Radio aerial',(.8,.9,1.4),(.8,.9,1.95),.014,'steel')
 box('Chair seat',(.1,-.2,.45),(.65,.6,.1),'canvas');box('Chair back',(.1,-.48,.82),(.65,.08,.65),'canvas')
 for x in [-.18,.38]:beam('Chair frame',(x,-.4,.05),(x,.05,.5),.027,'steel');beam('Chair frame',(x,.05,.05),(x,-.4,.5),.027,'steel')
 for z in [.22,.65]:box('Supply crate',(-1.25,.4,z),(.65,.7,.4),'yellow')
 beam('Antenna mast',(2,.8,.1),(2,.8,3.4),.035,'steel','Antenna');box('Antenna panel',(2,.8,2.5),(.16,.28,.55),'cream','Antenna');box('Antenna tip',(2,.8,3.4),(.08,.09,.25),'yellow','Antenna')
def rig_export(name,build):
 global parts,spec,mats
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
 for action in list(bpy.data.actions):bpy.data.actions.remove(action,do_unlink=True)
 parts=[];spec={};mats={k:material(k,v)for k,v in palette.items()};build();fittings(name)
 arm=bpy.data.armatures.new(name+'Rig');rig=bpy.data.objects.new(name+'Rig',arm);bpy.context.collection.objects.link(rig);bpy.context.view_layer.objects.active=rig;rig.select_set(True)
 bpy.ops.object.mode_set(mode='EDIT');root=arm.edit_bones.new('Root');root.head=(0,0,0);root.tail=(0,0,.3)
 for key,(a,b) in spec.items():bone=arm.edit_bones.new(key);bone.head=a;bone.tail=b;bone.parent=root
 bpy.ops.object.mode_set(mode='OBJECT');bpy.ops.object.select_all(action='DESELECT')
 for obj in parts:obj.select_set(True)
 bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();body=bpy.context.object;body.name=name+'Mesh'
 mod=body.modifiers.new('Rigid part animation','ARMATURE');mod.object=rig;body.parent=rig
 rig.animation_data_create();action=bpy.data.actions.new('Operate');rig.animation_data.action=action
 bpy.context.scene.render.fps=30;bpy.context.scene.frame_start=1;bpy.context.scene.frame_end=61
 for bone_name in spec:
  pose=rig.pose.bones[bone_name];pose.rotation_mode='XYZ'
  for f in range(1,62,5):
   t=(f-1)/60;pose.rotation_euler=(0,0,0);pose.location=(0,0,0)
   if bone_name in ['Auger','Flywheel','Drum']:pose.rotation_euler[1]=math.tau*t*(2 if bone_name=='Flywheel' else 1)
   if bone_name=='Ram':pose.location[1]=.24*math.sin(t*math.tau*2)
   if bone_name=='Output':pose.location[2]=.07*math.sin(t*math.tau)
   if bone_name=='Antenna':pose.rotation_euler[2]=.035*math.sin(t*math.tau)
   if bone_name=='Flap':pose.rotation_euler[0]=.045*math.sin(t*math.tau)
   pose.keyframe_insert(data_path='rotation_euler',frame=f);pose.keyframe_insert(data_path='location',frame=f)
 for layer in action.layers:
  for strip in layer.strips:
   for bag in getattr(strip,'channelbags',[]):
    for curve in bag.fcurves:
     for point in curve.keyframe_points:point.interpolation='LINEAR'
 bpy.context.scene.frame_set(1)
 bpy.ops.object.select_all(action='DESELECT');body.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
 bpy.ops.wm.save_as_mainfile(filepath=str(OUT/(name+'.blend')))
 bpy.ops.export_scene.gltf(filepath=str(OUT/(name+'.glb')),export_format='GLB',use_selection=True,export_animations=True,export_force_sampling=True,export_animation_mode='ACTIONS')
 count=sum(len(p.vertices)-2 for p in body.data.polygons);print('ASSET',name,'triangles',count,'bones',len(arm.bones),flush=True)
 # Separate inspection renders keep the source assets uncluttered.
 bpy.ops.object.camera_add(location=(6,-10,3.8) if name=='CompanyTent' else (6,-8,5.5));cam=bpy.context.object;cam.rotation_euler=(Vector((0,0,1.1))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=6.2;scene=bpy.context.scene;scene.camera=cam
 scene.render.engine='CYCLES';scene.cycles.samples=24;scene.render.resolution_x=900;scene.render.resolution_y=900;scene.render.resolution_percentage=100
 scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.15,.15,.15,1);scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.4
 for p,power,size in [((3,-4,7),650,5),((-4,-1,4),350,4),((1,4,6),600,3)]:
  bpy.ops.object.light_add(type='AREA',location=p);lamp=bpy.context.object;lamp.data.energy=power;lamp.data.shape='DISK';lamp.data.size=size;lamp.rotation_euler=(Vector((0,0,1))-lamp.location).to_track_quat('-Z','Y').to_euler()
 scene.render.filepath=str(OUT/(name+'-preview.png'));scene.view_settings.view_transform='Standard';scene.view_settings.look='None';scene.view_settings.exposure=-.2;scene.render.film_transparent=True;scene.render.image_settings.color_mode='RGBA';bpy.ops.render.render(write_still=True)
 bpy.ops.wm.save_as_mainfile(filepath=str(OUT/(name+'.blend')))
 report.append({'name':name,'triangles':count,'bones':len(arm.bones),'animation':'Operate','seconds':2})
report=[]
for name,build in [('MiningDrill',drill),('AmmoPress',press),('Refinery',refinery),('CompanyTent',tent)]:rig_export(name,build)
(OUT/'model-report.json').write_text(json.dumps(report,indent=2))
print('MINING MODELS COMPLETE',flush=True)
