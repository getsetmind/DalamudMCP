namespace DalamudMCP.Contracts.Bridge;

public static class ContractVersion
{
    public const string Current = "1.0.0";

    public static bool IsCompatible(string? version)
    {
        if (!TryGetMajor(Current, out var currentMajor) || !TryGetMajor(version, out var requestedMajor))
        {
            return false;
        }

        return currentMajor == requestedMajor;
    }

    public static void EnsureCompatible(string? version, string paramName)
    {
        if (!IsCompatible(version))
        {
            throw new InvalidOperationException(
                $"Unsupported contract version '{version ?? "<null>"}' for '{paramName}'. Expected compatibility with '{Current}'.");
        }
    }

    private static bool TryGetMajor(string? version, out int major)
    {
        major = 0;
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var separatorIndex = version.IndexOf('.');
        var majorSlice = separatorIndex >= 0
            ? version.AsSpan(0, separatorIndex)
            : version.AsSpan();

        return int.TryParse(majorSlice, out major);
    }
}
