from PIL import Image,ImageDraw
from pathlib import Path
folder=Path(r'C:/Users/codex/Downloads/byte engine files/Karlson V2/Previews')
for name in ['Pistol','Shotgun','Rifle']:
 labels=['side','top','front','Reload35','Reload70','Reload85','Reload96'];sheet=Image.new('RGB',(1152,825),'#f1f4f7');draw=ImageDraw.Draw(sheet)
 for i,label in enumerate(labels):
  im=Image.open(folder/(name+'_'+label+'.png')).convert('RGB');im.thumbnail((384,248));x=(i%3)*384;y=(i//3)*275;sheet.paste(im,(x,y+20));draw.text((x+8,y+3),name+' '+label,fill='#15283a')
 sheet.save(folder/(name+'_review_sheet.png'))
