// Exercise the normal exported main.js loop, input/audio/capture included.
const fs=require('fs'),http=require('http'),path=require('path'),crypto=require('crypto');
const {chromium}=require(process.env.BYTEENGINE_PLAYWRIGHT||'playwright');
const [site,runtime,out]=process.argv.slice(2).map(p=>path.resolve(p));fs.mkdirSync(out,{recursive:true});
const count=+(process.env.BYTEENGINE_SPIDERS||100),warmup=+(process.env.BYTEENGINE_WARMUP||600),measured=+(process.env.BYTEENGINE_MEASURED||1200),quality=process.env.BYTEENGINE_QUALITY||'High';
if(!['High','Balanced','Fast'].includes(quality))throw Error('Invalid quality');
const errors=[],server=http.createServer((req,res)=>{
 const name=decodeURIComponent(req.url.split('?')[0]);if(name==='/favicon.ico'){res.writeHead(204);return res.end();}
 const override=process.env.BYTEENGINE_RENDERER_OVERRIDE&&path.resolve(process.env.BYTEENGINE_RENDERER_OVERRIDE);
 const base=name.startsWith('/Game.bytepak')||name==='/web-game.json'?site:override&&fs.existsSync(path.resolve(override,'.'+(name==='/'?'/index.html':name)))?override:runtime,file=path.resolve(base,'.'+(name==='/'?'/index.html':name));
 if(!file.startsWith(base+path.sep)){res.writeHead(403);return res.end();}
 fs.readFile(file,(error,data)=>{res.statusCode=error?404:200;res.setHeader('Content-Type',({'.js':'application/javascript','.json':'application/json','.wasm':'application/wasm','.html':'text/html'})[path.extname(file)]||'application/octet-stream');
 if(!error&&name==='/main.js')data=data.toString().replace('game.Start(!!manifest.demo);',`game.Start(!!manifest.demo);game.ConfigurePresentation(JSON.stringify({quality:${JSON.stringify(quality)},worldResolution:'Native'}));game.ConfigureBenchmark(${count},'baseline');game.CaptureProfileComponents(false);
 globalThis.benchmarkRenderer=renderer;globalThis.benchmarkGame=game;globalThis.benchmarkRows=[];
 const previousCompletion=renderer.oncompleted;renderer.oncompleted=row=>{previousCompletion?.(row);if(row.id>${warmup}&&row.id<=${warmup+measured})benchmarkRows.push(row);if(row.id===${warmup+measured})globalThis.benchmarkDone=true;};`);
 res.end(error?'Missing':data);
 });
});
(async()=>{let browser,page;try{
 await new Promise(r=>server.listen(0,'127.0.0.1',r));browser=await chromium.launch({channel:'chrome',headless:process.env.BYTEENGINE_HEADFUL!=='1',args:['--autoplay-policy=no-user-gesture-required']});
 page=await browser.newPage({viewport:{width:1280,height:720}});page.on('pageerror',e=>errors.push(String(e)));page.on('console',m=>{if(m.type()==='error')errors.push(m.text());});
 await page.goto('http://127.0.0.1:'+server.address().port+'/');await page.waitForFunction(()=>document.querySelector('#status')?.textContent.includes('Click to start'),null,{timeout:240000});await page.locator('#status').click();
 let session;
 if(process.env.BYTEENGINE_CPU_PROFILE==='1'){
  await page.waitForFunction(n=>window.benchmarkRenderer?.completedFrames>=n,warmup,{timeout:240000});
  session=await page.context().newCDPSession(page);await session.send('Profiler.enable');await session.send('Profiler.setSamplingInterval',{interval:1000});await session.send('Profiler.start');
 }
 await page.waitForFunction(()=>window.benchmarkDone||document.querySelector('#status')?.dataset.failed,null,{timeout:300000});
 if(session){const {profile}=await session.send('Profiler.stop');fs.writeFileSync(path.join(out,'cpu-profile.json'),JSON.stringify(profile));await session.detach();}
 const result=await page.evaluate(async()=>({rows:benchmarkRows,threaded:benchmarkRenderer.isThreaded,hardware:byteEngineGraphics(),details:JSON.parse(benchmarkGame.PerformanceDetails()),report:await benchmarkRenderer.report(),capture:byteEngineCapture.report(),failed:document.querySelector('#status')?.dataset.failed}));
 const boot=JSON.parse(fs.readFileSync(path.join(runtime,'_framework/blazor.boot.json'))),native=Object.keys(boot.resources.fingerprinting).find(n=>/^dotnet\.native.*\.wasm$/.test(n));
 Object.assign(result,{count,quality,width:1280,height:720,warmup,measured,errors,capturedAt:new Date().toISOString(),runtimeSha256:crypto.createHash('sha256').update(fs.readFileSync(path.join(runtime,'_framework',native))).digest('hex')});
 fs.writeFileSync(path.join(out,'game.json'),JSON.stringify(result,null,2));await page.screenshot({path:path.join(out,'game.png')});
 if(result.failed||errors.length||!result.threaded||result.rows.length!==measured||result.report.error)throw Error(JSON.stringify({failed:result.failed,errors,threaded:result.threaded,rows:result.rows.length,glError:result.report.error}));
 const mean=k=>result.rows.reduce((s,r)=>s+r[k],0)/result.rows.length;console.log(JSON.stringify({count,quality,normalExportLoop:true,fps:1000/mean('intervalMs'),intervalMs:mean('intervalMs'),renderMs:mean('renderMs'),glError:result.report.error}));
 }catch(e){
  console.error(e);process.exitCode=1;
  if(page){try{
   const partial=await page.evaluate(()=>({status:document.querySelector('#status')?.textContent,failed:document.querySelector('#status')?.dataset.failed,completedFrames:globalThis.benchmarkRenderer?.completedFrames,rows:globalThis.benchmarkRows,details:globalThis.benchmarkGame?JSON.parse(benchmarkGame.PerformanceDetails()):null,capture:globalThis.byteEngineCapture?.report()}));
   fs.writeFileSync(path.join(out,'partial.json'),JSON.stringify({...partial,errors,error:String(e)},null,2));await page.screenshot({path:path.join(out,'partial.png')});
  }catch(captureError){fs.writeFileSync(path.join(out,'failure.json'),JSON.stringify({error:String(e),errors,captureError:String(captureError)},null,2));}}
 }finally{await browser?.close();server.close();}})();
