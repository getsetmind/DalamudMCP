using DalamudMCP.Plugin.Hosting;
using Manifold;

namespace DalamudMCP.Plugin.Tests;

public sealed class CapabilityApprovalServiceTests
{
    [Fact]
    public void Approved_request_is_consumed_once_for_the_exact_arguments()
    {
        DateTimeOffset now = new(2026, 8, 9, 0, 0, 0, TimeSpan.Zero);
        CapabilityApprovalService service = new(() => now);
        OperationDescriptor operation = CreateDescriptor();
        CapabilityAuthorizationDecision confirmation =
            CapabilityAuthorizationDecision.ConfirmationRequired("game.action.execute", null, 12);

        CapabilityAuthorizationDecision pending = service.AuthorizeOrQueue(
            operation,
            new TestRequest(42, "target-a"),
            confirmation);

        Assert.True(pending.RequiresConfirmation);
        Assert.NotNull(pending.ApprovalId);
        Assert.True(service.Approve(pending.ApprovalId));

        CapabilityAuthorizationDecision approved = service.AuthorizeOrQueue(
            operation,
            new TestRequest(42, "target-a"),
            confirmation);
        CapabilityAuthorizationDecision nextAttempt = service.AuthorizeOrQueue(
            operation,
            new TestRequest(42, "target-a"),
            confirmation);

        Assert.True(approved.IsAllowed);
        Assert.Equal(pending.ApprovalId, approved.ApprovalId);
        Assert.Equal(12, approved.MaximumCallsPerMinute);
        Assert.True(nextAttempt.RequiresConfirmation);
        Assert.NotEqual(pending.ApprovalId, nextAttempt.ApprovalId);
    }

    [Fact]
    public void Approval_cannot_be_reused_with_different_arguments()
    {
        CapabilityApprovalService service = new();
        OperationDescriptor operation = CreateDescriptor();
        CapabilityAuthorizationDecision confirmation =
            CapabilityAuthorizationDecision.ConfirmationRequired("game.action.execute", null);
        CapabilityAuthorizationDecision original = service.AuthorizeOrQueue(
            operation,
            new TestRequest(42, "target-a"),
            confirmation);
        Assert.True(service.Approve(original.ApprovalId!));

        CapabilityAuthorizationDecision different = service.AuthorizeOrQueue(
            operation,
            new TestRequest(43, "target-a"),
            confirmation);
        CapabilityAuthorizationDecision approved = service.AuthorizeOrQueue(
            operation,
            new TestRequest(42, "target-a"),
            confirmation);

        Assert.True(different.RequiresConfirmation);
        Assert.NotEqual(original.ApprovalId, different.ApprovalId);
        Assert.True(approved.IsAllowed);
        Assert.Equal(original.ApprovalId, approved.ApprovalId);
    }

    [Fact]
    public void Denial_and_expiry_are_enforced()
    {
        DateTimeOffset now = new(2026, 8, 9, 0, 0, 0, TimeSpan.Zero);
        CapabilityApprovalService service = new(() => now);
        OperationDescriptor operation = CreateDescriptor();
        CapabilityAuthorizationDecision confirmation =
            CapabilityAuthorizationDecision.ConfirmationRequired("game.action.execute", null);
        CapabilityAuthorizationDecision pending = service.AuthorizeOrQueue(
            operation,
            new TestRequest(42, "target-a"),
            confirmation);
        Assert.True(service.Deny(pending.ApprovalId!));

        CapabilityAuthorizationDecision denied = service.AuthorizeOrQueue(
            operation,
            new TestRequest(42, "target-a"),
            confirmation);

        Assert.False(denied.IsAllowed);
        Assert.False(denied.RequiresConfirmation);
        Assert.Equal(pending.ApprovalId, denied.ApprovalId);

        CapabilityAuthorizationDecision expiring = service.AuthorizeOrQueue(
            operation,
            new TestRequest(43, "target-b"),
            confirmation);
        now = now.AddMinutes(6);

        Assert.Empty(service.GetPending());
        Assert.False(service.Approve(expiring.ApprovalId!));
    }

    [Fact]
    public void Rate_limiter_is_scoped_by_capability_and_target()
    {
        DateTimeOffset now = new(2026, 8, 9, 0, 0, 0, TimeSpan.Zero);
        CapabilityRateLimiter limiter = new(() => now);

        Assert.True(limiter.TryAcquire("game.action.execute", new TestActionTarget("Action"), 2, out _));
        Assert.True(limiter.TryAcquire("game.action.execute", new TestActionTarget("Action"), 2, out _));
        Assert.False(limiter.TryAcquire("game.action.execute", new TestActionTarget("Action"), 2, out TimeSpan retryAfter));
        Assert.Equal(TimeSpan.FromMinutes(1), retryAfter);
        Assert.True(limiter.TryAcquire("game.action.execute", new TestActionTarget("Item"), 2, out _));

        now = now.AddMinutes(1).AddTicks(1);
        Assert.True(limiter.TryAcquire("game.action.execute", new TestActionTarget("Action"), 2, out _));
    }

    private static OperationDescriptor CreateDescriptor() =>
        new(
            "game.action.execute",
            typeof(object),
            "ExecuteAsync",
            typeof(object),
            OperationVisibility.Both,
            []);

    private sealed record TestRequest(int ActionId, string Target);

    private sealed record TestActionTarget(string ActionType);
}
