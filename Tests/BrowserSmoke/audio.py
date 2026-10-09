from pathlib import Path
import http.server,threading,sys,json,wave,io,shutil,math,struct
from playwright.sync_api import sync_playwright
root=Path(sys.argv[1]).resolve();root.mkdir(parents=True,exist_ok=True)
shutil.copy2(Path(__file__).resolve().parents[2]/"Player/ByteEngine.Browser/wwwroot/audio.js",root/"audio.js")
(root/"index.html").write_text("<button id='start'>Start audio test</button>")
stream=io.BytesIO()
with wave.open(stream,"wb") as w:
 w.setnchannels(1);w.setsampwidth(2);w.setframerate(22050);w.writeframes(b"".join(struct.pack("<h",int(1000*math.sin(i*2*math.pi*220/22050))) for i in range(22050*10)))
(root/"music.wav").write_bytes(stream.getvalue())
class Handler(http.server.SimpleHTTPRequestHandler):
 def __init__(self,*args,**kwargs):super().__init__(*args,directory=str(root),**kwargs)
 def log_message(self,*args):pass
server=http.server.ThreadingHTTPServer(("127.0.0.1",8795),Handler);threading.Thread(target=server.serve_forever,daemon=True).start()
try:
 with sync_playwright() as p:
  browser=p.chromium.launch(channel="chrome" if sys.platform=="win32" else None,headless=True,args=["--autoplay-policy=no-user-gesture-required"])
  page=browser.new_page();errors=[];page.on("pageerror",lambda e:errors.append(str(e)));page.goto("http://127.0.0.1:8795")
  result=page.evaluate("""async()=>{
   const {BrowserAudio}=await import('./audio.js');const bytes=await(await fetch('./music.wav')).arrayBuffer();let ended=0;
   const files=new Map();for(let i=0;i<20;i++)files.set('music'+i+'.wav',bytes);
   const audio=new BrowserAudio(files,()=>ended++);await audio.activate();await audio.decode();
   if(audio.bufferBytes.size)throw Error('Eager full-file decode');
   await audio.play({id:'stream',path:'music0.wav',stream:true,loop:true});
   const state=audio.sources.get('stream');if(!state.media||audio.buffers.size)throw Error('Music not using media streaming');
   await new Promise(r=>setTimeout(r,1500));if(state.media.currentTime<=0)throw Error('Music not progressing '+JSON.stringify({time:state.media.currentTime,paused:state.media.paused,ready:state.media.readyState,context:audio.context.state,error:state.media.error?.message}));
   audio.commands([{kind:'settings',id:'stream',volume:.16,pitch:1,loop:true,spatial:false,position:[0,0,0],min:1,max:100,rolloff:1}]);
   if(Math.abs(state.gain.gain.value-.16)>.0001)throw Error('Active bus gain not applied');
   audio.commands([{kind:'pause',id:'stream'}]);if(state.media||state.offset<=0)throw Error('Pause/resume offset lost');
   await audio.play({id:'stream',path:'music0.wav',stream:true,loop:true});audio.commands([{kind:'stop',id:'stream'}]);
   if(audio.sources.size)throw Error('Stopped stream retained voice');
   for(let i=0;i<20;i++){await audio.play({id:'voice',path:'music'+i+'.wav',stream:true,loop:true});audio.commands([{kind:'stop',id:'voice'}]);}
   if(audio.urls.size>16)throw Error('Inactive media URLs unbounded');
   audio.maximumDecodedBytes=100;await audio.play({id:'clip',path:'music0.wav',stream:false,loop:false});
   if(!audio.sources.get('clip').node||[...audio.bufferBytes.values()].reduce((a,b)=>a+b,0)>100)throw Error('Clip cache budget ignored');
   audio.commands([{kind:'stop',id:'clip'}]);
   const pending=audio.play({id:'cancel',path:'music1.wav',stream:true,loop:false});audio.commands([{kind:'pause',id:'cancel'}]);await pending;audio.commands([{kind:'stop',id:'cancel'}]);
   if(ended)throw Error('Stopped/paused stream falsely reported completion');
   const result={urls:audio.urls.size,decodedCacheBytes:[...audio.bufferBytes.values()].reduce((a,b)=>a+b,0),voices:audio.sources.size,checks:'lazy decode, media playback, pause/resume/stop, gain, URL/cache limits and pending-play cancellation'};
   for(const url of audio.urls.values())URL.revokeObjectURL(url);await audio.context.close();return result;
  }""")
  assert not errors,errors;(root/"audio-smoke.json").write_text(json.dumps(result,indent=2));print("PASS browser audio",result,flush=True);browser.close()
finally:server.shutdown()
