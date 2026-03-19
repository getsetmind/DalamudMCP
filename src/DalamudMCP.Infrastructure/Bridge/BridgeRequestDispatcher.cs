using DalamudMCP.Application.UseCases.Observation;
using DalamudMCP.Application.UseCases.Settings;
using DalamudMCP.Contracts.Bridge;
using DalamudMCP.Contracts.Bridge.Requests;
using DalamudMCP.Domain.Audit;

namespace DalamudMCP.Infrastructure.Bridge;

public sealed class BridgeRequestDispatcher
{
    private readonly GetSessionStatusUseCase getSessionStatusUseCase;
    private readonly GetPlayerContextUseCase getPlayerContextUseCase;
    private readonly GetDutyContextUseCase getDutyContextUseCase;
    private readonly GetInventorySummaryUseCase getInventorySummaryUseCase;
    private readonly GetAddonListUseCase getAddonListUseCase;
    private readonly GetAddonTreeUseCase getAddonTreeUseCase;
    private readonly GetAddonStringsUseCase getAddonStringsUseCase;
    private readonly GetCurrentSettingsUseCase getCurrentSettingsUseCase;
    private readonly RecordAuditEventUseCase recordAuditEventUseCase;

    public BridgeRequestDispatcher(
        GetSessionStatusUseCase getSessionStatusUseCase,
        GetPlayerContextUseCase getPlayerContextUseCase,
        GetDutyContextUseCase getDutyContextUseCase,
        GetInventorySummaryUseCase getInventorySummaryUseCase,
        GetAddonListUseCase getAddonListUseCase,
        GetAddonTreeUseCase getAddonTreeUseCase,
        GetAddonStringsUseCase getAddonStringsUseCase,
        GetCurrentSettingsUseCase getCurrentSettingsUseCase,
        RecordAuditEventUseCase recordAuditEventUseCase)
    {
        this.getSessionStatusUseCase = getSessionStatusUseCase;
        this.getPlayerContextUseCase = getPlayerContextUseCase;
        this.getDutyContextUseCase = getDutyContextUseCase;
        this.getInventorySummaryUseCase = getInventorySummaryUseCase;
        this.getAddonListUseCase = getAddonListUseCase;
        this.getAddonTreeUseCase = getAddonTreeUseCase;
        this.getAddonStringsUseCase = getAddonStringsUseCase;
        this.getCurrentSettingsUseCase = getCurrentSettingsUseCase;
        this.recordAuditEventUseCase = recordAuditEventUseCase;
    }

    public async Task<BridgeResponseEnvelope> DispatchAsync(BridgeRequestEnvelope request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        BridgeTrace.Write($"dispatcher.enter type={request.RequestType} requestId={request.RequestId}");

        if (!ContractVersion.IsCompatible(request.ContractVersion))
        {
            BridgeTrace.Write($"dispatcher.invalid_contract type={request.RequestType} requestId={request.RequestId}");
            return CreateErrorResponse(request.RequestId, "invalid_contract_version", "Unsupported contract version.");
        }

        try
        {
            var response = request.RequestType switch
            {
                BridgeRequestTypes.RecordAuditEvent => await RecordAuditEventAsync(request, cancellationToken).ConfigureAwait(false),
                BridgeRequestTypes.GetSessionStatus => CreateQueryResponse(
                    request.RequestId,
                    BridgeContractMapper.ToResponse(await getSessionStatusUseCase.ExecuteForToolAsync(cancellationToken).ConfigureAwait(false))),
                BridgeRequestTypes.ReadSessionStatusResource => CreateQueryResponse(
                    request.RequestId,
                    BridgeContractMapper.ToResponse(await getSessionStatusUseCase.ExecuteForResourceAsync(cancellationToken).ConfigureAwait(false))),
                BridgeRequestTypes.GetPlayerContext => CreateQueryResponse(
                    request.RequestId,
                    BridgeContractMapper.ToResponse(await getPlayerContextUseCase.ExecuteForToolAsync(cancellationToken).ConfigureAwait(false))),
                BridgeRequestTypes.ReadPlayerContextResource => CreateQueryResponse(
                    request.RequestId,
                    BridgeContractMapper.ToResponse(await getPlayerContextUseCase.ExecuteForResourceAsync(cancellationToken).ConfigureAwait(false))),
                BridgeRequestTypes.GetDutyContext => CreateQueryResponse(
                    request.RequestId,
                    BridgeContractMapper.ToResponse(await getDutyContextUseCase.ExecuteForToolAsync(cancellationToken).ConfigureAwait(false))),
                BridgeRequestTypes.ReadDutyContextResource => CreateQueryResponse(
                    request.RequestId,
                    BridgeContractMapper.ToResponse(await getDutyContextUseCase.ExecuteForResourceAsync(cancellationToken).ConfigureAwait(false))),
                BridgeRequestTypes.GetInventorySummary => CreateQueryResponse(
                    request.RequestId,
                    BridgeContractMapper.ToResponse(await getInventorySummaryUseCase.ExecuteForToolAsync(cancellationToken).ConfigureAwait(false))),
                BridgeRequestTypes.ReadInventorySummaryResource => CreateQueryResponse(
                    request.RequestId,
                    BridgeContractMapper.ToResponse(await getInventorySummaryUseCase.ExecuteForResourceAsync(cancellationToken).ConfigureAwait(false))),
                BridgeRequestTypes.GetAddonList => CreateQueryResponse(
                    request.RequestId,
                    BridgeContractMapper.ToResponse(await getAddonListUseCase.ExecuteForToolAsync(cancellationToken).ConfigureAwait(false))),
                BridgeRequestTypes.ReadAddonListResource => CreateQueryResponse(
                    request.RequestId,
                    BridgeContractMapper.ToResponse(await getAddonListUseCase.ExecuteForResourceAsync(cancellationToken).ConfigureAwait(false))),
                BridgeRequestTypes.GetAddonTree => CreateQueryResponse(
                    request.RequestId,
                    BridgeContractMapper.ToResponse(await getAddonTreeUseCase.ExecuteForToolAsync(ReadAddonName(request.Payload), cancellationToken).ConfigureAwait(false))),
                BridgeRequestTypes.ReadAddonTreeResource => CreateQueryResponse(
                    request.RequestId,
                    BridgeContractMapper.ToResponse(await getAddonTreeUseCase.ExecuteForResourceAsync(ReadAddonName(request.Payload), cancellationToken).ConfigureAwait(false))),
                BridgeRequestTypes.GetAddonStrings => CreateQueryResponse(
                    request.RequestId,
                    BridgeContractMapper.ToResponse(await getAddonStringsUseCase.ExecuteForToolAsync(ReadAddonName(request.Payload), cancellationToken).ConfigureAwait(false))),
                BridgeRequestTypes.ReadAddonStringsResource => CreateQueryResponse(
                    request.RequestId,
                    BridgeContractMapper.ToResponse(await getAddonStringsUseCase.ExecuteForResourceAsync(ReadAddonName(request.Payload), cancellationToken).ConfigureAwait(false))),
                BridgeRequestTypes.GetCapabilityState => CreateCapabilityStateResponse(
                    request.RequestId,
                    BridgeContractMapper.ToResponse(await getCurrentSettingsUseCase.ExecuteAsync(cancellationToken).ConfigureAwait(false))),
                _ => CreateErrorResponse(request.RequestId, "unsupported_request_type", $"Unsupported request type '{request.RequestType}'."),
            };
            BridgeTrace.Write($"dispatcher.exit type={request.RequestType} requestId={request.RequestId} success={response.Success}");
            return response;
        }
        catch (ArgumentException exception)
        {
            BridgeTrace.Write($"dispatcher.invalid_payload type={request.RequestType} requestId={request.RequestId} message={exception.Message}");
            return CreateErrorResponse(request.RequestId, "invalid_payload", exception.Message);
        }
        catch (Exception exception)
        {
            BridgeTrace.Write($"dispatcher.exception type={request.RequestType} requestId={request.RequestId} exception={exception.GetType().Name} message={exception.Message}");
            throw;
        }
    }

    private async Task<BridgeResponseEnvelope> RecordAuditEventAsync(BridgeRequestEnvelope request, CancellationToken cancellationToken)
    {
        var payload = BridgeJson.DeserializePayload<AuditEventRequest>(request.Payload);
        if (payload is null || string.IsNullOrWhiteSpace(payload.EventType) || string.IsNullOrWhiteSpace(payload.Summary))
        {
            throw new ArgumentException("Audit event payload is required.", nameof(request));
        }

        await recordAuditEventUseCase.ExecuteAsync(
            new AuditEvent(DateTimeOffset.UtcNow, payload.EventType.Trim(), payload.Summary.Trim()),
            cancellationToken).ConfigureAwait(false);

        return CreateQueryResponse(request.RequestId, new EmptyRequest());
    }

    private static string ReadAddonName(object? payload)
    {
        var request = BridgeJson.DeserializePayload<AddonRequest>(payload);
        if (request is null || string.IsNullOrWhiteSpace(request.AddonName))
        {
            throw new ArgumentException("AddonName is required.", nameof(payload));
        }

        return request.AddonName;
    }

    private static BridgeResponseEnvelope CreateQueryResponse<TPayload>(string requestId, TPayload payload) =>
        new(
            ContractVersion.Current,
            requestId,
            BridgeResponseTypes.Query,
            Success: true,
            ErrorCode: null,
            ErrorMessage: null,
            Payload: payload);

    private static BridgeResponseEnvelope CreateCapabilityStateResponse(string requestId, object payload) =>
        new(
            ContractVersion.Current,
            requestId,
            BridgeResponseTypes.CapabilityState,
            Success: true,
            ErrorCode: null,
            ErrorMessage: null,
            Payload: payload);

    private static BridgeResponseEnvelope CreateErrorResponse(string requestId, string errorCode, string errorMessage) =>
        new(
            ContractVersion.Current,
            requestId,
            BridgeResponseTypes.Error,
            Success: false,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage,
            Payload: null);
}
