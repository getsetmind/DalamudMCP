using System.Text.Json;
using Manifold;
using DalamudMCP.Protocol;

namespace DalamudMCP.Cli;

internal static class ProtocolOperationDescriptorMapper
{
    public static IReadOnlyList<OperationDescriptor> ToCliOperationDescriptors(
        IReadOnlyList<ProtocolOperationDescriptor> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        return operations
            .Select(ToCliOperationDescriptor)
            .ToArray();
    }

    private static OperationDescriptor ToCliOperationDescriptor(ProtocolOperationDescriptor operation)
    {
        ParameterDescriptor[] parameters = operation.Parameters
            .Select(ToCliParameterDescriptor)
            .ToArray();

        return new OperationDescriptor(
            operation.OperationId,
            typeof(RemoteCliInvoker),
            nameof(RemoteCliInvoker.TryInvoke),
            typeof(JsonElement),
            operation.Visibility switch
            {
                ProtocolOperationVisibility.CliOnly => OperationVisibility.CliOnly,
                ProtocolOperationVisibility.McpOnly => OperationVisibility.McpOnly,
                _ => OperationVisibility.Both
            },
            parameters,
            operation.Description,
            operation.Summary,
            operation.CliCommandPath?.ToArray(),
            operation.CliCommandAliases?.Select(static alias => (IReadOnlyList<string>)alias.ToArray()).ToArray(),
            operation.McpToolName,
            operation.Hidden,
            null);
    }

    private static ParameterDescriptor ToCliParameterDescriptor(ProtocolParameterDescriptor parameter)
    {
        return new ParameterDescriptor(
            parameter.Name,
            GetClrType(parameter),
            parameter.Source switch
            {
                ProtocolParameterSource.Option => ParameterSource.Option,
                ProtocolParameterSource.Argument => ParameterSource.Argument,
                _ => throw new InvalidOperationException($"Protocol parameter '{parameter.Name}' had an unsupported source.")
            },
            parameter.Required,
            parameter.Position,
            parameter.Description,
            parameter.Aliases?.ToArray(),
            parameter.CliName,
            parameter.McpName);
    }

    private static Type GetClrType(ProtocolParameterDescriptor parameter)
    {
        Type elementType = parameter.ValueKind switch
        {
            ProtocolValueKind.Text => typeof(string),
            ProtocolValueKind.Flag => typeof(bool),
            ProtocolValueKind.Number => typeof(int),
            ProtocolValueKind.LargeNumber => typeof(long),
            ProtocolValueKind.Real => typeof(double),
            ProtocolValueKind.Fixed => typeof(decimal),
            ProtocolValueKind.UniqueId => typeof(Guid),
            ProtocolValueKind.Address => typeof(Uri),
            ProtocolValueKind.Timestamp => typeof(DateTimeOffset),
            _ => typeof(JsonElement)
        };

        if (parameter.IsArray)
            return elementType.MakeArrayType();

        if (!parameter.IsNullable)
            return elementType;

        return Nullable.GetUnderlyingType(elementType) is null && elementType.IsValueType
            ? typeof(Nullable<>).MakeGenericType(elementType)
            : elementType;
    }
}



