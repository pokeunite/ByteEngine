from pathlib import Path
import shutil,json,uuid
w=Path(__file__).resolve().parents[2];p=Path('C:/Users/codex/Documents/palclone')
for f in (w/'artifacts/company-kit').iterdir():
 target=p/('Assets/UI/Mining/FieldHud' if f.suffix=='.png' else 'SourceArt/Company Systems')/f.name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(f,target)
 if f.suffix=='.png' and not Path(str(target)+'.meta').exists():Path(str(target)+'.meta').write_text(json.dumps({'guid':str(uuid.uuid5(uuid.NAMESPACE_URL,'hazardpay/company/'+f.name)),'type':'Texture2D'},indent=2))
print('Installed company pictograms and editable SVG source.')
