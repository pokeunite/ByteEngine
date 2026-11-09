"""Install reusable research assets and the scoped HUD/refinery fixes, without exporting."""
from pathlib import Path
import json,shutil,uuid,datetime,hashlib
WORK=Path(__file__).resolve().parents[2];ROOT=Path('C:/Users/codex/Documents/palclone');OUT=WORK/'artifacts/research-models'
BACK=ROOT/'SourceArt/Research Support Backups'/datetime.datetime.now().strftime('%Y%m%d-%H%M%S');BACK.mkdir(parents=True)
scene=ROOT/'Scenes/Main.bytescene';shutil.copy2(scene,BACK/scene.name);data=json.loads(scene.read_text(encoding='utf-8-sig'))
operation=next(o for o in data['gameObjects'] if any(c['type'].endswith('MiningOperation3D') for c in o.get('components',[])))
props=next(c['properties'] for c in operation['components'] if c['type'].endswith('MiningOperation3D'))
props.update(refineryLoadCapacity=50,finalEnemyMarkerHeight=1.8,finalEnemyMarkerSize=16)
scene.write_text(json.dumps(data,indent=2)+'\n',encoding='utf-8')
def copy(source,target):
 target.parent.mkdir(parents=True,exist_ok=True)
 if target.exists() and source.read_bytes()==target.read_bytes():return
 if target.exists():
  relative=target.relative_to(ROOT) if target.is_relative_to(ROOT) else Path(target.name);save=BACK/relative;save.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(target,save)
 shutil.copy2(source,target)
def meta(path,kind):
 m=path.with_name(path.name+'.meta')
 if m.exists():return json.loads(m.read_text())['guid']
 id=str(uuid.uuid5(uuid.NAMESPACE_URL,'hazard-pay/research/'+path.name));m.write_text(json.dumps({'guid':id,'type':kind},indent=2));return id
refs={}
for name in ('ResearchStation','SecurityDrone','FieldBarricade'):
 path=ROOT/'Assets/Models/Mining/Research'/(name+'.glb');copy(OUT/(name+'.glb'),path);refs[name]={'guid':meta(path,'Model3D'),'projectPath':path.relative_to(ROOT).as_posix()}
template=json.loads((ROOT/'Assets/Blueprints/Mining/Alien Hunter.byteblueprint').read_text())
def component(kind,**properties):return {'type':kind,'enabled':True,'properties':properties}
for name in refs:
 blueprint=json.loads(json.dumps(template));blueprint.update(id=str(uuid.uuid5(uuid.NAMESPACE_URL,'hazard-pay/research/blueprint/'+name)),name={'ResearchStation':'R&D Station','SecurityDrone':'Security Drone','FieldBarricade':'Field Barricade'}[name],type='GenericObject',children=[],variables=[],eventModules=[],skeletonAsset=None)
 root=blueprint['root'];root.update(id=str(uuid.uuid5(uuid.NAMESPACE_URL,'hazard-pay/research/root/'+name)),name=blueprint['name'],active=True,tags=[],layer=0,parentId=None,parentSocket='',components=[],variables=[])
 root['transform'].update(localPosition={'x':0,'y':1.7 if name=='SecurityDrone' else 0,'z':0},localRotation={'x':0,'y':0,'z':0,'w':1},localScale={'x':1,'y':1,'z':1})
 if name=='SecurityDrone':root['components']=[component('bytebard.karlsondemo.MiningSecurityDrone3D',operationObjectId=operation['id'],model=refs[name],batterySeconds=60,warningSeconds=5,drainOnlyDuringCombat=True,followSpeed=4,hoverHeight=1.7,attackRange=12,shotDamage=8,shotInterval=.5,explosionRadius=3,explosionDamage=50,explosionVisualSize=1.3,removeWhenDepleted=True)]
 else:
  root['components']=[component('SkeletalMeshRenderer',modelGuid=refs[name]['guid'],modelPath=refs[name]['projectPath'],defaultAnimation=name+'_Idle',playOnStart=True,loop=True,animationUpdateInterval=1/30),component('BoxCollider3D',center=[0,.48 if name=='FieldBarricade' else .95,0],size=[2.4,.95,.26] if name=='FieldBarricade' else [1.3,1.9,.8]),component('bytebard.karlsondemo.MiningCampObject3D',displayName=blueprint['name'],allowRelocation=True)]
  if name=='FieldBarricade':root['components'].append(component('HealthComponent',maxHealth=180,currentHealth=180,destroyOnDeath=True))
 path=ROOT/'Assets/Blueprints/Mining/Research'/(name+'.byteblueprint');path.parent.mkdir(parents=True,exist_ok=True)
 if path.exists():shutil.copy2(path,BACK/path.name)
 path.write_text(json.dumps(blueprint,indent=2));meta(path,'Blueprint')
source=ROOT/'SourceArt/Research Support';source.mkdir(parents=True,exist_ok=True)
for path in OUT.iterdir():
 if path.is_file() and path.suffix in ('.blend','.json','.png'):copy(path,source/path.name)
copy(WORK/'Tools/MiningPrototype/BuildResearchModels.py',source/'BuildResearchModels.py')
concepts=WORK/'Docs/Concepts/AlienMining/Research'
for path in concepts.glob('*.png'):copy(path,source/'References'/path.name)
plugin=WORK/'Plugins/KarlsonDemo';target=ROOT/'PluginSource/KarlsonDemo'
for path in plugin.rglob('*'):
 if path.is_file() and not any(p in ('bin','obj') for p in path.relative_to(plugin).parts):copy(path,target/path.relative_to(plugin))
package=WORK/'artifacts/plugins/KarlsonDemo.byteplugin';install=Path('C:/Users/codex/ByteEngine')
destinations=[WORK/'PluginLibrary',WORK/'Dist/ByteEngine/PluginLibrary',WORK/'Editor/ByteEngine.Editor/bin/Debug/net9.0-windows/win-x64/PluginLibrary',WORK/'Editor/ByteEngine.Editor/bin/Release/net9.0-windows/win-x64/PluginLibrary',install/'PluginLibrary',install/'Dist/ByteEngine/PluginLibrary',install/'Editor/ByteEngine.Editor/bin/Debug/net9.0-windows/win-x64/PluginLibrary',install/'Editor/ByteEngine.Editor/bin/Release/net9.0-windows/win-x64/PluginLibrary',ROOT/'Plugins',ROOT/'PluginLibrary']
digest=hashlib.sha256(package.read_bytes()).hexdigest()
for directory in destinations:
 directory.mkdir(parents=True,exist_ok=True);shutil.copy2(package,directory/'KarlsonDemo.byteplugin')
for directory in (WORK/'Dist/ByteEngine',install/'Dist/ByteEngine'):
 identity=directory/'build-identity.json'
 if identity.exists():
  report=json.loads(identity.read_text(encoding='utf-8-sig'));report['PluginSHA256']=digest.upper();identity.write_text(json.dumps(report,indent=2))
print(json.dumps({'backup':str(BACK),'models':list(refs),'plugin_sha256':digest,'packages':len(destinations),'game_exported':False,'shop_implemented':False},indent=2))
