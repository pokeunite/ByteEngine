"""Read-only extraction of selected authored progression data for a local design audit.
Inspection output belongs in ignored artifacts, never in the game content package.
"""
import sys, json, struct
from pathlib import Path
sys.path.insert(0, str(Path('artifacts/unity-inspector').resolve()))
import UnityPy
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
root=Path(r'C:/Program Files (x86)/Steam/steamapps/common/ZOMBILLIONAIRE Demo/ZOMBILLIONAIRE_Data')
out=Path('artifacts/zombillionaire-upgrade-audit');out.mkdir(parents=True,exist_ok=True)
interesting={'SkillTreeAsset','SkillConfig','SkillStatRegistry','PressConfig','ShopConfig','WaveTreeAsset','AirdropConfig','AtomicBombConfig'}
generator=None
records=[]; failures=[]; errors=[]; scripts=set()
class Reader:
    def __init__(self,data,pos): self.data=data;self.pos=pos
    def read(self,fmt):
        value=struct.unpack_from('<'+fmt,self.data,self.pos);self.pos+=struct.calcsize('<'+fmt);return value[0] if len(value)==1 else value
    def align(self): self.pos=(self.pos+3)&~3
    def string(self):
        size=self.read('i');assert 0<=size<100000,(self.pos,size)
        value=self.data[self.pos:self.pos+size].decode('utf-8');self.pos+=size;self.align();return value
    def array(self,read):
        size=self.read('i');assert 0<=size<100000,(self.pos,size)
        return [read() for _ in range(size)]
def read_tree(raw,start,header):
    r=Reader(raw,start);tree=dict(header);tree['TreeName']=r.string();tree['HiddenTab']=bool(r.read('?'));r.align();tree['CenterNodeId']=r.string()
    def node():
        n={k:r.string() for k in ['Id','Name','LocKey']};n['Icon']={'m_FileID':r.read('i'),'m_PathID':r.read('q')};n['IconManual']=r.read('?');r.align()
        n['Tint']=r.read('ffff');n['Position']=r.read('ff');n['Scale']=r.read('f');n['MaxRank']=r.read('i');n['Currency']=r.read('i');n['RankCosts']=r.array(lambda:r.read('q'))
        n['Effects']=r.array(lambda:{'Stat':r.read('i'),'Op':r.read('i'),'ValuePerRank':r.read('f')});n['Prereqs']=r.array(r.string);return n
    tree['Nodes']=r.array(node);assert r.pos==len(raw),(r.pos,len(raw));return tree
for filename in ['resources.assets','sharedassets0.assets','sharedassets1.assets','sharedassets2.assets','level1','level2']:
    print('READ',filename,flush=True)
    env=UnityPy.load(str(root/filename))
    if generator is None:
        version=next(iter(env.assets)).unity_version
        print('UNITY',version,flush=True)
        generator=TypeTreeGenerator(str(version))
        generator.load_local_dll_folder(str(root/'Managed'))
    env.typetree_generator=generator
    for obj in env.objects:
        if obj.type.name!='MonoBehaviour':continue
        try:
            env.typetree_generator=None
            data=obj.read(check_read=False)
            custom_start=(32+len(data.m_Name.encode("utf-8"))+3)&~3
            script=data.m_Script.deref().read()
            name=script.m_ClassName
            scripts.add(name)
            if name not in interesting:continue
            try:
                fullname=(script.m_Namespace+'.' if script.m_Namespace else '')+name
                if name=='SkillTreeAsset':tree=read_tree(obj.get_raw_data(),custom_start,{'m_Name':data.m_Name})
                else:
                    node=generator.get_nodes_up(script.m_AssemblyName,fullname)
                    tree=obj.read_typetree(nodes=node)
            except Exception as e:
                failures.append({'file':filename,'pathId':obj.path_id,'class':name,'error':str(e)});continue
            records.append({'file':filename,'pathId':obj.path_id,'class':name,'data':tree})
            print('FOUND',name,tree.get('m_Name',''),flush=True)
        except Exception as e:
            if len(errors)<12:errors.append(str(e))
    del env
(out/'authored-upgrades.json').write_text(json.dumps(records,indent=2,ensure_ascii=False,default=str),encoding='utf-8')
(out/'parse-failures.json').write_text(json.dumps(failures,indent=2),encoding='utf-8')
print('DONE',len(records),'records;',len(failures),'missing type trees',flush=True)
print('SCRIPTS',sorted(scripts), 'ERRORS',errors,flush=True)
