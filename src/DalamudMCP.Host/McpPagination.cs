using System.Globalization;

namespace DalamudMCP.Host;

internal static class McpPagination
{
    public static (int Offset, int Take) Parse(string? cursor, int pageSize)
    {
        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be positive.");
        }

        if (string.IsNullOrWhiteSpace(cursor))
        {
            return (0, pageSize);
        }

        if (!int.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out var offset) || offset < 0)
        {
            throw new InvalidOperationException($"Invalid cursor '{cursor}'.");
        }

        return (offset, pageSize);
    }

    public static string? CreateNextCursor(int offset, int take, int totalCount)
    {
        var next = offset + take;
        return next < totalCount ? next.ToString(CultureInfo.InvariantCulture) : null;
    }
}
