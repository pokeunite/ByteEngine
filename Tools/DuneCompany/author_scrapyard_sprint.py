"""Author a winding desert sprint with ordered gates and one physical opponent."""
import copy,json,uuid,math,importlib.util,sys
from pathlib import Path
from PIL import Image
import numpy as np
ROOT=Path(sys.argv[1]) if len(sys.argv)>1 else Path('C:/Users/codex/Documents/DuneCompany')
spec=importlib.util.spec_from_file_location('levels',Path(__file__).with_name('author_starter_levels.py'));m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
PATH=[(0,-12),(0,-28),(8,-42),(24,-48),(44,-44),(56,-28),(56,-4),(48,18),(28,30),(6,34),(-18,28),(-38,12),(-44,-12),(-36,-34),(-20,-48),(-6,-36),(0,-18),(0,0)]
ROUTE=[(x,z,1.1+.8*math.sin(i*.6)) for i,(x,z) in enumerate(PATH)]
def height(x,z):
 d,y=m.route(x,z,ROUTE);base=2.2+.9*math.sin(x*.13)*math.cos(z*.11);h=y+(base-y)*m.smooth((d-7)/6)
 if math.hypot(x,z)<14:h*=m.smooth((math.hypot(x,z)-8)/6)
 return h
folder=ROOT/'Assets/ContractLevels';folder.mkdir(exist_ok=True)
image=np.array([[round((height(x-96,z-96)+3)/16*65535) for x in range(193)] for z in range(193)],dtype=np.uint16);Image.fromarray(image).save(folder/'sprint-height.png')
packed=np.array([[65535 if m.route(x-96,z-96,ROUTE)[0]<7 else 16000 for x in range(193)] for z in range(193)],dtype=np.uint16);Image.fromarray(packed).save(folder/'sprint-packed.png')
scene=json.loads((ROOT/'Scenes/Workshop.bytescene').read_text(encoding='utf-8-sig'));scene['name']='Scrapyard Sprint';scene['sceneId']=str(uuid.uuid4())
rock_template=next((copy.deepcopy(o) for o in scene['gameObjects'] if o['name'].startswith('Contract landscape / sandstone')),None)
scene['gameObjects']=[o for o in scene['gameObjects'] if not any(c['type'].endswith(('DuneRecoveryContract3D','DuneSalvageRoute3D')) for c in o.get('components',[])) and not o['name'].startswith(('route post','western detour','riverbed canyon','depot entrance','abandoned equipment','terrace retaining','Contract landscape /','Desert dressing /'))]
rock=next((copy.deepcopy(o) for o in scene['gameObjects'] if any('rock' in c.get('properties',{}).get('mesh',{}).__str__().lower() for c in o.get('components',[])) and 'garage' not in o['name'].lower()),None)
for o in scene['gameObjects']:
 for c in o.get('components',[]):
  if c['type'].endswith(('DesertTerrain3D','InteractiveSand3D')):
   c['properties'].update(useReferenceSandSurface=False,cells=192,spacing=1,heightmapPath='Assets/ContractLevels/sprint-height.png',packedSurfaceMaskPath='Assets/ContractLevels/sprint-packed.png',heightmapHeight=16,baseHeight=-3,heightmapHash='',sandState='',sculptState='',autoLoadSavedSand=False)
points=[[x,height(x,z)+.8,z] for x,z in PATH]
obj=m.object_('Sprint route and rules',0,0,0,1,1,1,[1,1,1,0]);obj['components']=[{'type':'RoutePath3D','properties':{'waypointsJson':json.dumps(points),'corridorWidth':14,'gateRadius':6}},{'type':'bytebard.dunecompany.DuneSprintContract3D','properties':{'opponentSpeed':22,'opponentBlueprint':'Assets/RecoveryBlueprints/Recovery-demo.json'}}];scene['gameObjects'].append(obj)
for i,(x,z) in enumerate(PATH):
 a=PATH[max(0,i-1)];b=PATH[min(len(PATH)-1,i+1)];dx,dz=b[0]-a[0],b[1]-a[1];length=max(.01,math.hypot(dx,dz));sx,sz=-dz/length,dx/length
 for sign in [-1,1]:
  px,pz=x+sx*7*sign,z+sz*7*sign;scene['gameObjects'].append(m.object_(f'Sprint gate {i+1} / {sign}',px,height(px,pz)+.8,pz,.16,1.6,.16,[.95,.55,.1,1],0,False))
if rock_template:
 for i,(x,z) in enumerate(PATH):
  if i<2 or i>15:continue
  # Keep scanned boulders beyond the usable corridor and other course crossings.
  for side in [-1,1]:
   a=PATH[max(0,i-1)];b=PATH[min(len(PATH)-1,i+1)];dx,dz=b[0]-a[0],b[1]-a[1];length=max(.01,math.hypot(dx,dz));px,pz=x-dz/length*15*side,z+dx/length*15*side
   if m.route(px,pz,ROUTE)[0]<12 or math.hypot(px,pz)<18:continue
   rock=copy.deepcopy(rock_template);rock.update(id=str(uuid.uuid4()),name=f'Sprint canyon boulder {i}-{side}',parentId=None);rock['transform']['localPosition']=m.vector(px,height(px,pz)+1,pz);rock['transform']['localScale']=m.vector(2.5,2.5,2.5);scene['gameObjects'].append(rock)
(ROOT/'Scenes/ScrapyardSprint.bytescene').write_text(json.dumps(scene,indent=2)+'\n')
print('Authored 18 ordered sprint gates and desert course.')
