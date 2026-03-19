namespace DalamudMCP.Host;

public static class HostProgram
{
    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        TextReader input,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (!HostRuntimeOptions.TryParse(args, out var options, out var errorMessage))
        {
            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                await error.WriteLineAsync(errorMessage).ConfigureAwait(false);
            }

            await error.WriteLineAsync(HostRuntimeOptions.Usage).ConfigureAwait(false);
            await error.FlushAsync(cancellationToken).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(errorMessage) ? 0 : 1;
        }

        if (options!.Transport is HostTransportKind.Http)
        {
            var httpHost = new StreamableHttpTransportHost(
                StdioTransportHost.CreateForPipe(options.PipeName, options.PageSize),
                options.HttpHost,
                options.HttpPort,
                options.McpPath);
            await using (httpHost.ConfigureAwait(false))
            {
                await httpHost.RunAsync(cancellationToken).ConfigureAwait(false);
            }

            return 0;
        }

        var host = StdioTransportHost.CreateForPipe(options.PipeName, options.PageSize);
        await host.RunAsync(input, output, cancellationToken).ConfigureAwait(false);
        return 0;
    }
}
