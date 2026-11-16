using System.Text;
using System.Text.Json;

namespace ByteEngine.Mcp.Authoring;

internal static partial class SelfTest
{
    private static void CheckQueries(AuthoringSession session)
    {
        JsonElement Call(string domain, string op, object? args = null, bool expectedOk = true)
        {
            using var document = JsonDocument.Parse(session.Dispatch(domain, op,
                args == null ? null : JsonSerializer.SerializeToElement(args)));
            JsonElement result = document.RootElement.Clone();
            if (result.GetProperty("ok").GetBoolean() != expectedOk)
                throw new InvalidOperationException($"Unexpected {domain}/{op} result: {result}");
            return result;
        }
        foreach (string topic in new[] { "overview", "scenes", "gameplay", "debugging" })
            Call("catalog", "help", new { topic });
        Call("catalog", "help", new { topic = "unknown" }, false);
        var created = Call("scene", "create", new { name = "QueryFixture", expected_rev = "new" });
        string path = created.GetProperty("path").GetString()!;
        Call("scene", "apply", new
        {
            scene = path, expected_rev = created.GetProperty("rev").GetString(),
            edits = Enumerable.Range(0, 160).Select(i => new { op = "create_object", name = $"QueryItem{i:D3}" }).ToArray()
        });
        JsonElement first = Call("scene", "find_objects", new { scene = path, search = "queryitem", limit = 1 });
        string id = first.GetProperty("objects")[0].GetProperty("id").GetString()!;
        if (first.GetProperty("total").GetInt32() != 160 || first.GetProperty("next_offset").GetInt32() != 1)
            throw new InvalidOperationException("Filtered query count/paging failed.");
        var second = Call("scene", "find_objects", new { scene = path, search = "queryitem", limit = 1, offset = 1 });
        if (second.GetProperty("objects")[0].GetProperty("id").GetString() == id)
            throw new InvalidOperationException("Query paging repeated an object.");
        Call("scene", "apply", new
        {
            scene = path, expected_rev = first.GetProperty("rev").GetString(),
            edits = new[] { new { op = "set_component", target = id, type = "Camera3D", properties = new { } } }
        });
        var filtered = Call("scene", "find_objects", new { scene = path, component = "Camera3D", search = "QUERYITEM" });
        if (filtered.GetProperty("total").GetInt32() != 1)
            throw new InvalidOperationException("Component/name intersection failed.");
        Call("scene", "find_objects", new { scene = path, component = "NoSuchComponent" }, false);
        var empty = Call("scene", "find_objects", new { scene = path, offset = int.MaxValue });
        if (empty.GetProperty("objects").GetArrayLength() != 0 || empty.TryGetProperty("next_offset", out _))
            throw new InvalidOperationException("End-of-page handling failed.");
        var assets = Call("asset", "find", new { limit = 1, offset = int.MaxValue });
        if (assets.GetProperty("items").GetArrayLength() != 0 || assets.TryGetProperty("next_offset", out _))
            throw new InvalidOperationException("Asset end-of-page handling failed.");
        object[] requests = [new { tool = "be_project", op = "info" }, new { tool = "be_changes", op = "status" }];
        var batch = Call("project", "batch_read", new { requests });
        if (batch.GetProperty("results").GetArrayLength() != 2)
            throw new InvalidOperationException("Batch results missing.");
        Call("project", "batch_read", new { requests = new[] { new { tool = "be_scene", op = "create" } } }, false);
        Call("project", "batch_read", new { requests = new[] { new { tool = "be_project", op = "batch_read" } } }, false);
        Call("project", "batch_read", new { requests = Array.Empty<object>() }, false);
        Call("project", "batch_read", new { requests = Enumerable.Repeat(requests[0], 17).ToArray() }, false);
        var errors = Call("project", "batch_read", new
        {
            requests = new object[] { new { tool = "be_scene", op = "inspect_object", args = new { scene = path, @object = "missing" } }, requests[0] }
        }, false);
        if (!errors.GetProperty("results")[1].GetProperty("result").GetProperty("ok").GetBoolean())
            throw new InvalidOperationException("Read batch stopped after an individual error.");
        var full = Call("scene", "inspect", new { scene = path, limit = 200 });
        int fullBytes = Encoding.UTF8.GetByteCount(full.GetRawText());
        int filteredBytes = Encoding.UTF8.GetByteCount(filtered.GetRawText());
        Console.WriteLine($"Query regression: PASS; synthetic 160-object inspection {fullBytes} bytes; one Camera3D match {filteredBytes} bytes. Payload comparison only, not billed tokens.");
    }
}
