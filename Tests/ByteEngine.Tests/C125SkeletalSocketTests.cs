using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Diagnostics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;

namespace ByteEngine.Tests;

internal static class C125SkeletalSocketTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        string root=Path.Combine(Path.GetTempPath(),"ByteEngine-c125-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root,"Assets")); Directory.CreateDirectory(Path.Combine(root,"Scenes"));
        try
        {
            Guid modelGuid=Guid.NewGuid();
            SkeletalSocketDefinition source=new(){Id=Guid.NewGuid(),Name="Weapon_R",BoneName="RightHand",PositionOffset=new(.25f,.5f,.75f),RotationOffsetDegrees=new(0,90,0),Scale=new(1,2,1),InheritBoneScale=false};
            ModelSocketMetadataStore.Save(root,modelGuid,new[]{source});
            SkeletalSocketDefinition loaded=ModelSocketMetadataStore.Load(root,modelGuid).Single();
            Assert(loaded.Id==source.Id&&loaded.Name==source.Name&&loaded.BoneName==source.BoneName,"socket identity/name/bone persistence");
            Assert(Near(loaded.PositionOffset,source.PositionOffset)&&Near(loaded.RotationOffsetDegrees,source.RotationOffsetDegrees)&&Near(loaded.Scale,source.Scale),"socket transform persistence");
            bool duplicateRejected=false; try{ModelSocketMetadataStore.Save(root,modelGuid,new[]{source,source.Clone(false)});}catch(InvalidDataException){duplicateRejected=true;} Assert(duplicateRejected,"case-insensitive duplicate socket names rejected");

            ModelAsset reimported=CreateModel(modelGuid,source); reimported.ReplaceSockets(Array.Empty<SkeletalSocketDefinition>()); ModelSocketMetadataStore.MergeInto(root,reimported); Assert(reimported.FindSocket("Weapon_R")?.Id==source.Id,"model reload/reimport retains metadata");

            SkeletalSocketDefinition renamed=source.Clone(); Guid stableId=renamed.Id; renamed.Name="Weapon_Main"; Assert(renamed.Id==stableId,"rename preserves socket Guid");
            SkeletalSocketDefinition duplicate=renamed.Clone(false); Assert(duplicate.Id!=renamed.Id,"duplicate creates new Guid");
            Vector3 reassignPosition=renamed.PositionOffset; renamed.BoneName="Spine"; Assert(Near(renamed.PositionOffset,reassignPosition),"reassign bone preserves socket transform");
            SkeletalSocketDefinition back=source.Clone(false); back.Name="Back"; back.PositionOffset=new(0,0,-.5f);
            ModelAsset model=CreateModel(modelGuid,source); model.ReplaceSockets(new[]{source,back});
            Scene scene=new("C12.5"); GameObject parent=scene.CreateGameObject("Player"); SkeletalMeshRenderer renderer=parent.AddComponent(new SkeletalMeshRenderer()); BindRenderer(renderer,model,Matrix4x4.CreateScale(-2f,.01f,3f)*Matrix4x4.CreateTranslation(1,2,3));
            GameObject child=scene.CreateGameObject("Rifle");
            Assert(SkeletalSocketResolver.TryGetSocketWorldTransform(parent,"Weapon_R",out SkeletalSocketTransform socket),"socket resolver finds final pose");
            Assert(socket.Scale.X>0&&socket.Scale.Y>0&&socket.Scale.Z>0,"stable positive socket scale");
            Assert(SkeletalAttachmentService.AttachToSocket(child,parent,"Weapon_R",AttachmentTransformRule.SnapToTarget,AttachmentTransformRule.SnapToTarget,AttachmentTransformRule.SnapToTarget),"valid socket attach");
            Assert(child.IsAttached&&Near(child.Transform.WorldPosition,socket.Position)&&Near(child.Transform.WorldScale,socket.Scale),"snap/snap/snap semantics");
            Assert(SkeletalAttachmentService.HasCachedBinding(child),"runtime binding cached after attach");

            GameObject defaultChild=scene.CreateGameObject("Default Rifle");
            Assert(SkeletalAttachmentService.AttachToSocket(defaultChild,parent,"Weapon_R"),"old attachment overload remains valid");
            Assert(SkeletalSocketResolver.TryGetSocketWorldTransform(parent,"Weapon_R",out SkeletalSocketTransform defaultSocket)&&Near(defaultChild.Transform.WorldPosition,defaultSocket.Position),"zero-offset defaults preserve existing behavior");

            GameObject offsetChild=scene.CreateGameObject("Offset Rifle");
            Vector3 positionOffset=new(.1f,-.2f,.3f), rotationOffsetDegrees=new(30,45,-15), scaleMultiplier=new(.5f,.25f,2f);
            Assert(SkeletalAttachmentService.AttachToSocket(offsetChild,parent,"Weapon_R",AttachmentTransformRule.SnapToTarget,AttachmentTransformRule.SnapToTarget,AttachmentTransformRule.SnapToTarget,positionOffset,rotationOffsetDegrees,scaleMultiplier),"offset attachment succeeds");
            Assert(SkeletalSocketResolver.TryGetSocketWorldTransform(parent,"Weapon_R",out SkeletalSocketTransform offsetSocket),"offset socket resolves");
            Vector3 expectedOffsetPosition=offsetSocket.Position+Vector3.Transform(positionOffset*offsetSocket.Scale,offsetSocket.Rotation);
            Quaternion expectedOffsetRotation=Quaternion.Normalize(offsetSocket.Rotation*Quaternion.CreateFromYawPitchRoll(Radians(rotationOffsetDegrees.Y),Radians(rotationOffsetDegrees.X),Radians(rotationOffsetDegrees.Z)));
            Assert(Near(offsetChild.Transform.WorldPosition,expectedOffsetPosition),"position offset applies in socket-local space");
            Assert(Near(WorldRotation(offsetChild),expectedOffsetRotation),"degree rotation offset uses Transform Euler convention");
            Assert(Near(offsetChild.Transform.WorldScale,offsetSocket.Scale*scaleMultiplier),"scale multiplier applies after rule scale");
            SkeletalAttachmentService.Apply(offsetChild);
            Assert(Near(offsetChild.Transform.WorldPosition,expectedOffsetPosition)&&Near(WorldRotation(offsetChild),expectedOffsetRotation)&&Near(offsetChild.Transform.WorldScale,offsetSocket.Scale*scaleMultiplier),"offsets survive repeated Apply calls");
            BindPose(renderer,Matrix4x4.CreateFromYawPitchRoll(.35f,-.2f,.1f)*Matrix4x4.CreateTranslation(4,5,6));
            Assert(SkeletalAttachmentService.Apply(offsetChild),"animated attachment reapplies");
            Assert(SkeletalSocketResolver.TryGetSocketWorldTransform(parent,"Weapon_R",out SkeletalSocketTransform movedSocket),"animated parent socket continues resolving");
            Assert(Near(offsetChild.Transform.WorldPosition,movedSocket.Position+Vector3.Transform(positionOffset*movedSocket.Scale,movedSocket.Rotation)),"animated socket retains position offset");
            Assert(Near(WorldRotation(offsetChild),Quaternion.Normalize(movedSocket.Rotation*Quaternion.CreateFromYawPitchRoll(Radians(rotationOffsetDegrees.Y),Radians(rotationOffsetDegrees.X),Radians(rotationOffsetDegrees.Z)))),"animated socket retains rotation offset");
            parent.Transform.EulerAngles = new Vector3(15, 40, -20);
            parent.Transform.LocalScale = new Vector3(.5f, 1.5f, 2f);
            Assert(SkeletalAttachmentService.Apply(offsetChild),"attachment updates beneath rotated, non-uniformly scaled character");
            Assert(SkeletalSocketResolver.TryGetSocketWorldTransform(parent,"Weapon_R",out SkeletalSocketTransform rotatedSocket),"rotated socket resolves");
            Assert(Near(WorldRotation(offsetChild),Quaternion.Normalize(rotatedSocket.Rotation*Quaternion.CreateFromYawPitchRoll(Radians(rotationOffsetDegrees.Y),Radians(rotationOffsetDegrees.X),Radians(rotationOffsetDegrees.Z)))),"non-uniform socket scale does not alter authored weapon rotation");
            SkeletalSocketDefinition gun=source.Clone(false); gun.Name="Gun"; gun.Scale=new(2.3054833f,1.9900635f,3.2402015f); gun.RotationOffsetDegrees=new(71.96697f,37.576756f,31.00968f);
            model.ReplaceSockets(new[]{source,back,gun});
            GameObject gunChild=scene.CreateGameObject("Scaled Gun");
            Vector3 gunOffset=new(25,-40,35);
            Assert(SkeletalAttachmentService.AttachToSocket(gunChild,parent,"Gun",AttachmentTransformRule.SnapToTarget,AttachmentTransformRule.SnapToTarget,AttachmentTransformRule.SnapToTarget,Vector3.Zero,gunOffset,Vector3.One),"scaled, rotated gun attaches without matrix decomposition failure");
            Assert(SkeletalSocketResolver.TryGetSocketWorldTransform(parent,"Gun",out SkeletalSocketTransform gunSocket),"scaled gun socket resolves");
            Quaternion gunOffsetRotation=Quaternion.CreateFromYawPitchRoll(Radians(gunOffset.Y),Radians(gunOffset.X),Radians(gunOffset.Z));
            Assert(Near(WorldRotation(gunChild),Quaternion.Normalize(gunSocket.Rotation*gunOffsetRotation)),"scaled gun rotation matches socket plus Event Sheet offset");
            GameObject missingChild=scene.CreateGameObject("Missing Socket Rifle");
            Assert(!SkeletalAttachmentService.AttachToSocket(missingChild,parent,"MissingSocket",AttachmentTransformRule.SnapToTarget,AttachmentTransformRule.SnapToTarget,AttachmentTransformRule.KeepRelative,Vector3.One,Vector3.One,Vector3.One)&&!missingChild.IsAttached,"missing socket returns false without attaching");

            Assert(SkeletalAttachmentService.AttachToSocket(child,parent,"Back"),"socket switch succeeds"); SkeletalAttachmentService.Apply(child);
            Assert(child.ParentSocket=="Back"&&SkeletalAttachmentService.HasCachedBinding(child),"socket switch invalidates and rebuilds binding");
            GameObject secondParent=scene.CreateGameObject("Second Player"); SkeletalMeshRenderer secondRenderer=secondParent.AddComponent(new SkeletalMeshRenderer()); BindRenderer(secondRenderer,model,Matrix4x4.CreateTranslation(10,0,0));
            child.SetParent(secondParent,true); SkeletalAttachmentService.Apply(child); Assert(ReferenceEquals(child.Parent,secondParent)&&SkeletalAttachmentService.HasCachedBinding(child),"parent change invalidates and rebuilds binding");
            SkeletalAttachmentService.AttachToSocket(child,parent,"Weapon_R");
            child.AttachmentPosition=new(1,0,0); SkeletalAttachmentService.Apply(child); Vector3 first=child.Transform.WorldPosition;
            BindPose(renderer,Matrix4x4.CreateTranslation(5,2,3)); SkeletalAttachmentService.Apply(child); Assert(!Near(first,child.Transform.WorldPosition),"animated bone movement updates child");
            Quaternion rotationBefore=WorldRotation(child);
            Assert(SkeletalAttachmentService.AttachToSocket(child,parent,"Weapon_R",AttachmentTransformRule.KeepWorld,AttachmentTransformRule.KeepWorld,AttachmentTransformRule.KeepWorld),"keep-world attach"); Vector3 before=child.Transform.WorldPosition; Assert(Near(before,child.Transform.WorldPosition),"keep-world attach preserves position");
            Assert(Near(rotationBefore,WorldRotation(child)),"keep-world attach preserves rotation beneath rotated character");
            Assert(SkeletalAttachmentService.Detach(child,true)&&Near(before,child.Transform.WorldPosition)&&!child.IsAttached,"detach keep-world semantics");
            Assert(!SkeletalAttachmentService.HasCachedBinding(child),"detach clears cached binding");

            child.SetParent(parent,false); child.ParentSocket="Weapon_R"; child.AttachmentLocationRule=AttachmentTransformRule.SnapToTarget; child.AttachmentRotationRule=AttachmentTransformRule.SnapToTarget; child.AttachmentScaleRule=AttachmentTransformRule.KeepRelative; child.AttachmentPosition=new(.1f,.2f,.3f);
            using AssetDatabase database=new(root,new[]{"Assets","Scenes"}); using AssetManager assets=new(database); ComponentSerializer components=new(root,database,assets); SceneSerializer serializer=new(components); Scene clone=serializer.CloneForRuntime(scene); GameObject cloned=clone.FindGameObject("Rifle")!;
            Assert(cloned.ParentSocket=="Weapon_R"&&cloned.AttachmentLocationRule==AttachmentTransformRule.SnapToTarget&&Near(cloned.AttachmentPosition,child.AttachmentPosition),"Blueprint/scene attachment persistence");

            var registry=ByteEngine.Core.VisualLogic.VisualLogicRegistry.CreateDefault();
            Assert(registry.TryGetAction("attachment.attachToSocket",out ByteEngine.Core.VisualLogic.VisualActionDefinition? attachAction)&&attachAction!=null&&registry.TryGetAction("attachment.detach",out _)&&registry.TryGetCondition("attachment.isAttached",out _)&&registry.TryGetCondition("attachment.isAttachedToSocket",out _),"Attachment Visual Logic registration");
            GameObject actionChild=scene.CreateGameObject("Event Rifle");
            var legacyInstruction=new ByteEngine.Core.VisualLogic.VisualInstruction{Id="attachment.attachToSocket"};
            legacyInstruction.Arguments["object"]=ByteEngine.Core.VisualLogic.EventValue.String("id:"+actionChild.Id);
            legacyInstruction.Arguments["parent"]=ByteEngine.Core.VisualLogic.EventValue.String("id:"+parent.Id);
            legacyInstruction.Arguments["socket"]=ByteEngine.Core.VisualLogic.EventValue.String("Weapon_R");
            var eventContext=new ByteEngine.Core.VisualLogic.EventExecutionContext{Globals=new ByteEngine.Core.Variables.VariableStore(),Scene=scene,Self=actionChild};
            attachAction!.Execute(legacyInstruction,eventContext);
            Assert(actionChild.IsAttached&&Near(actionChild.AttachmentPosition,Vector3.Zero)&&Near(actionChild.AttachmentRotation,Quaternion.Identity)&&Near(actionChild.AttachmentScale,Vector3.One),"missing new Event Sheet arguments use safe defaults");
            Assert(SkeletalAttachmentService.IsAttachedToSocket(actionChild,parent,"Weapon_R"),"IsAttachedToSocket remains valid");
            Vector3 actionRotationOffset=new(25,-40,35);
            legacyInstruction.Arguments["rotationOffset"]=ByteEngine.Core.VisualLogic.EventValue.Vector3(actionRotationOffset);
            attachAction.Execute(legacyInstruction,eventContext);
            Assert(SkeletalSocketResolver.TryGetSocketWorldTransform(parent,"Weapon_R",out SkeletalSocketTransform actionSocket),"Event Sheet socket resolves");
            Quaternion actionOffset=Quaternion.CreateFromYawPitchRoll(Radians(actionRotationOffset.Y),Radians(actionRotationOffset.X),Radians(actionRotationOffset.Z));
            Assert(Near(WorldRotation(actionChild),Quaternion.Normalize(actionSocket.Rotation*actionOffset)),"Event Sheet rotation offset composes with socket rotation under non-uniform scale");
            string socketDump=RuntimeDiagnostics.CreateDump(scene);
            Assert(socketDump.Contains("Socket Attachment: Event Rifle",StringComparison.Ordinal)&&
                socketDump.Contains("Imported Payload Rotation",StringComparison.Ordinal)&&
                socketDump.Contains("Socket World Rotation",StringComparison.Ordinal),
                "Runtime diagnostics include the live socket and attached object rotations");
            Assert(SkeletalAttachmentService.Detach(actionChild,true)&&!actionChild.IsAttached,"Event attachment still detaches");
            Console.WriteLine("C12.5 skeletal socket regressions passed.");
        }
        finally { try{Directory.Delete(root,true);}catch{} }
    }

    private static ModelAsset CreateModel(Guid guid,SkeletalSocketDefinition socket)
    {
        ImportedModel imported=new(){Guid=guid,SourceAssetGuid=guid,Name="SocketModel",Nodes=new(){new ImportedNode{Key="RightHand",Name="RightHand"}},Skeleton=new SkeletonAsset{Key="Skeleton",Bones=new(){new Bone{Name="RightHand",ParentIndex=-1,BindPose=Matrix4x4.Identity}}}};
        ModelAsset model=new(imported); model.ReplaceSockets(new[]{socket}); return model;
    }
    private static void BindRenderer(SkeletalMeshRenderer renderer,ModelAsset model,Matrix4x4 pose)
    {
        Set(renderer,"_model",model); Set(renderer,"_skeleton",model.Skeleton); Set(renderer,"_nodes",model.Nodes.ToArray()); Set(renderer,"_boneNodeIndices",new[]{0}); Set(renderer,"_currentPoseGlobals",new[]{pose}); Set(renderer,"_resolved",true); Set(renderer,"_poseDirty",false);
    }
    private static void BindPose(SkeletalMeshRenderer renderer,Matrix4x4 pose)=>Set(renderer,"_currentPoseGlobals",new[]{pose});
    private static void Set(object target,string field,object? value)=>target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(target,value);
    private static bool Near(Vector3 a,Vector3 b)=>Vector3.Distance(a,b)<.001f;
    private static Quaternion WorldRotation(GameObject gameObject)
    {
        Quaternion rotation=gameObject.Transform.LocalRotation;
        for(GameObject? parent=gameObject.Parent;parent!=null;parent=parent.Parent)
            rotation=Quaternion.Normalize(parent.Transform.LocalRotation*rotation);
        return rotation;
    }
    private static bool Near(Quaternion a,Quaternion b)=>MathF.Abs(Quaternion.Dot(a,b))>.999f;
    private static float Radians(float value)=>value*MathF.PI/180f;
    private static void Assert(bool condition,string name){if(!condition)throw new InvalidOperationException("C12.5: "+name);}
}