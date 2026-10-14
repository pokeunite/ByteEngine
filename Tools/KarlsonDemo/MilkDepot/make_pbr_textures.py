"""Generate portable PBR surface maps, not Blender-only procedural shaders."""
from pathlib import Path
import numpy as np
from PIL import Image, ImageFilter, ImageDraw
OUT=Path('C:/Users/codex/Downloads/byte engine files/Milk Depot Environment/Textures/v2')
OUT.mkdir(parents=True,exist_ok=True)
N=1024
rng=np.random.default_rng(51013)
def noise(scale):
    a=Image.fromarray(rng.integers(0,256,(scale,scale),dtype=np.uint8)).resize((N,N),Image.Resampling.BICUBIC)
    return np.asarray(a,dtype=float)/255-.5
for name,color,rough,metal in [('Concrete',(182,186,181),.75,0),('Floor',(87,98,106),.58,.28),
                               ('Orange',(232,83,28),.40,.20),('Steel',(104,121,130),.32,.85),('Dark',(46,55,60),.62,.18)]:
    coarse=noise(16);grain=noise(128);fine=noise(1024)
    h=coarse*.13+grain*.045+fine*.014
    variation=coarse*.07+grain*.016+fine*.010
    albedo=np.clip(np.array(color)[None,None,:]*(1+variation[:,:,None]),0,255)
    wear=Image.new('L',(N,N),0);draw=ImageDraw.Draw(wear)
    # Controlled edge wear on UV borders; sparse scratches across the face.
    for i in range(320):
        side=int(rng.integers(0,4));t=int(rng.integers(0,N));length=int(rng.integers(2,28));inset=int(rng.integers(1,9))
        p=[(inset,t,inset+2,t+length),(N-inset,t,N-inset-2,t+length),(t,inset,t+length,inset+2),(t,N-inset,t+length,N-inset-2)][side]
        draw.line(p,fill=int(rng.integers(50,190)),width=int(rng.integers(1,3)))
    for i in range(80 if name!='Concrete' else 28):
        x,y=rng.integers(30,N-30,2);draw.line((int(x),int(y),int(x+rng.integers(4,30)),int(y+rng.integers(-2,3))),fill=60,width=1)
    edge=np.asarray(wear,dtype=float)/255
    if name=='Concrete':albedo-=edge[:,:,None]*20
    else:albedo=albedo*(1-edge[:,:,None]*.25)+np.array([140,150,151])[None,None,:]*edge[:,:,None]*.25
    roughness=np.clip(rough+coarse*.13+grain*.06+edge*.12,.12,.95)
    dx=np.roll(h,-1,axis=1)-np.roll(h,1,axis=1)
    dy=np.roll(h,-1,axis=0)-np.roll(h,1,axis=0)
    strength=5 if name=='Concrete' else 2.5
    normals=np.stack((-dx*strength,dy*strength,np.ones_like(h)),axis=2)
    normals/=np.linalg.norm(normals,axis=2)[:,:,None]
    Image.fromarray(albedo.clip(0,255).astype('uint8')).save(OUT/f'{name}_BaseColor.png')
    Image.fromarray((roughness*255).astype('uint8')).save(OUT/f'{name}_Roughness.png')
    Image.fromarray(((normals*.5+.5)*255).astype('uint8')).save(OUT/f'{name}_Normal.png')
    print(name,'1024px PBR maps')
