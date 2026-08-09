using DalamudMCP.Plugin.Services;

namespace DalamudMCP.Plugin.Tests;

public sealed class ScreenshotFileCleanupServiceTests
{
    [Fact]
    public void DeleteExpired_removes_registered_file()
    {
        string path = Path.Combine(Path.GetTempPath(), $"DalamudMCP-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, [1, 2, 3]);
        using ScreenshotFileCleanupService service = new();
        service.Register(path, DateTimeOffset.UtcNow.AddSeconds(-1));

        service.DeleteExpired();

        Assert.False(File.Exists(path));
    }
}
