import bpy,pathlib,json,math
from mathutils import Vector
s=bpy.context.scene;body=bpy.data.objects[s['jumpsuit_final_body']];rig=bpy.data.objects[s['jumpsuit_final_rig']]
rig.animation_data.action=None;rig.data.pose_position='REST'
for t in rig.animation_data.nla_tracks:t.mute=True
cloth=set(i for p in body.data.polygons if body.data.materials[p.material_index].name.startswith('Material_25') for i in p.vertices)
skin=set(i for p in body.data.polygons if body.data.materials[p.material_index].name.startswith('Material_29') for i in p.vertices)
count=0
for i in cloth|skin:
 v=body.data.vertices[i]
 if abs(v.co.x)<48 or v.co.y<126:continue
 side='L' if v.co.x>0 else 'R';wrist=abs(rig.data.bones['Hand_'+side].head_local.x)
 t=max(0,min(1,(abs(v.co.x)-50)/10));t=t*t*(3-2*t)
 if i in skin:t=1
 for g in list(v.groups):body.vertex_groups[g.group].remove([i])
 for n,w in [('Elbow_'+side,1-t),('Hand_'+side,t)]:
  if w:(body.vertex_groups.get(n) or body.vertex_groups.new(name=n)).add([i],w,'REPLACE')
 count+=1
# Match the actual openings, not just the wrist-joint locations. The imported
# standing mesh has an off-axis glove cuff on each hand.
trim=set(i for p in body.data.polygons if body.data.materials[p.material_index].name.startswith('Material_26') for i in p.vertices)
collars=[]
for sign in [-1,1]:
 glove=[body.data.vertices[i] for i in skin if body.data.vertices[i].co.x*sign>55]
 sleeve=[body.data.vertices[i] for i in cloth if body.data.vertices[i].co.x*sign>55]
 gx=min(v.co.x*sign for v in glove);cx=max(v.co.x*sign for v in sleeve)
 gp=[v.co for v in glove if v.co.x*sign<gx+2];cp=[v.co for v in sleeve if v.co.x*sign>cx-2]
 gc=sum(gp,Vector())/len(gp);cc=sum(cp,Vector())/len(cp);delta=cc-gc
 for v in glove:v.co+=delta
 for i in trim:
  v=body.data.vertices[i]
  if v.co.x*sign>65 and v.co.y>126:
   v.co+=delta
   for g in list(v.groups):body.vertex_groups[g.group].remove([i])
   (body.vertex_groups.get('Hand_'+('L' if sign>0 else 'R')) or body.vertex_groups.new(name='Hand_'+('L' if sign>0 else 'R'))).add([i],1,'REPLACE')
 if not any(m and m.name.startswith('Jumpsuit wrist gasket') for m in body.data.materials):collars.append((sign,cc))
for sign,cc in collars:
 vertices=[tuple(cc+Vector((sign*x,4.2*math.cos(i*math.tau/12),4.2*math.sin(i*math.tau/12)))) for x in [-6,4] for i in range(12)]
 faces=[(i,(i+1)%12,(i+1)%12+12,i+12) for i in range(12)]
 mesh=bpy.data.meshes.new('Flexible sealed wrist');mesh.from_pydata(vertices,[],faces);mesh.update();obj=bpy.data.objects.new('Flexible sealed wrist',mesh);s.collection.objects.link(obj);obj.matrix_world=body.matrix_world.copy()
 mat=bpy.data.materials.get('Jumpsuit wrist gasket') or bpy.data.materials.new('Jumpsuit wrist gasket');mat.diffuse_color=(.025,.029,.032,1);mat.use_nodes=True;p=mat.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(.025,.029,.032,1);p.inputs['Roughness'].default_value=.7;mesh.materials.append(mat)
 obj.vertex_groups.new(name='Hand_'+('L' if sign>0 else 'R')).add(list(range(24)),1,'REPLACE')
 bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);body.select_set(True);bpy.context.view_layer.objects.active=body;bpy.ops.object.join()
root=pathlib.Path('C:/Users/codex/Documents/palclone/SourceArt/HazardPayJumpsuit')
bpy.ops.object.select_all(action='DESELECT');body.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
p=rig.parent
while p:p.select_set(True);p=p.parent
bpy.ops.export_scene.gltf(filepath=str(root/'JumpsuitBind.glb'),export_format='GLB',use_selection=True,export_animations=False,export_def_bones=False,export_rest_position_armature=True)
(root/'wrist-repair.json').write_text(json.dumps({'cuff_and_glove_vertices_reweighted':count,'cuff_end_uses_same_hand_as_glove':True},indent=2))
print('Reweighted sleeve cuffs and gloves:',count)
