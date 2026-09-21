using System.Numerics;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Editor.Gizmos;
using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class SkeletalSocketPreview : IDisposable
{
    private readonly SceneFramebuffer _framebuffer=new();
    private readonly EditorCamera _camera2D=new();
    private readonly EditorCamera3D _camera=new();
    private readonly BlueprintTransformGizmo3D _gizmo=new();
    private Scene? _scene; private SkeletalMeshRenderer? _renderer; private GameObject? _gizmoProxy; private GameObject? _attachment;
    private Guid _modelGuid; private Guid? _previewGuid; private Vector3 _target=Vector3.Zero; private float _distance=2.5f; private bool _paused;
    public bool Draw(EditorProjectContext project, AssetReference reference, ModelAsset model, IReadOnlyList<SkeletalSocketDefinition> sockets,
        ref Guid selectedId, Renderer2D renderer, Renderer3D renderer3D, int windowWidth, int windowHeight, Action persist)
    {
        Ensure(project,reference,model); if(_scene==null||_renderer==null||_gizmoProxy==null){ImGui.TextDisabled("Socket preview unavailable.");return false;}
        if(!_paused)_scene.UpdateInternal();
        Guid selectedGuid=selectedId; SkeletalSocketDefinition? selected=sockets.FirstOrDefault(s=>s.Id==selectedGuid);
        EnsureAttachment(project,selected);
        SyncProxy(selected);
        if(selected!=null&&SkeletalSocketResolver.TryGetSocketWorldTransform(_renderer,selected.Name,out SkeletalSocketTransform socket)&&_attachment!=null)
        { _attachment.Transform.WorldPosition=socket.Position;_attachment.Transform.WorldRotation=socket.Rotation;_attachment.Transform.WorldScale=socket.Scale; }
        _framebuffer.Render(renderer,renderer3D,_scene,EditorMode.Edit,_camera2D,_camera,true,640,480,windowWidth,windowHeight,false,false,false);
        EditorUi.BeginToolbar("##SocketPreviewToolbar"); _gizmo.DrawToolbar(); ImGui.SameLine();
        if(EditorUi.ToolbarButton(_paused?"Play":"Pause",_paused?"Resume animation":"Pause animation"))_paused=!_paused;
        ImGui.SameLine(); if(EditorUi.ToolbarButton("Frame Socket","Frame selected socket"))Frame(selected);
        ImGui.SameLine(); ImGui.TextColored(EditorTheme.TextMuted,"W Translate   E Rotate   R Scale   F Frame"); EditorUi.EndToolbar();
        Vector2 avail=ImGui.GetContentRegionAvail(); Vector2 size=new(Math.Max(avail.X,240),Math.Max(avail.Y,240)); Vector2 min=ImGui.GetCursorScreenPos();
        ImGui.Image(_framebuffer.TextureId,size,new(0,1),new(1,0)); bool hovered=ImGui.IsItemHovered();
        DrawMarkers(sockets,ref selectedId,min,size);
        _gizmo.ApplyShortcuts(ImGui.IsKeyPressed(ImGuiKey.W),ImGui.IsKeyPressed(ImGuiKey.E),ImGui.IsKeyPressed(ImGuiKey.R));
        bool changed=false;
        if(selected!=null&&SkeletalSocketResolver.TryGetSocketWorldTransform(_renderer,selected.Name,out _))
            _gizmo.UpdateAndDraw(_gizmoProxy,_camera,hovered,min,size,()=>{changed=ApplyProxy(selected);if(changed)persist();});
        HandleCamera(hovered,min,size,selected);
        return changed;
    }

    private void Ensure(EditorProjectContext project,AssetReference reference,ModelAsset model)
    {
        if(_scene!=null&&_modelGuid==model.Guid)return; Reset(); _modelGuid=model.Guid;
        _scene=new Scene("Socket Preview",project.Project.Classification); GameObject root=_scene.CreateGameObject("Reference Model");
        _renderer=root.AddComponent(new SkeletalMeshRenderer{Model=reference,SkeletonKey=model.Skeleton?.Key,PlayOnStart=false,Loop=true,TransitionDuration=0});
        ImportedAnimation? animation=model.Animations.FirstOrDefault(); if(animation!=null)_renderer.Play(animation.Name,true,0);
        GameObject key=_scene.CreateGameObject("Key Light");key.Transform.EulerAngles=new(-35,-35,0);key.AddComponent(new DirectionalLight{Intensity=1.25f,AmbientIntensity=.25f,CastShadows=false});
        GameObject fill=_scene.CreateGameObject("Fill Light");fill.Transform.EulerAngles=new(20,145,0);fill.AddComponent(new DirectionalLight{Intensity=.5f,AmbientIntensity=.1f,CastShadows=false});
        _gizmoProxy=_scene.CreateGameObject("Socket Gizmo Proxy"); _scene.LoadInternal();
        if(_renderer.TryGetCurrentModelBounds(out BoundingBox3D bounds)&&bounds.IsValid){_target=(bounds.Minimum+bounds.Maximum)*.5f;_distance=Math.Max((bounds.Maximum-bounds.Minimum).Length(),1f);}
        UpdateCamera();
    }

    private void EnsureAttachment(EditorProjectContext project,SkeletalSocketDefinition? socket)
    {
        Guid? guid=socket?.PreviewAssetGuid; if(guid==_previewGuid)return; if(_attachment!=null&&_scene!=null)_scene.DestroyGameObject(_attachment);_attachment=null;_previewGuid=guid;
        if(guid==null||socket==null||_scene==null)return; AssetRecord? asset=project.AssetDatabase.Resolve(new AssetReference(guid.Value,socket.PreviewAssetPath)); if(asset?.Type!=AssetType.Model3D)return;
        AssetReference reference=new(asset.Guid,asset.ProjectPath); ModelAsset model=project.Assets.LoadModel(reference); _attachment=_scene.CreateGameObject("Socket Preview Asset");
        Dictionary<string,GameObject> nodes=new(); foreach(ImportedNode node in model.Nodes)nodes[node.Key]=_scene.CreateGameObject(node.Name);
        foreach(ImportedNode node in model.Nodes){GameObject item=nodes[node.Key];item.SetParent(node.ParentKey!=null&&nodes.TryGetValue(node.ParentKey,out GameObject? p)?p:_attachment,false);ApplyLocal(item,node.LocalTransform);
            foreach(string meshKey in node.MeshKeys){ImportedMesh mesh=model.Meshes.First(m=>m.Key==meshKey);GameObject meshObject=node.MeshKeys.Count==1?item:_scene.CreateGameObject(mesh.Name);if(!ReferenceEquals(meshObject,item))meshObject.SetParent(item,false);MeshRenderer mr=new(){MeshReference=new ModelMeshReference(reference,meshKey),Mesh=project.Assets.GetModelMesh(reference,meshKey)};if(mesh.MaterialKey!=null){mr.MaterialReference=new ModelMaterialReference(reference,mesh.MaterialKey);mr.Material=project.Assets.GetModelMaterial(reference,mesh.MaterialKey);}meshObject.AddComponent(mr);}}
    }

    private void SyncProxy(SkeletalSocketDefinition? socket)
    { if(socket==null||_renderer==null||_gizmoProxy==null)return;if(SkeletalSocketResolver.TryGetSocketWorldTransform(_renderer,socket.Name,out SkeletalSocketTransform t)){_gizmoProxy.Transform.WorldPosition=t.Position;_gizmoProxy.Transform.WorldRotation=t.Rotation;_gizmoProxy.Transform.WorldScale=t.Scale;} }
    private bool ApplyProxy(SkeletalSocketDefinition socket)
    {
        if(_renderer==null||_gizmoProxy==null||!_renderer.TryGetBoneWorldMatrix(socket.BoneName,out Matrix4x4 matrix)||!SkeletalSocketResolver.TryExtractStableBoneFrame(matrix,out Vector3 bp,out Quaternion br,out Vector3 bs))return false;
        Vector3 inherited=socket.InheritBoneScale?bs:Vector3.One; socket.PositionOffset=Vector3.Transform(_gizmoProxy.Transform.WorldPosition-bp,Quaternion.Inverse(br))/inherited;
        Quaternion local=Quaternion.Normalize(_gizmoProxy.Transform.WorldRotation*Quaternion.Inverse(br)); socket.RotationOffsetDegrees=Euler(local); socket.Scale=Vector3.Max(_gizmoProxy.Transform.WorldScale/inherited,new Vector3(.0001f)); return true;
    }
    private void DrawMarkers(IReadOnlyList<SkeletalSocketDefinition> sockets,ref Guid selected,Vector2 min,Vector2 size)
    { if(_renderer==null)return;ImDrawListPtr draw=ImGui.GetWindowDrawList();Vector2 mouse=ImGui.GetMousePos();foreach(SkeletalSocketDefinition socket in sockets){if(!SkeletalSocketResolver.TryGetSocketWorldTransform(_renderer,socket.Name,out SkeletalSocketTransform t))continue;Vector2 p=Gizmo3DController.Project(t.Position,_camera,min,size);bool isSelected=socket.Id==selected;uint color=ImGui.GetColorU32(isSelected?new Vector4(1,.8f,.1f,1):new Vector4(.2f,.8f,1,1));draw.AddCircleFilled(p,isSelected?7:5,color);draw.AddLine(p-new Vector2(9,0),p+new Vector2(9,0),color,2);draw.AddLine(p-new Vector2(0,9),p+new Vector2(0,9),color,2);draw.AddText(p+new Vector2(10,-8),color,socket.Name);if(ImGui.IsItemHovered()&&ImGui.IsMouseClicked(ImGuiMouseButton.Left)&&Vector2.Distance(mouse,p)<12)selected=socket.Id;}}
    private void Frame(SkeletalSocketDefinition? socket){if(socket!=null&&_renderer!=null&&SkeletalSocketResolver.TryGetSocketWorldTransform(_renderer,socket.Name,out SkeletalSocketTransform t)){_target=t.Position;_distance=Math.Max(t.Scale.Length()*1.5f,.5f);UpdateCamera();}}
    private void HandleCamera(bool hovered,Vector2 min,Vector2 size,SkeletalSocketDefinition? selected){if(!hovered)return;ImGuiIOPtr io=ImGui.GetIO();if(ImGui.IsKeyPressed(ImGuiKey.F))Frame(selected);if(!_gizmo.OwnsMouse&&ImGui.IsMouseDragging(ImGuiMouseButton.Right)){_camera.Yaw+=io.MouseDelta.X*.3f;_camera.Pitch=Math.Clamp(_camera.Pitch-io.MouseDelta.Y*.3f,-89,89);UpdateCamera();}if(!_gizmo.OwnsMouse&&ImGui.IsMouseDragging(ImGuiMouseButton.Middle)){float scale=Math.Max(_distance*.002f,.001f);_target+=(-_camera.Right*io.MouseDelta.X+Vector3.Normalize(Vector3.Cross(_camera.Right,_camera.Forward))*io.MouseDelta.Y)*scale;UpdateCamera();}if(io.MouseWheel!=0){_distance=Math.Clamp(_distance*MathF.Pow(.88f,io.MouseWheel),.05f,10000);UpdateCamera();}}
    private void UpdateCamera(){_camera.FieldOfView=35;float yaw=_camera.Yaw*MathF.PI/180,pitch=_camera.Pitch*MathF.PI/180;Vector3 forward=Vector3.Normalize(new(MathF.Cos(pitch)*MathF.Sin(yaw),MathF.Sin(pitch),-MathF.Cos(pitch)*MathF.Cos(yaw)));_camera.Position=_target-forward*_distance;}
    private static void ApplyLocal(GameObject item,Matrix4x4 matrix){if(Matrix4x4.Decompose(matrix,out Vector3 s,out Quaternion r,out Vector3 p)){item.Transform.LocalPosition=p;item.Transform.LocalRotation=r;item.Transform.LocalScale=s;}}
    private static Vector3 Euler(Quaternion q){q=Quaternion.Normalize(q);float x=MathF.Asin(Math.Clamp(2*(q.W*q.X-q.Y*q.Z),-1,1));float y=MathF.Atan2(2*(q.W*q.Y+q.X*q.Z),1-2*(q.X*q.X+q.Y*q.Y));float z=MathF.Atan2(2*(q.W*q.Z+q.X*q.Y),1-2*(q.X*q.X+q.Z*q.Z));return new(x*180/MathF.PI,y*180/MathF.PI,z*180/MathF.PI);}
    public void Reset(){if(_scene?.IsLoaded==true)_scene.UnloadInternal();_scene=null;_renderer=null;_gizmoProxy=null;_attachment=null;_modelGuid=Guid.Empty;_previewGuid=null;}
    public void Dispose(){Reset();_framebuffer.Dispose();}
}