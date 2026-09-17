using System.Numerics;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics.ThreeD;

using ImGuiNET;

namespace ByteEngine.Editor.Panels;

/// <summary>
/// Dedicated authoring workspace for .byteanim assets.
///
/// Editing is kept out of AnimationController so characters only reference a
/// profile. Changes stay in memory until Save, avoiding AssetDatabase scans and
/// hot reload work on every ImGui edit frame.
/// </summary>
internal sealed class AnimationProfileWorkspacePanel
{
    private AssetRecord? _asset;
    private EditorProjectContext? _project;
    private AnimationProfile? _profile;
    private bool _visible;
    private bool _focusNextDraw;
    private bool _dirty;
    private string? _loadError;

    public void Open(
        AssetRecord asset,
        EditorProjectContext project,
        EditorLog log)
    {
        if (asset.Type != AssetType.AnimationProfile)
        {
            return;
        }

        _asset = asset;
        _project = project;
        _visible = true;
        _focusNextDraw = true;
        _dirty = false;
        _loadError = null;

        try
        {
            _profile =
                AnimationProfileSerializer.Load(
                    asset.FullPath);
        }
        catch (Exception exception)
        {
            _profile = null;
            _loadError = exception.Message;
            log.Error(
                $"Could not open Animation Profile '{asset.ProjectPath}': {exception.Message}");
        }
    }

    public void Draw(
        EditorLog log)
    {
        if (!_visible ||
            _asset == null ||
            _project == null)
        {
            return;
        }

        if (_focusNextDraw)
        {
            ImGui.SetNextWindowFocus();
            _focusNextDraw = false;
        }

        string name =
            Path.GetFileNameWithoutExtension(
                _asset.ProjectPath);

        bool open = true;

        bool visible =
            ImGui.Begin(
                $"Animation Profile: {name}###AnimationProfileWorkspace",
                ref open,
                ImGuiWindowFlags.MenuBar);

        if (visible)
        {
            DrawMenuBar(log);
            DrawHeader(log);

            if (_profile == null)
            {
                ImGui.TextColored(
                    new Vector4(
                        1.0f,
                        0.38f,
                        0.32f,
                        1.0f),
                    "Animation Profile could not be loaded.");

                if (!string.IsNullOrWhiteSpace(_loadError))
                {
                    ImGui.TextWrapped(_loadError);
                }

                if (ImGui.Button("Retry Load"))
                {
                    Reload(log);
                }
            }
            else
            {
                DrawProfileTabs();
            }
        }

        ImGui.End();

        if (!open)
        {
            if (_dirty)
            {
                Save(log);
            }

            _visible = false;
        }
    }

    private void DrawMenuBar(
        EditorLog log)
    {
        if (!ImGui.BeginMenuBar())
        {
            return;
        }

        ImGui.BeginDisabled(
            !_dirty ||
            _profile == null);

        if (ImGui.MenuItem(
                "Save",
                "Ctrl+S"))
        {
            Save(log);
        }

        ImGui.EndDisabled();

        if (ImGui.MenuItem("Revert"))
        {
            Reload(log);
        }

        ImGui.Separator();

        if (ImGui.MenuItem("Close"))
        {
            if (_dirty)
            {
                Save(log);
            }

            _visible = false;
        }

        ImGui.EndMenuBar();
    }

    private void DrawHeader(
        EditorLog log)
    {
        if (_asset == null)
        {
            return;
        }

        ImGui.TextColored(
            EditorTheme.AccentHover,
            "ANIMATION PROFILE");

        ImGui.SameLine();

        ImGui.TextDisabled(
            _dirty
                ? "Unsaved changes"
                : "Saved");

        ImGui.TextDisabled(
            _asset.ProjectPath);

        ImGui.Spacing();

        ImGui.BeginDisabled(
            !_dirty ||
            _profile == null);

        if (ImGui.Button("Save"))
        {
            Save(log);
        }

        ImGui.EndDisabled();

        ImGui.SameLine();

        if (ImGui.Button("Revert"))
        {
            Reload(log);
        }

        if (ImGui.IsWindowFocused(
                ImGuiFocusedFlags.RootAndChildWindows) &&
            ImGui.GetIO().KeyCtrl &&
            ImGui.IsKeyPressed(ImGuiKey.S))
        {
            Save(log);
        }

        ImGui.Separator();
    }

    private void DrawProfileTabs()
    {
        if (_profile == null ||
            _project == null ||
            _asset == null)
        {
            return;
        }

        if (!ImGui.BeginTabBar(
                $"AnimationProfileEditorTabs##{_asset.Guid}"))
        {
            return;
        }

        if (ImGui.BeginTabItem("RIG"))
        {
            _dirty |= DrawRig();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("LOCOMOTION"))
        {
            _dirty |= DrawLocomotion();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("ACTIONS"))
        {
            _dirty |= DrawActions();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("LAYERS"))
        {
            DrawLayers();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("PROCEDURAL"))
        {
            _dirty |= DrawProcedural();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("DEBUG"))
        {
            DrawDebug();
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private bool DrawRig()
    {
        if (_profile == null ||
            _project == null)
        {
            return false;
        }

        bool changed = false;

        ImGui.SeparatorText("SKELETON RIG");

        int rigType =
            (int)_profile.Rig.Type;

        string[] rigNames =
            Enum.GetNames<AnimationRigType>();

        if (ImGui.Combo(
                "Rig Type",
                ref rigType,
                rigNames,
                rigNames.Length))
        {
            _profile.Rig.Type =
                (AnimationRigType)rigType;
            changed = true;
        }

        AssetReference model =
            _profile.Rig.ReferenceModel ??
            AssetReference.Empty;

        if (DrawAssetPicker(
                "Reference Model",
                AssetType.Model3D,
                ref model))
        {
            _profile.Rig.ReferenceModel = model;
            changed = true;
        }

        ImGui.Spacing();

        if (_profile.Rig.Type ==
            AnimationRigType.Humanoid)
        {
            ImGui.TextWrapped(
                "Humanoid semantic bone mapping and validation arrive in C9. The selected model is the reference character used by that system.");
        }
        else
        {
            ImGui.TextDisabled(
                "Generic uses the imported skeleton as authored.");
        }

        IReadOnlyList<string> clips =
            GetReferenceModelClipNames();

        ImGui.TextDisabled(
            clips.Count == 0
                ? "Reference model animation clips: none"
                : $"Reference model animation clips: {clips.Count}");

        return changed;
    }

    private bool DrawLocomotion()
    {
        if (_profile == null)
        {
            return false;
        }

        AnimationLocomotionProfile locomotion =
            _profile.Locomotion;

        bool changed = false;

        ImGui.SeparatorText("BASE LOCOMOTION");

        bool enabled = locomotion.Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled))
        {
            locomotion.Enabled = enabled;
            changed = true;
        }

        bool automatic =
            locomotion.DriveFromCharacterController;

        if (ImGui.Checkbox(
                "Automatic Speed / State",
                ref automatic))
        {
            locomotion.DriveFromCharacterController = automatic;
            changed = true;
        }

        IReadOnlyList<string> clips =
            GetReferenceModelClipNames();

        string idle = locomotion.Idle;
        if (DrawClipPicker("Idle", clips, ref idle))
        {
            locomotion.Idle = idle;
            changed = true;
        }

        string walk = locomotion.Walk;
        if (DrawClipPicker("Walk", clips, ref walk))
        {
            locomotion.Walk = walk;
            changed = true;
        }

        string run = locomotion.Run;
        if (DrawClipPicker("Run", clips, ref run))
        {
            locomotion.Run = run;
            changed = true;
        }

        string jump = locomotion.Jump;
        if (DrawClipPicker("Jump", clips, ref jump))
        {
            locomotion.Jump = jump;
            changed = true;
        }

        string fall = locomotion.Fall;
        if (DrawClipPicker("Fall", clips, ref fall))
        {
            locomotion.Fall = fall;
            changed = true;
        }

        string land = locomotion.Land;
        if (DrawClipPicker("Land", clips, ref land))
        {
            locomotion.Land = land;
            changed = true;
        }

        float runThreshold =
            locomotion.RunThreshold;

        if (ImGui.DragFloat(
                "Run Threshold",
                ref runThreshold,
                0.05f,
                0.0f,
                1000.0f))
        {
            locomotion.RunThreshold =
                Math.Max(runThreshold, 0.0f);
            changed = true;
        }

        float transition =
            locomotion.TransitionDuration;

        if (ImGui.DragFloat(
                "Blend Smoothness",
                ref transition,
                0.01f,
                0.0f,
                5.0f))
        {
            locomotion.TransitionDuration =
                Math.Max(transition, 0.0f);
            changed = true;
        }

        float playbackSpeed =
            locomotion.PlaybackSpeed;

        if (ImGui.DragFloat(
                "Playback Speed",
                ref playbackSpeed,
                0.01f,
                0.0f,
                10.0f))
        {
            locomotion.PlaybackSpeed =
                Math.Max(playbackSpeed, 0.0f);
            changed = true;
        }

        int rootMotion =
            (int)locomotion.RootMotionMode;

        string[] rootMotionNames =
            Enum.GetNames<RootMotionMode>();

        if (ImGui.Combo(
                "Root Motion",
                ref rootMotion,
                rootMotionNames,
                rootMotionNames.Length))
        {
            locomotion.RootMotionMode =
                (RootMotionMode)rootMotion;
            changed = true;
        }

        if (clips.Count == 0)
        {
            ImGui.TextWrapped(
                "Choose a Reference Model in RIG to populate the animation clip pickers.");
        }

        return changed;
    }

    private bool DrawActions()
    {
        if (_profile == null)
        {
            return false;
        }

        bool changed = false;
        IReadOnlyList<string> clips =
            GetReferenceModelClipNames();

        ImGui.SeparatorText("NAMED ACTIONS");

        if (ImGui.Button("+ Add Action"))
        {
            _profile.Actions.Add(
                new AnimationActionProfile());
            changed = true;
        }

        int removeIndex = -1;

        for (int index = 0;
             index < _profile.Actions.Count;
             index++)
        {
            AnimationActionProfile action =
                _profile.Actions[index];

            ImGui.PushID(
                $"ProfileAction:{index}");

            string title =
                string.IsNullOrWhiteSpace(action.Name)
                    ? $"Action {index + 1}"
                    : action.Name;

            bool open =
                ImGui.TreeNodeEx(
                    title,
                    ImGuiTreeNodeFlags.DefaultOpen);

            ImGui.SameLine();

            if (ImGui.SmallButton("Remove"))
            {
                removeIndex = index;
            }

            if (open)
            {
                string actionName =
                    action.Name ??
                    string.Empty;

                if (ImGui.InputText(
                        "Name",
                        ref actionName,
                        128))
                {
                    action.Name = actionName;
                    changed = true;
                }

                string actionClip =
                    action.Clip ??
                    string.Empty;

                if (DrawClipPicker(
                        "Clip",
                        clips,
                        ref actionClip))
                {
                    action.Clip = actionClip;
                    changed = true;
                }

                bool loop = action.Loop;
                if (ImGui.Checkbox("Loop", ref loop))
                {
                    action.Loop = loop;
                    changed = true;
                }

                float blendIn = action.BlendIn;
                if (ImGui.DragFloat(
                        "Blend In",
                        ref blendIn,
                        0.01f,
                        0.0f,
                        5.0f))
                {
                    action.BlendIn =
                        Math.Max(blendIn, 0.0f);
                    changed = true;
                }

                float blendOut = action.BlendOut;
                if (ImGui.DragFloat(
                        "Blend Out",
                        ref blendOut,
                        0.01f,
                        0.0f,
                        5.0f))
                {
                    action.BlendOut =
                        Math.Max(blendOut, 0.0f);
                    changed = true;
                }

                ImGui.TreePop();
            }

            ImGui.PopID();
        }

        if (removeIndex >= 0)
        {
            _profile.Actions.RemoveAt(
                removeIndex);
            changed = true;
        }

        if (_profile.Actions.Count == 0)
        {
            ImGui.TextDisabled(
                "Actions are named one-shot/override animations. C11 expands this area with sections, combos, event windows and targeting.");
        }

        return changed;
    }

    private static void DrawLayers()
    {
        ImGui.SeparatorText("ANIMATION LAYERS");
        ImGui.TextWrapped(
            "Layer definitions and body-region masks arrive in C13. They will live here instead of becoming separate character components.");
        ImGui.BulletText("Full Body");
        ImGui.BulletText("Upper Body");
        ImGui.BulletText("Lower Body");
        ImGui.BulletText("Arms / Head / Custom regions");
    }

    private bool DrawProcedural()
    {
        if (_profile == null)
        {
            return false;
        }

        AnimationProceduralProfile procedural =
            _profile.Procedural;

        bool changed = false;

        ImGui.SeparatorText("PROCEDURAL POSE FEATURES");

        bool aim = procedural.AimEnabled;
        if (ImGui.Checkbox("Aim", ref aim))
        {
            procedural.AimEnabled = aim;
            changed = true;
        }

        bool lookAt = procedural.LookAtEnabled;
        if (ImGui.Checkbox("Look At", ref lookAt))
        {
            procedural.LookAtEnabled = lookAt;
            changed = true;
        }

        bool footIk = procedural.FootIkEnabled;
        if (ImGui.Checkbox("Foot IK", ref footIk))
        {
            procedural.FootIkEnabled = footIk;
            changed = true;
        }

        bool handIk = procedural.HandIkEnabled;
        if (ImGui.Checkbox("Hand IK", ref handIk))
        {
            procedural.HandIkEnabled = handIk;
            changed = true;
        }

        ImGui.TextDisabled(
            "C8 stores these authoring switches only. Aim and IK solvers arrive in C14-C15.");

        return changed;
    }

    private void DrawDebug()
    {
        if (_profile == null ||
            _asset == null)
        {
            return;
        }

        IReadOnlyList<string> clips =
            GetReferenceModelClipNames();

        ImGui.SeparatorText("PROFILE STATUS");
        ImGui.Text($"Version: {_profile.Version}");
        ImGui.Text($"Actions: {_profile.Actions.Count}");
        ImGui.Text($"Reference clips: {clips.Count}");
        ImGui.Text($"Rig: {_profile.Rig.Type}");
        ImGui.TextDisabled($"GUID: {_asset.Guid}");
        ImGui.TextDisabled(_asset.ProjectPath);

        string[] duplicateActions =
            _profile.Actions
                .Where(action =>
                    !string.IsNullOrWhiteSpace(action.Name))
                .GroupBy(
                    action => action.Name,
                    StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();

        if (duplicateActions.Length > 0)
        {
            ImGui.TextColored(
                new Vector4(
                    1.0f,
                    0.67f,
                    0.25f,
                    1.0f),
                $"Duplicate action name(s): {string.Join(", ", duplicateActions)}");
        }
        else
        {
            ImGui.TextDisabled("No duplicate action names detected.");
        }
    }

    private IReadOnlyList<string> GetReferenceModelClipNames()
    {
        if (_profile == null ||
            _project == null ||
            _profile.Rig.ReferenceModel == null ||
            _profile.Rig.ReferenceModel.IsEmpty)
        {
            return Array.Empty<string>();
        }

        try
        {
            ModelAsset model =
                _project.Assets.LoadModel(
                    _profile.Rig.ReferenceModel);

            return model.Animations
                .Select(animation =>
                    string.IsNullOrWhiteSpace(animation.Name)
                        ? animation.Key
                        : animation.Name)
                .Where(name =>
                    !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name =>
                    name,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private bool DrawAssetPicker(
        string label,
        AssetType expectedType,
        ref AssetReference reference)
    {
        if (_project == null)
        {
            return false;
        }

        AssetRecord? current =
            reference.IsEmpty
                ? null
                : _project.AssetDatabase.Resolve(
                    reference);

        string preview =
            current?.ProjectPath ??
            reference.CachedProjectPath ??
            "None";

        bool changed = false;

        if (ImGui.BeginCombo(label, preview))
        {
            if (ImGui.Selectable(
                    "None",
                    reference.IsEmpty))
            {
                reference = AssetReference.Empty;
                changed = true;
            }

            foreach (AssetRecord asset
                     in _project.AssetDatabase.Assets
                         .Where(asset =>
                             asset.Type == expectedType)
                         .OrderBy(
                             asset => asset.ProjectPath,
                             StringComparer.OrdinalIgnoreCase))
            {
                bool selected =
                    asset.Guid != Guid.Empty &&
                    asset.Guid == reference.Guid;

                if (ImGui.Selectable(
                        asset.ProjectPath,
                        selected))
                {
                    reference =
                        new AssetReference(
                            asset.Guid,
                            asset.ProjectPath);
                    changed = true;
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        if (ImGui.BeginDragDropTarget())
        {
            Guid? id =
                AssetDragDrop.Accept();

            if (id.HasValue &&
                _project.AssetDatabase.TryGetAsset(
                    id.Value,
                    out AssetRecord? dropped) &&
                dropped?.Type == expectedType)
            {
                reference =
                    new AssetReference(
                        dropped.Guid,
                        dropped.ProjectPath);
                changed = true;
            }

            ImGui.EndDragDropTarget();
        }

        return changed;
    }

    private static bool DrawClipPicker(
        string label,
        IReadOnlyList<string> clips,
        ref string value)
    {
        value ??= string.Empty;

        string currentValue = value;

        bool exists =
            clips.Any(clip =>
                string.Equals(
                    clip,
                    currentValue,
                    StringComparison.OrdinalIgnoreCase));

        string preview =
            string.IsNullOrWhiteSpace(value)
                ? "None"
                : exists
                    ? value
                    : $"{value} (Missing)";

        bool changed = false;

        if (!ImGui.BeginCombo(label, preview))
        {
            return false;
        }

        if (ImGui.Selectable(
                "None",
                string.IsNullOrWhiteSpace(value)))
        {
            value = string.Empty;
            changed = true;
        }

        foreach (string clip in clips)
        {
            bool selected =
                string.Equals(
                    clip,
                    value,
                    StringComparison.OrdinalIgnoreCase);

            if (ImGui.Selectable(clip, selected))
            {
                value = clip;
                changed = true;
            }

            if (selected)
            {
                ImGui.SetItemDefaultFocus();
            }
        }

        ImGui.EndCombo();

        return changed;
    }

    private void Save(
        EditorLog log)
    {
        if (_profile == null ||
            _asset == null ||
            _project == null)
        {
            return;
        }

        try
        {
            _profile.Normalize();

            AnimationProfileSerializer.Save(
                _asset.FullPath,
                _profile);

            /*
             * One explicit scan per save keeps the AssetManager cache and live
             * references fresh without the per-control/per-frame churn that
             * made the first C8D editor feel slower.
             */
            _project.AssetDatabase.Scan();

            _dirty = false;
            _loadError = null;

            log.Info(
                $"Saved Animation Profile '{_asset.ProjectPath}'.");
        }
        catch (Exception exception)
        {
            _loadError = exception.Message;
            log.Error(
                $"Could not save Animation Profile '{_asset.ProjectPath}': {exception.Message}");
        }
    }

    private void Reload(
        EditorLog log)
    {
        if (_asset == null)
        {
            return;
        }

        try
        {
            _profile =
                AnimationProfileSerializer.Load(
                    _asset.FullPath);

            _dirty = false;
            _loadError = null;
        }
        catch (Exception exception)
        {
            _profile = null;
            _loadError = exception.Message;
            log.Error(
                $"Could not reload Animation Profile '{_asset.ProjectPath}': {exception.Message}");
        }
    }
}

/// <summary>
/// Lightweight request bridge used by AnimationController's inspector button.
/// The Asset Browser owns the actual workspace window.
/// </summary>
internal static class AnimationProfileWorkspaceRequest
{
    private static AssetReference? _pending;

    public static void Request(
        AssetReference reference)
    {
        if (reference == null ||
            reference.IsEmpty)
        {
            return;
        }

        _pending =
            new AssetReference(
                reference.Guid,
                reference.CachedProjectPath);
    }

    public static AssetReference? Consume()
    {
        AssetReference? pending =
            _pending;

        _pending = null;

        return pending;
    }
}
