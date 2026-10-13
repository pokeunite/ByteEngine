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
                var gunlab=player.Components.Single(c=>c.GetType().Name=="FpsGunLab3D");
                for(int slot=0;slot<4;slot++)
                {
                    Tick(scene,new(){KeysDown={ (Key)((int)Key.D1+slot)}});for(int tick=0;tick<45;tick++)Tick(scene,new());
                    if((int)gunlab.GetType().GetProperty("SelectedSlot")!.GetValue(gunlab)! != slot)throw new Exception("Editor slot event failed");
                    var renderer=scene.GameObjects.Single(o=>o.Name==(slot==3?"PROJECTILE PISTOL":Program.Names[slot].ToUpperInvariant())+" view model").GetComponent<SkeletalMeshRenderer>()!;
                    if(!renderer.ModelLoaded||!renderer.HasAnimation("Reload")||!renderer.TryGetCurrentModelBounds(out var bounds)||!bounds.IsValid)throw new Exception("Editor gun skin/animation failed");checks.Add("Editor resolves and selects slot "+slot);
                    Tick(scene,new(){MouseButtonsDown={MouseButton.Left}});for(int tick=0;tick<12;tick++)Tick(scene,new());Tick(scene,new(){KeysDown={Key.R}});
                    if((bool)gunlab.GetType().GetProperty("Reloading")!.GetValue(gunlab)! != true)throw new Exception("Editor reload event failed");for(int tick=0;tick<200;tick++)Tick(scene,new());checks.Add("Editor fires and reloads slot "+slot);
                }
            }
            if(frames==25){typeof(EditorApplication).GetMethod("StopPlayMode",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(editor,null);editor.Close();}
        };
        editor.Run();File.WriteAllText(Program.Root+"/Preview/editor-validation.json",JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("EDITOR PASS "+checks.Count+" checks");
    }
    static void Tick(Scene scene,RawInputSnapshot raw)
    {
        typeof(Input).GetMethod("UpdatePortable",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[raw,true]);InputActions.Update(raw,true);Input.SetGameViewPointer(new(.5f),new(1280,720),true);typeof(Time).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[1.0/60]);typeof(Scene).GetMethod("UpdateInternal",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(scene,null);
    }
}
