using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using Downhill;
internal sealed class PoseCheck
{
 readonly Scene scene;readonly BoardController3D board;readonly BoardRider3D rider;readonly SkeletalMeshRenderer renderer;
 readonly List<string> results=[];float maxPosition,maxRotation,minKnee=1;Vector3 previousPushFoot;bool hadPushFoot;float maxPushStep,maxHand;
 internal string Label {get;private set;}="Ride";
 internal bool Capture {get;private set;}
 internal PoseCheck(Scene scene){this.scene=scene;board=scene.FindGameObject("Board")!.GetComponent<BoardController3D>()!;rider=scene.FindGameObject("Rider")!.GetComponent<BoardRider3D>()!;renderer=scene.FindGameObject("Rider")!.GetComponent<SkeletalMeshRenderer>()!;scene.FindGameObject("Animation inspection floor")!.AddComponent(new BoxCollider3D());var chains=(System.Collections.ICollection)typeof(SkeletalMeshRenderer).GetField("_effectiveIkChains",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(renderer)!;if(chains.Count!=4)throw new Exception("Expected two legs and two arm IK chains");Console.WriteLine("PASS two leg and two arm IK chains");}
 void Set(string name,object value)=>typeof(BoardController3D).GetProperty(name)!.SetValue(board,value);
 internal bool Step(int frame){
  int phase=Math.Min(13,(frame-1)/150),local=(frame-1)%150;Label=new[]{"Ride","Brake","Heel slide","Toe slide","Tuck","Push","Soft heel","Soft toe","Hard heel","Hard toe","Tuck heel","Tuck toe","Hands down heel","Hands down toe"}[phase];Capture=local is 90 or 120;
  board.BrakeInput=phase==1;board.PushInput=phase is 4 or 5;board.HandDragInput=phase is 12 or 13;board.SlideInput=phase is 2 or 3 or 12 or 13;board.TuckInput=phase is 4 or 10 or 11;board.SteerInput=phase is 2?-.6f:phase is 3?.6f:phase==6?-.4f:phase==7?.4f:phase is 8 or 10 or 12?-1:phase is 9 or 11 or 13?1:0;
  Set("Speed",phase==5?2f:phase==4?4f:8f);Set("IsSliding",phase is 2 or 3 or 12 or 13);Set("SmoothedSteer",board.SteerInput);Set("SlipAngle",phase==2?35f:phase==3?-35f:0f);Set("Grounded",true);Set("PushPhase",local%69/69f);
  if(local>45){if(phase>=12)maxHand=Math.Max(maxHand,rider.HandContactError);maxPosition=Math.Max(maxPosition,rider.FootError);maxRotation=Math.Max(maxRotation,rider.FootRotationError);
   if(phase==5){renderer.TryGetBoneWorldMatrix("mixamorig:RightFoot",out var foot);Matrix4x4.Invert(board.Transform.WorldMatrix,out var invBoard);var ankle=Vector3.Transform(foot.Translation,invBoard);if(hadPushFoot){float step=Vector3.Distance(ankle,previousPushFoot);maxPushStep=Math.Max(maxPushStep,step);if(step>.06f)throw new Exception("Push foot discontinuity: "+step);}previousPushFoot=ankle;hadPushFoot=true;if(Math.Abs(ankle.X)<rider.DeckHalfWidth+.12f&&Math.Abs(ankle.Z)<rider.DeckHalfLength+.15f&&ankle.Y<rider.RightFootAnchor.Y+.012f)throw new Exception("Push shoe intersects deck at phase "+board.PushPhase+" ankle="+ankle);}
   if(board.TuckInput){if(rider.ActivePose!="Tuck")throw new Exception("Push incorrectly overrides tuck");Vector3 B(string name){renderer.TryGetBoneWorldMatrix("mixamorig:"+name,out var m);Matrix4x4.Invert(board.Transform.WorldMatrix,out var inv);return Vector3.Transform(m.Translation,inv);}var hips=B("Hips");var leftKnee=B("LeftLeg");if(Math.Abs(leftKnee.X-B("LeftFoot").X)>.12f)throw new Exception("Tuck left knee splays sideways: "+leftKnee);if(B("LeftFoot").Z>=B("RightFoot").Z||B("Head").Z>=hips.Z||B("LeftHand").Z<hips.Z||B("RightHand").Z<hips.Z)throw new Exception("Tuck must face forward, left foot ahead, both hands behind hips");}
   foreach(string side in phase==5?new[]{"Left"}:new[]{"Left","Right"}){
    Vector3 Bone(string name){renderer.TryGetBoneWorldMatrix("mixamorig:"+side+name,out var m);return m.Translation;}
    var hip=Bone("UpLeg");var knee=Bone("Leg");var foot=Bone("Foot");var toe=Bone("ToeBase");var axis=Vector3.Normalize(foot-hip);var bend=knee-hip;var direction=toe-foot;bend-=axis*Vector3.Dot(bend,axis);direction-=axis*Vector3.Dot(direction,axis);if(bend.LengthSquared()>1e-6f&&direction.LengthSquared()>1e-6f){float dot=Vector3.Dot(Vector3.Normalize(bend),Vector3.Normalize(direction));if(dot<-.2f)Console.WriteLine("BAD KNEE "+Label+" "+side+" "+dot);minKnee=Math.Min(minKnee,dot);}
   }
  }
  if(local==149){if(phase>=12){Console.WriteLine("Glove contact error "+Label+": "+maxHand);if(maxHand>.08f)throw new Exception("Glove cannot reach road");maxHand=0;}if(phase==5)Console.WriteLine("Maximum push foot step per 60 Hz frame: "+maxPushStep);var line=$"{Label}: ankle error {maxPosition:0.0000} m; orientation error {maxRotation:0.00} deg; minimum knee/toe alignment {minKnee:0.00}";Console.WriteLine(line);results.Add(line);if(maxPosition>.025f||maxRotation>2||minKnee<-.2f)throw new Exception("Rider pose constraint regression: "+line);maxPosition=0;maxRotation=0;minKnee=1;}
  if(frame==2100){File.WriteAllLines(Program.Root+"/Preview/Rider pose verification.txt",results);return true;}return false;
 }
}






