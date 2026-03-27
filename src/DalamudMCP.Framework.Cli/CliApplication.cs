using System.Text;
using System.Text.Json;

namespace DalamudMCP.Framework.Cli;

public sealed class CliApplication
{
    private static readonly byte[] NewLineUtf8 = [(byte)'\n'];
    private readonly IReadOnlyList<OperationDescriptor> operations;
    private readonly ICliInvoker cliInvoker;
    private readonly IServiceProvider? services;
    private readonly Stream? rawOutput;
    private readonly JsonSerializerOptions jsonSerializerOptions;

    public CliApplication(
        IReadOnlyList<OperationDescriptor> operations,
        ICliInvoker cliInvoker,
        IServiceProvider? services = null,
        Stream? rawOutput = null,
        JsonSerializerOptions? jsonSerializerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(cliInvoker);

        this.operations = operations;
        this.cliInvoker = cliInvoker;
        this.services = services;
        this.rawOutput = rawOutput;
        this.jsonSerializerOptions = jsonSerializerOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
    }

    public string GetUsage(string executableName = "app")
    {
        StringBuilder builder = new();
        builder.AppendLine("Usage:");
        foreach (OperationDescriptor operation in GetVisibleCliOperations())
            builder.AppendLine("  " + GetOperationUsage(operation, executableName));

        builder.AppendLine();
        builder.AppendLine("Options:");
        builder.AppendLine("  --help                Show help for the application or a specific command.");
        builder.AppendLine("  --json                Emit machine-readable JSON instead of text.");
        return builder.ToString().TrimEnd();
    }

    public async Task<int> ExecuteAsync(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        ParsedArguments parsedArguments = ParseArguments(args);
        if (parsedArguments.RequestHelp && parsedArguments.CommandTokens.Count == 0)
        {
            await output.WriteLineAsync(GetUsage()).ConfigureAwait(false);
            return CliExitCodes.Success;
        }

        if (!TryResolveOperation(parsedArguments.CommandTokens, out OperationDescriptor? operation, out int consumedTokens))
        {
            if (parsedArguments.CommandTokens.Count == 0)
            {
                await output.WriteLineAsync(GetUsage()).ConfigureAwait(false);
                return CliExitCodes.Success;
            }

            await error.WriteLineAsync(
                    $"Unknown command '{string.Join(' ', parsedArguments.CommandTokens)}'.")
                .ConfigureAwait(false);
            return CliExitCodes.UsageError;
        }

        if (parsedArguments.RequestHelp)
        {
            await output.WriteLineAsync(GetOperationUsage(operation!, "app")).ConfigureAwait(false);
            return CliExitCodes.Success;
        }

        OperationDescriptor resolvedOperation = operation!;

        try
        {
            ParsedCommandInput input = ParseCommandInput(resolvedOperation, parsedArguments.CommandTokens.Skip(consumedTokens));
            if (!cliInvoker.TryInvoke(
                    resolvedOperation.OperationId,
                    input.Options,
                    input.Arguments,
                    services,
                    parsedArguments.Json,
                    cancellationToken,
                    out ValueTask<CliInvocationResult> invocation))
            {
                throw new InvalidOperationException(
                    $"No generated CLI invoker was available for operation '{resolvedOperation.OperationId}'.");
            }

            CliInvocationResult generatedResult = await invocation.ConfigureAwait(false);
            await WriteResultAsync(output, generatedResult, parsedArguments.Json, cancellationToken).ConfigureAwait(false);
            return CliExitCodes.Success;
        }
        catch (ArgumentException exception)
        {
            await error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return CliExitCodes.UsageError;
        }
        catch (InvalidOperationException exception)
        {
            await error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return CliExitCodes.Unavailable;
        }
    }

    private IEnumerable<OperationDescriptor> GetVisibleCliOperations()
    {
        return operations
            .Where(static operation => operation.Visibility is not OperationVisibility.McpOnly &&
                                       operation.CliCommandPath is { Count: > 0 } &&
                                       !operation.Hidden)
            .OrderBy(static operation => string.Join(' ', operation.CliCommandPath!), StringComparer.OrdinalIgnoreCase);
    }

    private static ParsedArguments ParseArguments(IReadOnlyList<string> args)
    {
        List<string> commandTokens = [];
        bool json = false;
        bool requestHelp = false;

        foreach (string arg in args)
        {
            if (string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase))
            {
                json = true;
                continue;
            }

            if (string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase))
            {
                requestHelp = true;
                continue;
            }

            commandTokens.Add(arg);
        }

        return new ParsedArguments(commandTokens, json, requestHelp);
    }

    private bool TryResolveOperation(
        IReadOnlyList<string> commandTokens,
        out OperationDescriptor? operation,
        out int consumedTokens)
    {
        foreach (OperationDescriptor candidate in operations
                     .Where(static candidate => candidate.Visibility is not OperationVisibility.McpOnly &&
                                                candidate.CliCommandPath is { Count: > 0 })
                     .OrderByDescending(static candidate => candidate.CliCommandPath!.Count))
        {
            foreach (IReadOnlyList<string> path in GetCliCommandPaths(candidate))
            {
                if (commandTokens.Count < path.Count)
                    continue;

                bool matched = true;
                for (int index = 0; index < path.Count; index++)
                {
                    if (string.Equals(commandTokens[index], path[index], StringComparison.OrdinalIgnoreCase))
                        continue;

                    matched = false;
                    break;
                }

                if (!matched)
                    continue;

                operation = candidate;
                consumedTokens = path.Count;
                return true;
            }
        }

        operation = null;
        consumedTokens = 0;
        return false;
    }

    private static ParsedCommandInput ParseCommandInput(
        OperationDescriptor operation,
        IEnumerable<string> remainingTokens)
    {
        Dictionary<string, string> options = new(StringComparer.OrdinalIgnoreCase);
        List<string> arguments = [];

        using IEnumerator<string> enumerator = remainingTokens.GetEnumerator();
        while (enumerator.MoveNext())
        {
            string current = enumerator.Current;
            if (!current.StartsWith("--", StringComparison.Ordinal))
            {
                arguments.Add(current);
                continue;
            }

            string optionToken = current[2..];
            string optionName;
            string optionValue;
            int equalsIndex = optionToken.IndexOf('=');
            if (equalsIndex >= 0)
            {
                optionName = optionToken[..equalsIndex];
                optionValue = optionToken[(equalsIndex + 1)..];
            }
            else
            {
                optionName = optionToken;
                if (!enumerator.MoveNext() || string.IsNullOrWhiteSpace(enumerator.Current) ||
                    enumerator.Current.StartsWith("--", StringComparison.Ordinal))
                {
                    throw new ArgumentException($"The --{optionName} option requires a non-empty value.");
                }

                optionValue = enumerator.Current;
            }

            if (string.IsNullOrWhiteSpace(optionName))
                throw new ArgumentException("Encountered an empty option name.");

            options[optionName] = optionValue;
        }

        HashSet<string> knownOptionNames = GetKnownOptionNames(operation);
        foreach ((string optionName, _) in options)
        {
            if (!knownOptionNames.Contains(optionName))
                throw new ArgumentException($"Unknown option '--{optionName}' for command '{string.Join(' ', operation.CliCommandPath!)}'.");
        }

        foreach (ParameterDescriptor parameter in operation.Parameters.Where(static parameter =>
                     parameter.Source == ParameterSource.Option && parameter.Required))
        {
            if (CliBinding.TryFindOptionValue(options, GetCliParameterName(parameter), parameter.Aliases, out _))
                continue;

            throw new ArgumentException($"Missing required --{GetCliParameterName(parameter)} option.");
        }

        foreach (ParameterDescriptor parameter in operation.Parameters.Where(static parameter =>
                     parameter.Source == ParameterSource.Argument && parameter.Required))
        {
            if (parameter.Position is not null && parameter.Position.Value < arguments.Count)
                continue;

            throw new ArgumentException($"Missing required argument '{GetCliParameterName(parameter)}'.");
        }

        return new ParsedCommandInput(options, arguments);
    }

    private async Task WriteResultAsync(
        TextWriter output,
        CliInvocationResult invocationResult,
        bool json,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (json)
        {
            if (invocationResult.RawJsonPayload is { Length: > 0 } rawJsonPayload)
            {
                if (rawOutput is not null)
                {
                    await rawOutput.WriteAsync(rawJsonPayload, cancellationToken).ConfigureAwait(false);
                    await rawOutput.WriteAsync(NewLineUtf8, cancellationToken).ConfigureAwait(false);
                    await rawOutput.FlushAsync(cancellationToken).ConfigureAwait(false);
                    return;
                }

                await output.WriteLineAsync(Encoding.UTF8.GetString(rawJsonPayload)).ConfigureAwait(false);
                return;
            }

            string jsonText = JsonSerializer.Serialize(invocationResult.Result, invocationResult.ResultType, jsonSerializerOptions);
            await output.WriteLineAsync(jsonText).ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(invocationResult.Text))
            return;

        await output.WriteLineAsync(invocationResult.Text).ConfigureAwait(false);
    }

    private static HashSet<string> GetKnownOptionNames(OperationDescriptor operation)
    {
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (ParameterDescriptor parameter in operation.Parameters.Where(static parameter =>
                     parameter.Source == ParameterSource.Option))
        {
            names.Add(GetCliParameterName(parameter));
            if (parameter.Aliases is null)
                continue;

            foreach (string alias in parameter.Aliases)
                names.Add(alias);
        }

        return names;
    }

    private static string GetOperationUsage(OperationDescriptor operation, string executableName)
    {
        StringBuilder builder = new();
        builder.Append("Usage: ")
            .Append(executableName)
            .Append(' ')
            .Append(string.Join(' ', operation.CliCommandPath!));

        foreach (ParameterDescriptor parameter in operation.Parameters
                     .Where(static parameter => parameter.Source == ParameterSource.Argument)
                     .OrderBy(static parameter => parameter.Position))
        {
            builder.Append(' ')
                .Append(parameter.Required ? '<' : '[')
                .Append(GetCliParameterName(parameter))
                .Append(parameter.Required ? '>' : ']');
        }

        foreach (ParameterDescriptor parameter in operation.Parameters.Where(static parameter =>
                     parameter.Source == ParameterSource.Option))
        {
            builder.Append(parameter.Required ? " --" : " [--")
                .Append(GetCliParameterName(parameter))
                .Append(" <value>");
            if (!parameter.Required)
                builder.Append(']');
        }

        if (!string.IsNullOrWhiteSpace(operation.Description))
        {
            builder.AppendLine()
                .Append("  ")
                .Append(operation.Description);
        }

        if (operation.CliCommandAliases is { Count: > 0 })
        {
            builder.AppendLine()
                .Append("  Aliases: ")
                .Append(string.Join(", ", operation.CliCommandAliases.Select(static aliasPath => string.Join(' ', aliasPath))));
        }

        foreach (ParameterDescriptor parameter in operation.Parameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.Description))
                continue;

            builder.AppendLine();
            if (parameter.Source == ParameterSource.Argument)
                builder.Append("  ").Append(GetCliParameterName(parameter));
            else if (parameter.Source == ParameterSource.Option)
                builder.Append("  --").Append(GetCliParameterName(parameter));
            else
                builder.Append("  ").Append(parameter.Name);

            builder.Append(": ").Append(parameter.Description);
        }

        return builder.ToString();
    }

    private sealed record ParsedArguments(IReadOnlyList<string> CommandTokens, bool Json, bool RequestHelp);

    private sealed record ParsedCommandInput(
        IReadOnlyDictionary<string, string> Options,
        IReadOnlyList<string> Arguments);

    private static string GetCliParameterName(ParameterDescriptor parameter)
    {
        return string.IsNullOrWhiteSpace(parameter.CliName) ? parameter.Name : parameter.CliName;
    }

    private static IEnumerable<IReadOnlyList<string>> GetCliCommandPaths(OperationDescriptor operation)
    {
        yield return operation.CliCommandPath!;
        if (operation.CliCommandAliases is null)
            yield break;

        foreach (IReadOnlyList<string> aliasPath in operation.CliCommandAliases)
            yield return aliasPath;
    }
}


