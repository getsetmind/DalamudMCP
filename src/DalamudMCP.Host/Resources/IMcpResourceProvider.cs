namespace DalamudMCP.Host.Resources;

public interface IMcpResourceProvider
{
    public string UriTemplate { get; }

    public bool CanHandle(string uri);

    public Task<object> ReadAsync(string uri, CancellationToken cancellationToken);
}
