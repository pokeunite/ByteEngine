"""Blender MCP authoring: original explorer mesh on the untouched supplied rig."""
import bpy, math, random, json
from mathutils import Vector, Matrix
random.seed(8041)
rig=bpy.data.objects['PolygonSyntyCharacter']
rig.data.pose_position='REST'
collection=bpy.data.collections.get('Hazard Pay Explorer') or bpy.data.collections.new('Hazard Pay Explorer')
if collection.name not in bpy.context.scene.collection.children:bpy.context.scene.collection.children.link(collection)
# Only this generated collection is replaceable. Preserve the imported reference and user objects.
for obj in list(collection.objects):bpy.data.objects.remove(obj,do_unlink=True)
materials={}
for name,color,metal,rough in [
 ('Suit ivory',(.68,.69,.64),0,.78),('Ivory light',(.82,.82,.74),0,.73),
 ('Industrial yellow',(.78,.42,.025),.18,.39),('Yellow edge',(.95,.62,.07),.22,.33),
 ('Yellow shadow',(.50,.245,.02),.15,.47),('Graphite',(.045,.057,.068),.26,.4),
 ('Rubber',(.022,.029,.036),0,.83),('Steel',(.28,.34,.38),.7,.29),
 ('Visor teal',(.018,.105,.13),.55,.16),('Visor facet',(.028,.18,.21),.5,.2),
 ('Signal cyan',(.05,.75,.88),.25,.24)]:
 m=bpy.data.materials.get('Explorer / '+name) or bpy.data.materials.new('Explorer / '+name);m.diffuse_color=(*color,1);m.use_nodes=True
 p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
 if name=='Signal cyan':p.inputs['Emission Color'].default_value=(*color,1);p.inputs['Emission Strength'].default_value=1.3
 materials[name]=m
parts=[]
def mesh(name,vertices,faces,mat,weights):
 data=bpy.data.meshes.new(name);data.from_pydata(vertices,[],faces);data.materials.append(materials[mat]);data.update()
 obj=bpy.data.objects.new(name,data);collection.objects.link(obj);obj.parent=rig;obj.matrix_parent_inverse=Matrix.Identity(4);obj.matrix_basis=Matrix.Identity(4)
 groups={}
 for i in range(len(vertices)):
  ws=weights(i,Vector(vertices[i])) if callable(weights) else {weights:1} if isinstance(weights,str) else weights
  total=sum(ws.values())
  for bone,value in ws.items():
   if value<=.00001:continue
   group=groups.get(bone)
   if group is None:group=groups[bone]=obj.vertex_groups.new(name=bone)
   group.add([i],value/total,'REPLACE')
 parts.append(obj);return obj
def box(name,center,size,mat,bone,bevel=.7):
 x,y,z=center;a,b,c=[v/2 for v in size]
 v=[(x+i*a,y+j*b,z+k*c) for i,j,k in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
 o=mesh(name,v,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(3,7,6,2),(0,4,7,3),(1,2,6,5)],mat,bone)
 if bevel:
  bpy.context.view_layer.objects.active=o;o.select_set(True);mod=o.modifiers.new('Manufactured edge bevel','BEVEL');mod.width=bevel;mod.segments=1
  bpy.ops.object.modifier_apply(modifier=mod.name);o.select_set(False)
 return o
def tube(name,points,radii,mat,weights,sides=10,cap=True):
 points=[Vector(p) for p in points];verts=[]
 for i,p in enumerate(points):
  axis=(points[min(i+1,len(points)-1)]-points[max(0,i-1)]).normalized();seed=Vector((0,0,1)) if abs(axis.z)<.9 else Vector((0,1,0));u=axis.cross(seed).normalized();v=axis.cross(u).normalized()
  r=radii[i] if isinstance(radii,list) else radii;ru,rv=r if isinstance(r,tuple) else (r,r)
  for j in range(sides):
   q=p+u*math.cos(j*2*math.pi/sides)*ru+v*math.sin(j*2*math.pi/sides)*rv;verts.append(tuple(q))
 faces=[]
 for i in range(len(points)-1):
  for j in range(sides):faces.append((i*sides+j,i*sides+(j+1)%sides,(i+1)*sides+(j+1)%sides,(i+1)*sides+j))
 if cap:faces.extend([tuple(reversed(range(sides))),tuple((len(points)-1)*sides+j for j in range(sides))])
 return mesh(name,verts,faces,mat,weights)
def sphere(name,c,r,mat,bone,segments=16,rings=9,cap_angle=math.pi):
 verts=[]
 # Polar axis is depth (+Z); caps are layered helmet/visor surfaces.
 for i in range(rings+1):
  theta=.006+(cap_angle-.012)*i/rings
  for j in range(segments):
   phi=j*2*math.pi/segments
   verts.append((c[0]+r[0]*math.sin(theta)*math.cos(phi),c[1]+r[1]*math.sin(theta)*math.sin(phi),c[2]+r[2]*math.cos(theta)))
 faces=[]
 for i in range(rings):
  for j in range(segments):
   a=i*segments+j;b=i*segments+(j+1)%segments;cc=(i+1)*segments+(j+1)%segments;d=(i+1)*segments+j
   if cap_angle<math.pi and i%2:faces.extend([(a,b,cc),(a,cc,d)])
   else:faces.append((a,b,cc,d))
 obj=mesh(name,verts,faces,mat,bone)
 if mat=='Visor teal':
  obj.data.materials.append(materials['Visor facet'])
  for p in obj.data.polygons:p.material_index=1 if random.random()<.14 else 0
 return obj
def torso_weights(i,p):
 levels=[('Hips',91),('Spine_01',103),('Spine_02',120),('Spine_03',137)]
 for (a,y),(b,yy) in zip(levels,levels[1:]):
  if y<=p.y<=yy:t=(p.y-y)/(yy-y);return {a:1-t,b:t}
 return {levels[0 if p.y<91 else -1][0]:1}
def limb_weights(a,b,joint,spread=6):
 def weights(i,p):
  direction=(Vector(rig.data.bones[b].tail_local)-Vector(rig.data.bones[b].head_local)).normalized()
  t=max(0,min(1,((p-Vector(joint)).dot(direction)+spread)/(2*spread)))
  return {a:1-t,b:t}
 return weights
# Connected torso, with enough rings to bend at the supplied spine joints.
tube('Pressure suit torso',[(0,y,-1) for y in [86,91,97,104,112,121,130,137,142]],[(13,10),(15,10),(14,10),(14,10),(17,11),(20,12),(22,11),(22,10),(10,8)],'Suit ivory',torso_weights,16)
box('Belt main',(0,96,0),(31,7,22),'Graphite','Hips',1.1)
box('Belt buckle',(0,96,12.5),(8,6,2.6),'Steel','Hips',.7);box('Buckle inset',(0,96,14),(5.2,3.5,.7),'Rubber','Hips',.3)
# Shaped chest shell, six-sided outline rather than a plain cuboid.
outline=[(-17,136),(17,136),(18,123),(11,108),(-11,108),(-18,123)]
verts=[(x,y,z) for z in [9.5,15] for x,y in outline];n=len(outline)
chest=mesh('Chest protective shell',verts,[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],'Industrial yellow','Spine_02')
box('Chest panel gasket',(0,125,15.5),(9,13,2),'Graphite','Spine_02',1)
box('Chest panel steel',(0,125,16.7),(6,10,.9),'Steel','Spine_02',.6)
box('Chest status light',(0,128,17.3),(3.7,2.4,.5),'Signal cyan','Spine_02',.3)
box('Chest service display',(0,122.8,17.4),(3.8,2,.5),'Rubber','Spine_02',.2)
for side in [-1,1]:
 s='L' if side==1 else 'R';upper='Shoulder_'+s;lower='Elbow_'+s;hand='Hand_'+s;thigh='UpperLeg_'+s;shin='LowerLeg_'+s
 elbow=rig.data.bones[lower].head_local;wrist=rig.data.bones[hand].head_local;knee=rig.data.bones[shin].head_local
 armpts=[(side*x,139-(x-20)*.05,-3.5) for x in [20,26,34,43,49,53,57,65,73,79]]
 tube(s+' flexible sleeve',armpts,[8,8,7.3,6.4,5.9,5.7,5.7,5.8,5.2,4.7],'Ivory light',limb_weights(upper,lower,elbow),12)
 tube(s+' shoulder gasket',[(side*x,138,-3.5) for x in [19,22,27]],[(9,9),(9.2,9.2),(8.5,8.5)],'Graphite',upper,12)
 box(s+' shoulder plate',(side*25,146,-2),(17,5,19),'Industrial yellow',upper,1.8)
 box(s+' shoulder trim',(side*25,148.6,-2),(14,1.2,16),'Yellow edge',upper,.7)
 tube(s+' elbow joint',[(side*x,137,-3.5) for x in [50,52,55,57]],[(6.8,6.8)]*4,'Rubber',limb_weights(upper,lower,elbow),12)
 tube(s+' bracer chassis',[(side*x,136.6,-3.5) for x in [59,62,72,76]],[6.8,7.4,6.5,5.5],'Graphite',lower,10)
 box(s+' bracer front shell',(side*67,137,3),(14,10,3.2),'Industrial yellow',lower,1.4)
 box(s+' bracer top plate',(side*67,143.4,-3),(14,2,8),'Yellow edge',lower,.7)
 tube(s+' wrist seal',[(side*x,136,-3.5) for x in [77,79,81]],[5.2,5.2,4.9],'Rubber',hand,12)
 tube(s+' cuff yellow ring',[(side*x,136,-3.5) for x in [76.8,78.2]],[5.5,5.5],'Yellow edge',lower,12)
 box(s+' glove palm',(side*85.4,135.5,-3),(11,5.5,12),'Graphite',hand,1.6)
 box(s+' glove dorsal plate',(side*85,139,-3),(8,1.5,8.5),'Industrial yellow',hand,.8)
 # Four distinct fingers; middle/ring/pinky follow the source's grouped finger chain.
 suffix='' if side==1 else '_1'
 for f,z in enumerate([1.0,-2.0,-5.0,-8.0]):
  chain=('IndexFinger' if f==0 else 'Finger');names=[chain+'_'+str(i).zfill(2)+suffix for i in [1,2,3,4]]
  src=[rig.data.bones[name].head_local.copy() for name in names]
  dz=z-src[0].z;lengthscale=1 if f<2 else .94 if f==2 else .82
  pts=[]
  for p in src:pts.append((side*(90+(abs(p.x)-90)*lengthscale),p.y,p.z+dz))
  last=Vector(pts[-1]);pts.append(tuple(last+Vector((side*2.4,-.7,0))))
  def fw(i,p,names=names):
   k=min(i//8,len(names)-1);return {names[k]:1}
  tube(s+' glove finger '+str(f+1),pts,[1.35,1.32,1.2,1.05,.7],'Graphite',fw,8)
  for j,p in enumerate(pts[1:3]):box(s+' finger knuckle '+str(f)+' '+str(j),(p[0],p[1]+1.3,p[2]),(2.3,.8,2.0),'Steel',names[j],.3)
 names=['Thumb_'+str(i).zfill(2)+suffix for i in [1,2,3]];pts=[tuple(rig.data.bones[name].head_local) for name in names]+[tuple(rig.data.bones[names[-1]].tail_local)]
 tube(s+' glove thumb',pts,[2,1.8,1.45,.9],'Graphite',lambda i,p:{names[min(i//8,2)]:1},8)
 # Trousers and knee support keep blends around the unchanged source joint locations.
 pts=[(side*x,y,z) for x,y,z in [(9.9,86,-2),(10,79,-2),(10.5,69,-2),(11,57,-2),(11.1,48,-2),(11.2,43,-2),(11.3,38,-2),(11.4,28,-2),(11.4,17,-2),(11.4,8,-3)]]
 tube(s+' trouser and shin undersuit',pts,[(8.8,8.4),(9.0,8.4),(8.4,7.5),(7.2,6.8),(6.7,6.5),(6.6,6.3),(6.2,6),(5.9,5.8),(5.2,5.4),(5.0,5.2)],'Suit ivory',limb_weights(thigh,shin,knee),12)
 box(s+' thigh outer armor',(side*11,68,5.7),(14,27,5),'Industrial yellow',thigh,1.8)
 box(s+' thigh side strap',(side*11,61,0),(19,4,17),'Graphite',thigh,.6)
 box(s+' strap buckle',(side*19.3,61,6),(3,5,3),'Steel',thigh,.6)
 box(s+' knee outer frame',(side*11.2,44,5.5),(15,17,7),'Industrial yellow',shin,2.4)
 box(s+' knee impact pad',(side*11.2,44,10),(12,13,4),'Graphite',shin,1.8)
 box(s+' shin armor',(side*11.4,24,5),(12,22,6),'Industrial yellow',shin,1.8)
 box(s+' shin centre relief',(side*11.4,23,8.3),(5.5,15,1),'Yellow edge',shin,.5)
 ankle='Ankle_'+s
 box(s+' boot upper',(side*11.4,10,1),(13,17,17),'Graphite',ankle,2.3)
 box(s+' boot toe',(side*11.4,3.4,10),(16,8,23),'Graphite',ankle,2)
 box(s+' boot sole',(side*11.4,-.5,8),(17,3.5,28),'Rubber',ankle,1)
 box(s+' boot yellow instep',(side*11.4,8,6),(14,4,15),'Industrial yellow',ankle,1)
 for yy in [12,17]:
  box(s+' ankle strap '+str(yy),(side*11.4,yy,1),(14,2.5,18),'Rubber',ankle,.6)
  box(s+' ankle buckle '+str(yy),(side*17.8,yy,6),(3,3.3,3),'Steel',ankle,.5)
 for j in [-1,0,1]:box(s+' boot toe rib '+str(j),(side*11.4+j*4,4,21.2),(2.4,4,1),'Rubber',ankle,.4)
 # Utility pouches and the visible shoulder harness.
 box(s+' belt pouch',(side*17.3,96,4),(8,12,11),'Industrial yellow','Hips',1.3)
 box(s+' pouch flap',(side*17.3,100.3,10.2),(7,4,1.6),'Yellow edge','Hips',.5)
 box(s+' pouch catch',(side*17.3,95.2,10.3),(2,4,1),'Graphite','Hips',.3)
 box(s+' pouch status',(side*17.3,92.4,10.8),(1,2.5,.4),'Signal cyan','Hips',.2)
 tube(s+' shoulder harness',[(side*14.4,143,4),(side*14.7,135,12),(side*14.2,127,14),(side*12.8,118,13)],[(2.3,1.2)]*4,'Graphite','Spine_03',8)
 box(s+' harness buckle',(side*14.5,136,13.9),(5.8,8,2),'Steel','Spine_03',.6)
 box(s+' buckle centre',(side*14.5,136,15),(3.7,4.8,.8),'Graphite','Spine_03',.2)
 hose=[(side*4.6,115,17),(side*6.5,111.8,17.3),(side*9,109,16),(side*12.3,108,13),(side*15,110,10)]
 tube(s+' breathing hose',hose,2.1,'Rubber','Spine_02',10)
 for j in range(14):
  t=j/13*4;i=min(3,int(t));p=Vector(hose[i]).lerp(Vector(hose[i+1]),t-i);axis=(Vector(hose[i+1])-Vector(hose[i])).normalized();tube(s+' hose rib '+str(j),[p-axis*.3,p+axis*.3],2.35,'Steel','Spine_02',8)
# Life support on the back is deliberately compact for third-person readability.
box('Backpack rubber seat',(0,124,-13),(27,37,8),'Rubber','Spine_02',2)
box('Backpack yellow frame',(0,125,-18),(25,34,8),'Industrial yellow','Spine_02',2.4)
box('Backpack service cover',(0,126,-23),(19,23,3),'Graphite','Spine_02',1.6)
for y in [120,123,126,129,132]:box('Backpack cooling slot '+str(y),(0,y,-24.7),(12,1,.5),'Steel','Spine_02',.2)
box('Backpack power pack',(0,111,-19),(17,7,9),'Yellow shadow','Spine_01',1)
for side in [-1,1]:
 tube('Backpack oxygen pod '+str(side),[(side*13,112,-17),(side*13,114,-17),(side*13,134,-17),(side*13,137,-17)],[3.2,4,4,3.2],'Graphite','Spine_02',12)
 box('Backpack canister label '+str(side),(side*13,126,-21.2),(3,8,.5),'Yellow edge','Spine_02',.3)
# Helmet shell, collar, seal and separate faceted glazed visor.
for y,r,mat in [(145,10.5,'Rubber'),(147,11.8,'Steel'),(149,11.8,'Graphite'),(150.3,11.3,'Steel')]:tube('Pressure collar '+str(y),[(0,y-.6,0),(0,y+.6,0)],[(r,r*.83)]*2,mat,'Neck',24)
sphere('Helmet armor shell',(0,165,0),(15.3,18.2,14.3),'Industrial yellow','Head',20,12)
sphere('Visor rubber seal',(0,165,4.8),(14.2,16.2,12.8),'Graphite','Head',20,8,1.28)
sphere('Visor steel rim',(0,165,5.1),(13.5,15.5,13.1),'Steel','Head',20,8,1.26)
sphere('Faceted dark teal visor',(0,165,5.8),(12.6,14.5,13.2),'Visor teal','Head',20,9,1.24)
box('Helmet brow armor',(0,181,3.6),(10,3.1,12),'Yellow edge','Head',.9)
box('Helmet brow indicator',(0,179,13),(6.5,1,1.1),'Signal cyan','Head',.3)
box('Helmet chin respirator',(0,149.7,11),(11,5,7),'Graphite','Head',1.3)
box('Helmet chin light',(0,150,15),(4,1.5,.8),'Signal cyan','Head',.3)
for side in [-1,1]:
 tube('Helmet comms gasket '+str(side),[(side*14.5,165,0),(side*17.5,165,0)],[6.8,6.8],'Graphite','Head',12)
 tube('Helmet comms shell '+str(side),[(side*17.5,165,0),(side*18.8,165,0)],[5.3,5.3],'Yellow edge','Head',12)
 box('Helmet comms strip '+str(side),(side*19,165,0),(.8,5,1.9),'Signal cyan','Head',.3)
 box('Helmet temple hinge '+str(side),(side*11,153,10),(4,4,4),'Steel','Head',.9)
# Bake only mesh modifiers; consolidate for inexpensive per-material render parts.
bpy.ops.object.select_all(action='DESELECT')
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();body=bpy.context.object;body.name='HazardPay Explorer - skinned mesh';body.data.name='HazardPay Explorer topology'
modifier=body.modifiers.new('Source skeleton deformation','ARMATURE');modifier.object=rig;modifier.use_vertex_groups=True
rig.data.pose_position='REST';rig.show_in_front=True
for o in bpy.data.objects:
 if o.name in ['PolygonSyntyCharacterMesh','Icosphere']:o.hide_render=True;o.hide_set(True)
for o in parts:
 pass
# Scene object settings and an orthographic studio camera for inspection.
scene=bpy.context.scene;scene.frame_set(0);scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=1400;scene.render.resolution_y=1400;scene.render.resolution_percentage=100
scene.world.color=(.2,.2,.2);scene.view_settings.view_transform='AgX'
def world(v):return rig.matrix_world@Vector(v)
camera=bpy.data.objects.get('Explorer preview camera')
if camera is None:camera=bpy.data.objects.new('Explorer preview camera',bpy.data.cameras.new('Explorer preview camera'));scene.collection.objects.link(camera)
camera.location=(2.6,-6,2.1);target=Vector((0,0,.93));camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=2.5;scene.camera=camera
for name,loc,power,size in [('Explorer key',(2,-4,5),550,4),('Explorer fill',(-3,-2,2),350,3),('Explorer rim',(1,3,4),700,3)]:
 o=bpy.data.objects.get(name)
 if o is None:o=bpy.data.objects.new(name,bpy.data.lights.new(name,'AREA'));scene.collection.objects.link(o)
 o.location=loc;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(target-o.location).to_track_quat('-Z','Y').to_euler()
body.select_set(True);bpy.context.view_layer.objects.active=body
result={'vertices':len(body.data.vertices),'polygons':len(body.data.polygons),'materials':len(body.data.materials),'bones':len(rig.data.bones),'actions':len(rig.animation_data.nla_tracks),'bounds':list(body.dimensions)}
