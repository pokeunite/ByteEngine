import json,urllib.request,re,concurrent.futures,hashlib,html,time
from pathlib import Path
selected=['W1','W6','W8','C4','M2','M1','M4','F1','F3','F4','U1','U2','U3','U4','U5','A3','D1','D2']
rows=json.loads(Path('Docs/Reference/Freesound-Audition-Shortlist.json').read_text())['sounds'];root=Path('artifacts/selected-freesound');root.mkdir(parents=True,exist_ok=True)
def get(url):
 return urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'Mozilla/5.0 (sound asset import)'}),timeout=40)
def load(r):
 page=get(r['url']).read().decode();(root/(r['id']+'.html')).write_text(page,encoding='utf8');mode='original';data=None
 match=re.search(r'href="([^"]+/download/[^\"]+)"',page)
 if match:
  url='https://freesound.org'+html.unescape(match[1]);resp=get(url)
  if resp.headers.get('Content-Type','').startswith(('audio/','application/octet-stream')):data=resp.read();ext=Path(url).suffix
 if data is None:
  mode='hq-preview';url=html.unescape(re.search(r'data-static-file-url="([^"]+)"',page)[1]);data=get(url).read();ext='.mp3'
 path=root/(r['id']+ext);path.write_bytes(data);r.update(download_kind=mode,download_url=url,source_file=str(path),sha256=hashlib.sha256(data).hexdigest(),bytes=len(data));print(r['id'],mode,len(data),flush=True);return r
with concurrent.futures.ThreadPoolExecutor(max_workers=3) as ex:result=list(ex.map(load,[r for r in rows if r['id'] in selected]))
(root/'manifest.json').write_text(json.dumps(result,indent=2),encoding='utf8')
print('Downloaded',len(result),'selected assets. Reloads and footsteps untouched.')
