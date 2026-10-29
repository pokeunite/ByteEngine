// Deterministic full-scene test. No user browser profile, saves or export files are changed.
const http=require('http'),fs=require('fs'),path=require('path');
const {chromium}=require(process.env.BYTEENGINE_PLAYWRIGHT||'playwright');
const site=path.resolve(process.argv[2]),out=path.resolve(process.argv[3]||'artifacts/mining-combat-browser');
const runtime=process.argv[4]?path.resolve(process.argv[4]):site;
for(const file of ['content.js','renderer.js','frame-wire.js','_framework/dotnet.js'])if(!fs.existsSync(path.join(runtime,file)))throw Error('Missing test runtime file: '+path.join(runtime,file));
fs.mkdirSync(out,{recursive:true});
const source=`<!doctype html><style>body{margin:0}canvas{position:absolute;width:1280px;height:720px}#ui{pointer-events:none}</style><canvas id="game" width="1280" height="720"></canvas><canvas id="ui" width="1280" height="720"></canvas><div id="status"></div><script type="module">
import{dotnet}from'./_framework/dotnet.js';import{loadContent}from'./content.js';import{BrowserRenderer}from'./renderer.js';import{decodeUiPacket}from'./frame-wire.js';
try{
 console.log('BOOT');const rt=await dotnet.create();console.log('RUNTIME');await rt.runMain();const config=rt.getConfig(),exp=await rt.getAssemblyExports(config.mainAssemblyName);const game=exp.BrowserGame;
 console.log('CONTENT');await loadContent(game,document.querySelector('#status'));console.log('START');game.Start(false);console.log('STARTED');
 const renderer=new BrowserRenderer(document.querySelector('#game'),document.querySelector('#ui'));renderer.readBuffer=i=>game.ReadBuffer(i);
 const materials={},timings=[];let frame;
 function tick({keys='',buttons='',x=.5,y=.5,captured=false,dt=.016,render=true,dx=0,dy=0}={}){
  if(typeof game.ProfileComponents==='function')game.ProfileComponents(${process.env.BYTEENGINE_COMBAT_PROFILE==='0'?'false':'true'});
  const t=performance.now();frame=JSON.parse(game.Frame(dt,1280,720,keys,buttons,x,y,dx,dy,0,true,captured,''));const error=game.LastFrameError();if(error)throw Error(error);
  const managedMs=performance.now()-t;for(const m of frame.materials)materials[m.material]=m;
  frame.ui=decodeUiPacket(game.ReadBuffer(frame.uiBuffer),frame.uiCount);
  renderer.draw(render?frame:{...frame,draws:[],ui:[],sky:null,shadow:null},1280,720);
  timings.push({managedMs,loopMs:frame.loopMs,...renderer.profile});return frame.pendingTextures;
 }
 function click(x,y){tick();tick({buttons:'Left',x:x/1280,y:y/720});tick();}
 function snapshot(){return {frame,materials,components:typeof game.ComponentTimings==='function'?JSON.parse(game.ComponentTimings()):[],animations:JSON.parse(game.AnimationDiagnostics()),diagnostics:JSON.parse(game.Diagnostics()),timings:timings.slice(-100),renderer:renderer.gl.getParameter(renderer.gl.getExtension('WEBGL_debug_renderer_info').UNMASKED_RENDERER_WEBGL)};}
 window.test={tick,click,snapshot};tick();for(let n=0;frame.pendingTextures&&n<400;n++)tick();document.querySelector('#status').hidden=true;window.ready=true;
}catch(e){window.failure=String(e.stack||e);console.error(e);}
</script>`;
const server=http.createServer((req,res)=>{
 const name=decodeURIComponent(req.url.split('?')[0]);if(name==='/test.html'){res.setHeader('Content-Type','text/html');return res.end(source);}
 const base=name.startsWith('/Game.bytepak')||name==='/web-game.json'?site:runtime;
 const file=path.resolve(base,'.'+name);if(!file.startsWith(base+path.sep)){res.writeHead(403);return res.end();}
 res.setHeader('Content-Type',({'.js':'application/javascript','.json':'application/json','.wasm':'application/wasm'})[path.extname(file)]||'application/octet-stream');
 fs.readFile(file,(e,data)=>{res.statusCode=e?404:200;res.end(e?'Missing':data);});
});
(async()=>{let browser;try{
 await new Promise(r=>server.listen(0,'127.0.0.1',r));browser=await chromium.launch({channel:'msedge',headless:true,args:['--autoplay-policy=no-user-gesture-required']});
 const page=await browser.newPage({viewport:{width:1280,height:720}});page.on('pageerror',e=>console.error(e));page.on('console',m=>console.log('Browser:',m.text()));page.on('requestfailed',r=>console.log('REQUEST FAILED',r.url(),r.failure()));
 await page.goto('http://127.0.0.1:'+server.address().port+'/test.html');await page.waitForFunction(()=>window.ready||window.failure,null,{timeout:240000});
 const error=await page.evaluate(()=>window.failure);if(error)throw Error(error);
 async function save(name){const data=await page.evaluate(()=>test.snapshot());fs.writeFileSync(path.join(out,name+'.json'),JSON.stringify(data,null,2));await page.screenshot({path:path.join(out,name+'.png')});console.log(name,JSON.stringify({skins:data.animations.map(a=>[a.name,a.CurrentAnimation,a.bounds]),diagnostics:data.diagnostics}));return data;}
 await save('menu');await page.evaluate(()=>{test.click(260,355);test.click(640,380);for(let n=0;n<8;n++)test.tick();});
 await page.evaluate(()=>{for(let n=0;n<200;n++)if(test.tick()===0)break;});
 const idle=await save('idle');if(!idle.animations.some(a=>a.name==='Mannequin'))throw Error('Player skeletal renderer missing');
 if(process.env.BYTEENGINE_PROFILE_IDLE_ONLY==='1'){console.log('IDLE_PROFILE_PASS');return;}
 if(idle.frame.uiCount>1000)throw Error('HUD expanded into excessive browser commands: '+idle.frame.uiCount);
 await page.evaluate(()=>{for(let n=0;n<12;n++)test.tick({keys:'D',captured:true,dt:.1,render:n===11});});
 const walking=await save('walking'),before=idle.animations.find(a=>a.name==='Mannequin'),after=walking.animations.find(a=>a.name==='Mannequin');
 if(Math.hypot(...after.position.map((v,i)=>v-before.position[i]))<.5)throw Error('Player movement did not advance');
 await page.evaluate(()=>{for(let n=0;n<12;n++)test.tick({keys:'A',captured:true,dt:.1,render:n===11});test.tick({captured:true});});
 await page.evaluate(()=>{test.tick({keys:'E',captured:true});test.tick();});await save('contracts');
 await page.evaluate(()=>test.click(560,580));await save('operation-start');
 let firstCombat,moved=false;
 for(let batch=0;batch<8;batch++){await page.evaluate(()=>{for(let n=0;n<40;n++)test.tick({dt:.1,captured:true,render:n===39});});const data=await save('combat-'+batch);const spiders=data.animations.filter(a=>a.name==='Animated spider');if(batch===0)firstCombat=spiders;else if(firstCombat?.length) moved||=spiders.some((a,i)=>firstCombat[i]&&Math.hypot(...a.position.map((v,j)=>v-firstCombat[i].position[j]))>2);}
 const final=await page.evaluate(()=>test.snapshot());if(!final.animations.some(a=>a.name==='Animated spider'))throw Error('Combat did not spawn animated spiders');
 for(const a of final.animations){if(!a.bounds||a.bounds.some(v=>!Number.isFinite(v)||Math.abs(v)>100))throw Error('Invalid animation bounds: '+a.name);}
 if(!moved)throw Error('Spider animations loaded but enemies did not actually move');
 console.log('FULL_SCENE_PASS');
}catch(e){console.error(e);process.exitCode=1;}finally{await browser?.close();server.close();}})();
