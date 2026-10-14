using System.Reflection;
using System.Numerics;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Scene;
using ByteEngine.Editor;
namespace KarlsonDemoTool;
internal static class EditorCheck
{
    public static void Run()
    {
        using var editor=new EditorApplication(Program.Root+"/karlson demo.byteproject");int frames=0;var checks=new List<string>();
        editor.Load+=()=>editor.IsVisible=false;
        editor.UpdateFrame+=_=>
        {
            frames++;
            if(frames==2)
            {
                typeof(EditorApplication).GetMethod("EnterPlayMode",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(editor,null);
                var scene=editor.Scenes.ActiveScene??throw new Exception("Editor Play did not start");
                if(scene.GameObjects.SelectMany(o=>o.Components).Any(c=>c.GetType().Name=="MissingComponent"))throw new Exception("Missing component in editor Play");checks.Add("Editor Play loads all serialized components");
                for(int tick=0;tick<120;tick++)Tick(scene,new());
                var player=scene.GameObjects.Single(o=>o.Name=="FPS Character");if(!player.GetComponent<CharacterController3D>()!.IsGrounded||player.Transform.WorldPosition.Y<-.05f)throw new Exception("Editor character fell through starting floor");checks.Add("Editor character stays grounded for two seconds");
                var pickerType=typeof(EditorApplication).Assembly.GetType("ByteEngine.Editor.ComponentPropertyRenderer")!;
                var projectContext=typeof(EditorApplication).GetField("_projectContext",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(editor);
                var pickerMethod=pickerType.GetMethod("GetAnimationActionNames",BindingFlags.NonPublic|BindingFlags.Static)!;
                foreach(var obj in scene.GameObjects.Where(o=>o.Components.Any(c=>c.GetType().Name=="WeaponDefinition3D")))
                {
                    var definition=obj.Components.Single(c=>c.GetType().Name=="WeaponDefinition3D");
                    var names=(IReadOnlyList<string>)pickerMethod.Invoke(null,[definition,projectContext])!;
                    if(!new[]{"Fire","Reload","Equip"}.All(names.Contains))throw new Exception("Weapon picker missing profile actions");
                    foreach(var field in new[]{"FireAnimation","ReloadAnimation","EmptyReloadAnimation","EquipAnimation"})
                        if(!definition.GetType().GetProperty(field)!.IsDefined(typeof(AnimationActionPickerAttribute),true))throw new Exception("Weapon field is not a picker: "+field);
                    checks.Add("All animation fields use profile action pickers: "+obj.Name);
                }
                var gunlab=player.Components.Single(c=>c.GetType().Name=="FpsGunLab3D");
                for(int slot=0;slot<4;slot++)
                {
                    Tick(scene,new(){KeysDown={ (Key)((int)Key.D1+slot)}});for(int tick=0;tick<45;tick++)Tick(scene,new());
                    if((int)gunlab.GetType().GetProperty("SelectedSlot")!.GetValue(gunlab)! != slot)throw new Exception("Editor slot event failed");
                    var renderer=scene.GameObjects.Single(o=>o.Name==(slot==3?"PROJECTILE PISTOL":Program.Names[slot].ToUpperInvariant())+" view model").GetComponent<SkeletalMeshRenderer>()!;
                    if(!renderer.ModelLoaded||!renderer.HasAnimation("Reload")||!renderer.TryGetCurrentModelBounds(out var bounds)||!bounds.IsValid)throw new Exception("Editor gun skin/animation failed");checks.Add("Editor resolves and selects slot "+slot);
                    Tick(scene,new(){MouseButtonsDown={MouseButton.Left}});for(int tick=0;tick<12;tick++)Tick(scene,new());Tick(scene,new(){KeysDown={Key.R}});
                    if((bool)gunlab.GetType().GetProperty("Reloading")!.GetValue(gunlab)! != true)throw new Exception("Editor reload event failed");for(int tick=0;tick<200;tick++)Tick(scene,new());checks.Add("Editor fires and reloads slot "+slot);
                    if(slot==1) CheckShotgun(scene,gunlab,renderer,checks);
                }
            }
            if(frames==25){typeof(EditorApplication).GetMethod("StopPlayMode",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(editor,null);editor.Close();}
        };
        editor.Run();File.WriteAllText(Program.Root+"/Preview/editor-validation.json",JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("EDITOR PASS "+checks.Count+" checks");
    }
    static void CheckShotgun(Scene scene,object equipment,SkeletalMeshRenderer renderer,List<string> checks)
    {
        object weapon=equipment.GetType().GetProperty("SelectedWeapon")!.GetValue(equipment)!;
        object definition=weapon.GetType().GetProperty("Definition")!.GetValue(weapon)!;
        object? Get(string name)=>weapon.GetType().GetProperty(name)!.GetValue(weapon);
        object? Call(string name,params object[] args)=>weapon.GetType().GetMethod(name)!.Invoke(weapon,args);
        void Set(string name,object value)=>definition.GetType().GetProperty(name)!.SetValue(definition,value);
        bool Fire()=>(bool)equipment.GetType().GetMethod("Fire")!.Invoke(equipment,null)!;
        void Check(bool pass,string label){if(!pass)throw new Exception(label);checks.Add(label);}
        var animation=renderer.GameObject.GetComponent<AnimationController>()!;
        float baseSpeed=animation.PlaybackSpeed;
        Call("SetAmmo",2,20);Call("Reload");
        Check((bool)Get("CanFire")!,"Loaded shotgun can fire during reload");
        Check(Math.Abs(renderer.Speed* .8f-renderer.Duration)<.01f,"Reload animation matches configured shell duration");
        for(int tick=0;tick<10;tick++)Tick(scene,new());
        Check(Fire(),"Fire interrupts partial shell reload");
        Check(!(bool)Get("Reloading")! && (int)Get("CurrentAmmo")! ==1 && (int)Get("ReserveAmmo")! ==20,"Interrupted shell does not grant ammunition");
        Check(Math.Abs(animation.PlaybackSpeed-baseSpeed)<.001f,"Firing restores original animation speed");
        var flash=renderer.GameObject.Children.Single(o=>o.Name=="Weapon muzzle flash").GetComponent<ByteEngine.Core.Vfx.VfxPlayer>()!;
        Check(flash.ActiveParticles>0&&flash.ActiveParticles<=flash.ParticleCapacity,"Native muzzle flash emits bounded particles");
        var muzzleArgs=new object[]{Vector3.Zero};
        Check((bool)weapon.GetType().GetMethod("TryGetMuzzleWorldPosition")!.Invoke(weapon,muzzleArgs)! && Vector3.Distance(flash.Transform.WorldPosition,(Vector3)muzzleArgs[0])<.001f,"Muzzle flash starts at resolved weapon socket");
        Check(!flash.Effect.IsEmpty,"Muzzle flash uses editable native VFX asset");
        for(int tick=0;tick<50;tick++)Tick(scene,new());
        Check(flash.ActiveParticles==0,"Muzzle particles expire after the shot");
        Call("SetAmmo",0,20);Call("Reload");Check(!Fire()&&(bool)Get("Reloading")!,"Empty shotgun cannot cancel reload by firing");
        for(int tick=0;tick<62;tick++)Tick(scene,new());
        Check((int)Get("CurrentAmmo")! ==1&&Fire(),"Completed shell can be fired during following shell reload");
        Check(renderer.GameObject.Children.Count(o=>o.Name=="Weapon muzzle flash")==1,"Repeated shots reuse one muzzle emitter");
        for(int tick=0;tick<50;tick++)Tick(scene,new());
        Call("SetAmmo",2,20);Set("AllowReloadInterrupt",false);Call("Reload");Check(!Fire(),"Reload interruption can be disabled per weapon");Call("CancelReload");Set("AllowReloadInterrupt",true);
        Set("ReloadSpeedMultiplier",2f);Call("Reload");
        for(int tick=0;tick<25;tick++)Tick(scene,new());
        Check((int)Get("CurrentAmmo")! ==3,"Two-times reload speed loads shell in half the time");Call("CancelReload");Set("ReloadSpeedMultiplier",1f);
        Set("ReloadMode",Enum.Parse(definition.GetType().GetProperty("ReloadMode")!.PropertyType,"Magazine"));Call("Reload");Check(!Fire(),"Magazine reload remains protected from interruption");Call("CancelReload");Set("ReloadMode",Enum.Parse(definition.GetType().GetProperty("ReloadMode")!.PropertyType,"PerRound"));
        Call("SetAmmo",6,30);
    }
    static void Tick(Scene scene,RawInputSnapshot raw)
    {
        typeof(Input).GetMethod("UpdatePortable",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[raw,true]);InputActions.Update(raw,true);Input.SetGameViewPointer(new(.5f),new(1280,720),true);typeof(Time).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[1.0/60]);typeof(Scene).GetMethod("UpdateInternal",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(scene,null);
    }
}
