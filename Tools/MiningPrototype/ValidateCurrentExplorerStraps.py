import bpy,json,math
from mathutils import Vector
scene=bpy.context.scene;rig=bpy.data.objects['PolygonSyntyCharacter'];body=bpy.data.objects['Hazard Pay Explorer']
patch=json.load(open('C:/Users/codex/.codex/worktrees/52af/ByteEngine/artifacts/explorer-strap-weight-patch.json'))
targets={tuple(round(c,3) for c in row['co']) for row in patch};ids=[v.index for v in body.data.vertices if tuple(round(c,3) for c in v.co) in targets]
assert len(ids)==208
for i in ids:
 weights={body.vertex_groups[g.group].name:g.weight for g in body.data.vertices[i].groups}
 assert not any(n.startswith(('Head','Neck','Clavicle','Shoulder','Elbow','Hand')) and w>.00001 for n,w in weights.items())
 assert abs(sum(weights.values())-1)<.00001
results=[]
for clip in ['SHStandIdle','DHStandIdle','SHStandReload','DHStandReload','DHStandShoot','DHRun']:
 action=bpy.data.actions['PolygonSyntyCharacter_'+clip];rig.animation_data.action=action
 if action.slots:rig.animation_data.action_slot=action.slots[0]
 for fraction in [0,.25,.5,.75,1]:
  frame=action.frame_range[0]+(action.frame_range[1]-action.frame_range[0])*fraction;scene.frame_set(int(frame),subframe=frame%1)
  evaluated=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=evaluated.to_mesh()
  points=[body.matrix_world@mesh.vertices[i].co for i in ids]
  assert all(math.isfinite(c) for p in points for c in p)
  assert max((a-b).length for a in points for b in points)<1.25
  evaluated.to_mesh_clear()
 if clip in ['SHStandReload','DHStandReload']:
  scene.frame_set(int(action.frame_range[0]+(action.frame_range[1]-action.frame_range[0])*.5))
  scene.render.filepath='C:/Users/codex/.codex/worktrees/52af/ByteEngine/artifacts/explorer-straps-'+clip+'.png';bpy.ops.render.render(write_still=True)
 results.append({'clip':clip,'sampled_poses':5,'valid':True})
action=bpy.data.actions['PolygonSyntyCharacter_DHStandIdle'];rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0];scene.frame_set(int(action.frame_range[0]))
open('C:/Users/codex/.codex/worktrees/52af/ByteEngine/artifacts/explorer-strap-pose-validation.json','w').write(json.dumps(results,indent=2))
result={'validated_poses':30,'clips':results}
