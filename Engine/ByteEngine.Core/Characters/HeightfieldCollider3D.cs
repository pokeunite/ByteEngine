using System.Numerics;
using ByteEngine.Core.Graphics.ThreeD;

namespace ByteEngine.Core.Characters;

/// <summary>Finite, triangulated, Y-up terrain. Translation, yaw and positive scale are supported;
/// tilted or reflected heightfields are deliberately rejected. Samples and rays use the rendered triangles.</summary>
public abstract class HeightfieldCollider3D : Collider3D
{
    public abstract int Columns { get; }
    public abstract int Rows { get; }
    public abstract float CellSpacing { get; }
    public abstract Vector2 GridMinimum { get; }
    public abstract float HeightAt(int x, int z);
    public abstract BoundingBox3D LocalTerrainBounds { get; }
    public Matrix4x4 SurfaceMatrix => Matrix4x4.CreateTranslation(Center) * Transform.WorldMatrix;
    public BoundingBox3D WorldTerrainBounds => LocalTerrainBounds.Transform(SurfaceMatrix);
    public bool SupportedTransform => Transform.WorldScale.X > 0 && Transform.WorldScale.Y > 0 &&
        Transform.WorldScale.Z > 0 && Vector3.Dot(Transform.Up, Vector3.UnitY) > .99999f;

    public bool TrySampleLocal(float x, float z, out float height, out Vector3 normal)
    {
        height=0;normal=Vector3.UnitY;
        float gx=(x-GridMinimum.X)/CellSpacing,gz=(z-GridMinimum.Y)/CellSpacing;
        if(!float.IsFinite(gx)||!float.IsFinite(gz)||gx<0||gz<0||gx>Columns-1||gz>Rows-1) return false;
        int ix=Math.Min((int)gx,Columns-2),iz=Math.Min((int)gz,Rows-2);
        float u=gx-ix,v=gz-iz,a=HeightAt(ix,iz),b=HeightAt(ix+1,iz),c=HeightAt(ix,iz+1),d=HeightAt(ix+1,iz+1);
        float dx,dz;
        if(u+v<=1){height=a+u*(b-a)+v*(c-a);dx=(b-a)/CellSpacing;dz=(c-a)/CellSpacing;}
        else {height=d+(1-u)*(c-d)+(1-v)*(b-d);dx=(d-c)/CellSpacing;dz=(d-b)/CellSpacing;}
        normal=Vector3.Normalize(new Vector3(-dx,1,-dz));return true;
    }

    public bool TrySampleWorld(Vector3 position,out Vector3 surface,out Vector3 normal)
    {
        surface=default;normal=Vector3.UnitY;
        if(!SupportedTransform||!Matrix4x4.Invert(SurfaceMatrix,out var inverse))return false;
        var p=Vector3.Transform(position,inverse);
        if(!TrySampleLocal(p.X,p.Z,out float h,out var n))return false;
        surface=Vector3.Transform(new Vector3(p.X,h,p.Z),SurfaceMatrix);
        normal=Vector3.Normalize(Vector3.TransformNormal(n,Matrix4x4.Transpose(inverse)));return true;
    }

    /// <summary>Exact ray vs triangle traversal; sphere sweeps approximate radius as vertical clearance.</summary>
    public bool Cast(Vector3 origin,Vector3 direction,float radius,float maximumDistance,out float distance,out Vector3 normal)
    {
        distance=0;normal=Vector3.UnitY;
        if(!float.IsFinite(origin.X)||!float.IsFinite(origin.Y)||!float.IsFinite(origin.Z)||!float.IsFinite(direction.X)||!float.IsFinite(direction.Y)||!float.IsFinite(direction.Z)||!float.IsFinite(radius)||float.IsNaN(maximumDistance)||!SupportedTransform||direction.LengthSquared()<1e-12f||maximumDistance<0||!Matrix4x4.Invert(SurfaceMatrix,out var inverse))return false;
        direction=Vector3.Normalize(direction);
        var o=Vector3.Transform(origin,inverse);var d=Vector3.TransformNormal(direction,inverse);
        float expansion=Math.Max(0,radius)/Transform.WorldScale.Y;
        var bounds=LocalTerrainBounds;
        var min=bounds.Minimum-new Vector3(0,expansion,0);var max=bounds.Maximum+new Vector3(0,expansion,0);
        float enter=0,exit=maximumDistance;
        for(int axis=0;axis<3;axis++)
        {
            float a=axis==0?o.X:axis==1?o.Y:o.Z,b=axis==0?d.X:axis==1?d.Y:d.Z;
            float lo=axis==0?min.X:axis==1?min.Y:min.Z,hi=axis==0?max.X:axis==1?max.Y:max.Z;
            if(Math.Abs(b)<1e-9f){if(a<lo||a>hi)return false;continue;}
            float t0=(lo-a)/b,t1=(hi-a)/b;if(t0>t1)(t0,t1)=(t1,t0);
            enter=Math.Max(enter,t0);exit=Math.Min(exit,t1);if(enter>exit)return false;
        }
        if(TrySampleLocal(o.X,o.Z,out var initial,out var initialNormal)&&o.Y-expansion<=initial&&o.Y>=bounds.Minimum.Y)
        {normal=Vector3.Normalize(Vector3.TransformNormal(initialNormal,Matrix4x4.Transpose(inverse)));return true;}
        var start=o+d*(enter+1e-5f);int cx=Math.Clamp((int)((start.X-GridMinimum.X)/CellSpacing),0,Columns-2),cz=Math.Clamp((int)((start.Z-GridMinimum.Y)/CellSpacing),0,Rows-2);
        int sx=d.X>=0?1:-1,sz=d.Z>=0?1:-1;
        float tx=Math.Abs(d.X)<1e-9f?float.PositiveInfinity:(GridMinimum.X+(cx+(sx>0?1:0))*CellSpacing-o.X)/d.X;
        float tz=Math.Abs(d.Z)<1e-9f?float.PositiveInfinity:(GridMinimum.Y+(cz+(sz>0?1:0))*CellSpacing-o.Z)/d.Z;
        float deltaX=Math.Abs(d.X)<1e-9f?float.PositiveInfinity:CellSpacing/Math.Abs(d.X),deltaZ=Math.Abs(d.Z)<1e-9f?float.PositiveInfinity:CellSpacing/Math.Abs(d.Z);
        for(int steps=0;steps<Columns+Rows+4&&cx>=0&&cz>=0&&cx<Columns-1&&cz<Rows-1;steps++)
        {
            float x=GridMinimum.X+cx*CellSpacing,z=GridMinimum.Y+cz*CellSpacing;
            Vector3 a=new(x,HeightAt(cx,cz)+expansion,z),b=new(x+CellSpacing,HeightAt(cx+1,cz)+expansion,z),c=new(x,HeightAt(cx,cz+1)+expansion,z+CellSpacing),e=new(x+CellSpacing,HeightAt(cx+1,cz+1)+expansion,z+CellSpacing);
            float best=float.PositiveInfinity;Vector3 n=default;
            if(Triangle(o,d,a,c,b,out var t,out var nn)&&t>=enter-1e-4f&&t<=exit){best=t;n=nn;}
            if(Triangle(o,d,b,c,e,out t,out nn)&&t>=enter-1e-4f&&t<=exit&&t<best){best=t;n=nn;}
            if(float.IsFinite(best)){distance=Math.Max(0,best);normal=Vector3.Normalize(Vector3.TransformNormal(n,Matrix4x4.Transpose(inverse)));return true;}
            float next=Math.Min(tx,tz);if(next>exit||float.IsPositiveInfinity(next))break;
            if(tx<=tz+1e-6f){cx+=sx;tx+=deltaX;}
            if(tz<=next+1e-6f){cz+=sz;tz+=deltaZ;}
        }
        return false;
    }

    private static bool Triangle(Vector3 o,Vector3 d,Vector3 a,Vector3 b,Vector3 c,out float t,out Vector3 normal)
    {
        t=0;normal=Vector3.UnitY;var ab=b-a;var ac=c-a;var p=Vector3.Cross(d,ac);float det=Vector3.Dot(ab,p);
        if(Math.Abs(det)<1e-9f)return false;float inv=1/det;var s=o-a;float u=Vector3.Dot(s,p)*inv;
        if(u<-.00001f||u>1.00001f)return false;var q=Vector3.Cross(s,ab);float v=Vector3.Dot(d,q)*inv;
        if(v<-.00001f||u+v>1.00001f)return false;t=Vector3.Dot(ac,q)*inv;if(t<-.00001f)return false;
        normal=Vector3.Normalize(Vector3.Cross(ab,ac));return true;
    }
}
