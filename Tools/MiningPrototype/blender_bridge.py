"""Client for the user's enabled local Blender MCP newline-JSON bridge."""
import socket,json,sys
from pathlib import Path
code=Path(sys.argv[1]).read_text(encoding='utf8') if len(sys.argv)>1 else 'result = bpy.app.version_string'
with socket.create_connection(('127.0.0.1',9876),timeout=120) as s:
    s.sendall((json.dumps({'method':'execute_python','params':{'code':code}})+'\n').encode())
    print(s.makefile('r',encoding='utf8').readline())
