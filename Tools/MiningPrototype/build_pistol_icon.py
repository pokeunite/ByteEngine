from PIL import Image,ImageDraw
from pathlib import Path
im=Image.new('RGBA',(512,256));d=ImageDraw.Draw(im);white=(255,255,255,255);clear=(0,0,0,0)
d.polygon([(69,61),(83,42),(452,42),(471,55),(471,103),(102,103),(87,94),(65,94)],fill=white)
d.polygon([(84,96),(369,96),(353,117),(263,117),(255,149),(239,172),(176,172),(154,222),(82,222),(106,143),(101,124),(70,111)],fill=white)
d.polygon([(178,121),(237,121),(239,137),(228,157),(182,157),(172,149)],fill=clear)
d.line([(201,120),(195,139),(203,148)],fill=white,width=8)
d.polygon([(76,57),(58,48),(52,65),(69,78)],fill=white)
d.rectangle((423,30,440,43),fill=white);d.rectangle((92,32,110,43),fill=white)
for x in range(105,147,12):d.line([(x,56),(x-7,84)],fill=clear,width=5)
d.line([(170,56),(438,56)],fill=clear,width=4)
d.rectangle((454,63,471,83),fill=clear)
im=im.resize((256,128),Image.Resampling.LANCZOS)
out=Path('Plugins/KarlsonDemo/Samples/Hud/Art/company-pistol.png');out.parent.mkdir(parents=True,exist_ok=True);im.save(out)
