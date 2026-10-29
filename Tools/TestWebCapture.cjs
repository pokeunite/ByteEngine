// Verify actual exported-page controls and downloaded diagnostics, using existing content.
const fs=require('fs'),path=require('path'),http=require('http'),assert=require('assert/strict');
const {chromium}=require(process.env.BYTEENGINE_PLAYWRIGHT||'playwright');
const [site,runtime,out]=process.argv.slice(2).map(p=>path.resolve(p));fs.mkdirSync(out,{recursive:true});
const failures=[];
const server=http.createServer((req,res)=>{
 const name=decodeURIComponent(req.url.split('?')[0]);const base=name.startsWith('/Game.bytepak')||name==='/web-game.json'?site:runtime;
 const file=path.resolve(base,'.'+(name==='/'?'/index.html':name));if(!file.startsWith(base+path.sep)){res.writeHead(403);return res.end();}
 res.setHeader('Content-Type',({'.js':'application/javascript','.wasm':'application/wasm','.json':'application/json','.html':'text/html'})[path.extname(file)]||'application/octet-stream');
 fs.readFile(file,(e,data)=>{res.statusCode=e?404:200;res.end(e?'Missing':data);});
});
(async()=>{let browser;try{
 await new Promise(r=>server.listen(0,'127.0.0.1',r));
 browser=await chromium.launch({channel:'msedge',headless:true,args:['--autoplay-policy=no-user-gesture-required']});
 const page=await browser.newPage({viewport:{width:1280,height:720},acceptDownloads:true});
 page.on('pageerror',e=>failures.push(String(e)));page.on('console',m=>{if(m.type()==='error')failures.push(m.text());});
 await page.goto('http://127.0.0.1:'+server.address().port+'/');
 await page.waitForFunction(()=>document.querySelector('#status')?.textContent.includes('Click to start'),null,{timeout:240000});
 await page.locator('#status').click();await page.waitForFunction(()=>globalThis.byteEngineLastFrame?.pendingTextures===0,null,{timeout:240000});
 await page.waitForTimeout(1500);await page.mouse.click(260,360);await page.waitForTimeout(1200);await page.mouse.click(640,380);
 await page.waitForTimeout(6000);
 await page.mouse.click(640,360);await page.keyboard.press('F9');
 await page.keyboard.down('KeyD');await page.waitForTimeout(1000);await page.keyboard.up('KeyD');
 await page.waitForTimeout(5000);
 const during=await page.evaluate(()=>byteEngineCapture.report());
 assert(during.samples.some(s=>s.deep&&s.core.profiler.components.length>0),'deep callback timings missing');
 assert(during.samples.some(s=>s.deep&&s.core.profiler.objects.length>0),'per-object timings missing');
 assert(during.samples.some(s=>s.deep&&s.core.profiler.frames>1),'callback windows did not accumulate across frames');
 assert(during.samples.some(s=>s.deep&&s.core.profiler.systems.some(x=>x.Category.startsWith('Physics.'))),'query timings missing');
 assert(during.inventories.some(s=>s.data.objects.length>0),'scene inventory missing');
 assert(during.frames.some(f=>f.sceneMetrics&&Number.isFinite(f.renderPreparationMs)),'per-frame scene/render phase data missing');
 assert(during.samples.some(s=>s.browser.graphics.meshBufferBytes>0),'graphics residency missing');
 assert.match(during.build.bootSha256,/^[A-Fa-f0-9]{64}$/);assert(during.build.plugins['bytebard.karlsondemo']);
 await page.waitForFunction(()=>!byteEngineCapture.deep,null,{timeout:35000});
 await page.waitForTimeout(1500);await page.keyboard.press('F8');
 await page.locator('#byteengine-capture').waitFor({state:'visible'});
 await page.screenshot({path:path.join(out,'capture-panel.png')});
 async function download(label,name){const pending=page.waitForEvent('download');await page.getByRole('button',{name:label,exact:true}).click();const item=await pending;await item.saveAs(path.join(out,name));return JSON.parse(fs.readFileSync(path.join(out,name),'utf8'));}
 const report=await download('Download report','performance-report.json');
 const trace=await download('Download trace','performance-trace.json');
 assert.equal(report.version,2);assert(report.modeSummaries.normal.frames>0);assert(report.modeSummaries.deep.frames>0);
 assert.equal(report.samples.filter(s=>s.deep).reduce((sum,s)=>sum+s.core.profiler.frames,0),report.frames.filter(f=>f.deep).length,'profile windows lost a deep frame');
 assert(trace.traceEvents.some(e=>e.ph==='X'&&e.name==='Frame'));
 assert(!report.events.some(e=>e.kind==='diagnostic-error'),JSON.stringify(report.events.filter(e=>e.kind==='diagnostic-error')));
 assert.equal(failures.length,0,failures.join('\n'));
 console.log(JSON.stringify({result:'WEB_CAPTURE_PASS',frames:report.frames.length,samples:report.samples.length,deepSamples:report.samples.filter(s=>s.deep).length,
  bytes:fs.statSync(path.join(out,'performance-report.json')).size,summary:report.summary,modes:report.modeSummaries},null,2));
}catch(e){console.error(e);process.exitCode=1;}finally{fs.writeFileSync(path.join(out,'browser-errors.json'),JSON.stringify(failures,null,2));await browser?.close();server.close();}})();
