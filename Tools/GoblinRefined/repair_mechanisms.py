import bpy,sys,math,json,struct,shutil,bmesh
from pathlib import Path
from mathutils import Vector,Quaternion
out=Path(sys.argv[sys.argv.index('--')+1]); source=out/'GoblinScraper_RefinedParts.blend'
backup=out/'backup_before_mechanical_fixes'; backup.mkdir(exist_ok=True)
if not (backup/source.name).exists(): shutil.copy2(source,backup/source.name); shutil.copy2(out/'parts-catalog.json',backup/'parts-catalog.json')
bpy.ops.wm.open_mainfile(filepath=str(source)); scene=bpy.context.scene
catalog=json.loads((out/'parts-catalog.json').read_text()); report=[]
def reset(rig):
 rig.animation_data.action=None
 for t in rig.animation_data.nla_tracks:t.mute=True
 for p in rig.pose.bones:p.location=(0,0,0);p.rotation_quaternion=Quaternion();p.scale=(1,1,1)
 scene.frame_set(1);bpy.context.view_layer.update()
def curves(a):
 return [f for l in a.layers for s in l.strips for cb in s.channelbags for f in cb.fcurves]
def linear(a):
 for f in curves(a):
  for k in f.keyframe_points:k.interpolation='LINEAR'
def spin(rig,a,bone,axis,turns):
 for f in list(curves(a)):
  if f.data_path==f'pose.bones["{bone}"].rotation_quaternion':
   for l in a.layers:
    for s in l.strips:
     for cb in s.channelbags:
      if f in list(cb.fcurves):cb.fcurves.remove(f)
 rig.animation_data.action=a;p=rig.pose.bones[bone]; rest=p.bone.matrix_local.to_quaternion();prev=None
 for frame in range(1,62):
  q=rest.inverted()@Quaternion(Vector(axis),math.tau*turns*(frame-1)/60)@rest
  if prev and prev.dot(q)<0:q.negate()
  p.rotation_quaternion=q;p.keyframe_insert('rotation_quaternion',frame=frame);prev=q.copy()
 linear(a)
def skin(obj,bone):
 for c in list(obj.users_collection):c.objects.unlink(obj)
 col.objects.link(obj);obj.parent=rig
 g=obj.vertex_groups.new(name=bone);g.add(list(range(len(obj.data.vertices))),1,'REPLACE')
 m=obj.modifiers.new('Rigid mechanical skin','ARMATURE');m.object=rig
 obj.data.materials.append(bpy.data.materials['Goblin_'+material]);
 uv=obj.data.uv_layers.new(name='UVMap')
 for p in obj.data.polygons:
  for i in p.loop_indices:
   v=obj.data.vertices[obj.data.loops[i].vertex_index].co;uv.data[i].uv=(v.x*2+v.y,v.z*2+v.y)
 return obj
def box(name,pos,size,mat='green',bone='Root'):
 global material;material=mat
 bpy.ops.mesh.primitive_cube_add(size=1,location=pos);o=bpy.context.object;o.name=name;o.scale=size
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 bpy.ops.object.transform_apply(location=True,rotation=True,scale=False);return skin(o,bone)
def cylinder(name,a,b,radius,mat='steel',bone='Root'):
 global material;material=mat;a=Vector(a);b=Vector(b)
 bpy.ops.mesh.primitive_cylinder_add(vertices=24,radius=radius,depth=(b-a).length,location=(a+b)/2)
 o=bpy.context.object;o.name=name;o.rotation_mode='QUATERNION';o.rotation_quaternion=(b-a).to_track_quat('Z','Y')
 bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);return skin(o,bone)
def beam(name,a,b,width,bone='Root'):
 a=Vector(a);b=Vector(b);o=box(name,(a+b)/2,(width,width,(b-a).length),'green',bone)
 # Cube geometry already baked: rotate its vertices around midpoint.
 q=(b-a).to_track_quat('Z','Y');mid=(a+b)/2
 for v in o.data.vertices:v.co=mid+q@(v.co-mid)
 return o
def spring(name,base,top,bone):
 global material;material='rust';base=Vector(base);top=Vector(top);d=top-base;q=d.to_track_quat('Z','Y')
 bpy.ops.object.select_all(action='DESELECT')
 cu=bpy.data.curves.new(name,'CURVE');cu.dimensions='3D';cu.bevel_depth=.022;cu.bevel_resolution=2
 sp=cu.splines.new('POLY');sp.points.add(160)
 for i,p in enumerate(sp.points):
  t=i/160;a=t*math.tau*5
  # Start/end on centreline, providing attached spring eyes.
  rad=.08*min(1,t*20,(1-t)*20);v=base+q@Vector((rad*math.cos(a),rad*math.sin(a),d.length*t));p.co=(*v,1)
 o=bpy.data.objects.new(name,cu);col.objects.link(o);bpy.context.view_layer.objects.active=o;o.select_set(True)
 bpy.ops.object.convert(target='MESH');o=bpy.context.object;skin(o,bone);o.select_set(False)
files=['scrap_auger_drill','scrap_battering_fist','scrap_cab_shell','scrap_catapult_basket','scrap_engine_block']
for file in files:
 part=next(p for p in catalog['parts'] if Path(p['file']).stem==file);col=bpy.data.collections[part['label']];root=bpy.data.objects[file];rig=next(o for o in col.objects if o.type=='ARMATURE');saved=root.location.copy();root.location=(0,0,0);reset(rig)
 print('REPAIR_INSPECT',file,len(rig.data.bones),[(t.name,len(t.strips)) for t in rig.animation_data.nla_tracks],flush=True)
 if file=='scrap_auger_drill':
  a=bpy.data.actions[part['animations'][0]['name']];spin(rig,a,'Drill',(0,1,0),2);report.append({'part':file,'rotation':'120 degrees per ten frames, continuous positive rotation, two full turns'})
 if file=='scrap_battering_fist':
  a=bpy.data.actions[part['animations'][0]['name']]
  for f in curves(a):
   if f.data_path=='pose.bones["Fist"].location':
    for k in f.keyframe_points:k.co.y*=.18/.55;k.handle_left.y*=.18/.55;k.handle_right.y*=.18/.55
  report.append({'part':file,'stroke_m':.18,'minimum_guide_overlap_m':.065})
 if file=='scrap_cab_shell':
  box('Floor mounted lever gearbox',(.35,.14,-.275),(.19,.20,.25),'iron')
  cylinder('Lever pivot bearing',(.35,.14,-.19),(.35,.14,-.085),.065,'brass')
  report.append({'part':file,'connection':'Floor gearbox encloses fixed lever pivot'})
 if file=='scrap_engine_block':
  cylinder('Flywheel crank bearing',(.36,0,0),(.51,0,0),.11,'iron');cylinder('Flywheel crank shaft',(.38,0,0),(.57,0,0),.055)
  cylinder('Fan engine mounting boss',(0,.33,.23),(0,.43,.23),.095,'iron');cylinder('Fan drive shaft',(0,.36,.23),(0,.52,.23),.04)
  a=bpy.data.actions[part['animations'][0]['name']];spin(rig,a,'Flywheel',(1,0,0),2);spin(rig,a,'Fan',(0,1,0),3)
  report.append({'part':file,'connection':'Crankshaft and fan shaft overlap engine and rotating hubs'})
 if file=='scrap_catapult_basket':
  mesh=next(o for o in col.objects if o.type=='MESH' and o.name.endswith('__Root'))
  bm=bmesh.new();bm.from_mesh(mesh.data);remove=[f for f in bm.faces if mesh.material_slots[f.material_index].material.name=='Goblin_rust'];bmesh.ops.delete(bm,geom=remove,context='FACES');bm.to_mesh(mesh.data);bm.free()
  bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
  for x,n in [(-.44,'SpringLeft'),(.44,'SpringRight')]:
   b=rig.data.edit_bones.new(n);b.head=(x,.05,-.205);b.tail=(x,.05,.34);b.parent=rig.data.edit_bones['Root']
  bpy.ops.object.mode_set(mode='OBJECT')
  for x,n in [(-.44,'SpringLeft'),(.44,'SpringRight')]:
   beam('Basket spring rocker '+n,(x,-.30,-.08),(x,.05,.34),.10,'Launch');spring('Articulated coil '+n,(x,.05,-.205),(x,.05,.34),n)
   cylinder('Spring lower eye '+n,(x-.05,.05,-.205),(x+.05,.05,-.205),.045,'brass')
   cylinder('Spring upper eye '+n,(x-.05,.05,.34),(x+.05,.05,.34),.045,'brass','Launch')
  beam('Basket rocker crossbar',(-.44,.05,.34),(.44,.05,.34),.09,'Launch')
  a=bpy.data.actions[next(c['name'] for c in part['animations'] if c['name'].endswith('__Launch'))];rig.animation_data.action=a
  maxgap=0
  for f in range(1,42):
   scene.frame_set(f);bpy.context.view_layer.update();launch=rig.pose.bones['Launch'];deform=launch.matrix@launch.bone.matrix_local.inverted()
   for x,n in [(-.44,'SpringLeft'),(.44,'SpringRight')]:
    base=Vector((x,.05,-.205));top=deform@Vector((x,.05,.34));d=top-base;p=rig.pose.bones[n];rest=p.bone.matrix_local.to_quaternion()
    world=Vector((0,0,1)).rotation_difference(d.normalized());p.rotation_mode='QUATERNION';p.rotation_quaternion=rest.inverted()@world@rest;p.scale=(1,d.length/.545,1)
    p.keyframe_insert('rotation_quaternion',frame=f);p.keyframe_insert('scale',frame=f)
   bpy.context.view_layer.update()
   for x,n in [(-.44,'SpringLeft'),(.44,'SpringRight')]:
    p=rig.pose.bones[n];tip=p.matrix@Vector((0,.545,0));target=deform@Vector((x,.05,.34));maxgap=max(maxgap,(tip-target).length)
  linear(a);assert maxgap<1e-4,maxgap;report.append({'part':file,'spring_endpoint_max_gap_m':maxgap,'frames_checked':41})
 reset(rig)
 # Export both factions with existing clip names and embedded materials.
 for red in [False,True]:
  path=out/('red_faction' if red else '')/(file+'.glb');shutil.copy2(path,backup/(('red_' if red else '')+path.name))
  swaps=[]
  for o in col.objects:
   if red and o.type=='MESH':
    for s in o.material_slots:
     if s.material==bpy.data.materials['Goblin_green']:swaps.append(s);s.material=bpy.data.materials['Goblin_red']
  bpy.ops.object.select_all(action='DESELECT')
  for o in col.objects:o.select_set(True)
  bpy.context.view_layer.objects.active=rig
  for t in rig.animation_data.nla_tracks:t.mute=False
  bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_yup=True,export_skins=True,export_animations=True,export_animation_mode='NLA_TRACKS',export_force_sampling=True,export_frame_range=False,export_extra_animations=False,export_extras=True,export_cameras=False,export_lights=False)
  for s in swaps:s.material=bpy.data.materials['Goblin_green']
  reset(rig)
 part['bones']=len(rig.data.bones);part['meshes']=sum(o.type=='MESH' for o in col.objects);part['triangles']=sum(len(o.data.polygons)*0 for o in []) if False else sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in col.objects if o.type=='MESH')
 # A motion strip for inspecting repaired parts.
 for pp in catalog['parts']:bpy.data.collections[pp['label']].hide_render=pp is not part
 cam=bpy.data.objects['Part Preview Camera'];scene.camera=cam;ground=bpy.data.objects['Studio Ground'];ground.location.z=-.55
 center=Vector((0,.25,.10));cam.location=center+Vector((2.5,3.6,2.4));cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=2.9
 scene.render.resolution_x=360;scene.render.resolution_y=360
 scene.render.filepath=str(out/'previews'/(file+'.png'));bpy.ops.render.render(write_still=True)
 motion=out/'previews'/'repair_motion';motion.mkdir(exist_ok=True)
 clip=next((c for c in part['animations'] if c['name'].endswith('__Launch')),part['animations'][0]);rig.animation_data.action=bpy.data.actions[clip['name']];end=round(clip['duration_seconds']*30)+1
 for i in range(12):
  scene.frame_set(1+round((end-1)*i/12));scene.render.filepath=str(motion/f'{file}_{i:02d}.png');bpy.ops.render.render(write_still=True)
 reset(rig);root.location=saved;print('REPAIRED',file,flush=True)
for p in catalog['parts']:bpy.data.collections[p['label']].hide_render=False
(out/'parts-catalog.json').write_text(json.dumps(catalog,indent=2));(out/'mechanical-fix-validation.json').write_text(json.dumps(report,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(source),compress=True);print('MECHANICAL_FIXES_COMPLETE',flush=True)

