import bpy
from pathlib import Path
from mathutils import Vector
out=Path(r'C:/Users/codex/Downloads/byte engine files');bpy.ops.wm.open_mainfile(filepath=str(out/'Karlson_Guns.blend'))
for name,y,z in [('Pistol',.165,.024),('Shotgun',.536,.028),('Rifle',.472,.021)]:
    rig=bpy.data.objects[name+'_Rig'];home=rig.location.copy();rig.location=(0,0,0)
    bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
    bpy.ops.object.mode_set(mode='EDIT')
    bone=rig.data.edit_bones.get('MuzzleSocket') or rig.data.edit_bones.new('MuzzleSocket');bone.head=(0,y,z);bone.tail=(0,y,z+.025);bone.parent=rig.data.edit_bones['Frame']
    bpy.ops.object.mode_set(mode='OBJECT');rig.animation_data.action=None
    for bone in rig.pose.bones:bone.location=(0,0,0);bone.rotation_mode='QUATERNION';bone.rotation_quaternion=(1,0,0,0)
    for obj in bpy.data.collections[name].objects:obj.select_set(True)
    for track in rig.animation_data.nla_tracks:track.mute=False
    bpy.ops.export_scene.gltf(filepath=str(out/('Karlson_'+name+'.glb')),export_format='GLB',use_selection=True,export_animation_mode='NLA_TRACKS',export_force_sampling=True,export_anim_slide_to_zero=True,export_apply=False)
    for track in rig.animation_data.nla_tracks:track.mute=True
    rig.location=home
bpy.context.scene.frame_set(1);bpy.ops.wm.save_as_mainfile(filepath=str(out/'Karlson_Guns.blend'))
print('All three guns have muzzle socket bones.')
