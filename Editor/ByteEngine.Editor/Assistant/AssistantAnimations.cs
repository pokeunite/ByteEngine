using System.Text.Json;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace ByteEngine.Editor.Assistant;

internal sealed partial class AssistantTools
{
    private T WithProjectAssets<T>(Func<AssetManager, T> query)
    {
        if (EditorProjectContext.Active is {} project && Path.GetFullPath(project.ProjectRoot) == Path.GetFullPath(projectRoot)) return query(project.Assets);
        using var db = new AssetDatabase(projectRoot, [state.Project.AssetDirectory], readOnly: true);
        using var assets = new AssetManager(db);
        return query(assets);
    }

    private object Animations(JsonElement args)
    {
        var references = new List<AssetReference>();
        if (Text(args, "model") is { Length: > 0 } path)
        {
            path = _assetAliases.GetValueOrDefault(path, path); Inside(path);
            references.Add(new AssetReference(path));
        }
        else
        {
            GameObject root = Resolve(Text(args, "target", "$selected"), []);
            // A selected mesh/bone can still refer to the enclosing imported model.
            if (root.GetComponent<ModelHierarchyInstance>() == null)
                for (var parent = root.Parent; parent != null; parent = parent.Parent)
                    if (parent.GetComponent<ModelHierarchyInstance>() != null) { root = parent; break; }
            foreach (var obj in state.EditorScene.GameObjects.Where(o => o == root || o.IsDescendantOf(root)))
            {
                if (obj.GetComponent<ModelHierarchyInstance>() is {} model && !model.Model.IsEmpty) references.Add(model.Model);
                if (obj.GetComponent<SkeletalMeshRenderer>() is {} renderer && !renderer.Model.IsEmpty) references.Add(renderer.Model);
            }
        }
        if (references.Count == 0) throw new InvalidOperationException("No model source found on this object or its descendants. Select the imported character/model root, or specify a model asset path.");
        string search = Text(args, "search");
        return WithProjectAssets(assets => references.DistinctBy(r => r.Guid != Guid.Empty ? r.Guid.ToString() : r.ProjectPath).Take(8).Select(reference =>
        {
            var model = assets.LoadModel(reference);
            var clips = model.Animations.Where(c => search.Length == 0 || c.Name.Contains(search, StringComparison.OrdinalIgnoreCase)).ToArray();
            return new
            {
                model = reference.ProjectPath, name = model.Name, rig = model.RigType.ToString(),
                hasSkeleton = model.Skeleton != null, boneCount = model.Skeleton?.Bones.Count ?? 0,
                skinnedMeshes = model.Meshes.Count(m => m.JointIndices.Length > 0), totalClips = model.Animations.Count,
                matchingClips = clips.Length, clips = clips.Take(64).Select(c => new { name = c.Name, duration = c.Duration }),
                suggestedLocomotion = new { Idle = SuggestClip(model, "idle"), Walk = SuggestClip(model, "walk"), Run = SuggestClip(model, "run"), Jump = SuggestClip(model, "jump"), Fall = SuggestClip(model, "fall"), Land = SuggestClip(model, "land") },
                note = "Use these exact clip names in create_animation_profile. AnimationController on the character root creates skeletal playback for descendant ModelHierarchyInstance at runtime; an authored SkeletalMeshRenderer is not required."
            };
        }).ToArray());
    }

    private static string SuggestClip(ModelAsset model, string role)
    {
        string[] keywords = role == "run" ? ["run", "sprint", "jog"] : role == "idle" ? ["idle", "stand"] : role == "fall" ? ["fall", "jump_loop"] : [role];
        string[] exclude = ["pistol", "rifle", "shotgun", "crouch", "prone", "strafe", "backward", "back", "left", "right", "aim", "attack", "turn"];
        var names = model.Animations.Select(c => c.Name).Where(n => keywords.Any(k => n.Contains(k, StringComparison.OrdinalIgnoreCase)));
        return names.OrderBy(n => exclude.Count(k => n.Contains(k, StringComparison.OrdinalIgnoreCase)))
            .ThenBy(n => Array.FindIndex(keywords, k => n.Contains(k, StringComparison.OrdinalIgnoreCase)))
            .ThenBy(n => role == "jump" ? (n.Contains("start", StringComparison.OrdinalIgnoreCase) || n.Contains("takeoff", StringComparison.OrdinalIgnoreCase) ? 0 : n.Contains("land", StringComparison.OrdinalIgnoreCase) ? 2 : 1) : 0)
            .ThenBy(n => n.Length).FirstOrDefault() ?? "";
    }

    private void CreateAnimationProfile(JsonElement data, List<string> files)
    {
        string path = Name(data, "model"); path = _assetAliases.GetValueOrDefault(path, path);
        Inside(path);
        var profile = AnimationProfileSerializer.CreateDefault(Name(data, "name"));
        if (data.TryGetProperty("locomotion", out var locomotion)) SetAssetProperties(profile.Locomotion, locomotion, []);
        // These are native profile data structures, not scripts or executable code.
        if (data.TryGetProperty("sections", out var sections)) SetAssetProperties(profile, sections, ["Version", "Name", "Rig", "Locomotion"]);
        WithProjectAssets(assets =>
        {
            var model = assets.LoadModel(new AssetReference(path));
            if (model.Skeleton == null || !model.Meshes.Any(m => m.JointIndices.Length > 0)) throw new InvalidOperationException("The reference model does not have a usable skinned rig.");
            var names = model.Animations.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            // An omitted slot uses a discovered clip or is left empty; never invent Idle/Walk/Run names.
            foreach (string role in new[] { "Idle", "Walk", "Run", "Jump", "Fall", "Land", "WalkForward", "WalkBackward", "WalkLeft", "WalkRight", "RunForward", "RunBackward", "RunLeft", "RunRight" })
            {
                var property = typeof(AnimationLocomotionProfile).GetProperty(role)!;
                bool explicitClip = locomotion.ValueKind == JsonValueKind.Object && locomotion.EnumerateObject().Any(p => p.Name.Equals(role, StringComparison.OrdinalIgnoreCase));
                string clip = explicitClip ? (string)property.GetValue(profile.Locomotion)! : role is "Idle" or "Walk" or "Run" or "Jump" or "Fall" or "Land" ? SuggestClip(model, role.ToLowerInvariant()) : "";
                if (clip.Length > 0 && !names.Contains(clip)) throw new InvalidOperationException("Clip not found on reference model: " + clip + ". Use byte_animations for exact names.");
                property.SetValue(profile.Locomotion, clip);
            }
            foreach (var action in profile.Actions)
                if (action.Clip.Length > 0 && !names.Contains(action.Clip)) throw new InvalidOperationException("Action clip not found: " + action.Clip);
            profile.Rig.ReferenceModel = new AssetReference(path); profile.Rig.Type = model.RigType;
            return true;
        });
        profile.Normalize();
        var asset = NewAsset(data, ".byteanim", files);
        AnimationProfileSerializer.Save(asset.Path, profile);
        FinishAsset(asset, AssetType.AnimationProfile);
    }
}
