# ByteEngine MCP authoring server (initial release)

The server exposes nine small semantic tools through the official C# MCP SDK over stdio. It edits ByteEngine projects through Core serializers; it does not generate C# gameplay code or use editor mouse automation.

Run locally:

```powershell
dotnet run --project Tools/ByteEngine.Mcp/ByteEngine.Mcp.csproj --no-launch-profile
```

Codex MCP configuration example:

```toml
[mcp_servers.byteengine]
command = "dotnet"
args = ["run", "--project", "C:/Users/codex/ByteEngine/Tools/ByteEngine.Mcp/ByteEngine.Mcp.csproj", "--no-launch-profile"]
```

First call `be_project(op="open", args={"path":"C:/absolute/path/Game.byteproject"})`. The server has one active project per process. Opening another project invalidates compact handles.

Every mutation requires `expected_rev` from an inspect/create result. For a new file, use `"expected_rev":"new"`. If the file changed since inspection, the server rejects the edit with `CONFLICT`. Relative paths must remain inside the project and use the correct asset extension. Run `be_changes(op="status")` for the session's changed-file receipt.

## Tools and operations

| Tool | Operations |
| --- | --- |
| `be_plugin` | `list`, `import`, `enable`, `disable`, `remove` |
| `be_project` | `open`, `info`, `set_startup_scene` |
| `be_catalog` | `components`, `component`, `conditions`, `actions`, `asset_types` |
| `be_asset` | `find`, `inspect`, `refresh` |
| `be_scene` | `list`, `create`, `inspect`, `apply`, `place_blueprint` |
| `be_blueprint` | `list`, `create`, `create_playable`, `inspect`, `apply`, `attach_module` |
| `be_logic` | `list`, `create`, `inspect`, `add_rule` |
| `be_runtime` | `validate` |
| `be_changes` | `status` |

`be_scene/apply` and `be_blueprint/apply` take `edits`, an ordered array. Supported edit operations: `create_object`, `rename`, `set_active`, `set_layer`, `set_parent`, `delete_object`, `set_component`, `remove_component`, `set_position`, and `set_scale`. Use `as` on a created object and reference it by that alias in a later edit in the same batch. Use a GUID for existing objects; `root` means the Blueprint root. `dry_run:true` validates without writing.

For a playable character, first find a renderable model with `be_asset(find)`. Call `be_blueprint(create_playable)` with `name`, `model`, `view` (`tps`, `fps`, `topdown`, or `isometric`), and `expected_rev:"new"`. This builds the model hierarchy and ByteEngine's controller, collider, camera, animation, health, and shooter components. Call `be_scene(place_blueprint)` with `scene`, `blueprint`, `x/y/z`, and the scene's `expected_rev`. To connect logic, create a module with `be_logic(create)`, add a rule, then call `be_blueprint(attach_module)` with `blueprint`, `module`, and the Blueprint's latest `expected_rev`.

Example scene batch:

```json
{"scene":"Scenes/Main.bytescene","expected_rev":"<inspect rev>","edits":[
  {"op":"create_object","name":"Player","as":"$player"},
  {"op":"create_object","name":"Muzzle","parent":"$player"},
  {"op":"set_position","target":"$player","x":0,"y":1,"z":0}
]}
```

`be_logic/add_rule` takes `module`, `expected_rev`, `name`, `conditions` and `actions`. Each condition/action is `{"id":"registry.id","args":{"key":"value"}}`. IDs come from `be_catalog`; constants support string, number and boolean. Conditions use ByteEngine's standard implicit AND and actions execute in list order. Example:

```json
{"module":"Assets/Game.byteevents","expected_rev":"<inspect rev>","name":"Begin","conditions":[{"id":"system.always"}],"actions":[{"id":"time.startTimer"}]}
```

The initial release intentionally does not expose editor-only importers, arbitrary process execution, Play, exporter builds, undo, or complex graph branches. It returns `UNSUPPORTED_CAPABILITY` for those operations. Presets save and reload in automated tests, but camera framing, model scale, animation, and gameplay behavior still require a manual editor Play test.

Run the non-destructive integration test against a temporary project:

```powershell
dotnet run --project Tools/ByteEngine.Mcp/ByteEngine.Mcp.csproj -- --self-test
```

## Native plugin authoring

Import a `.byteplugin` with `be_plugin(op="import", args={"path":"C:/absolute/path/SpaceScraper.byteplugin"})`, then reopen the same project with `be_project/open`. Use `be_catalog/component` for the plugin component's canonical codec and camel-case properties. `be_scene/apply` with a `set_component` edit places it on an object and saves through that codec. Component edits/removals also resolve canonical IDs and old aliases. `be_catalog/actions` and `conditions` include plugin definitions and argument metadata. Package enable/disable/remove operations return `reopen_required:true`; they do not use scene revision tokens.

The verified round-trip includes disabled-plugin saving and restoring the retained data after re-enabling. See `../../Docs/PLUGIN-AUTHORING.md`. Game export is intentionally absent from MCP.
