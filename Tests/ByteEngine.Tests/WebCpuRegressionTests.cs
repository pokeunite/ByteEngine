using System.Numerics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Gameplay.Hydraulics;
using ByteEngine.Core.Variables;

namespace ByteEngine.Tests;
internal static class WebCpuRegressionTests
{
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
    public static void Run()
    {
        var scene=new Scene("Lookup mutation");var a=scene.CreateGameObject("duplicate");var b=scene.CreateGameObject("duplicate");
        Check(scene.FindGameObject("duplicate")==a&&scene.FindGameObject(a.Id)==a,"Indexed lookup preserves first duplicate and ID semantics");
        b.MoveBefore(a);Check(scene.FindGameObject("duplicate")==b,"Reordering invalidates duplicate-name lookup");
        b.Name="renamed";Check(scene.FindGameObject("duplicate")==a&&scene.FindGameObject("renamed")==b,"Rename invalidates positive and negative lookup");
        Check(scene.FindGameObject("later")==null,"Missing name remains missing");var c=scene.CreateGameObject("later");Check(scene.FindGameObject("later")==c,"New object replaces a cached miss");
        scene.DestroyGameObject(b);Check(scene.FindGameObject("renamed")==null&&scene.FindGameObject(b.Id)==null,"Destroyed objects leave both indexes");
        var context=new VariableResolutionContext{Scene=scene,Globals=new(),Self=a};var reference=new VariableReference{Scope=VariableScope.Component,ObjectName="duplicate",ComponentType="Transform",MemberName="LocalPosition.X"};
        Check(VariableResolver.TrySet(reference,context,4f)&&VariableResolver.TryGet(reference,context,out var value)&&Convert.ToSingle(value)==4,"Cached nested component member paths still update value-type parents");
        reference.MemberName="DoesNotExist";Check(!VariableResolver.TryGet(reference,context,out _)&&!VariableResolver.TryGet(reference,context,out _),"Cached missing reflected members fail cleanly");
        float largest=0;
        foreach(var drift in new[]{Vector2.Zero,new Vector2(.8f,0),new Vector2(-.6f,.45f)}){
            var fast=new ShallowWaterGrid(48,32,7,4);var original=new ShallowWaterGridReference(48,32,7,4);fast.Seed(.012f);original.Seed(.012f);
            for(int tick=0;tick<360;tick++){
                float t=tick/120f,amount=MathF.Sin(t*2.2f)*.00006f;
                foreach(var p in new[]{new Vector2(1.89f,1.28f),new Vector2(5.11f,2.72f),new Vector2(3+t*.12f,2)}){fast.Impulse(p.X,p.Y,.7f,amount);original.Impulse(p.X,p.Y,.7f,amount);}
                float depth=.02f+1.4f*(.5f+.5f*MathF.Sin(t*1.3f));fast.Step(1/120f,depth,drift,.45f,.12f);original.Step(1/120f,depth,drift,.45f,.12f);
                if(tick%20==0)for(int z=0;z<17;z++)for(int x=0;x<19;x++)largest=Math.Max(largest,Math.Abs(fast.Sample(x*7/18f,z*4/16f)-original.Sample(x*7/18f,z*4/16f)));
                if(Math.Abs(fast.MeanOffset)>1e-6f||fast.MaximumDisplacement>.12001f)throw new Exception("Water volume or amplitude limit changed");
            }
            fast.Clear();Check(fast.Energy==0&&fast.MaximumDisplacement==0,"Clearing water works after buffer swapping / drift "+drift);
        }
        Check(largest<.0001f,"Optimized water solver matches original reference within 0.1 mm (maximum "+largest+" m)");
    }
}
