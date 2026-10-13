import bpy
from pathlib import Path
scene=bpy.context.scene;out=Path(r'C:/Users/codex/Downloads/byte engine files/Karlson V2')
rigs=[o for o in scene.objects if o.type=='ARMATURE']
# Tighten the pistol frame/slide mating gap without changing the magazine cavity.
pistol=next(o for o in rigs if o.name.startswith('Pistol'))
for rig in rigs:
 rig.location=(0,0,0)
 rig.animation_data.action=None
 for b in rig.pose.bones:b.location=(0,0,0);b.rotation_quaternion=(1,0,0,0)
 bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
 for obj in rig.children:obj.select_set(True)
 bpy.context.view_layer.objects.active=rig
 for track in rig.animation_data.nla_tracks:track.mute=False
 bpy.ops.export_scene.gltf(filepath=str(out/('Karlson_'+rig.name.split('_')[0]+'.glb')),export_format='GLB',use_selection=True,use_active_scene=True,export_animations=True,export_animation_mode='NLA_TRACKS',export_nla_strips=True,export_anim_slide_to_zero=True,export_force_sampling=True,export_skins=True,export_yup=True)
 for track in rig.animation_data.nla_tracks:track.mute=True
# Arrange the editable source for a useful first opening, after exporting at origin.
from mathutils import Vector
for i,rig in enumerate(rigs):
 rig.location.x=(i-1)*.65
 for child in rig.children:child.hide_render=False
 if rig.name.startswith('Shotgun'):rig.pose.bones['Shell'].scale=(.001,.001,.001)
scene.frame_set(1);camera=scene.camera;camera.data.type='ORTHO';camera.data.ortho_scale=2.2
camera.location=(2.4,2.5,1.6);camera.rotation_euler=(Vector((0,.05,-.02))-camera.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.wm.save_as_mainfile(filepath=str(out/'Karlson_Guns_v2.blend'));result='Only current revision scene exported'
