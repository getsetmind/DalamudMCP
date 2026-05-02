using System.Runtime.Versioning;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DalamudMCP.Protocol;
using Manifold;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "plugin.reload",
    Description = "Reloads a target Dalamud plugin by sending /xlreload {plugin-name}. DalamudMCP blocks reloading itself through this operation.",
    Summary = "Reloads a specified Dalamud plugin.")]
[ResultFormatter(typeof(PluginReloadOperation.TextFormatter))]
[CliCommand("plugin", "reload")]
[McpTool("reload_plugin")]
public sealed partial class PluginReloadOperation
    : IOperation<PluginReloadOperation.Request, PluginReloadResult>
{
    private readonly Func<Request, CancellationToken, ValueTask<PluginReloadResult>> executor;

    [SupportedOSPlatform("windows")]
    public PluginReloadOperation(
        IDalamudPluginInterface pluginInterface,
        IFramework framework,
        ICommandManager commandManager)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(commandManager);

        executor = CreatePluginExecutor(
            pluginInterface.InternalName,
            () => pluginInterface.InstalledPlugins.Select(static plugin => plugin.InternalName).ToArray(),
            framework,
            commandManager);
    }

    internal PluginReloadOperation(Func<Request, CancellationToken, ValueTask<PluginReloadResult>> executor)
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public ValueTask<PluginReloadResult> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return executor(request, context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("plugin.reload")]
    public sealed partial class Request
    {
        [Option("plugin-name", Description = "Target plugin InternalName.")]
        public string PluginName { get; init; } = string.Empty;
    }

    public sealed class TextFormatter : IResultFormatter<PluginReloadResult>
    {
        public string? FormatText(PluginReloadResult result, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            return result.SummaryText;
        }
    }

    [SupportedOSPlatform("windows")]
    private static Func<Request, CancellationToken, ValueTask<PluginReloadResult>> CreatePluginExecutor(
        string currentPluginName,
        Func<IReadOnlyCollection<string>> getInstalledPluginNames,
        IFramework framework,
        ICommandManager commandManager)
    {
        return async (request, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            PluginReloadResult? validationResult = Validate(currentPluginName, getInstalledPluginNames(), request);
            if (validationResult is not null)
                return validationResult;

            string pluginName = request.PluginName.Trim();
            string reloadCommand = $"/xlreload {pluginName}";

            try
            {
                if (framework.IsInFrameworkUpdateThread)
                {
                    commandManager.ProcessCommand(reloadCommand);
                }
                else
                {
                    await framework.RunOnFrameworkThread(() => commandManager.ProcessCommand(reloadCommand)).ConfigureAwait(false);
                }

                return new PluginReloadResult(pluginName, true, "reload_initiated", null, $"Reload initiated for plugin: {pluginName}.");
            }
            catch (Exception exception)
            {
                return new PluginReloadResult(pluginName, false, "reload_failed", exception.Message, $"Reload failed for plugin: {pluginName}. Error: {exception.Message}");
            }
        };
    }

    internal static PluginReloadResult? Validate(
        string currentPluginName,
        IReadOnlyCollection<string> installedPluginNames,
        Request request)
    {
        ArgumentNullException.ThrowIfNull(installedPluginNames);
        ArgumentNullException.ThrowIfNull(request);

        string pluginName = string.IsNullOrWhiteSpace(request.PluginName)
            ? string.Empty
            : request.PluginName.Trim();

        if (string.IsNullOrWhiteSpace(pluginName))
            return new PluginReloadResult(pluginName, false, "validation_failed", "plugin-name is required.", "Plugin name is required.");

        if (string.Equals(pluginName, currentPluginName, StringComparison.OrdinalIgnoreCase))
        {
            return new PluginReloadResult(
                pluginName,
                false,
                "self_reload_blocked",
                "Cannot reload DalamudMCP itself through this operation.",
                $"Self-reload blocked: {pluginName} is the current plugin.");
        }

        if (!installedPluginNames.Contains(pluginName, StringComparer.OrdinalIgnoreCase))
        {
            return new PluginReloadResult(
                pluginName,
                false,
                "plugin_not_found",
                $"Plugin '{pluginName}' was not found in installed plugins.",
                $"Plugin not found: {pluginName}.");
        }

        return null;
    }
}

[MemoryPackable]
public sealed partial record PluginReloadResult(
    string PluginName,
    bool Success,
    string Status,
    string? ErrorMessage,
    string SummaryText);
