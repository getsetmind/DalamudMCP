using System.Globalization;

namespace DalamudMCP.Framework.Cli;

public static class CliBinding
{
    public static string GetRequiredArgument(
        IReadOnlyList<string> arguments,
        int position,
        string displayName)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        if (position >= 0 && position < arguments.Count && !string.IsNullOrWhiteSpace(arguments[position]))
            return arguments[position];

        throw new ArgumentException($"Missing required argument '{displayName}'.");
    }

    public static bool TryFindOptionValue(
        IReadOnlyDictionary<string, string> options,
        string name,
        IReadOnlyList<string>? aliases,
        out string? value)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (options.TryGetValue(name, out value))
            return true;

        if (aliases is not null)
        {
            foreach (string alias in aliases)
            {
                if (options.TryGetValue(alias, out value))
                    return true;
            }
        }

        value = null;
        return false;
    }

    public static TService GetRequiredService<TService>(IServiceProvider? services)
        where TService : class
    {
        return (TService)GetRequiredService(services, typeof(TService));
    }

    public static object GetRequiredService(IServiceProvider? services, Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        object? service = services?.GetService(serviceType);
        if (service is not null)
            return service;

        throw new InvalidOperationException($"Required service '{serviceType.FullName}' was not available.");
    }

    public static TService GetRequiredServiceOrThrow<TService>(IServiceProvider? services)
        where TService : class
    {
        object? service = services?.GetService(typeof(TService));
        if (service is TService typedService)
            return typedService;

        throw new InvalidOperationException($"Required service '{typeof(TService).FullName}' was not available.");
    }

    public static object? ConvertValue(Type targetType, string text, string displayName)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        if (targetType == typeof(string))
            return text;

        Type? nullableUnderlyingType = Nullable.GetUnderlyingType(targetType);
        if (nullableUnderlyingType is not null)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            return ConvertValue(nullableUnderlyingType, text, displayName);
        }

        if (targetType.IsArray)
        {
            Type elementType = targetType.GetElementType()
                               ?? throw new InvalidOperationException($"Array type '{targetType.FullName}' was missing an element type.");
            string[] segments = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Array values = Array.CreateInstance(elementType, segments.Length);
            for (int index = 0; index < segments.Length; index++)
                values.SetValue(ConvertValue(elementType, segments[index], displayName), index);

            return values;
        }

        if (targetType.IsEnum)
        {
            if (Enum.TryParse(targetType, text, ignoreCase: true, out object? enumValue))
                return enumValue;

            throw new ArgumentException($"The value '{text}' is not valid for '{displayName}'.");
        }

        if (targetType == typeof(bool))
        {
            if (bool.TryParse(text, out bool parsedBool))
                return parsedBool;

            throw new ArgumentException($"The value '{text}' is not valid for '{displayName}'.");
        }

        if (targetType == typeof(int))
        {
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedInt))
                return parsedInt;

            throw new ArgumentException($"The value '{text}' is not valid for '{displayName}'.");
        }

        if (targetType == typeof(long))
        {
            if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsedLong))
                return parsedLong;

            throw new ArgumentException($"The value '{text}' is not valid for '{displayName}'.");
        }

        if (targetType == typeof(double))
        {
            if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double parsedDouble))
                return parsedDouble;

            throw new ArgumentException($"The value '{text}' is not valid for '{displayName}'.");
        }

        if (targetType == typeof(decimal))
        {
            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsedDecimal))
                return parsedDecimal;

            throw new ArgumentException($"The value '{text}' is not valid for '{displayName}'.");
        }

        if (targetType == typeof(Guid))
        {
            if (Guid.TryParse(text, out Guid parsedGuid))
                return parsedGuid;

            throw new ArgumentException($"The value '{text}' is not valid for '{displayName}'.");
        }

        if (targetType == typeof(Uri))
        {
            if (Uri.TryCreate(text, UriKind.RelativeOrAbsolute, out Uri? parsedUri))
                return parsedUri;

            throw new ArgumentException($"The value '{text}' is not valid for '{displayName}'.");
        }

        if (targetType == typeof(DateTimeOffset))
        {
            if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset parsedDateTimeOffset))
                return parsedDateTimeOffset;

            throw new ArgumentException($"The value '{text}' is not valid for '{displayName}'.");
        }

        throw new InvalidOperationException($"The CLI runtime does not support parameter type '{targetType.FullName}'.");
    }

    public static string? FormatDefaultText(object? value)
    {
        return value switch
        {
            null => null,
            string text => text,
            _ => value.ToString()
        };
    }
}


