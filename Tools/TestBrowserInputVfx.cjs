// Real DOM input + full game, using an existing export and a runtime overlay.
const http=require('http'),fs=require('fs'),path=require('path'),assert=require('assert/strict');
const {chromium}=require(process.env.BYTEENGINE_PLAYWRIGHT||'playwright');
const [site,runtime,out]=process.argv.slice(2).map(p=>path.resolve(p));
fs.mkdirSync(out,{recursive:true});
const errors=[];
const server=http.createServer((req,res)=>{
 const name=decodeURIComponent(req.url.split('?')[0]);
 const base=name.startsWith('/Game.bytepak')||name==='/web-game.json'?site:runtime;
 const file=path.resolve(base,'.'+(name==='/'?'/index.html':name));
 if(!file.startsWith(base+path.sep)){res.writeHead(403);return res.end();}
 fs.readFile(file,(error,data)=>{
  res.statusCode=error?404:200;
  res.setHeader('Content-Type',({'.js':'application/javascript','.json':'application/json','.wasm':'application/wasm','.html':'text/html'})[path.extname(file)]||'application/octet-stream');
  if(!error&&path.basename(file)==='main.js')data=data.toString()
   .replace('const wasmEnd=performance.now();','globalThis.parityInput={keys:[...new Set([...keys,...keyPulses])],buttons:[...buttons],captured:document.pointerLockElement===canvas||pauseFromCapture};const wasmEnd=performance.now();')
   .replace('const drawCount=data.draws.length','globalThis.parityGame=game;globalThis.parityRenderer=renderer;globalThis.parityMaterials??={};for(const m of data.materials)parityMaterials[m.material]=m;globalThis.parityFrame=data;globalThis.parityVfx=JSON.parse(game.VfxDiagnostics());const drawCount=data.draws.length');
  res.end(error?'Missing':data);
 });
});
(async()=>{let browser;try{
 await new Promise(r=>server.listen(0,'127.0.0.1',r));
 browser=await chromium.launch({channel:'msedge',headless:true,args:['--autoplay-policy=no-user-gesture-required']});
 const page=await browser.newPage({viewport:{width:1280,height:720}});
 page.on('pageerror',e=>errors.push(String(e)));page.on('console',m=>{if(m.type()==='error')errors.push(m.text());});
 page.on('requestfailed',r=>errors.push(r.url()+': '+r.failure()?.errorText));
 await page.goto('http://127.0.0.1:'+server.address().port+'/');
 await page.waitForFunction(()=>document.querySelector('#status')?.textContent.includes('Click to start'),null,{timeout:240000});
 await page.locator('#status').click();
 await page.waitForFunction(()=>globalThis.parityFrame?.pendingTextures===0,null,{timeout:240000});
 await page.mouse.click(260,360);await page.waitForTimeout(1000);
 await page.mouse.click(640,380);await page.waitForTimeout(5000);
 assert(await page.evaluate(()=>parityGame.WantsPointerLock()),'fresh gameplay must request mouse capture');
 await page.mouse.click(640,360);
 await page.waitForFunction(()=>document.pointerLockElement===document.querySelector('#game'));
 await page.mouse.down({button:'right'});await page.mouse.down({button:'left'});
 await page.waitForFunction(()=>parityInput.buttons.includes('Right')&&parityInput.buttons.includes('Left'));
 await page.waitForTimeout(250);
 const particlePixels=await page.evaluate(()=>{
  const renderer=parityRenderer,gl=renderer.gl,frame=parityFrame;
  const transparent=frame.draws.filter(d=>{const m=parityMaterials[d.material];return m?.unlit&&(m.blend==='Additive'||m.blend==='AlphaBlend');});
  const width=gl.drawingBufferWidth,height=gl.drawingBufferHeight;
  const original=new Uint8Array(width*height*4),without=new Uint8Array(original.length);
  renderer.draw(frame,width,height);gl.readPixels(0,0,width,height,gl.RGBA,gl.UNSIGNED_BYTE,original);
  renderer.draw({...frame,draws:frame.draws.filter(d=>!transparent.includes(d))},width,height);
  gl.readPixels(0,0,width,height,gl.RGBA,gl.UNSIGNED_BYTE,without);renderer.draw(frame,width,height);
  let changed=0;for(let i=0;i<original.length;i+=4)if(Math.abs(original[i]-without[i])+Math.abs(original[i+1]-without[i+1])+Math.abs(original[i+2]-without[i+2])>3)changed++;
  return {draws:transparent.length,changedPixels:changed,vfx:parityVfx.filter(v=>v.RenderedParticles>0)};
 });
 fs.writeFileSync(path.join(out,'particle-pixels.json'),JSON.stringify(particlePixels,null,2));
 assert(particlePixels.changedPixels>0,'particles submitted geometry but did not contribute visible framebuffer pixels');
 assert(particlePixels.vfx.some(v=>v.name==='Weapon muzzle flash'),'aim/fire input must actually trigger the weapon effect');
 await page.screenshot({path:path.join(out,'aim-and-fire.png')});
 fs.writeFileSync(path.join(out,'aim-and-fire.json'),JSON.stringify(await page.evaluate(()=>({input:parityInput,vfx:parityVfx,animations:JSON.parse(parityGame.AnimationDiagnostics())})),null,2));
 await page.mouse.up({button:'left'});
 await page.waitForFunction(()=>parityInput.buttons.length===1&&parityInput.buttons[0]==='Right');
 await page.mouse.up({button:'right'});
 await page.keyboard.press('Escape');
 await page.waitForFunction(()=>!parityGame.WantsPointerLock(),null,{timeout:5000});
 await page.screenshot({path:path.join(out,'pause.png')});
 await page.waitForTimeout(300);
 await page.keyboard.press('Escape');
 await page.waitForFunction(()=>parityGame.WantsPointerLock(),null,{timeout:5000});
 // Menu resume cannot request browser capture without a user gesture. The
 // following click is the browser's permitted recapture gesture.
 await page.mouse.click(640,360);
 await page.waitForFunction(()=>document.pointerLockElement===document.querySelector('#game'));
 const effects=await page.evaluate(()=>parityVfx);
 fs.writeFileSync(path.join(out,'vfx.json'),JSON.stringify(effects,null,2));
 assert(effects.some(v=>v.ActiveParticles>0&&v.RenderedParticles>0),'machine particles must simulate and submit visible geometry');
 for(const effect of effects){
  assert(!/fallback|unavailable|^VFX:/.test(effect.Status),effect.name+': '+effect.Status);
  for(const layer of effect.layers)assert(!layer.error,layer.error);
 }
 assert(effects.some(v=>v.layers.some(l=>l.color?.[3]>0)),'particle assets must retain nonzero opacity');
 assert.equal(errors.length,0,errors.join('\n'));
 console.log('PASS real browser aim+fire, independent button release, Escape pause/resume and VFX geometry/asset opacity');
}catch(e){console.error(e);process.exitCode=1;}finally{fs.writeFileSync(path.join(out,'errors.json'),JSON.stringify(errors,null,2));await browser?.close();server.close();}})();
