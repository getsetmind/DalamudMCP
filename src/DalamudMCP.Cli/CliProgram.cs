using Manifold.Cli;
using DalamudMCP.Protocol;
using Microsoft.Extensions.DependencyInjection;

namespace DalamudMCP.Cli;

public static class CliProgram
{
    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        Stream? rawOutput,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (!CliRuntimeOptions.TryParse(args, out CliRuntimeOptions? options, out string? errorMessage))
        {
            if (!string.IsNullOrWhiteSpace(errorMessage))
                await error.WriteLineAsync(errorMessage).ConfigureAwait(false);

            await error.WriteLineAsync(CliRuntimeOptions.Usage).ConfigureAwait(false);
            return CliExitCodes.UsageError;
        }

        if (!options!.TryResolvePipeName(out string? resolvedPipeName, out string? resolveError))
        {
            if (!string.IsNullOrWhiteSpace(resolveError))
                await error.WriteLineAsync(resolveError).ConfigureAwait(false);

            return CliExitCodes.Unavailable;
        }

        options = options.WithPipeName(resolvedPipeName!);

        try
        {
            return options.Mode switch
            {
                CliCommandMode.ServeMcp => await CliMcpServerRunner.RunAsync(options, cancellationToken).ConfigureAwait(false),
                CliCommandMode.ServeHttp => await CliHttpServerRunner.RunAsync(options, cancellationToken).ConfigureAwait(false),
                _ => await RunDirectCliAsync(options, output, error, rawOutput, cancellationToken).ConfigureAwait(false)
            };
        }
        catch (Exception exception)
        {
            await error.WriteLineAsync(exception.ToString()).ConfigureAwait(false);
            return CliExitCodes.UnhandledFailure;
        }
    }

    public static Task<int> RunAsync(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(args, output, error, rawOutput: null, cancellationToken);
    }

    private static async Task<int> RunDirectCliAsync(
        CliRuntimeOptions options,
        TextWriter output,
        TextWriter error,
        Stream? rawOutput,
        CancellationToken cancellationToken)
    {
        ServiceCollection services = new();
        services.AddDalamudMcpProtocolClient(options);
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();
        IProtocolOperationClient protocolClient = serviceProvider.GetRequiredService<IProtocolOperationClient>();
        DescribeOperationsResponse catalog = await protocolClient.DescribeOperationsAsync(cancellationToken).ConfigureAwait(false);

        CliApplication application = new(
            ProtocolOperationDescriptorMapper.ToCliOperationDescriptors(catalog.Operations),
            new RemoteCliInvoker(catalog.Operations, protocolClient),
            serviceProvider,
            rawOutput);

        return await application.ExecuteAsync(options.CommandArguments, output, error, cancellationToken).ConfigureAwait(false);
    }
}



