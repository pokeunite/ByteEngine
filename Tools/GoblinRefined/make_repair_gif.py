from PIL import Image,ImageDraw,ImageFont
from pathlib import Path
import sys
out=Path(sys.argv[1]);names=['scrap_auger_drill','scrap_battering_fist','scrap_cab_shell','scrap_catapult_basket','scrap_engine_block'];labels=['Continuous drill','Shorter fist stroke','Mounted cab lever','Connected catapult','Engine drive shafts'];frames=[];font=ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf',20)
for i in range(12):
 canvas=Image.new('RGB',(1800,400),(35,39,32));d=ImageDraw.Draw(canvas)
 for j,(name,label) in enumerate(zip(names,labels)):
  im=Image.open(out/'previews'/'repair_motion'/f'{name}_{i:02d}.png').convert('RGB');canvas.paste(im,(j*360,40));d.text((j*360+10,9),label,font=font,fill=(235,235,205))
 frames.append(canvas)
frames[0].save(out/'previews'/'mechanical-fixes.gif',save_all=True,append_images=frames[1:],duration=160,loop=0)
frames[4].save(out/'previews'/'mechanical-fixes.png')
# Refresh drill frames used by the original four-part motion review.
for i in range(8):
 Image.open(out/'previews'/'repair_motion'/f'scrap_auger_drill_{round(i*12/8):02d}.png').resize((256,256)).save(out/'previews'/'motion_frames'/f'scrap_auger_drill_{i:02d}.png')
print('MECHANICAL_REVIEW_SAVED')
