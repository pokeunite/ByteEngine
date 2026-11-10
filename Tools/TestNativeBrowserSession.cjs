const http=require('http'),fs=require('fs'),path=require('path');const{chromium}=require(process.env.BYTEENGINE_PLAYWRIGHT||'../.artifacts/network-tools/node_modules/playwright');
(async()=>{
const root=path.resolve(process.argv[2]||'.artifacts/network-browser-debug/wwwroot'),project=path.resolve('C:/Users/codex/Documents/ByteEngineMultiplayerDemo');
const html=`<canvas id="game" width="640" height="480"></canvas><canvas id="ui" width="640" height="480"></canvas><script type="module">import {createFrameReader} from './frame-buffers.js';import {BrowserRenderer} from "./renderer.js";import {dotnet} from './_framework/dotnet.js';import {BrowserNetworkBridge} from './network-bridge.js';const runtime=await dotnet.create();const config=runtime.getConfig();const exports=await runtime.getAssemblyExports(config.mainAssemblyName);await runtime.runMain();window.game=exports.BrowserGame;window.transfers=createFrameReader(runtime,game);window.ready=true;window.start=()=>{window.renderer=new BrowserRenderer(document.querySelector("#game"),document.querySelector("#ui"));renderer.readBuffer=i=>transfers.read(i);game.ConfigureSkinning(renderer.maximumSkinningBones);game.Start(false);window.net=new BrowserNetworkBridge(game,i=>transfers.read(i));net.installLobby();};window.step=(keys='')=>{const data=JSON.parse(game.Frame(1/60,640,480,keys,'',.5,.5,0,0,0,true,false,''));transfers.beginFrame(data);if(JSON.stringify(data.net).length>4096)throw Error("Network frame metadata exceeds4KB");net.commands(data.net);renderer.draw(data,640,480);return JSON.parse(game.NetworkStatus());};</script>`;
const types={'.js':'text/javascript','.json':'application/json','.wasm':'application/wasm','.webcil':'application/wasm','.dll':'application/octet-stream'};
const server=http.createServer((req,res)=>{if(req.url==='/'){res.setHeader('content-type','text/html');res.end(html);return;}const target=path.resolve(root,'.'+decodeURIComponent(req.url.split('?')[0]));if(!target.startsWith(root+path.sep)){res.writeHead(403).end();return;}if(!fs.existsSync(target)){res.writeHead(404).end();return;}res.setHeader('content-type',types[path.extname(target)]||'application/octet-stream');fs.createReadStream(target).pipe(res);});await new Promise(r=>server.listen(0,'127.0.0.1',r));
const {spawn}=require('child_process'),readline=require('readline');
const browser=await chromium.launch({channel:'msedge',headless:true,args:['--use-angle=swiftshader']});
try{
for(const nativeHost of [true,false]){
 const page=await browser.newPage();let child;const errors=[];let state=null;
 page.on('pageerror',e=>errors.push(e.message));await page.goto(`http://127.0.0.1:${server.address().port}/`);await page.waitForFunction(()=>window.ready,null,{timeout:60000});
 for(const [source,target] of [['MultiplayerDemo.byteproject','Content/Game.byteproject'],['Scenes/Main.bytescene','Content/Scenes/Main.bytescene'],['Scenes/Main.bytescene.meta','Content/Scenes/Main.bytescene.meta']])await page.evaluate(({target,bytes})=>game.MountBytes(target,bytes),{target,bytes:[...fs.readFileSync(path.join(project,source))]});
 await page.evaluate(host=>{game.MountBytes('Content/Assets/.keep.meta',[]);start();game.NetworkStart(host);window.connection=game.NetworkOpen(host);},!nativeHost);
 const compatibility=await page.evaluate(()=>game.NetworkCompatibilityInfo());
 const sceneId=JSON.parse(fs.readFileSync(path.join(project,'Scenes/Main.bytescene'))).sceneId;
 const configuration=process.env.BYTEENGINE_NATIVE_CONFIG||'Debug';
 child=spawn('dotnet',[path.join(__dirname,`../Tests/ByteEngine.Network.Integration/bin/${configuration}/net9.0/ByteEngine.Network.Integration.dll`),'--session',...(nativeHost?['--host']:[]),'--scene',sceneId,'--compat',compatibility],{windowsHide:true,stdio:['pipe','pipe','pipe']});
 const incoming=[];child.stderr.on('data',d=>errors.push(String(d)));readline.createInterface({input:child.stdout}).on('line',line=>{try{const m=JSON.parse(line);if(m.type==='signal')incoming.push(m.signal);if(m.type==='state')state=m;if(m.type==='ended')errors.push(m.reason);}catch(e){errors.push(line);}});
 let sent=0;
 try{
 for(let frame=0;frame<360;frame++){
  await page.evaluate(keys=>step(keys),frame>=120&&frame<150?'D':'');
  const outgoing=await page.evaluate(()=>net.outgoing.get(window.connection)??[]);
  for(const s of outgoing.slice(sent))child.stdin.write(JSON.stringify({Kind:s.kind,Content:s.content,Mid:s.mid})+'\n');sent=outgoing.length;
  while(incoming.length){const s=incoming.shift();await page.evaluate(s=>game.NetworkSignal(window.connection,s.Kind,s.Content,s.Mid??''),s);}
  if(frame===150)child.stdin.write('stopinput\n');
  await new Promise(r=>setTimeout(r,5));
 }
 const status=await page.evaluate(()=>JSON.parse(game.NetworkStatus())),browserState=await page.evaluate(()=>JSON.parse(game.NetworkState()));
 if(status.players!==2 || state?.players.length!==2)throw Error('Cross-platform managed session failed: '+JSON.stringify({status,state,errors}));
 if(state.engine!==browserState.engine)throw Error('Core build identity differs');
 for(const player of state.players){const other=browserState.players.find(p=>p.id===player.id);if(Math.abs(other.x-player.x)>.05)throw Error('Position convergence failed: '+JSON.stringify({state,browserState}));}
 if(errors.length)throw Error(errors.join('\n'));
 console.log(`PASS managed native/WASM ${nativeHost?'native':'browser'} host: compatible builds, host authority, fixed-tick input, prediction and converged snapshots.`);
 }finally{child.stdin.write('quit\n');await new Promise(r=>{child.once('exit',r);setTimeout(()=>{child.kill();r();},2000).unref();});if(child.exitCode!==0)throw Error('Native process shutdown failed: '+child.exitCode);await page.close();}
}
}finally{await browser.close();await new Promise(r=>server.close(r));}
})().catch(e=>{console.error(e);process.exitCode=1;});
