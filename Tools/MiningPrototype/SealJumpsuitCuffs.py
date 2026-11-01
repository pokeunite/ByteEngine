"""Extend each original glove cuff under its sleeve using its own textured rim."""
import bpy,bmesh,collections,math,pathlib
from mathutils import Vector
s=bpy.context.scene;body=bpy.data.objects[s['jumpsuit_final_body']];rig=bpy.data.objects[s['jumpsuit_final_rig']];rig.animation_data.action=None;rig.data.pose_position='REST'
bm=bmesh.new();bm.from_mesh(body.data);bm.faces.ensure_lookup_table()
remove=[f for f in bm.faces if body.data.materials[f.material_index].name.startswith('Jumpsuit wrist gasket')]
if body.get('cuff_extension_faces',0):remove+=list(bm.faces)[-int(body['cuff_extension_faces']):]
if remove:bmesh.ops.delete(bm,geom=remove,context='FACES')
bm.to_mesh(body.data);bm.free()
edges=collections.Counter();point_ids={};uvs={};material=next(m for m in body.data.materials if m.name.startswith('Material_29'))
for p in body.data.polygons:
 if body.data.materials[p.material_index]!=material:continue
 keys=[tuple(round(x,3) for x in body.data.vertices[i].co) for i in p.vertices]
 for i,k,li in zip(p.vertices,keys,p.loop_indices):point_ids[k]=i;uvs[k]=tuple(body.data.uv_layers.active.data[li].uv)
 for i,k in enumerate(keys):edges[tuple(sorted((k,keys[(i+1)%len(keys)])))]+=1
graph=collections.defaultdict(set)
for (a,b),count in edges.items():
 if count==1 and abs(a[0])>55 and abs(b[0])>55:graph[a].add(b);graph[b].add(a)
seen=set();loops=[]
for first in graph:
 if first in seen:continue
 loop=[];now=first;previous=None
 while now not in seen:
  seen.add(now);loop.append(now);choices=graph[now]-({previous} if previous else set());previous,now=now,next(iter(choices))
 if len(loop)>10:loops.append(loop)
from mathutils.kdtree import KDTree
cloth=set(i for p in body.data.polygons if body.data.materials[p.material_index].name.startswith('Material_25') for i in p.vertices)
rig.data.pose_position='POSE'
action=next(st.action for t in rig.animation_data.nla_tracks for st in t.strips if 'SHStandReload' in st.action.name)
rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0];s.frame_set(int(sum(action.frame_range)/2));bpy.context.view_layer.update()
evaluated=body.evaluated_get(bpy.context.evaluated_depsgraph_get());posed=evaluated.to_mesh();posed_points=[v.co.copy() for v in posed.vertices];evaluated.to_mesh_clear()
tree=KDTree(len(cloth))
for i in cloth:tree.insert(posed_points[i],i)
tree.balance();added=0
for loop in loops:
 center=sum((Vector(k) for k in loop),Vector())/len(loop);sign=1 if center.x>0 else -1;n=len(loop)
 near=[tree.find(posed_points[point_ids[k]])[1] for k in loop]
 verts=[tuple(k) for k in loop]+[tuple(body.data.vertices[i].co) for i in near]
 faces=[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 mesh=bpy.data.meshes.new('Original glove cuff extension');mesh.from_pydata(verts,[],faces);mesh.materials.append(material);mesh.update();uv=mesh.uv_layers.new(name='UVMap')
 for p in mesh.polygons:
  p.use_smooth=True
  for li in p.loop_indices:uv.data[li].uv=uvs[loop[mesh.loops[li].vertex_index%n]]
 obj=bpy.data.objects.new('Original glove cuff extension',mesh);s.collection.objects.link(obj);obj.matrix_world=body.matrix_world.copy();obj.vertex_groups.new(name='Hand_'+('L' if sign>0 else 'R')).add(list(range(n)),1,'REPLACE')
 for j,idx in enumerate(near):
  for g in body.data.vertices[idx].groups:
   name=body.vertex_groups[g.group].name;(obj.vertex_groups.get(name) or obj.vertex_groups.new(name=name)).add([n+j],g.weight,'REPLACE')
 bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);body.select_set(True);bpy.context.view_layer.objects.active=body;bpy.ops.object.join()
 added+=n
body['cuff_extension_faces']=added
rig.animation_data.action=None;rig.data.pose_position='REST'
bpy.ops.object.select_all(action='DESELECT');body.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
p=rig.parent
while p:p.select_set(True);p=p.parent
bpy.ops.export_scene.gltf(filepath='C:/Users/codex/Documents/palclone/SourceArt/HazardPayJumpsuit/JumpsuitBind.glb',export_format='GLB',use_selection=True,export_animations=False,export_def_bones=False,export_rest_position_armature=True)
print('Textured cuff extensions:',len(loops))
