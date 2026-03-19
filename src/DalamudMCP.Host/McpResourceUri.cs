namespace DalamudMCP.Host;

public static class McpResourceUri
{
    public static bool TryNormalize(string uri, out string normalizedUri)
    {
        normalizedUri = string.Empty;
        if (string.IsNullOrWhiteSpace(uri))
        {
            return false;
        }

        var candidate = uri.Trim();
        if (!candidate.StartsWith("ffxiv://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        normalizedUri = candidate;
        return true;
    }

    public static bool TryMatchTemplate(string uri, string uriTemplate, out IReadOnlyDictionary<string, string> routeValues)
    {
        routeValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!TryNormalize(uri, out var normalizedUri))
        {
            return false;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(uriTemplate);
        var normalizedTemplate = uriTemplate.Trim();

        if (!normalizedTemplate.Contains('{', StringComparison.Ordinal))
        {
            return string.Equals(normalizedUri, normalizedTemplate, StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(normalizedTemplate, "ffxiv://ui/addon/{addonName}/tree", StringComparison.OrdinalIgnoreCase))
        {
            return TryMatchAddonRoute(normalizedUri, "/tree", out routeValues);
        }

        if (string.Equals(normalizedTemplate, "ffxiv://ui/addon/{addonName}/strings", StringComparison.OrdinalIgnoreCase))
        {
            return TryMatchAddonRoute(normalizedUri, "/strings", out routeValues);
        }

        return false;
    }

    private static bool TryMatchAddonRoute(string uri, string suffix, out IReadOnlyDictionary<string, string> routeValues)
    {
        routeValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        const string prefix = "ffxiv://ui/addon/";

        if (!uri.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !uri.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var addonName = uri[prefix.Length..^suffix.Length];
        if (string.IsNullOrWhiteSpace(addonName))
        {
            return false;
        }

        routeValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["addonName"] = addonName,
        };
        return true;
    }
}
