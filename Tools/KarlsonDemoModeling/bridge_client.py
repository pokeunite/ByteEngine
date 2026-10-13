import socket,json,sys
with socket.create_connection(('127.0.0.1',9876),timeout=35) as s:
 s.sendall((json.dumps({'method':sys.argv[1], 'params':json.loads(sys.argv[2]) if len(sys.argv)>2 else {}})+'\n').encode())
 data=b''
 while not data.endswith(b'\n'):
  chunk=s.recv(65536)
  if not chunk:break
  data+=chunk
 print(data.decode())
