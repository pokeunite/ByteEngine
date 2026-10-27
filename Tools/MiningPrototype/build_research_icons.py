from PIL import Image,ImageDraw
from pathlib import Path
import math
out=Path('Plugins/KarlsonDemo/Samples/Hud/Art');out.mkdir(parents=True,exist_ok=True)
for name in ['ammo','crate','market','health','shield','drill','turret','mortar','mine','company','speed','gear']:
 im=Image.new('RGBA',(256,256));d=ImageDraw.Draw(im);w='white'
 def line(p,n=12):d.line(p,fill=w,width=n,joint='curve')
 if name=='ammo':
  for x,h in [(55,70),(112,45),(169,65)]:d.polygon([(x,h+25),(x+17,h),(x+34,h+25),(x+34,205),(x,205)],fill=w);d.line([(x,177),(x+34,177)],fill=(0,0,0,0),width=7)
 elif name=='crate':
  d.polygon([(35,82),(128,35),(221,82),(128,128)],fill=w);d.polygon([(35,99),(119,143),(119,226),(35,181)],fill=w);d.polygon([(137,143),(221,99),(221,181),(137,226)],fill=w)
 elif name=='market':
  for x,y in [(45,150),(100,100),(155,45)]:d.rectangle((x,y,x+35,212),fill=w)
 elif name=='health':d.rectangle((104,38,151,218),fill=w);d.rectangle((38,104,218,151),fill=w)
 elif name=='shield':line([(128,32),(211,65),(199,143),(174,185),(128,222),(82,185),(57,143),(45,65),(128,32)]);line([(128,62),(180,82),(170,143),(128,187),(86,143),(76,82),(128,62)],8)
 elif name=='drill':
  d.polygon([(53,58),(159,30),(205,64),(100,96)],fill=w);d.polygon([(67,86),(105,105),(105,165),(67,146)],fill=w);d.rectangle((114,102,174,139),fill=w)
  for y,half in [(145,30),(165,23),(185,15)]:d.polygon([(144-half,y),(144+half,y),(144+half-8,y+14),(144-half+8,y+14)],fill=w)
  d.polygon([(137,201),(151,201),(144,222)],fill=w)
 elif name=='turret':
  d.rounded_rectangle((58,67,167,112),8,fill=w);d.rectangle((159,75,226,91),fill=w);d.rectangle((105,118,132,168),fill=w);line([(119,153),(68,218)],15);line([(119,153),(170,218)],15);d.ellipse((74,77,91,94),fill=(0,0,0,0))
 elif name=='mortar':
  line([(94,178),(173,57)],32);d.ellipse((154,40,192,66),outline=w,width=8);line([(115,149),(174,212)],12);d.rounded_rectangle((43,208,202,225),6,fill=w)
 elif name=='mine':
  d.ellipse((44,108,212,207),fill=w);d.rectangle((80,85,175,152),fill=w);d.rectangle((117,49,139,94),fill=w);line([(61,49),(77,74)],8);line([(194,49),(178,74)],8)
 elif name=='company':
  line([(40,221),(40,99),(95,99),(95,38),(164,38),(164,99),(215,99),(215,221)],14)
  for x in [62,111,157,190]:d.rectangle((x,126,x+15,146),fill=w);d.rectangle((x,166,x+15,186),fill=w)
  line([(32,221),(225,221)],12)
 elif name=='speed':
  d.ellipse((143,30,183,70),fill=w);line([(149,84),(116,130),(157,159),(137,216)],19);line([(120,130),(84,180),(37,180)],18);line([(143,92),(186,118),(220,89)],14);line([(108,88),(78,114)],14)
 elif name=='gear':
  d.ellipse((62,62,194,194),fill=w)
  for i in range(8):
   a=i*math.pi/4;cx=128+73*math.cos(a);cy=128+73*math.sin(a);d.ellipse((cx-19,cy-19,cx+19,cy+19),fill=w)
  d.ellipse((100,100,156,156),fill=(0,0,0,0))
 im.resize((128,128),Image.Resampling.LANCZOS).save(out/('research-'+name+'.png'))
