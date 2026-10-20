export class BrowserVideos {
    constructor(files,canvas,report){this.files=files;this.canvas=canvas;this.report=report;this.items=new Map();}
    command(c){
        if(c.kind==='videoLoad'){
            this.dispose(c.id);const bytes=this.files.get(c.path);if(!bytes){this.report(c.id,0,true,false,'Video was not packaged: '+c.path);return;}
            const url=URL.createObjectURL(new Blob([bytes],{type:/\.webm$/i.test(c.path)?'video/webm':/\.og[gv]$/i.test(c.path)?'video/ogg':'video/mp4'}));
            const video=document.createElement('video');video.preload='auto';video.playsInline=true;video.src=url;
            Object.assign(video.style,{position:'fixed',pointerEvents:'none',objectFit:'contain',zIndex:'2',display:'none'});document.body.append(video);
            const state={video,url,error:'',started:false};video.onerror=()=>state.error=video.error?.message||'Unsupported or damaged video';this.items.set(c.id,state);return;
        }
        const state=this.items.get(c.id);if(!state)return;const v=state.video;
        if(c.kind==='videoPlay'){state.started=true;v.style.display='block';v.play().catch(e=>{state.error=e.message;});}
        else if(c.kind==='videoPause')v.pause();
        else if(c.kind==='videoStop'){v.pause();v.currentTime=0;state.started=false;v.style.display='none';}
        else if(c.kind==='videoSeek'){if(v.readyState)v.currentTime=c.seconds;else v.addEventListener('loadedmetadata',()=>v.currentTime=c.seconds,{once:true});}
        else if(c.kind==='videoRect'){state.rect=c;this.layout(state);}
        else if(c.kind==='videoDispose')this.dispose(c.id);
    }
    layout(state){if(!state.rect)return;const r=this.canvas.getBoundingClientRect(),c=state.rect;Object.assign(state.video.style,{left:(r.left+c.x*r.width)+'px',top:(r.top+c.y*r.height)+'px',width:c.w*r.width+'px',height:c.h*r.height+'px'});}
    update(){for(const [id,state]of this.items){this.layout(state);this.report(id,state.video.currentTime,state.started&&state.video.paused,state.video.ended,state.error);}}
    dispose(id){const state=this.items.get(id);if(!state)return;state.video.pause();state.video.removeAttribute('src');state.video.load();state.video.remove();URL.revokeObjectURL(state.url);this.items.delete(id);}
}
