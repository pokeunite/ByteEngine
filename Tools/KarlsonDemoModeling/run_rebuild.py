import socket,json
from pathlib import Path
code=Path(r'C:/Users/codex/ByteEngine/Tools/KarlsonDemoModeling/rebuild_v2.py').read_text()
with socket.create_connection(('127.0.0.1',9876),timeout=35) as s:
 s.sendall((json.dumps({'method':'execute_python','params':{'code':code}})+'\n').encode());data=b''
 while not data.endswith(b'\n'):
  chunk=s.recv(65536)
  if not chunk:break
  data+=chunk
 response=json.loads(data);print(json.dumps(response)[:5000])
