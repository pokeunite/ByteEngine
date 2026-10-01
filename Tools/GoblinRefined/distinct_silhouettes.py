"""Second-pass function silhouettes, independently authored geometry."""
def sphere(name,radius,pos,mat='iron',bone='Root'):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=20,ring_count=12,radius=radius,location=pos);o=bpy.context.object;o.name=name;CUR.add(o,mat,bone);return o

def distinct(spec):
 i=spec['id'];k=spec['shape']
 if i not in {4,5,9,10,11,12,13,16,18,19,21,22,24,25,27,28,30,34,35,39,42,44,45,50,53,54,56,58,59,62,65,66,67,68,69,70,73,75,76,77,79,82,86,88,89,93,95,97}:return False
 mount()
 if i==12:
  box('Sliding square core',(.38,.35,.38),mat='wood');
  for x in [-.23,.23]:rod((x,-.15,-.19),(x,.50,-.19),.024,'steel');rod((x,-.15,.19),(x,.50,.19),.024,'steel')
  moving();box('Adjustment endplate',(.48,.08,.48),(0,.43,0),'green','Moving');output((0,.5,0),'Moving');travel((0,.3,0));cylinder('Adjustment handwheel',.14,.04,(.28,.05,0),'brass',(1,0,0))
 elif i in [4,5,13,19,22,28,44,76]:
  moving('Moving',{5:(0,.20,0),28:(0,.20,0),44:(0,.10,0)}.get(i,(0,.12,0)))
  if i==4:
   box('Split coupling rear',(.36,.16,.24),(0,-.05,0),'iron');box('Released coupling front',(.36,.12,.24),(0,.19,0),'green','Moving');rod((-.20,.02,.14),(.20,.02,.14),.027,'brass');box('Release latch',(.07,.22,.05),(.20,.06,.16),'rust');output((0,.3,0),'Moving');travel((0,.25,0))
  elif i in [5,28]:
   for x in [-.19,.19]:box('Open hinge fork',(.055,.36,.28),(x,.10,0),'iron')
   cylinder('Exposed hinge pin',.065,.47,(0,.2,0),'steel',(1,0,0));box('Hinge swinging leaf',(.25,.32,.055),(0,.31,.01),'green','Moving');output((0,.49,.01),'Moving');steer(axis=(1,0,0),degrees=55)
   if i==28:
    cylinder('Steering reduction gearbox',.15,.14,(-.27,.20,0),'green',(1,0,0));rod((-.30,.20,.12),(-.30,.02,.23),.035,'brass');box('Servo actuator',(.18,.20,.18),(-.30,-.05,.23),'iron')
  elif i==19:
   cylinder('Bare swivel collar',.13,.14,(0,.02,0),'steel',(0,1,0));rod((0,.09,0),(0,.38,0),.055,'steel','Moving');torus('Thrust bearing',.14,.018,(0,.14,0),'brass',(0,1,0));output((0,.4,0),'Moving');spin()
  elif i==76:
   rod((0,-.13,0),(0,.67,0),.055,'steel');
   for y in [-.10,.56]:torus('Axle bearing collar',.09,.02,(0,y,0),'iron',(0,1,0))
   output((0,.72,0))
  elif i==44:
   sphere('Visible ball joint',.14,(0,.1,0),'brass','Moving');
   for x in [-.16,.16]:rod((x,-.1,0),(x,.1,0),.035,'iron')
   rod((0,.10,0),(0,.40,0),.045,'steel','Moving');output((0,.45,0),'Moving');steer(axis=(1,0,1),degrees=35)
  elif i==13:
   box('Steering worm-gear box',(.34,.22,.20),(0,.03,0),'green');cylinder('Steering output pivot',.13,.13,(0,.22,0),'brass',(0,1,0),'Moving');rod((-.25,.26,0),(.25,.26,0),.034,'steel','Moving');output((0,.38,0),'Moving');steer(axis=(0,1,0))
   cylinder('Worm input shaft',.07,.35,(.23,0,0),'steel',(1,0,0))
  else:
   cylinder('Exposed motor body',.18,.35,(0,.08,0),'green',(0,1,0));
   for z in [-.12,0,.12]:rod((-.16,-.07,z),(-.16,.23,z),.015,'steel')
   cylinder('Drive pulley',.22,.05,(0,.29,0),'brass',(0,1,0),'Moving');output((0,.37,0),'Moving');spin()
 elif i in [9,16,18,42]:
  moving('Moving',(0,.10,0));output((0,.85,0),'Moving')
  if i==42:
   for x in [-.14,.14]:rod((x,-.12,0),(x,.90,0),.025,'steel')
   box('Open sliding carriage',(.39,.19,.16),(0,.63,0),'green','Moving');box('Rail cross brace',(.40,.05,.1),(0,.10,0),'iron');travel((0,.20,0))
  elif i==18:
   cylinder('Hydraulic cylinder',.13,.58,(0,.15,0),'green',(0,1,0));torus('Cylinder seal collar',.14,.022,(0,.43,0),'brass',(0,1,0));rod((0,.03,0),(0,.83,0),.045,'steel','Moving');curve_tube('Hydraulic feed hose',[(.13,-.1,0),(.24,0,0),(.24,.32,0),(.13,.35,0)],.015,'rubber');travel((0,.40,0))
  else:
   rod((0,-.10,0),(0,.10,0),.065,'iron');cylinder('Fixed spring end seat',.16,.045,(0,.10,0),'iron',(0,1,0));CUR.bone('Coil',(0,.1,0));pts=[(.13*math.cos(t*math.tau*(7 if i==9 else 5)),.10+.75*t,.13*math.sin(t*math.tau*(7 if i==9 else 5))) for t in [j/140 for j in range(141)]];curve_tube('Compressing spring',pts,.020,'steel' if i==9 else 'rust','Coil');CUR.scale_animation=('Coil',(0,1,0),.6)
   if i==16:cylinder('Shock damper body',.065,.45,(0,.22,0),'iron',(0,1,0));rod((0,.42,0),(0,.84,0),.025,'steel','Moving')
   travel((0,-.30,0))
 elif i in [10,24,73]:
  if i==10:
   for x in [-.24,0,.24]:box('Separate wooden plank',(.22,.075,.74),(x,0,0),'wood',bevel=.01)
   for z in [-.25,.25]:box('Rear timber cross batten',(.74,.06,.075),(0,-.055,z),'wood')
  elif i==24:
   blade([(-.38,-.30),(.38,-.30),(.32,.34),(-.32,.34)],'green');
   for x in [-.25,.25]:rod((x,-.25,.045),(x,.24,.045),.018,'iron')
   for x in [-.28,.28]:bolt((x,-.20,.05),(0,0,1),scale=.7)
  else:
   mesh('Taut fabric building surface',[(-.42,0,-.33),(.40,0,-.38),(.32,0,.45),(-.28,0,.35)],[(0,1,2,3)],'green');
   corners=[(-.42,0,-.33),(.40,0,-.38),(.32,0,.45),(-.28,0,.35)]
   for a,b in zip(corners,corners[1:]+corners[:1]):rod(a,b,.019,'wood')
 elif i in [25,34,79,89,95]:
  bone='Root'
  if i in [79,95]:moving();bone='Moving'
  if i==25:
   blade([(-.85,-.05),(.85,-.05),(.55,.50),(-.65,.50)],'wood');
   for x in [-.55,0,.55]:rod((x,-.03,.06),(x,.44,.06),.015,'iron')
  elif i==34:
   blade([(-.5,-.08),(.50,-.08),(.38,.44),(-.45,.44)],'green');rod((-.46,0,.05),(.46,0,.05),.022,'wood')
  elif i==89:
   for x in [-.4,-.2,0,.2,.4]:box('Directional air brake louvre',(.05,.65,.22),(x,.20,0),'iron',bevel=.007)
   for y in [-.12,.50]:box('Louvre end brace',(.88,.035,.24),(0,y,0),'green')
  elif i==79:
   blade([(-.16,.0),(.16,.0),(.35,.65),(-.30,.65)],'wood',bone);rod((0,-.10,0),(0,.55,0),.035,'steel',bone);steer(axis=(0,1,0),degrees=45)
  else:
   blade([(-.16,-.05),(.16,-.05),(.48,.57),(.02,.74)],'green',bone);rod((0,-.15,0),(0,.52,0),.025,'steel',bone);steer(axis=(0,1,0),degrees=35)
 elif i in [11,21,53,56,62]:
  moving();
  if i in [11,53]:
   cylinder('Cannon barrel',.17 if i==11 else .25,.68,(0,.24,.07),'iron',(0,1,0),'Moving');cylinder('Open bore',.12 if i==11 else .21,.015,(0,.59,.07),'black',(0,1,0),'Moving');torus('Muzzle ring',.17 if i==11 else .25,.025,(0,.59,.07),'steel',(0,1,0),'Moving')
   for x in [-.23,.23]:box('Cannon cradle',(.065,.38,.20),(x,.05,-.04),'wood')
   if i==53:
    cylinder('Wide blunderbuss bell',.29,.10,(0,.54,.07),'brass',(0,1,0),'Moving');
    for j in range(3):sphere('Shot pouch pellet',.045,(.31,-.08+j*.10,.02),'steel')
  elif i==21:
   for x in [-.18,.18]:cylinder('Pressurized fuel bottle',.10,.40,(x,-.02,.10),'green')
   rod((0,-.08,.08),(0,.65,.08),.052,'iron','Moving');rod((.09,.4,.04),(.09,.65,.04),.014,'brass');curve_tube('Fuel hose',[(-.18,.02,.22),(-.27,.19,.18),(0,.3,.1)],.018,'rubber');torus('Pilot flame guard',.05,.011,(.09,.66,.04),'steel',(0,1,0))
  elif i==56:
   sphere('Round pump reservoir',.20,(0,-.02,.11),'green');cylinder('Pump impeller housing',.18,.09,(.25,.04,.06),'brass',(1,0,0));curve_tube('Water delivery elbow',[(0,.05,.20),(0,.22,.22),(0,.49,.22)],.055,'steel','Moving');torus('Spray nozzle',.075,.015,(0,.5,.22),'brass',(0,1,0),'Moving')
  else:
   cylinder('Suction housing',.20,.25,(0,.05,.04),'green',(0,1,0));cylinder('Wide intake bell',.31,.15,(0,.31,.04),'iron',(0,1,0),'Moving');
   for x in [-.20,-.1,0,.1,.20]:rod((x,.40,-.15),(x,.40,.23),.012,'steel','Moving')
   curve_tube('Vacuum return pipe',[(.18,.0,.04),(.30,-.12,.04),(.12,-.18,.04)],.027,'rubber')
  travel((0,-.05,0))
 elif i in [27,30,77]:
  moving('Moving',{27:(.11,.13,0),77:(.07,-.08,0)}.get(i,(0,0,0)))
  if i==30:
   for x in [-.18,.18]:curve_tube('Payload retaining basket',[(x,-.10,-.10),(x,.2,-.18),(x,.5,0),(x,.5,.25)],.028,'iron')
   rod((-.18,.15,-.18),(.18,.15,-.18),.025,'wood');box('Bomb release latch',(.20,.10,.045),(0,.47,.22),'brass','Moving');travel((0,0,.12))
  elif i==27:
   box('Grabber actuator body',(.32,.25,.22),mat='green');
   for side in [-1,1]:curve_tube('Curved gripping finger',[(side*.11,.13,0),(side*.26,.35,0),(side*.22,.61,0),(side*.10,.65,0)],.037,'steel','Moving' if side>0 else 'Root')
   steer(axis=(0,0,1),degrees=25)
  else:
   for side in [-1,1]:
    bone='Moving' if side>0 else 'Root';blade([(side*.07,-.08),(side*.34,.26),(side*.27,.66),(side*.09,.42)],'iron',bone)
    for y in [.23,.35,.47]:box('Large jaw tooth',(.13,.08,.07),(side*.18,y,.04),'steel',bone,.003)
   rod((-.13,-.06,0),(.13,-.06,0),.035,'steel');CUR.clip('Bite',{'Moving':[(1,(0,0,1),-.35,(0,0,0)),(31,(0,0,1),.25,(0,0,0)),(61,(0,0,1),-.35,(0,0,0))]},False)
 elif i==35:
  box('Solid ballast stack',(.54,.40,.37),(0,.04,0),'iron',bevel=.025)
  for z in [-.12,0,.12]:box('Cast weight seam',(.56,.41,.018),(0,.04,z),'steel',bevel=.002)
  torus('Weight lifting ring',.10,.024,(0,.04,.26),'brass',(0,1,0))
 elif i==39:
  case();moving();cylinder('Powered gear disc',.37,.12,(0,.35,0),'iron',(0,1,0),'Moving')
  for j in range(16):a=j*math.tau/16;box('Powered gear tooth',(.08,.13,.08),(.40*math.cos(a),.35,.40*math.sin(a)),'steel','Moving',.005)
  box('Gear reduction motor',(.20,.30,.17),(.27,-.05,0),'green');output((0,.47,0),'Moving');spin()
 elif i in [45,75]:
  moving('Moving',(0,.07,.18));cylinder('Rope drum',.19 if i==45 else .10,.31,(0,.07,.18),'wood',(1,0,0),'Moving');rod((0,.15,.18),(0,1.05,.18),.015,'wood');flange((0,1.05,.18),(0,1,0),socket='Endpoint')
  if i==45:
   rod((-.22,-.15,0),(.22,-.15,0),.025,'iron');rod((-.24,.07,.18),(.24,.07,.18),.024,'steel')
   for x in [-.22,.22]:rod((x,-.15,0),(x,.07,.18),.024,'iron')
   for x in [-.19,.19]:cylinder('Winch drum flange',.24,.025,(x,.07,.18),'iron',(1,0,0),'Moving')
   rod((.22,.07,.18),(.22,.07,.43),.025,'steel');cylinder('Crank hand grip',.033,.12,(.22,.07,.46),'wood',(1,0,0))
  else:
   box('Rope counter case',(.25,.18,.18),(.17,.0,.10),'green');
   for j in range(3):box('Counter digit window',(.038,.012,.055),(.10+j*.045,-.096,.10),'black',bevel=.002)
  spin(axis=(1,0,0))
 elif i in [50,86,88]:
  moving('Moving',(0,.16 if i==88 else .18,0));r=.23 if i==50 else .17 if i==86 else .42;axis=(1,0,0)
  if i==88:
   cylinder('Heavy solid flywheel',r,.16,(0,.16,0),'iron',axis,'Moving');torus('Flywheel polished rim',r,.027,(0,.16,0),'steel',axis,'Moving');
   for a in [j*math.tau/6 for j in range(6)]:cylinder('Flywheel balance bolt',.025,.19,(0,.16+r*.65*math.cos(a),r*.65*math.sin(a)),'brass',axis,'Moving')
  else:
   torus('Smooth compact tire',r*.74,r*.26,(0,.18,0),'rubber',axis,'Moving');cylinder('Compact hub',r*.5,.15,(0,.18,0),'steel',axis,'Moving')
   for x in [-.11,.11]:rod((x,-.04,0),(x,.18,0),.018 if i==86 else .03,'iron')
   if i==50:cylinder('Caster swivel top',.12,.12,(0,-.03,0),'brass',(0,1,0))
  spin(axis=axis)
 elif i==59:
  moving();cylinder('Self-propelled rocket body',.095,.66,(0,.28,.12),'rust',(0,1,0),'Moving');cylinder('Rocket exhaust throat',.065,.09,(0,-.10,.12),'black',(0,1,0),'Moving')
  bpy.ops.mesh.primitive_cone_add(vertices=16,radius1=.095,radius2=0,depth=.22,location=(0,.72,.12));o=bpy.context.object;o.rotation_euler[0]=-math.pi/2;CUR.add(o,'steel','Moving')
  for x in [-.11,.11]:blade([(x-.04,-.05),(x+.04,-.05),(x+.04,.20),(x-.04,.1)],'green','Moving')
  for x in [-.13,.13]:rod((x,-.12,.0),(x,.64,.0),.018,'wood')
  curve_tube('Rocket fuse wick',[(.04,-.10,.18),(.12,-.15,.22),(.13,-.24,.27)],.009,'wood');travel((0,.45,0))
 elif i==54:
  sphere('Grenade canister',.22,(0,.12,0),'green');torus('Grenade neck',.06,.014,(0,.12,.22),'iron');box('Trigger spoon',(.07,.31,.025),(.02,.22,.22),'steel');torus('Grenade pull pin',.065,.008,(.10,.13,.26),'brass',(0,1,0))
 elif i in [58,65,66,67,68,69,70,93]:
  moving('Moving',{66:(0,.23,.17),70:(0,.23,.17),69:(0,.18,.18)}.get(i,(0,0,0)));box('Instrument base',(.36,.23,.20),(0,.03,0),'green')
  if i==58:
   cylinder('Camera lens barrel',.15,.35,(0,.27,.06),'iron',(0,1,0));cylinder('Lens glass',.12,.01,(0,.45,.06),'black',(0,1,0));box('Camera hood',(.34,.15,.035),(0,.37,.22),'steel')
  elif i==65:
   rod((-.10,0,.1),(-.10,0,.57),.012,'brass');sphere('Sensor antenna cap',.043,(-.10,0,.57),'brass');cylinder('Sensor dish',.19,.04,(0,.23,.12),'iron',(0,1,0));rod((0,.13,.12),(0,.23,.12),.05,'iron');sphere('Receiver bulb',.045,(0,.29,.12),'brass')
  elif i==68:
   for x in [-.10,0,.10]:cylinder('Logic switch base',.027,.08,(x,.11,.14),'brass');rod((x,.11,.17),(x,.18,.32),.018,'steel','Moving')
   for x in [-.11,.11]:cylinder('Logic connection jack',.035,.065,(x,.02,-.14),'iron')
  elif i==67:
   box('Vertical altitude ruler',(.17,.09,.65),(0,.16,.30),'wood');
   for z in [.07,.17,.27,.37,.47,.57]:box('Height scale graduation',(.08,.018,.012),(.03,.10,z),'steel',bevel=.001)
   box('Altitude moving pointer',(.22,.035,.04),(0,.10,.28),'brass','Moving');travel((0,0,.25))
  elif i==69:
   rod((-.21,.18,.06),(.21,.18,.06),.018,'iron');
   for x in [-.12,.12]:rod((x,.10,.05),(x,.18,.05),.02,'iron')
   torus('Angle protractor arc',.22,.025,(0,.18,.18),'brass',(0,1,0));rod((0,.18,.18),(.19,.18,.31),.018,'rust','Moving');steer(axis=(0,1,0),degrees=60)
  else:
   cylinder('Instrument standoff',.10,.12,(0,.15,.14),'iron',(0,1,0));radius=.20 if i==66 else .25;cylinder('Clock face' if i==66 else 'Speed dial',radius,.035,(0,.20,.17),'wood',(0,1,0));torus('Round dial bezel',radius,.024,(0,.20,.17),'brass',(0,1,0));rod((0,.23,.17),(0,.23,.17+radius*.75),.012,'rust','Moving')
   for j in range(12):a=j*math.tau/12;sphere('Dial tick',.008,(radius*.80*math.cos(a),.225,.17+radius*.80*math.sin(a)),'steel')
   if i==66:rod((-.18,.01,.08),(-.18,.01,.35),.009,'brass');sphere('Clock bell',.08,(-.18,.01,.36),'brass')
   else:box('Speed drive cable socket',(.12,.15,.08),(.2,-.03,-.1),'iron')
   steer(axis=(0,1,0),degrees=70)
 elif i==97:
  box('Parachute packing box',(.3,.25,.20),mat='wood');moving('Moving',(0,0,.16));CUR.bone('Canopy',(0,0,.16));
  for j in range(8):a=j*math.tau/8;rod((.65*math.cos(a),.65*math.sin(a),1.20),(0,0,.16),.008,'wood','Canopy')
  bpy.ops.mesh.primitive_uv_sphere_add(segments=24,ring_count=12,radius=.72,location=(0,0,1.20));o=bpy.context.object;import bmesh;bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.delete(bm,geom=[v for v in bm.verts if v.co.z<-.0001],context='VERTS');bm.to_mesh(o.data);bm.free();o.scale.z=.34;CUR.add(o,'green','Canopy');CUR.clip('Deploy',{'Canopy':[(1,(0,0,1),0,(0,0,0)),(61,(0,0,1),0,(0,0,0))]},False);CUR.uniform_scale_animation=('Canopy',.05,1)
 return True
