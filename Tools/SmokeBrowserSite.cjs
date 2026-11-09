const fs=require('fs'),http=require('http'),path=require('path'),assert=require('assert/strict');
const {chromium}=require(process.env.BYTEENGINE_PLAYWRIGHT||'playwright');
const [site,runtime]=process.argv.slice(2).map(p=>path.resolve(p));
const server=http.createServer((req,res)=>{
 const name=decodeURIComponent(req.url.split('?')[0]),relative='.'+(name==='/'?'/index.html':name);
 const base=name==='/Game.bytepak'||name==='/web-game.json'?site:runtime;
 const file=path.resolve(base,relative);if(!file.startsWith(base+path.sep)){res.writeHead(403);return res.end();}
 fs.readFile(file,(error,data)=>{res.statusCode=error?404:200;res.setHeader('Content-Type',({'.js':'application/javascript','.wasm':'application/wasm','.json':'application/json','.html':'text/html'})[path.extname(file)]||'application/octet-stream');res.end(error?'Missing':data);});
});
(async()=>{let browser;try{
 await new Promise(r=>server.listen(0,'127.0.0.1',r));browser=await chromium.launch({channel:'chrome',headless:true,args:['--autoplay-policy=no-user-gesture-required']});
 const page=await browser.newPage();const errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto('http://127.0.0.1:'+server.address().port+'?previewQuality=Fast');
 await page.waitForFunction(()=>document.querySelector('#status')?.textContent.includes('Click to start'),null,{timeout:120000});
 await page.evaluate(()=>document.querySelector('#status').click());await page.waitForTimeout(8000);
 const state=await page.evaluate(()=>({presentation:byteEnginePresentation,graphics:byteEngineGraphics?.(),debug:byteEngineDebug?.(),status:document.querySelector('#status')?.textContent,errors:byteEngineCapture?.report?.().events?.filter(e=>e.kind==='error')??[]}));
 await page.screenshot({path:path.resolve('artifacts/browser-optimization/font-smoke.png')});
 assert.equal(errors.length,0,errors.join('\n'));assert.equal(state.graphics?.error,0,'WebGL error');
 console.log(JSON.stringify({...state,pageErrors:errors}));
}catch(e){console.error(e);process.exitCode=1;}finally{await browser?.close();server.close();}})();
