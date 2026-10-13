"""Original weathered PBR atlas for Blender-authored race props."""
from pathlib import Path
import json,uuid
import numpy as np
from PIL import Image,ImageDraw
ROOT=Path('C:/Users/codex/Documents/DuneCompany');OUT=ROOT/'Assets/RaceKit';OUT.mkdir(parents=True,exist_ok=True)
COLORS=[(76,88,59),(44,48,43),(177,162,123),(204,91,27),(26,27,23),(139,133,112),(221,214,188),(115,58,25)]
rng=np.random.default_rng(772);w,h=4096,1024;albedo=np.zeros((h,w,3),dtype=np.uint8);pbr=np.zeros((h,w,3),dtype=np.uint8);height=np.zeros((h,w),dtype=np.float32)
for i,color in enumerate(COLORS):
    noise=rng.normal(0,3,(h,512));large=np.array(Image.fromarray(rng.integers(0,255,(64,32),dtype=np.uint8)).resize((512,h),Image.Resampling.BICUBIC))/255
    wear=large>.77;rough=(.55+.28*large) if i!=4 else (.86+.08*large);tile=np.clip(np.array(color)[None,None,:]*(.91+.12*large[:,:,None])+noise[:,:,None],0,255)
    if i in [0,1,2,3]:tile[wear]=tile[wear]*.55+np.array([99,60,29])*.45
    albedo[:,i*512:(i+1)*512]=tile.astype(np.uint8);pbr[:,i*512:(i+1)*512,0]=255;pbr[:,i*512:(i+1)*512,1]=(rough*255).astype(np.uint8);pbr[:,i*512:(i+1)*512,2]=150 if i in [0,1,2,3,7] else 0;height[:,i*512:(i+1)*512]=noise*.0007+large*.015
image=Image.fromarray(albedo);d=ImageDraw.Draw(image)
for i in [0,1,2,3,7]:
    for j in range(350):
        x=i*512+int(rng.integers(8,503));y=int(rng.integers(4,1019));length=int(rng.integers(3,45));c=tuple(int(v) for v in np.clip(np.array(COLORS[i])*.63+np.array([19,11,6]),0,255));d.line((x,y,min((i+1)*512-4,x+length),y+int(rng.integers(-3,4))),fill=c,width=1)
image.save(OUT/'race-kit-albedo.png');Image.fromarray(pbr).save(OUT/'race-kit-pbr.png');gy,gx=np.gradient(height);n=np.stack((-gx*13,-gy*13,np.ones_like(gx)),2);n/=np.linalg.norm(n,axis=2,keepdims=True);Image.fromarray(((n*.5+.5)*255).astype(np.uint8)).save(OUT/'race-kit-normal.png')
for file in OUT.glob('race-kit-*.png'):Path(str(file)+'.meta').write_text(json.dumps({'guid':str(uuid.uuid5(uuid.NAMESPACE_URL,'dune-racekit/'+file.name)),'type':'Texture2D'}))
(OUT/'LICENSE.txt').write_text('Original procedural textures and Blender race-kit geometry authored for Dune Company. CC0 1.0. Existing fonts and rock scans retain their own licenses.\n')
print('Authored shared weathered albedo, packed roughness/metallic and normal atlas')
