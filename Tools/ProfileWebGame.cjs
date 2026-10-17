// Profile the actual exported entry page. Optional actions use normalized canvas coordinates.
// node Tools/ProfileWebGame.cjs SITE OUTPUT ACTIONS_JSON [SECONDS]
const http=require('http'),fs=require('fs'),path=require('path');
const {chromium}=require(process.env.BYTEENGINE_PLAYWRIGHT||'playwright');
const site=path.resolve(process.argv[2]||''),output=path.resolve(process.argv[3]||'WebProfile');
const quality=process.env.BYTEENGINE_QUALITY||'Balanced';if(!['Fast','Balanced','High'].includes(quality))throw Error('Invalid BYTEENGINE_QUALITY');
const actions=JSON.parse(process.argv[4]||'[]'),seconds=Number(process.argv[5]||20);
if(!fs.existsSync(path.join(site,'web-game.json'))||!Number.isFinite(seconds)||seconds<5)throw Error('Pass SITE OUTPUT ACTIONS_JSON [SECONDS >= 5]');
fs.mkdirSync(output,{recursive:true});
const server=http.createServer((req,res)=>{
 let url=decodeURIComponent(req.url.split('?')[0]);if(url==='/favicon.ico'){res.writeHead(204);res.end();return;}if(url==='/')url='/index.html';
 const file=path.resolve(site,'.'+url);if(!file.startsWith(site+path.sep)){res.writeHead(403);res.end();return;}
 res.setHeader('Content-Type',({'.js':'text/javascript','.wasm':'application/wasm','.json':'application/json','.html':'text/html'})[path.extname(file)]||'application/octet-stream');
 fs.readFile(file,(e,b)=>{res.statusCode=e?404:200;res.end(e?'Missing':b);});
});
function distribution(values){const a=values.filter(Number.isFinite).sort((x,y)=>x-y);return {mean:a.reduce((s,n)=>s+n,0)/a.length,p50:a[Math.floor(a.length*.5)],p95:a[Math.floor(a.length*.95)]};}
(async()=>{let browser;try{
 await new Promise(r=>server.listen(0,'127.0.0.1',r));browser=await chromium.launch({channel:'msedge',headless:true,args:['--autoplay-policy=no-user-gesture-required']});
 const page=await browser.newPage({viewport:{width:1280,height:720}}),errors=[];
 page.on('pageerror',e=>errors.push(e.message));page.on('console',m=>{if(m.type()==='error')errors.push(m.text());});
 await page.goto('http://127.0.0.1:'+server.address().port+'/?previewQuality='+quality);
 await page.waitForFunction(()=>document.querySelector('#status').textContent.includes('Click to start'),null,{timeout:240000});
 await page.locator('#status').click();await page.waitForFunction(()=>window.byteEngineLastFrame?.pendingTextures===0,null,{timeout:180000});
 for(const action of actions){await page.mouse.click(action.x*1280,action.y*720);await page.waitForTimeout(action.waitMs??3000);}
 await page.evaluate(async quality=>{const {dotnet}=await import('./_framework/dotnet.js');const rt=await dotnet.create();const game=(await rt.getAssemblyExports(rt.getConfig().mainAssemblyName)).BrowserGame;game.ConfigurePresentation(JSON.stringify({quality,worldResolution:'Native'}));},quality);
 await page.waitForTimeout(3000);
 await page.evaluate(()=>{window.byteEnginePerf.frames.length=0;window.__phaseSamples=[];window.__benchmarkFrames=[];window.__profileInterval=setInterval(()=>{__phaseSamples.push({...byteEngineDebug(),renderProfile:structuredClone(byteEngineRenderProfile)});__benchmarkFrames.push(...byteEnginePerf.frames.splice(0));},500);});
 await page.waitForTimeout(seconds*1000);
 const report=await page.evaluate(()=>{clearInterval(__profileInterval);return {diagnostics:byteEngineDebug(),phases:__phaseSamples,frames:[...__benchmarkFrames,...byteEnginePerf.frames.splice(0)],presentation:byteEnginePresentation,graphics:byteEngineGraphics(),renderProfile:byteEngineRenderProfile,lastFrame:byteEngineLastFrame};});
 const intervals=report.frames.map(f=>f.intervalMs);report.performance={fps:1000/distribution(intervals).mean,intervalMs:distribution(intervals),loopMs:distribution(report.frames.map(f=>f.loopMs)),encodingAndBridgeMs:distribution(report.frames.map(f=>f.managedMs-f.loopMs)),renderMs:distribution(report.frames.map(f=>f.renderMs))};
 report.capture=await page.evaluate(()=>byteEngineCapture.report());
 await page.screenshot({path:path.join(output,'Game.png'),timeout:120000});
 if(process.argv.includes('--probes')){report.probeResults={};for(const mode of ['geometryOff','uiOff','bothOff']){await page.evaluate(mode=>{byteEngineCapture.probes.skipGeometry=mode!=='uiOff';byteEngineCapture.probes.skipUi=mode!=='geometryOff';},mode);await page.waitForTimeout(1500);await page.evaluate(()=>byteEnginePerf.frames.length=0);await page.waitForTimeout(4000);const frames=await page.evaluate(()=>byteEnginePerf.frames.slice());report.probeResults[mode]={fps:1000/distribution(frames.map(f=>f.intervalMs)).mean,loopMs:distribution(frames.map(f=>f.loopMs)),renderMs:distribution(frames.map(f=>f.renderMs))};}await page.evaluate(()=>{byteEngineCapture.probes.skipGeometry=false;byteEngineCapture.probes.skipUi=false;});}
 await page.evaluate(()=>byteEngineProfile(true));report.componentSamples=[];
 for(let i=0;i<12;i++){await page.waitForTimeout(300);report.componentSamples.push(await page.evaluate(()=>byteEngineDebug()));}
 await page.evaluate(()=>byteEngineProfile(false));report.errors=errors;report.site=site;report.graphicsQuality=quality;report.actions=actions;report.requestedSeconds=seconds;report.runtimeBuild=fs.existsSync(path.join(site,"browser-build.json"))?JSON.parse(fs.readFileSync(path.join(site,"browser-build.json"))):{codeOptimization:"SDK default"};
 fs.writeFileSync(path.join(output,'Profile.json'),JSON.stringify(report,null,2));
 console.log(JSON.stringify({scene:report.diagnostics.scene,performance:report.performance,graphics:report.graphics,errors},null,2));
 if(report.presentation.worldWidth!==1280||report.presentation.worldHeight!==720)throw Error('Benchmark did not retain 1280 x 720 world resolution');if(errors.length||report.graphics.error)throw Error('Browser errors were recorded');
}catch(e){console.error(e);process.exitCode=1;}finally{if(browser)await browser.close();server.close();}})();





