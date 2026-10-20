namespace ByteEngine.Core.Scene;

/// <summary>Completed preparation work, optionally carrying a fully constructed scene.</summary>
public readonly record struct SceneLoadStep(float Progress, Scene? Scene = null);
