using DalamudMCP.Protocol;
using Microsoft.Extensions.DependencyInjection;

namespace DalamudMCP.Cli;

public static class CliServiceCollectionExtensions
{
    public static IServiceCollection AddDalamudMcpProtocolClient(this IServiceCollection services, CliRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.PipeName))
            throw new InvalidOperationException("A live --pipe connection is required.");

        services.AddSingleton<IProtocolOperationClient>(_ => new NamedPipeProtocolClient(options.PipeName!));
        return services;
    }
}
