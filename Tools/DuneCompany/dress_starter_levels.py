"""Apply selected CC0 Goegap sky and desert dressing; retain playable terrain."""
import json,math,shutil,datetime,uuid,importlib.util
from pathlib import Path
ROOT=Path('C:/Users/codex/Documents/DuneCompany')
ASSETS=Path('.artifacts/desert-dressing-assets').resolve()

def apply(root):
 spec=importlib.util.spec_from_file_location('levels',Path(__file__).with_name('author_starter_levels.py'));m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
 folder=root/'Assets/ContractLevels';backup=root/'.byteengine/backups'/('desert-dressing-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'));backup.mkdir(parents=True)
 for name in ['goegap_2k.hdr','rock_boulder_dry_diff_1k.jpg']:shutil.copy2(ASSETS/name,folder/name)
 # Attach a material to the existing original rock meshes; retain GUIDs.
 (folder/'sandstone.mtl').write_text('newmtl DesertRock\nKd 0.72 0.52 0.33\nNs 5\nmap_Kd rock_boulder_dry_diff_1k.jpg\n')
 for variant in range(3):
  path=folder/f'sandstone-{variant}.obj';shutil.copy2(path,backup/path.name)
  lines=path.read_text().splitlines();vertices=[l for l in lines if l.startswith('v ')];output=['mtllib sandstone.mtl','usemtl DesertRock','o sandstone']+vertices
  # Project each triangle along its dominant face normal to avoid seam stretching.
  coords=[tuple(map(float,v.split()[1:])) for v in vertices];faces=[];uv=[]
  for line in lines:
   if not line.startswith('f '):continue
   ids=[int(n.split('/')[0]) for n in line.split()[1:]];a,b,c=[coords[i-1] for i in ids]
   ab=[b[i]-a[i] for i in range(3)];ac=[c[i]-a[i] for i in range(3)]
   normal=[ab[1]*ac[2]-ab[2]*ac[1],ab[2]*ac[0]-ab[0]*ac[2],ab[0]*ac[1]-ab[1]*ac[0]]
   dominant=max(range(3),key=lambda i:abs(normal[i]));axes=[i for i in range(3) if i!=dominant]
   refs=[]
   for idx in ids:
    v=coords[idx-1];uv.append(f'vt {v[axes[0]]*1.5:.5f} {v[axes[1]]*1.5:.5f}');refs.append(f'{idx}/{len(uv)}')
   faces.append('f '+' '.join(refs))
  output+=uv+faces
  path.write_text('\n'.join(output))
 (folder/'POLYHAVEN-LICENSE.md').write_text('Goegap HDRI and Rock Boulder Dry texture: Poly Haven, CC0 1.0.\nhttps://polyhaven.com/license\nhttps://polyhaven.com/a/goegap\nhttps://polyhaven.com/a/rock_boulder_dry\nHDRI by Greg Zaal; rock texture by Dimitrios Savva and Rico Cilliers.\n')
 for name,winch in [('Workshop',False),('WinchRescue',True)]:
  path=root/'Scenes'/f'{name}.bytescene';shutil.copy2(path,backup/path.name);scene=json.loads(path.read_text(encoding='utf-8-sig'))
  scene['gameObjects']=[o for o in scene['gameObjects'] if not o['name'].startswith('Desert dressing / ')]
  for obj in scene['gameObjects']:
   for c in obj.get('components',[]):
    p=c.get('properties',{})
    if c['type']=='SkyEnvironment':
     p.update(environmentMapPath='Assets/ContractLevels/goegap_2k.hdr',environmentMapGuid=str(uuid.UUID(int=0)),skyMode=1,environmentRotationDegrees=0,skyIntensity=.8,environmentIntensity=.65,exposure=.9,ambientIntensity=.38,fogEnabled=True,fogDensity=.001,fogMaxOpacity=.3,fogColor=[.69,.72,.72])
     p.get('graphicsLook',{}).update(warmth=.025,saturation=.96,contrast=1.04)
    if c['type']=='DirectionalLight' and obj['name'].startswith('Sun'):
     p.update(color=[1,.96,.87],intensity=1.4,shadowStrength=.7)
     # Noon sun; soft blue fill remains for readable tyres.
     yaw=math.radians(51.064453125);pitch=math.radians(-46.494140625)
     obj['transform']['localRotation']=dict(x=math.sin(pitch/2)*math.cos(yaw/2),y=math.sin(yaw/2)*math.cos(pitch/2),z=-math.sin(yaw/2)*math.sin(pitch/2),w=math.cos(yaw/2)*math.cos(pitch/2))
    if c['type']=='DirectionalLight' and 'Soft sky fill' in obj['name']:p['intensity']=.55
    if c['type']=='MeshRenderer' and p.get('modelPath','').startswith('Assets/ContractLevels/sandstone-'):
     guid=p['modelGuid'].replace('-','');p['materialKey']=f'{guid}:material:DesertRock';p['baseColor']=[1,1,1,1]
  # Grouped formations rather than evenly spaced standalone rocks.
  def rock(label,x,y,z,sx,sy,sz,a,collide=False):
   obj=m.object_('sandstone '+label,x,y,z,sx,sy,sz,[1,1,1,1],a,collide);obj['name']='Desert dressing / '+label
   p=obj['components'][0]['properties'];p['materialKey']=p['modelGuid'].replace('-','')+':material:DesertRock';p['baseColor']=[1,1,1,1];return obj
  for i in range(14):
   a=i*math.tau/14;r=108+8*math.sin(i*2.1);x,z=math.cos(a)*r,math.sin(a)*r
   scene['gameObjects'].append(rock(f'distant ridge {i}',x,5,z,48+i%3*6,18+i%4*4,45,a))
  points=m.WINCH if winch else m.TOW
  for i,(x,z,y) in enumerate(points[1:]):
   side=-1 if i%2 else 1;xx=x+side*21;zz=z+3
   for j in range(3):
    px,pz=xx+side*j*3,zz+j*2;hh=m.height(px,pz,winch)
    scene['gameObjects'].append(rock(f'outcrop {i}-{j}',px,hh+2,pz,7+j,4+j*.8,5+j,i*.8+j*.2,True))
   for j in range(3):
    px,pz=x+side*(8+j*.7),z-3+j*2;hh=m.height(px,pz,winch)
    scene['gameObjects'].append(rock(f'broken stone {i}-{j}',px,hh+.16,pz,.8+j*.2,.35,.65,i+j))
  path.write_text(json.dumps(scene,indent=2),encoding='utf-8')
 print('Goegap + textured outcrops installed; backups:',backup)
if __name__=='__main__':apply(ROOT)
