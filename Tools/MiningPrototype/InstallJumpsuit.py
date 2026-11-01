import pathlib,json,uuid,shutil,datetime
root=pathlib.Path('C:/Users/codex/Documents/palclone');folder=root/'SourceArt/HazardPayJumpsuit'
scene_path=root/'Scenes/Main.bytescene';scene=json.loads(scene_path.read_text(encoding='utf-8-sig'))
backup=folder/('scene-backup-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'));backup.mkdir(parents=True,exist_ok=True);shutil.copy2(scene_path,backup/scene_path.name)
old=root/'Assets/Characters/HazardPayExplorer.glb';new=root/'Assets/Characters/HazardPayJumpsuit.glb'
meta_path=new.with_suffix('.glb.meta');meta=json.loads((meta_path if meta_path.exists() else old.with_suffix('.glb.meta')).read_text(encoding='utf-8-sig'))
if not meta_path.exists():meta['guid']=str(uuid.uuid4())
meta['modelImporter']['maxTextureSize']=2048
meta_path.write_text(json.dumps(meta,indent=2)+'\n');model_id=meta['guid']
old_profile=root/'Assets/Animations/HazardPayExplorer.byteanim';profile=root/'Assets/Animations/HazardPayJumpsuit.byteanim'
p=json.loads(old_profile.read_text(encoding='utf-8-sig'));p['name']='Hazard Pay Jumpsuit';p['rig']['referenceModel']={'guid':model_id,'cachedProjectPath':'Assets/Characters/HazardPayJumpsuit.glb'}
profile.write_text(json.dumps(p,indent=2)+'\n');pm=profile.with_suffix('.byteanim.meta')
m=json.loads((pm if pm.exists() else old_profile.with_suffix('.byteanim.meta')).read_text(encoding='utf-8-sig'))
if not pm.exists():m['guid']=str(uuid.uuid4())
pm.write_text(json.dumps(m,indent=2)+'\n')
player_id=next(c['properties']['playerObjectId'] for o in scene['gameObjects'] for c in o['components'] if c['type'].endswith('MiningOperation3D'))
player=next(o for o in scene['gameObjects'] if o['id']==player_id)
for c in player['components']:
 if c['type']=='ModelHierarchyInstance':c['properties'].update(modelGuid=model_id,modelPath='Assets/Characters/HazardPayJumpsuit.glb')
 if c['type']=='AnimationController':c['properties'].update(animationProfileGuid=m['guid'],animationProfilePath='Assets/Animations/HazardPayJumpsuit.byteanim')
old_id=json.loads(old.with_suffix('.glb.meta').read_text())['guid'];sockets=root/'.byteengine/ModelSockets';s=json.loads((sockets/(old_id.replace('-','')+'.sockets.json')).read_text());s['modelGuid']=model_id
target=sockets/(model_id.replace('-','')+'.sockets.json')
if not target.exists():target.write_text(json.dumps(s,indent=2)+'\n')
scene_path.write_text(json.dumps(scene,indent=2)+'\n')
credit='''# Character attribution

JUMPSUIT Low Poly by DanlyVostok (@1799danly)
https://sketchfab.com/3d-models/jumpsuit-low-poly-ddbd830d325b40ceb3829149de146d63
Creative Commons Attribution (CC BY 4.0): https://creativecommons.org/licenses/by/4.0/

Changes for Hazard Pay: seam cleanup, fitted skeleton, skin weights, pose conversion,
retargeted locomotion/action animations, game material import and weapon sockets.
Original model appearance and textures belong to the credited creator.
'''
(folder/'ATTRIBUTION.md').write_text(credit);(root/'Jumpsuit-CREDITS.txt').write_text(credit)
print('Jumpsuit assigned, current animation settings preserved, grip sockets migrated. Model:',model_id)
