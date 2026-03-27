using System.Runtime.CompilerServices;
using DalamudMCP.Protocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DalamudMCP.Cli.Tests;

public sealed class RemoteMcpToolServiceTests
{
    [Fact]
    public async Task ListToolsAsync_does_not_allocate_after_warmup()
    {
        RemoteMcpToolService service = new(
            [
                new ProtocolOperationDescriptor(
                    "player.context",
                    ProtocolOperationVisibility.Both,
                    [],
                    Description: "Gets the current player context.",
                    Summary: "Gets player context.",
                    CliCommandPath: ["player", "context"],
                    McpToolName: "get_player_context")
            ],
            new FakeProtocolOperationClient());
        RequestContext<ListToolsRequestParams> requestContext = CreateRequestContext<ListToolsRequestParams>();

        _ = await service.ListToolsAsync(requestContext, TestContext.Current.CancellationToken);

        long before = GC.GetAllocatedBytesForCurrentThread();
        ListToolsResult result = await service.ListToolsAsync(requestContext, TestContext.Current.CancellationToken);
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Single(result.Tools);
        Assert.Equal(0, after - before);
    }

    [Fact]
    public async Task ListToolsAsync_refreshes_catalog_when_interval_expires()
    {
        FakeProtocolOperationClient client = new(
            new DescribeOperationsResponse(
            [
                new ProtocolOperationDescriptor(
                    "player.context",
                    ProtocolOperationVisibility.Both,
                    [],
                    Description: "Gets the current player context.",
                    Summary: "Gets player context.",
                    CliCommandPath: ["player", "context"],
                    McpToolName: "get_player_context"),
                new ProtocolOperationDescriptor(
                    "inventory.summary",
                    ProtocolOperationVisibility.Both,
                    [],
                    Description: "Gets the current inventory summary.",
                    Summary: "Gets inventory summary.",
                    CliCommandPath: ["inventory", "summary"],
                    McpToolName: "get_inventory_summary")
            ]));

        RemoteMcpToolService service = new(
            [
                new ProtocolOperationDescriptor(
                    "player.context",
                    ProtocolOperationVisibility.Both,
                    [],
                    Description: "Gets the current player context.",
                    Summary: "Gets player context.",
                    CliCommandPath: ["player", "context"],
                    McpToolName: "get_player_context")
            ],
            client,
            TimeSpan.FromMilliseconds(1));

        RequestContext<ListToolsRequestParams> requestContext = CreateRequestContext<ListToolsRequestParams>();
        await Task.Delay(50, TestContext.Current.CancellationToken);

        ListToolsResult result = await service.ListToolsAsync(requestContext, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Tools.Count);
        Assert.Contains(result.Tools, static tool => string.Equals(tool.Name, "get_inventory_summary", StringComparison.Ordinal));
    }

    private static RequestContext<TParams> CreateRequestContext<TParams>()
        where TParams : class
    {
        return (RequestContext<TParams>)RuntimeHelpers.GetUninitializedObject(typeof(RequestContext<TParams>));
    }

    private sealed class FakeProtocolOperationClient : IProtocolOperationClient
    {
        private readonly DescribeOperationsResponse response;

        public FakeProtocolOperationClient(DescribeOperationsResponse? response = null)
        {
            this.response = response ?? new DescribeOperationsResponse([]);
        }

        public ValueTask<ProtocolInvocationResult> InvokeAsync(
            string requestType,
            ProtocolRequestPayload request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<TResponse> InvokeAsync<TRequest, TResponse>(
            TRequest request,
            CancellationToken cancellationToken)
            where TRequest : class
        {
            throw new NotSupportedException();
        }

        public ValueTask<DescribeOperationsResponse> DescribeOperationsAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(response);
        }
    }
}
