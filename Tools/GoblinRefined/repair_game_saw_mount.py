from pathlib import Path
import bpy,math,json,types,struct
from mathutils import Vector,Quaternion
SRC=Path(r'C:/Users/codex/Downloads/parts/refined/standard-flight-v2');OUT=Path(r'C:/Users/codex/Documents/Goblin Scraper/Assets/ContraptionParts')
bpy.ops.wm.open_mainfile(filepath=str(SRC/'Goblin_Parts_Redesign.blend'));SCENE=bpy.context.scene
lib=Path(r'C:/Users/codex/ByteEngine/Tools/GoblinRefined/build_refined.py').read_text();a=lib.index('def glb_json');b=lib.index('bpy.ops.wm.save_as_mainfile',a);exec(compile(lib[a:b],'own_export','exec'),globals())
file='goblin_circular_saw';rig=bpy.data.objects[file+'_rig'];col=rig.users_collection[0];col.hide_viewport=False;root=bpy.data.objects[file]
for o in col.objects:
 if o.type=='MESH':
  vg=o.vertex_groups.get('Root')
  if vg:
   for v in o.data.vertices:
    if v.co.y<-.20 and any(g.group==vg.index and g.weight>.5 for g in v.groups):v.co.y-=.24
for o in col.objects:
 if o.type=='EMPTY' and 'SOCKET_Mount' in o.name:
  matrix=o.matrix_world.copy();matrix.translation.y-=.24;o.matrix_world=matrix
bpy.ops.mesh.primitive_cube_add(size=1,location=(0,-.32,0));o=bpy.context.object;o.scale=(.10,.28,.10)
for c in list(o.users_collection):c.objects.unlink(o)
col.objects.link(o);o.data.materials.append(bpy.data.materials['Goblin_iron']);bpy.context.view_layer.objects.active=o;bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);o.vertex_groups.new(name='Root').add(list(range(len(o.data.vertices))),1,'REPLACE');o.parent=rig;mod=o.modifiers.new('Armature','ARMATURE');mod.object=rig
acts=[(t.strips[0].action,t.strips[0].action.name,False,61) for t in rig.animation_data.nla_tracks];p=types.SimpleNamespace(file=file,rig=rig,col=col,root=root,objects=[o for o in col.objects if o.type=='MESH'],actions=acts);export(p)
for path in [OUT/'parts-catalog.json',OUT.parent/'GarageUI/parts-catalog.json',Path(r'C:/Users/codex/ByteEngine/output/goblin-scraper/selected-contraption-parts.json')]:
 data=json.loads(path.read_text());d=next(p for p in data['parts'] if p['reference_id']==17);d['sockets'][0]['position'][2]+=.24;d['root_bounds_game'][1][2]+=.24;d['bounds_blender_z_up'][0][1]-=.24;path.write_text(json.dumps(data,indent=2))
print('SAW_MOUNT_REPAIRED',flush=True)
