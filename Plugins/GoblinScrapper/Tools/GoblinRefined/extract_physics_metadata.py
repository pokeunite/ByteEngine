import bpy,json
from pathlib import Path
from mathutils import Vector
src=Path(r'C:\Users\codex\Downloads\parts\refined\standard-flight-v2');target=Path(r'C:\Users\codex\Documents\Goblin Scraper\Assets\ContraptionParts\parts-catalog.json');bpy.ops.wm.open_mainfile(filepath=str(src/'Goblin_Parts_Redesign.blend'));j=json.loads(target.read_text())
def game(v):return [float(v[0]),float(v[2]),float(-v[1])]
for p in j['parts']:
 rig=bpy.data.objects[Path(p['file']).stem+'_rig'];col=rig.users_collection[0];groups={}
 for o in col.objects:
  if o.type!='MESH':continue
  names={g.index:g.name for g in o.vertex_groups}
  for v in o.data.vertices:
   for w in v.groups:
    if w.weight>.5:groups.setdefault(names[w.group],[]).append(Vector(game(o.matrix_world@v.co)))
 def bounds(points):return [[min(v[i] for v in points) for i in range(3)],[max(v[i] for v in points) for i in range(3)]]
 p['root_bounds_game']=bounds(groups.get('Root') or [Vector(p['bounds_blender_z_up'][0]),Vector(p['bounds_blender_z_up'][1])])
 if groups.get('Moving'):p['moving_bounds_game']=bounds(groups['Moving'])
 if rig.data.bones.get('Moving'):p['pivot_game']=game(rig.data.bones['Moving'].head_local)
 p['axis_game']=[1,0,0] if p['reference_id'] in [2,5,17,28,38,40,46,50,51,60,86,88] else [0,1,0] if p['reference_id'] in [27,77] else [0,0,-1]
 p['mass']=80 if p['reference_id']==35 else 8 if p['reference_id']==0 else 5 if p['kind'] in ['beam','panel','surface','wing','wingpanel','rudder','steeringfin'] else 30 if p['reference_id'] in [46,60] else 12 if p['kind'] in ['wheel','caster','skate','flywheel','gear'] else 1 if p['kind'] in ['balloon','parachute','propeller'] else 15
 p['runtime_status']='prototype-physics'
target.write_text(json.dumps(j,indent=2));Path(r'C:\Users\codex\Documents\Goblin Scraper\Assets\GarageUI\parts-catalog.json').write_text(json.dumps(j,indent=2));Path(r'C:\Users\codex\ByteEngine\output\goblin-scraper\selected-contraption-parts.json').write_text(json.dumps(j,indent=2));print('PHYSICS_METADATA',len(j['parts']))
