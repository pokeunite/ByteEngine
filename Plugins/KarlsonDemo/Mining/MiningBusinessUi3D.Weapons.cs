using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Graphics;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningBusinessUi3D
{
 public bool ShowOwnedWeaponKeys{get;set;}=true;
 public float WeaponUnlockNoticeSeconds{get;set;}=7;
 readonly HashSet<Guid> knownHudWeapons=[];bool weaponHudInitialized;
 string weaponUnlockNotice="";float weaponUnlockClock;
 public int OwnedWeaponHudCount=>GameObject.Scene?.FindGameObject(Op?.PlayerObjectId??Guid.Empty)?.GetComponent<FpsGunLab3D>()?.CarriedWeapons.Count??0;
 void TickWeaponHud(float dt)
 {
  var carried=GameObject.Scene?.FindGameObject(Op?.PlayerObjectId??Guid.Empty)?.GetComponent<FpsGunLab3D>()?.CarriedWeapons;
  if(carried==null)return;
  for(int i=0;i<carried.Count;i++)if(knownHudWeapons.Add(carried[i].GameObject.Id)&&weaponHudInitialized){weaponUnlockNotice=$"{carried[i].Definition?.DisplayName??"WEAPON"} UNLOCKED / PRESS {WeaponKey(i)}";weaponUnlockClock=Math.Clamp(WeaponUnlockNoticeSeconds,2,20);}
  knownHudWeapons.RemoveWhere(id=>!carried.Any(w=>w.GameObject.Id==id));weaponHudInitialized=true;
  if(Screen==MiningScreen.None)weaponUnlockClock=Math.Max(0,weaponUnlockClock-dt);
 }
 static string WeaponKey(int index)=>index is >=0 and <3?MiningPauseMenu3D.KeyLabel(index==0?Key.D1:index==1?Key.D2:Key.D3):(index+1).ToString();
 void DrawOwnedWeaponHud(RenderContext c,float width,float height,float scale)
 {
  if(!ShowOwnedWeaponKeys||GameObject.Scene?.FindGameObject(Op?.PlayerObjectId??Guid.Empty)?.GetComponent<FpsGunLab3D>() is not {} gun)return;
  var carried=gun.CarriedWeapons;int count=Math.Min(3,carried.Count);if(count==0)return;
  float x=width-285,y=height-170,cell=265f/count;
  void Q(float xx,float yy,float w,float h,Vector4 color)=>c.DrawUiQuad(new(xx*scale,yy*scale),new(w*scale,h*scale),color);
  void T(string t,float xx,float yy,int size,Vector4 color)=>c.DrawUiText(t,BodyFont,(int)(size*scale),new(xx*scale,yy*scale),color,UiAnchor.TopLeft);
  for(int i=0;i<count;i++)
  {
   var weapon=carried[i];bool selected=weapon==gun.SelectedWeapon;float xx=x+i*cell;
   Q(xx,y,cell-4,61,selected?Accent:new(.29f,.31f,.34f,1));Q(xx+1,y+1,cell-6,59,Surface);
   T($"[{WeaponKey(i)}]",xx+7,y+5,17,selected?Accent:TextColor);
   var icon=weapon.Definition?.HudIcon;float iconWidth=Math.Min(58,cell-45);if(icon is {IsEmpty:false}&&AnimationRuntimeAssets.TryGet(out var assets)&&assets!=null)c.DrawUiImage(assets.LoadTexture(icon),new((xx+cell-iconWidth-7)*scale,(y+5)*scale),new(iconWidth*scale,25*scale),Vector4.One);
   string name=weapon.Definition?.DisplayName??"Weapon";T(name.Length>12?name[..11]+"…":name,xx+7,y+36,16,selected?Accent:TextColor);
  }
  if(weaponUnlockClock>0){Q(width-485,height-219,465,36,Surface);T(weaponUnlockNotice,width-474,height-211,17,Accent);}
 }
}
