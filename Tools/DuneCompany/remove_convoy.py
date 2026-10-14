"""Retire the experimental contract while preserving source/art outside active game folders."""
import ast,shutil
from pathlib import Path
repo=Path('C:/Users/codex/ByteEngine');plugin=repo/'Plugins/DuneCompany';project=Path('C:/Users/codex/Documents/DuneCompany')
tree=ast.parse((repo/'Tools/DuneCompany/integrate_convoy.py').read_text())
for statement in reversed(tree.body):
 if isinstance(statement,ast.Expr) and isinstance(statement.value,ast.Call) and isinstance(statement.value.func,ast.Name) and statement.value.func.id=='edit':
  name,old,new=[ast.literal_eval(a) for a in statement.value.args]
  if old==new:continue
  p=plugin/name;s=p.read_text(encoding='utf-8-sig')
  if new in s:p.write_text(s.replace(new,old),encoding='utf-8')
  else:print('Check unmapped change',name,new[:65])
p=plugin/'DuneWorkshop3D.MotorsportMenus.cs';s=p.read_text();s='\n'.join(line for line in s.splitlines() if 'Contracts combat page' not in line and 'Contracts combat category' not in line);p.write_text(s+'\n')
p=plugin/'DuneCompanyPlugin.cs';s=p.read_text();p.write_text('\n'.join(line for line in s.splitlines() if 'RegisterSimpleComponent<DuneConvoyContract3D>' not in line)+'\n')
p=plugin/'DuneWorkshop3D.Winch.cs';s=p.read_text();p.write_text(s.replace('if(AnchorMission||ConvoyMission)return;','if(AnchorMission)return;'))
p=plugin/'LandVehiclePhysics.Assemblies.cs';s=p.read_text();p.write_text('\n'.join(line for line in s.splitlines() if 'public void EnableRecoveryDrive()' not in line)+'\n')
p=plugin/'DuneWorkshop3D.Recovery.cs';s=p.read_text();a=s.index('   foreach(float z in new[]{-.65f,1.3f,3.1f})');b=s.index('\n  }else{',a);s=s[:a]+'''   foreach(float z in new[]{-.65f,1.3f,3.1f})foreach(int x in new[]{-1,1})Add(15,new(x*.78f,.04f,z),Quaternion.CreateFromAxisAngle(Vector3.UnitY,-x*MathF.PI*.5f));'''+s[b:];p.write_text(s)
p=repo/'Player/ByteEngine.Browser/Program.cs';s=p.read_text();p.write_text(s.replace('combat=vehicle?.ConvoyDiagnostics,',''))
p=repo/'Tests/ByteEngine.Tests/DunePolishDiagnostic.StarterLevels.cs';s=p.read_text();p.write_text('\n'.join(line for line in s.splitlines() if 'DUNE_CONVOY_ONLY' not in line)+'\n')
def archive(p,base,destination):
 p=p.resolve();base=base.resolve();destination=destination.resolve()
 assert p.is_relative_to(base) and destination.is_relative_to(base),p
 if p.exists():
  target=destination/p.relative_to(base);target.parent.mkdir(parents=True,exist_ok=True)
  if target.exists():raise RuntimeError('Archive target already exists: '+str(target))
  shutil.move(str(p),str(target))
for p in [plugin/'DuneConvoyContract3D.cs',plugin/'DuneWorkshop3D.Convoy.cs',repo/'Tests/ByteEngine.Tests/DunePolishDiagnostic.Convoy.cs']:
 archive(p,repo,repo/'Docs/ArchivedCombat')
for base in [project,repo]:
 root=project if base==project else plugin
 active=root/'Assets' if base==project else root/'Content'
 for folder,patterns in [('RaceKit',['convoy*','salvage-sign*']),('ContractLevels',['convoy*']),('Presentation',['contract-8*'])]:
  for pattern in patterns:
   for p in (active/folder).glob(pattern):archive(p,base,base/('Docs/CombatConcepts/Archived' if base==project else 'Docs/ArchivedCombat'))
 scene=project/'Scenes/ConvoyBreaker.bytescene' if base==project else plugin/'Content/Scenes/ConvoyBreaker.bytescene'
 archive(scene,base,base/('Docs/CombatConcepts/Archived' if base==project else 'Docs/ArchivedCombat'))
for name in ['DuneConvoyContract3D.cs','DuneWorkshop3D.Convoy.cs']:
 archive(project/'PluginSource/DuneCompany'/name,project,project/'Docs/CombatConcepts/Archived')
print('Combat contract removed from active code/scenes/assets; originals archived')
