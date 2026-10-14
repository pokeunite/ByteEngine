"""Convert verified Freesound CC0 previews to compact game WAVs."""
from pathlib import Path
import sys,uuid,json
sys.path.insert(0,str(Path('.artifacts/audio-tools').resolve()))
import soundfile as sf
import numpy as np
root=Path('C:/Users/codex/Documents/DuneCompany/Assets/Audio/Feedback')
for name,source,duration,url,author in [('combat-gun','convoy-gun.mp3',.40,'https://freesound.org/people/Brokenphono/sounds/344142/','Brokenphono'),('combat-blast','convoy-explosion.mp3',2.5,'https://freesound.org/people/qubodup/sounds/741175/','qubodup')]:
 data,rate=sf.read('.artifacts/'+source,always_2d=True);data=data.mean(axis=1)[:int(duration*rate)];data*=.82/max(.01,np.abs(data).max());fade=min(int(rate*.035),len(data)//3);data[-fade:]*=np.linspace(1,0,fade);target=root/(name+'.wav');sf.write(target,data,rate,subtype='PCM_16')
 Path(str(target)+'.meta').write_text(json.dumps({'guid':str(uuid.uuid5(uuid.NAMESPACE_URL,'dune-'+name)),'type':'AudioClip'}))
 (root/(name+'-LICENSE.txt')).write_text(f'{name}\nSource: {url}\nAuthor: {author}\nLicense: CC0 1.0 https://creativecommons.org/publicdomain/zero/1.0/\nConverted from the public high-quality preview; mono, trimmed, gain normalized and tail faded.\n')
 print(target)
