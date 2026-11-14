# Active context

Updated: 2026-11-13 (ByteEngine MCP authoring server v2)

## Current focus

Agent-driven game authoring through `Tools/ByteEngine.Mcp`. The server is published
to `Dist/ByteEngine-MCP` by `Tools/PublishMcp.ps1` and registered in Codex
(`~/.codex/config.toml`) and Cline (`~/.cline/data/settings/cline_mcp_settings.json`).
Next: choose the first game project under `Documents`, create it with
`be_project(create)`, and build gameplay from engine components first, plugins second.

## What changed

- MCP now covers project templates/creation, asset import and model sub-asset
  inspection, import settings/reimport, scene transform/duplicate/tags/variables,
  Blueprint extraction, plugin scaffold/build/install/update, whole-project
  validation, Windows/Web export plus web analysis, and editor launch.
- Starter scenes moved to `Engine/ByteEngine.Core/Authoring/ProjectTemplateFactory.cs`
  (`ByteEngine.Core.Authoring.ProjectTemplate`) so the editor and MCP create
  identical projects. Editor and test usings were updated; the editor-only
  ByteArena/Last Stand/Karlson asset generation stays in the editor.
- `AssetDatabase` exposes `DetectAssetType` and a full
  `SetModelImporterSettings(Guid, ModelImporterSettings)`.
- Plugin build runs `dotnet build -c Release -nodeReuse:false` with node reuse
  disabled, stdin closed and output read through events. Without that, reused
  MSBuild nodes kept the pipes open and the MCP call never returned.
- `Tools/ByteEngine.Mcp/README.md` documents every operation;
  `Docs/PLUGIN-AUTHORING.md` no longer claims MCP cannot export.

## Verification

- `dotnet build ByteEngine.sln` succeeds (final incremental build, 0 warnings/errors).
- Default test suite passed: "ByteEngine tests passed: v0.7 through v0.8-d
  regressions and Phase 1/2/3 gameplay systems."
- Published `ByteEngine.Mcp.exe --self-test` passes project creation, asset import,
  sub-asset inspection/settings, scene rotation/transform/duplicate/tags/variables,
  Blueprint extraction, playable placement, Event Module attach and validation.
- Stdio handshake lists all 11 `be_*` tools.
- Disposable plugin smoke test: scaffold -> build (10.7 s) -> install -> reopen ->
  `be_catalog` resolves `smoke.mcp.McpSmokeSystem3D`. Sources and package cleaned.

## Limitations and next steps

- No headless Play, input injection or screenshots. Use `be_editor(op:"open")` for
  Play/visual checks and the `Tools/TestBrowser*.cjs` harnesses for web rendering.
- Editor-only templates (ByteArena assets, Last Stand, Karlson Demo) still require
  the Project Browser; MCP returns `EDITOR_REQUIRED`.
- Exports need matching published runtimes and are never run automatically.
- Compressed model/audio formats and plugin hot reload remain editor/runtime work.
