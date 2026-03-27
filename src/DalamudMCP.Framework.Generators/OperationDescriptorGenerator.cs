using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace DalamudMCP.Framework.Generators;

[Generator]
public sealed class OperationDescriptorGenerator : IIncrementalGenerator
{
    private const string OperationAttributeMetadataName = "DalamudMCP.Framework.OperationAttribute";
    private const string OptionAttributeMetadataName = "DalamudMCP.Framework.OptionAttribute";
    private const string ArgumentAttributeMetadataName = "DalamudMCP.Framework.ArgumentAttribute";
    private const string AliasAttributeMetadataName = "DalamudMCP.Framework.AliasAttribute";
    private const string CliCommandAttributeMetadataName = "DalamudMCP.Framework.CliCommandAttribute";
    private const string CliNameAttributeMetadataName = "DalamudMCP.Framework.CliNameAttribute";
    private const string McpToolAttributeMetadataName = "DalamudMCP.Framework.McpToolAttribute";
    private const string McpNameAttributeMetadataName = "DalamudMCP.Framework.McpNameAttribute";
    private const string CliOnlyAttributeMetadataName = "DalamudMCP.Framework.CliOnlyAttribute";
    private const string McpOnlyAttributeMetadataName = "DalamudMCP.Framework.McpOnlyAttribute";
    private const string FromServicesAttributeMetadataName = "DalamudMCP.Framework.FromServicesAttribute";
    private const string ResultFormatterAttributeMetadataName = "DalamudMCP.Framework.ResultFormatterAttribute";
    private const string OperationInterfaceMetadataName = "DalamudMCP.Framework.IOperation<TRequest, TResult>";
    private const string OperationContextMetadataName = "DalamudMCP.Framework.OperationContext";
    private static readonly SymbolDisplayFormat FullyQualifiedTypeFormat = SymbolDisplayFormat.FullyQualifiedFormat;
    private static readonly DiagnosticDescriptor ConflictingVisibilityDescriptor = new(
        id: "DMCF001",
        title: "Conflicting CLI and MCP visibility",
        messageFormat: "Operation '{0}' cannot be marked with both [CliOnly] and [McpOnly]",
        category: "DalamudMCP.Framework",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor ConflictingParameterBindingDescriptor = new(
        id: "DMCF002",
        title: "Conflicting parameter binding",
        messageFormat: "Parameter '{0}' on operation '{1}' cannot be marked with both [Option] and [Argument]",
        category: "DalamudMCP.Framework",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor UnsupportedParameterBindingDescriptor = new(
        id: "DMCF003",
        title: "Unsupported parameter binding",
        messageFormat: "Parameter '{0}' on operation '{1}' must be bound with [Option], [Argument], [FromServices], or be a CancellationToken",
        category: "DalamudMCP.Framework",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor UnsupportedOperationClassDescriptor = new(
        id: "DMCF004",
        title: "Unsupported operation class",
        messageFormat: "Operation class '{0}' must implement IOperation<TRequest, TResult> and expose ExecuteAsync(TRequest, OperationContext)",
        category: "DalamudMCP.Framework",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor UnsupportedRequestPropertyBindingDescriptor = new(
        id: "DMCF005",
        title: "Unsupported request property binding",
        messageFormat: "Property '{0}' on request type '{1}' for operation '{2}' must be writable with a public init or set accessor",
        category: "DalamudMCP.Framework",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<OperationAnalysisResult> candidates = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                OperationAttributeMetadataName,
                static (_, _) => true,
                static (attributeContext, cancellationToken) => CreateCandidate(attributeContext, cancellationToken));

        IncrementalValueProvider<(Compilation Left, ImmutableArray<OperationAnalysisResult> Right)> generationInputs =
            context.CompilationProvider.Combine(candidates.Collect());

        context.RegisterSourceOutput(generationInputs, static (productionContext, input) =>
            Execute(productionContext, input.Left, input.Right));
    }

    private static OperationAnalysisResult CreateCandidate(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        AttributeData operationAttribute = context.Attributes[0];
        return context.TargetSymbol switch
        {
            IMethodSymbol methodSymbol => CreateMethodCandidate(context, methodSymbol, operationAttribute, cancellationToken),
            INamedTypeSymbol typeSymbol => CreateClassCandidate(context, typeSymbol, operationAttribute, cancellationToken),
            _ => new OperationAnalysisResult(null, [])
        };
    }

    private static OperationAnalysisResult CreateMethodCandidate(
        GeneratorAttributeSyntaxContext context,
        IMethodSymbol methodSymbol,
        AttributeData operationAttribute,
        CancellationToken cancellationToken)
    {
        ImmutableArray<OperationDiagnostic>.Builder diagnosticBuilder = ImmutableArray.CreateBuilder<OperationDiagnostic>();

        bool hasCliOnly = HasAttribute(methodSymbol, CliOnlyAttributeMetadataName);
        bool hasMcpOnly = HasAttribute(methodSymbol, McpOnlyAttributeMetadataName);
        if (hasCliOnly && hasMcpOnly)
        {
            diagnosticBuilder.Add(new OperationDiagnostic(
                ConflictingVisibilityDescriptor,
                GetBestLocation(methodSymbol),
                [methodSymbol.Name]));
        }

        ImmutableArray<ParameterCandidate>.Builder parameterBuilder = ImmutableArray.CreateBuilder<ParameterCandidate>();
        foreach (IParameterSymbol parameterSymbol in methodSymbol.Parameters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ParameterAnalysisResult parameterAnalysis = CreateParameterCandidate(methodSymbol.Name, parameterSymbol);
            diagnosticBuilder.AddRange(parameterAnalysis.Diagnostics);
            if (parameterAnalysis.Candidate is not null)
                parameterBuilder.Add(parameterAnalysis.Candidate);
        }

        if (diagnosticBuilder.Count > 0)
            return new OperationAnalysisResult(null, diagnosticBuilder.ToImmutable());

        ImmutableArray<string>? cliCommandPath = GetCliCommandPath(methodSymbol);
        string? cliName = GetSingleName(methodSymbol, CliNameAttributeMetadataName);
        if ((cliCommandPath is null || cliCommandPath.Value.IsDefaultOrEmpty) && !string.IsNullOrWhiteSpace(cliName))
            cliCommandPath = [cliName!];

        ImmutableArray<ImmutableArray<string>>? cliCommandAliases = GetCliCommandAliases(methodSymbol, cliCommandPath);
        MethodReturnKind methodReturnKind = GetMethodReturnKind(methodSymbol.ReturnType);
        ITypeSymbol resultType = UnwrapResultType(methodSymbol.ReturnType, context.SemanticModel.Compilation);
        string? mcpToolName = GetSingleName(methodSymbol, McpToolAttributeMetadataName) ??
                              GetSingleName(methodSymbol, McpNameAttributeMetadataName) ??
                              (hasCliOnly ? null : (string)operationAttribute.ConstructorArguments[0].Value!);

        return new OperationAnalysisResult(
            new OperationCandidate(
                (string)operationAttribute.ConstructorArguments[0].Value!,
                methodSymbol.ContainingType.ToDisplayString(FullyQualifiedTypeFormat),
                methodSymbol.Name,
                methodSymbol.ReturnType.ToDisplayString(FullyQualifiedTypeFormat),
                resultType.ToDisplayString(FullyQualifiedTypeFormat),
                methodReturnKind,
                GetVisibility(hasCliOnly, hasMcpOnly),
                parameterBuilder.ToImmutable(),
                GetNamedString(operationAttribute, "Description"),
                GetNamedString(operationAttribute, "Summary"),
                cliCommandPath,
                cliCommandAliases,
                mcpToolName,
                GetFormatterTypeName(methodSymbol),
                GetNamedBoolean(operationAttribute, "Hidden"),
                InvocationKind.StaticMethod,
                null),
            diagnosticBuilder.ToImmutable());
    }

    private static OperationAnalysisResult CreateClassCandidate(
        GeneratorAttributeSyntaxContext context,
        INamedTypeSymbol typeSymbol,
        AttributeData operationAttribute,
        CancellationToken cancellationToken)
    {
        ImmutableArray<OperationDiagnostic>.Builder diagnosticBuilder = ImmutableArray.CreateBuilder<OperationDiagnostic>();

        bool hasCliOnly = HasAttribute(typeSymbol, CliOnlyAttributeMetadataName);
        bool hasMcpOnly = HasAttribute(typeSymbol, McpOnlyAttributeMetadataName);
        if (hasCliOnly && hasMcpOnly)
        {
            diagnosticBuilder.Add(new OperationDiagnostic(
                ConflictingVisibilityDescriptor,
                GetBestLocation(typeSymbol),
                [typeSymbol.Name]));
        }

        INamedTypeSymbol? operationInterface = typeSymbol.AllInterfaces.FirstOrDefault(static candidate =>
            string.Equals(candidate.OriginalDefinition.ToDisplayString(), OperationInterfaceMetadataName, StringComparison.Ordinal));
        if (operationInterface is null)
        {
            diagnosticBuilder.Add(new OperationDiagnostic(
                UnsupportedOperationClassDescriptor,
                GetBestLocation(typeSymbol),
                [typeSymbol.Name]));
            return new OperationAnalysisResult(null, diagnosticBuilder.ToImmutable());
        }

        ITypeSymbol requestType = operationInterface.TypeArguments[0];
        IMethodSymbol? executeMethod = FindExecuteMethod(typeSymbol, requestType);
        if (executeMethod is null)
        {
            diagnosticBuilder.Add(new OperationDiagnostic(
                UnsupportedOperationClassDescriptor,
                GetBestLocation(typeSymbol),
                [typeSymbol.Name]));
            return new OperationAnalysisResult(null, diagnosticBuilder.ToImmutable());
        }

        if (requestType is INamedTypeSymbol requestTypeSymbol)
        {
            ImmutableArray<ParameterCandidate>.Builder parameterBuilder = ImmutableArray.CreateBuilder<ParameterCandidate>();
            foreach (IPropertySymbol propertySymbol in GetBindableProperties(requestTypeSymbol))
            {
                cancellationToken.ThrowIfCancellationRequested();

                ParameterAnalysisResult parameterAnalysis = CreatePropertyCandidate(
                    typeSymbol.Name,
                    requestTypeSymbol,
                    propertySymbol);
                diagnosticBuilder.AddRange(parameterAnalysis.Diagnostics);
                if (parameterAnalysis.Candidate is not null)
                    parameterBuilder.Add(parameterAnalysis.Candidate);
            }

            if (diagnosticBuilder.Count > 0)
                return new OperationAnalysisResult(null, diagnosticBuilder.ToImmutable());

            ImmutableArray<string>? cliCommandPath = GetCliCommandPath(typeSymbol);
            string? cliName = GetSingleName(typeSymbol, CliNameAttributeMetadataName);
            if ((cliCommandPath is null || cliCommandPath.Value.IsDefaultOrEmpty) && !string.IsNullOrWhiteSpace(cliName))
                cliCommandPath = [cliName!];

            ImmutableArray<ImmutableArray<string>>? cliCommandAliases = GetCliCommandAliases(typeSymbol, cliCommandPath);
            string? mcpToolName = GetSingleName(typeSymbol, McpToolAttributeMetadataName) ??
                                  GetSingleName(typeSymbol, McpNameAttributeMetadataName) ??
                                  (hasCliOnly ? null : (string)operationAttribute.ConstructorArguments[0].Value!);

            return new OperationAnalysisResult(
                new OperationCandidate(
                    (string)operationAttribute.ConstructorArguments[0].Value!,
                    typeSymbol.ToDisplayString(FullyQualifiedTypeFormat),
                    executeMethod.Name,
                    executeMethod.ReturnType.ToDisplayString(FullyQualifiedTypeFormat),
                    operationInterface.TypeArguments[1].ToDisplayString(FullyQualifiedTypeFormat),
                    GetMethodReturnKind(executeMethod.ReturnType),
                    GetVisibility(hasCliOnly, hasMcpOnly),
                    parameterBuilder.ToImmutable(),
                    GetNamedString(operationAttribute, "Description"),
                    GetNamedString(operationAttribute, "Summary"),
                    cliCommandPath,
                    cliCommandAliases,
                    mcpToolName,
                    GetFormatterTypeName(typeSymbol),
                    GetNamedBoolean(operationAttribute, "Hidden"),
                    InvocationKind.InstanceOperation,
                    requestTypeSymbol.ToDisplayString(FullyQualifiedTypeFormat)),
                diagnosticBuilder.ToImmutable());
        }

        diagnosticBuilder.Add(new OperationDiagnostic(
            UnsupportedOperationClassDescriptor,
            GetBestLocation(typeSymbol),
            [typeSymbol.Name]));
        return new OperationAnalysisResult(null, diagnosticBuilder.ToImmutable());
    }

    private static IMethodSymbol? FindExecuteMethod(INamedTypeSymbol typeSymbol, ITypeSymbol requestType)
    {
        return typeSymbol.GetMembers("ExecuteAsync")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(methodSymbol =>
                !methodSymbol.IsStatic &&
                methodSymbol.Parameters.Length == 2 &&
                SymbolEqualityComparer.Default.Equals(methodSymbol.Parameters[0].Type, requestType) &&
                string.Equals(methodSymbol.Parameters[1].Type.ToDisplayString(), OperationContextMetadataName, StringComparison.Ordinal));
    }

    private static IEnumerable<IPropertySymbol> GetBindableProperties(INamedTypeSymbol requestType)
    {
        return requestType.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(static property =>
                !property.IsStatic &&
                property.Parameters.Length == 0 &&
                (HasAttribute(property, OptionAttributeMetadataName) || HasAttribute(property, ArgumentAttributeMetadataName)));
    }

    private static string? GetFormatterTypeName(ISymbol symbol)
    {
        AttributeData? formatterAttribute = GetAttribute(symbol, ResultFormatterAttributeMetadataName);
        if (formatterAttribute is null || formatterAttribute.ConstructorArguments.Length == 0)
            return null;

        return formatterAttribute.ConstructorArguments[0].Value is ITypeSymbol formatterType
            ? formatterType.ToDisplayString(FullyQualifiedTypeFormat)
            : null;
    }

    private static ParameterAnalysisResult CreateParameterCandidate(
        string operationName,
        IParameterSymbol parameterSymbol)
    {
        bool isCancellationToken = IsCancellationToken(parameterSymbol.Type);
        bool hasFromServices = HasAttribute(parameterSymbol, FromServicesAttributeMetadataName);
        AttributeData? optionAttribute = GetAttribute(parameterSymbol, OptionAttributeMetadataName);
        AttributeData? argumentAttribute = GetAttribute(parameterSymbol, ArgumentAttributeMetadataName);

        if (optionAttribute is not null && argumentAttribute is not null)
        {
            return ParameterAnalysisResult.FromDiagnostic(
                new OperationDiagnostic(
                    ConflictingParameterBindingDescriptor,
                    GetBestLocation(parameterSymbol),
                    [parameterSymbol.Name, operationName]));
        }

        if (!isCancellationToken && !hasFromServices && optionAttribute is null && argumentAttribute is null)
        {
            return ParameterAnalysisResult.FromDiagnostic(
                new OperationDiagnostic(
                    UnsupportedParameterBindingDescriptor,
                    GetBestLocation(parameterSymbol),
                    [parameterSymbol.Name, operationName]));
        }

        ImmutableArray<string>? aliases = GetAliases(parameterSymbol);
        string parameterTypeName = parameterSymbol.Type.ToDisplayString(FullyQualifiedTypeFormat);
        string? cliName = GetSingleName(parameterSymbol, CliNameAttributeMetadataName);
        string? mcpName = GetSingleName(parameterSymbol, McpNameAttributeMetadataName);

        if (isCancellationToken)
            return ParameterAnalysisResult.FromCandidate(
                new ParameterCandidate(parameterSymbol.Name, parameterTypeName, ParameterSourceCandidate.CancellationToken, false, null, null, aliases, cliName, mcpName));

        if (hasFromServices)
            return ParameterAnalysisResult.FromCandidate(
                new ParameterCandidate(parameterSymbol.Name, parameterTypeName, ParameterSourceCandidate.Service, false, null, null, aliases, cliName, mcpName));

        if (optionAttribute is not null)
        {
            return ParameterAnalysisResult.FromCandidate(new ParameterCandidate(
                (string)optionAttribute.ConstructorArguments[0].Value!,
                parameterTypeName,
                ParameterSourceCandidate.Option,
                GetNamedBoolean(optionAttribute, "Required", true),
                null,
                GetNamedString(optionAttribute, "Description"),
                aliases,
                cliName,
                mcpName));
        }

        return ParameterAnalysisResult.FromCandidate(new ParameterCandidate(
            GetNamedString(argumentAttribute!, "Name") ?? parameterSymbol.Name,
            parameterTypeName,
            ParameterSourceCandidate.Argument,
            GetNamedBoolean(argumentAttribute!, "Required", true),
            (int)argumentAttribute!.ConstructorArguments[0].Value!,
            GetNamedString(argumentAttribute!, "Description"),
            aliases,
            cliName,
            mcpName));
    }

    private static ParameterAnalysisResult CreatePropertyCandidate(
        string operationName,
        INamedTypeSymbol requestTypeSymbol,
        IPropertySymbol propertySymbol)
    {
        AttributeData? optionAttribute = GetAttribute(propertySymbol, OptionAttributeMetadataName);
        AttributeData? argumentAttribute = GetAttribute(propertySymbol, ArgumentAttributeMetadataName);

        if (optionAttribute is not null && argumentAttribute is not null)
        {
            return ParameterAnalysisResult.FromDiagnostic(
                new OperationDiagnostic(
                    ConflictingParameterBindingDescriptor,
                    GetBestLocation(propertySymbol),
                    [propertySymbol.Name, operationName]));
        }

        if (propertySymbol.SetMethod is null || propertySymbol.SetMethod.DeclaredAccessibility != Accessibility.Public)
        {
            return ParameterAnalysisResult.FromDiagnostic(
                new OperationDiagnostic(
                    UnsupportedRequestPropertyBindingDescriptor,
                    GetBestLocation(propertySymbol),
                    [propertySymbol.Name, requestTypeSymbol.Name, operationName]));
        }

        ImmutableArray<string>? aliases = GetAliases(propertySymbol);
        string parameterTypeName = propertySymbol.Type.ToDisplayString(FullyQualifiedTypeFormat);
        string? cliName = GetSingleName(propertySymbol, CliNameAttributeMetadataName);
        string? mcpName = GetSingleName(propertySymbol, McpNameAttributeMetadataName);

        if (optionAttribute is not null)
        {
            return ParameterAnalysisResult.FromCandidate(new ParameterCandidate(
                (string)optionAttribute.ConstructorArguments[0].Value!,
                parameterTypeName,
                ParameterSourceCandidate.Option,
                GetNamedBoolean(optionAttribute, "Required", true),
                null,
                GetNamedString(optionAttribute, "Description"),
                aliases,
                cliName,
                mcpName,
                propertySymbol.Name));
        }

        return ParameterAnalysisResult.FromCandidate(new ParameterCandidate(
            GetNamedString(argumentAttribute!, "Name") ?? propertySymbol.Name,
            parameterTypeName,
            ParameterSourceCandidate.Argument,
            GetNamedBoolean(argumentAttribute!, "Required", true),
            (int)argumentAttribute!.ConstructorArguments[0].Value!,
            GetNamedString(argumentAttribute!, "Description"),
            aliases,
            cliName,
            mcpName,
            propertySymbol.Name));
    }

    private static OperationVisibilityCandidate GetVisibility(bool hasCliOnly, bool hasMcpOnly)
    {
        return hasCliOnly
            ? OperationVisibilityCandidate.CliOnly
            : hasMcpOnly
                ? OperationVisibilityCandidate.McpOnly
                : OperationVisibilityCandidate.Both;
    }

    private static ImmutableArray<string>? GetCliCommandPath(ISymbol symbol)
    {
        AttributeData? cliCommandAttribute = GetAttribute(symbol, CliCommandAttributeMetadataName);
        if (cliCommandAttribute is null || cliCommandAttribute.ConstructorArguments.Length == 0)
            return null;

        TypedConstant pathArgument = cliCommandAttribute.ConstructorArguments[0];
        if (pathArgument.IsNull || pathArgument.Kind != TypedConstantKind.Array)
            return null;

        ImmutableArray<string>.Builder builder = ImmutableArray.CreateBuilder<string>();
        foreach (TypedConstant value in pathArgument.Values)
        {
            if (value.Value is string segment && !string.IsNullOrWhiteSpace(segment))
                builder.Add(segment.Trim());
        }

        return builder.Count == 0 ? null : builder.ToImmutable();
    }

    private static ImmutableArray<string>? GetAliases(ISymbol symbol)
    {
        List<string> aliases = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (AttributeData aliasAttribute in symbol.GetAttributes().Where(static attribute => IsAttribute(attribute, AliasAttributeMetadataName)))
        {
            if (aliasAttribute.ConstructorArguments.Length == 0)
                continue;

            TypedConstant aliasesArgument = aliasAttribute.ConstructorArguments[0];
            if (aliasesArgument.IsNull || aliasesArgument.Kind != TypedConstantKind.Array)
                continue;

            foreach (TypedConstant value in aliasesArgument.Values)
            {
                if (value.Value is string alias && !string.IsNullOrWhiteSpace(alias))
                {
                    string normalizedAlias = alias.Trim();
                    if (seen.Add(normalizedAlias))
                        aliases.Add(normalizedAlias);
                }
            }
        }

        return aliases.Count == 0 ? null : [.. aliases];
    }

    private static ImmutableArray<ImmutableArray<string>>? GetCliCommandAliases(
        ISymbol symbol,
        ImmutableArray<string>? cliCommandPath)
    {
        ImmutableArray<string>? aliases = GetAliases(symbol);
        if (aliases is null || aliases.Value.IsDefaultOrEmpty || cliCommandPath is null || cliCommandPath.Value.IsDefaultOrEmpty)
            return null;

        List<ImmutableArray<string>> aliasPaths = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        ImmutableArray<string> primaryPath = cliCommandPath.Value;
        foreach (string alias in aliases.Value)
        {
            ImmutableArray<string>? aliasPath = BuildCliAliasPath(primaryPath, alias);
            if (aliasPath is null || aliasPath.Value.IsDefaultOrEmpty)
                continue;

            string key = string.Join("\u001F", aliasPath.Value);
            if (!seen.Add(key))
                continue;

            aliasPaths.Add(aliasPath.Value);
        }

        return aliasPaths.Count == 0 ? null : [.. aliasPaths];
    }

    private static ImmutableArray<string>? BuildCliAliasPath(ImmutableArray<string> primaryPath, string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
            return null;

        string[] segments = alias
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(static segment => segment.Trim())
            .Where(static segment => segment.Length > 0)
            .ToArray();
        if (segments.Length == 0)
            return null;

        if (segments.Length == 1)
        {
            if (primaryPath.Length == 0)
                return [segments[0]];

            ImmutableArray<string>.Builder builder = ImmutableArray.CreateBuilder<string>(primaryPath.Length);
            for (int index = 0; index < primaryPath.Length - 1; index++)
                builder.Add(primaryPath[index]);

            builder.Add(segments[0]);
            return builder.ToImmutable();
        }

        return [.. segments];
    }

    private static ITypeSymbol UnwrapResultType(ITypeSymbol returnType, Compilation compilation)
    {
        if (returnType is INamedTypeSymbol namedType)
        {
            if (namedType.IsGenericTaskLike("System.Threading.Tasks.Task") ||
                namedType.IsGenericTaskLike("System.Threading.Tasks.ValueTask"))
                return namedType.TypeArguments[0];

            if (namedType.IsNonGenericTaskLike("System.Threading.Tasks.Task") ||
                namedType.IsNonGenericTaskLike("System.Threading.Tasks.ValueTask"))
                return compilation.GetSpecialType(SpecialType.System_Void);
        }

        return returnType;
    }

    private static MethodReturnKind GetMethodReturnKind(ITypeSymbol returnType)
    {
        if (returnType is INamedTypeSymbol namedType)
        {
            if (namedType.IsGenericTaskLike("System.Threading.Tasks.Task"))
                return MethodReturnKind.TaskOfT;

            if (namedType.IsGenericTaskLike("System.Threading.Tasks.ValueTask"))
                return MethodReturnKind.ValueTaskOfT;

            if (namedType.IsNonGenericTaskLike("System.Threading.Tasks.Task"))
                return MethodReturnKind.Task;

            if (namedType.IsNonGenericTaskLike("System.Threading.Tasks.ValueTask"))
                return MethodReturnKind.ValueTask;
        }

        return returnType.SpecialType == SpecialType.System_Void
            ? MethodReturnKind.Void
            : MethodReturnKind.Value;
    }

    private static bool IsCancellationToken(ITypeSymbol typeSymbol)
    {
        return typeSymbol is INamedTypeSymbol namedType &&
               string.Equals(namedType.ToDisplayString(), "System.Threading.CancellationToken", StringComparison.Ordinal);
    }

    private static string? GetSingleName(ISymbol symbol, string metadataName)
    {
        AttributeData? attribute = GetAttribute(symbol, metadataName);
        return attribute is null || attribute.ConstructorArguments.Length == 0
            ? null
            : attribute.ConstructorArguments[0].Value as string;
    }

    private static AttributeData? GetAttribute(ISymbol symbol, string metadataName)
    {
        return symbol.GetAttributes().FirstOrDefault(attribute => IsAttribute(attribute, metadataName));
    }

    private static bool HasAttribute(ISymbol symbol, string metadataName)
    {
        return symbol.GetAttributes().Any(attribute => IsAttribute(attribute, metadataName));
    }

    private static bool IsAttribute(AttributeData attribute, string metadataName)
    {
        return string.Equals(attribute.AttributeClass?.ToDisplayString(), metadataName, StringComparison.Ordinal);
    }

    private static Location? GetBestLocation(ISymbol symbol)
    {
        return symbol.Locations.FirstOrDefault(static location => location.IsInSource) ?? symbol.Locations.FirstOrDefault();
    }

    private static string? GetNamedString(AttributeData attribute, string name)
    {
        foreach (KeyValuePair<string, TypedConstant> namedArgument in attribute.NamedArguments)
        {
            if (string.Equals(namedArgument.Key, name, StringComparison.Ordinal))
                return namedArgument.Value.Value as string;
        }

        return null;
    }

    private static bool GetNamedBoolean(AttributeData attribute, string name, bool defaultValue = false)
    {
        foreach (KeyValuePair<string, TypedConstant> namedArgument in attribute.NamedArguments)
        {
            if (string.Equals(namedArgument.Key, name, StringComparison.Ordinal) && namedArgument.Value.Value is bool value)
                return value;
        }

        return defaultValue;
    }

    private static void Execute(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<OperationAnalysisResult> collectedCandidates)
    {
        List<OperationCandidate> operations = [];
        foreach (OperationAnalysisResult analysisResult in collectedCandidates)
        {
            foreach (OperationDiagnostic diagnostic in analysisResult.Diagnostics)
                context.ReportDiagnostic(Diagnostic.Create(diagnostic.Descriptor, diagnostic.Location, diagnostic.MessageArgs));

            if (analysisResult.Candidate is not null)
                operations.Add(analysisResult.Candidate);
        }

        operations.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.OperationId, right.OperationId));

        if (operations.Count == 0)
            return;

        context.AddSource(
            "GeneratedOperationRegistry.g.cs",
            SourceText.From(GenerateSource(operations), Encoding.UTF8));

        if (compilation.GetTypeByMetadataName("DalamudMCP.Framework.IOperationInvoker") is not null &&
            compilation.GetTypeByMetadataName("DalamudMCP.Framework.OperationInvocationResult") is not null &&
            compilation.GetTypeByMetadataName("DalamudMCP.Framework.OperationBinding") is not null)
        {
            context.AddSource(
                "GeneratedOperationInvoker.g.cs",
                SourceText.From(GenerateOperationInvokerSource(operations), Encoding.UTF8));
        }

        if (compilation.GetTypeByMetadataName("DalamudMCP.Framework.Cli.ICliInvoker") is not null &&
            compilation.GetTypeByMetadataName("DalamudMCP.Framework.Cli.CliInvocationResult") is not null)
        {
            context.AddSource(
                "GeneratedCliInvoker.g.cs",
                SourceText.From(GenerateCliInvokerSource(operations), Encoding.UTF8));
        }

        if (compilation.GetTypeByMetadataName("DalamudMCP.Framework.Mcp.McpBinding") is not null &&
            compilation.GetTypeByMetadataName("ModelContextProtocol.Server.McpServerToolTypeAttribute") is not null &&
            compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.McpServerBuilderExtensions") is not null)
        {
            context.AddSource(
                "GeneratedMcpTools.g.cs",
                SourceText.From(GenerateMcpToolsSource(operations), Encoding.UTF8));
        }
    }

    private static string GenerateSource(IReadOnlyList<OperationCandidate> operations)
    {
        StringBuilder builder = new();
        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#nullable enable");
        builder.AppendLine("namespace DalamudMCP.Framework.Generated;");
        builder.AppendLine();
        builder.AppendLine("public static class GeneratedOperationRegistry");
        builder.AppendLine("{");
        builder.AppendLine("    private static readonly global::DalamudMCP.Framework.OperationDescriptor[] operations =");
        builder.AppendLine("    [");
        foreach (OperationCandidate operation in operations)
            AppendOperation(builder, operation);

        builder.AppendLine("    ];");
        builder.AppendLine();
        builder.AppendLine("    public static global::System.Collections.Generic.IReadOnlyList<global::DalamudMCP.Framework.OperationDescriptor> Operations => operations;");
        builder.AppendLine();
        builder.AppendLine("    public static bool TryFind(string operationId, out global::DalamudMCP.Framework.OperationDescriptor? descriptor)");
        builder.AppendLine("    {");
        builder.AppendLine("        foreach (global::DalamudMCP.Framework.OperationDescriptor operation in operations)");
        builder.AppendLine("        {");
        builder.AppendLine("            if (global::System.String.Equals(operation.OperationId, operationId, global::System.StringComparison.Ordinal))");
        builder.AppendLine("            {");
        builder.AppendLine("                descriptor = operation;");
        builder.AppendLine("                return true;");
        builder.AppendLine("            }");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        descriptor = null;");
        builder.AppendLine("        return false;");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string GenerateOperationInvokerSource(IReadOnlyList<OperationCandidate> operations)
    {
        StringBuilder builder = new();
        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#nullable enable");
        builder.AppendLine("namespace DalamudMCP.Framework.Generated;");
        builder.AppendLine();
        builder.AppendLine("public sealed class GeneratedOperationInvoker : global::DalamudMCP.Framework.IOperationInvoker");
        builder.AppendLine("{");
        builder.AppendLine("    public bool TryInvoke(");
        builder.AppendLine("        string operationId,");
        builder.AppendLine("        object? request,");
        builder.AppendLine("        global::System.IServiceProvider? services,");
        builder.AppendLine("        global::DalamudMCP.Framework.InvocationSurface surface,");
        builder.AppendLine("        global::System.Threading.CancellationToken cancellationToken,");
        builder.AppendLine("        out global::System.Threading.Tasks.ValueTask<global::DalamudMCP.Framework.OperationInvocationResult> invocation)");
        builder.AppendLine("    {");
        foreach (OperationCandidate operation in operations.Where(static operation => operation.InvocationKind == InvocationKind.InstanceOperation))
        {
            builder.Append("        if (global::System.String.Equals(operationId, ")
                .Append(ToLiteral(operation.OperationId))
                .AppendLine(", global::System.StringComparison.Ordinal))");
            builder.AppendLine("        {");
            builder.Append("            invocation = Invoke")
                .Append(GetOperationMethodBaseName(operation.OperationId))
                .AppendLine("Async(request, services, surface, cancellationToken);");
            builder.AppendLine("            return true;");
            builder.AppendLine("        }");
        }

        builder.AppendLine();
        builder.AppendLine("        invocation = default;");
        builder.AppendLine("        return false;");
        builder.AppendLine("    }");
        builder.AppendLine();

        foreach (OperationCandidate operation in operations.Where(static operation => operation.InvocationKind == InvocationKind.InstanceOperation))
            AppendOperationInvoker(builder, operation);

        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string GenerateCliInvokerSource(IReadOnlyList<OperationCandidate> operations)
    {
        StringBuilder builder = new();
        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#nullable enable");
        builder.AppendLine("namespace DalamudMCP.Framework.Generated;");
        builder.AppendLine();
        builder.AppendLine("public sealed class GeneratedCliInvoker : global::DalamudMCP.Framework.Cli.ICliInvoker");
        builder.AppendLine("{");
        builder.AppendLine("    public bool TryInvoke(");
        builder.AppendLine("        string operationId,");
        builder.AppendLine("        global::System.Collections.Generic.IReadOnlyDictionary<string, string> options,");
        builder.AppendLine("        global::System.Collections.Generic.IReadOnlyList<string> arguments,");
        builder.AppendLine("        global::System.IServiceProvider? services,");
        builder.AppendLine("        bool jsonRequested,");
        builder.AppendLine("        global::System.Threading.CancellationToken cancellationToken,");
        builder.AppendLine("        out global::System.Threading.Tasks.ValueTask<global::DalamudMCP.Framework.Cli.CliInvocationResult> invocation)");
        builder.AppendLine("    {");
        foreach (OperationCandidate operation in operations.Where(static operation => operation.Visibility is not OperationVisibilityCandidate.McpOnly))
        {
            builder.Append("        if (global::System.String.Equals(operationId, ")
                .Append(ToLiteral(operation.OperationId))
                .AppendLine(", global::System.StringComparison.Ordinal))");
            builder.AppendLine("        {");
            builder.Append("            invocation = Invoke")
                .Append(GetOperationMethodBaseName(operation.OperationId))
                .AppendLine("Async(options, arguments, services, cancellationToken);");
            builder.AppendLine("            return true;");
            builder.AppendLine("        }");
        }

        builder.AppendLine();
        builder.AppendLine("        invocation = default;");
        builder.AppendLine("        return false;");
        builder.AppendLine("    }");
        builder.AppendLine();

        foreach (OperationCandidate operation in operations.Where(static operation => operation.Visibility is not OperationVisibilityCandidate.McpOnly))
            AppendCliInvoker(builder, operation);

        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void AppendOperationInvoker(StringBuilder builder, OperationCandidate operation)
    {
        string methodBaseName = GetOperationMethodBaseName(operation.OperationId);
        builder.Append("    private static ");
        builder.Append(operation.ReturnKind is MethodReturnKind.TaskOfT or MethodReturnKind.ValueTaskOfT or MethodReturnKind.Task or MethodReturnKind.ValueTask
            ? "async "
            : string.Empty);
        builder.AppendLine("global::System.Threading.Tasks.ValueTask<global::DalamudMCP.Framework.OperationInvocationResult> Invoke" + methodBaseName + "Async(");
        builder.AppendLine("        object? request,");
        builder.AppendLine("        global::System.IServiceProvider? services,");
        builder.AppendLine("        global::DalamudMCP.Framework.InvocationSurface surface,");
        builder.AppendLine("        global::System.Threading.CancellationToken cancellationToken)");
        builder.AppendLine("    {");

        if (string.IsNullOrWhiteSpace(operation.RequestTypeName))
            throw new InvalidOperationException("Generated operation invoker requires a request type.");

        builder.Append("        ").Append(operation.RequestTypeName).AppendLine(" __request;");
        builder.AppendLine("        switch (request)");
        builder.AppendLine("        {");
        builder.AppendLine("            case null:");
        builder.Append("                __request = new ").Append(operation.RequestTypeName).AppendLine("();");
        builder.AppendLine("                break;");
        builder.Append("            case ").Append(operation.RequestTypeName).AppendLine(" typedRequest:");
        builder.AppendLine("                __request = typedRequest;");
        builder.AppendLine("                break;");
        builder.AppendLine("            default:");
        builder.Append("                throw new global::System.ArgumentException(")
            .Append(ToLiteral(
                $"Operation '{operation.OperationId}' expected a request instance of type '{operation.RequestTypeName}'."))
            .AppendLine(");");
        builder.AppendLine("        }");

        builder.Append("        ").Append(operation.DeclaringTypeName).Append(" __operation = global::DalamudMCP.Framework.OperationBinding.GetRequiredService<")
            .Append(operation.DeclaringTypeName).AppendLine(">(services);");
        string invocationExpression = "__operation." + operation.MethodName + "(__request, new global::DalamudMCP.Framework.OperationContext(" +
                                   ToLiteral(operation.OperationId) +
                                   ", surface, services, cancellationToken: cancellationToken))";

        switch (operation.ReturnKind)
        {
            case MethodReturnKind.Value:
                builder.Append("        ").Append(operation.ResultTypeName).Append(" result = ").Append(invocationExpression).AppendLine(";");
                AppendOperationResultReturn(builder, operation, wrapInValueTask: true);
                break;
            case MethodReturnKind.TaskOfT:
            case MethodReturnKind.ValueTaskOfT:
                builder.Append("        ").Append(operation.ResultTypeName).Append(" result = await ").Append(invocationExpression)
                    .AppendLine(".ConfigureAwait(false);");
                AppendOperationResultReturn(builder, operation, wrapInValueTask: false);
                break;
            default:
                throw new InvalidOperationException("Generated operation invoker only supports operations with results.");
        }

        builder.AppendLine("    }");
        builder.AppendLine();
    }

    private static string GenerateMcpToolsSource(IReadOnlyList<OperationCandidate> operations)
    {
        StringBuilder builder = new();
        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#nullable enable");
        builder.AppendLine("namespace DalamudMCP.Framework.Generated;");
        builder.AppendLine();
        builder.AppendLine("[global::ModelContextProtocol.Server.McpServerToolType]");
        builder.AppendLine("public sealed class GeneratedMcpTools");
        builder.AppendLine("{");
        builder.AppendLine("    private readonly global::System.IServiceProvider? services;");
        builder.AppendLine();
        builder.AppendLine("    public GeneratedMcpTools(global::System.IServiceProvider? services = null)");
        builder.AppendLine("    {");
        builder.AppendLine("        this.services = services;");
        builder.AppendLine("    }");
        builder.AppendLine();
        foreach (OperationCandidate operation in operations.Where(static operation =>
                     operation.Visibility is not OperationVisibilityCandidate.CliOnly &&
                     !string.IsNullOrWhiteSpace(operation.McpToolName)))
        {
            AppendMcpToolMethod(builder, operation);
        }

        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("public static class GeneratedMcpServiceCollectionExtensions");
        builder.AppendLine("{");
        builder.AppendLine("    public static global::Microsoft.Extensions.DependencyInjection.IMcpServerBuilder AddGeneratedMcpServer(");
        builder.AppendLine("        this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        builder.AppendLine("    {");
        builder.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(services);");
        builder.AppendLine();
        builder.AppendLine("        global::Microsoft.Extensions.DependencyInjection.IMcpServerBuilder builder =");
        builder.AppendLine("            global::Microsoft.Extensions.DependencyInjection.McpServerServiceCollectionExtensions.AddMcpServer(services, static _ => { });");
        builder.AppendLine("        return global::Microsoft.Extensions.DependencyInjection.McpServerBuilderExtensions.WithTools<GeneratedMcpTools>(builder);");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void AppendCliInvoker(StringBuilder builder, OperationCandidate operation)
    {
        string methodBaseName = GetOperationMethodBaseName(operation.OperationId);
        builder.Append("    private static ");
        builder.Append(operation.ReturnKind is MethodReturnKind.TaskOfT or MethodReturnKind.ValueTaskOfT or MethodReturnKind.Task or MethodReturnKind.ValueTask
            ? "async "
            : string.Empty);
        builder.AppendLine("global::System.Threading.Tasks.ValueTask<global::DalamudMCP.Framework.Cli.CliInvocationResult> Invoke" + methodBaseName + "Async(");
        builder.AppendLine("        global::System.Collections.Generic.IReadOnlyDictionary<string, string> options,");
        builder.AppendLine("        global::System.Collections.Generic.IReadOnlyList<string> arguments,");
        builder.AppendLine("        global::System.IServiceProvider? services,");
        builder.AppendLine("        global::System.Threading.CancellationToken cancellationToken)");
        builder.AppendLine("    {");

        foreach (ParameterCandidate parameter in operation.Parameters)
            AppendCliParameterBinding(builder, parameter);

        string invocationExpression;
        if (operation.InvocationKind == InvocationKind.InstanceOperation)
        {
            AppendRequestBinding(builder, operation, "__request", static parameter => GetBoundVariableName(parameter));
            builder.Append("        ").Append(operation.DeclaringTypeName).Append(" __operation = global::DalamudMCP.Framework.Cli.CliBinding.GetRequiredServiceOrThrow<")
                .Append(operation.DeclaringTypeName).AppendLine(">(services);");
            invocationExpression = "__operation." + operation.MethodName + "(__request, global::DalamudMCP.Framework.OperationContext.ForCli(" +
                                   ToLiteral(operation.OperationId) +
                                   ", services, cancellationToken: cancellationToken))";
        }
        else
        {
            invocationExpression = operation.DeclaringTypeName + "." + operation.MethodName + "(" +
                                   string.Join(", ", operation.Parameters.Select(static parameter => GetBoundVariableName(parameter))) + ")";
        }

        switch (operation.ReturnKind)
        {
            case MethodReturnKind.Void:
                builder.Append("        ").Append(invocationExpression).AppendLine(";");
                builder.Append("        return global::System.Threading.Tasks.ValueTask.FromResult(new global::DalamudMCP.Framework.Cli.CliInvocationResult(null, typeof(")
                    .Append(operation.ResultTypeName).AppendLine("), null));");
                break;
            case MethodReturnKind.Value:
                builder.Append("        ").Append(operation.ResultTypeName).Append(" result = ").Append(invocationExpression).AppendLine(";");
                AppendCliResultReturn(builder, operation);
                break;
            case MethodReturnKind.Task:
                builder.Append("        await ").Append(invocationExpression).AppendLine(".ConfigureAwait(false);");
                builder.Append("        return new global::DalamudMCP.Framework.Cli.CliInvocationResult(null, typeof(")
                    .Append(operation.ResultTypeName).AppendLine("), null);");
                break;
            case MethodReturnKind.TaskOfT:
            case MethodReturnKind.ValueTaskOfT:
                builder.Append("        ").Append(operation.ResultTypeName).Append(" result = await ").Append(invocationExpression)
                    .AppendLine(".ConfigureAwait(false);");
                AppendCliResultReturn(builder, operation);
                break;
            case MethodReturnKind.ValueTask:
                builder.Append("        await ").Append(invocationExpression).AppendLine(".ConfigureAwait(false);");
                builder.Append("        return new global::DalamudMCP.Framework.Cli.CliInvocationResult(null, typeof(")
                    .Append(operation.ResultTypeName).AppendLine("), null);");
                break;
            default:
                throw new InvalidOperationException("Unsupported return kind.");
        }

        builder.AppendLine("    }");
        builder.AppendLine();
    }

    private static void AppendMcpToolMethod(StringBuilder builder, OperationCandidate operation)
    {
        string methodBaseName = GetOperationMethodBaseName(operation.OperationId);
        builder.Append("    [global::ModelContextProtocol.Server.McpServerTool(Name = ")
            .Append(ToLiteral(operation.McpToolName!))
            .AppendLine(")]");
        if (!string.IsNullOrWhiteSpace(operation.Description))
        {
            builder.Append("    [global::System.ComponentModel.Description(")
                .Append(ToLiteral(operation.Description!))
                .AppendLine(")]");
        }

        builder.Append("    public ");
        builder.Append(GetGeneratedMcpReturnType(operation));
        builder.Append(' ');
        builder.Append(methodBaseName);
        builder.Append("Async(");
        AppendMcpParameterList(builder, operation);
        builder.AppendLine(")");
        builder.AppendLine("    {");

        foreach (ParameterCandidate parameter in operation.Parameters.Where(static parameter =>
                     parameter.Source == ParameterSourceCandidate.Service))
        {
            builder.Append("        ")
                .Append(parameter.ParameterTypeName)
                .Append(' ')
                .Append(GetBoundVariableName(parameter))
                .Append(" = global::DalamudMCP.Framework.Mcp.McpBinding.GetRequiredService<")
                .Append(parameter.ParameterTypeName)
                .AppendLine(">(services);");
        }

        string invocationExpression;
        if (operation.InvocationKind == InvocationKind.InstanceOperation)
        {
            AppendRequestBinding(builder, operation, "__request", static parameter => GetInvocationArgumentName(parameter));
            builder.Append("        ").Append(operation.DeclaringTypeName).Append(" __operation = global::DalamudMCP.Framework.Mcp.McpBinding.GetRequiredServiceOrThrow<")
                .Append(operation.DeclaringTypeName).AppendLine(">(services);");
            string cancellationTokenExpression = operation.Parameters.FirstOrDefault(static parameter =>
                    parameter.Source == ParameterSourceCandidate.CancellationToken) is { } cancellationTokenParameter
                ? GetMcpParameterName(cancellationTokenParameter)
                : "global::System.Threading.CancellationToken.None";
            invocationExpression = "__operation." + operation.MethodName + "(__request, global::DalamudMCP.Framework.OperationContext.ForMcp(" +
                                   ToLiteral(operation.OperationId) +
                                   ", services, cancellationToken: " + cancellationTokenExpression + "))";
        }
        else
        {
            invocationExpression = operation.DeclaringTypeName + "." + operation.MethodName + "(" +
                                   string.Join(", ", operation.Parameters.Select(static parameter => GetInvocationArgumentName(parameter))) + ")";
        }
        switch (operation.ReturnKind)
        {
            case MethodReturnKind.Void:
                builder.Append("        ").Append(invocationExpression).AppendLine(";");
                builder.AppendLine("        return global::System.Threading.Tasks.Task.CompletedTask;");
                break;
            case MethodReturnKind.Value:
                builder.Append("        return global::System.Threading.Tasks.Task.FromResult(")
                    .Append(invocationExpression)
                    .AppendLine(");");
                break;
            case MethodReturnKind.Task:
            case MethodReturnKind.TaskOfT:
                builder.Append("        return ").Append(invocationExpression).AppendLine(";");
                break;
            case MethodReturnKind.ValueTask:
                builder.Append("        return ").Append(invocationExpression).AppendLine(".AsTask();");
                break;
            case MethodReturnKind.ValueTaskOfT:
                builder.Append("        return ").Append(invocationExpression).AppendLine(".AsTask();");
                break;
            default:
                throw new InvalidOperationException("Unsupported MCP return kind.");
        }

        builder.AppendLine("    }");
        builder.AppendLine();
    }

    private static void AppendRequestBinding(
        StringBuilder builder,
        OperationCandidate operation,
        string requestVariableName,
        Func<ParameterCandidate, string> valueSelector)
    {
        if (string.IsNullOrWhiteSpace(operation.RequestTypeName))
            throw new InvalidOperationException("Request binding is only valid for instance operations.");
        if (valueSelector is null)
            throw new ArgumentNullException(nameof(valueSelector));

        IReadOnlyList<ParameterCandidate> requestParameters = operation.Parameters
            .Where(static parameter => parameter.Source is ParameterSourceCandidate.Option or ParameterSourceCandidate.Argument)
            .ToArray();

        builder.Append("        ").Append(operation.RequestTypeName).Append(' ').Append(requestVariableName).Append(" = new ")
            .Append(operation.RequestTypeName);
        if (requestParameters.Count == 0)
        {
            builder.AppendLine("();");
            return;
        }

        builder.AppendLine();
        builder.AppendLine("        {");
        foreach (ParameterCandidate parameter in requestParameters)
        {
            builder.Append("            ")
                .Append(parameter.RequestPropertyName ?? parameter.Name)
                .Append(" = ")
                .Append(valueSelector(parameter))
                .AppendLine(",");
        }

        builder.AppendLine("        };");
    }

    private static string GetGeneratedMcpReturnType(OperationCandidate operation)
    {
        return operation.ReturnKind switch
        {
            MethodReturnKind.Void => "global::System.Threading.Tasks.Task",
            MethodReturnKind.Value => "global::System.Threading.Tasks.Task<" + operation.ResultTypeName + ">",
            MethodReturnKind.Task => "global::System.Threading.Tasks.Task",
            MethodReturnKind.TaskOfT => operation.MethodReturnTypeName,
            MethodReturnKind.ValueTask => "global::System.Threading.Tasks.Task",
            MethodReturnKind.ValueTaskOfT => "global::System.Threading.Tasks.Task<" + operation.ResultTypeName + ">",
            _ => throw new InvalidOperationException("Unsupported MCP return kind.")
        };
    }

    private static void AppendMcpParameterList(StringBuilder builder, OperationCandidate operation)
    {
        bool first = true;
        foreach (ParameterCandidate parameter in operation.Parameters.Where(static parameter =>
                     parameter.Source is ParameterSourceCandidate.Option or ParameterSourceCandidate.Argument))
        {
            if (!first)
                builder.Append(", ");

            string mcpParameterName = parameter.McpName ?? parameter.Name;
            if (!string.IsNullOrWhiteSpace(parameter.Description))
            {
                builder.Append("[global::System.ComponentModel.Description(")
                    .Append(ToLiteral(parameter.Description!))
                    .Append(")] ");
            }

            string identifier = GetMcpParameterName(parameter);
            if (!string.Equals(identifier, mcpParameterName, StringComparison.Ordinal))
            {
                builder.Append("[global::System.Text.Json.Serialization.JsonPropertyName(")
                    .Append(ToLiteral(mcpParameterName))
                    .Append(")] ");
            }

            builder.Append(parameter.ParameterTypeName)
                .Append(' ')
                .Append(identifier);
            first = false;
        }

        ParameterCandidate? cancellationTokenParameter = operation.Parameters.FirstOrDefault(static parameter =>
            parameter.Source == ParameterSourceCandidate.CancellationToken);
        if (cancellationTokenParameter is not null)
        {
            if (!first)
                builder.Append(", ");

            builder.Append("global::System.Threading.CancellationToken ")
                .Append(GetMcpParameterName(cancellationTokenParameter))
                .Append(" = default");
        }
    }

    private static void AppendCliResultReturn(StringBuilder builder, OperationCandidate operation)
    {
        if (!string.IsNullOrWhiteSpace(operation.FormatterTypeName))
        {
            builder.Append("        ").Append(operation.FormatterTypeName).Append(" formatter = global::DalamudMCP.Framework.Cli.CliBinding.GetRequiredServiceOrThrow<")
                .Append(operation.FormatterTypeName).AppendLine(">(services);");
            builder.Append("        string? text = formatter.FormatText(result, global::DalamudMCP.Framework.OperationContext.ForCli(")
                .Append(ToLiteral(operation.OperationId))
                .AppendLine(", services, cancellationToken: cancellationToken));");
        }
        else
        {
            builder.AppendLine("        string? text = global::DalamudMCP.Framework.Cli.CliBinding.FormatDefaultText(result);");
        }

        builder.Append("        return ");
        if (operation.ReturnKind is MethodReturnKind.Value)
            builder.Append("global::System.Threading.Tasks.ValueTask.FromResult(");
        builder.Append("new global::DalamudMCP.Framework.Cli.CliInvocationResult(result, typeof(")
            .Append(operation.ResultTypeName)
            .Append("), text)");
        if (operation.ReturnKind is MethodReturnKind.Value)
            builder.Append(')');
        builder.AppendLine(";");
    }

    private static void AppendOperationResultReturn(StringBuilder builder, OperationCandidate operation, bool wrapInValueTask)
    {
        if (!string.IsNullOrWhiteSpace(operation.FormatterTypeName))
        {
            builder.Append("        ").Append(operation.FormatterTypeName).Append(" formatter = global::DalamudMCP.Framework.OperationBinding.GetRequiredService<")
                .Append(operation.FormatterTypeName).AppendLine(">(services);");
            builder.Append("        string? displayText = formatter.FormatText(result, new global::DalamudMCP.Framework.OperationContext(")
                .Append(ToLiteral(operation.OperationId))
                .AppendLine(", surface, services, cancellationToken: cancellationToken));");
        }
        else
        {
            builder.AppendLine("        string? displayText = null;");
        }

        builder.Append("        return ");
        if (wrapInValueTask)
            builder.Append("global::System.Threading.Tasks.ValueTask.FromResult(");
        builder.Append("new global::DalamudMCP.Framework.OperationInvocationResult(result, typeof(")
            .Append(operation.ResultTypeName)
            .Append("), displayText)");
        if (wrapInValueTask)
            builder.Append(')');
        builder.AppendLine(";");
    }

    private static void AppendCliParameterBinding(StringBuilder builder, ParameterCandidate parameter)
    {
        string boundName = GetBoundVariableName(parameter);
        switch (parameter.Source)
        {
            case ParameterSourceCandidate.CancellationToken:
                builder.Append("        ").Append(parameter.ParameterTypeName).Append(' ').Append(boundName)
                    .AppendLine(" = cancellationToken;");
                break;
            case ParameterSourceCandidate.Service:
                builder.Append("        ").Append(parameter.ParameterTypeName).Append(' ').Append(boundName)
                    .Append(" = global::DalamudMCP.Framework.Cli.CliBinding.GetRequiredService<").Append(parameter.ParameterTypeName)
                    .AppendLine(">(services);");
                break;
            case ParameterSourceCandidate.Argument:
                if (parameter.Required)
                {
                    string cliDisplayName = parameter.CliName ?? parameter.Name;
                    builder.Append("        ").Append(parameter.ParameterTypeName).Append(' ').Append(boundName)
                        .Append(" = (").Append(parameter.ParameterTypeName).Append(")global::DalamudMCP.Framework.Cli.CliBinding.ConvertValue(typeof(")
                        .Append(parameter.ParameterTypeName).Append("), global::DalamudMCP.Framework.Cli.CliBinding.GetRequiredArgument(arguments, ")
                        .Append(parameter.Position!.Value.ToString(CultureInfo.InvariantCulture)).Append(", ")
                        .Append(ToLiteral(cliDisplayName)).Append("), ").Append(ToLiteral(cliDisplayName)).AppendLine(")!;");
                }
                else
                {
                    string cliDisplayName = parameter.CliName ?? parameter.Name;
                    builder.Append("        ").Append(parameter.ParameterTypeName).Append(' ').Append(boundName).Append(" = arguments.Count > ")
                        .Append(parameter.Position!.Value.ToString(CultureInfo.InvariantCulture))
                        .Append(" ? (").Append(parameter.ParameterTypeName)
                        .Append(")global::DalamudMCP.Framework.Cli.CliBinding.ConvertValue(typeof(").Append(parameter.ParameterTypeName)
                        .Append("), arguments[").Append(parameter.Position!.Value.ToString(CultureInfo.InvariantCulture)).Append("], ")
                        .Append(ToLiteral(cliDisplayName)).AppendLine(")! : default!;");
                }

                break;
            case ParameterSourceCandidate.Option:
                string cliOptionName = parameter.CliName ?? parameter.Name;
                builder.Append("        ").Append(parameter.ParameterTypeName).Append(' ').Append(boundName);
                if (parameter.Required)
                {
                    builder.Append(" = global::DalamudMCP.Framework.Cli.CliBinding.TryFindOptionValue(options, ")
                        .Append(ToLiteral(cliOptionName)).Append(", ")
                        .Append(GetStringArrayExpression(parameter.Aliases))
                        .Append(", out string? ").Append(boundName).Append("Text) ? (")
                        .Append(parameter.ParameterTypeName)
                        .Append(")global::DalamudMCP.Framework.Cli.CliBinding.ConvertValue(typeof(").Append(parameter.ParameterTypeName)
                        .Append("), ").Append(boundName).Append("Text!, ").Append(ToLiteral(cliOptionName))
                        .Append(")! : throw new global::System.ArgumentException(")
                        .Append(ToLiteral($"Missing required --{cliOptionName} option.")).AppendLine(");");
                }
                else
                {
                    builder.Append(" = global::DalamudMCP.Framework.Cli.CliBinding.TryFindOptionValue(options, ")
                        .Append(ToLiteral(cliOptionName)).Append(", ")
                        .Append(GetStringArrayExpression(parameter.Aliases))
                        .Append(", out string? ").Append(boundName).Append("Text) ? (")
                        .Append(parameter.ParameterTypeName)
                        .Append(")global::DalamudMCP.Framework.Cli.CliBinding.ConvertValue(typeof(").Append(parameter.ParameterTypeName)
                        .Append("), ").Append(boundName).Append("Text!, ").Append(ToLiteral(cliOptionName))
                        .AppendLine(") : default;");
                }

                break;
            default:
                throw new InvalidOperationException("Unsupported CLI parameter source.");
        }
    }

    private static string GetOperationMethodBaseName(string operationId)
    {
        StringBuilder builder = new();
        bool upperNext = true;
        foreach (char character in operationId)
        {
            if (!char.IsLetterOrDigit(character))
            {
                upperNext = true;
                continue;
            }

            builder.Append(upperNext ? char.ToUpperInvariant(character) : character);
            upperNext = false;
        }

        return builder.ToString();
    }

    private static string GetBoundVariableName(ParameterCandidate parameter)
    {
        StringBuilder builder = new("__uops_");
        foreach (char character in parameter.Name)
            builder.Append(char.IsLetterOrDigit(character) ? character : '_');

        return builder.ToString();
    }

    private static string GetInvocationArgumentName(ParameterCandidate parameter)
    {
        return parameter.Source == ParameterSourceCandidate.Service
            ? GetBoundVariableName(parameter)
            : GetMcpParameterName(parameter);
    }

    private static string GetMcpParameterName(ParameterCandidate parameter)
    {
        string? candidate = string.IsNullOrWhiteSpace(parameter.McpName) ? parameter.Name : parameter.McpName;
        if (string.IsNullOrWhiteSpace(candidate))
            candidate = "value";
        else
            candidate = candidate!.Trim();
        string normalizedCandidate = candidate;
        StringBuilder builder = new();
        foreach (char character in normalizedCandidate)
            builder.Append(char.IsLetterOrDigit(character) ? character : '_');

        if (builder.Length == 0)
            builder.Append("value");

        if (!char.IsLetter(builder[0]) && builder[0] != '_')
            builder.Insert(0, '_');

        return builder.ToString();
    }

    private static void AppendOperation(StringBuilder builder, OperationCandidate operation)
    {
        builder.AppendLine("        new global::DalamudMCP.Framework.OperationDescriptor(");
        builder.Append("            ").Append(ToLiteral(operation.OperationId)).AppendLine(",");
        builder.Append("            typeof(").Append(operation.DeclaringTypeName).AppendLine("),");
        builder.Append("            ").Append(ToLiteral(operation.MethodName)).AppendLine(",");
        builder.Append("            typeof(").Append(operation.ResultTypeName).AppendLine("),");
        builder.Append("            global::DalamudMCP.Framework.OperationVisibility.").Append(operation.Visibility).AppendLine(",");
        builder.AppendLine("            new global::DalamudMCP.Framework.ParameterDescriptor[]");
        builder.AppendLine("            {");
        foreach (ParameterCandidate parameter in operation.Parameters)
            AppendParameter(builder, parameter);

        builder.AppendLine("            },");
        builder.Append("            ").Append(ToLiteralOrNull(operation.Description)).AppendLine(",");
        builder.Append("            ").Append(ToLiteralOrNull(operation.Summary)).AppendLine(",");
        builder.Append("            ").Append(GetStringArrayExpression(operation.CliCommandPath)).AppendLine(",");
        builder.Append("            ").Append(GetNestedStringArrayExpression(operation.CliCommandAliases)).AppendLine(",");
        builder.Append("            ").Append(ToLiteralOrNull(operation.McpToolName)).AppendLine(",");
        builder.Append("            ").Append(operation.Hidden ? "true" : "false").AppendLine(",");
        builder.Append("            ").Append(string.IsNullOrWhiteSpace(operation.RequestTypeName) ? "null" : "typeof(" + operation.RequestTypeName + ")").AppendLine("),");
    }

    private static void AppendParameter(StringBuilder builder, ParameterCandidate parameter)
    {
        builder.AppendLine("                new global::DalamudMCP.Framework.ParameterDescriptor(");
        builder.Append("                    ").Append(ToLiteral(parameter.Name)).AppendLine(",");
        builder.Append("                    typeof(").Append(parameter.ParameterTypeName).AppendLine("),");
        builder.Append("                    global::DalamudMCP.Framework.ParameterSource.").Append(parameter.Source).AppendLine(",");
        builder.Append("                    ").Append(parameter.Required ? "true" : "false").AppendLine(",");
        builder.Append("                    ").Append(parameter.Position?.ToString(CultureInfo.InvariantCulture) ?? "null").AppendLine(",");
        builder.Append("                    ").Append(ToLiteralOrNull(parameter.Description)).AppendLine(",");
        builder.Append("                    ").Append(GetStringArrayExpression(parameter.Aliases)).AppendLine(",");
        builder.Append("                    ").Append(ToLiteralOrNull(parameter.CliName)).AppendLine(",");
        builder.Append("                    ").Append(ToLiteralOrNull(parameter.McpName)).AppendLine(",");
        builder.Append("                    ").Append(ToLiteralOrNull(parameter.RequestPropertyName)).AppendLine("),");
    }

    private static string GetStringArrayExpression(ImmutableArray<string>? values)
    {
        return values is null || values.Value.IsDefaultOrEmpty
            ? "null"
            : "new string[] { " + string.Join(", ", values.Value.Select(ToLiteral)) + " }";
    }

    private static string GetNestedStringArrayExpression(ImmutableArray<ImmutableArray<string>>? values)
    {
        return values is null || values.Value.IsDefaultOrEmpty
            ? "null"
            : "new global::System.Collections.Generic.IReadOnlyList<string>[] { " +
              string.Join(", ", values.Value.Select(static value => "new string[] { " + string.Join(", ", value.Select(ToLiteral)) + " }")) +
              " }";
    }

    private static string ToLiteral(string value)
    {
        return "\"" + value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n") + "\"";
    }

    private static string ToLiteralOrNull(string? value)
    {
        return value is null ? "null" : ToLiteral(value);
    }

    private sealed class OperationCandidate
    {
        public OperationCandidate(
            string operationId,
            string declaringTypeName,
            string methodName,
            string methodReturnTypeName,
            string resultTypeName,
            MethodReturnKind returnKind,
            OperationVisibilityCandidate visibility,
            ImmutableArray<ParameterCandidate> parameters,
            string? description,
            string? summary,
            ImmutableArray<string>? cliCommandPath,
            ImmutableArray<ImmutableArray<string>>? cliCommandAliases,
            string? mcpToolName,
            string? formatterTypeName,
            bool hidden,
            InvocationKind invocationKind,
            string? requestTypeName)
        {
            OperationId = operationId;
            DeclaringTypeName = declaringTypeName;
            MethodName = methodName;
            MethodReturnTypeName = methodReturnTypeName;
            ResultTypeName = resultTypeName;
            ReturnKind = returnKind;
            Visibility = visibility;
            Parameters = parameters;
            Description = description;
            Summary = summary;
            CliCommandPath = cliCommandPath;
            CliCommandAliases = cliCommandAliases;
            McpToolName = mcpToolName;
            FormatterTypeName = formatterTypeName;
            Hidden = hidden;
            InvocationKind = invocationKind;
            RequestTypeName = requestTypeName;
        }

        public string OperationId { get; }
        public string DeclaringTypeName { get; }
        public string MethodName { get; }
        public string MethodReturnTypeName { get; }
        public string ResultTypeName { get; }
        public MethodReturnKind ReturnKind { get; }
        public OperationVisibilityCandidate Visibility { get; }
        public ImmutableArray<ParameterCandidate> Parameters { get; }
        public string? Description { get; }
        public string? Summary { get; }
        public ImmutableArray<string>? CliCommandPath { get; }
        public ImmutableArray<ImmutableArray<string>>? CliCommandAliases { get; }
        public string? McpToolName { get; }
        public string? FormatterTypeName { get; }
        public bool Hidden { get; }
        public InvocationKind InvocationKind { get; }
        public string? RequestTypeName { get; }
    }

    private sealed class OperationAnalysisResult
    {
        public OperationAnalysisResult(
            OperationCandidate? candidate,
            ImmutableArray<OperationDiagnostic> diagnostics)
        {
            Candidate = candidate;
            Diagnostics = diagnostics;
        }

        public OperationCandidate? Candidate { get; }
        public ImmutableArray<OperationDiagnostic> Diagnostics { get; }
    }

    private sealed class ParameterCandidate
    {
        public ParameterCandidate(
            string name,
            string parameterTypeName,
            ParameterSourceCandidate source,
            bool required,
            int? position,
            string? description,
            ImmutableArray<string>? aliases,
            string? cliName,
            string? mcpName,
            string? requestPropertyName = null)
        {
            Name = name;
            ParameterTypeName = parameterTypeName;
            Source = source;
            Required = required;
            Position = position;
            Description = description;
            Aliases = aliases;
            CliName = cliName;
            McpName = mcpName;
            RequestPropertyName = requestPropertyName;
        }

        public string Name { get; }
        public string ParameterTypeName { get; }
        public ParameterSourceCandidate Source { get; }
        public bool Required { get; }
        public int? Position { get; }
        public string? Description { get; }
        public ImmutableArray<string>? Aliases { get; }
        public string? CliName { get; }
        public string? McpName { get; }
        public string? RequestPropertyName { get; }
    }

    private sealed class ParameterAnalysisResult
    {
        private ParameterAnalysisResult(
            ParameterCandidate? candidate,
            ImmutableArray<OperationDiagnostic> diagnostics)
        {
            Candidate = candidate;
            Diagnostics = diagnostics;
        }

        public ParameterCandidate? Candidate { get; }
        public ImmutableArray<OperationDiagnostic> Diagnostics { get; }

        public static ParameterAnalysisResult FromCandidate(ParameterCandidate candidate)
        {
            return new ParameterAnalysisResult(candidate, []);
        }

        public static ParameterAnalysisResult FromDiagnostic(OperationDiagnostic diagnostic)
        {
            return new ParameterAnalysisResult(null, [diagnostic]);
        }
    }

    private sealed class OperationDiagnostic
    {
        public OperationDiagnostic(
            DiagnosticDescriptor descriptor,
            Location? location,
            object?[] messageArgs)
        {
            Descriptor = descriptor;
            Location = location;
            MessageArgs = messageArgs;
        }

        public DiagnosticDescriptor Descriptor { get; }
        public Location? Location { get; }
        public object?[] MessageArgs { get; }
    }

    private enum OperationVisibilityCandidate
    {
        Both = 0,
        CliOnly = 1,
        McpOnly = 2
    }

    private enum ParameterSourceCandidate
    {
        Option = 0,
        Argument = 1,
        Service = 2,
        CancellationToken = 3
    }

    private enum MethodReturnKind
    {
        Void = 0,
        Value = 1,
        Task = 2,
        TaskOfT = 3,
        ValueTask = 4,
        ValueTaskOfT = 5
    }

    private enum InvocationKind
    {
        StaticMethod = 0,
        InstanceOperation = 1
    }
}

internal static class OperationDescriptorGeneratorSymbolExtensions
{
    public static bool IsGenericTaskLike(this INamedTypeSymbol symbol, string metadataName)
    {
        return symbol.TypeArguments.Length == 1 &&
               string.Equals(symbol.OriginalDefinition.ToDisplayString(), metadataName + "<TResult>", StringComparison.Ordinal);
    }

    public static bool IsNonGenericTaskLike(this INamedTypeSymbol symbol, string metadataName)
    {
        return symbol.TypeArguments.Length == 0 &&
               string.Equals(symbol.ToDisplayString(), metadataName, StringComparison.Ordinal);
    }
}



