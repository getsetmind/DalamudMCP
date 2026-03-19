using System.Text.Json;
using DalamudMCP.Host;

if (args.Length is 0 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.Error.WriteLine("Pipe name is required.");
    return 1;
}

var pipeName = args[0].Trim();
var bridgeClient = new PluginBridgeClient(pipeName);
Console.WriteLine($"SMOKE start pipe={pipeName}");

try
{
    using var directCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
    Console.WriteLine("SMOKE direct capability_state...");
    var capabilityState = await bridgeClient.GetCapabilityStateAsync(directCts.Token);
    Console.WriteLine("DIRECT capability_state OK");
    Console.WriteLine(JsonSerializer.Serialize(capabilityState));
}
catch (Exception exception)
{
    Console.WriteLine("DIRECT capability_state FAILED");
    Console.WriteLine(exception);
}

var host = StdioTransportHost.CreateForPipe(pipeName);
var requests = new[]
{
    """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"manual-smoke","version":"0.1.0"}}}""",
    """{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"get_session_status","arguments":{}}}""",
    """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"get_player_context","arguments":{}}}""",
};

foreach (var request in requests)
{
    try
    {
        using var requestCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        Console.WriteLine($"SMOKE request {request}");
        var response = await host.ProcessMessageAsync(request, requestCts.Token);
        Console.WriteLine(response);
    }
    catch (Exception exception)
    {
        Console.WriteLine("SMOKE request FAILED");
        Console.WriteLine(exception);
    }
}

Environment.Exit(0);
return 0;
