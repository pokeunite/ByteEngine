import bpy,json,struct
from pathlib import Path
out=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine/artifacts/reference-refinery');scene=bpy.data.scenes['Refinery Reference'];bpy.context.window.scene=scene;scene.frame_set(1)
rig=bpy.data.objects['RefineryRig'];collection=bpy.data.collections['Reference Refinery']
# Clearance for the centre of an ingot above the belt surface.
bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
for name in ['OutputStart','OutputEnd']:
 bone=rig.data.edit_bones[name];bone.head.z=1.60;bone.tail.z=1.72
bpy.ops.object.mode_set(mode='OBJECT');bpy.ops.wm.save_as_mainfile(filepath=str(out/'Refinery.blend'))
bpy.ops.object.select_all(action='DESELECT');copies=[]
for o in list(collection.objects):
 if o.type!='MESH' or o.name.startswith('Reference '):continue
 c=o.copy();c.data=o.data.copy();collection.objects.link(c);c.select_set(True);copies.append(c)
bpy.context.view_layer.objects.active=copies[0];bpy.ops.object.join();body=bpy.context.object;body.name='RefineryRuntimeMesh';rig.select_set(True);bpy.context.view_layer.objects.active=rig
path=out/'Refinery.glb'
bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,use_active_scene=True,export_animations=True,export_force_sampling=True,export_animation_mode='ACTIVE_ACTIONS')
triangles=sum(len(p.vertices)-2 for p in body.data.polygons);bpy.data.objects.remove(body,do_unlink=True)
# Keep the stable engine clip name without renaming another model's Blender action.
data=path.read_bytes();n=struct.unpack_from('<I',data,12)[0];doc=json.loads(data[20:20+n]);animations=doc.get('animations',[])
if len(animations)!=1:raise RuntimeError('Expected one refinery-only active action, found '+str([a.get('name')for a in animations]))
animations[0]['name']='Operate';payload=json.dumps(doc,separators=(',',':')).encode();payload+=b' '*((-len(payload))%4);tail=data[20+n:]
path.write_bytes(struct.pack('<4sII',b'glTF',2,20+len(payload)+len(tail))+struct.pack('<I4s',len(payload),b'JSON')+payload+tail)
result={'animations':[(a['name'],len(a['channels']))for a in animations],'triangles':triangles,'bones':len(rig.data.bones),'active_scene_only':True}
