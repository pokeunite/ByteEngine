import pathlib,json,shutil,datetime
p=pathlib.Path('C:/Users/codex/Documents/palclone/Scenes/Main.bytescene');backup=p.parent.parent/'SourceArt/Audio-Mix-Backups'/datetime.datetime.now().strftime('%Y%m%d-%H%M%S');backup.mkdir(parents=True,exist_ok=True);shutil.copy2(p,backup/p.name)
d=json.loads(p.read_text(encoding='utf-8-sig'))
for o in d['gameObjects']:
 for c in o['components']:
  v=c['properties'];t=c['type']
  if t.endswith('MiningAudio3D'):v.update(interfaceVolume=.58,hoverVolume=.32,spiderHitVolume=.85,spiderDeathVolume=1,drillDamageVolume=.9,creatureHearingDistance=8,creatureRolloff=.55)
  if t.endswith(('MiningBusinessUi3D','MiningPauseMenu3D')):v['hoverSoundGain']=1
p.write_text(json.dumps(d,indent=2)+'\n',encoding='utf-8');print('Audio mix updated; backup:',backup)
