using System.Numerics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using OpenTK.Graphics.OpenGL4;
namespace ByteEngine.Tests;
internal static class RenderOptimizationTests
{
    private static void Check(bool value,string message) { if(!value)throw new Exception(message); }
    public static void Run(Renderer3D renderer,Mesh mesh)
    {
        using var texture=Texture2D.FromPixels(2,2,[255,255,255,255,255,255,255,0,255,255,255,0,255,255,255,255]);
        var original=new Material();
        Material Clone(){var m=new Material();foreach(var p in typeof(Material).GetProperties().Where(p=>p.CanRead&&p.CanWrite))p.SetValue(m,p.GetValue(original));return m;}
        RenderSubmission Submission(Material material,RenderQueue3D queue=RenderQueue3D.Opaque,bool receive=true)=>new(mesh,material,Matrix4x4.Identity,mesh.LocalBounds,queue,true,true,receive,0,0);
        Check(RenderBatcher.Build([Submission(original),Submission(Clone())]).Count==1,"Equivalent material instances do not batch");
        var reusable=new RenderBatcher.Builder();var changing=Clone();
        Check(reusable.Build([Submission(original),Submission(changing)]).Count==1,"Reusable builder does not merge equivalent materials");
        changing.BaseColor=new(1,0,0,1);
        Check(reusable.Build([Submission(original),Submission(changing)]).Count==2,"Per-pass material hash cache retained stale authored state");
        foreach(var p in typeof(Material).GetProperties().Where(p=>p.CanRead&&p.CanWrite))
        {
            var m=Clone();object value=p.GetValue(m)!;object changed;
            if(p.PropertyType==typeof(Texture2D))changed=texture;
            else if(value is bool flag)changed=!flag;
            else if(value is float number)changed=number+.25f;
            else if(value is Vector2 v2)changed=v2+new Vector2(.2f);
            else if(value is Vector3 v3)changed=v3+new Vector3(.2f);
            else if(value is Vector4 v4)changed=v4+new Vector4(.2f);
            else if(p.PropertyType.IsEnum)changed=Enum.GetValues(p.PropertyType).Cast<object>().First(v=>!v.Equals(value));
            else throw new Exception("New material state needs coverage: "+p.Name);
            p.SetValue(m,changed);
            Check(RenderBatcher.Build([Submission(original),Submission(m)]).Count==2,"Incompatible state merged: "+p.Name);
        }
        Check(RenderBatcher.Build([Submission(original,receive:false),Submission(Clone())]).Count==2,"Receive shadow flag merged");
        var noDepth=new Material{DepthTest=false};
        Check(RenderBatcher.Build([Submission(noDepth),Submission(noDepth)]).Count==2,"Depth-disabled draw order merged");
        var transparent=new Material{BlendMode=BlendMode3D.AlphaBlend};
        Check(RenderBatcher.Build([Submission(transparent,RenderQueue3D.Transparent),Submission(transparent,RenderQueue3D.Transparent)]).Count==2,"Transparent order merged");
        var view=new RenderView3D(Matrix4x4.CreateLookAt(new(0,0,3),Vector3.Zero,Vector3.UnitY),Matrix4x4.CreatePerspectiveFieldOfView(1,1,.1f,10),new(0,0,3),128,128);
        var lighting=new RenderLighting3D([new(new(0,0,-1),Vector3.One,1,0,true,256,5,.001f,1,0)],[new(new(0,0,2),Vector3.One,1,5,true,256,.001f,1,0)],0,1,1);
        foreach(bool cutout in new[]{false,true})
        {
            var m=new Material{BlendMode=cutout?BlendMode3D.Cutout:BlendMode3D.Opaque,MainTexture=cutout?texture:null};
            var items=new List<RenderSubmission>();
            for(int i=0;i<8;i++){var matrix=Matrix4x4.CreateScale(.5f)*Matrix4x4.CreateTranslation((i%4-1.5f)*.5f,(i/4-.5f)*.5f,0);items.Add(Submission(m) with {ModelMatrix=matrix,WorldBounds=mesh.LocalBounds.Transform(matrix)});}
            var copies=new List<Mesh>();
            for(int i=0;i<items.Count;i++)copies.Add(new Mesh(new float[]{-.4f,-.4f,0,0,0,1,0,0,.4f,-.4f,0,0,0,1,1,0,0,.4f,0,0,0,1,.5f,1},new uint[]{0,1,2}));
            var singles=items.Select((item,i)=>item with{Mesh=copies[i]}).ToArray();
            float[] Read(int id,bool cube,int resolution){GL.BindTexture(cube?TextureTarget.TextureCubeMap:TextureTarget.Texture2D,id);var result=new float[resolution*resolution*(cube?6:1)];for(int face=0;face<(cube?6:1);face++){var data=new float[resolution*resolution];GL.GetTexImage(cube?(TextureTarget)((int)TextureTarget.TextureCubeMapPositiveX+face):TextureTarget.Texture2D,0,PixelFormat.DepthComponent,PixelType.Float,data);data.CopyTo(result,face*data.Length);}GL.BindTexture(cube?TextureTarget.TextureCubeMap:TextureTarget.Texture2D,0);return result;}
            var separate=renderer.RenderDirectionalShadowMap(singles,view,lighting);var expected=Read(separate.Shadow!.Value.DepthTextureId,false,separate.Shadow.Value.Resolution);
            var batched=renderer.RenderDirectionalShadowMap(items,view,lighting);var actual=Read(batched.Shadow!.Value.DepthTextureId,false,batched.Shadow.Value.Resolution);
            Check(expected.Any(d=>d<.999f)&&expected.Zip(actual).All(p=>Math.Abs(p.First-p.Second)<.00001f),"Directional instanced shadow depths differ: cutout="+cutout);
            Check(separate.DrawCalls==8&&batched.DrawCalls==1,"Directional shadows do not batch");
            var pointSeparate=renderer.RenderPointShadowMaps(singles,view,lighting);expected=Read(pointSeparate.Shadows[0].DepthCubeTextureId,true,pointSeparate.Shadows[0].Resolution);
            var pointBatched=renderer.RenderPointShadowMaps(items,view,lighting);actual=Read(pointBatched.Shadows[0].DepthCubeTextureId,true,pointBatched.Shadows[0].Resolution);
            Check(expected.Any(d=>d<.999f)&&expected.Zip(actual).All(p=>Math.Abs(p.First-p.Second)<.00001f),"Point instanced shadow depths differ: cutout="+cutout);
            Check(pointSeparate.DrawCalls==48&&pointBatched.DrawCalls==6,"Point shadows do not batch");
            // Reuse must retain exact depth images, and live inputs must invalidate it.
            var cachedPoint=renderer.RenderPointShadowMaps(items,view,lighting);
            Check(cachedPoint.DrawCalls==0,"Unchanged point depth target was redrawn");
            Check(actual.SequenceEqual(Read(cachedPoint.Shadows[0].DepthCubeTextureId,true,cachedPoint.Shadows[0].Resolution)),"Point cache changes depth pixels");
            var cachedDirectional=renderer.RenderDirectionalShadowMap(items,view,lighting);
            Check(cachedDirectional.DrawCalls==0,"Unchanged directional depth target was redrawn");
            var moved=items.ToArray();
            var movedMatrix=moved[0].ModelMatrix*Matrix4x4.CreateTranslation(.4f,0,0);
            moved[0]=moved[0] with{ModelMatrix=movedMatrix,WorldBounds=mesh.LocalBounds.Transform(movedMatrix)};
            var updated=renderer.RenderDirectionalShadowMap(moved,view,lighting);
            var updatedDepth=Read(updated.Shadow!.Value.DepthTextureId,false,updated.Shadow.Value.Resolution);
            Check(updated.DrawCalls>0,"Moved caster used stale directional depth");
            renderer.EnableShadowCaching=false;
            var fresh=renderer.RenderDirectionalShadowMap(moved,view,lighting);
            Check(updatedDepth.SequenceEqual(Read(fresh.Shadow!.Value.DepthTextureId,false,fresh.Shadow.Value.Resolution)),"Invalidated depth differs from uncached rendering");
            renderer.EnableShadowCaching=true;
            Check(renderer.RenderPointShadowMaps(moved,view,lighting).DrawCalls>0,"Moved caster used stale point depth");
            var shifted=new RenderLighting3D(lighting.DirectionalLights,
                lighting.PointLights.Select(l=>l with{Position=l.Position+new Vector3(.2f,0,0)}),0,1,1);
            Check(renderer.RenderPointShadowMaps(moved,view,shifted).DrawCalls>0,"Moved point light reused stale depth");
            Check(renderer.RenderPointShadowMaps(moved,view,lighting).DrawCalls>0,"Returning point light reused overwritten target");
            Check(renderer.RenderDirectionalShadowMap(items,view,lighting).DrawCalls>0,"Returning caster reused overwritten target");
            if(cutout)
            {
                texture.UpdatePixels([255,255,255,255,255,255,255,255,255,255,255,255,255,255,255,255]);
                Check(renderer.RenderDirectionalShadowMap(items,view,lighting).DrawCalls>0,"Edited alpha mask reused stale directional depth");
                Check(renderer.RenderPointShadowMaps(items,view,lighting).DrawCalls>0,"Edited alpha mask reused stale point depth");
                m.AlphaCutoff=.7f;
                Check(renderer.RenderDirectionalShadowMap(items,view,lighting).DrawCalls>0,"Edited cutoff reused stale depth");
            }
            foreach(var copy in copies)copy.Dispose();
        }
        // An animated caster must not invalidate the cached buildings. Compare the composed
        // map against a complete fresh pass after motion, geometry and projection changes.
        using var movingMesh=new Mesh(mesh.VertexData.ToArray(),mesh.IndexData.ToArray(),dynamicVertices:true);
        var mixed=new List<RenderSubmission>{Submission(new Material()),Submission(new Material()) with{Mesh=movingMesh,ModelMatrix=Matrix4x4.CreateTranslation(.5f,0,0)}};
        float[] Depth(DirectionalShadowPassResult pass){var shadow=pass.Shadow!.Value;GL.BindTexture(TextureTarget.Texture2D,shadow.DepthTextureId);var result=new float[shadow.Resolution*shadow.Resolution];GL.GetTexImage(TextureTarget.Texture2D,0,PixelFormat.DepthComponent,PixelType.Float,result);GL.BindTexture(TextureTarget.Texture2D,0);return result;}
        void CompareMixed(RenderView3D camera,bool rebuild)
        {
            renderer.EnableShadowCaching=true;
            var pass=renderer.RenderDirectionalShadowMap(mixed,camera,lighting);
            Check(pass.DrawCalls==(rebuild?2:1),"Static/dynamic split unexpectedly rebuilt: "+pass.DrawCalls);
            var composed=Depth(pass);
            renderer.EnableShadowCaching=false;
            var fresh=renderer.RenderDirectionalShadowMap(mixed,camera,lighting);
            Check(composed.SequenceEqual(Depth(fresh)),"Static/dynamic shadow composition changes depth pixels");
            renderer.EnableShadowCaching=true;
        }
        CompareMixed(view,true);
        CompareMixed(view,true); // Second stable pass builds the static layer.
        CompareMixed(view,false);
        mixed[1]=mixed[1] with{ModelMatrix=Matrix4x4.CreateTranslation(-.5f,0,0)};
        CompareMixed(view,false);
        movingMesh.UpdateVertices(mesh.VertexData.ToArray());
        CompareMixed(view,false);
        mixed[0]=mixed[0] with{ModelMatrix=Matrix4x4.CreateTranslation(0,.3f,0)};
        CompareMixed(view,true);
        CompareMixed(view,true);
        CompareMixed(view,false);
        var shiftedView=new RenderView3D(Matrix4x4.CreateLookAt(new(.5f,0,3),Vector3.Zero,Vector3.UnitY),view.ProjectionMatrix,new(.5f,0,3),128,128);
        CompareMixed(shiftedView,true);
        Console.WriteLine("PASS exact cached shadow depths, alpha edits, moving lights/casters, and static/animated depth composition");
        // Multiple material groups must address distinct offsets in the shared instance stream.
        var multi=new List<RenderSubmission>();var red=new Material{Shading=ByteEngine.Core.Assets.MaterialShadingMode.Unlit,BaseColor=new(1,0,0,1)};var blue=new Material{Shading=ByteEngine.Core.Assets.MaterialShadingMode.Unlit,BaseColor=new(0,0,1,1)};
        for(int i=0;i<16;i++)multi.Add(Submission(i%2==0?red:blue) with{ModelMatrix=Matrix4x4.CreateScale(.3f)*Matrix4x4.CreateTranslation((i%4-1.5f)*.45f,(i/4-1.5f)*.45f,0)});
        byte[] Main(bool grouped){GL.Viewport(0,0,128,128);GL.ClearColor(0,0,0,1);GL.Clear(ClearBufferMask.ColorBufferBit|ClearBufferMask.DepthBufferBit);
            if(grouped){var batches=RenderBatcher.Build(multi);renderer.PrepareInstanceStream(batches);foreach(var batch in batches)renderer.Draw(batch.First.Mesh,batch.First.Material,batch.First.ModelMatrix,Matrix4x4.Identity,Matrix4x4.Identity,RenderLighting3D.Default,null,Array.Empty<RenderPointShadow3D>(),false,null,batch.Instances);}
            else foreach(var item in multi)renderer.Draw(item.Mesh,item.Material,item.ModelMatrix,Matrix4x4.Identity,Matrix4x4.Identity,RenderLighting3D.Default);
            GL.Finish();var pixels=new byte[128*128*4];GL.ReadPixels(0,0,128,128,PixelFormat.Rgba,PixelType.UnsignedByte,pixels);return pixels;}
        var reference=Main(false);var streamed=Main(true);Check(reference.Zip(streamed).All(p=>Math.Abs(p.First-p.Second)<=1),"Shared instance-stream offsets change pixels");
        // The instance normal transform must match the CPU normal matrix for signed,
        // nonuniform, rotated and sheared transforms, across distinct stream offsets.
        var litRed=new Material{BaseColor=new(.8f,.3f,.1f,1),CullMode=CullMode3D.None};
        var litBlue=new Material{BaseColor=new(.1f,.3f,.8f,1),CullMode=CullMode3D.None};
        multi.Clear();
        for(int i=0;i<16;i++)
        {
            var matrix=Matrix4x4.CreateScale(i%3==0?-.4f:.4f,.25f,.5f)*Matrix4x4.CreateRotationX(.4f)*Matrix4x4.CreateRotationY(.2f);
            matrix.M12+=.06f;
            matrix*=Matrix4x4.CreateTranslation((i%4-1.5f)*.45f,(i/4-1.5f)*.45f,0);
            multi.Add(Submission(i%2==0?litRed:litBlue) with{ModelMatrix=matrix});
        }
        reference=Main(false);streamed=Main(true);
        Check(reference.Zip(streamed).All(p=>Math.Abs(p.First-p.Second)<=1),"Fast instance normals change lit pixels");
        Check(reference.Where((v,i)=>i%4!=3&&v>10).Count()>100,"Normal-stream fixture is blank");
        Console.WriteLine("PASS fast instance normals: signed/nonuniform scale, rotation, shear and multiple material offsets");
        Check(GL.GetError()==ErrorCode.NoError,"Shadow instancing GL error");
        Console.WriteLine("PASS all material states, transparent ordering, independent shadow flags, and pixel-equivalent opaque/cutout directional and six-face point shadows");
    }
}
