using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Vfx;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 public bool AutomaticWeapons {get;set;}=true;
 float _gunCooldown,_rocketCooldown;bool _contractComplete;readonly List<(GameObject visual,float life)> _tracers=[];
 readonly List<(VfxPlayer player,VfxPreset preset)> _effects=[];readonly Dictionary<int,VfxPlayer> _tyreDust=[];
 readonly Dictionary<int,(GameObject node,Matrix4x4 rest)> _aimNodes=[];
 public int TargetsDefeated=>Targets().Count(t=>t.Defeated);
 DuneBountyTarget3D[] _targetCache=[];double _targetRefresh=-1;
 DuneBountyTarget3D[] Targets(){if(Time.TotalTime>=_targetRefresh){_targetRefresh=Time.TotalTime+.25;_targetCache=GameObject.Scene!.GameObjects.SelectMany(g=>g.Components).OfType<DuneBountyTarget3D>().ToArray();}return _targetCache;}
 DuneBountyTarget3D? AutoTarget(Vector3 origin,Vector3 forward,float range)
 {
  DuneBountyTarget3D? closest=null;float best=range;
  foreach(var target in Targets()){if(target.Defeated)continue;var center=target.Transform.WorldMatrix.Translation+Vector3.UnitY*.75f;var delta=center-origin;float distance=delta.Length();if(distance<.05f||distance>=best||Vector3.Dot(delta/distance,forward)<-.5f)continue;if(_sand!=null&&_sand.Heightfield.Cast(origin,delta,0,distance-.8f,out _,out _))continue;closest=target;best=distance;}
  return closest;
 }
 public bool FireWeapon(bool rocket)
 {
  if(Building||(rocket?_rocketCooldown:_gunCooldown)>0)return false;var guns=_blocks.Where(b=>b.Type==(rocket?31:30)).ToArray();if(guns.Length==0)return false;bool fired=false;
  foreach(var gun in guns){var visual=_visuals[gun.Id];var matrix=visual.Transform.WorldMatrix;var origin=Vector3.Transform(new Vector3(0,rocket?.85f:.59f,-.55f),matrix);var forward=Vector3.Normalize(Vector3.TransformNormal(-Vector3.UnitZ,matrix));var hit=AutoTarget(origin,forward,rocket?110:90);if(hit==null&&AutomaticWeapons)continue;var direction=hit!=null?Vector3.Normalize(hit.Transform.WorldMatrix.Translation+Vector3.UnitY*.75f-origin):forward;float distance=hit!=null?Vector3.Distance(origin,hit.Transform.WorldMatrix.Translation+Vector3.UnitY*.75f):90;var end=origin+direction*distance;
   if(hit!=null){bool wasDead=hit.Defeated;if(rocket)foreach(var target in Targets().Where(t=>Vector3.Distance(t.Transform.WorldMatrix.Translation,hit.Transform.WorldMatrix.Translation)<5))target.Damage(90*gun.Power);else hit.Damage(18*gun.Power);Effect(rocket?VfxPreset.Explosion:hit.Defeated&&!wasDead?VfxPreset.Explosion:VfxPreset.Impact,end,Quaternion.Identity,rocket?1.5f:.65f);}
   AimWeapon(gun, direction);Effect(VfxPreset.MuzzleFlash,origin,FromTo(-Vector3.UnitZ,direction),rocket?1.4f:.7f);
   var line=GameObject.Scene!.CreateGameObject(rocket?"Rocket trail":"Gun tracer");line.Transform.WorldPosition=(origin+end)*.5f;line.Transform.WorldRotation=FromTo(Vector3.UnitZ,direction);line.Transform.LocalScale=new(rocket?.065f:.018f,rocket?.065f:.018f,distance);line.AddComponent(new MeshRenderer{UsePrimitive=true,Primitive=PrimitiveMeshType.Cube,Material=new(){Shading=MaterialShadingMode.Unlit,BaseColor=rocket?new(1,.52f,.15f,1):new(1,.9f,.5f,1)},CastShadows=false});_tracers.Add((line,rocket?.18f:.045f));fired=true;
  }
  if(fired){if(rocket)_rocketCooldown=1.5f;else _gunCooldown=.16f;}return fired;
 }
 void AimWeapon(PlacedBlock gun,Vector3 direction)
 {
  if(!_aimNodes.TryGetValue(gun.Id,out var aim)){var root=_visuals[gun.Id];var node=Descendants(root).FirstOrDefault(o=>o.Name==(gun.Type==30?"Moving_WeaponRecoil":"Fixed_Body"));if(node==null)return;Matrix4x4.Invert(root.Transform.WorldMatrix,out var inverse);aim=(node,node.Transform.WorldMatrix*inverse);_aimNodes[gun.Id]=aim;}
  var rootMatrix=_visuals[gun.Id].Transform.WorldMatrix;var pivot=Vector3.Transform(aim.rest.Translation,rootMatrix);var rest=aim.rest;rest.Translation=Vector3.Zero;var targetRotation=FromTo(-Vector3.UnitZ,direction);var world=Matrix4x4.CreateFromQuaternion(targetRotation)*Matrix4x4.CreateTranslation(pivot);SetRenderedWorldPose(aim.node,rest*world);
 }
 void Effect(VfxPreset preset,Vector3 position,Quaternion rotation,float size)
 {
  VfxPlayer? player=_effects.FirstOrDefault(e=>e.preset==preset&&!e.player.IsPlaying).player;
  if(player==null){if(_effects.Count>=16)return;var obj=GameObject.Scene!.CreateGameObject("Combat VFX - "+preset);player=obj.AddComponent(new VfxPlayer{Preset=preset,PlayOnStart=false,ViewDistance=160,QualityDistance=45});string name=preset switch{VfxPreset.Explosion=>"fuel-explosion",VfxPreset.Impact=>"metal-impact",_=>"muzzle-flash"};string file=Path.Combine(ProjectRoot,"Assets/VFX/"+name+".bvfx");if(File.Exists(file))player.SetDefinition(VfxEffectSerializer.Load(file));_effects.Add((player,preset));}
  player.Transform.WorldPosition=position;player.Transform.WorldRotation=rotation;player.Size=size;player.Play();
 }
 void ClearCombatVisuals(){ClearVehicleFeedback();_aimNodes.Clear();foreach(var p in _tyreDust.Values){if(p.GameObject.Scene!=null)GameObject.Scene!.DestroyGameObject(p.GameObject);}_tyreDust.Clear();}
 void UpdateCombat(float dt)
 {
  _gunCooldown=Math.Max(0,_gunCooldown-dt);_rocketCooldown=Math.Max(0,_rocketCooldown-dt);for(int i=_tracers.Count-1;i>=0;i--){var (visual,life)=_tracers[i];life-=dt;if(life<=0){GameObject.Scene!.DestroyGameObject(visual);_tracers.RemoveAt(i);}else _tracers[i]=(visual,life);}
  if(Building){StopVehicleFeedback();foreach(var dust in _tyreDust.Values)dust.Stop(true);Text("Battle objective","PROVING CONTRACT / Build a machine and press B");return;}
  if(AutomaticWeapons)foreach(var gun in _blocks.Where(b=>b.Type is 30 or 31)){var matrix=_visuals[gun.Id].Transform.WorldMatrix;var origin=matrix.Translation+Vector3.UnitY*.7f;var target=AutoTarget(origin,Vector3.Normalize(Vector3.TransformNormal(-Vector3.UnitZ,matrix)),gun.Type==31?110:90);if(target!=null)AimWeapon(gun,Vector3.Normalize(target.Transform.WorldMatrix.Translation+Vector3.UnitY*.75f-origin));}
  if(AutomaticWeapons||Input.IsMouseButtonDown(MouseButton.Left)&&Input.GameViewPointerNormalized.Y>.08f)FireWeapon(false);if(AutomaticWeapons||Input.IsKeyDown(Key.F))FireWeapon(true);UpdateVehicleFeedback(dt);
  foreach(var target in Targets().Where(t=>!t.Defeated))if(Math.Abs(Speed)>5&&Vector3.Distance(Transform.WorldPosition,target.Transform.WorldPosition)<target.Radius+1)target.Damage(Math.Abs(Speed)*12*dt);
  var targets=Targets();if(targets.Length>0){int dead=targets.Count(t=>t.Defeated);bool returnToCamp=dead==targets.Length;if(returnToCamp&&Vector3.Distance(Transform.WorldPosition,_garage)<5)_contractComplete=true;Text("Battle objective",_contractComplete?"BOUNTY COMPLETE / Return to Build to improve your machine":returnToCamp?"ALL TARGETS DOWN / Return to the workshop":"PROVING CONTRACT / Destroy caches "+dead+"/"+targets.Length+" / Weapons track and fire automatically");}
 }
}
