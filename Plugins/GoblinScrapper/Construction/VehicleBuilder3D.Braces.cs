using ByteEngine.Core.Scene;
using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Construction;
namespace GoblinScrapper.Construction;
public sealed partial class VehicleBuilder3D
{
 private BraceEndpoint? _braceStart;
 private int _pendingMountTurns;
 private readonly Dictionary<int,GameObject> _braceVisuals=new();
 private GameObject? _bracePreview;
 private bool BraceSelected=>SelectedPart=="goblin_brace";
 public int AddAssemblyBrace(int blockA,string socketA,int blockB,string socketB)
 {
  if(!Building||Assembly==null)return -1;var a=new BraceEndpoint(blockA,socketA);var b=new BraceEndpoint(blockB,socketB);
  var issue=Assembly.BraceIssue(a,b);if(issue!=""){_message=issue;return -1;}RememberAssembly();int id=Assembly.AddBrace(a,b);
  _selectedBlock=-id-1;SyncBraceVisuals();_placeSound?.Play();_message="Brace connected. The span stays fixed; its endpoints can pivot.";return id;
 }
 private GameObject MakeBraceVisual(string name)
 {
  var root=Own(name);root.SetParent(GameObject,false);
  CreateModelVisual(GameObject.Scene!,Assets!,PartsDirectory+"/goblin_brace_span.glb",root,"Span");
  CreateModelVisual(GameObject.Scene!,Assets!,PartsDirectory+"/goblin_brace_end.glb",root,"First cap");
  CreateModelVisual(GameObject.Scene!,Assets!,PartsDirectory+"/goblin_brace_end.glb",root,"Second cap");return root;
 }
 private void PoseBrace(GameObject obj,Vector3 a,Vector3 b)
 {
  var delta=b-a;float length=delta.Length();var rotation=AlignNormals(-Vector3.UnitZ,delta/Math.Max(.001f,length));
  obj.Transform.LocalPosition=(a+b)*.5f;obj.Transform.LocalRotation=rotation;
  var children=obj.Children.ToArray();children[0].Transform.LocalScale=new(1,1,length);
  children[1].Transform.LocalPosition=new(0,0,length*.5f);children[2].Transform.LocalPosition=new(0,0,-length*.5f);
 }
 private void SyncBraceVisuals()
 {
  if(Assembly==null)return;
  foreach(var id in _braceVisuals.Keys.Where(id=>!Assembly.Braces.ContainsKey(id)).ToArray()){GameObject.Scene!.DestroyGameObject(_braceVisuals[id]);_braceVisuals.Remove(id);}
  foreach(var brace in Assembly.Braces.Values){
   if(!_braceVisuals.TryGetValue(brace.Id,out var obj)){obj=MakeBraceVisual("Brace #"+brace.Id);_braceVisuals.Add(brace.Id,obj);}
   Vector3 Point(BraceEndpoint e){if(Building||_contraption==null)return Assembly.BracePoint(e);Matrix4x4.Invert(Transform.WorldMatrix,out var inv);return Vector3.Transform(_contraption.BracePoint(e),inv);}
   PoseBrace(obj,Point(brace.A),Point(brace.B));
  }
 }
 private bool DeleteBrace(int encoded)
 {
  int id=-encoded-1;if(!Building||Assembly==null||!Assembly.Braces.ContainsKey(id))return false;
  RememberAssembly();Assembly.Braces.Remove(id);SyncBraceVisuals();_selectedBlock=0;_message="Brace removed";return true;
 }
 private void PlaceBracePreview()
 {
  if(!_candidateVisible||_hoveredBlock<0)return;var end=new BraceEndpoint(_hoveredBlock,_candidateConnector);
  if(_braceStart==null){_braceStart=end;_message="First endpoint set. Choose the second connector; Escape cancels.";return;}
  if(AddAssemblyBrace(_braceStart.Block,_braceStart.Socket,end.Block,end.Socket)>=0){_braceStart=null;if(_bracePreview!=null)_bracePreview.Active=false;}
 }
 private void UpdateBracePreview(Vector3 point)
 {
  _ghost!.Active=false;
  if(_bracePreview==null)_bracePreview=MakeBraceVisual("Brace placement preview");
  _bracePreview.Active=_braceStart!=null&&_candidateVisible;
  _placementIssue=_braceStart==null?"":Assembly!.BraceIssue(_braceStart,new(_hoveredBlock,_candidateConnector));
  if(_bracePreview.Active)PoseBrace(_bracePreview,Assembly!.BracePoint(_braceStart!),point);
  _message=_braceStart==null?"BRACE: click the first connector":"BRACE: click the second connector. Escape cancels.";
 }
 private void PickBraceForErase(Vector3 origin,Vector3 direction,ref float nearest)
 {
  foreach(var brace in Assembly!.Braces.Values){var a=Assembly.BracePoint(brace.A);var b=Assembly.BracePoint(brace.B);var v=b-a;var w=origin-a;float vd=Vector3.Dot(v,direction),den=v.LengthSquared()-vd*vd;
   float t=den<.00001f?0:Math.Clamp((Vector3.Dot(w,v)-vd*Vector3.Dot(w,direction))/den,0,1);var point=a+v*t;float distance=Vector3.Dot(point-origin,direction);
   if(distance>0&&distance<nearest+.12f&&(point-origin-direction*distance).Length()<.1f){nearest=distance;_hoveredBlock=-brace.Id-1;}
  }
 }
}
