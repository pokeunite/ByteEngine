"""Weld real cuff openings, then bind the separated T-pose from scratch."""
import bpy,bmesh,collections,json
from mathutils import Vector
from mathutils.kdtree import KDTree
s=bpy.context.scene;body=bpy.data.objects[s['jumpsuit_final_body']];rig=bpy.data.objects[s['jumpsuit_final_rig']];rig.animation_data.action=None;rig.data.pose_position='REST';bpy.context.view_layer.update()
bm=bmesh.new();bm.from_mesh(body.data);bm.faces.ensure_lookup_table();n=int(body.get('cuff_extension_faces',0))
if n:bmesh.ops.delete(bm,geom=list(bm.faces)[-n:],context='FACES')
bm.to_mesh(body.data);bm.free();body['cuff_extension_faces']=0
cloth=set(i for p in body.data.polygons if body.data.materials[p.material_index].name.startswith('Material_25') for i in p.vertices)
tree=KDTree(len(cloth))
for i in cloth:tree.insert(body.data.vertices[i].co,i)
tree.balance();edges=collections.Counter();ids=collections.defaultdict(set)
for p in body.data.polygons:
 if not body.data.materials[p.material_index].name.startswith('Material_29'):continue
 keys=[tuple(round(x,3) for x in body.data.vertices[i].co) for i in p.vertices]
 for k,i in zip(keys,p.vertices):ids[k].add(i)
 for i,k in enumerate(keys):edges[tuple(sorted((k,keys[(i+1)%len(keys)])))]+=1
boundary={k for e,count in edges.items() if count==1 for k in e if abs(k[0])>55}
for k in boundary:
 co,idx,distance=tree.find(Vector(k))
 if distance>15:raise RuntimeError('Cuff opening is too far from sleeve')
 for i in ids[k]:body.data.vertices[i].co=co
bm=bmesh.new();bm.from_mesh(body.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.02);bm.to_mesh(body.data);bm.free();body.data.update()
world=body.matrix_world.copy();body.parent=None;body.matrix_world=world
for m in list(body.modifiers):
 if m.type=='ARMATURE':body.modifiers.remove(m)
body.vertex_groups.clear()
for b in rig.data.bones:b.use_deform=b.name not in ['Root','Root_ExportRoot','Eyes','Eyebrows','Jaw']
bpy.ops.object.select_all(action='DESELECT');body.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.parent_set(type='ARMATURE_AUTO')
cloth=set(i for p in body.data.polygons if body.data.materials[p.material_index].name.startswith('Material_25') for i in p.vertices)
skin=set(i for p in body.data.polygons if body.data.materials[p.material_index].name.startswith('Material_29') for i in p.vertices)
helmet=set(i for p in body.data.polygons if body.data.materials[p.material_index].name.startswith('Material_27') for i in p.vertices)
weighted=[v for v in body.data.vertices if sum(g.weight for g in v.groups)>1e-6];tree=KDTree(len(weighted))
for v in weighted:tree.insert(v.co,v.index)
tree.balance()
for v in body.data.vertices:
 values={body.vertex_groups[g.group].name:g.weight for g in v.groups}
 if not values:
  _,idx,_=tree.find(v.co);values={body.vertex_groups[g.group].name:g.weight for g in body.data.vertices[idx].groups}
 if v.index in helmet:values={'Head':1}
 elif v.index in skin and v.index not in cloth and abs(v.co.x)>55:values={'Hand_'+('L' if v.co.x>0 else 'R'):1}
 else:
  for n in list(values):
   if n.startswith(('Finger_','IndexFinger_','Thumb_')):
    hand='Hand_'+('R' if n.endswith('_1') else 'L');values[hand]=values.get(hand,0)+values.pop(n)
 for gi in [g.group for g in v.groups]:body.vertex_groups[gi].remove([v.index])
 total=sum(values.values())
 for n,w in values.items():(body.vertex_groups.get(n) or body.vertex_groups.new(name=n)).add([v.index],w/total,'REPLACE')
print('Welded cuff boundary positions:',len(boundary),'T-pose rebind vertices:',len(body.data.vertices))
bpy.ops.object.select_all(action='DESELECT');body.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
p=rig.parent
while p:p.select_set(True);p=p.parent
bpy.ops.export_scene.gltf(filepath='C:/Users/codex/Documents/palclone/SourceArt/HazardPayJumpsuit/JumpsuitBind.glb',export_format='GLB',use_selection=True,export_animations=False,export_def_bones=False,export_rest_position_armature=True)
