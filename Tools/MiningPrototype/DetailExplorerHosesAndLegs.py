"""Repair the two reference mismatches without rebuilding or changing the rig."""
import bpy,bmesh,math,pathlib
from mathutils import Vector,Matrix
rig=bpy.data.objects['PolygonSyntyCharacter'];body=bpy.data.objects['HazardPay Explorer - skinned mesh'];collection=body.users_collection[0]
backup=body.copy();backup.data=body.data.copy();backup.name='Explorer before hose and strap repair';backup_collection=bpy.data.collections.get('Explorer repair backups') or bpy.data.collections.new('Explorer repair backups')
if backup_collection.name not in bpy.context.scene.collection.children:bpy.context.scene.collection.children.link(backup_collection)
backup_collection.objects.link(backup);backup.hide_render=True;backup.hide_set(True)
bm=bmesh.new();bm.from_mesh(body.data);seen=set();remove=[];components=0
for vertex in bm.verts:
 if vertex in seen:continue
 stack=[vertex];seen.add(vertex);group=[]
 while stack:
  v=stack.pop();group.append(v)
  for edge in v.link_edges:
   other=edge.other_vert(v)
   if other not in seen:seen.add(other);stack.append(other)
 center=sum((v.co for v in group),Vector())/len(group);mats={body.data.materials[f.material_index].name for v in group for f in v.link_faces};dark=any(s.endswith(('Graphite','Rubber','Steel')) for s in mats)
 old_hose=dark and 5<abs(center.x)<19 and 109<center.y<121 and center.z>9
 old_strap=dark and 65.5<center.y<68.5 and abs(center.x)>3
 if old_hose or old_strap:remove.extend(group);components+=1
 # Fabric folds follow the sleeve axis; avoid rigid plates and pressure seals.
 if any(s.endswith('Ivory light') for s in mats) and 25<abs(center.x)<78 and center.y>125:
  for v in group:
   t=abs(v.co.x);offset=Vector((0,v.co.y-137,v.co.z+3.5));length=max(.1,offset.length);wave=.55*math.sin(t*1.75)+.24*math.sin(t*3.3+v.co.z)
   v.co+=offset/length*wave
bmesh.ops.delete(bm,geom=list(set(remove)),context='VERTS');bm.to_mesh(body.data);bm.free()
parts=[];materials={m.name.split('Explorer / ',1)[1]:m for m in bpy.data.materials if m.name.startswith('Explorer / ')}
source=pathlib.Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine/Tools/MiningPrototype/BuildExplorerCharacter.py').read_text();exec(source[source.index('def mesh('):source.index('# Connected torso,')])
def curved_hose(side):
 a=Vector((side*7.2,120,17.3));b=Vector((side*10,116.8,19.1));c=Vector((side*15.8,113.2,12));d=Vector((side*16.2,112.7,4.5))
 verts=[];faces=[];rings=145;sides=12;u=None
 for i in range(rings):
  t=i/(rings-1);q=(1-t)**3*a+3*(1-t)**2*t*b+3*(1-t)*t*t*c+t**3*d
  tangent=(3*(1-t)**2*(b-a)+6*(1-t)*t*(c-b)+3*t*t*(d-c)).normalized()
  if u is None:u=tangent.cross(Vector((0,1,0))).normalized()
  else:u=(u-tangent*u.dot(tangent)).normalized()
  v=tangent.cross(u).normalized();r=1.40+.17*(.5+.5*math.cos(t*math.tau*36))
  for j in range(sides):verts.append(tuple(q+r*(u*math.cos(j*math.tau/sides)+v*math.sin(j*math.tau/sides))))
 for i in range(rings-1):
  for j in range(sides):faces.append((i*sides+j,i*sides+(j+1)%sides,(i+1)*sides+(j+1)%sides,(i+1)*sides+j))
 faces.extend([tuple(reversed(range(sides))),tuple((rings-1)*sides+j for j in range(sides))]);mesh(('Left' if side>0 else 'Right')+' continuous corrugated air hose',verts,faces,'Graphite','Spine_02')
 for label,point,axis in [('chest',a,(a-b).normalized()),('side',d,(d-c).normalized())]:
  tube(label+' pressure hose mounting boot '+str(side),[point-axis*1.5,point+axis*.3],2.15,'Rubber','Spine_02',12)
  tube(label+' hex locking collar '+str(side),[point-axis*.7,point+axis*.2],1.95,'Steel','Spine_02',8)
  tube(label+' hose ferrule '+str(side),[point+axis*.2,point+axis*.7],1.65,'Graphite','Spine_02',12)
def band(side):
 n=32;verts=[]
 for outer,yoffset in [(0,-1.7),(0,1.7),(.45,-1.7),(.45,1.7)]:
  for j in range(n):
   angle=j*math.tau/n;x=side*11+(8.55+outer)*math.cos(angle);z=-2+(8.15+outer)*math.sin(angle);y=66.4+yoffset+side*(x-side*11)*.15;verts.append((x,y,z))
 faces=[]
 for j in range(n):
  k=(j+1)%n;faces.extend([(j,k,n+k,n+j),(2*n+j,3*n+j,3*n+k,2*n+k),(j,2*n+j,2*n+k,k),(n+j,n+k,3*n+k,3*n+j)])
 bone='UpperLeg_'+('L' if side>0 else 'R');mesh('Independent diagonal thigh strap '+str(side),verts,faces,'Graphite',bone)
 x=side*15.0;y=67.1;z=5.8
 # Four metal rails leave an actual rectangular hole, instead of a solid grey slab.
 for xx in [-1.9,1.9]:box('Buckle vertical rail '+str(side)+str(xx),(x+xx,y,z),(1,5,1),'Steel',bone,.22)
 for yy in [-2.1,2.1]:box('Buckle horizontal rail '+str(side)+str(yy),(x,y+yy,z),(4.8,.85,1),'Steel',bone,.2)
 box('Buckle centre tongue '+str(side),(x,y,z+.3),(.7,3.5,.7),'Steel',bone,.15)
 # A front-facing hard pouch, release latch and hinge make the outer thigh read correctly.
 box('Thigh yellow utility pouch '+str(side),(side*20,72,7.2),(7.8,12,3.6),'Industrial yellow',bone,.9)
 box('Thigh utility flap '+str(side),(side*20,76.7,9.3),(7,3,1.1),'Yellow edge',bone,.4)
 box('Thigh pouch recessed latch '+str(side),(side*20,71,9.2),(2,4,.9),'Graphite',bone,.25)
 box('Thigh pouch metal catch '+str(side),(side*20,71.3,9.8),(1.3,1.6,.3),'Steel',bone,.15)
for side in [-1,1]:curved_hose(side);band(side)
# A modest tailored fly/gusset and seam below the belt, not a box bridging the thighs.
outline=[(-4.3,91),(4.3,91),(5.1,85),(3.2,79),(-3.2,79),(-5.1,85)];n=len(outline);verts=[(x,y,z) for z in [9.0,10.5] for x,y in outline]
mesh('Tailored trouser fly panel',verts,[tuple(reversed(range(n))),tuple(range(n,n*2))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],'Industrial yellow','Hips')
for side in [-1,1]:
 tube('Trouser fly seam '+str(side),[(side*4.3,90.5,10.6),(side*4.7,85,10.6),(side*3,79,10.6)],.22,'Yellow shadow','Hips',6)
 # Belt box lids and fasteners echo the source reference's small functional details.
 box('Belt pouch flap '+str(side),(side*17.8,100.5,10.4),(6.5,3,1.4),'Industrial yellow','Hips',.5)
 for y in [93,100]:box('Belt pouch rivet '+str(side)+str(y),(side*20.1,y,10.3),(.8,.8,.3),'Steel','Hips',.15)
bpy.ops.object.select_all(action='DESELECT');body.select_set(True)
for part in parts:part.select_set(True)
bpy.context.view_layer.objects.active=body;bpy.ops.object.join();body['reference_rebuild']=3
rig.data.pose_position='REST';scene=bpy.context.scene;camera=bpy.data.objects['Explorer preview camera'];camera.location=(2.2,-6,1.5);camera.rotation_euler=(Vector((0,0,.98))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=2.16
scene.render.filepath='C:/Users/codex/.codex/worktrees/52af/ByteEngine/artifacts/explorer-reference-v3-detail.png';bpy.ops.render.render(write_still=True)
result={'removed_components':components,'vertices':len(body.data.vertices),'bones_preserved':len(rig.data.bones),'render':scene.render.filepath}
