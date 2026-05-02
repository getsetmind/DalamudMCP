using Manifold;
using DalamudMCP.Plugin.Readers;

namespace DalamudMCP.Plugin.Ui;

internal sealed class PluginConfigWindowModel
{
    private static readonly string ProtocolServerRunningText = "Server status: running";
    private static readonly string ProtocolServerStoppedText = "Server status: stopped";
    private static readonly string McpServerRunningText = "Status: running";
    private static readonly string McpServerStoppedText = "Status: stopped";

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
        IPluginReaderStatus?[] readerStatuses)
    {
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

    public string ProtocolServerStatusText { get; private set; } = ProtocolServerStoppedText;

    public bool AutoStartHttpServerOnLoad { get; private set; }

    public bool ActionOperationsEnabled { get; private set; }

    public string ActionOperationsStatusText => actionOperationsStatusText ?? "Action operations: disabled";

    public bool UnsafeOperationsEnabled { get; private set; }

    public string UnsafeOperationsStatusText => unsafeOperationsStatusText ?? "Unsafe operations: disabled";

    public bool McpServerRunning { get; private set; }

    public string McpServerEndpoint { get; private set; } = string.Empty;

    public string McpServerEndpointText { get; private set; } = string.Empty;

    public string? McpServerCommand => mcpServerCommand;

    public string? McpServerError => mcpServerError;

    public string? McpServerErrorText { get; private set; }

    public string McpServerStatusText { get; private set; } = McpServerStoppedText;

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
        IReadOnlyList<IPluginReaderStatus> readerStatuses)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(mcpServerStatus);
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(readerStatuses);

        PluginConfigOperationRow[] rows = CreateRows(operations, readerStatuses, out IPluginReaderStatus?[] rowsByReader);
        PluginConfigWindowModel model = new(
            options.PipeName,
            "Active pipe (advanced): " + options.PipeName,
            @"dotnet run --project .\src\DalamudMCP.Cli\DalamudMCP.Cli.csproj -- player context",
            @"dotnet run --project .\src\DalamudMCP.Cli\DalamudMCP.Cli.csproj -- serve mcp",
            rows,
            rowsByReader);
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
            ? ProtocolServerRunningText
            : ProtocolServerStoppedText;
        AutoStartHttpServerOnLoad = autoStartHttpServerOnLoad;
        ActionOperationsEnabled = actionOperationsEnabled;
        actionOperationsStatusText = actionOperationsEnabled
            ? "Action operations: enabled"
            : "Action operations: disabled";
        UnsafeOperationsEnabled = unsafeOperationsEnabled;
        unsafeOperationsStatusText = unsafeOperationsEnabled
            ? "Unsafe operations: enabled"
            : "Unsafe operations: disabled";
        McpServerRunning = mcpServerRunning;
        McpServerStatusText = mcpServerRunning
            ? McpServerRunningText
            : McpServerStoppedText;

        UpdateEndpointText(mcpServerEndpoint);
        UpdateCommand(mcpServerCommand);
        UpdateError(mcpServerError);
        RefreshExposureStatuses();
        RefreshReaderStatuses();
    }

    private static PluginConfigOperationRow[] CreateRows(
        IReadOnlyList<OperationDescriptor> operations,
        IReadOnlyList<IPluginReaderStatus> readerStatuses,
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
                operation.McpToolName);
            readersByKey.TryGetValue(operation.OperationId, out rowsByReader[index]);
        }

        return rows;
    }

    private void UpdateEndpointText(string endpoint)
    {
        if (string.Equals(McpServerEndpoint, endpoint, StringComparison.Ordinal))
            return;

        McpServerEndpoint = endpoint;
        McpServerEndpointText = "Endpoint: " + endpoint;
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
            : "Last error: " + error;
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
            ? $"Reader status: {newReadyReaderCount}/{newReaderCount} ready"
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
    public PluginConfigOperationRow(
        string operationId,
        string summary,
        string? cliCommand,
        string? mcpToolName)
    {
        OperationId = operationId;
        Summary = summary;
        CliCommand = cliCommand;
        McpToolName = mcpToolName;
        CliCommandText = string.IsNullOrWhiteSpace(cliCommand)
            ? null
            : "CLI: " + cliCommand;
        McpToolText = string.IsNullOrWhiteSpace(mcpToolName)
            ? null
            : "MCP: " + mcpToolName;
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
                ? "Exposure: disabled until unsafe operations are enabled"
                : IsActionOperation && !actionOperationsEnabled
                    ? "Exposure: disabled until action operations are enabled"
                    : null;

        if (string.Equals(ExposureStatusText, exposureStatusText, StringComparison.Ordinal))
        {
            IsExposed = exposureStatusText is null;
            return;
        }

        ExposureStatusText = exposureStatusText;
        IsExposed = exposureStatusText is null;
    }

    private static string? CreateReaderStatusText(bool? isReady, string? detail)
    {
        if (isReady is null)
            return null;

        string readiness = isReady.Value ? "ready" : "not ready";
        if (string.IsNullOrWhiteSpace(detail))
            return "Reader: " + readiness;

        return $"Reader: {readiness} ({detail})";
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