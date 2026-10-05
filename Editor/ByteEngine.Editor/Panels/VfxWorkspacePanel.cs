using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Vfx;
using ImGuiNET;

namespace ByteEngine.Editor.Panels;

/// <summary>Preset-first authoring: layers left, live preview center, focused settings right.</summary>
internal sealed class VfxWorkspacePanel : IDisposable
{
    private readonly EditorDocumentManager _documents;
    private readonly SceneFramebuffer _framebuffer=new();
    private readonly EditorCamera _camera2D=new();
    private readonly EditorCamera3D _camera=new();
    private readonly System.Diagnostics.Stopwatch _clock=System.Diagnostics.Stopwatch.StartNew();
    private readonly List<VfxEffect> _history=new();
    private AssetRecord? _asset;
    private EditorProjectContext? _project;
    private EditorDocumentId? _id;
    private VfxEffect _draft=new();
    private Scene? _scene;
    private VfxPlayer? _player;
    private int _selected,_historyIndex;
    private bool _dirty,_restart=true,_playing=true,_repeat=true,_closePrompt,_historyPending;
    private double _lastTime;
    private float _distance=6,_previewTime;
    private string? _error;
    public VfxWorkspacePanel(EditorDocumentManager documents) => _documents=documents;
    public void Open(AssetRecord asset,EditorProjectContext project,EditorLog log)
    {
        _asset=asset; _project=project; _draft=VfxEffectSerializer.Load(asset.FullPath);
        _id=new(EditorDocumentType.Vfx,asset.Guid.ToString("N"));
        _documents.RegisterOrFocus(new EditorDocument(_id.Value,"VFX: "+_draft.Name,()=>{},RequestClose,()=>Save(log),Reload,()=>_dirty));
        _history.Add(VfxEffectSerializer.Clone(_draft));
        _camera.Yaw=-135; _camera.Pitch=-12; PositionCamera();
    }
    public void Draw(EditorLog log,Renderer2D renderer,Renderer3D renderer3D,int windowWidth,int windowHeight)
    {
        if(_asset==null || _project==null) return;
        bool open=true;
        ImGui.Begin($"VFX: {_draft.Name}{(_dirty?" *":"")}###VfxWorkspace",ref open);
        if(_id.HasValue && ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows)) _documents.Activate(_id.Value);
        if(ImGui.Button("Save")) Save(log); ImGui.SameLine();
        if(ImGui.Button("Revert")) Reload(); ImGui.SameLine();
        ImGui.BeginDisabled(_historyIndex==0); if(ImGui.Button("Undo")) RestoreHistory(-1); ImGui.EndDisabled(); ImGui.SameLine();
        ImGui.BeginDisabled(_historyIndex>=_history.Count-1); if(ImGui.Button("Redo")) RestoreHistory(1); ImGui.EndDisabled(); ImGui.SameLine();
        if(ImGui.Button(_playing?"Pause":"Play")) _playing=!_playing; ImGui.SameLine();
        if(ImGui.Button("Restart")) { _restart=true; _playing=true; } ImGui.SameLine();
        if(ImGui.Button("Stop")) { _playing=false; _player?.Stop(true); _previewTime=0; } ImGui.SameLine();
        ImGui.Checkbox("Repeat preview",ref _repeat);
        if(ImGui.GetIO().KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.S,false)) Save(log);
        if(_dirty) ImGui.TextColored(new(1,.7f,.2f,1),"Unsaved changes");
        if(_error!=null) ImGui.TextColored(new(1,.4f,.3f,1),_error);
        ImGui.Separator();
        float available=ImGui.GetContentRegionAvail().X, left=180, right=Math.Clamp(available*.32f,300,430);
        ImGui.BeginChild("##VfxLayers",new(left,0),ImGuiChildFlags.Borders);
        ImGui.TextUnformatted("EFFECT LAYERS"); ImGui.TextDisabled("Up to 8 layers");
        for(int i=0;i<_draft.Layers.Count;i++)
        {
            ImGui.PushID(i);
            if(ImGui.Selectable(_draft.Layers[i].Name,_selected==i)) _selected=i;
            ImGui.PopID();
        }
        if(ImGui.BeginCombo("Add layer","Choose preset..."))
        {
            foreach(var preset in Enum.GetValues<VfxPreset>())
                if(ImGui.Selectable(preset.ToString()) && _draft.Layers.Count<8)
                { _draft.Layers.Add(VfxPresets.Create(preset).Layers[0]); _selected=_draft.Layers.Count-1; Changed(); }
            ImGui.EndCombo();
        }
        ImGui.BeginDisabled(_draft.Layers.Count==0);
        if(ImGui.Button("Duplicate") && _draft.Layers.Count<8)
        { var copy=VfxEffectSerializer.Clone(_draft).Layers[_selected]; copy.Name+=" copy"; _draft.Layers.Add(copy); _selected=_draft.Layers.Count-1; Changed(); }
        if(ImGui.Button("Remove") && _draft.Layers.Count>0) { _draft.Layers.RemoveAt(_selected); _selected=Math.Max(0,_selected-1); Changed(); }
        ImGui.EndDisabled();
        ImGui.Separator(); ImGui.TextWrapped("Layers combine into one effect. Select a layer to edit its look and motion.");
        ImGui.EndChild(); ImGui.SameLine();
        ImGui.BeginChild("##VfxPreview",new(Math.Max(220,available-left-right-16),0));
        ImGui.TextUnformatted("LIVE PREVIEW"); ImGui.SameLine();
        if(ImGui.Button("Frame")) { _distance=6; PositionCamera(); }
        DrawPreview(renderer,renderer3D,windowWidth,windowHeight);
        ImGui.TextDisabled("Left drag: orbit  |  Wheel: zoom");
        ImGui.TextUnformatted($"Particles: {_player?.ActiveParticles??0} / {_player?.ParticleCapacity??0}   Dropped: {_player?.DroppedParticles??0}");
        ImGui.TextDisabled("One draw per active layer; no per-particle GameObjects.");
        if(_player?.DroppedParticles>0) ImGui.TextColored(new(1,.7f,.2f,1),"Budget reached: reduce emission or increase capacity.");
        ImGui.Separator();
        ImGui.TextWrapped("Use: add VFX Player to an object, select this .bvfx asset, then use Event Sheet > VFX > Play VFX. Presets also work without an asset.");
        ImGui.EndChild(); ImGui.SameLine();
        ImGui.BeginChild("##VfxSettings",new(0,0),ImGuiChildFlags.Borders);
        DrawSettings();
        ImGui.EndChild(); ImGui.End();
        if(_historyPending && !ImGui.IsAnyItemActive()) CommitHistory();
        if(!open) RequestClose();
        if(_closePrompt) { ImGui.OpenPopup("Unsaved VFX"); _closePrompt=false; }
        if(ImGui.BeginPopupModal("Unsaved VFX",ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextUnformatted("Save effect changes before closing?");
            if(ImGui.Button("Save") && Save(log)) { Close(); ImGui.CloseCurrentPopup(); } ImGui.SameLine();
            if(ImGui.Button("Discard")) { Close(); ImGui.CloseCurrentPopup(); } ImGui.SameLine();
            if(ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
    }
    private void DrawSettings()
    {
        ImGui.TextUnformatted("EFFECT");
        string name=_draft.Name; if(ImGui.InputText("Name",ref name,128)) { _draft.Name=name; Changed(); }
        bool loop=_draft.Loop; if(ImGui.Checkbox("Loop in game",ref loop)) { _draft.Loop=loop; Changed(); }
        Float("Emission duration (s)",_draft.Duration,.01f,120,v=>_draft.Duration=v);
        Int("Total particle budget",_draft.ParticleBudget,1,8192,v=>_draft.ParticleBudget=v);
        ImGui.TextDisabled("Repeat preview does not change in-game looping.");
        if(_draft.Layers.Count==0) { ImGui.TextWrapped("Add a layer to start."); return; }
        _selected=Math.Clamp(_selected,0,_draft.Layers.Count-1); var p=_draft.Layers[_selected];
        ImGui.Separator(); ImGui.TextUnformatted("SELECTED LAYER");
        name=p.Name; if(ImGui.InputText("Layer name",ref name,128)) { p.Name=name; Changed(); }
        Bool("Enabled",p.Enabled,v=>p.Enabled=v);
        if(ImGui.CollapsingHeader("Look",ImGuiTreeNodeFlags.DefaultOpen))
        {
            EnumEdit("Render as",p.RenderMode,v=>p.RenderMode=v); EnumEdit("Sprite shape",p.Sprite,v=>p.Sprite=v);
            Bool("Glow / additive",p.Additive,v=>p.Additive=v);
            Vector4 color=p.StartColor; if(ImGui.ColorEdit4("Start color",ref color)) { p.StartColor=color; Changed(); }
            color=p.EndColor; if(ImGui.ColorEdit4("End color / fade",ref color)) { p.EndColor=color; Changed(); }
            Float("Start size (m)",p.StartSize,.001f,100,v=>p.StartSize=v); Float("End size (m)",p.EndSize,0,100,v=>p.EndSize=v);
            Float("Size variation",p.SizeVariation,0,.95f,v=>p.SizeVariation=v);
            Texture(p); Int("Sprite sheet columns",p.FlipbookColumns,1,8,v=>p.FlipbookColumns=v); Int("Sprite sheet rows",p.FlipbookRows,1,8,v=>p.FlipbookRows=v);
            ImGui.TextWrapped("Optional PNG texture, including alpha. Sheet frames play once over each particle's life.");
        }
        if(ImGui.CollapsingHeader("Emission",ImGuiTreeNodeFlags.DefaultOpen))
        {
            Float("Particles / second",p.Rate,0,10000,v=>p.Rate=v); Int("Initial burst",p.Burst,0,8192,v=>p.Burst=v);
            Float("Start delay (s)",p.Delay,0,120,v=>p.Delay=v); Float("Lifetime (s)",p.Lifetime,.02f,120,v=>p.Lifetime=v);
            Float("Lifetime variation",p.LifetimeVariation,0,.95f,v=>p.LifetimeVariation=v);
            EnumEdit("Spawn shape",p.Shape,v=>p.Shape=v); Vector("Shape half-extents",p.Extents,v=>p.Extents=v); Vector("Local offset",p.Offset,v=>p.Offset=v);
            Bool("Follow object (local space)",p.LocalSpace,v=>p.LocalSpace=v);
            ImGui.TextWrapped("World space leaves smoke and trails behind a moving object. Local space moves existing particles with it.");
        }
        if(ImGui.CollapsingHeader("Motion"))
        {
            Vector("Direction (local)",p.Direction,v=>p.Direction=v); Float("Spread (degrees)",p.Spread,0,180,v=>p.Spread=v);
            Float("Speed (m/s)",p.Speed,0,1000,v=>p.Speed=v); Float("Speed variation",p.SpeedVariation,0,1,v=>p.SpeedVariation=v);
            Vector("Gravity / acceleration",p.Gravity,v=>p.Gravity=v); Float("Drag",p.Drag,0,100,v=>p.Drag=v);
            Float("Rotation (degrees/s)",p.RotationSpeed,-3600,3600,v=>p.RotationSpeed=v);
            if(p.RenderMode==VfxRenderMode.Stretched) Float("Velocity stretch",p.Stretch,0,10,v=>p.Stretch=v);
            if(p.RenderMode==VfxRenderMode.Beam) Vector("Beam end (local)",p.BeamEnd,v=>p.BeamEnd=v);
        }
        if(ImGui.CollapsingHeader("Ground bounce"))
        {
            Bool("Bounce on flat plane",p.GroundBounce,v=>p.GroundBounce=v);
            Float("Plane height",p.GroundHeight,-10000,10000,v=>p.GroundHeight=v);
            Float("Bounciness",p.Bounciness,0,1,v=>p.Bounciness=v);
            ImGui.TextWrapped("Fast flat-plane collision, not scene-mesh collision. Height is world Y, or local Y when Follow Object is enabled.");
        }
        if(ImGui.CollapsingHeader("Performance",ImGuiTreeNodeFlags.DefaultOpen))
        {
            Int("Layer particle cap",p.MaxParticles,1,8192,v=>p.MaxParticles=v);
            ImGui.TextWrapped("Total budget is shared in layer order. Later layers receive the remaining capacity. Transparent particles are depth-sorted per layer. Glow layers skip sorting.");
        }
    }
    private void Float(string label,float value,float min,float max,Action<float> set)
    { if(ImGui.DragFloat(label,ref value,.01f,min,max,"%.3f",ImGuiSliderFlags.AlwaysClamp)) { set(value); Changed(); } }
    private void Int(string label,int value,int min,int max,Action<int> set)
    { if(ImGui.DragInt(label,ref value,1,min,max,"%d",ImGuiSliderFlags.AlwaysClamp)) { set(value); Changed(); } }
    private void Bool(string label,bool value,Action<bool> set) { if(ImGui.Checkbox(label,ref value)) { set(value); Changed(); } }
    private void Vector(string label,Vector3 value,Action<Vector3> set) { if(ImGui.DragFloat3(label,ref value,.02f)) { set(value); Changed(); } }
    private void EnumEdit<T>(string label,T value,Action<T> set) where T:struct,Enum
    { int index=Convert.ToInt32(value); var names=Enum.GetNames<T>(); if(ImGui.Combo(label,ref index,names,names.Length)) { set((T)Enum.ToObject(typeof(T),index)); Changed(); } }
    private void Texture(VfxLayer layer)
    {
        if(_project==null) return;
        var current=_project.AssetDatabase.Resolve(layer.Texture);
        if(ImGui.BeginCombo("Texture",current?.ProjectPath??"Built-in procedural sprite"))
        {
            if(ImGui.Selectable("Built-in procedural sprite",layer.Texture.IsEmpty)) { layer.Texture=AssetReference.Empty; Changed(); }
            foreach(var asset in _project.AssetDatabase.Assets.Where(a=>a.Type==AssetType.Texture2D).OrderBy(a=>a.ProjectPath))
                if(ImGui.Selectable(asset.ProjectPath,asset.Guid==current?.Guid)) { layer.Texture=new(asset.Guid,asset.ProjectPath); Changed(); }
            ImGui.EndCombo();
        }
        if(ImGui.BeginDragDropTarget())
        {
            var id=AssetDragDrop.Accept();
            if(id.HasValue && _project.AssetDatabase.TryGetAsset(id.Value,out var a) && a?.Type==AssetType.Texture2D)
            { layer.Texture=new(a.Guid,a.ProjectPath); Changed(); }
            ImGui.EndDragDropTarget();
        }
    }
    private void DrawPreview(Renderer2D renderer,Renderer3D renderer3D,int ww,int wh)
    {
        if(_project==null) return;
        _scene??=new Scene("VFX Preview",_project.Project.Classification);
        _player??=_scene.CreateGameObject("Effect").AddComponent(new VfxPlayer { PlayOnStart=false });
        double now=_clock.Elapsed.TotalSeconds; float dt=(float)Math.Clamp(now-_lastTime,0,.1); _lastTime=now;
        try
        {
            if(_restart && !ImGui.IsAnyItemActive())
            {
                _player.SetDefinition(_draft); _player.Play(); _restart=false; _previewTime=0;
                // Show something immediately for continuous emitters without changing the authored asset.
                _player.Advance(.12f);
            }
            if(_playing)
            {
                _player.Advance(dt); _previewTime+=dt;
                if(!_player.IsPlaying && _repeat) { _player.Play(); _previewTime=0; }
            }
            int width=Math.Clamp((int)ImGui.GetContentRegionAvail().X,256,1024),height=Math.Clamp((int)(width*.7f),200,720);
            _framebuffer.Render(renderer,renderer3D,_scene,EditorMode.Edit,_camera2D,_camera,true,width,height,ww,wh,
                drawGrid3D:true,prepareEnvironmentLighting3D:false,renderShadows3D:false);
            ImGui.Image(_framebuffer.TextureId,new(ImGui.GetContentRegionAvail().X,height),new(0,1),new(1,0));
            if(ImGui.IsItemHovered())
            {
                if(ImGui.IsMouseDragging(ImGuiMouseButton.Left)) { var d=ImGui.GetIO().MouseDelta; _camera.Yaw+=d.X*.3f; _camera.Pitch=Math.Clamp(_camera.Pitch-d.Y*.3f,-80,80); PositionCamera(); }
                if(ImGui.GetIO().MouseWheel!=0) { _distance=Math.Clamp(_distance*MathF.Pow(.88f,ImGui.GetIO().MouseWheel),.3f,100); PositionCamera(); }
            }
            ImGui.TextUnformatted($"Preview time: {_previewTime:0.00}s");
        }
        catch(Exception e) { _error=e.Message; _playing=false; }
    }
    private void PositionCamera() => _camera.Position=new Vector3(0,1,0)-_camera.Forward*_distance;
    private void Changed() { _dirty=true; _restart=true; _historyPending=true; _error=null; }
    private void CommitHistory()
    {
        _historyPending=false;
        if(_historyIndex<_history.Count-1) _history.RemoveRange(_historyIndex+1,_history.Count-_historyIndex-1);
        _history.Add(VfxEffectSerializer.Clone(_draft));
        if(_history.Count>50) _history.RemoveAt(0);
        _historyIndex=_history.Count-1;
    }
    private void RestoreHistory(int offset)
    { _historyIndex=Math.Clamp(_historyIndex+offset,0,_history.Count-1); _draft=VfxEffectSerializer.Clone(_history[_historyIndex]); _dirty=true; _restart=true; _historyPending=false; }
    private bool Save(EditorLog log)
    {
        if(_asset==null || _project==null) return false;
        try { VfxEffectSerializer.Save(_asset.FullPath,_draft); _project.Assets.ReloadVfxEffects(); _dirty=false; _error=null; log.Info("Saved VFX '"+_asset.ProjectPath+"'."); return true; }
        catch(Exception e) { _error=e.Message; log.Error("Could not save VFX: "+e.Message); return false; }
    }
    private void Reload()
    {
        if(_asset==null) return;
        try { _draft=VfxEffectSerializer.Load(_asset.FullPath); _dirty=false; _restart=true; _historyPending=false; _history.Clear(); _history.Add(VfxEffectSerializer.Clone(_draft)); _historyIndex=0; _error=null; }
        catch(Exception e) { _error=e.Message; }
    }
    private void RequestClose() { if(_dirty) _closePrompt=true; else Close(); }
    private void Close() { if(_id.HasValue) _documents.Unregister(_id.Value); _id=null; }
    public void Dispose()
    {
        if(_scene!=null) { _scene.UnloadInternal(); foreach(var obj in _scene.GameObjects.ToArray()) _scene.DestroyGameObject(obj); }
        _scene=null; _player=null; _framebuffer.Dispose(); Close();
    }
}
