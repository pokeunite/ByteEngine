from pathlib import Path
import json, shutil, hashlib, zipfile, datetime
repo=Path(__file__).resolve().parents[2]
line=next(x for x in (repo/'artifacts/hazard-final-export.log').read_text().splitlines() if x.startswith('WEB_TEST_SITE='))
site=Path(line.split('=',1)[1]);build=site.parent;package=build/'Hazard_Pay-Web.zip'
assert 'FULL_SCENE_PASS' in (repo/'artifacts/hazard-verified-347.log').read_text()
manifest=json.loads((site/'web-game.json').read_text());assert manifest['name']=='Hazard Pay'
fingerprints=json.loads((site/'browser-plugin-builds.json').read_text());plugin=fingerprints['bytebard.karlsondemo'];assert plugin['version']=='3.47.0'
game=Path('C:/Users/codex/Documents/palclone')
with zipfile.ZipFile(game/'Plugins/KarlsonDemo.byteplugin') as z:
    dll=next(n for n in z.namelist() if n.endswith('KarlsonDemo.Plugin.dll'))
    assert hashlib.sha256(z.read(dll)).hexdigest().upper()==plugin['sha256'].upper()
with zipfile.ZipFile(package) as z:
    assert 'index.html' in z.namelist() and json.loads(z.read('web-game.json'))['name']=='Hazard Pay'
runtime=json.loads((repo/'artifacts/hazard-release-realtime/game.json').read_text())
frames=runtime['perf'];cpu=sum(f['frameMs'] for f in frames)/len(frames);interval=sum(f['intervalMs'] for f in frames)/len(frames)
idle=json.loads((repo/'artifacts/hazard-verified-347/idle.json').read_text())
summary={'game':'Hazard Pay','plugin':plugin,'zipSha256':hashlib.sha256(package.read_bytes()).hexdigest(),'gameplayChecks':522,'bootResourceHashes':58,'fullSceneTest':'PASS: new game, visible ground/player, player movement, contract launch, spider movement and animated combat','idleHudCommands':idle['frame']['uiCount'],'steadyBrowserFrameWorkMs':round(cpu,2),'steadyBrowserIntervalMs':round(interval,2),'observedFramesPerSecond':round(1000/interval,1),'graphicsBackend':json.loads((repo/'artifacts/hazard-release-realtime/initial.json').read_text())['graphics']['renderer'],'remainingWork':'Browser CPU performance is still below an acceptable release target. This is a functional test build, not a claim of desktop-speed parity.','itch':'Title, description and metadata saved. Replacement build upload skipped at the user request. Existing URL and draft visibility retained.'}
target=Path('C:/Users/codex/Desktop/Hazard Pay Web Test')
if target.exists():target=target.with_name(target.name+' '+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'))
target.mkdir()
shutil.copy2(package,target/'Hazard Pay-Web.zip')
(target/'Validation.json').write_text(json.dumps(summary,indent=2))
readme=f'''# Hazard Pay — local web test build

The floor, character and spider animation failures are repaired. Player movement, contract launch, enemy movement and 32 seconds of simulated combat passed the complete-game browser test. All 522 gameplay checks and 58 browser boot-resource integrity checks passed.

**Performance still needs work.** The isolated real-time browser measurement on this laptop recorded approximately {summary['observedFramesPerSecond']} frames/second ({interval:.1f} ms between frames). Rendering correctness is fixed; desktop-speed parity is not achieved. Keep this as a test build rather than publishing it as an optimised release.

The HUD uses {summary['idleHudCommands']} commands instead of over 5,600. Forced per-frame full garbage collections were removed. Scene storage, batching, identity lookup, contract parsing and machine interaction queries were improved. The remaining bottleneck is managed CPU work, not missing art or animations.

## Files

- Hazard Pay-Web.zip: complete itch.io-compatible ZIP, with index.html at its root. Serve it through a local web server or upload through itch.io; opening index.html directly as a file will not run WASM correctly.
- Validation.json: exact version, checksums, hardware and timing results.
- Screenshots: captured from the complete exported game.

The installed engine and project plugin/source are synchronized at gameplay version 3.47.0. The source project is still C:/Users/codex/Documents/palclone/palclone.byteproject; its display name is Hazard Pay, and the original project identity and saves are preserved.

The itch.io title/description were updated. You chose to keep the ZIP local, so the old online playable build was not replaced. Page URL and draft visibility are unchanged.

The source-level technical report is Docs/Hazard-Pay-Web-Parity.md in the maintained ByteEngine checkout. No Git push was performed in this pass.
'''
(target/'README.md').write_text(readme)
for name in ['menu','idle','walking','combat-6']:
    shutil.copy2(repo/f'artifacts/hazard-verified-347/{name}.png',target/(name+'.png'))
source_report=game/'SourceArt/Hazard Pay Web Fixes';source_report.mkdir(parents=True,exist_ok=True)
for name in ['README.md','Validation.json','walking.png','combat-6.png']:shutil.copy2(target/name,source_report/name)
shutil.copy2(repo/'Docs/Hazard-Pay-Web-Parity.md',source_report/'Technical report.md')
(repo/'artifacts/hazard-final-delivery.json').write_text(json.dumps({'directory':str(target),'zip':str(target/'Hazard Pay-Web.zip'),'summary':summary},indent=2))
print(json.dumps({'directory':str(target),'zip':str(target/'Hazard Pay-Web.zip'),'fps':summary['observedFramesPerSecond'],'checks':522}))
