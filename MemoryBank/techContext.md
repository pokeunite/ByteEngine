# Tech context

Updated: 2026-11-14

## Stack

| Layer | Choice |
| --- | --- |
| Runtime | .NET 9. `ByteEngine.Core` targets `net9.0`; editor, player and tests target `net9.0-windows` with `win-x64`. |
| Desktop graphics | OpenTK 4.9.4, OpenGL 3.3 core profile. |
| Editor UI | ImGui.NET custom editor shell (no WPF/WinForms). |
| Browser runtime | C# frame producer in `Player/ByteEngine.Browser`, WebGL2 front end in `wwwroot` (plain ES modules, no bundler). |
| Model import | AssimpNetter 6.0.5 (FBX/OBJ), SharpGLTF.Core 1.0.6 (GLB/glTF). |
| Textures/text | StbImageSharp 2.30.16, StbTrueTypeSharp 1.26.13. |
| Audio | NVorbis 0.10.5 plus Silk.NET.OpenAL.Soft.Native 1.23.1; `soft_oal.dll` is copied and published as `openal32.dll`. |
| Automation | PowerShell 7 scripts in `Tools/`, Node `.cjs`/`.mjs` scripts for browser checks and Playwright-driven runtime tests. |

## Commands

| Purpose | Command |
| --- | --- |
| Build everything | `dotnet build ByteEngine.sln` |
| Run the editor | `dotnet run --project Editor/ByteEngine.Editor/ByteEngine.Editor.csproj` |
| Run the test suite | `dotnet run --project Tests/ByteEngine.Tests/ByteEngine.Tests.csproj` |
| Read the engine version | `dotnet run --project Tests/ByteEngine.Tests -- --version` |
| Benchmarks | `dotnet run -c Release --project Tests/ByteEngine.Tests -- --engine-performance .artifacts/engine-performance.json` |
| Publish editor | `./Tools/PublishEditor.ps1` (`-Configuration Debug\|Release`, `-SkipBrowser`, `-IncludeBrowser`) |
| Publish the MCP authoring server | `./Tools/PublishMcp.ps1` (writes `Dist/ByteEngine-MCP`) |
| Publish browser runtime | `./Tools/PublishBrowser.ps1` (`-Configuration Debug\|Release`, default Release) |
| Mirror browser runtime into editor bins | `./Tools/Sync-BrowserRuntime.ps1` (`-Check` for drift only) |
| Publish Windows player | `./Tools/PublishPlayer.ps1` (`-Configuration`, `-DistributionDirectory`) |
| Sync browser plugin packages | `./Tools/SyncBrowserPluginPackages.ps1 -SourceRoot <path>` |
| Audit project systems | `./Tools/AuditProjectSystems.ps1` (defaults to DuneCompany and Headwater) |
| Clean generated output | `./Tools/Clean-Workspace.ps1` (dry run; `-Apply` writes, `-IncludeBinObj`/`-Full` also removes bin and obj) |
| Render a Markdown doc to PDF | `python Tools/MarkdownToPdf.py Docs/<file>.md -o <out>.pdf` (needs Python + `markdown`; uses installed Chrome/Edge; see `Documentation/Markdown-to-PDF.md`) |

Outputs: editor publish goes to `Dist/ByteEngine` (`Dist/` and the root launcher
are gitignored); benchmarks and ad-hoc artifacts go under `.artifacts/`.

## Testing model

- `Tests/ByteEngine.Tests` is a console application, not xUnit. Suites are
  selected by flags such as `--web-platform`, `--graphics`, `--plugins`,
  `--engine-optimization`, `--engine-performance`, `--web-cpu-regressions`.
  With no arguments it runs the main regression suite.
- The test project references `ByteEngine.Core`, `ByteEngine.Editor` and the
  DesertTerrain, GoblinScrapper and DuneCompany plugins, so a plugin build break
  fails the suite.
- Additional projects: `Tests/ByteEngine.Browser.Tests`,
  `Tests/ByteEngine.HumanoidGeometry.Tests`.
- Browser smoke tests (`Tools/TestBrowserRuntime.cjs`, `TestBrowserDrawPacket.cjs`,
  `TestShadowBatcher.cjs`, ...) need Node, Playwright (override with
  `BYTEENGINE_PLAYWRIGHT`), and an exported site directory containing
  `web-game.json`.

## Environment notes

- Windows host, PowerShell shell. Paths in scripts are quoted for
  `powershell.exe`; use `-LiteralPath` variants for deletes.
- `.editorconfig` at the repository root governs formatting; `Directory.Build.props`
  governs versioning; do not add a second version literal.
- `imgui.ini`, `layout.ini`, `bin/`, `obj/`, `Dist/`, `.artifacts/`, `output/`,
  `Designs/` and `tmp/` are gitignored generated or local-only content.
- Authored `.byteproject` files are outside the repository; `AGENTS.md` lists the
  canonical locations and the plugin synchronization rules for each.
- The MCP authoring server is registered in `~/.codex/config.toml` and
  `~/.cline/data/settings/cline_mcp_settings.json`; republish with
  `Tools/PublishMcp.ps1` after changing its tools.

## Third-party and native caveats

- OpenAL Soft is required on Windows; the editor publish script renames
  `soft_oal.dll` to `openal32.dll` because that is the name OpenTK resolves.
- Assimp import remains the source of FBX quirks; GLB is the recommended
  exchange format.
- The browser runtime is statically linked: gameplay plugin changes require
  `Tools/PublishBrowser.ps1`, not just a desktop package rebuild.
