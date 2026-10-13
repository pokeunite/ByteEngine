import bpy,shutil
from pathlib import Path
out=Path(r'C:/Users/codex/Downloads/byte engine files');src=out/'Karlson_Guns.blend'
backup=out/'Karlson_Guns_before_optimization.blend'
if not backup.exists():shutil.copy2(src,backup)
bpy.ops.wm.open_mainfile(filepath=str(src))
obj=bpy.data.objects.get('Pistol_Muzzle inset')
if obj:
    for vertex in obj.data.vertices:vertex.co.y+=.014
for name in ['Pistol','Shotgun','Rifle']:
    collection=bpy.data.collections[name];rig=bpy.data.objects[name+'_Rig'];home=rig.location.copy();rig.location=(0,0,0);rig.animation_data.action=None
    for track in rig.animation_data.nla_tracks:track.mute=True
    for bone in rig.pose.bones:bone.location=(0,0,0);bone.rotation_quaternion=(1,0,0,0)
    groups={}
    for part in list(collection.objects):
        if part.type=='MESH':groups.setdefault((part['motion_assembly'],part.data.materials[0].name),[]).append(part)
    for (bone,material),objects in groups.items():
        bpy.ops.object.select_all(action='DESELECT')
        for part in objects:part.select_set(True)
        bpy.context.view_layer.objects.active=objects[0]
        if len(objects)>1:bpy.ops.object.join()
        merged=bpy.context.object;merged.name=name+'_'+bone+'_'+material;merged['motion_assembly']=bone
    bpy.ops.object.select_all(action='DESELECT')
    for part in collection.objects:part.select_set(True)
    bpy.context.view_layer.objects.active=rig
    for track in rig.animation_data.nla_tracks:track.mute=False
    bpy.ops.export_scene.gltf(filepath=str(out/('Karlson_'+name+'.glb')),export_format='GLB',use_selection=True,export_animation_mode='NLA_TRACKS',export_force_sampling=True,export_anim_slide_to_zero=True,export_apply=False)
    for track in rig.animation_data.nla_tracks:track.mute=True
    rig.location=home
    print(name,'merged render assemblies',len(groups))
bpy.context.scene.frame_set(1);bpy.ops.wm.save_as_mainfile(filepath=str(src))
