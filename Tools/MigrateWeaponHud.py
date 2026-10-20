"""Generate the reusable HUD sample; optionally migrate the existing Ultra Violence scene."""
from pathlib import Path
import argparse,copy,json,uuid,datetime,shutil

parser=argparse.ArgumentParser();parser.add_argument('--project',type=Path);parser.add_argument('--apply',action='store_true');args=parser.parse_args()
root=Path(__file__).resolve().parents[1]
module_id=uuid.UUID('8a088859-2601-4af5-97c2-851a0ec26fac')
def uid(name):return str(uuid.uuid5(module_id,name))
def constant(text):return {'kind':'Constant','constant':{'type':'String','string':text}}
def reference(member):return {'kind':'Reference','reference':{'scope':'Component','componentType':'FpsGunLab3D','memberName':member}}
def instruction(kind,name,arguments=None):return {'id':kind,'instanceId':uid(name),'arguments':arguments or {},'conditionInputIds':[],'nextActionId':None}
def rule(name,conditions,actions,roots=None):
    for a,b in zip(actions,actions[1:]):a['nextActionId']=b['instanceId']
    return {'id':uid(name),'displayName':name,'enabled':True,'conditions':conditions,'actions':actions,'subEvents':[],
            'hasExplicitConditionFlow':True,'connectedConditionIds':roots or [c['instanceId'] for c in conditions],
            'hasExplicitExecutionFlow':True,'firstActionId':actions[0]['instanceId'] if actions else None,
            'editorHeaderColor':'42A8B6','editorLayoutInitialized':False}
def formatted(name,target,format,*members):
    return instruction('ui.setFormattedText',name,{'target':constant(target),'format':constant(format),**{'value'+str(n):reference(m) for n,m in enumerate(members)}})
changed=instruction('bytebard.karlsondemo.hudChanged','HUD values changed')
reload=instruction('bytebard.karlsondemo.reloading','Reload active')
idle_reload=instruction('bytebard.karlsondemo.reloading','Reload inactive child')
inverse=instruction('logic.not','Reload not active');inverse['conditionInputIds']=[idle_reload['instanceId']]
module={'id':str(module_id),'version':2,'name':'Weapon HUD — editable text bindings','requiredComponents':[],
    'rules':[
        rule('Update weapon name, magazine and reserve',[changed],[
            formatted('Weapon name','HUD Weapon Name','{0}','WeaponName'),
            formatted('Magazine ammo','HUD Magazine Ammo','{0:0} / {1:0}','Ammo','MagazineCapacity'),
            formatted('Reserve ammo','HUD Reserve Ammo','RESERVE {0:0}','ReserveAmmo')]),
        rule('Reload — show message',[reload],[instruction('ui.show','Show reload',{'target':constant('HUD Reload State')})]),
        rule('Reload — hide when ready',[inverse,idle_reload],[instruction('ui.hide','Hide reload',{'target':constant('HUD Reload State')})],[inverse['instanceId']])],
    'editorLooseActions':[],'editorLooseConditions':[],'editorGroups':[],'editorNodeScale':.72}
sample=root/'Plugins/KarlsonDemo/Samples/Weapon HUD.byteevents';sample.write_text(json.dumps(module,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
print('Reusable sample:',sample)
if args.project:
    project=args.project.resolve();scene_path=project/'Scenes/Weapons Range.bytescene';scene=json.loads(scene_path.read_text(encoding='utf-8-sig'))
    objects=scene['gameObjects'];player=next(o for o in objects if any(c['type']=='bytebard.karlsondemo.FpsGunLab3D' for c in o['components']))
    equipment=next(c for c in player['components'] if c['type']=='bytebard.karlsondemo.FpsGunLab3D')
    legacy_id=equipment['properties'].get('statusTextObjectId')
    existing=next((o for o in objects if o['id']==legacy_id or o['name']=='HUD Weapon Name'),None)
    if existing is None:raise RuntimeError('Existing weapon HUD text was not found; refusing to change an unknown scene.')
    template=copy.deepcopy(existing);mapping={}
    layouts=[('HUD Weapon Name',28,[28,-76]),('HUD Magazine Ammo',32,[28,-34]),('HUD Reserve Ammo',21,[240,-34]),('HUD Reload State',22,[28,-114])]
    for index,(name,size,offset) in enumerate(layouts):
        obj=existing if index==0 else next((o for o in objects if o['name']==name),None)
        if obj is None:obj=copy.deepcopy(template);obj['id']=str(uuid.uuid5(uuid.UUID(existing['id']),name));objects.append(obj)
        obj['name']=name;mapping[name]=obj['id'];text=next(c['properties'] for c in obj['components'] if c['type']=='UiText')
        text.update(size=size,anchor='BottomLeft',offset=offset,wrapWidth=500,autoFit=False)
        text['text']=['WEAPON','0 / 0','RESERVE 0','RELOADING'][index];text['visible']=index!=3
        text['shadowOffset']=[1,1];text['shadowColor']=[0,0,0,.65]
    equipment['properties'].pop('statusTextObjectId',None)
    event_component=next((c for c in player['components'] if c['type']=='EventModuleComponent'),None)
    if event_component is None:event_component={'type':'EventModuleComponent','enabled':True,'properties':{'modules':[]}};player['components'].append(event_component)
    asset_path='Assets/Events/Weapon HUD.byteevents';asset_id=uid('Project HUD asset')
    modules=event_component['properties'].setdefault('modules',[])
    if not any(m.get('path')==asset_path for m in modules):modules.append({'guid':asset_id,'path':asset_path})
    project_module=copy.deepcopy(module)
    for r in project_module['rules']:
        for a in r['actions']:
            target=a['arguments'].get('target',{}).get('constant',{}).get('string')
            if target in mapping:a['arguments']['target']=constant('id:'+mapping[target])
    print('Project:',project,'— native HUD labels:',', '.join(mapping))
    if args.apply:
        backup=project/'.byteengine/Backups'/('Weapon HUD '+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'));backup.mkdir(parents=True,exist_ok=True);shutil.copy2(scene_path,backup/scene_path.name)
        destination=project/asset_path
        if destination.exists():shutil.copy2(destination,backup/destination.name)
        destination.parent.mkdir(parents=True,exist_ok=True);destination.write_text(json.dumps(project_module,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
        meta=destination.with_suffix(destination.suffix+'.meta')
        if not meta.exists():meta.write_text(json.dumps({'guid':asset_id,'type':'EventModule'},indent=2)+'\n',encoding='utf-8')
        else:
            actual_guid=json.loads(meta.read_text(encoding='utf-8-sig'))['guid']
            for entry in modules:
                if entry.get('path')==asset_path:entry['guid']=actual_guid
        scene_path.write_text(json.dumps(scene,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
        print('Updated scene and HUD sheet. Backup:',backup)
    else:print('Preview only; pass --apply to update the project.')
