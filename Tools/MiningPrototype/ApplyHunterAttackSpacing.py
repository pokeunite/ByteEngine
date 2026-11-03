"""Author the hunter's editable attack/back-off cycle; leave rig raiders alone."""
import argparse, copy, datetime, json, pathlib, uuid
p=argparse.ArgumentParser();p.add_argument('--apply',action='store_true');a=p.parse_args()
path=pathlib.Path('C:/Users/codex/Documents/palclone/Assets/AI/Mining/Alien Hunter.byteai')
original=path.read_text(encoding='utf-8-sig');tree=json.loads(original);gate=tree['Root']['Children'][0]
if gate['Children'][0].get('Name')=='Strike, back off and reassess':
    print('Hunter spacing already authored');raise SystemExit(0)
attack=gate['Children'][0]
assert attack['Kind']=='SubBehavior' and 'Alien melee attack' in attack['SubBehavior']['ProjectPath']
def node(kind,name,**values):
    n=copy.deepcopy(attack);n.update(Id=str(uuid.uuid5(uuid.NAMESPACE_URL,'hazard-pay/hunter-spacing/'+kind)),Name=name,Kind=kind,Children=[],SubBehavior={'Guid':'00000000-0000-0000-0000-000000000000','ProjectPath':''},**values);return n
sequence=node('Sequence','Strike, back off and reassess')
sequence['Children']=[attack,
    node('Retreat','Step back after attacking',Distance=2.7,Speed=2.8,Timeout=1.25,Notes='Back off to 2.7 m after recovery. A blocked retreat times out and returns to normal pursuit.'),
    node('StopMovement','Stop at the recovery spacing'),
    node('FaceTarget','Face the operator again'),
    node('Wait','Brief opening before pursuing again',Duration=.3)]
gate['Children']=[sequence];gate['Interruption']='FinishBranch'
gate['Notes']='Keep this complete attack/back-off cycle running after entering reach. Death and designated stagger still interrupt it.'
tree['Name']='Alien Hunter - strike, back off and pursue'
output=json.dumps(tree,indent=2)+'\n'
if a.apply:
    backup=pathlib.Path('artifacts/hunter-spacing-backups')/datetime.datetime.now().strftime('%Y%m%d-%H%M%S');backup.mkdir(parents=True)
    (backup/path.name).write_text(original,encoding='utf-8');path.write_text(output,encoding='utf-8')
    print('Authored:',path,'Backup:',backup)
else: print('Preview: attack/recover -> retreat to 2.7 m at 2.8 m/s (1.25 s timeout) -> face target -> pause 0.3 s. Use --apply to write.')
