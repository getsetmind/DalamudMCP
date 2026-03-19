namespace DalamudMCP.Host.Tools;

public interface IMcpToolHandler
{
    public string ToolName { get; }

    public Task<object> InvokeAsync(object? arguments, CancellationToken cancellationToken);
}
