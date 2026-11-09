// World resolution is independent of layout and the full-resolution UI overlay.
export class BrowserPresentation {
    constructor(){this.factor=1;this.samples=[];this.lastChange=0;this.key='';}
    dimensions(cssWidth,cssHeight,dpr,quality='Balanced',resolution='720p',maxTextureSize=4096,gpuTimingAvailable=true){
        const key=quality+'/'+resolution+'/'+cssWidth+'/'+cssHeight+'/'+dpr+'/'+gpuTimingAvailable;
        if(key!==this.key){this.key=key;this.factor=1;this.samples=[];}
        const width=Math.max(1,Math.round(cssWidth)),height=Math.max(1,Math.round(cssHeight));
        const uiBudget=quality==='High'?[3840,2160]:[1920,1080];
        const pixelScale=Math.min(Math.max(1,dpr||1),2,maxTextureSize/Math.max(width,height));
        const uiScale=Math.min(pixelScale,uiBudget[0]/width,uiBudget[1]/height);
        const budget=quality==='High'?[2560,1440]:quality==='Fast'?[1280,720]:[1920,1080];
        let scale=Math.min(pixelScale,budget[0]/width,budget[1]/height);
        if(resolution==='Native')scale=pixelScale;
        else if(resolution==='1080p')scale=Math.min(pixelScale,1920/width,1080/height);
        else if(resolution==='720p')scale=Math.min(pixelScale,1280/width,720/height);
        else if(resolution==='75%')scale=pixelScale*.75;
        else if(resolution==='50%')scale=pixelScale*.5;
        else {if(!gpuTimingAvailable)scale=Math.min(scale,1280/width,720/height);scale*=this.factor;}
        scale=Math.min(scale,maxTextureSize/Math.max(width,height));
        this.expectedWidth=Math.max(1,Math.round(width*scale));this.expectedHeight=Math.max(1,Math.round(height*scale));return {width,height,worldWidth:Math.max(1,Math.round(width*scale)),worldHeight:Math.max(1,Math.round(height*scale)),uiWidth:Math.max(1,Math.round(width*uiScale)),uiHeight:Math.max(1,Math.round(height*uiScale)),scale};
    }
    sample(gpuMs,now,resolution,pending=0,sampleId=null,sampleWidth=null,sampleHeight=null){
        if(sampleId!==null){if(sampleId===this.lastSampleId)return;this.lastSampleId=sampleId;if(sampleWidth!==this.expectedWidth||sampleHeight!==this.expectedHeight)return;}
        // Ignore simulation time, startup uploads and short spikes. No GPU timer: keep selected quality.
        if(resolution!=='Auto'||pending||!Number.isFinite(gpuMs)||gpuMs<=0)return;
        this.samples.push(gpuMs);if(this.samples.length<8||now-this.lastChange<1000)return;
        const samples=this.samples.splice(0).sort((a,b)=>a-b),p75=samples[Math.floor(samples.length*.75)];
        const next=p75>40?Math.max(.5,this.factor*.75):p75>15?Math.max(.5,this.factor-.1):p75<11&&now-this.lastChange>=5000?Math.min(1,this.factor+.05):this.factor;
        if(next!==this.factor){this.factor=next;this.lastChange=now;}
    }
}
