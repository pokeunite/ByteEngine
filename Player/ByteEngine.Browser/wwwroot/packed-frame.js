// WASM views expire at the next managed Frame. Copy once into owned, aligned
// storage before sending work to another thread; never transfer the WASM heap.
export class PackedFramePool {
    constructor(){this.pool=new Map();this.bytes=0;this.limit=64*1024*1024;}
    pack(frame,read){
        const count=(frame.bufferViews?.length??0)/2;
        if(!Number.isInteger(count))throw Error('Invalid frame buffer table');
        const ranges=new Uint32Array(count*2);let length=0;
        for(let i=0;i<count;i++){const source=read(i);ranges[i*2]=length;ranges[i*2+1]=source.byteLength;length=Math.ceil((length+source.byteLength)/4)*4;if(length>0xffffffff)throw Error('Frame buffer limit exceeded');}
        const list=this.pool.get(length);let packed=list?.pop();if(packed)this.bytes-=packed.byteLength;else packed=new ArrayBuffer(length);
        const bytes=new Uint8Array(packed);for(let i=0;i<count;i++)bytes.set(read(i),ranges[i*2]);
        return {packed,ranges};
    }
    release(buffer){
        if(!buffer?.byteLength||this.bytes+buffer.byteLength>this.limit)return;
        let list=this.pool.get(buffer.byteLength);
        if(!list){if(this.pool.size>=256){const key=this.pool.keys().next().value;for(const old of this.pool.get(key))this.bytes-=old.byteLength;this.pool.delete(key);}list=[];this.pool.set(buffer.byteLength,list);}
        list.push(buffer);this.bytes+=buffer.byteLength;
    }
}
export function packedFrameReader(packed,ranges){
    return index=>{if(!Number.isSafeInteger(index)||index<0||index*2+1>=ranges.length)throw Error('Invalid packed frame index');const start=ranges[index*2],length=ranges[index*2+1];if(start%4||start+length>packed.byteLength)throw Error('Invalid packed frame range');return new Uint8Array(packed,start,length);};
}
