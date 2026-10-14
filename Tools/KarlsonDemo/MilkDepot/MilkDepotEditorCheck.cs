using System.Numerics;
using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.Characters;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Scene;
using ByteEngine.Editor;
namespace KarlsonDemoTool;
internal static class MilkDepotEditorCheck
{
    internal static void Run()
    {
        using var editor=new EditorApplication(Program.Root+"/karlson demo.byteproject");int frames=0;
        editor.Load+=()=>editor.IsVisible=false;
        editor.UpdateFrame+=_=>
        {
            frames++;
            if(frames==2)
            {
                typeof(EditorApplication).GetMethod("EnterPlayMode",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(editor,null);
                var scene=editor.Scenes.ActiveScene!;
                if(!scene.Name.StartsWith("Milk Depot"))throw new Exception("Wrong startup scene");
                if(scene.GameObjects.SelectMany(o=>o.Components).Any(c=>c.GetType().Name=="MissingComponent"))throw new Exception("Packaged course component missing");
                for(int tick=0;tick<90;tick++)
                {
                    var raw=new RawInputSnapshot();typeof(Input).GetMethod("UpdatePortable",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[raw,true]);InputActions.Update(raw,true);
                    Input.SetGameViewPointer(new(.5f),new(1280,720),true);typeof(Time).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[1.0/60]);typeof(Scene).GetMethod("UpdateInternal",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(scene,null);
                }
                var player=scene.GameObjects.Single(o=>o.Name=="FPS Character");
                if(!player.GetComponent<CharacterController3D>()!.IsGrounded)throw new Exception("Packaged player not grounded");
                if(scene.GameObjects.Count(o=>o.Components.Any(c=>c.GetType().Name=="WeaponAmmoPickup3D"))!=6)throw new Exception("Packaged pickups missing");
                var course=scene.GameObjects.SelectMany(o=>o.Components).Single(c=>c.GetType().Name=="MilkDepotCourse3D");
                if((int)course.GetType().GetProperty("RemainingTargets")!.GetValue(course)!=8)throw new Exception("Packaged target count wrong");
                Console.WriteLine("MILK EDITOR PASS: startup, package components, ground, six pickups and eight targets");
            }
            if(frames==5){typeof(EditorApplication).GetMethod("StopPlayMode",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(editor,null);editor.Close();}
        };
        editor.Run();
    }
}
