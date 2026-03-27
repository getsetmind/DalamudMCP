using DalamudMCP.Framework;
using DalamudMCP.Plugin.Readers;
using DalamudMCP.Plugin.Ui;

namespace DalamudMCP.Plugin.Tests;

public sealed class PluginConfigWindowModelTests
{
    [Fact]
    public void Create_builds_rows_and_reader_status_from_operations()
    {
        PluginRuntimeOptions options = new("DalamudMCP.12345");
        OperationDescriptor[] operations =
        [
            new(
                "player.context",
                typeof(object),
                "ExecuteAsync",
                typeof(object),
                OperationVisibility.Both,
                [],
                Description: "Gets the current player context.",
                Summary: "Gets player context.",
                CliCommandPath: ["player", "context"],
                McpToolName: "get_player_context"),
            new(
                "session.status",
                typeof(object),
                "ExecuteAsync",
                typeof(object),
                OperationVisibility.Both,
                [],
                Description: "Gets the current session status.",
                Summary: "Gets session status.",
                CliCommandPath: ["session", "status"],
                McpToolName: "get_session_status")
        ];
        IPluginReaderStatus[] readerStatuses =
        [
            new FakePluginReaderStatus("player.context", true, "ready")
        ];

        PluginConfigWindowModel model = PluginConfigWindowModel.Create(
            options,
            protocolServerRunning: true,
            autoStartHttpServerOnLoad: true,
            actionOperationsEnabled: false,
            unsafeOperationsEnabled: false,
            new Hosting.PluginMcpServerStatus(
                true,
                "http://127.0.0.1:38473/mcp",
                "server\\DalamudMCP.Cli.exe --pipe DalamudMCP.12345 serve http",
                null),
            operations,
            readerStatuses);

        Assert.Equal("DalamudMCP.12345", model.PipeName);
        Assert.True(model.ProtocolServerRunning);
        Assert.True(model.AutoStartHttpServerOnLoad);
        Assert.False(model.ActionOperationsEnabled);
        Assert.True(model.McpServerRunning);
        Assert.Equal("http://127.0.0.1:38473/mcp", model.McpServerEndpoint);
        Assert.Equal(1, model.ReadyReaderCount);
        Assert.Equal(1, model.ReaderCount);
        Assert.Equal("Active pipe (advanced): DalamudMCP.12345", model.PipeNameText);
        Assert.Equal(@"dotnet run --project .\src\DalamudMCP.Cli\DalamudMCP.Cli.csproj -- player context", model.CliCommand);
        Assert.Equal(@"dotnet run --project .\src\DalamudMCP.Cli\DalamudMCP.Cli.csproj -- serve mcp", model.McpCommand);

        PluginConfigOperationRow playerContext = Assert.Single(
            model.Operations,
            static row => string.Equals(row.OperationId, "player.context", StringComparison.Ordinal));
        Assert.Equal("player context", playerContext.CliCommand);
        Assert.Equal("get_player_context", playerContext.McpToolName);
        Assert.True(playerContext.IsReaderReady);
        Assert.Equal("ready", playerContext.ReaderDetail);
    }

    [Fact]
    public void Create_keeps_non_reader_operations_visible()
    {
        PluginConfigWindowModel model = PluginConfigWindowModel.Create(
            new PluginRuntimeOptions("DalamudMCP.99999"),
            protocolServerRunning: false,
            autoStartHttpServerOnLoad: false,
            actionOperationsEnabled: false,
            unsafeOperationsEnabled: false,
            new Hosting.PluginMcpServerStatus(
                false,
                "http://127.0.0.1:38473/mcp",
                null,
                "not running"),
            [
                new OperationDescriptor(
                    "duty.context",
                    typeof(object),
                    "ExecuteAsync",
                    typeof(object),
                    OperationVisibility.Both,
                    [],
                    Summary: "Gets duty context.",
                    CliCommandPath: ["duty", "context"],
                    McpToolName: "get_duty_context")
            ],
            []);

        PluginConfigOperationRow row = Assert.Single(model.Operations);
        Assert.Equal("duty.context", row.OperationId);
        Assert.Null(row.IsReaderReady);
        Assert.Null(row.ReaderDetail);
        Assert.False(model.ProtocolServerRunning);
        Assert.False(model.AutoStartHttpServerOnLoad);
        Assert.False(model.ActionOperationsEnabled);
        Assert.False(model.McpServerRunning);
        Assert.Equal("not running", model.McpServerError);
    }

    [Fact]
    public void ApplyStatus_does_not_allocate_after_warmup()
    {
        PluginConfigWindowModel model = PluginConfigWindowModel.Create(
            new PluginRuntimeOptions("DalamudMCP.12345"),
            protocolServerRunning: true,
            autoStartHttpServerOnLoad: false,
            actionOperationsEnabled: false,
            unsafeOperationsEnabled: false,
            new Hosting.PluginMcpServerStatus(
                true,
                "http://127.0.0.1:38473/mcp",
                "dotnet server",
                null),
            [
                new OperationDescriptor(
                    "player.context",
                    typeof(object),
                    "ExecuteAsync",
                    typeof(object),
                    OperationVisibility.Both,
                    [],
                    Summary: "Gets player context.",
                    CliCommandPath: ["player", "context"],
                    McpToolName: "get_player_context")
            ],
            [
                new FakePluginReaderStatus("player.context", true, "ready")
            ]);

        model.ApplyStatus(
            protocolServerRunning: true,
            autoStartHttpServerOnLoad: false,
            actionOperationsEnabled: false,
            unsafeOperationsEnabled: false,
            mcpServerRunning: true,
            mcpServerEndpoint: "http://127.0.0.1:38473/mcp",
            mcpServerCommand: "dotnet server",
            mcpServerError: null);

        long before = GC.GetAllocatedBytesForCurrentThread();
        model.ApplyStatus(
            protocolServerRunning: true,
            autoStartHttpServerOnLoad: false,
            actionOperationsEnabled: false,
            unsafeOperationsEnabled: false,
            mcpServerRunning: true,
            mcpServerEndpoint: "http://127.0.0.1:38473/mcp",
            mcpServerCommand: "dotnet server",
            mcpServerError: null);
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
    }

    [Fact]
    public void Create_handles_reader_status_that_requires_main_thread()
    {
        PluginConfigWindowModel model = PluginConfigWindowModel.Create(
            new PluginRuntimeOptions("DalamudMCP.12345"),
            protocolServerRunning: true,
            autoStartHttpServerOnLoad: false,
            actionOperationsEnabled: false,
            unsafeOperationsEnabled: false,
            new Hosting.PluginMcpServerStatus(
                true,
                "http://127.0.0.1:38473/mcp",
                "dotnet server",
                null),
            [
                new OperationDescriptor(
                    "fate.context",
                    typeof(object),
                    "ExecuteAsync",
                    typeof(object),
                    OperationVisibility.Both,
                    [],
                    Summary: "Gets nearby FATE context.",
                    CliCommandPath: ["fate", "context"],
                    McpToolName: "get_fate_context")
            ],
            [
                new ThrowingPluginReaderStatus("fate.context")
            ]);

        PluginConfigOperationRow row = Assert.Single(model.Operations);
        Assert.False(row.IsReaderReady);
        Assert.Equal("main_thread_required", row.ReaderDetail);
        Assert.Equal("Reader: not ready (main_thread_required)", row.ReaderStatusText);
    }

    [Fact]
    public void Create_marks_action_operations_as_disabled_by_default()
    {
        PluginConfigWindowModel model = PluginConfigWindowModel.Create(
            new PluginRuntimeOptions("DalamudMCP.12345"),
            protocolServerRunning: true,
            autoStartHttpServerOnLoad: false,
            actionOperationsEnabled: false,
            unsafeOperationsEnabled: false,
            new Hosting.PluginMcpServerStatus(
                true,
                "http://127.0.0.1:38473/mcp",
                "dotnet server",
                null),
            [
                new OperationDescriptor(
                    "teleport.to.aetheryte",
                    typeof(object),
                    "ExecuteAsync",
                    typeof(object),
                    OperationVisibility.Both,
                    [],
                    Summary: "Teleports to an aetheryte.",
                    CliCommandPath: ["teleport", "to", "aetheryte"],
                    McpToolName: "teleport_to_aetheryte")
            ],
            []);

        PluginConfigOperationRow row = Assert.Single(model.Operations);
        Assert.Equal("Action operations: disabled", model.ActionOperationsStatusText);
        Assert.Equal("Exposure: disabled until action operations are enabled", row.ExposureStatusText);
    }

    [Fact]
    public void Create_marks_unsafe_operations_as_disabled_by_default()
    {
        PluginConfigWindowModel model = PluginConfigWindowModel.Create(
            new PluginRuntimeOptions("DalamudMCP.12345"),
            protocolServerRunning: true,
            autoStartHttpServerOnLoad: false,
            actionOperationsEnabled: false,
            unsafeOperationsEnabled: false,
            new Hosting.PluginMcpServerStatus(
                true,
                "http://127.0.0.1:38473/mcp",
                "dotnet server",
                null),
            [
                new OperationDescriptor(
                    "unsafe.invoke.plugin-ipc",
                    typeof(object),
                    "ExecuteAsync",
                    typeof(object),
                    OperationVisibility.Both,
                    [],
                    Summary: "Invokes a plugin IPC function callgate.",
                    CliCommandPath: ["unsafe", "invoke", "plugin-ipc"],
                    McpToolName: "unsafe_invoke_plugin_ipc")
            ],
            []);

        PluginConfigOperationRow row = Assert.Single(model.Operations);
        Assert.Equal("Unsafe operations: disabled", model.UnsafeOperationsStatusText);
        Assert.Equal("Exposure: disabled until unsafe operations are enabled", row.ExposureStatusText);
    }

    private sealed record FakePluginReaderStatus(string ReaderKey, bool IsReady, string Detail) : IPluginReaderStatus;

    private sealed class ThrowingPluginReaderStatus(string readerKey) : IPluginReaderStatus
    {
        public string ReaderKey { get; } = readerKey;

        public bool IsReady => throw new InvalidOperationException("Not on main thread!");

        public string Detail => throw new InvalidOperationException("Not on main thread!");
    }
}
