"""Execute in the user's Blender MCP. Editable reference-based mechanical assets."""
import bpy, math, json
from pathlib import Path
from mathutils import Vector, Quaternion
WORK=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine')
OUT=WORK/'artifacts/research-models';OUT.mkdir(parents=True,exist_ok=True)
S=bpy.data.scenes.get('Hazard Pay Research Workshop') or bpy.data.scenes.new('Hazard Pay Research Workshop')
bpy.context.window.scene=S
S.render.engine='BLENDER_EEVEE';S.render.resolution_x=1200;S.render.resolution_y=1000;S.render.resolution_percentage=100;S.render.fps=30
S.world=S.world or bpy.data.worlds.new('Research studio');S.world.use_nodes=True
S.world.node_tree.nodes['Background'].inputs[0].default_value=(.17,.16,.15,1);S.world.node_tree.nodes['Background'].inputs[1].default_value=.4
S.view_settings.view_transform='AgX';S.view_settings.look='AgX - Medium High Contrast'
def material(name,color,metal=.25,rough=.4,emit=0):
 m=bpy.data.materials.get('RD '+name) or bpy.data.materials.new('RD '+name);m.use_nodes=True;m.diffuse_color=(*color,1)
 p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
 if emit:p.inputs['Emission Color'].default_value=(*color,1);p.inputs['Emission Strength'].default_value=emit
 return m
M={'yellow':material('safety yellow',(.94,.48,.012)), 'edge':material('sunlit yellow',(1,.66,.025)), 'dark':material('graphite',(.028,.037,.047),.5), 'steel':material('brushed steel',(.18,.22,.25),.72,.32), 'black':material('recess',(.006,.012,.016),.1,.62), 'cyan':material('cyan status',(.01,.65,.85),.2,.25,2), 'glass':material('chamber dark teal',(.018,.12,.15),.6,.18), 'amber':material('battery warning',(1,.24,.006),.15,.3,1.2)}
C=None;PARTS=[];BONES={};PREFIX=''
def bone(name,p,parent='Root'):BONES[name]=(Vector(p),parent)
def finish(o,name,mat='yellow',joint='Root',bevel=.012):
 o.name=PREFIX+' '+name
 for c in list(o.users_collection):c.objects.unlink(o)
 C.objects.link(o);o.data.materials.append(M[mat]);bpy.context.view_layer.objects.active=o
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
 if bevel:
  mod=o.modifiers.new('Machined edge','BEVEL');mod.width=bevel;mod.segments=1;bpy.ops.object.modifier_apply(modifier=mod.name)
 for f in o.data.polygons:f.use_smooth=False
 o.vertex_groups.new(name=joint).add(list(range(len(o.data.vertices))),1,'REPLACE');o['assembly']=joint;PARTS.append(o);o.select_set(False);return o
def box(name,p,size,mat='yellow',joint='Root',bevel=.012,rot=None):
 bpy.ops.mesh.primitive_cube_add(size=1,location=p);o=bpy.context.object;o.dimensions=size
 if rot:o.rotation_euler=rot
 return finish(o,name,mat,joint,bevel)
def cylinder(name,p,r,d,mat='steel',joint='Root',axis='Z',n=12,bevel=.006):
 bpy.ops.mesh.primitive_cylinder_add(vertices=n,radius=r,depth=d,location=p);o=bpy.context.object
 o.rotation_euler=(0,math.pi/2,0) if axis=='X' else (math.pi/2,0,0) if axis=='Y' else (0,0,0)
 return finish(o,name,mat,joint,bevel)
def beam(name,a,b,w,mat='steel',joint='Root',thick=None):
 a,b=Vector(a),Vector(b);return box(name,(a+b)/2,(w,thick or w,(b-a).length),mat,joint,w*.12,(b-a).to_track_quat('Z','Y').to_euler())
def prism(name,points,axis,lo,hi,mat='yellow',joint='Root',bevel=.009):
 axes=[i for i in range(3) if i!=axis];v=[]
 for d in (lo,hi):
  for x,y in points:
   p=[0,0,0];p[axis]=d;p[axes[0]]=x;p[axes[1]]=y;v.append(p)
 n=len(points);faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 me=bpy.data.meshes.new(name);me.from_pydata(v,[],faces);me.update();o=bpy.data.objects.new(name,me);C.objects.link(o);return finish(o,name,mat,joint,bevel)
def ring(name,p,r,inner,d,mat='yellow',joint='Root',n=20):
 verts=[];faces=[]
 for z,rr in ((-d/2,r),(d/2,r),(-d/2,inner),(d/2,inner)):
  for i in range(n):
   a=math.tau*i/n;verts.append((p[0]+rr*math.cos(a),p[1]+rr*math.sin(a),p[2]+z))
 for i in range(n):
  j=(i+1)%n;faces.extend([(i,j,n+j,n+i),(2*n+j,2*n+i,3*n+i,3*n+j),(i,2*n+i,2*n+j,j),(n+i,n+j,3*n+j,3*n+i)])
 me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update();o=bpy.data.objects.new(name,me);C.objects.link(o);return finish(o,name,mat,joint,0)
def bolt(p,joint='Root',axis='Y',r=.022):return cylinder('captive hex fastener',p,r,.018,'steel',joint,axis,6,0)
def lamp(name,p,size,joint='Root',mat='cyan'):
 box(name+' bezel',p,tuple(x*1.28 for x in size),'black',joint,.006);q=Vector(p);q.y-=.007;box(name+' lens',q,size,mat,joint,.004)
def research():
 # Cabinet and feet: 1.65m wide including fold-out workbench, 2.1m antenna.
 for x in (-.53,.53):
  for y in (-.3,.3):prism('splayed steel foot',[(x-.10,.03),(x+.10,.03),(x+.075,.18),(x-.075,.18)],1,y-.13,y+.13,'steel')
 box('cabinet core',(0,0,.54),(1.14,.70,.83),'dark',bevel=.055)
 box('cabinet yellow casing',(0,-.01,.55),(1.23,.73,.74),'yellow',bevel=.045)
 box('drawer recess',(0,-.392,.55),(.84,.035,.62),'black',bevel=.015)
 for i,z in enumerate((.34,.55,.76)):
  j='Drawer'+str(i);bone(j,(0,-.4,z));box('separate sliding drawer',(0,-.422,z),(.78,.07,.18),'yellow',j,.018)
  box('drawer handle',(0,-.48,z),(.24,.055,.035),'steel',j,.007)
 for x in (-.54,.54):
  box('cabinet corner reinforcement',(x,-.405,.55),(.11,.08,.71),'edge',bevel=.013)
  for z in (.27,.83):bolt((x,-.455,z))
 box('beveled worktop',(0,-.025,1.01),(1.35,.91,.11),'steel',bevel=.045)
 box('worktop rim',(0,-.45,1.03),(1.29,.065,.10),'dark',bevel=.015)
 box('back machine tower',(0,.235,1.48),(1.18,.31,.87),'dark',bevel=.05)
 # Left sample chamber, open front with teal backing instead of opaque glass obscuring sample.
 cx=-.29
 cylinder('sample chamber bottom',(cx,-.015,1.10),.30,.12,'steel',n=12)
 cylinder('sample chamber illuminated turntable',(cx,-.015,1.175),.22,.045,'cyan',n=12)
 bone('Sample',(cx,-.015,1.2));cylinder('sample tray',(cx,-.015,1.205),.21,.045,'steel','Sample',n=12)
 box('chamber teal back',(cx,.205,1.43),(.49,.025,.46),'glass',bevel=.015)
 for x in (cx-.255,cx+.255):
  beam('chamber corner pillar',(x,-.17,1.16),(x,-.17,1.72),.055,'steel')
  beam('chamber rear pillar',(x,.17,1.16),(x,.17,1.72),.055,'dark')
 for i,(x,y,r,h) in enumerate(((-.11,0,.055,.24),(.015,-.05,.07,.34),(.12,.035,.065,.20))):
  bpy.ops.mesh.primitive_cone_add(vertices=5,radius1=r,radius2=0,depth=h,location=(cx+x,y,1.25+h/2));finish(bpy.context.object,'faceted specimen crystal','cyan','Sample',0)
 cylinder('chamber ceiling',(cx,0,1.75),.32,.12,'dark',n=12)
 lamp('chamber top light',(cx,-.23,1.705),(.38,.025,.035))
 # Top canopy custom chamfered outline.
 prism('yellow sculpted canopy',[(-.67,1.74),(.63,1.74),(.60,1.89),(.49,1.98),(-.49,1.98),(-.65,1.89)],1,-.33,.37,'yellow',bevel=.018)
 for x in (-.49,.46):
  box('roof graphite service inset',(x,.02,1.985),(.20,.28,.018),'dark',bevel=.015)
 for x in (-.50,-.08,.49):bolt((x,-.35,1.83),r=.028)
 lamp('roof status',(0,-.12,1.991),(.20,.04,.025))
 # Angled monitor with geometric cyan diagrams, distinct articulated hinge.
 bone('Monitor',(.34,.10,1.40));box('monitor outer shell',(.34,-.125,1.51),(.53,.14,.42),'steel','Monitor',.035)
 box('monitor dark bezel',(.34,-.205,1.51),(.45,.035,.34),'black','Monitor',.02)
 box('monitor screen',(.34,-.229,1.51),(.385,.013,.275),'glass','Monitor',.013)
 for i,length in enumerate((.13,.20,.15,.22)):
  box('screen readout',(.39,-.241,1.60-i*.055),(length,.008,.012),'cyan','Monitor',.002)
 # Flask silhouette on left of monitor.
 beam('screen specimen symbol',(.215,-.242,1.47),(.235,-.242,1.56),.015,'cyan','Monitor')
 beam('screen specimen symbol',(.255,-.242,1.47),(.235,-.242,1.56),.015,'cyan','Monitor')
 cylinder('monitor pivot',(.34,.06,1.38),.055,.35,'dark',axis='X')
 # Right side folding shelf: properly connected hinge and bracket.
 bone('Shelf',(.63,.03,1.01));box('fold-out laboratory shelf',(.88,-.015,1.015),(.49,.78,.07),'yellow','Shelf',.025)
 box('shelf working inset',(.88,-.015,1.06),(.41,.65,.02),'steel','Shelf',.012)
 cylinder('shelf hinge',(.63,.03,1.01),.035,.65,'steel',axis='Y')
 beam('shelf diagonal supporting bracket',(.64,.22,.72),(1.02,.22,.985),.055,'dark','Shelf')
 for x in (.77,.88,.99):
  cylinder('sample vial holder',(x,.19,1.09),.034,.06,'dark','Shelf',n=8)
  cylinder('cyan sample vial',(x,.19,1.16),.018,.10,'cyan','Shelf',n=8)
  cylinder('vial cap',(x,.19,1.22),.023,.02,'steel','Shelf',n=8)
 box('shelf specimen case',(.88,-.22,1.13),(.20,.18,.13),'yellow','Shelf',.012)
 # Left handle and coherent ventilation.
 for y in (-.22,.22):beam('side handle mount',(-.65,y,1.3),(-.72,y,1.3),.035,'steel')
 beam('side carry handle',(-.72,-.22,1.3),(-.72,.22,1.3),.045,'dark')
 for i in range(5):box('left side vent',(-.625,.04,.49+i*.06),(.015,.36,.022),'black',bevel=.002)
 # Rear antenna dish and extendable mast.
 cylinder('antenna base',(.42,.30,1.995),.075,.08,'dark');bone('Antenna',(.42,.30,2.01))
 cylinder('antenna mast',(.42,.30,2.22),.018,.43,'steel','Antenna',n=8)
 cylinder('antenna yellow sleeve',(.42,.30,2.20),.029,.16,'yellow','Antenna',n=8)
 cylinder('antenna cyan tip',(.42,.30,2.45),.018,.05,'cyan','Antenna',n=8)
 bone('Dish',(.15,.26,2.0));beam('dish support',(.15,.26,2.0),(.15,.26,2.16),.035,'steel','Dish')
 box('octagonal comms plate',(.15,.25,2.22),(.27,.065,.25),'steel','Dish',.065,rot=(.35,0,-.25))
 for x in (-.49,.49):
  beam('rear vertical protective rail',(x,.435,.29),(x,.435,1.74),.055,'dark')
 box('rear bolted access panel',(0,.41,.60),(.78,.05,.52),'steel',bevel=.025)
 for x in (-.32,.32):
  for z in (.42,.78):bolt((x,.45,z))
def drone():
 # World-space metres, main shell ~45cm wide; four physical duct rotors ~1m span.
 bone('Body',(0,0,.53));j='Body'
 prism('faceted graphite fuselage',[(-.24,.44),(.24,.44),(.29,.58),(.21,.74),(-.19,.74),(-.29,.58)],1,-.28,.28,'dark',j,.017)
 prism('yellow upper armor',[(-.27,.57),(.27,.57),(.22,.76),(-.17,.79),(-.28,.66)],1,-.26,.23,'yellow',j,.018)
 box('upper black service plate',(0,-.01,.795),(.24,.30,.025),'dark',j,.02)
 for i in range(3):box('upper cooling slot',(0,-.08+i*.065,.811),(.15,.028,.009),'steel',j,.003)
 for x in (-.24,.24):
  for y in (-.19,.16):bolt((x,y,.75),j,'Z',.013)
 # Side maintenance armour, cut-in cooling slots and a separate rear service plate.
 for side in (-1,1):
  prism('faceted cheek armor',[(-.16,.59),(.18,.59),(.15,.70),(-.10,.73)],0,side*.266-.012,side*.266+.012,'edge',j,.006)
  for i in range(3):box('side recessed vent',(side*.282,.025+i*.055,.656),(.007,.022,.055),'black',j,.002,rot=(.22,0,0))
  for y in (-.12,.17):bolt((side*.286,y,.62),j,'X',.012)
 box('rear dark service cassette',(0,.293,.585),(.25,.035,.17),'steel',j,.012)
 for x in (-.085,.085):bolt((x,.318,.61),j,r=.01)
 # Eyes face -Y. Mounted as actual optically stacked lens assemblies.
 for x in (-.10,.10):
  cylinder('camera lens outer bezel',(x,-.292,.56),.076,.045,'steel',j,'Y',12)
  cylinder('camera dark socket',(x,-.32,.56),.060,.025,'black',j,'Y',12)
  cylinder('cyan optical eye',(x,-.337,.56),.042,.012,'cyan',j,'Y',12,0)
  cylinder('inner optical pupil',(x,-.346,.56),.022,.005,'glass',j,'Y',12,0)
 for x in (-.245,.245):lamp('amber warning', (x,-.18,.69),(.055,.035,.04),j,'amber')
 # Ducts retain real empty interiors; only rotors spin, outer rings stay attached.
 for i,(x,y) in enumerate(((-.46,-.27),(.46,-.27),(-.46,.30),(.46,.30))):
  p=Vector((x,y,.56));hip=Vector((math.copysign(.20,x),y*.43,.55));joint='Rotor'+str(i);bone(joint,p,'Body')
  beam('connected outrigger structural arm',hip,p,.07,'dark',j,.095)
  beam('arm top steel reinforcement',hip+Vector((0,0,.047)),p+Vector((0,0,.047)),.04,'steel',j,.065)
  cylinder('arm hinge pin',hip,.048,.13,'steel',j,'Y',10)
  ring('yellow rotor safety duct',p,.197,.170,.105,'yellow',j,20)
  ring('duct lower graphite rim',p-Vector((0,0,.055)),.195,.171,.032,'dark',j,20)
  ring('duct upper steel lip',p+Vector((0,0,.057)),.198,.174,.015,'steel',j,20)
  cylinder('stationary motor',p,.044,.11,'dark',j,n=12)
  for a in (0,math.pi/2):
   d=Vector((math.cos(a),math.sin(a),0))*.172
   beam('motor support cross',p-d-Vector((0,0,.037)),p+d-Vector((0,0,.037)),.016,'steel',j)
  cylinder('cyan motor collar',p+Vector((0,0,.035)),.048,.014,'cyan',joint,n=12,bevel=0)
  cylinder('rotor center hub',p+Vector((0,0,.054)),.038,.026,'steel',joint,n=12)
  for k in range(3):
   a=math.tau*k/3;d=Vector((math.cos(a),math.sin(a),0));box('swept rotor blade',p+d*.095+Vector((0,0,.045)),(.115,.043,.013),'dark',joint,.009,rot=(.08,.1,a))
  lamp('duct forward cyan strip',p+Vector((0,-.192,0)),(.08,.015,.022),j)
  for a in (0,math.pi/2,math.pi,math.pi*1.5):bolt(p+Vector((math.cos(a)*.185,math.sin(a)*.185,.065)),j,'Z',.009)
 # Battery above rear edge, physically seated in a tray.
 bone('Battery',(0,.23,.70),'Body');box('battery support tray',(0,.31,.76),(.33,.25,.07),'steel',j,.016)
 box('detachable rear battery',(0,.34,.85),(.32,.18,.17),'yellow','Battery',.025)
 box('battery charge bezel',(0,.241,.865),(.26,.026,.10),'black','Battery',.012)
 for i in range(5):
  key='Charge'+str(i);bone(key,(-.094+i*.047,.222,.865),'Battery')
  box('separate charge indicator',(-.094+i*.047,.222,.865),(.027,.012,.065),'cyan',key,.004)
 for x in (-.14,.14):bolt((x,.234,.90),'Battery',r=.01)
 # Underslung independently yawing mount and recoil emitter.
 cylinder('underbody turntable',(0,0,.40),.105,.075,'steel',j,n=12)
 bone('Aim',(0,0,.36),'Body');cylinder('gimbal swivel',(0,0,.35),.080,.045,'dark','Aim',n=12)
 bone('Emitter',(0,-.06,.30),'Aim');box('defensive emitter receiver',(0,-.04,.30),(.13,.26,.12),'dark','Emitter',.021)
 cylinder('emitter front collar',(0,-.188,.30),.066,.065,'steel','Emitter','Y',10)
 cylinder('emitter aperture',(0,-.225,.30),.046,.015,'black','Emitter','Y',10,0)
 cylinder('emitter cyan core',(0,-.235,.30),.029,.008,'cyan','Emitter','Y',10,0)
 bone('Muzzle',(0,-.25,.30),'Emitter')
 # Two folded landing skids with actual hinges.
 for side in (-1,1):
  key='Skid'+str(side);bone(key,(side*.20,.08,.43),'Body')
  beam('landing strut',(side*.20,.08,.43),(side*.23,-.13,.28),.035,'dark',key)
  beam('yellow landing skid',(side*.23,-.23,.26),(side*.23,.21,.26),.040,'yellow',key,.055)
def barricade():
 # Fixed end posts + telescoping panel, 2.4m wide, 0.95m high.
 for side in (-1,1):
  x=side*1.12
  prism('octagonal upright',[(x-.10,.04),(x+.10,.04),(x+.10,.88),(x+.055,.96),(x-.06,.96),(x-.10,.90)],1,-.11,.11,'yellow',bevel=.012)
  box('upright graphite insert',(x,-.126,.47),(.10,.025,.64),'dark',bevel=.015)
  box('upright steel sole',(x,0,.045),(.25,.28,.09),'steel',bevel=.02)
  for z in (.21,.74):bolt((x,-.15,z),r=.025)
  for direction in (-1,1):
   key='Foot'+str(side)+str(direction);bone(key,(x,direction*.09,.38))
   a=Vector((x,direction*.09,.38));b=Vector((x,direction*.48,.075))
   beam('folding yellow outrigger',a,b,.075,'yellow',key,.10)
   beam('triangular lower brace',(x,direction*.09,.09),b,.045,'yellow',key,.065)
   box('wide traction foot',(x,direction*.48,.045),(.23,.22,.09),'dark',key,.025)
   cylinder('foot hinge pin',a,.045,.15,'steel',key,'X',10)
   bolt((x+side*.082,direction*.09,.38),key,'X',.022)
 # Lower panel stays fixed, upper raises vertically and overlaps lower with nested guide.
 box('lower graphite panel',(0,0,.30),(2.02,.16,.44),'dark',bevel=.025)
 box('lower steel sill',(0,-.005,.09),(2.08,.21,.10),'steel',bevel=.018)
 bone('Panel',(0,0,.50));box('upper telescoping panel',(0,.035,.68),(2.02,.13,.43),'dark','Panel',.025)
 for x in (-.66,0,.66):
  box('upper recessed armor plate',(x,-.039,.69),(.60,.028,.32),'steel','Panel',.025)
  box('lower recessed armor plate',(x,-.096,.30),(.60,.025,.28),'steel',bevel=.022)
 for x in (-.34,.34):
  box('lower yellow panel stiffener',(x,-.126,.30),(.066,.048,.38),'yellow',bevel=.009)
  box('upper yellow sliding stiffener',(x,-.077,.69),(.066,.048,.39),'yellow','Panel',.009)
  for z in (.15,.43):bolt((x,-.158,z))
  for z in (.57,.82):bolt((x,-.108,z),'Panel')
 prism('upper sloped protective lip',[(-1.04,.85),(1.04,.85),(1.04,.91),(.98,.96),(-.96,.96),(-1.04,.91)],1,-.14,.15,'yellow','Panel',.012)
 box('top lip graphite inset',(0,-.01,.97),(1.70,.12,.012),'dark','Panel',.005)
 for x in (-.64,0,.64):lamp('cyan identification strip',(x,-.065,.80),(.17,.015,.017),'Panel')
 for side in (-1,1):
  x=side*1.015
  cylinder('sliding rail guide',(x,0,.54),.032,.60,'steel',n=10)
  for z in (.26,.76):box('rail retaining bracket',(x,-.012,z),(.075,.22,.075),'dark',bevel=.01)
  for z in (.18,.81):
   cylinder('side panel hinge',(side*1.07,-.13,z),.035,.13,'steel',axis='Z',n=10)
 for x in (-.78,-.60,-.42,.42,.60,.78):box('flush painted yellow safety slash',(x,-.1115,.23),(.14,.004,.028),'yellow',bevel=0,rot=(0,-.7,0))
 # Rear bracing is visible and structural, not an empty back.
 beam('rear cross brace',(-.94,.13,.15),(.94,.13,.45),.045,'steel')
 beam('rear cross brace',(.94,.13,.15),(-.94,.13,.45),.045,'steel')
def rig_asset():
 arm=bpy.data.armatures.new(PREFIX+' mechanical skeleton');rig=bpy.data.objects.new(PREFIX+' Rig',arm);C.objects.link(rig)
 bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
 for name,(p,parent) in BONES.items():
  b=arm.edit_bones.new(name);b.head=p;b.tail=p+Vector((0,0,.10));b.parent=arm.edit_bones.get(parent) if parent else None
 bpy.ops.object.mode_set(mode='OBJECT');rig.select_set(False)
 for o in PARTS:o.parent=rig;mod=o.modifiers.new('Rigid mechanical skin','ARMATURE');mod.object=rig
 return rig
def animations(rig,kind):
 rig.animation_data_create();clips=[]
 def action(label,seconds,fn):
  name=PREFIX+'_'+label;a=bpy.data.actions.new(name);a.use_fake_user=True;rig.animation_data.action=a;count=round(seconds*30)+1
  for f in range(1,count+1):
   t=(f-1)/(count-1)
   for b in rig.pose.bones:b.rotation_mode='QUATERNION';b.location=(0,0,0);b.rotation_quaternion=(1,0,0,0);b.scale=(1,1,1)
   fn(t,rig.pose.bones)
   for b in rig.pose.bones:
    b.keyframe_insert(data_path='location',frame=f);b.keyframe_insert(data_path='rotation_quaternion',frame=f);b.keyframe_insert(data_path='scale',frame=f)
  for layer in a.layers:
   for strip in layer.strips:
    for bag in getattr(strip,'channelbags',[]):
     for fc in bag.fcurves:
      for p in fc.keyframe_points:p.interpolation='LINEAR'
  track=rig.animation_data.nla_tracks.new();track.name=name;s=track.strips.new(name,1,a);s.action_slot=a.slots[0];track.mute=True;clips.append(name)
 def rotate(b,axis,r):b.rotation_quaternion=Quaternion(axis,r)
 def smooth(t):return t*t*(3-2*t)
 if kind=='ResearchStation':
  def idle(t,b):rotate(b['Sample'],(0,1,0),math.tau*t);rotate(b['Dish'],(0,1,0),math.sin(t*math.tau)*.08)
  action('Idle',4,idle)
  action('Research',3,lambda t,b:(idle(t,b),setattr(b['Sample'],'location',(0,.02*math.sin(t*math.tau),0)),rotate(b['Monitor'],(1,0,0),.03*math.sin(t*math.tau))))
  def deploy(t,b):
   k=1-smooth(t);rotate(b['Shelf'],(0,0,1),-1.48*k);b['Antenna'].location.y=-.32*k;rotate(b['Monitor'],(1,0,0),.12*k)
  action('Deploy',1.5,deploy);action('Retract',1.5,lambda t,b:deploy(1-t,b))
  action('Service',3,lambda t,b:[setattr(b['Drawer'+str(i)],'location',(0,0,.20*math.sin(math.pi*t)**2 if i==0 else 0)) for i in range(3)])
 elif kind=='SecurityDrone':
  def hover(t,b):
   b['Body'].location.y=.025*math.sin(t*math.tau)
   for i in range(4):rotate(b['Rotor'+str(i)],(0,1,0),math.tau*6*t*(1 if i%2 else -1))
  action('Hover',2,hover)
  def deploy(t,b):
   hover(t,b);k=1-smooth(t);b['Body'].location.y=-.19*k
   for side in (-1,1):rotate(b['Skid'+str(side)],(1,0,0),.65*k)
  action('Deploy',1,deploy)
  action('AimSweep',3,lambda t,b:(hover(t,b),rotate(b['Aim'],(0,1,0),math.sin(t*math.tau)*.7)))
  action('Fire',.4,lambda t,b:(hover(t,b),setattr(b['Emitter'],'location',(0,0,-.035*math.sin(math.pi*t)**2))))
  def depleted(t,b):
   hover(t,b)
   for i in range(5):
    k=1 if t<(i+1)/6 else .02;b['Charge'+str(4-i)].scale=(k,k,k)
   rotate(b['Body'],(0,0,1),math.sin(t*math.tau*5)*.025*t)
  action('BatteryDrain',5,depleted)
  def overload(t,b):
   depleted(1,b);rotate(b['Body'],(1,0,0),math.sin(t*math.tau*6)*.035*t)
   b['Body'].location.y=.025*math.sin(t*math.tau*8)
  action('Overload',1,overload)
 else:
  action('Idle',1,lambda t,b:None)
  def deploy(t,b):
   k=1-smooth(t);b['Panel'].location.y=-.37*k
   for side in (-1,1):
    for direction in (-1,1):rotate(b['Foot'+str(side)+str(direction)],(1,0,0),direction*1.10*k)
  action('Deploy',1.2,deploy);action('Retract',1.2,lambda t,b:deploy(1-t,b))
  action('Hit',.3,lambda t,b:rotate(b['Root'],(1,0,0),.012*math.sin(t*math.tau)*(1-t)))
 rig.animation_data.action=None;rig.animation_data.use_nla=False;S.frame_set(1)
 return clips
def build(kind):
 global C,PARTS,BONES,PREFIX
 PREFIX=kind;name='RD_'+kind;old=bpy.data.collections.get(name)
 if old:
  for o in list(old.objects):bpy.data.objects.remove(o,do_unlink=True)
  bpy.data.collections.remove(old)
 for a in list(bpy.data.actions):
  if a.name.startswith(kind+'_'):bpy.data.actions.remove(a)
 C=bpy.data.collections.new(name);S.collection.children.link(C);PARTS=[];BONES={};bone('Root',(0,0,0),None)
 {'ResearchStation':research,'SecurityDrone':drone,'FieldBarricade':barricade}[kind]()
 rig=rig_asset();clips=animations(rig,kind);rig['asset_id']=kind;rig['clips']=','.join(clips)
 bpy.context.view_layer.update()
 points=[o.matrix_world@Vector(c) for o in PARTS for c in o.bound_box]
 entry=dict(id=kind,parts=len(PARTS),bones=len(BONES),triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in PARTS),min=[min(p[i] for p in points) for i in range(3)],max=[max(p[i] for p in points) for i in range(3)],clips=clips)
 path=OUT/'models.json';manifest=json.loads(path.read_text()) if path.exists() else {'assets':[]};manifest['assets']=[a for a in manifest['assets'] if a['id']!=kind]+[entry];path.write_text(json.dumps(manifest,indent=2));return entry
def reset(rig):
 rig.animation_data.action=None
 for b in rig.pose.bones:b.location=(0,0,0);b.rotation_quaternion=(1,0,0,0);b.scale=(1,1,1)
 S.frame_set(1);bpy.context.view_layer.update()
def export(kind):
 col=bpy.data.collections['RD_'+kind];rig=next(o for o in col.objects if o.type=='ARMATURE');reset(rig)
 bpy.ops.object.select_all(action='DESELECT');copies=[]
 for o in list(col.objects):
  if o.type=='MESH':
   cp=o.copy();cp.data=o.data.copy();col.objects.link(cp);cp.select_set(True);copies.append(cp)
 bpy.context.view_layer.objects.active=copies[0];bpy.ops.object.join();skin=bpy.context.object;skin.name=kind+' game skin';rig.select_set(True);bpy.context.view_layer.objects.active=rig
 bpy.ops.export_scene.gltf(filepath=str(OUT/(kind+'.glb')),export_format='GLB',use_selection=True,export_animations=True,export_force_sampling=True,export_animation_mode='ACTIONS',export_anim_single_armature=False,export_nla_strips=True,export_def_bones=False,export_skins=True,export_cameras=False,export_lights=False)
 bpy.data.objects.remove(skin,do_unlink=True);return str(OUT/(kind+'.glb'))
def preview(kind,clip=None,frame=1):
 col=bpy.data.collections['RD_'+kind]
 for c in S.collection.children:
  if c.name.startswith('RD_'):c.hide_render=c!=col
 rig=next(o for o in col.objects if o.type=='ARMATURE');reset(rig)
 if clip:rig.animation_data.action=bpy.data.actions[kind+'_'+clip]
 S.frame_set(frame);bpy.context.view_layer.update();pts=[o.matrix_world@Vector(c) for o in col.objects if o.type=='MESH' for c in o.bound_box];lo=Vector(tuple(min(p[i] for p in pts) for i in range(3)));hi=Vector(tuple(max(p[i] for p in pts) for i in range(3)));center=(lo+hi)/2;span=max(hi-lo)
 def obj(name,typ):
  o=bpy.data.objects.get(name)
  if not o:o=bpy.data.objects.new(name,bpy.data.cameras.new(name) if typ=='CAMERA' else bpy.data.lights.new(name,typ));S.collection.objects.link(o)
  return o
 camera=obj('RD studio camera','CAMERA');S.camera=camera;camera.location=center+Vector((1.4,-2.6,1.5))*span;camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=span*1.33
 for name,delta,power,color in [('RD warm key',(1,-2,2.8),900,(1,.90,.73)),('RD soft fill',(-2,-1,1.2),650,(.74,.84,1)),('RD rim',(1,2,2.2),1100,(1,.85,.65))]:
  o=obj(name,'AREA');o.location=center+Vector(delta)*span;o.rotation_euler=(center-o.location).to_track_quat('-Z','Y').to_euler();o.data.energy=power*span*span/4;o.data.size=span*1.5;o.data.color=color
 floor=bpy.data.objects.get('RD studio floor')
 if not floor:
  bpy.ops.mesh.primitive_plane_add(size=200);floor=bpy.context.object;floor.name='RD studio floor';floor.data.materials.append(material('studio sand',(.22,.19,.15),0,.9))
 floor.location.z=lo.z-.02
 S.render.filepath=str(OUT/(kind+('-'+clip+str(frame) if clip else '')+'.png'));bpy.ops.render.render(write_still=True);return S.render.filepath
