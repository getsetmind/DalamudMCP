using System.Text.Json;

namespace DalamudMCP.Host;

public sealed class McpSchemaRegistry
{
    private const string JsonSchemaVersion = "https://json-schema.org/draft/2020-12/schema";

    private readonly Dictionary<string, JsonElement> schemasById;

    public McpSchemaRegistry()
    {
        schemasById = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["sessionStatus.input"] = Parse("""{"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","additionalProperties":false}"""),
            ["playerContext.input"] = Parse("""{"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","additionalProperties":false}"""),
            ["dutyContext.input"] = Parse("""{"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","additionalProperties":false}"""),
            ["inventorySummary.input"] = Parse("""{"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","additionalProperties":false}"""),
            ["addonCatalog.input"] = Parse("""{"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","additionalProperties":false}"""),
            ["addonTree.input"] = Parse("""{"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","properties":{"addonName":{"type":"string","minLength":1}},"required":["addonName"],"additionalProperties":false}"""),
            ["stringTable.input"] = Parse("""{"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","properties":{"addonName":{"type":"string","minLength":1}},"required":["addonName"],"additionalProperties":false}"""),
            ["nearbyInteractables.input"] = Parse("""{"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","properties":{"maxDistance":{"type":"number","minimum":1,"maximum":40},"nameContains":{"type":"string","minLength":1},"includePlayers":{"type":"boolean"}},"additionalProperties":false}"""),
            ["targetObject.input"] = Parse("""{"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","properties":{"gameObjectId":{"type":"string","minLength":1}},"required":["gameObjectId"],"additionalProperties":false}"""),
            ["interactWithTarget.input"] = Parse("""{"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","properties":{"expectedGameObjectId":{"type":"string","minLength":1},"checkLineOfSight":{"type":"boolean"}},"additionalProperties":false}"""),
            ["moveToEntity.input"] = Parse("""{"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","properties":{"gameObjectId":{"type":"string","minLength":1},"allowFlight":{"type":"boolean"}},"required":["gameObjectId"],"additionalProperties":false}"""),
            ["teleportToAetheryte.input"] = Parse("""{"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","properties":{"query":{"type":"string","minLength":1}},"required":["query"],"additionalProperties":false}"""),
            ["addonCallbackInt.input"] = Parse("""{"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","properties":{"addonName":{"type":"string","minLength":1},"value":{"type":"integer"}},"required":["addonName","value"],"additionalProperties":false}"""),
            ["addonCallbackValues.input"] = Parse("""{"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","properties":{"addonName":{"type":"string","minLength":1},"values":{"type":"array","minItems":1,"items":{"type":"integer"}}},"required":["addonName","values"],"additionalProperties":false}"""),
            ["sessionStatus.output"] = QueryResponseSchema("""{"type":"object","properties":{"pipeName":{"type":"string"},"isBridgeServerRunning":{"type":"boolean"},"readyComponentCount":{"type":"integer"},"totalComponentCount":{"type":"integer"},"components":{"type":"array","items":{"type":"object","properties":{"componentName":{"type":"string"},"isReady":{"type":"boolean"},"status":{"type":"string"}},"required":["componentName","isReady","status"]}},"summaryText":{"type":"string"}},"required":["pipeName","isBridgeServerRunning","readyComponentCount","totalComponentCount","components","summaryText"]}"""),
            ["playerContext.output"] = QueryResponseSchema("""{"type":"object","properties":{"characterName":{"type":["string","null"]},"homeWorld":{"type":["string","null"]},"currentWorld":{"type":["string","null"]},"classJobId":{"type":"integer"},"classJobName":{"type":["string","null"]},"level":{"type":"integer"},"territoryId":{"type":["integer","null"]},"territoryName":{"type":["string","null"]},"mapId":{"type":["integer","null"]},"mapName":{"type":["string","null"]},"position":{"type":["object","null"]},"inCombat":{"type":"boolean"},"inDuty":{"type":"boolean"},"isCrafting":{"type":"boolean"},"isGathering":{"type":"boolean"},"isMounted":{"type":"boolean"},"isMoving":{"type":"boolean"},"zoneType":{"type":["string","null"]},"contentStatus":{"type":["string","null"]},"summaryText":{"type":"string"}},"required":["classJobId","level","inCombat","inDuty","isCrafting","isGathering","isMounted","isMoving","summaryText"]}"""),
            ["dutyContext.output"] = QueryResponseSchema("""{"type":"object","properties":{"territoryId":{"type":["integer","null"]},"dutyName":{"type":["string","null"]},"dutyType":{"type":["string","null"]},"inDuty":{"type":"boolean"},"isDutyComplete":{"type":"boolean"},"summaryText":{"type":"string"}},"required":["inDuty","isDutyComplete","summaryText"]}"""),
            ["inventorySummary.output"] = QueryResponseSchema("""{"type":"object","properties":{"currencyGil":{"type":"integer"},"occupiedSlots":{"type":"integer"},"totalSlots":{"type":"integer"},"categoryCounts":{"type":"object","additionalProperties":{"type":"integer"}},"summaryText":{"type":"string"}},"required":["currencyGil","occupiedSlots","totalSlots","categoryCounts","summaryText"]}"""),
            ["addonCatalog.output"] = QueryResponseSchema("""{"type":"array","items":{"type":"object","properties":{"addonName":{"type":"string"},"isReady":{"type":"boolean"},"isVisible":{"type":"boolean"},"capturedAt":{"type":"string","format":"date-time"},"summaryText":{"type":"string"}},"required":["addonName","isReady","isVisible","capturedAt","summaryText"]}}"""),
            ["addonTree.output"] = QueryResponseSchema("""{"type":"object","properties":{"addonName":{"type":"string"},"capturedAt":{"type":"string","format":"date-time"},"roots":{"type":"array","items":{"type":"object"}}},"required":["addonName","capturedAt","roots"]}"""),
            ["stringTable.output"] = QueryResponseSchema("""{"type":"object","properties":{"addonName":{"type":"string"},"capturedAt":{"type":"string","format":"date-time"},"entries":{"type":"array","items":{"type":"object","properties":{"index":{"type":"integer"},"rawValue":{"type":["string","null"]},"decodedValue":{"type":["string","null"]}},"required":["index"]}}},"required":["addonName","capturedAt","entries"]}"""),
            ["nearbyInteractables.output"] = QueryResponseSchema("""{"type":"object","properties":{"maxDistance":{"type":"number"},"interactables":{"type":"array","items":{"type":"object","properties":{"gameObjectId":{"type":"string"},"name":{"type":"string"},"objectKind":{"type":"string"},"isTargetable":{"type":"boolean"},"distance":{"type":"number"},"hitboxRadius":{"type":"number"},"position":{"type":["object","null"]}},"required":["gameObjectId","name","objectKind","isTargetable","distance","hitboxRadius"]}},"summaryText":{"type":"string"}},"required":["maxDistance","interactables","summaryText"]}"""),
            ["targetObject.output"] = QueryResponseSchema("""{"type":"object","properties":{"requestedGameObjectId":{"type":"string"},"succeeded":{"type":"boolean"},"reason":{"type":["string","null"]},"targetedGameObjectId":{"type":["string","null"]},"targetName":{"type":["string","null"]},"objectKind":{"type":["string","null"]},"summaryText":{"type":"string"}},"required":["requestedGameObjectId","succeeded","summaryText"]}"""),
            ["interactWithTarget.output"] = QueryResponseSchema("""{"type":"object","properties":{"expectedGameObjectId":{"type":["string","null"]},"succeeded":{"type":"boolean"},"reason":{"type":["string","null"]},"interactedGameObjectId":{"type":["string","null"]},"targetName":{"type":["string","null"]},"objectKind":{"type":["string","null"]},"distance":{"type":["number","null"]},"summaryText":{"type":"string"}},"required":["succeeded","summaryText"]}"""),
            ["moveToEntity.output"] = QueryResponseSchema("""{"type":"object","properties":{"requestedGameObjectId":{"type":"string"},"succeeded":{"type":"boolean"},"reason":{"type":["string","null"]},"resolvedGameObjectId":{"type":["string","null"]},"targetName":{"type":["string","null"]},"objectKind":{"type":["string","null"]},"destination":{"type":["object","null"]},"summaryText":{"type":"string"}},"required":["requestedGameObjectId","succeeded","summaryText"]}"""),
            ["teleportToAetheryte.output"] = QueryResponseSchema("""{"type":"object","properties":{"requestedQuery":{"type":"string"},"succeeded":{"type":"boolean"},"reason":{"type":["string","null"]},"aetheryteId":{"type":["integer","null"]},"aetheryteName":{"type":["string","null"]},"territoryName":{"type":["string","null"]},"summaryText":{"type":"string"}},"required":["requestedQuery","succeeded","summaryText"]}"""),
            ["addonCallbackInt.output"] = QueryResponseSchema("""{"type":"object","properties":{"addonName":{"type":"string"},"value":{"type":"integer"},"succeeded":{"type":"boolean"},"reason":{"type":["string","null"]},"summaryText":{"type":"string"}},"required":["addonName","value","succeeded","summaryText"]}"""),
            ["addonCallbackValues.output"] = QueryResponseSchema("""{"type":"object","properties":{"addonName":{"type":"string"},"values":{"type":"array","items":{"type":"integer"}},"succeeded":{"type":"boolean"},"reason":{"type":["string","null"]},"summaryText":{"type":"string"}},"required":["addonName","values","succeeded","summaryText"]}"""),
        };
    }

    public JsonElement GetRequired(string schemaId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaId);
        if (!schemasById.TryGetValue(schemaId, out var schema))
        {
            throw new InvalidOperationException($"Unknown schema id '{schemaId}'.");
        }

        return schema;
    }

    private static JsonElement QueryResponseSchema(string dataSchemaJson) =>
        Parse(
            $$"""
            {
              "$schema": "{{JsonSchemaVersion}}",
              "type": "object",
              "properties": {
                "available": { "type": "boolean" },
                "reason": { "type": ["string", "null"] },
                "contractVersion": { "type": "string" },
                "capturedAt": { "type": ["string", "null"], "format": "date-time" },
                "snapshotAgeMs": { "type": ["integer", "null"] },
                "data": {{dataSchemaJson}}
              },
              "required": ["available", "contractVersion"]
            }
            """);

    private static JsonElement Parse(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();
}
