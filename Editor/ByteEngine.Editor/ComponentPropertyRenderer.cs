using System.Numerics;
using System.Reflection;
using System.Text.RegularExpressions;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Classification;
using ImGuiNET;

namespace ByteEngine.Editor;

internal enum PropertyEditorContext { Scene, Blueprint, Runtime }
internal sealed record ComponentPropertyDescriptor(PropertyInfo Property, PropertyMetadata Metadata)
{
    public bool ReadOnly => Property.SetMethod?.IsPublic != true || Metadata.ReadOnly;
}

/// <summary>One descriptor list and widget pipeline for scene, Blueprint and runtime component values.</summary>
internal static class ComponentPropertyRenderer
{
    private static readonly Dictionary<Type, IReadOnlyList<ComponentPropertyDescriptor>> Cache = new();
    public static IReadOnlyList<ComponentPropertyDescriptor> Descriptors(Type type, PropertyEditorContext context)
    {
        if (Cache.TryGetValue(type, out var cached)) return cached;
        var result = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.DeclaringType != typeof(Component) && p.GetIndexParameters().Length == 0 &&
                p.GetMethod?.IsPublic == true && Supported(p.PropertyType) &&
                p.Name is not ("UpdateOrder" or "RenderOrder") && type != typeof(BlueprintInstance))
            .Select(p => new ComponentPropertyDescriptor(p, ComponentMetadataRegistry.GetProperty(type, p.Name) ??
                new PropertyMetadata(Regex.Replace(p.Name, "(?<=[a-z])(?=[A-Z])", " "), "Properties", p.Name)))
            .ToArray();
        Cache[type] = result;
        return result;
    }

    private static bool Supported(Type t) => t == typeof(float) || t == typeof(int) || t == typeof(bool) ||
        t == typeof(string) || t == typeof(Vector2) || t == typeof(Vector3) || t == typeof(Vector4) ||
        t == typeof(Guid) || t == typeof(LayerMask) || t == typeof(AssetReference) || t == typeof(InputActionReference) || t.IsEnum;

    public static bool SetValue(Component component, ComponentPropertyDescriptor descriptor, object value, PropertyEditorContext context)
    {
        if (descriptor.ReadOnly || (context == PropertyEditorContext.Runtime && !descriptor.Metadata.RuntimeEditable)) return false;
        if (value is float number)
        {
            var range = ComponentPropertyConstraints.Get(component.GetType(), descriptor.Property.Name);
            value = Math.Clamp(float.IsFinite(number) ? number : 0, range.Minimum, range.Maximum);
        }
        descriptor.Property.SetValue(component, value);
        return true;
    }

    public static void Draw(Component component, PropertyEditorContext context, bool advanced,
        Action begin, Action changed, Action end, EditorProjectContext? project = null)
    {
        ImGui.PushID(component.GetHashCode());
        foreach (var descriptor in Descriptors(component.GetType(), context))
        {
            if (descriptor.Metadata.Advanced && !advanced) continue;
            object? before = descriptor.Property.GetValue(component);
            string label = descriptor.Metadata.DisplayName + "##" + descriptor.Property.Name;
            bool readOnly = descriptor.ReadOnly || context == PropertyEditorContext.Runtime && !descriptor.Metadata.RuntimeEditable;
            if (readOnly)
            {
                ImGui.TextDisabled($"{descriptor.Metadata.DisplayName}: {before ?? "None"}");
                continue;
            }
            object? after = before;
            bool edited = false;
            if (before is float f)
            {
                var range = ComponentPropertyConstraints.Get(component.GetType(), descriptor.Property.Name);
                edited = ImGui.DragFloat(label, ref f, range.Speed, range.Minimum, range.Maximum);
                after = f;
            }
            else if (before is int n) { edited = ImGui.DragInt(label, ref n); after = n; }
            else if (before is bool b) { edited = ImGui.Checkbox(label, ref b); after = b; }
            else if (before is Vector2 v2) { edited = ImGui.DragFloat2(label, ref v2, .05f); after = v2; }
            else if (before is Vector3 v3) { edited = ImGui.DragFloat3(label, ref v3, .05f); after = v3; }
            else if (before is Vector4 v4) { edited = ImGui.DragFloat4(label, ref v4, .01f); after = v4; }
            else if (descriptor.Property.PropertyType.IsEnum)
            {
                string[] names = Enum.GetNames(descriptor.Property.PropertyType);
                Array values = Enum.GetValues(descriptor.Property.PropertyType);
                int index = Array.IndexOf(values.Cast<object>().ToArray(), before);
                edited = ImGui.Combo(label, ref index, names, names.Length);
                if (edited) after = values.GetValue(index);
            }
            else if (descriptor.Property.PropertyType == typeof(AssetReference))
            {
                AssetReference reference = before as AssetReference ?? AssetReference.Empty;
                ImGui.Button($"{descriptor.Metadata.DisplayName}: {reference}##{descriptor.Property.Name}");
                if (ImGui.BeginDragDropTarget())
                {
                    Guid? id = AssetDragDrop.Accept();
                    if (id.HasValue) { after = new AssetReference(id.Value); edited = true; }
                    ImGui.EndDragDropTarget();
                }
                ImGui.SameLine();
                if (ImGui.SmallButton("Clear##" + descriptor.Property.Name)) { after = AssetReference.Empty; edited = true; }
            }
            else if (descriptor.Property.PropertyType == typeof(InputActionReference))
            {
                InputActionReference reference = before as InputActionReference ?? new InputActionReference();
                InputMap map = project?.Project.InputMap ?? InputActions.Map;
                InputActionDefinition? selected = map.Resolve(reference);
                string preview = selected?.DisplayName ?? (string.IsNullOrWhiteSpace(reference.Name) ? "None" : reference.Name + " (Missing)");
                if (ImGui.BeginCombo(label, preview))
                {
                    foreach (InputActionDefinition action in map.Actions)
                    {
                        bool isSelected = action.Id == selected?.Id;
                        if (ImGui.Selectable(action.DisplayName + "##" + action.Id, isSelected))
                        {
                            after = new InputActionReference(action.Id, action.DisplayName);
                            edited = true;
                        }
                        if (isSelected) ImGui.SetItemDefaultFocus();
                    }
                    ImGui.EndCombo();
                }
            }
            else if (descriptor.Property.PropertyType == typeof(LayerMask))
            {
                LayerMask mask = before is LayerMask value ? value : LayerMask.All;
                edited = ClassificationPickers.DrawLayerMask(label,
                    project?.Project.Classification ?? component.GameObject.Scene?.Classification ?? ClassificationSettings.CreateDefault(), ref mask);
                after = mask;
            }
            else if (descriptor.Property.PropertyType == typeof(Guid) &&
                     descriptor.Property.Name.EndsWith("TagId", StringComparison.Ordinal))
            {
                Guid tag = before is Guid value ? value : Guid.Empty;
                edited = ClassificationPickers.DrawTag(label,
                    project?.Project.Classification ?? component.GameObject.Scene?.Classification ?? ClassificationSettings.CreateDefault(), ref tag);
                after = tag;
            }
            else
            {
                string text = before?.ToString() ?? string.Empty;
                edited = ImGui.InputText(label, ref text, 1024);
                if (descriptor.Property.PropertyType == typeof(Guid))
                {
                    edited &= Guid.TryParse(text, out _);
                    if (edited) after = Guid.Parse(text);
                }
                else after = text;
            }
            bool active = ImGui.IsItemActive();
            if (ImGui.IsItemActivated() || edited) begin();
            if (edited && after != null && SetValue(component, descriptor, after, context))
            {
                if (component is SpriteRenderer sprite && descriptor.Property.Name == nameof(SpriteRenderer.TextureReference) && project != null)
                    sprite.Texture = sprite.TextureReference == null || sprite.TextureReference.IsEmpty ? null : project.Assets.LoadTexture(sprite.TextureReference);
                changed();
            }
            if (ImGui.IsItemDeactivatedAfterEdit() || edited && !active) end();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(descriptor.Metadata.Tooltip);
        }
        if (component is PlayerController3D playerInput && project != null)
            DrawMissingActions(playerInput, project, context, begin, changed, end);
        if (component is MeshRenderer mesh)
        {
            Vector4 color = mesh.Material.BaseColor;
            bool edited = ImGui.ColorEdit4("Base Color", ref color);
            if (ImGui.IsItemActivated() || edited) begin();
            if (edited) { mesh.Material.BaseColor = color; changed(); }
            if (ImGui.IsItemDeactivatedAfterEdit() || edited && !ImGui.IsItemActive()) end();
        }
        if (component is CapsuleCollider3D && project != null && context != PropertyEditorContext.Runtime &&
            ImGui.Button("Fit To Visual"))
        {
            begin();
            if (CharacterCapsuleAutoFit.TryFit(component.GameObject, project.Assets, out _)) changed();
            end();
        }
        ImGui.PopID();
    }

    private static void DrawMissingActions(PlayerController3D playerInput, EditorProjectContext project,
        PropertyEditorContext context, Action begin, Action changed, Action end)
    {
        var references = new (string DefaultName, InputActionReference Reference, Action<InputActionReference> Set)[]
        {
            ("Move", playerInput.MoveAction, value => playerInput.MoveAction = value),
            ("Look", playerInput.LookAction, value => playerInput.LookAction = value),
            ("Jump", playerInput.JumpAction, value => playerInput.JumpAction = value),
            ("Sprint", playerInput.SprintAction, value => playerInput.SprintAction = value)
        };
        foreach (var item in references)
        {
            if (project.Project.InputMap.Resolve(item.Reference) != null) continue;
            ImGui.TextColored(new Vector4(1f, .65f, .2f, 1f), $"Missing {item.DefaultName} Action: {item.Reference}");
            if (context == PropertyEditorContext.Runtime || !ImGui.SmallButton($"Create Default {item.DefaultName} Action##{item.DefaultName}")) continue;
            begin();
            project.Project.InputMap.EnsureGameplayDefaults();
            InputActions.Configure(project.Project.InputMap);
            item.Set(InputActions.Reference(item.DefaultName));
            project.SaveProject();
            changed();
            end();
        }
    }
}
