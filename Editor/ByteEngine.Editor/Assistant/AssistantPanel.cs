using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using ImGuiNET;

namespace ByteEngine.Editor.Assistant;

internal sealed class AssistantPanel : IDisposable
{
    public bool IsOpen { get; set; }
    private CodexConnection? _connection;
    private EditorState? _turnState;
    private ByteEngine.Core.Scene.Scene? _contextScene;
    private AssistantTools? _tools;
    private readonly ConcurrentQueue<Action> _ui = new();
    private sealed class Message(string role, string text) { public string Role = role; public string Text = text; }
    private sealed record Model(string Id, string Name, string[] Efforts);
    private readonly List<Message> _messages = [];
    private readonly AssistantJournal _journal = new();
    private Model[] _models = [];
    private Message? _reply;
    private string _executable = CodexConnection.FindExecutable();
    private string _input = "", _status = "Connect to use your existing Codex sign-in.", _plan = "", _model = "", _effort = "low";
    private string? _threadId, _turnId;
    private volatile bool _busy, _connecting, _signedIn;
    private bool _allowEdits = true, _scroll;
    private long _tokens, _inputTokens, _cachedTokens, _outputTokens;
    private int _nativeCalls, _toolResultCharacters;
    private int _taskTurns;
    private string _allowance = "", _lastContext = "", _lastAppliedSummary = "";
    private readonly List<string> _changes = [];

    // Called even when the dock is closed; native tool calls must always run on the editor thread.
    public void Pump(EditorState? state, EditorProjectContext? project)
    {
        while (_ui.TryDequeue(out var update)) update();
        if (_busy && !ReferenceEquals(state, _turnState)) Disconnect("Project changed. Start a new assistant task.");
        if (!ReferenceEquals(_contextScene, state?.EditorScene))
        {
            if (_busy) Disconnect("Scene changed. Start a new assistant task.");
            else if (_threadId != null) NewTask(false);
            _contextScene = state?.EditorScene;
        }
        if (_connection == null) return;
        if (!_connection.Connected && !_connecting) { _busy = false; _signedIn = false; _status = "Codex disconnected. Reconnect to continue."; }
        while (_connection != null && _connection.Messages.TryDequeue(out var message)) Handle(message);
    }
    public void Draw(EditorState? state, EditorProjectContext? project)
    {
        if (!IsOpen) return;
        ImGui.SetNextWindowSize(new(470, 650), ImGuiCond.FirstUseEver);
        bool open = IsOpen;
        bool visible = ImGui.Begin("Byte Assistant", ref open); IsOpen = open;
        if (!visible) { ImGui.End(); return; }
        ImGui.TextColored(new(.35f, .8f, 1, 1), "BUILD WITH YOUR WORDS");
        ImGui.TextWrapped("Native scene edits, asset authoring and engine reference.");
        ImGui.Separator();
        ImGui.TextWrapped(_status);
        if (_plan.Length > 0) ImGui.TextDisabled("ChatGPT " + _plan + " | Shares your Codex allowance");
        ImGui.BeginDisabled(_busy || _connecting);
        if (_connection?.Connected != true)
        {
            if (ImGui.Button(_connecting ? "Connecting..." : "Connect Codex")) Run(ConnectAsync);
        }
        else
        {
            if (!_signedIn && ImGui.Button("Sign in with ChatGPT")) Run(LoginAsync);
            if (ImGui.SmallButton("Disconnect")) Disconnect("Disconnected. Your other Codex sessions are unaffected.");
            ImGui.SameLine(); if (ImGui.SmallButton("New task")) NewTask();
            ImGui.SameLine(); if (ImGui.SmallButton("Refresh usage")) Run(RefreshAsync);
        }
        if (_models.Length > 0)
        {
            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo("##AssistantModel", _models.FirstOrDefault(m => m.Id == _model)?.Name ?? _model))
            {
                foreach (var model in _models) if (ImGui.Selectable(model.Name, model.Id == _model))
                { _model = model.Id; _effort = model.Efforts.Contains("low") ? "low" : model.Efforts.FirstOrDefault() ?? "low"; NewTask(false); }
                ImGui.EndCombo();
            }
            var efforts = _models.FirstOrDefault(m => m.Id == _model)?.Efforts ?? ["low"];
            ImGui.SetNextItemWidth(150);
            if (ImGui.BeginCombo("Reasoning", _effort)) { foreach (var e in efforts) if (ImGui.Selectable(e, _effort == e)) _effort = e; ImGui.EndCombo(); }
        }
        ImGui.Checkbox("Allow scene edits", ref _allowEdits);
        ImGui.EndDisabled();
        if (_allowance.Length > 0) ImGui.TextDisabled(_allowance);
        if (_tokens > 0)
        {
            ImGui.TextDisabled($"Request: {_tokens:N0} tokens | cached input: {_cachedTokens:N0}");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"Input {_inputTokens:N0} (includes cached), output {_outputTokens:N0}.\nModel-reported tokens across this request's rounds; not subscription credits or a price.");
        }
        if (ImGui.CollapsingHeader("Connection settings"))
        {
            ImGui.BeginDisabled(_connection?.Connected == true || _connecting);
            ImGui.SetNextItemWidth(-1); ImGui.InputText("##CodexExecutable", ref _executable, 2048);
            if (ImGui.SmallButton("Find Codex")) _executable = CodexConnection.FindExecutable();
            ImGui.SameLine(); if (ImGui.SmallButton("Browse...")) { using var picker = new System.Windows.Forms.OpenFileDialog { Filter = "Codex executable|codex.exe", Title = "Choose native codex.exe" }; if (picker.ShowDialog() == System.Windows.Forms.DialogResult.OK) _executable = picker.FileName; }
            ImGui.EndDisabled();
            ImGui.TextWrapped("Local prototype using Codex app-server. No API key and no separate API billing. Requests use your existing subscription allowance.");
        }
        if (ImGui.SmallButton("Export chat and activity..."))
        {
            using var picker = new System.Windows.Forms.SaveFileDialog { Filter = "Assistant session log (*.json)|*.json", FileName = "ByteAssistant-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json", Title = "Export chat and native tool activity" };
            if (picker.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                try { File.WriteAllText(picker.FileName, _journal.Export(_messages.Select(m => new { role = m.Role, text = m.Text }).ToArray())); _status = "Chat and tool activity exported."; }
                catch (Exception error) { _status = "Export failed: " + error.Message; }
            }
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Exports this editor session: chat, context, model, tool arguments/results, changes and errors. Includes project data; review before sharing.");
        float transcriptHeight = Math.Max(100, ImGui.GetContentRegionAvail().Y - 150);
        ImGui.BeginChild("##AssistantConversation", new(0, transcriptHeight), ImGuiChildFlags.Borders);
        if (_messages.Count == 0) ImGui.TextWrapped("Try: Inspect the selected object.\nOr: Make a custom fireball and fire it with J.\nOr: Add a Space-key event that moves this object up.");
        foreach (var message in _messages)
        {
            ImGui.TextColored(message.Role == "You" ? new(.4f, .8f, 1, 1) : new Vector4(.86f, .87f, .9f, 1), message.Role);
            ImGui.PushTextWrapPos(0); ImGui.TextUnformatted(message.Text); ImGui.PopTextWrapPos(); ImGui.Spacing();
        }
        if (_scroll) { ImGui.SetScrollHereY(1); _scroll = false; } ImGui.EndChild();
        ImGui.BeginDisabled(_busy || !_signedIn || state == null || project == null || state.Mode != EditorMode.Edit);
        ImGui.InputTextMultiline("##AssistantPrompt", ref _input, 6000, new(-1, 66));
        if (ImGui.Button("Send", new(90, 0)) && !string.IsNullOrWhiteSpace(_input)) StartTurn(state!, project!);
        ImGui.EndDisabled();
        ImGui.SameLine(); ImGui.BeginDisabled(!_busy); if (ImGui.Button("Stop")) Run(InterruptAsync); ImGui.EndDisabled();
        string selectedLabel = state?.SelectedObject is {} selected ? "Selected: " + selected.Name : "No object selected";
        ImGui.TextDisabled(selectedLabel.Length > 42 ? selectedLabel[..39] + "..." : selectedLabel);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(selectedLabel);
        if (state?.Mode != EditorMode.Edit) ImGui.TextDisabled("Open a scene in Edit mode to send requests.");
        if (ImGui.CollapsingHeader("Last changes and context"))
        {
            foreach (var change in _changes) ImGui.TextWrapped(change);
            ImGui.TextWrapped(_lastContext);
            ImGui.TextDisabled("Scene edits stay unsaved until you save. Ctrl+Z undoes the batch.");
        }
        ImGui.End();
    }
    private void StartTurn(EditorState state, EditorProjectContext project)
    {
        // Keep the visible journal, but do not replay accumulated catalogue/tool dumps.
        _threadId = _turnId = null; _tokens = _inputTokens = _cachedTokens = _outputTokens = 0;
        _nativeCalls = _toolResultCharacters = 0;
        _turnState = state; _tools = new(state, project.ProjectRoot) { AllowEdits = _allowEdits };
        _lastContext = _tools.Snapshot(); string prompt = _input; _input = ""; _changes.Clear();
        _journal.Record("request", new { prompt, model = _model, effort = _effort, context = _lastContext, allowEdits = _allowEdits });
        string recent = string.Join("\n", _messages.TakeLast(4).Select(m => m.Role + ": " + m.Text[..Math.Min(m.Text.Length, 500)]));
        string recipes = AssistantTools.Serialize(AssistantKnowledge.Search(prompt + " " + string.Join(" ", _messages.Where(m => m.Role == "You").TakeLast(1).Select(m => m.Text))));
        string turnText = "Editor context: " + _lastContext + "\nRelevant engine recipes: " + recipes + "\nLast applied engine edits/assets: " + _lastAppliedSummary + "\nRecent conversation (context only):\n" + recent + "\nUser request: " + prompt;
        _journal.Record("context_size", new { characters = turnText.Length, freshThread = true });
        Add("You", prompt); _reply = Add("Assistant", ""); _busy = true; _taskTurns++;
        Run(async connection =>
        {
            var account = await connection.RequestAsync("account/read", new { refreshToken = false });
            if (!account.TryGetProperty("account", out var currentAccount) || currentAccount.ValueKind != JsonValueKind.Object || AssistantTools.Text(currentAccount, "type") != "chatgpt")
                throw new InvalidOperationException("ChatGPT sign-in is required. This assistant does not use API-key billing.");
            if (_threadId == null)
            {
                var thread = await connection.RequestAsync("thread/start", AssistantTools.ThreadParameters(_model));
                _threadId = thread.GetProperty("thread").GetProperty("id").GetString();
            }
            var turn = await connection.RequestAsync("turn/start", new { threadId = _threadId, model = _model, effort = _effort,
                environments = Array.Empty<object>(), input = new[] { new { type = "text", text = turnText } } });
            _turnId = turn.GetProperty("turn").GetProperty("id").GetString();
        }, clearBusyOnError: true);
    }
    private async Task ConnectAsync(CodexConnection _)
    {
        await _connection!.ConnectAsync(_executable);
        await RefreshAsync(_connection);
    }
    private async Task RefreshAsync(CodexConnection connection)
    {
        var account = await connection.RequestAsync("account/read", new { refreshToken = false });
        bool signedIn = account.TryGetProperty("account", out var a) && a.ValueKind == JsonValueKind.Object && AssistantTools.Text(a, "type") == "chatgpt";
        string plan = signedIn ? AssistantTools.Text(a, "planType", "subscription") : "";
        Model[] models = [];
        if (signedIn)
        {
            var listing = await connection.RequestAsync("model/list", new { includeHidden = false });
            models = listing.GetProperty("data").EnumerateArray().Where(m => !m.TryGetProperty("hidden", out var hidden) || !hidden.GetBoolean())
                .Select(m => new Model(m.GetProperty("model").GetString()!, m.GetProperty("displayName").GetString()!, m.GetProperty("supportedReasoningEfforts").EnumerateArray().Select(e => e.GetProperty("reasoningEffort").GetString()!).ToArray())).ToArray();
            var defaultModel = listing.GetProperty("data").EnumerateArray().FirstOrDefault(m => m.TryGetProperty("isDefault", out var d) && d.GetBoolean());
            string? defaultId = defaultModel.ValueKind == JsonValueKind.Object ? defaultModel.GetProperty("model").GetString() : models.FirstOrDefault()?.Id;
            var result = models;
            _ui.Enqueue(() => { _models = result; if (!result.Any(m => m.Id == _model)) _model = defaultId ?? ""; var options = result.FirstOrDefault(m => m.Id == _model)?.Efforts ?? []; if (!options.Contains(_effort)) _effort = options.Contains("low") ? "low" : options.FirstOrDefault() ?? "low"; });
            try { var limits = await connection.RequestAsync("account/rateLimits/read", new { }); string text = Allowance(limits); _ui.Enqueue(() => _allowance = text); } catch { }
        }
        _ui.Enqueue(() => { _signedIn = signedIn; _plan = plan; _connecting = false; _status = signedIn ? "Ready. Short tasks use the selected model and reasoning level." : "ChatGPT sign-in required. API-key billing is not used by this assistant."; });
    }
    private async Task LoginAsync(CodexConnection connection)
    {
        var result = await connection.RequestAsync("account/login/start", new { type = "chatgpt" });
        string url = result.GetProperty("authUrl").GetString()!;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https") throw new InvalidOperationException("Codex returned an invalid sign-in URL.");
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        _ui.Enqueue(() => _status = "Finish signing in in your browser. This uses your Codex allowance.");
    }
    private async Task InterruptAsync(CodexConnection connection)
    {
        if (_threadId != null && _turnId != null) await connection.RequestAsync("turn/interrupt", new { threadId = _threadId, turnId = _turnId });
        else _ui.Enqueue(() => Disconnect("Stopped before the request started. Reconnect to continue."));
    }
    private void Handle(JsonElement message)
    {
        string method = AssistantTools.Text(message, "method");
        var p = message.TryGetProperty("params", out var value) ? value : default;
        if (message.TryGetProperty("id", out var requestId))
        {
            if (method == "item/tool/call")
            {
                object result; bool success;
                JsonElement loggedArguments = p.TryGetProperty("arguments", out var originalArguments) ? originalArguments.Clone() : default;
                try
                {
                    if (!_busy || _tools == null || AssistantTools.Text(p, "threadId") != _threadId) throw new InvalidOperationException("This task is no longer active.");
                    JsonElement arguments = p.GetProperty("arguments");
                    if (arguments.ValueKind == JsonValueKind.String) { using var doc = JsonDocument.Parse(arguments.GetString()!); arguments = doc.RootElement.Clone(); }
                    loggedArguments = arguments.Clone();
                    result = _tools.Execute(AssistantTools.Text(p, "tool"), arguments); success = true;
                    _changes.Clear(); _changes.AddRange(_tools.Changes);
                }
                catch (Exception error) { result = new { error = error.GetBaseException().Message }; success = false; }
                _nativeCalls++;
                _journal.Record("tool", new { tool = AssistantTools.Text(p, "tool"), arguments = loggedArguments.ValueKind == JsonValueKind.Undefined ? (object?)null : loggedArguments, success, result, changes = _tools?.Changes.ToArray() });
                string text = AssistantTools.Serialize(result);
                if (success && AssistantTools.Text(p, "tool") == "byte_apply") _lastAppliedSummary = text[..Math.Min(text.Length, 3000)];
                _toolResultCharacters += text.Length; if (text.Length > 48000) { text = "Tool response too large. Use a narrower search or inspect a smaller object."; success = false; }
                Run(c => c.RespondAsync(requestId, new { contentItems = new[] { new { type = "inputText", text } }, success }));
                if (_tools?.BudgetExhausted == true) Run(InterruptAsync);
            }
            else if (method.EndsWith("requestApproval", StringComparison.Ordinal))
                Run(c => c.RespondAsync(requestId, new { decision = "decline" }));
            else Run(c => c.RejectAsync(requestId, "This editor integration supports native ByteEngine tools only. Ask questions in your reply."));
            return;
        }
        switch (method)
        {
            case "turn/started": _turnId = p.GetProperty("turn").GetProperty("id").GetString(); _status = "Working with native engine tools..."; break;
            case "item/agentMessage/delta": if (_reply != null && _reply.Text.Length < 24000) { _reply.Text += AssistantTools.Text(p, "delta"); _scroll = true; } break;
            case "turn/completed":
                if (_reply != null) _journal.Record("assistant", new { text = _reply.Text });
                _journal.Record("turn_completed", new { status = AssistantTools.Text(p.GetProperty("turn"), "status"), requestTokens = _tokens, inputTokens = _inputTokens, cachedInputTokens = _cachedTokens, outputTokens = _outputTokens, nativeToolCalls = _nativeCalls, toolResultCharacters = _toolResultCharacters });
                _busy = false; _turnId = null; string status = AssistantTools.Text(p.GetProperty("turn"), "status");
                _status = status == "completed" ? "Done. Scene edits can be undone with Ctrl+Z." : "Task " + status + ". Any completed scene edit is still undoable.";
                if (p.GetProperty("turn").TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object) Add("Notice", AssistantTools.Text(error, "message", "Request failed."));
                break;
            case "thread/tokenUsage/updated":
                if (p.TryGetProperty("tokenUsage", out var usage) && usage.TryGetProperty("total", out var total))
                {
                    long Count(string name) => total.TryGetProperty(name, out var count) && count.TryGetInt64(out long n) ? n : 0;
                    _tokens = Count("totalTokens"); _inputTokens = Count("inputTokens"); _cachedTokens = Count("cachedInputTokens"); _outputTokens = Count("outputTokens");
                    _journal.Record("usage", new { requestTokens = _tokens, inputTokens = _inputTokens, cachedInputTokens = _cachedTokens, outputTokens = _outputTokens });
                }
                break;
            case "account/login/completed": if (p.TryGetProperty("success", out var ok) && ok.GetBoolean()) Run(RefreshAsync); else _status = "Sign-in was not completed."; break;
            case "account/updated": if (AssistantTools.Text(p, "authMode") != "chatgpt") { _signedIn = false; _status = "ChatGPT sign-in required before sending another request."; } break;
            case "account/rateLimits/updated": _allowance = Allowance(p); break;
            case "error": if (p.TryGetProperty("error", out var e)) Add("Notice", AssistantTools.Text(e, "message", "Codex reported an error.")); break;
        }
    }
    private static string Allowance(JsonElement value)
    {
        var windows = new List<double>();
        void Find(JsonElement item)
        {
            if (item.ValueKind == JsonValueKind.Object) foreach (var property in item.EnumerateObject())
            { if (property.Name == "usedPercent" && property.Value.TryGetDouble(out var used)) windows.Add(Math.Clamp(100 - used, 0, 100)); else Find(property.Value); }
            else if (item.ValueKind == JsonValueKind.Array) foreach (var child in item.EnumerateArray()) Find(child);
        }
        Find(value); return windows.Count > 0 ? "Remaining allowance windows: " + string.Join(" / ", windows.Take(2).Select(n => n.ToString("0") + "%")) : "Usage limits unavailable.";
    }
    private void Run(Func<CodexConnection, Task> operation, bool clearBusyOnError = false)
    {
        if (_connection == null) { _connection = new(); _connecting = true; }
        var connection = _connection;
        _ = Task.Run(async () =>
        {
            try { await operation(connection); }
            catch (Exception error) { _ui.Enqueue(() => { if (!ReferenceEquals(_connection, connection)) return; _connecting = false; if (clearBusyOnError) _busy = false; _status = error.GetBaseException().Message; Add("Notice", _status); }); }
        });
    }
    private Message Add(string role, string text) { if (role == "Notice") _journal.Record("notice", new { text }); var message = new Message(role, text); _messages.Add(message); if (_messages.Count > 40) _messages.RemoveAt(0); _scroll = true; return message; }
    private void NewTask(bool clear = true) { _lastAppliedSummary = ""; _journal.Record("new_task", new { clearVisibleChat = clear }); _threadId = _turnId = null; _taskTurns = 0; _tokens = _inputTokens = _cachedTokens = _outputTokens = 0; _tools = null; _reply = null; if (clear) { _messages.Clear(); _changes.Clear(); _lastContext = ""; } }
    private void Disconnect(string status) { if (_busy && _reply != null) _journal.Record("assistant_interrupted", new { text = _reply.Text }); _journal.Record("disconnect", new { status }); var connection = _connection; _connection = null; connection?.Dispose(); _busy = _connecting = _signedIn = false; _plan = ""; NewTask(false); _status = status; }
    public void Dispose() => Disconnect("Disconnected.");
}
