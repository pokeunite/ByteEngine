// Local cross-origin iframe, actual fullscreen transitions and fixed/native
// resolution comparisons. Persist every completed stage before continuing.
const fs=require('fs'),path=require('path'),http=require('http');
const {chromium}=require(process.env.BYTEENGINE_PLAYWRIGHT||'playwright');
const [site,runtime,out]=process.argv.slice(2).map(p=>path.resolve(p));fs.mkdirSync(out,{recursive:true});
const overlay=process.env.BYTEENGINE_RENDERER_OVERRIDE&&path.resolve(process.env.BYTEENGINE_RENDERER_OVERRIDE);
const quality=process.env.BYTEENGINE_QUALITY||'High';if(!['High','Balanced','Fast'].includes(quality))throw Error('Invalid quality');
const initialResolution=process.env.BYTEENGINE_INITIAL_RESOLUTION||'720p';
const embeddedWidth=!process.env.BYTEENGINE_DEATH_MODE&&initialResolution==='720p'?960:640;
const embeddedHeight=embeddedWidth*9/16;
const errors=[],results=[];let port;
const server=http.createServer((req,res)=>{
 const name=decodeURIComponent(req.url.split('?')[0]);
 if(name==='/host'){res.setHeader('Content-Type','text/html');return res.end(`<style>body{margin:0;background:#222}iframe{width:${embeddedWidth}px;height:${embeddedHeight}px;border:0}iframe:fullscreen{width:100vw;height:100vh}</style><button id="full" onclick="document.querySelector('iframe').requestFullscreen()">Fullscreen</button><iframe allow="fullscreen;autoplay" allowfullscreen src="http://localhost:${port}/"></iframe>`);}
 if(name==='/favicon.ico'){res.writeHead(204);return res.end();}
 const relative='.'+(name==='/'?'/index.html':name);
 const base=name.startsWith('/Game.bytepak')||name==='/web-game.json'?site:overlay&&fs.existsSync(path.resolve(overlay,relative))?overlay:runtime;
 const file=path.resolve(base,relative);if(!file.startsWith(base+path.sep)){res.writeHead(403);return res.end();}
 fs.readFile(file,(error,data)=>{res.statusCode=error?404:200;res.setHeader('Content-Type',({'.js':'application/javascript','.wasm':'application/wasm','.json':'application/json','.html':'text/html'})[path.extname(file)]||'application/octet-stream');
 if(!error&&name==='/main.js')data=data.toString().replace('game.Start(!!manifest.demo);',`game.Start(!!manifest.demo);game.ConfigureBenchmark(${+(process.env.BYTEENGINE_SPIDERS||30)},'baseline');game.ConfigurePresentation(JSON.stringify({quality:${JSON.stringify(quality)},worldResolution:${JSON.stringify(initialResolution)}}));globalThis.testGame=game;globalThis.testRenderer=renderer;globalThis.testRows=[];const previousTestCompletion=renderer.oncompleted;renderer.oncompleted=r=>{previousTestCompletion?.(r);testRows.push(r);if(testRows.length>1800)testRows.shift();};`);
 res.end(error?'Missing':data);
 });
});
let browser,page,frame;
async function snapshot(label){
 const data=await frame.evaluate(async()=>({rows:testRows,presentation:byteEnginePresentation,details:JSON.parse(testGame.PerformanceDetails()),renderer:await testRenderer.report(),capture:byteEngineCapture.report(),fullscreen:!!document.fullscreenElement}));
 const intervals=data.rows.map(r=>r.intervalMs).filter(n=>n>0),ordered=[...intervals].sort((a,b)=>a-b);
 const row={label,...data,fps:1000/(intervals.reduce((a,b)=>a+b,0)/intervals.length),p95:ordered[Math.floor(ordered.length*.95)],max:Math.max(...intervals)};
 results.push({label,fps:row.fps,p95:row.p95,max:row.max,presentation:row.presentation});fs.writeFileSync(path.join(out,label+'.json'),JSON.stringify(row,null,2));fs.writeFileSync(path.join(out,'summary.json'),JSON.stringify({results,errors},null,2));console.log(JSON.stringify(results.at(-1)));
 if(data.renderer.error)throw Error(`WebGL error at ${label}: ${data.renderer.error}`);
 const target=data.renderer.diagnostics?.worldTarget;
 if(target&&(target[0]!==data.presentation.worldWidth||target[1]!==data.presentation.worldHeight))throw Error(`Render target mismatch at ${label}`);
}
async function stage(label,resolution){
 await frame.evaluate(({res,q})=>{testGame.ConfigurePresentation(JSON.stringify({quality:q,worldResolution:res}));byteEngineCapture.event('test-stage',{resolution:res});},{res:resolution,q:quality});
 await page.waitForTimeout(2500);await frame.evaluate(()=>{testRows=[];});
 await page.waitForTimeout(10000);await snapshot(label);
}
(async()=>{try{
 await new Promise(r=>server.listen(0,'0.0.0.0',r));port=server.address().port;
 browser=await chromium.launch({channel:'chrome',headless:false,args:['--autoplay-policy=no-user-gesture-required']});
 page=await browser.newPage({viewport:{width:1920,height:1080},deviceScaleFactor:+(process.env.BYTEENGINE_DPR||1)});
 page.on('pageerror',e=>errors.push(String(e)));page.on('console',m=>{if(m.type()==='error')errors.push(m.text());});
 await page.goto(`http://127.0.0.1:${port}/host`);frame=await page.waitForEvent('framenavigated',{predicate:f=>f.url().startsWith(`http://localhost:${port}/`)}).catch(()=>page.frames().find(f=>f.url().startsWith(`http://localhost:${port}/`)));
 if(!frame)throw Error('Game iframe missing');
 await frame.waitForFunction(()=>document.querySelector('#status')?.textContent.includes('Click to start'),null,{timeout:8000});await frame.evaluate(()=>document.querySelector('#status').click());await frame.waitForFunction(()=>globalThis.testRenderer?.completedFrames>120,null,{timeout:180000});
 if(process.env.BYTEENGINE_DEEP_PROFILE)await frame.evaluate(()=>testGame.CaptureProfileComponents(true));
 await stage('embedded-'+initialResolution,initialResolution);
 if(process.env.BYTEENGINE_SINGLE_STAGE){await page.screenshot({path:path.join(out,'final.png')});if(errors.length)throw Error(errors.join('\n'));return;}
 if(process.env.BYTEENGINE_DEATH_MODE){
  for(let i=0;i<3;i++){
   // Report serialization is outside the measurement window. Let its queued
   // frames drain before triggering the next death.
   await page.waitForTimeout(2500);
   await frame.evaluate(()=>{testRows=[];});
   const death=await frame.evaluate(mode=>{const result=JSON.parse(testGame.BenchmarkDeath(mode));byteEngineCapture.trigger('scripted-death',{mode,...result});return result;},process.env.BYTEENGINE_DEATH_MODE);
   fs.writeFileSync(path.join(out,'death-'+i+'-callback.json'),JSON.stringify(death,null,2));
   await page.waitForTimeout(5000);await snapshot('death-'+i);
  }
  if(errors.length)throw Error(errors.join('\n'));return;
 }
 await page.bringToFront();await page.locator('#full').click();
 try{await page.waitForFunction(()=>!!document.fullscreenElement,null,{timeout:5000});}catch{
  // Windows/Chrome can transiently reject an automation fullscreen request.
  // Retry only through a real click; never simulate fullscreen presentation state.
  await page.waitForTimeout(500);await page.bringToFront();await page.locator('#full').click();await page.waitForFunction(()=>!!document.fullscreenElement);
 }
 await stage('fullscreen-720p','720p');await stage('fullscreen-auto','Auto');await stage('fullscreen-native','Native');
 await page.evaluate(()=>document.exitFullscreen());await page.waitForFunction(()=>!document.fullscreenElement);await stage('returned-720p','720p');
 // Also exercise a large desktop viewport independently of physical screen size.
 await page.locator('iframe').evaluate(e=>{e.style.width='1920px';e.style.height='1080px';});await stage('large-viewport-native','Native');
 await page.screenshot({path:path.join(out,'final.png')});if(errors.length)throw Error(errors.join('\n'));
 }catch(error){console.error(error);process.exitCode=1;fs.writeFileSync(path.join(out,'failure.json'),JSON.stringify({error:String(error),results,errors},null,2));if(frame)await Promise.race([snapshot('partial-failure').catch(()=>{}),new Promise(r=>setTimeout(r,15000))]);
 }finally{await browser?.close();server.close();}})();
