"""Add a user-disableable development credit shortcut, without touching saves."""
from pathlib import Path
import json,uuid,copy,datetime,shutil,hashlib
W=Path(__file__).resolve().parents[2];P=Path('C:/Users/codex/Documents/palclone');scene=P/'Scenes/Main.bytescene'
data=json.loads(scene.read_text(encoding='utf-8-sig'));backup=P/'SourceArt/Money Event Backups'/datetime.datetime.now().strftime('%Y%m%d-%H%M%S');backup.mkdir(parents=True);shutil.copy2(scene,backup/scene.name)
op=next(o for o in data['gameObjects'] if any(c['type'].endswith('MiningOperation3D') for c in o.get('components',[])))
module=json.loads((P/'Assets/Events/Mining/Mining business controls.byteevents').read_text())
rule=copy.deepcopy(next(r for r in module['rules'] if any(c['id']=='input.keyPressed' for c in r['conditions'])))
mid=str(uuid.uuid5(uuid.NAMESPACE_URL,'hazard-pay/developer-money-event'));oid=str(uuid.uuid5(uuid.NAMESPACE_URL,'hazard-pay/developer-money-object'))
rule.update(id=str(uuid.uuid4()),displayName='F9 - add 1,000 camp credits (testing)',editorTitle='F9 - add 1,000 camp credits (testing)',enabled=True,subEvents=[])
rule['conditions']=rule['conditions'][:1];condition=rule['conditions'][0];condition['instanceId']=str(uuid.uuid4());condition['arguments']['key']['constant']['string']='F9'
action=copy.deepcopy(rule['actions'][0]);action.update(instanceId=str(uuid.uuid4()),id='bytebard.karlsondemo.mining.addCredits')
action['arguments']['target']['constant']['string']='id:'+op['id'];action['arguments']['amount']={'kind':'Constant','constant':{'type':'Number','number':1000}}
rule['actions']=[action];module.update(id=mid,name='money - development credits',rules=[rule]);path='Assets/Events/Mining/money.byteevents';out=P/path
if out.exists():shutil.copy2(out,backup/out.name)
out.write_text(json.dumps(module,indent=2)+'\n');out.with_name(out.name+'.meta').write_text(json.dumps({'guid':mid,'type':'EventModule'},indent=2))
existing=next((o for o in data['gameObjects'] if o['name']=='money'),None)
if existing is None:
 existing={'id':oid,'name':'money','active':True,'tags':[],'layer':0,'parentId':None,'transform':{'localPosition':{'x':0,'y':0,'z':0},'localRotation':{'x':0,'y':0,'z':0,'w':1},'localScale':{'x':1,'y':1,'z':1}},'components':[],'variables':[]};data['gameObjects'].append(existing)
component=next((c for c in existing['components'] if c['type']=='EventModuleComponent'),None)
if component is None:component={'type':'EventModuleComponent','enabled':True,'properties':{'modules':[]}};existing['components'].append(component)
refs=component['properties'].setdefault('modules',[])
if not any(r.get('guid')==mid for r in refs):refs.append({'guid':mid,'path':path})
scene.write_text(json.dumps(data,indent=2)+'\n')
for relative in ('Mining/MiningEvents.cs','plugin.json'):
 source=W/'Plugins/KarlsonDemo'/relative;target=P/'PluginSource/KarlsonDemo'/relative
 if target.exists():saved=backup/'PluginSource'/relative;saved.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(target,saved)
 target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target)
package=W/'artifacts/plugins/KarlsonDemo.byteplugin';engine=Path('C:/Users/codex/ByteEngine')
for root in (W,engine):
 for relative in ('PluginLibrary','Dist/ByteEngine/PluginLibrary','Editor/ByteEngine.Editor/bin/Debug/net9.0-windows/win-x64/PluginLibrary','Editor/ByteEngine.Editor/bin/Release/net9.0-windows/win-x64/PluginLibrary'):
  folder=root/relative;folder.mkdir(parents=True,exist_ok=True);shutil.copy2(package,folder/'KarlsonDemo.byteplugin')
 identity=root/'Dist/ByteEngine/build-identity.json'
 if identity.exists():report=json.loads(identity.read_text(encoding='utf-8-sig'));report['PluginSHA256']=hashlib.sha256(package.read_bytes()).hexdigest().upper();identity.write_text(json.dumps(report,indent=2))
for relative in ('Plugins','PluginLibrary'):shutil.copy2(package,P/relative/'KarlsonDemo.byteplugin')
assert sum(o['name']=='money' for o in data['gameObjects'])==1
assert rule['conditions'][0]['id']=='input.keyPressed' and rule['actions'][0]['arguments']['amount']['constant']['number']==1000
print(json.dumps({'object':existing['name'],'object_id':existing['id'],'key':'F9','credits_per_press':1000,'event':str(out),'backup':str(backup),'save_files_changed':False}))
