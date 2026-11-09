"""Install the selected public ParticleFX sheet and editable, bounded VFX layers."""
import pathlib,json,shutil,uuid,datetime
root=pathlib.Path('C:/Users/codex/Documents/palclone')
assets=root/'Assets/VFX/Mining';source=pathlib.Path('artifacts/particlefx-download/cartoon-explosion-sheet.png')
backup=root/'SourceArt/ParticleFX Cartoon Explosion'/datetime.datetime.now().strftime('%Y%m%d-%H%M%S');backup.mkdir(parents=True)
texture=assets/'ParticleFX Cartoon Explosion.png';meta=texture.with_suffix('.png.meta')
guid=json.loads(meta.read_text())['guid'] if meta.exists() else str(uuid.uuid4())
shutil.copy2(source,texture);meta.write_text(json.dumps({'guid':guid,'type':'Texture2D','importer':{'filter':'Linear'}},indent=2))
def v(x,y,z):return {'X':x,'Y':y,'Z':z}
def color(r,g,b,a):return {'X':r,'Y':g,'Z':b,'W':a}
def layer(name,**kwargs):
 d=dict(Name=name,Enabled=True,Shape='Point',RenderMode='Billboard',Sprite='SoftDisc',Additive=False,LocalSpace=False,MaxParticles=1,Burst=1,Rate=0,Delay=0,Lifetime=.2,LifetimeVariation=0,Speed=0,SpeedVariation=0,Direction=v(0,1,0),Spread=180,Offset=v(0,.85,0),Gravity=v(0,0,0),StartSize=1,EndSize=0,SizeVariation=0,StartColor=color(1,1,1,1),EndColor=color(1,1,1,0),RandomRotation=True,SpriteResolution=64)
 d.update(kwargs);return d
for name in ['Mine blast','Mortar impact']:
 path=assets/(name+'.bvfx');shutil.copy2(path,backup/path.name);effect=json.loads(path.read_text());size=effect['Layers'][0]['StartSize']
 effect.update(Duration=1.45,ParticleBudget=32,Loop=False)
 effect['Layers']=[
  layer('ParticleFX cartoon explosion - 39 frames + clear tail',Texture={'Guid':guid,'CachedProjectPath':'Assets/VFX/Mining/ParticleFX Cartoon Explosion.png'},FlipbookColumns=7,FlipbookRows=6,SpriteResolution=256,Lifetime=1.4,StartSize=size,EndSize=size,StartColor=color(1,1,1,1),EndColor=color(1,1,1,1),RandomRotation=False),
  layer('Brief hot core',Sprite='Star',Additive=True,Lifetime=.065,StartSize=size*.28,EndSize=.05,Brightness=1.8,StartColor=color(1,.88,.42,.7),EndColor=color(1,.5,.1,0)),
  layer('Ground pressure ring',Sprite='Ring',RenderMode='Horizontal',Additive=True,Lifetime=.28,StartSize=.15,EndSize=size*.98,Offset=v(0,.06,0),StartColor=color(1,.72,.3,.4),EndColor=color(.8,.4,.2,0),RandomRotation=False),
  layer('Low rolling dust',Sprite='ToonPuff',Shape='Ring',Extents=v(.22,.02,.22),MaxParticles=6,Burst=6,Delay=.035,Lifetime=.55,LifetimeVariation=.15,Speed=1.3,SpeedVariation=.2,Direction=v(0,.15,0),Spread=180,Offset=v(0,.12,0),StartSize=.12,EndSize=.6,StartColor=color(.58,.38,.25,.35),EndColor=color(.45,.29,.22,0),Drag=2,SizeVariation=.2),
  layer('Hot fragments',Sprite='Solid',RenderMode='Stretched',MaxParticles=10,Burst=10,Lifetime=.55,LifetimeVariation=.2,Speed=3,SpeedVariation=.3,Direction=v(0,1,0),Spread=80,Gravity=v(0,-8,0),Offset=v(0,.3,0),StartSize=.045,EndSize=.015,StartColor=color(1,.72,.23,1),EndColor=color(.36,.19,.08,0),GroundBounce=True,GroundHeight=0,Bounciness=.15,RotationSpeed=120,SpriteResolution=32)
 ]
 path.write_text(json.dumps(effect,indent=2)+'\n')
notice='ParticleFX Studio — Cartoon Explosion\nhttps://www.particlefx.studio/effects/cartoon-explosion\nDownloaded using the public free PNG SHEET export.\nThe source page permits use in commercial games without credit. ParticleFX Originals sprites are listed CC0.\nByteEngine adaptation: original sheet retained; ground pressure ring, dust and fragments added as editable VFX layers.\n39 frames in a 7x6 sheet; final three cells are transparent. Playback: 1.4 seconds, once.\n'
(assets/'ParticleFX-CREDITS.txt').write_text(notice);(backup/'SOURCE.txt').write_text(notice);shutil.copy2(source,backup/source.name)
credit_meta=assets/'ParticleFX-CREDITS.txt.meta'
if not credit_meta.exists():credit_meta.write_text(json.dumps({'guid':str(uuid.uuid4()),'type':'Unknown'},indent=2))
print(json.dumps({'texture':str(texture),'backup':str(backup),'layers':5,'particle_budget':32,'blast_scales_unchanged':True}))
