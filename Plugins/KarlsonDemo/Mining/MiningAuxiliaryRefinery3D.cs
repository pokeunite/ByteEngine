using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using Component=ByteEngine.Core.Scene.Component;
namespace KarlsonDemo.Plugin;
/// <summary>Independent finite-load refinery; shared upgrades, separate batch timer and reserved raw ore.</summary>
public sealed partial class MiningAuxiliaryRefinery3D:Component
{
 [SceneObjectReference]public Guid OperationObjectId{get;set;}
 public bool AutoLoad{get;set;}
 public float AutomaticTimeMultiplier{get;set;}=1.35f;
 bool automaticLoad,automaticRun;
 public int LoadCapacity{get;set;}=50;
 public AssetReference Font{get;set;}=new("Assets/UI/Mining/Fonts/BarlowCondensed-Medium.ttf");
 [Browsable(false)]public int LoadedRaw{get;private set;}
 [Browsable(false)]public int CompletedOre{get;private set;}
 [Browsable(false)]public float Remaining{get;private set;}
 [Browsable(false)]public float Progress=>batch>0?Math.Clamp(1-Remaining/Math.Max(.1f,total),0,1):0;
 public string Status{get;private set;}="Load up to 50 raw ore";
 int batch;float total;double remainder;bool near;
 MiningOperation3D? Op=>GameObject.Scene?.FindGameObject(OperationObjectId)?.GetComponent<MiningOperation3D>();
 public bool BeginLoad(){if(Op is not {} op||LoadedRaw>0)return false;automaticRun=automaticLoad;LoadedRaw=op.LoadAuxiliaryRaw(Math.Clamp(LoadCapacity,1,50));if(LoadedRaw==0){Status="No unreserved raw ore available";return false;}StartBatch(op);op.GameObject.GetComponent<MiningResearchStation3D>()?.PersistSupport();return true;}
 void StartBatch(MiningOperation3D op){batch=Math.Min(LoadedRaw,BatchSize);Remaining=total=BatchDuration*(automaticRun?Math.Clamp(AutomaticTimeMultiplier,1,3):1);Status="Processing";}
 public void Restore(int raw,int completed){LoadedRaw=Math.Clamp(raw,0,50);CompletedOre=Math.Max(0,completed);if(LoadedRaw>0&&Op is {} op)StartBatch(op);}
 public void Tick(float dt)
 {
  if(!float.IsFinite(dt)||dt<=0||Op is not {} op)return;
  if(op.Phase==MiningPhase.Failed){if(LoadedRaw>0){op.ReturnAuxiliaryRaw(LoadedRaw);LoadedRaw=0;batch=0;Remaining=0;}return;}
  if(batch<=0){if(AutoLoad&&op.CanUseIndustry&&op.RawOre>0){automaticLoad=true;BeginLoad();automaticLoad=false;}return;}Remaining=Math.Max(0,Remaining-dt);if(Remaining>0)return;
  int input=batch;double produced=input*YieldMultiplier+remainder;int output=(int)Math.Floor(produced);LoadedRaw-=input;
  if(!op.CompleteAuxiliaryBatch(output)){LoadedRaw+=input;Status="Cargo full / sell ore";return;}
  remainder=produced-output;CompletedOre+=output;batch=0;if(LoadedRaw>0)StartBatch(op);else{Status="Load complete / E to reload";Remaining=0;}op.GameObject.GetComponent<MiningResearchStation3D>()?.PersistSupport();
 }
 protected override void OnUpdate(){Tick((float)Time.DeltaTime);var op=Op;var player=GameObject.Scene?.FindGameObject(op?.PlayerObjectId??Guid.Empty);near=op!=null&&MiningCampInteraction.Nearest(GameObject.Scene!,op)==GameObject;if(near&&player?.GetComponent<MiningDefensePlanner3D>()?.BuildMode!=true&&op?.CanUseIndustry==true&&!Input.IsUiPointerRequested&&player?.GetComponent<MiningCampLayout3D>()?.IsEditing!=true&&Input.IsKeyPressed(Key.E))Open();if(IsOpen){if(!near||op?.CanUseIndustry!=true)Close();else TickMenuInput();}}
 protected override void OnRender(RenderContext c){if(IsOpen){DrawMenu(c);return;}if(!near||Input.IsUiPointerRequested)return;float s=Math.Min(c.TargetWidth/1280f,c.TargetHeight/720f),x=c.TargetWidth/s/2-185,y=c.TargetHeight/s-128;c.DrawUiQuad(new(x*s,y*s),new(370*s,54*s),new(.025f,.035f,.045f,.96f));c.DrawUiText(LoadedRaw>0?$"REFINERY / {LoadedRaw} RAW / {Progress:P0}":"[E] EXTRA REFINERY",Font,(int)(18*s),new((x+12)*s,(y+8)*s),new(1,.79f,.12f,1),UiAnchor.TopLeft);c.DrawUiQuad(new((x+12)*s,(y+39)*s),new(346*s,5*s),new(.2f,.23f,.25f,1));c.DrawUiQuad(new((x+12)*s,(y+39)*s),new(346*s*Progress,5*s),new(1,.79f,.12f,1));}
 protected override void OnStop()=>Close();

}
public sealed partial class MiningOperation3D
{
 public int AuxiliaryRawReserved=>GameObject.Scene?.GetComponentsSnapshot<MiningAuxiliaryRefinery3D>().Where(r=>r.OperationObjectId==GameObject.Id).Sum(r=>r.LoadedRaw)??0;
 internal int LoadAuxiliaryRaw(int maximum){Initialize();if(!CanUseIndustry)return 0;int amount=Math.Min(maximum,Math.Max(0,RawOre-RefiningAmount));RawOre-=amount;return amount;}
 internal void ReturnAuxiliaryRaw(int amount){RawOre=Add(RawOre,Math.Max(0,amount));}
 internal bool CompleteAuxiliaryBatch(int amount){if(amount<0)return false;RefinedOre=Add(RefinedOre,amount);return true;}
 public void AddTestingRefinedOre(int amount){Initialize();RefinedOre=Add(RefinedOre,Math.Clamp(amount,0,1000000));SaveProgress();}
}
