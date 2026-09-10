using System.Numerics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ImGuiNET;
namespace ByteEngine.Editor.Gizmos;

internal sealed class Gizmo3DController
{
    private Vector3 _axis; private Vector2 _startMouse; private Vector3 _startPosition; private bool _dragging;
    public void UpdateAndDraw(EditorState state,EditorCamera3D camera,bool hovered,Vector2 min,Vector2 size)
    {
        GameObject? selected=state.SelectedObject;
        if(selected!=null)DrawAxes(selected,camera,min,size);
        if(state.Mode!=EditorMode.Edit)return;
        if(_dragging)
        {
            if(ImGui.IsMouseDown(ImGuiMouseButton.Left)){Vector2 origin=Project(_startPosition,camera,min,size);Vector2 endpoint=Project(_startPosition+_axis,camera,min,size);Vector2 screenAxis=Vector2.Normalize(endpoint-origin);float distance=Vector3.Distance(camera.Position,_startPosition);float unitsPerPixel=2f*distance*MathF.Tan(camera.FieldOfView*MathF.PI/360f)/Math.Max(size.Y,1);float movement=Vector2.Dot(ImGui.GetMousePos()-_startMouse,screenAxis)*unitsPerPixel;selected!.Transform.WorldPosition=_startPosition+_axis*movement;}
            else{_dragging=false;state.Undo?.CommitGesture(state);}return;
        }
        if(!hovered||!ImGui.IsMouseClicked(ImGuiMouseButton.Left))return;
        if(selected!=null&&TryHitAxis(selected,camera,min,size,out Vector3 axis)){_axis=axis;_startMouse=ImGui.GetMousePos();_startPosition=selected.Transform.WorldPosition;_dragging=true;state.Undo?.BeginGesture(state,"Move Object 3D");return;}
        Ray ray=ScreenRay(ImGui.GetMousePos(),camera,min,size);GameObject? hit=null;float best=float.MaxValue;
        foreach(GameObject item in state.DisplayedScene.GameObjects){if(item.GetComponent<MeshRenderer>()==null||!item.ActiveInHierarchy)continue;if(RayBox(ray,item.Transform.WorldMatrix,out float distance)&&distance<best){best=distance;hit=item;}}
        state.Selection.Set(hit); if(hit!=null){state.SelectedAssetId=null;state.SelectedAssetPath=null;}
    }
    private static void DrawAxes(GameObject item,EditorCamera3D camera,Vector2 min,Vector2 size)
    {
        Vector3 p=item.Transform.WorldPosition;Vector2 o=Project(p,camera,min,size);ImDrawListPtr draw=ImGui.GetWindowDrawList();
        DrawAxis(draw,o,Project(p+Vector3.UnitX,camera,min,size),new(1,.2f,.2f,1));
        DrawAxis(draw,o,Project(p+Vector3.UnitY,camera,min,size),new(.2f,1,.3f,1));
        DrawAxis(draw,o,Project(p+Vector3.UnitZ,camera,min,size),new(.2f,.45f,1,1));
    }
    private static void DrawAxis(ImDrawListPtr draw,Vector2 a,Vector2 b,Vector4 color){Vector2 d=b-a;if(d.LengthSquared()<1)return;d=Vector2.Normalize(d);b=a+d*70;uint c=ImGui.GetColorU32(color);draw.AddLine(a,b,c,4);draw.AddCircleFilled(b,5,c);}
    private static bool TryHitAxis(GameObject item,EditorCamera3D camera,Vector2 min,Vector2 size,out Vector3 axis)
    {
        Vector3 p=item.Transform.WorldPosition;Vector2 o=Project(p,camera,min,size),mouse=ImGui.GetMousePos();
        foreach(Vector3 candidate in new[]{Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ}){Vector2 d=Project(p+candidate,camera,min,size)-o;if(d.LengthSquared()<1)continue;Vector2 end=o+Vector2.Normalize(d)*70;if(DistanceToSegment(mouse,o,end)<9){axis=candidate;return true;}}
        axis=default;return false;
    }
    internal static Vector2 Project(Vector3 point,EditorCamera3D camera,Vector2 min,Vector2 size)
    {
        Vector4 clip=Vector4.Transform(new Vector4(point,1),camera.View*camera.Projection(size.X/Math.Max(size.Y,1)));if(Math.Abs(clip.W)<.0001f)return min+size*.5f;Vector3 ndc=new(clip.X/clip.W,clip.Y/clip.W,clip.Z/clip.W);return min+new Vector2((ndc.X+1)*.5f*size.X,(1-ndc.Y)*.5f*size.Y);
    }
    private static Ray ScreenRay(Vector2 mouse,EditorCamera3D camera,Vector2 min,Vector2 size)
    {
        float x=(mouse.X-min.X)/size.X*2-1,y=1-(mouse.Y-min.Y)/size.Y*2;Matrix4x4.Invert(camera.View*camera.Projection(size.X/size.Y),out Matrix4x4 inv);
        Vector4 near=Vector4.Transform(new Vector4(x,y,0,1),inv),far=Vector4.Transform(new Vector4(x,y,1,1),inv);Vector3 a=new(near.X/near.W,near.Y/near.W,near.Z/near.W),b=new(far.X/far.W,far.Y/far.W,far.Z/far.W);return new(a,Vector3.Normalize(b-a));
    }
    private static bool RayBox(Ray ray,Matrix4x4 world,out float distance)
    {
        distance=0;if(!Matrix4x4.Invert(world,out Matrix4x4 inv))return false;Vector3 o=Vector3.Transform(ray.Origin,inv),d=Vector3.TransformNormal(ray.Direction,inv);float min=0,max=100000;
        for(int axis=0;axis<3;axis++){float origin=axis==0?o.X:axis==1?o.Y:o.Z,dir=axis==0?d.X:axis==1?d.Y:d.Z;if(Math.Abs(dir)<.00001f){if(origin<-.5f||origin>.5f)return false;continue;}float a=(-.5f-origin)/dir,b=(.5f-origin)/dir;if(a>b)(a,b)=(b,a);min=Math.Max(min,a);max=Math.Min(max,b);if(min>max)return false;}distance=min;return true;
    }
    private static float DistanceToSegment(Vector2 p,Vector2 a,Vector2 b){Vector2 d=b-a;float t=Math.Clamp(Vector2.Dot(p-a,d)/Math.Max(d.LengthSquared(),.001f),0,1);return Vector2.Distance(p,a+d*t);}
    private readonly record struct Ray(Vector3 Origin,Vector3 Direction);
}
