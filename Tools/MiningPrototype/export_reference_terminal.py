import bpy,json,struct,math
from pathlib import Path
out=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine/artifacts/reference-terminal');scene=bpy.data.scenes['Company Terminal Reference'];bpy.context.window.scene=scene;scene.frame_set(1);collection=bpy.data.collections['Reference Company Terminal'];rig=bpy.data.objects['CompanyTerminalRig']
bpy.ops.wm.save_as_mainfile(filepath=str(out/'CompanyTerminal.blend'))
bpy.ops.object.select_all(action='DESELECT');copies=[]
for o in list(collection.objects):
 if o.type!='MESH' or o.name.startswith('Presentation '):continue
 c=o.copy();c.data=o.data.copy();collection.objects.link(c);c.select_set(True);copies.append(c)
bpy.context.view_layer.objects.active=copies[0];bpy.ops.object.join();body=bpy.context.object;body.name='CompanyTerminalRuntimeMesh';rig.select_set(True);bpy.context.view_layer.objects.active=rig
triangles=sum(len(p.vertices)-2 for p in body.data.polygons);path=out/'CompanyTerminal.glb'
bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,use_active_scene=True,export_animations=True,export_force_sampling=True,export_animation_mode='ACTIVE_ACTIONS')
bpy.data.objects.remove(body,do_unlink=True)
raw=path.read_bytes();size=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+size]);tail=bytearray(raw[20+size:]);animations=doc.get('animations',[]);assert len(animations)==1
animations[0]['name']='Operate';inputs={s['input']for s in animations[0]['samplers']};start=min(doc['accessors'][i]['min'][0]for i in inputs)
for ai in inputs:
 a=doc['accessors'][ai];v=doc['bufferViews'][a['bufferView']];at=8+v.get('byteOffset',0)+a.get('byteOffset',0);stride=v.get('byteStride',4)
 for i in range(a['count']):t=struct.unpack_from('<f',tail,at+i*stride)[0];struct.pack_into('<f',tail,at+i*stride,max(0,t-start))
 a['min']=[max(0,a['min'][0]-start)];a['max']=[a['max'][0]-start]
payload=json.dumps(doc,separators=(',',':')).encode();payload+=b' '*((-len(payload))%4)
path.write_bytes(struct.pack('<4sII',b'glTF',2,20+len(payload)+len(tail))+struct.pack('<I4s',len(payload),b'JSON')+payload+tail)
report={'triangles':triangles,'bones':len(rig.data.bones),'material_batches':sum(len(m['primitives'])for m in doc['meshes']),'packed_textures':len(doc.get('images',[])),'animation':'Operate','seconds':4}
(out/'model-report.json').write_text(json.dumps(report,indent=2));result=report
