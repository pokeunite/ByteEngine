using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;

using ImGuiNET;

namespace ByteEngine.Editor;

/// <summary>
/// C8D first unified Animation Profile authoring surface.
///
/// The profile is edited from the AnimationController inspector so users do not
/// need separate Movement Blend / Aim / IK / Action components. Later milestones
/// expand these same four sections instead of scattering animation setup.
/// </summary>
internal static class AnimationProfileInspector
{
    public static void Draw(
        AnimationController controller,
        EditorProjectContext project,
        PropertyEditorContext context,
        Action begin,
        Action changed,
        Action end)
    {
        ImGui.SeparatorText("ANIMATION PROFILE");

        if (controller.AnimationProfile == null ||
            controller.AnimationProfile.IsEmpty)
        {
            ImGui.TextWrapped(
                "No Animation Profile assigned. Create one to keep rig, locomotion, actions and procedural animation in one place.");

            if (context == PropertyEditorContext.Runtime)
            {
                return;
            }

            if (ImGui.Button("Create Animation Profile"))
            {
                begin();

                if (TryCreateAndAssign(
                        controller,
                        project,
                        out string? error))
                {
                    changed();
                }
                else if (!string.IsNullOrWhiteSpace(error))
                {
                    Console.WriteLine(
                        $"Could not create Animation Profile: {error}");
                }

                end();
            }

            return;
        }

        AssetRecord? asset =
            project.AssetDatabase.Resolve(
                controller.AnimationProfile);

        if (asset == null ||
            asset.Type != AssetType.AnimationProfile)
        {
            ImGui.TextColored(
                new System.Numerics.Vector4(1.0f, 0.55f, 0.2f, 1.0f),
                "Assigned Animation Profile is missing or invalid.");
            return;
        }

        AnimationProfile profile;

        try
        {
            profile =
                project.Assets.LoadAnimationProfile(
                    controller.AnimationProfile);
        }
        catch (Exception exception)
        {
            ImGui.TextWrapped(
                $"Could not load profile: {exception.Message}");
            return;
        }

        ImGui.TextDisabled(
            asset.ProjectPath);

        bool editable =
            context != PropertyEditorContext.Runtime;

        bool profileChanged =
            false;

        if (ImGui.BeginTabBar(
                $"AnimationProfileTabs##{asset.Guid}"))
        {
            if (ImGui.BeginTabItem("Rig"))
            {
                profileChanged |=
                    DrawRig(
                        profile,
                        project,
                        editable);

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Locomotion"))
            {
                profileChanged |=
                    DrawLocomotion(
                        controller,
                        profile,
                        project,
                        editable);

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Actions"))
            {
                profileChanged |=
                    DrawActions(
                        controller,
                        profile,
                        project,
                        editable);

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Procedural"))
            {
                profileChanged |=
                    DrawProcedural(
                        profile,
                        editable);

                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        if (!editable ||
            !profileChanged)
        {
            return;
        }

        begin();

        try
        {
            profile.Normalize();

            AnimationProfileSerializer.Save(
                asset.FullPath,
                profile);

            controller.ApplyAnimationProfile();
            project.AssetDatabase.RequestRefresh();

            changed();
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                $"Could not save Animation Profile '{asset.ProjectPath}': {exception.Message}");
        }

        end();
    }

    private static bool DrawRig(
        AnimationProfile profile,
        EditorProjectContext project,
        bool editable)
    {
        bool changed =
            false;

        ImGui.BeginDisabled(
            !editable);

        int rigType =
            (int)profile.Rig.Type;

        string[] rigNames =
            Enum.GetNames<AnimationRigType>();

        if (ImGui.Combo(
                "Rig Type",
                ref rigType,
                rigNames,
                rigNames.Length))
        {
            profile.Rig.Type =
                (AnimationRigType)rigType;

            changed =
                true;
        }

        AssetReference model =
            profile.Rig.ReferenceModel ??
            AssetReference.Empty;

        if (DrawAssetPicker(
                "Reference Model",
                AssetType.Model3D,
                project,
                ref model))
        {
            profile.Rig.ReferenceModel =
                model;

            changed =
                true;
        }

        ImGui.EndDisabled();

        if (profile.Rig.Type ==
            AnimationRigType.Humanoid)
        {
            ImGui.TextWrapped(
                "Humanoid mapping/validation arrives in C9. This model will become the reference skeleton for that setup.");
        }
        else
        {
            ImGui.TextDisabled(
                "Generic preserves the imported skeleton as authored.");
        }

        return changed;
    }

    private static bool DrawLocomotion(
        AnimationController controller,
        AnimationProfile profile,
        EditorProjectContext project,
        bool editable)
    {
        AnimationLocomotionProfile locomotion =
            profile.Locomotion;

        bool changed =
            false;

        ImGui.BeginDisabled(
            !editable);

        bool enabled =
            locomotion.Enabled;

        if (ImGui.Checkbox(
                "Enabled",
                ref enabled))
        {
            locomotion.Enabled =
                enabled;

            changed =
                true;
        }

        bool drive =
            locomotion.DriveFromCharacterController;

        if (ImGui.Checkbox(
                "Automatic Speed / State",
                ref drive))
        {
            locomotion.DriveFromCharacterController =
                drive;

            changed =
                true;
        }

        IReadOnlyList<string> clips =
            AnimationClipDiscovery.GetClipNames(
                controller,
                project);

        string idle = locomotion.Idle;
        if (DrawClipPicker(
                "Idle",
                clips,
                ref idle))
        {
            locomotion.Idle = idle;
            changed = true;
        }

        string walk = locomotion.Walk;
        if (DrawClipPicker(
                "Walk",
                clips,
                ref walk))
        {
            locomotion.Walk = walk;
            changed = true;
        }

        string run = locomotion.Run;
        if (DrawClipPicker(
                "Run",
                clips,
                ref run))
        {
            locomotion.Run = run;
            changed = true;
        }

        string jump = locomotion.Jump;
        if (DrawClipPicker(
                "Jump",
                clips,
                ref jump))
        {
            locomotion.Jump = jump;
            changed = true;
        }

        string fall = locomotion.Fall;
        if (DrawClipPicker(
                "Fall",
                clips,
                ref fall))
        {
            locomotion.Fall = fall;
            changed = true;
        }

        string land = locomotion.Land;
        if (DrawClipPicker(
                "Land",
                clips,
                ref land))
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
                Math.Max(
                    runThreshold,
                    0.0f);

            changed =
                true;
        }

        float blend =
            locomotion.TransitionDuration;

        if (ImGui.DragFloat(
                "Blend Smoothness",
                ref blend,
                0.01f,
                0.0f,
                5.0f))
        {
            locomotion.TransitionDuration =
                Math.Max(
                    blend,
                    0.0f);

            changed =
                true;
        }

        float speed =
            locomotion.PlaybackSpeed;

        if (ImGui.DragFloat(
                "Playback Speed",
                ref speed,
                0.01f,
                0.0f,
                10.0f))
        {
            locomotion.PlaybackSpeed =
                Math.Max(
                    speed,
                    0.0f);

            changed =
                true;
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

            changed =
                true;
        }

        ImGui.EndDisabled();

        return changed;
    }

    private static bool DrawActions(
        AnimationController controller,
        AnimationProfile profile,
        EditorProjectContext project,
        bool editable)
    {
        bool changed =
            false;

        IReadOnlyList<string> clips =
            AnimationClipDiscovery.GetClipNames(
                controller,
                project);

        ImGui.BeginDisabled(
            !editable);

        if (ImGui.Button(
                "+ Add Action"))
        {
            profile.Actions.Add(
                new AnimationActionProfile());

            changed =
                true;
        }

        int removeIndex =
            -1;

        for (int index = 0;
             index < profile.Actions.Count;
             index++)
        {
            AnimationActionProfile action =
                profile.Actions[index];

            ImGui.PushID(
                $"AnimationAction:{index}");

            string heading =
                string.IsNullOrWhiteSpace(action.Name)
                    ? $"Action {index + 1}"
                    : action.Name;

            bool open =
                ImGui.TreeNodeEx(
                    heading,
                    ImGuiTreeNodeFlags.DefaultOpen);

            ImGui.SameLine();

            if (ImGui.SmallButton(
                    "Remove"))
            {
                removeIndex =
                    index;
            }

            if (open)
            {
                string name =
                    action.Name ?? string.Empty;

                if (ImGui.InputText(
                        "Name",
                        ref name,
                        128))
                {
                    action.Name =
                        name;

                    changed =
                        true;
                }

                string actionClip =
                    action.Clip;

                if (DrawClipPicker(
                        "Clip",
                        clips,
                        ref actionClip))
                {
                    action.Clip =
                        actionClip;

                    changed =
                        true;
                }

                bool loop =
                    action.Loop;

                if (ImGui.Checkbox(
                        "Loop",
                        ref loop))
                {
                    action.Loop =
                        loop;

                    changed =
                        true;
                }

                float blendIn =
                    action.BlendIn;

                if (ImGui.DragFloat(
                        "Blend In",
                        ref blendIn,
                        0.01f,
                        0.0f,
                        5.0f))
                {
                    action.BlendIn =
                        Math.Max(
                            blendIn,
                            0.0f);

                    changed =
                        true;
                }

                float blendOut =
                    action.BlendOut;

                if (ImGui.DragFloat(
                        "Blend Out",
                        ref blendOut,
                        0.01f,
                        0.0f,
                        5.0f))
                {
                    action.BlendOut =
                        Math.Max(
                            blendOut,
                            0.0f);

                    changed =
                        true;
                }

                ImGui.TreePop();
            }

            ImGui.PopID();
        }

        if (removeIndex >=
            0)
        {
            profile.Actions.RemoveAt(
                removeIndex);

            changed =
                true;
        }

        ImGui.EndDisabled();

        if (profile.Actions.Count ==
            0)
        {
            ImGui.TextDisabled(
                "No named actions yet. C11 expands these with sections, combos, layers and event windows.");
        }

        return changed;
    }

    private static bool DrawProcedural(
        AnimationProfile profile,
        bool editable)
    {
        bool changed =
            false;

        AnimationProceduralProfile procedural =
            profile.Procedural;

        ImGui.BeginDisabled(
            !editable);

        bool aim =
            procedural.AimEnabled;

        if (ImGui.Checkbox(
                "Aim",
                ref aim))
        {
            procedural.AimEnabled =
                aim;

            changed =
                true;
        }

        bool look =
            procedural.LookAtEnabled;

        if (ImGui.Checkbox(
                "Look At",
                ref look))
        {
            procedural.LookAtEnabled =
                look;

            changed =
                true;
        }

        bool footIk =
            procedural.FootIkEnabled;

        if (ImGui.Checkbox(
                "Foot IK",
                ref footIk))
        {
            procedural.FootIkEnabled =
                footIk;

            changed =
                true;
        }

        bool handIk =
            procedural.HandIkEnabled;

        if (ImGui.Checkbox(
                "Hand IK",
                ref handIk))
        {
            procedural.HandIkEnabled =
                handIk;

            changed =
                true;
        }

        ImGui.EndDisabled();

        ImGui.TextDisabled(
            "These switches reserve the unified workflow. Their solvers are implemented in later animation milestones.");

        return changed;
    }

    private static bool DrawClipPicker(
        string label,
        IReadOnlyList<string> clips,
        ref string value)
    {
        value ??=
            string.Empty;

        string currentValue =
            value;

        bool exists =
            clips.Any(
                clip =>
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

        bool changed =
            false;

        if (ImGui.BeginCombo(
                label,
                preview))
        {
            if (ImGui.Selectable(
                    "None",
                    string.IsNullOrWhiteSpace(value)))
            {
                value =
                    string.Empty;

                changed =
                    true;
            }

            foreach (string clip
                     in clips)
            {
                bool selected =
                    string.Equals(
                        clip,
                        value,
                        StringComparison.OrdinalIgnoreCase);

                if (ImGui.Selectable(
                        clip,
                        selected))
                {
                    value =
                        clip;

                    changed =
                        true;
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        return changed;
    }

    private static bool DrawAssetPicker(
        string label,
        AssetType type,
        EditorProjectContext project,
        ref AssetReference reference)
    {
        AssetRecord? current =
            reference.IsEmpty
                ? null
                : project.AssetDatabase.Resolve(
                    reference);

        string preview =
            current?.ProjectPath ??
            reference.CachedProjectPath ??
            "None";

        bool changed =
            false;

        if (ImGui.BeginCombo(
                label,
                preview))
        {
            if (ImGui.Selectable(
                    "None",
                    reference.IsEmpty))
            {
                reference =
                    AssetReference.Empty;

                changed =
                    true;
            }

            foreach (AssetRecord asset
                     in project.AssetDatabase.Assets
                         .Where(asset => asset.Type == type)
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

                    changed =
                        true;
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        return changed;
    }

    private static bool TryCreateAndAssign(
        AnimationController controller,
        EditorProjectContext project,
        out string? error)
    {
        error =
            null;

        try
        {
            string assetRoot =
                Path.Combine(
                    project.ProjectRoot,
                    project.Project.AssetDirectory.Replace(
                        '/',
                        Path.DirectorySeparatorChar));

            string profileDirectory =
                Path.Combine(
                    assetRoot,
                    "Animation");

            Directory.CreateDirectory(
                profileDirectory);

            string objectName =
                SanitizeFileName(
                    controller.GameObject.Name);

            if (string.IsNullOrWhiteSpace(objectName))
            {
                objectName =
                    "Character";
            }

            string baseName =
                objectName +
                "Animation";

            string path =
                Path.Combine(
                    profileDirectory,
                    baseName +
                    AnimationProfileSerializer.FileExtension);

            int suffix =
                2;

            while (File.Exists(path))
            {
                path =
                    Path.Combine(
                        profileDirectory,
                        $"{baseName}{suffix}{AnimationProfileSerializer.FileExtension}");

                suffix++;
            }

            var profile =
                new AnimationProfile
                {
                    Name =
                        Path.GetFileNameWithoutExtension(
                            path),
                    Locomotion =
                        new AnimationLocomotionProfile
                        {
                            Enabled =
                                controller.DriveLocomotion,
                            DriveFromCharacterController =
                                controller.DriveLocomotion,
                            Idle =
                                controller.Idle,
                            Walk =
                                controller.Walk,
                            Run =
                                controller.Run,
                            Jump =
                                controller.Jump,
                            Fall =
                                controller.Fall,
                            Land =
                                controller.Land,
                            RunThreshold =
                                controller.RunThreshold,
                            TransitionDuration =
                                controller.TransitionDuration,
                            PlaybackSpeed =
                                controller.PlaybackSpeed,
                            RootMotionMode =
                                controller.RootMotionMode
                        }
                };

            if (AnimationClipDiscovery.TryGetModel(
                    controller,
                    project,
                    out _,
                    out AssetReference modelReference))
            {
                profile.Rig.ReferenceModel =
                    modelReference;
            }

            AnimationProfileSerializer.Save(
                path,
                profile);

            project.AssetDatabase.Scan();

            string projectPath =
                Path.GetRelativePath(
                        project.ProjectRoot,
                        path)
                    .Replace(
                        '\\',
                        '/');

            if (!project.AssetDatabase.TryGetAsset(
                    projectPath,
                    out AssetRecord? record) ||
                record == null ||
                record.Type != AssetType.AnimationProfile)
            {
                error =
                    "The profile file was created but the Asset Database did not register it.";

                return false;
            }

            controller.AnimationProfile =
                new AssetReference(
                    record.Guid,
                    record.ProjectPath);

            controller.ApplyAnimationProfile();

            return true;
        }
        catch (Exception exception)
        {
            error =
                exception.Message;

            return false;
        }
    }

    private static string SanitizeFileName(
        string value)
    {
        char[] invalid =
            Path.GetInvalidFileNameChars();

        return new string(
            value
                .Select(
                    character =>
                        invalid.Contains(character)
                            ? '_'
                            : character)
                .ToArray())
            .Trim();
    }
}
