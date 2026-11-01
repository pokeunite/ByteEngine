"""New T-pose geometry; downloaded meshes are read-only surface references.
All body, branch, palm, finger, helmet and equipment vertices are authored here.
"""
import bpy,bmesh,math,pathlib,json
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform
TAU=math.tau
out=pathlib.Path('C:/Users/codex/Documents/Jumpsuit Reconstruction');out.mkdir(parents=True,exist_ok=True)
scene=bpy.context.scene
refs={}
for o in scene.objects:
 if o.type=='MESH':
  if o.name.startswith('Low Poly Jumpsuit'):refs['suit']=o
  elif o.name.startswith('Low Poly Headset'):refs['helmet']=o
  elif o.name.startswith('Boots Low Poly'):refs['boots']=o
if len(refs)!=3:raise RuntimeError('Import the original reference GLB first')
for o in scene.objects:
 if o.type=='MESH':o.hide_render=True;o.hide_set(True)
collection=bpy.data.collections.new('RECONSTRUCTED - new T pose geometry');scene.collection.children.link(collection)
def newmat(name,color,rough=.6,metal=0):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=rough;p.inputs['Metallic'].default_value=metal;return m
leather=newmat('Rebuilt / charcoal glove leather',(.025,.031,.035),.58)
brown=newmat('Rebuilt / brown glove back',(.20,.135,.078),.6)
ivory=newmat('Rebuilt / pale protective polymer',(.62,.65,.62),.48)
dark=newmat('Rebuilt / rubber and seam binding',(.012,.016,.018),.83)
yellow=newmat('Rebuilt / industrial yellow stitching',(.63,.40,.055),.65)
red=newmat('Rebuilt / red sole accent',(.35,.025,.018),.58)
steel=newmat('Rebuilt / dark fasteners',(.10,.12,.13),.43,.55)
# Copy the original surface materials, not its geometry.
materials={k:o.data.materials[0].copy() for k,o in refs.items()}
for k,m in materials.items():m.name='Rebuilt / original '+k+' surface artwork'
maps={}
scale=1.8/(4.640323638916016-.02583656646311283)
for kind,o in refs.items():
 o.data.calc_loop_triangles();verts=[]
 for v in o.data.vertices:
  p=o.matrix_world@v.co;verts.append(Vector((p.x*scale,p.y*scale,(p.z-.02583656646311283)*scale)))
 tris=[tuple(t.vertices) for t in o.data.loop_triangles];uv=o.data.uv_layers.active
 maps[kind]=(BVHTree.FromPolygons(verts,tris,all_triangles=True),verts,tris,[[Vector((*uv.data[li].uv,0)) for li in t.loops] for t in o.data.loop_triangles])
def project_uv(obj,kind,tags=None):
 tree,verts,tris,uv=maps[kind];layer=obj.data.uv_layers.new(name='Reference surface mapping')
 for poly in obj.data.polygons:
  tag=tags[poly.index] if tags else 0
  def query(p):
   p=p.copy()
   if tag:
    sign=tag;pivot=Vector((sign*.188,0,1.4275));q=Vector((sign,0,0)).rotation_difference(Vector((sign*.16,0,-1)).normalized());p=pivot+q@(p-pivot);p.z+=.035
   return p
  center=query(sum((obj.data.vertices[i].co for i in poly.vertices),Vector())/len(poly.vertices));hit,normal,index,distance=tree.find_nearest(center)
  a,b,c=[verts[i] for i in tris[index]];ua,ub,uc=uv[index]
  for li in poly.loop_indices:
   p=query(obj.data.vertices[obj.data.loops[li].vertex_index].co);mapped=barycentric_transform(p,a,b,c,ua,ub,uc);layer.data[li].uv=(mapped.x,mapped.y)
class Builder:
 def __init__(self):self.v=[];self.f=[];self.mat=[];self.tags=[]
 def vertex(self,p):self.v.append(tuple(p));return len(self.v)-1
 def face(self,indices,material=0,tag=0):self.f.append(tuple(indices));self.mat.append(material);self.tags.append(tag)
 def bridge(self,a,b,material=0,tag=0):
  assert len(a)==len(b)
  for i in range(len(a)):self.face((a[i],a[(i+1)%len(a)],b[(i+1)%len(b)],b[i]),material,tag)
 def object(self,name,mats):
  mesh=bpy.data.meshes.new(name+' topology');mesh.from_pydata(self.v,[],self.f);mesh.update();o=bpy.data.objects.new(name,mesh);collection.objects.link(o)
  for m in mats:mesh.materials.append(m)
  for p,mi in zip(mesh.polygons,self.mat):p.material_index=mi;p.use_smooth=True
  return o
B=Builder();N=32
levels=[(.86,.16,.103),(.90,.166,.107),(.94,.16,.103),(.98,.15,.097),(1.02,.143,.094),(1.06,.145,.095),(1.10,.154,.102),(1.14,.168,.110),(1.18,.181,.115),(1.22,.190,.118),(1.26,.200,.118),(1.30,.211,.117),(1.34,.222,.114),(1.375,.230,.109),(1.41,.235,.105),(1.445,.235,.101),(1.48,.217,.096),(1.515,.172,.084),(1.54,.063,.054),(1.57,.054,.048)]
torso=[]
for r,(z,rx,ry) in enumerate(levels):
 ring=[]
 for j in range(N):
  a=TAU*j/N;fold=.0018*math.sin(j*2.3+r*1.7)*math.sin(math.pi*r/(len(levels)-1));ring.append(B.vertex(((rx+fold)*math.cos(a),(ry+fold)*math.sin(a),z+.0015*math.sin(a*3+r))))
 torso.append(ring)
for r in range(len(torso)-1):
 for j in range(N):
  hole=12<=r<17 and (j<4 or j>=28 or 12<=j<20)
  if not hole:B.face((torso[r][j],torso[r][(j+1)%N],torso[r+1][(j+1)%N],torso[r+1][j]))
B.face(tuple(reversed(torso[-1])))
arm_ends={}
for sign,center in [(1,0),(-1,16)]:
 boundary=[torso[12][(center+j)%N] for j in range(-4,5)]+[torso[r][(center+4)%N] for r in range(13,18)]+[torso[17][(center+j)%N] for j in range(3,-5,-1)]+[torso[r][(center-4)%N] for r in range(16,12,-1)]
 boundary.sort(key=lambda i:math.atan2(B.v[i][2]-1.4275,B.v[i][1]))
 angles=[math.atan2(B.v[i][2]-1.4275,B.v[i][1]) for i in boundary];previous=boundary
 for k,(x,ry,rz) in enumerate([(.255,.079,.077),(.28,.080,.076),(.315,.077,.073),(.355,.072,.069),(.395,.068,.064),(.435,.063,.060),(.465,.061,.055),(.49,.057,.053),(.52,.059,.051),(.55,.057,.047),(.58,.052,.043),(.615,.047,.039),(.645,.044,.036),(.67,.042,.034),(.695,.038,.032),(.715,.034,.029),(.728,.033,.027)]):
  ring=[]
  for j,a in enumerate(angles):
   fold=(.002 if 2<k<15 else .0005)*math.sin(k*2.3+j*1.7);ring.append(B.vertex((sign*x,(ry+fold)*math.cos(a),1.4275+(rz+fold)*math.sin(a))))
  B.bridge(previous,ring,0,sign);previous=ring
 arm_ends[sign]=(previous,angles)
# Pelvis splits through a shared crotch seam into two quad leg tubes.
mid=[B.vertex((0,y,.805-.012*math.cos(y*20))) for y in [.055,0,-.055]]
for sign in [1,-1]:
 half=[torso[0][j%32] for j in (list(range(24,41)) if sign==1 else list(range(8,25)))]
 boundary=half+(mid if sign==1 else list(reversed(mid)))
 boundary.sort(key=lambda i:math.atan2(B.v[i][1],B.v[i][0]-sign*.092))
 angles=[math.atan2(B.v[i][1],B.v[i][0]-sign*.092) for i in boundary];previous=boundary
 profile=[(.79,.092,.094),(.755,.091,.094),(.715,.088,.093),(.67,.086,.092),(.625,.083,.090),(.58,.077,.082),(.55,.076,.078),(.515,.074,.074),(.49,.067,.068),(.465,.063,.066),(.44,.065,.068),(.405,.070,.072),(.37,.069,.073),(.335,.065,.071),(.30,.064,.069),(.265,.060,.066),(.23,.058,.063),(.195,.056,.061),(.16,.054,.058),(.135,.055,.056),(.12,.056,.057)]
 for k,(z,rx,ry) in enumerate(profile):
  center=sign*(.092+.016*(1-z/.8));ring=[]
  for j,a in enumerate(angles):
   fold=(.0032 if k>2 else .0015)*math.sin(k*2.2+j*1.1);ring.append(B.vertex((center+(rx+fold)*math.cos(a),(ry+fold)*math.sin(a),z+.002*math.sin(j*2+k))))
  B.bridge(previous,ring);previous=ring
 # Connected boot ankle and outsole: broad toe, compact heel.
 for k,(z,rx,front,back) in enumerate([(.105,.058,.067,.052),(.087,.063,.092,.057),(.067,.070,.151,.060),(.045,.075,.170,.063),(.025,.077,.174,.066),(.012,.075,.171,.064)]):
  ring=[B.vertex((sign*.108+rx*math.cos(a),(front if math.sin(a)<0 else back)*math.sin(a),z)) for a in angles];B.bridge(previous,ring,1);previous=ring
 B.face(tuple(previous),1)
# Palms and all five fingers are new connected quad branches.
for sign,(wrist,angles) in arm_ends.items():
 rows_top=[];rows_bottom=[];perimeter=[]
 for j in range(13):
  y=-.033+.066*j/12;z=.027*math.sqrt(max(.03,1-(y/.0335)**2));perimeter.append((y,1.4275+z,'t',j))
 for j in reversed(range(13)):
  y=-.033+.066*j/12;z=.027*math.sqrt(max(.03,1-(y/.0335)**2));perimeter.append((y,1.4275-z,'b',j))
 ordered=sorted(perimeter,key=lambda p:math.atan2(p[1]-1.4275,p[0]));lookup={}
 for idx,p in zip(wrist,ordered):B.v[idx]=(sign*.728,p[0],p[1]);lookup[(p[2],p[3])]=idx
 for k,(x,halfwidth,thickness) in enumerate([(.728,.033,.027),(.754,.041,.025),(.78,.047,.023),(.808,.051,.022),(.834,.051,.020)]):
  top=[];bottom=[]
  for j in range(13):
   y=-halfwidth+2*halfwidth*j/12;curve=.92+.08*math.cos((j/12-.5)*math.pi)
   top.append(lookup[('t',j)] if k==0 else B.vertex((sign*x,y,1.4275+thickness*curve)))
   bottom.append(lookup[('b',j)] if k==0 else B.vertex((sign*x,y,1.4275-thickness*curve)))
  rows_top.append(top);rows_bottom.append(bottom)
 for k in range(4):
  for j in range(12):
   B.face((rows_top[k][j],rows_top[k+1][j],rows_top[k+1][j+1],rows_top[k][j+1]),2)
   B.face((rows_bottom[k][j+1],rows_bottom[k+1][j+1],rows_bottom[k+1][j],rows_bottom[k][j]),2)
  B.face((rows_top[k][12],rows_top[k+1][12],rows_bottom[k+1][12],rows_bottom[k][12]),2)
  if k==0:B.face((rows_bottom[k][0],rows_bottom[k+1][0],rows_top[k+1][0],rows_top[k][0]),2)
 # Four fingers open from adjacent palm grid segments; eight vertices per root.
 for finger,length in enumerate([.102,.117,.109,.085]):
  j=finger*3;base=rows_top[-1][j:j+4]+list(reversed(rows_bottom[-1][j:j+4]));yc=-.051+(finger+.5)*.102/4
  a=[math.atan2(B.v[i][2]-1.4275,B.v[i][1]-yc) for i in base];previous=base
  for step,t in enumerate([.12,.36,.48,.63,.82,.94]):
   radius=(.0108 if finger<3 else .0094)*(1-.25*t);ring=[B.vertex((sign*(.834+length*t),yc+(finger-1.5)*.006*t+radius*math.cos(q),1.4275-.004*t+radius*.82*math.sin(q))) for q in a];B.bridge(previous,ring,2);previous=ring
  tip=B.vertex((sign*(.834+length),yc+(finger-1.5)*.006,1.4235))
  for j in range(8):B.face((previous[j],previous[(j+1)%8],tip),2)
 # Thumb branches out of the palm side through the three open side quads.
  # The skin between finger roots is shared, leaving deliberate finger gaps.
 base=[rows_top[k][0] for k in range(1,5)]+[rows_bottom[k][0] for k in range(4,0,-1)]
 center=sum((Vector(B.v[i]) for i in base),Vector())/8;axis=Vector((sign*.58,-.82,-.08)).normalized();u=Vector((0,0,1));v=axis.cross(u).normalized();u=v.cross(axis).normalized();previous=base
 a=[math.atan2((Vector(B.v[i])-center).dot(v),(Vector(B.v[i])-center).dot(u)) for i in base]
 for t,radius in [(.025,.019),(.045,.017),(.068,.014),(.086,.011)]:
  c=center+axis*t;ring=[B.vertex(c+radius*(u*math.cos(q)+v*math.sin(q))) for q in a];B.bridge(previous,ring,2);previous=ring
 tip=B.vertex(center+axis*.095)
 for j in range(8):B.face((previous[j],previous[(j+1)%8],tip),2)
body=B.object('Reconstructed jumpsuit - connected T pose',[materials['suit'],materials['boots'],leather])
# Only cloth/boot faces use the original artwork; glove UVs use a neutral field.
project_uv(body,'suit',B.tags)
# Boot faces need their own reference material/UV projection.
tree,verts,tris,uv=maps['boots'];layer=body.data.uv_layers.active
for p in body.data.polygons:
 if p.material_index!=1:continue
 center=sum((body.data.vertices[i].co for i in p.vertices),Vector())/len(p.vertices);_,_,idx,_=tree.find_nearest(center);a,b,c=[verts[i] for i in tris[idx]];ua,ub,uc=uv[idx]
 for li in p.loop_indices:
  co=body.data.vertices[body.data.loops[li].vertex_index].co;q=barycentric_transform(co,a,b,c,ua,ub,uc);layer.data[li].uv=q.xy
bm=bmesh.new();bm.from_mesh(body.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(body.data);bm.free()
def meshobj(name,verts,faces,material):
 data=bpy.data.meshes.new(name+' new geometry');data.from_pydata(verts,[],faces);data.materials.append(material);data.update();o=bpy.data.objects.new(name,data);collection.objects.link(o);return o
def box(name,location,dimensions,material,bevel=.004):
 x,y,z=dimensions;verts=[(sx*x/2,sy*y/2,sz*z/2) for sx in [-1,1] for sy in [-1,1] for sz in [-1,1]];faces=[(0,4,6,2),(1,3,7,5),(0,1,5,4),(2,6,7,3),(0,2,3,1),(4,5,7,6)];o=meshobj(name,verts,faces,material);o.location=location
 if bevel:m=o.modifiers.new('Rounded protective edge','BEVEL');m.width=bevel;m.segments=2;bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.modifier_apply(modifier=m.name);o.select_set(False)
 for p in o.data.polygons:p.use_smooth=False
 return o
def tube(name,points,radius,material,sides=8):
 pts=[Vector(p) for p in points];verts=[]
 for k,p in enumerate(pts):
  axis=(pts[min(k+1,len(pts)-1)]-pts[max(k-1,0)]).normalized();u=axis.cross(Vector((0,0,1)))
  if u.length<.01:u=axis.cross(Vector((0,1,0)))
  u.normalize();v=axis.cross(u).normalized();verts.extend(p+radius*(u*math.cos(TAU*j/sides)+v*math.sin(TAU*j/sides)) for j in range(sides))
 faces=[(k*sides+j,k*sides+(j+1)%sides,(k+1)*sides+(j+1)%sides,(k+1)*sides+j) for k in range(len(pts)-1) for j in range(sides)]
 return meshobj(name,verts,faces,material)
# Helmet is a newly authored continuous oval shell, not a sphere primitive.
H=Builder();rings=[]
for z,rx,ry,cy in [(1.555,.057,.065,.005),(1.575,.071,.079,0),(1.60,.083,.094,-.006),(1.63,.094,.108,-.010),(1.67,.101,.115,-.011),(1.71,.102,.112,-.008),(1.745,.091,.098,-.002),(1.775,.069,.073,.002),(1.791,.041,.044,.004)]:
 ring=[H.vertex((rx*math.cos(TAU*j/32),cy+ry*math.sin(TAU*j/32),z)) for j in range(32)]
 if rings:H.bridge(rings[-1],ring)
 rings.append(ring)
bottom=H.vertex((0,.005,1.55));top=H.vertex((0,.004,1.80))
for j in range(32):H.face((bottom,rings[0][(j+1)%32],rings[0][j]));H.face((rings[-1][j],rings[-1][(j+1)%32],top))
helmet=H.object('Reconstructed teal helmet shell',[materials['helmet']]);project_uv(helmet,'helmet')
# Deliberate protective shapes and garment construction detail.
for sign in [-1,1]:
 hip=box(('Left' if sign<0 else 'Right')+' long thigh side panel',(sign*.187,.005,.72),(.033,.105,.235),ivory,.012);hip.rotation_euler[1]=sign*-.08
 box('Thigh panel inset',(sign*.207,-.005,.72),(.006,.074,.176),ivory,.005)
 box('Thigh mounting tab',(sign*.17,.009,.852),(.053,.086,.027),dark,.004)
 knee=box('Rounded knee guard',(sign*.106,-.072,.485),(.108,.040,.093),ivory,.018)
 box('Knee guard strap',(sign*.106,.018,.493),(.123,.13,.022),dark,.004)
 elbow=box('Elbow protective cap',(sign*.463,.050,1.4275),(.080,.026,.066),ivory,.010)
 box('Elbow retention strap',(sign*.463,.008,1.4275),(.025,.123,.11),dark,.003)
 # Leather glove back and stitched outline; palms face downward in T-pose.
 glove=box('Brown leather back-of-hand patch',(sign*.786,0,1.453),(.075,.069,.004),brown,.007)
 for y in [-.027,.027]:tube('Glove seam stitching',[(sign*x,y,1.456) for x in [.754,.77,.79,.816]],.0007,yellow,5)
 for f in range(4):box('Knuckle reinforcement',(sign*.834,-.039+f*.026,1.448),(.017,.019,.005),brown,.003)
 # Tactical boots have a sole, toe bumper, vamp and laces.
 box('Boot heel block',(sign*.108,.038,.018),(.12,.058,.023),dark,.005)
 for y in [-.135,-.112,-.089,-.066]:
  tube('Boot crossed lace',[(sign*.108-.04,y, .075),(sign*.108+.04,y+.015,.077)],.0015,dark,6)
 tube('Toe bumper seam',[(sign*.108+x,-.153+.022*abs(x)/.055,.046) for x in [-.055,-.03,0,.03,.055]],.0025,dark)
 box('Boot red side trim',(sign*.176,-.035,.052),(.004,.057,.014),red,.002)
 for x in [-.049,-.025,0,.025,.049]:box('Boot outsole tread',(sign*.108+x,-.04,.009),(.009,.19,.008),dark,.001)
 # Double stitched inseams remain geometry rather than painted-on gaps.
 tube('Trouser side seam',[(sign*(.17-.025*(.8-z)),.018,z) for z in [.80,.74,.68,.62,.56]],.0012,dark,6)
box('Helmet rear service plate',(0,.102,1.687),(.043,.014,.038),ivory,.007)
box('Helmet rear vent',(0,.111,1.687),(.025,.003,.019),dark,.003)
for x in [-.057,.057]:box('Upper back protective tab',(x,.108,1.40),(.029,.014,.037),ivory,.005)
# Collar and zipper construction; the suit artwork carries the original labels.
tube('Raised collar seam',[(.055*math.cos(TAU*j/32),.050*math.sin(TAU*j/32),1.56) for j in range(33)],.003,dark)
tube('Front closure seam',[(0,-ry-.001,z) for z,rx,ry in levels[2:18]],.0014,dark,6)
box('Zipper pull',(0,-.097,1.505),(.006,.003,.016),steel,.001)
# Scene layout and visual review.
for o in scene.objects:
 if o.type=='LIGHT':o.hide_render=True
for name,pos,power,size in [('Rebuild key',(3,-4,5),450,4),('Rebuild fill',(-3,-2,3),220,3),('Rebuild rim',(1,3,4),600,3)]:
 l=bpy.data.lights.new(name,'AREA');l.energy=power;l.size=size;o=bpy.data.objects.new(name,l);scene.collection.objects.link(o);o.location=pos;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
cam=scene.camera;cam.location=(0,-5,1);cam.rotation_euler=(Vector((0,0,.9))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=2.13
scene.render.resolution_x=1500;scene.render.resolution_y=1500;scene.render.resolution_percentage=100;scene.render.filepath=str(out/'Reconstruction-front.png')
bpy.ops.object.select_all(action='DESELECT');body.select_set(True);bpy.context.view_layer.objects.active=body
report={'new_body_vertices':len(body.data.vertices),'new_body_faces':len(body.data.polygons),'new_helmet_vertices':len(helmet.data.vertices),'new_objects':len(collection.objects),'reference_geometry_reused':False,'surface_artwork_reused':True,'pose':'T pose; open five-finger hands; no skeleton or animation'}
bm=bmesh.new();bm.from_mesh(body.data);report['body_boundary_edges']=sum(e.is_boundary for e in bm.edges);report['body_nonmanifold_edges']=sum(not e.is_manifold for e in bm.edges);bm.free();(out/'construction-report.json').write_text(json.dumps(report,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(out/'Jumpsuit-Reconstructed.blend'));bpy.ops.render.render(write_still=True)
print(json.dumps(report))
