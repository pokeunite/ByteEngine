using System.Numerics;

using ByteEngine.Core.Assets;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Core.VisualLogic;

using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class BlueprintWorkspacePanel
    : IDisposable
{
    private readonly SceneFramebuffer _framebuffer =
        new();

    private readonly EditorCamera3D _camera =
        new();

    private BlueprintDefinition? _blueprint;

    private Scene? _preview;

    private AssetRecord? _asset;

    private EditorProjectContext? _project;

    private Guid _selectedPreviewObjectId =
        Guid.Empty;

    private bool _open;

    public void Open(
        AssetRecord asset,
        EditorProjectContext project)
    {
        ArgumentNullException.ThrowIfNull(
            asset);

        ArgumentNullException.ThrowIfNull(
            project);

        _asset =
            asset;

        _project =
            project;

        _blueprint =
            new BlueprintSerializer()
                .Load(
                    asset.FullPath);

        var objects =
            new List<GameObjectData>
            {
                _blueprint.Root
            };

        objects.AddRange(
            _blueprint.Children);

        _preview =
            project.Scenes.Deserialize(
                new SceneData
                {
                    Name =
                        _blueprint.Name +
                        " Preview",

                    SceneId =
                        Guid.NewGuid(),

                    GameObjects =
                        objects
                });

        GameObject? firstRoot =
            _preview.GameObjects
                .FirstOrDefault(
                    gameObject =>
                        gameObject.Parent ==
                        null);

        _selectedPreviewObjectId =
            firstRoot?.Id ??
            Guid.Empty;

        _camera.Reset();

        _open =
            true;
    }

    public void Draw(
        Renderer2D renderer,
        Renderer3D renderer3D,
        int windowWidth,
        int windowHeight)
    {
        if (!_open ||
            _blueprint ==
                null ||
            _preview ==
                null ||
            _asset ==
                null)
        {
            return;
        }

        ImGui.SetNextWindowSize(
            new Vector2(
                1180.0f,
                760.0f),
            ImGuiCond.FirstUseEver);

        ImGui.Begin(
            $"{_blueprint.Name} Blueprint##BlueprintWorkspace",
            ref _open);

        if (ImGui.BeginTable(
                "BlueprintLayout",
                3,
                ImGuiTableFlags.Resizable |
                ImGuiTableFlags.BordersInnerV))
        {
            ImGui.TableSetupColumn(
                "Hierarchy",
                ImGuiTableColumnFlags.WidthFixed,
                270.0f);

            ImGui.TableSetupColumn(
                "Viewport",
                ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableSetupColumn(
                "Inspector",
                ImGuiTableColumnFlags.WidthFixed,
                300.0f);

            DrawHierarchyColumn();

            DrawViewportColumn(
                renderer,
                renderer3D,
                windowWidth,
                windowHeight);

            DrawInspectorColumn();

            ImGui.EndTable();
        }

        ImGui.End();
    }

    private void DrawHierarchyColumn()
    {
        ImGui.TableNextColumn();

        ImGui.SeparatorText(
            "BLUEPRINT HIERARCHY");

        foreach (GameObject root
                 in _preview!.GameObjects
                     .Where(
                         gameObject =>
                             gameObject.Parent ==
                             null))
        {
            DrawGameObjectNode(
                root);
        }

        ImGui.Dummy(
            new Vector2(
                0.0f,
                8.0f));

        ImGui.SeparatorText(
            "BLUEPRINT");

        ImGui.Text(
            $"Type: {_blueprint!.Type}");

        ImGui.TextDisabled(
            _asset!.ProjectPath);
    }

    private void DrawGameObjectNode(
        GameObject gameObject)
    {
        bool selected =
            _selectedPreviewObjectId ==
            gameObject.Id;

        ImGuiTreeNodeFlags flags =
            ImGuiTreeNodeFlags.OpenOnArrow |
            ImGuiTreeNodeFlags.SpanFullWidth |
            ImGuiTreeNodeFlags.DefaultOpen;

        if (selected)
        {
            flags |=
                ImGuiTreeNodeFlags.Selected;
        }

        if (gameObject.Children.Count ==
            0)
        {
            flags |=
                ImGuiTreeNodeFlags.Leaf;
        }

        bool open =
            ImGui.TreeNodeEx(
                $"{gameObject.Name}##bp-object:{gameObject.Id}",
                flags);

        if (ImGui.IsItemClicked(
                ImGuiMouseButton.Left) &&
            !ImGui.IsItemToggledOpen())
        {
            _selectedPreviewObjectId =
                gameObject.Id;
        }

        if (open)
        {
            foreach (Component component
                     in gameObject.Components)
            {
                DrawComponentEntry(
                    component);
            }

            foreach (GameObject child
                     in gameObject.Children)
            {
                DrawGameObjectNode(
                    child);
            }

            ImGui.TreePop();
        }
    }

    private void DrawComponentEntry(
        Component component)
    {
        string name =
            component.GetType().Name;

        ImGui.BulletText(
            name);

        if (component is not
            EventModuleComponent eventModules)
        {
            return;
        }

        foreach (AssetReference reference
                 in eventModules.Modules)
        {
            string moduleName =
                ResolveEventModuleName(
                    reference);

            ImGui.Indent();

            ImGui.TextDisabled(
                $"Event: {moduleName}");

            ImGui.Unindent();
        }
    }

    private string ResolveEventModuleName(
        AssetReference reference)
    {
        if (_project ==
            null)
        {
            return reference.ToString();
        }

        AssetRecord? asset =
            _project.AssetDatabase.Resolve(
                reference);

        return asset !=
            null
                ? Path.GetFileNameWithoutExtension(
                    asset.ProjectPath)
                : reference.CachedProjectPath ??
                  reference.Guid.ToString();
    }

    private void DrawViewportColumn(
        Renderer2D renderer,
        Renderer3D renderer3D,
        int windowWidth,
        int windowHeight)
    {
        ImGui.TableNextColumn();

        Vector2 viewport =
            ImGui.GetContentRegionAvail();

        viewport.X =
            Math.Max(
                viewport.X,
                1.0f);

        viewport.Y =
            Math.Max(
                viewport.Y,
                1.0f);

        _framebuffer.Render(
            renderer,
            renderer3D,
            _preview!,
            EditorMode.Edit,
            new EditorCamera(),
            _camera,
            true,
            (int)viewport.X,
            (int)viewport.Y,
            windowWidth,
            windowHeight);

        ImGui.Image(
            _framebuffer.TextureId,
            viewport,
            new Vector2(
                0.0f,
                1.0f),
            new Vector2(
                1.0f,
                0.0f));

        if (ImGui.IsItemHovered() &&
            ImGui.IsMouseDragging(
                ImGuiMouseButton.Right))
        {
            Vector2 delta =
                ImGui.GetIO()
                    .MouseDelta;

            _camera.Yaw +=
                delta.X *
                0.18f;

            _camera.Pitch =
                Math.Clamp(
                    _camera.Pitch -
                    delta.Y *
                    0.18f,
                    -89.0f,
                    89.0f);
        }
    }

    private void DrawInspectorColumn()
    {
        ImGui.TableNextColumn();

        ImGui.SeparatorText(
            "INSPECTOR");

        GameObject? selected =
            _selectedPreviewObjectId !=
                Guid.Empty
                ? _preview!.FindGameObject(
                    _selectedPreviewObjectId)
                : null;

        selected ??=
            _preview!.GameObjects
                .FirstOrDefault(
                    gameObject =>
                        gameObject.Parent ==
                        null);

        if (selected ==
            null)
        {
            ImGui.TextDisabled(
                "No Blueprint object selected.");

            return;
        }

        ImGui.Text(
            selected.Name);

        ImGui.TextDisabled(
            selected.Parent ==
                null
                ? "Blueprint Root"
                : $"Child of {selected.Parent.Name}");

        ImGui.SeparatorText(
            "TRANSFORM");

        DrawVector3ReadOnly(
            "Position",
            selected.Transform.LocalPosition);

        DrawVector3ReadOnly(
            "Rotation",
            selected.Transform.EulerAngles);

        DrawVector3ReadOnly(
            "Scale",
            selected.Transform.LocalScale);

        ImGui.SeparatorText(
            "COMPONENTS");

        if (selected.Components.Count ==
            0)
        {
            ImGui.TextDisabled(
                "No components.");
        }
        else
        {
            foreach (Component component
                     in selected.Components)
            {
                ImGui.BulletText(
                    component.GetType().Name);

                if (component is
                    EventModuleComponent eventModules)
                {
                    foreach (AssetReference reference
                             in eventModules.Modules)
                    {
                        ImGui.Indent();

                        ImGui.TextDisabled(
                            ResolveEventModuleName(
                                reference));

                        ImGui.Unindent();
                    }
                }
            }
        }

        ImGui.SeparatorText(
            "VARIABLES");

        bool anyVariables =
            false;

        foreach (var variable
                 in selected.Variables)
        {
            anyVariables =
                true;

            ImGui.Text(
                $"{variable.Key}: {variable.Value.BoxedValue}");
        }

        if (!anyVariables)
        {
            ImGui.TextDisabled(
                "No variables on this object.");
        }

        if (selected.Parent ==
            null &&
            _blueprint!.Variables.Count >
            0)
        {
            ImGui.SeparatorText(
                "BLUEPRINT DEFAULTS");

            foreach (VariableData variable
                     in _blueprint.Variables)
            {
                ImGui.Text(
                    $"{variable.Name}: {variable.Value.BoxedValue}");
            }
        }
    }

    private static void DrawVector3ReadOnly(
        string label,
        Vector3 value)
    {
        ImGui.TextDisabled(
            label);

        ImGui.SameLine();

        ImGui.Text(
            $"{value.X:0.###}, {value.Y:0.###}, {value.Z:0.###}");
    }

    public void Dispose()
    {
        _framebuffer.Dispose();
    }
}
