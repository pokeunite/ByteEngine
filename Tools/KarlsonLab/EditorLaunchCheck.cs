using System.Reflection;
using ByteEngine.Editor;
namespace KarlsonLab;
internal static class EditorLaunchCheck
{
    public static void Run()
    {
        using var editor=new EditorApplication(Program.ProjectFile);
        int frames=0;object? courseBeforeRestart=null;
        editor.Load+=()=>editor.IsVisible=false;
        editor.UpdateFrame+=_=>
        {
            frames++;
            if(frames==2)
            {
                typeof(EditorApplication).GetMethod("EnterPlayMode",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(editor,null);
                var active=editor.Scenes.ActiveScene??throw new Exception("Editor Play did not create a runtime scene.");
                if(active.GameObjects.SelectMany(o=>o.Components).Any(c=>c.GetType().Name=="MissingComponent"))throw new Exception("Editor Play contains missing components.");
                Console.WriteLine("EDITOR PLAY PASS: "+active.Name+"; core="+typeof(ByteEngine.Core.Gameplay.ResourceMeter3D).Assembly.Location);
                var director=active.GameObjects.SelectMany(o=>o.Components).Single(c=>c.GetType().Name=="KarlsonDirector3D");
                director.GetType().GetMethod("Begin")!.Invoke(director,null);
                var player=active.GameObjects.Single(o=>o.GetComponent<ByteEngine.Core.Characters.CharacterController3D>()!=null&&o.GetComponent<ByteEngine.Core.Gameplay.ResourceMeter3D>()!=null);
                Console.WriteLine($"EDITOR SPAWN position={player.Transform.WorldPosition} health={player.GetComponent<ByteEngine.Core.Gameplay.HealthComponent>()!.CurrentHealth}");
                for(int tick=0;tick<120;tick++)TestInput.Frame(active);
                Console.WriteLine($"EDITOR AFTER TWO SECONDS position={player.Transform.WorldPosition} health={player.GetComponent<ByteEngine.Core.Gameplay.HealthComponent>()!.CurrentHealth}");
                if(player.Transform.WorldPosition.Y<-.1f||player.GetComponent<ByteEngine.Core.Gameplay.HealthComponent>()!.IsDead)throw new Exception("Editor spawn does not survive on the course floor.");
                TestInput.Clear();
            }
            if(frames==10)editor.Scenes.ActiveScene!.RequestLoad("Scenes/Course01.bytescene");
            if(frames==22){var course=editor.Scenes.ActiveScene!;if(!course.Name.Contains("ORIENTATION")||course.GameObjects.SelectMany(o=>o.Components).Any(c=>c.GetType().Name=="MissingComponent"))throw new Exception("Editor course load failed.");courseBeforeRestart=course;Console.WriteLine("EDITOR COURSE LOAD PASS");}
            if(frames==24)editor.Scenes.ActiveScene!.RequestRestart();
            if(frames==34){if(ReferenceEquals(courseBeforeRestart,editor.Scenes.ActiveScene))throw new Exception("Editor course restart failed.");Console.WriteLine("EDITOR COURSE RESTART PASS");}
            if(frames==35){typeof(EditorApplication).GetMethod("StopPlayMode",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(editor,null);editor.Close();}
        };
        editor.Run();
        Console.WriteLine("EDITOR START/PLAY/RENDER/STOP PASS");
    }
}
