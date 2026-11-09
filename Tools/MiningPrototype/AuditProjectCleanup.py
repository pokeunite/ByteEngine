from pathlib import Path
from collections import defaultdict
import json,hashlib,zipfile,re,datetime
ROOT=Path('C:/Users/codex/Documents/palclone')
OUT=Path(__file__).resolve().parents[2]/'artifacts/project-cleanup'
OUT.mkdir(parents=True,exist_ok=True)
def size(p):return sum(f.stat().st_size for f in p.rglob('*') if f.is_file()) if p.is_dir() else p.stat().st_size
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
entries=[]
def add(p,reason):
 if p.is_symlink() or any(f.is_symlink() for f in p.rglob('*')):raise RuntimeError('Link in cleanup target: '+str(p))
 entries.append(dict(path=str(p.resolve()),relative=p.relative_to(ROOT).as_posix(),reason=reason,bytes=size(p),mtime=p.stat().st_mtime_ns))
cache=ROOT/'.byteengine/ImportCache';groups=defaultdict(list)
for d in cache.iterdir():
 if d.is_dir() and re.fullmatch('[A-Fa-f0-9]{64}',d.name):groups[tuple(sorted(p.stem for p in d.rglob('*.bytemodel')))].append(d)
for ids,dirs in groups.items():
 if not ids:continue
 for d in sorted(dirs,key=lambda p:p.stat().st_mtime_ns,reverse=True)[1:]:add(d,'Older derived import for the same model; latest cache retained')
current=set()
for folder in ('Plugins','PluginLibrary'):
 for p in (ROOT/folder).glob('*.byteplugin'):
  with zipfile.ZipFile(p) as z:manifest=json.loads(z.read('plugin.json').decode('utf-8-sig'))
  current.add(manifest['id']+'-'+sha(p))
for d in (ROOT/'.byteengine/PluginCache').iterdir():
 if d.is_dir() and (d.name.startswith('stage-') or re.search('-[a-fA-F0-9]{64}$',d.name)) and d.name not in current:add(d,'Stale extracted plugin; current packaged version retained')
backups=sorted((ROOT/'.byteengine/Backups').iterdir(),key=lambda p:p.stat().st_mtime_ns,reverse=True)
for p in backups[5:]:add(p,'Historical recovery snapshot; five newest snapshots retained')
for d in (ROOT/'SourceArt').iterdir():
 if d.is_dir() and 'backup' in d.name.lower():
  # Preserve folders containing unique source art or reference images.
  if any(p.suffix.lower() in ('.blend','.glb','.fbx','.wav','.png','.jpg','.psd') for p in d.rglob('*') if p.is_file()):continue
  add(d,'Historical scene/code revision backup; current editable files retained')
for p in (ROOT/'SourceArt').rglob('*'):
 if p.is_file() and re.fullmatch(r'\.blend[1-9][0-9]*',p.suffix.lower()) and p.with_suffix('.blend').is_file() and p.with_suffix('.blend').stat().st_size>0:add(p,'Blender backup copy; primary .blend source retained')
for p in (ROOT/'Scenes').iterdir():
 if p.is_file() and ('.before-' in p.name or '.backup-' in p.name):add(p,'Old inline scene backup; current scene retained')
logs=sorted((ROOT/'Logs').glob('*.txt'),key=lambda p:p.stat().st_mtime_ns,reverse=True)
for p in logs[5:]:add(p,'Old diagnostic log; five newest logs retained')
# Reject overlaps so deletion and freed-space accounting are exact.
for i,a in enumerate(entries):
 for b in entries[i+1:]:
  ap,bp=Path(a['path']),Path(b['path'])
  if ap==bp or ap in bp.parents or bp in ap.parents:raise RuntimeError('Overlapping cleanup targets')
protected={}
for folder in ('Assets','Scenes','Saves','Plugins','PluginLibrary','PluginSource','Scripts'):
 for p in (ROOT/folder).rglob('*'):
  if p.is_file() and not any(p==Path(e['path']) or Path(e['path']) in p.parents for e in entries):protected[p.relative_to(ROOT).as_posix()]=sha(p)
for p in (ROOT/'palclone.byteproject',ROOT/'Jumpsuit-CREDITS.txt'):protected[p.relative_to(ROOT).as_posix()]=sha(p)
before=sum(p.stat().st_size for p in ROOT.rglob('*') if p.is_file())
plan=dict(root=str(ROOT.resolve()),created=datetime.datetime.now().isoformat(),beforeBytes=before,removeBytes=sum(e['bytes'] for e in entries),entries=entries,protected=protected,retainedSnapshots=[p.name for p in backups[:5]])
(OUT/'plan.json').write_text(json.dumps(plan,indent=2),encoding='utf-8')
print(json.dumps(dict(totalMiB=round(before/1024**2,1),removeMiB=round(plan['removeBytes']/1024**2,1),targets=len(entries),protectedFiles=len(protected),retainedSnapshots=plan['retainedSnapshots']),indent=2))
