"""Install generated fitted rigs without rewriting scene gameplay or AI assets."""
import json,struct,uuid,shutil,datetime
from pathlib import Path
WORK=Path(__file__).resolve().parents[2];GAME=Path('C:/Users/codex/Documents/palclone');OUT=WORK/'artifacts/spider-models'
backup=GAME/'.byteengine/Backups'/('spider-rigs-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'));backup.mkdir(parents=True)
for kind,blueprint,scale in [('PlayerHunter','Alien Hunter',.55),('DrillBreaker','Alien Raider',.68)]:
 raw=(OUT/(kind+'-Animated.glb')).read_bytes();length=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+length]);tail=raw[20+length:]
 doc['animations']=[a for a in doc['animations'] if a.get('name','').startswith(kind+'_')]
 assert {a['name'] for a in doc['animations']}=={kind+'_'+s for s in ['Idle','Walk','Jump','Attack','Death']}
 assert len(doc['skins'])==1 and len(doc['skins'][0]['joints'])==56
 encoded=json.dumps(doc,separators=(',',':')).encode();encoded+=b' '*((-len(encoded))%4)
 final=struct.pack('<III',0x46546c67,2,12+8+len(encoded)+len(tail))+struct.pack('<II',len(encoded),0x4E4F534A)+encoded+tail
 (OUT/(kind+'-Animated.glb')).write_bytes(final)
 target=GAME/'Assets/Models/Mining/Spiders'/(kind+'.glb');target.parent.mkdir(parents=True,exist_ok=True)
 if target.exists():shutil.copy2(target,backup/target.name)
 target.write_bytes(final);meta=target.with_suffix('.glb.meta');guid=json.loads(meta.read_text())['guid'] if meta.exists() else str(uuid.uuid4());meta.write_text(json.dumps({'type':'Model3D','guid':guid},indent=2))
 path=GAME/'Assets/Blueprints/Mining'/(blueprint+'.byteblueprint');shutil.copy2(path,backup/path.name);bp=json.loads(path.read_text(encoding='utf-8-sig'))
 # Remove only the old generated placeholder visual children. All root gameplay is retained.
 bp['children']=[o for o in bp.get('children',[]) if o['name'] not in ['Alien carapace','Alien head','Alien leg']]
 components=bp['root']['components'];components[:]=[c for c in components if not c['type'].endswith('MiningSpiderAnimation3D')]
 components.append({'type':'bytebard.karlsondemo.MiningSpiderAnimation3D','enabled':True,'properties':{'model':{'guid':guid,'projectPath':target.relative_to(GAME).as_posix()},'modelScale':scale,'clipPrefix':kind,'walkSpeedAtOne':3.2 if kind=='PlayerHunter' else 2.8,'animationInterval':1/24,'corpseSeconds':3,'maximumCorpses':12}})
 path.write_text(json.dumps(bp,indent=2))
source=GAME/'SourceArt/SpiderModels';source.mkdir(parents=True,exist_ok=True)
for name in ['PlayerHunter-Animated.glb','DrillBreaker-Animated.glb','rig-fit-report.json']:
 shutil.copy2(OUT/name,source/name)
(source/'Mesh2Motion-CREDITS.txt').write_text('Spider skeleton and base motion clips: Mesh2Motion, CC0. https://github.com/scottpetrovic/mesh2motion-app\nOriginal reference supplied by user: exported-model.glb (Walk).\nFitted anatomy, independent leg weights and contact-preserving retarget in Blender.\n')
print('Installed fitted rigs and updated both enemy blueprints. Backup:',backup)
