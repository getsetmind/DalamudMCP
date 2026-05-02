using Manifold;
using DalamudMCP.Plugin.Configuration;
using DalamudMCP.Plugin.Hosting;
using DalamudMCP.Protocol;

namespace DalamudMCP.Plugin.Tests;

public sealed class OperationProtocolDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_resolves_player_context_by_protocol_operation_id()
    {
        TestDispatcherHarness harness = CreateHarness();
        OperationProtocolDispatcher dispatcher = harness.Dispatcher;

        ProtocolResponseEnvelope response = await dispatcher.DispatchAsync(
            new ProtocolRequestEnvelope(
                ProtocolContract.CurrentVersion,
                "player.context",
                "req-1",
                ProtocolPayloadFormat.None,
                ProtocolPayloadFormat.Json,
                null),
            TestContext.Current.CancellationToken);

        Assert.True(response.Success, $"{response.ErrorCode}: {response.ErrorMessage}");
        TestPlayerContextSnapshot? payload = ProtocolContract.DeserializePayload<TestPlayerContextSnapshot>(response.PayloadFormat, response.Payload);
        Assert.NotNull(payload);
        Assert.Equal("Test Adventurer", payload.CharacterName);
    }

    [Fact]
    public async Task DispatchAsync_resolves_player_context_by_legacy_request_name()
    {
        TestDispatcherHarness harness = CreateHarness();
        OperationProtocolDispatcher dispatcher = harness.Dispatcher;

        ProtocolResponseEnvelope response = await dispatcher.DispatchAsync(
            new ProtocolRequestEnvelope(
                ProtocolContract.CurrentVersion,
                "GetPlayerContext",
                "req-2",
                ProtocolPayloadFormat.None,
                ProtocolPayloadFormat.Json,
                null),
            TestContext.Current.CancellationToken);

        Assert.True(response.Success, $"{response.ErrorCode}: {response.ErrorMessage}");
        TestPlayerContextSnapshot? payload = ProtocolContract.DeserializePayload<TestPlayerContextSnapshot>(response.PayloadFormat, response.Payload);
        Assert.NotNull(payload);
        Assert.Equal("ExampleWorld", payload.HomeWorld);
    }

    [Fact]
    public async Task DispatchAsync_resolves_duty_context_by_protocol_operation_id()
    {
        TestDispatcherHarness harness = CreateHarness();
        OperationProtocolDispatcher dispatcher = harness.Dispatcher;

        ProtocolResponseEnvelope response = await dispatcher.DispatchAsync(
            new ProtocolRequestEnvelope(
                ProtocolContract.CurrentVersion,
                "duty.context",
                "req-3",
                ProtocolPayloadFormat.None,
                ProtocolPayloadFormat.Json,
                null),
            TestContext.Current.CancellationToken);

        Assert.True(response.Success, $"{response.ErrorCode}: {response.ErrorMessage}");
        TestDutyContextSnapshot? payload = ProtocolContract.DeserializePayload<TestDutyContextSnapshot>(response.PayloadFormat, response.Payload);
        Assert.NotNull(payload);
        Assert.Equal("Territory#777 is active.", payload.SummaryText);
    }

    [Fact]
    public async Task DispatchAsync_resolves_duty_context_by_legacy_request_name()
    {
        TestDispatcherHarness harness = CreateHarness();
        OperationProtocolDispatcher dispatcher = harness.Dispatcher;

        ProtocolResponseEnvelope response = await dispatcher.DispatchAsync(
            new ProtocolRequestEnvelope(
                ProtocolContract.CurrentVersion,
                "GetDutyContext",
                "req-4",
                ProtocolPayloadFormat.None,
                ProtocolPayloadFormat.Json,
                null),
            TestContext.Current.CancellationToken);

        Assert.True(response.Success, $"{response.ErrorCode}: {response.ErrorMessage}");
        TestDutyContextSnapshot? payload = ProtocolContract.DeserializePayload<TestDutyContextSnapshot>(response.PayloadFormat, response.Payload);
        Assert.NotNull(payload);
        Assert.True(payload.InDuty);
    }

    [Fact]
    public async Task DispatchAsync_describes_generated_operations()
    {
        TestDispatcherHarness harness = CreateHarness();
        OperationProtocolDispatcher dispatcher = harness.Dispatcher;

        ProtocolResponseEnvelope response = await dispatcher.DispatchAsync(
            new ProtocolRequestEnvelope(
                ProtocolContract.CurrentVersion,
                "__system.describe-operations",
                "req-5",
                ProtocolPayloadFormat.None,
                ProtocolPayloadFormat.MemoryPack,
                null),
            TestContext.Current.CancellationToken);

        Assert.True(response.Success, $"{response.ErrorCode}: {response.ErrorMessage}");
        DescribeOperationsResponse? payload = ProtocolContract.DeserializePayload<DescribeOperationsResponse>(response.PayloadFormat, response.Payload);
        Assert.NotNull(payload);
        Assert.Contains(payload.Operations, static operation => string.Equals(operation.OperationId, "player.context", StringComparison.Ordinal));
        Assert.Contains(payload.Operations, static operation => string.Equals(operation.OperationId, "session.status", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DispatchAsync_hides_action_operations_when_disabled()
    {
        TestDispatcherHarness harness = CreateHarness(enableActionOperations: false);
        OperationProtocolDispatcher dispatcher = harness.Dispatcher;

        ProtocolResponseEnvelope response = await dispatcher.DispatchAsync(
            new ProtocolRequestEnvelope(
                ProtocolContract.CurrentVersion,
                "__system.describe-operations",
                "req-6",
                ProtocolPayloadFormat.None,
                ProtocolPayloadFormat.MemoryPack,
                null),
            TestContext.Current.CancellationToken);

        Assert.True(response.Success, $"{response.ErrorCode}: {response.ErrorMessage}");
        DescribeOperationsResponse? payload = ProtocolContract.DeserializePayload<DescribeOperationsResponse>(response.PayloadFormat, response.Payload);
        Assert.NotNull(payload);
        Assert.DoesNotContain(payload.Operations, static operation => string.Equals(operation.OperationId, "teleport.to.aetheryte", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DispatchAsync_rejects_action_operations_when_disabled()
    {
        TestDispatcherHarness harness = CreateHarness(enableActionOperations: false);
        OperationProtocolDispatcher dispatcher = harness.Dispatcher;

        ProtocolResponseEnvelope response = await dispatcher.DispatchAsync(
            new ProtocolRequestEnvelope(
                ProtocolContract.CurrentVersion,
                "teleport.to.aetheryte",
                "req-7",
                ProtocolPayloadFormat.None,
                ProtocolPayloadFormat.Json,
                null),
            TestContext.Current.CancellationToken);

        Assert.False(response.Success);
        Assert.Equal("disabled", response.ErrorCode);
    }

    private static TestDispatcherHarness CreateHarness(bool enableActionOperations = true, bool enableUnsafeOperations = false)
    {
        TestPlayerContextOperation playerContextOperation = new(_ => ValueTask.FromResult(new TestPlayerContextSnapshot(
            "Test Adventurer",
            "ExampleWorld",
            "Dancer",
            100,
            "Sample Plaza",
            new TestPlayerPosition(1.0, 2.0, 3.0))));
        TestDutyContextOperation dutyContextOperation = new(_ => ValueTask.FromResult(new TestDutyContextSnapshot(
            777,
            "Territory#777",
            "duty",
            true,
            false,
            "Territory#777 is active.")));
        TestSessionStatusOperation sessionStatusOperation = new(_ => ValueTask.FromResult(new TestSessionStatusSnapshot(
            TestSessionRuntimeState.Ready,
            "ready",
            [
                new TestSessionReaderStatus("player.context", true, "ready"),
                new TestSessionReaderStatus("duty.context", true, "ready")
            ])));
        TestTeleportToAetheryteOperation teleportOperation = new((request, _) => ValueTask.FromResult(
            new TestTeleportToAetheryteResult(
                request.Query,
                true,
                null,
                62,
                "Sample Plaza",
                "Sample Plaza",
                "Teleport started.")));
        TestUnsafeInvokePluginIpcOperation unsafeInvokePluginIpcOperation = new((request, _) => ValueTask.FromResult(
            new TestUnsafeInvokePluginIpcResult(
                request.Callgate,
                true,
                null,
                request.ResultKind,
                "true",
                $"IPC '{request.Callgate}' returned true.")));

        IReadOnlyList<OperationDescriptor> operations = CreateOperationDescriptors();
        TestPluginUiConfigurationAccessor configuration = new(
            new PluginUiConfiguration
            {
                EnableActionOperations = enableActionOperations,
                EnableUnsafeOperations = enableUnsafeOperations
            });
        TestOperationInvoker invoker = new(
            playerContextOperation,
            dutyContextOperation,
            sessionStatusOperation,
            teleportOperation,
            unsafeInvokePluginIpcOperation);

        return new TestDispatcherHarness(
            new OperationProtocolDispatcher(
                services: TestServiceProvider.Instance,
                operationInvoker: invoker,
                operations: operations,
                configurationStore: configuration));
    }

    [Fact]
    public async Task DispatchAsync_hides_unsafe_operations_when_disabled()
    {
        TestDispatcherHarness harness = CreateHarness(enableUnsafeOperations: false);
        OperationProtocolDispatcher dispatcher = harness.Dispatcher;

        ProtocolResponseEnvelope response = await dispatcher.DispatchAsync(
            new ProtocolRequestEnvelope(
                ProtocolContract.CurrentVersion,
                "__system.describe-operations",
                "req-8",
                ProtocolPayloadFormat.None,
                ProtocolPayloadFormat.MemoryPack,
                null),
            TestContext.Current.CancellationToken);

        Assert.True(response.Success, $"{response.ErrorCode}: {response.ErrorMessage}");
        DescribeOperationsResponse? payload = ProtocolContract.DeserializePayload<DescribeOperationsResponse>(response.PayloadFormat, response.Payload);
        Assert.NotNull(payload);
        Assert.DoesNotContain(payload.Operations, static operation => string.Equals(operation.OperationId, "unsafe.invoke.plugin-ipc", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DispatchAsync_rejects_unsafe_operations_when_disabled()
    {
        TestDispatcherHarness harness = CreateHarness(enableUnsafeOperations: false);
        OperationProtocolDispatcher dispatcher = harness.Dispatcher;

        ProtocolResponseEnvelope response = await dispatcher.DispatchAsync(
            new ProtocolRequestEnvelope(
                ProtocolContract.CurrentVersion,
                "unsafe.invoke.plugin-ipc",
                "req-9",
                ProtocolPayloadFormat.None,
                ProtocolPayloadFormat.Json,
                null),
            TestContext.Current.CancellationToken);

        Assert.False(response.Success);
        Assert.Equal("disabled", response.ErrorCode);
    }

    private static IReadOnlyList<OperationDescriptor> CreateOperationDescriptors()
    {
        return
        [
            new OperationDescriptor(
                "player.context",
                typeof(TestPlayerContextOperation),
                nameof(TestPlayerContextOperation.ExecuteAsync),
                typeof(TestPlayerContextSnapshot),
                OperationVisibility.Both,
                [],
                Description: "Gets the current player context.",
                Summary: "Gets player context.",
                CliCommandPath: ["player", "context"],
                McpToolName: "get_player_context",
                RequestType: typeof(TestPlayerContextRequest)),
            new OperationDescriptor(
                "duty.context",
                typeof(TestDutyContextOperation),
                nameof(TestDutyContextOperation.ExecuteAsync),
                typeof(TestDutyContextSnapshot),
                OperationVisibility.Both,
                [],
                Description: "Gets the current duty context.",
                Summary: "Gets duty context.",
                CliCommandPath: ["duty", "context"],
                McpToolName: "get_duty_context",
                RequestType: typeof(TestDutyContextRequest)),
            new OperationDescriptor(
                "session.status",
                typeof(TestSessionStatusOperation),
                nameof(TestSessionStatusOperation.ExecuteAsync),
                typeof(TestSessionStatusSnapshot),
                OperationVisibility.Both,
                [],
                Description: "Gets the current DalamudMCP session status.",
                Summary: "Gets session status.",
                CliCommandPath: ["session", "status"],
                McpToolName: "get_session_status",
                RequestType: typeof(TestSessionStatusRequest)),
            new OperationDescriptor(
                "teleport.to.aetheryte",
                typeof(TestTeleportToAetheryteOperation),
                nameof(TestTeleportToAetheryteOperation.ExecuteAsync),
                typeof(TestTeleportToAetheryteResult),
                OperationVisibility.Both,
                [
                    new ParameterDescriptor(
                        "query",
                        typeof(string),
                        ParameterSource.Option,
                        true,
                        Description: "Aetheryte name or alias to search for.",
                        CliName: "query",
                        McpName: "query",
                        RequestPropertyName: nameof(TestTeleportToAetheryteRequest.Query))
                ],
                Description: "Teleports to an unlocked aetheryte by query.",
                Summary: "Teleports to an aetheryte.",
                CliCommandPath: ["teleport", "to", "aetheryte"],
                McpToolName: "teleport_to_aetheryte",
                RequestType: typeof(TestTeleportToAetheryteRequest)),
            new OperationDescriptor(
                "unsafe.invoke.plugin-ipc",
                typeof(TestUnsafeInvokePluginIpcOperation),
                nameof(TestUnsafeInvokePluginIpcOperation.ExecuteAsync),
                typeof(TestUnsafeInvokePluginIpcResult),
                OperationVisibility.Both,
                [
                    new ParameterDescriptor("callgate", typeof(string), ParameterSource.Option, true, Description: "IPC callgate.", CliName: "callgate", McpName: "callgate", RequestPropertyName: nameof(TestUnsafeInvokePluginIpcRequest.Callgate)),
                    new ParameterDescriptor("resultKind", typeof(string), ParameterSource.Option, true, Description: "Expected result kind.", CliName: "result-kind", McpName: "result-kind", RequestPropertyName: nameof(TestUnsafeInvokePluginIpcRequest.ResultKind)),
                    new ParameterDescriptor("argumentKinds", typeof(string), ParameterSource.Option, false, Description: "Argument kinds.", CliName: "argument-kinds", McpName: "argument-kinds", RequestPropertyName: nameof(TestUnsafeInvokePluginIpcRequest.ArgumentKinds)),
                    new ParameterDescriptor("argumentsJson", typeof(string), ParameterSource.Option, false, Description: "Arguments JSON.", CliName: "arguments-json", McpName: "arguments-json", RequestPropertyName: nameof(TestUnsafeInvokePluginIpcRequest.ArgumentsJson)),
                    new ParameterDescriptor("runOnFrameworkThread", typeof(bool), ParameterSource.Option, false, Description: "Run on framework thread.", CliName: "run-on-framework-thread", McpName: "run-on-framework-thread", RequestPropertyName: nameof(TestUnsafeInvokePluginIpcRequest.RunOnFrameworkThread))
                ],
                Description: "Invokes a plugin IPC function callgate.",
                Summary: "Invokes a plugin IPC function callgate.",
                CliCommandPath: ["unsafe", "invoke", "plugin-ipc"],
                McpToolName: "unsafe_invoke_plugin_ipc",
                RequestType: typeof(TestUnsafeInvokePluginIpcRequest))
        ];
    }

    private sealed class TestDispatcherHarness(OperationProtocolDispatcher dispatcher)
    {
        public OperationProtocolDispatcher Dispatcher { get; } = dispatcher;
    }

    private sealed class TestServiceProvider : IServiceProvider
    {
        public static TestServiceProvider Instance { get; } = new();

        public object? GetService(Type serviceType)
        {
            ArgumentNullException.ThrowIfNull(serviceType);
            return null;
        }
    }

    private sealed class TestOperationInvoker(
        TestPlayerContextOperation playerContextOperation,
        TestDutyContextOperation dutyContextOperation,
        TestSessionStatusOperation sessionStatusOperation,
        TestTeleportToAetheryteOperation teleportOperation,
        TestUnsafeInvokePluginIpcOperation unsafeInvokePluginIpcOperation) : IOperationInvoker
    {
        public bool TryInvoke(
            string operationId,
            object? request,
            IServiceProvider? services,
            InvocationSurface surface,
            CancellationToken cancellationToken,
            out ValueTask<OperationInvocationResult> invocation)
        {
            ArgumentNullException.ThrowIfNull(services);

            switch (operationId)
            {
                case "player.context":
                    invocation = InvokeAsync(
                        playerContextOperation,
                        request as TestPlayerContextRequest ?? new TestPlayerContextRequest(),
                        operationId,
                        services,
                        surface,
                        cancellationToken);
                    return true;
                case "duty.context":
                    invocation = InvokeAsync(
                        dutyContextOperation,
                        request as TestDutyContextRequest ?? new TestDutyContextRequest(),
                        operationId,
                        services,
                        surface,
                        cancellationToken);
                    return true;
                case "session.status":
                    invocation = InvokeAsync(
                        sessionStatusOperation,
                        request as TestSessionStatusRequest ?? new TestSessionStatusRequest(),
                        operationId,
                        services,
                        surface,
                        cancellationToken);
                    return true;
                case "teleport.to.aetheryte":
                    invocation = InvokeAsync(
                        teleportOperation,
                        request as TestTeleportToAetheryteRequest ?? new TestTeleportToAetheryteRequest(),
                        operationId,
                        services,
                        surface,
                        cancellationToken);
                    return true;
                case "unsafe.invoke.plugin-ipc":
                    invocation = InvokeAsync(
                        unsafeInvokePluginIpcOperation,
                        request as TestUnsafeInvokePluginIpcRequest ?? new TestUnsafeInvokePluginIpcRequest(),
                        operationId,
                        services,
                        surface,
                        cancellationToken);
                    return true;
                default:
                    invocation = default;
                    return false;
            }
        }

        private static async ValueTask<OperationInvocationResult> InvokeAsync<TRequest, TResult>(
            IOperation<TRequest, TResult> operation,
            TRequest request,
            string operationId,
            IServiceProvider? services,
            InvocationSurface surface,
            CancellationToken cancellationToken)
        {
            TResult result = await operation.ExecuteAsync(
                request,
                new OperationContext(operationId, surface, services, cancellationToken: cancellationToken));

            return new OperationInvocationResult(result, typeof(TResult));
        }
    }

    private sealed class TestPluginUiConfigurationAccessor(PluginUiConfiguration current) : IPluginUiConfigurationAccessor
    {
        public PluginUiConfiguration Current { get; } = current;
    }

    [ProtocolOperation("player.context")]
    [LegacyBridgeRequest("GetPlayerContext")]
    private sealed record TestPlayerContextRequest;

    private sealed class TestPlayerContextOperation(Func<CancellationToken, ValueTask<TestPlayerContextSnapshot>> executor)
        : IOperation<TestPlayerContextRequest, TestPlayerContextSnapshot>
    {
        public ValueTask<TestPlayerContextSnapshot> ExecuteAsync(TestPlayerContextRequest request, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(context);
            return executor(context.CancellationToken);
        }
    }

    private sealed record TestPlayerPosition(double X, double Y, double Z);

    private sealed record TestPlayerContextSnapshot(
        string CharacterName,
        string HomeWorld,
        string JobName,
        int JobLevel,
        string TerritoryName,
        TestPlayerPosition Position);

    [ProtocolOperation("duty.context")]
    [LegacyBridgeRequest("GetDutyContext")]
    private sealed record TestDutyContextRequest;

    private sealed class TestDutyContextOperation(Func<CancellationToken, ValueTask<TestDutyContextSnapshot>> executor)
        : IOperation<TestDutyContextRequest, TestDutyContextSnapshot>
    {
        public ValueTask<TestDutyContextSnapshot> ExecuteAsync(TestDutyContextRequest request, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(context);
            return executor(context.CancellationToken);
        }
    }

    private sealed record TestDutyContextSnapshot(
        int? TerritoryId,
        string? DutyName,
        string DutyType,
        bool InDuty,
        bool IsDutyComplete,
        string SummaryText);

    [ProtocolOperation("session.status")]
    [LegacyBridgeRequest("GetSessionStatus")]
    private sealed record TestSessionStatusRequest;

    private sealed class TestSessionStatusOperation(Func<CancellationToken, ValueTask<TestSessionStatusSnapshot>> executor)
        : IOperation<TestSessionStatusRequest, TestSessionStatusSnapshot>
    {
        public ValueTask<TestSessionStatusSnapshot> ExecuteAsync(TestSessionStatusRequest request, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(context);
            return executor(context.CancellationToken);
        }
    }

    private enum TestSessionRuntimeState
    {
        Unknown = 0,
        Starting = 1,
        Ready = 2,
        Degraded = 3
    }

    private sealed record TestSessionReaderStatus(string ReaderKey, bool IsReady, string? Detail = null);

    private sealed record TestSessionStatusSnapshot(
        TestSessionRuntimeState State,
        string SummaryText,
        IReadOnlyList<TestSessionReaderStatus> Readers);

    [ProtocolOperation("teleport.to.aetheryte")]
    [LegacyBridgeRequest("TeleportToAetheryte")]
    private sealed record TestTeleportToAetheryteRequest
    {
        public string Query { get; init; } = string.Empty;
    }

    private sealed class TestTeleportToAetheryteOperation(
        Func<TestTeleportToAetheryteRequest, CancellationToken, ValueTask<TestTeleportToAetheryteResult>> executor)
        : IOperation<TestTeleportToAetheryteRequest, TestTeleportToAetheryteResult>
    {
        public ValueTask<TestTeleportToAetheryteResult> ExecuteAsync(TestTeleportToAetheryteRequest request, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(context);
            return executor(request, context.CancellationToken);
        }
    }

    private sealed record TestTeleportToAetheryteResult(
        string RequestedQuery,
        bool Succeeded,
        string? Reason,
        uint? AetheryteId,
        string? AetheryteName,
        string? TerritoryName,
        string SummaryText);

    [ProtocolOperation("unsafe.invoke.plugin-ipc")]
    private sealed record TestUnsafeInvokePluginIpcRequest
    {
        public string Callgate { get; init; } = string.Empty;

        public string ResultKind { get; init; } = string.Empty;

        public string? ArgumentKinds { get; init; }

        public string? ArgumentsJson { get; init; }

        public bool? RunOnFrameworkThread { get; init; }
    }

    private sealed class TestUnsafeInvokePluginIpcOperation(
        Func<TestUnsafeInvokePluginIpcRequest, CancellationToken, ValueTask<TestUnsafeInvokePluginIpcResult>> executor)
        : IOperation<TestUnsafeInvokePluginIpcRequest, TestUnsafeInvokePluginIpcResult>
    {
        public ValueTask<TestUnsafeInvokePluginIpcResult> ExecuteAsync(TestUnsafeInvokePluginIpcRequest request, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(context);
            return executor(request, context.CancellationToken);
        }
    }

    private sealed record TestUnsafeInvokePluginIpcResult(
        string Callgate,
        bool Succeeded,
        string? Reason,
        string ResultKind,
        string? ResultJson,
        string SummaryText);
}



