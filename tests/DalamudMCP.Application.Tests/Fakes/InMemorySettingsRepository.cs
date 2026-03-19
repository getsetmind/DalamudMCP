using DalamudMCP.Application.Abstractions.Repositories;
using DalamudMCP.Domain.Policy;

namespace DalamudMCP.Application.Tests.Fakes;

internal sealed class InMemorySettingsRepository : ISettingsRepository
{
    public ExposurePolicy Policy { get; set; } = ExposurePolicy.Default;

    public Task<ExposurePolicy> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(Policy);

    public Task SaveAsync(ExposurePolicy policy, CancellationToken cancellationToken)
    {
        Policy = policy;
        return Task.CompletedTask;
    }
}
