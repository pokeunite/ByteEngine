"""Author original, renamed mining upgrades from the inspected mechanical categories."""
import json,math,hashlib
from pathlib import Path
c=json.loads(Path('Docs/Reference/Zombillionaire-Upgrade-Catalog.json').read_text())
definitions={
'UnlockAirdrop':('Emergency freight','Base','Unlock one supply crate delivery per operation; restores 24 reserve ammo and 15 health.'),
'PistolDamageFlat':('Bore tuning','Pistol','Pistol damage +{v} per rank.'),
'PistolFireRatePct':('Cycle regulator','Pistol','Pistol fire rate +{v}% per rank.'),
'PistolPellets':('Duplex cartridge','Pistol','Fire {v} extra pellet per shot per rank.'),
'SmallAmmoCap':('Field bandolier','Ammo','Reserve capacity +{v} rounds per rank.'),
'AmmoCapacityPct':('Logistics harness','Ammo','Reserve capacity +{v}% per rank.'),
'PlayerMaxHealth':('Operator plating','Health','Maximum health +{v} per rank.'),
'MoveSpeedPct':('Servo soles','Health','Movement speed +{v}% per rank.'),
'BunkerHealthPct':('Rig reinforcement','Base','Drill health +{v}% per rank.'),
'OilPriceFloor':('Broker margin','Market','Sale price +{v} credits per unit per rank.'),
'OilSellRate':('Transfer pump','Market','Shipment speed +{v} ore per second per rank.'),
'OilTankCapacity':('Cargo expansion','Market','Cargo storage +{v} units per rank.'),
'MarketPeakPct':('Premium contracts','Market','Market upper price limit +{v}% per rank.'),
'SirenLevel':('Hazard permit','Base','Each rank adds 2 enemies and +15% extraction.'),
'AutogunGrant':('Watchpost licence','Defence','Allows {v} additional deployed sentry per rank.'),
'AutogunDamageFlat':('Sentry payload','Defence','Sentry damage +{v} per rank.'),
'AutogunFireRateFlat':('Sentry drive','Defence','Sentry shots per second +{v} per rank.'),
'AutogunPierce':('Sentry penetrator','Defence','Sentry rounds pierce {v} extra target per rank.'),
'UnlockMachineGun':('Rotary licence','Defence','Unlock one deployable rotary emplacement.'),
'MachineGunDamageFlat':('Rotary payload','Defence','Rotary damage +{v} per rank.'),
'MachineGunFireRateFlat':('Rotary drive','Defence','Rotary shots per second +{v} per rank.'),
'MachineGunPierce':('Rotary penetrator','Defence','Rotary rounds pierce {v} extra target per rank.'),
'LandMineGrant':('Seismic charges','Mines','Allows {v} additional proximity mine per operation per rank.'),
'LandMineDamageFlat':('Charge packing','Mines','Mine damage +{v} per rank.'),
'LandMineBlastRadius':('Blast lens','Mines','Mine radius +{v} metres per rank.'),
'MortarStrikeGrant':('Lobber licence','Artillery','Allows {v} additional mortar emplacement per rank.'),
'MortarStrikeDamageFlat':('Lobber payload','Artillery','Mortar impact damage +{v} per rank.'),
'MortarStrikeInterval':('Lobber autoloader','Artillery','Mortar interval changes by {v} seconds per rank (min 0.8s).'),
'MortarShells':('Lobber salvo','Artillery','Mortar fires {v} additional shell per salvo per rank (max 8).')}
selected=[n for n in c['nodes'] if not n['DemoLocked'] and n['Effects'] and all(e['StatName'] in definitions for e in n['Effects'])]
assert all(len(n['Effects'])==1 for n in selected)
ids={n['Id']:'research-'+hashlib.sha256(('ByteCompany-'+n['Id']).encode()).hexdigest()[:16] for i,n in enumerate(selected)};allnodes={n['Id']:n for n in c['nodes']}
def ancestors(old,seen=None):
 seen=set() if seen is None else seen
 if old in seen:return []
 seen.add(old)
 if old in ids:return [ids[old]]
 return [p for ref in allnodes.get(old,{}).get('Prereqs',[]) for p in ancestors(ref,seen)]
counts={};family={};out=[]
columns={'Pistol':0,'Ammo':1,'Market':2,'Base':3,'Health':5,'Defence':6,'Mines':7,'Artillery':8}
for n in selected:
 e=n['Effects'][0];stat=e['StatName'];name,branch,desc=definitions[stat];family[stat]=family.get(stat,0)+1;counts[branch]=counts.get(branch,0)+1
 amount=e['ValuePerRank']
 if stat=='SmallAmmoCap':amount=max(3,round(min(24,amount*.2)))
 if stat=='PlayerMaxHealth':amount=round(amount*.4)
 if stat=='MachineGunFireRateFlat':amount=round(amount*.2,2)
 prereqs=list(dict.fromkeys(p for old in n['Prereqs'] for p in ancestors(old))) or ['company']
 costs=[max(15,round(10+math.sqrt(max(0,cost))*4)) if n['Currency']==0 else 100+rank*65+cost*20 for rank,cost in enumerate(n['RankCosts'])]
 out.append(dict(Id=ids[n['Id']],Name=name+(' '+str(family[stat]) if family[stat]>1 else ''),Description=desc.format(v=f'{amount:g}'),Stat=stat,Branch=branch,Amount=amount,Cost=costs[0],MaxRank=n['MaxRank'],RankCosts=costs,Prerequisites=prereqs,X=columns[branch],Y=4+counts[branch]))
Path('Plugins/KarlsonDemo/Mining/CompanyResearchCatalog.json').write_text(json.dumps(out,indent=2),encoding='utf-8')
Path('Docs/Reference/Company-Upgrade-Mapping.json').write_text(json.dumps([{'reference':n['Name'],'company':o['Name'],'id':o['Id'],'stat':o['Stat']} for n,o in zip(selected,out)],indent=2))
print(len(out),'new nodes;',sum(n['MaxRank'] for n in out),'ranks;',len(definitions),'supported effect types')
