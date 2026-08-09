using DalamudMCP.Protocol;
using FFXIVClientStructs.FFXIV.Client.Game;
using Manifold;

namespace DalamudMCP.Plugin.Hosting;

public static class ProtocolOperationCatalog
{
    public static DescribeOperationsResponse Create(IReadOnlyList<OperationDescriptor> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        ProtocolOperationDescriptor[] descriptors = operations
            .OrderBy(static operation => operation.OperationId, StringComparer.Ordinal)
            .Select(ToProtocolDescriptor)
            .ToArray();

        return new DescribeOperationsResponse(descriptors);
    }

    private static ProtocolOperationDescriptor ToProtocolDescriptor(OperationDescriptor operation)
    {
        ProtocolParameterDescriptor[] parameters = operation.Parameters
            .Where(static parameter => parameter.Source is ParameterSource.Option or ParameterSource.Argument)
            .Select(parameter => ToProtocolDescriptor(operation.OperationId, parameter))
            .ToArray();
        ProtocolOperationMetadata metadata = PluginOperationMetadataCatalog.Resolve(operation.OperationId);

        return new ProtocolOperationDescriptor(
            operation.OperationId,
            operation.Visibility switch
            {
                OperationVisibility.CliOnly => ProtocolOperationVisibility.CliOnly,
                OperationVisibility.McpOnly => ProtocolOperationVisibility.McpOnly,
                _ => ProtocolOperationVisibility.Both
            },
            parameters,
            operation.Description,
            operation.Summary,
            operation.CliCommandPath?.ToArray(),
            operation.CliCommandAliases?.Select(static alias => (IReadOnlyList<string>)alias.ToArray()).ToArray(),
            operation.McpToolName,
            operation.Hidden,
            metadata.Effect,
            metadata.PermissionScope,
            metadata.Idempotent,
            metadata.OpenWorld,
            metadata.SupportsDryRun,
            metadata.RequiresFrameworkThread,
            ProtocolJsonSchemaBuilder.Create(operation.ResultType));
    }

    private static ProtocolParameterDescriptor ToProtocolDescriptor(string operationId, ParameterDescriptor parameter)
    {
        (ProtocolValueKind valueKind, bool isNullable, bool isArray) = GetValueShape(parameter.ParameterType);
        string requestPropertyName = string.IsNullOrWhiteSpace(parameter.RequestPropertyName)
            ? parameter.Name
            : parameter.RequestPropertyName;
        string? cliName = string.IsNullOrWhiteSpace(parameter.CliName)
            ? parameter.Name
            : parameter.CliName;
        string? mcpName = string.IsNullOrWhiteSpace(parameter.McpName)
            ? parameter.Name
            : parameter.McpName;
        ParameterConstraints constraints = ResolveConstraints(operationId, requestPropertyName, valueKind, isArray);

        return new ProtocolParameterDescriptor(
            requestPropertyName,
            valueKind,
            parameter.Source switch
            {
                ParameterSource.Option => ProtocolParameterSource.Option,
                ParameterSource.Argument => ProtocolParameterSource.Argument,
                _ => throw new InvalidOperationException($"Parameter '{parameter.Name}' cannot be exposed over protocol.")
            },
            parameter.Required,
            isNullable,
            isArray,
            parameter.Position,
            parameter.Description,
            parameter.Aliases?.ToArray(),
            cliName,
            mcpName,
            constraints.Minimum,
            constraints.Maximum,
            constraints.MinLength,
            constraints.MaxLength,
            constraints.MaxItems,
            constraints.AllowedValues);
    }

    private static ParameterConstraints ResolveConstraints(
        string operationId,
        string name,
        ProtocolValueKind valueKind,
        bool isArray)
    {
        string normalized = name.ToLowerInvariant();
        double? minimum = normalized switch
        {
            "capacity" => 16,
            "ttlseconds" => 60,
            "limit" or "maxcount" or "timeoutmilliseconds" or "actionid" => 1,
            "rowid" or "aftercursor" or "cursor" when valueKind is not ProtocolValueKind.Text => 0,
            _ => null,
        };
        double? maximum = normalized switch
        {
            "actionid" or "rowid" or "extraparam" or "comborouteid" => uint.MaxValue,
            "timeoutmilliseconds" => 30000,
            "capacity" => 10000,
            "ttlseconds" => 86400,
            "limit" when operationId.StartsWith("game-data.", StringComparison.Ordinal) => 100,
            "limit" when operationId.StartsWith("events.", StringComparison.Ordinal) => 500,
            "limit" when string.Equals(operationId, "inventory.items", StringComparison.Ordinal) => 200,
            "limit" or "maxcount" => 500,
            _ => null,
        };
        int? minLength = valueKind is ProtocolValueKind.Text && normalized is "sheet" or "pluginname" or "callgate"
            ? 1
            : null;
        int? maxLength = valueKind is ProtocolValueKind.Text
            ? normalized switch
            {
                "where" or "argumentsjson" => 16384,
                "cursor" => 2048,
                "sheet" or "query" or "actionname" or "pluginname" or "callgate" => 256,
                _ => 4096,
            }
            : null;
        int? maxItems = isArray ? 64 : null;
        IReadOnlyList<string>? allowedValues = normalized switch
        {
            "action" when string.Equals(operationId, "plugin.lifecycle.control", StringComparison.Ordinal) =>
                ["load", "unload", "reload", "enable", "disable"],
            "action" when string.Equals(operationId, "plugin.package.control", StringComparison.Ordinal) =>
                ["install", "update", "uninstall"],
            "actiontype" => Enum.GetNames<ActionType>()
                .Where(static value => !string.Equals(value, nameof(ActionType.None), StringComparison.Ordinal))
                .ToArray(),
            "mode" => Enum.GetNames<ActionManager.UseActionMode>(),
            "language" => ["none", "ja", "en", "de", "fr", "chs", "cht", "ko"],
            "capturearea" => ["client", "window"],
            _ => null,
        };
        return new ParameterConstraints(minimum, maximum, minLength, maxLength, maxItems, allowedValues);
    }

    private static (ProtocolValueKind ValueKind, bool IsNullable, bool IsArray) GetValueShape(Type parameterType)
    {
        ArgumentNullException.ThrowIfNull(parameterType);

        bool isArray = parameterType.IsArray;
        Type type = isArray
            ? parameterType.GetElementType() ?? throw new InvalidOperationException($"Array parameter '{parameterType.FullName}' was missing an element type.")
            : parameterType;
        Type? nullableUnderlyingType = Nullable.GetUnderlyingType(type);
        bool isNullable = nullableUnderlyingType is not null;
        Type effectiveType = nullableUnderlyingType ?? type;

        ProtocolValueKind valueKind = effectiveType == typeof(string)
            ? ProtocolValueKind.Text
            : effectiveType == typeof(bool)
                ? ProtocolValueKind.Flag
                : effectiveType == typeof(int)
                    ? ProtocolValueKind.Number
                    : effectiveType == typeof(long)
                        ? ProtocolValueKind.LargeNumber
                        : effectiveType == typeof(double)
                            ? ProtocolValueKind.Real
                            : effectiveType == typeof(decimal)
                                ? ProtocolValueKind.Fixed
                                : effectiveType == typeof(Guid)
                                    ? ProtocolValueKind.UniqueId
                                    : effectiveType == typeof(Uri)
                                        ? ProtocolValueKind.Address
                                        : effectiveType == typeof(DateTimeOffset)
                                            ? ProtocolValueKind.Timestamp
                                            : ProtocolValueKind.Json;

        return (valueKind, isNullable, isArray);
    }

    private sealed record ParameterConstraints(
        double? Minimum,
        double? Maximum,
        int? MinLength,
        int? MaxLength,
        int? MaxItems,
        IReadOnlyList<string>? AllowedValues);
}
