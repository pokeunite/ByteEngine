import {qualityProfiles} from './shared-render.js';
// World resolution is independent of layout and the full-resolution UI overlay.
export class BrowserPresentation {
    constructor(){this.factor=1;this.minimumFactor=.6;this.samples=[];this.lastChange=0;this.key='';}
    dimensions(cssWidth,cssHeight,dpr,quality='Balanced',resolution='Auto',maxTextureSize=4096){
        const key=quality+'/'+resolution;
        if(key!==this.key){this.key=key;this.factor=1;this.samples=[];}
        const width=Math.max(1,Math.round(cssWidth)),height=Math.max(1,Math.round(cssHeight));
        const uiScale=Math.min(Math.max(1,dpr||1),2,maxTextureSize/Math.max(width,height));
        const profile=qualityProfiles[quality]||qualityProfiles.Balanced,budget=[profile.WorldWidth,profile.WorldHeight];
        let scale=Math.min(uiScale,budget[0]/width,budget[1]/height);
        if(resolution==='Native')scale=uiScale;
        else if(resolution==='1080p')scale=Math.min(uiScale,1920/width,1080/height);
        else if(resolution==='720p')scale=Math.min(uiScale,1280/width,720/height);
        else if(resolution==='75%')scale=uiScale*.75;
        else if(resolution==='50%')scale=uiScale*.5;
        else {
            // Auto may reduce larger targets to a fitted 720p image, never turn a
            // 720p window into a blurry 432p world. Smaller windows retain native pixels.
            const floor=Math.min(uiScale,1280/width,720/height);
            this.minimumFactor=Math.min(1,floor/Math.max(.0001,scale));
            this.factor=Math.max(this.minimumFactor,this.factor);scale*=this.factor;
        }
        scale=Math.min(scale,maxTextureSize/Math.max(width,height));
        return {width,height,worldWidth:Math.max(1,Math.round(width*scale)),worldHeight:Math.max(1,Math.round(height*scale)),uiWidth:Math.max(1,Math.round(width*uiScale)),uiHeight:Math.max(1,Math.round(height*uiScale)),scale};
    }
    sample(gpuMs,now,resolution,pending=0){
        // Ignore simulation time, startup uploads and short spikes. No GPU timer: keep selected quality.
        if(resolution!=='Auto'||pending||!Number.isFinite(gpuMs)||gpuMs<=0)return;
        this.samples.push(gpuMs);if(this.samples.length<180||now-this.lastChange<5000)return;
        const samples=this.samples.splice(0).sort((a,b)=>a-b),p75=samples[Math.floor(samples.length*.75)];
        const next=p75>20?Math.max(this.minimumFactor,this.factor-.1):p75<11?Math.min(1,this.factor+.05):this.factor;
        if(next!==this.factor){this.factor=next;this.lastChange=now;}
    }
}
