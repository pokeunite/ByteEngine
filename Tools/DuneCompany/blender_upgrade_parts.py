"""Editable Blender reconstruction of the approved industrial upgrade reference.
Run Blender --background --python this_file. Outputs staged GLBs and previews.
"""
import bpy, math, json, random
from pathlib import Path
from mathutils import Vector
ROOT=Path('C:/Users/codex/ByteEngine')
OUT=ROOT/'.artifacts/blender-upgrades';OUT.mkdir(parents=True,exist_ok=True)
random.seed(34)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
scene=bpy.context.scene
scene.render.engine='CYCLES';scene.cycles.samples=24
scene.render.resolution_x=640;scene.render.resolution_y=640;scene.render.resolution_percentage=100
scene.render.film_transparent=True
scene.world.color=(.35,.35,.35)
scene.view_settings.view_transform='AgX'
materials={}
for name,color,metal,rough in [('Ivory',(.52,.45,.32),.65,.48),('Olive',(.13,.16,.11),.7,.43),('Orange',(.68,.19,.045),.4,.45),('Steel',(.19,.20,.18),.85,.35),('Rubber',(.025,.023,.018),.05,.8),('Cable',(.12,.13,.12),.8,.45)]:
 mat=bpy.data.materials.new(name);mat.diffuse_color=(*color,1);mat.use_nodes=True
 bs=mat.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*color,1);bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=rough
 # Embedded image texture survives GLB import, unlike Blender-only procedural nodes.
 img=bpy.data.images.new(name+'_weathered',256,256);pixels=[]
 for y in range(256):
  for x in range(256):
   noise=random.uniform(.88,1.07);chip=random.random()<(.014 if name not in ('Rubber','Cable') else .003)
   c=(.21,.19,.14) if chip else color
   pixels.extend([min(1,v*noise) for v in c]+[1])
 # Larger clustered paint chips and hairline scuffs are visible at game scale.
 for _ in range(180 if name not in ('Cable','Rubber') else 40):
  cx=random.randrange(256);cy=random.randrange(256);radius=random.randrange(1,5)
  for yy in range(max(0,cy-radius),min(256,cy+radius)):
   for xx in range(max(0,cx-radius),min(256,cx+radius)):
    if (xx-cx)**2+(yy-cy)**2<radius**2 and random.random()<.8:
     idx=(yy*256+xx)*4;pixels[idx:idx+3]=[.20,.17,.12] if name!='Rubber' else [.05,.04,.025]
 for _ in range(45):
  x=random.randrange(256);y=random.randrange(256)
  for j in range(random.randrange(3,20)):
   xx=(x+j)%256;yy=(y+j//3)%256;idx=(yy*256+xx)*4;pixels[idx:idx+3]=[.29,.27,.21]
 img.pixels=pixels;img.pack()
 tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=img;mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
 materials[name]=mat
objects=[];moving=[]
def finish(o,name,mat,bevel=0,move=False):
 o.name=name;o.data.materials.append(materials[mat]);objects.append(o)
 if move:moving.append(o)
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if bevel:
  m=o.modifiers.new('Machined edge bevel','BEVEL');m.width=bevel;m.segments=2
  bpy.ops.object.modifier_apply(modifier=m.name)
 if o.type=='MESH':
  for poly in o.data.polygons:poly.use_smooth=len(poly.vertices)==4 and name.startswith(('shaft','drum','hose','turbo','rim'))
 return o
def box(name,p,s,mat='Olive',b=.012,move=False):
 bpy.ops.mesh.primitive_cube_add(size=1,location=p);o=bpy.context.object;o.dimensions=s;return finish(o,name,mat,b,move)
def cyl(name,p,r,d,mat='Steel',axis='Z',n=24,move=False):
 bpy.ops.mesh.primitive_cylinder_add(vertices=n,radius=r,depth=d,location=p);o=bpy.context.object
 o.rotation_euler[1 if axis=='X' else 0]=math.pi/2 if axis!='Z' else 0
 o=finish(o,name,mat,.003,move)
 if n>8:
  for p in o.data.polygons:p.use_smooth=len(p.vertices)==4
 return o
def torus(name,p,major,minor,mat='Steel',axis='Z',move=False):
 bpy.ops.mesh.primitive_torus_add(major_segments=32,minor_segments=8,major_radius=major,minor_radius=minor,location=p);o=bpy.context.object
 o.rotation_euler[1 if axis=='X' else 0]=math.pi/2 if axis!='Z' else 0
 o=finish(o,name,mat,0,move)
 for p in o.data.polygons:p.use_smooth=True
 return o
def pipe(name,points,r,mat='Steel',segments=3,move=False):
 curve=bpy.data.curves.new(name,'CURVE');curve.dimensions='3D';curve.resolution_u=1;curve.bevel_depth=r;curve.bevel_resolution=segments
 spline=curve.splines.new('POLY');spline.points.add(len(points)-1)
 for p,v in zip(spline.points,points):p.co=(*v,1)
 o=bpy.data.objects.new(name,curve);bpy.context.collection.objects.link(o);bpy.context.view_layer.objects.active=o;o.select_set(True)
 for item in bpy.context.selected_objects:
  if item!=o:item.select_set(False)
 bpy.ops.object.convert(target='MESH');o=finish(o,name,mat,0,move)
 for p in o.data.polygons:p.use_smooth=True
 return o
def bolts_ring(p,r,count=10,axis='X',move=False):
 for i in range(count):
  a=math.tau*i/count;v=list(p)
  if axis=='X':v[1]+=r*math.cos(a);v[2]+=r*math.sin(a)
  else:v[0]+=r*math.cos(a);v[1]+=r*math.sin(a)
  cyl('hex bolt',v,.015,.025,'Steel',axis,6,move)
def base(w=.75,d=.62):
 box('Ivory mounting skid',(0,0,.035),(w,d,.07),'Ivory',.018)
 for x in [-w/2+.06,w/2-.06]:
  for y in [-d/2+.06,d/2-.06]:cyl('mount washer',(x,y,.081),.028,.012);cyl('hex mount bolt',(x,y,.097),.019,.022,'Steel','Z',6)
def winch(power=False,long=False):
 base(1.14 if power else 1.22 if long else .87,.72 if power else .63)
 z=.34 if power else .29;span=.64 if long else .44 if power else .35
 # Large wire drum, recessed central flanges and bolted side housings.
 cyl('drum cable core',(0,0,z),.18,span,'Cable','X',48)
 for x in [-span/2-.025,span/2+.025]:
  cyl('drum ivory flange',(x,0,z),.215,.035,'Ivory','X',40)
  cyl('drum inset plate',(x+(-.023 if x<0 else .023),0,z),.185,.018,'Olive','X',40)
  bolts_ring((x+(-.035 if x<0 else .035),0,z),.173,12)
  box('Bearing bracket',(x,0,.145),(.07,.32,.20),'Ivory')
 cyl('shaft drum axle',(0,0,z),.04,.65,'Steel','X')
 # Continuous helix rather than disconnected rings. Crossed strand ridges read as wire.
 for strand in range(2):
  pts=[]
  for i in range(1801):
   t=i/1800;a=math.tau*30*t+strand*math.pi;pts.append((-span/2+span*t,.184*math.cos(a),z+.184*math.sin(a)))
  pipe('Braided drum strand '+str(strand),pts,.006,'Cable',0)
 x=span/2+.13
 box('Motor gearbox',(x,.035,.30),(.20,.29,.32),'Olive',.027)
 for zz in [.21,.38]:
  for xx in [x-.075,x+.075]:cyl('hex motor plate bolt',(xx,-.122,zz),.012,.018,'Steel','Y',6)
 for zz in [.18,.21,.24]:box('Motor cooling louvre',(x,-.13,zz),(.13,.013,.008),'Steel',.001)
 box('Motor casing cover',(x,.03,.48),(.21,.30,.08),'Ivory')
 box('Orange motor service stripe',(x,-.118,.34),(.2,.014,.08),'Orange',.003)
 for yy in [-.06,.12]:cyl('hex casing bolt',(x,yy,.529),.014,.018,'Steel','Z',6)
 if power:
  cyl('Heavy reduction gear',(-span/2-.08,0,z),.145,.13,'Olive','X',32)
  bolts_ring((-span/2-.15,0,z),.118,10)
  box('Power motor rear housing',(x,.23,.32),(.24,.23,.23),'Olive')
  box('Heavy orange reduction cover',(-span/2-.105,0,.50),(.21,.29,.13),'Orange',.018)
  for side in [-1,1]:box('Reinforced power cheek',(side*(span/2+.025),0,.12),(.10,.43,.17),'Ivory',.014)
  cyl('Twin power motor',(x,.24,.48),.105,.34,'Olive','Y',32)
  box('Power top bridge',(0,.16,.59),(.80,.12,.09),'Olive',.012)
 elif long:
  pipe('Tall long-line cage',[(-.46,.19,.1),(-.46,.19,.48),(-.39,.19,.66),(.39,.19,.66),(.46,.19,.48),(.46,.19,.1)],.026,'Olive',2)
  box('Long-line orange cage bar',(0,.19,.66),(.64,.058,.052),'Orange',.009)
  for x in [-.44,.44]:box('Long-line mounting outrigger',(x,0,.10),(.13,.51,.09),'Ivory',.012)
 for x in [-.22,.22]:box('Roller fairlead upright',(x,-.245,.17),(.07,.07,.25),'Ivory');cyl('shaft vertical roller',(x,-.245,.205),.035,.20,'Steel','Z',20)
 for zz in [.10,.30]:cyl('shaft fairlead roller',(0,-.245,zz),.027,.44,'Steel','X',24)
 pipe('Lead cable',[(.08,-.05,.2),(.08,-.25,.2),(.12,-.37,.14)],.009,'Cable',1)
 torus('Hook swivel eye',(.12,-.38,.13),.024,.008,'Steel','Y')
 hook=[(.12+.048*math.sin(a),-.385,.064+.05*math.cos(a)) for a in [i*math.pi*1.6/26 for i in range(27)]]
 pipe('Forged hook',hook,.014,'Steel',1);box('Hook orange witness stripe',(.16,-.385,.054),(.019,.035,.035),'Orange',.004)
def engine(race=False):
 base(.73,.72)
 box('Block casting',(0,0,.25),(.47,.43,.34),'Ivory',.027)
 for x in [-.2,.2]:
  for y in [-.15,0,.15]:
   cyl('Cylinder casting',(x,y,.33),.062,.22,'Steel','Z',20)
 for x in [-.25,.25]:
  for y in [-.19,.20]:box('Mounting foot',(x,y,.09),(.12,.09,.06),'Steel',.008)
 box('Block inspection panel',(0,-.224,.28),(.19,.018,.13),'Ivory',.008)
 for x in [-.07,.07]:
  for z in [.24,.32]:cyl('hex inspection bolt',(x,-.242,z),.01,.014,'Steel','Y',6)
 for x in [-.20,.20]:
  box('Valve bank cover',(x,.02,.48),(.23,.48,.16),'Olive',.022)
  box('Orange bank stripe',(x,-.04,.565),(.234,.075,.013),'Orange',.003)
  box('Valve cover seam',(x,.02,.41),(.237,.489,.014),'Steel',.003)
  for y in [-.1,.15]:cyl('Oil filler cap',(x,y,.582),.033,.042,'Steel','Z',12)
  for y in [-.18,.08,.21]:cyl('hex head bolt',(x,y,.57),.016,.017,'Steel','Z',6)
  for y in [-.15,0,.15]:
   pipe('Exhaust manifold',[(x,y,.38),(x*1.7,y,.35),(x*1.7,y+.06,.20)],.025,'Steel',1)
  box('Lower side service cover',(x*1.35,.04,.17),(.08,.27,.18),'Ivory')
  for y in [-.06,.14]:cyl('hex service bolt',(x*1.52,y,.22),.012,.02,'Steel','X',6)
 if not race:
  cyl('Heavy low gear flywheel',(0,-.29,.27),.235,.105,'Ivory','Y',40,True)
  cyl('Flywheel steel inset',(0,-.353,.27),.195,.035,'Steel','Y',40,True)
  cyl('Flywheel central boss',(0,-.39,.27),.073,.07,'Olive','Y',24,True)
  cyl('Flywheel spindle',(0,-.44,.27),.035,.04,'Steel','Y',24,True)
  torus('Flywheel orange band',(0,-.352,.27),.218,.012,'Orange','Y',True)
  for i in range(12):
   a=i*math.tau/12;cyl('hex flywheel bolt',(.20*math.cos(a),-.379,.27+.20*math.sin(a)),.015,.018,'Steel','Y',6,True)
  cyl('Air cleaner neck',(0,.08,.63),.045,.14)
  cyl('Air filter housing',(0,.08,.72),.135,.11,'Steel','Z',32)
  for i in range(20):
   a=i*math.tau/20;box('Air filter fin',(.125*math.cos(a),.08+.125*math.sin(a),.72),(.015,.015,.09),'Ivory',.001)
  pipe('Cooling return hose',[(.22,.16,.43),(.34,.20,.43),(.34,.24,.16),(.15,.24,.13)],.026,'Rubber',1)
 else:
  # Twin-scroll silhouette: large turbine, intake bell and crossing boost pipes.
  torus('turbo compressor scroll',(.32,.04,.44),.104,.044,'Ivory','X')
  cyl('turbo center throat',(.36,.04,.44),.077,.075,'Steel','X',32)
  cyl('turbo dark inlet',(.402,.04,.44),.055,.009,'Rubber','X',32)
  pipe('Boost pipe',[(.31,.15,.48),(.31,.23,.62),(-.12,.23,.62),(-.2,.16,.56)],.036,'Steel',1)
  for x in [-.12,.12]:
   pipe('Intake runner',[(x,-.12,.56),(x,-.12,.66),(x,.08,.66)],.028,'Rubber',1)
   cyl('Intake orange collar',(x,-.12,.59),.033,.03,'Orange','Z')
  box('Radiator outer frame',(0,-.315,.26),(.60,.105,.38),'Ivory',.018)
  box('Radiator dark core',(0,-.379,.26),(.51,.026,.29),'Rubber',.004)
  for i in range(15):box('Radiator horizontal cooling fin',(0,-.399,.13+i*.018),(.51,.011,.006),'Steel',.001)
  for x in [-.26,.26]:
   for z in [.105,.42]:cyl('hex radiator bolt',(x,-.381,z),.012,.017,'Steel','Y',6)
  cyl('Alternator housing',(-.25,-.22,.15),.09,.13,'Ivory','Y',24)
  for i in range(12):
   a=i*math.tau/12;box('Alternator cooling slot',(-.25+.078*math.cos(a),-.295,.15+.078*math.sin(a)),(.014,.018,.014),'Steel',.001)
 center=(-.25,-.33,.15) if race else (0,-.45,.27)
 cyl('Engine rotating pulley',center,.065,.035,'Steel','Y',24,True)
 for i in range(3):
  a=i*math.tau/3;box('Engine pulley orange timing mark',(center[0]+.04*math.cos(a),center[1]-.023,center[2]+.04*math.sin(a)),(.016,.012,.016),'Orange',.002,True)
def tyre():
 # Wheel axis becomes engine Z when GLB converts Blender coordinates.
 torus('tyre carcass',(0,.18,0),.43,.15,'Rubber','Y',True)
 for y in [-.005,.365]:
  cyl('rim outer dish',(0,y,0),.305,.038,'Olive','Y',40,True)
  torus('rim bead',(0,y+(-.025 if y<.1 else .025),0),.29,.019,'Ivory','Y',True)
  cyl('rim central hub',(0,y+(-.03 if y<.1 else .03),0),.105,.065,'Steel','Y',24,True)
  for i in range(8):
   a=i*math.tau/8;cyl('rim vent',( .205*math.cos(a),y+(-.021 if y<.1 else .021),.205*math.sin(a)),.034,.009,'Rubber','Y',16,True)
   cyl('hex wheel lug',(.13*math.cos(a),y+(-.048 if y<.1 else .048),.13*math.sin(a)),.018,.025,'Ivory','Y',6,True)
 for i in range(24):
  a=i*math.tau/24
  for side in [-1,1]:
   o=box('Chevron sand tread',(.562*math.sin(a),.18+side*.105,.562*math.cos(a)),(.115,.20,.065),'Rubber',.013,True)
   o.rotation_euler=(0,a,side*.28)
 box('Fixed wheel mount',(0,.01,0),(.13,.09,.13),'Steel')
def aim(o,p):o.rotation_euler=(Vector(p)-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(1.6,-2.0,1.4));camera=bpy.context.object;scene.camera=camera;camera.data.type='ORTHO';camera.data.ortho_scale=1.40;aim(camera,(0,0,.32))
for p,power,size in [((1,-2,3),400,3),((-2,-1,1.5),220,2),((0,2,3),450,2)]:
 bpy.ops.object.light_add(type='AREA',location=p);light=bpy.context.object;light.data.energy=power;light.data.shape='DISK';light.data.size=size;aim(light,(0,0,.3))
specs=[(33,'powered_winch',lambda:winch()),(34,'low_gear_engine',lambda:engine()),(35,'long_line_winch',lambda:winch(long=True)),(36,'power_winch',lambda:winch(power=True)),(37,'sand_tyre',tyre),(38,'race_engine',lambda:engine(True))]
records=[]
for id,name,make in specs:
 objects=[];moving=[];make()
 factor=1.8 if id==34 else 1.9 if id==38 else 1
 if factor!=1:
  for o in objects:o.location*=factor;o.scale*=factor
 # Mesh groups retain the articulation naming contract and collapse draw calls.
 groups=[]
 for group,label in [(moving,'Engine_Pulley' if id in (34,38) else 'Moving_Wheel'),([o for o in objects if o not in moving],'Fixed_Body')]:
  if not group:continue
  bpy.ops.object.select_all(action='DESELECT')
  for o in group:o.select_set(True)
  bpy.context.view_layer.objects.active=group[0];bpy.ops.object.join();o=bpy.context.object;o.name=label
  pivot=Vector((-.25,-.33,.15) if id==38 else (0,-.45,.27))*factor if label=='Engine_Pulley' else Vector((0,0,0))
  bpy.context.scene.cursor.location=pivot;bpy.ops.object.origin_set(type='ORIGIN_CURSOR');bpy.ops.object.transform_apply(location=label!='Engine_Pulley',rotation=True,scale=True)
  bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=1.15,island_margin=.01);bpy.ops.object.mode_set(mode='OBJECT')
  groups.append(o)
 points=[o.matrix_world@Vector(p) for o in groups for p in o.bound_box]
 # Convert Blender Z-up to engine Y-up (x,z,-y).
 bounds=[[min(p.x for p in points),min(p.z for p in points),min(-p.y for p in points)],[max(p.x for p in points),max(p.z for p in points),max(-p.y for p in points)]]
 bpy.ops.object.select_all(action='DESELECT')
 for o in groups:o.select_set(True)
 filename=f'{id:02d}_{name}'
 bpy.ops.export_scene.gltf(filepath=str(OUT/(filename+'.glb')),export_format='GLB',use_selection=True,export_yup=True,export_materials='EXPORT',export_texcoords=True,export_normals=True)
 camera.location=((-1.6,-2.0,1.4) if id in (33,35,36) else (1.6,-2.0,1.4)) if id!=37 else (1.4,-2,1);aim(camera,(0,0,.32*factor) if id!=37 else (0,.18,0));camera.data.ortho_scale=1.35*factor if id!=37 else 1.5
 scene.render.filepath=str(OUT/(filename+'.png'));bpy.ops.render.render(write_still=True)
 records.append({'id':id,'name':name,'min':bounds[0],'max':bounds[1],'vertices':sum(len(o.data.vertices) for o in groups),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in groups)})
 collection=bpy.data.collections.new(filename);scene.collection.children.link(collection)
 for o in groups:
  o.name=filename+'_'+o.name
  for old in list(o.users_collection):old.objects.unlink(o)
  collection.objects.link(o);o.hide_render=True;o.hide_set(True)
 print('COMPLETED',filename,records[-1],flush=True)
(OUT/'model-report.json').write_text(json.dumps(records,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'DuneCompany-Upgrade-Parts.blend'))
