using DalamudMCP.Domain.Capabilities;

namespace DalamudMCP.Domain.Registry;

public sealed record ToolBinding(
    CapabilityId CapabilityId,
    string ToolName,
    string InputSchemaId,
    string OutputSchemaId,
    string HandlerType,
    bool Experimental);
