namespace DalamudMCP.Domain.Snapshots;

internal static class SnapshotGuard
{
    public static DateTimeOffset RequiredCapturedAt(DateTimeOffset value, string paramName)
    {
        if (value == default)
        {
            throw new ArgumentException("CapturedAt must be a real timestamp.", paramName);
        }

        return value;
    }

    public static string RequiredText(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be null, empty, or whitespace.", paramName);
        }

        return value;
    }

    public static int NonNegative(int value, string paramName)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value, paramName);
        return value;
    }

    public static float NonNegative(float value, string paramName)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value, paramName);
        return value;
    }
}
