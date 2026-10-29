import bpy, json, math, pathlib
from mathutils import Vector
rig=bpy.data.objects['PolygonSyntyCharacter'];body=bpy.data.objects['HazardPay Explorer - skinned mesh'];scene=bpy.context.scene
original=json.load(open('C:/Users/codex/.codex/worktrees/52af/ByteEngine/artifacts/explorer-rig-inspection.json'))['rigs'][0]
bone_errors=[]
for b in original['bones']:
 now=rig.data.bones.get(b['name'])
 if now is None or (now.parent.name if now.parent else None)!=b['parent'] or max(abs(now.matrix_local[r][c]-b['matrix'][r][c]) for r in range(4) for c in range(4))>1e-5:bone_errors.append(b['name'])
if bone_errors:raise RuntimeError('Source skeleton changed: '+str(bone_errors))
unweighted=[v.index for v in body.data.vertices if sum(g.weight for g in v.groups)<.999]
if unweighted:raise RuntimeError('Unweighted explorer vertices: '+str(unweighted[:20]))
unknown=[g.name for g in body.vertex_groups if g.name not in rig.data.bones]
if unknown:raise RuntimeError('Groups not in source skeleton: '+str(unknown))
for track in rig.animation_data.nla_tracks:track.mute=True
rig.data.pose_position='POSE';results=[]
for name in ['SHStandIdle','SHWalk','SHRun','SHWalkStrafeLeft','SHWalkStrafeRight','SHStandShoot','SHStandReload','DHStandIdle','DHWalk','DHRun','DHWalkStrafeLeft','DHWalkStrafeRight','DHStandShoot','DHStandReload','StandDeath','CrouchDeath']:
 action=bpy.data.actions.get('PolygonSyntyCharacter_'+name)
 if not action:raise RuntimeError('Missing clip '+name)
 rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0]
 samples=[]
 for fraction in [.1,.5,.9]:
  frame=action.frame_range[0]+(action.frame_range[1]-action.frame_range[0])*fraction;scene.frame_set(int(frame),subframe=frame%1)
  obj=body.evaluated_get(bpy.context.evaluated_depsgraph_get());evaluated=obj.to_mesh()
  points=[obj.matrix_world@v.co for v in evaluated.vertices]
  if not all(math.isfinite(x) for p in points for x in p):raise RuntimeError('Invalid deformed vertices: '+name)
  extent=[max(p[i] for p in points)-min(p[i] for p in points) for i in range(3)]
  if max(extent)>3.5:raise RuntimeError('Exploded pose bounds: '+name+str(extent))
  samples.append(extent);obj.to_mesh_clear()
 results.append({'clip':name,'sampled_extents':samples})
# Polish glass while retaining the low-poly rim/silhouette.
for poly in body.data.polygons:
 if body.data.materials[poly.material_index].name.startswith('Explorer / Visor'):poly.use_smooth=True;poly.material_index=body.data.materials.find('Explorer / Visor teal')
p=bpy.data.materials['Explorer / Visor teal'].node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(.009,.055,.072,1);p.inputs['Roughness'].default_value=.23;p.inputs['Metallic'].default_value=.28;p.inputs['Coat Weight'].default_value=.3
rig.animation_data.action=None;rig.data.pose_position='REST';scene.frame_set(0)
folder=pathlib.Path('C:/Users/codex/Documents/palclone/SourceArt/HazardPayExplorer');folder.mkdir(parents=True,exist_ok=True)
report={'source_skeleton_unchanged':True,'bones':len(rig.data.bones),'vertices':len(body.data.vertices),'unweighted_vertices':len(unweighted),'clips':results,'source_tracks':len(rig.animation_data.nla_tracks),'fps':scene.render.fps}
(folder/'animation-validation.json').write_text(json.dumps(report,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(folder/'HazardPayExplorer.blend'))
bpy.ops.object.select_all(action='DESELECT');body.select_set(True);rig.select_set(True)
root=rig.parent
while root:root.select_set(True);root=root.parent
bpy.context.view_layer.objects.active=rig
rig.data.pose_position='POSE'
bpy.ops.export_scene.gltf(filepath=str(folder/'HazardPayExplorer.glb'),export_format='GLB',use_selection=True,export_animations=True,export_animation_mode='ACTIONS',export_merge_animation='ACTION',export_force_sampling=True,export_def_bones=False,export_rest_position_armature=True,export_apply=False)
rig.data.pose_position='REST'
result={'blend':str(folder/'HazardPayExplorer.blend'),'glb':str(folder/'HazardPayExplorer.glb'),'tested_clips':len(results),'bones':len(rig.data.bones),'unweighted_vertices':len(unweighted)}
