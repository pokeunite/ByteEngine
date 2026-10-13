import bpy,math
from mathutils import Vector,Matrix
C=bpy.app.driver_namespace['cyber'];Gun=C['Gun'];scene=bpy.context.scene

def tube(g,name,path,radii,bones,mat='Glove',sides=10,flatten=1):
 verts=[];weights=[]
 for i,p in enumerate(path):
  p=Vector(p);direction=Vector(path[min(i+1,len(path)-1)])-Vector(path[max(0,i-1)])
  if direction.length<1e-7:direction=Vector((0,1,0))
  tangent=direction.normalized();u=tangent.cross(Vector((0,0,1)))
  if u.length<.01:u=tangent.cross(Vector((1,0,0)))
  u.normalize();v=tangent.cross(u).normalized()
  for j in range(sides):
   angle=j*math.tau/sides;verts.append(p+u*(math.cos(angle)*radii[i])+v*(math.sin(angle)*radii[i]*flatten))
   weights.append(bones[i])
 faces=[]
 for i in range(len(path)-1):
  for j in range(sides):faces.append((i*sides+j,i*sides+(j+1)%sides,(i+1)*sides+(j+1)%sides,(i+1)*sides+j))
 faces.append(tuple(range(sides-1,-1,-1)));faces.append(tuple((len(path)-1)*sides+j for j in range(sides)))
 data=bpy.data.meshes.new(name);data.from_pydata(verts,[],faces);data.update();obj=bpy.data.objects.new(g.name+' '+name,data);scene.collection.objects.link(obj)
 g.own(obj,'__skinned',mat);g.parts.pop();g.skinned.append((obj,weights))
 return obj

def finger(g,hand,name,path,radius):
 bones=[]
 for j in range(3):
  bone=hand+name+str(j);g.add_bone(bone,path[j],path[j+1],parent=hand if j==0 else bones[-1],connected=j>0);bones.append(bone)
 # More rings at the joints produce a continuous gloved finger, blended across hinges.
 points=[];weights=[];radii=[]
 for j in range(3):
  a=Vector(path[j]);b=Vector(path[j+1])
  for t in [0,.35,.70]:
   points.append(a.lerp(b,t));weights.append({bones[j]:1} if t>.1 or j==0 else {bones[j-1]:.5,bones[j]:.5});radii.append(radius*(1-.10*j))
 points.append(Vector(path[-1]));weights.append({bones[-1]:1});radii.append(radius*.58)
 tube(g,hand+' '+name,points,radii,weights,'Glove',8)
 # Knuckle armor remains with its finger, not with the static gun.
 for j in range(2):
  p=(Vector(path[j])+Vector(path[j+1]))/2
  g.box('Knuckle protector',p,(radius*1.4,radius*2.2,radius*.9),bones[j],'Graphite',bevel=.002)
 return bones

def arm(g,side,palm,support=False):
 hand=side+'Hand';s=-1 if side=='Right' else 1;palm=Vector(palm)
 wrist=palm+Vector((s*.006,-.043,-.045))
 shoulder=Vector((s*.26,-.55 if g.name=='Pistol' else -.64,-.28))
 elbow=Vector((s*.22,-.34 if g.name=='Pistol' else -.19,-.22 if side=='Right' else -.27))
 upper=side+'UpperArm';fore=side+'Forearm';control=side+'WristControl';pole=side+'ElbowPole'
 g.add_bone(upper,shoulder,elbow,parent='WeaponRoot');g.add_bone(fore,elbow,wrist,parent=upper,connected=True);g.add_bone(hand,wrist,palm+Vector((0,.012,.008)),parent=fore,connected=True)
 g.add_bone(control,wrist,palm+Vector((0,.012,.008)));g.add_bone(pole,elbow+Vector((s*.20,-.10,-.18)),elbow+Vector((s*.20,-.10,-.13)),parent='WeaponRoot')
 g.contacts[side]={'palm':palm,'wrist':wrist,'control':control,'fore':fore,'upper':upper,'pole':pole}
 # One connected sleeve mesh spans upper arm and forearm, with smooth elbow weighting.
 a=shoulder;b=elbow;c=wrist
 path=[a,a.lerp(b,.20),a.lerp(b,.65),b,b.lerp(c,.25),b.lerp(c,.66),b.lerp(c,.90),c]
 weights=[{upper:1},{upper:1},{upper:1},{upper:.5,fore:.5},{fore:1},{fore:1},{fore:1},{fore:1}]
 sleeve=tube(g,side+' continuous sleeve',path,[.068,.067,.061,.059,.054,.043,.036,.034],weights,'Sleeve',12,.82)
 # Circumferential seams and cuff loops, same arm bone as their segment.
 for t in [.10,.34,.60,.87]:
  p=b.lerp(c,t);q=b.lerp(c,min(1,t+.018));tube(g,side+' sleeve seam',[p,q],[.054*(1-t)+.035*t]*2,[{fore:1}]*2,'Seam',12,.83)
 tube(g,side+' glove cuff',[c,c.lerp(palm,.20)],[.035,.033],[{fore:1},{hand:1}],'Rubber',12,.82)
 # A restrained luminous strip and stitched wrist panel matches the concept sleeve.
 plate=b.lerp(c,.68)+Vector((s*.026,0,.015));g.box(side+' forearm plate',plate,(.018,.062,.014),fore,'Graphite',bevel=.003,rotation=(.25,0,0))
 g.box(side+' sleeve orange strip',plate+Vector((0,0,.009)),(.005,.045,.002),fore,'Orange',bevel=.0006,rotation=(.25,0,0))
 for t in [.18,.32,.47,.62,.76]:
  p=b.lerp(c,t)+Vector((s*.032,0,.015));g.box(side+' sleeve stitch',p,(.0018,.010,.0018),fore,'Steel',bevel=.0003)
 if side=='Right':
  g.box('Right palm',palm,(.021,.049,.078),hand,'Glove',bevel=.008)
  g.box('Right hand armor',palm+Vector((-.013,-.002,.003)),(.008,.036,.051),hand,'Graphite',bevel=.006)
  x,y,z=palm
  # Index reaches the trigger; lower fingers wrap the front strap.
  trigger=g.bones['Trigger'][0]
  finger(g,hand,'Index',[(x+.01,y+.02,z+.032),(-.030,y+.050,z+.037),(-.015,trigger.y,trigger.z-.012),(.002,trigger.y+.006,trigger.z-.010)],.0065)
  for i,name in enumerate(['Middle','Ring','Little']):
   zz=z+.011-i*.018
   finger(g,hand,name,[(x+.007,y+.015,zz),(-.026,y+.034,zz),(0,y+.037,zz),(.018,y+.022,zz)],.0073-i*.0005)
  finger(g,hand,'Thumb',[(x,y-.01,z+.031),(-.020,y-.021,z+.045),(.005,y-.025,z+.045),(.018,y-.010,z+.033)],.009)
 else:
  x,y,z=palm
  if g.support_kind=='pistol':
   g.box('Left supporting palm',palm,(.020,.047,.060),hand,'Glove',bevel=.007)
   g.box('Left hand armor',palm+Vector((.014,-.003,0)),(.008,.031,.044),hand,'Graphite',bevel=.005)
   for i,name in enumerate(['Index','Middle','Ring','Little']):
    zz=z+.019-i*.015
    finger(g,hand,name,[(x-.004,y+.007,zz),(.023,y+.031,zz),(.002,y+.034,zz),(-.014,y+.021,zz)],.0068-i*.0003)
   finger(g,hand,'Thumb',[(x,y-.01,z+.024),(.017,y+.012,z+.045),(.011,y+.035,z+.046),(.009,y+.05,z+.032)],.008)
  else:
   g.box('Left supporting palm',palm,(.046,.068,.021),hand,'Glove',bevel=.007)
   g.box('Left hand armor',palm+Vector((.01,-.007,-.011)),(.035,.041,.007),hand,'Graphite',bevel=.005)
   for i,name in enumerate(['Index','Middle','Ring','Little']):
    yy=y+.023-i*.018
    finger(g,hand,name,[(x+.008,yy,z+.002),(.045,yy,z+.023),(.037,yy,z+.055),(.018,yy,z+.061)],.007-i*.0004)
   finger(g,hand,'Thumb',[(x-.015,y-.006,z+.006),(.015,y-.013,z+.019),(-.001,y-.012,z+.029),(-.014,y+.005,z+.027)],.0085)

def add_arms(g):
 g.skinned=[];arm(g,'Right',g.grip);arm(g,'Left',g.support,True)
Gun.add_arms=add_arms
result='Continuous sleeves and articulated glove helpers ready'
