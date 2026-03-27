namespace DalamudMCP.Protocol;

public interface IProtocolOperationClient
{
    public ValueTask<ProtocolInvocationResult> InvokeAsync(
        string requestType,
        ProtocolRequestPayload request,
        CancellationToken cancellationToken);

    public ValueTask<TResponse> InvokeAsync<TRequest, TResponse>(
        TRequest request,
        CancellationToken cancellationToken)
        where TRequest : class;

    public ValueTask<DescribeOperationsResponse> DescribeOperationsAsync(CancellationToken cancellationToken);
}
