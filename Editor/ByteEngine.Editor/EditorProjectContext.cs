using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Diagnostics;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Core.VisualLogic;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Physics;

namespace ByteEngine.Editor;

internal sealed class EditorProjectContext
    : IDisposable
{
    public static EditorProjectContext? Active { get; private set; }

    private readonly ProjectSerializer _projectSerializer =
        new();

    private readonly Guid _runtimeSpawnRegistration;

    public ProjectData Project { get; }

    public string ProjectFilePath { get; }

    public string ProjectRoot { get; }

    public AssetManager Assets { get; }

    public AssetDatabase AssetDatabase { get; }

    public SceneSerializer Scenes { get; }

    private EditorProjectContext(
        ProjectData project,
        string projectFilePath,
        Action<string> warningSink)
    {
        Project =
            project;

        Project.InputMap ??= InputMap.CreateDefault();
        Project.InputMap.EnsureValid();
        InputActions.Configure(Project.InputMap);
        Project.Classification.EnsureValid();

        ProjectFilePath =
            Path.GetFullPath(
                projectFilePath
            );

        ProjectRoot =
            Path.GetDirectoryName(
                ProjectFilePath
            ) ??
            throw new InvalidDataException(
                "The project file must have a parent directory."
            );

        CrashDebugLog.ConfigureProjectRoot(
            ProjectRoot);

        CrashDebugLog.Write(
            $"EditorProjectContext: opening project '{ProjectFilePath}'.");

        AssetDatabase = new AssetDatabase(
            ProjectRoot,
            new[]
            {
                Project.AssetDirectory,
                Project.SceneDirectory
            },
            warningSink,
            warningSink);

        Assets =
            new AssetManager(
                AssetDatabase,
                warningSink);

        ComponentSerializer components =
            new(
                ProjectRoot,
                AssetDatabase,
                Assets,
                warningSink
            );

        Scenes =
            new SceneSerializer(
                components,
                Project.Classification
            );

        /*
         * SceneSerializer installs the standard renderer codecs in its
         * constructor. Register the v0.9-L2 SkyEnvironment codec afterward so
         * scene/Blueprint persistence includes the new Exposure property.
         */
        SkyEnvironmentExposureSerialization.Register(
            components);

        PhysicsSerializationRegistrar.Register(
            components);

        Active =
            this;

        _runtimeSpawnRegistration =
            RuntimeSpawnService.ConfigureBlueprintSpawner(
                (
                    scene,
                    blueprint,
                    worldPosition
                ) =>
                    RuntimeBlueprintSpawner.Spawn(
                        scene,
                        blueprint,
                        worldPosition,
                        this,
                        warningSink));

        CrashDebugLog.Write(
            "EditorProjectContext: project initialization complete.");
    }

    public static EditorProjectContext Open(
        string projectFilePath,
        Action<string> warningSink)
    {
        ProjectSerializer serializer =
            new();

        ProjectData project =
            serializer.Load(
                projectFilePath
            );

        return new EditorProjectContext(
            project,
            projectFilePath,
            warningSink
        );
    }

    public static EditorProjectContext Create(
        string projectFilePath,
        Action<string> warningSink)
    {
        string fullPath =
            Path.GetFullPath(
                projectFilePath
            );

        string selectedDirectory =
            Path.GetDirectoryName(
                fullPath
            ) ??
            throw new InvalidDataException(
                "The project file must have a parent directory."
            );

        string projectName =
            Path.GetFileNameWithoutExtension(
                fullPath
            );

        string projectRoot =
            string.Equals(
                Path.GetFileName(
                    selectedDirectory
                        .TrimEnd(
                            Path.DirectorySeparatorChar,
                            Path.AltDirectorySeparatorChar
                        )
                ),
                projectName,
                StringComparison.OrdinalIgnoreCase
            )
                ? selectedDirectory
                : Path.Combine(
                    selectedDirectory,
                    projectName
                );

        fullPath =
            Path.Combine(
                projectRoot,
                $"{projectName}.byteproject"
            );

        Directory.CreateDirectory(
            projectRoot
        );

        Directory.CreateDirectory(
            Path.Combine(
                projectRoot,
                "Assets"
            )
        );

        Directory.CreateDirectory(
            Path.Combine(
                projectRoot,
                "Scenes"
            )
        );

        Directory.CreateDirectory(
            Path.Combine(
                projectRoot,
                "Scripts"
            )
        );

        ProjectData project =
            new()
            {
                Name =
                    projectName,

                ProjectId =
                    Guid.NewGuid(),

                EngineVersion =
                    ByteEngineInfo.Version,

                StartupScene =
                    "Scenes/Main.bytescene",

                AssetDirectory =
                    "Assets",

                SceneDirectory =
                    "Scenes"
            };

        ProjectSerializer serializer =
            new();

        serializer.Save(
            project,
            fullPath
        );

        return new EditorProjectContext(
            project,
            fullPath,
            warningSink
        );
    }

    public string ResolveProjectPath(
        string relativePath)
    {
        return Assets.ResolveProjectPath(
            relativePath
        );
    }

    public void SaveProject()
    {
        Project.EngineVersion =
            ByteEngineInfo.Version;

        _projectSerializer.Save(
            Project,
            ProjectFilePath
        );
    }

    public void Dispose()
    {
        CrashDebugLog.Write(
            "EditorProjectContext.Dispose: begin.");

        RuntimeSpawnService.ClearBlueprintSpawner(
            _runtimeSpawnRegistration);

        if (ReferenceEquals(
                Active,
                this))
        {
            Active =
                null;
        }

        Assets.Dispose();
        AssetDatabase.Dispose();

        CrashDebugLog.Write(
            "EditorProjectContext.Dispose: complete.");
    }
}
