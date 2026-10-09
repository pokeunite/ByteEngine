export class BrowserAudio {
    constructor(files, ended) {
        this.context=new AudioContext();this.master=this.context.createGain();this.master.connect(this.context.destination);
        this.files=files;this.ended=ended;this.buffers=new Map();this.sources=new Map();this.urls=new Map();this.bufferBytes=new Map();this.maximumDecodedBytes=32*1024*1024;
    }
    async activate(){await this.context.resume();for(const state of this.sources.values())if(state.media&&state.wantsPlay)await state.media.play().catch(()=>{});}
    // Keep encoded files until first use. Music uses browser-managed media streaming, not decodeAudioData.
    async decode() {}
    state(id){
        let state=this.sources.get(id);if(state)return state;
        const gain=this.context.createGain(),panner=this.context.createPanner();panner.panningModel='HRTF';panner.distanceModel='inverse';panner.connect(this.master);gain.connect(this.master);
        state={gain,panner,offset:0,pitch:1,generation:0};this.sources.set(id,state);return state;
    }
    async play(c){
        const state=this.state(c.id);this.halt(state);const generation=++state.generation;
        if(state.path!==c.path)state.offset=0;state.path=c.path;state.loop=c.loop;state.wantsPlay=true;
        const bytes=this.files.get(c.path);if(!bytes)throw new Error('Audio was not packaged: '+c.path);
        if(c.stream||bytes.byteLength>2*1024*1024){
            let url=this.urls.get(c.path);if(!url){url=URL.createObjectURL(new Blob([bytes],{type:/\.ogg$/i.test(c.path)?'audio/ogg':'audio/wav'}));this.urls.set(c.path,url);}
            const media=new Audio(url);media.preload='metadata';media.loop=c.loop;media.playbackRate=state.pitch;media.preservesPitch=false;
            const node=this.context.createMediaElementSource(media);node.connect(state.gain);state.media=media;state.mediaNode=node;state.mediaUrl=url;this.prune();
            if(state.offset)media.addEventListener('loadedmetadata',()=>{media.currentTime=state.offset%Math.max(.001,media.duration);},{once:true});
            media.onended=()=>{if(state.media===media){state.wantsPlay=false;state.offset=0;this.ended(c.id);}};
            await media.play().catch(error=>{if(this.sources.get(c.id)!==state||state.generation!==generation||!state.wantsPlay)return;if(error.name!=='NotAllowedError')throw error;});return;
        }
        let pending=this.buffers.get(c.path);
        if(!pending){pending=this.context.decodeAudioData(bytes.slice(0)).then(buffer=>{this.bufferBytes.set(c.path,buffer.length*buffer.numberOfChannels*4);this.prune();return buffer;}).catch(error=>{this.buffers.delete(c.path);this.bufferBytes.delete(c.path);throw error;});this.buffers.set(c.path,pending);}
        const buffer=await pending;
        if(this.sources.get(c.id)!==state||state.generation!==generation||!state.wantsPlay)return;
        const source=this.context.createBufferSource();source.buffer=buffer;source.loop=state.loop;source.playbackRate.value=state.pitch;source.connect(state.gain);
        state.node=source;state.started=this.context.currentTime;source.onended=()=>{if(state.node===source){state.node=null;state.offset=0;state.wantsPlay=false;this.ended(c.id);}};
        source.start(0,state.offset%Math.max(buffer.duration,.001));
    }
    commands(commands){
        for(const c of commands){
            if(c.kind==='listener'){
                const l=this.context.listener,t=this.context.currentTime;
                for(const [key,value]of Object.entries({positionX:c.position[0],positionY:c.position[1],positionZ:c.position[2],forwardX:c.forward[0],forwardY:c.forward[1],forwardZ:c.forward[2],upX:c.up[0],upY:c.up[1],upZ:c.up[2]}))l[key]?.setValueAtTime(value,t);
                this.master.gain.value=c.volume;continue;
            }
            let state=this.sources.get(c.id);
            if(c.kind==='play'){this.play(c).catch(error=>{console.error('Audio playback failed',error);this.ended(c.id);});}
            else if(c.kind==='pause'){
                if(state){state.wantsPlay=false;state.generation++;if(state.media)state.offset=state.media.currentTime;else if(state.node)state.offset+=(this.context.currentTime-state.started)*state.pitch;this.halt(state);}
            }else if(c.kind==='stop'){
                if(state){state.wantsPlay=false;state.generation++;this.halt(state);state.gain.disconnect();state.panner.disconnect();this.sources.delete(c.id);}
            }else if(c.kind==='settings'&&state){
                if(state.node&&state.pitch!==c.pitch){state.offset+=(this.context.currentTime-state.started)*state.pitch;state.started=this.context.currentTime;}
                state.pitch=c.pitch;state.loop=c.loop;if(state.node){state.node.playbackRate.value=c.pitch;state.node.loop=c.loop;}if(state.media){state.media.playbackRate=c.pitch;state.media.loop=c.loop;}
                state.gain.gain.value=c.volume;if(state.spatial!==c.spatial){state.gain.disconnect();state.gain.connect(c.spatial?state.panner:this.master);state.spatial=c.spatial;}
                const p=state.panner,t=this.context.currentTime;p.refDistance=Math.max(.001,c.min);p.maxDistance=Math.max(p.refDistance,c.max);p.rolloffFactor=Math.max(0,c.rolloff);
                p.positionX.setValueAtTime(c.position[0],t);p.positionY.setValueAtTime(c.position[1],t);p.positionZ.setValueAtTime(c.position[2],t);
            }
        }
    }
    prune(){
        let total=0;for(const size of this.bufferBytes.values())total+=size;
        for(const [path,size] of this.bufferBytes){if(total<=this.maximumDecodedBytes)break;this.buffers.delete(path);this.bufferBytes.delete(path);total-=size;}
        for(const [path,url] of this.urls){if(this.urls.size<=16)break;if([...this.sources.values()].some(state=>state.mediaUrl===url))continue;URL.revokeObjectURL(url);this.urls.delete(path);}
    }
    halt(state){
        if(state.node){state.node.onended=null;state.node.stop();state.node.disconnect();state.node=null;}
        if(state.media){state.media.onended=null;state.media.pause();state.media.removeAttribute('src');state.media.load();state.media=null;state.mediaNode?.disconnect();state.mediaNode=null;state.mediaUrl=null;this.prune();}
    }
}
