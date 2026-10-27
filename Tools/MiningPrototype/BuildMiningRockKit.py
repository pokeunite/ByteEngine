"""Executed INSIDE the open Blender through MCP. Editable named parts; merged game exports."""
import bpy,math,random,json
from pathlib import Path
from mathutils import Vector,Matrix

WORK=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine')
OUT=WORK/'artifacts/mining-rock-kit';OUT.mkdir(parents=True,exist_ok=True)
CAT=json.loads((WORK/'Docs/Concepts/AlienMining/RockKit/catalog.json').read_text())['assets']

def material(name,color,rough=.9,emission=0):
 m=bpy.data.materials.get(name) or bpy.data.materials.new(name);m.use_nodes=True;m.diffuse_color=(*color,1)
 n=m.node_tree.nodes.get('Principled BSDF');n.inputs['Base Color'].default_value=(*color,1);n.inputs['Roughness'].default_value=rough;n.inputs['Metallic'].default_value=0
 if emission:n.inputs['Emission Color'].default_value=(*color,1);n.inputs['Emission Strength'].default_value=emission
 return m

def setup():
 s=bpy.data.scenes.get('Mining Rock Kit') or bpy.data.scenes.new('Mining Rock Kit');bpy.context.window.scene=s
 s.render.engine='BLENDER_EEVEE';s.render.resolution_x=960;s.render.resolution_y=720;s.render.resolution_percentage=100
 s.world=s.world or bpy.data.worlds.new('Rock kit studio');s.world.use_nodes=True;s.world.node_tree.nodes['Background'].inputs[0].default_value=(.075,.065,.11,1);s.world.node_tree.nodes['Background'].inputs[1].default_value=.45
 s.view_settings.view_transform='AgX';s.render.image_settings.file_format='PNG';s.render.film_transparent=False
 for name,loc,power,color,size in [('Rock kit warm key',(15,-20,26),3200,(1,.82,.65),15),('Rock kit cool fill',(-16,-10,14),2400,(.64,.68,1),18),('Rock kit rim',(4,20,25),3000,(1,.73,.46),12)]:
  o=bpy.data.objects.get(name)
  if not o:d=bpy.data.lights.new(name,'AREA');o=bpy.data.objects.new(name,d);s.collection.objects.link(o)
  o.location=loc;o.data.energy=power;o.data.color=color;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector((0,0,4))-o.location).to_track_quat('-Z','Y').to_euler()
 c=bpy.data.objects.get('Rock kit inspection camera')
 if not c:c=bpy.data.objects.new('Rock kit inspection camera',bpy.data.cameras.new('Rock kit camera'));s.collection.objects.link(c)
 c.data.type='ORTHO';c.data.clip_end=1000;s.camera=c
 return s

SCENE=setup()
MATS=[material('Basalt - slate violet',(.075,.062,.102)),material('Basalt - light facet',(.093,.079,.123)),material('Basalt - deep facet',(.052,.045,.078)),material('Crystal - teal',(.015,.28,.40),.32,.8),material('Crystal - cyan',(.025,.62,.75),.26,1.2),material('Crystal - pale tip',(.28,.86,.95),.22,1.5),material('Foliage - violet',(.25,.022,.34),.86),material('Foliage - magenta',(.48,.045,.35),.86)]

def asset_collection(id):
 name='ROCKKIT_'+id
 old=bpy.data.collections.get(name)
 if old:
  for o in list(old.objects):bpy.data.objects.remove(o,do_unlink=True)
  bpy.data.collections.remove(old)
 c=bpy.data.collections.new(name);SCENE.collection.children.link(c)
 root=bpy.data.objects.new(id+' - '+next(a['name'] for a in CAT if a['id']==id),None);c.objects.link(root);root['rockkit_id']=id
 index=next(i for i,a in enumerate(CAT) if a['id']==id);root.location=((index%4)*42,-(index//4)*42,0);root['source_reference']=next(a['file'] for a in CAT if a['id']==id)
 return c,root

def mesh_obj(c,root,name,verts,faces,mats=(0,1,2),seed=1):
 me=bpy.data.meshes.new(name+' editable geometry');me.from_pydata(verts,[],faces);me.update();o=bpy.data.objects.new(root['rockkit_id']+'_'+name,me);c.objects.link(o);o.parent=root
 for i in mats:me.materials.append(MATS[i])
 rng=random.Random(seed)
 for p in me.polygons:p.use_smooth=False;p.material_index=0 if len(mats)==1 or rng.random()<.78 else rng.randrange(len(mats))
 return o

def rock(c,root,name,p,size,seed,lean=(0,0),boulder=False,yaw=0):
 rng=random.Random(seed);w,d,h=size
 # Bevelled irregular octagon. Rings create broad planar facets, rather than noisy displaced spheres.
 shape=[(-.36,-.5),(.29,-.5),(.5,-.29),(.5,.30),(.29,.5),(-.31,.5),(-.5,.27),(-.5,-.27)]
 shape=[(x+rng.uniform(-.065,.065),y+rng.uniform(-.065,.065)) for x,y in shape]
 rings=[(0,.92),(.48,1.03),(.87,.85),(1,.66)] if boulder else [(0,1),(.46,1.01),(.87,.89),(1,.76)]
 verts=[]
 for ri,(z,f) in enumerate(rings):
  for i,(x,y) in enumerate(shape):
   f2=f*(1+rng.uniform(-.11,.11));zz=z*h+(rng.uniform(-.085,.085)*h if ri in (1,2) else 0)
   if ri==3:zz+=h*(.035*math.sin(i*1.7+seed))
   crease=(rng.uniform(-.10,.10) if ri in (1,2) else 0)
   verts.append((p[0]+x*w*f2+lean[0]*z+crease*w,p[1]+y*d*f2+lean[1]*z,p[2]+zz))
 faces=[tuple(reversed(range(8)))];faces.append(tuple(range(24,32)))
 for r in range(3):
  for i in range(8):
   a=r*8+i;b=r*8+(i+1)%8;cc=b+8;dd=a+8
   if (i+r+seed)%3==0:faces.extend([(a,b,cc),(a,cc,dd)])
   else:faces.append((a,b,cc,dd))
 o=mesh_obj(c,root,name,verts,faces,seed=seed)
 if yaw:
  ang=math.radians(yaw);co,si=math.cos(ang),math.sin(ang)
  for v in o.data.vertices:x,y=v.co.x-p[0],v.co.y-p[1];v.co.x=p[0]+x*co-y*si;v.co.y=p[1]+x*si+y*co
 o['collision_candidate']=True
 return o

def crystal(c,root,name,p,height,radius,seed,lean=(0,0)):
 rng=random.Random(seed);n=5;verts=[]
 for z,f in [(0,.78),(.72,1)]:
  for i in range(n):a=2*math.pi*i/n+.25;verts.append((p[0]+math.cos(a)*radius*f+lean[0]*z,p[1]+math.sin(a)*radius*f+lean[1]*z,p[2]+z*height))
 verts.append((p[0]+lean[0],p[1]+lean[1],p[2]+height));faces=[tuple(reversed(range(n)))]
 for i in range(n):j=(i+1)%n;faces.extend([(i,j,n+j,n+i),(n+i,n+j,2*n)])
 o=mesh_obj(c,root,name,verts,faces,(3,4,5),seed)
 for i,f in enumerate(o.data.polygons):f.material_index=2 if i>0 and i%2==0 else (1 if i%3 else 0)
 return o

def leaf(c,root,name,angle,length,width,height,seed):
 co,si=math.cos(angle),math.sin(angle)
 raw=[(0,0,0),(length*.48,-width*.5,height*.55),(length,0,height),(length*.48,width*.5,height*.55),(length*.45,0,height*.78),(length*.42,0,height*.36)]
 verts=[(x*co-y*si,x*si+y*co,z) for x,y,z in raw];faces=[(0,1,4),(1,2,4),(2,3,4),(3,0,4),(0,5,1),(1,5,2),(2,5,3),(3,5,0)]
 return mesh_obj(c,root,name,verts,faces,(6,7),seed)

def build(id):
 c,r=asset_collection(id);add=lambda name,p,size,seed,**kw:rock(c,r,name,p,size,seed,**kw)
 if id=='R01':
  for i,(x,h) in enumerate([(-3.4,9.8),(0,8.5),(3.2,7.2)]):add('main column '+str(i),(x,.5,0),(3.7,3.5,h),10+i,lean=(.15,0))
  for i,(x,h) in enumerate([(-4.5,4.3),(-2,4.5),(.4,2.2),(2.2,3.8),(4.4,2.5)]):add('basal column '+str(i),(x,-1.45,0),(1.9,2.0,h),25+i)
 elif id=='R02':
  for i,h in enumerate([7.8,6.5,5.2,4.3,3.5]):add('descending column '+str(i),(-5.4+i*2.7,.3,0),(3.1,3.5,h),41+i)
  for i,h in enumerate([2.6,3.1,2.0,2.3,1.4,1.7]):add('basal ledge '+str(i),(-6+i*2.45,-1.4,0),(1.8,1.8,h),51+i)
 elif id=='R03':
  for i,(x,h,l) in enumerate([(-2.9,6.0,-1.1),(0,8.6,-.8),(2.9,7.2,-.5)]):add('leaning slab '+str(i),(x,.2,0),(3.2,2.8,h),71+i,lean=(l,.3))
  for i,x in enumerate([-3.7,-.8,2.5]):add('toe block '+str(i),(x,-1.2,0),(1.7,1.4,1.8+i*.3),78+i,boulder=True)
 elif id=='R04':
  points=[(-6,3),(-4.2,2),(-2.4,.9),(-.5,0),(1.4,.9),(3.3,2),(5.3,3.1)]
  for i,(x,y) in enumerate(points):add('corner boulder '+str(i),(x,y,0),(2.8,2.8,2.7+(i%3)*.28),101+i,boulder=True,yaw=i*11)
  for i,(x,y) in enumerate(points[1:-1]):add('corner toe '+str(i),(x,y-1.1,0),(1.2,1.3,1.0),117+i,boulder=True)
 elif id=='R05':
  add('dominant spire',(1,.3,0),(5.1,4.1,17),131,lean=(-.5,.2));add('split secondary',(-2,-.1,0),(3.3,3.4,12.6),132,lean=(.1,.1))
  for i,(x,y) in enumerate([(-2.7,-1),(1.5,-1.7),(3,.7)]):add('spire base '+str(i),(x,y,0),(2.7,2.9,3.2+i*1.1),137+i)
 elif id=='R06':
  for i,x in enumerate([-7,-3.7,0,3.7,7]):add('mesa crown '+str(i),(x,.8,0),(4.6,8,14.5+(i%2)*.3),151+i)
  for i,(x,y,h) in enumerate([(-9,-1,6),(-6,-3.6,7.3),(-2,-4.1,5.8),(2,-4,6.8),(6,-3,8),(9,-.7,5.5),(-6,4,6),(4,4,7)]):add('mesa skirt '+str(i),(x,y,0),(4,4,h),163+i)
 elif id=='R07':
  for i,x in enumerate([-9,9]):add('bridge pier '+str(i),(x,0,0),(6.6,7,10.7-i*.6),181+i)
  # Thick irregular rock cap connects the two supports; lower opening stays real geometry.
  add('bridge cap',(-.3,.6,8.3),(17,5.8,3.5),187,boulder=True,lean=(.6,0))
  for i,(x,y,h) in enumerate([(-11,-2,4),(-7,-2.7,5),(8,-2.6,4),(11,-1.8,3)]):add('bridge basal '+str(i),(x,y,0),(3.4,3.2,h),191+i)
 elif id=='R08':add('single broad boulder',(0,0,0),(2.6,2.1,1.45),211,boulder=True,lean=(.18,-.12),yaw=12)
 elif id=='R09':
  for i,(p,size) in enumerate([((0,.2,0),(3.5,2.7,1.7)),((-1.6,-.5,0),(1.35,1.4,.95)),((1.7,.1,0),(1.4,1.5,.85))]):add('low boulder '+str(i),p,size,231+i,boulder=True)
 elif id=='R10':
  for i,(p,size) in enumerate([((-.5,.4,0),(3.1,2.5,2.7)),((1.3,-.05,0),(1.7,1.8,1.65)),((.2,-1.15,0),(1.0,1.1,.65)),((-1.6,-1,0),(.65,.9,.4))]):add('upright cover '+str(i),p,size,251+i,boulder=True)
 elif id=='R11':
  for i,(x,y,h,w) in enumerate([(0,.2,.72,.8),(-.6,.1,.45,.6),(.6,0,.4,.6),(-.4,-.5,.32,.5),(.2,-.5,.22,.4),(-.95,-.25,.18,.3),(.5,.65,.25,.4)]):add('broken fragment '+str(i),(x,y,0),(w,w*.8,h),271+i,boulder=True,yaw=i*33)
 elif id=='R12':
  rng=random.Random(291)
  for i in range(14):w=rng.uniform(.13,.48);add('scatter chip '+str(i),(rng.uniform(-1.7,1.7),rng.uniform(-.9,.9),0),(w,w*rng.uniform(.5,1),w*rng.uniform(.35,.8)),291+i,boulder=True,yaw=i*57)
 elif id=='R13':
  for i,(p,size) in enumerate([((-2.3,.2,0),(3,3.5,7.5)),((1.1,.65,0),(3.5,3.4,9.0)),((3.4,.7,0),(2.7,3.5,7.7)),((-2.6,-1.7,0),(1.9,1.6,3.2)),((2.7,-1.5,0),(2,1.8,3.3))]):add('fissure cliff '+str(i),p,size,321+i)
  for i,(x,y,z,h,rr) in enumerate([(-.65,-.9,1.2,4.6,.27),(-.2,-1.3,.7,2.9,.32),(-.7,-1.4,.5,1.6,.29),(-.9,-1,3.6,2.1,.21),(-.4,-1.65,.1,.9,.25)]):crystal(c,r,'fissure crystal '+str(i),(x,y,z),h,rr,341+i,(.12,-.15))
 elif id=='R14':
  for i,(p,size) in enumerate([((1,.6,0),(3,2.8,2.7)),((-1.2,.3,0),(2.2,2.2,1.7)),((-.6,-1.2,0),(2.5,1.4,1.1))]):add('crystal boulder '+str(i),p,size,361+i,boulder=True)
  for i,(x,y,h,rr) in enumerate([(0,-.3,2,.21),(-.4,-.5,1.3,.24),(.3,-.55,1.1,.21),(-.2,-.7,.6,.24)]):crystal(c,r,'boulder crystal '+str(i),(x,y,.3),h,rr,371+i,(.12,0))
 elif id=='D01':
  for i,(x,y,h,rr) in enumerate([(0,.2,1.25,.18),(-.28,0,.85,.16),(.28,.03,.73,.15),(0,-.23,.60,.16),(-.43,-.22,.43,.12),(.43,-.17,.36,.1)]):crystal(c,r,'independent crystal '+str(i),(x,y,0),h,rr,391+i,(x*.2,y*.15))
 elif id=='D02':
  for i,(a,l,w,h) in enumerate([(0,.48,.17,.24),(.8,.42,.16,.37),(1.7,.32,.17,.64),(2.5,.51,.19,.30),(3.3,.43,.17,.16),(4.2,.37,.17,.42),(5.2,.49,.19,.23)]):leaf(c,r,'violet leaf '+str(i),a,l,w,h,411+i)
 else:raise ValueError(id)
 # Editable source reference packed into the blend, hidden from renders and exports.
 refs=bpy.data.collections.get('Rock kit image references')
 if not refs:refs=bpy.data.collections.new('Rock kit image references');SCENE.collection.children.link(refs);refs.hide_viewport=True
 imagepath=WORK/'Docs/Concepts/AlienMining/RockKit'/next(a['file'] for a in CAT if a['id']==id)
 image=bpy.data.images.load(str(imagepath),check_existing=True);image.pack();guide=bpy.data.objects.get('Reference '+id)
 if not guide:guide=bpy.data.objects.new('Reference '+id,None);refs.objects.link(guide)
 guide.empty_display_type='IMAGE';guide.data=image;guide.empty_display_size=10;guide.location=r.location+Vector((0,10,7));guide.rotation_euler=(math.pi/2,0,0);guide.hide_render=True
 return r

def export_asset(root):
 bpy.context.view_layer.update();parts=[o for o in root.children if o.type=='MESH'];verts=[];faces=[];indices=[];mats=[];inverse=root.matrix_world.inverted()
 bounds=[];part_bounds=[]
 for o in parts:
  matrix=inverse@o.matrix_world;offset=len(verts);points=[matrix@v.co for v in o.data.vertices];verts.extend([tuple(p) for p in points]);bounds.extend(points)
  if o.get('collision_candidate'):part_bounds.append({'name':o.name,'min':[min(p[i] for p in points) for i in range(3)],'max':[max(p[i] for p in points) for i in range(3)]})
  for p in o.data.polygons:
   faces.append(tuple(offset+i for i in p.vertices));m=o.data.materials[p.material_index]
   if m not in mats:mats.append(m)
   indices.append(mats.index(m))
 me=bpy.data.meshes.new(root['rockkit_id']+' game mesh');me.from_pydata(verts,[],faces);me.update();temp=bpy.data.objects.new(root['rockkit_id']+'_Game',me);SCENE.collection.objects.link(temp)
 for m in mats:me.materials.append(m)
 for p,i in zip(me.polygons,indices):p.material_index=i;p.use_smooth=False
 for o in bpy.context.selected_objects:o.select_set(False)
 temp.select_set(True);bpy.context.view_layer.objects.active=temp
 id=root['rockkit_id'];bpy.ops.export_scene.gltf(filepath=str(OUT/(id+'.glb')),export_format='GLB',use_selection=True,export_yup=True,export_apply=True,export_materials='EXPORT',export_animations=False,export_extras=True,export_cameras=False,export_lights=False)
 me.calc_loop_triangles();entry={'id':id,'name':root.name,'file':id+'.glb','vertices':len(me.vertices),'triangles':len(me.loop_triangles),'materials':[m.name for m in mats],'min':[min(p[i] for p in bounds) for i in range(3)],'max':[max(p[i] for p in bounds) for i in range(3)],'parts':part_bounds}
 bpy.data.objects.remove(temp,do_unlink=True);bpy.data.meshes.remove(me)
 return entry

def build_assets(ids):
 manifest=json.loads((OUT/'models.json').read_text()) if (OUT/'models.json').exists() else {'units':'metres','axis':'Blender Z up; glTF export Y up','assets':[]}
 for id in ids:
  root=build(id);entry=export_asset(root);manifest['assets']=[a for a in manifest['assets'] if a['id']!=id]+[entry];(OUT/'models.json').write_text(json.dumps(manifest,indent=2));print(id,entry['triangles'],'triangles')
 result={'built':ids,'files':len(manifest['assets'])}
 return result

def inspect_asset(id,render=True):
 root=next(o for o in SCENE.objects if o.get('rockkit_id')==id);parts=[o for o in root.children if o.type=='MESH'];bpy.context.view_layer.update();pts=[o.matrix_world@Vector(c) for o in parts for c in o.bound_box];low=Vector(tuple(min(p[i] for p in pts) for i in range(3)));high=Vector(tuple(max(p[i] for p in pts) for i in range(3)));centre=(low+high)/2;span=max((high-low).x,(high-low).y,(high-low).z)
 for o in SCENE.objects:
  if o.get('rockkit_id'):o.hide_render=o!=root
 for a in CAT:
  col=bpy.data.collections.get('ROCKKIT_'+a['id'])
  if col:
   for o in col.objects:o.hide_render=a['id']!=id
 cam=SCENE.camera;cam.location=centre+Vector((1.3,-2.1,1.2))*max(4,span);cam.rotation_euler=(centre-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=span*1.95
 for name,delta in [('Rock kit warm key',(1.5,-2,2.7)),('Rock kit cool fill',(-1.5,-1,1.8)),('Rock kit rim',(.5,2.2,2.8))]:
  light=bpy.data.objects[name];light.location=centre+Vector(delta)*max(4,span);light.rotation_euler=(centre-light.location).to_track_quat('-Z','Y').to_euler();light.data.energy=(max(4,span)/10)**2*(3600 if 'key' in name else 2800);light.data.size=max(4,span)*1.2
 for name,direction,energy,color in [('Rock kit sun',(1.1,-1.4,2),3.2,(1,.68,.42)),('Rock kit fill sun',(-1,-.4,1.1),.7,(.57,.65,1))]:
  light=bpy.data.objects.get(name)
  if not light:light=bpy.data.objects.new(name,bpy.data.lights.new(name,'SUN'));SCENE.collection.objects.link(light)
  light.data.energy=energy;light.data.color=color;light.data.angle=.12;light.rotation_euler=(-Vector(direction)).to_track_quat('-Z','Y').to_euler()
 SCENE.render.filepath=str(OUT/(id+'-preview.png'))
 if render:bpy.ops.render.render(write_still=True)
 return {'file':SCENE.render.filepath,'span':span,'parts':len(parts)}
