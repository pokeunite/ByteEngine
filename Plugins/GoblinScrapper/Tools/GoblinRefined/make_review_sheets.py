"""Make catalog and motion review sheets from actual Blender renders."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import json,sys,shutil
out=Path(sys.argv[1]); catalog=json.loads((out/'parts-catalog.json').read_text())
fontfile=Path('C:/Windows/Fonts/segoeui.ttf')
fontbold=Path('C:/Windows/Fonts/segoeuib.ttf')
def font(size,bold=False):
    return ImageFont.truetype(str(fontbold if bold else fontfile),size)
def sheet(group,cols,name):
    parts=[p for p in catalog['parts'] if p['group']==group]
    cell=420; header=120; footer=48; rows=(len(parts)+cols-1)//cols
    image=Image.new('RGB',(cols*cell,header+rows*cell+footer),(42,44,38)); d=ImageDraw.Draw(image)
    d.text((28,20),'GOBLIN SCRAPER / REFINED '+('CORE KIT' if group=='Core' else 'WAR MACHINE KIT'),font=font(35,True),fill=(219,229,180))
    d.text((28,72),'Actual exported Blender models | metres | compatible four-bolt mounts | green and red factions',font=font(19),fill=(190,193,174))
    for i,p in enumerate(parts):
        x=(i%cols)*cell; y=header+(i//cols)*cell
        thumb=Image.open(out/'previews'/Path(p['file']).with_suffix('.png').name).convert('RGB').resize((cell-12,cell-50),Image.Resampling.LANCZOS)
        image.paste(thumb,(x+6,y+4))
        d.text((x+12,y+cell-43),p['label'],font=font(20,True),fill=(230,230,210))
        clips=[a['name'].split('__')[-1] for a in p['animations']]
        note=' / '.join(clips) if clips else 'Static structural part'
        d.text((x+12,y+cell-19),note,font=font(14),fill=(172,193,139))
    d.text((28,image.height-34),'Reference interpretation: visible silhouettes and materials reconstructed; hidden geometry designed for modular assembly.',font=font(16),fill=(181,185,167))
    path=out/'previews'/name; image.save(path); print(path)
sheet('Core',5,'core-kit-contact-sheet.png')
sheet('Expansion',4,'war-machine-contact-sheet.png')
parts=['scrap_crushing_drum','scrap_auger_drill','scrap_grabber_jaws','scrap_track_pod']
labels=['Crushing drum','Auger drill','Grabber jaws','Circulating track links']
frames=[]
for i in range(8):
    frame=Image.new('RGB',(768,860),(42,44,38)); d=ImageDraw.Draw(frame)
    d.text((20,13),'GOBLIN SCRAPER / MECHANICAL MOTION',font=font(25,True),fill=(219,229,180))
    for j,(p,label) in enumerate(zip(parts,labels)):
        x=(j%2)*384; y=60+(j//2)*400
        img=Image.open(out/'previews'/'motion_frames'/f'{p}_{i:02d}.png').convert('RGB').resize((376,370),Image.Resampling.LANCZOS)
        frame.paste(img,(x+4,y)); d.text((x+15,y+371),label,font=font(20),fill=(230,230,210))
    frames.append(frame)
frames[0].save(out/'previews'/'mechanical-motion.gif',save_all=True,append_images=frames[1:],duration=180,loop=0)
print(out/'previews'/'mechanical-motion.gif')
