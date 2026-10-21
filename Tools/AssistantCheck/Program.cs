using System.Numerics;
using System.Text.Json;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;
using ByteEngine.Editor;
using ByteEngine.Editor.Assistant;
using ByteEngine.Editor.Commands;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.Common;

string root = Path.Combine(Path.GetTempPath(), "ByteEngine-AssistantCheck-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(root, "Assets"));
using var database = new AssetDatabase(root, ["Assets"]);
var assets = new AssetManager(database);
var serializer = new SceneSerializer(new ComponentSerializer(root, database, assets));
var scene = new Scene("Assistant test scene"); var selected = scene.CreateGameObject("Selected");
var state = new EditorState { EditorScene = scene, Project = new ProjectData { Name = "Assistant tests" }, ProjectFilePath = Path.Combine(root, "Test.byteproject") };
state.SelectedObject = selected; state.Undo = new UndoManager(serializer, restored => state.EditorScene = restored); state.Undo.Reset(state, true);
int checks = 0;
void Check(bool yes, string message) { if (!yes) throw new Exception(message); checks++; Console.WriteLine("PASS " + message); }
JsonElement Parse(string text) { using var doc = JsonDocument.Parse(text); return doc.RootElement.Clone(); }
object Call(AssistantTools tools, string tool, string args) => tools.Execute(tool, Parse(args));
bool Throws(Action action) { try { action(); return false; } catch { return true; } }

if (args.Contains("--topdown")) { TopDownChecks.Run(serializer); return; }

if (args.Contains("--preview"))
{
    string output = Path.GetFullPath("artifacts/assistant-check"); Directory.CreateDirectory(output);
    using var window = new GameWindow(GameWindowSettings.Default, new NativeWindowSettings { StartVisible = false, ClientSize = new(900, 720), APIVersion = new(3, 3), Profile = ContextProfile.Core }); window.Context.MakeCurrent();
    using var controller = new ImGuiController(window, Path.Combine(output, "layout.ini"));
    using var panel = new AssistantPanel { IsOpen = true };
    foreach (var size in new[] { new Vector2(470, 650), new Vector2(380, 650) })
    {
        for (int frame = 0; frame < 4; frame++)
        {
            controller.Update(1f / 60); ImGui.SetNextWindowPos(new(10, 10), ImGuiCond.Always); ImGui.SetNextWindowSize(size, ImGuiCond.Always);
            panel.Draw(state, null); ImGui.SetWindowSize("Byte Assistant", size, ImGuiCond.Always); ImGui.SetWindowPos("Byte Assistant", new(10, 10), ImGuiCond.Always);
            GL.Viewport(0, 0, 900, 720); GL.ClearColor(.04f, .045f, .055f, 1); GL.Clear(ClearBufferMask.ColorBufferBit); controller.Render(); GL.Finish();
        }
        var pixels = new byte[900 * 720 * 4]; GL.ReadPixels(0, 0, 900, 720, OpenTK.Graphics.OpenGL4.PixelFormat.Bgra, PixelType.UnsignedByte, pixels);
        using var bitmap = new System.Drawing.Bitmap(900, 720, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var locked = bitmap.LockBits(new(0, 0, 900, 720), System.Drawing.Imaging.ImageLockMode.WriteOnly, bitmap.PixelFormat);
        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, locked.Scan0, pixels.Length); bitmap.UnlockBits(locked); bitmap.RotateFlip(System.Drawing.RotateFlipType.RotateNoneFlipY);
        bitmap.Save(Path.Combine(output, "assistant-" + size.X + ".png")); Console.WriteLine("PASS native assistant panel at width " + size.X);
    }
    return;
}


if (args.Contains("--live-fireball"))
{
    // One subscription-backed evaluation of the reported task in a temporary project.
    using var connection = new CodexConnection();
    connection.ConnectAsync(CodexConnection.FindExecutable()).GetAwaiter().GetResult();
    var account = connection.RequestAsync("account/read",new {refreshToken=false}).GetAwaiter().GetResult();
    Check(account.GetProperty("account").GetProperty("type").GetString()=="chatgpt","Live fireball uses existing ChatGPT sign-in");
    var models = connection.RequestAsync("model/list",new {includeHidden=false}).GetAwaiter().GetResult().GetProperty("data").EnumerateArray().ToArray();
    var chosen = models.FirstOrDefault(m=>m.GetProperty("model").GetString()=="gpt-6.1-sol");
    if(chosen.ValueKind!=JsonValueKind.Object) throw new Exception("The model used in the user's log is unavailable; do not silently substitute.");
    string model = chosen.GetProperty("model").GetString()!;
    var tools = new AssistantTools(state,root);
    var liveJournal = new AssistantJournal();
    string prompt = "Make a custom fireball VFX asset and wire it to shoot a real traveling, repeatable projectile from Selected when J is pressed. Build the needed Blueprint too. Use native editable components/assets/events, then briefly confirm what was made.";
    string input = "Editor context: "+tools.Snapshot()+"\nRelevant engine recipes: "+AssistantTools.Serialize(AssistantKnowledge.Search(prompt))+"\nUser request: "+prompt;
    liveJournal.Record("request",new {model,effort="low",prompt,contextCharacters=input.Length});
    var started = connection.RequestAsync("thread/start",AssistantTools.ThreadParameters(model)).GetAwaiter().GetResult();
    string threadId = started.GetProperty("thread").GetProperty("id").GetString()!;
    var turn=connection.RequestAsync("turn/start",new {threadId,model,effort="low",environments=Array.Empty<object>(),input=new[]{new{type="text",text=input}}}).GetAwaiter().GetResult();
    string turnId=turn.GetProperty("turn").GetProperty("id").GetString()!;
    bool done=false; int calls=0; long tokens=0; string reply=""; var deadline=DateTime.UtcNow.AddSeconds(180);
    try
    {
        while(!done && DateTime.UtcNow<deadline)
        {
            while(connection.Messages.TryDequeue(out var message))
            {
                string method=AssistantTools.Text(message,"method"); var p=message.GetProperty("params");
                if(message.TryGetProperty("id",out var request))
                {
                    if(method=="item/tool/call")
                    {
                        calls++; bool success=true;object result;
                        var arguments=p.GetProperty("arguments"); if(arguments.ValueKind==JsonValueKind.String) arguments=Parse(arguments.GetString()!);
                        try {result=tools.Execute(AssistantTools.Text(p,"tool"),arguments);} catch(Exception error){success=false;result=new{error=error.GetBaseException().Message};}
                        liveJournal.Record("tool",new {tool=AssistantTools.Text(p,"tool"),arguments,success,result});
                        Console.WriteLine("LIVE "+AssistantTools.Text(p,"tool")+" success="+success);
                        connection.RespondAsync(request,new{success,contentItems=new[]{new{type="inputText",text=AssistantTools.Serialize(result)}}}).GetAwaiter().GetResult();
                    }
                    else connection.RejectAsync(request,"Native engine tools only").GetAwaiter().GetResult();
                }
                else if(method=="item/agentMessage/delta") reply+=AssistantTools.Text(p,"delta");
                else if(method=="thread/tokenUsage/updated") {liveJournal.Record("usage",p.GetProperty("tokenUsage").Clone());tokens=p.GetProperty("tokenUsage").GetProperty("total").GetProperty("totalTokens").GetInt64();}
                else if(method=="turn/completed") {done=true;liveJournal.Record("completed",new{status=AssistantTools.Text(p.GetProperty("turn"),"status"),reply,calls,tokens});}
            }
            Thread.Sleep(15);
        }
        if(!done) {connection.RequestAsync("turn/interrupt",new{threadId,turnId}).GetAwaiter().GetResult();throw new Exception("Live evaluation timed out");}
        var shooter=state.EditorScene.FindGameObject("Selected")!;
        var launcher=shooter.GetComponent<ByteEngine.Core.Gameplay.ProjectileLauncher3D>();
        Check(launcher!=null && !launcher.ProjectileBlueprint.IsEmpty,"Live assistant builds a custom projectile Blueprint and connects the launcher");
        database.RefreshPaths(Directory.EnumerateFiles(Path.Combine(root,"Assets"),"*",SearchOption.AllDirectories));
        var registration=RuntimeSpawnService.ConfigureBlueprintSpawner((sc,reference,position)=>ByteEngine.Core.Runtime.BlueprintRuntimeFactory.Spawn(sc,reference,position,database,serializer,Console.WriteLine));
        try
        {
            var modules=shooter.GetComponent<EventModuleComponent>()!.Modules;
            var rule=modules.SelectMany(r=>new EventModuleSerializer().Load(Path.Combine(root,r.ProjectPath)).Rules).First(r=>r.Conditions.Any(c=>c.Id=="input.keyPressed" && c.Arguments["key"].Constant.String=="J"));
            foreach(var action in rule.Actions){var registry=VisualLogicRegistry.CreateDefault();registry.TryGetAction(action.Id,out var a);a!.Execute(action,new EventExecutionContext{Scene=state.EditorScene,Self=shooter,Globals=new VariableStore()});}
            var moving=state.EditorScene.GameObjects.First(o=>o.GetComponent<ByteEngine.Core.Gameplay.Projectile3D>() is {Enabled:true});
            var projectile=moving.GetComponent<ByteEngine.Core.Gameplay.Projectile3D>()!;
            var visual=state.EditorScene.GameObjects.First(o=>o.IsDescendantOf(moving) && o.GetComponent<ByteEngine.Core.Vfx.VfxPlayer>()!=null);
            var effect=ByteEngine.Core.Vfx.VfxEffectSerializer.Load(Path.Combine(root,visual.GetComponent<ByteEngine.Core.Vfx.VfxPlayer>()!.Effect.ProjectPath));
            Check(effect.Layers.Count>0 && projectile.Velocity.Length()>0,"Live assistant's J action spawns a real projectile with custom native VFX");
            Vector3 liveBefore=moving.Transform.WorldPosition;
            typeof(ByteEngine.Core.Gameplay.Projectile3D).GetMethod("Simulate",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(projectile,[.1f]);
            Check(Vector3.Distance(liveBefore,moving.Transform.WorldPosition)>.1f,"Live-authored fireball actually travels in native simulation");
        }
        finally{RuntimeSpawnService.ClearBlueprintSpawner(registration);}
        Console.WriteLine($"Live result: {calls} native calls; {tokens} model-reported tokens; same gpt-6.1-sol low effort as supplied log.");
    }
    finally {Directory.CreateDirectory("artifacts/assistant-check");File.WriteAllText("artifacts/assistant-check/live-fireball.json",liveJournal.Export());}
    Console.WriteLine(checks+" checks passed. Temporary scene: "+root);return;
}

if (args.Contains("--connect") || args.Contains("--live"))
{
    using var connection = new CodexConnection();
    connection.ConnectAsync(CodexConnection.FindExecutable()).GetAwaiter().GetResult();
    Check(connection.Connected, "Installed Codex app-server handshake succeeds");
    var account = connection.RequestAsync("account/read", new { refreshToken = false }).GetAwaiter().GetResult();
    Check(account.GetProperty("account").GetProperty("type").GetString() == "chatgpt", "Existing ChatGPT sign-in is available; no API-key billing");
    var models = connection.RequestAsync("model/list", new { includeHidden = false }).GetAwaiter().GetResult().GetProperty("data").EnumerateArray().ToArray();
    Check(models.Length > 0, "Available models are discovered from the signed-in account");
    if (!args.Contains("--live"))
    {
        string defaultModel = models.First(m => m.GetProperty("isDefault").GetBoolean()).GetProperty("model").GetString()!;
        var thread = connection.RequestAsync("thread/start", AssistantTools.ThreadParameters(defaultModel)).GetAwaiter().GetResult();
        Check(thread.GetProperty("thread").GetProperty("id").GetString()?.Length > 0, "Current native-tool schema and isolated subscription session initialize without a model turn");
    }
    if (args.Contains("--live"))
    {
        // One small subscription-backed request against this temporary scene only.
        var model = models.FirstOrDefault(m => m.GetProperty("model").GetString()!.Contains("luna"));
        if (model.ValueKind != JsonValueKind.Object) model = models.First(m => m.GetProperty("isDefault").GetBoolean());
        string id = model.GetProperty("model").GetString()!;
        var efforts = model.GetProperty("supportedReasoningEfforts").EnumerateArray().Select(e => e.GetProperty("reasoningEffort").GetString()!).ToArray();
        string effort = efforts.Contains("low") ? "low" : efforts[0];
        var tools = new AssistantTools(state, root);
        var started = connection.RequestAsync("thread/start", AssistantTools.ThreadParameters(id)).GetAwaiter().GetResult();
        string threadId = started.GetProperty("thread").GetProperty("id").GetString()!;
        var turn = connection.RequestAsync("turn/start", new { threadId, model = id, effort, environments = Array.Empty<object>(), input = new[] { new { type = "text", text = "Editor context: " + tools.Snapshot() + "\nCreate one cube named Assistant smoke cube with a BoxCollider3D. Add one native event rule to that cube: pressing Space sets its position to [0,2,0]. Use the catalogue to find exact condition/action arguments. Apply in one batch. Briefly confirm completion." } } }).GetAwaiter().GetResult();
        string turnId = turn.GetProperty("turn").GetProperty("id").GetString()!;
        bool done = false; var deadline = DateTime.UtcNow.AddSeconds(150);
        while (!done && DateTime.UtcNow < deadline)
        {
            while (connection.Messages.TryDequeue(out var message))
            {
                string method = AssistantTools.Text(message, "method"); var p = message.GetProperty("params");
                if (message.TryGetProperty("id", out var request))
                {
                    if (method == "item/tool/call")
                    {
                        bool success = true; object result;
                        try { var arguments = p.GetProperty("arguments"); if (arguments.ValueKind == JsonValueKind.String) arguments = Parse(arguments.GetString()!); result = tools.Execute(p.GetProperty("tool").GetString()!, arguments); }
                        catch (Exception error) { success = false; result = new { error = error.GetBaseException().Message }; }
                        Console.WriteLine("TOOL " + AssistantTools.Text(p, "tool") + " success=" + success);
                        connection.RespondAsync(request, new { success, contentItems = new[] { new { type = "inputText", text = AssistantTools.Serialize(result) } } }).GetAwaiter().GetResult();
                    }
                    else connection.RejectAsync(request, "Use native ByteEngine tools only.").GetAwaiter().GetResult();
                }
                else if (method == "turn/completed") { done = true; Console.WriteLine("TURN " + AssistantTools.Text(p.GetProperty("turn"), "status")); }
                else if (method == "error") Console.WriteLine("SERVER ERROR " + p.GetRawText()[..Math.Min(500, p.GetRawText().Length)]);
            }
            Thread.Sleep(15);
        }
        if (!done) { connection.RequestAsync("turn/interrupt", new { threadId, turnId }).GetAwaiter().GetResult(); throw new Exception("Live test timed out."); }
        var cube = state.EditorScene.FindGameObject("Assistant smoke cube");
        Check(cube?.GetComponent<BoxCollider3D>() != null, "Live assistant creates a native cube with a collider");
        Check(cube!.GetComponent<EventModuleComponent>()?.Modules.Count == 1, "Live assistant attaches an editable native event sheet");
        var module = new EventModuleSerializer().Load(Path.Combine(root, cube.GetComponent<EventModuleComponent>()!.Modules[0].ProjectPath));
        Check(module.Rules[0].Conditions.Any(c => c.Id == "input.keyPressed") && module.Rules[0].Actions.Any(), "Live assistant authors a wired Space-key event");
        foreach (var instruction in module.Rules[0].Actions)
        {
            var registry = VisualLogicRegistry.CreateDefault();
            registry.TryGetAction(instruction.Id, out var action);
            action!.Execute(instruction, new EventExecutionContext { Scene = state.EditorScene, Self = cube, Globals = new VariableStore() });
        }
        Check(cube.Transform.WorldPosition == new Vector3(0, 2, 0), "Generated native event action actually moves the cube to the requested position");
        state.Undo.Undo(state); Check(state.EditorScene.FindGameObject("Assistant smoke cube") == null, "Live assistant batch is undone as one native operation");
    }
    Console.WriteLine(checks + " checks passed. Temporary scene: " + root); return;
}

var native = new AssistantTools(state, root);
Check(AssistantTools.Serialize(Call(native, "byte_inspect", "{}")).Contains("Selected"), "Inspect exposes only bounded native scene data");
Check(AssistantTools.Serialize(Call(native, "byte_catalog", "{\"kind\":\"conditions\",\"search\":\"key pressed\"}")).Contains("input.keyPressed"), "Catalogue discovers exact native instruction IDs");
Call(native, "byte_apply", """{"label":"Build test cube","operations":[{"kind":"create_object","target":"","data":{"name":"Door","primitive":"Cube"}},{"kind":"add_component","target":"@Door","data":{"component":"BoxCollider3D"}},{"kind":"set_transform","target":"@Door","data":{"position":[1,2,3]}},{"kind":"set_variable","target":"@Door","data":{"name":"Keys","value":3}},{"kind":"add_event_rule","target":"@Door","data":{"name":"Space test","conditions":[{"id":"input.keyPressed","arguments":{"key":"Space"}}],"actions":[{"id":"object.setActive","arguments":{"target":"Self","active":true}}]}}]}""");
var door = state.EditorScene.FindGameObject("Door")!;
Check(door.GetComponent<BoxCollider3D>() != null && door.Transform.LocalPosition == new Vector3(1, 2, 3), "Atomic batch creates objects, components and transforms");
Check(door.Variables["Keys"].Number == 3 && state.IsDirty, "Variables remain native and scene is marked unsaved");
var sheet = door.GetComponent<EventModuleComponent>()!.Modules[0]; var authored = new EventModuleSerializer().Load(Path.Combine(root, sheet.ProjectPath));
Check(authored.Rules[0].HasExplicitConditionFlow && authored.Rules[0].FirstActionId == authored.Rules[0].Actions[0].InstanceId, "Generated event sheet has explicit executable wiring");
state.Undo.Undo(state); Check(state.EditorScene.FindGameObject("Door") == null && !state.IsDirty, "One Undo restores the saved scene");
Check(File.Exists(Path.Combine(root, sheet.ProjectPath)), "Detached generated assets remain available for recovery");
state.Undo.Redo(state); Check(state.EditorScene.FindGameObject("Door")?.GetComponent<EventModuleComponent>()?.Modules.Count == 1, "Redo restores native objects and event-sheet attachment");
var before = state.EditorScene.FindGameObject("Selected")!; state.SelectedObject = before;
var edit = new AssistantTools(state, root);
Call(edit, "byte_apply", """{"operations":[{"kind":"add_component","target":"$selected","data":{"component":"PointLight"}},{"kind":"set_property","target":"$selected","data":{"component":"PointLight","property":"Intensity","value":4}}]}""");
Check(before.GetComponent<PointLight>()!.Intensity == 4, "Editable component properties use native setters");
var failure = new AssistantTools(state, root);
Check(Throws(() => Call(failure, "byte_apply", """{"operations":[{"kind":"rename_object","target":"$selected","data":{"name":"Wrong"}},{"kind":"set_transform","target":"$selected","data":{"scale":[0,1,1]}}]}""")), "Invalid operation rejects the batch");
Check(state.EditorScene.FindGameObject("Selected") != null && state.EditorScene.FindGameObject("Wrong") == null, "Failed batch rolls earlier edits back");
var readOnly = new AssistantTools(state, root) { AllowEdits = false };
Check(Throws(() => Call(readOnly, "byte_apply", """{"operations":[{"kind":"create_object","target":"","data":{"name":"Forbidden"}}]}""")), "Read-only mode prevents edits");
var playing = new AssistantTools(state, root); state.Mode = EditorMode.Play;
Check(Throws(() => Call(playing, "byte_apply", """{"operations":[{"kind":"create_object","target":"","data":{"name":"Forbidden"}}]}""")), "Play mode cannot mutate the authored scene"); state.Mode = EditorMode.Edit;
var stale = new AssistantTools(state, root); state.EditorScene = new Scene("Switched");
Check(Throws(() => Call(stale, "byte_inspect", "{}")), "Stale task cannot target a switched scene");
state.EditorScene = new Scene("Argument regression"); state.SelectedObject = state.EditorScene.CreateGameObject("Cube"); state.Undo.Reset(state, true);
var argumentTest = new AssistantTools(state, root);
Check(AssistantTools.Serialize(Call(argumentTest, "byte_catalog", "{\"kind\":\"actions\",\"search\":\"transform.setPosition\"}")).Contains("Vector3"), "Legacy action catalogue exposes the native editor's vector argument type");
Call(argumentTest, "byte_apply", """{"operations":[{"kind":"add_event_rule","target":"$selected","data":{"name":"Space moves cube","conditions":[{"id":"input.keyPressed","arguments":{"key":"Space"}}],"actions":[{"id":"transform.setPosition","arguments":{"position":[0,2,0]}}]}}]}""");
var eventAsset = state.SelectedObject!.GetComponent<EventModuleComponent>()!.Modules[0];
var nativeRule = new EventModuleSerializer().Load(Path.Combine(root, eventAsset.ProjectPath)).Rules[0];
Check(nativeRule.Actions[0].Arguments["position"].Constant.Type == VariableType.Vector3, "Plain JSON vectors are converted to native typed event values");
var actionRegistry = VisualLogicRegistry.CreateDefault(); actionRegistry.TryGetAction(nativeRule.Actions[0].Id, out var nativeAction);
nativeAction!.Execute(nativeRule.Actions[0], new EventExecutionContext { Scene = state.EditorScene, Self = state.SelectedObject, Globals = new VariableStore() });
Check(state.SelectedObject.Transform.WorldPosition == new Vector3(0, 2, 0), "Generated action executes with the requested position rather than silently doing nothing");
// Reproduce the reported third-person cube setup through exactly the exposed tools.
state.EditorScene = new Scene("Third person regression"); state.SelectedObject = null; state.Undo.Reset(state, true);
var tps = new AssistantTools(state, root);
Check(AssistantTools.Serialize(Call(tps, "byte_help", "{\"search\":\"third person\"}")).Contains("DESCENDANT"), "Engine help explains third-person ownership and camera hierarchy");
Check(tps.Snapshot().Contains("Move"), "Context includes actual project input actions and bindings");
Check(AssistantTools.Serialize(Call(tps, "byte_catalog", "{\"kind\":\"components\",\"search\":\"PlayerController3D\"}")).Contains("MoveAction"), "Input action references are discoverable and editable");
Call(tps, "byte_apply", """
{"label":"Third person cube","operations":[
{"kind":"create_object","target":"","data":{"name":"Player","primitive":"Cube"}},
{"kind":"set_transform","target":"@Player","data":{"position":[0,2,0]}},
{"kind":"add_component","target":"@Player","data":{"component":"CapsuleCollider3D"}},
{"kind":"add_component","target":"@Player","data":{"component":"PlayerController3D"}},
{"kind":"add_component","target":"@Player","data":{"component":"CameraBoom3D"}},
{"kind":"create_object","target":"","data":{"name":"Follow camera","parent":"@Player"}},
{"kind":"add_component","target":"@Follow camera","data":{"component":"Camera3D"}},
{"kind":"set_property","target":"@Follow camera","data":{"component":"Camera3D","property":"ActiveGameCamera","value":true}},
{"kind":"set_property","target":"@Player","data":{"component":"CameraBoom3D","property":"CameraObjectId","value":"@Follow camera"}},
{"kind":"set_property","target":"@Player","data":{"component":"PlayerController3D","property":"MoveAction","value":"Move"}},
{"kind":"create_object","target":"","data":{"name":"Ground","primitive":"Cube"}},
{"kind":"set_transform","target":"@Ground","data":{"scale":[20,1,20],"position":[0,-0.5,0]}},
{"kind":"add_component","target":"@Ground","data":{"component":"BoxCollider3D"}}
]}
""");
var player = state.EditorScene.FindGameObject("Player")!;
var follow = state.EditorScene.FindGameObject("Follow camera")!;
var boom = player.GetComponent<ByteEngine.Core.Gameplay.CameraBoom3D>()!;
Check(player.GetComponent<CharacterController3D>() != null && follow.IsDescendantOf(player) && boom.CameraObjectId == follow.Id, "Native dependency and new-object camera reference wiring work in one batch");
Check(state.EditorScene.ActiveCamera == follow.GetComponent<Camera3D>(), "Created camera is the rendered game camera");
typeof(ByteEngine.Core.Gameplay.CameraBoom3D).GetMethod("OnStart", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(boom, null);
Check(Vector3.Distance(follow.Transform.WorldPosition, player.Transform.WorldPosition) > 2 && float.IsFinite(follow.Transform.WorldPosition.Y), "Native camera rig resolves and positions its child behind the cube");
Check(player.GetComponent<ByteEngine.Core.Gameplay.PlayerController3D>()!.MoveAction.Name == "Move", "Named movement input is authored through native setters");
state.Undo.Undo(state); Check(state.EditorScene.FindGameObject("Player") == null, "Third-person setup remains one undoable batch");
var journal = new AssistantJournal(); journal.Record("request", new { prompt = "test <cube>" }); journal.Record("tool", new { tool = "byte_apply", success = false, result = new { error = "Rollback" } }); journal.Record("assistant", new { text = "A reply" });
using var log = JsonDocument.Parse(journal.Export());
Check(log.RootElement.GetProperty("entries").GetArrayLength() == 3 && log.RootElement.GetProperty("entries")[1].GetProperty("data").GetProperty("result").GetProperty("error").GetString() == "Rollback", "Export preserves timestamped chat, tool results and failures as valid JSON");

state.EditorScene = new Scene("Fireball regression"); state.SelectedObject = state.EditorScene.CreateGameObject("Shooter"); state.Undo.Reset(state, true);
var authoring = new AssistantTools(state, root);
int contextLength = authoring.Snapshot().Length;
Check(contextLength < 1800, "Compact default context eliminates verbose irrelevant input-binding fields");
Check(AssistantTools.Serialize(Call(authoring, "byte_reference", "{\"search\":\"ProjectileLauncher3D FireInternal\"}")).Contains("ProjectileLauncher3D"), "Reference retrieval includes build-matched native projectile implementation");
Check(AssistantTools.Serialize(Call(authoring, "byte_catalog", "{\"queries\":[{\"kind\":\"asset_types\",\"search\":\"vfx\"},{\"kind\":\"actions\",\"search\":\"projectile.fire\"}]}")).Contains("create_vfx"), "One catalogue call batches asset schema and action discovery");
Call(authoring, "byte_apply", """
{"label":"Custom fireball","operations":[
{"kind":"create_vfx","target":"","data":{"name":"Fireball","path":"Assets/Vfx/Fireball.bvfx","properties":{"Loop":true,"ParticleBudget":256},"layers":[{"Name":"Core","LocalSpace":true,"Rate":30,"Burst":8,"Lifetime":0.2,"Speed":0,"Gravity":[0,0,0],"StartSize":0.35,"EndSize":0.1,"StartColor":[1,0.8,0.2,1],"EndColor":[1,0.1,0,0],"SizeOverLife":{"keys":[{"time":0,"value":1},{"time":1,"value":0}]}},{"Name":"Trail","LocalSpace":false,"Rate":35,"Lifetime":0.35,"Speed":0,"Gravity":[0,0.2,0],"StartSize":0.2,"EndSize":0.01,"StartColor":[1,0.3,0,1],"EndColor":[1,0,0,0]}]}},
{"kind":"create_material","target":"","data":{"name":"Glow","path":"Assets/Materials/Glow.bmat","properties":{"Shading":"Unlit","BaseColor":[1,0.4,0,1],"EmissionEnabled":true,"EmissionColor":[1,0.2,0],"EmissionIntensity":2}}},
{"kind":"create_object","target":"","data":{"name":"Fireball template"}},
{"kind":"add_component","target":"@Fireball template","data":{"component":"VfxPlayer","properties":{"Effect":"$asset:Fireball","PlayOnStart":true}}},
{"kind":"add_component","target":"@Fireball template","data":{"component":"Projectile3D","properties":{"Radius":0.15}}},
{"kind":"add_component","target":"@Fireball template","data":{"component":"LifetimeComponent","properties":{"LifetimeSeconds":3}}},
{"kind":"save_blueprint","target":"@Fireball template","data":{"name":"Fireball projectile","path":"Assets/Blueprints/Fireball.byteblueprint"}},
{"kind":"delete_object","target":"@Fireball template","data":{}},
{"kind":"add_component","target":"$selected","data":{"component":"ProjectileLauncher3D","properties":{"ProjectileBlueprint":"$asset:Fireball projectile","ProjectileSpeed":18,"MuzzleOffset":[0,0,-1]}}},
{"kind":"add_event_rule","target":"$selected","data":{"name":"J fires fireball","conditions":[{"id":"input.keyPressed","arguments":{"key":"J"}}],"actions":[{"id":"projectile.fire","arguments":{}}]}}
]}
""");
var effectPath = Path.Combine(root,"Assets/Vfx/Fireball.bvfx");
var authoredEffect = ByteEngine.Core.Vfx.VfxEffectSerializer.Load(effectPath);
var particleSim = new ByteEngine.Core.Vfx.VfxSimulation(authoredEffect); particleSim.Play(Matrix4x4.Identity); particleSim.Advance(.1f,Matrix4x4.CreateTranslation(0,0,-1));
Check(particleSim.Layers.All(l=>l.Count>0) && particleSim.Capacity<=authoredEffect.ParticleBudget,"All custom effect layers receive capacity and actually emit within the budget");
Check(AssistantTools.Serialize(Call(new AssistantTools(state,root),"byte_inspect","{\"asset\":\"Assets/Vfx/Fireball.bvfx\"}")).Contains("Core"),"Native assets can be inspected for later refinements");
Check(authoredEffect.Layers.Count == 2 && authoredEffect.Layers[0].StartColor.X == 1 && authoredEffect.Layers[0].SizeOverLife != null, "Custom layered VFX, vectors, colors and lifetime curves round-trip through native serializer");
Check(MaterialAssetSerializer.Load(Path.Combine(root,"Assets/Materials/Glow.bmat")).Standard.EmissionIntensity == 2, "Custom material round-trips as native editable asset");
Check(state.EditorScene.FindGameObject("Fireball template") == null, "Temporary projectile template is removed after saving its reusable Blueprint");
database.RefreshPaths(Directory.EnumerateFiles(Path.Combine(root,"Assets"),"*",SearchOption.AllDirectories));
var blueprintPath = Path.Combine(root,"Assets/Blueprints/Fireball.byteblueprint");
var fireBlueprint = new ByteEngine.Core.Blueprints.BlueprintSerializer().Load(blueprintPath);
Check(fireBlueprint.Root.Components.Any(c=>c.Type.Contains("VfxPlayer")) && fireBlueprint.Root.Components.Any(c=>c.Type.Contains("Projectile3D")), "Native Blueprint retains authored effect and projectile properties");
var spawnRegistration = RuntimeSpawnService.ConfigureBlueprintSpawner((sc, reference, position) => ByteEngine.Core.Runtime.BlueprintRuntimeFactory.Spawn(sc,reference,position,database,serializer,Console.WriteLine));
try
{
    var shooter = state.SelectedObject!;
    var refSheet = shooter.GetComponent<EventModuleComponent>()!.Modules[0];
    var rule = new EventModuleSerializer().Load(Path.Combine(root,refSheet.ProjectPath)).Rules[0];
    Check(rule.Conditions[0].Arguments["key"].Constant.String == "J", "Requested J condition is exposed in editable native event sheet");
    actionRegistry.TryGetAction(rule.Actions[0].Id,out var launch);
    launch!.Execute(rule.Actions[0],new EventExecutionContext { Scene=state.EditorScene,Self=shooter,Globals=new VariableStore() });
    var moving = state.EditorScene.GameObjects.First(o=>o.GetComponent<ByteEngine.Core.Gameplay.Projectile3D>() is {Enabled:true});
    var projectile = moving.GetComponent<ByteEngine.Core.Gameplay.Projectile3D>()!;
    var visual = moving.Children.Single();
    Check(projectile.OwnerId == shooter.Id && projectile.Velocity.Z == -18 && visual.GetComponent<ByteEngine.Core.Vfx.VfxPlayer>() != null, "Native event fires a real moving fireball with authored visual and shooter ownership");
    Check(moving.GetComponent<ByteEngine.Core.Gameplay.LifetimeComponent>()!.LifetimeSeconds == 3 && visual.GetComponent<ByteEngine.Core.Gameplay.Projectile3D>()!.Enabled == false, "Launcher transfers lifetime and avoids competing nested projectile motion");
    var start = moving.Transform.WorldPosition;
    typeof(ByteEngine.Core.Gameplay.Projectile3D).GetMethod("Simulate",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(projectile,[0.1f]);
    Check(Math.Abs(moving.Transform.WorldPosition.Z-start.Z+1.8f)<0.001f && visual.Transform.WorldPosition == moving.Transform.WorldPosition, "Projectile simulation carries the fireball visual forward");
}
finally { RuntimeSpawnService.ClearBlueprintSpawner(spawnRegistration); }
// Restore authored scene before testing Undo; the spawned objects above are runtime-only test objects.
state.Undo.Undo(state);
Check(state.EditorScene.FindGameObject("Shooter")?.GetComponent<ByteEngine.Core.Gameplay.ProjectileLauncher3D>() == null && File.Exists(effectPath), "Undo restores authored scene while keeping generated native assets recoverable");
var retry = new AssistantTools(state,root);
Check(Throws(()=>Call(retry,"byte_apply","""{"operations":[{"kind":"create_vfx","target":"","data":{"name":"Failed","path":"Assets/Vfx/Failed.bvfx","preset":"Fire"}},{"kind":"add_component","target":"$selected","data":{"component":"InvalidComponent"}}]}""")),"Failed asset/scene batch is rejected");
Check(!File.Exists(Path.Combine(root,"Assets/Vfx/Failed.bvfx")),"Rollback removes only newly generated assets from failed batch");
Call(retry,"byte_apply","""{"operations":[{"kind":"set_variable","target":"$selected","data":{"name":"Retried","value":true}}]}""");
Check(state.SelectedObject!.Variables["Retried"].Boolean,"A rolled-back batch can be corrected without starting another model request");
var noOverwrite=new AssistantTools(state,root);
string preserved=File.ReadAllText(effectPath);
Check(Throws(()=>Call(noOverwrite,"byte_apply","""{"operations":[{"kind":"create_vfx","target":"","data":{"name":"Fireball","path":"Assets/Vfx/Fireball.bvfx","preset":"Smoke"}}]}""")) && File.ReadAllText(effectPath)==preserved,"Asset creation preserves existing project assets");
var meshTools=new AssistantTools(state,root);
Call(meshTools,"byte_apply","""{"operations":[{"kind":"create_mesh","target":"","data":{"name":"Triangle","path":"Assets/Models/Triangle.obj","vertices":[[0,0,0],[1,0,0],[0,1,0]],"triangles":[[0,1,2]]}}]}""");
Check(File.ReadAllText(Path.Combine(root,"Assets/Models/Triangle.obj")).Contains("f 1//1 2//1 3//1"),"Custom bounded mesh geometry is saved as native-importable OBJ with normals");
database.RefreshPaths([Path.Combine(root,"Assets/Models/Triangle.obj")]);
var meshRecord=database.Resolve(new AssetReference("Assets/Models/Triangle.obj"))!;
var imported=ByteEngine.Core.Assets.Importers.ModelImporter.ForPath(meshRecord.FullPath).Import(meshRecord,meshRecord.Metadata.ModelImporter);
Check(imported.Meshes.Single().Indices.Length==3,"Generated custom mesh passes the engine's actual OBJ importer");
Check(AssistantTools.Serialize(Call(new AssistantTools(state,root),"byte_catalog","{\"kind\":\"components\",\"search\":\"VfxPlayer ProjectileLauncher3D\"}")).Contains("VfxPlayer"),"A space-separated list of exact component types resolves together");
Check(Throws(()=>Call(new AssistantTools(state,root),"byte_apply","""{"operations":[{"kind":"create_material","target":"","data":{"name":"Escape","path":"Assets/../Escape.bmat","properties":{}}}]}""")) && !File.Exists(Path.Combine(root,"Escape.bmat")),"Native asset writes cannot escape project Assets using traversal");

// Actual bundled mannequin: discover model-owned clips and exercise a native profile/playback.
string mannequinRelative="Assets/Characters/Mannequin.glb";
Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root,mannequinRelative))!);
File.Copy("Editor/ByteEngine.Editor/Resources/Characters/ByteEngine/Mannequin.glb",Path.Combine(root,mannequinRelative));
database.RefreshPaths([Path.Combine(root,mannequinRelative)]);
state.EditorScene=new Scene("Animation profile regression");state.SelectedObject=state.EditorScene.CreateGameObject("Mannequin");state.Undo.Reset(state,true);
state.SelectedObject.AddComponent(new ModelHierarchyInstance{Model=new AssetReference(mannequinRelative)});
for(int i=0;i<50;i++){var bone=state.EditorScene.CreateGameObject("Bone "+i);bone.SetParent(state.SelectedObject,false);}
var animTools=new AssistantTools(state,root);
var discovered=Parse(AssistantTools.Serialize(Call(animTools,"byte_animations","{}")));
var animatedModel=discovered[0];var recommended=animatedModel.GetProperty("suggestedLocomotion");
string idleClip=recommended.GetProperty("idle").GetString()!,walkClip=recommended.GetProperty("walk").GetString()!,runClip=recommended.GetProperty("run").GetString()!;
Check(animatedModel.GetProperty("hasSkeleton").GetBoolean() && animatedModel.GetProperty("totalClips").GetInt32()>3 && idleClip.Length>0 && walkClip.Length>0 && runClip.Length>0,"Native animation discovery loads real mannequin clips, rig and recommended locomotion");
Check(Parse(AssistantTools.Serialize(Call(new AssistantTools(state,root),"byte_animations",JsonSerializer.Serialize(new {target=state.SelectedObject!.Children.First().Id.ToString()}))))[0].GetProperty("hasSkeleton").GetBoolean(),"Selecting an imported bone still discovers its enclosing model animations");
Check(!idleClip.Contains("crouch",StringComparison.OrdinalIgnoreCase) && !idleClip.Contains("pistol",StringComparison.OrdinalIgnoreCase),"Idle recommendation avoids crouch/weapon variants");
Check(AssistantTools.Serialize(Call(animTools,"byte_catalog","{\"kind\":\"assets\",\"search\":\"Assets/Characters/Mannequin.glb\"}")).Contains(mannequinRelative),"Asset catalogue matches forward-slash paths on Windows");
var compactInspect=Parse(AssistantTools.Serialize(Call(animTools,"byte_inspect","{}")));
Check(compactInspect.GetProperty("objects").GetArrayLength()==1 && compactInspect.GetProperty("hiddenTransformNodes").GetInt32()==50,"Normal scene inspection hides imported transform-only bones");
Check(Parse(AssistantTools.Serialize(Call(animTools,"byte_inspect","{\"includeBones\":true}"))).GetProperty("objects").GetArrayLength()>1,"Explicit bone inspection remains available");
Call(animTools,"byte_apply",JsonSerializer.Serialize(new {label="Mannequin locomotion",operations=new object[]{
 new{kind="create_animation_profile",target="",data=new{name="Mannequin locomotion",path="Assets/Animations/Mannequin.byteanim",model=mannequinRelative,locomotion=new{Idle=idleClip,Walk=walkClip,Run=runClip,RunThreshold=4,RootMotionMode="InPlace"},sections=new{Actions=new[]{new{Name="Test action",Clip=idleClip,Loop=false}}}}},
 new{kind="add_component",target="$selected",data=new{component="CharacterController3D",properties=new{MoveSpeed=2}}},
 new{kind="add_component",target="$selected",data=new{component="AnimationController",properties=new{AnimationProfile="$asset:Mannequin locomotion",DriveLocomotion=true}}},
 new{kind="add_event_rule",target="$selected",data=new{name="Sprint held",conditions=new[]{new{id="input.actionHeld",arguments=new{action="Sprint"}}},actions=new[]{new{id="character.setMoveSpeed",arguments=new{speed=6}}}}},
 new{kind="add_event_rule",target="$selected",data=new{name="Sprint released",conditions=new[]{new{id="input.actionReleased",arguments=new{action="Sprint"}}},actions=new[]{new{id="character.setMoveSpeed",arguments=new{speed=2}}}}}
}}));
var profileAsset=Path.Combine(root,"Assets/Animations/Mannequin.byteanim");
var animationProfile=ByteEngine.Core.Animation.AnimationProfileSerializer.Load(profileAsset);
Check(animationProfile.Locomotion.Idle==idleClip && animationProfile.Locomotion.Walk==walkClip && animationProfile.Locomotion.Run==runClip && animationProfile.Actions.Count==1,"Native animation profile persists exact discovered clips and action sections");
Check(AssistantTools.Serialize(Call(new AssistantTools(state,root),"byte_inspect","{\"asset\":\"Assets/Animations/Mannequin.byteanim\"}")).Contains(idleClip),"Saved animation profiles are inspectable through native assistant tools");
database.RefreshPaths([profileAsset]);
using(var animationWindow=new GameWindow(GameWindowSettings.Default,new NativeWindowSettings{StartVisible=false,ClientSize=new(320,240),APIVersion=new(3,3),Profile=ContextProfile.Core}))
{
 animationWindow.Context.MakeCurrent();
 var animationRegistration=ByteEngine.Core.Animation.AnimationRuntimeAssets.Configure(assets);
 try
 {
  var driver=state.SelectedObject!.GetComponent<ByteEngine.Core.Animation.AnimationController>()!;
  Check(driver.ApplyAnimationProfile() && driver.DriveLocomotion && driver.RootMotionMode==ByteEngine.Core.Animation.RootMotionMode.InPlace,"Runtime controller loads authored profile with in-place locomotion");
  driver.RefreshRenderers();
  var skinned=state.SelectedObject.GetComponent<ByteEngine.Core.Graphics.ThreeD.SkeletalMeshRenderer>();
  Check(skinned!=null && skinned.SkinnedMeshCount>0,"Native controller creates actual skeletal playback for imported mannequin");
  foreach(string clip in new[]{idleClip,walkClip,runClip}) Check(driver.Play(clip) && skinned!.Seek(.2f) && skinned.CurrentAnimation==clip,"Native mannequin plays and skins clip: "+clip);
  var locomotionResolve=typeof(ByteEngine.Core.Animation.AnimationController).GetMethod("ResolveLocomotionStateStable",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
  var motor=state.SelectedObject.GetComponent<CharacterController3D>()!;
  typeof(CharacterController3D).GetProperty("IsGrounded")!.SetValue(motor,true);
  var stateType=typeof(ByteEngine.Core.Animation.AnimationController).Assembly.GetType("ByteEngine.Core.Animation.LocomotionState")!;
  object previous=Enum.Parse(stateType,"Idle");
  foreach(var pair in new[]{(0f,"Idle"),(2f,"Walk"),(6f,"Run")})
  {
   typeof(CharacterController3D).GetProperty("Velocity")!.SetValue(motor,new Vector3(pair.Item1,0,0));
   var result=locomotionResolve.Invoke(null,[motor,previous,false,.05f,4f,.15f]);
   Check(result!.ToString()==pair.Item2,"Native locomotion selects "+pair.Item2+" at speed "+pair.Item1);
  }
  var sprintSheets=state.SelectedObject.GetComponent<EventModuleComponent>()!.Modules.Select(r=>new EventModuleSerializer().Load(Path.Combine(root,r.ProjectPath))).ToArray();
  Check(sprintSheets.Length==1 && sprintSheets[0].Rules.Count==2 && sprintSheets[0].Name=="Mannequin Events", "Walk/run rows share one descriptively named sheet");
  foreach(var pair in new[]{(0,6f),(1,2f)})
  {
   var instruction=sprintSheets[0].Rules[pair.Item1].Actions[0];actionRegistry.TryGetAction(instruction.Id,out var speedAction);
   speedAction!.Execute(instruction,new EventExecutionContext{Scene=state.EditorScene,Self=state.SelectedObject,Globals=new VariableStore()});
   Check(motor.MoveSpeed==pair.Item2,"Editable sprint event sets movement speed "+pair.Item2);
  }
  typeof(ByteEngine.Core.Graphics.ThreeD.SkeletalMeshRenderer).GetMethod("OnDestroy",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.Invoke(skinned,null);
 }
 finally{ByteEngine.Core.Animation.AnimationRuntimeAssets.Clear(animationRegistration);}
}
state.Undo.Undo(state);
Check(state.EditorScene.FindGameObject("Mannequin")!.GetComponent<ByteEngine.Core.Animation.AnimationController>()==null && File.Exists(profileAsset),"Animation setup has one scene Undo and retains native profile for recovery");
var invalidAnimation=new AssistantTools(state,root);
Check(Throws(()=>Call(invalidAnimation,"byte_apply",JsonSerializer.Serialize(new{operations=new[]{new{kind="create_animation_profile",target="",data=new{name="Invalid",path="Assets/Animations/Invalid.byteanim",model=mannequinRelative,locomotion=new{Idle="Invented missing clip"}}}}}))) && !File.Exists(Path.Combine(root,"Assets/Animations/Invalid.byteanim")),"Profile authoring rejects nonexistent clips without leaving assets");
Check(AssistantTools.Serialize(AssistantReference.Search("", "Core.Animation/AnimationController.cs", 265)).Contains("ApplyAnimationProfile"),"Engine reference can read the relevant method beyond initial search snippets");
var sheetOwner = state.EditorScene.CreateGameObject("Sheet owner");
state.Selection.Set([sheetOwner]);
string EventBatch(string name, bool fail = false) => JsonSerializer.Serialize(new { operations =
    new object[] { new { kind = "add_event_rule", target = "$selected", data = new { sheet = "Player Movement", name,
        conditions = new[] { new { id = "input.keyPressed", arguments = new { key = "Space" } } },
        actions = new[] { new { id = "object.setActive", arguments = new { active = true } } } } } }
        .Concat(fail ? new object[] { new { kind = "invalid_operation", target = "$selected", data = new { } } } : []) });
Call(new AssistantTools(state, root), "byte_apply", EventBatch("Jump pressed - jump"));
string movementPath = Path.Combine(root, "Assets/Events/Assistant/Player Movement.byteevents");
byte[] originalMovement = File.ReadAllBytes(movementPath);
Call(new AssistantTools(state, root), "byte_apply", EventBatch("Sprint held - run"));
Check(new EventModuleSerializer().Load(movementPath).Rules.Count == 2 && state.SelectedObject!.GetComponent<EventModuleComponent>()!.Modules.Count == 1,
    "Follow-up request appends to the existing named sheet without another asset");
Check(AssistantTools.Serialize(Call(new AssistantTools(state, root), "byte_inspect", """{"detail":true}""")).Contains("Player Movement"),
    "Inspection exposes existing sheet names for reuse");
state.Undo.Undo(state);
Check(File.ReadAllBytes(movementPath).SequenceEqual(originalMovement), "Undo restores exact original event asset contents");
state.Undo.Redo(state);
Check(new EventModuleSerializer().Load(movementPath).Rules.Count == 2, "Redo restores appended event rows");
byte[] updatedMovement = File.ReadAllBytes(movementPath);
Check(Throws(() => Call(new AssistantTools(state, root), "byte_apply", EventBatch("Walk resumes", true))) && File.ReadAllBytes(movementPath).SequenceEqual(updatedMovement),
    "Failed follow-up batch restores existing event file and scene");
Check(Throws(() => Call(new AssistantTools(state, root), "byte_apply", EventBatch("Sprint held - run"))) && File.ReadAllBytes(movementPath).SequenceEqual(updatedMovement),
    "Duplicate rule names are rejected without corrupting the existing sheet");
Check(Throws(() => Call(new AssistantTools(state, root), "byte_apply", EventBatch("Traversal").Replace("Player Movement", "../Escape"))),
    "Sheet names reject path traversal");
var otherOwner = state.EditorScene.CreateGameObject("Other sheet owner"); state.Selection.Set([otherOwner]);
Check(Throws(() => Call(new AssistantTools(state, root), "byte_apply", EventBatch("Unrelated rule"))) && File.ReadAllBytes(movementPath).SequenceEqual(updatedMovement),
    "Unrelated objects cannot silently reuse or overwrite a named sheet");
Console.WriteLine("Compact context characters: "+contextLength);
Console.WriteLine(checks + " checks passed. Temporary scene: " + root);
