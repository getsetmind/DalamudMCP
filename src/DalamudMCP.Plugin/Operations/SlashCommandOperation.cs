using System.Runtime.Versioning;
using Dalamud.Plugin.Services;
using DalamudMCP.Protocol;
using Manifold;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "command.slash",
    Description = "Sends a Dalamud-registered slash command through ICommandManager. Native game chat commands are not handled by ICommandManager.",
    Summary = "Sends a Dalamud slash command.")]
[ResultFormatter(typeof(SlashCommandOperation.TextFormatter))]
[CliCommand("command", "slash")]
[McpTool("slash_command")]
public sealed partial class SlashCommandOperation
    : IOperation<SlashCommandOperation.Request, SlashCommandResult>
{
    private const int MaxCommandLength = 256;

    private readonly Func<Request, CancellationToken, ValueTask<SlashCommandResult>> executor;

    [SupportedOSPlatform("windows")]
    public SlashCommandOperation(IFramework framework, ICommandManager commandManager)
    {
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(commandManager);

        executor = CreatePluginExecutor(framework, commandManager);
    }

    internal SlashCommandOperation(Func<Request, CancellationToken, ValueTask<SlashCommandResult>> executor)
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public ValueTask<SlashCommandResult> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return executor(request, context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("command.slash")]
    public sealed partial class Request
    {
        [Option("command", Description = "Slash command to send. Must start with '/' and be at most 256 characters.")]
        public string Command { get; init; } = string.Empty;
    }

    public sealed class TextFormatter : IResultFormatter<SlashCommandResult>
    {
        public string? FormatText(SlashCommandResult result, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            return result.SummaryText;
        }
    }

    [SupportedOSPlatform("windows")]
    private static Func<Request, CancellationToken, ValueTask<SlashCommandResult>> CreatePluginExecutor(
        IFramework framework,
        ICommandManager commandManager)
    {
        return async (request, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            SlashCommandResult? validationResult = Validate(request.Command);
            if (validationResult is not null)
                return validationResult;

            try
            {
                if (framework.IsInFrameworkUpdateThread)
                {
                    commandManager.ProcessCommand(request.Command);
                }
                else
                {
                    await framework.RunOnFrameworkThread(() => commandManager.ProcessCommand(request.Command)).ConfigureAwait(false);
                }

                return new SlashCommandResult(request.Command, true, "command_sent", $"Command sent: {request.Command}");
            }
            catch (Exception exception)
            {
                return new SlashCommandResult(request.Command, false, "command_failed", $"Command failed: {exception.Message}");
            }
        };
    }

    internal static SlashCommandResult? Validate(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return new SlashCommandResult(command, false, "validation_failed", "Command is required.");

        if (!command.StartsWith('/'))
            return new SlashCommandResult(command, false, "validation_failed", "Command must start with '/'.");

        if (command.Length > MaxCommandLength)
            return new SlashCommandResult(command, false, "validation_failed", $"Command must be {MaxCommandLength} characters or fewer.");

        return null;
    }
}

[MemoryPackable]
public sealed partial record SlashCommandResult(
    string Command,
    bool Success,
    string Status,
    string SummaryText);
