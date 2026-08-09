using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DalamudMCP.Protocol;
using Manifold;

namespace DalamudMCP.Plugin.Hosting;

public sealed class CapabilityApprovalService
{
    private const int MaximumPendingApprovals = 32;
    private static readonly TimeSpan ApprovalLifetime = TimeSpan.FromMinutes(5);
    private readonly Func<DateTimeOffset> getUtcNow;
    private readonly object syncRoot = new();
    private readonly List<ApprovalEntry> entries = [];

    public CapabilityApprovalService()
        : this(static () => DateTimeOffset.UtcNow)
    {
    }

    internal CapabilityApprovalService(Func<DateTimeOffset> getUtcNow)
    {
        this.getUtcNow = getUtcNow ?? throw new ArgumentNullException(nameof(getUtcNow));
    }

    public CapabilityAuthorizationDecision AuthorizeOrQueue(
        OperationDescriptor operation,
        object? request,
        CapabilityAuthorizationDecision decision)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(decision);
        if (!decision.RequiresConfirmation)
            return decision;

        string requestJson = JsonSerializer.Serialize(request, request?.GetType() ?? typeof(object), ProtocolContract.JsonOptions);
        string fingerprint = CreateFingerprint(operation.OperationId, decision.PermissionScope, requestJson);
        DateTimeOffset now = getUtcNow();

        lock (syncRoot)
        {
            RemoveExpired(now);
            ApprovalEntry? existing = entries.FirstOrDefault(entry =>
                string.Equals(entry.Fingerprint, fingerprint, StringComparison.Ordinal));
            if (existing is not null)
            {
                if (existing.State is CapabilityApprovalState.Approved)
                {
                    entries.Remove(existing);
                    return CapabilityAuthorizationDecision.Allowed(
                        decision.PermissionScope,
                        decision.MaximumCallsPerMinute,
                        existing.Id,
                        "A one-time approval was consumed for this exact request.");
                }

                if (existing.State is CapabilityApprovalState.Denied)
                {
                    entries.Remove(existing);
                    return CapabilityAuthorizationDecision.Denied(
                        decision.PermissionScope,
                        "The pending request was denied in the plugin UI.",
                        existing.Id);
                }

                return CapabilityAuthorizationDecision.ConfirmationRequired(
                    decision.PermissionScope,
                    existing.Id,
                    decision.MaximumCallsPerMinute);
            }

            if (entries.Count >= MaximumPendingApprovals)
                entries.Remove(entries.OrderBy(static entry => entry.CreatedAtUtc).First());

            string id = Guid.NewGuid().ToString("N")[..12];
            entries.Add(new ApprovalEntry(
                id,
                fingerprint,
                operation.OperationId,
                decision.PermissionScope,
                CreateDisplayJson(requestJson),
                now,
                now.Add(ApprovalLifetime),
                CapabilityApprovalState.Pending));
            return CapabilityAuthorizationDecision.ConfirmationRequired(
                decision.PermissionScope,
                id,
                decision.MaximumCallsPerMinute);
        }
    }

    public IReadOnlyList<CapabilityApprovalRequest> GetPending()
    {
        DateTimeOffset now = getUtcNow();
        lock (syncRoot)
        {
            RemoveExpired(now);
            return entries
                .Where(static entry => entry.State is CapabilityApprovalState.Pending)
                .OrderBy(static entry => entry.CreatedAtUtc)
                .Select(static entry => new CapabilityApprovalRequest(
                    entry.Id,
                    entry.OperationId,
                    entry.PermissionScope,
                    entry.ArgumentsJson,
                    entry.CreatedAtUtc,
                    entry.ExpiresAtUtc))
                .ToArray();
        }
    }

    public bool Approve(string approvalId) => SetState(approvalId, CapabilityApprovalState.Approved);

    public bool Deny(string approvalId) => SetState(approvalId, CapabilityApprovalState.Denied);

    private static string CreateFingerprint(string operationId, string permissionScope, string requestJson)
    {
        byte[] bytes = Encoding.UTF8.GetBytes($"{operationId}\n{permissionScope}\n{requestJson}");
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static string CreateDisplayJson(string requestJson)
    {
        const int maximumLength = 4096;
        return requestJson.Length <= maximumLength
            ? requestJson
            : requestJson[..maximumLength] + "…";
    }

    private bool SetState(string approvalId, CapabilityApprovalState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approvalId);
        DateTimeOffset now = getUtcNow();
        lock (syncRoot)
        {
            RemoveExpired(now);
            ApprovalEntry? entry = entries.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, approvalId, StringComparison.Ordinal));
            if (entry is null || entry.State is not CapabilityApprovalState.Pending)
                return false;

            entry.State = state;
            return true;
        }
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        entries.RemoveAll(entry => entry.ExpiresAtUtc <= now);
    }

    private sealed record ApprovalEntry(
        string Id,
        string Fingerprint,
        string OperationId,
        string PermissionScope,
        string ArgumentsJson,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset ExpiresAtUtc,
        CapabilityApprovalState InitialState)
    {
        public CapabilityApprovalState State { get; set; } = InitialState;
    }
}

public sealed record CapabilityApprovalRequest(
    string Id,
    string OperationId,
    string PermissionScope,
    string ArgumentsJson,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc);

public enum CapabilityApprovalState
{
    Pending = 0,
    Approved = 1,
    Denied = 2,
}

public sealed class CapabilityRateLimiter
{
    private readonly Func<DateTimeOffset> getUtcNow;
    private readonly object syncRoot = new();
    private readonly Dictionary<string, Queue<DateTimeOffset>> callsByKey = new(StringComparer.Ordinal);

    public CapabilityRateLimiter()
        : this(static () => DateTimeOffset.UtcNow)
    {
    }

    internal CapabilityRateLimiter(Func<DateTimeOffset> getUtcNow)
    {
        this.getUtcNow = getUtcNow ?? throw new ArgumentNullException(nameof(getUtcNow));
    }

    public bool TryAcquire(
        string permissionScope,
        object? request,
        int maximumCallsPerMinute,
        out TimeSpan retryAfter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionScope);
        retryAfter = TimeSpan.Zero;
        if (maximumCallsPerMinute <= 0)
            return true;

        string target = ReadTarget(request);
        string key = $"{permissionScope}\n{target}";
        DateTimeOffset now = getUtcNow();
        DateTimeOffset cutoff = now.AddMinutes(-1);
        lock (syncRoot)
        {
            if (!callsByKey.TryGetValue(key, out Queue<DateTimeOffset>? calls))
            {
                calls = new Queue<DateTimeOffset>();
                callsByKey[key] = calls;
            }

            while (calls.TryPeek(out DateTimeOffset timestamp) && timestamp <= cutoff)
                calls.Dequeue();

            if (calls.Count >= maximumCallsPerMinute)
            {
                retryAfter = calls.Peek().AddMinutes(1) - now;
                return false;
            }

            calls.Enqueue(now);
            return true;
        }
    }

    private static string ReadTarget(object? request)
    {
        if (request is null)
            return "*";

        foreach (string propertyName in new[]
                 {
                     "PluginName",
                     "ActionType",
                     "ActionId",
                     "Sheet",
                     "Callgate",
                     "TargetObjectId",
                     "GameObjectId",
                     "ExpectedGameObjectId",
                 })
        {
            PropertyInfo? property = request.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            string? value = property?.GetValue(request)?.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return "*";
    }
}
