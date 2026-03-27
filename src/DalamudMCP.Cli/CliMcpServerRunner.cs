using DalamudMCP.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DalamudMCP.Cli;

public static class CliMcpServerRunner
{
    public static IHost BuildHost(CliRuntimeOptions? options = null)
    {
        if (options is null || string.IsNullOrWhiteSpace(options.PipeName))
            throw new InvalidOperationException("A live --pipe connection is required.");

        NamedPipeProtocolClient protocolClient = new(options.PipeName);
        DescribeOperationsResponse catalog = LoadCatalog(protocolClient);
        RemoteMcpToolService toolService = new(catalog.Operations, protocolClient);

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<IProtocolOperationClient>(protocolClient);
        builder.Services.AddSingleton(toolService);
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithListToolsHandler((requestContext, cancellationToken) => toolService.ListToolsAsync(requestContext, cancellationToken))
            .WithCallToolHandler((requestContext, cancellationToken) => toolService.CallToolAsync(requestContext, cancellationToken));
        return builder.Build();
    }

    public static async Task<int> RunAsync(CliRuntimeOptions? options = null, CancellationToken cancellationToken = default)
    {
        using IHost host = BuildHost(options);
        await host.RunAsync(cancellationToken).ConfigureAwait(false);
        return DalamudMCP.Framework.Cli.CliExitCodes.Success;
    }

    private static DescribeOperationsResponse LoadCatalog(NamedPipeProtocolClient protocolClient)
    {
        return Task.Run(
                async () => await protocolClient.DescribeOperationsAsync(CancellationToken.None).ConfigureAwait(false))
            .GetAwaiter()
            .GetResult();
    }
}
