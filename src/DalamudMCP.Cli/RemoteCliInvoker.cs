using System.Text.Json;
using DalamudMCP.Framework.Cli;
using DalamudMCP.Protocol;

namespace DalamudMCP.Cli;

public sealed class RemoteCliInvoker : ICliInvoker
{
    private readonly Dictionary<string, ProtocolOperationDescriptor> operationsById;
    private readonly IProtocolOperationClient protocolClient;

    public RemoteCliInvoker(
        IReadOnlyList<ProtocolOperationDescriptor> operations,
        IProtocolOperationClient protocolClient)
    {
        ArgumentNullException.ThrowIfNull(operations);
        this.protocolClient = protocolClient ?? throw new ArgumentNullException(nameof(protocolClient));

        operationsById = operations.ToDictionary(
            static operation => operation.OperationId,
            StringComparer.Ordinal);
    }

    public bool TryInvoke(
        string operationId,
        IReadOnlyDictionary<string, string> options,
        IReadOnlyList<string> arguments,
        IServiceProvider? services,
        bool jsonRequested,
        CancellationToken cancellationToken,
        out ValueTask<CliInvocationResult> invocation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(arguments);

        if (!operationsById.TryGetValue(operationId, out ProtocolOperationDescriptor? operation))
        {
            invocation = default;
            return false;
        }

        invocation = InvokeAsync(operation, options, arguments, jsonRequested, cancellationToken);
        return true;
    }

    private async ValueTask<CliInvocationResult> InvokeAsync(
        ProtocolOperationDescriptor operation,
        IReadOnlyDictionary<string, string> options,
        IReadOnlyList<string> arguments,
        bool jsonRequested,
        CancellationToken cancellationToken)
    {
        ProtocolRequestPayload payload = ProtocolOperationRequestFactory.CreateFromCli(operation, options, arguments);
        ProtocolInvocationResult result = await protocolClient.InvokeAsync(
                operation.OperationId,
                payload,
                cancellationToken)
            .ConfigureAwait(false);

        byte[]? rawJsonPayload = result.PayloadFormat == ProtocolPayloadFormat.Json
            ? result.Payload
            : null;
        if (!string.IsNullOrWhiteSpace(result.DisplayText))
            return new CliInvocationResult(null, typeof(object), result.DisplayText, rawJsonPayload);

        if (jsonRequested && rawJsonPayload is { Length: > 0 })
            return new CliInvocationResult(null, typeof(object), null, rawJsonPayload);

        object? normalizedPayload = result.Payload is null
            ? null
            : ProtocolContract.DeserializePayloadElement(result.PayloadFormat, result.Payload);
        return new CliInvocationResult(
            normalizedPayload,
            typeof(JsonElement),
            CliBinding.FormatDefaultText(normalizedPayload),
            rawJsonPayload);
    }
}
