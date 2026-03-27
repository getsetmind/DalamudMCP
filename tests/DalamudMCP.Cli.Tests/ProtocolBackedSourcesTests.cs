using System.Text.Json;
using DalamudMCP.Framework.Cli;
using DalamudMCP.Protocol;

namespace DalamudMCP.Cli.Tests;

public sealed class ProtocolBackedSourcesTests
{
    [Fact]
    public async Task RemoteCliInvoker_returns_display_text_and_json_payload()
    {
        ProtocolOperationDescriptor operation = new(
            "player.context",
            ProtocolOperationVisibility.Both,
            [],
            "Gets player context.",
            "Gets player context.",
            ["player", "context"],
            null,
            "get_player_context",
            false);
        object playerPayload = new { characterName = "Test Adventurer" };
        FakeProtocolOperationClient client = new FakeProtocolOperationClient()
            .WithInvocationResult(new ProtocolInvocationResult(
                ProtocolPayloadFormat.Json,
                ProtocolContract.SerializePayload(
                    playerPayload,
                    playerPayload.GetType(),
                    ProtocolPayloadFormat.Json),
                "Test Adventurer @ ExampleWorld (Dancer 100)"));
        RemoteCliInvoker invoker = new([operation], client);

        bool invoked = invoker.TryInvoke(
            "player.context",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            [],
            services: null,
            jsonRequested: false,
            TestContext.Current.CancellationToken,
            out ValueTask<CliInvocationResult> invocation);

        Assert.True(invoked);

        CliInvocationResult result = await invocation;
        Assert.Equal("Test Adventurer @ ExampleWorld (Dancer 100)", result.Text);
        Assert.Null(result.Result);
        Assert.NotNull(result.RawJsonPayload);
        JsonElement payload = JsonSerializer.Deserialize<JsonElement>(result.RawJsonPayload!, ProtocolContract.JsonOptions);
        Assert.Equal("Test Adventurer", payload.GetProperty("characterName").GetString());
    }

    [Fact]
    public async Task RemoteCliInvoker_propagates_protocol_errors()
    {
        ProtocolOperationDescriptor operation = new(
            "player.context",
            ProtocolOperationVisibility.Both,
            [],
            "Gets player context.",
            "Gets player context.",
            ["player", "context"],
            null,
            "get_player_context",
            false);
        FakeProtocolOperationClient client = new FakeProtocolOperationClient()
            .WithException(new InvalidOperationException("player_not_ready"));
        RemoteCliInvoker invoker = new([operation], client);

        bool invoked = invoker.TryInvoke(
            "player.context",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            [],
            services: null,
            jsonRequested: false,
            TestContext.Current.CancellationToken,
            out ValueTask<CliInvocationResult> invocation);

        Assert.True(invoked);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await invocation);
        Assert.Contains("player_not_ready", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemoteCliInvoker_maps_option_payloads_for_protocol_operations()
    {
        ProtocolOperationDescriptor operation = new(
            "session.status",
            ProtocolOperationVisibility.Both,
            [
                new ProtocolParameterDescriptor(
                    "pipeName",
                    ProtocolValueKind.Text,
                    ProtocolParameterSource.Option,
                    true,
                    CliName: "pipe")
            ],
            "Gets session status.",
            "Gets session status.",
            ["session", "status"],
            null,
            "get_session_status",
            false);
        object sessionPayload = new { state = 2 };
        FakeProtocolOperationClient client = new FakeProtocolOperationClient()
            .WithInvocationResult(new ProtocolInvocationResult(
                ProtocolPayloadFormat.Json,
                ProtocolContract.SerializePayload(
                    sessionPayload,
                    sessionPayload.GetType(),
                    ProtocolPayloadFormat.Json),
                "ready"));
        RemoteCliInvoker invoker = new([operation], client);

        bool invoked = invoker.TryInvoke(
            "session.status",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["pipe"] = "DalamudMCP.1234"
            },
            [],
            services: null,
            jsonRequested: false,
            TestContext.Current.CancellationToken,
            out ValueTask<CliInvocationResult> invocation);

        Assert.True(invoked);
        await invocation;
        Assert.Equal("session.status", client.LastRequestType);

        Assert.Equal(ProtocolPayloadFormat.Json, client.LastPayload.Format);
        JsonElement payload = JsonSerializer.Deserialize<JsonElement>(client.LastPayload.Payload!, ProtocolContract.JsonOptions);
        Assert.Equal("DalamudMCP.1234", payload.GetProperty("pipeName").GetString());
    }

    [Fact]
    public async Task RemoteCliInvoker_keeps_raw_json_for_json_requested_path()
    {
        ProtocolOperationDescriptor operation = new(
            "player.context",
            ProtocolOperationVisibility.Both,
            [],
            "Gets player context.",
            "Gets player context.",
            ["player", "context"],
            null,
            "get_player_context",
            false);
        object playerPayload = new { characterName = "Test Adventurer" };
        FakeProtocolOperationClient client = new FakeProtocolOperationClient()
            .WithInvocationResult(new ProtocolInvocationResult(
                ProtocolPayloadFormat.Json,
                ProtocolContract.SerializePayload(
                    playerPayload,
                    playerPayload.GetType(),
                    ProtocolPayloadFormat.Json),
                null));
        RemoteCliInvoker invoker = new([operation], client);

        bool invoked = invoker.TryInvoke(
            "player.context",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            [],
            services: null,
            jsonRequested: true,
            TestContext.Current.CancellationToken,
            out ValueTask<CliInvocationResult> invocation);

        Assert.True(invoked);

        CliInvocationResult result = await invocation;
        Assert.Null(result.Result);
        Assert.NotNull(result.RawJsonPayload);
        Assert.Equal("{\"characterName\":\"Test Adventurer\"}", JsonSerializer.Deserialize<JsonElement>(result.RawJsonPayload!, ProtocolContract.JsonOptions).GetRawText());
    }

    private sealed class FakeProtocolOperationClient : IProtocolOperationClient
    {
        private ProtocolInvocationResult response;
        private Exception? exception;
        private bool hasResponse;

        public string? LastRequestType { get; private set; }

        public ProtocolRequestPayload LastPayload { get; private set; }

        public FakeProtocolOperationClient WithInvocationResult(ProtocolInvocationResult value)
        {
            response = value;
            hasResponse = true;
            exception = null;
            return this;
        }

        public FakeProtocolOperationClient WithException(Exception value)
        {
            exception = value;
            hasResponse = false;
            return this;
        }

        public ValueTask<ProtocolInvocationResult> InvokeAsync(string requestType, ProtocolRequestPayload request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (exception is not null)
                throw exception;
            if (!hasResponse)
                throw new InvalidOperationException("No protocol response was configured.");

            LastRequestType = requestType;
            LastPayload = request;
            return ValueTask.FromResult(response);
        }

        public ValueTask<TResponse> InvokeAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken)
            where TRequest : class
        {
            throw new NotSupportedException();
        }

        public ValueTask<DescribeOperationsResponse> DescribeOperationsAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
