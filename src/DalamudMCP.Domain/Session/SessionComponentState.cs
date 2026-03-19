namespace DalamudMCP.Domain.Session;

public sealed record SessionComponentState
{
    public SessionComponentState(string componentName, bool isReady, string status)
    {
        ComponentName = NormalizeComponentName(componentName);
        IsReady = isReady;
        Status = NormalizeStatus(status);
    }

    public string ComponentName { get; }

    public bool IsReady { get; }

    public string Status { get; }

    private static string NormalizeComponentName(string componentName)
    {
        if (string.IsNullOrWhiteSpace(componentName))
        {
            throw new ArgumentException("ComponentName cannot be null or whitespace.", nameof(componentName));
        }

        return componentName.Trim();
    }

    private static string NormalizeStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Status cannot be null or whitespace.", nameof(status));
        }

        return status.Trim();
    }
}
