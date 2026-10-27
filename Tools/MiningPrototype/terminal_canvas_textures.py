"""Small original tileable canvas textures, embedded into the Blender/GLB asset."""
from pathlib import Path
import math,random
from PIL import Image
out=Path(__file__).resolve().parents[2]/'artifacts/reference-terminal';out.mkdir(parents=True,exist_ok=True)
for name,base in [('canvas',(72,74,52)),('lining',(87,85,60))]:
 rng=random.Random(318);im=Image.new('RGB',(512,512));pixels=[]
 for y in range(512):
  for x in range(512):
   cloud=2.8*math.sin(x*math.tau/128)*math.sin(y*math.tau/256)+1.5*math.cos((x+y)*math.tau/64)
   weave=(1.8 if x%4<2 else -1.8)+(1.5 if y%4<2 else -1.5)+rng.uniform(-1.6,1.6)
   pixels.append(tuple(max(0,min(255,round(c+cloud+weave)))for c in base))
 im.putdata(pixels);im.save(out/(name+'.png'))
