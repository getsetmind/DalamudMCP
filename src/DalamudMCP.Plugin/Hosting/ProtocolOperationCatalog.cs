using Manifold;
using DalamudMCP.Protocol;

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
            .Select(ToProtocolDescriptor)
            .ToArray();

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
            operation.Hidden);
    }

    private static ProtocolParameterDescriptor ToProtocolDescriptor(ParameterDescriptor parameter)
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
            mcpName);
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
}