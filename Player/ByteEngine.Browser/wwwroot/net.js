// One WebRTC link. Signaling is supplied by the host UI/service; no game data uses JSON.
export class ByteNetworkLink {
  constructor({iceServers = [], onSignal, onMessage, onDisconnect} = {}) {
    this.pc = new RTCPeerConnection({iceServers});
    this.channels = new Map(); this.queue = []; this.closed = false; this.ended = false;
    this.onSignal = onSignal; this.onMessage = onMessage; this.onDisconnect = onDisconnect;
    this.pc.onicecandidate = e => { if(e.candidate) this.enqueue(() => this.onSignal?.({kind:'candidate',content:e.candidate.candidate,mid:e.candidate.sdpMid ?? '0'})); };
    this.pc.ondatachannel = e => this.bind(e.channel);
    this.pc.onconnectionstatechange = () => { if(['failed','closed','disconnected'].includes(this.pc.connectionState)) this.enqueue(() => this.end(`WebRTC ${this.pc.connectionState}`)); };
  }
  enqueue(fn) { if(this.closed)return; if(this.queue.length >= 256){this.overflow=true;return;} this.queue.push(fn); }
  end(reason) { if(this.ended)return; this.ended=true; this.onDisconnect?.(reason); }
  bind(channel) {
    const kind = channel.label === 'ben-state' ? 0 : channel.label === 'ben-events' ? 1 : -1;
    if(kind<0 || this.channels.has(kind)){channel.close();return;}
    channel.binaryType='arraybuffer'; this.channels.set(kind,channel);
    channel.onmessage = e => {
      if(!(e.data instanceof ArrayBuffer) || e.data.byteLength > 65536){this.overflow=true;return;}
      const copy = new Uint8Array(e.data).slice(); this.enqueue(() => this.onMessage?.(kind,copy));
    };
    channel.onclose = () => this.enqueue(() => this.end('channel closed'));
    channel.onerror = () => this.enqueue(() => this.end('data channel error'));
  }
  async start(offerer) {
    if(this.started)throw new Error('Link already started');this.started=true;
    if(!offerer)return;
    this.bind(this.pc.createDataChannel('ben-state',{ordered:false,maxRetransmits:0}));
    this.bind(this.pc.createDataChannel('ben-events',{ordered:true}));
    await this.pc.setLocalDescription(await this.pc.createOffer());
    this.enqueue(() => this.onSignal?.({kind:'offer',content:this.pc.localDescription.sdp,mid:''}));
  }
  async accept(signal) {
    if(typeof signal.content !== 'string' || signal.content.length>131072)throw new Error('Invalid signaling message');
    if(signal.kind==='candidate') {
      const candidate={candidate:signal.content,sdpMid:signal.mid};
      if(!this.pc.remoteDescription){(this.pendingCandidates??=[]).push(candidate);if(this.pendingCandidates.length>128)throw new Error('Too many ICE candidates');}
      else await this.pc.addIceCandidate(candidate);
      return;
    }
    if(!['offer','answer'].includes(signal.kind))throw new Error('Unknown signaling message');
    await this.pc.setRemoteDescription({type:signal.kind,sdp:signal.content});
    for(const c of this.pendingCandidates??[]) await this.pc.addIceCandidate(c);
    this.pendingCandidates=[];
    if(signal.kind==='offer') {
      await this.pc.setLocalDescription(await this.pc.createAnswer());
      this.enqueue(() => this.onSignal?.({kind:'answer',content:this.pc.localDescription.sdp,mid:''}));
    }
  }
  get connected(){return !this.closed && this.channels.size===2 && [...this.channels.values()].every(c=>c.readyState==='open');}
  send(kind,bytes) {
    const c=this.channels.get(kind);
    if(this.closed || !c || c.readyState!=='open' || bytes.byteLength>(kind===0?1200:65536) || c.bufferedAmount>262144)return false;
    try{c.send(bytes);return true;}catch{return false;}
  }
  poll(){if(this.overflow){this.end('receive queue capacity exceeded');this.dispose();return;} const batch=this.queue.splice(0,256);for(const action of batch)action();}
  dispose(){if(this.closed)return;this.closed=true;this.queue=[];for(const c of this.channels.values())c.close();this.channels.clear();this.pc.close();}
}
