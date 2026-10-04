from pathlib import Path
from PIL import Image
from io import BytesIO
import struct,json
for p in Path('Designs/BattlePrototype/assets').glob('*.glb'):
 b=p.read_bytes();n=struct.unpack_from('<I',b,12)[0];j=json.loads(b[20:20+n]);blob=bytearray(b[28+n:])
 for image in j.get('images',[]):
  if image.get('mimeType')!='image/webp':continue
  view=j['bufferViews'][image['bufferView']];raw=blob[view.get('byteOffset',0):view.get('byteOffset',0)+view['byteLength']];im=Image.open(BytesIO(raw));out=BytesIO();im.save(out,format='PNG');data=out.getvalue()
  while len(blob)%4:blob.append(0)
  offset=len(blob);blob.extend(data);image['bufferView']=len(j['bufferViews']);j['bufferViews'].append({'buffer':0,'byteOffset':offset,'byteLength':len(data)});image['mimeType']='image/png'
 for texture in j.get('textures',[]):
  if 'EXT_texture_webp' in texture.get('extensions',{}):texture['source']=texture['extensions'].pop('EXT_texture_webp')['source']
 for field in ['extensionsUsed','extensionsRequired']:
  if field in j:j[field]=[x for x in j[field] if x!='EXT_texture_webp']
 j['buffers'][0]['byteLength']=len(blob)
 while len(blob)%4:blob.append(0)
 doc=json.dumps(j,separators=(',',':')).encode();doc+=b' '*((-len(doc))%4)
 p.write_bytes(struct.pack('<III',0x46546c67,2,28+len(doc)+len(blob))+struct.pack('<II',len(doc),0x4e4f534a)+doc+struct.pack('<II',len(blob),0x004e4942)+blob)
print('Converted all embedded textures to engine-compatible PNG.')
