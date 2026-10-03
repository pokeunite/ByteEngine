import bpy,json
from pathlib import Path
from mathutils import Vector
root=Path(r'C:\Users\codex\Downloads\parts\refined\standard-flight-v3')
bpy.ops.wm.open_mainfile(filepath=str(root/'Goblin_Standard_Flight_v3.blend'))
p=root/'parts-catalog.json';j=json.loads(p.read_text(encoding='utf-8'))
for row in j['parts']:
 if row['reference_id'] not in [2,40,46,50,60,17,38,39,51,88]:continue
 file=Path(row['file']).stem;obj=bpy.data.objects[file+'__Moving'];g=row['axis_game'];axis=Vector((g[0],-g[2],g[1]));vertices=[v.co.copy() for v in obj.data.vertices]
 radius=max((v-axis*v.dot(axis)).length for v in vertices)
 tyre=[v for v in vertices if (v-axis*v.dot(axis)).length>=radius*.65]
 low=[min(v[i] for v in tyre) for i in range(3)];high=[max(v[i] for v in tyre) for i in range(3)]
 row['moving_collider_game']=[[low[0],low[2],-high[1]],[high[0],high[2],-low[1]]]
 print(row['reference_id'],row['moving_collider_game'],flush=True)
p.write_text(json.dumps(j,indent=2),encoding='utf-8')
j['legacy_catalog']='parts-catalog-v2.json';Path(r'C:\Users\codex\ByteEngine\.artifacts\v3-qa-project\Assets\GarageUI\parts-catalog.json').write_text(json.dumps(j,indent=2),encoding='utf-8')
print('TYRE_COLLIDERS_READY',flush=True)