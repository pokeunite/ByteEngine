"""Editable eight-legged reference spiders. Execute inside the user's Blender MCP."""
import bpy,bmesh,math,json,random
from pathlib import Path
from mathutils import Vector
WORK=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine');OUT=WORK/'artifacts/spider-models';OUT.mkdir(parents=True,exist_ok=True)
S=bpy.data.scenes.get('Spider Modelling Workshop') or bpy.data.scenes.new('Spider Modelling Workshop');bpy.context.window.scene=S
S.render.engine='BLENDER_EEVEE';S.render.resolution_x=1200;S.render.resolution_y=1200;S.render.resolution_percentage=100;S.view_settings.view_transform='AgX';S.view_settings.look='AgX - Medium High Contrast';S.view_settings.exposure=-.65;S.world=S.world or bpy.data.worlds.new('Spider reference studio');S.world.use_nodes=True;S.world.node_tree.nodes['Background'].inputs[0].default_value=(.22,.22,.23,1);S.world.node_tree.nodes['Background'].inputs[1].default_value=.4
def mat(name,color,rough=.65,emission=0):
 m=bpy.data.materials.get(name) or bpy.data.materials.new(name);m.use_nodes=True;m.diffuse_color=(*color,1);p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=rough
 if emission:p.inputs['Emission Color'].default_value=(*color,1);p.inputs['Emission Strength'].default_value=emission
 return m
M=[mat('Spider deep chitin',(.018,.014,.027)),mat('Spider slate violet',(.043,.032,.060)),mat('Spider pale facet',(.067,.050,.084)),mat('Spider joint membrane',(.085,.024,.018)),mat('Hunter red-orange marking',(.45,.055,.025)),mat('Spider black recess',(.009,.008,.014)),mat('Spider amber eyes',(.70,.18,.01),.22,.2),mat('Drill breaker ochre shell',(.36,.16,.04)),mat('Drill breaker gold facet',(.48,.24,.065)),mat('Spider horn edge',(.19,.16,.20))]
M.extend([mat('Spider middle violet facet',(.052,.039,.072)),mat('Breaker shaded gold facet',(.40,.18,.05)),mat('Hunter shaded red',(.36,.04,.018))])
C=None;ROOT=None;PARTS=[];KIND='';HEAVY=False;JOINTS=[];BODYRINGS=[]
def mesh(name,vertices,faces,material=1,indices=None,tag=None):
 me=bpy.data.meshes.new(name+' topology');me.from_pydata(vertices,[],faces);me.update();o=bpy.data.objects.new(name,me);C.objects.link(o);o.parent=ROOT
 for m in M:me.materials.append(m)
 for i,p in enumerate(me.polygons):p.use_smooth=False;p.material_index=indices[i] if indices else material
 bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free();me.update()
 if tag:o['anatomy']=tag
 PARTS.append(o);return o
def ellipsoid(name,p,size,material=1,sub=1):
 bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=sub,radius=1,location=p);o=bpy.context.object;o.name=name;o.scale=size;bpy.context.view_layer.objects.active=o;bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
 for col in list(o.users_collection):col.objects.unlink(o)
 C.objects.link(o);o.parent=ROOT;o.data.materials.append(M[material]);PARTS.append(o);return o
def plate(name,points,height=.04,material=1,normal=None):
 # Flush pigment/scutes are represented by body faces, avoiding coplanar overlapping shells.
 if any(key in name for key in ['facial shell','abdomen scute','dorsal chevron','side warning']):return None
 points=[Vector(p) for p in points]
 fitted=any(key in name for key in ['facial shell','abdomen scute','dorsal chevron','side warning','mineral plate','mineral ridge'])
 if fitted:
  for p in points:p.z=body_surface(p.x,p.y)+.045
 centre=sum(points,Vector())/len(points)
 if fitted:centre.z=body_surface(centre.x,centre.y)+.045
 centre+=(Vector(normal) if normal is not None else Vector((0,0,1)))*height;n=len(points);v=[tuple(p) for p in points]+[tuple(centre)];faces=[(i,(i+1)%n,n) for i in range(n)];faces.append(tuple(range(n-1,-1,-1)));return mesh(name,v,faces,material)
def body_surface(x,y):
 for a,b in zip(BODYRINGS,BODYRINGS[1:]):
  if a[0]<=y<=b[0]:
   t=(y-a[0])/(b[0]-a[0]);w=a[1]*(1-t)+b[1]*t;h=a[2]*(1-t)+b[2]*t;z=a[3]*(1-t)+b[3]*t;return z+h*math.sqrt(max(0,1-(x/max(.001,w))**2))
 return BODYRINGS[0][3] if y<BODYRINGS[0][0] else BODYRINGS[-1][3]
def facets(o,seed,amount=.008,subdivide=False):
 bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.triangulate(bm,faces=list(bm.faces))
 if subdivide:bmesh.ops.subdivide_edges(bm,edges=list(bm.edges),cuts=1,use_grid_fill=True);bmesh.ops.triangulate(bm,faces=list(bm.faces))
 bm.normal_update();rng=random.Random(seed)
 for v in bm.verts:v.co+=v.normal*rng.uniform(-amount,amount)
 for f in bm.faces:
  if f.material_index in (1,2) and rng.random()<.28:f.material_index=10
  elif f.material_index in (7,8):
   r=rng.random()
   if r<.17:f.material_index=1
   elif r<.44:f.material_index=11
  elif f.material_index==4 and rng.random()<.25:f.material_index=12
 bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free();o.data.update()
def body():
 global BODYRINGS
 # One closed continuous body: facial wedge, cephalothorax, narrow pedicel, faceted abdomen.
 rings=[(-.93,.035,.055,.57),(-.86,.20,.15,.67),(-.66,.37,.29,.76),(-.36,.48,.34,.80),(-.04,.47,.31,.83),(.23,.34,.24,.83),(.37,.19,.17,.83),(.47,.29,.25,.94),(.66,.50,.43,1.04),(.98,.64,.50,1.10),(1.26,.56,.44,1.10),(1.52,.34,.27,1.09),(1.72,.025,.04,1.04)]
 if HEAVY:rings=[(y,w*1.14,h*1.08,z-.03) for y,w,h,z in rings]
 BODYRINGS=rings
 v=[];n=32
 for j,(y,w,h,z) in enumerate(rings):
  for i in range(n):
   a=math.tau*i/n;f=1+(.025*math.sin(i*2.2+j*1.7));v.append((math.cos(a)*w*f,y,z+math.sin(a)*h*f))
 faces=[tuple(range(n-1,-1,-1))];inds=[0]
 for j in range(len(rings)-1):
  for i in range(n):
   a=j*n+i;b=j*n+(i+1)%n;c=b+n;d=a+n
   colour=0 if i>=n//2 else 1+((i+j)%4==0)
   if HEAVY and 1<=i<=n//2-2 and ((j>=7 and (j%3!=1 or i in [2,3,8,9])) or (j<6 and j%2==0 and 3<=i<=8)):colour=7+((i+j)%4==0)
   if not HEAVY and j>=7 and i in [n//4-1,n//4]:colour=4
   if not HEAVY and j in [8,10] and i in [2,9]:colour=4
   if (i+j)%3==0:faces.extend([(a,b,c),(a,c,d)]);inds.extend([colour,colour])
   else:faces.append((a,b,c,d));inds.append(colour)
 faces.append(tuple(range((len(rings)-1)*n,len(rings)*n)));inds.append(1);o=mesh(KIND+' continuous body',v,faces,indices=inds,tag='cephalothorax-pedicel-abdomen');o['closed_body']=True;facets(o,81,.013,True)
 if HEAVY:
  # Mineral plates follow the abdomen instead of covering leg joints or forming extra limbs.
  for side in [-1,1]:
   for j,(x,y,z) in enumerate([(.48,.57,1.06),(.60,.80,1.10),(.63,1.10,1.17),(.49,1.39,1.18),(.34,1.54,1.19)]):
    plate('Breaker side mineral plate '+str(side)+' '+str(j),[(side*(x-.10),y-.12,z),(side*(x+.08),y-.03,z-.03),(side*(x+.02),y+.15,z+.025),(side*(x-.17),y+.12,z+.11)],.14,7 if j%2 else 8)
  for j,(y,z) in enumerate([(.47,1.22),(.75,1.47),(1.04,1.60),(1.34,1.50),(1.62,1.24)]):
   plate('Breaker central mineral ridge '+str(j),[(-.10,y-.12,z),(.09,y-.11,z),(.12,y+.10,z-.03),(-.10,y+.13,z-.03)],.18,8)
def limb(name,anchors,radii,side,index):
 # A single manifold tube passes continuously through all four articulated sections.
 anchors=[Vector(p) for p in anchors];v=[];rings=[];radiiOut=[];n=8
 for segment in range(len(anchors)-1):
  for t in [0,.16,.52,.85]:
   p=anchors[segment].lerp(anchors[segment+1],t);r=radii[segment]*(1-t)+radii[segment+1]*t;r*=1.10 if .2<t<.8 else 1
   tangent=(anchors[segment+1]-anchors[segment]).normalized()
   if t==0 and segment>0:tangent=((anchors[segment]-anchors[segment-1]).normalized()+tangent).normalized()
   u=tangent.cross(Vector((0,0,1))).normalized();w=u.cross(tangent).normalized();rings.append((segment,t,tuple(p)));radiiOut.append(r)
   for i in range(n):
    a=math.tau*i/n;v.append(tuple(p+u*math.cos(a)*r+w*math.sin(a)*r*.83))
 tangent=(anchors[-1]-anchors[-2]).normalized();u=tangent.cross(Vector((0,0,1))).normalized();w=u.cross(tangent).normalized();p=anchors[-1]
 for i in range(n):a=math.tau*i/n;v.append(tuple(p+u*math.cos(a)*radii[-1]+w*math.sin(a)*radii[-1]))
 faces=[tuple(range(n-1,-1,-1))];indices=[0]
 for ring in range(len(rings)):
  segment,t,p=rings[ring]
  for i in range(n):
   faces.append((ring*n+i,ring*n+(i+1)%n,(ring+1)*n+(i+1)%n,(ring+1)*n+i))
   colour=3 if t<.05 and segment>0 else (2 if i in (2,3) else 1)
   if HEAVY and segment in [1,2] and t>=.16 and i in [0,1,2,3]:colour=7+(i==2)
   if not HEAVY and segment>0 and t<.05:colour=4
   indices.append(colour)
 faces.append(tuple(range(len(rings)*n,(len(rings)+1)*n)));indices.append(0)
 obj=mesh(name,v,faces,indices=indices,tag='walking_leg');obj['side']=side;obj['pair']=index+1;obj['joint_positions']=json.dumps([list(a) for a in anchors]);obj['continuous_joint_topology']=True
 facets(obj,500+index,.005,False)
 for segment in range(4):
  g=obj.vertex_groups.new(name=('L' if side<0 else 'R')+str(index+1)+'_'+['Coxa','Femur','Tibia','Tarsus'][segment]);g.add([i for i in range(len(v)) if min(i//(4*n),3)==segment],1,'REPLACE')
 JOINTS.append({'leg':name,'side':side,'pair':index+1,'points':[list(a) for a in anchors]});return obj
def legs():
 # Foot positions fan into four unmistakable pairs. Mirrored front legs are not mouthparts.
 roots=[(.31,-.64,.73),(.43,-.36,.77),(.46,-.08,.78),(.36,.19,.79)]
 knees=[(.88,-1.05,.75),(1.23,-.70,.87),(1.33,.50,.98),(.94,1.23,1.17)]
 ankles=[(1.22,-1.73,.24),(1.89,-1.00,.26),(1.98,.19,.28),(1.43,1.91,.28)]
 feet=[(1.32,-1.94,.024),(2.08,-1.09,.024),(2.17,.10,.024),(1.55,2.15,.024)]
 for side in [-1,1]:
  for i,(root,knee,ankle,foot) in enumerate(zip(roots,knees,ankles,feet)):
   factor=1.07 if HEAVY else 1;root=Vector((side*root[0]*factor,root[1],root[2]));knee=Vector((side*knee[0]*factor,knee[1],knee[2]));ankle=Vector((side*ankle[0]*factor,ankle[1],ankle[2]));foot=Vector((side*foot[0]*factor,foot[1],foot[2]));coxa=root.lerp(knee,.20);coxa.z-=.025
   r=.14 if HEAVY else .105;limb(KIND+(' L' if side<0 else ' R')+str(i+1)+' walking leg',[root,coxa,knee,ankle,foot],[r*.9,r*1.15,r*1.25,r*.50,.012],side,i)
def face():
 # Six inset eyes, compact chelicerae and two short pedipalps, distinct from the eight long legs.
 for side in [-1,1]:
  for i,(x,y,z,r) in enumerate([(.23,-.72,.86,.064),(.16,-.83,.75,.050),(.095,-.88,.67,.038)]):
   ellipsoid('Dark eye socket '+str(side)+' '+str(i),(side*x,y,z),(r*1.40,r*.90,r*1.25),5,2);ellipsoid('Amber eye '+str(side)+' '+str(i),(side*x,y-.026,z+.012),(r,r*.65,r),6,2)
  anchors=[(side*.13,-.82,.59),(side*.20,-.99,.52),(side*.17,-1.05,.33),(side*.065,-1.12,.39)]
  # Separate compact mouthpart, never a ninth walking limb.
  o=mouthtube(KIND+' crushing fang '+str(side),anchors,[.115 if HEAVY else .075,.12 if HEAVY else .065,.068 if HEAVY else .042,.006],7 if HEAVY else 0);o['anatomy']='chelicera'
  mouthtube(KIND+' compact pedipalp '+str(side),[(side*.26,-.80,.58),(side*.31,-.94,.47),(side*.28,-1.01,.39)],[.055,.038,.008],1)['anatomy']='pedipalp'
def mouthtube(name,points,radii,material):
 points=[Vector(p) for p in points];v=[];n=8
 for j,p in enumerate(points):
  tangent=(points[min(j+1,len(points)-1)]-points[max(0,j-1)]).normalized();u=tangent.cross(Vector((0,0,1))).normalized();w=u.cross(tangent).normalized()
  for i in range(n):a=math.tau*i/n;v.append(tuple(p+(u*math.cos(a)+w*math.sin(a))*radii[j]))
 faces=[tuple(range(n-1,-1,-1))]+[(j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i) for j in range(len(points)-1) for i in range(n)]+[tuple(range((len(points)-1)*n,len(points)*n))];return mesh(name,v,faces,material)
def build(kind):
 global C,ROOT,PARTS,KIND,HEAVY,JOINTS
 KIND=kind;HEAVY=kind=='DrillBreaker';JOINTS=[];PARTS=[]
 if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
 old=bpy.data.collections.get('SPIDER_'+kind)
 if old:
  for o in list(old.objects):bpy.data.objects.remove(o,do_unlink=True)
  bpy.data.collections.remove(old)
 C=bpy.data.collections.new('SPIDER_'+kind);S.collection.children.link(C);ROOT=bpy.data.objects.new(kind+' source',None);C.objects.link(ROOT);ROOT['role']='Attacks drill' if HEAVY else 'Attacks player';ROOT['walking_legs']=8;ROOT['rig_status']='Unrigged neutral rest pose for Mesh2Motion';body();legs();face()
 image=bpy.data.images.load(str(WORK/'Docs/Concepts/AlienMining/Spiders'/(kind+'-eight-legs.png')),check_existing=True);image.pack();ROOT['reference_image']=image.name
 bpy.context.view_layer.update();pts=[o.matrix_world@Vector(p) for o in PARTS for p in o.bound_box]
 entry={'id':kind,'walkingLegs':len(JOINTS),'legs':JOINTS,'parts':len(PARTS),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in PARTS),'min':[min(p[i] for p in pts) for i in range(3)],'max':[max(p[i] for p in pts) for i in range(3)],'rig':'none; eight continuous legs in rest pose'}
 (OUT/(kind+'-model.json')).write_text(json.dumps(entry,indent=2));return entry
def preview(kind,view='reference'):
 col=bpy.data.collections['SPIDER_'+kind]
 for c in S.collection.children:
  if c.name.startswith('SPIDER_'):c.hide_render=c!=col
 def obj(name,typ):
  o=bpy.data.objects.get(name)
  if not o:o=bpy.data.objects.new(name,bpy.data.cameras.new(name) if typ=='CAMERA' else bpy.data.lights.new(name,typ));S.collection.objects.link(o)
  return o
 centre=Vector((0,.1,.65));cam=obj('Spider inspection camera','CAMERA');S.camera=cam;cam.location=(0,-4.4,10) if view=='reference' else (5,-7,4.5) if view=='side' else (0,0,10);cam.rotation_euler=(centre-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=5.2
 for name,pos,power,color in [('Spider key',(2,-4,7),850,(1,.90,.78)),('Spider fill',(-4,-1,5),650,(.73,.79,1)),('Spider rim',(1,5,6),1000,(1,.9,.8))]:
  o=obj(name,'AREA');o.location=pos;o.rotation_euler=(centre-o.location).to_track_quat('-Z','Y').to_euler();o.data.energy=power;o.data.size=5;o.data.color=color
 floor=bpy.data.objects.get('Spider studio floor')
 if not floor:
  bpy.ops.mesh.primitive_plane_add(size=200);floor=bpy.context.object;floor.name='Spider studio floor';floor.data.materials.append(mat('Spider studio grey',(.28,.28,.29),.85))
 floor.location.z=0;S.render.filepath=str(OUT/(kind+'-'+view+'.png'));bpy.ops.render.render(write_still=True);return S.render.filepath
def export(kind):
 col=bpy.data.collections['SPIDER_'+kind];col.hide_render=False;copies=[];bpy.ops.object.select_all(action='DESELECT')
 source_body=next(o for o in col.objects if o.get('closed_body'));core=source_body.copy();core.data=source_body.data.copy();core.parent=None;col.objects.link(core);bpy.context.view_layer.objects.active=core
 # Weld only the upload copy's leg roots into the body. Keep all source limbs editable.
 for leg in [o for o in col.objects if o.get('anatomy')=='walking_leg']:
  mod=core.modifiers.new('Connected '+leg.name,'BOOLEAN');mod.operation='UNION';mod.solver='EXACT';mod.object=leg;bpy.ops.object.modifier_apply(modifier=mod.name)
 bm=bmesh.new();bm.from_mesh(core.data);bm.verts.ensure_lookup_table();assert all(e.is_manifold for e in bm.edges),'Upload core has an open edge';todo=[bm.verts[0]];seen=set()
 while todo:
  v=todo.pop()
  if v in seen:continue
  seen.add(v);todo.extend(e.other_vert(v) for e in v.link_edges)
 assert len(seen)==len(bm.verts),'Upload body and all eight legs must form one connected shell'
 (OUT/(kind+'-upload-core.json')).write_text(json.dumps({'id':kind,'bodyAndEightLegsConnected':True,'closed':True,'vertices':len(bm.verts),'volume':bm.calc_volume()},indent=2));bm.free()
 copies.append(core);core.select_set(True)
 for o in list(col.objects):
  if o.type=='MESH' and o!=core and o!=source_body and o.get('anatomy')!='walking_leg':
   copy=o.copy();copy.data=o.data.copy();copy.parent=None;col.objects.link(copy);copy.select_set(True);copies.append(copy)
 bpy.context.view_layer.objects.active=core;bpy.ops.object.join();merged=bpy.context.object;merged.name=kind+' unrigged eight-legged mesh'
 bpy.ops.export_scene.gltf(filepath=str(OUT/(kind+'-Unrigged.glb')),export_format='GLB',use_selection=True,export_animations=False,export_skins=False,export_cameras=False,export_lights=False)
 bpy.data.objects.remove(merged,do_unlink=True);return str(OUT/(kind+'-Unrigged.glb'))
