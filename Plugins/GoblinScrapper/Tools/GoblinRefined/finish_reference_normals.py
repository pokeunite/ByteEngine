from pathlib import Path
import bpy,sys,json
sys.path.insert(0,str(Path(__file__).parent))
from reference_material_normals import add_normals
OUT=Path(sys.argv[sys.argv.index('--')+1]);source=OUT/'Goblin_Standard_Flight_v3.blend'
bpy.ops.wm.open_mainfile(filepath=str(source));catalog=json.loads((OUT/'parts-catalog.json').read_text(encoding='utf-8'))
used={m for obj in bpy.data.objects if obj.type=='MESH' for m in obj.data.materials if m};add_normals(used,OUT)
for col in bpy.data.collections:col.hide_viewport=False
bpy.context.scene.frame_set(1)
for part in catalog['parts']:
    stem=Path(part['file']).stem;rig=bpy.data.objects[stem+'_rig'];col=rig.users_collection[0]
    rig.animation_data.action=None if rig.animation_data else None
    if rig.animation_data:
        for track in rig.animation_data.nla_tracks:track.mute=False
    for pb in rig.pose.bones:pb.location=(0,0,0);pb.scale=(1,1,1);pb.rotation_quaternion=(1,0,0,0)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in col.objects:obj.hide_set(False);obj.select_set(True)
    bpy.context.view_layer.objects.active=rig
    bpy.ops.export_scene.gltf(filepath=str(OUT/part['file']),export_format='GLB',use_selection=True,export_yup=True,export_texcoords=True,export_normals=True,export_materials='EXPORT',export_skins=True,export_animations=True,export_animation_mode='NLA_TRACKS',export_force_sampling=True,export_frame_range=False,export_frame_step=1,export_anim_single_armature=True,export_extra_animations=False,export_extras=True,export_cameras=False,export_lights=False,export_apply=False)
    part['normal_maps']=True
    print('REFERENCE_NORMAL_READY',stem,flush=True)
catalog['materials']='Own embedded base colour, roughness/metallic and tangent normal textures.'
(OUT/'parts-catalog.json').write_text(json.dumps(catalog,indent=2),encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(source),compress=True)
print('REFERENCE_NORMAL_COMPLETE',len(catalog['parts']),flush=True)
