using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dalamud.Plugin.Services;
using Lumina.Data;
using Lumina.Excel;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace DalamudMCP.Plugin.Services;

public sealed class GameDataSheetService
{
    private const int DefaultLimit = 25;
    private const int MaximumLimit = 100;
    private const int MaximumProperties = 96;
    private const int MaximumScannedRows = 10_000;
    private static readonly TimeSpan MaximumSearchDuration = TimeSpan.FromMilliseconds(500);
    private readonly IDataManager dataManager;
    private readonly object cacheSyncRoot = new();
    private GameDataCache cache;

    public GameDataSheetService(IDataManager dataManager)
    {
        this.dataManager = dataManager ?? throw new ArgumentNullException(nameof(dataManager));
        cache = CreateCache(GetDataIdentity());
    }

    public GameDataSheetListResult ListSheets(string? query, string? cursor, int? requestedLimit)
    {
        GameDataCache currentCache = GetCache();
        string cursorKey = CreateCursorKey(currentCache.Identity, "list", query?.Trim() ?? string.Empty);
        if (!TryDecodeCursor(cursor, cursorKey, out int offset))
            return new GameDataSheetListResult(false, "invalid_cursor", [], null, false, "The cursor was invalid or belongs to another data version or query.", currentCache.Identity);

        int limit = NormalizeLimit(requestedLimit);
        string[] sheets = dataManager.Excel.SheetNames
            .Where(name => string.IsNullOrWhiteSpace(query) || name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (offset > sheets.Length)
            return new GameDataSheetListResult(false, "invalid_cursor", [], null, false, "The cursor is outside the result set.", currentCache.Identity);

        string[] page = sheets.Skip(offset).Take(limit).ToArray();
        int nextOffset = offset + page.Length;
        bool truncated = nextOffset < sheets.Length;
        return new GameDataSheetListResult(
            true,
            null,
            page,
            truncated ? EncodeCursor(nextOffset, cursorKey) : null,
            truncated,
            $"Returned {page.Length} of {sheets.Length} matching Excel sheets.",
            currentCache.Identity);
    }

    public GameDataSheetDescribeResult DescribeSheet(string sheetName, string? language)
    {
        GameDataCache currentCache = GetCache();
        if (!TryNormalizeSheetName(sheetName, out string normalizedSheetName, out string? error))
            return new GameDataSheetDescribeResult(false, "invalid_sheet", sheetName, null, 0, false, [], error!, currentCache.Identity);

        if (!TryParseLanguage(language, out Language? parsedLanguage))
            return new GameDataSheetDescribeResult(false, "invalid_language", normalizedSheetName, language, 0, false, [], $"Unsupported language '{language}'.", currentCache.Identity);

        try
        {
            if (currentCache.RowTypes.TryGetValue(normalizedSheetName, out Type? rowType))
            {
                IExcelSheet sheet = dataManager.Excel.GetBaseSheet(rowType, parsedLanguage);
                GameDataFieldDescription[] fields = GetTypedProperties(rowType)
                    .Select(static property => new GameDataFieldDescription(property.Name, GetFriendlyTypeName(property.PropertyType)))
                    .ToArray();
                return new GameDataSheetDescribeResult(
                    true,
                    null,
                    normalizedSheetName,
                    sheet.Language.ToString(),
                    sheet.Count,
                    true,
                    fields,
                    $"Described typed Excel sheet {normalizedSheetName} with {fields.Length} fields.",
                    currentCache.Identity);
            }

            RawExcelSheet rawSheet = dataManager.Excel.GetRawSheet(normalizedSheetName, parsedLanguage);
            GameDataFieldDescription[] rawFields = rawSheet.Columns
                .Select(static (column, index) => new GameDataFieldDescription($"column{index}", column.Type.ToString()))
                .Prepend(new GameDataFieldDescription("RowId", "UInt32"))
                .ToArray();
            return new GameDataSheetDescribeResult(
                true,
                null,
                normalizedSheetName,
                rawSheet.Language.ToString(),
                rawSheet.Count,
                false,
                rawFields,
                $"Described raw Excel sheet {normalizedSheetName} with {rawFields.Length} fields.",
                currentCache.Identity);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return new GameDataSheetDescribeResult(false, "sheet_unavailable", normalizedSheetName, language, 0, false, [], exception.Message, currentCache.Identity);
        }
    }

    public GameDataRowResult GetRow(string sheetName, long rowId, IReadOnlyList<string>? fields, string? language)
    {
        GameDataCache currentCache = GetCache();
        if (!TryNormalizeSheetName(sheetName, out string normalizedSheetName, out string? error))
            return GameDataRowResult.Failure(sheetName, rowId, "invalid_sheet", error!, currentCache.Identity);

        if (rowId is < 0 or > uint.MaxValue)
            return GameDataRowResult.Failure(normalizedSheetName, rowId, "invalid_row_id", "row-id must be in the UInt32 range.", currentCache.Identity);

        if (!TryParseLanguage(language, out Language? parsedLanguage))
            return GameDataRowResult.Failure(normalizedSheetName, rowId, "invalid_language", $"Unsupported language '{language}'.", currentCache.Identity);

        try
        {
            uint typedRowId = checked((uint)rowId);
            if (currentCache.RowTypes.TryGetValue(normalizedSheetName, out Type? rowType))
            {
                IExcelSheet sheet = dataManager.Excel.GetBaseSheet(rowType, parsedLanguage);
                object? row = GetTypedRow(sheet, typedRowId);
                if (row is null)
                    return GameDataRowResult.Failure(normalizedSheetName, rowId, "row_not_found", $"Row {rowId} was not found in {normalizedSheetName}.", currentCache.Identity);

                if (!TryProjectTypedRow(row, rowType, fields, out JsonElement data, out error))
                    return GameDataRowResult.Failure(normalizedSheetName, rowId, "field_not_found", error!, currentCache.Identity);

                return GameDataRowResult.Success(normalizedSheetName, rowId, sheet.Language.ToString(), true, data, currentCache.Identity);
            }

            RawExcelSheet rawSheet = dataManager.Excel.GetRawSheet(normalizedSheetName, parsedLanguage);
            ExcelSheet<RawRow> rows = new(rawSheet);
            RawRow? rawRow = rows.GetRowOrDefault(typedRowId);
            if (rawRow is null)
                return GameDataRowResult.Failure(normalizedSheetName, rowId, "row_not_found", $"Row {rowId} was not found in {normalizedSheetName}.", currentCache.Identity);

            if (!TryProjectRawRow(rawRow.Value, fields, out JsonElement rawData, out error))
                return GameDataRowResult.Failure(normalizedSheetName, rowId, "field_not_found", error!, currentCache.Identity);

            return GameDataRowResult.Success(normalizedSheetName, rowId, rawSheet.Language.ToString(), false, rawData, currentCache.Identity);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException or TargetInvocationException)
        {
            return GameDataRowResult.Failure(normalizedSheetName, rowId, "sheet_unavailable", exception.GetBaseException().Message, currentCache.Identity);
        }
    }

    public GameDataSearchResult Search(
        string sheetName,
        string? query,
        IReadOnlyList<string>? fields,
        string? where,
        string? language,
        string? cursor,
        int? requestedLimit,
        CancellationToken cancellationToken)
    {
        GameDataCache currentCache = GetCache();
        if (!TryNormalizeSheetName(sheetName, out string normalizedSheetName, out string? error))
            return GameDataSearchResult.Failure(sheetName, "invalid_sheet", error!, currentCache.Identity);

        if (!TryParseLanguage(language, out Language? parsedLanguage))
            return GameDataSearchResult.Failure(normalizedSheetName, "invalid_language", $"Unsupported language '{language}'.", currentCache.Identity);

        if (!TryParsePredicate(where, out GameDataPredicate? predicate, out error))
            return GameDataSearchResult.Failure(normalizedSheetName, "invalid_where", error!, currentCache.Identity);

        string cursorKey = CreateCursorKey(
            currentCache.Identity,
            "search",
            normalizedSheetName,
            query?.Trim() ?? string.Empty,
            string.Join(',', fields ?? []),
            language?.Trim() ?? string.Empty,
            where?.Trim() ?? string.Empty);
        if (!TryDecodeCursor(cursor, cursorKey, out int offset))
            return GameDataSearchResult.Failure(normalizedSheetName, "invalid_cursor", "The cursor was invalid or belongs to another data version or query.", currentCache.Identity);

        int limit = NormalizeLimit(requestedLimit);
        string normalizedQuery = query?.Trim() ?? string.Empty;
        Stopwatch stopwatch = Stopwatch.StartNew();
        List<GameDataSearchRow> results = [];
        int index = 0;
        int scanned = 0;
        bool truncated = false;

        try
        {
            bool typed = currentCache.RowTypes.TryGetValue(normalizedSheetName, out Type? rowType);
            IExcelSheet sheet = typed
                ? dataManager.Excel.GetBaseSheet(rowType!, parsedLanguage)
                : dataManager.Excel.GetRawSheet(normalizedSheetName, parsedLanguage);
            if (offset > sheet.Count)
                return GameDataSearchResult.Failure(normalizedSheetName, "invalid_cursor", "The cursor is outside the result set.", currentCache.Identity);

            IEnumerable rows = typed
                ? (IEnumerable)sheet
                : new ExcelSheet<RawRow>((RawExcelSheet)sheet);

            foreach (object row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                index++;
                scanned++;
                if (scanned > MaximumScannedRows || stopwatch.Elapsed > MaximumSearchDuration)
                {
                    truncated = true;
                    break;
                }

                if (index <= offset)
                    continue;

                uint rowId;
                JsonElement data;
                bool projected = typed
                    ? TryProjectTypedRow(row, rowType!, fields, out data, out error)
                    : TryProjectRawRow((RawRow)row, fields, out data, out error);
                if (!projected)
                    return GameDataSearchResult.Failure(normalizedSheetName, "field_not_found", error!, currentCache.Identity);

                rowId = typed ? ReadRowId(row) : ((RawRow)row).RowId;
                if (!string.IsNullOrEmpty(normalizedQuery) &&
                    !data.GetRawText().Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool matches = false;
                if (predicate is not null && !TryMatchesPredicate(data, predicate, out matches, out error))
                    return GameDataSearchResult.Failure(normalizedSheetName, "invalid_where", error!, currentCache.Identity);
                if (predicate is not null && !matches)
                    continue;

                results.Add(new GameDataSearchRow(rowId, data));
                if (results.Count < limit)
                    continue;

                truncated = index < sheet.Count;
                break;
            }

            string? nextCursor = truncated ? EncodeCursor(index, cursorKey) : null;
            return new GameDataSearchResult(
                true,
                null,
                normalizedSheetName,
                sheet.Language.ToString(),
                typed,
                results,
                nextCursor,
                truncated,
                scanned,
                $"Returned {results.Count} rows from {normalizedSheetName} after scanning {scanned} rows.",
                currentCache.Identity);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException or TargetInvocationException)
        {
            return GameDataSearchResult.Failure(normalizedSheetName, "sheet_unavailable", exception.GetBaseException().Message, currentCache.Identity);
        }
    }

    private static object? GetTypedRow(IExcelSheet sheet, uint rowId)
    {
        MethodInfo? method = sheet.GetType().GetMethod("GetRowOrDefault", [typeof(uint)]);
        return method?.Invoke(sheet, [rowId]);
    }

    private static bool TryProjectTypedRow(
        object row,
        Type rowType,
        IReadOnlyList<string>? requestedFields,
        out JsonElement data,
        out string? error)
    {
        PropertyInfo[] available = GetTypedProperties(rowType);
        if (!TrySelectProperties(available, requestedFields, out PropertyInfo[] selected, out error))
        {
            data = default;
            return false;
        }

        JsonObject projected = [];
        foreach (PropertyInfo property in selected)
        {
            try
            {
                projected[property.Name] = ToJsonNode(property.GetValue(row));
            }
            catch (Exception exception) when (exception is TargetInvocationException or InvalidOperationException)
            {
                projected[property.Name] = new JsonObject
                {
                    ["error"] = exception.GetBaseException().Message
                };
            }
        }

        data = JsonSerializer.SerializeToElement(projected);
        error = null;
        return true;
    }

    private static bool TryProjectRawRow(
        RawRow row,
        IReadOnlyList<string>? requestedFields,
        out JsonElement data,
        out string? error)
    {
        HashSet<int>? selectedColumns = null;
        bool includeRowId = true;
        if (requestedFields is { Count: > 0 })
        {
            selectedColumns = [];
            includeRowId = false;
            foreach (string requestedField in requestedFields)
            {
                if (string.Equals(requestedField, "RowId", StringComparison.OrdinalIgnoreCase))
                {
                    includeRowId = true;
                    continue;
                }

                if (!requestedField.StartsWith("column", StringComparison.OrdinalIgnoreCase) ||
                    !int.TryParse(requestedField["column".Length..], NumberStyles.None, CultureInfo.InvariantCulture, out int index) ||
                    index < 0 ||
                    index >= row.Columns.Count)
                {
                    data = default;
                    error = $"Field '{requestedField}' does not exist. Raw fields use column0 through column{row.Columns.Count - 1}.";
                    return false;
                }

                selectedColumns.Add(index);
            }
        }

        JsonObject projected = [];
        if (includeRowId)
            projected["RowId"] = row.RowId;

        int maximum = Math.Min(row.Columns.Count, MaximumProperties);
        for (int index = 0; index < maximum; index++)
        {
            if (selectedColumns is not null && !selectedColumns.Contains(index))
                continue;

            try
            {
                projected[$"column{index}"] = ToJsonNode(row.ReadColumn(index));
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                projected[$"column{index}"] = new JsonObject { ["error"] = exception.Message };
            }
        }

        data = JsonSerializer.SerializeToElement(projected);
        error = null;
        return true;
    }

    private static PropertyInfo[] GetTypedProperties(Type rowType)
    {
        return rowType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.GetMethod is not null && property.GetIndexParameters().Length == 0)
            .OrderBy(static property => string.Equals(property.Name, "RowId", StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(static property => property.MetadataToken)
            .Take(MaximumProperties)
            .ToArray();
    }

    private static bool TrySelectProperties(
        PropertyInfo[] available,
        IReadOnlyList<string>? requestedFields,
        out PropertyInfo[] selected,
        out string? error)
    {
        if (requestedFields is not { Count: > 0 })
        {
            selected = available;
            error = null;
            return true;
        }

        Dictionary<string, PropertyInfo> byName = available.ToDictionary(static property => property.Name, StringComparer.OrdinalIgnoreCase);
        List<PropertyInfo> result = [];
        foreach (string field in requestedFields)
        {
            if (!byName.TryGetValue(field, out PropertyInfo? property))
            {
                selected = [];
                error = $"Field '{field}' does not exist on the generated row type.";
                return false;
            }

            if (!result.Contains(property))
                result.Add(property);
        }

        selected = [.. result];
        error = null;
        return true;
    }

    internal static bool TryParsePredicate(string? json, out GameDataPredicate? predicate, out string? error)
    {
        predicate = null;
        error = null;
        if (string.IsNullOrWhiteSpace(json))
            return true;
        if (json.Length > 16384)
        {
            error = "where must not exceed 16384 characters.";
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 4,
            });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !TryGetProperty(root, "field", out JsonElement fieldElement) ||
                fieldElement.ValueKind != JsonValueKind.String ||
                !TryGetProperty(root, "operator", out JsonElement operatorElement) ||
                operatorElement.ValueKind != JsonValueKind.String ||
                !TryGetProperty(root, "value", out JsonElement valueElement))
            {
                error = "where must be a JSON object containing string field, string operator, and value.";
                return false;
            }

            string field = fieldElement.GetString()?.Trim() ?? string.Empty;
            string comparisonOperator = operatorElement.GetString()?.Trim().ToLowerInvariant() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(field))
            {
                error = "where.field must not be empty.";
                return false;
            }

            string[] supported = ["eq", "ne", "contains", "startswith", "endswith", "gt", "gte", "lt", "lte"];
            if (!supported.Contains(comparisonOperator, StringComparer.Ordinal))
            {
                error = $"Unsupported where.operator '{comparisonOperator}'.";
                return false;
            }

            predicate = new GameDataPredicate(field, comparisonOperator, valueElement.Clone());
            return true;
        }
        catch (JsonException exception)
        {
            error = $"where is not valid JSON: {exception.Message}";
            return false;
        }
    }

    internal static bool TryMatchesPredicate(
        JsonElement data,
        GameDataPredicate predicate,
        out bool matches,
        out string? error)
    {
        matches = false;
        error = null;
        if (data.ValueKind != JsonValueKind.Object || !TryGetProperty(data, predicate.Field, out JsonElement actual))
        {
            error = $"where.field '{predicate.Field}' is not present in the projected fields.";
            return false;
        }

        JsonElement expected = predicate.Value;
        switch (predicate.Operator)
        {
            case "eq":
            case "ne":
                bool equal = AreEqual(actual, expected);
                matches = predicate.Operator == "eq" ? equal : !equal;
                return true;
            case "contains":
            case "startswith":
            case "endswith":
                if (!TryReadString(actual, out string? actualText) || !TryReadString(expected, out string? expectedText))
                {
                    error = $"where.operator '{predicate.Operator}' requires string-compatible values.";
                    return false;
                }

                matches = predicate.Operator switch
                {
                    "contains" => actualText.Contains(expectedText, StringComparison.OrdinalIgnoreCase),
                    "startswith" => actualText.StartsWith(expectedText, StringComparison.OrdinalIgnoreCase),
                    _ => actualText.EndsWith(expectedText, StringComparison.OrdinalIgnoreCase),
                };
                return true;
            default:
                if (!actual.TryGetDouble(out double actualNumber) || !expected.TryGetDouble(out double expectedNumber))
                {
                    error = $"where.operator '{predicate.Operator}' requires numeric values.";
                    return false;
                }

                matches = predicate.Operator switch
                {
                    "gt" => actualNumber > expectedNumber,
                    "gte" => actualNumber >= expectedNumber,
                    "lt" => actualNumber < expectedNumber,
                    "lte" => actualNumber <= expectedNumber,
                    _ => false,
                };
                return true;
        }
    }

    internal static JsonNode? ToJsonNode(object? value) =>
        ToJsonNode(value, depth: 0, new HashSet<object>(ReferenceEqualityComparer.Instance));

    private static JsonNode? ToJsonNode(object? value, int depth, HashSet<object> ancestors)
    {
        if (value is null)
            return null;

        Type type = value.GetType();
        if (value is string or char or bool || type.IsPrimitive || value is decimal)
            return JsonValue.Create(value);

        if (type.IsEnum)
            return JsonValue.Create(value.ToString());

        if (value is Guid or DateTime or DateTimeOffset or TimeSpan or Uri)
            return JsonValue.Create(Convert.ToString(value, CultureInfo.InvariantCulture));

        if (type.FullName?.Contains("SeString", StringComparison.Ordinal) == true)
            return JsonValue.Create(value.ToString());

        if (type.FullName?.StartsWith("Lumina.Excel.RowRef", StringComparison.Ordinal) == true)
        {
            PropertyInfo? rowIdProperty = type.GetProperty("RowId");
            object? rowId = rowIdProperty?.GetValue(value);
            Type? referencedType = type.IsGenericType ? type.GetGenericArguments().FirstOrDefault() : null;
            return new JsonObject
            {
                ["sheet"] = referencedType?.Name,
                ["rowId"] = rowId is null ? null : JsonValue.Create(rowId)
            };
        }

        if (depth >= 8)
            return JsonValue.Create("<maximum-depth>");

        if (value is IEnumerable enumerable and not string)
        {
            if (!type.IsValueType && !ancestors.Add(value))
                return JsonValue.Create("<cycle>");

            JsonArray array = [];
            try
            {
                foreach (object? item in enumerable)
                {
                    if (array.Count >= 32)
                        break;

                    array.Add(ToJsonNode(item, depth + 1, ancestors));
                }
            }
            finally
            {
                if (!type.IsValueType)
                    ancestors.Remove(value);
            }

            return array;
        }

        return JsonValue.Create(value.ToString());
    }

    private static uint ReadRowId(object row)
    {
        object? value = row.GetType().GetProperty("RowId")?.GetValue(row);
        return value is uint rowId ? rowId : 0;
    }

    private static bool TryNormalizeSheetName(string? value, out string sheetName, out string? error)
    {
        sheetName = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sheetName))
        {
            error = "sheet is required.";
            return false;
        }

        error = null;
        return true;
    }

    internal static bool TryParseLanguage(string? value, out Language? language)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            language = null;
            return true;
        }

        string normalized = value.Trim().ToLowerInvariant();
        language = normalized switch
        {
            "none" => Language.None,
            "ja" or "jp" or "japanese" => Language.Japanese,
            "en" or "english" => Language.English,
            "de" or "german" => Language.German,
            "fr" or "french" => Language.French,
            "chs" or "zh-cn" or "chinesesimplified" => Language.ChineseSimplified,
            "cht" or "zh-tw" or "chinesetraditional" => Language.ChineseTraditional,
            "ko" or "korean" => Language.Korean,
            _ => null
        };
        return language is not null;
    }

    private static int NormalizeLimit(int? requestedLimit)
    {
        return Math.Clamp(requestedLimit ?? DefaultLimit, 1, MaximumLimit);
    }

    private static string EncodeCursor(int offset, string cursorKey)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes($"v2:{cursorKey}:{offset}"));
    }

    internal static bool TryDecodeCursor(string? cursor, out int offset)
    {
        offset = 0;
        if (string.IsNullOrWhiteSpace(cursor))
            return true;

        try
        {
            string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cursor.Trim()));
            int separator = decoded.LastIndexOf(':');
            return separator > 0 &&
                   (decoded.StartsWith("v1:", StringComparison.Ordinal) || decoded.StartsWith("v2:", StringComparison.Ordinal)) &&
                   int.TryParse(decoded[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out offset) &&
                   offset >= 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryDecodeCursor(string? cursor, string expectedKey, out int offset)
    {
        offset = 0;
        if (string.IsNullOrWhiteSpace(cursor))
            return true;

        try
        {
            string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cursor.Trim()));
            string prefix = $"v2:{expectedKey}:";
            return decoded.StartsWith(prefix, StringComparison.Ordinal) &&
                   int.TryParse(decoded[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out offset) &&
                   offset >= 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string CreateCursorKey(string identity, params string[] components)
    {
        string value = string.Join('\n', components.Prepend(identity));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..24];
    }

    private static bool TryGetProperty(JsonElement value, string name, out JsonElement property)
    {
        foreach (JsonProperty candidate in value.EnumerateObject())
        {
            if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                property = candidate.Value;
                return true;
            }
        }

        property = default;
        return false;
    }

    private static bool AreEqual(JsonElement left, JsonElement right)
    {
        if (left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number &&
            left.TryGetDecimal(out decimal leftNumber) && right.TryGetDecimal(out decimal rightNumber))
        {
            return leftNumber == rightNumber;
        }

        if (TryReadString(left, out string? leftText) && TryReadString(right, out string? rightText))
            return string.Equals(leftText, rightText, StringComparison.OrdinalIgnoreCase);

        return JsonElement.DeepEquals(left, right);
    }

    private static bool TryReadString(JsonElement value, out string text)
    {
        text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            JsonValueKind.Number => value.GetRawText(),
            _ => string.Empty,
        };
        return value.ValueKind is JsonValueKind.String or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Number;
    }

    private static string GetFriendlyTypeName(Type type)
    {
        Type effectiveType = Nullable.GetUnderlyingType(type) ?? type;
        if (effectiveType.IsArray)
            return $"{GetFriendlyTypeName(effectiveType.GetElementType()!)}[]";

        return effectiveType.IsGenericType
            ? $"{effectiveType.Name[..effectiveType.Name.IndexOf('`')]}<{string.Join(',', effectiveType.GetGenericArguments().Select(GetFriendlyTypeName))}>"
            : effectiveType.Name;
    }

    private static Dictionary<string, Type> CreateGeneratedRowTypes()
    {
        return typeof(LuminaAction).Assembly.GetTypes()
            .Where(static type =>
                string.Equals(type.Namespace, "Lumina.Excel.Sheets", StringComparison.Ordinal) &&
                type.IsPublic &&
                type.IsValueType)
            .GroupBy(static type => type.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    private GameDataCache GetCache()
    {
        string identity = GetDataIdentity();
        lock (cacheSyncRoot)
        {
            if (!string.Equals(cache.Identity, identity, StringComparison.Ordinal))
                cache = CreateCache(identity);
            return cache;
        }
    }

    private string GetDataIdentity()
    {
        object gameData = dataManager.GameData;
        string gameVersion = ReadVersion(gameData) ?? "unknown";
        string dalamudVersion = typeof(IDataManager).Assembly.GetName().Version?.ToString() ?? "unknown";
        string luminaVersion = typeof(LuminaAction).Assembly.GetName().Version?.ToString() ?? "unknown";
        return $"game={gameVersion};dalamud={dalamudVersion};lumina={luminaVersion}";
    }

    private static string? ReadVersion(object value)
    {
        try
        {
            foreach (string propertyName in new[] { "GameVersion", "Version" })
            {
                object? propertyValue = value.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(value);
                if (propertyValue is not null && !string.IsNullOrWhiteSpace(propertyValue.ToString()))
                    return propertyValue.ToString();
            }

            object? repository = value.GetType().GetProperty("Repository", BindingFlags.Public | BindingFlags.Instance)?.GetValue(value);
            return repository is null ? null : ReadVersion(repository);
        }
        catch (Exception exception) when (exception is TargetInvocationException or InvalidOperationException)
        {
            return null;
        }
    }

    private static GameDataCache CreateCache(string identity) => new(identity, CreateGeneratedRowTypes());

    private sealed record GameDataCache(string Identity, Dictionary<string, Type> RowTypes);
}

public sealed record GameDataPredicate(string Field, string Operator, JsonElement Value);

public sealed record GameDataSheetListResult(
    bool Succeeded,
    string? Reason,
    IReadOnlyList<string> Sheets,
    string? NextCursor,
    bool Truncated,
    string SummaryText,
    string DataVersion = "unknown");

public sealed record GameDataFieldDescription(string Name, string Type);

public sealed record GameDataSheetDescribeResult(
    bool Succeeded,
    string? Reason,
    string Sheet,
    string? Language,
    int RowCount,
    bool Typed,
    IReadOnlyList<GameDataFieldDescription> Fields,
    string SummaryText,
    string DataVersion = "unknown");

public sealed record GameDataRowResult(
    bool Succeeded,
    string? Reason,
    string Sheet,
    long RowId,
    string? Language,
    bool Typed,
    JsonElement? Data,
    string SummaryText,
    string DataVersion = "unknown")
{
    public static GameDataRowResult Failure(string sheet, long rowId, string reason, string summary, string dataVersion = "unknown")
    {
        return new GameDataRowResult(false, reason, sheet, rowId, null, false, null, summary, dataVersion);
    }

    public static GameDataRowResult Success(string sheet, long rowId, string language, bool typed, JsonElement data, string dataVersion = "unknown")
    {
        return new GameDataRowResult(true, null, sheet, rowId, language, typed, data, $"Returned row {rowId} from {sheet}.", dataVersion);
    }
}

public sealed record GameDataSearchRow(uint RowId, JsonElement Data);

public sealed record GameDataSearchResult(
    bool Succeeded,
    string? Reason,
    string Sheet,
    string? Language,
    bool Typed,
    IReadOnlyList<GameDataSearchRow> Rows,
    string? NextCursor,
    bool Truncated,
    int ScannedRows,
    string SummaryText,
    string DataVersion = "unknown")
{
    public static GameDataSearchResult Failure(string sheet, string reason, string summary, string dataVersion = "unknown")
    {
        return new GameDataSearchResult(false, reason, sheet, null, false, [], null, false, 0, summary, dataVersion);
    }
}
