import bpy,bmesh,json,math
from pathlib import Path
OUT=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine/artifacts/spider-models');result=[]
for kind in ['PlayerHunter','DrillBreaker']:
 col=bpy.data.collections['SPIDER_'+kind];legs=[o for o in col.objects if o.get('anatomy')=='walking_leg'];assert len(legs)==8
 assert sorted((o['side'],o['pair']) for o in legs)==[(side,pair) for side in [-1,1] for pair in range(1,5)]
 findings=[]
 for o in col.objects:
  if o.type!='MESH':continue
  bm=bmesh.new();bm.from_mesh(o.data);bad=sum(not e.is_manifold for e in bm.edges);assert bad==0,(kind,o.name,bad)
  assert bm.calc_volume()>0,(kind,o.name,'inverted volume');assert all(math.isfinite(c) for v in bm.verts for c in v.co)
  if o in legs:
   bm.verts.ensure_lookup_table()
   seen=set();todo=[bm.verts[0]]
   while todo:
    v=todo.pop()
    if v in seen:continue
    seen.add(v);todo.extend(e.other_vert(v) for e in v.link_edges)
   assert len(seen)==len(bm.verts),(kind,o.name,'disconnected joint');findings.append({'leg':o.name,'vertices':len(bm.verts),'closed':True,'connectedThroughJoints':True})
  bm.free()
 assert not any(o.type=='ARMATURE' for o in col.objects)
 result.append({'id':kind,'walkingLegs':8,'left':4,'right':4,'limbs':findings,'allSourcePartsClosed':True,'unrigged':True})
(OUT/'topology-verification.json').write_text(json.dumps(result,indent=2))
