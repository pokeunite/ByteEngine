// Version 1 UI records: two little-endian int32s followed by twelve float32s.
const kinds=['quad','image','clip','unclip','line'];
const coordinates=['x','y','w','h','sx','sy','sw','sh'];
export function decodeDrawPacket(bytes,command){
    if(bytes.byteLength<136||bytes.byteLength%4||bytes.byteOffset%4)throw new Error('Invalid draw frame buffer');
    const header=new DataView(bytes.buffer,bytes.byteOffset,8),instances=header.getInt32(0,true),shadows=header.getInt32(4,true);
    if(instances<0||shadows<0||bytes.byteLength!==136+64*(instances+shadows))throw new Error('Invalid draw instance counts');
    const values=new Float32Array(bytes.buffer,bytes.byteOffset+8,(bytes.byteLength-8)/4);
    command.model=values.subarray(0,16);command.wet=values.subarray(16,32);
    command.instances=values.subarray(32,32+instances*16);command.shadowInstances=values.subarray(32+instances*16);
    return command;
}
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

// Own only changed packets: transferred frame storage is returned after rendering.
export class RetainedDrawPackets {
    constructor(){this.packets=[];this.skins=[];}
    apply(commands,read,slotCount=commands.length){
        if(!Number.isSafeInteger(slotCount)||slotCount<0)throw Error("Invalid retained slot count");
        for(const command of commands){
            if(command.packetSlot===undefined||command.packetSlot<0){if(command.dataBuffer!==undefined)decodeDrawPacket(read(command.dataBuffer),command);continue;}
            const slot=command.packetSlot;
            if(!Number.isSafeInteger(slot)||slot<0||slot>=slotCount)throw Error('Invalid retained packet slot');
            command.packetChanged=command.dataBuffer>=0;
            if(command.packetChanged){const bytes=read(command.dataBuffer);if(this.packets[slot]?.length!==bytes.length)this.packets[slot]=new Uint8Array(bytes.length);this.packets[slot].set(bytes);}
            const packet=this.packets[slot];if(!packet)throw Error('Missing retained draw packet');
            decodeDrawPacket(packet,command);
            if(command.skinBuffer>=0){const bytes=read(command.skinBuffer);if(this.skins[slot]?.length!==bytes.length)this.skins[slot]=new Uint8Array(bytes.length);this.skins[slot].set(bytes);}
            if(!command.skinned)this.skins[slot]=null;
            command.skinPacket=this.skins[slot];
            if(command.skinned&&!command.skinPacket)throw Error('Missing retained skin packet');
        }
        this.packets.length=slotCount;this.skins.length=slotCount;
    }
}
