"""Local reference-derived Besiege shapes, new Goblin surfaces, native mechanical rigs.
Original game geometry is retained as explicitly requested; no Besiege textures/code are shipped.
"""
from pathlib import Path
import bpy,sys,json,math
from mathutils import Vector,Quaternion,Matrix
args=sys.argv[sys.argv.index('--')+1:]
OUT=Path(args[0]);OUT.mkdir(parents=True,exist_ok=True)
lib=Path(__file__).with_name('build_refined.py').read_text(encoding='utf-8')
head=lib[:lib.index('# ----------------------------- CORE KIT')].replace("material('wood',(.23,.115,.047)","material('wood',(.36,.24,.12)").replace("material('iron',(.15,.17,.18)","material('iron',(.22,.24,.25)")
exec(compile(head,'goblin_material_and_geometry_helpers','exec'),globals())
material('canvas',(.55,.48,.31),'metal',0)
sys.path.insert(0,str(Path(__file__).parent))
from reference_material_normals import add_normals
add_normals(MATS.values(),OUT)
OUT=Path(args[0]);source=json.loads(Path(args[1]).read_text(encoding='utf-8'))
old=json.loads(Path(args[2]).read_text(encoding='utf-8'));selected=old['parts'];references={p['id']:p for p in source['parts']}
SCALE=.5
rotors={2,14,17,22,38,39,40,46,48,50,51,60,86,88}
servos={13,27,28,77,79,95};sliders={9,12,16,18,42};articulated=rotors|servos|sliders|{5,19,44,97}
beams={1:2,15:1,41:2,63:3,76:1}
critical={0,1,2,13,28,46,14,25,26,16,18,9}
manifest=dict(kit='Goblin standard and flight reference-aligned workshop',version=3,geometry_revision=3,units='metres',authoring_blender='5.0',
 provenance='Geometry derived from the locally installed Besiege game. Goblin materials, export rigs and socket definitions authored for ByteEngine. Original textures are not included.',parts=[])

def socket(name,pos,normal=(0,0,1),bone='Root'):
 e=CUR.socket(CUR.file+'__'+name.removeprefix('SOCKET_'),pos,normal,bone);e['connector_name']=name;return e

def unity_matrix(n):
 q=n['rotation'];return Matrix.LocRotScale(Vector(n['position']),Quaternion((q[3],q[0],q[1],q[2])),Vector(n['scale']))
def bounds(points):return [[min(v[i] for v in points) for i in range(3)],[max(v[i] for v in points) for i in range(3)]]
def game(v):return [round(float(v[0]),6),round(float(v[2]),6),round(float(-v[1]),6)]
def bgame(box):
 a,b=box;return [[a[0],a[2],-b[1]],[b[0],b[2],-a[1]]]
def source_meshes(ref,offset):
 transforms=[];result=[]
 for n in ref['nodes']:
  local=unity_matrix(n);world=transforms[n['parent']]@local if n['parent']>=0 else Matrix.Identity(4);transforms.append(world)
  if 'mesh' not in n:continue
  data=source['meshes'][n['mesh']];uv=data['uv'];verts=[]
  for v in data['vertices']:
   u=world@Vector(v);u-=offset;verts.append(Vector((u.x,u.z,u.y))*SCALE)
  faces=[];materials=[]
  for index,tris in enumerate(data['submeshes']):
   regions=n.get('material_regions',[[]]*len(data['submeshes']))[index]
   for i,t in enumerate(tris):faces.append(tuple(reversed(t)));materials.append(regions[i] if i<len(regions) else 'iron')
  result.append((n['name'],verts,faces,materials))
 return result

def add_faces(name,verts,faces,regions,bone):
 if not faces:return
 used=sorted({i for f in faces for i in f});mapping={v:i for i,v in enumerate(used)}
 data=bpy.data.meshes.new(name);data.from_pydata([verts[i] for i in used],[],[tuple(mapping[i] for i in f) for f in faces]);data.update()
 obj=bpy.data.objects.new(name,data);CUR.col.objects.link(obj);CUR.add(obj,'wood',bone)
 data.materials.clear()
 for key in ['wood','green','iron','steel','brass','black','canvas']:data.materials.append(MATS[key])
 for f,region in zip(data.polygons,regions):
  mat='green' if region=='iron' and bone=='Root' else region
  if CUR.ref_id in {6,35,36}:mat='iron'
  if CUR.ref_id in {25,34,79,95} and mat=='steel':mat='canvas'
  if CUR.ref_id in {43,97} and mat=='wood':mat='canvas'
  f.material_index=['wood','green','iron','steel','brass','black','canvas'].index(mat)
  # Preserve planar hard surfaces, soften only genuinely curved adjacent faces.
  f.use_smooth=True
 return obj

for row in selected:
 id=row['reference_id'];ref=references[id];file=Path(row['file']).stem
 part(file,row['label'],row['description'],row['group']);CUR.ref_id=id;CUR.root['source_geometry']='Installed Besiege block '+str(id)
 offset=Vector((0,0,beams[id]/2)) if id in beams else Vector((0,0,0))
 meshes=source_meshes(ref,offset)
 if id==73:
  box('Editable surface default panel',(.5,.5,.035),(0,0,0),'wood',bevel=.006);meshes=[]
 points=[v for _,vs,_,_ in meshes for v in vs]
 if not points:points=[Vector(o.matrix_world@Vector(v)) for o in CUR.objects for v in o.bound_box]
 lo,hi=map(Vector,bounds(points));middle=(lo+hi)*.5
 # Native forward is Unity +Z, represented by Blender +Y / canonical game -Z.
 axis_unity=Vector((0,1,0)) if id in {5,19,28,27,77} else Vector((0,0,1))
 native_axis=ref.get('behavior',{}).get('axis')
 if native_axis and sum(abs(native_axis.get(c,0)) for c in ['x','y','z'])>.5:axis_unity=Vector([native_axis.get(c,0) for c in ['x','y','z']])
 axis=Vector((axis_unity.x,axis_unity.z,axis_unity.y)).normalized()
 if id==13:axis=Vector((0,1,0))
 if id in {79,95}:axis=Vector((0,0,1))
 if id==86:axis=Vector((1,0,0))
 pivot=Vector((0,.25,0)) if id in {5,13,19,28,44,27,77} else Vector((0,middle.y,0))
 if id in sliders:pivot=Vector((0,lo.y+.02,0))
 if id==97:pivot=Vector((0,0,.30))
 if id in articulated:CUR.bone('Moving',pivot)
 for name,vs,faces,regions in meshes:
  if id not in articulated or id==97:add_faces(name,vs,faces,regions,'Root');continue
  fixed=[];moving=[];fm=[];mm=[]
  for face,region in zip(faces,regions):
   centroid=sum((vs[i] for i in face),Vector())/len(face)
   if id in rotors:move=True
   elif id==97:move=centroid.z>middle.z
   elif id in {79,95}:move=True
   else:move=centroid.y>=pivot.y
   (moving if move else fixed).append(face);(mm if move else fm).append(region)
  add_faces(name+' fixed',vs,fixed,fm,'Root');add_faces(name+' moving',vs,moving,mm,'Moving')
 if id==97:
  canopy=json.loads(Path(args[1]).with_name('parachute-deployed.json').read_text(encoding='utf-8'))
  verts=[pivot+Vector(v)*.75*.001 for v in canopy['vertices']]
  faces=[tuple(t) for tris in canopy['submeshes'] for t in tris]
  add_faces('Deployable reference canopy',verts,faces,['canvas']*len(faces),'Moving')
 # Separate bearing at the input datum; the rotating geometry never becomes the welded root.
 mount=Vector((0,lo.y-.002,0))
 if id in articulated:
  cylinder('Fixed attachment bearing',.085,.045,(0,mount.y+.0225,0),'iron',(0,1,0),'Root',verts=16,bevel=.002)
  if not CUR.groups.get('Moving'):
   cylinder('Functional output cap',.09,.04,(0,hi.y-.02,0),'iron',(0,1,0),'Moving',verts=16,bevel=.002)
 if id==0:
  for oldsocket in row['sockets']:
   g=oldsocket['normal'];normal=Vector((g[0],-g[2],g[1]));socket(oldsocket['name'],normal*.25,normal)
 elif id in beams:
  old_length=2 if id==1 else 1 if id==15 else max(abs(v['position'][2]) for v in row['sockets'])*2
  for oldsocket in row['sockets']:
   name=oldsocket['name'];g=oldsocket['normal'];normal=Vector((g[0],-g[2],g[1]));position=oldsocket['position']
   if abs(g[2])>.9:point=Vector((0,lo.y-.002 if g[2]>0 else hi.y+.002,0))
   else:
    station=-position[2]/max(old_length,.01)*beams[id]*SCALE
    side=min(.25,(hi.x-lo.x)/2);vertical=min(.25,(hi.z-lo.z)/2)
    point=Vector((g[0]*side,station,g[1]*vertical))
   socket(name,point,normal)
 else:
  for oldsocket in row['sockets']:
   name=oldsocket['name']
   if 'Mount' in name:socket(name,mount,(0,-1,0),'Root')
   elif 'Output' in name:socket(name,(0,hi.y+.002,0),(0,1,0),'Moving' if id in articulated else 'Root')
   else:
    g=oldsocket['normal'];normal=Vector((g[0],-g[2],g[1]));point=middle.copy()
    for axisindex in range(3):
     if abs(normal[axisindex])>.9:point[axisindex]=hi[axisindex] if normal[axisindex]>0 else lo[axisindex]
    socket(name,point,normal,oldsocket.get('animation_bone','Root'))
 if id in rotors:
  CUR.clip('Spin',{'Moving':[(1+i*10,axis,math.tau*i/6,(0,0,0)) for i in range(7)]},True)
 elif id in servos|{5,19,44}:
  CUR.clip('Steer',{'Moving':[(1,axis,-math.pi/6,(0,0,0)),(31,axis,0,(0,0,0)),(61,axis,math.pi/6,(0,0,0))]},False)
 elif id in sliders:
  CUR.clip('Extend',{'Moving':[(1,axis,0,(0,0,0)),(61,axis,0,axis*.4)]},False)
 elif id==97:
  CUR.clip('Deploy',{'Moving':[(1,(0,0,1),0,(0,0,0)),(61,(0,0,1),0,(0,0,0))]},False)
 CUR.source_row=row;CUR.axis=axis;CUR.ref_pivot=pivot
print('REFERENCE_MODELS_READY',len(ALL),flush=True)
a=lib.index('def planar_uv');b=lib.index('for p in ALL:\n    make_rig',a);exec(compile(lib[a:b],'goblin_rig_library','exec'),globals())
a=lib.index('def glb_json');b=lib.index('bpy.ops.wm.save_as_mainfile',a);exec(compile(lib[a:b],'goblin_export_library','exec'),globals())
for p in ALL:
 make_rig(p)
 if p.ref_id==97:
  for action,clip,loop,end in p.actions:
   p.rig.animation_data.action=action;pb=p.rig.pose.bones['Moving']
   for frame,scale in [(1,1),(61,1000)]:pb.scale=(scale,scale,scale);pb.keyframe_insert(data_path='scale',frame=frame,group='Moving')
   for curve in action_curves(action):
    for key in curve.keyframe_points:key.interpolation='LINEAR'
  p.rig.animation_data.action=None
  for pb in p.rig.pose.bones:pb.scale=(1,1,1)
 # Elastic spring geometry follows both anchor bodies rather than breaking in two.
 if p.ref_id in {9,16}:
  for obj in p.objects:
   if not obj.data.vertices:continue
   values=[v.co.y for v in obj.data.vertices];bottom=min(values);top=max(values)
   if top-bottom<.08:continue
   # Only the narrow coil/rod section gets blended skinning; rigid caps retain their weights.
   root_group=obj.vertex_groups.get('Root') or obj.vertex_groups.new(name='Root');moving_group=obj.vertex_groups.get('Moving') or obj.vertex_groups.new(name='Moving')
   for v in obj.data.vertices:
    t=max(0,min(1,(v.co.y-bottom)/(top-bottom)));root_group.add([v.index],1-t,'REPLACE');moving_group.add([v.index],t,'REPLACE')
 export(p)
 row=dict(p.source_row);row['bounds_blender_z_up']=p.bounds
 row['sockets']=[dict(name=s['connector_name'],position=game(s['rest_position_blender']),normal=game(s['rest_normal_blender']),animation_bone=s.get('animation_bone','Root')) for s in p.sockets]
 groups={}
 for obj in p.objects:
  names={g.index:g.name for g in obj.vertex_groups}
  for v in obj.data.vertices:
   for w in v.groups:
    if w.weight>.5:groups.setdefault(names[w.group],[]).append(obj.matrix_world@v.co)
 row['root_bounds_game']=bgame(bounds(groups.get('Root') or [Vector(p.bounds[0]),Vector(p.bounds[1])]))
 if groups.get('Moving'):row['moving_bounds_game']=bgame(bounds(groups['Moving']));row['pivot_game']=game(p.ref_pivot);row['axis_game']=game(p.axis)
 else:row.pop('moving_bounds_game',None);row.pop('pivot_game',None)
 if p.ref_id in {2,40,46,50,60,17,38,39,51,88} and groups.get('Moving'):
  vertices=groups['Moving'];radius=max((v-p.axis*v.dot(p.axis)).length for v in vertices)
  tyre=[v for v in vertices if (v-p.axis*v.dot(p.axis)).length>=radius*.65]
  row['moving_collider_game']=bgame(bounds(tyre))
 row['animations']=[dict(name=a['name'],channels=len(a['channels'])) for a in p.export_doc.get('animations',[])]
 row['geometry_provenance']='Reference-derived from installed Besiege block '+str(p.ref_id);manifest['parts'].append(row)
(OUT/'parts-catalog.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
for i,p in enumerate(ALL):p.col.hide_viewport=i>0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Goblin_Standard_Flight_v3.blend'),compress=True)
# Real geometry previews, never generated image mockups.
a=lib.index('# Studio for real geometry thumbnails');b=lib.index('for p in ALL:',a);exec(compile(lib[a:b],'goblin_studio_library','exec'),globals())
SCENE.render.resolution_x=320;SCENE.render.resolution_y=240;SCENE.render.film_transparent=True
if hasattr(SCENE,'eevee'):SCENE.eevee.taa_render_samples=16
for p in ALL:
 p.col.hide_viewport=False;frame_part(p);ground.hide_render=True
 SCENE.render.filepath=str(OUT/'previews'/(p.file+'.png'));bpy.ops.render.render(write_still=True)
 print('REFERENCE_PREVIEW_READY',p.file,flush=True)
print('REFERENCE_V3_COMPLETE',len(ALL),flush=True)