import bpy
from mathutils import Vector
from pathlib import Path
scene=bpy.context.scene
for i,name in enumerate(['convoy-cargo','convoy-turret','convoy-barrier','salvage-sign']):
 obj=bpy.data.objects.get(name);obj.location=(i*4,0,0)
for name in ['Cube','Camera','Light']:
 obj=bpy.data.objects.get(name)
 if obj:bpy.data.objects.remove(obj,do_unlink=True)
bpy.ops.mesh.primitive_plane_add(size=100,location=(6,0,-.03));ground=bpy.context.object
mat=bpy.data.materials.new('Convoy preview sand');mat.diffuse_color=(.45,.38,.25,1);ground.data.materials.append(mat)
bpy.ops.object.camera_add(location=(18,-18,16));camera=bpy.context.object;camera.rotation_euler=(Vector((6,0,1))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=18;scene.camera=camera
bpy.ops.object.light_add(type='AREA',location=(7,-10,14));bpy.context.object.data.energy=2300;bpy.context.object.data.shape='DISK';bpy.context.object.data.size=12
bpy.ops.object.light_add(type='SUN');bpy.context.object.data.energy=2;bpy.context.object.rotation_euler=(.35,-.4,-.6)
scene.render.engine='BLENDER_EEVEE_NEXT';scene.render.resolution_x=1600;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.render.filepath='C:/Users/codex/Documents/DuneCompany/Docs/CombatConcepts/Convoy-models.png'
bpy.ops.render.render(write_still=True)
result={'render':scene.render.filepath}
