import bpy
C=bpy.app.driver_namespace['cyber'];scene=bpy.context.scene;g=C['guns'][0];camera=scene.camera
from mathutils import Vector
for other in C['guns']:
 for obj in other.meshes:obj.hide_render=other!=g
g.rest();g.rig.animation_data.action=g.actions['ReloadEmpty'];scene.frame_set(150);bpy.context.view_layer.update();target=Vector((0,-.20,-.12));camera.data.type='ORTHO';camera.data.ortho_scale=.88;camera.location=target+Vector((1.3,1.3,.8));camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();scene.render.resolution_x=1280;scene.render.resolution_y=900
scene.render.filepath=str(C['OUT']/'Previews'/'Pistol_FinalRack.png');bpy.ops.render.render(write_still=True);g.rest();result='Pistol grasp rendered'
