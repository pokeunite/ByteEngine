import json,struct,io,zlib,sys
from pathlib import Path
site=Path(sys.argv[1]);manifest=json.loads((site/'web-game.json').read_text())
stream=io.BytesIO(b''.join((site/f['path']).read_bytes() for f in manifest['files']))
assert stream.read(8)==b'BYTEPAK2'
count=struct.unpack('<i',stream.read(4))[0]
def string():
    n=0;shift=0
    while True:
        b=stream.read(1)[0];n|=(b&127)<<shift
        if b<128:break
        shift+=7
    return stream.read(n).decode()
for _ in range(count):
    name=string();size=struct.unpack('<q',stream.read(8))[0];stream.read(32);packed=struct.unpack('<q',stream.read(8))[0]
    if name.endswith('.bytemodel'):
        data=zlib.decompress(stream.read(packed));length=struct.unpack_from('<i',data,8)[0]
        model=json.loads(data[12:12+length]);print(model['name'],name,'metadataMB',round(length/1048576,2),'totalMB',round(size/1048576,2),'bones',len((model.get('skeleton')or{}).get('bones',[])),'clips',len(model.get('animations',[])))
    else:stream.seek(packed,1)
