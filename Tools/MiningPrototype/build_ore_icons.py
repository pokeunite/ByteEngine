from pathlib import Path
from PIL import Image,ImageDraw
out=Path('Plugins/KarlsonDemo/Samples/Hud/Art');out.mkdir(parents=True,exist_ok=True)
white=(246,248,250,255);light=(218,225,231,255);mid=(165,180,194,255)
# Original geometric UI symbols. Faces are supersampled for clean small-size edges.
raw=[([(10,8),(16,2),(24,5),(26,16),(18,23),(10,18)],mid), ([(16,2),(24,5),(19,12),(10,8)],white), ([(10,8),(19,12),(18,23),(10,18)],light), ([(1,17),(7,11),(12,16),(12,25),(5,29),(1,24)],mid), ([(1,17),(7,11),(8,20),(5,29),(1,24)],white), ([(7,11),(12,16),(8,20)],light), ([(20,20),(27,15),(31,20),(30,28),(23,31),(18,27)],mid), ([(20,20),(27,15),(27,24),(23,31),(18,27)],white), ([(27,15),(31,20),(30,28),(27,24)],light)]
def bar(x,y):
 return [([(x,y+6),(x+3,y),(x+12,y),(x+16,y+6),(x+12,y+10),(x,y+10)],mid), ([(x+3,y),(x+12,y),(x+16,y+6),(x+4,y+6),(x,y+6)],white), ([(x,y+6),(x+4,y+6),(x+16,y+6),(x+12,y+10),(x,y+10)],light)]
refined=bar(11,3)+bar(1,16)+bar(16,20)
for name,faces in [('ore-raw',raw),('ore-refined',refined)]:
 im=Image.new('RGBA',(512,512));d=ImageDraw.Draw(im)
 for points,color in faces:d.polygon([(x*16,y*16) for x,y in points],fill=color)
 im.resize((128,128),Image.Resampling.LANCZOS).save(out/(name+'.png'))
 polys=''.join('<polygon points="'+' '.join(f'{x},{y}' for x,y in pts)+'" fill="#'+''.join(f'{v:02x}' for v in color[:3])+'"/>' for pts,color in faces)
 (out/(name+'.svg')).write_text('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32">'+polys+'</svg>')
