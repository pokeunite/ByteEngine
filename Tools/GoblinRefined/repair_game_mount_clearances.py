from pathlib import Path
import bpy,math,json,types,struct
from mathutils import Vector,Quaternion
SRC=Path(r'C:/Users/codex/Downloads/parts/refined/standard-flight-v2');OUT=Path(r'C:/Users/codex/Documents/Goblin Scraper/Assets/ContraptionParts');manifest=json.loads((OUT/'parts-catalog.json').read_text())
bpy.ops.wm.open_mainfile(filepath=str(SRC/'Goblin_Parts_Redesign.blend'));SCENE=bpy.context.scene
lib=Path(r'C:/Users/codex/ByteEngine/Tools/GoblinRefined/build_refined.py').read_text();a=lib.index('def glb_json');b=lib.index('bpy.ops.wm.save_as_mainfile',a);exec(compile(lib[a:b],'own_export','exec'),globals())
for d in manifest['parts']:
 if d['reference_id'] in [0,17]:continue
 socket=d['sockets'][0]
 if socket['normal']!=[0,0,1] and socket['normal']!=[0.0,0.0,1.0]:continue
 maxz=max(d['root_bounds_game'][1][2],d.get('moving_bounds_game',d['root_bounds_game'])[1][2]);old=socket['position'][2]
 if maxz-old<.068:continue
 new=maxz+.07;file=Path(d['file']).stem;rig=bpy.data.objects[file+'_rig'];col=rig.users_collection[0];col.hide_viewport=False;root=bpy.data.objects[file]
 for o in col.objects:
  if o.type=='EMPTY' and o.name==socket['name']:
   matrix=o.matrix_world.copy();matrix.translation.y-=new-old;o.matrix_world=matrix
 for size,pos in [((.10,new-old+.04,.10),(0,-(new+old)/2,0)),((.30,.04,.30),(0,-new,0))]:
  bpy.ops.mesh.primitive_cube_add(size=1,location=pos);o=bpy.context.object;o.scale=size
  for c in list(o.users_collection):c.objects.unlink(o)
  col.objects.link(o);o.data.materials.append(bpy.data.materials['Goblin_iron']);bpy.context.view_layer.objects.active=o;bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);o.vertex_groups.new(name='Root').add(list(range(len(o.data.vertices))),1,'REPLACE');o.parent=rig;mod=o.modifiers.new('Armature','ARMATURE');mod.object=rig
 acts=[(t.strips[0].action,t.strips[0].action.name,False,61) for t in rig.animation_data.nla_tracks];p=types.SimpleNamespace(file=file,rig=rig,col=col,root=root,objects=[o for o in col.objects if o.type=='MESH'],actions=acts);export(p)
 socket['position'][2]=new;d['root_bounds_game'][1][2]=new+.02;d['bounds_blender_z_up'][0][1]=-new-.02
 print('GAME_MOUNT_CLEARANCE',file,new,flush=True)
for path in [OUT/'parts-catalog.json',OUT.parent/'GarageUI/parts-catalog.json',Path(r'C:/Users/codex/ByteEngine/output/goblin-scraper/selected-contraption-parts.json')]:path.write_text(json.dumps(manifest,indent=2))
print('GAME_MOUNTS_COMPLETE',flush=True)
