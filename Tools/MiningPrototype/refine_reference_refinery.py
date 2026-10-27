import bpy,math
from pathlib import Path
from mathutils import Vector
BASE=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine');OUT=BASE/'artifacts/reference-refinery'
scene=bpy.data.scenes['Refinery Reference'];bpy.context.window.scene=scene;collection=bpy.data.collections['Reference Refinery'];bpy.context.view_layer.active_layer_collection=bpy.context.view_layer.layer_collection.children[collection.name]
rig=bpy.data.objects['RefineryRig']
# Bring the output lip over the skip rim rather than hiding it behind the skip.
for o in collection.objects:
 if o.type!='MESH':continue
 if o.name.startswith(('Output side rail','Output roller','Roller end bearing','Output drive casing','Output motor cap','Reference conveyor ingot')):
  for v in o.data.vertices:v.co.y-=.35
  # Roller meshes are rigid skinned; adjust their rest bones below as well.
bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
for bone in rig.data.edit_bones:
 if bone.name.startswith('Output'):
  bone.head.y-=.35;bone.tail.y-=.35
bpy.ops.object.mode_set(mode='OBJECT')
cam=scene.camera;cam.location=(-6.3,-10.5,7.1);cam.rotation_euler=(Vector((.45,-.65,1.6))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=8.4
# Distinguish the hot viewing window from the ordinary rock load.
ore=bpy.data.materials.new('Refinery ore');ore.diffuse_color=(.30,.12,.035,1);ore.use_nodes=True;ore.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=ore.diffuse_color
for o in collection.objects:
 if o.name.startswith('Angular feed ore'):
  for i,m in enumerate(o.data.materials):
   if m and m.name=='Refinery orange':o.data.materials[i]=ore
p=bpy.data.materials['Refinery orange'].node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(1,.18,.012,1);p.inputs['Emission Color'].default_value=(1,.12,.004,1);p.inputs['Emission Strength'].default_value=3.5
# Hollow-looking stack mouths, retained as separate editable meshes.
for x,z in [(.44,3.245),(1.0,3.005)]:
 bpy.ops.mesh.primitive_cylinder_add(vertices=12,radius=.105,depth=.012,location=(x,.49,z+.007));o=bpy.context.object;o.name='Recessed exhaust mouth';o.data.materials.append(bpy.data.materials['Refinery black']);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);o.vertex_groups.new(name='Root').add(list(range(len(o.data.vertices))),1,'REPLACE');mod=o.modifiers.new('Rigid mechanical part','ARMATURE');mod.object=rig;o.parent=rig
# Save an editable source with reference-only ingots; game uses stock-backed output.
scene.frame_set(1);bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Refinery.blend'))
bpy.ops.object.select_all(action='DESELECT');copies=[]
for o in list(collection.objects):
 if o.type!='MESH' or o.name.startswith('Reference '):continue
 c=o.copy();c.data=o.data.copy();collection.objects.link(c);c.select_set(True);copies.append(c)
bpy.context.view_layer.objects.active=copies[0];bpy.ops.object.join();body=bpy.context.object;body.name='RefineryRuntimeMesh';rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.gltf(filepath=str(OUT/'Refinery.glb'),export_format='GLB',use_selection=True,export_animations=True,export_force_sampling=True,export_animation_mode='ACTIVE_ACTIONS')
bpy.data.objects.remove(body,do_unlink=True)
result={'corrections':'clear output over bin rim, glowing inspection window, non-glowing ore, open stack mouths, clearer product camera'}
