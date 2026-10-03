using GoblinScrapper.Construction;
using System.Numerics;
using ByteEngine.Core.Construction;
namespace ByteEngine.Tests;
internal static class VehicleDriftTests
{
    public static void Run()
    {
        static void Check(bool result,string message) { if(!result) throw new InvalidOperationException(message); }
        static VehicleDriveMotion Drive(bool drift,int fps)
        {
            var motion=new VehicleDriveMotion();
            for(int i=0;i<fps;i++) motion.Step(1,0,false,false,1f/fps,16);
            for(int i=0;i<fps/2;i++) motion.Step(1,1,false,drift,1f/fps,16);
            return motion;
        }
        var grip=Drive(false,60); var sliding=Drive(true,60);
        Check(sliding.IsDrifting && sliding.DriftAngle>15,"Handbrake must create substantial sideways slip.");
        Check(sliding.DriftAngle>grip.DriftAngle*3,"Drift and normal steering must differ in actual momentum.");
        float before=sliding.DriftAngle;
        for(int i=0;i<60;i++) sliding.Step(1,0,false,false,1f/60,16);
        Check(!sliding.IsDrifting && sliding.DriftAngle<2 && sliding.DriftAngle<before*.1f,"Releasing handbrake must recover grip.");
        for(int i=0;i<120;i++) sliding.Step(0,0,true,false,1f/60,16);
        Check(sliding.Velocity.Length()<.02f,"Brake must remove both longitudinal and lateral momentum.");
        for(int i=0;i<120;i++) sliding.Step(-1,0,false,true,1f/60,16);
        Check(sliding.ForwardSpeed<0 && !sliding.IsDrifting,"Reverse must work without enabling forward drift assistance.");
        sliding.Reset();Check(sliding.Velocity==Vector3.Zero && sliding.Yaw==0,"Reset must clear all drift state.");
        var lowSpeed=new VehicleDriveMotion();lowSpeed.Step(1,1,false,true,1f/60,16);Check(!lowSpeed.IsDrifting,"Drift cannot activate at standstill.");
        foreach(int fps in new[]{30,60,120})
        {
            var sample=Drive(true,fps);Check(sample.DriftAngle>15 && sample.Velocity.Length()<16.001f,"Frame-rate-dependent drift or speed cap.");
            Check(Math.Abs(sample.DriftAngle-Drive(true,60).DriftAngle)<3,"Drift varies too much across frame rates.");
        }
        var tracks=new VehicleBuildLayout();tracks.Place(19,"scrap_track_pod");tracks.Place(20,"scrap_track_pod");tracks.Place(6,"scrap_engine_block");tracks.Place(7,"scrap_cab_shell");
        Check(tracks.CanDrive,"Two complete track pods plus engine and cab must drive.");
        Check(!tracks.Remove(99),"Invalid mount removal."); tracks.Remove(20);Check(!tracks.CanDrive,"One track cannot drive.");
        var engine=VehicleBuilder3D.PartPlacement(6,"scrap_engine_block");
        Check(Vector3.Distance(engine.Position+new Vector3(0,-.44f,0),VehicleBuildLayout.Mounts[6].Position)<1e-5,"Engine socket alignment.");
        var cab=VehicleBuilder3D.PartPlacement(7,"scrap_cab_shell");
        Check(Vector3.Distance(cab.Position+new Vector3(0,-.53f,0),VehicleBuildLayout.Mounts[7].Position)<1e-5,"Cab socket alignment.");
        Check(VehicleBuildLayout.PartFiles.Length==28 && VehicleBuildLayout.PartFiles.Where(p=>p!="scrap_frame_long").All(p=>VehicleBuildLayout.Mounts.Any(m=>m.Parts.Contains(p))),"Every refined part must have a compatible mount.");
        var roundTrip=VehicleBuildLayout.FromJson("{\"Version\":1,\"Parts\":{\"0\":\"scrap_wheel_large\",\"6\":\"scrap_engine_block\"}}");
        Check(roundTrip.Parts.Count==2 && roundTrip.Parts[0]=="scrap_wheel_large","Existing saves must retain mount IDs.");
        var exclusive=new VehicleBuildLayout();exclusive.Place(19,"scrap_track_pod");
        Check(!exclusive.Place(0,"scrap_wheel_large") && !exclusive.Place(4,"scrap_axle_2m"),"A single track must block wheels and axles.");
        exclusive.Remove(19);exclusive.Place(0,"scrap_wheel_small");Check(!exclusive.Place(20,"scrap_track_pod"),"A wheel must block tracks.");
        exclusive.Remove(0);exclusive.Place(4,"scrap_axle_2m");Check(!exclusive.Place(19,"scrap_track_pod"),"An axle must block tracks.");
        Check(exclusive.SetChassis("scrap_frame_long") && exclusive.ActiveMounts[0].Position.Z<-1.4f,"Long chassis must have its own wheel mount positions.");
        Check(!exclusive.Place(24,"scrap_frame_long"),"Long chassis must never attach as an extension.");
        var savedLong=VehicleBuildLayout.FromJson(exclusive.ToJson());Check(savedLong.LongChassis && savedLong.Parts[4]=="scrap_axle_2m","Long chassis save round trip.");
        var migrated=VehicleBuildLayout.FromJson("{\"Version\":1,\"Parts\":{\"24\":\"scrap_frame_long\",\"0\":\"scrap_wheel_large\"}}");
        Check(migrated.LongChassis && !migrated.Parts.ContainsKey(24),"Legacy long extension must migrate to a base selection.");
        Console.WriteLine($"PASS: planar drift ({Drive(true,60).DriftAngle:0.0} degrees vs grip {grip.DriftAngle:0.0}); release recovery; braking both axes; reverse; reset; 30/60/120 Hz; wheel/track requirements; socket alignment; 27 attachable parts + 2 bases; exclusive running gear; long-base mounts and saves; legacy migration.");
    }
}
