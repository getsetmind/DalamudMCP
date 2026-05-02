using System.Globalization;
using System.Runtime.Versioning;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Manifold;
using DalamudMCP.Protocol;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "target.object",
    Description = "Targets a specific entity by game object id.",
    Summary = "Targets an object.")]
[ResultFormatter(typeof(TargetObjectOperation.TextFormatter))]
[CliCommand("target", "object")]
[McpTool("target_object")]
public sealed partial class TargetObjectOperation : IOperation<TargetObjectOperation.Request, TargetObjectResult>
{
    private readonly Func<Request, CancellationToken, ValueTask<TargetObjectResult>> executor;

    [SupportedOSPlatform("windows")]
    public TargetObjectOperation(
        IFramework framework,
        IObjectTable objectTable,
        ITargetManager targetManager)
    {
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(objectTable);
        ArgumentNullException.ThrowIfNull(targetManager);

        executor = CreateDalamudExecutor(framework, objectTable, targetManager);
    }

    internal TargetObjectOperation(Func<Request, CancellationToken, ValueTask<TargetObjectResult>> executor)
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public ValueTask<TargetObjectResult> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return executor(request, context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("target.object")]
    [LegacyBridgeRequest("TargetObject")]
    public sealed partial class Request
    {
        [Option("game-object-id", Description = "Game object id to target.")]
        public string GameObjectId { get; init; } = string.Empty;
    }

    public sealed class TextFormatter : IResultFormatter<TargetObjectResult>
    {
        public string? FormatText(TargetObjectResult result, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            return result.SummaryText;
        }
    }

    [SupportedOSPlatform("windows")]
    private static Func<Request, CancellationToken, ValueTask<TargetObjectResult>> CreateDalamudExecutor(
        IFramework framework,
        IObjectTable objectTable,
        ITargetManager targetManager)
    {
        return async (request, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            string gameObjectId = NormalizeGameObjectId(request.GameObjectId);

            if (framework.IsInFrameworkUpdateThread)
                return TargetCore(objectTable, targetManager, gameObjectId, cancellationToken);

            return await framework.RunOnFrameworkThread(() => TargetCore(objectTable, targetManager, gameObjectId, cancellationToken))
                .ConfigureAwait(false);
        };
    }

    private static TargetObjectResult TargetCore(
        IObjectTable objectTable,
        ITargetManager targetManager,
        string gameObjectId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryParseGameObjectId(gameObjectId, out ulong parsedId))
        {
            return new TargetObjectResult(
                gameObjectId,
                false,
                "invalid_object_id",
                null,
                null,
                null,
                $"'{gameObjectId}' is not a valid game object id.");
        }

        IGameObject? gameObject = objectTable.FirstOrDefault(candidate => candidate is not null && candidate.GameObjectId == parsedId);
        if (gameObject is null)
        {
            return new TargetObjectResult(
                gameObjectId,
                false,
                "object_not_found",
                null,
                null,
                null,
                $"Object {gameObjectId} was not found in the current object table.");
        }

        if (!gameObject.IsTargetable)
        {
            return new TargetObjectResult(
                gameObjectId,
                false,
                "object_not_targetable",
                $"0x{gameObject.GameObjectId:X}",
                ReadName(gameObject),
                gameObject.ObjectKind.ToString(),
                $"Object {ReadName(gameObject)} is not targetable.");
        }

        targetManager.Target = gameObject;
        IGameObject? assignedTarget = targetManager.Target;
        bool succeeded = assignedTarget is not null && assignedTarget.GameObjectId == gameObject.GameObjectId;

        return new TargetObjectResult(
            gameObjectId,
            succeeded,
            succeeded ? null : "target_assignment_failed",
            $"0x{gameObject.GameObjectId:X}",
            ReadName(gameObject),
            gameObject.ObjectKind.ToString(),
            succeeded
                ? $"Targeted {ReadName(gameObject)}."
                : $"Failed to target {ReadName(gameObject)}.");
    }

    private static string NormalizeGameObjectId(string gameObjectId)
    {
        return string.IsNullOrWhiteSpace(gameObjectId)
            ? throw new ArgumentException("Game object id is required.", nameof(gameObjectId))
            : gameObjectId.Trim();
    }

    private static bool TryParseGameObjectId(string value, out ulong gameObjectId)
    {
        string trimmed = value.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return ulong.TryParse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out gameObjectId);

        return ulong.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out gameObjectId);
    }

    private static string ReadName(IGameObject gameObject)
    {
        return string.IsNullOrWhiteSpace(gameObject.Name.TextValue)
            ? $"Object#{gameObject.GameObjectId:X}"
            : gameObject.Name.TextValue;
    }
}

[MemoryPackable]
public sealed partial record TargetObjectResult(
    string RequestedGameObjectId,
    bool Succeeded,
    string? Reason,
    string? TargetedGameObjectId,
    string? TargetName,
    string? ObjectKind,
    string SummaryText);