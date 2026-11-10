import {ByteNetworkLink} from './net.js';
export class BrowserNetworkBridge {
 constructor(game,readBuffer){this.game=game;this.readBuffer=readBuffer;this.links=new Map();this.outgoing=new Map();this.selected=-1;this.error='';this.timer=setInterval(()=>{for(const link of this.links.values())link.poll();},10);}
 commands(commands){for(const command of commands??[]){const id=command.id;try{
  if(command.kind==='download'){const url=URL.createObjectURL(new Blob([command.signal.Content],{type:'text/plain'}));const a=document.createElement('a');a.href=url;a.download=command.signal.Mid||'network-errors.log';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);}
  else if(command.kind==='start'){
   const status=JSON.parse(this.game.NetworkStatus());
   const link=new ByteNetworkLink({iceServers:status.iceServers.map(url=>{const match=/^(turns?):([^:@]+):([^@]+)@(.+)$/.exec(url);return match?{urls:`${match[1]}:${match[4]}`,username:decodeURIComponent(match[2]),credential:decodeURIComponent(match[3])}:{urls:url};}),onSignal:signal=>{const list=this.outgoing.get(id)??[];if(list.length<128)list.push(signal);this.outgoing.set(id,list);this.game.NetworkNotify(id,'signal',JSON.stringify({Kind:signal.kind,Content:signal.content,Mid:signal.mid}));this.render();},onMessage:(kind,bytes)=>this.game.NetworkReceive(id,kind,bytes),onDisconnect:reason=>{this.error=reason;this.game.NetworkNotify(id,'error',reason);this.render();}});
   this.links.set(id,link);link.start(command.offerer).catch(e=>this.fail(id,e));
  }else if(command.kind==='signal'){const s=command.signal;this.links.get(id)?.accept({kind:s.Kind,content:s.Content,mid:s.Mid}).catch(e=>this.fail(id,e));}
  else if(command.kind==='send'){const bytes=new Uint8Array(this.readBuffer(command.buffer)).slice();if(!this.links.get(id)?.send(command.channel,bytes))this.fail(id,Error('network send backpressure'));}
  else if(command.kind==='close'){this.links.get(id)?.dispose();this.links.delete(id);this.outgoing.delete(id);}
 }catch(e){this.fail(id,e);}}
 for(const [id,link] of this.links)if(link.connected&&!link.notified){link.notified=true;this.game.NetworkNotify(id,'connected','');}
 }
 fail(id,e){this.error=String(e);this.game.NetworkNotify(id,'error',this.error);this.render();}
 installLobby(){if(!this.game.NetworkAvailable())return;
  const root=document.createElement('details');root.style='position:fixed;z-index:10000;top:8px;right:8px;background:#181c22;color:white;padding:10px;font:13px sans-serif;max-width:360px';root.innerHTML='<summary>Multiplayer</summary><p>Host creates an invite. Joiner pastes it and returns an answer.</p><button data-host>Host</button> <button data-join>Join</button> <button data-invite>New invite</button><p><select data-connections></select></p><textarea data-out readonly rows="4" style="width:330px" aria-label="Outgoing connection data"></textarea><button data-copy>Copy connection data</button><textarea data-in rows="4" style="width:330px" aria-label="Incoming connection data"></textarea><button data-apply>Apply incoming data</button> <button data-leave>Leave</button><p data-status></p>';
  document.body.appendChild(root);this.root=root;const q=s=>root.querySelector(s);
  const start=host=>{try{this.game.NetworkStart(host);this.selected=-1;this.outgoing.clear();this.error='';if(!host)this.selected=this.game.NetworkOpen(false);this.render();}catch(e){this.error=String(e);this.render();}};
  q('[data-host]').onclick=()=>start(true);q('[data-join]').onclick=()=>start(false);
  q('[data-invite]').onclick=()=>{try{this.selected=this.game.NetworkOpen(true);this.outgoing.set(this.selected,[]);this.render();}catch(e){this.error=String(e);this.render();}};
  q('[data-copy]').onclick=()=>{q('[data-out]').select();document.execCommand('copy');};
  q('[data-connections]').onchange=e=>{this.selected=Number(e.target.value);this.render();};
  q('[data-apply]').onclick=()=>{try{const text=q('[data-in]').value;if(text.length>262144)throw Error('Connection data too large');const signals=JSON.parse(text);if(!Array.isArray(signals)||signals.length>128)throw Error('Invalid connection data');for(const s of signals)this.game.NetworkSignal(this.selected,s.kind,s.content,s.mid??'');q('[data-in]').value='';}catch(e){this.error=String(e);}this.render();};
  q('[data-leave]').onclick=()=>{this.game.NetworkStop();this.render();};
  this.statusTimer=setInterval(()=>this.render(),1000);this.render();
 }
 render(){if(!this.root)return;const select=this.root.querySelector('[data-connections]');const ids=[...new Set([...this.links.keys(),...this.outgoing.keys(),...(this.selected>=0?[this.selected]:[])])];select.replaceChildren(...ids.map(id=>{const option=document.createElement('option');option.value=id;option.textContent=`Connection ${id}`;option.selected=id===this.selected;return option;}));this.root.querySelector('[data-out]').value=JSON.stringify(this.outgoing.get(this.selected)??[]);const s=JSON.parse(this.game.NetworkStatus());this.root.querySelector('[data-status]').textContent=this.error||`${s.active?(s.host?'Hosting':'Joining'):'Offline'} · peer ${s.peer} · ${s.players} players · ${(s.sentBytes/1024).toFixed(1)} KB sent · ${(s.receivedBytes/1024).toFixed(1)} KB received · ${s.rttMs.toFixed(0)} ms RTT`;}
 dispose(){clearInterval(this.timer);clearInterval(this.statusTimer);for(const l of this.links.values())l.dispose();this.links.clear();this.root?.remove();}
}
