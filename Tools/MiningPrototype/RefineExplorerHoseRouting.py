import bpy,bmesh,pathlib,math,random
from mathutils import Vector,Matrix
rig=bpy.data.objects['PolygonSyntyCharacter'];body=bpy.data.objects['HazardPay Explorer - skinned mesh'];collection=body.users_collection[0]
bm=bmesh.new();bm.from_mesh(body.data);seen=set();remove=[]
for start in bm.verts:
 if start in seen:continue
 group=[];stack=[start];seen.add(start)
 while stack:
  v=stack.pop();group.append(v)
  for edge in v.link_edges:
   other=edge.other_vert(v)
   if other not in seen:seen.add(other);stack.append(other)
 center=sum((v.co for v in group),Vector())/len(group);mats={body.data.materials[f.material_index].name for v in group for f in v.link_faces}
 if 5<abs(center.x)<19 and 110<center.y<121 and center.z>2 and any(s.endswith(('Graphite','Steel','Rubber')) for s in mats):remove.extend(group)
bmesh.ops.delete(bm,geom=list(set(remove)),context='VERTS');bm.to_mesh(body.data);bm.free()
parts=[];materials={m.name.split('Explorer / ',1)[1]:m for m in bpy.data.materials if m.name.startswith('Explorer / ')}
source=pathlib.Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine/Tools/MiningPrototype/BuildExplorerCharacter.py').read_text();exec(source[source.index('def mesh('):source.index('# Connected torso,')])
repair=pathlib.Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine/Tools/MiningPrototype/DetailExplorerHosesAndLegs.py').read_text();exec(repair[repair.index('def curved_hose('):repair.index('def band(')])
for side in [-1,1]:curved_hose(side)
bpy.ops.object.select_all(action='DESELECT');body.select_set(True)
for part in parts:part.select_set(True)
bpy.context.view_layer.objects.active=body;bpy.ops.object.join()
# Keep the posed source rig out of the way when reviewing the model.
rig.show_in_front=False;body['reference_rebuild']=4
scene=bpy.context.scene;camera=bpy.data.objects['Explorer preview camera'];camera.location=(1.2,-5,1.35);camera.rotation_euler=(Vector((0,0,1.15))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=1.6
scene.render.filepath='C:/Users/codex/.codex/worktrees/52af/ByteEngine/artifacts/explorer-reference-v4-hoses-legs.png';bpy.ops.render.render(write_still=True)
result={'render':scene.render.filepath,'vertices':len(body.data.vertices)}
