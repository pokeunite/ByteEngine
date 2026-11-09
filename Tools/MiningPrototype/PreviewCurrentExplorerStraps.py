import bpy
from mathutils import Vector
rig=bpy.data.objects['PolygonSyntyCharacter'];body=bpy.data.objects['Hazard Pay Explorer'];scene=bpy.context.scene
for t in rig.animation_data.nla_tracks:t.mute=True
action=bpy.data.actions['PolygonSyntyCharacter_DHStandIdle'];rig.animation_data.action=action
if action.slots:rig.animation_data.action_slot=action.slots[0]
scene.frame_set(int(action.frame_range[0]))
camera=bpy.data.objects.get('repair_strap_camera')
if not camera:
 data=bpy.data.cameras.new('repair_strap_camera');camera=bpy.data.objects.new('repair_strap_camera',data);scene.collection.objects.link(camera)
camera.location=(2.6,4.5,2.2);camera.rotation_euler=(Vector((0,0,1.35))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=1.75;scene.camera=camera
scene.render.engine='BLENDER_WORKBENCH';scene.display.shading.light='STUDIO';scene.display.shading.color_type='MATERIAL';scene.display.shading.show_shadows=True;scene.display.shading.show_cavity=True;scene.display.shading.background_type='WORLD';scene.world.color=(.18,.18,.18)
scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
hidden={o:o.hide_render for o in scene.objects}
for o in scene.objects:o.hide_render=o not in [body,rig,camera,rig.parent]
scene.render.filepath='C:/Users/codex/.codex/worktrees/52af/ByteEngine/artifacts/explorer-straps-before.png'
bpy.ops.render.render(write_still=True)
for o,value in hidden.items():o.hide_render=value
result={'image':scene.render.filepath}
