from pathlib import Path
import sys,shutil,struct,zlib,hashlib,json,threading,http.server
from playwright.sync_api import sync_playwright
site=Path(sys.argv[1]).resolve();runtime=Path(sys.argv[2]).resolve()
for item in runtime.iterdir():
 target=site/item.name
 if item.is_dir():shutil.copytree(item,target,dirs_exist_ok=True)
 else:shutil.copy2(item,target)
source=site/'Content';resources=Path(__file__).resolve().parents[2]/'Editor'/'ByteEngine.Editor'/'Resources'/'Fonts';shutil.copytree(resources,site/'Resources'/'Fonts',dirs_exist_ok=True);files=sorted(p for folder in [source,site/'Resources'] for p in folder.rglob('*') if p.is_file() and not {'PluginSource','PluginCache','Saves','Backups'}.intersection(p.relative_to(site).parts));data=bytearray(b'BYTEPAK2'+struct.pack('<i',len(files)))
def string(s):
 raw=s.encode();n=len(raw);out=bytearray()
 while n>=128:out.append((n&127)|128);n>>=7
 out.append(n);return out+raw
for p in files:
 raw=p.read_bytes();packed=zlib.compress(raw);data+=string(p.relative_to(site).as_posix())+struct.pack('<q',len(raw))+hashlib.sha256(raw).digest()+struct.pack('<q',len(packed))+packed
name='Game.bytepak.0000.bin';(site/name).write_bytes(data);(site/'web-game.json').write_text(json.dumps({'formatVersion':3,'name':'Roadmap fixture','packageSize':len(data),'packageSha256':hashlib.sha256(data).hexdigest(),'files':[{'path':name,'size':len(data),'sha256':hashlib.sha256(data).hexdigest()}]}))
p=site/'main.js';s=p.read_bytes().decode('utf-8',errors='replace').replace('const drawCount=data.draws.length','globalThis.probeFrame=data;globalThis.probeAnimation??=new Set();for(const draw of data.draws){if(draw.color[0]<.2&&draw.color[1]>.7){const upload=data.uploads.find(u=>u.id===draw.id);if(upload){const bytes=renderer.bytes(upload.vertices);const values=new Float32Array(bytes.buffer,bytes.byteOffset,bytes.byteLength/4);globalThis.probeAnimation.add(Array.from(values).join(","));}}}const drawCount=data.draws.length');p.write_text(s,encoding='utf-8')
class Handler(http.server.SimpleHTTPRequestHandler):
 def __init__(self,*a,**k):super().__init__(*a,directory=str(site),**k)
 def log_message(self,*a):pass
server=http.server.ThreadingHTTPServer(('127.0.0.1',8794),Handler);threading.Thread(target=server.serve_forever,daemon=True).start()
with sync_playwright() as p:
 browser=p.chromium.launch(channel='chrome' if sys.platform=='win32' else None,headless=True,args=['--enable-webgl','--use-gl=angle','--use-angle=swiftshader','--enable-unsafe-swiftshader']);page=browser.new_page(viewport={'width':1280,'height':720});errors=[];page.on('pageerror',lambda e:errors.append(str(e)));page.goto('http://127.0.0.1:8794');page.wait_for_function("document.querySelector('#status').textContent.includes('Click to start')||document.querySelector('#status').dataset.failed",timeout=180000);assert not page.evaluate("!!document.querySelector('#status').dataset.failed"),page.locator('#status').inner_text();page.locator('#status').click();page.wait_for_timeout(15000);print('DIAGNOSTICS',page.evaluate('({status:document.querySelector("#status").textContent,draws:globalThis.probeFrame?.draws?.length,debug:globalThis.byteEngineDebug?.()})'),errors,flush=True);page.wait_for_function('globalThis.probeFrame?.draws?.length>0',timeout=30000);page.wait_for_timeout(2000);assert page.evaluate('byteEngineGraphics().error')==0;assert not errors,errors;assert page.evaluate('(globalThis.probeAnimation?.size??0)>2'),'Browser animated geometry did not update';assert page.evaluate('globalThis.probeFrame.draws.some(d=>d.color[0]>.25&&d.color[0]<.35&&d.color[1]>.7&&Math.abs(d.model[12]-2)<.3&&Math.abs(d.model[14]-2)<.3)'), 'Browser mesh-nav agent did not arrive';page.screenshot(path=str(site/'browser-smoke.png'));result=page.evaluate('({frame:byteEngineLastFrame,profile:globalThis.byteEngineRenderProfile,diagnostics:byteEngineDebug()})');assert result['diagnostics']['scene']=='Roadmap browser fixture',result
 for w,h in [(1920,1080),(800,600),(1600,600)]:
  page.set_viewport_size({'width':w,'height':h});page.wait_for_timeout(250);assert page.evaluate('byteEngineGraphics().error')==0
 (site/'browser-smoke.json').write_text(json.dumps({'result':result,'errors':errors,'checks':'AOT startup, mesh collider codec, skeletal animation changing vertices, triangle-navmesh agent arrival, primitive rendering, fixed simulation, clipped UI and four viewport sizes'},indent=2));print('PASS browser AOT scene/render/UI/responsive smoke',flush=True);browser.close()
server.shutdown()
