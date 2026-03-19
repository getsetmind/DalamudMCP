namespace DalamudMCP.Host.Tools;

public sealed class DutyContextToolHandler : McpToolHandlerBase
{
    private readonly PluginBridgeClient bridgeClient;

    public DutyContextToolHandler(PluginBridgeClient bridgeClient)
        : base("get_duty_context")
    {
        this.bridgeClient = bridgeClient;
    }

    public override async Task<object> InvokeAsync(object? arguments, CancellationToken cancellationToken)
    {
        _ = arguments;
        return await bridgeClient.GetDutyContextAsync(cancellationToken).ConfigureAwait(false);
    }
}
