import socket,json,sys
from pathlib import Path
request={'method':'execute_python','params':{'code':Path(sys.argv[1]).read_text()}}
with socket.create_connection(('127.0.0.1',9876),timeout=35) as s:
 s.sendall((json.dumps(request)+'\n').encode());data=b''
 while not data.endswith(b'\n'):
  chunk=s.recv(65536)
  if not chunk:break
  data+=chunk
 response=json.loads(data)
 if response.get('ok'):
  result=response['result'];print(json.dumps(result.get('result')));print(result.get('output','')[-1500:])
 else:print(json.dumps(response))
