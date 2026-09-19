using System.Numerics;
using System.Reflection;
using System.Text.RegularExpressions;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Classification;
using ByteEngine.Core.VisualLogic;
using ImGuiNET;

namespace ByteEngine.Editor;

internal enum PropertyEditorContext { Scene, Blueprint, Runtime }

internal sealed record ComponentPropertyDescriptor(
    PropertyInfo Property,
    PropertyMetadata Metadata)
{
    public bool ReadOnly =>
        Property.SetMethod?.IsPublic != true ||
        Metadata.ReadOnly;
}

/// <summary>
/// One descriptor list and widget pipeline for scene, Blueprint and runtime
/// component values.
/// </summary>
internal static class ComponentPropertyRenderer
{
    private static readonly Dictionary<Type, IReadOnlyList<ComponentPropertyDescriptor>> Cache =
        new();

    public static IReadOnlyList<ComponentPropertyDescriptor> Descriptors(
        Type type,
        PropertyEditorContext context)
    {
        if (Cache.TryGetValue(
                type,
                out IReadOnlyList<ComponentPropertyDescriptor>? cached))
        {
            return cached;
        }

        ComponentPropertyDescriptor[] result =
            type.GetProperties(
                    BindingFlags.Instance |
                    BindingFlags.Public)
                .Where(
                    property =>
                        property.DeclaringType != typeof(Component) &&
                        property.GetIndexParameters().Length == 0 &&
                        property.GetMethod?.IsPublic == true &&
                        Supported(property.PropertyType) &&
                        property.Name is not ("UpdateOrder" or "RenderOrder") &&
                        type != typeof(BlueprintInstance))
                .Select(
                    property =>
                        new ComponentPropertyDescriptor(
                            property,
                            ComponentMetadataRegistry.GetProperty(
                                type,
                                property.Name)
                            ?? new PropertyMetadata(
                                Regex.Replace(
                                    property.Name,
                                    "(?<=[a-z])(?=[A-Z])",
                                    " "),
                                "Properties",
                                property.Name)))
                .ToArray();

        Cache[type] =
            result;

        return result;
    }

    private static bool Supported(
        Type type) =>
        type == typeof(float) ||
        type == typeof(int) ||
        type == typeof(bool) ||
        type == typeof(string) ||
        type == typeof(Vector2) ||
        type == typeof(Vector3) ||
        type == typeof(Vector4) ||
        type == typeof(Guid) ||
        type == typeof(LayerMask) ||
        type == typeof(AssetReference) ||
        type == typeof(InputActionReference) ||
        type.IsEnum;

    public static bool SetValue(
        Component component,
        ComponentPropertyDescriptor descriptor,
        object value,
        PropertyEditorContext context)
    {
        if (descriptor.ReadOnly ||
            context == PropertyEditorContext.Runtime &&
            !descriptor.Metadata.RuntimeEditable)
        {
            return false;
        }

        if (value is float number)
        {
            var range =
                ComponentPropertyConstraints.Get(
                    component.GetType(),
                    descriptor.Property.Name);

            value =
                Math.Clamp(
                    float.IsFinite(number)
                        ? number
                        : 0,
                    range.Minimum,
                    range.Maximum);
        }

        descriptor.Property.SetValue(
            component,
            value);

        return true;
    }

    public static void Draw(
        Component component,
        PropertyEditorContext context,
        bool advanced,
        Action begin,
        Action changed,
        Action end,
        EditorProjectContext? project = null)
    {
        ImGui.PushID(
            component.GetHashCode());

        // C9.5 UX: profile owns locomotion. Do not show duplicate component
        // authoring fields that will be overwritten by the assigned profile.
        bool profileOwnsLocomotion =
            component is AnimationController profileDrivenController &&
            project != null &&
            HasValidAnimationProfile(
                profileDrivenController,
                project);

        foreach (ComponentPropertyDescriptor descriptor
                 in Descriptors(
                     component.GetType(),
                     context))
        {
            if (profileOwnsLocomotion &&
                IsAnimationProfileOwnedControllerProperty(
                    descriptor.Property.Name))
            {
                continue;
            }

            if (descriptor.Metadata.Advanced &&
                !advanced)
            {
                continue;
            }

            object? before =
                descriptor.Property.GetValue(
                    component);

            string label =
                descriptor.Metadata.DisplayName +
                "##" +
                descriptor.Property.Name;

            bool readOnly =
                descriptor.ReadOnly ||
                context == PropertyEditorContext.Runtime &&
                !descriptor.Metadata.RuntimeEditable;

            if (readOnly)
            {
                ImGui.TextDisabled(
                    $"{descriptor.Metadata.DisplayName}: {before ?? "None"}");

                continue;
            }

            object? after =
                before;

            bool edited =
                false;

            if (before is float floatValue)
            {
                var range =
                    ComponentPropertyConstraints.Get(
                        component.GetType(),
                        descriptor.Property.Name);

                edited =
                    ImGui.DragFloat(
                        label,
                        ref floatValue,
                        range.Speed,
                        range.Minimum,
                        range.Maximum);

                after =
                    floatValue;
            }
            else if (before is int intValue)
            {
                edited =
                    ImGui.DragInt(
                        label,
                        ref intValue);

                after =
                    intValue;
            }
            else if (before is bool boolValue)
            {
                edited =
                    ImGui.Checkbox(
                        label,
                        ref boolValue);

                after =
                    boolValue;
            }
            else if (before is Vector2 vector2)
            {
                edited =
                    ImGui.DragFloat2(
                        label,
                        ref vector2,
                        0.05f);

                after =
                    vector2;
            }
            else if (before is Vector3 vector3)
            {
                if (IsColorProperty(
                        component,
                        descriptor))
                {
                    edited =
                        ImGui.ColorEdit3(
                            label,
                            ref vector3,
                            ImGuiColorEditFlags.Float |
                            ImGuiColorEditFlags.HDR);
                }
                else
                {
                    edited =
                        ImGui.DragFloat3(
                            label,
                            ref vector3,
                            0.05f);
                }

                after =
                    vector3;
            }
            else if (before is Vector4 vector4)
            {
                edited =
                    ImGui.DragFloat4(
                        label,
                        ref vector4,
                        0.01f);

                after =
                    vector4;
            }
            else if (descriptor.Property.PropertyType.IsEnum)
            {
                string[] names =
                    Enum.GetNames(
                        descriptor.Property.PropertyType);

                Array values =
                    Enum.GetValues(
                        descriptor.Property.PropertyType);

                int index =
                    Array.IndexOf(
                        values.Cast<object>().ToArray(),
                        before);

                edited =
                    ImGui.Combo(
                        label,
                        ref index,
                        names,
                        names.Length);

                if (edited)
                {
                    after =
                        values.GetValue(
                            index);
                }
            }
            else if (descriptor.Property.PropertyType ==
                     typeof(AssetReference))
            {
                AssetReference reference =
                    before as AssetReference ??
                    AssetReference.Empty;

                if (project != null)
                {
                    edited =
                        DrawAssetReferenceSelector(
                            component,
                            descriptor,
                            project,
                            label,
                            ref reference);

                    after =
                        reference;
                }
                else
                {
                    ImGui.TextDisabled(
                        $"{descriptor.Metadata.DisplayName}: {reference}");
                }
            }
            else if (descriptor.Property.PropertyType ==
                     typeof(InputActionReference))
            {
                InputActionReference reference =
                    before as InputActionReference ??
                    new InputActionReference();

                InputMap map =
                    project?.Project.InputMap ??
                    InputActions.Map;

                InputActionDefinition? selected =
                    map.Resolve(
                        reference);

                string preview =
                    selected?.DisplayName ??
                    (string.IsNullOrWhiteSpace(reference.Name)
                        ? "None"
                        : reference.Name + " (Missing)");

                if (ImGui.BeginCombo(
                        label,
                        preview))
                {
                    foreach (InputActionDefinition action
                             in map.Actions)
                    {
                        bool isSelected =
                            action.Id ==
                            selected?.Id;

                        if (ImGui.Selectable(
                                action.DisplayName +
                                "##" +
                                action.Id,
                                isSelected))
                        {
                            after =
                                new InputActionReference(
                                    action.Id,
                                    action.DisplayName);

                            edited =
                                true;
                        }

                        if (isSelected)
                        {
                            ImGui.SetItemDefaultFocus();
                        }
                    }

                    ImGui.EndCombo();
                }
            }
            else if (descriptor.Property.PropertyType ==
                     typeof(LayerMask))
            {
                LayerMask mask =
                    before is LayerMask value
                        ? value
                        : LayerMask.All;

                edited =
                    ClassificationPickers.DrawLayerMask(
                        label,
                        project?.Project.Classification ??
                        component.GameObject.Scene?.Classification ??
                        ClassificationSettings.CreateDefault(),
                        ref mask);

                after =
                    mask;            }
            else if (descriptor.Property.PropertyType ==
                         typeof(Guid) &&
                     descriptor.Property.Name.EndsWith(
                         "TagId",
                         StringComparison.Ordinal))
            {
                Guid tag =
                    before is Guid value
                        ? value
                        : Guid.Empty;

                edited =
                    ClassificationPickers.DrawTag(
                        label,
                        project?.Project.Classification ??
                        component.GameObject.Scene?.Classification ??
                        ClassificationSettings.CreateDefault(),
                        ref tag);

                after =
                    tag;
            }
            else if (component is BoneSocket3D boneSocket &&
                     project != null &&
                     descriptor.Property.Name ==
                         nameof(BoneSocket3D.BoneName) &&
                     descriptor.Property.PropertyType ==
                         typeof(string))
            {
                string boneName =
                    before?.ToString() ??
                    string.Empty;

                edited =
                    DrawBoneSelector(
                        boneSocket,
                        project,
                        label,
                        ref boneName);

                after =
                    boneName;
            }
            else if (component is AnimationController clipController &&
                     project != null &&
                     descriptor.Property.PropertyType ==
                         typeof(string) &&
                     AnimationClipDiscovery.IsLocomotionClipProperty(
                         descriptor.Property.Name))
            {
                string clipName =
                    before?.ToString() ??
                    string.Empty;

                edited =
                    DrawAnimationClipSelector(
                        clipController,
                        project,
                        label,
                        ref clipName);

                after =
                    clipName;
            }
            else
            {
                string text =
                    before?.ToString() ??
                    string.Empty;

                edited =
                    ImGui.InputText(
                        label,
                        ref text,
                        1024);

                if (descriptor.Property.PropertyType ==
                    typeof(Guid))
                {
                    edited &=
                        Guid.TryParse(
                            text,
                            out _);

                    if (edited)
                    {
                        after =
                            Guid.Parse(
                                text);
                    }
                }
                else
                {
                    after =
                        text;
                }
            }

            bool active =
                ImGui.IsItemActive();

            if (ImGui.IsItemActivated() ||
                edited)
            {
                begin();
            }

            if (edited &&
                after != null &&
                SetValue(
                    component,
                    descriptor,
                    after,
                    context))
            {
                if (component is SpriteRenderer sprite &&
                    descriptor.Property.Name ==
                        nameof(SpriteRenderer.TextureReference) &&
                    project != null)
                {
                    sprite.Texture =
                        sprite.TextureReference == null ||
                        sprite.TextureReference.IsEmpty
                            ? null
                            : project.Assets.LoadTexture(
                                sprite.TextureReference);
                }

                if (component is AudioSource3D audioSource &&
                    descriptor.Property.Name ==
                        nameof(AudioSource3D.ClipReference) &&
                    project != null)
                {
                    AudioSerializationRegistrar.TryLoadClip(
                        audioSource,
                        audioSource.ClipReference,
                        project.AssetDatabase,
                        message =>
                            Console.WriteLine(
                                message));
                }

                if (component is SkyEnvironment environment &&
                    descriptor.Property.Name ==
                        nameof(SkyEnvironment.EnvironmentMapReference) &&
                    project != null)
                {
                    environment.SetEnvironmentMapTexture(
                        environment.EnvironmentMapReference.IsEmpty
                            ? null
                            : project.Assets.LoadTexture(
                                environment.EnvironmentMapReference));
                }

                if (component is AnimationController profileController &&
                    descriptor.Property.Name ==
                        nameof(AnimationController.AnimationProfile))
                {
                    profileController.ApplyAnimationProfile();
                }

                changed();
            }

            if (ImGui.IsItemDeactivatedAfterEdit() ||
                edited &&
                !active)
            {
                end();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(
                    descriptor.Metadata.Tooltip);
            }
        }

        if (component is PlayerController3D playerInput &&
            project != null)
        {
            DrawMissingActions(
                playerInput,
                project,
                context,
                begin,
                changed,
                end);
        }

        if (component is EventModuleComponent eventModules &&
            project != null)
        {
            DrawEventModules(
                eventModules,
                project,
                context,
                begin,
                changed,
                end);
        }

        if (component is AnimationController animationController &&
            project != null)
        {
            DrawAnimationControllerTools(
                animationController,
                project,
                context,
                begin,
                changed,
                end);
        }

        if (component is MeshRenderer mesh)
        {
            Vector4 color =
                mesh.Material.BaseColor;

            bool edited =
                ImGui.ColorEdit4(
                    "Base Color",
                    ref color);

            if (ImGui.IsItemActivated() ||
                edited)
            {
                begin();
            }

            if (edited)
            {
                mesh.Material.BaseColor =
                    color;

                changed();
            }

            if (ImGui.IsItemDeactivatedAfterEdit() ||
                edited &&
                !ImGui.IsItemActive())
            {
                end();
            }
        }

        if (component is CapsuleCollider3D &&
            project != null &&
            context != PropertyEditorContext.Runtime &&
            ImGui.Button("Fit To Visual"))
        {
            begin();

            if (CharacterCapsuleAutoFit.TryFit(
                    component.GameObject,
                    project.Assets,
                    out _))
            {
                changed();
            }

            end();
        }

        ImGui.PopID();
    }

    private static bool DrawAssetReferenceSelector(
        Component component,
        ComponentPropertyDescriptor descriptor,
        EditorProjectContext project,
        string label,
        ref AssetReference reference)
    {
        AssetType? expectedType =
            GetExpectedAssetType(
                component,
                descriptor.Property.Name);

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

        bool animationProfileField =
            component is AnimationController &&
            descriptor.Property.Name ==
                nameof(AnimationController.AnimationProfile);

        string comboLabel =
            animationProfileField
                ? $"##asset-picker:{descriptor.Property.Name}"
                : label;

        if (animationProfileField)
        {
            ImGui.TextUnformatted(
                descriptor.Metadata.DisplayName);

            float clearWidth =
                reference.IsEmpty
                    ? 0.0f
                    : ImGui.CalcTextSize("Clear").X +
                      ImGui.GetStyle().FramePadding.X * 2.0f;

            float comboWidth =
                ImGui.GetContentRegionAvail().X -
                (reference.IsEmpty
                    ? 0.0f
                    : clearWidth +
                      ImGui.GetStyle().ItemSpacing.X);

            ImGui.SetNextItemWidth(
                Math.Max(
                    comboWidth,
                    120.0f));
        }

        if (ImGui.BeginCombo(
                comboLabel,
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

            AssetRecord[] candidates =
                project.AssetDatabase.Assets
                    .Where(
                        asset =>
                            expectedType == null ||
                            asset.Type == expectedType.Value)
                    .OrderBy(
                        asset =>
                            asset.ProjectPath,
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray();

            if (candidates.Length ==
                0)
            {
                ImGui.TextDisabled(
                    expectedType.HasValue
                        ? $"No {expectedType.Value} assets found."
                        : "No compatible assets found.");
            }

            foreach (AssetRecord asset
                     in candidates)
            {
                bool selected =
                    asset.Guid != Guid.Empty &&
                    asset.Guid == reference.Guid;

                if (ImGui.Selectable(
                        $"{asset.ProjectPath}##asset-picker:{descriptor.Property.Name}:{asset.Guid}",
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

        if (ImGui.BeginDragDropTarget())
        {
            Guid? id =
                AssetDragDrop.Accept();

            if (id.HasValue &&
                project.AssetDatabase.TryGetAsset(                    id.Value,
                    out AssetRecord? dropped) &&
                dropped != null &&
                (!expectedType.HasValue ||
                 dropped.Type == expectedType.Value))
            {
                reference =
                    new AssetReference(
                        dropped.Guid,
                        dropped.ProjectPath);

                changed =
                    true;
            }

            ImGui.EndDragDropTarget();
        }

        if (!reference.IsEmpty)
        {
            ImGui.SameLine();

            if (ImGui.SmallButton(
                    $"Clear##asset-picker-clear:{descriptor.Property.Name}"))
            {
                reference =
                    AssetReference.Empty;

                changed =
                    true;
            }
        }

        return changed;
    }

    private static AssetType? GetExpectedAssetType(
        Component component,
        string propertyName)
    {
        if (component is AnimationController &&
            propertyName ==
                nameof(AnimationController.AnimationProfile))
        {
            return AssetType.AnimationProfile;
        }

        if (component is SpriteRenderer &&
            propertyName ==
                nameof(SpriteRenderer.TextureReference))
        {
            return AssetType.Texture2D;
        }

        if (component is AudioSource3D &&
            propertyName ==
                nameof(AudioSource3D.ClipReference))
        {
            return AssetType.AudioClip;
        }

        if (component is SkyEnvironment &&
            propertyName ==
                nameof(SkyEnvironment.EnvironmentMapReference))
        {
            return AssetType.Texture2D;
        }

        if (component is ModelHierarchyInstance &&
            propertyName ==
                nameof(ModelHierarchyInstance.Model))
        {
            return AssetType.Model3D;
        }

        if (component is SkeletalMeshRenderer &&
            propertyName ==
                nameof(SkeletalMeshRenderer.Model))
        {
            return AssetType.Model3D;
        }

        return null;
    }

    private static bool DrawBoneSelector(
        BoneSocket3D socket,
        EditorProjectContext project,
        string label,
        ref string boneName)
    {
        IReadOnlyList<string> bones =
            GetSocketBoneNames(
                socket,
                project);

        string currentBoneName =
            boneName;

        bool currentExists =
            bones.Any(
                candidate =>
                    string.Equals(
                        candidate,
                        currentBoneName,
                        StringComparison.OrdinalIgnoreCase));

        string preview =
            string.IsNullOrWhiteSpace(
                boneName)
                ? "Select Bone..."
                : currentExists
                    ? boneName
                    : $"{boneName} (Missing)";

        bool changed =
            false;

        if (!ImGui.BeginCombo(
                label,
                preview))
        {
            return false;
        }

        if (ImGui.Selectable(
                "None",
                string.IsNullOrWhiteSpace(
                    boneName)))
        {
            boneName =
                string.Empty;

            changed =
                true;
        }

        if (bones.Count ==
            0)
        {
            ImGui.TextDisabled(
                "No skeleton bones found for this character/model.");
        }
        else
        {
            foreach (string bone
                     in bones)
            {
                bool selected =
                    string.Equals(
                        bone,
                        boneName,
                        StringComparison.OrdinalIgnoreCase);

                if (ImGui.Selectable(
                        bone,
                        selected))
                {
                    boneName =
                        bone;

                    changed =
                        true;
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }
        }

        ImGui.EndCombo();

        return changed;
    }

    private static IReadOnlyList<string> GetSocketBoneNames(
        BoneSocket3D socket,
        EditorProjectContext project)
    {
        SkeletalMeshRenderer? renderer =
            socket.ResolveSourceRenderer();

        if (renderer != null)
        {
            string[] liveBones =
                renderer.BoneNames
                    .Where(
                        name =>
                            !string.IsNullOrWhiteSpace(
                                name))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray();

            if (liveBones.Length >
                0)
            {
                return liveBones;
            }
        }

        for (GameObject? current = socket.GameObject;
             current != null;
             current = current.Parent)
        {
            AnimationController? controller =
                current.GetComponent<AnimationController>();

            if (controller ==
                null)
            {
                continue;
            }

            if (AnimationClipDiscovery.TryGetModel(
                    controller,
                    project,
                    out ModelAsset? model,
                    out _) &&
                model?.Skeleton != null)
            {
                string[] modelBones =
                    model.Skeleton.Bones
                        .Select(
                            bone =>
                                bone.Name)
                        .Where(
                            name =>
                                !string.IsNullOrWhiteSpace(
                                    name))
                        .Distinct(
                            StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                if (modelBones.Length >
                0)
                {
                    return modelBones;
                }
            }
        }

        GameObject branchToSkip =
            socket.GameObject;

        for (GameObject? ancestor = socket.GameObject.Parent;
             ancestor != null;
             ancestor = ancestor.Parent)
        {
            if (TryFindModelBones(
                    ancestor,
                    branchToSkip,
                    project,
                    out IReadOnlyList<string> modelBones))
            {
                return modelBones;
            }

            branchToSkip =
                ancestor;
        }

        return Array.Empty<string>();
    }

    private static bool TryFindModelBones(
        GameObject root,
        GameObject branchToSkip,
        EditorProjectContext project,
        out IReadOnlyList<string> bones)
    {
        foreach (GameObject candidate
                 in SelfAndDescendantsForSocket(
                     root,
                     branchToSkip))
        {
            ModelHierarchyInstance? instance =
                candidate.GetComponent<ModelHierarchyInstance>();

            if (instance == null ||
                instance.Model.IsEmpty)
            {
                continue;
            }

            try
            {
                ModelAsset model =
                    project.Assets.LoadModel(
                        instance.Model);

                if (model.Skeleton ==
                    null)
                {
                    continue;
                }

                string[] names =
                    model.Skeleton.Bones
                        .Select(
                            bone =>
                                bone.Name)
                        .Where(
                            name =>
                                !string.IsNullOrWhiteSpace(
                                    name))
                        .Distinct(
                            StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                if (names.Length ==
                    0)
                {
                    continue;
                }

                bones =
                    names;

                return true;
            }
            catch
            {
            }
        }

        bones =
            Array.Empty<string>();

        return false;
    }

    private static IEnumerable<GameObject> SelfAndDescendantsForSocket(
        GameObject root,
        GameObject branchToSkip)
    {
        if (!ReferenceEquals(
                root,
                branchToSkip))
        {
            yield return root;
        }

        foreach (GameObject child
                 in root.Children)
        {
            if (ReferenceEquals(
                    child,
                    branchToSkip))
            {
                continue;
            }

            foreach (GameObject candidate
                     in SelfAndDescendantsForSocket(
                         child,
                         branchToSkip))
            {
                yield return candidate;
            }
        }
    }

    private static bool HasValidAnimationProfile(
        AnimationController controller,
        EditorProjectContext project)
    {
        AssetReference reference =
            controller.AnimationProfile ??
            AssetReference.Empty;

        if (reference.IsEmpty)
        {
            return false;
        }

        AssetRecord? asset =
            project.AssetDatabase.Resolve(
                reference);

        return
            asset?.Type ==
            AssetType.AnimationProfile;
    }

    private static bool IsAnimationProfileOwnedControllerProperty(
        string propertyName)
    {
        return propertyName is
            nameof(AnimationController.Idle) or
            nameof(AnimationController.Walk) or
            nameof(AnimationController.Run) or
            nameof(AnimationController.Jump) or
            nameof(AnimationController.Fall) or
            nameof(AnimationController.Land) or
            nameof(AnimationController.RunThreshold) or
            nameof(AnimationController.DriveLocomotion) or
            nameof(AnimationController.TransitionDuration) or
            nameof(AnimationController.PlaybackSpeed) or
            nameof(AnimationController.RootMotionMode);
    }

    private static bool DrawAnimationClipSelector(
        AnimationController controller,
        EditorProjectContext project,
        string label,
        ref string clipName)
    {
        IReadOnlyList<string> clips =
            AnimationClipDiscovery.GetClipNames(
                controller,
                project);

        string currentClipName =
            clipName;

        bool hasClip =
            clips.Any(
                candidate =>
                    string.Equals(
                        candidate,
                        currentClipName,
                        StringComparison.OrdinalIgnoreCase));

        string preview =
            string.IsNullOrWhiteSpace(
                clipName)
                ? "None"
                : hasClip
                    ? clipName
                    : $"{clipName} (Missing)";

        bool changed =
            false;

        if (!ImGui.BeginCombo(
                label,                preview))
        {
            return false;
        }

        if (ImGui.Selectable(
                "None",
                string.IsNullOrWhiteSpace(
                    clipName)))
        {
            clipName =
                string.Empty;

            changed =
                true;
        }

        if (clips.Count ==
            0)
        {
            ImGui.TextDisabled(
                "No animation clips found under this character.");
        }
        else
        {
            foreach (string clip
                     in clips)
            {
                bool selected =
                    string.Equals(
                        clipName,
                        clip,
                        StringComparison.OrdinalIgnoreCase);

                if (ImGui.Selectable(
                        clip,
                        selected))
                {
                    clipName =
                        clip;

                    changed =
                        true;
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }
        }

        ImGui.EndCombo();

        return changed;
    }

    private static void DrawAnimationControllerTools(
        AnimationController controller,
        EditorProjectContext project,
        PropertyEditorContext context,
        Action begin,
        Action changed,
        Action end)
    {
        AnimationProfileInspector.Draw(
            controller,
            project,
            context,
            begin,
            changed,
            end);

        bool profileOwnsLocomotion =
            HasValidAnimationProfile(
                controller,
                project);

        ImGui.SeparatorText(
            "ANIMATION CLIPS");

        if (!AnimationClipDiscovery.TryGetModel(
                controller,
                project,
                out ModelAsset? model,
                out AssetReference modelReference) ||
            model ==
                null)
        {
            ImGui.TextDisabled(
                "No animated model found under this character.");

            return;
        }

        AssetRecord? asset =
            project.AssetDatabase.Resolve(
                modelReference);

        ImGui.TextDisabled(
            $"Source: {Path.GetFileName(asset?.ProjectPath ?? model.Name)}");

        ImGui.TextDisabled(
            $"{model.Animations.Count} clip(s) discovered automatically.");

        if (context ==
            PropertyEditorContext.Runtime)
        {
            return;
        }

        if (profileOwnsLocomotion)
        {
            ImGui.TextDisabled(
                "Locomotion slots and playback settings are owned by the assigned Animation Profile.");

            return;
        }

        if (ImGui.Button(
                "Auto Assign Locomotion Clips"))
        {
            begin();

            if (AnimationClipDiscovery.AutoAssign(
                    controller,
                    project))
            {
                changed();
            }

            end();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Matches common Idle, Walk, Run, Jump, Fall and Land names from the imported model.");
        }
    }

    private static bool IsColorProperty(
        Component component,
        ComponentPropertyDescriptor descriptor)
    {
        if (descriptor.Property.PropertyType != typeof(Vector3) ||
            !descriptor.Property.Name.EndsWith(
                "Color",
                StringComparison.Ordinal))
        {
            return false;
        }

        return component is
            SkyEnvironment or
            DirectionalLight or
            PointLight;
    }

    private static void DrawEventModules(
        EventModuleComponent runner,
        EditorProjectContext project,
        PropertyEditorContext context,
        Action begin,
        Action changed,
        Action end)
    {
        ImGui.SeparatorText(
            "EVENT MODULES");

        bool editable =
            context !=
            PropertyEditorContext.Runtime;

        AssetReference[] attached =
            runner.Modules.ToArray();

        if (attached.Length ==
            0)
        {
            ImGui.TextDisabled(
                "No Event Modules attached.");
        }

        foreach (AssetReference reference
                 in attached)
        {
            AssetRecord? asset =
                project.AssetDatabase.Resolve(
                    reference);

            string displayName =
                asset != null
                    ? Path.GetFileNameWithoutExtension(
                        asset.ProjectPath)
                    : reference.CachedProjectPath ??
                      reference.Guid.ToString();

            ImGui.TextUnformatted(
                displayName);

            if (!editable)
            {
                continue;
            }

            ImGui.SameLine();

            if (ImGui.SmallButton(
                    $"Remove##event-module:{reference.Guid}:{displayName}"))
            {
                begin();

                runner.RemoveModule(
                    reference);

                changed();
                end();
            }
        }

        if (!editable)
        {
            return;
        }

        ImGui.SetNextItemWidth(
            -1.0f);

        if (!ImGui.BeginCombo(
                "##AttachEventModuleFromComponent",
                "Attach Event Module..."))
        {
            return;
        }

        AssetRecord[] modules =
            project.AssetDatabase.Assets
                .Where(
                    asset =>
                        asset.Type ==
                        AssetType.EventModule)
                .OrderBy(
                    asset =>
                        asset.ProjectPath,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        if (modules.Length ==
            0)
        {
            ImGui.TextDisabled(
                "No Event Module assets in this project.");
        }

        foreach (AssetRecord moduleAsset
                 in modules)
        {
            bool alreadyAttached =
                runner.Modules.Any(
                    reference =>
                        reference.Guid !=
                            Guid.Empty &&
                        reference.Guid ==
                            moduleAsset.Guid);

            string displayName =
                Path.GetFileNameWithoutExtension(
                    moduleAsset.ProjectPath);

            ImGui.BeginDisabled(
                alreadyAttached);

            if (ImGui.Selectable(
                    $"{displayName}##scene-event:{moduleAsset.Guid}"))
            {
                try
                {
                    EventModuleDefinition definition =
                        new EventModuleSerializer()
                            .Load(
                                moduleAsset.FullPath);

                    var reference =
                        new AssetReference(
                            moduleAsset.Guid,
                            moduleAsset.ProjectPath);

                    begin();

                    runner.AddResolvedModule(
                        reference,
                        definition);

                    changed();
                    end();
                }
                catch (Exception exception)
                {
                    Console.WriteLine(
                        $"Could not attach Event Module '{moduleAsset.ProjectPath}': {exception.Message}");
                }
            }

            ImGui.EndDisabled();

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(
                    alreadyAttached
                        ? "Already attached."
                        : moduleAsset.ProjectPath);
            }
        }

        ImGui.EndCombo();
    }

    private static void DrawMissingActions(
        PlayerController3D playerInput,
        EditorProjectContext project,
        PropertyEditorContext context,
        Action begin,
        Action changed,
        Action end)
    {
        var references =
            new (
                string DefaultName,
                InputActionReference Reference,
                Action<InputActionReference> Set)[]
            {
                (
                    "Move",
                    playerInput.MoveAction,
                    value =>
                        playerInput.MoveAction = value),
                (
                    "Look",
                    playerInput.LookAction,
                    value =>
                        playerInput.LookAction = value),
                (
                    "Jump",
                    playerInput.JumpAction,
                    value =>
                        playerInput.JumpAction = value),
                (
                    "Sprint",
                    playerInput.SprintAction,
                    value =>
                        playerInput.SprintAction = value)
            };

        foreach (var item
                 in references)
        {
            if (project.Project.InputMap.Resolve(
                    item.Reference) != null)
            {
                continue;
            }

            ImGui.TextColored(
                new Vector4(
                    1.0f,
                    0.65f,
                    0.2f,
                    1.0f),
                $"Missing {item.DefaultName} Action: {item.Reference}");

            if (context ==
                    PropertyEditorContext.Runtime ||
                !ImGui.SmallButton(
                    $"Create Default {item.DefaultName} Action##{item.DefaultName}"))
            {
                continue;
            }

            begin();

            project.Project.InputMap.EnsureGameplayDefaults();
            InputActions.Configure(
                project.Project.InputMap);

            item.Set(
                InputActions.Reference(
                    item.DefaultName));

            project.SaveProject();

            changed();
            end();
        }
    }
}