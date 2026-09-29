# Exporting a Windows game

1. Save the scene and every open asset document. Stop Play mode.
2. Choose **File > Export Windows Game...**.
3. Select the saved **Startup Scene** and choose an export folder.
4. Click **Export Windows Game** and wait for completion.
5. Open the export folder and launch the executable named after your project.
6. Zip the **entire build folder** when sharing it. Do not send only the EXE.

The build is a standalone Windows x64 game, not the editor. It includes the .NET
runtime, native engine libraries, all saved Assets/Scenes, their GUID metadata,
and model-owned sockets/animation libraries. Fonts, materials, UI, animation,
audio, input maps, global variables, Event Sheets and runtime Blueprint spawning
use the engine's existing serialization and runtime systems.

The player's content lookup is relative to its executable, not your original
project path or the working directory. The runtime asset database does not watch
files, create metadata, or rewrite packaged content. Runtime GPU assets are
released before the window's graphics context is destroyed.

Each export creates a new dated folder. Existing builds and your source project
are not overwritten. A failed export retains BUILD-INCOMPLETE.txt and
build-error.txt; do not distribute an incomplete folder.

## Requirements and diagnostics

- Windows x64 and a compatible OpenGL graphics driver.
- Microsoft Visual C++ x64 v14 Redistributable for native Assimp/OpenAL libraries.
  Use the included official prerequisite link if startup reports a missing
  native dependency. The engine does not install software automatically.
- No .NET SDK or separately installed .NET runtime is required.
- Managed startup/runtime errors display a dialog with the log location.
- Logs are in %LOCALAPPDATA%/ByteEngine/Games/Logs (temporary-folder fallback).
- Escape releases captured mouse input; click to recapture; close the window to quit.

The first release is a folder export, not an installer, encrypted asset archive,
ARM-native build, or custom C# game-code compiler. It packages gameplay already
supported by engine components and Event Sheets. All project assets are included;
there is no unused-asset stripping yet. Check the redistribution rights for your
game assets and included third-party libraries before public release.

## Verification

The executable supports --validate to verify the content hashes, native-library
loading, packaged project settings and read-only asset index without opening a
graphics window. This does not replace testing rendering and gameplay on another
Windows PC.

Developer regressions:

    dotnet run --project Tests/ByteEngine.Tests/ByteEngine.Tests.csproj -- --windows-export
    dotnet run --project Tests/ByteEngine.Tests/ByteEngine.Tests.csproj -- --windows-export "Dist/ByteEngine/PlayerRuntime"

The engine rebuild script automatically publishes and bundles PlayerRuntime with
both the packaged editor and its Debug launch output. Export itself does not invoke
dotnet or require an SDK. To publish only the player template:

    powershell -ExecutionPolicy Bypass -File Tools/PublishPlayer.ps1

Deployment approach:
https://learn.microsoft.com/en-us/dotnet/core/deploying/
Native prerequisite:
https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist
