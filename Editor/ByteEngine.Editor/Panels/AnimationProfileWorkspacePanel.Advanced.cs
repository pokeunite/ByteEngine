using System.Numerics;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Scene;
using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed partial class AnimationProfileWorkspacePanel
{
    private bool DrawLocomotionSource()
    {
        if (_profile == null) return false;
        bool changed = false;
        AnimationLocomotionProfile locomotion = _profile.Locomotion;
        ImGui.SeparatorText("GROUNDED MOVEMENT SOURCE");
        int mode = string.IsNullOrWhiteSpace(locomotion.BlendSpace) ? 0 : 1;
        if (ImGui.Combo("Source", ref mode, new[] { "Clip Slots", "Blend Space" }, 2))
        {
            if (mode == 0) locomotion.BlendSpace = string.Empty;
            else
            {
                if (_profile.BlendSpaces.Count == 0)
                    _profile.BlendSpaces.Add(CreateMovementBlendSpace(locomotion));
                locomotion.BlendSpace = _profile.BlendSpaces[0].Name;
            }
            changed = true;
        }
        if (mode == 1)
        {
            string selected = locomotion.BlendSpace;
            if (Picker("Active Blend Space", _profile.BlendSpaces.Select(space => space.Name), ref selected))
            { locomotion.BlendSpace = selected; changed = true; }
            ImGui.TextWrapped("This replaces Idle, Walk, Run and directional clip slots while grounded. Jump, Fall, Land and actions still use their own clips.");
            if (_profile.StateGraph.Enabled)
                ImGui.TextColored(new Vector4(1f, .68f, .25f, 1f),
                    "State Graph is enabled and takes priority over automatic locomotion.");
        }
        else
            ImGui.TextDisabled("Clip slots below drive the existing automatic locomotion.");
        if (ImGui.Button("Create 2D Space From Clip Slots"))
        {
            AnimationBlendSpace space = CreateMovementBlendSpace(locomotion);
            _profile.BlendSpaces.Add(space);
            locomotion.BlendSpace = space.Name;
            changed = true;
        }
        return changed;
    }

    private AnimationBlendSpace CreateMovementBlendSpace(AnimationLocomotionProfile locomotion)
    {
        string name = "Grounded Locomotion";
        if (_profile != null)
        {
            int suffix = 2;
            while (_profile.BlendSpaces.Any(space => string.Equals(space.Name, name, StringComparison.OrdinalIgnoreCase)))
                name = "Grounded Locomotion " + suffix++;
        }
        float walk = Math.Max(.1f, locomotion.WalkReferenceSpeed);
        float run = Math.Max(walk + .1f, locomotion.RunReferenceSpeed);
        var space = new AnimationBlendSpace
        {
            Name = name, TwoDimensional = true, ParameterX = "Right", ParameterY = "Forward",
            MinX = -run, MaxX = run, MinY = -run, MaxY = run
        };
        void Add(string clip, float x, float y)
        {
            if (!string.IsNullOrWhiteSpace(clip))
                space.Samples.Add(new AnimationBlendSample { Clip = clip, X = x, Y = y });
        }
        Add(locomotion.Idle, 0f, 0f);
        Add(string.IsNullOrWhiteSpace(locomotion.WalkForward) ? locomotion.Walk : locomotion.WalkForward, 0f, walk);
        Add(locomotion.WalkBackward, 0f, -walk);
        Add(locomotion.WalkLeft, -walk, 0f);
        Add(locomotion.WalkRight, walk, 0f);
        Add(string.IsNullOrWhiteSpace(locomotion.RunForward) ? locomotion.Run : locomotion.RunForward, 0f, run);
        Add(locomotion.RunBackward, 0f, -run);
        Add(locomotion.RunLeft, -run, 0f);
        Add(locomotion.RunRight, run, 0f);
        return space;
    }

    private bool DrawBlendSpaces()
    {
        if (_profile == null) return false;
        bool changed = false;
        ImGui.SeparatorText("BLEND SPACES");
        ImGui.TextDisabled("Samples blend continuously. For 2D locomotion, X is right/left speed and Y is forward/back speed.");
        ImGui.TextDisabled("Assign a space under Locomotion > Grounded Movement Source.");
        if (ImGui.Button("Add Blend Space"))
        { _profile.BlendSpaces.Add(new AnimationBlendSpace { Name = "New Blend Space" }); changed = true; }
        int remove = -1;
        for (int i = 0; i < _profile.BlendSpaces.Count; i++)
        {
            AnimationBlendSpace space = _profile.BlendSpaces[i];
            ImGui.PushID(i);
            if (ImGui.CollapsingHeader(space.Name + "###BlendSpace"))
            {
                string name = space.Name;
                if (ImGui.InputText("Name", ref name, 128)) { space.Name = name; changed = true; }
                bool twoD = space.TwoDimensional;
                if (ImGui.Checkbox("2D", ref twoD))
                {
                    space.TwoDimensional = twoD;
                    if (twoD && space.ParameterX == "Speed" && space.ParameterY == "Direction")
                    {
                        space.ParameterX = "Right"; space.ParameterY = "Forward";
                        if (space.MinX == 0f && space.MaxX == 6f)
                        { space.MinX = -5f; space.MaxX = 5f; }
                    }
                    changed = true;
                }
                string px = space.ParameterX, py = space.ParameterY;
                if (ImGui.InputText("X Parameter", ref px, 128)) { space.ParameterX = px; changed = true; }
                if (twoD && ImGui.InputText("Y Parameter", ref py, 128)) { space.ParameterY = py; changed = true; }
                float minX = space.MinX, maxX = space.MaxX, minY = space.MinY, maxY = space.MaxY;
                if (ImGui.DragFloat("Min X", ref minX, .1f)) { space.MinX = Math.Min(minX, space.MaxX - .01f); changed = true; }
                if (ImGui.DragFloat("Max X", ref maxX, .1f)) { space.MaxX = Math.Max(maxX, space.MinX + .01f); changed = true; }
                if (twoD)
                {
                    if (ImGui.DragFloat("Min Y", ref minY, .1f)) { space.MinY = Math.Min(minY, space.MaxY - .01f); changed = true; }
                    if (ImGui.DragFloat("Max Y", ref maxY, .1f)) { space.MaxY = Math.Max(maxY, space.MinY + .01f); changed = true; }
                }
                changed |= DrawBlendSpaceCanvas(space);
                if (ImGui.Button("Add Sample"))
                { space.Samples.Add(new AnimationBlendSample()); changed = true; }
                ImGui.SameLine();
                if (ImGui.Button("Delete Space")) remove = i;
                int removeSample = -1;
                for (int j = 0; j < space.Samples.Count; j++)
                {
                    AnimationBlendSample sample = space.Samples[j];
                    ImGui.PushID(j);
                    string clip = sample.Clip;
                    if (DrawClipPicker("Clip", GetAnimationClipNames(), ref clip))
                    { sample.Clip = clip; changed = true; }
                    float x = sample.X, y = sample.Y;
                    if (ImGui.DragFloat("X", ref x, .05f)) { sample.X = x; changed = true; }
                    if (twoD && ImGui.DragFloat("Y", ref y, .05f)) { sample.Y = y; changed = true; }
                    if (ImGui.SmallButton("Remove Sample")) removeSample = j;
                    ImGui.Separator();
                    ImGui.PopID();
                }
                if (removeSample >= 0) { space.Samples.RemoveAt(removeSample); changed = true; }
            }
            ImGui.PopID();
        }
        if (remove >= 0) { _profile.BlendSpaces.RemoveAt(remove); changed = true; }
        return changed;
    }

    private bool DrawAdvancedLayers()
    {
        if (_profile == null) return false;
        bool changed = false;
        ImGui.SeparatorText("ANIMATION LAYERS");
        if (ImGui.Button("Add Layer"))
        { _profile.Layers.Add(new AnimationLayerProfile()); changed = true; }
        int remove = -1;
        for (int i = 0; i < _profile.Layers.Count; i++)
        {
            AnimationLayerProfile layer = _profile.Layers[i];
            ImGui.PushID(i);
            if (ImGui.CollapsingHeader(layer.Name + "###Layer"))
            {
                string name = layer.Name, clip = layer.Clip;
                if (ImGui.InputText("Name", ref name, 128)) { layer.Name = name; changed = true; }
                bool enabled = layer.Enabled;
                if (ImGui.Checkbox("Enabled", ref enabled)) { layer.Enabled = enabled; changed = true; }
                if (DrawClipPicker("Clip", GetAnimationClipNames(), ref clip)) { layer.Clip = clip; changed = true; }
                string weightParameter = layer.WeightParameter;
                if (ImGui.InputText("Weight Parameter (optional)", ref weightParameter, 128))
                { layer.WeightParameter = weightParameter; changed = true; }
                float weight = layer.Weight, blendIn = layer.BlendIn, blendOut = layer.BlendOut;
                if (ImGui.SliderFloat("Weight", ref weight, 0f, 1f)) { layer.Weight = weight; changed = true; }
                if (ImGui.DragFloat("Blend In", ref blendIn, .01f, 0f, 10f)) { layer.BlendIn = blendIn; changed = true; }
                if (ImGui.DragFloat("Blend Out", ref blendOut, .01f, 0f, 10f)) { layer.BlendOut = blendOut; changed = true; }
                int mode = (int)layer.BlendMode;
                if (ImGui.Combo("Mode", ref mode, Enum.GetNames<AnimationLayerBlendMode>(), 2))
                { layer.BlendMode = (AnimationLayerBlendMode)mode; changed = true; }
                int kind = (int)layer.Mask.Kind;
                if (ImGui.Combo("Mask", ref kind, Enum.GetNames<AnimationBoneMaskKind>(), 4))
                { layer.Mask.Kind = (AnimationBoneMaskKind)kind; changed = true; }
                if (kind != 0)
                {
                    string root = layer.Mask.RootBone;
                    if (ImGui.InputText("Mask Root Bone", ref root, 128) ||
                        Picker("Choose Root Bone", GetBoneNames(), ref root))
                    { layer.Mask.RootBone = root; changed = true; }
                    string excluded = string.Join(",", layer.Mask.ExcludedBones);
                    if (ImGui.InputText("Excluded Bones", ref excluded, 1024))
                    { layer.Mask.ExcludedBones = excluded.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList(); changed = true; }
                }
                if (layer.Mask.Kind == AnimationBoneMaskKind.Custom)
                {
                    if (ImGui.SmallButton("Add Bone Weight"))
                    {
                        string first = GetBoneNames().FirstOrDefault() ?? "Bone";
                        layer.Mask.BoneWeights.TryAdd(first, 1f);
                        changed = true;
                    }
                    foreach (var entry in layer.Mask.BoneWeights.ToArray())
                    {
                        ImGui.PushID(entry.Key);
                        string bone = entry.Key;
                        float boneWeight = entry.Value;
                        if (Picker("Bone", GetBoneNames(), ref bone) && !string.IsNullOrWhiteSpace(bone))
                        {
                            layer.Mask.BoneWeights.Remove(entry.Key);
                            layer.Mask.BoneWeights[bone] = boneWeight;
                            changed = true;
                        }
                        if (ImGui.SliderFloat("Bone Weight", ref boneWeight, 0f, 1f))
                        { layer.Mask.BoneWeights[bone] = boneWeight; changed = true; }
                        if (ImGui.SmallButton("Remove Bone"))
                        { layer.Mask.BoneWeights.Remove(bone); changed = true; }
                        ImGui.PopID();
                    }
                }
                if (ImGui.SmallButton("Delete Layer")) remove = i;
            }
            ImGui.PopID();
        }
        if (remove >= 0) { _profile.Layers.RemoveAt(remove); changed = true; }
        return changed;
    }

    private bool DrawStateGraph()
    {
        if (_profile == null) return false;
        AnimationStateGraph graph = _profile.StateGraph;
        bool changed = false;
        bool enabled = graph.Enabled;
        if (ImGui.Checkbox("Enable State Graph", ref enabled)) { graph.Enabled = enabled; changed = true; }
        string entry = graph.EntryState;
        if (ImGui.InputText("Entry State", ref entry, 128)) { graph.EntryState = entry; changed = true; }
        ImGui.TextWrapped("Optional advanced control: enabling this graph takes over locomotion selection. For a standard character, leave it off and use Locomotion > Clip Slots or Blend Space.");
        changed |= DrawStateGraphCanvas(graph);
        ImGui.SeparatorText("PARAMETERS");
        if (ImGui.Button("Add Parameter")) { graph.Parameters.Add(new AnimationGraphParameter()); changed = true; }
        for (int i = 0; i < graph.Parameters.Count; i++)
        {
            ImGui.PushID(i);
            AnimationGraphParameter parameter = graph.Parameters[i];
            string name = parameter.Name;
            if (ImGui.InputText("Name", ref name, 128)) { parameter.Name = name; changed = true; }
            int kind = (int)parameter.Kind;
            if (ImGui.Combo("Kind", ref kind, Enum.GetNames<AnimationParameterKind>(), 3))
            { parameter.Kind = (AnimationParameterKind)kind; changed = true; }
            if (kind == 0)
            {
                float value = parameter.FloatDefault;
                if (ImGui.DragFloat("Default", ref value, .05f)) { parameter.FloatDefault = value; changed = true; }
            }
            else if (kind == 1)
            {
                bool value = parameter.BoolDefault;
                if (ImGui.Checkbox("Default", ref value)) { parameter.BoolDefault = value; changed = true; }
            }
            if (ImGui.SmallButton("Delete Parameter")) { graph.Parameters.RemoveAt(i--); changed = true; }
            ImGui.Separator();
            ImGui.PopID();
        }
        ImGui.SeparatorText("STATES");
        if (ImGui.Button("Add State")) { graph.States.Add(new AnimationGraphState()); changed = true; }
        for (int i = 0; i < graph.States.Count; i++)
        {
            AnimationGraphState state = graph.States[i];
            ImGui.PushID(i);
            string name = state.Name, source = state.Source;
            if (ImGui.InputText("Name", ref name, 128)) { state.Name = name; changed = true; }
            int kind = (int)state.SourceKind;
            if (ImGui.Combo("Source Kind", ref kind, Enum.GetNames<AnimationPoseSourceKind>(), 3))
            { state.SourceKind = (AnimationPoseSourceKind)kind; changed = true; }
            if (kind == 0)
            {
                if (DrawClipPicker("Source Clip", GetAnimationClipNames(), ref source)) { state.Source = source; changed = true; }
            }
            else if (Picker("Source Blend Space", _profile.BlendSpaces.Select(space => space.Name), ref source))
            { state.Source = source; changed = true; }
            bool loop = state.Loop;
            if (ImGui.Checkbox("Loop", ref loop)) { state.Loop = loop; changed = true; }
            float speed = state.PlaybackSpeed;
            if (ImGui.DragFloat("Playback Speed", ref speed, .05f, 0f, 10f)) { state.PlaybackSpeed = speed; changed = true; }
            if (ImGui.SmallButton("Delete State")) { graph.States.RemoveAt(i--); changed = true; }
            ImGui.Separator();
            ImGui.PopID();
        }
        ImGui.SeparatorText("TRANSITIONS");
        if (ImGui.Button("Add Transition")) { graph.Transitions.Add(new AnimationGraphTransition()); changed = true; }
        for (int i = 0; i < graph.Transitions.Count; i++)
        {
            AnimationGraphTransition transition = graph.Transitions[i];
            ImGui.PushID(i);
            string from = transition.From, to = transition.To;
            if (Picker("From", graph.States.Select(state => state.Name), ref from)) { transition.From = from; changed = true; }
            if (Picker("To", graph.States.Select(state => state.Name), ref to)) { transition.To = to; changed = true; }
            float duration = transition.Duration, exitTime = transition.ExitTime;
            if (ImGui.DragFloat("Blend Duration", ref duration, .01f, 0f, 10f))
            { transition.Duration = duration; changed = true; }
            if (ImGui.DragFloat("Exit Phase (-1 = any)", ref exitTime, .01f, -1f, 1f))
            { transition.ExitTime = exitTime; changed = true; }
            if (ImGui.SmallButton("Add Condition")) { transition.Conditions.Add(new AnimationGraphCondition()); changed = true; }
            for (int j = 0; j < transition.Conditions.Count; j++)
            {
                AnimationGraphCondition condition = transition.Conditions[j];
                ImGui.PushID(j);
                string parameter = condition.Parameter;
                if (Picker("Parameter", graph.Parameters.Select(item => item.Name), ref parameter))
                { condition.Parameter = parameter; changed = true; }
                int comparison = (int)condition.Comparison;
                if (ImGui.Combo("Compare", ref comparison, Enum.GetNames<AnimationComparison>(), 6))
                { condition.Comparison = (AnimationComparison)comparison; changed = true; }
                float value = condition.FloatValue;
                if (ImGui.DragFloat("Float Value", ref value, .05f)) { condition.FloatValue = value; changed = true; }
                bool boolValue = condition.BoolValue;
                if (ImGui.Checkbox("Bool Value", ref boolValue)) { condition.BoolValue = boolValue; changed = true; }
                if (ImGui.SmallButton("Delete Condition")) { transition.Conditions.RemoveAt(j--); changed = true; }
                ImGui.PopID();
            }
            if (ImGui.SmallButton("Delete Transition")) { graph.Transitions.RemoveAt(i--); changed = true; }
            ImGui.Separator();
            ImGui.PopID();
        }
        return changed;
    }

    private Guid _boneNameModelGuid;
    private IReadOnlyList<string> _boneNames = Array.Empty<string>();

    private IReadOnlyList<string> GetBoneNames()
    {
        if (_profile == null || _project == null || _profile.Rig.ReferenceModel.IsEmpty)
            return Array.Empty<string>();
        var reference = _profile.Rig.ReferenceModel;
        if (_boneNameModelGuid == reference.Guid) return _boneNames;
        try
        {
            var model = _project.Assets.LoadModel(reference);
            _boneNames = model.Nodes.Select(node => node.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
            _boneNameModelGuid = reference.Guid;
        }
        catch { _boneNames = Array.Empty<string>(); _boneNameModelGuid = Guid.Empty; }
        return _boneNames;
    }

    private static bool Picker(string label, IEnumerable<string> choices, ref string value)
    {
        bool changed = false;
        if (ImGui.BeginCombo(label, string.IsNullOrWhiteSpace(value) ? "None" : value))
        {
            if (ImGui.Selectable("None", string.IsNullOrWhiteSpace(value))) { value = string.Empty; changed = true; }
            foreach (string choice in choices)
                if (!string.IsNullOrWhiteSpace(choice) && ImGui.Selectable(choice, value == choice))
                { value = choice; changed = true; }
            ImGui.EndCombo();
        }
        return changed;
    }

    private bool DrawAdvancedProcedural()
    {
        if (_profile == null) return false;
        AnimationProceduralProfile settings = _profile.Procedural;
        bool changed = false;
        ImGui.SeparatorText("AIM / LOOK AT");
        ImGui.TextDisabled("Aim needs yaw/pitch input; enabling it alone leaves the pose unchanged.");
        ImGui.TextDisabled("Look At needs a scene target. Test both on the Debug tab during Play.");
        if (settings.LookAtEnabled && string.IsNullOrWhiteSpace(settings.LookAtTarget))
            ImGui.TextColored(new Vector4(1f, .68f, .3f, 1f), "Look At has no target object assigned.");
        float aimWeight = settings.AimWeight, yaw = settings.AimYawLimit, pitch = settings.AimPitchLimit;
        float smoothing = settings.AimSmoothing, lookWeight = settings.LookAtWeight;
        if (ImGui.SliderFloat("Aim Weight", ref aimWeight, 0f, 1f)) { settings.AimWeight = aimWeight; changed = true; }
        if (ImGui.DragFloat("Yaw Limit", ref yaw, 1f, 0f, 180f)) { settings.AimYawLimit = yaw; changed = true; }
        if (ImGui.DragFloat("Pitch Limit", ref pitch, 1f, 0f, 180f)) { settings.AimPitchLimit = pitch; changed = true; }
        if (ImGui.DragFloat("Aim Smoothing", ref smoothing, .1f, 0f, 100f)) { settings.AimSmoothing = smoothing; changed = true; }
        if (ImGui.SliderFloat("Look At Weight", ref lookWeight, 0f, 1f)) { settings.LookAtWeight = lookWeight; changed = true; }
        string aimBone = settings.AimBone, lookBone = settings.LookAtBone, target = settings.LookAtTarget;
        if (ImGui.InputText("Aim Bone (blank = mapped torso)", ref aimBone, 128) ||
            Picker("Choose Aim Bone", GetBoneNames(), ref aimBone))
        { settings.AimBone = aimBone; changed = true; }
        if (ImGui.InputText("Look Bone (blank = mapped head)", ref lookBone, 128) ||
            Picker("Choose Look Bone", GetBoneNames(), ref lookBone))
        { settings.LookAtBone = lookBone; changed = true; }
        if (ImGui.InputText("Look Target Object (scene name)", ref target, 128)) { settings.LookAtTarget = target; changed = true; }
        ImGui.SeparatorText("FOOT GROUNDING");
        float ray = settings.FootRayDistance, offset = settings.FootOffset, pelvis = settings.PelvisCompensation;
        if (ImGui.DragFloat("Ray Distance", ref ray, .05f, 0f, 10f)) { settings.FootRayDistance = ray; changed = true; }
        if (ImGui.DragFloat("Foot Offset", ref offset, .01f, -2f, 2f)) { settings.FootOffset = offset; changed = true; }
        if (ImGui.SliderFloat("Pelvis Compensation", ref pelvis, 0f, 1f)) { settings.PelvisCompensation = pelvis; changed = true; }
        ImGui.SeparatorText("IK CHAINS");
        ImGui.TextDisabled("Two-bone chains use local pose joints. Target Object may be a weapon grip or other scene object.");
        if (ImGui.Button("Add IK Chain")) { settings.IkChains.Add(new AnimationIkChainProfile()); changed = true; }
        for (int i = 0; i < settings.IkChains.Count; i++)
        {
            AnimationIkChainProfile chain = settings.IkChains[i];
            ImGui.PushID(i);
            string name = chain.Name, root = chain.RootBone, mid = chain.MidBone, end = chain.EndBone, objectName = chain.TargetObject;
            if (ImGui.InputText("Name", ref name, 128)) { chain.Name = name; changed = true; }
            if (ImGui.InputText("Root Bone", ref root, 128) ||
                Picker("Choose Root", GetBoneNames(), ref root))
            { chain.RootBone = root; changed = true; }
            if (ImGui.InputText("Mid Bone", ref mid, 128) ||
                Picker("Choose Mid", GetBoneNames(), ref mid))
            { chain.MidBone = mid; changed = true; }
            if (ImGui.InputText("End Bone", ref end, 128) ||
                Picker("Choose End", GetBoneNames(), ref end))
            { chain.EndBone = end; changed = true; }
            if (ImGui.InputText("Target Object", ref objectName, 128)) { chain.TargetObject = objectName; changed = true; }
            bool foot = chain.FootGrounding;
            if (ImGui.Checkbox("Ground Foot", ref foot)) { chain.FootGrounding = foot; changed = true; }
            float weight = chain.Weight;
            if (ImGui.SliderFloat("Weight", ref weight, 0f, 1f)) { chain.Weight = weight; changed = true; }
            Vector3 pole = chain.PoleOffset;
            if (ImGui.DragFloat3("Pole Offset", ref pole, .01f)) { chain.PoleOffset = pole; changed = true; }
            if (ImGui.SmallButton("Delete IK Chain")) { settings.IkChains.RemoveAt(i--); changed = true; }
            ImGui.Separator();
            ImGui.PopID();
        }
        return changed;
    }

    private bool DrawSyncGroups()
    {
        if (_profile == null) return false;
        bool changed = false;
        ImGui.SeparatorText("SYNC GROUPS / MARKERS");
        string binding = _profile.Locomotion.SyncGroup;
        if (Picker("Locomotion Group", _profile.SyncGroups.Select(group => group.Name), ref binding))
        { _profile.Locomotion.SyncGroup = binding; changed = true; }
        if (ImGui.Button("Add Sync Group")) { _profile.SyncGroups.Add(new AnimationSyncGroup()); changed = true; }
        for (int i = 0; i < _profile.SyncGroups.Count; i++)
        {
            AnimationSyncGroup group = _profile.SyncGroups[i];
            ImGui.PushID(i);
            string name = group.Name, clips = string.Join(",", group.Clips);
            if (ImGui.InputText("Name", ref name, 128)) { group.Name = name; changed = true; }
            if (ImGui.InputText("Clips (comma separated)", ref clips, 2048))
            { group.Clips = clips.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList(); changed = true; }
            string markerClip = group.Clips.Count > 0 ? group.Clips[0] : string.Empty;
            if (group.Clips.Count > 0 && Picker("Marker Clip", group.Clips, ref markerClip)) _syncMarkerClip = markerClip;
            if (!string.IsNullOrWhiteSpace(_syncMarkerClip) && group.Clips.Contains(_syncMarkerClip))
                markerClip = _syncMarkerClip;
            if (!string.IsNullOrWhiteSpace(markerClip))
            {
                if (!group.Markers.TryGetValue(markerClip, out List<AnimationSyncMarker>? markers))
                    group.Markers[markerClip] = markers = new();
                if (ImGui.SmallButton("Add Marker")) { markers.Add(new AnimationSyncMarker { Name = "LeftFoot" }); changed = true; }
                for (int j = 0; j < markers.Count; j++)
                {
                    ImGui.PushID(j);
                    AnimationSyncMarker marker = markers[j];
                    string markerName = marker.Name;
                    float phase = marker.NormalizedTime;
                    if (ImGui.InputText("Marker", ref markerName, 128)) { marker.Name = markerName; changed = true; }
                    if (ImGui.SliderFloat("Phase", ref phase, 0f, 1f)) { marker.NormalizedTime = phase; changed = true; }
                    if (ImGui.SmallButton("Delete Marker")) { markers.RemoveAt(j--); changed = true; }
                    ImGui.PopID();
                }
            }
            if (ImGui.SmallButton("Delete Group")) { _profile.SyncGroups.RemoveAt(i--); changed = true; }
            ImGui.Separator();
            ImGui.PopID();
        }
        return changed;
    }

    private void DrawAdvancedDebug()
    {
        if (_profile == null || _asset == null) return;
        ImGui.SeparatorText("POSE PIPELINE");
        ImGui.Text("Blend Spaces: " + _profile.BlendSpaces.Count);
        ImGui.Text("Layers: " + _profile.Layers.Count);
        ImGui.Text("Sync Groups: " + _profile.SyncGroups.Count);
        ImGui.Text("Graph States: " + _profile.StateGraph.States.Count);
        ImGui.Text("IK Chains: " + _profile.Procedural.IkChains.Count);
        Scene? scene = _activeScene();
        bool found = false;
        if (scene != null)
        {
            foreach (GameObject gameObject in scene.GameObjects)
            {
                AnimationController? controller = gameObject.GetComponent<AnimationController>();
                if (controller?.AnimationProfile.Guid != _asset.Guid) continue;
                found = true;
                ImGui.SeparatorText("LIVE: " + gameObject.Name);
                ImGui.PushID(gameObject.GetHashCode());
                ImGui.SeparatorText("LIVE AIM / LOOK AT TEST");
                ImGui.TextDisabled("Temporary scene controls. These values are not saved to the profile.");
                if (ImGui.Checkbox("Override Aim", ref _debugAimOverride) && !_debugAimOverride)
                    controller.SetAim(0f, 0f);
                if (_debugAimOverride)
                {
                    float yawLimit = MathF.Max(1f, _profile.Procedural.AimYawLimit);
                    float pitchLimit = MathF.Max(1f, _profile.Procedural.AimPitchLimit);
                    ImGui.SliderFloat("Aim Yaw", ref _debugAimYaw, -yawLimit, yawLimit);
                    ImGui.SliderFloat("Aim Pitch", ref _debugAimPitch, -pitchLimit, pitchLimit);
                    controller.SetAim(_debugAimYaw, _debugAimPitch);
                }
                if (ImGui.Checkbox("Override Look At", ref _debugLookOverride))
                {
                    if (_debugLookOverride)
                        _debugLookTarget = gameObject.Transform.WorldPosition +
                            gameObject.Transform.Forward * 3f + Vector3.UnitY * 1.6f;
                    else controller.SetLookAtTarget(null);
                }
                if (_debugLookOverride)
                {
                    ImGui.DragFloat3("Look Target (world)", ref _debugLookTarget, .05f);
                    controller.SetLookAtTarget(_debugLookTarget);
                }
                if (ImGui.Button("Reset Live Tests"))
                {
                    _debugAimOverride = false;
                    _debugAimYaw = _debugAimPitch = 0f;
                    _debugLookOverride = false;
                    controller.SetAim(0f, 0f);
                    controller.SetLookAtTarget(null);
                }
                ImGui.PopID();
                ImGui.Text("State: " + (string.IsNullOrEmpty(controller.CurrentGraphState)
                    ? controller.State.ToString() : controller.CurrentGraphState));
                ImGui.Text("Clip: " + controller.CurrentAnimation);
                ImGui.Text("Action: " + controller.CurrentAction);
                ImGui.Text("Blend Space: " + controller.CurrentBlendSpace);
                ImGui.Text($"Sync: {controller.CurrentSyncGroup}  Phase: {controller.CurrentSyncPhase:0.000}");
                foreach (var renderer in controller.AnimationRenderers)
                {
                    ImGui.Text($"Blend X/Y: {renderer.BlendParameterX:0.00} / {renderer.BlendParameterY:0.00}");
                    for (int i = 0; i < renderer.ActiveBlendWeights.Count; i++)
                        ImGui.Text($"  Sample {i + 1}: {renderer.ActiveBlendWeights[i]:0.00}");
                    for (int i = 0; i < renderer.ActiveLayerWeights.Count; i++)
                    {
                        string name = i < _profile.Layers.Count ? _profile.Layers[i].Name : $"Layer {i + 1}";
                        ImGui.Text($"  {name}: {renderer.ActiveLayerWeights[i]:0.00}");
                    }
                    ImGui.Text($"Aim Weight: {renderer.ActiveAimWeight:0.00}");
                    for (int i = 0; i < _profile.Procedural.IkChains.Count; i++)
                        ImGui.Text($"IK {_profile.Procedural.IkChains[i].Name}: {_profile.Procedural.IkChains[i].Weight:0.00}");
                    break;
                }
            }
        }
        if (!found) ImGui.TextDisabled("No live controller using this profile in the active scene.");
    }

    private bool _debugAimOverride;
    private float _debugAimYaw, _debugAimPitch;
    private bool _debugLookOverride;
    private Vector3 _debugLookTarget;
    private string _syncMarkerClip = string.Empty;
}
