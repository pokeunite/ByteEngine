import bpy,sys
from pathlib import Path
out=Path(sys.argv[sys.argv.index('--')+1]);bpy.ops.wm.open_mainfile(filepath=str(out/'GoblinScraper_RefinedParts.blend'))
col=bpy.data.collections['Spring catapult basket'];rig=next(o for o in col.objects if o.type=='ARMATURE');root=bpy.data.objects['scrap_catapult_basket'];saved=root.location.copy();root.location=(0,0,0)
for o in col.objects:
 if o.type=='MESH':
  if not any(m.type=='ARMATURE' for m in o.modifiers):
   modifier=o.modifiers.new('Rigid mechanical skin','ARMATURE');modifier.object=rig
  bad=[v for v in o.data.vertices if abs(sum(g.weight for g in v.groups)-1)>1e-4]
  print('WEIGHTS',o.name,len(bad),len(o.data.vertices),[(g.name,g.index) for g in o.vertex_groups])
  for v in bad:
   if not v.groups:o.vertex_groups['Root'].add([v.index],1,'REPLACE')
   else:
    total=sum(g.weight for g in v.groups)
    for g in list(v.groups):o.vertex_groups[g.group].add([v.index],g.weight/total,'REPLACE')
for red in [False,True]:
 swaps=[]
 bpy.ops.object.select_all(action='DESELECT')
 for o in col.objects:
  o.select_set(True)
  if red and o.type=='MESH':
   for s in o.material_slots:
    if s.material==bpy.data.materials['Goblin_green']:swaps.append(s);s.material=bpy.data.materials['Goblin_red']
 bpy.context.view_layer.objects.active=rig
 rig.animation_data.action=None
 for t in rig.animation_data.nla_tracks:t.mute=False
 bpy.ops.export_scene.gltf(filepath=str(out/('red_faction' if red else '')/'scrap_catapult_basket.glb'),export_format='GLB',use_selection=True,export_yup=True,export_skins=True,export_animations=True,export_animation_mode='NLA_TRACKS',export_force_sampling=True,export_frame_range=False,export_extra_animations=False,export_extras=True,export_cameras=False,export_lights=False)
 for s in swaps:s.material=bpy.data.materials['Goblin_green']
for t in rig.animation_data.nla_tracks:t.mute=True
root.location=saved;bpy.ops.wm.save_as_mainfile(filepath=str(out/'GoblinScraper_RefinedParts.blend'),compress=True)

