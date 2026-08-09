using DalamudMCP.Protocol;

namespace DalamudMCP.Plugin.Hosting;

internal static class PluginOperationMetadataCatalog
{
    public static ProtocolOperationMetadata Resolve(string operationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);

        if (operationId.StartsWith("plugin.package.", StringComparison.Ordinal))
            return Mutation("plugin.package", destructive: true, openWorld: true, supportsDryRun: true);

        if (operationId.StartsWith("plugin.lifecycle.", StringComparison.Ordinal) ||
            string.Equals(operationId, "plugin.reload", StringComparison.Ordinal))
        {
            return Mutation("plugin.lifecycle", destructive: true, openWorld: true, supportsDryRun: true);
        }

        if (operationId.StartsWith("plugin.inspect.", StringComparison.Ordinal))
            return ReadOnly("plugin.inspect", openWorld: true);

        if (operationId.Contains("plugin-ipc", StringComparison.Ordinal) ||
            string.Equals(operationId, "plugin.ipc", StringComparison.Ordinal))
        {
            return Mutation("plugin.ipc.invoke", openWorld: true);
        }

        if (operationId.StartsWith("plugin.data.", StringComparison.Ordinal))
            return Mutation("plugin.data", openWorld: true);

        if (operationId.StartsWith("game-data.", StringComparison.Ordinal))
            return ReadOnly("game.data.read");

        if (string.Equals(operationId, "game.action.resolve", StringComparison.Ordinal))
            return ReadOnly("game.action.read", requiresFrameworkThread: true);

        if (string.Equals(operationId, "chat.read", StringComparison.Ordinal))
            return ReadOnly("chat.read");

        if (string.Equals(operationId, "events.configure", StringComparison.Ordinal))
            return Mutation("events.configure");

        if (operationId.StartsWith("events.", StringComparison.Ordinal))
            return ReadOnly("events.read");

        if (string.Equals(operationId, "game.screenshot", StringComparison.Ordinal))
            return Mutation("screenshot.capture");

        if (string.Equals(operationId, "command.slash", StringComparison.Ordinal))
            return Mutation("command.execute", openWorld: true);

        if (operationId.StartsWith("addon.", StringComparison.Ordinal))
        {
            return operationId is "addon.list" or "addon.tree" or "addon.strings"
                ? ReadOnly("ui.read")
                : Mutation("ui.interact", requiresFrameworkThread: true);
        }

        if (PluginOperationExposurePolicy.IsActionOperation(operationId) ||
            string.Equals(operationId, "game.action.execute", StringComparison.Ordinal))
        {
            return Mutation(
                "game.action.execute",
                supportsDryRun: string.Equals(operationId, "game.action.execute", StringComparison.Ordinal),
                requiresFrameworkThread: true);
        }

        return ReadOnly("game.read");
    }

    private static ProtocolOperationMetadata ReadOnly(
        string permissionScope,
        bool openWorld = false,
        bool requiresFrameworkThread = false)
    {
        return new ProtocolOperationMetadata(
            ProtocolOperationEffect.ReadOnly,
            permissionScope,
            Idempotent: true,
            OpenWorld: openWorld,
            SupportsDryRun: false,
            RequiresFrameworkThread: requiresFrameworkThread);
    }

    private static ProtocolOperationMetadata Mutation(
        string permissionScope,
        bool destructive = false,
        bool openWorld = false,
        bool supportsDryRun = false,
        bool requiresFrameworkThread = false)
    {
        return new ProtocolOperationMetadata(
            destructive ? ProtocolOperationEffect.Destructive : ProtocolOperationEffect.Mutation,
            permissionScope,
            Idempotent: false,
            OpenWorld: openWorld,
            SupportsDryRun: supportsDryRun,
            RequiresFrameworkThread: requiresFrameworkThread);
    }
}

internal sealed record ProtocolOperationMetadata(
    ProtocolOperationEffect Effect,
    string PermissionScope,
    bool Idempotent,
    bool OpenWorld,
    bool SupportsDryRun,
    bool RequiresFrameworkThread);
