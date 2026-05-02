using System.Numerics;
using Dalamud.Bindings.ImGui;
using DalamudMCP.Plugin.Configuration;
using DalamudMCP.Plugin.Readers;
using DalamudMCP.Plugin.Ui.Localization;
using DalamudMCP.Protocol;
using Manifold;

namespace DalamudMCP.Plugin.Ui;

public sealed class PluginConfigWindow
{
    private const long RefreshIntervalMilliseconds = 250;
    private static readonly Vector4 AccentColor = new(0.42f, 0.77f, 0.95f, 1f);
    private static readonly Vector4 SuccessColor = new(0.41f, 0.83f, 0.60f, 1f);
    private static readonly Vector4 WarningColor = new(0.96f, 0.72f, 0.34f, 1f);
    private static readonly Vector4 DangerColor = new(0.92f, 0.43f, 0.43f, 1f);
    private static readonly Vector4 MutedColor = new(0.67f, 0.71f, 0.77f, 1f);

    private readonly PluginUiConfigurationStore configurationStore;
    private readonly Hosting.PluginMcpServerController mcpServerController;
    private readonly PluginRuntimeOptions options;
    private readonly IReadOnlyList<OperationDescriptor> operations;
    private readonly IReadOnlyList<IPluginReaderStatus> readerStatuses;
    private readonly IUiLocalization localization;
    private readonly NamedPipeProtocolServer protocolServer;
    private PluginConfigWindowModel model;
    private bool isOpen;
    private bool showBlockedOnly;
    private bool showReaderBackedOnly;
    private bool showAdvancedDetails;
    private string operationFilter = string.Empty;
    private long nextRefreshAt;

    public PluginConfigWindow(
        PluginRuntimeOptions options,
        NamedPipeProtocolServer protocolServer,
        PluginUiConfigurationStore configurationStore,
        Hosting.PluginMcpServerController mcpServerController,
        IReadOnlyList<OperationDescriptor> operations,
        IReadOnlyList<IPluginReaderStatus> readerStatuses,
        IUiLocalization localization)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.protocolServer = protocolServer ?? throw new ArgumentNullException(nameof(protocolServer));
        this.configurationStore = configurationStore ?? throw new ArgumentNullException(nameof(configurationStore));
        this.mcpServerController = mcpServerController ?? throw new ArgumentNullException(nameof(mcpServerController));
        this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
        this.readerStatuses = readerStatuses ?? throw new ArgumentNullException(nameof(readerStatuses));
        this.localization = localization ?? throw new ArgumentNullException(nameof(localization));
        this.localization.SetLanguage(configurationStore.Current.SelectedLanguage);

        model = CreateModel();
    }

    public void Open()
    {
        isOpen = true;
        RefreshModel(force: true);
    }

    public void Draw()
    {
        if (!isOpen)
            return;

        RefreshModel(force: false);

        ImGui.SetNextWindowSize(new Vector2(980f, 760f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin(localization["window.title"], ref isOpen, ImGuiWindowFlags.NoCollapse))
        {
            ImGui.End();
            return;
        }

        DrawHeader();
        DrawOverview();
        DrawQuickStart();
        DrawAdvancedDetails();
        DrawOperations();

        ImGui.End();
    }

    private void RefreshModel(bool force)
    {
        long now = Environment.TickCount64;
        if (!force && now < nextRefreshAt)
            return;

        model.Refresh(
            protocolServer.IsRunning,
            configurationStore.Current.AutoStartHttpServerOnLoad,
            configurationStore.Current.EnableActionOperations,
            configurationStore.Current.EnableUnsafeOperations,
            mcpServerController);
        nextRefreshAt = now + RefreshIntervalMilliseconds;
    }

    private PluginConfigWindowModel CreateModel()
    {
        return PluginConfigWindowModel.Create(
            options,
            protocolServer.IsRunning,
            configurationStore.Current.AutoStartHttpServerOnLoad,
            configurationStore.Current.EnableActionOperations,
            configurationStore.Current.EnableUnsafeOperations,
            mcpServerController.GetStatus(),
            operations,
            readerStatuses,
            localization);
    }

    private void DrawHeader()
    {
        ImGui.TextColored(AccentColor, "DalamudMCP");
        ImGui.SameLine();
        ImGui.TextDisabled(localization["header.subtitle"]);

        DrawInlineBadge(
            model.ProtocolServerRunning ? localization["header.pipe_live"] : localization["header.pipe_down"],
            model.ProtocolServerRunning ? SuccessColor : DangerColor);
        DrawInlineBadge(
            model.McpServerRunning ? localization["header.http_live"] : localization["header.http_stopped"],
            model.McpServerRunning ? SuccessColor : WarningColor);
        DrawInlineBadge(
            localization.Format("header.exposed", model.ExposedOperationCount, model.OperationCount),
            AccentColor);

        DrawLanguageSelector();
        ImGui.TextColored(MutedColor, localization["header.hint"]);
        ImGui.Separator();
    }

    private void DrawLanguageSelector()
    {
        ImGui.SameLine();
        ImGui.SetNextItemWidth(140f);
        string selectedLanguage = localization.CurrentLanguage;
        string selectedLabel = localization.SupportedLanguages
            .FirstOrDefault(language => string.Equals(language.Code, selectedLanguage, StringComparison.OrdinalIgnoreCase))
            ?.DisplayName ?? selectedLanguage;
        if (!ImGui.BeginCombo(localization["language.label"], selectedLabel))
            return;

        for (int index = 0; index < localization.SupportedLanguages.Count; index++)
        {
            UiLanguage language = localization.SupportedLanguages[index];
            bool isSelected = string.Equals(language.Code, selectedLanguage, StringComparison.OrdinalIgnoreCase);
            if (ImGui.Selectable(language.DisplayName, isSelected))
            {
                localization.SetLanguage(language.Code);
                configurationStore.Update(configuration => configuration.SelectedLanguage = language.Code);
                model = CreateModel();
                RefreshModel(force: true);
            }

            if (isSelected)
                ImGui.SetItemDefaultFocus();
        }

        ImGui.EndCombo();
    }

    private void DrawOverview()
    {
        Vector2 available = ImGui.GetContentRegionAvail();
        float spacing = ImGui.GetStyle().ItemSpacing.X;
        float leftWidth = (available.X - spacing) * 0.46f;
        float panelHeight = 248f;

        DrawRuntimePanel(leftWidth, panelHeight);
        ImGui.SameLine();
        DrawServerPanel(new Vector2(0f, panelHeight));
    }

    private void DrawRuntimePanel(float width, float height)
    {
        if (!ImGui.BeginChild("RuntimePanel", new Vector2(width, height), true))
        {
            ImGui.EndChild();
            return;
        }

        DrawPanelTitle(localization["runtime.title"], localization["runtime.subtitle"]);
        DrawKeyValue(localization["runtime.discovery"], localization["runtime.discovery_value"]);
        DrawStatusLine(localization["runtime.pipe"], model.ProtocolServerRunning, model.ProtocolServerStatusText);
        if (!string.IsNullOrWhiteSpace(model.ReaderStatusText))
            DrawStatusLine(localization["runtime.readers"], model.ReadyReaderCount == model.ReaderCount, model.ReaderStatusText!);

        DrawStatusLine(localization["runtime.action_tools"], model.ActionOperationsEnabled, model.ActionOperationsStatusText);
        DrawStatusLine(localization["runtime.unsafe_tools"], model.UnsafeOperationsEnabled, model.UnsafeOperationsStatusText);
        DrawKeyValue(localization["runtime.operations"], localization.Format("runtime.operations_value", model.OperationCount, model.ExposedOperationCount, model.BlockedOperationCount));

        ImGui.Spacing();
        bool actionOperationsEnabled = model.ActionOperationsEnabled;
        if (ImGui.Checkbox(localization["runtime.enable_actions"], ref actionOperationsEnabled))
        {
            configurationStore.Update(configuration =>
                configuration.EnableActionOperations = actionOperationsEnabled);
            RefreshModel(force: true);
        }

        ImGui.TextWrapped(localization["runtime.enable_actions_hint"]);

        bool unsafeOperationsEnabled = model.UnsafeOperationsEnabled;
        if (ImGui.Checkbox(localization["runtime.enable_unsafe"], ref unsafeOperationsEnabled))
        {
            configurationStore.Update(configuration =>
                configuration.EnableUnsafeOperations = unsafeOperationsEnabled);
            RefreshModel(force: true);
        }

        ImGui.TextWrapped(localization["runtime.enable_unsafe_hint"]);
        ImGui.EndChild();
    }

    private void DrawQuickStart()
    {
        ImGui.Spacing();
        if (!ImGui.BeginChild("QuickStartPanel", new Vector2(0f, 166f), true))
        {
            ImGui.EndChild();
            return;
        }

        DrawPanelTitle(localization["quick.title"], localization["quick.subtitle"]);
        ImGui.Columns(2, "QuickStartColumns", false);
        DrawCommandCard(
            localization["quick.cli_title"],
            localization["quick.cli_desc"],
            ToCommandSummary(model.CliCommand),
            model.CliCommand,
            localization["quick.cli_button"]);
        ImGui.NextColumn();
        DrawCommandCard(
            localization["quick.mcp_title"],
            localization["quick.mcp_desc"],
            ToCommandSummary(model.McpCommand),
            model.McpCommand,
            localization["quick.mcp_button"]);
        ImGui.Columns(1);
        ImGui.EndChild();
    }

    private void DrawAdvancedDetails()
    {
        ImGui.Spacing();
        if (!ImGui.CollapsingHeader(localization["advanced.header"], ref showAdvancedDetails))
            return;

        if (!ImGui.BeginChild("AdvancedPanel", new Vector2(0f, 132f), true))
        {
            ImGui.EndChild();
            return;
        }

        DrawKeyValue(localization["advanced.pipe"], model.PipeName);
        DrawKeyValue(localization["advanced.cli_command"], model.CliCommand);
        DrawKeyValue(localization["advanced.mcp_serve"], model.McpCommand);
        if (!string.IsNullOrWhiteSpace(model.McpServerCommand))
            DrawKeyValue(localization["advanced.http_command"], model.McpServerCommand);
        if (!string.IsNullOrWhiteSpace(model.McpServerErrorText))
            DrawWrappedStatus(DangerColor, model.McpServerErrorText);

        ImGui.EndChild();
    }

    private void DrawServerPanel(Vector2 size)
    {
        if (!ImGui.BeginChild("ServerPanel", size, true))
        {
            ImGui.EndChild();
            return;
        }

        DrawPanelTitle(localization["server.title"], localization["server.subtitle"]);
        DrawKeyValue(localization["server.endpoint"], model.McpServerEndpoint);
        DrawStatusLine(localization["server.http_state"], model.McpServerRunning, model.McpServerStatusText);

        bool autoStartHttpServerOnLoad = model.AutoStartHttpServerOnLoad;
        if (ImGui.Checkbox(localization["server.auto_start"], ref autoStartHttpServerOnLoad))
        {
            configurationStore.Update(configuration =>
                configuration.AutoStartHttpServerOnLoad = autoStartHttpServerOnLoad);
            RefreshModel(force: true);
        }

        ImGui.Spacing();
        if (!model.McpServerRunning)
        {
            if (ImGui.Button(localization["server.start"], new Vector2(220f, 0f)))
            {
                mcpServerController.Start();
                nextRefreshAt = 0;
            }
        }
        else if (ImGui.Button(localization["server.stop"], new Vector2(220f, 0f)))
        {
            mcpServerController.Stop();
            nextRefreshAt = 0;
        }

        ImGui.SameLine();
        if (ImGui.Button(localization["server.copy_endpoint"], new Vector2(180f, 0f)))
            ImGui.SetClipboardText(model.McpServerEndpoint);

        if (!string.IsNullOrWhiteSpace(model.McpServerCommand))
        {
            if (ImGui.Button(localization["server.copy_command"], new Vector2(220f, 0f)))
                ImGui.SetClipboardText(model.McpServerCommand);
        }

        ImGui.EndChild();
    }

    private void DrawOperations()
    {
        ImGui.Spacing();
        if (!ImGui.BeginChild("OperationsPanel", new Vector2(0f, 0f), true))
        {
            ImGui.EndChild();
            return;
        }

        DrawPanelTitle(localization["operations.title"], localization["operations.subtitle"]);
        DrawKeyValue(
            localization["operations.catalog"],
            localization.Format("operations.catalog_value", model.OperationCount, model.ActionOperationCount, model.UnsafeOperationCount, model.BlockedOperationCount));

        ImGui.SetNextItemWidth(280f);
        ImGui.InputText(localization["operations.search"], ref operationFilter, 128);
        ImGui.SameLine();
        ImGui.Checkbox(localization["operations.blocked_only"], ref showBlockedOnly);
        ImGui.SameLine();
        ImGui.Checkbox(localization["operations.reader_only"], ref showReaderBackedOnly);

        IReadOnlyList<PluginConfigOperationRow> operations = model.Operations;
        const ImGuiTableFlags tableFlags =
            ImGuiTableFlags.RowBg |
            ImGuiTableFlags.BordersInnerH |
            ImGuiTableFlags.BordersOuter |
            ImGuiTableFlags.Resizable |
            ImGuiTableFlags.SizingStretchProp |
            ImGuiTableFlags.ScrollY;

        if (ImGui.BeginTable("OperationsTable", 4, tableFlags, new Vector2(0f, 0f)))
        {
            ImGui.TableSetupColumn(localization["operations.column.operation"], ImGuiTableColumnFlags.WidthStretch, 0.27f);
            ImGui.TableSetupColumn(localization["operations.column.access"], ImGuiTableColumnFlags.WidthStretch, 0.23f);
            ImGui.TableSetupColumn(localization["operations.column.state"], ImGuiTableColumnFlags.WidthStretch, 0.20f);
            ImGui.TableSetupColumn(localization["operations.column.summary"], ImGuiTableColumnFlags.WidthStretch, 0.30f);
            ImGui.TableHeadersRow();

            int visibleCount = 0;
            for (int index = 0; index < operations.Count; index++)
            {
                PluginConfigOperationRow operation = operations[index];
                if (!MatchesOperationFilters(operation))
                    continue;

                visibleCount++;
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                ImGui.TextUnformatted(operation.OperationId);
                DrawInlineTag(operation.IsActionOperation ? localization["operations.tag_action"] : localization["operations.tag_observe"], operation.IsActionOperation ? WarningColor : SuccessColor);
                if (operation.IsUnsafeOperation)
                    DrawInlineTag(localization["operations.tag_unsafe"], DangerColor);

                ImGui.TableSetColumnIndex(1);
                if (!string.IsNullOrWhiteSpace(operation.CliCommandText))
                    ImGui.TextUnformatted(operation.CliCommandText);
                if (!string.IsNullOrWhiteSpace(operation.McpToolText))
                    ImGui.TextUnformatted(operation.McpToolText);

                ImGui.TableSetColumnIndex(2);
                if (!string.IsNullOrWhiteSpace(operation.ReaderStatusText))
                    DrawWrappedStatus(operation.IsReaderReady == true ? SuccessColor : WarningColor, operation.ReaderStatusText);
                if (!string.IsNullOrWhiteSpace(operation.ExposureStatusText))
                    DrawWrappedStatus(WarningColor, operation.ExposureStatusText);
                if (string.IsNullOrWhiteSpace(operation.ReaderStatusText) &&
                    string.IsNullOrWhiteSpace(operation.ExposureStatusText))
                {
                    ImGui.TextColored(SuccessColor, localization["operations.ready_to_expose"]);
                }

                ImGui.TableSetColumnIndex(3);
                ImGui.TextWrapped(operation.Summary);
            }

            if (visibleCount == 0)
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.TextColored(MutedColor, localization["operations.empty"]);
            }

            ImGui.EndTable();
        }

        ImGui.EndChild();
    }

    private bool MatchesOperationFilters(PluginConfigOperationRow operation)
    {
        if (showBlockedOnly && operation.IsExposed)
            return false;

        if (showReaderBackedOnly && operation.IsReaderReady is null)
            return false;

        if (string.IsNullOrWhiteSpace(operationFilter))
            return true;

        return operation.OperationId.Contains(operationFilter, StringComparison.OrdinalIgnoreCase) ||
               operation.Summary.Contains(operationFilter, StringComparison.OrdinalIgnoreCase) ||
               (!string.IsNullOrWhiteSpace(operation.CliCommand) &&
                operation.CliCommand.Contains(operationFilter, StringComparison.OrdinalIgnoreCase)) ||
               (!string.IsNullOrWhiteSpace(operation.McpToolName) &&
                operation.McpToolName.Contains(operationFilter, StringComparison.OrdinalIgnoreCase));
    }

    private static void DrawPanelTitle(string title, string subtitle)
    {
        ImGui.TextColored(AccentColor, title);
        ImGui.TextWrapped(subtitle);
        ImGui.Spacing();
    }

    private static void DrawKeyValue(string label, string value)
    {
        ImGui.TextColored(MutedColor, label);
        ImGui.SameLine();
        ImGui.TextUnformatted(value);
    }

    private static void DrawStatusLine(string label, bool isHealthy, string text)
    {
        ImGui.TextColored(MutedColor, label);
        ImGui.SameLine();
        ImGui.TextColored(isHealthy ? SuccessColor : WarningColor, text);
    }

    private static void DrawWrappedStatus(Vector4 color, string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }

    private static void DrawInlineBadge(string text, Vector4 color)
    {
        ImGui.SameLine();
        ImGui.TextColored(color, text);
    }

    private static void DrawInlineTag(string text, Vector4 color)
    {
        ImGui.SameLine();
        ImGui.TextColored(color, $"[{text}]");
    }

    private static void DrawCommandCard(string title, string description, string displayCommand, string copyCommand, string copyButtonLabel)
    {
        ImGui.TextColored(AccentColor, title);
        ImGui.TextWrapped(description);
        DrawCodeBlock(title, displayCommand);
        if (ImGui.Button(copyButtonLabel, new Vector2(220f, 0f)))
            ImGui.SetClipboardText(copyCommand);
    }

    private static void DrawCodeBlock(string id, string content)
    {
        if (!ImGui.BeginChild(id, new Vector2(0f, 58f), true))
        {
            ImGui.EndChild();
            return;
        }

        ImGui.TextWrapped(content);
        ImGui.EndChild();
    }

    private static string ToCommandSummary(string command)
    {
        int markerIndex = command.LastIndexOf(" -- ", StringComparison.Ordinal);
        return markerIndex >= 0
            ? command[(markerIndex + 4)..]
            : command;
    }
}
