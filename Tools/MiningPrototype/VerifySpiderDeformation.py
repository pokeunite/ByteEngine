import bpy,json,math
from pathlib import Path
report=[];s=bpy.data.scenes['Spider Rig Repair'];bpy.context.window.scene=s
for kind in ['PlayerHunter','DrillBreaker']:
 a=bpy.data.objects[kind+' Mesh2Motion rig'];mesh=next(o for o in bpy.data.collections['RIGGED_'+kind].objects if o.type=='MESH');maxr=0;shaft=0;count=0;worst=None
 for clip in ['Idle','Walk','Jump','Attack','Death']:
  action=bpy.data.actions[kind+'_'+clip];a.animation_data.action=action
  for f in range(int(action.frame_range[0]),int(action.frame_range[1])+1,3):
   s.frame_set(f);dg=bpy.context.evaluated_depsgraph_get();evaluated=mesh.evaluated_get(dg);me=evaluated.to_mesh();coords=[v.co.copy() for v in me.vertices]
   assert all(math.isfinite(v) for p in coords for v in p)
   assert max(p.length for p in coords)<8
   for e in mesh.data.edges:
    i,j=e.vertices;rest=(mesh.data.vertices[i].co-mesh.data.vertices[j].co).length
    if rest>.025:
     ratio=(coords[i]-coords[j]).length/rest
     if ratio>maxr:maxr=ratio;worst={'clip':clip,'frame':f,'edge':[i,j],'weights':[[[mesh.vertex_groups[g.group].name,round(g.weight,3)] for g in mesh.data.vertices[v].groups] for v in [i,j]]}
     wi=[(g.group,round(g.weight,5)) for g in mesh.data.vertices[i].groups];wj=[(g.group,round(g.weight,5)) for g in mesh.data.vertices[j].groups]
     if wi==wj:shaft=max(shaft,ratio)
   evaluated.to_mesh_clear();count+=1
 report.append({'model':kind,'sampledPoses':count,'maximumJointMembraneStretch':maxr,'maximumRigidShaftStretch':shaft,'worst':worst,'skinWeightsNormalized':all(abs(sum(g.weight for g in v.groups)-1)<.0001 for v in mesh.data.vertices)})
 assert shaft<1.02,'Rigid shell or shaft deformation'
Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine/artifacts/spider-models/animation-deformation-check.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
