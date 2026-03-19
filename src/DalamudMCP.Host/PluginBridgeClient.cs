using DalamudMCP.Contracts.Bridge;
using DalamudMCP.Contracts.Bridge.Requests;
using DalamudMCP.Contracts.Bridge.Responses;
using DalamudMCP.Infrastructure.Bridge;

namespace DalamudMCP.Host;

public sealed class PluginBridgeClient
{
    private readonly NamedPipeBridgeClient bridgeClient;

    public PluginBridgeClient(string pipeName)
        : this(new NamedPipeBridgeClient(pipeName))
    {
    }

    public PluginBridgeClient(NamedPipeBridgeClient bridgeClient)
    {
        this.bridgeClient = bridgeClient;
    }

    public Task<QueryResponse<SessionStateContract>> GetSessionStatusAsync(CancellationToken cancellationToken) =>
        SendQueryAsync<SessionStateContract>(BridgeRequestTypes.GetSessionStatus, new EmptyRequest(), cancellationToken);

    public Task<QueryResponse<SessionStateContract>> ReadSessionStatusResourceAsync(CancellationToken cancellationToken) =>
        SendQueryAsync<SessionStateContract>(BridgeRequestTypes.ReadSessionStatusResource, new EmptyRequest(), cancellationToken);

    public Task<QueryResponse<PlayerContextContract>> GetPlayerContextAsync(CancellationToken cancellationToken) =>
        SendQueryAsync<PlayerContextContract>(BridgeRequestTypes.GetPlayerContext, new EmptyRequest(), cancellationToken);

    public Task<QueryResponse<PlayerContextContract>> ReadPlayerContextResourceAsync(CancellationToken cancellationToken) =>
        SendQueryAsync<PlayerContextContract>(BridgeRequestTypes.ReadPlayerContextResource, new EmptyRequest(), cancellationToken);

    public Task<QueryResponse<DutyContextContract>> GetDutyContextAsync(CancellationToken cancellationToken) =>
        SendQueryAsync<DutyContextContract>(BridgeRequestTypes.GetDutyContext, new EmptyRequest(), cancellationToken);

    public Task<QueryResponse<DutyContextContract>> ReadDutyContextResourceAsync(CancellationToken cancellationToken) =>
        SendQueryAsync<DutyContextContract>(BridgeRequestTypes.ReadDutyContextResource, new EmptyRequest(), cancellationToken);

    public Task<QueryResponse<InventorySummaryContract>> GetInventorySummaryAsync(CancellationToken cancellationToken) =>
        SendQueryAsync<InventorySummaryContract>(BridgeRequestTypes.GetInventorySummary, new EmptyRequest(), cancellationToken);

    public Task<QueryResponse<InventorySummaryContract>> ReadInventorySummaryResourceAsync(CancellationToken cancellationToken) =>
        SendQueryAsync<InventorySummaryContract>(BridgeRequestTypes.ReadInventorySummaryResource, new EmptyRequest(), cancellationToken);

    public Task<QueryResponse<IReadOnlyList<AddonSummaryContract>>> GetAddonListAsync(CancellationToken cancellationToken) =>
        SendQueryAsync<IReadOnlyList<AddonSummaryContract>>(BridgeRequestTypes.GetAddonList, new EmptyRequest(), cancellationToken);

    public Task<QueryResponse<IReadOnlyList<AddonSummaryContract>>> ReadAddonListResourceAsync(CancellationToken cancellationToken) =>
        SendQueryAsync<IReadOnlyList<AddonSummaryContract>>(BridgeRequestTypes.ReadAddonListResource, new EmptyRequest(), cancellationToken);

    public Task<QueryResponse<AddonTreeContract>> GetAddonTreeAsync(string addonName, CancellationToken cancellationToken) =>
        SendQueryAsync<AddonTreeContract>(BridgeRequestTypes.GetAddonTree, new AddonRequest(addonName), cancellationToken);

    public Task<QueryResponse<AddonTreeContract>> ReadAddonTreeResourceAsync(string addonName, CancellationToken cancellationToken) =>
        SendQueryAsync<AddonTreeContract>(BridgeRequestTypes.ReadAddonTreeResource, new AddonRequest(addonName), cancellationToken);

    public Task<QueryResponse<StringTableContract>> GetAddonStringsAsync(string addonName, CancellationToken cancellationToken) =>
        SendQueryAsync<StringTableContract>(BridgeRequestTypes.GetAddonStrings, new AddonRequest(addonName), cancellationToken);

    public Task<QueryResponse<StringTableContract>> ReadAddonStringsResourceAsync(string addonName, CancellationToken cancellationToken) =>
        SendQueryAsync<StringTableContract>(BridgeRequestTypes.ReadAddonStringsResource, new AddonRequest(addonName), cancellationToken);

    public Task<QueryResponse<NearbyInteractablesContract>> GetNearbyInteractablesAsync(NearbyInteractablesRequest request, CancellationToken cancellationToken) =>
        SendQueryAsync<NearbyInteractablesContract>(BridgeRequestTypes.GetNearbyInteractables, request, cancellationToken);

    public Task<QueryResponse<TargetObjectResultContract>> TargetObjectAsync(TargetObjectRequest request, CancellationToken cancellationToken) =>
        SendQueryAsync<TargetObjectResultContract>(BridgeRequestTypes.TargetObject, request, cancellationToken);

    public Task<QueryResponse<InteractWithTargetResultContract>> InteractWithTargetAsync(InteractWithTargetRequest request, CancellationToken cancellationToken) =>
        SendQueryAsync<InteractWithTargetResultContract>(BridgeRequestTypes.InteractWithTarget, request, cancellationToken);

    public Task<QueryResponse<MoveToEntityResultContract>> MoveToEntityAsync(MoveToEntityRequest request, CancellationToken cancellationToken) =>
        SendQueryAsync<MoveToEntityResultContract>(BridgeRequestTypes.MoveToEntity, request, cancellationToken);

    public Task<QueryResponse<TeleportToAetheryteResultContract>> TeleportToAetheryteAsync(TeleportToAetheryteRequest request, CancellationToken cancellationToken) =>
        SendQueryAsync<TeleportToAetheryteResultContract>(BridgeRequestTypes.TeleportToAetheryte, request, cancellationToken);

    public Task<QueryResponse<AddonCallbackIntResultContract>> SendAddonCallbackIntAsync(AddonCallbackIntRequest request, CancellationToken cancellationToken) =>
        SendQueryAsync<AddonCallbackIntResultContract>(BridgeRequestTypes.SendAddonCallbackInt, request, cancellationToken);

    public Task<QueryResponse<AddonCallbackValuesResultContract>> SendAddonCallbackValuesAsync(AddonCallbackValuesRequest request, CancellationToken cancellationToken) =>
        SendQueryAsync<AddonCallbackValuesResultContract>(BridgeRequestTypes.SendAddonCallbackValues, request, cancellationToken);

    public Task<CapabilityStateResponse> GetCapabilityStateAsync(CancellationToken cancellationToken) =>
        SendPayloadAsync<CapabilityStateResponse>(BridgeRequestTypes.GetCapabilityState, new EmptyRequest(), BridgeResponseTypes.CapabilityState, cancellationToken);

    public Task RecordAuditEventAsync(string eventType, string summary, CancellationToken cancellationToken) =>
        SendCommandAsync(
            BridgeRequestTypes.RecordAuditEvent,
            new AuditEventRequest(eventType, summary),
            BridgeResponseTypes.Query,
            cancellationToken);

    public Task<BridgeResponseEnvelope> SendAsync(BridgeRequestEnvelope request, CancellationToken cancellationToken) =>
        bridgeClient.SendAsync(request, cancellationToken);

    private Task<QueryResponse<TPayload>> SendQueryAsync<TPayload>(string requestType, object payload, CancellationToken cancellationToken)
        where TPayload : class =>
        SendPayloadAsync<QueryResponse<TPayload>>(requestType, payload, BridgeResponseTypes.Query, cancellationToken);

    private async Task<TPayload> SendPayloadAsync<TPayload>(
        string requestType,
        object payload,
        string expectedResponseType,
        CancellationToken cancellationToken)
        where TPayload : class
    {
        BridgeResponseEnvelope response;
        try
        {
            response = await bridgeClient.SendAsync(
                new BridgeRequestEnvelope(
                    ContractVersion.Current,
                    requestType,
                    Guid.NewGuid().ToString("N"),
                    payload),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is TimeoutException or IOException && exception is not OperationCanceledException)
        {
            throw new InvalidOperationException("Plugin bridge is unavailable.", exception);
        }

        if (!response.Success)
        {
            throw new InvalidOperationException(response.ErrorMessage ?? response.ErrorCode ?? "Bridge request failed.");
        }

        ContractVersion.EnsureCompatible(response.ContractVersion, nameof(response));

        if (!string.Equals(response.ResponseType, expectedResponseType, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unexpected response type '{response.ResponseType}'.");
        }

        var typedPayload = BridgeJson.DeserializePayload<TPayload>(response.Payload);
        return typedPayload ?? throw new InvalidOperationException("Bridge payload could not be deserialized.");
    }

    private async Task SendCommandAsync(
        string requestType,
        object payload,
        string expectedResponseType,
        CancellationToken cancellationToken)
    {
        var response = await SendEnvelopeAsync(requestType, payload, cancellationToken).ConfigureAwait(false);
        if (!response.Success)
        {
            throw new InvalidOperationException(response.ErrorMessage ?? response.ErrorCode ?? "Bridge request failed.");
        }

        ContractVersion.EnsureCompatible(response.ContractVersion, nameof(response));

        if (!string.Equals(response.ResponseType, expectedResponseType, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unexpected response type '{response.ResponseType}'.");
        }
    }

    private async Task<BridgeResponseEnvelope> SendEnvelopeAsync(
        string requestType,
        object payload,
        CancellationToken cancellationToken)
    {
        try
        {
            return await bridgeClient.SendAsync(
                new BridgeRequestEnvelope(
                    ContractVersion.Current,
                    requestType,
                    Guid.NewGuid().ToString("N"),
                    payload),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is TimeoutException or IOException && exception is not OperationCanceledException)
        {
            throw new InvalidOperationException("Plugin bridge is unavailable.", exception);
        }
    }
}
