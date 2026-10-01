import sys,subprocess,re
from pathlib import Path
sys.path.insert(0,str(Path('Tools/Inspection/python').resolve()))
import imageio_ffmpeg
exe=imageio_ffmpeg.get_ffmpeg_exe();video=r'C:\Users\codex\Videos\2026-10-01 15-13-34.mp4';out=Path('output/goblin-scraper/besiege-video');out.mkdir(exist_ok=True)
p=subprocess.run([exe,'-i',video],capture_output=True,text=True);(out/'metadata.txt').write_text(p.stderr)
print(p.stderr[-1800:])
subprocess.run([exe,'-y','-i',video,'-vf','fps=1/6,scale=960:-1',str(out/'frame-%03d.jpg')],capture_output=True,check=True)
from PIL import Image,ImageDraw
files=list(out.glob('frame-*.jpg'));thumbs=[]
for i,f in enumerate(files):
 im=Image.open(f).convert('RGB');im.thumbnail((480,270));thumb=Image.new('RGB',(480,294),(25,25,25));thumb.paste(im,(0,24));ImageDraw.Draw(thumb).text((10,6),f'{i*6}s',fill='white');thumbs.append(thumb)
for k in range(0,len(thumbs),12):
 subset=thumbs[k:k+12];canvas=Image.new('RGB',(1440,294*((len(subset)+2)//3)))
 for i,im in enumerate(subset):canvas.paste(im,((i%3)*480,(i//3)*294))
 canvas.save(out/f'contact-{k//12}.jpg')
print('FRAMES',len(files))
