import bpy,json,math
C=bpy.app.driver_namespace['cyber'];scene=bpy.context.scene;audit=[]
for g in C['guns']:
 g.rest();max_contact=0;min_groups=99;max_weight_error=0
 for mesh in g.meshes:
  mesh.data.calc_loop_triangles()
  for v in mesh.data.vertices:
   min_groups=min(min_groups,len(v.groups));max_weight_error=max(max_weight_error,abs(sum(x.weight for x in v.groups)-1))
 for clip,action in g.actions.items():
  g.rig.animation_data.action=action
  for frame in range(1,int(action.frame_range[1])+1,3):
   scene.frame_set(frame);bpy.context.view_layer.update()
   for side in ['Right','Left']:
    hand=g.rig.pose.bones[side+'Hand'].matrix.translation;target=g.rig.pose.bones[side+'WristControl'].matrix.translation;max_contact=max(max_contact,(hand-target).length)
   for bone in g.rig.pose.bones:
    assert all(math.isfinite(v) for row in bone.matrix for v in row)
 g.rest();audit.append({'gun':g.name,'triangles':sum(len(m.data.loop_triangles) for m in g.meshes),'meshes':len(g.meshes),'bones':len(g.rig.data.bones),'clips':list(g.actions),'minimum_vertex_influences':min_groups,'max_weight_sum_error':max_weight_error,'maximum_wrist_contact_error_m':max_contact})
(C['OUT']/'source_validation.json').write_text(json.dumps(audit,indent=2));result=audit
