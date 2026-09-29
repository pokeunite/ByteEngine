// Automated runtime check: no editor interaction or visible browser window.
const http = require('http');
const fs = require('fs');
const path = require('path');
const { chromium } = require(process.env.BYTEENGINE_PLAYWRIGHT || 'playwright');
const site = path.resolve(process.argv[2] || '');
if (!fs.existsSync(path.join(site, 'web-game.json'))) throw new Error('Pass an exported site directory.');
const pageSource = `<!doctype html><canvas id="game" width="640" height="480"></canvas><canvas id="ui" width="640" height="480"></canvas><div id="status"></div>
<script type="module">
import { dotnet } from './_framework/dotnet.js';
import { loadContent } from './content.js';
import { BrowserRenderer } from './renderer.js';
import { BrowserAudio } from './audio.js';
try {
 const renderer=new BrowserRenderer(document.querySelector('#game'),document.querySelector('#ui'));
 const runtime=await dotnet.create(),config=runtime.getConfig();
 const exports=await runtime.getAssemblyExports(config.mainAssemblyName);await runtime.runMain();
 const game=exports.BrowserGame;
 const {audio:files}=await loadContent(game,document.querySelector('#status'));
 const audio=new BrowserAudio(files,id=>game.AudioEnded(id));await audio.decode();await audio.activate();
 game.Start(false);
 const frames=[];let sound=false;
 for(let i=0;i<20;i++){
  const data=JSON.parse(game.Frame(.05,640,480,'','',.5,.5,0,0,0,true,false,''));
  audio.commands(data.audio);renderer.draw(data,640,480);
  if(renderer.gl.getError()!==renderer.gl.NO_ERROR)throw new Error('WebGL error');
  if(data.audio.some(c=>c.kind==='play'))sound=true;
  frames.push(data);
 }
 if(!frames[0].draws.length)throw new Error('No real scene geometry.');
 if(!frames[0].ui.length)throw new Error('No font/image UI.');
 if(!frames[0].textures.length)throw new Error('No texture uploads.');
 if(!frames.slice(1).some(f=>f.uploads.length))throw new Error('Animated mesh did not change.');
 if(!sound)throw new Error('Scene audio did not play.');
 window.result={passed:true,draws:frames[0].draws.length,ui:frames[0].ui.length,textures:frames[0].textures.length,animatedUpdates:frames.slice(1).filter(f=>f.uploads.length).length,audio:sound};
}catch(e){console.error(e);window.result={passed:false,error:e.stack||String(e)};}
</script>`;
const server=http.createServer((req,res)=>{
 if(req.url==='/__test.html'){res.setHeader('Content-Type','text/html');res.end(pageSource);return;}
 const file=path.resolve(site,'.'+decodeURIComponent(req.url.split('?')[0]));
 if(!file.startsWith(site+path.sep)){res.writeHead(403);res.end();return;}
 const mime={'.js':'application/javascript','.wasm':'application/wasm','.json':'application/json','.html':'text/html'};
 res.setHeader('Content-Type',mime[path.extname(file)]||'application/octet-stream');
 fs.readFile(file,(error,data)=>{res.statusCode=error?404:200;res.end(error?'Missing file':data);});
});
(async()=>{
 let browser;
 try{
  await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
  browser=await chromium.launch({channel:'msedge',headless:true,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader','--autoplay-policy=no-user-gesture-required']});
  const page=await browser.newPage();page.on('console',msg=>console.log('Browser:',msg.text()));page.on('pageerror',e=>console.error(e));
  await page.goto('http://127.0.0.1:'+server.address().port+'/__test.html');
  await page.waitForFunction(()=>window.result!==undefined,{},{timeout:180000});
  const result=await page.evaluate(()=>window.result);console.log(JSON.stringify(result));
  if(!result.passed)process.exitCode=1;
 }catch(e){console.error(e);process.exitCode=1;}
 finally{if(browser)await browser.close();await new Promise(resolve=>server.close(resolve));}
})();
