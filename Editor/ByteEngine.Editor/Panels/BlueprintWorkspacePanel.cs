using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization.SerializationModels;
using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class BlueprintWorkspacePanel : IDisposable
{
    private readonly SceneFramebuffer _framebuffer = new();
    private readonly EditorCamera3D _camera = new();
    private BlueprintDefinition? _blueprint;
    private Scene? _preview;
    private AssetRecord? _asset;
    private bool _open;

    public void Open(AssetRecord asset, EditorProjectContext project)
    {
        _asset = asset;
        _blueprint = new BlueprintSerializer().Load(asset.FullPath);
        var objects = new List<GameObjectData> { _blueprint.Root };
        objects.AddRange(_blueprint.Children);
        _preview = project.Scenes.Deserialize(new SceneData
        {
            Name = _blueprint.Name + " Preview",
            SceneId = Guid.NewGuid(),
            GameObjects = objects
        });
        _camera.Reset();
        _open = true;
    }

    public void Draw(Renderer2D renderer, Renderer3D renderer3D, int windowWidth, int windowHeight)
    {
        if (!_open || _blueprint == null || _preview == null || _asset == null) return;
        ImGui.SetNextWindowSize(new Vector2(1100f, 700f), ImGuiCond.FirstUseEver);
        ImGui.Begin($"{_blueprint.Name} Blueprint##BlueprintWorkspace", ref _open);
        if (ImGui.BeginTable("BlueprintLayout", 3, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV))
        {
            ImGui.TableSetupColumn("Components", ImGuiTableColumnFlags.WidthFixed, 230f);
            ImGui.TableSetupColumn("Viewport", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Inspector", ImGuiTableColumnFlags.WidthFixed, 260f);
            ImGui.TableNextColumn();
            ImGui.SeparatorText("COMPONENTS / HIERARCHY");
            ImGui.Text(_blueprint.Root.Name);
            foreach (GameObjectData child in _blueprint.Children) ImGui.BulletText(child.Name);
            ImGui.SeparatorText("LOGIC MODULES");
            foreach (Guid module in _blueprint.EventModules) ImGui.BulletText(module.ToString());
            if (_blueprint.Type == BlueprintType.Character)
            {
                ImGui.SeparatorText("SKELETON");
                ImGui.TextDisabled(_blueprint.SkeletonAsset ?? "No skeleton assigned");
                ImGui.SeparatorText("SOCKETS");
                foreach (SocketDefinition socket in _blueprint.Sockets)
                    ImGui.BulletText($"{socket.Name} -> {socket.Bone}");
            }

            ImGui.TableNextColumn();
            Vector2 viewport = ImGui.GetContentRegionAvail();
            viewport.X = Math.Max(viewport.X, 1f);
            viewport.Y = Math.Max(viewport.Y, 1f);
            _framebuffer.Render(
                renderer, renderer3D, _preview, EditorMode.Edit, new EditorCamera(), _camera, true,
                (int)viewport.X, (int)viewport.Y, windowWidth, windowHeight);
            ImGui.Image(_framebuffer.TextureId, viewport, new Vector2(0f, 1f), new Vector2(1f, 0f));
            if (ImGui.IsItemHovered() && ImGui.IsMouseDragging(ImGuiMouseButton.Right))
            {
                Vector2 delta = ImGui.GetIO().MouseDelta;
                _camera.Yaw += delta.X * .18f;
                _camera.Pitch = Math.Clamp(_camera.Pitch - delta.Y * .18f, -89f, 89f);
            }

            ImGui.TableNextColumn();
            ImGui.SeparatorText("INSPECTOR");
            ImGui.Text($"Type: {_blueprint.Type}");
            ImGui.TextDisabled(_asset.ProjectPath);
            ImGui.SeparatorText("VARIABLE DEFAULTS");
            foreach (VariableData variable in _blueprint.Variables)
                ImGui.Text($"{variable.Name}: {variable.Value.BoxedValue}");
            ImGui.EndTable();
        }
        ImGui.End();
    }

    public void Dispose() => _framebuffer.Dispose();
}
