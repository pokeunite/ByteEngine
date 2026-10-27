import bpy
from pathlib import Path
from mathutils import Vector
out=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine/artifacts/reference-press')
scene=bpy.context.scene;cam=scene.camera
cam.location=(-6,-8,4.4);cam.rotation_euler=(Vector((-.05,-1.0,1.65))-cam.location).to_track_quat('-Z','Y').to_euler()
for name,color,metal,rough in [('yellow',(.72,.34,.012,1),.18,.39),('steel',(.12,.15,.19,1),.65,.34),('ingot',(.40,.43,.47,1),.75,.28),('dark',(.025,.034,.045,1),.4,.43),('black',(.012,.016,.020,1),.05,.65)]:
 m=bpy.data.materials['Press '+name];m.diffuse_color=color;p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=color;p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
scene.view_settings.exposure=-.35
bpy.ops.wm.save_as_mainfile(filepath=str(out/'AmmoPress.blend'))
# Export one skinned mesh/material batches, while retaining separate editable source parts.
collection=bpy.data.collections['Reference Ammo Press'];originals=[o for o in collection.objects if o.type=='MESH'];rig=bpy.data.objects['AmmoPressRig']
bpy.ops.object.select_all(action='DESELECT');copies=[]
for o in originals:
 c=o.copy();c.data=o.data.copy();collection.objects.link(c);c.select_set(True);copies.append(c)
bpy.context.view_layer.objects.active=copies[0];bpy.ops.object.join();body=bpy.context.object;body.name='AmmoPressRuntimeMesh'
rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.gltf(filepath=str(out/'AmmoPress.glb'),export_format='GLB',use_selection=True,export_animations=True,export_force_sampling=True,export_animation_mode='ACTIONS')
bpy.data.objects.remove(body,do_unlink=True)
result={'corrections':'camera exposes flywheel, richer yellow, steel material separation, merged runtime mesh','source':str(out/'AmmoPress.blend')}
