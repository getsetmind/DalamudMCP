using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Plugin.Readers;

public sealed class NullStringTableReader : IStringTableReader, IPluginReaderDiagnostics
{
    public string ComponentName => "string_table";

    public bool IsReady => false;

    public string Status => "not_attached";

    public Task<StringTableSnapshot?> ReadCurrentAsync(string addonName, CancellationToken cancellationToken) =>
        Task.FromResult<StringTableSnapshot?>(null);
}
