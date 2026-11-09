"""Reference-built editable prop kit + review-only camp. Execute in the connected Blender."""
import bpy, math, random, json
from pathlib import Path
from mathutils import Vector
W=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine');OUT=W/'artifacts/camp-decorations';OUT.mkdir(parents=True,exist_ok=True)
PREFIX='HPD ';KIT={};C=None
def mat(name,color,metal=.25,rough=.38,emit=0):
 m=bpy.data.materials.get(PREFIX+name) or bpy.data.materials.new(PREFIX+name);m.use_nodes=True;m.diffuse_color=(*color,1);p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
 if emit:p.inputs['Emission Color'].default_value=(*color,1);p.inputs['Emission Strength'].default_value=emit
 return m
M={'yellow':mat('safety yellow',(1,.58,.018)), 'edge':mat('gold edge',(1,.72,.045)), 'dark':mat('charcoal',(.038,.05,.065),.5), 'steel':mat('silver',(.34,.39,.45),.75,.28), 'black':mat('rubber',(.011,.017,.023),.05,.65), 'cyan':mat('cyan lights',(.005,.7,.9),.2,.28,3), 'white':mat('LED diffuser',(1,.89,.62),.05,.35,5), 'canvas':mat('ochre canvas',(.72,.43,.09),0,.85), 'sand':mat('orange sand',(.55,.16,.045),0,.95), 'rock':mat('purple canyon',(.16,.095,.20),.05,.9), 'green':mat('canvas green',(.21,.26,.075),0,.8)}
def new(name):
 global C
 C=bpy.data.collections.new(PREFIX+name);bpy.context.scene.collection.children.link(C);C['asset_name']=name;KIT[name]=C
 return C
def finish(o,name,material,bevel=0):
 o.name=PREFIX+name
 for c in list(o.users_collection):c.objects.unlink(o)
 C.objects.link(o);o.data.materials.append(M[material]);bpy.context.view_layer.objects.active=o;o.select_set(True)
 if o.type=='MESH':bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if bevel and o.type=='MESH':b=o.modifiers.new('Small machined bevel','BEVEL');b.width=bevel;b.segments=2
 o.select_set(False);return o
def box(name,p,size,material='yellow',bevel=.018,rot=None):
 bpy.ops.mesh.primitive_cube_add(size=1,location=p);o=bpy.context.object;o.dimensions=size
 if rot:o.rotation_euler=rot
 return finish(o,name,material,bevel)
def cyl(name,p,r,depth,material='steel',axis='Z',n=12):
 bpy.ops.mesh.primitive_cylinder_add(vertices=n,radius=r,depth=depth,location=p);o=bpy.context.object
 o.rotation_euler=(0,math.pi/2,0) if axis=='X' else (math.pi/2,0,0) if axis=='Y' else (0,0,0)
 return finish(o,name,material,.006)
def beam(name,a,b,width,material='dark',depth=None):
 a,b=Vector(a),Vector(b);return box(name,(a+b)/2,(width,depth or width,(b-a).length),material,min(.012,width*.1),(b-a).to_track_quat('Z','Y').to_euler())
def tube(name,points,r=.035,material='black'):
 curve=bpy.data.curves.new(PREFIX+name,'CURVE');curve.dimensions='3D';curve.resolution_u=1;curve.bevel_depth=r;curve.bevel_resolution=1;poly=curve.splines.new('POLY');poly.points.add(len(points)-1)
 for v,p in zip(poly.points,points):v.co=(*p,1)
 o=bpy.data.objects.new(PREFIX+name,curve);C.objects.link(o);o.data.materials.append(M[material]);return o
def ring(name,p,outer,inner,depth,material='steel',axis='Z',n=16):
 verts=[];faces=[]
 for h,r in [(-depth/2,outer),(depth/2,outer),(-depth/2,inner),(depth/2,inner)]:
  for i in range(n):
   a=math.tau*i/n;x,y=r*math.cos(a),r*math.sin(a);q=(h,x,y) if axis=='X' else (x,h,y) if axis=='Y' else (x,y,h);verts.append(tuple(p[j]+q[j] for j in range(3)))
 for i in range(n):
  j=(i+1)%n;faces.extend([(i,j,n+j,n+i),(2*n+j,2*n+i,3*n+i,3*n+j),(i,2*n+i,2*n+j,j),(n+i,n+j,3*n+j,3*n+i)])
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update();o=bpy.data.objects.new(name,mesh);C.objects.link(o);return finish(o,name,material)
def bolt(p,axis='Y',r=.024):return cyl('hex fastener',p,r,.02,'steel',axis,6)
def lamp(p,size,material='cyan'):
 box('recessed light bezel',p,tuple(v*1.2 for v in size),'black',.008);box('light lens',(p[0],p[1]-.008,p[2]),size,material,.006)
def frame(w,d,h):
 for x in (-w/2,w/2):
  for y in (-d/2,d/2):
   box('rack upright',(x,y,h/2),(.085,.085,h),'dark');box('rack corner cap',(x,y,h),(.15,.15,.15));box('stable foot',(x,y,.06),(.19,.19,.12),'dark')
 for z in (.12,h):
  for y in (-d/2,d/2):box('rack crossbar',(0,y,z),(w+.10,.09,.09))
  for x in (-w/2,w/2):box('rack side beam',(x,0,z),(.09,d,.09))
def crate(center=(0,0,0),scale=1,material='dark'):
 x,y,z=center;s=scale
 box('cargo case body',(x,y,z+.32*s),(1.05*s,.72*s,.60*s),material,.045*s);box('reinforced lid',(x,y,z+.65*s),(1.08*s,.75*s,.12*s),'steel',.025*s)
 for xx in (-.48,.48):
  for yy in (-.32,.32):
   for zz in (.12,.60):box('yellow protective corner',(x+xx*s,y+yy*s,z+zz*s),(.18*s,.17*s,.22*s),'yellow',.03*s)
 for xx in (-.32,.32):box('latch recess',(x+xx*s,y-.376*s,z+.49*s),(.14*s,.03*s,.23*s),'black',.015*s);box('captive latch',(x+xx*s,y-.40*s,z+.49*s),(.09*s,.05*s,.17*s),'steel',.01*s)
 lamp((x,y-.377*s,z+.39*s),(.16*s,.016*s,.035*s));box('side grip recess',(x+.533*s,y,z+.36*s),(.02*s,.30*s,.15*s),'black',.015*s);box('carry handle',(x+.56*s,y,z+.36*s),(.045*s,.23*s,.055*s),'yellow',.012*s)
def cylinder_bottle(x,y,z):
 cyl('gas cylinder steel shoulder',(x,y,z+.86),.16,.22);cyl('yellow pressure vessel',(x,y,z+.45),.17,.75,'yellow');cyl('lower protective boot',(x,y,z+.08),.19,.15,'dark');cyl('valve neck',(x,y,z+1.03),.055,.12,'steel');ring('valve wheel',(x,y,z+1.11),.10,.055,.045,'dark',n=8)
 box('retaining strap',(x,y-.17,z+.52),(.30,.045,.11),'dark');bolt((x,y-.205,z+.52));lamp((x,y-.15,z+.84),(.07,.025,.035))
def create_assets():
 new('01-tripod-work-light')
 cyl('telescopic mast',(0,0,1.25),.045,2.15,'dark');cyl('yellow sleeve',(0,0,.8),.065,1.15,'yellow')
 for z in (.43,1.0,1.65):cyl('locking collar',(0,0,z),.09,.10,'yellow');box('collar lever',(.09,0,z),(.11,.04,.035),'steel')
 for i in range(3):
  a=i*math.tau/3;tip=(.66*math.cos(a),.66*math.sin(a),.05);beam('splayed tripod leg',(0,0,.7),tip,.07,'yellow');beam('tripod brace',(0,0,.26),(.36*math.cos(a),.36*math.sin(a),.38),.025,'steel');box('rubber foot',tip,(.20,.17,.08),'dark')
 beam('light mounting yoke',(-.65,0,2.1),(.65,0,2.1),.075,'steel')
 for x in (-.35,.35):
  box('bevelled floodlight housing',(x,0,2.42),(.64,.17,.48),'yellow',.055);box('black diffuser bezel',(x,-.095,2.42),(.53,.025,.37),'dark',.022);lamp((x,-.115,2.42),(.45,.015,.29),'white');cyl('head hinge',(x+.33,0,2.42),.07,.04,'steel','X');lamp((x,-.11,2.18),(.15,.02,.025))
 new('02-spare-auger-rack');frame(2.8,.72,2.45)
 for x in (-.88,0,.88):
  cyl('central auger shaft',(x,0,1.28),.09,1.95,'steel');cyl('coupling',(x,0,2.23),.13,.27,'dark')
  bpy.ops.mesh.primitive_cone_add(vertices=10,radius1=0,radius2=.23,depth=.4,location=(x,0,.36));finish(bpy.context.object,'drill bit tip','steel')
  verts=[];faces=[];N=128
  for i in range(N+1):
   a=i/N*math.tau*3.0;z=.55+i/N*1.46
   for r in (.09,.34):verts.append((x+r*math.cos(a),r*math.sin(a),z))
  for i in range(N):faces.append((i*2,i*2+1,i*2+3,i*2+2))
  mesh=bpy.data.meshes.new('helical flight');mesh.from_pydata(verts,[],faces);mesh.update();o=bpy.data.objects.new('flight',mesh);C.objects.link(o);finish(o,'continuous spiral auger flight','steel');o.modifiers.new('Flight plate thickness','SOLIDIFY').thickness=.035
  ring('drill storage seat',(x,0,.19),.31,.20,.11,'yellow');lamp((x,-.12,2.21),(.06,.02,.05))
 new('03-pipe-storage-rack');frame(2.6,.8,1.8)
 for z in (.26,.88):
  for x,y,h in [(-.75,-.16,0),(-.25,-.16,0),(.25,-.16,0),(.75,-.16,0),(-.5,-.16,.37),(0,-.16,.37),(.5,-.16,.37)]:ring('hollow pipe stock',(x,y,z+.22+h),.23,.175,1.42,'steel','Y',12)
  for y in (-.45,.45):box('pipe shelf front rail',(0,y,z),(2.75,.10,.10))
 new('04-gas-cylinder-rack');frame(1.1,.52,1.05)
 for x in (-.37,0,.37):cylinder_bottle(x,0,.08)
 new('05-fabric-shade-canopy')
 for x in (-1.75,1.75):
  for y in (-1.3,1.3):
   box('canopy upright',(x,y,1.23),(.075,.075,2.4),'yellow');box('post foot',(x,y,.06),(.25,.25,.12),'dark');beam('corner brace',(x,y,1.85),(x-math.copysign(.35,x),y,2.4),.045,'steel');bolt((x,y-.05,2.30))
 verts=[];faces=[];N=12
 for j in range(N+1):
  for i in range(N+1):
   x=-1.8+3.6*i/N;y=-1.35+2.7*j/N;z=2.52-.30*(1-(x/1.8)**2)*(1-(y/1.35)**2);verts.append((x,y,z))
 for j in range(N):
  for i in range(N):a=j*(N+1)+i;faces.append((a,a+1,a+N+2,a+N+1))
 mesh=bpy.data.meshes.new('sagging canvas roof');mesh.from_pydata(verts,[],faces);mesh.update();o=bpy.data.objects.new('canvas',mesh);C.objects.link(o);finish(o,'tensioned canvas fabric','canvas');o.modifiers.new('Canvas thickness','SOLIDIFY').thickness=.018
 for y in (-1.35,1.35):tube('stitched edge',[(-1.8,y,2.52),(0,y,2.49),(1.8,y,2.52)],.025,'edge')
 new('06-reinforced-cargo-crate');crate()
 new('07-small-supply-cases');crate((0,0,0),1,'yellow');crate((0,0,.76),.79,'yellow');crate((0,0,1.37),.59,'yellow')
 new('08-cargo-pallet')
 for x in (-.68,0,.68):box('pallet riser',(x,0,.12),(.18,1.12,.24),'dark')
 for i in range(6):box('pallet top plank',(-.70+i*.28,0,.29),(.25,1.2,.10),'steel',.012)
 for x in (-.8,.8):
  for y in (-.55,.55):box('pallet corner guard',(x,y,.25),(.16,.18,.19),'yellow');bolt((x,y,.36),'Z')
 new('09-workshop-tool-trolley')
 box('tool cabinet core',(0,0,.62),(1.10,.68,.97),'dark',.045);box('worktop',(0,0,1.15),(1.20,.80,.10),'steel',.035)
 for i in range(5):z=.28+i*.16;box('separate drawer',(0,-.355,z),(.98,.045,.135),'yellow',.015);box('pull handle',(0,-.395,z),(.40,.04,.04),'dark',.007)
 for x in (-.43,.43):
  for y in (-.23,.23):cyl('caster wheel',(x,y,.09),.09,.07,'black','X');box('caster bracket',(x,y,.17),(.08,.10,.10),'steel')
 box('perforated tool back',(0,.32,1.60),(1.1,.055,.85),'dark')
 for x in (-.48,.48):box('back panel yellow edge',(x,.29,1.60),(.055,.04,.84),'yellow')
 for x in (-.35,0,.35):
  beam('hanging wrench handle',(x,.27,1.51),(x,.27,1.88),.045,'steel');ring('wrench head',(x,.27,1.88),.08,.045,.035,'steel','Y',8)
 box('vise base',(-.1,-.05,1.22),(.32,.28,.06));box('vise body',(-.1,-.05,1.31),(.24,.20,.15),'steel');box('vise jaw',(-.22,-.05,1.40),(.06,.24,.09),'dark');box('vise moving jaw',(.02,-.05,1.40),(.06,.24,.09),'steel');cyl('vise screw',(-.1,-.22,1.31),.025,.4,'steel','X')
 for x in (.33,.46):cyl('screwdriver grip',(x,-.08,1.29),.032,.20,'yellow');cyl('screwdriver shank',(x,-.08,1.45),.012,.16,'steel')
 new('10-power-cable-reel')
 for x in (-.52,.52):
  for y in (-.36,.36):beam('reel frame leg',(x,y,.05),(x,0,.9),.07,'yellow');box('reel foot',(x,y,.05),(.20,.24,.10),'dark')
  cyl('yellow spool flange',(x,0,.72),.52,.07,'yellow','X',20)
 cyl('spool axle',(0,0,.72),.075,1.3,'steel','X')
 for i in range(13):ring('wound power cable',(-.45+i*.075,0,.72),.46,.39,.071,'black','X',20)
 beam('crank arm',(.69,0,.72),(.69,.26,.72),.06,'steel');cyl('crank handle',(.79,.26,.72),.045,.21,'yellow','X')
 tube('cable free end',[(0,-.43,.6),(0,-.65,.18),(.35,-.91,.07),(.6,-.91,.07)],.045);cyl('cable connector',(.72,-.91,.07),.09,.25,'yellow','X')
 new('11-portable-generator')
 box('generator housing',(0,0,.53),(1.35,.76,.88),'yellow',.075);box('steel base',(0,0,.10),(1.49,.87,.18),'dark',.04)
 box('front ventilation recess',(.30,-.405,.54),(.66,.025,.59),'black',.02)
 for i in range(6):box('front grille louvre',(.30,-.43,.31+i*.085),(.57,.05,.027),'steel',.007)
 box('control panel',(-.40,-.41,.64),(.44,.04,.46),'dark',.025);lamp((-.40,-.44,.75),(.30,.02,.15))
 for x in (-.50,-.30):cyl('covered outlet',(x,-.455,.48),.075,.045,'steel','Y',8)
 tube('protective hoop',[(-.75,-.37,.14),(-.75,-.37,1.04),(.75,-.37,1.04),(.75,-.37,.14)],.035,'dark')
 tube('exhaust',[(-.45,.15,.95),(-.45,.15,1.14),(-.45,.38,1.14)],.065,'steel');ring('exhaust opening',(-.45,.39,1.14),.065,.045,.025,'steel','Y')
 new('12-communications-mast')
 box('mast base',(0,0,.25),(.66,.66,.44),'yellow',.04)
 for a in (0,math.tau/3,2*math.tau/3):beam('mast stabilizer',(0,0,.5),(.72*math.cos(a),.72*math.sin(a),.07),.10,'steel');box('mast foot',(.72*math.cos(a),.72*math.sin(a),.06),(.28,.26,.12),'yellow')
 for x in (-.16,.16):
  for y in (-.16,.16):beam('lattice mast upright',(x,y,.45),(x,y,4.4),.035,'steel')
 for i in range(8):
  z=.5+i*.48
  for y in (-.16,.16):beam('lattice diagonal',(-.16,y,z),(.16,y,z+.45),.022,'dark');beam('lattice diagonal',(.16,y,z),(-.16,y,z+.45),.022,'dark')
 for x in (-.34,.34):box('signal panel',(x,0,3.67),(.22,.13,.86),'yellow',.025);lamp((x,-.075,3.7),(.035,.02,.33))
 cyl('antenna whip',(0,0,4.5),.018,.8,'steel');cyl('beacon tip',(0,0,4.91),.035,.12,'cyan')
 beam('dish bracket',(.16,0,2.95),(.47,0,2.95),.06,'steel');ring('dish circular rim',(.48,-.08,3.03),.33,.30,.03,'steel','Y',20)
 bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=8,radius=1,location=(.48,0,3.03));o=bpy.context.object;o.scale=(.30,.06,.30);finish(o,'dish reflector','steel');beam('feedhorn',(.48,-.10,3.03),(.48,-.36,3.03),.035,'dark')
 new('13-ground-power-cable')
 pts=[(1.7*math.sin(i/32*math.pi*1.6),-1.2+i/32*2.4,.08) for i in range(33)];tube('S curve power lead',pts,.06)
 for p in (pts[0],pts[-1]):cyl('yellow plug body',p,.13,.28,'yellow','Y',10);cyl('connector ferrule',(p[0],p[1]+.1,p[2]),.14,.06,'steel','Y')
 new('14-cable-protector-ramp')
 box('rubber cable ramp',(0,0,.08),(2.6,.40,.16),'black',.055);box('hinged yellow channel cover',(0,0,.17),(2.4,.28,.055),'yellow',.015)
 for i in range(10):box('diagonal hazard band',(-1.1+i*.24,-.195,.10),(.13,.014,.09),'edge',.002,rot=(0,.35,0))
 for x in (-1.0,0,1.0):cyl('cover hinge',(x,.15,.18),.035,.17,'steel','X')
 new('15-walkway-floor-panel')
 box('panel foundation',(0,0,.07),(1.96,1.96,.14),'dark',.045)
 for i in range(5):box('walkway steel plank',(-.76+i*.38,0,.16),(.36,1.89,.065),'steel',.012)
 for x in (-.90,.90):
  for y in (-.90,.90):box('panel corner guard',(x,y,.17),(.19,.19,.08),'yellow',.012);bolt((x,y,.23),'Z',.025)
 return KIT

# Build all geometry in a dedicated scene. The user's existing scene is untouched.
library=bpy.data.scenes.new('Hazard Pay Decoration Library');bpy.context.window.scene=library
create_assets()
# Library grid uses collection instances; the original individual meshes stay editable in their own collections.
for index,(name,col) in enumerate(KIT.items()):
 library.collection.children.unlink(col)
 o=bpy.data.objects.new(name,None);o.instance_type='COLLECTION';o.instance_collection=col;library.collection.objects.link(o);o.location=((index%5)*5,(index//5)*6,0)
manifest={name:{'collection':col.name,'objects':len(col.objects),'mesh_faces':sum(len(o.data.polygons) for o in col.objects if o.type=='MESH')} for name,col in KIT.items()}
(OUT/'decoration-library.json').write_text(json.dumps(manifest,indent=2))
print('DECORATION LIBRARY READY',json.dumps(manifest))
