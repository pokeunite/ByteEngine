from pathlib import Path
import bpy,json,bmesh
root=Path(r'C:\Users\codex\Downloads\parts\refined\besiege-redesign')
bpy.ops.wm.open_mainfile(filepath=str(root/'Goblin_Parts_Redesign.blend'))
manifest=json.loads((root/'parts-catalog.json').read_text())
for part in manifest['parts']:
 seen=set();sockets=[];duplicates=[]
 for s in part['sockets']:
  key=tuple(s['position']+s['normal'])
  if key in seen:duplicates.append(s['name'])
  else:seen.add(key);sockets.append(s)
 if not duplicates:continue
 part['sockets']=sockets
 for name in duplicates:
  obj=bpy.data.objects.get(name)
  if obj:bpy.data.objects.remove(obj,do_unlink=True)
 # Merge exactly coincident double-mount geometry, retaining UV data and bone weights.
 meshes=[o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith(Path(part['file']).stem+'__')]
 for o in meshes:
  bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001);bm.to_mesh(o.data);bm.free();o.data.update()
 rig=bpy.data.objects.get(Path(part['file']).stem+'_rig');col=rig.users_collection[0]
 bpy.ops.object.select_all(action='DESELECT');col.hide_viewport=False
 for o in col.objects:o.select_set(True)
 bpy.context.view_layer.objects.active=rig
 for t in rig.animation_data.nla_tracks:t.mute=False
 bpy.ops.export_scene.gltf(filepath=str(root/part['file']),export_format='GLB',use_selection=True,export_yup=True,export_texcoords=True,export_normals=True,export_materials='EXPORT',export_skins=True,export_animations=True,export_animation_mode='NLA_TRACKS',export_force_sampling=True,export_frame_range=False,export_extras=True,export_cameras=False,export_lights=False,export_apply=False)
 for t in rig.animation_data.nla_tracks:t.mute=True
 print('REPAIRED_CONNECTORS',part['file'],flush=True)
(root/'parts-catalog.json').write_text(json.dumps(manifest,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(root/'Goblin_Parts_Redesign.blend'),compress=True)
print('REPAIR_COMPLETE',flush=True)
