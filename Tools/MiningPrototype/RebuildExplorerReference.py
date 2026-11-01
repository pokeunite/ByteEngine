"""Second reference pass: tailored pressure suit, shaped armor and brighter machinery yellow.
Executed through the open Blender MCP; never changes source armature bone data.
"""
import bpy, pathlib, math, random
from mathutils import Vector,Matrix
folder=pathlib.Path('C:/Users/codex/Documents/palclone/SourceArt/HazardPayExplorer')
if not (folder/'HazardPayExplorer-before-reference-rebuild.blend').exists():bpy.ops.wm.save_as_mainfile(filepath=str(folder/'HazardPayExplorer-before-reference-rebuild.blend'))
source=pathlib.Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine/Tools/MiningPrototype/BuildExplorerCharacter.py').read_text()
exec(source.split('# Connected torso,')[0])
for name,color,metal,rough in [('Industrial yellow',(1,.58,.008),.12,.42),('Yellow edge',(1,.72,.035),.15,.36),('Yellow shadow',(.65,.32,.006),.1,.5),('Suit ivory',(.78,.77,.66),0,.84),('Ivory light',(.86,.84,.73),0,.83),('Graphite',(.035,.043,.049),.1,.6),('Steel',(.33,.37,.37),.65,.4),('Visor teal',(.009,.12,.14),.4,.24),('Visor facet',(.012,.19,.21),.35,.25)]:
 m=materials[name];m.diffuse_color=(*color,1);p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough

def cloth(name,points,radii,material,weights,sides=14):
 o=tube(name,points,radii,material,weights,sides)
 # Controlled broad triangular fabric folds; the joint rings remain in place.
 for i,v in enumerate(o.data.vertices):
  ring=i//sides;j=i%sides
  if ring not in [0,len(points)-1]:
   v.co+=Vector((math.sin(i*8.7),math.cos(i*4.6),math.sin(i*3.1)))*.22
 faces=[]
 for poly in o.data.polygons:
  q=list(poly.vertices)
  if len(q)==4:
   a,b,c,d=q;faces.extend([(a,b,c),(a,c,d)])
  else:faces.append(q)
 names=[g.name for g in o.vertex_groups];weights_by_vertex=[[(g.group,g.weight) for g in v.groups] for v in o.data.vertices]
 verts=[tuple(v.co) for v in o.data.vertices];data=bpy.data.meshes.new(name+' tailored triangles');data.from_pydata(verts,[],faces);data.materials.append(materials[material]);o.data=data
 groups_by_name={name:o.vertex_groups.get(name) or o.vertex_groups.new(name=name) for name in names}
 for i,groups in enumerate(weights_by_vertex):
  for group,weight in groups:groups_by_name[names[group]].add([i],weight,'REPLACE')
 return o

def plate(name,center,outline,depth,material,bone,bevel=.5):
 x,y,z=center;n=len(outline);verts=[(x+xx,y+yy,z+zz) for zz in [-depth/2,depth/2] for xx,yy in outline]
 o=mesh(name,verts,[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],material,bone)
 if bevel:
  bpy.context.view_layer.objects.active=o;o.select_set(True);m=o.modifiers.new('Armor edge chamfer','BEVEL');m.width=bevel;m.segments=1;bpy.ops.object.modifier_apply(modifier=m.name);o.select_set(False)
 return o

cloth('Fitted ivory pressure jacket',[(0,y,-1) for y in [86,91,98,105,113,121,129,137,143,147]],[(14,9),(15,10),(14,10),(15,10.3),(17,11),(20,11.5),(23,11.5),(24,10.5),(19,9),(10,8)],'Suit ivory',torso_weights,18)
cloth('Yellow pressure trousers pelvis',[(0,94,-1),(0,88,-1),(0,83,-1)],[(15.7,10.5),(17,11.2),(15.2,10)],'Industrial yellow','Hips',16)
# The chest rises to the collar, tapers naturally to the abdomen, and wraps its sides.
plate('Contoured chest cuirass',(0,129,11),[(-15,17),(15,17),(18,11),(14,-10),(8,-18),(-8,-18),(-14,-10),(-18,11)],6,'Industrial yellow','Spine_02',1.4)
plate('Chest lower highlight',(0,113,15),[(-9,2),(9,2),(6,-1),(-6,-1)],1,'Yellow edge','Spine_02',.25)
plate('Recessed instrument gasket',(0,132,16),[(-5,7),(4,7),(6,4),(5,-7),(-4,-7),(-6,-4)],2,'Graphite','Spine_02',.5)
plate('Instrument frame',(0,132,17.2),[(-3.7,5),(3.7,5),(3.7,-5),(-3.7,-5)],.7,'Steel','Spine_02',.3)
box('Upper cyan status',(0,135,17.8),(4,2,.8),'Signal cyan','Spine_02',.3)
box('Lower instrument',(0,129,17.8),(4,2.3,.8),'Rubber','Spine_02',.3)
box('Instrument inset',(0,129.6,18.3),(2.8,.45,.2),'Ivory light','Spine_02',.1)
box('Wide flexible utility belt',(0,95.5,0),(33,7.3,24),'Rubber','Hips',1.5)
for side in [-1,1]:
 s='L' if side>0 else 'R';upper='Shoulder_'+s;lower='Elbow_'+s;hand='Hand_'+s;thigh='UpperLeg_'+s;shin='LowerLeg_'+s;ankle='Ankle_'+s
 elbow=rig.data.bones[lower].head_local;knee=rig.data.bones[shin].head_local
 # Fabric is visibly padded and wrinkled, rather than exposed cylindrical robot joints.
 xs=[20,25,31,37,43,48,51,54,57,60,66,72,77,80]
 cloth(s+' full ivory sleeve',[(side*x,138.5-(x-20)*.04,-3.5) for x in xs],[9,9,8.3,8,7.2,6.8,6.6,6.5,6.4,6.2,6,5.6,5,4.7],'Ivory light',limb_weights(upper,lower,elbow,8),14)
 # Shaped low-profile shoulder armor over cloth.
 box(s+' shoulder rubber skirt',(side*25,146,-3),(20,3,21),'Graphite',upper,1.6)
 box(s+' shoulder yellow shell',(side*25,148,-3),(18.5,3.6,20),'Industrial yellow',upper,1.9)
 box(s+' shoulder bright rim',(side*25,150,-3),(15.5,.8,16),'Yellow edge',upper,.6)
 box(s+' clavicle coupling',(side*14,148,-2),(7,6,10),'Graphite','Spine_03',1)
 box(s+' clavicle indicator',(side*14,148.5,3.2),(1.3,3,.7),'Signal cyan','Spine_03',.2)
 tube(s+' faceted forearm cuff',[(side*x,136,-3.5) for x in [59,61,65,71,75,77]],[(6.4,6),(7.6,7.2),(7.8,7.4),(7.1,6.7),(6,5.7),(5.5,5.2)],'Graphite',lower,12)
 box(s+' forearm armor crown',(side*67.5,143,-3.5),(15,2.5,10.5),'Industrial yellow',lower,1)
 plate(s+' forearm protective face',(side*67.5,136,4.5),[(-6,5),(5,5),(7,2),(5,-5),(-6,-5),(-7,-2)],2,'Industrial yellow',lower,.7)
 for x in [61,74.7]:tube(s+' bracer seam '+str(x),[(side*x,136,-3.5),(side*(x+.5),136,-3.5)],7 if x<70 else 6,'Rubber',lower,12)
 tube(s+' glove yellow seal',[(side*x,136,-3.4) for x in [77.6,79.3]],5.3,'Industrial yellow',hand,12)
 tube(s+' glove seal gasket',[(side*x,136,-3.4) for x in [79.5,80.6]],4.9,'Graphite',hand,12)
 box(s+' padded glove palm',(side*85.4,135.5,-3),(11,5.7,12),'Graphite',hand,1.7)
 box(s+' glove dorsal yellow plate',(side*85,138.9,-3),(8,1.4,8),'Industrial yellow',hand,.8)
 suffix='' if side>0 else '_1'
 for f,z in enumerate([1,-2,-5,-8]):
  names=[('IndexFinger' if f==0 else 'Finger')+'_'+str(i).zfill(2)+suffix for i in [1,2,3,4]];src=[rig.data.bones[n].head_local.copy() for n in names];dz=z-src[0].z;factor=[1,1,.94,.82][f];pts=[(side*(90+(abs(p.x)-90)*factor),p.y,p.z+dz) for p in src];pts.append(tuple(Vector(pts[-1])+Vector((side*2.4,-.7,0))))
  tube(s+' articulated finger '+str(f),pts,[1.38,1.3,1.18,1.02,.65],'Graphite',lambda i,p,names=names:{names[min(i//8,3)]:1},8)
  for j,p in enumerate(pts[1:3]):box(s+' finger reinforcement '+str(f)+str(j),(p[0],p[1]+1.2,p[2]),(2.2,.65,2),'Rubber',names[j],.2)
 names=['Thumb_'+str(i).zfill(2)+suffix for i in [1,2,3]];pts=[tuple(rig.data.bones[n].head_local) for n in names]+[tuple(rig.data.bones[names[-1]].tail_local)]
 tube(s+' glove thumb',pts,[2,1.8,1.4,.8],'Graphite',lambda i,p,names=names:{names[min(i//8,2)]:1},8)
 # Full yellow trouser volume replaces thin floating armor strips.
 cloth(s+' full yellow upper trousers',[(side*x,y,z) for x,y,z in [(9.9,90,-2),(10,83,-2),(10.3,77,-2),(10.6,69,-2),(11,60,-2),(11.1,52,-2),(11.2,47,-2)]],[(10,9.2),(10.3,9.5),(9.7,9),(9,8.4),(8,7.5),(7.2,6.8),(6.7,6.6)],'Industrial yellow',limb_weights(thigh,shin,knee,8),14)
 cloth(s+' ivory knee articulation',[(side*11.2,y,-2) for y in [51,47,43,39,35]],[(7,6.8),(7.4,7),(7.2,6.8),(6.5,6.3),(6,6)],'Suit ivory',limb_weights(thigh,shin,knee,7),12)
 plate(s+' armored knee outer',(side*11.2,43,5),[(-6,9),(5,9),(8,4),(6,-10),(-5,-10),(-8,-5)],5,'Steel',shin,.7)
 plate(s+' raised graphite knee pad',(side*11.2,43,8.2),[(-5,7),(4,7),(6,3),(4,-8),(-4,-8),(-6,-3)],3.4,'Graphite',shin,.9)
 plate(s+' knee pad inset',(side*11.2,43,10),[(-3.8,5.5),(3.2,5.5),(4.3,2),(3,-6),(-3,-6),(-4.2,-2)],.6,'Rubber',shin,.3)
 cloth(s+' yellow shin armor suit',[(side*11.4,y,-2) for y in [36,32,26,20,15]],[(6.3,6.3),(7.5,7.4),(7.2,7),(6,6),(5.2,5.6)],'Industrial yellow',shin,12)
 plate(s+' shin angular face',(side*11.4,24,5),[(-5,8),(4,8),(6,2),(3,-8),(-3,-8),(-6,2)],2.5,'Yellow edge',shin,.6)
 cloth(s+' ivory ankle gaiter',[(side*11.4,y,-1) for y in [17,14.5,12]],[(5.7,6.1),(6.2,6.6),(5.6,6)],'Ivory light',ankle,12)
 box(s+' black boot upper',(side*11.4,8,1),(13,16,18),'Graphite',ankle,2)
 box(s+' armored boot toe',(side*11.4,3.3,11),(16,8,24),'Graphite',ankle,2)
 box(s+' heavy tread sole',(side*11.4,-.5,8),(17,3,29),'Rubber',ankle,1.2)
 box(s+' yellow boot toe surround',(side*11.4,6.8,10),(15,3.4,19),'Industrial yellow',ankle,1.6)
 box(s+' boot toe centre',(side*11.4,8.6,13),(9,1,10),'Graphite',ankle,.8)
 for yy in [10,15]:
  box(s+' boot retaining band '+str(yy),(side*11.4,yy,1),(14,2.8,18),'Rubber',ankle,.7);box(s+' yellow strap catch '+str(yy),(side*11.4,yy,10.5),(6,2,.7),'Industrial yellow',ankle,.3)
  box(s+' boot cyan tag '+str(yy),(side*17.9,yy,6),(1,1.7,2),'Signal cyan',ankle,.2)
 for j in [-1,0,1]:box(s+' toe tread '+str(j),(side*11.4+j*4,2,23),(2,4,1),'Rubber',ankle,.3)
 box(s+' thigh strap',(side*11,67,0),(20,3.5,20),'Graphite',thigh,.7)
 box(s+' silver thigh buckle',(side*16.5,67,10.5),(4.5,5,1.2),'Steel',thigh,.4);box(s+' thigh buckle inset',(side*16.5,67,11.2),(2.7,2.8,.5),'Rubber',thigh,.15)
 box(s+' thigh utility case',(side*20,70,0),(7,14,12),'Graphite',thigh,1.1);box(s+' thigh yellow case lid',(side*23.7,70,1),(2,12,10),'Industrial yellow',thigh,.8)
 box(s+' belt pouch',(side*17.8,96,3),(8,12,12),'Industrial yellow','Hips',1.3);box(s+' belt pouch inset',(side*17.8,96,9.5),(4.5,8,.8),'Graphite','Hips',.5);box(s+' belt cyan tag',(side*17.8,96,10),(1.4,4,.4),'Signal cyan','Hips',.15)
 # Broad dark shoulder straps, recognizable silver rectangular buckles.
 tube(s+' shoulder harness',[(side*14,148,2),(side*14.5,143,11),(side*14.6,136,15),(side*13,124,16),(side*11,113,13)],[(2,1.0)]*5,'Graphite','Spine_03',8)
 box(s+' shoulder strap buckle',(side*14.5,138,16),(5.3,6.6,1.5),'Steel','Spine_03',.4);box(s+' shoulder buckle opening',(side*14.5,138.5,17),(3.5,4.2,.6),'Rubber','Spine_03',.15)
 hose=[(side*6.2,119,17),(side*8,115,18),(side*11,112,18),(side*15,113,15),(side*17,116,11)]
 tube(s+' ribbed breathing hose',hose,1.8,'Rubber','Spine_02',10)
 for j in range(20):
  t=j/19*4;i=min(3,int(t));q=Vector(hose[i]).lerp(Vector(hose[i+1]),t-i);axis=(Vector(hose[i+1])-Vector(hose[i])).normalized();tube(s+' hose corrugation '+str(j),[q-axis*.22,q+axis*.22],2,'Graphite','Spine_02',8)
box('Belt silver rectangular buckle',(0,95.5,13),(8,6,2),'Steel','Hips',.5);box('Belt buckle dark centre',(0,95.5,14.3),(5.3,3.8,.7),'Graphite','Hips',.2)
box('Backpack yellow life support',(0,129,-17),(27,34,10),'Industrial yellow','Spine_02',2.2)
box('Backpack panel',(0,129,-23),(21,24,3),'Graphite','Spine_02',1.2)
for yy in [122,125,128,131,134]:box('Cooling louver '+str(yy),(0,yy,-24.8),(15,1.1,.7),'Rubber','Spine_02',.2)
for side in [-1,1]:tube('Oxygen canister '+str(side),[(side*13,114,-15),(side*13,117,-15),(side*13,138,-15),(side*13,141,-15)],[3.2,4,4,3.2],'Steel','Spine_02',12)
# Broad pressure collar and a squarer visor silhouette, avoiding the old bubble-eye shape.
for y,r,mat in [(146,11.8,'Rubber'),(147.5,12.8,'Steel'),(149,12.3,'Graphite'),(150,11.8,'Suit ivory')]:tube('Pressure collar '+str(y),[(0,y-.6,0),(0,y+.6,0)],[(r,r*.82)]*2,mat,'Neck',24)
sphere('Yellow helmet shell',(0,166,-1),(15.6,18,14.5),'Industrial yellow','Head',24,12)
def visor_ring(name,rx,ry,z,material,inner=.9):
 verts=[];n=24
 for factor,zz in [(1,z),(inner,z+.6)]:
  for j in range(n):
   a=2*math.pi*j/n;x=math.copysign(abs(math.cos(a))**.78,math.cos(a))*rx*factor;y=166+math.copysign(abs(math.sin(a))**.78,math.sin(a))*ry*factor;verts.append((x,y,zz))
 return mesh(name,verts,[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],material,'Head')
visor_ring('Visor thick dark rim',14.5,15.6,10.7,'Graphite',.87)
visor_ring('Visor edge gasket',12.75,13.7,11.4,'Steel',.95)
# Staggered elliptical rings triangulate into broad glass facets, with low central curvature.
verts=[];n=24
for k,(f,z) in enumerate([(1,12.2),(.66,14.5),(.30,15.8)]):
 for j in range(n):
  a=(j+(k%2)*.5)*math.tau/n;verts.append((12.1*f*math.copysign(abs(math.cos(a))**.78,math.cos(a)),166+13.1*f*math.copysign(abs(math.sin(a))**.78,math.sin(a)),z))
faces=[]
for k in range(2):
 for j in range(n):a=k*n+j;b=k*n+(j+1)%n;c=(k+1)*n+(j+1)%n;d=(k+1)*n+j;faces.extend([(a,b,c),(a,c,d)])
faces.append(tuple(range(2*n,3*n)));glass=mesh('Broad faceted teal visor',verts,faces,'Visor teal','Head');glass.data.materials.append(materials['Visor facet'])
for face in glass.data.polygons:
 face.material_index=1 if random.random()<.08 else 0
box('Helmet crown stripe',(0,182.7,1),(8,2.4,13),'Yellow edge','Head',.8);box('Brow cyan strip',(0,180.5,12),(6,1,1),'Signal cyan','Head',.25)
box('Chin respirator',(0,150.6,10.8),(12,5,7),'Graphite','Head',1.1);box('Chin cyan light',(0,151,15),(4,1.5,.7),'Signal cyan','Head',.25)
for side in [-1,1]:
 tube('Helmet ear gasket '+str(side),[(side*14.8,166,-1),(side*17.8,166,-1)],6,'Graphite','Head',12);tube('Helmet ear plate '+str(side),[(side*17.8,166,-1),(side*18.8,166,-1)],5.3,'Industrial yellow','Head',12)
 box('Ear cyan strip '+str(side),(side*19,166,-1),(.8,5.5,1.6),'Signal cyan','Head',.2)
 sphere('Helmet cheek coupling '+str(side),(side*9.8,152.7,11),(2.5,2.5,2),'Steel','Head',10,6)
bpy.ops.object.select_all(action='DESELECT')
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();body=bpy.context.object;body.name='HazardPay Explorer - skinned mesh';body.data.name='Explorer reference rebuild topology';body.modifiers.new('Source skeleton deformation','ARMATURE').object=rig
for o in bpy.data.objects:
 if o.name in ['PolygonSyntyCharacterMesh','Icosphere']:o.hide_render=True;o.hide_set(True)
scene=bpy.context.scene;scene.frame_set(0);scene.view_settings.view_transform='Standard';scene.view_settings.look='Medium High Contrast';scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=1400;scene.render.resolution_y=1400;scene.render.resolution_percentage=100
camera=bpy.data.objects['Explorer preview camera'];camera.location=(0,-6,.93);camera.rotation_euler=(Vector((0,0,.93))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=2.30;scene.camera=camera
for name,power in [('Explorer key',340),('Explorer fill',240),('Explorer rim',400)]:
 if name in bpy.data.objects:bpy.data.objects[name].data.energy=power
scene.render.filepath='C:/Users/codex/.codex/worktrees/52af/ByteEngine/artifacts/explorer-reference-v2-front.png';bpy.ops.render.render(write_still=True)
result={'vertices':len(body.data.vertices),'polygons':len(body.data.polygons),'bones_unchanged':len(rig.data.bones),'render':scene.render.filepath}
