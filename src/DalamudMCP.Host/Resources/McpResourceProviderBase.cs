namespace DalamudMCP.Host.Resources;

public abstract class McpResourceProviderBase : IMcpResourceProvider
{
    protected McpResourceProviderBase(string uriTemplate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uriTemplate);
        UriTemplate = uriTemplate;
    }

    public string UriTemplate { get; }

    public abstract bool CanHandle(string uri);

    public abstract Task<object> ReadAsync(string uri, CancellationToken cancellationToken);

    protected static bool TryMatchAddonUri(string uri, string prefix, string suffix, out string addonName)
    {
        addonName = string.Empty;
        var template = $"{prefix}{{addonName}}{suffix}";
        if (!McpResourceUri.TryMatchTemplate(uri, template, out var routeValues))
        {
            return false;
        }

        addonName = routeValues["addonName"];
        return true;
    }
}
