import re,json,csv,sys
from pathlib import Path
src=Path(sys.argv[1]);out=Path('artifacts/gdevelop-audit');out.mkdir(parents=True,exist_ok=True)
def args_at(s,pos):
 depth=1;quote=None;esc=False;parts=[];start=pos;i=pos
 while i<len(s):
  c=s[i]
  if quote:
   if esc:esc=False
   elif c=='\\':esc=True
   elif c==quote:quote=None
  elif c in '"\'':quote=c
  elif c=='(':depth+=1
  elif c==')':
   depth-=1
   if depth==0:parts.append(s[start:i]);return parts,i
  elif c==',' and depth==1:parts.append(s[start:i]);start=i+1
  i+=1
 return [],i
def text(arg):
 bits=re.findall(r'"((?:\\.|[^"\\])*)"|\'((?:\\.|[^\'\\])*)\'',arg)
 return ''.join((a or b).replace('\\"','"').replace("\\'", "'").replace('\\n',' ').replace('\\\\','\\') for a,b in bits)
rows=[]
for base in [src/'Core/GDCore/Extensions/Builtin',src/'Extensions']:
 for p in sorted(list(base.rglob('*.cpp'))+list(base.rglob('JsExtension.js'))):
  s=p.read_text(encoding='utf-8-sig',errors='replace')
  for m in re.finditer(r'\b(?:Add|add)(Action|Condition)\s*\(',s):
   args,end=args_at(s,m.end())
   if len(args)<2:continue
   key=text(args[0]);label=text(args[1])
   if not key or not label:continue
   line=s.count('\n',0,m.start())+1
   rows.append(dict(kind=m[1].lower(),id=key,label=label,description=text(args[2]) if len(args)>2 else '',category=text(args[4]) if len(args)>4 else '',file=p.relative_to(src).as_posix(),line=line))
(out/'gdevelop-explicit-events.json').write_text(json.dumps(rows,indent=2,ensure_ascii=False),encoding='utf-8')
with (out/'GDevelop explicit conditions and actions.csv').open('w',encoding='utf-8-sig',newline='') as f:
 w=csv.DictWriter(f,fieldnames=['kind','id','label','description','category','file','line']);w.writeheader();w.writerows(rows)
print('EXPLICIT DECLARATIONS',len(rows),'actions',sum(r['kind']=='action' for r in rows),'conditions',sum(r['kind']=='condition' for r in rows))
