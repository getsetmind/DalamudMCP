using System.Runtime.Versioning;
using Dalamud.Plugin.Services;
using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Plugin.Readers;

[SupportedOSPlatform("windows")]
public sealed class DalamudStringTableReader : IStringTableReader, IPluginReaderDiagnostics
{
    private readonly IClientState clientState;
    private readonly IFramework framework;
    private readonly IGameGui gameGui;

    public DalamudStringTableReader(
        IFramework framework,
        IClientState clientState,
        IGameGui gameGui)
    {
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(clientState);
        ArgumentNullException.ThrowIfNull(gameGui);
        this.framework = framework;
        this.clientState = clientState;
        this.gameGui = gameGui;
    }

    public string ComponentName => "string_table";

    public bool IsReady => clientState.IsLoggedIn;

    public string Status => IsReady ? "ready" : "not_logged_in";

    public Task<StringTableSnapshot?> ReadCurrentAsync(string addonName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!framework.IsInFrameworkUpdateThread)
        {
            return framework.RunOnFrameworkThread(() => ReadCurrentCore(addonName, cancellationToken));
        }

        return Task.FromResult(ReadCurrentCore(addonName, cancellationToken));
    }

    private StringTableSnapshot? ReadCurrentCore(string addonName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addonName);
        cancellationToken.ThrowIfCancellationRequested();
        if (!clientState.IsLoggedIn)
        {
            return null;
        }

        var addon = gameGui.GetAddonByName(addonName, 1);
        if (addon.IsNull || !addon.IsReady)
        {
            return null;
        }

        var values = new List<object?>();
        foreach (var atkValue in addon.AtkValues)
        {
            try
            {
                values.Add(atkValue.GetValue());
            }
            catch (NotImplementedException)
            {
                values.Add(null);
            }
        }

        var entries = PluginReaderValueFormatter.CreateStringEntries(values);
        return new StringTableSnapshot(
            AddonName: addonName,
            CapturedAt: DateTimeOffset.UtcNow,
            Entries: entries);
    }
}
