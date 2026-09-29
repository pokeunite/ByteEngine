using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;

namespace ByteEngine.Editor;

/// <summary>Editor Play and exported games share exactly the same Blueprint spawn implementation.</summary>
internal static class RuntimeBlueprintSpawner
{
    public static GameObject? Spawn(Scene scene, AssetReference blueprint, Vector3 position,
        EditorProjectContext project, Action<string>? warningSink) =>
        ByteEngine.Core.Runtime.BlueprintRuntimeFactory.Spawn(scene, blueprint, position,
            project.AssetDatabase, project.Scenes, warningSink);
}
