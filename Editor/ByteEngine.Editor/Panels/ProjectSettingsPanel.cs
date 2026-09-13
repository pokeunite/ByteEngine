using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Classification;
using System.Text.RegularExpressions;
using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class ProjectSettingsPanel
{
    private readonly PopupInteractionState _addActionPopup = new();
    private readonly PopupInteractionState _deleteActionPopup = new();
    private readonly PopupInteractionState _addTagPopup = new();
    private Guid _selectedActionId;
    private Guid _deleteActionId;
    private Guid _captureBindingId;
    private string _captureSlot = string.Empty;
    private int _captureDelayFrames;
    private string _newActionName = "New Action";
    private InputActionType _newActionType;
    private string _newTagName = string.Empty;
    private string _classificationMessage = string.Empty;

    public bool IsOpen { get; set; }

    public void Draw(EditorProjectContext project)
    {
        if (!IsOpen) return;
        bool open = IsOpen;
        ImGui.SetNextWindowSize(new Vector2(760f, 520f), ImGuiCond.FirstUseEver);
        bool visible = ImGui.Begin("Project Settings", ref open);
        IsOpen = open;
        if (!visible) { ImGui.End(); return; }

        if (ImGui.BeginTabBar("ProjectSettingsTabs"))
        {
            if (ImGui.BeginTabItem("Input"))
            {
                DrawInputSettings(project);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Tags & Layers"))
            {
                DrawTagsAndLayers(project);
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
        ImGui.End();
        DrawAddActionPopup(project);
        DrawDeleteActionPopup(project);
        DrawAddTagPopup(project);
    }

    private void DrawTagsAndLayers(EditorProjectContext project)
    {
        ClassificationSettings settings = project.Project.Classification;
        ImGui.SeparatorText("TAGS");
        foreach (TagDefinition tag in settings.Tags.ToArray())
        {
            ImGui.PushID(tag.Id.ToString());
            string name = tag.Name;
            ImGui.SetNextItemWidth(260f);
            if (ImGui.InputText("##TagName", ref name, 96, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                _classificationMessage = settings.RenameTag(tag.Id, name) ? string.Empty : "Tag names must be non-empty and unique.";
                if (_classificationMessage.Length == 0) Commit(project);
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("Delete"))
            {
                int uses = CountReferences(project.ProjectRoot, tag.Id);
                if (uses > 0) _classificationMessage = $"Tag \"{tag.Name}\" is used by {uses} objects. Remove those assignments before deleting this tag.";
                else { settings.Tags.Remove(tag); Commit(project); _classificationMessage = string.Empty; }
            }
            ImGui.PopID();
        }
        if (ImGui.Button("+ Add Tag")) { _newTagName = string.Empty; _addTagPopup.Request(true); }

        ImGui.SeparatorText("LAYERS");
        for (int index = 0; index < 32; index++)
        {
            ObjectLayerDefinition? layer = settings.FindLayer(index);
            string name = layer?.Name ?? string.Empty;
            ImGui.PushID(index);
            ImGui.TextUnformatted(index.ToString());
            ImGui.SameLine();
            ImGui.SetNextItemWidth(260f);
            if (index == 0) ImGui.BeginDisabled();
            if (ImGui.InputText("##LayerName", ref name, 64, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                if (string.IsNullOrWhiteSpace(name) && index != 0)
                {
                    int uses = CountLayerReferences(project.ProjectRoot, index);
                    if (uses > 0) _classificationMessage = $"Layer \"{layer?.Name}\" is used by {uses} objects. Reassign those objects before clearing this layer.";
                    else { settings.Layers.RemoveAll(item => item.Index == index); Commit(project); _classificationMessage = string.Empty; }
                }
                else
                {
                    _classificationMessage = settings.DefineLayer(index, name) ? string.Empty : "Layer names must be unique.";
                    if (_classificationMessage.Length == 0) Commit(project);
                }
            }
            if (index == 0) ImGui.EndDisabled();
            ImGui.PopID();
        }

        ImGui.SeparatorText("COLLISION MATRIX");
        ImGui.TextDisabled("The project matrix controls normal interactions. Query masks separately choose which layers a query inspects.");
        ObjectLayerDefinition[] layers = settings.Layers.OrderBy(item => item.Index).ToArray();
        if (ImGui.BeginTable("CollisionMatrix", layers.Length + 1, ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollX))
        {
            ImGui.TableSetupColumn("Layer");
            foreach (ObjectLayerDefinition layer in layers) ImGui.TableSetupColumn(layer.Name);
            ImGui.TableHeadersRow();
            foreach (ObjectLayerDefinition row in layers)
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0); ImGui.TextUnformatted(row.Name);
                for (int column = 0; column < layers.Length; column++)
                {
                    ImGui.TableSetColumnIndex(column + 1);
                    ObjectLayerDefinition other = layers[column];
                    bool interacts = settings.CollisionMatrix.ShouldInteract(row.Index, other.Index);
                    if (ImGui.Checkbox($"##Matrix{row.Index}_{other.Index}", ref interacts))
                    {
                        settings.CollisionMatrix.SetInteraction(row.Index, other.Index, interacts);
                        Commit(project);
                    }
                }
            }
            ImGui.EndTable();
        }
        if (_classificationMessage.Length > 0) ImGui.TextColored(new Vector4(1f, .65f, .2f, 1f), _classificationMessage);
    }

    private void DrawInputSettings(EditorProjectContext project)
    {
        InputMap map = project.Project.InputMap;
        if (_selectedActionId == Guid.Empty || map.Find(_selectedActionId) == null)
            _selectedActionId = map.Actions.FirstOrDefault()?.Id ?? Guid.Empty;

        ImGui.BeginChild("Actions", new Vector2(220f, 0f), ImGuiChildFlags.Borders);
        ImGui.Text("ACTIONS");
        ImGui.SameLine();
        if (ImGui.SmallButton("+##AddAction"))
        {
            _newActionName = "New Action";
            _newActionType = InputActionType.Button;
            _addActionPopup.Request(true);
        }
        ImGui.Separator();
        foreach (InputActionDefinition action in map.Actions)
            if (ImGui.Selectable($"{action.DisplayName}##{action.Id}", action.Id == _selectedActionId)) _selectedActionId = action.Id;
        ImGui.EndChild();
        ImGui.SameLine();

        ImGui.BeginChild("ActionDetails", Vector2.Zero, ImGuiChildFlags.Borders);
        InputActionDefinition? selected = map.Find(_selectedActionId);
        if (selected == null) ImGui.TextDisabled("Select or add an input action.");
        else DrawAction(project, selected);
        ImGui.EndChild();
    }

    private void DrawAction(EditorProjectContext project, InputActionDefinition action)
    {
        string name = action.DisplayName;
        ImGui.SetNextItemWidth(260f);
        if (ImGui.InputText("Name", ref name, 128, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            if (!project.Project.InputMap.Rename(action.Id, name)) ImGui.OpenPopup("DuplicateActionName");
            else Commit(project);
        }
        if (ImGui.BeginPopup("DuplicateActionName")) { ImGui.TextColored(new Vector4(1f, .65f, .2f, 1f), "Action names must be unique."); ImGui.EndPopup(); }

        int type = (int)action.Type;
        string[] types = Enum.GetNames<InputActionType>();
        if (ImGui.Combo("Type", ref type, types, types.Length)) { action.Type = (InputActionType)type; Commit(project); }
        string group = action.Group;
        if (ImGui.InputText("Group", ref group, 64, ImGuiInputTextFlags.EnterReturnsTrue)) { action.Group = string.IsNullOrWhiteSpace(group) ? "Gameplay" : group.Trim(); Commit(project); }

        if (ImGui.Button("Delete Action")) { _deleteActionId = action.Id; _deleteActionPopup.Request(); }
        ImGui.SeparatorText("Bindings");
        if (ImGui.Button("Add Binding")) ImGui.OpenPopup("AddBindingMenu");
        if (ImGui.BeginPopup("AddBindingMenu"))
        {
            foreach (InputBindingType bindingType in SensibleBindings(action.Type))
            {
                if (!ImGui.MenuItem(bindingType.ToString())) continue;
                InputBinding created = CreateBinding(bindingType);
                action.Bindings.Add(created);
                if (bindingType == InputBindingType.KeyboardKey) BeginCapture(created, "Key");
                else if (bindingType == InputBindingType.MouseButton) BeginCapture(created, "MouseButton");
                Commit(project);
            }
            ImGui.EndPopup();
        }

        foreach (InputBinding binding in action.Bindings.ToArray())
        {
            ImGui.PushID(binding.Id.ToString());
            InputActionDefinition? duplicateAction = project.Project.InputMap.Actions.FirstOrDefault(candidate =>
                candidate.Bindings.Any(other => other.Id != binding.Id && Equivalent(binding, other)));
            if (duplicateAction != null)
                ImGui.TextColored(new Vector4(1f, .65f, .2f, 1f), $"Already bound to {duplicateAction.DisplayName}.");
            ImGui.Text(binding.Type.ToString());
            ImGui.SameLine();
            if (ImGui.SmallButton("Remove")) { action.Bindings.Remove(binding); Commit(project); ImGui.PopID(); continue; }
            DrawBinding(project, binding);
            ImGui.Separator();
            ImGui.PopID();
        }
    }

    private void DrawBinding(EditorProjectContext project, InputBinding binding)
    {
        switch (binding.Type)
        {
            case InputBindingType.KeyboardKey:
                CaptureButton(binding, "Key", binding.Key.ToString());
                break;
            case InputBindingType.MouseButton:
                CaptureButton(binding, "MouseButton", binding.MouseButton.ToString());
                break;
            case InputBindingType.KeyboardAxis:
                CaptureButton(binding, "NegativeKey", "Negative: " + binding.NegativeKey);
                CaptureButton(binding, "PositiveKey", "Positive: " + binding.PositiveKey);
                break;
            case InputBindingType.Keyboard2DComposite:
                CaptureButton(binding, "UpKey", "Up: " + binding.UpKey);
                CaptureButton(binding, "DownKey", "Down: " + binding.DownKey);
                CaptureButton(binding, "LeftKey", "Left: " + binding.LeftKey);
                CaptureButton(binding, "RightKey", "Right: " + binding.RightKey);
                break;
            case InputBindingType.GamepadButton:
            case InputBindingType.GamepadStick:
            case InputBindingType.GamepadTrigger:
                int control = (int)binding.GamepadControl;
                string[] controls = Enum.GetNames<GamepadControl>();
                if (ImGui.Combo("Control", ref control, controls, controls.Length)) { binding.GamepadControl = (GamepadControl)control; Commit(project); }
                break;
        }

        if (binding.Type is InputBindingType.GamepadStick or InputBindingType.GamepadTrigger)
        {
            float deadzone = binding.Deadzone;
            if (ImGui.SliderFloat("Deadzone", ref deadzone, 0f, .95f)) { binding.Deadzone = deadzone; Commit(project); }
        }
        Vector2 scale = binding.Scale;
        if (ImGui.DragFloat2("Scale", ref scale, .05f)) { binding.Scale = scale; Commit(project); }

        if (_captureBindingId == binding.Id)
        {
            ImGui.TextColored(new Vector4(.3f, .8f, 1f, 1f), "Press a key or mouse button... (Escape cancels)");
            if (_captureDelayFrames > 0)
            {
                _captureDelayFrames--;
            }
            else if (TryCapture(_captureSlot == "MouseButton", out Key key, out MouseButton mouse, out bool isMouse, out bool cancel))
            {
                if (!cancel)
                {
                    if (_captureSlot == "MouseButton" && isMouse)
                    {
                        binding.MouseButton = mouse;
                        Commit(project);
                    }
                    else if (!isMouse && _captureSlot != "MouseButton")
                    {
                        SetKeySlot(binding, _captureSlot, key);
                        Commit(project);
                    }
                }

                _captureBindingId = Guid.Empty;
                _captureSlot = string.Empty;
            }
        }
    }

    private void CaptureButton(InputBinding binding, string slot, string label)
    {
        if (ImGui.Button(label + "##" + slot)) BeginCapture(binding, slot);
    }

    private void BeginCapture(InputBinding binding, string slot)
    {
        _captureBindingId = binding.Id;
        _captureSlot = slot;
        _captureDelayFrames = 1;
    }

    private static bool TryCapture(bool captureMouse, out Key key, out MouseButton mouse, out bool isMouse, out bool cancel)
    {
        key = Key.Space;
        mouse = MouseButton.Left;
        isMouse = false;
        cancel = false;

        if (Input.IsKeyPressed(Key.Escape))
        {
            cancel = true;
            return true;
        }

        if (!captureMouse)
        {
            foreach (Key candidate in Enum.GetValues<Key>())
            {
                if (candidate == Key.Escape) continue;
                if (Input.IsKeyPressed(candidate))
                {
                    key = candidate;
                    return true;
                }
            }
        }
        else
        {
            foreach (MouseButton candidate in Enum.GetValues<MouseButton>())
            {
                if (Input.IsMouseButtonPressed(candidate))
                {
                    mouse = candidate;
                    isMouse = true;
                    return true;
                }
            }
        }

        return false;
    }

    private static void SetKeySlot(InputBinding binding, string slot, Key key)
    {
        switch (slot)
        {
            case "Key": binding.Key = key; break;
            case "NegativeKey": binding.NegativeKey = key; break;
            case "PositiveKey": binding.PositiveKey = key; break;
            case "UpKey": binding.UpKey = key; break;
            case "DownKey": binding.DownKey = key; break;
            case "LeftKey": binding.LeftKey = key; break;
            case "RightKey": binding.RightKey = key; break;
        }
    }

    private void DrawAddActionPopup(EditorProjectContext project)
    {
        if (_addActionPopup.ConsumeOpenRequest()) ImGui.OpenPopup("Add Input Action");
        bool visible = true;
        if (ImGui.BeginPopupModal("Add Input Action", ref visible, ImGuiWindowFlags.AlwaysAutoResize))
        {
            _addActionPopup.MarkVisible();
            if (_addActionPopup.ConsumeFocusRequest()) ImGui.SetKeyboardFocusHere();
            ImGui.InputText("Name", ref _newActionName, 128);
            int type = (int)_newActionType;
            string[] names = Enum.GetNames<InputActionType>();
            if (ImGui.Combo("Type", ref type, names, names.Length)) _newActionType = (InputActionType)type;
            bool duplicate = project.Project.InputMap.Find(_newActionName) != null;
            if (duplicate) ImGui.TextColored(new Vector4(1f, .65f, .2f, 1f), "An action with this name already exists.");
            if (ImGui.Button("Add") && !duplicate && !string.IsNullOrWhiteSpace(_newActionName))
            {
                InputActionDefinition action = project.Project.InputMap.Add(_newActionName, _newActionType);
                _selectedActionId = action.Id;
                Commit(project); ImGui.CloseCurrentPopup(); _addActionPopup.Reset();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel")) { ImGui.CloseCurrentPopup(); _addActionPopup.Reset(); }
            ImGui.EndPopup();
        }
        else _addActionPopup.RecoverWhenNotVisible();
    }

    private void DrawDeleteActionPopup(EditorProjectContext project)
    {
        if (_deleteActionPopup.ConsumeOpenRequest()) ImGui.OpenPopup("Delete Input Action?");
        bool visible = true;
        if (ImGui.BeginPopupModal("Delete Input Action?", ref visible, ImGuiWindowFlags.AlwaysAutoResize))
        {
            _deleteActionPopup.MarkVisible();
            ImGui.Text("Components using this action will safely resolve to zero until reassigned.");
            if (ImGui.Button("Delete"))
            {
                project.Project.InputMap.Remove(_deleteActionId); _selectedActionId = Guid.Empty; Commit(project);
                ImGui.CloseCurrentPopup(); _deleteActionPopup.Reset();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel")) { ImGui.CloseCurrentPopup(); _deleteActionPopup.Reset(); }
            ImGui.EndPopup();
        }
        else _deleteActionPopup.RecoverWhenNotVisible();
    }

    private static InputBinding CreateBinding(InputBindingType type) => type switch
    {
        InputBindingType.KeyboardKey => InputBinding.KeyButton(Key.Space),
        InputBindingType.MouseButton => InputBinding.MouseButtonBinding(MouseButton.Left),
        InputBindingType.KeyboardAxis => InputBinding.Axis(Key.A, Key.D),
        InputBindingType.Keyboard2DComposite => InputBinding.Composite(Key.W, Key.S, Key.A, Key.D),
        _ => new InputBinding { Type = type }
    };

    private static IEnumerable<InputBindingType> SensibleBindings(InputActionType type) => type switch
    {
        InputActionType.Button => new[] { InputBindingType.KeyboardKey, InputBindingType.MouseButton,
            InputBindingType.GamepadButton, InputBindingType.GamepadTrigger },
        InputActionType.Axis1D => new[] { InputBindingType.KeyboardAxis, InputBindingType.MouseWheel,
            InputBindingType.GamepadTrigger },
        _ => new[] { InputBindingType.Keyboard2DComposite, InputBindingType.MouseDelta, InputBindingType.GamepadStick }
    };

    private static bool Equivalent(InputBinding a, InputBinding b) => a.Type == b.Type && a.Type switch
    {
        InputBindingType.KeyboardKey => a.Key == b.Key,
        InputBindingType.MouseButton => a.MouseButton == b.MouseButton,
        InputBindingType.KeyboardAxis => a.NegativeKey == b.NegativeKey && a.PositiveKey == b.PositiveKey,
        InputBindingType.Keyboard2DComposite => a.UpKey == b.UpKey && a.DownKey == b.DownKey && a.LeftKey == b.LeftKey && a.RightKey == b.RightKey,
        InputBindingType.GamepadButton or InputBindingType.GamepadStick or InputBindingType.GamepadTrigger => a.GamepadControl == b.GamepadControl,
        _ => true
    };

    private void DrawAddTagPopup(EditorProjectContext project)
    {
        if (_addTagPopup.ConsumeOpenRequest()) ImGui.OpenPopup("Add Tag");
        bool visible = true;
        if (ImGui.BeginPopupModal("Add Tag", ref visible, ImGuiWindowFlags.AlwaysAutoResize))
        {
            _addTagPopup.MarkVisible();
            ImGui.TextUnformatted("Tag Name:");
            if (_addTagPopup.ConsumeFocusRequest()) ImGui.SetKeyboardFocusHere();
            ImGui.InputText("##NewTagName", ref _newTagName, 96);
            bool duplicate = project.Project.Classification.FindTag(_newTagName) != null;
            if (duplicate) ImGui.TextColored(new Vector4(1f, .65f, .2f, 1f), "A tag with this name already exists.");
            if (ImGui.Button("Create") && !duplicate && project.Project.Classification.AddTag(_newTagName) != null)
            {
                Commit(project); ImGui.CloseCurrentPopup(); _addTagPopup.Reset();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel")) { ImGui.CloseCurrentPopup(); _addTagPopup.Reset(); }
            ImGui.EndPopup();
        }
        else _addTagPopup.RecoverWhenNotVisible();
    }

    private static int CountReferences(string root, Guid tagId)
    {
        string needle = tagId.ToString();
        return ProjectAssets(root).Sum(path => Regex.Matches(File.ReadAllText(path), Regex.Escape(needle), RegexOptions.IgnoreCase).Count);
    }

    private static int CountLayerReferences(string root, int layer) =>
        ProjectAssets(root).Sum(path => Regex.Matches(File.ReadAllText(path),
            $"\"layer\"\\s*:\\s*{layer}(?!\\d)", RegexOptions.IgnoreCase).Count);

    private static IEnumerable<string> ProjectAssets(string root) =>
        Directory.EnumerateFiles(root, "*.bytescene", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(root, "*.byteblueprint", SearchOption.AllDirectories));

    private static void Commit(EditorProjectContext project)
    {
        project.Project.InputMap.EnsureValid();
        InputActions.Configure(project.Project.InputMap);
        project.Project.Classification.EnsureValid();
        project.SaveProject();
    }
}
