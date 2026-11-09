"""Bake each editable collection to a single static mesh for efficient game instancing."""
import bpy,json
from pathlib import Path
W=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine');OUT=W/'artifacts/camp-decorations';scene=bpy.data.scenes['Hazard Pay Decoration Library'];bpy.context.window.scene=scene;report=[]
for col in sorted([c for c in bpy.data.collections if c.get('asset_name')],key=lambda c:c.name):
 scene.collection.children.link(col);bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get();verts=[];faces=[];indices=[];materials=[]
 for o in col.objects:
  if o.type not in ('MESH','CURVE'):continue
  evaluated=o.evaluated_get(deps);mesh=evaluated.to_mesh();base=len(verts);verts.extend([tuple(o.matrix_world@v.co) for v in mesh.vertices])
  for face in mesh.polygons:
   material=mesh.materials[face.material_index] if len(mesh.materials)>face.material_index else bpy.data.materials['HPD charcoal']
   if material not in materials:materials.append(material)
   faces.append(tuple(base+i for i in face.vertices));indices.append(materials.index(material))
  evaluated.to_mesh_clear()
 mesh=bpy.data.meshes.new(col['asset_name']+' static baked');mesh.from_pydata(verts,[],faces);mesh.update()
 for m in materials:mesh.materials.append(m)
 for f,index in zip(mesh.polygons,indices):f.material_index=index
 temp=bpy.data.objects.new(col['asset_name'],mesh);scene.collection.objects.link(temp);bpy.ops.object.select_all(action='DESELECT');temp.select_set(True);bpy.context.view_layer.objects.active=temp
 bpy.ops.export_scene.gltf(filepath=str(OUT/(col['asset_name']+'.glb')),export_format='GLB',use_selection=True,export_apply=True,export_animations=False)
 report.append({'asset':col['asset_name'],'min':[min(v[i] for v in verts) for i in range(3)],'max':[max(v[i] for v in verts) for i in range(3)],'vertices':len(verts),'faces':len(faces),'materials':len(materials)})
 bpy.data.objects.remove(temp,do_unlink=True);bpy.data.meshes.remove(mesh);scene.collection.children.unlink(col)
(OUT/'game-assets.json').write_text(json.dumps(report,indent=2));bpy.context.window.scene=bpy.data.scenes['Hazard Pay Decorated Camp REVIEW'];print(json.dumps({'optimized_static_props':len(report),'total_faces':sum(a['faces'] for a in report)}))
