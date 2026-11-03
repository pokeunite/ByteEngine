// The managed pool pins storage until the next Frame. Views are consumed during
// synchronous rendering only; no heap view is retained across an await or Frame.
// localHeapViewU8 is the public .NET memory API, not a runtime-internal pointer hook.
export function createFrameReader(runtime,game){
    let table=null;
    return {
        enabled:true,
        beginFrame(frame){table=frame.bufferViews??null;},
        read(index){
            if(!this.enabled||!table||typeof runtime.localHeapViewU8!=='function')return game.ReadBuffer(index);
            if(!Number.isSafeInteger(index)||index<0||index*2+1>=table.length)throw new Error('Invalid frame buffer index');
            const start=table[index*2],length=table[index*2+1],heap=runtime.localHeapViewU8();
            if(!Number.isSafeInteger(start)||!Number.isSafeInteger(length)||start<0||length<0||start+length>heap.byteLength)throw new Error('Invalid frame memory range');
            return heap.subarray(start,start+length);
        }
    };
}
