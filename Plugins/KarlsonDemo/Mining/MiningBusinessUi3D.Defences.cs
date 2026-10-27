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
 public Key DefencePointerKey{get;set;}=Key.LeftAlt;
 bool DefencePointerHeld=>Input.IsKeyDown(DefencePointerKey)||(DefencePointerKey is Key.LeftAlt or Key.RightAlt&& (Input.IsKeyDown(Key.LeftAlt)||Input.IsKeyDown(Key.RightAlt)));
 readonly object defencePointerOwner=new();bool defencePicked;
 readonly Dictionary<MiningDefenseKind,int> lastReady=[];readonly Dictionary<MiningDefenseKind,double> readyGlow=[];
 MiningDefensePlanner3D? DefencePlanner=>GameObject.Scene?.FindGameObject(Op?.PlayerObjectId??Guid.Empty)?.GetComponent<MiningDefensePlanner3D>();
 public void SelectDefence(int index)
 {
  if(CampEditing||Screen!=MiningScreen.None||!Enum.IsDefined((MiningDefenseKind)index)||DefencePlanner is not {} planner)return;
  if(planner.SelectBuild((MiningDefenseKind)index)){defencePicked=true;foreach(var id in DefenceButtonObjectIds)if(W(id) is {} button)button.Interactable=false;Input.RequestUiPointer(defencePointerOwner,false);MiningAudio3D.Find(GameObject)?.PlayCue(MiningSoundCue.Confirm);}
 }
 void UpdateDefenceHud()
 {
  var planner=DefencePlanner;bool visible=!CampEditing&&Screen==MiningScreen.None&&Op?.Phase!=MiningPhase.Failed;
  if(!DefencePointerHeld)defencePicked=false;
  bool choosing=visible&&!defencePicked&&DefencePointerHeld&&(CampLayout?.CanEdit==true||planner!=null&&Enum.GetValues<MiningDefenseKind>().Any(k=>planner.Available(k)>0));
  Input.RequestUiPointer(defencePointerOwner,choosing);
  var size=Input.GameViewSize;float s=Math.Max(.01f,Math.Min(size.X/1280,size.Y/720));float width=size.X/s,height=size.Y/s;int row=0;
  for(int i=0;i<5;i++)
  {
   var kind=(MiningDefenseKind)i;int available=planner?.Available(kind)??0;bool show=visible&&(planner?.Allowance(kind)??0)>0;
   if(available>lastReady.GetValueOrDefault(kind))readyGlow[kind]=Time.TotalTime+3;lastReady[kind]=available;
   if(i<DefenceButtonObjectIds.Count&&W(DefenceButtonObjectIds[i]) is {} widget)
   {widget.Visible=show;widget.Interactable=available>0&&!defencePicked&&(choosing||!Input.IsGameInputCaptured);widget.Anchor=UiAnchor.Center;widget.Size=new(200,64);widget.Offset=new(width-220+100-width/2,108+row*72+32-height/2);}
   if(show)row++;
  }
 }
 void DrawDefenceHud(RenderContext c,float width,float height,float scale)
 {
  if(DefencePlanner is not {} planner||Op?.Phase==MiningPhase.Failed)return;int row=0;
  void Q(float x,float y,float w,float h,Vector4 color)=>c.DrawUiQuad(new(x*scale,y*scale),new(w*scale,h*scale),color);
  void T(string text,float x,float y,int size,Vector4 color)=>c.DrawUiText(text,BodyFont,Math.Max(12,(int)(size*scale)),new(x*scale,y*scale),color,UiAnchor.TopLeft);
  for(int i=0;i<5;i++)
  {
   var kind=(MiningDefenseKind)i;if(planner.Allowance(kind)<=0)continue;int count=planner.Available(kind);float x=width-220,y=108+row++*72;bool glow=count>0&&readyGlow.GetValueOrDefault(kind)>Time.TotalTime;
   Q(x-1,y-1,202,66,glow||(planner.BuildMode&&planner.SelectedKind==kind)?Accent:new(.32f,.38f,.27f,1));Q(x,y,200,64,count>0?new(.10f,.15f,.085f,.97f):Surface);
   string icon=kind is MiningDefenseKind.Sentry or MiningDefenseKind.Repeater?"turret":kind==MiningDefenseKind.Mortar?"mortar":kind==MiningDefenseKind.Mine?"mine":"crate";
   if(AnimationRuntimeAssets.TryGet(out var assets)&&assets!=null)c.DrawUiImage(assets.LoadTexture(new AssetReference("Assets/UI/Mining/Artwork/research-"+icon+".png")),new((x+9)*scale,(y+13)*scale),new(38*scale),count>0?TextColor:new(.4f,.45f,.36f,1));
   string name=kind switch{MiningDefenseKind.Sentry=>"SENTRY",MiningDefenseKind.Repeater=>"ROTARY GUN",MiningDefenseKind.Mortar=>"MORTAR",MiningDefenseKind.Mine=>"MINE",_=>"SUPPLY DROP"};
   T(name,x+56,y+5,20,TextColor);var shortcut=new[]{planner.SentryKey,planner.RepeaterKey,planner.MortarKey,planner.MineKey,planner.SupplyKey}[i];T(count>0?$"[{MiningPauseMenu3D.KeyLabel(shortcut)}] PLACE / {count} READY":"ALL DEPLOYED",x+56,y+34,16,count>0?Accent:new(.6f,.65f,.54f,1));
  }
  if(row>0)T($"Hold {DefencePointerKey.ToString().Replace("Left","").Replace("Right","")} to click a defence",width-220,108+row*72+2,17,TextColor);
  if(planner.BuildMode)
  {Q(width/2-265,height-185,530,54,Surface);T($"{planner.SelectedKind} / [LMB] PLACE / [RMB] CANCEL / [{MiningPauseMenu3D.KeyLabel(planner.BuildKey)}] NEXT",width/2-250,height-174,20,Accent);}
 }
}
