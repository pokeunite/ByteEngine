using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Graphics;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningBusinessUi3D
{
 public List<Guid> DefenceButtonObjectIds{get;set;}=[];
 public string DefenceButtonBindings{get=>string.Join(";",DefenceButtonObjectIds);set=>DefenceButtonObjectIds=(value??"").Split(';',StringSplitOptions.RemoveEmptyEntries).Select(s=>Guid.TryParse(s,out var id)?id:Guid.Empty).Where(id=>id!=Guid.Empty).ToList();}
 readonly object defencePointerOwner=new();
 readonly Dictionary<MiningDefenseKind,int> lastReady=[];readonly Dictionary<MiningDefenseKind,double> readyGlow=[];
 MiningDefensePlanner3D? DefencePlanner=>GameObject.Scene?.FindGameObject(Op?.PlayerObjectId??Guid.Empty)?.GetComponent<MiningDefensePlanner3D>();
 public void SelectDefence(int index)
 {
  if(CampEditing||Screen!=MiningScreen.None||!Enum.IsDefined((MiningDefenseKind)index)||DefencePlanner is not {} planner)return;
  if(planner.SelectBuild((MiningDefenseKind)index)){foreach(var id in DefenceButtonObjectIds)if(W(id) is {} button)button.Interactable=false;Input.RequestUiPointer(defencePointerOwner,false);MiningAudio3D.Find(GameObject)?.PlayCue(MiningSoundCue.Confirm);}
 }
 void UpdateDefenceHud()
 {
  Input.RequestUiPointer(defencePointerOwner,false);
  foreach(var id in DefenceButtonObjectIds)if(W(id) is {} widget){widget.Visible=false;widget.Interactable=false;}
  if(DefencePlanner is not {} planner)return;
  foreach(var kind in Enum.GetValues<MiningDefenseKind>()){int available=planner.Available(kind);if(available>lastReady.GetValueOrDefault(kind))readyGlow[kind]=Time.TotalTime+3;lastReady[kind]=available;}
 }
 public int ReadyDefenceHudCount=>DefencePlanner is {} p?Enum.GetValues<MiningDefenseKind>().Count(k=>p.Available(k)>0):0;
 void DrawDefenceHud(RenderContext c,float width,float height,float scale)
 {
  if(DefencePlanner is not {} planner||Op?.Phase==MiningPhase.Failed)return;int row=0;
  void Q(float x,float y,float w,float h,Vector4 color)=>c.DrawUiQuad(new(x*scale,y*scale),new(w*scale,h*scale),color);
  void T(string text,float x,float y,int size,Vector4 color,UiAnchor anchor=UiAnchor.TopLeft)=>c.DrawUiText(text,BodyFont,Math.Max(12,(int)(size*scale)),new(x*scale,y*scale),color,anchor);
  for(int i=0;i<5;i++)
  {
   var kind=(MiningDefenseKind)i;int count=planner.Available(kind);if(count<=0)continue;float x=width-268,y=100+row++*72;bool glow=count>0&&readyGlow.GetValueOrDefault(kind)>Time.TotalTime;
   Q(x-1,y-1,250,66,glow||(planner.BuildMode&&planner.SelectedKind==kind)?Accent:new(.26f,.29f,.32f,1));Q(x,y,248,64,Surface);
   string icon=kind is MiningDefenseKind.Sentry or MiningDefenseKind.Repeater?"turret":kind==MiningDefenseKind.Mortar?"mortar":kind==MiningDefenseKind.Mine?"mine":"crate";
   if(AnimationRuntimeAssets.TryGet(out var assets)&&assets!=null)c.DrawUiImage(assets.LoadTexture(new AssetReference("Assets/UI/Mining/Artwork/research-"+icon+".png")),new((x+9)*scale,(y+13)*scale),new(38*scale),count>0?TextColor:new(.4f,.45f,.36f,1));
   string name=kind switch{MiningDefenseKind.Sentry=>"SENTRY",MiningDefenseKind.Repeater=>"ROTARY GUN",MiningDefenseKind.Mortar=>"MORTAR",MiningDefenseKind.Mine=>"MINE",_=>"SUPPLY DROP"};
   T(name,x+56,y+7,18,TextColor);var shortcut=new[]{planner.SentryKey,planner.RepeaterKey,planner.MortarKey,planner.MineKey,planner.SupplyKey}[i];T($"[{MiningPauseMenu3D.KeyLabel(shortcut)}] PLACE",x+56,y+36,14,Accent);T($"{count} READY",x+234,y+36,14,TextColor,UiAnchor.TopRight);
  }

  if(planner.BuildMode)
  {Q(width/2-245,height-160,490,58,Surface);T($"{planner.SelectedKind.ToString().ToUpperInvariant()} / LMB PLACE / RMB CANCEL",width/2-230,height-151,16,Accent);T($"[{MiningPauseMenu3D.KeyLabel(planner.BuildKey)}] NEXT DEFENCE",width/2-230,height-128,14,TextColor);}
 }
}
