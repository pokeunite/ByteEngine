import bpy,json,struct,math
from pathlib import Path
from mathutils import Vector
out=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine/artifacts/reference-drill');scene=bpy.data.scenes['Drill Reference'];bpy.context.window.scene=scene;scene.frame_set(1)
rig=bpy.data.objects['MiningDrillRig'];collection=bpy.data.collections['Reference Mining Drill']
# Verify the editable rig and its mechanical motion before exporting.
scene.frame_set(1);bpy.context.view_layer.update();a=rig.pose.bones['Auger'].matrix.copy();h=rig.pose.bones['Carriage'].matrix.copy();m=rig.pose.bones['MotorFan'].matrix.copy()
scene.frame_set(11);bpy.context.view_layer.update();assert rig.pose.bones['Auger'].matrix!=a
assert rig.pose.bones['Carriage'].matrix!=h;assert rig.pose.bones['MotorFan'].matrix!=m
scene.frame_set(1);bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=str(out/'MiningDrill.blend'))
bpy.ops.object.select_all(action='DESELECT');copies=[]
for o in list(collection.objects):
 if o.type!='MESH' or o.name.startswith('Presentation '):continue
 assert all(math.isfinite(c)for v in o.data.vertices for c in v.co),o.name
 c=o.copy();c.data=o.data.copy();collection.objects.link(c);c.select_set(True);copies.append(c)
bpy.context.view_layer.objects.active=copies[0];bpy.ops.object.join();body=bpy.context.object;body.name='MiningDrillRuntimeMesh';rig.select_set(True);bpy.context.view_layer.objects.active=rig
triangles=sum(len(p.vertices)-2 for p in body.data.polygons);vertices=len(body.data.vertices)
path=out/'MiningDrill.glb';bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,use_active_scene=True,export_animations=True,export_force_sampling=True,export_animation_mode='ACTIVE_ACTIONS')
bpy.data.objects.remove(body,do_unlink=True)
raw=path.read_bytes();size=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+size]);animations=doc.get('animations',[]);assert len(animations)==1
animations[0]['name']='Operate';tail=bytearray(raw[20+size:])
# Blender starts at frame 1. Normalize all exported sampler times to 0..3 s
# so a looping game player does not hold the first pose for an extra frame.
inputs={s['input']for s in animations[0]['samplers']}
start=min(doc['accessors'][ai]['min'][0]for ai in inputs)
for ai in inputs:
 accessor=doc['accessors'][ai];view=doc['bufferViews'][accessor['bufferView']];offset=8+view.get('byteOffset',0)+accessor.get('byteOffset',0);stride=view.get('byteStride',4)
 for i in range(accessor['count']):
  value=struct.unpack_from('<f',tail,offset+i*stride)[0];struct.pack_into('<f',tail,offset+i*stride,max(0,value-start))
 accessor['min']=[max(0,accessor['min'][0]-start)];accessor['max']=[accessor['max'][0]-start]
payload=json.dumps(doc,separators=(',',':')).encode();payload+=b' '*((-len(payload))%4)
path.write_bytes(struct.pack('<4sII',b'glTF',2,20+len(payload)+len(tail))+struct.pack('<I4s',len(payload),b'JSON')+payload+tail)
report={'triangles':triangles,'vertices':vertices,'bones':len(rig.data.bones),'materials':sum(len(m['primitives'])for m in doc['meshes']),'animation':'Operate','seconds':3,'motion_verified':['auger','hydraulic carriage','motor fan'],'presentation_ground_excluded':True}
(out/'model-report.json').write_text(json.dumps(report,indent=2));result=report
