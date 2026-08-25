using DalamudMCP.Plugin.Hosting;
using DalamudMCP.Plugin.Services;
using DalamudMCP.Protocol;
using Manifold;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation("plugin.inspect.list", Description = "Lists installed Dalamud plugins and their public status with cursor pagination.", Summary = "Lists installed plugins.")]
[ResultFormatter(typeof(PluginListOperation.TextFormatter))]
[CliCommand("plugin", "list")]
[McpTool("plugin_list")]
public sealed partial class PluginListOperation : IOperation<PluginListOperation.Request, PluginListResult>
{
    private readonly PluginInspectionService service;

    public PluginListOperation(PluginInspectionService service)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public ValueTask<PluginListResult> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        context.CancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(service.List(request.Query, request.Cursor, request.Limit));
    }

    [MemoryPackable]
    [ProtocolOperation("plugin.inspect.list")]
    public sealed partial class Request
    {
        [Option("query", Description = "Optional case-insensitive name filter.", Required = false)]
        public string? Query { get; init; }

        [Option("cursor", Description = "Opaque cursor returned by a previous call.", Required = false)]
        public string? Cursor { get; init; }

        [Option("limit", Description = "Maximum number of plugins to return, from 1 through 100.", Required = false)]
        public int? Limit { get; init; }
    }

    public sealed class TextFormatter : IResultFormatter<PluginListResult>
    {
        public string? FormatText(PluginListResult result, OperationContext context) => result.SummaryText;
    }
}

[Operation("plugin.inspect.describe", Description = "Describes one installed Dalamud plugin using the public IExposedPlugin contract.", Summary = "Describes an installed plugin.")]
[ResultFormatter(typeof(PluginDescribeOperation.TextFormatter))]
[CliCommand("plugin", "describe")]
[McpTool("plugin_describe")]
public sealed partial class PluginDescribeOperation : IOperation<PluginDescribeOperation.Request, PluginDescribeResult>
{
    private readonly PluginInspectionService service;

    public PluginDescribeOperation(PluginInspectionService service)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public ValueTask<PluginDescribeResult> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        context.CancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(service.Describe(request.PluginName));
    }

    [MemoryPackable]
    [ProtocolOperation("plugin.inspect.describe")]
    public sealed partial class Request
    {
        [Option("plugin-name", Description = "Target plugin InternalName.")]
        public string PluginName { get; init; } = string.Empty;
    }

    public sealed class TextFormatter : IResultFormatter<PluginDescribeResult>
    {
        public string? FormatText(PluginDescribeResult result, OperationContext context) => result.SummaryText;
    }
}

[Operation("plugin.lifecycle.control", Description = "Loads, unloads, reloads, enables, or disables an installed Dalamud plugin. Self-management is blocked.", Summary = "Controls an installed plugin lifecycle.")]
[ResultFormatter(typeof(PluginLifecycleOperation.TextFormatter))]
[CliCommand("plugin", "lifecycle")]
[McpTool("plugin_lifecycle")]
public sealed partial class PluginLifecycleOperation : IOperation<PluginLifecycleOperation.Request, PluginManagementResult>
{
    private readonly IPluginManagerAdapter adapter;

    public PluginLifecycleOperation(IPluginManagerAdapter adapter)
    {
        this.adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public ValueTask<PluginManagementResult> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        return adapter.ExecuteLifecycleAsync(request.PluginName, request.Action, request.DryRun, context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("plugin.lifecycle.control")]
    public sealed partial class Request
    {
        [Option("plugin-name", Description = "Target plugin InternalName.")]
        public string PluginName { get; init; } = string.Empty;

        [Option("action", Description = "One of load, unload, reload, enable, or disable.")]
        public string Action { get; init; } = string.Empty;

        [Option("dry-run", Description = "Validate the request without changing plugin state.", Required = false)]
        public bool DryRun { get; init; }
    }

    public sealed class TextFormatter : IResultFormatter<PluginManagementResult>
    {
        public string? FormatText(PluginManagementResult result, OperationContext context) => result.SummaryText;
    }
}

[Operation("plugin.package.control", Description = "Installs, updates, or uninstalls a repository-backed Dalamud plugin. Self-management is blocked.", Summary = "Controls an installed plugin package.")]
[ResultFormatter(typeof(PluginPackageOperation.TextFormatter))]
[CliCommand("plugin", "package")]
[McpTool("plugin_package")]
public sealed partial class PluginPackageOperation : IOperation<PluginPackageOperation.Request, PluginManagementResult>
{
    private readonly IPluginManagerAdapter adapter;

    public PluginPackageOperation(IPluginManagerAdapter adapter)
    {
        this.adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public ValueTask<PluginManagementResult> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        return adapter.ExecutePackageAsync(
            request.PluginName,
            request.Action,
            request.UseTesting,
            request.DryRun,
            request.SupervisorMode,
            context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("plugin.package.control")]
    public sealed partial class Request
    {
        [Option("plugin-name", Description = "Target plugin InternalName from configured repositories.")]
        public string PluginName { get; init; } = string.Empty;

        [Option("action", Description = "One of install, update, or uninstall.")]
        public string Action { get; init; } = string.Empty;

        [Option("use-testing", Description = "Install the testing version when available.", Required = false)]
        public bool UseTesting { get; init; }

        [Option("dry-run", Description = "Validate the request without changing installed packages.", Required = false)]
        public bool DryRun { get; init; }

        [Option("supervisor-mode", Description = "Allow DalamudMCP self-package management only when an external CLI supervisor remains alive to verify shutdown or restart.", Required = false)]
        public bool SupervisorMode { get; init; }
    }

    public sealed class TextFormatter : IResultFormatter<PluginManagementResult>
    {
        public string? FormatText(PluginManagementResult result, OperationContext context) => result.SummaryText;
    }
}
