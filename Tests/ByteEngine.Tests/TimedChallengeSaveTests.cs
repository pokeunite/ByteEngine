using ByteEngine.Core;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;

namespace ByteEngine.Tests;
internal static class TimedChallengeSaveTests
{
    public static void Run(string projectRoot)
    {
        using var db=new AssetDatabase(projectRoot,["Assets"]);
        using var assets=new AssetManager(db);
        var binding=AnimationRuntimeAssets.Configure(assets);
        string directory=GameSaveStorage.Register(projectRoot,Guid.NewGuid(),Guid.NewGuid());
        string key="SaveRegression-"+Guid.NewGuid().ToString("N");
        var store=new GameSaveService(projectRoot);
        TimedChallenge3D Clock(out Scene scene)
        {
            scene=new Scene("Record test"){FixedSimulation=true};
            scene.Variables.Set("Started",VariableValue.FromBoolean(false));
            scene.Variables.Set("Completed",VariableValue.FromBoolean(false));
            var clock=scene.CreateGameObject("Challenge").AddComponent(new TimedChallenge3D{RecordKey=key});
            scene.LoadInternal();return clock;
        }
        void Tick(Scene scene,int frames){for(int i=0;i<frames;i++){Time.Update(1d/60);scene.UpdateInternal();}}
        try
        {
            var first=Clock(out var scene);scene.Variables["Started"].Boolean=true;Tick(scene,60);
            scene.Variables["Completed"].Boolean=true;Tick(scene,1);
            if(!first.Finished||first.BestTime<=0||!store.Exists("Records/"+key+".json"))throw new Exception("Challenge does not save inside browser-exported runtime save root");
            double best=first.BestTime;scene.UnloadInternal();
            // Simulate browser restore into a new virtual filesystem before scene startup.
            string data=store.LoadText("Records/"+key+".json");store.Delete("Records/"+key+".json");store.SaveText("Records/"+key+".json",data);
            var second=Clock(out var replay);if(Math.Abs(second.BestTime-best)>.00001)throw new Exception("Saved best time was not restored on a fresh scene");
            replay.Variables["Started"].Boolean=true;Tick(replay,120);replay.Variables["Completed"].Boolean=true;Tick(replay,1);
            if(second.NewRecord||Math.Abs(second.BestTime-best)>.00001)throw new Exception("Slower completion replaces best record");
            replay.UnloadInternal();
            Console.WriteLine("PASS challenge records: project-scoped save, browser save/restore payload, fresh scene reload and slower-time preservation");
        }
        finally{if(store.Exists("Records/"+key+".json"))store.Delete("Records/"+key+".json");GameSaveStorage.Unregister(projectRoot);AnimationRuntimeAssets.Clear(binding);}
    }
}
