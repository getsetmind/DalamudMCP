using DalamudMCP.Domain.Policy;
using DalamudMCP.Infrastructure.Settings;

namespace DalamudMCP.Infrastructure.Tests.Settings;

public sealed class JsonSettingsRepositoryTests
{
    [Fact]
    public async Task LoadAsync_ReturnsDefault_WhenFileMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json");
        var repository = new JsonSettingsRepository(path);

        var result = await repository.LoadAsync(CancellationToken.None);

        Assert.Equal(ExposurePolicy.Default, result);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsPolicy()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "settings.json");
        var repository = new JsonSettingsRepository(path);
        var policy = new ExposurePolicy(
            enabledTools: ["get_player_context"],
            enabledResources: ["ffxiv://player/context"],
            enabledAddons: ["Inventory"],
            observationProfileEnabled: true,
            actionProfileEnabled: true);

        await repository.SaveAsync(policy, CancellationToken.None);
        var loaded = await repository.LoadAsync(CancellationToken.None);

        Assert.Contains("get_player_context", loaded.EnabledTools);
        Assert.Contains("ffxiv://player/context", loaded.EnabledResources);
        Assert.Contains("Inventory", loaded.EnabledAddons);
        Assert.True(loaded.ActionProfileEnabled);
    }

    [Fact]
    public async Task LoadAsync_ReturnsDefault_AndBacksUpCorruptFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "settings.json");
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(path, "{ invalid json", CancellationToken.None);
        var repository = new JsonSettingsRepository(path);

        var loaded = await repository.LoadAsync(CancellationToken.None);

        Assert.Equal(ExposurePolicy.Default, loaded);
        Assert.False(File.Exists(path));
        var backup = Assert.Single(Directory.GetFiles(dir, "settings.json.corrupt.*.json"));
        Assert.True(File.Exists(backup));
    }

    [Fact]
    public async Task LoadAsync_ReadsLegacyProfilePropertyNames()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "settings.json");
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(
            path,
            """
            {
              "baselineProfileEnabled": true,
              "experimentalProfileEnabled": true,
              "enabledTools": ["get_player_context"],
              "enabledResources": ["ffxiv://player/context"],
              "enabledAddons": ["Inventory"]
            }
            """,
            CancellationToken.None);
        var repository = new JsonSettingsRepository(path);

        var loaded = await repository.LoadAsync(CancellationToken.None);

        Assert.True(loaded.ObservationProfileEnabled);
        Assert.True(loaded.ActionProfileEnabled);
        Assert.Contains("get_player_context", loaded.EnabledTools);
        Assert.Contains("ffxiv://player/context", loaded.EnabledResources);
        Assert.Contains("Inventory", loaded.EnabledAddons);
    }

    [Fact]
    public async Task SaveAsync_SupportsConcurrentWriters_WithoutCorruptingFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "settings.json");
        var repository = new JsonSettingsRepository(path);
        var policies = Enumerable.Range(0, 12)
            .Select(index => new ExposurePolicy(
                enabledTools: [$"tool_{index}"],
                enabledResources: [$"ffxiv://resource/{index}"],
                enabledAddons: [$"Addon{index}"],
                observationProfileEnabled: true,
                actionProfileEnabled: index % 2 == 0))
            .ToArray();

        await Task.WhenAll(policies.Select(policy => repository.SaveAsync(policy, CancellationToken.None)));
        var loaded = await repository.LoadAsync(CancellationToken.None);
        var json = await File.ReadAllTextAsync(path, CancellationToken.None);

        Assert.Contains(loaded.EnabledTools.Single(), policies.SelectMany(static policy => policy.EnabledTools));
        Assert.Contains(loaded.EnabledResources.Single(), policies.SelectMany(static policy => policy.EnabledResources));
        Assert.Contains(loaded.EnabledAddons.Single(), policies.SelectMany(static policy => policy.EnabledAddons));
        Assert.StartsWith("{", json, StringComparison.Ordinal);
        Assert.DoesNotContain(".tmp", json, StringComparison.OrdinalIgnoreCase);
    }
}
