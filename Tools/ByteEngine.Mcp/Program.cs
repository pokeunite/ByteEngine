using ByteEngine.Mcp.Authoring;
using ByteEngine.Mcp.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.Text.Json;

if (args.Length == 2 && args[0] == "--validate-project")
{
    using var session = new AuthoringSession();
    string opened = session.Dispatch("project", "open", JsonSerializer.SerializeToElement(new { path = Path.GetFullPath(args[1]) }));
    Console.WriteLine(opened);
    using var openResult = JsonDocument.Parse(opened);
    if (!openResult.RootElement.GetProperty("ok").GetBoolean()) { Environment.ExitCode = 1; return; }
    string result = session.Dispatch("runtime", "validate");
    Console.WriteLine(result);
    using var validation = JsonDocument.Parse(result);
    Environment.ExitCode = validation.RootElement.GetProperty("ok").GetBoolean() ? 0 : 1;
    return;
}

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTest.RunAsync();
    return;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Services.AddSingleton<AuthoringSession>();
builder.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();
await builder.Build().RunAsync();
