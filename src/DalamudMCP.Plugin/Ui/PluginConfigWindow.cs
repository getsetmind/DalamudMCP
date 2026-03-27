using System.Numerics;
using Dalamud.Bindings.ImGui;
using DalamudMCP.Framework;
using DalamudMCP.Plugin.Configuration;
using DalamudMCP.Plugin.Readers;
using DalamudMCP.Protocol;

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
    private readonly PluginConfigWindowModel model;
    private readonly NamedPipeProtocolServer protocolServer;
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
        IReadOnlyList<IPluginReaderStatus> readerStatuses)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.protocolServer = protocolServer ?? throw new ArgumentNullException(nameof(protocolServer));
        this.configurationStore = configurationStore ?? throw new ArgumentNullException(nameof(configurationStore));
        this.mcpServerController = mcpServerController ?? throw new ArgumentNullException(nameof(mcpServerController));
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(readerStatuses);

        model = PluginConfigWindowModel.Create(
            options,
            protocolServer.IsRunning,
            configurationStore.Current.AutoStartHttpServerOnLoad,
            configurationStore.Current.EnableActionOperations,
            configurationStore.Current.EnableUnsafeOperations,
            mcpServerController.GetStatus(),
            operations,
            readerStatuses);
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
        if (!ImGui.Begin("DalamudMCP Settings", ref isOpen, ImGuiWindowFlags.NoCollapse))
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

    private void DrawHeader()
    {
        ImGui.TextColored(AccentColor, "DalamudMCP");
        ImGui.SameLine();
        ImGui.TextDisabled("Live bridge for FFXIV observations, actions, and MCP exposure.");

        DrawInlineBadge(
            model.ProtocolServerRunning ? "PIPE LIVE" : "PIPE DOWN",
            model.ProtocolServerRunning ? SuccessColor : DangerColor);
        DrawInlineBadge(
            model.McpServerRunning ? "HTTP LIVE" : "HTTP STOPPED",
            model.McpServerRunning ? SuccessColor : WarningColor);
        DrawInlineBadge(
            $"{model.ExposedOperationCount}/{model.OperationCount} EXPOSED",
            AccentColor);

        ImGui.TextColored(MutedColor, "The top row is for runtime health. The bottom half is for operations browsing and copy-ready commands.");
        ImGui.Separator();
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

        DrawPanelTitle("Runtime", "Connection health, discovery, and exposure posture.");
        DrawKeyValue("Discovery", "CLI auto-discovery enabled");
        DrawStatusLine("Named pipe", model.ProtocolServerRunning, model.ProtocolServerStatusText);
        if (!string.IsNullOrWhiteSpace(model.ReaderStatusText))
            DrawStatusLine("Readers", model.ReadyReaderCount == model.ReaderCount, model.ReaderStatusText!);

        DrawStatusLine("Action tools", model.ActionOperationsEnabled, model.ActionOperationsStatusText);
        DrawStatusLine("Unsafe tools", model.UnsafeOperationsEnabled, model.UnsafeOperationsStatusText);
        DrawKeyValue("Operations", $"{model.OperationCount} total  |  {model.ExposedOperationCount} exposed  |  {model.BlockedOperationCount} gated");

        ImGui.Spacing();
        bool actionOperationsEnabled = model.ActionOperationsEnabled;
        if (ImGui.Checkbox("Enable action operations over CLI/MCP", ref actionOperationsEnabled))
        {
            configurationStore.Update(configuration =>
                configuration.EnableActionOperations = actionOperationsEnabled);
            RefreshModel(force: true);
        }

        ImGui.TextWrapped("Observation tools stay live. Actions remain default-off until you explicitly expose them here.");

        bool unsafeOperationsEnabled = model.UnsafeOperationsEnabled;
        if (ImGui.Checkbox("Enable unsafe integration tools (developer only)", ref unsafeOperationsEnabled))
        {
            configurationStore.Update(configuration =>
                configuration.EnableUnsafeOperations = unsafeOperationsEnabled);
            RefreshModel(force: true);
        }

        ImGui.TextWrapped("Unsafe tools can invoke arbitrary plugin IPC functions. Leave them off unless you are deliberately debugging another plugin.");
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

        DrawPanelTitle("Command Desk", "Copy the two entrypoints people need most often.");
        ImGui.Columns(2, "QuickStartColumns", false);
        DrawCommandCard(
            "CLI quick check",
            "Reads the live player snapshot from the active plugin instance.",
            ToCommandSummary(model.CliCommand),
            model.CliCommand,
            "Copy Player Context Command");
        ImGui.NextColumn();
        DrawCommandCard(
            "MCP serve",
            "Starts the local MCP bridge with the plugin-discovered live pipe.",
            ToCommandSummary(model.McpCommand),
            model.McpCommand,
            "Copy MCP Serve Command");
        ImGui.Columns(1);
        ImGui.EndChild();
    }

    private void DrawAdvancedDetails()
    {
        ImGui.Spacing();
        if (!ImGui.CollapsingHeader("Advanced details", ref showAdvancedDetails))
            return;

        if (!ImGui.BeginChild("AdvancedPanel", new Vector2(0f, 132f), true))
        {
            ImGui.EndChild();
            return;
        }

        DrawKeyValue("Pipe", model.PipeName);
        DrawKeyValue("CLI command", model.CliCommand);
        DrawKeyValue("MCP serve", model.McpCommand);
        if (!string.IsNullOrWhiteSpace(model.McpServerCommand))
            DrawKeyValue("HTTP command", model.McpServerCommand);
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

        DrawPanelTitle("HTTP Server", "Stable MCP endpoint for clients that should not care about pipe names.");
        DrawKeyValue("Endpoint", model.McpServerEndpoint);
        DrawStatusLine("HTTP state", model.McpServerRunning, model.McpServerStatusText);

        bool autoStartHttpServerOnLoad = model.AutoStartHttpServerOnLoad;
        if (ImGui.Checkbox("Start MCP HTTP Server automatically on plugin load", ref autoStartHttpServerOnLoad))
        {
            configurationStore.Update(configuration =>
                configuration.AutoStartHttpServerOnLoad = autoStartHttpServerOnLoad);
            RefreshModel(force: true);
        }

        ImGui.Spacing();
        if (!model.McpServerRunning)
        {
            if (ImGui.Button("Start MCP HTTP Server", new Vector2(220f, 0f)))
            {
                mcpServerController.Start();
                nextRefreshAt = 0;
            }
        }
        else if (ImGui.Button("Stop MCP HTTP Server", new Vector2(220f, 0f)))
        {
            mcpServerController.Stop();
            nextRefreshAt = 0;
        }

        ImGui.SameLine();
        if (ImGui.Button("Copy MCP Endpoint", new Vector2(180f, 0f)))
            ImGui.SetClipboardText(model.McpServerEndpoint);

        if (!string.IsNullOrWhiteSpace(model.McpServerCommand))
        {
            if (ImGui.Button("Copy MCP Server Command", new Vector2(220f, 0f)))
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

        DrawPanelTitle("Operations", "Filter the exported surface before you hand the plugin to another client.");
        DrawKeyValue(
            "Catalog",
            $"{model.OperationCount} total  |  {model.ActionOperationCount} action  |  {model.UnsafeOperationCount} unsafe  |  {model.BlockedOperationCount} gated");

        ImGui.SetNextItemWidth(280f);
        ImGui.InputText("Search", ref operationFilter, 128);
        ImGui.SameLine();
        ImGui.Checkbox("Blocked only", ref showBlockedOnly);
        ImGui.SameLine();
        ImGui.Checkbox("Reader-backed only", ref showReaderBackedOnly);

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
            ImGui.TableSetupColumn("Operation", ImGuiTableColumnFlags.WidthStretch, 0.27f);
            ImGui.TableSetupColumn("Access", ImGuiTableColumnFlags.WidthStretch, 0.23f);
            ImGui.TableSetupColumn("State", ImGuiTableColumnFlags.WidthStretch, 0.20f);
            ImGui.TableSetupColumn("Summary", ImGuiTableColumnFlags.WidthStretch, 0.30f);
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
                DrawInlineTag(operation.IsActionOperation ? "ACTION" : "OBSERVE", operation.IsActionOperation ? WarningColor : SuccessColor);
                if (operation.IsUnsafeOperation)
                    DrawInlineTag("UNSAFE", DangerColor);

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
                    ImGui.TextColored(SuccessColor, "Ready to expose");
                }

                ImGui.TableSetColumnIndex(3);
                ImGui.TextWrapped(operation.Summary);
            }

            if (visibleCount == 0)
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.TextColored(MutedColor, "No operations matched the current filter.");
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
