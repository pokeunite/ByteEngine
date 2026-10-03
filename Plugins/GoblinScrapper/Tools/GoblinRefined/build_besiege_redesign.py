"""Original Goblin silhouettes based on the audited Besiege part functions. No Besiege mesh/texture is exported.
Run Blender --background --factory-startup --python this.py -- <destination> <inventory.json>
"""
from pathlib import Path
import bpy,sys,json,math,random
from mathutils import Vector,Quaternion
args=sys.argv[sys.argv.index('--')+1:];DEST=Path(args[0]);DEST.mkdir(parents=True,exist_ok=True)
# Reuse our own material/rig/mesh library, not the Besiege geometry.
lib=Path(__file__).with_name('build_refined.py').read_text()
head=lib[:lib.index('# ----------------------------- CORE KIT')]
exec(compile(head,'goblin_geometry_library','exec'),globals())
OUT=DEST
for d in ['previews','textures','red_faction']:(OUT/d).mkdir(exist_ok=True)
rows=json.loads(Path(args[1]).read_text())['parts'];specs=[]

def moving(bone='Moving',pivot=(0,0,0)):
 CUR.bone(bone,pivot);return bone

def mount():
 if any((s.location-Vector((0,-.24,0))).length<.00001 for s in CUR.sockets):return
 flange((0,-.24,0),(0,-1,0),socket='Mount')
 box('Connected mount stem',(.10,.24,.10),(0,-.10,0),'iron',bevel=.008)

def case(size=(.36,.36,.30)):
 box('Green iron casting',size,(0,0,0),'green',bevel=.023)
 for x in [-size[0]/2,size[0]/2]:
  box('Cast seam',(.026,size[1]*.9,size[2]*.9),(x,0,0),'iron',bevel=.004)
 for x in [-.115,.115]:bolt((x,-.19,.105),(0,-1,0),scale=.60)
 mount()

def output(pos=(0,.28,0),bone='Root',normal=(0,1,0)):
 flange(pos,normal,bone,socket='Output')

def spin(bone='Moving',axis=(0,1,0)):
 anim_rotate('Spin',bone,axis,1,61)

def travel(delta=(0,.8,0),bone='Moving'):
 CUR.clip('Extend',{bone:[(1,(0,0,1),0,(0,0,0)),(61,(0,0,1),0,delta)]},False)

def steer(bone='Moving',axis=(0,0,1),degrees=45):
 CUR.clip('Steer',{bone:[(1,axis,-math.radians(degrees),(0,0,0)),(31,axis,0,(0,0,0)),(61,axis,math.radians(degrees),(0,0,0))]},False)

def rod(a,b,r=.018,mat='steel',bone='Root'):tube('Connecting rod',a,b,r,mat,bone)

def blade(points,mat='steel',bone='Root'):
 verts=[(x,y,z) for z in [-.025,.025] for x,y in points];n=len(points)
 return mesh('Hand forged blade',verts,[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],mat,bone,.008)

for spec in rows:
 if spec['visibility'] in ['reserved','surface-edit-handle','internal/unconfirmed']:continue
 kind=spec['shape'];part(Path(spec['goblin_file']).stem,spec['name'],spec['function'],spec['category']);CUR.root['besiege_reference_id']=spec['id'];CUR.root['runtime_parity']='not_verified';specs.append(spec)
 if kind=='beam' or kind in ['pole','log','brace','edge']:
  length=2 if spec['symbol']=='DoubleWoodenBlock' else 1
  if kind=='log':length=3
  if kind=='pole':length=2
  if kind=='brace':length=1.5
  width=.30 if kind=='log' else .14 if kind in ['pole','brace'] else .25
  if kind=='log':cylinder('Solid timber log',width*.5,length,(0,0,0),'wood',(0,1,0),verts=12)
  else:box('Carved wooden beam',(width,length,width),(0,0,0),'wood',bevel=.015)
  for y in [-length/2,length/2]:flange((0,y,0),(0,1 if y>0 else -1,0),socket='End_Front' if y>0 else 'Mount')
  for y in [-length*.3,0,length*.3]:
   box('Iron reinforcing band',(width+.035,.065,width+.035),(0,y,0),'iron',bevel=.006)
   if kind not in ['brace','pole']:
    for n in [(1,0,0),(-1,0,0),(0,0,1),(0,0,-1)]:flange(Vector((0,y,0))+Vector(n)*width*.55,n,socket='Branch_'+str(y)+'_'+str(n))
 elif kind=='core':
  box('Steel boxed timber core',(.48,.48,.48),mat='wood',bevel=.028)
  for n in [(1,0,0),(-1,0,0),(0,1,0),(0,-1,0),(0,0,1),(0,0,-1)]:flange(Vector(n)*.25,n,socket='Face_'+str(n))
 elif kind in ['wheel','spacewheel','caster','skate','gear','flywheel']:
  powered=spec['symbol'] in ['Wheel','LargeWheel','CogMediumPowered','SpaceWheel','FlyWheel'];rad=.64 if 'Large' in spec['symbol'] else .27 if kind in ['caster','skate'] else .43
  width=.20 if kind in ['wheel','spacewheel'] else .12
  moving();cylinder('Hub spindle',.10,width+.18,(0,0,0),'iron',(1,0,0));CUR.socket('Mount',(-width*.65,0,0),(-1,0,0))
  if kind=='gear':
   cylinder('Solid gear web',rad*.86,width,mat='iron',axis=(1,0,0),bone='Moving',verts=32)
   for j in range(20 if rad>.5 else 14):
    a=j*math.tau/(20 if rad>.5 else 14);q=Quaternion(Vector((1,0,0)),a);box('Machined gear tooth',(width,.11,.11),(0,rad*math.cos(a),rad*math.sin(a)),'steel','Moving',.004,q)
  else:
   torus('Wheel tire',rad*.77,rad*.23,mat='rubber' if kind!='flywheel' else 'iron',axis=(1,0,0),bone='Moving',majorseg=40,minseg=10)
   cylinder('Riveted wheel rim',rad*.60,width*.7,mat='green' if powered else 'iron',axis=(1,0,0),bone='Moving')
   for j in range(24):
    a=j*math.tau/24;q=Quaternion(Vector((1,0,0)),a);box('Tread block',(width,.105,.075),(0,rad*math.cos(a),rad*math.sin(a)),'rubber','Moving',.005,q)
  for j in range(6):
   a=j*math.tau/6;bolt((width*.65,rad*.38*math.cos(a),rad*.38*math.sin(a)),(1,0,0),'Moving',.65)
  cylinder('Hub endcap',rad*.21,width+.10,mat='brass' if powered else 'steel',axis=(1,0,0),bone='Moving')
  if powered:
   cylinder('Motor reduction cover',.16,.12,(-width*.6,0,0),'green',(1,0,0));box('Motor grease box',(.16,.19,.12),(-width*.8,-.05,.14),'green')
  CUR.socket('Output',(width*.65,0,0),(1,0,0),'Moving');spin(axis=(1,0,0))
 elif kind in ['hinge','steeringhinge','steer','swivel','balljoint','motor','axle','decoupler']:
  case();moving('Moving',(0,.13,0))
  if kind in ['hinge','steeringhinge']:
   for x in [-.17,.17]:box('Fixed hinge cheek',(.065,.27,.27),(x,.15,0),'iron')
   cylinder('Hinge pin',.09,.43,(0,.16,0),'brass',(1,0,0));box('Moving hinge leaf',(.22,.30,.12),(0,.25,.03),'green','Moving')
   output((0,.42,.03),'Moving');steer(axis=(1,0,0),degrees=60)
  elif kind=='balljoint':
   torus('Ball socket cup',.15,.032,(0,.17,0),axis=(0,1,0));cylinder('Ball stem',.06,.25,(0,.29,0),'steel',(0,1,0),'Moving')
   bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=8,radius=.12,location=(0,.15,0));CUR.add(bpy.context.object,'brass','Moving');output((0,.44,0),'Moving');steer(axis=(1,0,1),degrees=35)
  else:
   cylinder('Round bearing housing',.21,.13,(0,.20,0),'iron',(0,1,0));cylinder('Moving output bearing',.16,.10,(0,.30,0),'brass',(0,1,0),'Moving');output((0,.38,0),'Moving')
   if kind in ['steer','motor']:box('Visible drive casing',(.18,.20,.12),(.16,0,.10),'green')
   if kind=='steer':steer()
   elif kind=='decoupler':travel((0,.30,0))
   else:spin()
 elif kind in ['piston','slider','suspension','spring','winch','ropemeter','fuelline']:
  case();moving()
  if kind in ['piston','slider','suspension']:
   for x in [-.12,.12]:rod((x,.10,0),(x,.78,0),.025,'iron')
   cylinder('Actuator body',.095,.45,(0,.30,0),'green',(0,1,0));cylinder('Polished piston shaft',.040,.48,(0,.58,0),'steel',(0,1,0),'Moving');output((0,.86,0),'Moving')
   if kind=='suspension':
    pts=[(.14*math.cos(t*math.tau*5),.15+.62*t,.14*math.sin(t*math.tau*5)) for t in [i/100 for i in range(101)]];curve_tube('Exposed suspension coil',pts,.021,'rust')
   travel((0,.50,0))
  elif kind=='spring':
   pts=[(.09*math.cos(t*math.tau*7),-.15+1.20*t,.09*math.sin(t*math.tau*7)) for t in [i/140 for i in range(141)]];curve_tube('Tension spring',pts,.018,'steel','Moving');output((0,1.05,0),'Moving');travel((0,-.5,0))
  else:
   cylinder('Cable spool',.16,.31,(0,.1,.18),'wood',(1,0,0),'Moving');torus('Winch gear rim',.18,.025,(.17,.1,.18),'brass',(1,0,0),'Moving')
   rod((0,.18,.19),(0,1.1,.19),.022,'rubber' if kind=='fuelline' else 'wood');flange((0,1.1,.19),(0,1,0),socket='Endpoint');spin(axis=(1,0,0))
 elif kind in ['armor','panel','surface','roundarmor','wing','wingpanel','rudder','steeringfin','gridfin','drag']:
  size=1.1 if spec['symbol']=='ArmorPlateLarge' else .7;mount();moving() if kind in ['rudder','steeringfin'] else None
  bone='Moving' if kind in ['rudder','steeringfin'] else 'Root'
  if kind=='roundarmor':cylinder('Round forged armor',.42,.075,mat='green',axis=(0,1,0),verts=24)
  elif kind=='gridfin':
   for x in [-.38,.38]:box('Fin side',(.035,.10,.9),(x,0,.2),'iron')
   for z in [-.25,.65]:box('Fin edge',(.79,.10,.035),(0,0,z),'iron')
   for x in [-.24,0,.24]:box('Grid vane',(.020,.095,.90),(x,0,.20),'steel')
   for z in [-.10,.10,.30,.50]:box('Grid cross vane',(.76,.095,.020),(0,0,z),'steel')
  else:
   if kind in ['wing','wingpanel','drag','rudder','steeringfin']:
    blade([(-size,-.08),(size,-.08),(size*.75,.5),(-size*.6,.5)],'wood',bone)
    for x in [-size*.6,0,size*.6]:box('Aerodynamic rib',(.025,.59,.025),(x,.22,.033),'iron',bone,.003)
   else:
    box('Patchwork plate',(size,.065,size),(0,0,0),'wood' if kind in ['panel','surface'] else 'green',bone,.025)
    for x in [-size*.42,size*.42]:box('Plate reinforcing rib',(.055,.085,size*.90),(x,0,0),'iron',bone)
    for x in [-size*.36,size*.36]:
     for z in [-size*.36,size*.36]:bolt((x,-.05,z),(0,-1,0),bone,.7)
  if bone=='Moving':output((0,.13,0),bone);steer()
 elif kind in ['cannon','shrapnel','water','flame','vacuum','fuelcannon','harpoon','rocket','booster','steeringthruster']:
  case();moving('Moving',(0,0,0));radius=.16 if kind in ['cannon','shrapnel','fuelcannon','booster'] else .105
  cylinder('Barrel outer body',radius,.67,(0,.30,.08),'iron',(0,1,0),'Moving',verts=20)
  for y in [.12,.45,.64]:torus('Barrel retaining band',radius+.008,.018,(0,y,.08),'brass' if kind in ['water','vacuum'] else 'rust',(0,1,0),'Moving')
  cylinder('Dark open muzzle',radius*.78,.008,(0,.64,.08),'black',(0,1,0),'Moving');torus('Flared muzzle lip',radius,.022,(0,.64,.08),'steel',(0,1,0),'Moving')
  if kind in ['flame','water','vacuum','fuelcannon']:
   cylinder('Pressure reservoir',.12,.39,(.25,.05,.04),'green',(0,0,1));curve_tube('Feed pipe',[(.24,.1,.15),(.24,.25,.15),(0,.30,.15)],.022,'brass')
  if kind in ['rocket','booster','steeringthruster']:
   for x in [-.20,.20]:blade([(x-.05,.02),(x+.05,.02),(x+.05,.50),(x-.05,.40)],'green')
   if kind=='rocket':
    bpy.ops.mesh.primitive_cone_add(vertices=16,radius1=.17,radius2=.015,depth=.32,location=(0,.85,.08));o=bpy.context.object;o.rotation_euler[0]=math.pi/2;CUR.add(o,'rust','Moving')
  if kind=='harpoon':rod((0,.30,.08),(0,.96,.08),.02,'steel','Moving');blade([(-.10,.81),(0,1.05),(.10,.81)],'steel','Moving')
  if kind=='steeringthruster':steer()
  else:travel((0,-.05,0))
 elif kind in ['blade','spike','drill','saw','fan','propeller','screw']:
  case();moving('Moving',(0,.15,0) if kind=='saw' else (0,0,0))
  if kind=='spike':
   bpy.ops.mesh.primitive_cone_add(vertices=8,radius1=.16,radius2=0,depth=.75,location=(0,.42,0));o=bpy.context.object;o.rotation_euler[0]=-math.pi/2;CUR.add(o,'steel');
  elif kind=='blade':blade([(-.10,-.08),(.10,-.08),(.22,.57),(0,.81),(-.12,.57)])
  elif kind=='drill':
   rod((0,.10,0),(0,.95,0),.055,'iron','Moving')
   for i in range(3):
    pts=[((.16*(1-t))*math.cos(t*math.tau*2+i*math.tau/3),.15+.80*t,(.16*(1-t))*math.sin(t*math.tau*2+i*math.tau/3)) for t in [j/80 for j in range(81)]];curve_tube('Spiral cutting edge',pts,.028,'steel','Moving')
   spin()
  else:
   rad=.46 if kind=='saw' else .64 if kind=='propeller' and spec['symbol']=='Propeller' else .38
   axis=(1,0,0) if kind=='saw' else (0,1,0)
   cylinder('Rotor hub',.10,.18,(0,.16,0),'brass',axis,'Moving')
   if kind=='saw':
    cylinder('Saw disc',rad*.87,.05,(0,.15,0),'steel',axis,'Moving',verts=32)
    for j in range(20):
     a=j*math.tau/20;box('Saw tooth',(.07,.09,.09),(0,.15+rad*math.cos(a),rad*math.sin(a)),'steel','Moving',.002,Quaternion(Vector((1,0,0)),a))
   else:
    count=6 if kind=='fan' else 3 if kind=='screw' else 2
    for j in range(count):
     a=j*math.tau/count;q=Quaternion(Vector((0,1,0)),a);o=box('Pitched rotor blade',(.19,.07,rad*.9),(0,.18,rad*.5),'green' if kind in ['fan','screw'] else 'wood','Moving',.018);o.rotation_mode='QUATERNION';o.rotation_quaternion=q@Quaternion(Vector((0,0,1)),.32);o.location=q@Vector((0,.18,rad*.5))
    if kind=='fan':torus('Fan protective ring',rad+.06,.027,(0,.17,0),'iron',axis)
   spin(axis=axis)
 elif kind=='torch':
  mount();cylinder('Iron torch stem',.055,.60,(0,0,.28),'iron');cylinder('Pitch basket',.13,.20,(0,0,.64),'rust');
  for j in range(6):a=j*math.tau/6;rod((.13*math.cos(a),.13*math.sin(a),.54),(.15*math.cos(a),.15*math.sin(a),.80),.013,'iron')
 elif kind=='plow':
  mount();mesh('Curved ram scoop',[(-.64,-.12,-.18),(.64,-.12,-.18),(-.58,.32,.20),(.58,.32,.20),(-.58,.47,.46),(.58,.47,.46)],[(0,1,3,2),(2,3,5,4)],'green')
  for x in [-.44,.44]:rod((x,-.10,0),(x,.42,.34),.035,'iron')
  rod((-.64,-.12,-.18),(.64,-.12,-.18),.027,'steel')
 elif kind=='fuelcoupler':
  mount();case();moving();cylinder('Quick release fuel collar',.16,.24,(0,.25,0),'brass',(0,1,0),'Moving');torus('Sealing gasket',.15,.022,(0,.37,0),'rubber',(0,1,0),'Moving');output((0,.40,0))
  travel((0,.12,0))
 elif kind in ['sphere','bomb','grenade','flameball','rock','balloon','squareballoon','float','barrel','fuel','ballast','bounce','grip','holder','grabber','jaw','crossbow','sail','paddle','halfpipe','corner','parachute','pin','magnet','reaction','node']:
  mount()
  if kind in ['sphere','bomb','grenade','flameball','rock','balloon','squareballoon']:
   rad=.70 if kind in ['balloon','squareballoon'] else .42 if kind=='rock' else .25;pos=(0,.10,0) if kind not in ['balloon','squareballoon'] else (0,.30,1.20)
   if kind=='squareballoon':box('Stitched square gas bag',(.95,.85,.9),pos,'green',bevel=.12)
   else:bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3 if kind=='balloon' else 2,radius=rad,location=pos);CUR.add(bpy.context.object,'wood' if kind=='rock' else 'green' if kind in ['bomb','balloon'] else 'iron')
   if kind in ['balloon','squareballoon']:
    for x in [-.25,.25]:rod((x,.30,.80),(0,0,0),.013,'wood');torus('Balloon stitched belt',rad*.9,.018,pos,'iron')
   else:
    torus('Payload iron band',rad*.94,.020,pos,'iron');cylinder('Fuse socket',.045,.07,(0,.10,rad),'brass');
    if kind in ['bomb','grenade']:curve_tube('Fuse wick',[(0,.10,rad),(0,.14,rad+.09),(.04,.14,rad+.14)],.009,'wood')
    if kind=='flameball':
     for j in range(5):rod((0,.1,.18),(.14*math.cos(j),.1+.14*math.sin(j),.45),.016,'rust')
  elif kind in ['fuel','float','barrel','ballast']:
   rad=.37 if spec['symbol'] in ['BigBarrel','FuelBarrelBig'] else .23;length=.9 if spec['symbol']=='FuelBarrelBig' else .58
   cylinder('Stave barrel' if kind in ['float','barrel'] else 'Tank shell',rad,length,(0,.15,0),'wood' if kind in ['float','barrel'] else 'green',(0,1,0),verts=16)
   for y in [.15-length*.35,.15+length*.35]:torus('Tank bands',rad,.023,(0,y,0),'iron',(0,1,0))
   cylinder('Fill valve',.06,.07,(0,.15,.25),'brass');output((0,.15+length*.5,0))
  elif kind in ['grip','bounce']:
   box('Steel foot',(.52,.09,.52),mat='iron');box('Rubber contact sole',(.50,.08,.50),(0,0,-.085),'rubber')
   for x in [-.18,0,.18]:box('Traction ridge',(.045,.46,.025),(x,0,-.14),'rubber')
   if kind=='bounce':spring((0,0,.20),.15,.25,4)
  elif kind in ['holder','grabber','jaw']:
   case();moving('Moving',(0,.08,0))
   for side in [-1,1]:
    blade([(side*.10,0),(side*.32,.36),(side*.25,.61),(side*.13,.40)],'steel','Moving' if side>0 else 'Root')
    for j in range(4):box('Gripping tooth',(.09,.08,.06),(side*.21,.20+j*.10,0),'steel','Moving' if side>0 else 'Root',.003)
   if kind=='jaw':CUR.clip('Bite',{'Moving':[(1,(0,0,1),-.7,(0,0,0)),(21,(0,0,1),.4,(0,0,0)),(61,(0,0,1),-.7,(0,0,0))]},False)
   else:steer(axis=(0,0,1),degrees=30)
  elif kind=='crossbow':
   box('Crossbow timber stock',(.15,.90,.17),(0,.15,0),'wood');rod((0,-.18,0),(0,.82,0),.020,'steel');moving()
   for side in [-1,1]:curve_tube('Forged spring bow',[(0,.23,0),(side*.30,.31,0),(side*.54,.20,0)],.028,'iron','Moving')
   curve_tube('Bow string',[(-.54,.20,0),(0,-.18,0),(.54,.20,0)],.009,'wood','Moving');travel((0,.16,0))
  elif kind=='sail':
   rod((0,0,-.15),(0,0,1.5),.035,'wood');rod((-.7,0,1.2),(.7,0,1.2),.03,'wood');mesh('Stitched sail cloth',[(-.65,0,1.18),(.65,0,1.18),(.45,.06,.30),(-.45,.06,.30)],[(0,1,2,3)],'green')
   for x in [-.65,.65]:rod((x,0,1.18),(0,0,-.1),.009,'wood')
  elif kind=='paddle':
   case();moving();rod((0,0,0),(0,.66,0),.035,'wood','Moving');box('Broad paddle blade',(.30,.4,.05),(0,.72,0),'wood','Moving');steer(axis=(1,0,0),degrees=65)
  elif kind=='halfpipe':
   for j in range(10):
    a=-math.pi/2+j*math.pi/9;box('Channel stave',(.075,.72,.15),(.30*math.sin(a),.1,-.30*math.cos(a)),'wood',quat=Quaternion(Vector((0,1,0)),a))
   for y in [-.2,.4]:curve_tube('Channel metal hoop',[(.30*math.sin(-math.pi/2+i*math.pi/20),y,-.30*math.cos(-math.pi/2+i*math.pi/20)) for i in range(21)],.016,'iron')
  elif kind=='corner':
   beam('Corner timber A',(0,-.25,0),(0,.25,0),.25,'wood');beam('Corner timber B',(0,.25,0),(.5,.25,0),.25,'wood');flange((.5,.25,0),(1,0,0),socket='Output');box('Corner gusset',(.30,.30,.04),(.12,.15,.14),'green')
  elif kind=='parachute':
   case();moving();bpy.ops.mesh.primitive_uv_sphere_add(segments=20,ring_count=10,radius=.75,location=(0,0,1.25));o=bpy.context.object;o.scale.z=.34;CUR.add(o,'green','Moving')
   for j in range(8):a=j*math.tau/8;rod((.70*math.cos(a),.70*math.sin(a),1.22),(0,0,.13),.009,'wood','Moving')
   CUR.clip('Deploy',{'Moving':[(1,(0,0,1),0,(0,0,-1.0)),(61,(0,0,1),0,(0,0,0))]},False)
  elif kind=='pin':case();cylinder('Ground anchor pin',.05,.65,(0,.20,0),'steel',(0,1,0));torus('Pull ring',.12,.025,(0,.50,.08),'brass')
  else:case();cylinder('Reaction inertial disc',.25,.15,(0,.23,0),'brass',(0,1,0));output()
 elif spec['category']=='Automation':
  case();moving();cylinder('Instrument dial frame',.15,.06,(0,0,.19),'brass');cylinder('Dial face',.13,.01,(0,0,.23),'wood');box('Dial needle',(.015,.17,.014),(0,.045,.245),'rust','Moving',.002)
  for i in range(8):a=i*math.tau/8;box('Meter graduation',(.008,.025,.008),(.105*math.cos(a),.105*math.sin(a),.247),'iron')
  if kind in ['sensor','camera']:cylinder('Optical lens barrel',.09,.18,(0,.23,0),'iron',(0,1,0));cylinder('Blue glass lens',.068,.008,(0,.32,0),'brass',(0,1,0))
  elif kind=='logic':
   for i in range(3):box('Logic input lever',(.025,.16,.045),(-.11+i*.11,-.12,.30),'brass')
  CUR.clip('Meter',{'Moving':[(1,(0,0,1),-1.5,(0,0,0)),(61,(0,0,1),1.5,(0,0,0))]},False)
 else:raise RuntimeError('Unmodeled shape '+kind+' '+spec['symbol'])
 # Every visible model has an explicit input and, where useful, moving output; no fake animation flags.
 CUR.root['goblin_design']='Original green goblin foundry: timber, riveted iron, visible motion shaft, descriptive function'
print('GEOMETRY_READY',len(ALL),flush=True)
# Own mechanical rigging/export library.
a=lib.index('def planar_uv');b=lib.index('for p in ALL:\n    make_rig',a);exec(compile(lib[a:b],'goblin_rig_library','exec'),globals())
a=lib.index('def glb_json');b=lib.index('bpy.ops.wm.save_as_mainfile',a);exec(compile(lib[a:b],'goblin_export_library','exec'),globals())
for p in ALL:
 make_rig(p);export(p);print('PART_READY',p.file,flush=True)
manifest={'kit':'Goblin functional silhouette redesign','version':1,'units':'metres','parts':[]}
for p,s in zip(ALL,specs):
 tog=lambda v:[round(float(v[0]),5),round(float(v[2]),5),round(float(-v[1]),5)]
 manifest['parts'].append({'file':p.file+'.glb','label':p.label,'description':p.description,'group':p.group,'reference_id':s['id'],'bounds_blender_z_up':p.bounds,'sockets':[{'name':o.name,'position':tog(o['rest_position_blender']),'normal':tog(o['rest_normal_blender']),'animation_bone':o.get('animation_bone','Root')} for o in p.sockets],'animations':[{'name':a['name'],'channels':len(a['channels'])} for a in p.export_doc.get('animations',[])],'runtime_status':'not-ported'})
(OUT/'parts-catalog.json').write_text(json.dumps(manifest,indent=2))
for i,p in enumerate(ALL):p.col.hide_viewport=i>0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Goblin_Parts_Redesign.blend'),compress=True)
# Geometry renders used as references/cutouts, independent of runtime verification.
a=lib.index("# Studio for real geometry thumbnails");b=lib.index('for p in ALL:',a);exec(compile(lib[a:b],'goblin_preview_library','exec'),globals())
SCENE.render.resolution_x=256;SCENE.render.resolution_y=192;SCENE.render.film_transparent=True
if hasattr(SCENE,'eevee'):SCENE.eevee.taa_render_samples=24
for p in ALL:
 p.col.hide_viewport=False;frame_part(p);ground.hide_render=True
 SCENE.render.filepath=str(OUT/'previews'/(p.file+'.png'));bpy.ops.render.render(write_still=True)
 print('PREVIEW_READY',p.file,flush=True)
print('REDESIGN_COMPLETE',len(ALL),flush=True)
