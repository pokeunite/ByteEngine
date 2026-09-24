using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Editor;

namespace ByteEngine.Tests;

/// <summary>
/// Reproduces a saved socket against the actual imported models and live
/// animation pose, without changing the user's project or requiring UI clicks.
/// </summary>
internal sealed class SocketPoseDiagnostic : ByteEngineApplication
{
    private readonly string _projectFile;
    private readonly string _characterPath;
    private readonly string _weaponPath;
    private readonly string _socketName;
    private readonly string _clipName;

    public SocketPoseDiagnostic(string projectFile, string characterPath, string weaponPath, string socketName, string clipName)
        : base(320, 240, "ByteEngine Socket Pose Diagnostic")
    {
        _projectFile = projectFile;
        _characterPath = characterPath;
        _weaponPath = weaponPath;
        _socketName = socketName;
        _clipName = clipName;
        IsVisible = false;
    }

    protected override void OnEngineStart()
    {
        using EditorProjectContext project = EditorProjectContext.Open(_projectFile, message => Console.WriteLine("WARNING: " + message));
        AssetReference character = Reference(project, _characterPath);
        AssetReference weapon = Reference(project, _weaponPath);
        ModelAsset characterModel = project.Assets.LoadModel(character);
        ModelAsset weaponModel = project.Assets.LoadModel(weapon);
        SkeletalSocketDefinition socket = characterModel.FindSocket(_socketName)
            ?? throw new InvalidOperationException("Socket not found: " + _socketName);

        Scene scene = new("Socket Pose Diagnostic");
        GameObject owner = scene.CreateGameObject("Character");
        SkeletalMeshRenderer renderer = owner.AddComponent(new SkeletalMeshRenderer
        {
            Model = character,
            SkeletonKey = characterModel.Skeleton?.Key,
            PlayOnStart = false,
            TransitionDuration = 0
        });
        scene.LoadInternal();
        renderer.ResolveRuntimeResources();
        renderer.Stop();
        PrintPose("bind", renderer, socket, weaponModel);
        if (!renderer.Play(_clipName, true, 0))
            throw new InvalidOperationException("Clip not found: " + _clipName);
        renderer.Seek(0);
        PrintPose("clip 0", renderer, socket, weaponModel);
        GameObject legacyWeapon = scene.CreateGameObject("Legacy scene gun");
        legacyWeapon.AddComponent(new ModelHierarchyInstance { Model = weapon });
        if (!SkeletalAttachmentService.AttachToSocket(
                legacyWeapon, owner, socket.Name,
                AttachmentTransformRule.SnapToTarget,
                AttachmentTransformRule.SnapToTarget,
                AttachmentTransformRule.SnapToTarget))
            throw new InvalidOperationException("Actual-asset gun attachment failed.");
        PrintLegacyAttachment("clip 0", legacyWeapon, renderer, weaponModel);
        Console.WriteLine(SkeletalAttachmentService.BuildTraceSample(legacyWeapon));
        GameObject currentWeapon = scene.CreateGameObject("Current imported gun");
        currentWeapon.AddComponent(new ModelHierarchyInstance { Model = weapon });
        ImportedNode importNode = weaponModel.Nodes.First(item =>
            item.Key == ImportedModelSpace.CorrectionNodeKey);
        GameObject importSpace = scene.CreateGameObject("Import Space");
        importSpace.SetParent(currentWeapon, false);
        if (!Matrix4x4.Decompose(importNode.LocalTransform,
                out Vector3 importScale, out Quaternion importRotation,
                out Vector3 importPosition))
            throw new InvalidOperationException("Imported model basis is not decomposable.");
        importSpace.Transform.LocalPosition = importPosition;
        importSpace.Transform.LocalRotation = importRotation;
        importSpace.Transform.LocalScale = importScale;
        if (!SkeletalAttachmentService.AttachToSocket(
                currentWeapon, owner, socket.Name,
                AttachmentTransformRule.SnapToTarget,
                AttachmentTransformRule.SnapToTarget,
                AttachmentTransformRule.SnapToTarget))
            throw new InvalidOperationException("Current imported gun attachment failed.");
        AssertSameVisualBasis(legacyWeapon, importSpace, "clip 0");
        ImportedAnimation clip = characterModel.Animations.First(item =>
            string.Equals(item.Name, _clipName, StringComparison.OrdinalIgnoreCase));
        Vector3 beforeSeek = legacyWeapon.Transform.WorldPosition;
        renderer.Seek(clip.Duration * .5f);
        if (Vector3.Distance(beforeSeek, legacyWeapon.Transform.WorldPosition) < .001f)
            throw new InvalidOperationException("Socket follower did not update with the new pose.");
        PrintPose("clip midpoint", renderer, socket, weaponModel);
        PrintLegacyAttachment("clip midpoint", legacyWeapon, renderer, weaponModel);
        Console.WriteLine(SkeletalAttachmentService.BuildTraceSample(legacyWeapon));
        AssertSameVisualBasis(legacyWeapon, importSpace, "clip midpoint");
        scene.UnloadInternal();
        Close();
    }

    private static AssetReference Reference(EditorProjectContext project, string path)
    {
        if (!project.AssetDatabase.TryGetAsset(path, out AssetRecord? record) || record == null)
            throw new FileNotFoundException("Asset not found in project: " + path);
        return new AssetReference(record.Guid, record.ProjectPath);
    }

    private static void PrintPose(string label, SkeletalMeshRenderer renderer, SkeletalSocketDefinition socket, ModelAsset weapon)
    {
        if (!renderer.TryGetBoneWorldMatrix(socket.BoneName, out Matrix4x4 bone) ||
            !SkeletalSocketResolver.TryGetSocketWorldTransform(renderer, socket.Name, out SkeletalSocketTransform resolved))
            throw new InvalidOperationException("Bone/socket pose unavailable: " + socket.Name);

        Vector3 barrel = BarrelAxis(weapon);
        Vector3 barrelWorld = Vector3.Normalize(Vector3.TransformNormal(barrel, Matrix4x4.CreateFromQuaternion(resolved.Rotation)));
        Vector3 characterForward = Vector3.Normalize(renderer.Transform.Forward);
        Console.WriteLine($"SOCKET_POSE {label}: bone={socket.BoneName} bonePosition={bone.Translation} socketPosition={resolved.Position} socketRotation={resolved.Rotation} socketScale={resolved.Scale}");
        Console.WriteLine($"SOCKET_POSE {label}: weaponBarrelLocal={barrel} barrelWorld={barrelWorld} characterForward={characterForward} forwardDot={Vector3.Dot(barrelWorld, characterForward):F4}");
    }

    private static void AssertSameVisualBasis(GameObject legacyRoot, GameObject currentImportSpace, string label)
    {
        Matrix4x4 legacy = legacyRoot.Transform.WorldMatrix;
        Matrix4x4 current = currentImportSpace.Transform.WorldMatrix;
        float minimumAlignment = 1f;
        foreach (Vector3 axis in new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ })
        {
            Vector3 left = Vector3.Normalize(Vector3.TransformNormal(axis, legacy));
            Vector3 right = Vector3.Normalize(Vector3.TransformNormal(axis, current));
            minimumAlignment = MathF.Min(minimumAlignment, Vector3.Dot(left, right));
        }
        Console.WriteLine($"IMPORT_BASIS {label}: renderedAxisAlignment={minimumAlignment:F6}");
        if (minimumAlignment < .9999f)
            throw new InvalidOperationException("Legacy gun and current imported gun disagree on rendered axes.");
    }

    private static void PrintLegacyAttachment(string label, GameObject root, SkeletalMeshRenderer renderer, ModelAsset weapon)
    {
        Vector3 barrel = BarrelAxis(weapon);
        ImportedNode? correction = weapon.Nodes.FirstOrDefault(item =>
            item.Key == ImportedModelSpace.CorrectionNodeKey);
        if (correction != null &&
            Matrix4x4.Invert(correction.LocalTransform, out Matrix4x4 inverse))
            barrel = Vector3.Normalize(Vector3.TransformNormal(barrel, inverse));
        Vector3 barrelWorld = Vector3.Normalize(Vector3.Transform(barrel, root.Transform.WorldRotation));
        Console.WriteLine($"ATTACHED_LEGACY {label}: rootRotation={root.Transform.WorldRotation} barrelWorld={barrelWorld} forwardDot={Vector3.Dot(barrelWorld, renderer.Transform.Forward):F4}");
    }

    private static Vector3 BarrelAxis(ModelAsset weapon)
    {
        ImportedNode? muzzle = weapon.Nodes.FirstOrDefault(item => item.Name.Equals("Muzzle", StringComparison.OrdinalIgnoreCase));
        ImportedNode? grip = weapon.Nodes.FirstOrDefault(item => item.Name.Equals("PistolArmature", StringComparison.OrdinalIgnoreCase));
        if (muzzle == null || grip == null)
            return Vector3.UnitX;

        var byKey = weapon.Nodes.ToDictionary(item => item.Key, StringComparer.Ordinal);
        Vector3 muzzlePosition = Global(muzzle, byKey).Translation;
        Vector3 gripPosition = Global(grip, byKey).Translation;
        Vector3 direction = muzzlePosition - gripPosition;
        return direction.LengthSquared() > 1e-8f ? Vector3.Normalize(direction) : Vector3.UnitX;
    }

    private static Matrix4x4 Global(ImportedNode node, Dictionary<string, ImportedNode> byKey)
    {
        Matrix4x4 result = node.LocalTransform;
        while (node.ParentKey != null && byKey.TryGetValue(node.ParentKey, out ImportedNode? parent))
        {
            result *= parent.LocalTransform;
            node = parent;
        }
        return result;
    }

    protected override bool ShouldUpdateScene => false;
    protected override bool ShouldRenderSceneToWindow => false;
}
