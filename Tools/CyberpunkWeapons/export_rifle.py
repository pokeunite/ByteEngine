import bpy
C=bpy.app.driver_namespace['cyber'];g=C['guns'][2];g.rest();g.rig.location=(0,0,0);bpy.ops.object.select_all(action='DESELECT');g.rig.select_set(True)
for obj in g.meshes:obj.select_set(True)
bpy.context.view_layer.objects.active=g.rig
for track in g.rig.animation_data.nla_tracks:track.mute=False
bpy.ops.export_scene.gltf(filepath=str(C['OUT']/('Cyberpunk_'+g.name+'.glb')),export_format='GLB',use_selection=True,use_active_scene=True,export_animations=True,export_animation_mode='NLA_TRACKS',export_nla_strips=True,export_anim_slide_to_zero=True,export_force_sampling=True,export_skins=True,export_yup=True)
g.rest();result={'export':g.name}
