import bpy, json
path='C:/Users/codex/Downloads/byte engine files/synty new.glb'
before=set(bpy.data.objects)
bpy.ops.import_scene.gltf(filepath=path)
created=set(bpy.data.objects)-before
collection=bpy.data.collections.new('Explorer animation reference - preserved')
bpy.context.scene.collection.children.link(collection)
for o in created:
 for c in list(o.users_collection):c.objects.unlink(o)
 collection.objects.link(o)
armatures=[o for o in created if o.type=='ARMATURE']
result={'objects':[(o.name,o.type,list(o.dimensions),list(o.scale)) for o in created], 'actions':[(a.name,a.frame_range[:]) for a in bpy.data.actions], 'rigs':[]}
for rig in armatures:
 rig.data.pose_position='REST'
 result['rigs'].append({'name':rig.name,'matrix':[list(row) for row in rig.matrix_world], 'bones':[{'name':b.name,'parent':b.parent.name if b.parent else None,'head':list(b.head_local),'tail':list(b.tail_local),'matrix':[list(r) for r in b.matrix_local]} for b in rig.data.bones]})
open('C:/Users/codex/.codex/worktrees/52af/ByteEngine/artifacts/explorer-rig-inspection.json','w').write(json.dumps(result,indent=2))
result={'rigs':[(r['name'],len(r['bones'])) for r in result['rigs']],'objects':result['objects'],'actions':len(bpy.data.actions)}
