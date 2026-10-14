import copy,json,uuid,shutil
from pathlib import Path
ROOT=Path('C:/Users/codex/Documents/DuneCompany');REPO=Path('C:/Users/codex/ByteEngine')
catalog=json.loads((ROOT/'Assets/DuneParts/catalog.json').read_text(encoding='utf-8-sig'))
gearbox=next(p for p in catalog if p['id']==39)
gearbox['function']='Five-speed manual gearbox. Attach directly to the rear face of an engine. X shifts up; Z shifts down (rebind in Driving Controls). Shift in the green RPM band. Final drive ratio trades torque for top speed.'
for path in [ROOT/'Assets/DuneParts/catalog.json',REPO/'Plugins/DuneCompany/Content/DuneParts/catalog.json']:
 path.write_text(json.dumps(catalog,indent=2)+'\n',encoding='utf-8')
profile=json.loads(Path('C:/Users/codex/Documents/karlson demo/Assets/Animation/Pistol.byteanim').read_text(encoding='utf-8-sig'))
meta=json.loads((ROOT/'Assets/DuneParts/glb/39_gearbox.glb.meta').read_text(encoding='utf-8-sig'))
profile['name']='Gearbox lever';profile['rig']['referenceModel']=dict(guid=meta['guid'],cachedProjectPath='Assets/DuneParts/glb/39_gearbox.glb',projectPath=None,path=None)
profile['locomotion'].update(enabled=False,driveFromCharacterController=False,idle='Idle',transitionDuration=0)
template=profile['actions'][0];profile['actions']=[]
for name in ['ShiftUp','ShiftDown']:
 action=copy.deepcopy(template);action.update(name=name,clip=name,blendIn=0,blendOut=0,interruptible=False);profile['actions'].append(action)
path=ROOT/'Assets/Animation/Gearbox Lever.byteanim';path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps(profile,indent=2)+'\n')
Path(str(path)+'.meta').write_text(json.dumps(dict(guid=str(uuid.uuid5(uuid.NAMESPACE_URL,'dune/gearbox/lever-profile')),type='AnimationProfile'),indent=2))
for target in [REPO/'Plugins/DuneCompany/Content/DuneParts',ROOT/'PluginSource/DuneCompany/Content/DuneParts']:
 for relative in ['glb/39_gearbox.glb','glb/39_gearbox.glb.meta','cutouts/39_gearbox.png','cutouts/39_gearbox.png.meta']:
  destination=target/relative;destination.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(ROOT/'Assets/DuneParts'/relative,destination)
print('Manual gearbox catalog, animation profile and canonical source assets synchronized')
