using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DalamudMCP.Protocol;

public static class ProtocolJsonSchemaBuilder
{
    private const int MaximumDepth = 8;

    public static string Create(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        JsonObject schema = Build(type, new HashSet<Type>(), 0);
        return schema.ToJsonString(ProtocolContract.JsonOptions);
    }

    private static JsonObject Build(Type type, HashSet<Type> ancestors, int depth)
    {
        Type? nullableType = Nullable.GetUnderlyingType(type);
        if (nullableType is not null)
            return MakeNullable(Build(nullableType, ancestors, depth));

        if (type == typeof(string) || type == typeof(char) || type == typeof(Guid) || type == typeof(Uri))
            return Scalar("string", type == typeof(Guid) ? "uuid" : type == typeof(Uri) ? "uri" : null);

        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
            return Scalar("string", "date-time");

        if (type == typeof(TimeSpan))
            return Scalar("string", "duration");

        if (type == typeof(bool))
            return Scalar("boolean");

        if (type.IsEnum)
        {
            JsonArray values = [];
            foreach (string name in Enum.GetNames(type))
                values.Add(JsonNamingPolicy.CamelCase.ConvertName(name));

            return new JsonObject
            {
                ["type"] = "string",
                ["enum"] = values
            };
        }

        if (IsInteger(type))
            return Scalar("integer");

        if (IsNumber(type))
            return Scalar("number");

        if (type == typeof(JsonElement) || type == typeof(JsonDocument) || type == typeof(object))
            return new JsonObject();

        if (type == typeof(byte[]))
            return Scalar("string", "byte");

        if (TryGetDictionaryValueType(type, out Type? dictionaryValueType))
        {
            return new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = Build(dictionaryValueType!, ancestors, depth + 1)
            };
        }

        if (TryGetEnumerableElementType(type, out Type? elementType))
        {
            return new JsonObject
            {
                ["type"] = "array",
                ["items"] = Build(elementType!, ancestors, depth + 1)
            };
        }

        if (depth >= MaximumDepth || !ancestors.Add(type))
            return new JsonObject { ["type"] = "object" };

        JsonObject properties = [];
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetMethod is null || property.GetIndexParameters().Length != 0 ||
                property.IsDefined(typeof(JsonIgnoreAttribute), inherit: true))
            {
                continue;
            }

            string name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ??
                          JsonNamingPolicy.CamelCase.ConvertName(property.Name);
            properties[name] = Build(property.PropertyType, ancestors, depth + 1);
        }

        ancestors.Remove(type);
        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["additionalProperties"] = false
        };
    }

    private static JsonObject Scalar(string type, string? format = null)
    {
        JsonObject schema = new() { ["type"] = type };
        if (!string.IsNullOrWhiteSpace(format))
            schema["format"] = format;

        return schema;
    }

    private static JsonObject MakeNullable(JsonObject schema)
    {
        return new JsonObject
        {
            ["anyOf"] = new JsonArray(schema, new JsonObject { ["type"] = "null" })
        };
    }

    private static bool IsInteger(Type type)
    {
        return type == typeof(byte) || type == typeof(sbyte) ||
               type == typeof(short) || type == typeof(ushort) ||
               type == typeof(int) || type == typeof(uint) ||
               type == typeof(long) || type == typeof(ulong);
    }

    private static bool IsNumber(Type type)
    {
        return type == typeof(float) || type == typeof(double) || type == typeof(decimal);
    }

    private static bool TryGetEnumerableElementType(Type type, out Type? elementType)
    {
        if (type.IsArray)
        {
            elementType = type.GetElementType();
            return elementType is not null;
        }

        Type? enumerable = type.GetInterfaces()
            .Append(type)
            .FirstOrDefault(static candidate =>
                candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        elementType = enumerable?.GetGenericArguments()[0];
        return elementType is not null;
    }

    private static bool TryGetDictionaryValueType(Type type, out Type? valueType)
    {
        Type? dictionary = type.GetInterfaces()
            .Append(type)
            .FirstOrDefault(static candidate =>
                candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IDictionary<,>) &&
                candidate.GetGenericArguments()[0] == typeof(string));
        valueType = dictionary?.GetGenericArguments()[1];
        return valueType is not null;
    }
}
