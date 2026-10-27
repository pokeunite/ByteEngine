"""Run inside Blender MCP. Reference-built rigid mechanical rigs, editable parts and glTF clips."""
import bpy,math,json
from pathlib import Path
from mathutils import Vector,Quaternion
WORK=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine');OUT=WORK/'artifacts/defence-models';OUT.mkdir(parents=True,exist_ok=True)
NAMES={'sentry':'Sentry','repeater':'Repeater','mortar':'Mortar','mine':'Mine','supply-cache':'SupplyCache'}
S=bpy.data.scenes.get('Defence Model Workshop') or bpy.data.scenes.new('Defence Model Workshop');bpy.context.window.scene=S
S.render.engine='BLENDER_EEVEE';S.render.resolution_x=1000;S.render.resolution_y=1000;S.render.resolution_percentage=100;S.render.fps=30;S.view_settings.view_transform='AgX';S.world=S.world or bpy.data.worlds.new('Defence studio');S.world.use_nodes=True;S.world.node_tree.nodes['Background'].inputs[0].default_value=(.18,.16,.14,1);S.world.node_tree.nodes['Background'].inputs[1].default_value=.4

def mat(name,color,metal=0,rough=.55,emission=0):
 m=bpy.data.materials.get(name) or bpy.data.materials.new(name);m.use_nodes=True;m.diffuse_color=(*color,1);n=m.node_tree.nodes.get('Principled BSDF');n.inputs['Base Color'].default_value=(*color,1);n.inputs['Metallic'].default_value=metal;n.inputs['Roughness'].default_value=rough
 if emission:n.inputs['Emission Color'].default_value=(*color,1);n.inputs['Emission Strength'].default_value=emission
 return m
M={'yellow':mat('Defence ochre armour',(.58,.28,.012),.32,.48),'edge':mat('Defence yellow edge',(.80,.43,.025),.3,.43),'shadow':mat('Defence shaded ochre',(.38,.19,.025),.35,.57),'dark':mat('Defence graphite',(.025,.030,.038),.55,.5),'steel':mat('Defence steel',(.085,.10,.12),.72,.36),'black':mat('Defence recess',(.009,.012,.015),.2,.68),'cyan':mat('Defence cyan status',(.015,.65,.76),.3,.26,2),'brass':mat('Defence feed rounds',(.49,.29,.08),.65,.4),'red':mat('Defence armed amber',(.95,.12,.015),.15,.35,1.8)}
S.view_settings.look='AgX - Medium High Contrast';S.view_settings.exposure=-.25
C=None;PARTS=[];SPEC={};ID='';PREFIX=''
def bone(name,p,parent='Root'):
 SPEC[name]=(Vector(p),parent)
def finish(o,name,material,joint='Root',bevel=0):
 o.name=PREFIX+' '+name
 for c in list(o.users_collection):c.objects.unlink(o)
 C.objects.link(o);o.data.materials.append(M[material]);bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
 if bevel:
  mod=o.modifiers.new('Machined bevel','BEVEL');mod.width=bevel;mod.segments=1;bpy.ops.object.modifier_apply(modifier=mod.name)
 for p in o.data.polygons:p.use_smooth=False
 g=o.vertex_groups.new(name=joint);g.add(list(range(len(o.data.vertices))),1,'REPLACE');o['assembly']=joint;PARTS.append(o);o.select_set(False);return o
def box(name,p,size,material='yellow',joint='Root',bevel=.025,rot=None):
 bpy.ops.mesh.primitive_cube_add(size=1,location=p);o=bpy.context.object;o.dimensions=size
 if rot:o.rotation_euler=rot
 return finish(o,name,material,joint,bevel)
def cyl(name,p,r,depth,material='steel',joint='Root',axis='Z',n=12,bevel=.009):
 bpy.ops.mesh.primitive_cylinder_add(vertices=n,radius=r,depth=depth,location=p);o=bpy.context.object
 o.rotation_euler=(0,math.pi/2,0) if axis=='X' else (math.pi/2,0,0) if axis=='Y' else (0,0,0)
 return finish(o,name,material,joint,bevel)
def beam(name,a,b,w,material='dark',joint='Root',thickness=None):
 a,b=Vector(a),Vector(b);mid=(a+b)/2;rot=(b-a).to_track_quat('Z','Y').to_euler();return box(name,mid,(w,thickness or w,(b-a).length),material,joint,w*.15,rot)
def prism(name,outline,axis,lo,hi,material='yellow',joint='Root',bevel=.02):
 # outline in the other two axes; extruded armour profile remains editable.
 axes=[i for i in range(3) if i!=axis];v=[]
 for d in [lo,hi]:
  for a,b in outline:
   p=[0,0,0];p[axis]=d;p[axes[0]]=a;p[axes[1]]=b;v.append(p)
 n=len(outline);faces=[tuple(range(n-1,-1,-1)),tuple(range(n,n*2))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 me=bpy.data.meshes.new(name);me.from_pydata(v,[],faces);me.update();o=bpy.data.objects.new(name,me);C.objects.link(o);return finish(o,name,material,joint,bevel)
def tube(name,p,outer,inner,length,material,joint,axis='Y',n=12):
 # True hollow muzzle with inner wall and dark bore, never a capped cylinder.
 v=[];faces=[]
 for y,r in [(-length/2,outer),(length/2,outer),(-length/2,inner),(length/2,inner)]:
  for i in range(n):
   a=math.tau*i/n;point=Vector((math.cos(a)*r,y,math.sin(a)*r))
   if axis=='Z':point=Vector((point.x,point.z,point.y))
   v.append(tuple(Vector(p)+point))
 for i in range(n):
  j=(i+1)%n;faces.extend([(i,j,n+j,n+i),(2*n+j,2*n+i,3*n+i,3*n+j),(i,2*n+i,2*n+j,j),(n+i,n+j,3*n+j,3*n+i)])
 me=bpy.data.meshes.new(name);me.from_pydata(v,[],faces);me.update();o=bpy.data.objects.new(name,me);C.objects.link(o);return finish(o,name,material,joint,0)
def screw(name,p,joint='Root',axis='Y',r=.035):
 cyl(name,p,r,.022,'steel',joint,axis,6,0)
def status(p,size,joint='Root'):
 box('recessed status bezel',p,tuple(v*1.28 for v in size),'black',joint,.008);q=Vector(p);q.y-=.008;box('cyan status inset',q,size,'cyan',joint,.006)
def legs(count,radius=1.35,height=.62):
 for i in range(count):
  a=math.tau*i/count+math.pi/4;d=Vector((math.cos(a),math.sin(a),0));hip=d*.43+Vector((0,0,height));knee=d*.86+Vector((0,0,.36));foot=d*radius+Vector((0,0,.13));upper='Leg'+str(i);lower='Knee'+str(i);shoe='Foot'+str(i);bone(upper,hip);bone(lower,knee,upper);bone(shoe,foot,lower)
  beam('leg inner structural link',hip,knee,.18,'dark',upper,.22);beam('leg yellow upper armour',hip+d*.04,knee+d*.04,.28,'yellow',upper,.30)
  beam('leg lower graphite link',knee,foot,.17,'dark',lower,.23);beam('leg lower armour',knee+d*.04,foot+d*.04,.27,'edge',lower,.28)
  for j,p in [(upper,hip),(lower,knee),(shoe,foot)]:
   # Angular caps remain with the moving joint.
   cyl('outrigger pivot hub',p,.14,.23,'dark',j,'X',10);cyl('outrigger hub cap',p+Vector((.13,0,0)),.09,.035,'steel',j,'X',8)
  box('broad stabilizer shoe',foot-Vector((0,0,.055)),(.55,.43,.14),'dark',shoe,.055,rot=(0,0,a));box('foot sole plate',foot-Vector((0,0,.105)),(.60,.48,.055),'steel',shoe,.035,rot=(0,0,a))
  box('leg status inset',knee+Vector((0,-.145,.04)),(.075,.02,.04),'cyan',lower,.004)
  beam('exposed hydraulic piston',hip+Vector((0,0,-.12)),knee+Vector((0,0,-.06)),.065,'steel',upper)
  box('foot traction pad',foot+Vector((0,0,.031)),(.30,.22,.015),'black',shoe,.012,rot=(0,0,a))
  screw('upper leg captive bolt',hip+Vector((0,-.17,.05)),upper,'Y',.035)
def base(kind):
 cyl('lower octagonal armoured plinth',(0,0,.37),.60,.36,'dark',n=12,bevel=.035)
 cyl('upper yellow segmented ring',(0,0,.62),.64,.21,'yellow',n=12,bevel=.025)
 for i in range(8):
  a=math.tau*i/8;p=(.60*math.cos(a),.60*math.sin(a),.62);box('ring recessed service plate',p,(.13,.045,.09),'dark',bevel=.012,rot=(0,0,a+math.pi/2))
 cyl('yaw lower bearing',(0,0,.79),.43,.16,'steel',n=16);bone('Yaw',(0,0,.82));cyl('yaw rotating turret ring',(0,0,.88),.46,.13,'dark','Yaw',n=16)
 for h in [.29,.44,.77,.86]:cyl('machined bearing band',(0,0,h),.61 if h<.5 else .44,.035,'steel',n=16,bevel=.005)
 for i in range(8):
  a=math.tau*i/8;screw('base ring top fastener',(.52*math.cos(a),.52*math.sin(a),.735),'Root','Z',.028)
 status((0,-.62,.60),(.18,.018,.065))
 legs(4,1.32 if kind=='sentry' else 1.48,.62)
def turret(kind):
 base(kind);z=1.48 if kind=='sentry' else 1.60;bone('Pitch',(0,.05,z),'Yaw');bone('Recoil',(0,-.45,z),'Pitch')
 # Cradle and elevation trunnions, mounted to yaw assembly.
 for x in [-.58,.58]:
  prism('angled elevation support',[(-.2,.86),(.25,.86),(.47,z-.12),(.24,z+.12),(-.16,z+.1)],0,x-.08,x+.08,'yellow','Yaw')
  exterior=x+(.20 if x>0 else -.20)
  cyl('large elevation bearing',(exterior,.05,z),.24,.13,'dark','Yaw','X',12);cyl('bearing silver cap',(exterior+(.075 if x>0 else -.075),.05,z),.15,.028,'steel','Yaw','X',12)
 # Graphite receiver and characteristic swept side armour.
 box('main graphite receiver',(0,.02,z+.10),(1.10,1.10,.70),'dark','Pitch',.08)
 outline=[(-.64,z-.15),(-.58,z+.42),(-.33,z+.56),(.48,z+.49),(.63,z+.27),(.58,z-.18),(.23,z-.30),(-.36,z-.30)]
 for x in [-.59,.59]:
  prism('swept yellow side armour',outline,0,x-.085,x+.085,'yellow','Pitch',.028)
  for y,h in [(-.42,z+.32),(.40,z+.30),(-.30,z-.17),(.36,z-.16)]:screw('side panel captive hex bolt',(x+(-.10 if x<0 else .10),y,h),'Pitch','X',.044)
  box('lower dark side inset',(x+(-.09 if x<0 else .09),.15,z-.13),(.025,.43,.12),'black','Pitch',.01)
  for j in range(3):box('angled side cooling slot',(x+(-.10 if x<0 else .10),.24+j*.08,z+.03),(.025,.035,.14),'black','Pitch',.006,rot=(.25,0,0))
 box('top yellow bridge plate',(0,.08,z+.51),(.93,.91,.13),'edge','Pitch',.035)
 for x in [-.26,.26]:box('top recessed rail',(x,.1,z+.585),(.14,.52,.025),'dark','Pitch',.008)
 for x in [-.35,.35]:
  for y in [-.27,.43]:screw('upper armour fastener',(x,y,z+.585),'Pitch','Z',.025)
 for x in [-.68,.68]:
  box('side replaceable panel',(x,.24,z+.17),(.035,.28,.22),'shadow','Pitch',.016)
  for y in [.15,.32]:screw('service panel screw',(x+(-.025 if x<0 else .025),y,z+.20),'Pitch','X',.022)
 if kind=='sentry':
  barrel('single barrel',0,z+.09,-1.04,.16,.92)
  box('front sensor housing',(-.30,-.59,z+.15),(.26,.12,.51),'black','Pitch',.04)
  for h,r in [(z+.28,.095),(z+.04,.065)]:cyl('cyan optical lens',(-.30,-.67,h),r,.025,'cyan','Pitch','Y',12,0);cyl('optical bezel',(-.30,-.65,h),r*1.3,.04,'steel','Pitch','Y',12,0)
 else:
  for x in [-.30,.30]:barrel('twin barrel',x,z+.12,-1.40,.13,1.77)
  box('left ammunition feed box',(-.89,.23,z+.12),(.48,.70,.66),'dark','Pitch',.045);box('feed box yellow lid',(-.90,.23,z+.47),(.51,.72,.085),'yellow','Pitch',.02)
  for i in range(6):cyl('visible feed belt round',(-.64,-.10,z-.18+i*.09),.038,.25,'brass','Pitch','X',8,0)
  for x in [-.90,-1.08]:box('feed case rib',(x,.22,z+.1),(.035,.72,.57),'steel','Pitch',.01)
  box('receiver side sensor',(0,-.62,z+.46),(.20,.06,.08),'cyan','Pitch',.012)
 # Mechanical rear power pack and restrained armour seams.
 box('rear power module',(0,.64,z+.05),(.71,.23,.40),'shadow','Pitch',.035)
 for x in [-.23,0,.23]:box('rear cooling channel',(x,.77,z+.05),(.09,.015,.25),'black','Pitch',.004)
def barrel(name,x,z,y,r,length):
 cyl(name+' chamber',(x,-.48,z),r*1.4,.28,'steel','Recoil','Y',12,.015)
 tube(name+' outer tube',(x,y,z),r,r*.65,length,'dark','Recoil')
 for yy in [y+length*.30,y-length*.28]:tube(name+' collar',(x,yy,z),r*1.18,r*.65,.09,'steel','Recoil')
 muzzle=y-length/2-.10;tube(name+' hollow muzzle brake',(x,muzzle,z),r*1.48,r*.72,.28,'steel','Recoil')
 for side in [-1,1]:
  for yy in [muzzle-.075,muzzle+.045]:box(name+' brake gas port',(x+side*r*1.43,yy,z),(.012,.07,.065),'black','Recoil',.003)
 bone('Muzzle' if x==0 else ('MuzzleLeft' if x<0 else 'MuzzleRight'),(x,muzzle-.15,z),'Recoil')
def mortar():
 base('mortar');bone('Pitch',(0,.08,1.36),'Yaw');bone('Recoil',(0,-.1,1.55),'Pitch')
 # The outrigged yoke feet must overlap a real bridge, not float beside the round bearing.
 cyl('central swivel riser',(0,.06,.94),.34,.18,'dark','Yaw',n=12,bevel=.018)
 box('continuous bolted mounting bridge',(0,.10,.99),(1.34,.76,.16),'steel','Yaw',.035)
 box('bridge yellow upper plate',(0,.10,1.075),(1.26,.70,.05),'yellow','Yaw',.018)
 for x in [-.50,.50]:
  box('yoke bolted foot',(x,.18,1.10),(.25,.42,.10),'dark','Yaw',.018)
  prism('continuous elevation yoke',[(-.04,1.08),(.39,1.08),(.32,1.25),(.25,1.49),(-.06,1.49),(-.14,1.35)],0,x-.10,x+.10,'yellow','Yaw',.018)
  for y in [.04,.32]:screw('mounting foot bolt',(x,y,1.16),'Yaw','Z',.032)
  cyl('elevation hinge',(x,.08,1.36),.19,.20,'dark','Yaw','X',12)
  cyl('elevation bearing outer cap',(x+(.12 if x>0 else -.12),.08,1.36),.13,.035,'steel','Yaw','X',12)
 # Build tube already inclined 38 degrees upward from horizontal.
 axis=Vector((0,-.79,.61));centre=Vector((0,-.12,1.74));length=1.34
 # Temporarily build local horizontal tube assembly then rotate vertices about the pivot.
 before=len(PARTS);tube('open mortar launch tube',centre,.38,.285,length,'dark','Recoil')
 for off in [-.54,.40]:tube('armoured tube collar',centre+Vector((0,off,0)),.42,.285,.15,'yellow','Recoil')
 cyl('dark breech interior',centre+Vector((0,.66,0)),.283,.025,'black','Recoil','Y',12,0)
 for x in [-.35,.35]:
  box('tube side armour',(x,-.05,1.74),(.11,1.02,.47),'yellow','Recoil',.035)
  for yy in [-.40,.28]:screw('tube panel bolt',(x+(.065 if x>0 else -.065),yy,1.88),'Recoil','X',.032)
 box('tube top broad armour',(0,-.02,2.10),(.48,.88,.085),'edge','Recoil',.025)
 from mathutils import Matrix
 rotation=Matrix.Rotation(math.radians(-38),4,'X')
 for o in PARTS[before:]:
  for v in o.data.vertices:v.co=centre+rotation@(v.co-centre)
 bone('Muzzle',centre+axis*(length/2+.03),'Recoil')
 for x in [-.42,.42]:
  a=Vector((x,.40,1.03));b=Vector((x,-.30,1.62));beam('hydraulic elevation strut',a,b,.09,'steel','Pitch');beam('hydraulic cylinder barrel',a,a+(b-a)*.56,.15,'dark','Yaw')
 box('rear breech equipment',(0,.42,1.57),(.75,.41,.67),'dark','Pitch',.06);box('breech yellow top',(0,.42,1.93),(.79,.44,.09),'yellow','Pitch',.025)
 status((0,-.45,.95),(.19,.025,.065),'Yaw')
def mine():
 bone('Crown',(0,0,.18));bone('ArmingRing',(0,0,.18));
 cyl('low octagonal base',(0,0,.10),.49,.16,'dark',n=16,bevel=.02);cyl('upper segmented body',(0,0,.20),.44,.13,'steel',n=16)
 for i in range(8):
  a=math.tau*i/8;box('armoured perimeter segment',(.385*math.cos(a),.385*math.sin(a),.235),(.23,.17,.16),'yellow',bevel=.028,rot=(0,0,a+math.pi/2));screw('rim fastener',(.40*math.cos(a),.40*math.sin(a),.322),'Root','Z',.022)
 cyl('arming cyan ring',(0,0,.276),.27,.035,'cyan','ArmingRing',n=16,bevel=0);cyl('raised graphite crown',(0,0,.305),.23,.065,'dark','Crown',n=12,bevel=.015);cyl('centre activation plate',(0,0,.344),.16,.018,'steel','Crown',n=12,bevel=.005)
 for i in range(3):
  a=math.tau*i/3;d=Vector((math.cos(a),math.sin(a),0));p=d*.50+Vector((0,0,.07));name='Anchor'+str(i);bone(name,p);box('mine anchoring foot',p,(.18,.16,.11),'shadow',name,.025,rot=(0,0,a));cyl('anchor bolt',p+Vector((0,0,.07)),.035,.018,'steel',name,n=8,bevel=0)
def cache():
 bone('Lid',(0,.47,.94));bone('Latch',(0,-.56,.79));bone('HandleL',(-.79,0,.62));bone('HandleR',(.79,0,.62))
 box('crate interior bottom',(0,0,.24),(1.60,1.08,.15),'dark',bevel=.035)
 for x in [-.755,.755]:box('hollow crate side wall',(x,0,.59),(.09,1.08,.78),'dark',bevel=.025)
 for y in [-.495,.495]:box('hollow crate end wall',(0,y,.59),(1.43,.09,.78),'dark',bevel=.025)
 box('bottom reinforced sill',(0,0,.16),(1.69,1.15,.18),'steel',bevel=.04)
 for side in [-1,1]:
  for x in [-.54,.54]:box('front yellow armour panel',(x,side*.555,.53),(.43,.07,.57),'yellow',bevel=.035)
  box('centre front graphite plate',(0,side*.56,.56),(.48,.075,.47),'steel',bevel=.02)
  for x in [-.76,.76]:box('corner armour guard',(x,side*.49,.52),(.17,.20,.83),'yellow',bevel=.035)
 box('lid seal',(0,0,1.00),(1.68,1.13,.10),'black','Lid',.025);box('heavy armoured lid',(0,0,1.11),(1.70,1.18,.19),'yellow','Lid',.05)
 box('recessed lid graphite panel',(0,0,1.214),(1.12,.70,.025),'dark','Lid',.035)
 for x in [-.60,.60]:box('lid steel restraint strap',(x,0,1.22),(.12,1.14,.035),'steel','Lid',.015)
 for x in [-.50,.50]:cyl('rear lid hinge',(x,.56,.98),.065,.28,'steel','Root','X',10)
 box('front clasp base',(0,-.61,.83),(.23,.11,.32),'dark','Latch',.025);box('clasp silver face',(0,-.68,.83),(.15,.035,.20),'steel','Latch',.015)
 status((0,-.604,.63),(.18,.016,.04))
 for side,key in [(-1,'HandleL'),(1,'HandleR')]:
  x=side*.85
  for y in [-.19,.19]:beam('side handle bracket',(x,y,.51),(x,y,.73),.055,'steel',key)
  beam('side carry grip',(x,-.19,.73),(x,.19,.73),.075,'dark',key)
 for x in [-.72,.72]:
  for y in [-.45,.45]:box('heavy splayed crate foot',(x,y,.105),(.27,.28,.17),'dark',bevel=.045)
 # Complete interior remains visible when lid opens.
 box('inner cargo floor',(0,0,.33),(1.35,.82,.045),'black',bevel=.02)
 for x in [-.43,0,.43]:
  box('stored supply cassette',(x,0,.61),(.34,.68,.47),'shadow',bevel=.02)
  box('cassette lid',(x,0,.86),(.35,.70,.055),'steel',bevel=.014)
  box('cassette yellow ID strip',(x,-.35,.66),(.18,.012,.09),'yellow',bevel=.005)

def rig_asset():
 arm=bpy.data.armatures.new(PREFIX+' mechanical skeleton');rig=bpy.data.objects.new(PREFIX+' Rig',arm);C.objects.link(rig);bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
 for name,(p,parent) in SPEC.items():
  b=arm.edit_bones.new(name);b.head=p;b.tail=p+Vector((0,0,.12));b.parent=arm.edit_bones.get(parent) if parent else None
 bpy.ops.object.mode_set(mode='OBJECT');rig.select_set(False)
 for o in PARTS:
  o.parent=rig;mod=o.modifiers.new('Rigid mechanical skin','ARMATURE');mod.object=rig
 return rig
def animate(rig):
 names=[];rig.animation_data_create()
 def action(label,length,pose):
  a=bpy.data.actions.new(PREFIX+'_'+label);a.use_fake_user=True;rig.animation_data.action=a
  for f in range(1,length+1):
   t=(f-1)/(length-1)
   for b in rig.pose.bones:b.location=(0,0,0);b.rotation_mode='QUATERNION';b.rotation_quaternion=(1,0,0,0);b.scale=(1,1,1)
   pose(t,rig.pose.bones)
   for b in rig.pose.bones:
    b.keyframe_insert(data_path='location',frame=f);b.keyframe_insert(data_path='rotation_quaternion',frame=f);b.keyframe_insert(data_path='scale',frame=f)
  for layer in a.layers:
   for strip in layer.strips:
    for bag in getattr(strip,'channelbags',[]):
     for fc in bag.fcurves:
      for k in fc.keyframe_points:k.interpolation='LINEAR'
  names.append(a.name);return a
 def yaw(b,a):b.rotation_quaternion=Quaternion((0,1,0),a)
 def pitch(b,a):b.rotation_quaternion=Quaternion((1,0,0),a)
 def fold(t,b):
  k=1-(t*t*(3-2*t))
  if ID in ('sentry','repeater','mortar'):
   for i in range(4):
    a=math.tau*i/4+math.pi/4;axis=(math.sin(a),0,math.cos(a));b['Leg'+str(i)].rotation_quaternion=Quaternion(axis,k*.72);b['Knee'+str(i)].rotation_quaternion=Quaternion(axis,-k*.9)
   b['Root'].location.y=.18*k
  elif ID=='mine':
   for i in range(3):b['Anchor'+str(i)].scale=(1-k*.72,1,1-k*.72)
   b['Crown'].location.y=-.06*k;yaw(b['ArmingRing'],t*math.pi/2)
  else:b['Root'].location.y=.55*k;b['Root'].scale=(1-.1*k,)*3
 action('Deploy',46,fold)
 action('Retract',46,lambda t,b:fold(1-t,b))
 if ID in ('sentry','repeater','mortar'):
  action('Idle',91,lambda t,b:(yaw(b['Yaw'],math.sin(t*math.tau)*.24),pitch(b['Pitch'],math.sin(t*math.tau)*.025)))
  action('AimSweep',121,lambda t,b:(yaw(b['Yaw'],math.sin(t*math.tau)*.85),pitch(b['Pitch'],math.sin(t*math.tau)*.15)))
  def fire(t,b):
   kick=max(0,math.sin(math.pi*min(1,t/.24)))*(1-t)**2;b['Recoil'].location.z=-.12*kick
   if ID=='mortar':b['Recoil'].location.y=-.08*kick
   pitch(b['Pitch'],.045*kick)
  action('Fire',13 if ID!='mortar' else 25,fire)
 elif ID=='mine':
  action('Idle',61,lambda t,b:yaw(b['ArmingRing'],.08*math.sin(t*math.tau)))
  action('Arm',31,lambda t,b:(yaw(b['ArmingRing'],t*math.pi/2),setattr(b['Crown'],'location',(0,.035*math.sin(t*math.pi/2),0))))
  action('Trigger',16,lambda t,b:setattr(b['Crown'],'location',(0,-.07*math.sin(t*math.pi),0)))
 else:
  def lid(t,b,close=False):
   k=1-t if close else t;k=k*k*(3-2*k);pitch(b['Lid'],-math.radians(105)*k);b['Latch'].location.y=.11*min(1,k*4)
  action('Open',46,lambda t,b:lid(t,b));action('Close',46,lambda t,b:lid(t,b,True));action('Idle',61,lambda t,b:None)
 rig.animation_data.action=bpy.data.actions[PREFIX+'_Idle'];S.frame_set(1);S.frame_start=1;S.frame_end=91
 for name in names:
  track=rig.animation_data.nla_tracks.new();track.name=name;strip=track.strips.new(name,1,bpy.data.actions[name]);strip.action_slot=bpy.data.actions[name].slots[0];track.mute=True
 rig.animation_data.use_nla=False
 rig['animation_clips']=','.join(names);return names
def build(id):
 global C,PARTS,SPEC,ID,PREFIX
 if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
 bpy.ops.object.select_all(action='DESELECT')
 ID=id;PREFIX=NAMES[id];old=bpy.data.collections.get('DEFENCE_'+PREFIX)
 if old:
  for o in list(old.objects):bpy.data.objects.remove(o,do_unlink=True)
  bpy.data.collections.remove(old)
 for a in list(bpy.data.actions):
  if a.name.startswith(PREFIX+'_'):bpy.data.actions.remove(a)
 C=bpy.data.collections.new('DEFENCE_'+PREFIX);S.collection.children.link(C);PARTS=[];SPEC={};bone('Root',(0,0,0),None)
 if id in ('sentry','repeater'):turret(id)
 elif id=='mortar':mortar()
 elif id=='mine':mine()
 else:cache()
 rig=rig_asset();clips=animate(rig)
 # Pack the approved reference with the source file.
 path=WORK/'Docs/Concepts/AlienMining/Defences'/(id+'.png');im=bpy.data.images.load(str(path),check_existing=True);im.pack();rig['reference']=str(path);rig['asset_id']=id
 bpy.context.view_layer.update();points=[o.matrix_world@Vector(c) for o in PARTS for c in o.bound_box];lo=[min(p[i] for p in points) for i in range(3)];hi=[max(p[i] for p in points) for i in range(3)]
 entry={'id':id,'name':PREFIX,'parts':len(PARTS),'bones':len(SPEC),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in PARTS),'min':lo,'max':hi,'clips':clips}
 manifest=json.loads((OUT/'models.json').read_text()) if (OUT/'models.json').exists() else {'assets':[]};manifest['assets']=[a for a in manifest['assets'] if a['id']!=id]+[entry];(OUT/'models.json').write_text(json.dumps(manifest,indent=2));return entry
def preview(id,clip=None,frame=1):
 prefix=NAMES[id];col=bpy.data.collections['DEFENCE_'+prefix];rig=next(o for o in col.objects if o.type=='ARMATURE')
 for c in S.collection.children:
  if c.name.startswith('DEFENCE_'):c.hide_render=c!=col
 if clip:rig.animation_data.action=bpy.data.actions[prefix+'_'+clip]
 else:
  rig.animation_data.action=None
  for b in rig.pose.bones:b.location=(0,0,0);b.rotation_quaternion=(1,0,0,0);b.scale=(1,1,1)
 S.frame_set(frame);bpy.context.view_layer.update();objects=[o for o in col.objects if o.type=='MESH'];pts=[o.matrix_world@Vector(c) for o in objects for c in o.bound_box];lo=Vector(tuple(min(p[i] for p in pts) for i in range(3)));hi=Vector(tuple(max(p[i] for p in pts) for i in range(3)));centre=(lo+hi)/2;span=max(hi-lo)
 def obj(name,typ):
  o=bpy.data.objects.get(name)
  if not o:o=bpy.data.objects.new(name,bpy.data.cameras.new(name) if typ=='CAMERA' else bpy.data.lights.new(name,typ));S.collection.objects.link(o)
  return o
 cam=obj('Defence product camera','CAMERA');S.camera=cam;cam.location=centre+Vector((1.35,-2.1,1.15))*span;cam.rotation_euler=(centre-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=span*1.40
 for name,delta,power,color in [('Defence warm key',(1,-1.6,2.4),700,(1,.85,.65)),('Defence soft fill',(-1.5,-.5,1.2),450,(.65,.76,1)),('Defence edge light',(.4,1.5,2),800,(1,.88,.65))]:
  o=obj(name,'AREA');o.location=centre+Vector(delta)*span;o.rotation_euler=(centre-o.location).to_track_quat('-Z','Y').to_euler();o.data.energy=power*span*span/4;o.data.shape='DISK';o.data.size=span*1.6;o.data.color=color
 # A studio floor is not part of asset exports.
 floor=bpy.data.objects.get('Defence studio floor')
 if not floor:
  bpy.ops.mesh.primitive_plane_add(size=200);floor=bpy.context.object;floor.name='Defence studio floor';floor.data.materials.append(mat('Defence studio sand',(.19,.17,.15),0,.9))
 floor.location.z=min(-.015,lo.z-.015);S.render.filepath=str(OUT/(prefix+('-'+clip+str(frame) if clip else '')+'-preview.png'));bpy.ops.render.render(write_still=True);return S.render.filepath
def export(id):
 prefix=NAMES[id];col=bpy.data.collections['DEFENCE_'+prefix];col.hide_viewport=False;col.hide_render=False;rig=next(o for o in col.objects if o.type=='ARMATURE');rig.animation_data.action=None
 for b in rig.pose.bones:b.location=(0,0,0);b.rotation_quaternion=(1,0,0,0);b.scale=(1,1,1)
 S.frame_set(1);bpy.ops.object.select_all(action='DESELECT');copies=[]
 for o in list(col.objects):
  if o.type=='MESH':
   copy=o.copy();copy.data=o.data.copy();col.objects.link(copy);copy.select_set(True);copies.append(copy)
 bpy.context.view_layer.objects.active=copies[0];bpy.ops.object.join();merged=bpy.context.object;merged.name=prefix+' game skin';rig.select_set(True);bpy.context.view_layer.objects.active=rig
 rig.animation_data.action=None
 # Restrict exports to this rig's five NLA actions; do not attach every workshop action to each rig.
 if not rig.animation_data.nla_tracks:
  for a in bpy.data.actions:
   if a.name.startswith(prefix+'_'):
    track=rig.animation_data.nla_tracks.new();track.name=a.name;strip=track.strips.new(a.name,1,a);strip.action_slot=a.slots[0];track.mute=True
 rig.animation_data.use_nla=False
 bpy.ops.export_scene.gltf(filepath=str(OUT/(prefix+'.glb')),export_format='GLB',use_selection=True,export_animations=True,export_force_sampling=True,export_animation_mode='ACTIONS',export_anim_single_armature=False,export_nla_strips=True,export_def_bones=False,export_skins=True,export_cameras=False,export_lights=False)
 bpy.data.objects.remove(merged,do_unlink=True);rig.animation_data.action=None;return str(OUT/(prefix+'.glb'))
