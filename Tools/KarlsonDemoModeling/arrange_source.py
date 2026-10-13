import bpy
from mathutils import Vector
scene=bpy.context.scene;rigs=[o for o in scene.objects if o.type=='ARMATURE']
for i,rig in enumerate(rigs):
 rig.location.x=(i-1)*.65;rig.animation_data.action=None
 for bone in rig.pose.bones:bone.location=(0,0,0);bone.rotation_quaternion=(1,0,0,0)
 for child in rig.children:child.hide_render=False
 if rig.name.startswith('Shotgun'):rig.pose.bones['Shell'].scale=(.001,.001,.001)
scene.frame_set(1);camera=scene.camera;camera.data.type='ORTHO';camera.data.ortho_scale=2.2
camera.location=(2.4,2.5,1.6);camera.rotation_euler=(Vector((0,.05,-.02))-camera.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.wm.save_as_mainfile(filepath=r'C:/Users/codex/Downloads/byte engine files/Karlson V2/Karlson_Guns_v2.blend')
scene.render.filepath=r'C:/Users/codex/Downloads/byte engine files/Karlson V2/Previews/All_guns_v2.png';bpy.ops.render.render(write_still=True)
result='Source arranged and saved; exported game transforms unchanged'
