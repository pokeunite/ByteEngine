# ByteEngine MCP authoring server

The server exposes ByteEngine's authoring tools over MCP stdio using the official
C# SDK. It edits projects through the same Core serializers, project templates,
importer metadata, plugin build targets and package exporters the editor uses.
Nothing is written to a game project except through those shared paths.

## Build and register

```powershell
./Tools/PublishMcp.ps1
```

Publishes to `Dist/ByteEngine-MCP/ByteEngine.Mcp.exe`. Add the server to Codex:

```toml
[mcp_servers.byteengine]
command = 'C:\Users\codex\ByteEngine\Dist\ByteEngine-MCP\ByteEngine.Mcp.exe'
args = []
startup_timeout_sec = 120
```

Or to the Cline settings (`~/.cline/data/settings/cline_mcp_settings.json`):

```json
{
  "mcpServers": {
    "byteengine": {
      "transport": {
        "type": "stdio",
        "command": "C:\\Users\\codex\\ByteEngine\\Dist\\ByteEngine-MCP\\ByteEngine.Mcp.exe"
      },
      "timeout": 120
    }
  }
}
```

Run from source: `dotnet run --project Tools/ByteEngine.Mcp/ByteEngine.Mcp.csproj --no-launch-profile`.
Set `BYTEENGINE_ROOT` only when the server binary is outside the checkout; otherwise
it walks up to the folder containing `ByteEngine.sln`.

## First project

1. `be_project(op:"templates")` lists starter scenes.
2. `be_project(op:"create", args:{"path":"C:/Users/codex/Documents/MyGame","name":"MyGame","template":"starter3d"})`
   creates the folder, `.byteproject`, `Assets/Scenes/Scripts`, bundled fonts and
   character models, and opens the project.
3. `be_asset(op:"import", args:{"source":"C:/Downloads/hero.glb","dest":"Assets/Characters"})`.
4. `be_scene(op:"apply", ...)` batches scene edits.
5. `be_plugin` scaffolds, builds and installs gameplay code when engine components
   are not enough.
6. `be_runtime(op:"validate")` parses every scene, Blueprint and Event Module.

Only one project is active per server process. Opening another project invalidates
compact handles.

## Tools

| Tool | Operations |
| --- | --- |
| `be_project` | `templates`, `create`, `open`, `info`, `set_startup_scene`, `tags`, `layers`, `add_tag`, `define_layer` |
| `be_asset` | `find`, `inspect`, `refresh`, `import`, `import_folder`, `set_import_settings`, `reimport` |
| `be_scene` | `list`, `create`, `inspect`, `inspect_object`, `apply`, `place_blueprint`, `attach_module` |
| `be_blueprint` | `list`, `create`, `create_playable`, `inspect`, `apply`, `extract`, `attach_module` |
| `be_logic` | `list`, `create`, `inspect`, `inspect_rule`, `add_rule` |
| `be_catalog` | `components`, `component`, `conditions`, `actions`, `asset_types` |
| `be_plugin` | `list`, `create`, `build`, `install`/`import`, `update`, `enable`, `disable`, `remove` |
| `be_runtime` | `validate` |
| `be_export` | `analyze_web`, `windows`, `web` |
| `be_editor` | `open` (launches the editor for human Play/visual checks) |
| `be_changes` | `status` |

Every mutation needs `expected_rev` from a preceding inspect/create result. Use
`"expected_rev":"new"` when creating a file. `dry_run:true` validates a batch
without writing. A mismatch returns `CONFLICT` with the current revision.
`be_changes(op:"status")` is the session receipt of files this server changed.

## Scene edit operations

`be_scene/apply` and `be_blueprint/apply` take an ordered `edits` array. Alias a
created/duplicated object with `as` and reference it later in the same batch:

| Operation | Arguments |
| --- | --- |
| `create_object` | `name`, optional `parent`, `as` |
| `rename` | `target`, `name` |
| `set_active` | `target`, `value` |
| `set_layer` | `target`, `layer` (0..31) |
| `set_parent` | `target`, `parent` or `null` |
| `delete_object` | `target` (removes the subtree) |
| `set_component` | `target`, `type`, `properties` (canonical or legacy component name) |
| `remove_component` | `target`, `type` |
| `set_position` / `set_scale` | `target`, `x`, `y`, `z` |

## Assets

`be_asset/import` copies an absolute source file into a project-relative folder
(defaults to `Assets`), creates its `.meta` identity through `AssetDatabase` and
returns the asset handle. Supported extensions: `.fbx`, `.obj`, `.glb`, `.gltf`,
`.png`, `.jpg`, `.jpeg`, `.tga`, `.bmp`, `.hdr`, `.wav`, `.ogg`, `.ttf`, `.otf`,
`.fnt`, `.bmat`, `.bvfx`, `.uitheme`, `.byteanim`, `.byteanimevents`,
`.bytescene`, `.byteblueprint`, `.byteevents`.

`import_folder` keeps subdirectories (`recursive:true` walks them), `name` renames
a single import, and `overwrite:true` replaces an existing file.

`be_asset/inspect` returns model detail: nodes, meshes with vertex/triangle
counts, materials, animation clips (duration/channels/events), skeleton bone names
and sockets. This is the data needed to choose mesh/material/animation sub-asset
keys without opening the editor. Pass `summary_only:true` on large models.

`set_import_settings` accepts model fields (`import_scale`, `generate_lods`,
`generate_normals`, `prefer_embedded_materials`, `import_animations`,
`auto_detect_humanoid_rig`, `rig_type`) and texture `filter`. It writes the
persistent `.meta` through `AssetDatabase` and reimports the model by default.
`reimport` forces a fresh CPU model import.

## Playable characters and Blueprints

1. Find a model with `be_asset(op:"find", args:{"type":"Model3D"})`.
2. `be_blueprint(op:"create_playable", args:{"name":"Player","model":"Assets/hero.glb","view":"tps","expected_rev":"new"})`
   builds the model hierarchy and adds `CharacterController3D`, collider, camera,
   animation, health and shooter components.
3. `be_scene(op:"place_blueprint", args:{"scene":"Scenes/Main.bytescene","blueprint":"Assets/Player.byteblueprint","x":0,"y":0,"z":0,"expected_rev":"<rev>"})`.
4. `be_blueprint(op:"extract", args:{"name":"Crate","scene":"Scenes/Main.bytescene","object":"Crate","expected_rev":"new"})`
   turns any scene subtree into a reusable Blueprint.
5. `be_logic(op:"create")` plus `add_rule` then `attach_module` connects Event
   Sheet logic to a Blueprint or scene object.

## Plugins

Gameplay code is a project-local plugin. The whole loop is MCP-driven:

1. `be_plugin(op:"create", args:{"name":"MyGame","id":"studio.mygame"})` scaffolds
   `Plugins/MyGame/MyGame.Plugin.csproj`, `plugin.json`, a component source file
   and a README. It is a direct child of the engine `Plugins` folder so the shared
   `../BytePlugin.targets` package step works.
2. Edit the generated C# (components, Event Sheet conditions/actions, systems).
3. `be_plugin(op:"build", args:{"plugin":"MyGame","install":true})` runs
   `dotnet build -c Release`, finds the packaged `.byteplugin`, and installs or
   updates it in the active project.
4. `be_project(op:"open", args:{"path":"..."})` to load the new assembly.
5. `be_catalog(op:"component", args:{"name":"MyGameSystem3D"})` shows its codec and
   default properties; `be_scene/set_component` places it.

`be_plugin/list` also reports plugin source projects in the checkout. Build a
plugin that lives elsewhere by passing its absolute `.csproj` path. Installed
packages remain at `<project>/Plugins/<id>.byteplugin` with enable state in the
adjacent `.state.json`; the disposable cache is `.byteengine/PluginCache`.

## Export and editor

`be_export(op:"analyze_web")` reports blocking/non-blocking browser compatibility
issues without writing anything.

`be_export(op:"windows")` and `be_export(op:"web")` package the active project
using `GamePackageExporter`/`WebGamePackageExporter` and the published runtimes in
`Dist/ByteEngine/PlayerRuntime` and `Dist/ByteEngine/BrowserRuntime`. They never run
automatically; the caller must request them explicitly. A mismatched Core build
identity is rejected with a hint to republish the runtime.

`be_editor(op:"open")` launches the editor on the active project for Play-mode and
visual checks. Headless Play, input injection and screenshots are intentionally
not provided; the browser smoke harnesses under `Tools/` cover runtime rendering.

## Self-test

```powershell
dotnet run --project Tools/ByteEngine.Mcp/ByteEngine.Mcp.csproj -- --self-test
```

The non-destructive self-test covers: asset import and model sub-asset inspection,
import settings, scene rotation/transform/duplicate/variables/tags, Blueprint
extraction, playable Blueprint creation and placement, Event Module attachment,
project template creation and validation, plus plugin round-trip checks when
`BYTEENGINE_TEST_PLUGIN` (and optionally `BYTEENGINE_TEST_GOBLIN_PLUGIN` +
`BYTEENGINE_TEST_GOBLIN_CATALOG`) point at real packages.

## Limits

- No undo, Play-mode simulation, mouse automation or arbitrary process execution.
- Editor-only templates (ByteArena assets, Last Stand, Karlson Demo) must be created
  once in the Project Browser; MCP reports `EDITOR_REQUIRED`.
- Web export rejects enabled managed runtime plugins, matching engine policy.
- One active project and non-collectible plugin assemblies per process.

| `set_rotation` | `target`, `x`, `y`, `z` in degrees (YawPitchRoll semantics) |
| `set_transform` | `target`, optional `position`, `rotation`, `scale` objects |
| `duplicate_object` | `target`, optional `name`, `parent`, `as`; remaps component object references |
| `set_tags` | `target`, `tags` names/GUIDs, or `clear:true` (use `be_project/add_tag` first) |
| `set_variable` | `name`, `value` (number/string/bool/{x,y}/{x,y,z}); omit `target` for scene/Blueprint variables, set `remove:true` to delete |

Example:

```json
{"scene":"Scenes/Main.bytescene","expected_rev":"<rev>","edits":[
  {"op":"create_object","name":"Player","as":"$player"},
  {"op":"set_transform","target":"$player","position":{"x":0,"y":1,"z":0},"rotation":{"x":0,"y":90,"z":0}},
  {"op":"set_component","target":"$player","type":"Camera3D","properties":{"fieldOfView":70}},
  {"op":"duplicate_object","target":"$player","name":"Player2","parent":"none"}
]}
```

Component names, canonical codec ids and plugin aliases are accepted by
`set_component`; `be_catalog/component` returns the canonical codec and default
properties for any component, including installed plugins.
