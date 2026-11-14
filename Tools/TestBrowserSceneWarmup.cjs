const fs=require('fs'),vm=require('vm'),assert=require('assert');
const source=fs.readFileSync('Player/ByteEngine.Browser/wwwroot/main.js','utf8');
const body=source.slice(source.indexOf('    function frame(now){'),source.indexOf('    // Initial capture'));
const packets=[],deltas=[],events=[],errors=[];
const canvas={clientWidth:1280,clientHeight:720,style:{},focus(){}},overlay={style:{}},status={style:{}};
const context={performance:{now:()=>1},document:{hidden:false,activeElement:canvas,pointerLockElement:null},navigator:{getGamepads:()=>[]},devicePixelRatio:1,requestAnimationFrame:()=>{},canvas,overlay,status,
 presentation:{dimensions:()=>({width:1280,height:720,worldWidth:1280,worldHeight:720,uiWidth:1280,uiHeight:720}),sample(){},factor:1},
 graphics:{quality:'Balanced',worldResolution:'Auto'},renderer:{capabilities:{maxTextureSize:4096},profile:{uploadMs:0},draw(){}},
 game:{Frame:(dt)=>{deltas.push(dt);return JSON.stringify(packets.shift());},WantsPointerLock:()=>false},
 capture:{probes:{},event:(n,v)=>events.push([n,v]),frame(){}},audio:{commands(){}},keys:new Set(),keyPulses:new Set(),buttons:new Set(),buttonPulses:new Set(),pointer:[.5,.5],delta:[0,0],wheel:0,previous:0,graphicsReady:false,previousPending:-1,sceneRevision:-1,readyFrames:0,stopped:false,
 byteEnginePerf:{frames:[]},fail:e=>errors.push(e),uiCommands:[],drawCommands:[]};
vm.createContext(context);vm.runInContext(body,context);
let now=0;function tick(rev,pending){packets.push({sceneRevision:rev,pendingTextures:pending,draws:[],ui:[],textures:[],audio:[],uiBuffer:-1,drawBuffer:-1});context.frame(now+=16);assert.deepEqual(errors,[]);}
tick(1,20);assert.equal(canvas.style.visibility,'hidden');tick(1,0);assert.equal(canvas.style.visibility,'hidden');tick(1,0);assert.equal(canvas.style.visibility,'visible');tick(1,5);assert.equal(canvas.style.visibility,'visible','Background updates must not hide gameplay');tick(2,10);assert.equal(canvas.style.visibility,'hidden','A second scene must warm textures');tick(2,0);assert.equal(deltas.at(-1),0,'Warmup freezes the puzzle');tick(2,0);assert.equal(canvas.style.visibility,'visible');tick(3,0);assert.equal(canvas.style.visibility,'hidden','Reloads with resident textures still get one stable frame');tick(3,0);assert.equal(canvas.style.visibility,'visible');
console.log('PASS browser scene transitions, texture warmup, frozen simulation and background uploads');
