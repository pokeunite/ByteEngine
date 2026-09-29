export class BrowserAudio {
    constructor(files, ended) {
        this.context = new AudioContext();
        this.master = this.context.createGain(); this.master.connect(this.context.destination);
        this.files = files; this.ended = ended; this.buffers = new Map(); this.sources = new Map();
        this.pending = Promise.resolve();
    }
    async activate() { await this.context.resume(); }
    async decode() {
        for (const [path, bytes] of this.files) this.buffers.set(path, await this.context.decodeAudioData(bytes.slice(0)));
        this.files.clear();
    }
    commands(commands) {
        for (const c of commands) {
            if (c.kind==='listener') {
                const l=this.context.listener, t=this.context.currentTime;
                for(const [key,value] of Object.entries({
                    positionX:c.position[0],positionY:c.position[1],positionZ:c.position[2],
                    forwardX:c.forward[0],forwardY:c.forward[1],forwardZ:c.forward[2],
                    upX:c.up[0],upY:c.up[1],upZ:c.up[2]})) l[key]?.setValueAtTime(value,t);
                this.master.gain.value=c.volume; continue;
            }
            let state=this.sources.get(c.id);
            if(c.kind==='play') {
                const buffer=this.buffers.get(c.path);
                if(!buffer) throw new Error('Audio was not packaged: '+c.path);
                if(!state) {
                    const gain=this.context.createGain(), panner=this.context.createPanner();
                    panner.panningModel='HRTF';panner.distanceModel='inverse';panner.connect(this.master);
                    state={gain,panner,offset:0,pitch:1};this.sources.set(c.id,state);
                    gain.connect(this.master);
                }
                this.halt(state);
                // Pause preserves the offset; Stop/replay resets it.
                if(state.path!==c.path) state.offset=0;
                state.path=c.path;state.loop=c.loop;
                const source=this.context.createBufferSource();
                source.buffer=buffer;source.loop=c.loop;source.playbackRate.value=state.pitch;
                source.connect(state.gain);state.node=source;state.started=this.context.currentTime;
                source.onended=()=>{if(state.node===source){state.node=null;state.offset=0;this.ended(c.id);}};
                source.start(0, state.offset % Math.max(buffer.duration,.001));
            } else if(c.kind==='pause') {
                if(state?.node) state.offset+=(this.context.currentTime-state.started)*state.pitch;
                if(state) this.halt(state);
            } else if(c.kind==='stop') {
                if(state){this.halt(state);state.offset=0;state.gain.disconnect();state.panner.disconnect();this.sources.delete(c.id);}
            } else if(c.kind==='settings' && state) {
                if(state.node && state.pitch!==c.pitch) {
                    state.offset+=(this.context.currentTime-state.started)*state.pitch;state.started=this.context.currentTime;
                }
                state.pitch=c.pitch;state.loop=c.loop;
                if(state.node){state.node.playbackRate.value=c.pitch;state.node.loop=c.loop;}
                state.gain.gain.value=c.volume;
                if(state.spatial!==c.spatial){state.gain.disconnect();state.gain.connect(c.spatial?state.panner:this.master);state.spatial=c.spatial;}
                const p=state.panner,t=this.context.currentTime;
                p.refDistance=Math.max(.001,c.min);p.maxDistance=Math.max(p.refDistance,c.max);
                p.rolloffFactor=Math.max(0,c.rolloff);
                p.positionX.setValueAtTime(c.position[0],t);p.positionY.setValueAtTime(c.position[1],t);p.positionZ.setValueAtTime(c.position[2],t);
            }
        }
    }
    halt(state) { if(state.node){state.node.onended=null;state.node.stop();state.node.disconnect();state.node=null;} }
}
