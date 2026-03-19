using DalamudMCP.Host;

return await HostProgram.RunAsync(
    args,
    Console.In,
    Console.Out,
    Console.Error,
    CancellationToken.None);
