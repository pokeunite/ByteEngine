// Version 1 UI records: two little-endian int32s followed by twelve float32s.
const kinds=['quad','image','clip','unclip'];
const coordinates=['x','y','w','h','sx','sy','sw','sh'];
export function decodeUiPacket(bytes,count,output=[]){
    if(!Number.isSafeInteger(count)||count<0||bytes.byteLength!==count*56)throw new Error('Invalid UI frame buffer');
    const view=new DataView(bytes.buffer,bytes.byteOffset,bytes.byteLength);
    for(let i=0;i<count;i++){
        const start=i*56,kind=view.getInt32(start,true);
        if(kind<0||kind>=kinds.length)throw new Error('Unknown UI frame command');
        const c=output[i]??(output[i]={color:[0,0,0,0]});
        c.kind=kinds[kind];c.texture=view.getInt32(start+4,true);
        for(let j=0;j<8;j++)c[coordinates[j]]=view.getFloat32(start+8+j*4,true);
        for(let j=0;j<4;j++)c.color[j]=view.getFloat32(start+40+j*4,true);
    }
    output.length=count;return output;
}

const waterFields=['column','motion','flow','impact','wake','absorption','scattering','style','waves','depthRect','depthSettings'];
export function decodeDrawPacket(bytes,output=[]){
    if(bytes.byteLength<8||bytes.byteOffset%4)throw new Error('Invalid draw frame buffer');
    const view=new DataView(bytes.buffer,bytes.byteOffset,bytes.byteLength);let offset=0;
    const int=()=>{if(offset+4>bytes.byteLength)throw new Error('Truncated draw frame');const n=view.getInt32(offset,true);offset+=4;return n;};
    const floats=count=>{if(count<0||offset+count*4>bytes.byteLength)throw new Error('Invalid draw frame dimensions');const a=new Float32Array(bytes.buffer,bytes.byteOffset+offset,count);offset+=count*4;return a;};
    if(int()!==0x31445742)throw new Error('Unknown draw frame version');const count=int();
    if(count<0||count>Math.floor((bytes.byteLength-8)/148))throw new Error('Invalid draw frame count');
    for(let i=0;i<count;i++){
        const c=output[i]??(output[i]={});c.id=int();c.material=int();const flags=int(),shadows=int(),instances=int();
        if(flags&~15||shadows<0||instances<0||shadows%16||instances%16)throw new Error('Invalid draw command');
        c.receiveShadows=!!(flags&1);c.model=floats(16);c.wet=floats(16);
        if(flags&2){const w=c.water??{};w.surface=!!(flags&4);w.reflections=!!(flags&8);w.depthTexture=int();w.size=floats(2);for(const name of waterFields)w[name]=floats(4);c.water=w;}else c.water=null;
        c.shadowInstances=floats(shadows);c.instances=floats(instances);
    }
    if(offset!==bytes.byteLength)throw new Error('Trailing draw frame data');output.length=count;return output;
}
