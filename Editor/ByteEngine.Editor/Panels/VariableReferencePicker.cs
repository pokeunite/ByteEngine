using System.Numerics;
using System.Reflection;

using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;

using ImGuiNET;

namespace ByteEngine.Editor.Panels;

/// <summary>
/// Reusable visual picker for ByteEngine VariableReference values.
///
/// Supports:
///
/// Global.Score
/// Scene.Wave
/// Self.Health
/// Self.Transform.WorldPosition
/// Self.CharacterController3D.Speed
/// Enemy.Health
/// MainCamera.Camera3D.FieldOfView
///
/// This class is EDITOR ONLY.
/// Runtime resolution remains inside VariableResolver.
/// </summary>
internal sealed class VariableReferencePicker
{
    private string _search =
        string.Empty;

    /// <summary>
    /// Draw an already-open ImGui popup containing the
    /// VariableReference browser.
    ///
    /// Call ImGui.OpenPopup(popupId) before calling this.
    /// </summary>
    public bool DrawPopup(
        string popupId,
        EditorState state,
        GameObject? selfContext,
        VariableType? expectedType,
        bool writableOnly,
        out VariableReference? selected)
    {
        selected =
            null;

        if (!ImGui.BeginPopup(
                popupId))
        {
            return false;
        }

        ImGui.SetNextItemWidth(
            320.0f);

        ImGui.InputTextWithHint(
            "##ReferenceSearch",
            "Search variables, objects or properties...",
            ref _search,
            256);

        ImGui.Separator();

        ImGui.BeginChild(
            "ReferencePickerContent",
            new Vector2(
                430.0f,
                460.0f),
            ImGuiChildFlags.None);

        DrawGlobalVariables(
            state,
            expectedType,
            out VariableReference? globalReference);

        if (globalReference != null)
        {
            selected =
                globalReference;
        }

        if (selected == null)
        {
            DrawSceneVariables(
                state,
                expectedType,
                out VariableReference? sceneReference);

            selected =
                sceneReference;
        }

        if (selected == null)
        {
            DrawSelf(
                state,
                selfContext,
                expectedType,
                writableOnly,
                out VariableReference? selfReference);

            selected =
                selfReference;
        }

        if (selected == null)
        {
            DrawSceneObjects(
                state,
                selfContext,
                expectedType,
                writableOnly,
                out VariableReference? objectReference);

            selected =
                objectReference;
        }

        ImGui.EndChild();

        if (selected != null)
        {
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();

        return selected !=
               null;
    }

    // ========================================================
    // GLOBAL
    // ========================================================

    private void DrawGlobalVariables(
        EditorState state,
        VariableType? expectedType,
        out VariableReference? selected)
    {
        selected =
            null;

        if (!ImGui.TreeNodeEx(
                "Global",
                ImGuiTreeNodeFlags.DefaultOpen |
                ImGuiTreeNodeFlags.SpanAvailWidth))
        {
            return;
        }

        if (state.Project.GlobalVariables.Count ==
            0)
        {
            ImGui.TextDisabled(
                "No Global variables");
        }
        else
        {
            foreach (var variable
                     in state.Project.GlobalVariables
                         .OrderBy(
                             variable =>
                                 variable.Name,
                             StringComparer.OrdinalIgnoreCase))
            {
                if (!AcceptType(
                        variable.Value.Type,
                        expectedType))
                {
                    continue;
                }

                if (!MatchesSearch(
                        "Global",
                        variable.Name))
                {
                    continue;
                }

                if (DrawLeaf(
                        variable.Name,
                        $"Global.{variable.Name}",
                        variable.Value.Type))
                {
                    selected =
                        new VariableReference
                        {
                            Scope =
                                VariableScope.Global,

                            MemberName =
                                variable.Name
                        };

                    break;
                }
            }
        }

        ImGui.TreePop();
    }

    // ========================================================
    // SCENE
    // ========================================================

    private void DrawSceneVariables(
        EditorState state,
        VariableType? expectedType,
        out VariableReference? selected)
    {
        selected =
            null;

        if (!ImGui.TreeNodeEx(
                "Scene",
                ImGuiTreeNodeFlags.DefaultOpen |
                ImGuiTreeNodeFlags.SpanAvailWidth))
        {
            return;
        }

        VariableStore variables =
            state.EditorScene.Variables;

        if (variables.Count ==
            0)
        {
            ImGui.TextDisabled(
                "No Scene variables");
        }
        else
        {
            foreach (KeyValuePair<string, VariableValue> pair
                     in variables
                         .OrderBy(
                             pair =>
                                 pair.Key,
                             StringComparer.OrdinalIgnoreCase))
            {
                if (!AcceptType(
                        pair.Value.Type,
                        expectedType))
                {
                    continue;
                }

                if (!MatchesSearch(
                        "Scene",
                        pair.Key))
                {
                    continue;
                }

                if (DrawLeaf(
                        pair.Key,
                        $"Scene.{pair.Key}",
                        pair.Value.Type))
                {
                    selected =
                        new VariableReference
                        {
                            Scope =
                                VariableScope.Scene,

                            MemberName =
                                pair.Key
                        };

                    break;
                }
            }
        }

        ImGui.TreePop();
    }

    // ========================================================
    // SELF
    // ========================================================

    private void DrawSelf(
        EditorState state,
        GameObject? selfContext,
        VariableType? expectedType,
        bool writableOnly,
        out VariableReference? selected)
    {
        selected =
            null;

        string label =
            selfContext != null
                ? $"Self ({selfContext.Name})"
                : "Self";

        if (!ImGui.TreeNodeEx(
                label,
                ImGuiTreeNodeFlags.DefaultOpen |
                ImGuiTreeNodeFlags.SpanAvailWidth))
        {
            return;
        }

        /*
         * Object variables require an example Self object.
         *
         * Generic Event Modules may not have one while being
         * authored, but Transform/component references are still
         * available.
         */
        if (selfContext !=
            null)
        {
            if (ImGui.TreeNodeEx(
                    "Variables",
                    ImGuiTreeNodeFlags.SpanAvailWidth))
            {
                DrawVariableStore(
                    selfContext.Variables,
                    VariableScope.Self,
                    null,
                    null,
                    expectedType,
                    out selected);

                ImGui.TreePop();
            }

            if (selected !=
                null)
            {
                ImGui.TreePop();

                return;
            }
        }

        if (ImGui.TreeNodeEx(
                "Transform",
                ImGuiTreeNodeFlags.SpanAvailWidth))
        {
            DrawTransformMembers(
                null,
                null,
                expectedType,
                writableOnly,
                out selected);

            ImGui.TreePop();
        }

        if (selected !=
            null)
        {
            ImGui.TreePop();

            return;
        }

        if (ImGui.TreeNodeEx(
                "Components",
                ImGuiTreeNodeFlags.SpanAvailWidth))
        {
            if (selfContext !=
                null)
            {
                DrawActualComponents(
                    selfContext,
                    true,
                    expectedType,
                    writableOnly,
                    out selected);
            }
            else
            {
                /*
                 * Generic Event Module:
                 *
                 * We do not know the eventual Self object yet,
                 * so show known engine Component types.
                 */
                DrawKnownComponentTypes(
                    expectedType,
                    writableOnly,
                    out selected);
            }

            ImGui.TreePop();
        }

        ImGui.TreePop();
    }

    // ========================================================
    // OTHER OBJECTS
    // ========================================================

    private void DrawSceneObjects(
        EditorState state,
        GameObject? selfContext,
        VariableType? expectedType,
        bool writableOnly,
        out VariableReference? selected)
    {
        selected =
            null;

        if (!ImGui.TreeNodeEx(
                "Objects",
                ImGuiTreeNodeFlags.SpanAvailWidth))
        {
            return;
        }

        IEnumerable<GameObject> objects =
            state.EditorScene.GameObjects
                .OrderBy(
                    gameObject =>
                        gameObject.Name,
                    StringComparer.OrdinalIgnoreCase);

        foreach (GameObject gameObject
                 in objects)
        {
            /*
             * Self already has its own section.
             */
            if (selfContext !=
                    null &&
                ReferenceEquals(
                    selfContext,
                    gameObject))
            {
                continue;
            }

            if (!MatchesSearch(
                    "Objects",
                    gameObject.Name) &&
                !HasMatchingObjectContents(
                    gameObject,
                    expectedType))
            {
                continue;
            }

            if (!ImGui.TreeNodeEx(
                    $"{gameObject.Name}##Object:{gameObject.Id}",
                    ImGuiTreeNodeFlags.SpanAvailWidth))
            {
                continue;
            }

            if (ImGui.TreeNodeEx(
                    "Variables",
                    ImGuiTreeNodeFlags.SpanAvailWidth))
            {
                DrawVariableStore(
                    gameObject.Variables,
                    VariableScope.Object,
                    gameObject.Id,
                    gameObject.Name,
                    expectedType,
                    out selected);

                ImGui.TreePop();
            }

            if (selected !=
                null)
            {
                ImGui.TreePop();

                break;
            }

            if (ImGui.TreeNodeEx(
                    "Transform",
                    ImGuiTreeNodeFlags.SpanAvailWidth))
            {
                DrawTransformMembers(
                    gameObject.Id,
                    gameObject.Name,
                    expectedType,
                    writableOnly,
                    out selected);

                ImGui.TreePop();
            }

            if (selected !=
                null)
            {
                ImGui.TreePop();

                break;
            }

            if (ImGui.TreeNodeEx(
                    "Components",
                    ImGuiTreeNodeFlags.SpanAvailWidth))
            {
                DrawActualComponents(
                    gameObject,
                    false,
                    expectedType,
                    writableOnly,
                    out selected);

                ImGui.TreePop();
            }

            ImGui.TreePop();

            if (selected !=
                null)
            {
                break;
            }
        }

        ImGui.TreePop();
    }

    // ========================================================
    // VARIABLE STORES
    // ========================================================

    private void DrawVariableStore(
        VariableStore store,
        VariableScope scope,
        Guid? objectId,
        string? objectName,
        VariableType? expectedType,
        out VariableReference? selected)
    {
        selected =
            null;

        if (store.Count ==
            0)
        {
            ImGui.TextDisabled(
                "No variables");

            return;
        }

        foreach (KeyValuePair<string, VariableValue> pair
                 in store.OrderBy(
                     pair =>
                         pair.Key,
                     StringComparer.OrdinalIgnoreCase))
        {
            if (!AcceptType(
                    pair.Value.Type,
                    expectedType))
            {
                continue;
            }

            string fullName =
                scope switch
                {
                    VariableScope.Self =>
                        $"Self.{pair.Key}",

                    VariableScope.Object =>
                        $"{objectName}.{pair.Key}",

                    VariableScope.Scene =>
                        $"Scene.{pair.Key}",

                    VariableScope.Global =>
                        $"Global.{pair.Key}",

                    _ =>
                        pair.Key
                };

            if (!MatchesSearch(
                    fullName,
                    pair.Key))
            {
                continue;
            }

            if (DrawLeaf(
                    pair.Key,
                    fullName,
                    pair.Value.Type))
            {
                selected =
                    new VariableReference
                    {
                        Scope =
                            scope,

                        ObjectId =
                            objectId,

                        ObjectName =
                            objectName,

                        MemberName =
                            pair.Key
                    };

                return;
            }
        }
    }

    // ========================================================
    // TRANSFORM
    // ========================================================

    private void DrawTransformMembers(
        Guid? objectId,
        string? objectName,
        VariableType? expectedType,
        bool writableOnly,
        out VariableReference? selected)
    {
        selected =
            null;

        TransformMember[] members =
        {
            new(
                "World Position",
                "WorldPosition",
                VariableType.Vector3,
                true),

            new(
                "Local Position",
                "LocalPosition",
                VariableType.Vector3,
                true),

            new(
                "Euler Angles",
                "EulerAngles",
                VariableType.Vector3,
                true),

            new(
                "World Scale",
                "WorldScale",
                VariableType.Vector3,
                true),

            new(
                "Local Scale",
                "LocalScale",
                VariableType.Vector3,
                true),

            new(
                "Forward",
                "Forward",
                VariableType.Vector3,
                false),

            new(
                "Right",
                "Right",
                VariableType.Vector3,
                false),

            new(
                "Up",
                "Up",
                VariableType.Vector3,
                false)
        };

        foreach (TransformMember member
                 in members)
        {
            if (writableOnly &&
                !member.Writable)
            {
                continue;
            }

            DrawMemberAndVectorChildren(
                objectId,
                objectName,
                "Transform",
                member.DisplayName,
                member.Path,
                member.Type,
                member.Writable,
                expectedType,
                writableOnly,
                out selected);

            if (selected !=
                null)
            {
                return;
            }
        }
    }

    // ========================================================
    // COMPONENTS
    // ========================================================

    private void DrawActualComponents(
        GameObject gameObject,
        bool self,
        VariableType? expectedType,
        bool writableOnly,
        out VariableReference? selected)
    {
        selected =
            null;

        foreach (Component component
                 in gameObject.Components
                     .OrderBy(
                         component =>
                             component.GetType().Name,
                         StringComparer.OrdinalIgnoreCase))
        {
            Type type =
                component.GetType();

            if (!ImGui.TreeNodeEx(
                    $"{PrettyName(type.Name)}##Component:{gameObject.Id}:{type.FullName}",
                    ImGuiTreeNodeFlags.SpanAvailWidth))
            {
                continue;
            }

            DrawComponentMembers(
                type,
                self
                    ? null
                    : gameObject.Id,
                self
                    ? null
                    : gameObject.Name,
                expectedType,
                writableOnly,
                out selected);

            ImGui.TreePop();

            if (selected !=
                null)
            {
                return;
            }
        }
    }

    private void DrawKnownComponentTypes(
        VariableType? expectedType,
        bool writableOnly,
        out VariableReference? selected)
    {
        selected =
            null;

        IEnumerable<Type> componentTypes =
            GetKnownComponentTypes();

        foreach (Type type
                 in componentTypes)
        {
            if (!TypeHasSelectableMembers(
                    type,
                    expectedType,
                    writableOnly))
            {
                continue;
            }

            if (!MatchesSearch(
                    type.Name,
                    PrettyName(
                        type.Name)) &&
                !TypeContainsSearchMatch(
                    type))
            {
                continue;
            }

            if (!ImGui.TreeNodeEx(
                    $"{PrettyName(type.Name)}##KnownComponent:{type.FullName}",
                    ImGuiTreeNodeFlags.SpanAvailWidth))
            {
                continue;
            }

            DrawComponentMembers(
                type,
                null,
                null,
                expectedType,
                writableOnly,
                out selected);

            ImGui.TreePop();

            if (selected !=
                null)
            {
                return;
            }
        }
    }

    private void DrawComponentMembers(
        Type componentType,
        Guid? objectId,
        string? objectName,
        VariableType? expectedType,
        bool writableOnly,
        out VariableReference? selected)
    {
        selected =
            null;

        foreach (MemberInfo member
                 in GetSelectableMembers(
                     componentType))
        {
            Type memberType =
                GetMemberType(
                    member);

            if (!TryGetVariableType(
                    memberType,
                    out VariableType variableType))
            {
                continue;
            }

            bool writable =
                IsWritable(
                    member);

            if (writableOnly &&
                !writable)
            {
                continue;
            }

            DrawMemberAndVectorChildren(
                objectId,
                objectName,
                componentType.Name,
                PrettyName(
                    member.Name),
                member.Name,
                variableType,
                writable,
                expectedType,
                writableOnly,
                out selected);

            if (selected !=
                null)
            {
                return;
            }
        }
    }

    // ========================================================
    // MEMBER + VECTOR CHILDREN
    // ========================================================

    private void DrawMemberAndVectorChildren(
        Guid? objectId,
        string? objectName,
        string componentType,
        string displayName,
        string memberPath,
        VariableType memberType,
        bool writable,
        VariableType? expectedType,
        bool writableOnly,
        out VariableReference? selected)
    {
        selected =
            null;

        bool isVector =
            memberType is
                VariableType.Vector2 or
                VariableType.Vector3;

        bool canSelectParent =
            AcceptType(
                memberType,
                expectedType) &&
            (!writableOnly ||
             writable);

        bool childrenMayMatch =
            isVector &&
            AcceptType(
                VariableType.Number,
                expectedType);

        if (!canSelectParent &&
            !childrenMayMatch)
        {
            return;
        }

        string referenceText =
            objectId.HasValue
                ? $"{objectName}.{componentType}.{memberPath}"
                : $"Self.{componentType}.{memberPath}";

        if (!MatchesSearch(
                displayName,
                referenceText,
                componentType,
                memberPath) &&
            !string.IsNullOrWhiteSpace(
                _search))
        {
            /*
             * Child X/Y/Z might still match a search.
             */
            bool childMatch =
                isVector &&
                (
                    MatchesSearch(
                        referenceText + ".X") ||
                    MatchesSearch(
                        referenceText + ".Y") ||
                    (
                        memberType ==
                            VariableType.Vector3 &&
                        MatchesSearch(
                            referenceText + ".Z")
                    )
                );

            if (!childMatch)
            {
                return;
            }
        }

        if (!isVector)
        {
            if (canSelectParent &&
                DrawLeaf(
                    displayName,
                    referenceText,
                    memberType))
            {
                selected =
                    CreateComponentReference(
                        objectId,
                        objectName,
                        componentType,
                        memberPath);
            }

            return;
        }

        bool open =
            ImGui.TreeNodeEx(
                $"{displayName}##Member:{componentType}:{memberPath}:{objectId}",
                ImGuiTreeNodeFlags.SpanAvailWidth);

        /*
         * Clicking the vector label itself selects the whole vector.
         * Expanding it allows choosing X/Y/Z.
         */
        if (canSelectParent &&
            ImGui.IsItemClicked(
                ImGuiMouseButton.Left) &&
            !ImGui.IsItemToggledOpen())
        {
            selected =
                CreateComponentReference(
                    objectId,
                    objectName,
                    componentType,
                    memberPath);

            return;
        }

        if (!open)
        {
            return;
        }

        DrawVectorAxis(
            "X",
            memberPath + ".X",
            objectId,
            objectName,
            componentType,
            expectedType,
            writableOnly,
            writable,
            out selected);

        if (selected ==
            null)
        {
            DrawVectorAxis(
                "Y",
                memberPath + ".Y",
                objectId,
                objectName,
                componentType,
                expectedType,
                writableOnly,
                writable,
                out selected);
        }

        if (selected ==
                null &&
            memberType ==
                VariableType.Vector3)
        {
            DrawVectorAxis(
                "Z",
                memberPath + ".Z",
                objectId,
                objectName,
                componentType,
                expectedType,
                writableOnly,
                writable,
                out selected);
        }

        ImGui.TreePop();
    }

    private void DrawVectorAxis(
        string axis,
        string path,
        Guid? objectId,
        string? objectName,
        string componentType,
        VariableType? expectedType,
        bool writableOnly,
        bool writable,
        out VariableReference? selected)
    {
        selected =
            null;

        if (!AcceptType(
                VariableType.Number,
                expectedType))
        {
            return;
        }

        if (writableOnly &&
            !writable)
        {
            return;
        }

        string fullName =
            objectId.HasValue
                ? $"{objectName}.{componentType}.{path}"
                : $"Self.{componentType}.{path}";

        if (!MatchesSearch(
                axis,
                fullName))
        {
            return;
        }

        if (DrawLeaf(
                axis,
                fullName,
                VariableType.Number))
        {
            selected =
                CreateComponentReference(
                    objectId,
                    objectName,
                    componentType,
                    path);
        }
    }

    private static VariableReference CreateComponentReference(
        Guid? objectId,
        string? objectName,
        string componentType,
        string memberPath)
    {
        return new VariableReference
        {
            Scope =
                VariableScope.Component,

            ObjectId =
                objectId,

            ObjectName =
                objectName,

            ComponentType =
                componentType,

            MemberName =
                memberPath
        };
    }

    // ========================================================
    // LEAF UI
    // ========================================================

    private static bool DrawLeaf(
        string label,
        string fullReference,
        VariableType type)
    {
        ImGui.PushID(
            fullReference);

        bool clicked =
            ImGui.Selectable(
                label);

        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();

            ImGui.Text(
                fullReference);

            ImGui.TextDisabled(
                type.ToString());

            ImGui.EndTooltip();
        }

        ImGui.SameLine();

        ImGui.TextDisabled(
            TypeBadge(
                type));

        ImGui.PopID();

        return clicked;
    }

    private static string TypeBadge(
        VariableType type)
    {
        return type switch
        {
            VariableType.Number =>
                "[#]",

            VariableType.String =>
                "[Text]",

            VariableType.Boolean =>
                "[Bool]",

            VariableType.Vector2 =>
                "[V2]",

            VariableType.Vector3 =>
                "[V3]",

            _ =>
                "[?]"
        };
    }

    // ========================================================
    // COMPONENT REFLECTION
    // ========================================================

    private static IEnumerable<Type> GetKnownComponentTypes()
    {
        try
        {
            return typeof(Component)
                .Assembly
                .GetTypes()
                .Where(
                    type =>
                        !type.IsAbstract &&
                        typeof(Component)
                            .IsAssignableFrom(
                                type))
                .OrderBy(
                    type =>
                        type.Name,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types
                .Where(
                    type =>
                        type !=
                            null &&
                        !type.IsAbstract &&
                        typeof(Component)
                            .IsAssignableFrom(
                                type))
                .Cast<Type>()
                .OrderBy(
                    type =>
                        type.Name,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    private static IEnumerable<MemberInfo> GetSelectableMembers(
        Type type)
    {
        const BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public;

        IEnumerable<MemberInfo> properties =
            type
                .GetProperties(
                    flags)
                .Where(
                    property =>
                        property.GetIndexParameters().Length ==
                        0)
                .Cast<MemberInfo>();

        IEnumerable<MemberInfo> fields =
            type
                .GetFields(
                    flags)
                .Where(
                    field =>
                        !field.IsStatic)
                .Cast<MemberInfo>();

        return properties
            .Concat(
                fields)
            .Where(
                member =>
                    !IsHiddenComponentMember(
                        member.Name))
            .OrderBy(
                member =>
                    member.Name,
                StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsHiddenComponentMember(
        string name)
    {
        return name is
            "GameObject" or
            "Transform" or
            "RenderOrder" or
            "UpdateOrder";
    }

    private static Type GetMemberType(
        MemberInfo member)
    {
        return member switch
        {
            PropertyInfo property =>
                property.PropertyType,

            FieldInfo field =>
                field.FieldType,

            _ =>
                typeof(object)
        };
    }

    private static bool IsWritable(
        MemberInfo member)
    {
        return member switch
        {
            PropertyInfo property =>
                property.SetMethod !=
                    null &&
                property.SetMethod.IsPublic,

            FieldInfo field =>
                !field.IsInitOnly &&
                !field.IsLiteral,

            _ =>
                false
        };
    }

    private static bool TypeHasSelectableMembers(
        Type type,
        VariableType? expectedType,
        bool writableOnly)
    {
        foreach (MemberInfo member
                 in GetSelectableMembers(
                     type))
        {
            if (!TryGetVariableType(
                    GetMemberType(
                        member),
                    out VariableType variableType))
            {
                continue;
            }

            if (writableOnly &&
                !IsWritable(
                    member))
            {
                continue;
            }

            if (AcceptType(
                    variableType,
                    expectedType))
            {
                return true;
            }

            if (variableType is
                    VariableType.Vector2 or
                    VariableType.Vector3 &&
                AcceptType(
                    VariableType.Number,
                    expectedType))
            {
                return true;
            }
        }

        return false;
    }

    private bool TypeContainsSearchMatch(
        Type type)
    {
        if (string.IsNullOrWhiteSpace(
                _search))
        {
            return true;
        }

        return GetSelectableMembers(
                type)
            .Any(
                member =>
                    MatchesSearch(
                        member.Name,
                        PrettyName(
                            member.Name)));
    }

    private static bool TryGetVariableType(
        Type type,
        out VariableType variableType)
    {
        Type effectiveType =
            Nullable.GetUnderlyingType(
                type) ??
            type;

        if (effectiveType ==
            typeof(string))
        {
            variableType =
                VariableType.String;

            return true;
        }

        if (effectiveType ==
            typeof(bool))
        {
            variableType =
                VariableType.Boolean;

            return true;
        }

        if (effectiveType ==
            typeof(Vector2))
        {
            variableType =
                VariableType.Vector2;

            return true;
        }

        if (effectiveType ==
            typeof(Vector3))
        {
            variableType =
                VariableType.Vector3;

            return true;
        }

        if (effectiveType.IsEnum)
        {
            /*
             * Enum support will eventually get its own picker.
             * For references, treating it as String matches the
             * resolver's existing enum conversion behavior.
             */
            variableType =
                VariableType.String;

            return true;
        }

        if (effectiveType ==
                typeof(byte) ||
            effectiveType ==
                typeof(sbyte) ||
            effectiveType ==
                typeof(short) ||
            effectiveType ==
                typeof(ushort) ||
            effectiveType ==
                typeof(int) ||
            effectiveType ==
                typeof(uint) ||
            effectiveType ==
                typeof(long) ||
            effectiveType ==
                typeof(ulong) ||
            effectiveType ==
                typeof(float) ||
            effectiveType ==
                typeof(double) ||
            effectiveType ==
                typeof(decimal))
        {
            variableType =
                VariableType.Number;

            return true;
        }

        variableType =
            default;

        return false;
    }

    // ========================================================
    // FILTERING
    // ========================================================

    private static bool AcceptType(
        VariableType actual,
        VariableType? expected)
    {
        return !expected.HasValue ||
               actual ==
               expected.Value;
    }

    private bool MatchesSearch(
        params string?[] values)
    {
        if (string.IsNullOrWhiteSpace(
                _search))
        {
            return true;
        }

        string search =
            _search.Trim();

        return values.Any(
            value =>
                !string.IsNullOrWhiteSpace(
                    value) &&
                value.Contains(
                    search,
                    StringComparison.OrdinalIgnoreCase));
    }

    private bool HasMatchingObjectContents(
        GameObject gameObject,
        VariableType? expectedType)
    {
        if (string.IsNullOrWhiteSpace(
                _search))
        {
            return true;
        }

        foreach (KeyValuePair<string, VariableValue> variable
                 in gameObject.Variables)
        {
            if (AcceptType(
                    variable.Value.Type,
                    expectedType) &&
                MatchesSearch(
                    variable.Key,
                    $"{gameObject.Name}.{variable.Key}"))
            {
                return true;
            }
        }

        if (MatchesSearch(
                "Transform",
                gameObject.Name + ".Transform"))
        {
            return true;
        }

        foreach (Component component
                 in gameObject.Components)
        {
            if (MatchesSearch(
                    component.GetType().Name,
                    PrettyName(
                        component.GetType().Name)))
            {
                return true;
            }

            if (TypeContainsSearchMatch(
                    component.GetType()))
            {
                return true;
            }
        }

        return false;
    }

    private static string PrettyName(
        string name)
    {
        if (string.IsNullOrEmpty(
                name))
        {
            return name;
        }

        var result =
            new System.Text.StringBuilder();

        for (int index = 0;
             index < name.Length;
             index++)
        {
            char current =
                name[index];

            if (index >
                    0 &&
                char.IsUpper(
                    current) &&
                !char.IsUpper(
                    name[index - 1]))
            {
                result.Append(
                    ' ');
            }

            result.Append(
                current);
        }

        return result.ToString();
    }

    private readonly record struct TransformMember(
        string DisplayName,
        string Path,
        VariableType Type,
        bool Writable);
}