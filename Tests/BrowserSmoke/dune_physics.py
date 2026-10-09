from pathlib import Path
import sys, threading, http.server, json, shutil
sys.path.insert(0,str(Path('.artifacts/web-test-tools').resolve()))
from playwright.sync_api import sync_playwright
site=Path(sys.argv[1]).resolve()
iterations=int(sys.argv[3]) if len(sys.argv)>3 else 6
substeps=int(sys.argv[4]) if len(sys.argv)>4 else 2
runtime=Path(sys.argv[2]).resolve()
for item in runtime.iterdir():
    if item.is_dir(): shutil.copytree(item,site/item.name,dirs_exist_ok=True)
    else: shutil.copy2(item,site/item.name)
# Diagnostic-only runtime access; no change to installed player files.
p=site/'main.js'
s=p.read_text(encoding='utf-8-sig').replace('async runtime=>{','async runtime=>{globalThis.byteEngineTestRuntime=runtime;',1)
p.write_text(s,encoding='utf-8')
class Handler(http.server.SimpleHTTPRequestHandler):
    def __init__(self,*a,**k): super().__init__(*a,directory=str(site),**k)
    def log_message(self,*a): pass
server=http.server.ThreadingHTTPServer(('127.0.0.1',8793),Handler)
threading.Thread(target=server.serve_forever,daemon=True).start()
vehicle=(site/'content/Content/Assets/RecoveryBlueprints/Heavy-recovery-demo.json').read_text()
state={'vehicle.json':vehicle,'workshop-tutorial.json':'true'}
try:
    with sync_playwright() as p:
        browser=p.chromium.launch(channel='chrome',headless=True)
        page=browser.new_page(viewport={'width':1280,'height':720})
        errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
        page.add_init_script(f'localStorage.setItem("byteengine-saves-80b99cd7954945faa2149ba822f95460",{json.dumps(json.dumps(state))});')
        page.goto('http://127.0.0.1:8793')
        page.wait_for_function("document.querySelector('#status').textContent.includes('Click to start')||document.querySelector('#status').dataset.failed",timeout=180000)
        assert not page.evaluate("!!document.querySelector('#status').dataset.failed"),page.locator('#status').inner_text()
        page.locator('#status').click()
        page.wait_for_function("document.querySelector('#status').hidden",timeout=120000)
        page.wait_for_timeout(1500)
        page.locator('#game').click(position={'x':110,'y':658})
        page.wait_for_function('byteEngineDebug().building===false',timeout=15000)
        if len(sys.argv)>3: page.evaluate('''async ([iterations,substeps])=>{const r=byteEngineTestRuntime;const e=await r.getAssemblyExports(r.getConfig().mainAssemblyName);e.BrowserGame.PhysicsProfile(iterations,substeps);}''',[iterations,substeps])
        page.keyboard.down('w');page.wait_for_timeout(2000)
        page.evaluate('byteEngineCapture.probes.skipGeometry=false;byteEngineCapture.probes.skipUi=false')
        page.keyboard.press('F8')
        page.get_by_role('button',name='New capture',exact=True).click()
        page.locator('#game').click(position={'x':640,'y':360})
        page.keyboard.down('w')
        profiler=page.context.new_cdp_session(page)
        profiler.send('Profiler.enable');profiler.send('Profiler.start')
        page.wait_for_timeout(20000)
        profile=profiler.send('Profiler.stop')['profile']
        (site/f'physics-solver-{iterations}x{substeps}.cpuprofile').write_text(json.dumps(profile))
        page.keyboard.up('w')
        report=page.evaluate('byteEngineCapture.report()')
        assert report['diagnostics']['physicsPhases']['iterations']==iterations and report['diagnostics']['physicsPhases']['substeps']==substeps, report['diagnostics']
        assert report['diagnostics']['blocks']==11, report['diagnostics']
        assert report['diagnostics']['velocity']>.5, 'Vehicle did not drive during capture'
        assert any(f.get('diagnostics',{}).get('physicsPhases',{}).get('solverMs',0)>0 for f in report['frames']), 'Missing solver phase'
        assert not errors, errors
        (site/f'physics-phases-{iterations}x{substeps}.json').write_text(json.dumps(report,indent=2))
        print(json.dumps({'summary':report['summary'],'diagnostics':report['diagnostics'],'hardware':report['hardware'],'errors':errors}),flush=True)
        browser.close()
finally:
    server.shutdown()
