using System.Security.Cryptography;
using System.Text.Json;
using ByteEngine.Core.Runtime;

static class RuntimeIntegrityChecks
{
    public static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "byte-browser-integrity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "_framework"));
        try
        {
            var files = new Dictionary<string, string>();
            void Write(string path, string value)
            {
                File.WriteAllText(Path.Combine(root, path), value);
                files[path] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, path))));
            }
            void Manifest() => File.WriteAllText(Path.Combine(root, BrowserRuntimeIntegrity.ManifestName),
                JsonSerializer.Serialize(new { formatVersion = 1, files }));
            void Reject(string name)
            {
                try { BrowserRuntimeIntegrity.Validate(root); }
                catch (InvalidDataException) { Console.WriteLine("PASS: " + name); return; }
                throw new Exception("FAILED: " + name);
            }
            Reject("export refuses a runtime with no integrity manifest");
            foreach (string path in new[] { "index.html", "main.js", "content.js", "renderer.js", "frame-wire.js",
                         "graphics.js", "water.js", "audio.js", "video.js", "performance.js", "browser-plugins.json",
                         "browser-plugin-builds.json", "_framework/dotnet.js", "_framework/engine.wasm", "_framework/game.wasm" })
                Write(path, "test:" + path);
            string BootHash(string path) => "sha256-" + Convert.ToBase64String(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, "_framework", path))));
            Write("_framework/blazor.boot.json", JsonSerializer.Serialize(new { resources = new {
                assembly = new Dictionary<string, string> { ["engine.wasm"] = BootHash("engine.wasm"), ["game.wasm"] = BootHash("game.wasm") },
                jsModuleRuntime = new Dictionary<string, string> { ["dotnet.js"] = BootHash("dotnet.js") }
            } }));
            Manifest();
            if (BrowserRuntimeIntegrity.Validate(root) != 3) throw new Exception("Valid runtime was rejected");
            Console.WriteLine("PASS: complete runtime validates its boot resources and JavaScript");
            File.AppendAllText(Path.Combine(root, "renderer.js"), "corrupt");
            Reject("mixed renderer/runtime files are rejected");
            Write("renderer.js", "restored"); Manifest();
            File.WriteAllText(Path.Combine(root, "_framework", "stale.wasm"), "stale");
            Reject("untracked stale framework binary is rejected");
            File.Delete(Path.Combine(root, "_framework", "stale.wasm"));
            files["../escape"] = new string('0', 64); Manifest();
            Reject("runtime manifest path traversal is rejected");
            files.Remove("../escape");
            files.Remove("main.js"); Manifest();
            Reject("required runtime file omitted from manifest is rejected");
            Write("main.js", "main");
            Write("_framework/engine.wasm", "changed assembly with outdated boot manifest"); Manifest();
            Reject("boot hash disagreement is rejected even with updated outer manifest");
        }
        finally { Directory.Delete(root, true); }
    }
}
