using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ByteEngine.Editor.Assistant;

/// <summary>Line-delimited Codex app-server transport. No API key or credential handling in the editor.</summary>
internal sealed class CodexConnection : IDisposable
{
    private Process? _process;
    private readonly SemaphoreSlim _writer = new(1);
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> _pending = new();
    private long _nextId;
    private bool _disposed;
    private volatile bool _readClosed;
    public ConcurrentQueue<JsonElement> Messages { get; } = new();
    public bool Connected => _process is { HasExited: false } && !_disposed && !_readClosed;
    public static string Workspace => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ByteEngine", "AssistantWorkspace");

    public static string FindExecutable()
    {
        foreach (string folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            if (File.Exists(Path.Combine(folder, "codex.exe"))) return Path.Combine(folder, "codex.exe");
        string bundled = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "bin");
        if (Directory.Exists(bundled))
            return Directory.EnumerateDirectories(bundled).OrderByDescending(Directory.GetLastWriteTimeUtc)
                .Select(p => Path.Combine(p, "codex.exe")).FirstOrDefault(File.Exists) ?? "";
        return "";
    }

    public async Task ConnectAsync(string executable)
    {
        if (_process != null) throw new InvalidOperationException("Already connected. Disconnect before reconnecting.");
        if (!File.Exists(executable) || !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Choose the native codex.exe installed with Codex.");
        Directory.CreateDirectory(Workspace);
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = new System.Text.UTF8Encoding(false), StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8, WorkingDirectory = Workspace };
        start.ArgumentList.Add("app-server"); start.ArgumentList.Add("--stdio");
        foreach (string feature in new[] { "shell_tool", "unified_exec", "apps", "multi_agent", "skill_search", "code_mode" })
        { start.ArgumentList.Add("--disable"); start.ArgumentList.Add(feature); }
        start.ArgumentList.Add("-c"); start.ArgumentList.Add("project_doc_max_bytes=0");
        start.ArgumentList.Add("-c"); start.ArgumentList.Add("mcp_servers={}");
        start.ArgumentList.Add("-c"); start.ArgumentList.Add("plugins={}");
        start.ArgumentList.Add("--enable"); start.ArgumentList.Add("skip_host_skill_discovery");
        _process = Process.Start(start) ?? throw new InvalidOperationException("Could not start Codex.");
        _ = ReadAsync(_process);
        // Drain stderr without persisting credentials, prompts or unbounded diagnostic text.
        _ = Task.Run(async () => { try { while (await _process.StandardError.ReadLineAsync() != null) { } } catch { } });
        await RequestAsync("initialize", new { clientInfo = new { name = "byteengine_assistant", title = "ByteEngine Assistant", version = "0.1.0" }, capabilities = new { experimentalApi = true } });
        await NotifyAsync("initialized", new { });
    }

    public async Task<JsonElement> RequestAsync(string method, object parameters, int timeoutSeconds = 30)
    {
        if (!Connected) throw new InvalidOperationException("Connect to Codex first.");
        long id = Interlocked.Increment(ref _nextId);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = completion;
        try
        {
            await SendAsync(new { id, method, @params = parameters });
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(timeoutSeconds));
        }
        finally { _pending.TryRemove(id, out _); }
    }
    public Task NotifyAsync(string method, object parameters) => SendAsync(new { method, @params = parameters });
    public Task RespondAsync(JsonElement id, object result) => SendAsync(new { id, result });
    public Task RejectAsync(JsonElement id, string message) => SendAsync(new { id, error = new { code = -32601, message } });
    private async Task SendAsync(object message)
    {
        await _writer.WaitAsync();
        try
        {
            if (!Connected) throw new InvalidOperationException("Codex disconnected.");
            await _process!.StandardInput.WriteLineAsync(JsonSerializer.Serialize(message));
            await _process.StandardInput.FlushAsync();
        }
        finally { _writer.Release(); }
    }
    private async Task ReadAsync(Process process)
    {
        try
        {
            while (await process.StandardOutput.ReadLineAsync() is { } line)
            {
                if (line.Length > 4 * 1024 * 1024) continue;
                using var document = JsonDocument.Parse(line);
                JsonElement message = document.RootElement;
                if (!message.TryGetProperty("method", out _) && message.TryGetProperty("id", out var id) && id.TryGetInt64(out long key) && _pending.TryGetValue(key, out var request))
                {
                    if (message.TryGetProperty("error", out var error))
                        request.TrySetException(new InvalidOperationException(error.TryGetProperty("message", out var text) ? text.GetString() : "Codex request failed."));
                    else if (message.TryGetProperty("result", out var result)) request.TrySetResult(result.Clone());
                }
                else if (message.TryGetProperty("method", out var method) &&
                    (message.TryGetProperty("id", out _) || method.GetString() is "item/agentMessage/delta" or "item/completed" or "turn/started" or "turn/completed" or "thread/tokenUsage/updated" or "account/login/completed" or "account/updated" or "account/rateLimits/updated" or "error"))
                    Messages.Enqueue(message.Clone());
            }
        }
        catch (Exception error) { FailPending(error); }
        finally { _readClosed = true; FailPending(new IOException("Codex connection closed.")); }
    }
    private void FailPending(Exception error) { foreach (var request in _pending.Values) request.TrySetException(error); }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        FailPending(new OperationCanceledException("Assistant disconnected."));
        try { if (_process is { HasExited: false }) _process.Kill(entireProcessTree: true); } catch { }
        // Only the child app-server owned by this instance is stopped. The desktop Codex app is unaffected.
        _process?.Dispose(); _process = null;
    }
}
