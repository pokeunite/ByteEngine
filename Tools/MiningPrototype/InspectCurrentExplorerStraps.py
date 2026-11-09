import bpy,json
from collections import Counter
body=bpy.data.objects['Hazard Pay Explorer'];rig=body.parent
adj=[[] for v in body.data.vertices]
coincident={}
for v in body.data.vertices:
 key=tuple(round(x,3) for x in v.co)
 if key in coincident:
  other=coincident[key];adj[v.index].append(other);adj[other].append(v.index)
 else:coincident[key]=v.index
for e in body.data.edges:
 a,b=e.vertices;adj[a].append(b);adj[b].append(a)
seen=set();rows=[]
for v in body.data.vertices:
 if v.index in seen:continue
 stack=[v.index];seen.add(v.index);ids=[]
 while stack:
  i=stack.pop();ids.append(i)
  for j in adj[i]:
   if j not in seen:seen.add(j);stack.append(j)
 pts=[body.data.vertices[i].co for i in ids]
 groups=Counter()
 for i in ids:
  for g in body.data.vertices[i].groups:groups[body.vertex_groups[g.group].name]+=g.weight
 rows.append({'seed':min(ids),'n':len(ids),'min':[min(p[a] for p in pts) for a in range(3)],'max':[max(p[a] for p in pts) for a in range(3)],'weights':groups.most_common(4)})
open('C:/Users/codex/.codex/worktrees/52af/ByteEngine/artifacts/explorer-strap-islands.json','w').write(json.dumps(rows,indent=2))
result={'objects':[o.name for o in bpy.context.scene.objects],'matrix':str(body.matrix_world),'materials':[m.name for m in body.data.materials],'islands':len(rows),'bones':[b.name for b in rig.data.bones]}
