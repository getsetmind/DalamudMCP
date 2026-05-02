using DalamudMCP.Plugin.Readers;
using DalamudMCP.Plugin.Ui.Localization;
using Manifold;

namespace DalamudMCP.Plugin.Ui;

internal sealed class PluginConfigWindowModel
{
    private readonly IUiLocalization localization;
    private readonly IPluginReaderStatus?[] readerStatuses;
    private readonly PluginConfigOperationRow[] operations;
    private string? mcpServerCommand;
    private string? mcpServerError;
    private string? actionOperationsStatusText;
    private string? unsafeOperationsStatusText;
    private int readyReaderCount;
    private int readerCount;
    private int actionOperationCount;
    private int unsafeOperationCount;
    private int exposedOperationCount;
    private int blockedOperationCount;

    private PluginConfigWindowModel(
        string pipeName,
        string pipeNameText,
        string cliCommand,
        string mcpCommand,
        PluginConfigOperationRow[] operations,
        IPluginReaderStatus?[] readerStatuses,
        IUiLocalization localization)
    {
        this.localization = localization;
        PipeName = pipeName;
        PipeNameText = pipeNameText;
        CliCommand = cliCommand;
        McpCommand = mcpCommand;
        this.operations = operations;
        this.readerStatuses = readerStatuses;
        Operations = operations;
    }

    public string PipeName { get; }

    public string PipeNameText { get; }

    public bool ProtocolServerRunning { get; private set; }

    public string ProtocolServerStatusText { get; private set; } = string.Empty;

    public bool AutoStartHttpServerOnLoad { get; private set; }

    public bool ActionOperationsEnabled { get; private set; }

    public string ActionOperationsStatusText => actionOperationsStatusText ?? localization["status.actions_disabled"];

    public bool UnsafeOperationsEnabled { get; private set; }

    public string UnsafeOperationsStatusText => unsafeOperationsStatusText ?? localization["status.unsafe_disabled"];

    public bool McpServerRunning { get; private set; }

    public string McpServerEndpoint { get; private set; } = string.Empty;

    public string McpServerEndpointText { get; private set; } = string.Empty;

    public string? McpServerCommand => mcpServerCommand;

    public string? McpServerError => mcpServerError;

    public string? McpServerErrorText { get; private set; }

    public string McpServerStatusText { get; private set; } = string.Empty;

    public string CliCommand { get; }

    public string McpCommand { get; }

    public IReadOnlyList<PluginConfigOperationRow> Operations { get; }

    public int OperationCount => operations.Length;

    public int ReadyReaderCount => readyReaderCount;

    public int ReaderCount => readerCount;

    public int ActionOperationCount => actionOperationCount;

    public int UnsafeOperationCount => unsafeOperationCount;

    public int ExposedOperationCount => exposedOperationCount;

    public int BlockedOperationCount => blockedOperationCount;

    public string? ReaderStatusText { get; private set; }

    public static PluginConfigWindowModel Create(
        PluginRuntimeOptions options,
        bool protocolServerRunning,
        bool autoStartHttpServerOnLoad,
        bool actionOperationsEnabled,
        bool unsafeOperationsEnabled,
        Hosting.PluginMcpServerStatus mcpServerStatus,
        IReadOnlyList<OperationDescriptor> operations,
        IReadOnlyList<IPluginReaderStatus> readerStatuses,
        IUiLocalization? localization = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(mcpServerStatus);
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(readerStatuses);

        IUiLocalization activeLocalization = localization ?? EnglishLocalization.Instance;
        PluginConfigOperationRow[] rows = CreateRows(operations, readerStatuses, activeLocalization, out IPluginReaderStatus?[] rowsByReader);
        PluginConfigWindowModel model = new(
            options.PipeName,
            activeLocalization.Format("label.pipe_name", options.PipeName),
            @"dotnet run --project .\src\DalamudMCP.Cli\DalamudMCP.Cli.csproj -- player context",
            @"dotnet run --project .\src\DalamudMCP.Cli\DalamudMCP.Cli.csproj -- serve mcp",
            rows,
            rowsByReader,
            activeLocalization);
        model.ApplyStatus(
            protocolServerRunning,
            autoStartHttpServerOnLoad,
            actionOperationsEnabled,
            unsafeOperationsEnabled,
            mcpServerStatus.IsRunning,
            mcpServerStatus.EndpointUrl,
            mcpServerStatus.CommandText,
            mcpServerStatus.LastError);
        return model;
    }

    internal void Refresh(
        bool protocolServerRunning,
        bool autoStartHttpServerOnLoad,
        bool actionOperationsEnabled,
        bool unsafeOperationsEnabled,
        Hosting.PluginMcpServerController mcpServerController)
    {
        ArgumentNullException.ThrowIfNull(mcpServerController);
        ApplyStatus(
            protocolServerRunning,
            autoStartHttpServerOnLoad,
            actionOperationsEnabled,
            unsafeOperationsEnabled,
            mcpServerController.IsRunning,
            mcpServerController.EndpointUrl,
            mcpServerController.LastCommandText,
            mcpServerController.LastError);
    }

    internal void ApplyStatus(
        bool protocolServerRunning,
        bool autoStartHttpServerOnLoad,
        bool actionOperationsEnabled,
        bool unsafeOperationsEnabled,
        bool mcpServerRunning,
        string mcpServerEndpoint,
        string? mcpServerCommand,
        string? mcpServerError)
    {
        ProtocolServerRunning = protocolServerRunning;
        ProtocolServerStatusText = protocolServerRunning
            ? localization["status.server_running"]
            : localization["status.server_stopped"];
        AutoStartHttpServerOnLoad = autoStartHttpServerOnLoad;
        ActionOperationsEnabled = actionOperationsEnabled;
        actionOperationsStatusText = actionOperationsEnabled
            ? localization["status.actions_enabled"]
            : localization["status.actions_disabled"];
        UnsafeOperationsEnabled = unsafeOperationsEnabled;
        unsafeOperationsStatusText = unsafeOperationsEnabled
            ? localization["status.unsafe_enabled"]
            : localization["status.unsafe_disabled"];
        McpServerRunning = mcpServerRunning;
        McpServerStatusText = mcpServerRunning
            ? localization["status.http_running"]
            : localization["status.http_stopped"];

        UpdateEndpointText(mcpServerEndpoint);
        UpdateCommand(mcpServerCommand);
        UpdateError(mcpServerError);
        RefreshExposureStatuses();
        RefreshReaderStatuses();
    }

    private static PluginConfigOperationRow[] CreateRows(
        IReadOnlyList<OperationDescriptor> operations,
        IReadOnlyList<IPluginReaderStatus> readerStatuses,
        IUiLocalization localization,
        out IPluginReaderStatus?[] rowsByReader)
    {
        OperationDescriptor[] sortedOperations = new OperationDescriptor[operations.Count];
        for (int index = 0; index < operations.Count; index++)
            sortedOperations[index] = operations[index];

        Array.Sort(
            sortedOperations,
            static (left, right) => string.Compare(left.OperationId, right.OperationId, StringComparison.Ordinal));

        Dictionary<string, IPluginReaderStatus> readersByKey = new(readerStatuses.Count, StringComparer.Ordinal);
        for (int index = 0; index < readerStatuses.Count; index++)
        {
            IPluginReaderStatus readerStatus = readerStatuses[index];
            readersByKey[readerStatus.ReaderKey] = readerStatus;
        }

        PluginConfigOperationRow[] rows = new PluginConfigOperationRow[sortedOperations.Length];
        rowsByReader = new IPluginReaderStatus?[sortedOperations.Length];
        for (int index = 0; index < sortedOperations.Length; index++)
        {
            OperationDescriptor operation = sortedOperations[index];
            rows[index] = new PluginConfigOperationRow(
                operation.OperationId,
                operation.Summary ?? operation.Description ?? operation.OperationId,
                operation.CliCommandPath is { Count: > 0 } cliPath ? string.Join(' ', cliPath) : null,
                operation.McpToolName,
                localization);
            readersByKey.TryGetValue(operation.OperationId, out rowsByReader[index]);
        }

        return rows;
    }

    private void UpdateEndpointText(string endpoint)
    {
        if (string.Equals(McpServerEndpoint, endpoint, StringComparison.Ordinal))
            return;

        McpServerEndpoint = endpoint;
        McpServerEndpointText = localization.Format("label.endpoint", endpoint);
    }

    private void UpdateCommand(string? command)
    {
        if (string.Equals(mcpServerCommand, command, StringComparison.Ordinal))
            return;

        mcpServerCommand = command;
    }

    private void UpdateError(string? error)
    {
        if (string.Equals(mcpServerError, error, StringComparison.Ordinal))
            return;

        mcpServerError = error;
        McpServerErrorText = string.IsNullOrWhiteSpace(error)
            ? null
            : localization.Format("label.last_error", error);
    }

    private void RefreshReaderStatuses()
    {
        int newReadyReaderCount = 0;
        int newReaderCount = 0;
        for (int index = 0; index < operations.Length; index++)
        {
            PluginConfigOperationRow operation = operations[index];
            operation.UpdateReaderStatus(readerStatuses[index]);
            if (operation.IsReaderReady is null)
                continue;

            newReaderCount++;
            if (operation.IsReaderReady.Value)
                newReadyReaderCount++;
        }

        if (readyReaderCount == newReadyReaderCount &&
            readerCount == newReaderCount)
        {
            return;
        }

        readyReaderCount = newReadyReaderCount;
        readerCount = newReaderCount;
        ReaderStatusText = newReaderCount > 0
            ? localization.Format("status.reader_format", newReadyReaderCount, newReaderCount)
            : null;
    }

    private void RefreshExposureStatuses()
    {
        int newActionOperationCount = 0;
        int newUnsafeOperationCount = 0;
        int newExposedOperationCount = 0;
        int newBlockedOperationCount = 0;
        for (int index = 0; index < operations.Length; index++)
        {
            PluginConfigOperationRow operation = operations[index];
            operation.UpdateExposureStatus(ActionOperationsEnabled, UnsafeOperationsEnabled);
            if (operation.IsActionOperation)
                newActionOperationCount++;

            if (operation.IsUnsafeOperation)
                newUnsafeOperationCount++;

            if (operation.IsExposed)
                newExposedOperationCount++;
            else
                newBlockedOperationCount++;
        }

        actionOperationCount = newActionOperationCount;
        unsafeOperationCount = newUnsafeOperationCount;
        exposedOperationCount = newExposedOperationCount;
        blockedOperationCount = newBlockedOperationCount;
    }
}

internal sealed class PluginConfigOperationRow
{
    private readonly IUiLocalization localization;

    public PluginConfigOperationRow(
        string operationId,
        string summary,
        string? cliCommand,
        string? mcpToolName,
        IUiLocalization localization)
    {
        this.localization = localization;
        OperationId = operationId;
        Summary = summary;
        CliCommand = cliCommand;
        McpToolName = mcpToolName;
        CliCommandText = string.IsNullOrWhiteSpace(cliCommand)
            ? null
            : localization.Format("label.cli_prefix", cliCommand);
        McpToolText = string.IsNullOrWhiteSpace(mcpToolName)
            ? null
            : localization.Format("label.mcp_prefix", mcpToolName);
        IsActionOperation = Hosting.PluginOperationExposurePolicy.IsActionOperation(operationId);
        IsUnsafeOperation = Hosting.PluginOperationExposurePolicy.IsUnsafeOperation(operationId);
    }

    public string OperationId { get; }

    public string Summary { get; }

    public string? CliCommand { get; }

    public string? McpToolName { get; }

    public string? CliCommandText { get; }

    public string? McpToolText { get; }

    public bool? IsReaderReady { get; private set; }

    public string? ReaderDetail { get; private set; }

    public string? ReaderStatusText { get; private set; }

    public string? ExposureStatusText { get; private set; }

    public bool IsActionOperation { get; }

    public bool IsUnsafeOperation { get; }

    public bool IsExposed { get; private set; } = true;

    internal void UpdateReaderStatus(IPluginReaderStatus? readerStatus)
    {
        bool? isReady = null;
        string? detail = null;
        if (readerStatus is not null)
            TryReadReaderStatus(readerStatus, out isReady, out detail);

        if (IsReaderReady == isReady &&
            string.Equals(ReaderDetail, detail, StringComparison.Ordinal))
        {
            return;
        }

        IsReaderReady = isReady;
        ReaderDetail = detail;
        ReaderStatusText = CreateReaderStatusText(isReady, detail);
    }

    internal void UpdateExposureStatus(bool actionOperationsEnabled, bool unsafeOperationsEnabled)
    {
        string? exposureStatusText =
            IsUnsafeOperation && !unsafeOperationsEnabled
                ? localization["status.exposure_unsafe_pending"]
                : IsActionOperation && !actionOperationsEnabled
                    ? localization["status.exposure_action_pending"]
                    : null;

        if (string.Equals(ExposureStatusText, exposureStatusText, StringComparison.Ordinal))
        {
            IsExposed = exposureStatusText is null;
            return;
        }

        ExposureStatusText = exposureStatusText;
        IsExposed = exposureStatusText is null;
    }

    private string? CreateReaderStatusText(bool? isReady, string? detail)
    {
        if (isReady is null)
            return null;

        string readiness = isReady.Value
            ? localization["status.reader_ready_word"]
            : localization["status.reader_not_ready_word"];
        if (string.IsNullOrWhiteSpace(detail))
            return localization.Format("status.reader", readiness);

        return localization.Format("status.reader_detail", readiness, detail);
    }

    private static void TryReadReaderStatus(
        IPluginReaderStatus readerStatus,
        out bool? isReady,
        out string? detail)
    {
        try
        {
            isReady = readerStatus.IsReady;
            detail = readerStatus.Detail;
        }
        catch (InvalidOperationException exception) when (string.Equals(exception.Message, "Not on main thread!", StringComparison.Ordinal))
        {
            isReady = false;
            detail = "main_thread_required";
        }
        catch
        {
            isReady = false;
            detail = "status_unavailable";
        }
    }
}
