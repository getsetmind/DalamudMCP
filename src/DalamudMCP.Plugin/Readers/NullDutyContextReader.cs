using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Plugin.Readers;

public sealed class NullDutyContextReader : IDutyContextReader, IPluginReaderDiagnostics
{
    public string ComponentName => "duty_context";

    public bool IsReady => false;

    public string Status => "not_attached";

    public Task<DutyContextSnapshot?> ReadCurrentAsync(CancellationToken cancellationToken) =>
        Task.FromResult<DutyContextSnapshot?>(null);
}
