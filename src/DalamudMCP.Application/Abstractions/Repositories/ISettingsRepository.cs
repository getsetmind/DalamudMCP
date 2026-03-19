using DalamudMCP.Domain.Policy;

namespace DalamudMCP.Application.Abstractions.Repositories;

public interface ISettingsRepository
{
    public Task<ExposurePolicy> LoadAsync(CancellationToken cancellationToken);

    public Task SaveAsync(ExposurePolicy policy, CancellationToken cancellationToken);
}
