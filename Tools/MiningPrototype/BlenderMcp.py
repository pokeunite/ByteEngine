"""Client for the user's existing local Blender MCP add-on (newline JSON protocol)."""
import argparse,json,socket
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('method',choices=['get_scene_info','list_objects','execute_python','save_blend']);p.add_argument('--code');p.add_argument('--file');p.add_argument('--save');args=p.parse_args()
params={}
if args.code:params['code']=args.code
if args.file:params['code']=Path(args.file).read_text(encoding='utf-8')
if args.save:params['filepath']=str(Path(args.save).resolve())
with socket.create_connection(('127.0.0.1',9876),5) as sock:
 sock.settimeout(40);sock.sendall((json.dumps({'method':args.method,'params':params})+'\n').encode());response=json.loads(sock.makefile('r',encoding='utf-8').readline())
print(json.dumps(response,ensure_ascii=True))
if not response.get('ok'):raise SystemExit(1)
