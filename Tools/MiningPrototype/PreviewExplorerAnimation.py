import bpy, json, math
from mathutils import Vector
rig=bpy.data.objects['PolygonSyntyCharacter'];body=bpy.data.objects['HazardPay Explorer - skinned mesh'];scene=bpy.context.scene
for track in rig.animation_data.nla_tracks:track.mute=True
rig.data.pose_position='POSE'
action=bpy.data.actions['PolygonSyntyCharacter_SHRun'];rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0];scene.frame_set(12)
camera=bpy.data.objects['Explorer preview camera'];camera.location=(2.5,-6,2.2);camera.rotation_euler=(Vector((0,0,.95))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=2.25
scene.render.filepath='C:/Users/codex/.codex/worktrees/52af/ByteEngine/artifacts/explorer-run-test.png';bpy.ops.render.render(write_still=True)
result={'render':scene.render.filepath,'action':action.name,'frame':scene.frame_current,'bone_names_preserved':len(rig.data.bones)}
