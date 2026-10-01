from pathlib import Path
import bpy,json,types,math,struct
from mathutils import Vector,Quaternion
OUT=Path(r'C:\Users\codex\Downloads\parts\refined\standard-flight-v2');bpy.ops.wm.open_mainfile(filepath=str(OUT/'Goblin_Parts_Redesign.blend'));SCENE=bpy.context.scene;ALL=[]
lib=Path(__file__).with_name('build_refined.py').read_text();a=lib.index('def planar_uv');b=lib.index('def action_curves',a);exec(compile(lib[a:b],'planar_uv','exec'),globals());a=lib.index('def glb_json');b=lib.index('bpy.ops.wm.save_as_mainfile',a);exec(compile(lib[a:b],'own_export','exec'),globals());MATS={k:bpy.data.materials.get('Goblin_'+k,bpy.data.materials['Goblin_green']) for k in ['green','red','iron']}
manifest=json.loads((OUT/'parts-catalog.json').read_text())
for item in manifest['parts']:
 file=Path(item['file']).stem;rig=bpy.data.objects[file+'_rig'];col=rig.users_collection[0];col.hide_viewport=False;root=bpy.data.objects[file]
 acts=[(t.strips[0].action,t.strips[0].action.name,False,61) for t in rig.animation_data.nla_tracks]
 p=types.SimpleNamespace(file=file,rig=rig,col=col,root=root,objects=[o for o in col.objects if o.type=='MESH'],actions=acts);ALL.append(p)
 if not any((Vector(s['position'])-Vector((0,0,.24))).length<.0001 for s in item['sockets']):continue
 target=next(o for o in col.objects if o.type=='MESH' and o.name.startswith(file+'__Root'));extras=[]
 def add_obj(o):
  for c in list(o.users_collection):c.objects.unlink(o)
  col.objects.link(o);o.data.materials.append(MATS['iron']);bpy.context.view_layer.objects.active=o;bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);planar_uv(o);vg=o.vertex_groups.new(name='Root');vg.add(list(range(len(o.data.vertices))),1,'REPLACE');o.parent=rig;extras.append(o)
 bpy.ops.object.select_all(action='DESELECT');bpy.ops.mesh.primitive_cube_add(size=1,location=(0,-.10,0));o=bpy.context.object;o.scale=(.10,.24,.10);add_obj(o)
 if item['reference_id'] in [9,16]:
  for radius,depth,y in [(.065,.20,0),(.16,.045,.10)]:
   bpy.ops.mesh.primitive_cylinder_add(vertices=20,radius=radius,depth=depth,location=(0,y,0));o=bpy.context.object;o.rotation_euler[0]=math.pi/2;add_obj(o)
 bpy.ops.object.select_all(action='DESELECT');target.select_set(True)
 for o in extras:o.select_set(True)
 bpy.context.view_layer.objects.active=target;bpy.ops.object.join();p.objects=[o for o in col.objects if o.type=='MESH'];export(p);item['bounds_blender_z_up']=p.bounds;print('MOUNT_CONNECTED',file,flush=True)
(OUT/'parts-catalog.json').write_text(json.dumps(manifest,indent=2));bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Goblin_Parts_Redesign.blend'),compress=True)
a=lib.index('# Studio for real geometry thumbnails');b=lib.index('for p in ALL:',a);exec(compile(lib[a:b],'studio','exec'),globals());SCENE.render.resolution_x=256;SCENE.render.resolution_y=192;SCENE.render.film_transparent=True;ground.hide_render=True
for p in ALL:
 frame_part(p);SCENE.render.filepath=str(OUT/'previews'/(p.file+'.png'));bpy.ops.render.render(write_still=True)
print('CONNECTED_MOUNTS_COMPLETE',flush=True)


