"""Private browser fixture check: spring arms settle and remain aligned while driving.
Uses an existing unpacked diagnostic site, never creates a distributable game export.
"""
from pathlib import Path
import sys,json,math,struct,zlib,hashlib,shutil,threading,http.server,os
sys.path.insert(0,str(Path('.artifacts/web-test-tools').resolve()))
from playwright.sync_api import sync_playwright
site=Path(sys.argv[1]).resolve();runtime=Path(sys.argv[2]).resolve();project=Path(sys.argv[3]).resolve()
for item in runtime.iterdir():
    if item.is_dir():shutil.copytree(item,site/item.name,dirs_exist_ok=True)
    else:shutil.copy2(item,site/item.name)
content=site/'content/Content'
for folder in ['Assets','Scenes']:shutil.copytree(project/folder,content/folder,dirs_exist_ok=True)
# Match the exporter scan: newly installed art has no sidecars until first import.
# Generate those only in this isolated fixture, preserving existing asset GUIDs.
import uuid
types={**dict.fromkeys(['.png','.jpg','.jpeg','.tga','.bmp','.hdr'],'Texture2D'),**dict.fromkeys(['.wav','.ogg'],'AudioClip'),**dict.fromkeys(['.fbx','.obj','.gltf','.glb'],'Model3D'),'.bytescene':'Scene','.byteevents':'EventModule','.byteblueprint':'Blueprint','.byteanimevents':'AnimationEvents','.byteanim':'AnimationProfile','.bmat':'Material','.bvfx':'VfxEffect','.uitheme':'UiTheme',**dict.fromkeys(['.ttf','.otf','.fnt'],'Font')}
for asset in content.rglob('*'):
    if asset.is_file() and asset.suffix not in ['.meta','.tmp']:
        meta=Path(str(asset)+'.meta')
        if not meta.exists():meta.write_text(json.dumps({'guid':str(uuid.uuid4()),'type':types.get(asset.suffix.lower(),'Unknown')}))
data=bytearray(b'BYTEPAK2');files=[(p,'Content/'+p.relative_to(content).as_posix()) for p in sorted(content.rglob('*')) if p.is_file() and not {'Saves','PluginSource','PluginCache'}.intersection(p.relative_to(content).parts)]
files += [(p,p.relative_to(site).as_posix()) for p in sorted((site/'Resources').rglob('*')) if p.is_file()]
data+=struct.pack('<i',len(files))
def string(s):
    raw=s.encode();n=len(raw);prefix=bytearray()
    while n>=128:prefix.append((n&127)|128);n>>=7
    prefix.append(n);return prefix+raw
for file,name in files:
    raw=file.read_bytes();packed=zlib.compress(raw);data+=string(name)+struct.pack('<q',len(raw))+hashlib.sha256(raw).digest()+struct.pack('<q',len(packed))+packed
entries=[]
for i,start in enumerate(range(0,len(data),8*1024*1024)):
    chunk=data[start:start+8*1024*1024];name=f'Spring-test.{i:04}.bin';(site/name).write_bytes(chunk);entries.append({'path':name,'size':len(chunk),'sha256':hashlib.sha256(chunk).hexdigest()})
(site/'web-game.json').write_text(json.dumps({'formatVersion':3,'name':'Private spring-arm diagnostic','packageSize':len(data),'packageSha256':hashlib.sha256(data).hexdigest(),'files':entries}))
main=site/'main.js';main.write_text(main.read_text(encoding='utf-8-sig').replace('async runtime=>{','async runtime=>{globalThis.byteEngineTestRuntime=runtime;',1).replace('const renderer=new BrowserRenderer(canvas,overlay);','const renderer=new BrowserRenderer(canvas,overlay);globalThis.byteEngineTestRenderer=renderer;',1).replace('const data=JSON.parse(payload);','const data=JSON.parse(payload);globalThis.byteEngineTestFrame=data;',1))
blocks=[]
def add(type,parent,pos,q,moving=False):blocks.append({'Id':len(blocks),'Type':type,'Parent':parent,'Position':pos,'Rotation':q,'MovingMount':moving,'SpeedLimit':32,'Power':1,'SpringRate':4,'Damping':1,'Travel':.17})
add(3,-1,[0,0,0],[0,0,0,1]);add(3,0,[0,0,2],[0,0,0,1]);add(24,0,[0,.5,0],[0,0,0,1]);add(25,1,[0,.57,2],[0,0,0,1])
for side in [-1,1]:
    for z in [-.6,2.6]:
        q=[0,math.sin(-side*math.pi/4),0,math.cos(math.pi/4)];add(32,0,[side*.52,0,z],q);parent=blocks[-1]['Id'];add(13,parent,[side*1.46,.25,z],q,True)
if len(sys.argv)>4:blocks=json.loads(Path(sys.argv[4]).read_text(encoding='utf-8-sig'))
state={'vehicle.json':json.dumps(blocks),'workshop-tutorial.json':'true','workshop-economy-v1.json':json.dumps({'Version':2,'Balance':100000,'Owned':{str(i):1 for i in [34,35,36,37,38]},'Tuning':[],'Rewards':[]})}
if os.environ.get('TEST_TUTORIAL')=='1':
    state.pop('workshop-tutorial.json');state['vehicle.json']=json.dumps([{'Id':0,'Type':1,'Parent':-1,'Position':[0,0,0],'Rotation':[0,0,0,1]}])
    state['workshop-economy-v1.json']=json.dumps({'Version':2,'Balance':0,'Owned':{},'Tuning':[],'Rewards':[]})
class Handler(http.server.SimpleHTTPRequestHandler):
    def __init__(self,*a,**kw):super().__init__(*a,directory=str(site),**kw)
    def log_message(self,*a):pass
server=http.server.ThreadingHTTPServer(('127.0.0.1',8793),Handler);threading.Thread(target=server.serve_forever,daemon=True).start()
try:
    with sync_playwright() as p:
        browser=p.chromium.launch(channel='chrome',headless=True);page=browser.new_page(viewport={'width':1280,'height':720});errors=[];page.on('pageerror',lambda e:errors.append(str(e)));page.on('console',lambda m:print('BROWSER',m.text,flush=True) if 'WebGL' in m.text or 'Could not load scene' in m.text or m.type=='error' else None)
        if os.environ.get('TEST_DEBUG_RUNTIME')=='1':page.set_default_timeout(180000)
        page.add_init_script(f'localStorage.setItem("byteengine-saves-80b99cd7954945faa2149ba822f95460",{json.dumps(json.dumps(state))});')
        page.add_init_script('navigator.getGamepads=()=>[];')
        page.goto('http://127.0.0.1:8793');page.wait_for_function("document.querySelector('#status').textContent.includes('Click to start')||document.querySelector('#status').dataset.failed",timeout=180000)
        assert not page.evaluate("!!document.querySelector('#status').dataset.failed"),page.locator('#status').inner_text()
        page.locator('#status').click();page.wait_for_function("document.querySelector('#status').hidden||document.querySelector('#status').dataset.failed",timeout=120000)
        print('STARTUP',page.locator('#status').inner_text(),errors,flush=True)
        assert not page.evaluate("!!document.querySelector('#status').dataset.failed"),page.locator('#status').inner_text()
        import os
        if os.environ.get('TEST_MOTORSPORT')=='1':
            def click(x,y):page.locator('#game').click(position={'x':x,'y':y});page.wait_for_timeout(1000)
            for title,kind,row in [('Dust Bowl Drift','Drift',320),('Dead Straight','Drag',384),('Red Rock Rally','Rally',448)]:
                if os.environ.get('TEST_MOTORSPORT_TARGET') and os.environ['TEST_MOTORSPORT_TARGET']!=kind:continue
                click(110,276);click(470,524);click(400,row);page.screenshot(path=str(site/(kind+'-contract.png')))
                held_launch=kind=='Drag' and os.environ.get('TEST_DRAG_LAUNCH')=='1'
                if held_launch:page.keyboard.down('w')
                click(830,629)
                page.wait_for_function('byteEngineDebug().building===false||document.querySelector("#status").dataset.failed',timeout=90000)
                assert not page.evaluate('!!document.querySelector("#status").dataset.failed'),page.locator('#status').inner_text()
                page.wait_for_function('byteEngineTestFrame.pendingTextures===0',timeout=60000);page.wait_for_timeout(4500)
                debug=page.evaluate('byteEngineDebug()');assert debug['scene']==title and debug['motorsport']['kind']==kind,debug
                if held_launch:
                    assert debug['motorsport']['launchPenalty']==0 and debug['velocity']>0.1,debug
                    page.keyboard.up('w')
                    for i in range(5):page.keyboard.press('x');page.wait_for_timeout(950)
                    debug=page.evaluate('byteEngineDebug()');assert debug['motorsport']['gear']==6 and debug['motorsport']['topGear'],debug
                    assert not page.evaluate('byteEngineTestFrame.ui.some(c=>String(c.text||"").includes("REV LIMIT / SHIFT UP"))'),debug
                    page.screenshot(path=str(site/'Drag-top-gear.png'))
                page.screenshot(path=str(site/(kind+'-start.png')))
                start=debug['position'];page.keyboard.down('w');page.wait_for_timeout(4000);page.keyboard.up('w')
                moving=page.evaluate('byteEngineDebug()');assert moving['velocity']>1 and moving['position']!=start,moving
                if kind=='Drift':
                    page.keyboard.down('w');page.keyboard.down('d');page.keyboard.down('Shift');page.wait_for_timeout(7000);page.keyboard.up('w');page.keyboard.up('d');page.keyboard.up('Shift')
                    debug=page.evaluate('byteEngineDebug()');assert debug['motorsport']['driftBanked']+debug['motorsport']['driftChain']>0,debug
                elif kind=='Drag':assert moving['motorsport']['gate']>=1,moving
                else:assert moving['motorsport']['state']=='Racing',moving
                page.screenshot(path=str(site/(kind+'-driving.png')));assert page.evaluate('byteEngineGraphics().error')==0
                print('PASS browser motorsport',kind,debug,flush=True)
                click(1170,115);page.wait_for_function('byteEngineDebug().building===true||document.querySelector("#status").dataset.failed',timeout=30000)
            for w,h in [(800,600),(1920,1080)]:
                page.set_viewport_size({'width':w,'height':h});page.wait_for_timeout(1200);assert page.evaluate('byteEngineGraphics().error')==0
            assert not errors,errors;print('PASS browser new race assets, deployment, physical driving, drift scoring, race state, quit and resize',flush=True);browser.close();sys.exit(0)
        if os.environ.get('TEST_TUTORIAL')=='1':
            def click(x,y):page.locator('#game').click(position={'x':x,'y':y});page.wait_for_timeout(1000)
            page.wait_for_timeout(1500);page.screenshot(path=str(site/'polish-tutorial-start.png'));click(110,218);click(480,250)
            click(110,160);page.screenshot(path=str(site/'polish-tutorial-build.png'));click(72,684);click(110,276);click(400,253);click(830,629)
            page.wait_for_function('byteEngineDebug().building===false||document.querySelector("#status").dataset.failed',timeout=30000)
            assert not page.evaluate('!!document.querySelector("#status").dataset.failed'),page.locator('#status').inner_text()
            page.wait_for_function('JSON.parse(localStorage.getItem("byteengine-saves-80b99cd7954945faa2149ba822f95460"))["workshop-tutorial.json"]',timeout=15000)
            assert page.evaluate('byteEngineDebug().blocks')>3,page.evaluate('byteEngineDebug()')
            assert not errors,errors;assert page.evaluate('byteEngineGraphics().error')==0
            page.screenshot(path=str(site/'polish-tutorial-mission.png'));print('PASS browser first-player tutorial: starter blueprint, build, garage, first contract and persisted tutorial completion',flush=True);browser.close();sys.exit(0)
        if os.environ.get('TEST_PRESENTATION')=='1':
            def click(x,y):page.locator('#game').click(position={'x':x,'y':y});page.wait_for_timeout(900)
            page.wait_for_timeout(1800);page.screenshot(path=str(site/'polish-garage.png'))
            click(100,160);page.screenshot(path=str(site/'polish-builder.png'));click(72,684)
            click(110,365);page.screenshot(path=str(site/'polish-settings.png'))
            if os.environ.get('TEST_QUALITY')=='1':
                assert page.evaluate('byteEnginePresentation.quality')=='Balanced'
                click(620,445);assert page.evaluate('byteEnginePresentation.quality')=='High'
                click(620,487);assert page.evaluate('byteEnginePresentation.worldResolution')=='Native'
                click(620,487);click(620,487);assert page.evaluate('byteEnginePresentation.worldResolution')=='50%'
                page.set_viewport_size({'width':1920,'height':1080});page.wait_for_timeout(1800)
                page.wait_for_function('byteEngineTestFrame.pendingTextures===0',timeout=30000)
                size=page.evaluate('byteEnginePresentation');assert size['worldWidth']==960 and size['uiWidth']==1920,size
                page.wait_for_function('''()=>{const s=JSON.parse(localStorage.getItem("byteengine-saves-80b99cd7954945faa2149ba822f95460")||"{}");const p=JSON.parse(s["menu-settings.json"]||"{}");return p.Graphics==="High"&&p.WorldResolution==="50%";}''',timeout=15000)
                page.screenshot(path=str(site/'quality-high-half-world-sharp-ui.png'))
                click(930,730);assert page.evaluate('byteEnginePresentation.worldResolution')=='Auto'
                page.set_viewport_size({'width':1280,'height':720});page.wait_for_timeout(1200)
                click(620,445);assert page.evaluate('byteEnginePresentation.quality')=='Fast'
                click(620,445);assert page.evaluate('byteEnginePresentation.quality')=='Balanced'
            click(620,528)
            page.screenshot(path=str(site/'polish-controls.png'));click(600,200);page.keyboard.press('ArrowUp');page.wait_for_timeout(700)
            page.wait_for_function('''()=>{const s=JSON.parse(localStorage.getItem("byteengine-saves-80b99cd7954945faa2149ba822f95460")||"{}");const p=JSON.parse(s["menu-settings.json"]||"{}");return p.DriveBindings?.Forward==="Up";}''',timeout=15000)
            stored=page.evaluate('JSON.parse(JSON.parse(localStorage.getItem("byteengine-saves-80b99cd7954945faa2149ba822f95460"))["menu-settings.json"])')
            assert stored['DriveBindings']['Forward']=='Up',stored
            click(730,578);click(450,602)
            click(110,276);click(400,253);page.screenshot(path=str(site/'polish-contracts.png'));click(830,629)
            page.wait_for_function('byteEngineDebug().building===false||document.querySelector("#status").dataset.failed',timeout=30000)
            assert not page.evaluate('!!document.querySelector("#status").dataset.failed'),page.locator('#status').inner_text()
            page.keyboard.down('ArrowUp');page.wait_for_timeout(3000);page.keyboard.up('ArrowUp')
            assert page.evaluate('byteEngineDebug().velocity')>.5,page.evaluate('byteEngineDebug()')
            shading=page.evaluate('''()=>{const r=byteEngineTestRenderer,f=byteEngineTestFrame;let shadows=0;for(const d of f.draws){const m=r.materials.get(d.material);if(m?.unlit&&m.blend==='AlphaBlend'&&m.color.slice(0,3).every(v=>v===0)&&r.textures.get(m.texture)?.width===64)shadows++;}return {sky:f.sky?.intensity,hdr:f.sky?.hdr,ambient:f.ambient,shadows};}''')
            assert shading['hdr'] and abs(shading['sky']-.85)<.001 and abs(shading['ambient']-.30)<.001 and shading['shadows']>0,shading
            assert page.evaluate('byteEngineTestFrame.shadow.resolution')==1024
            assert page.evaluate('byteEngineTestRenderer.profile.shadowDrawCalls')>0
            print('PRESENTATION SHADING',shading,flush=True)
            page.screenshot(path=str(site/'polish-mission.png'))
            for w,h in [(800,600),(1920,1080),(1280,720)]:
                page.set_viewport_size({'width':w,'height':h});page.wait_for_timeout(1500)
                page.wait_for_function('byteEngineTestFrame.pendingTextures===0',timeout=30000)
                graphics=page.evaluate('byteEngineGraphics()');assert graphics['error']==0,graphics
                size=page.evaluate('byteEnginePresentation');assert size['uiWidth']==w and size['width']==w,size
                page.screenshot(path=str(site/f'polish-mission-{w}x{h}.png'))
            report=page.evaluate('byteEngineCapture.report()');(site/'polish-performance.json').write_text(json.dumps(report))
            assert not errors,errors;print('PASS browser build menu, settings, actual key rebinding/persistence, contract deployment, driving and three viewport renders',flush=True);browser.close();sys.exit(0)
        if os.environ.get('TEST_GARAGE_TRANSITIONS')=='1':
            for cycle in range(3):
                page.locator('#game').click(position={'x':640,'y':360});page.keyboard.press('b')
                page.wait_for_function('byteEngineDebug().building===false||document.querySelector("#status").dataset.failed',timeout=25000)
                assert not page.evaluate('!!document.querySelector("#status").dataset.failed'),page.locator('#status').inner_text()
                page.wait_for_timeout(1500);page.keyboard.press('b')
                page.wait_for_function('byteEngineDebug().building===true||document.querySelector("#status").dataset.failed',timeout=25000)
                assert not page.evaluate('!!document.querySelector("#status").dataset.failed'),page.locator('#status').inner_text()
                for w,h in [(1305,1067),(800,600),(1280,720)]:page.set_viewport_size({'width':w,'height':h});page.wait_for_timeout(1500)
                graphics=page.evaluate('byteEngineGraphics()')
                assert graphics['error']==0,graphics
                print('PASS test-drive/garage return and resize cycle',cycle,graphics,flush=True)
            assert not errors,errors;assert page.evaluate('byteEngineGraphics().error')==0
            page.screenshot(path=str(site/'garage-return.png'));browser.close();sys.exit(0)
        page.locator('#game').click(position={'x':110,'y':658});page.wait_for_function('byteEngineDebug().building===false',timeout=20000)
        page.wait_for_timeout(12000);samples=[]
        for i in range(15):samples.append(page.evaluate('byteEngineDebug()'));page.wait_for_timeout(200)
        (site/'spring-idle.json').write_text(json.dumps(samples,indent=2));print('IDLE',samples[-1],flush=True)
        assert all(s['blocks']==len(blocks) and s['velocity']<.15 and s['suspension']['lateralError']<.04 and s['suspension']['wheelAxisError']<6 and s['suspension']['mountAngularError']<3 and s['physicsPhases']['substeps']>=4 for s in samples),samples[-1]
        page.screenshot(path=str(site/'spring-idle.png'));page.keyboard.down('w');page.keyboard.down('a');page.wait_for_timeout(7000);page.keyboard.up('w');page.keyboard.up('a');moving=page.evaluate('byteEngineDebug()');print('DRIVE',moving,flush=True);assert moving['velocity']>.5 and moving['suspension']['lateralError']<.06 and moving['suspension']['wheelAxisError']<6 and moving['suspension']['mountAngularError']<6,moving
        page.screenshot(path=str(site/'spring-drive.png'))
        (site/'spring-performance.json').write_text(json.dumps(page.evaluate('byteEngineCapture.report()')))
        assert page.evaluate('byteEngineGraphics().error')==0,'WebGL error'
        import os
        if os.environ.get('POLISH_SAND_CHECK')=='1':
            imprint=page.evaluate('''()=>{const r=byteEngineTestRenderer;let maps=0,deepest=0;for(const m of r.materials.values())if(m.sandImprint){maps++;const t=r.textures.get(m.sandImprint);if(t){const p=t.image.getContext('2d').getImageData(0,0,t.image.width,t.image.height).data;for(let i=2;i<p.length;i+=4)deepest=Math.max(deepest,p[i]);}}return {maps,deepest};}''')
            print('SAND',imprint,flush=True);assert imprint['maps']>0 and imprint['deepest']>40,imprint
            page.evaluate('requestAnimationFrame=()=>0');page.wait_for_timeout(500)
            page.evaluate('''()=>{const r=byteEngineTestRenderer,f=byteEngineTestFrame,cx=-8,cz=0;const vp=[1/12,0,0,0,0,0,-.01,0,0,1/7,0,0,-cx/12,-cz/7,.5,1];r.draw({...f,vp,eye:[cx,50,cz],sky:null,ui:[],uploads:[],textures:[],deadMeshes:[],deadTextures:[],deadMaterials:[],materials:[],draws:f.draws.filter(d=>r.materials.get(d.material)?.sandImprint)},1280,720);}''')
            page.screenshot(path=str(site/'sand-tracks-top.png'))
        assert not errors,errors;print('PASS browser spring-arm idle stability, joint alignment and driving',flush=True);browser.close()
finally:server.shutdown()









