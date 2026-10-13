import bpy, math, json
from pathlib import Path
from mathutils import Vector, Matrix
BASE=Path(r'C:/Users/codex/ByteEngine/Tools/KarlsonDemoModeling/build_guns.py').read_text()
OUT=Path(r'C:/Users/codex/Downloads/byte engine files/Karlson V2');OUT.mkdir(exist_ok=True)
# Disk source is preserved; the revision is a separate scene/file.
scene=bpy.context.scene
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.render.fps=60
scene.render.engine='BLENDER_EEVEE_NEXT';scene.render.resolution_x=1100;scene.render.resolution_y=720;scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new('Studio');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.12,.16,.21,1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.45
scene.view_settings.view_transform='AgX'
MATS={}
for name,col,rough,metal in [('Body',(.07,.09,.12,1),.55,.35),('Slide',(.17,.20,.24,1),.48,.45),('Grip',(.025,.033,.043,1),.9,0),('Steel',(.30,.34,.39,1),.34,.65),('Dark',(.007,.011,.016,1),.9,0),('Glove',(.13,.16,.19,1),.85,0),('Accent',(.45,.62,.66,1),.55,.15),('Shell',(.7,.20,.08,1),.7,0)]:
 m=bpy.data.materials.new(name);m.use_nodes=True;m.diffuse_color=col
 node=m.node_tree.nodes['Principled BSDF'];node.inputs['Base Color'].default_value=col;node.inputs['Roughness'].default_value=rough;node.inputs['Metallic'].default_value=metal;MATS[name]=m
exec(BASE[BASE.index('class Gun:'):BASE.index("p=Gun('Pistol'")])
def shape(self,name,profile,width,bone='Frame',mat='Body',bevel=.001):
 # Extruded authored side silhouette, rather than rectangular blocks.
 n=len(profile);verts=[(x,y,z) for x in [-width/2,width/2] for y,z in profile]
 faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update();obj=bpy.data.objects.new(self.name+'_'+name,mesh);scene.collection.objects.link(obj)
 bpy.context.view_layer.update();bpy.context.view_layer.objects.active=obj;obj.select_set(True)
 if bevel:
  mod=obj.modifiers.new('Edge facets','BEVEL');mod.width=bevel;mod.segments=1;bpy.ops.object.modifier_apply(modifier=mod.name)
 return self.own(obj,bone,mat)
Gun.shape=shape

def hand(gun,bone,center,support=False):
 x,y,z=center
 if bone=='RightHand':
  # Palm alongside the grip; four fingers wrap around its front, thumb across the back.
  gun.box(bone+' palm',(x-.009,y,z),(.022,.042,.078),bone,'Glove',bevel=.006)
  gun.box(bone+' wrist',(x-.002,y-.035,z-.044),(.034,.04,.038),bone,'Grip',angle=-20,bevel=.005)
  gun.box(bone+' forearm',(x+.008,y-.082,z-.068),(.046,.07,.046),bone,'Body',angle=-25,bevel=.006)
  for i in range(4):
   fz=z+.029-i*.017
   gun.box(bone+' knuckle'+str(i),(x-.013,y+.022,fz),(.025,.019,.015),bone,'Glove',bevel=.004)
   gun.box(bone+' finger'+str(i),(.001,y+.026,fz),(.041,.016,.014),bone,'Glove',bevel=.004)
   gun.box(bone+' fingertip'+str(i),(-.018,y+.014,fz),(.015,.022,.014),bone,'Glove',bevel=.003)
  gun.box(bone+' thumb',(.016,y-.014,z+.036),(.043,.021,.021),bone,'Glove',bevel=.005)
 else:
  gun.box(bone+' palm',(x,y,z),(.035,.065,.022),bone,'Glove',bevel=.006)
  gun.box(bone+' wrist',(x-.018,y-.040,z-.015),(.032,.035,.034),bone,'Grip',bevel=.005)
  gun.box(bone+' forearm',(x-.033,y-.085,z-.042),(.043,.07,.043),bone,'Body',angle=-25,bevel=.006)
  for i in range(4):
   fy=y+.022-i*.015
   gun.box(bone+' knuckle'+str(i),(x+.015,fy,z+.015),(.02,.012,.025),bone,'Glove',bevel=.003)
   gun.box(bone+' finger'+str(i),(x+.032,fy,z+.025),(.025,.012,.017),bone,'Glove',bevel=.003)
  gun.box(bone+' thumb',(x-.008,y+.03,z+.022),(.021,.032,.02),bone,'Glove',bevel=.004)

def rig(self):
 data=bpy.data.armatures.new(self.name+'_Skeleton');obj=bpy.data.objects.new(self.name+'_Rig',data);self.collection.objects.link(obj)
 bpy.context.view_layer.update();bpy.context.view_layer.objects.active=obj;obj.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
 root=data.edit_bones.new('WeaponRoot');root.head=(0,0,0);root.tail=(0,0,.08)
 motion=data.edit_bones.new('WeaponMotion');motion.head=(0,0,0);motion.tail=(0,0,.07);motion.parent=root
 for name,head in self.bones.items():
  b=data.edit_bones.new(name);b.head=head;b.tail=Vector(head)+Vector((0,0,.035));b.parent=motion
 bpy.ops.object.mode_set(mode='OBJECT');obj.select_set(False)
 # Merge only parts that share motion and material; preserve all mechanical assemblies.
 grouped={}
 for part,bone in self.parts:grouped.setdefault((bone,part.data.materials[0].name),[]).append(part)
 self.parts=[]
 for (bone,mat),parts in grouped.items():
  bpy.ops.object.select_all(action='DESELECT')
  for part in parts:part.select_set(True)
  bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();part=bpy.context.object;part.name=self.name+'_'+bone+'_'+mat
  part.parent=obj;group=part.vertex_groups.new(name=bone);group.add(list(range(len(part.data.vertices))),1,'REPLACE');mod=part.modifiers.new('Rigid weights','ARMATURE');mod.object=obj;part.select_set(False);self.parts.append((part,bone))
 self.rig_object=obj;obj.animation_data_create()
 for b in obj.pose.bones:b.rotation_mode='QUATERNION'
 self.make_actions()
Gun.rig=rig

def actions(self):
 lengths={'Idle':61,'Fire':40 if self.name=='Shotgun' else 16,'Reload':120 if self.name=='Shotgun' else 108,'Equip':31}
 for clip,last in lengths.items():
  action=bpy.data.actions.new(self.name+'_'+clip);action.use_fake_user=True;self.rig_object.animation_data.action=action
  for b in self.rig_object.pose.bones:self.key(b.name,1);self.key(b.name,last)
  if clip=='Idle':
   for f in [1,16,31,46,61]:self.key('WeaponMotion',f,(0,0,.001*math.sin((f-1)/60*math.tau)),roll=.15*math.sin((f-1)/60*math.tau))
  elif clip=='Fire':
   for f,pos,pitch in [(1,(0,0,0),0),(3,(0,-.02,.003),4),(7,(0,-.008,.001),1),(13,(0,0,0),0)]:self.key('WeaponMotion',f,pos,pitch)
   self.key('Trigger',3,pitch=-10);self.key('Trigger',7)
   if self.name=='Pistol':self.key('Slide',3,(0,-.032,0));self.key('Slide',6)
   if self.name=='Rifle':self.key('Bolt',3,(0,-.035,0));self.key('Bolt',6)
   if self.name=='Shotgun':
    for f,t in [(7,0),(14,1),(21,1),(30,0),(40,0)]:
     self.key('Pump',f,(0,-.1*t,0));self.key('LeftHand',f,(0,-.1*t,0))
  elif clip=='Reload':
   self.key('WeaponMotion',14,(0,.03,.015),pitch=8,roll=-22);self.key('WeaponMotion',last-16,(0,.03,.015),pitch=8,roll=-22)
   if self.name!='Shotgun':
    # Fully clear the well, carry below frame, align the incoming prop, then seat it.
    for f,move,pitch in [(1,(0,0,0),0),(16,(0,0,0),0),(35,(0,-.045,-.16),0),(46,(-.11,-.08,-.25),-12),(58,(-.11,-.08,-.25),-12),(70,(0,-.045,-.16),0),(85,(0,0,-.012),0),(91,(0,0,0),0),(108,(0,0,0),0)]:
     self.key('Magazine',f,move,pitch)
    # Support hand approaches the magazine, withdraws with it and seats it.
    approach=(.015,-.005,-.015) if self.name=='Pistol' else (.03,-.16,-.10)
    for f,move in [(1,(0,0,0)),(16,approach),(35,tuple(approach[i]+(0,-.045,-.16)[i] for i in range(3))),(46,tuple(approach[i]+(-.11,-.08,-.25)[i] for i in range(3))),(58,tuple(approach[i]+(-.11,-.08,-.25)[i] for i in range(3))),(70,tuple(approach[i]+(0,-.045,-.16)[i] for i in range(3))),(91,approach),(108,(0,0,0))]:self.key('LeftHand',f,move)
    mech='Slide' if self.name=='Pistol' else 'Bolt';self.key(mech,96,(0,-.032,0));self.key(mech,102)
   else:
    # Single shell loading cycle; runtime per-round reload repeats this clip.
    for f,t in [(1,0),(20,1),(37,1),(60,1),(82,0),(120,0)]:self.key('LeftHand',f,(-.015,-.28*t,-.055*t),roll=-15*t)
    for f,pos in [(1,(-.3,0,-.4)),(28,(-.08,-.08,-.13)),(45,(0,-.025,-.035)),(65,(0,.04,0)),(78,(0,.08,.014)),(82,(-.3,0,-.4)),(120,(-.3,0,-.4))]:self.key('Shell',f,pos)
    self.key('LoadingGate',45,pitch=35);self.key('LoadingGate',78)
  else:
   self.key('WeaponMotion',1,(0,-.08,-.22),pitch=-18,roll=8);self.key('WeaponMotion',19,(0,0,.006),pitch=1);self.key('WeaponMotion',31)
  self.actions[clip]=action
 self.rig_object.animation_data.action=None
 for clip,action in self.actions.items():
  track=self.rig_object.animation_data.nla_tracks.new();track.name=clip;strip=track.strips.new(clip,1,action);strip.name=clip;strip.extrapolation='NOTHING';track.mute=True
 self.rest()
Gun.make_actions=actions

p=Gun('Pistol',{'Frame':(0,0,0),'MuzzleSocket':(0,.15,.025),'Slide':(0,0,.02),'Magazine':(0,-.043,-.08),'Trigger':(0,.02,-.03),'RightHand':(.04,-.07,-.075),'LeftHand':(-.035,-.055,-.125)})
p.shape('Lower receiver',[(-.10,.005),(.145,.005),(.145,-.033),(.062,-.045),(-.01,-.04),(-.093,-.052)],.038,bevel=.002)
p.shape('Faceted slide',[(-.103,.012),(-.103,.043),(-.084,.055),(.127,.055),(.153,.036),(.153,.006),(-.09,.006)],.047,'Slide','Slide',.0025)
# Separate grip side walls and front/back leave an actual open magazine well.
for side in [-1,1]:
 p.box('Grip panel',(side*.022,-.06,-.092),(.009,.044,.105),mat='Grip',angle=-12,bevel=.003)
 for i in range(5):p.box('Slide serration',(side*.024,-.086+i*.01,.026),(.002,.003,.027),'Slide','Dark',angle=-14,bevel=0)
 for i in range(4):p.box('Grip checkering',(side*.027,-.063,-.065-i*.017),(.002,.031,.003),mat='Body',angle=-12,bevel=0)
p.box('Backstrap',(0,-.084,-.09),(.036,.008,.11),mat='Grip',angle=-12)
p.box('Frontstrap',(0,-.036,-.09),(.033,.006,.105),mat='Grip',angle=-12)
p.shape('Magazine',[(-.058,-.029),(-.029,-.034),(-.054,-.147),(-.082,-.147)],.024,'Magazine','Steel',.001)
p.box('Magazine base',(0,-.068,-.153),(.032,.043,.01),'Magazine','Grip',angle=-12)
p.box('Feed lips',(0,-.044,-.029),(.026,.022,.006),'Magazine','Dark',bevel=.0008)
p.guard(.026,-.051,.069,.034,.054);p.box('Trigger',(0,.018,-.052),(.01,.012,.034),'Trigger','Steel',angle=15)
p.cylinder('Barrel',(0,.089,.026),.011,.136);p.cylinder('Bore',(0,.158,.026),.0068,.002,mat='Dark')
p.box('Ejection port',(.024,-.006,.03),(.002,.041,.019),'Slide','Dark',bevel=.0006)
p.box('Front sight',(0,.115,.062),(.009,.013,.012),'Slide','Grip');p.box('Rear sight',(0,-.081,.064),(.029,.014,.014),'Slide','Grip')
p.box('Sight marker',(0,-.088,.064),(.006,.002,.005),'Slide','Accent',bevel=0)
p.box('Slide catch',(.026,-.024,-.005),(.006,.023,.01),mat='Steel')
hand(p,'RightHand',(.045,-.065,-.095));hand(p,'LeftHand',(-.035,-.055,-.125));p.rig()

s=Gun('Shotgun',{'Frame':(0,0,0),'MuzzleSocket':(0,.54,.03),'Pump':(0,.25,-.008),'LoadingGate':(0,-.02,-.033),'Trigger':(0,-.068,-.05),'RightHand':(.045,-.13,-.10),'LeftHand':(-.057,.25,-.035),'Shell':(0,-.023,-.043)})
s.shape('Receiver',[(-.126,-.03),(-.126,.035),(-.10,.052),(.093,.046),(.117,.015),(.112,-.038),(-.082,-.041)],.057,mat='Slide',bevel=.003)
s.cylinder('Barrel',(0,.315,.028),.0165,.44);s.cylinder('Bore',(0,.536,.028),.0105,.003,mat='Dark');s.cylinder('Tube',(0,.30,-.018),.014,.39,mat='Body')
s.shape('Pump silhouette',[(.165,-.042),(.155,.008),(.18,.02),(.33,.02),(.355,-.004),(.34,-.046)],.066,'Pump','Grip',.003)
for i in range(7):s.box('Pump ribs',(0,.18+i*.024,-.013),(.071,.006,.057),'Pump','Body',bevel=.001)
s.shape('Stock',[(-.13,.025),(-.19,.021),(-.24,-.032),(-.41,-.019),(-.425,-.15),(-.375,-.16),(-.21,-.08),(-.14,-.04)],.052,bevel=.004)
s.box('Butt pad',(0,-.419,-.089),(.058,.022,.137),mat='Grip',angle=-6,bevel=.004)
s.box('Grip',(0,-.135,-.086),(.041,.05,.103),mat='Grip',angle=-22,bevel=.004)
s.guard(-.061,-.055,.079,.036,.058);s.box('Trigger',(0,-.064,-.055),(.011,.012,.034),'Trigger','Steel',angle=15)
s.box('Gate',(0,-.013,-.043),(.032,.055,.006),'LoadingGate','Steel')
s.box('Ejection port',(.029,.027,.009),(.002,.057,.023),mat='Dark');s.box('Front sight',(0,.508,.05),(.011,.014,.015),mat='Grip')
s.cylinder('Loading shell',(0,-.027,-.043),.01,.05,'Shell','Shell',sides=10)
hand(s,'RightHand',(.045,-.135,-.10));hand(s,'LeftHand',(-.025,.25,-.05));s.rig()

r=Gun('Rifle',{'Frame':(0,0,0),'MuzzleSocket':(0,.473,.025),'Magazine':(0,.032,-.10),'Trigger':(0,-.067,-.052),'Bolt':(0,0,.025),'RightHand':(.045,-.13,-.10),'LeftHand':(-.063,.20,-.05)})
r.shape('Upper receiver',[(-.134,-.006),(-.131,.037),(-.108,.062),(.11,.058),(.132,.027),(.119,-.021),(-.105,-.022)],.061,mat='Slide',bevel=.003)
r.shape('Lower receiver',[(-.121,-.014),(.086,-.016),(.065,-.063),(.009,-.063),(-.019,-.041),(-.115,-.052)],.048,bevel=.002)
r.shape('Handguard',[(.09,-.026),(.098,.048),(.31,.046),(.327,.01),(.311,-.036),(.13,-.034)],.070,bevel=.003)
for side in [-1,1]:
 for i in range(5):r.box('Vent',(side*.036,.126+i*.033,.02),(.002,.019,.018),mat='Dark',bevel=.001)
for i in range(12):r.box('Top rail',(0,-.09+i*.034,.066),(.036,.012,.013),mat='Grip',bevel=.001)
r.cylinder('Barrel',(0,.374,.02),.014,.13);r.box('Muzzle brake',(0,.456,.02),(.041,.05,.039),mat='Steel',bevel=.003);r.cylinder('Bore',(0,.482,.02),.009,.003,mat='Dark')
r.shape('Curved magazine',[(.015,-.046),(.067,-.046),(.08,-.095),(.095,-.159),(.081,-.20),(.023,-.197),(.032,-.143),(.027,-.092)],.034,'Magazine','Body',.002)
for side in [-1,1]:
 for i in range(3):r.box('Magazine ribs',(side*.018,.038+i*.014,-.124),(.002,.005,.116),'Magazine','Grip',angle=-7,bevel=.0006)
r.box('Mag base',(0,.053,-.198),(.04,.068,.011),'Magazine','Grip',bevel=.002)
r.box('Grip',(0,-.133,-.094),(.041,.049,.116),mat='Grip',angle=-18,bevel=.004)
r.box('Stock rail',(0,-.247,.019),(.036,.225,.031),mat='Steel',bevel=.003)
r.shape('Stock frame',[(-.183,.027),(-.355,.034),(-.372,.01),(-.372,-.088),(-.335,-.09),(-.33,-.02),(-.215,-.021)],.045,bevel=.003)
r.box('Stock lower brace',(0,-.277,-.052),(.026,.155,.018),mat='Body',angle=-24)
r.box('Butt pad',(0,-.372,-.035),(.057,.018,.123),mat='Grip',bevel=.003)
r.guard(-.056,-.056,.08,.036,.059);r.box('Trigger',(0,-.065,-.057),(.01,.012,.032),'Trigger','Steel',angle=15)
r.box('Bolt',(.032,.005,.025),(.005,.065,.025),'Bolt','Steel');r.box('Charging handle',(.038,-.009,.031),(.018,.02,.013),'Bolt','Grip')
r.box('Rear sight',(0,-.08,.08),(.027,.013,.02),mat='Grip');r.box('Front sight',(0,.282,.08),(.012,.014,.026),mat='Grip')
hand(r,'RightHand',(.045,-.13,-.10));hand(r,'LeftHand',(-.025,.20,-.058));r.rig()
guns=[p,s,r]
for gun in guns:gun.export()
for gun in guns:gun.rest()
scene.frame_start=1;scene.frame_end=120
camdata=bpy.data.cameras.new('Review camera');camera=bpy.data.objects.new('Review camera',camdata);scene.collection.objects.link(camera);scene.camera=camera;camdata.type='ORTHO'
for name,pos,power,size in [('Key',(1.5,1,2),190,2),('Fill',(-1.5,1,1),90,2),('Rim',(0,-2,1.5),160,2)]:
 data=bpy.data.lights.new(name,'AREA');data.energy=power;data.size=size;obj=bpy.data.objects.new(name,data);scene.collection.objects.link(obj);obj.location=pos;obj.rotation_euler=(-obj.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Karlson_Guns_v2.blend'))
result={'saved':str(OUT),'guns':[g.name for g in guns]}
