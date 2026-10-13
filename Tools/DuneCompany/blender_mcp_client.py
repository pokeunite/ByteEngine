"""Client for the user's installed Blender MCP add-on's local control bridge."""
import argparse,json,socket
from pathlib import Path

def call(method,params=None,port=19876):
    with socket.create_connection(('127.0.0.1',port),timeout=10) as connection:
        connection.settimeout(35)
        connection.sendall((json.dumps({'method':method,'params':params or {}})+'\n').encode())
        response=json.loads(connection.makefile('r',encoding='utf-8').readline())
    if not response.get('ok'):raise RuntimeError(response.get('error',response))
    return response['result']

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--port',type=int,default=19876);parser.add_argument('--script');parser.add_argument('--method',default='get_scene_info');args=parser.parse_args()
    result=call('execute_python',{'code':Path(args.script).read_text(encoding='utf-8')} if args.script else {},args.port) if args.script else call(args.method,port=args.port)
    print(json.dumps(result,indent=2))
