using System.Diagnostics;
using System.Text.Json;
using DalamudMCP.Protocol;
using Manifold.Cli;

namespace DalamudMCP.Cli;

internal static class SelfPackageSupervisorRunner
{
    public static async Task<int> RunAsync(
        CliRuntimeOptions options,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!TryParse(options.CommandArguments, out string? action, out bool useTesting, out TimeSpan timeout, out string? parseError))
        {
            await error.WriteLineAsync(parseError).ConfigureAwait(false);
            return CliExitCodes.UsageError;
        }

        ProtocolClientDiscovery.TryRead(out ProtocolClientDiscoveryRecord? startingInstance);
        byte[] requestJson = JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                pluginName = "DalamudMCP",
                action,
                useTesting,
                dryRun = false,
                supervisorMode = true,
            },
            ProtocolContract.JsonOptions);

        bool requestAccepted = false;
        try
        {
            NamedPipeProtocolClient client = new(options.PipeName!);
            ProtocolInvocationResult response = await client.InvokeAsync(
                "plugin.package.control",
                new ProtocolRequestPayload(ProtocolPayloadFormat.Json, requestJson),
                cancellationToken).ConfigureAwait(false);
            JsonElement payload = ProtocolContract.DeserializePayloadElement(response.PayloadFormat, response.Payload);
            requestAccepted = payload.ValueKind == JsonValueKind.Object &&
                              payload.TryGetProperty("success", out JsonElement success) &&
                              success.ValueKind == JsonValueKind.True;
            if (!requestAccepted)
            {
                await error.WriteLineAsync(payload.GetRawText()).ConfigureAwait(false);
                return CliExitCodes.Unavailable;
            }
        }
        catch (Exception exception) when (exception is IOException or TimeoutException or EndOfStreamException)
        {
            // A self-update can unload the plugin before the named-pipe response is completed.
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The request-level timeout can race with the plugin unloading itself.
        }

        if (string.Equals(action, "uninstall", StringComparison.Ordinal))
        {
            bool stopped = await WaitForShutdownAsync(startingInstance, timeout, cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync(JsonSerializer.Serialize(new
            {
                success = requestAccepted || stopped,
                action,
                pluginStopped = stopped,
                summaryText = stopped
                    ? "DalamudMCP stopped after the supervised uninstall request."
                    : "The uninstall request returned, but shutdown was not observed before the timeout.",
            }, ProtocolContract.JsonOptions)).ConfigureAwait(false);
            return requestAccepted || stopped ? CliExitCodes.Success : CliExitCodes.Unavailable;
        }

        ProtocolClientDiscoveryRecord? restarted = await WaitForRestartAsync(startingInstance, timeout, cancellationToken).ConfigureAwait(false);
        if (restarted is null)
        {
            await error.WriteLineAsync("DalamudMCP did not reconnect after the supervised update before the timeout.").ConfigureAwait(false);
            return CliExitCodes.Unavailable;
        }

        await output.WriteLineAsync(JsonSerializer.Serialize(new
        {
            success = true,
            action,
            previousPipe = startingInstance?.PipeName,
            currentPipe = restarted.PipeName,
            restarted.UpdatedAtUtc,
            summaryText = "DalamudMCP reconnected and exposed its operation catalog after the supervised update.",
        }, ProtocolContract.JsonOptions)).ConfigureAwait(false);
        return CliExitCodes.Success;
    }

    internal static bool TryParse(
        IReadOnlyList<string> arguments,
        out string? action,
        out bool useTesting,
        out TimeSpan timeout,
        out string? error)
    {
        action = null;
        useTesting = false;
        timeout = TimeSpan.FromSeconds(90);
        List<string> remaining = [.. arguments];
        for (int index = 0; index < remaining.Count; index++)
        {
            if (string.Equals(remaining[index], "--use-testing", StringComparison.OrdinalIgnoreCase))
            {
                useTesting = true;
                continue;
            }

            if (string.Equals(remaining[index], "--action", StringComparison.OrdinalIgnoreCase) && index + 1 < remaining.Count)
            {
                action = remaining[++index].Trim().ToLowerInvariant();
                continue;
            }

            if (string.Equals(remaining[index], "--timeout-seconds", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < remaining.Count &&
                int.TryParse(remaining[++index], out int seconds) &&
                seconds is >= 30 and <= 300)
            {
                timeout = TimeSpan.FromSeconds(seconds);
                continue;
            }

            error = $"Unsupported or invalid supervisor argument '{remaining[index]}'.";
            return false;
        }

        if (action is not "update" and not "uninstall")
        {
            error = "The supervisor requires --action update or --action uninstall.";
            return false;
        }

        error = null;
        return true;
    }

    private static async Task<bool> WaitForShutdownAsync(
        ProtocolClientDiscoveryRecord? startingInstance,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ProtocolClientDiscovery.TryRead(out ProtocolClientDiscoveryRecord? current) ||
                current is null ||
                (startingInstance is not null && !string.Equals(current.PipeName, startingInstance.PipeName, StringComparison.Ordinal)))
            {
                return true;
            }

            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    private static async Task<ProtocolClientDiscoveryRecord?> WaitForRestartAsync(
        ProtocolClientDiscoveryRecord? startingInstance,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ProtocolClientDiscovery.TryRead(out ProtocolClientDiscoveryRecord? current) &&
                current is not null &&
                (startingInstance is null ||
                 current.UpdatedAtUtc > startingInstance.UpdatedAtUtc ||
                 !string.Equals(current.PipeName, startingInstance.PipeName, StringComparison.Ordinal)))
            {
                try
                {
                    using CancellationTokenSource probe = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    probe.CancelAfter(TimeSpan.FromSeconds(2));
                    DescribeOperationsResponse catalog = await new NamedPipeProtocolClient(current.PipeName)
                        .DescribeOperationsAsync(probe.Token)
                        .ConfigureAwait(false);
                    if (catalog.Operations.Count > 0)
                        return current;
                }
                catch (Exception exception) when (exception is IOException or TimeoutException or OperationCanceledException)
                {
                }
            }

            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }
}
