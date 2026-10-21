using System.Numerics;
using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;
using ByteEngine.Editor;

internal static class TopDownChecks
{
    public static void Run(SceneSerializer serializer)
    {
        int count=0;
        void Check(bool ok,string description) { if(!ok)throw new Exception(description); Console.WriteLine("PASS "+description);count++; }
        var updateTime=typeof(Time).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Static)!;
        var updateScene=typeof(Scene).GetMethod("UpdateInternal",BindingFlags.NonPublic|BindingFlags.Instance)!;
        var s=new Scene("Top down runtime test");
        var floor=s.CreateGameObject("Floor");floor.Transform.WorldPosition=new(0,-.5f,0);floor.AddComponent(new BoxCollider3D{Size=new(100,1,100)});
        var wall=s.CreateGameObject("Wall");wall.Transform.WorldPosition=new(3,2,0);wall.AddComponent(new BoxCollider3D{Size=new(1,4,20)});
        var player=s.CreateGameObject("Player");player.Transform.WorldPosition=new(0,.1f,0);
        player.AddComponent(new CapsuleCollider3D());var motor=player.AddComponent(new CharacterController3D{MoveSpeed=4,Acceleration=40,Deceleration=40});
        var control=player.AddComponent(new TopDownController3D{CameraRelativeMovement=false,TurnSharpness=100});
        var cameraObject=s.CreateGameObject("Camera");var camera=cameraObject.AddComponent(new Camera3D{ActiveGameCamera=true});
        var rig=cameraObject.AddComponent(new TopDownCamera3D{TargetId=player.Id,CursorLookAhead=false,FollowSharpness=0});control.CameraObjectId=cameraObject.Id;
        var manager=new SceneManager();manager.LoadScene(s);InputActions.Configure(InputMap.CreateDefault());
        var input=new RawInputSnapshot();
        void Tick(bool enabled=true,Vector2? pointer=null,float dt=1f/60)
        {
            updateTime.Invoke(null,[ (double)dt ]);
            Input.SetGameViewPointer(pointer??new(.5f,.5f),new(1280,720),true);
            InputActions.Update(input,enabled);updateScene.Invoke(s,null);
        }
        Tick();
        Check(Vector3.Dot(camera.Transform.Forward,Vector3.Normalize(player.Transform.WorldPosition+Vector3.UnitY*rig.TargetHeight-camera.Transform.WorldPosition))>.999f,"Camera points at player with correct downward pitch");
        Check(camera.Orthographic && Input.KeepGameViewPointerFree,"Top-down view uses orthographic projection and free cursor");
        Check(TopDownController3D.CalculateMovement(Vector2.One,-Vector3.UnitZ).Length()<1.0001f,"Diagonal movement is normalized");
        Check(Vector3.Distance(TopDownController3D.CalculateMovement(Vector2.UnitY,-Vector3.UnitX),-Vector3.UnitX)<.001f,"Movement respects a rotated camera");
        Check(TopDownController3D.CalculateMovement(new(.5f,0),-Vector3.UnitZ).Length()==.5f,"Analog movement preserves partial stick input");
        input.KeysDown.Add(Key.D);for(int i=0;i<90;i++)Tick();
        Check(player.Transform.WorldPosition.X>1 && player.Transform.WorldPosition.X<2.5f,"Native motor moves player and prevents crossing wall");
        Check(motor.IsGrounded,"Top-down player stays grounded on native collider");
        input.Clear();for(int i=0;i<30;i++)Tick();var stopped=player.Transform.WorldPosition;
        input.KeysDown.Add(Key.W);for(int i=0;i<30;i++)Tick(false);
        Check(Vector3.Distance(stopped,player.Transform.WorldPosition)<.02f,"Blocked gameplay input stops movement");
        input.Clear();control.InputEnabled=false;input.KeysDown.Add(Key.W);for(int i=0;i<30;i++)Tick();
        Check(Vector3.Distance(stopped,player.Transform.WorldPosition)<.02f,"Controller input switch stops movement independently");
        control.InputEnabled=true;input.Clear();
        foreach(var pointer in new[]{new Vector2(.2f,.25f),new Vector2(.8f,.25f),new Vector2(.2f,.75f),new Vector2(.8f,.75f)})
        {for(int i=0;i<10;i++)Tick(true,pointer);var toward=control.AimWorldPoint-player.Transform.WorldPosition;toward.Y=0;
            Check(control.HasCursorAim && Vector3.Dot(player.Transform.Forward,Vector3.Normalize(toward))>.995f,"Cursor facing is correct in quadrant "+pointer);}
        Check(TopDownController3D.TryCursorPoint(camera,new(.5f,.5f),new(1280,720),player.Transform.WorldPosition.Y+rig.TargetHeight,out var center) && Vector3.Distance(center,player.Transform.WorldPosition+Vector3.UnitY*rig.TargetHeight)<.01f,"Orthographic screen ray hits expected player-height plane");
        camera.Orthographic=false;
        Check(TopDownController3D.TryCursorPoint(camera,new(.5f,.5f),new(1280,720),player.Transform.WorldPosition.Y+rig.TargetHeight,out center),"Perspective cursor ray also intersects aim plane");
        Check(TopDownCamera3D.CalculateLookAhead(new(.5f,.5f),new(1280,720),45,.1f,.3f,4)==Vector3.Zero,"Look-ahead has a center dead zone");
        var ahead=TopDownCamera3D.CalculateLookAhead(new(1,0),new(1280,720),45,.1f,.3f,4);
        Check(ahead.Length()<=4.0001f,"Look-ahead stays within configured maximum distance");
        var screenRight=TopDownCamera3D.CalculateLookAhead(new(1,.5f),new(1280,720),45,.1f,.3f,4);
        Check(Vector3.Dot(screenRight, camera.Transform.Right)>3.9f,"Look-ahead rotates into camera space");
        rig.SetIsometricView();rig.SnapToTarget();Check(camera.Orthographic&&Math.Abs(rig.Pitch-35.26439f)<.001f,"Isometric preset uses true isometric pitch and projection");
        rig.UseBounds=true;rig.BoundsMinimum=new(-1,-1);rig.BoundsMaximum=new(1,1);player.Transform.WorldPosition=new(20,0,20);rig.SnapToTarget();
        var orbit=ThirdPersonCamera3D.CalculateOrbitVector(rig.Yaw,rig.Pitch,rig.Distance);
        var pivot=camera.Transform.WorldPosition-orbit;Check(Math.Abs(pivot.X-1)<.01f&&Math.Abs(pivot.Z-1)<.01f,"Camera focus clamps to world bounds");
        control.MoveAction=new InputActionReference(Guid.NewGuid(),"CustomMove");control.JumpAction=new InputActionReference(Guid.NewGuid(),"CustomJump");
        var restored=serializer.Deserialize(serializer.Serialize(s));var restoredControl=restored.FindGameObject("Player")!.GetComponent<TopDownController3D>()!;
        var restoredRig=restored.FindGameObject("Camera")!.GetComponent<TopDownCamera3D>()!;
        Check(restoredControl.MoveAction.Id==control.MoveAction.Id&&restoredControl.MoveAction.Name==control.MoveAction.Name&&restoredControl.JumpAction.Id==control.JumpAction.Id&&restoredControl.JumpAction.Name==control.JumpAction.Name,"Custom input action IDs and names survive save/load");
        Check(restoredControl.CameraObjectId==cameraObject.Id&&restoredRig.TargetId==player.Id&&restoredRig.UseBounds,"Controller/camera links and framing survive save/load");
        Check(ComponentMetadataRegistry.RegisteredTypes.Contains(typeof(TopDownController3D))&&ComponentMetadataRegistry.RegisteredTypes.Contains(typeof(TopDownCamera3D)),"Both components are discoverable in editor and assistant catalogues");
        var registry=VisualLogicRegistry.CreateDefault();
        var context=new EventExecutionContext{Scene=s,Self=player,Globals=new VariableStore()};
        registry.TryGetAction("topDown.setInput",out var action);var instruction=new VisualInstruction{Id="topDown.setInput"};instruction.Arguments["enabled"]=new EventValue{Constant=VariableValue.FromBoolean(false)};action!.Execute(instruction,context);
        Check(!control.InputEnabled,"Event-sheet action controls top-down input");
        var cameraContext=new EventExecutionContext{Scene=s,Self=cameraObject,Globals=new VariableStore()};
        registry.TryGetAction("topDownCamera.setZoom",out action);instruction=new(){Id="topDownCamera.setZoom"};instruction.Arguments["zoom"]=new(){Constant=VariableValue.FromNumber(30)};action!.Execute(instruction,cameraContext);
        Check(rig.OrthographicWidth==30,"Event-sheet action controls camera zoom");
        for(int i=0;i<300;i++)Tick();Check(float.IsFinite(camera.Transform.WorldPosition.LengthSquared()),"Camera remains finite over sustained runtime updates");
        manager.UnloadScene();Console.WriteLine(count+" top-down checks passed.");
    }
}
