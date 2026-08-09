using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Manifold;

namespace DalamudMCP.Plugin.Hosting;

public sealed class OperationAuditLog : IDisposable
{
    private static readonly HashSet<string> IncludedArgumentNames = new(StringComparer.Ordinal)
    {
        "Action",
        "ActionId",
        "ActionType",
        "PackageSha256",
        "SourceUrl",
        "Callgate",
        "DryRun",
        "PluginName",
        "RowId",
        "Sheet",
        "TargetObjectId",
    };
    private static readonly HashSet<string> IncludedResultNames = new(StringComparer.Ordinal)
    {
        "CurrentState",
        "DryRun",
        "ErrorMessage",
        "PreviousState",
        "Reason",
        "Repository",
        "Status",
        "Version",
    };

    private readonly string path;
    private readonly SemaphoreSlim writeLock = new(1, 1);

    public OperationAuditLog(PluginRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        path = Path.Combine(options.WorkingDirectory, "audit", "operations.jsonl");
    }

    public async ValueTask WriteAsync(
        OperationDescriptor operation,
        string requestId,
        object? request,
        object? result,
        CapabilityAuthorizationDecision authorization,
        bool succeeded,
        string? errorCode,
        long elapsedMilliseconds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(authorization);

        if (PluginOperationMetadataCatalog.Resolve(operation.OperationId).Effect is Protocol.ProtocolOperationEffect.ReadOnly &&
            authorization.IsAllowed)
        {
            return;
        }

        var entry = new
        {
            timestampUtc = DateTimeOffset.UtcNow,
            requestId,
            operationId = operation.OperationId,
            permissionScope = authorization.PermissionScope,
            authorization = authorization.IsAllowed
                ? "allow"
                : authorization.RequiresConfirmation ? "confirm" : "deny",
            authorizationReason = authorization.Reason,
            approvalId = authorization.ApprovalId,
            arguments = CreateArgumentSummary(request),
            result = CreateResultSummary(result),
            succeeded,
            errorCode,
            elapsedMilliseconds,
        };

        try
        {
            await writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.AppendAllTextAsync(
                    path,
                    JsonSerializer.Serialize(entry) + Environment.NewLine,
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                writeLock.Release();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"DalamudMCP audit log write failed: {exception.Message}");
        }
    }

    public void Dispose()
    {
        writeLock.Dispose();
        GC.SuppressFinalize(this);
    }

    private static Dictionary<string, object?> CreateArgumentSummary(object? request)
    {
        if (request is null)
            return [];

        return request.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => IncludedArgumentNames.Contains(property.Name) && property.GetIndexParameters().Length == 0)
            .ToDictionary(
                static property => property.Name,
                property => property.GetValue(request),
                StringComparer.Ordinal);
    }

    private static Dictionary<string, object?> CreateResultSummary(object? result)
    {
        if (result is null)
            return [];

        return result.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => IncludedResultNames.Contains(property.Name) && property.GetIndexParameters().Length == 0)
            .ToDictionary(
                static property => property.Name,
                property => property.GetValue(result),
                StringComparer.Ordinal);
    }
}
