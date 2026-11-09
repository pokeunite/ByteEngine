import bpy,json
from mathutils.kdtree import KDTree
body=bpy.data.objects['Hazard Pay Explorer'];rig=body.parent
backup=body.copy();backup.data=body.data.copy();backup.name='repair_original_strap_weights';bpy.context.scene.collection.objects.link(backup);backup.hide_render=True;backup.hide_set(True)
# The two rear webbing strips were incorrectly bound entirely to Head.
# Include their front halves, which also had neck influence rather than suit weights.
adj=[[] for v in body.data.vertices];same={}
for v in body.data.vertices:
 key=tuple(round(c,3) for c in v.co)
 if key in same:
  i=same[key];adj[i].append(v.index);adj[v.index].append(i)
 else:same[key]=v.index
for e in body.data.edges:
 a,b=e.vertices;adj[a].append(b);adj[b].append(a)
targets=set()
for seed in [20506,20560,20610,20664]:
 stack=[seed]
 while stack:
  i=stack.pop()
  if i in targets:continue
  targets.add(i);stack.extend(adj[i])
assert len(targets)==208, len(targets)
spines={'Hips','Spine_01','Spine_02','Spine_03'}
sources=[]
for v in body.data.vertices:
 weights={body.vertex_groups[g.group].name:g.weight for g in v.groups}
 if v.index not in targets and abs(v.co.x)<23 and 118<v.co.y<162 and sum(weights.get(n,0) for n in spines)>.98 and abs(v.co.z)<12:
  sources.append(v)
tree=KDTree(len(sources))
for i,v in enumerate(sources):tree.insert(v.co,i)
tree.balance();patch=[]
for i in sorted(targets):
 v=body.data.vertices[i];_,nearest,_=tree.find(v.co);source=sources[nearest]
 weights={body.vertex_groups[g.group].name:g.weight for g in source.groups if body.vertex_groups[g.group].name in spines}
 if v.co.y>155:weights={'Spine_03':1.0}
 total=sum(weights.values());weights={n:w/total for n,w in weights.items()}
 for group in body.vertex_groups:group.remove([i])
 for name,weight in weights.items():body.vertex_groups[name].add([i],weight,'REPLACE')
 patch.append({'co':list(v.co),'weights':weights})
open('C:/Users/codex/.codex/worktrees/52af/ByteEngine/artifacts/explorer-strap-weight-patch.json','w').write(json.dumps(patch))
body.data.update()
result={'fixed_vertices':len(targets),'rear_straps_previously_bound_to':'Head','new_weights':'matched torso garment / upper spine; no head or arm influence'}
