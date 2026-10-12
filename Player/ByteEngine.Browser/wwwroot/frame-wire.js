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
