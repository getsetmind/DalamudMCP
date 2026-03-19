using DalamudMCP.Domain.Capabilities;
using DalamudMCP.Domain.Policy;

namespace DalamudMCP.Application.Services;

public static class ExposurePolicyEvaluator
{
    public static bool CanExposeTool(ExposurePolicy policy, CapabilityDefinition capability, string toolName) =>
        policy.CanExposeTool(capability, toolName);

    public static bool CanExposeResource(ExposurePolicy policy, CapabilityDefinition capability, string uriTemplate) =>
        policy.CanExposeResource(capability, uriTemplate);

    public static bool CanInspectAddon(ExposurePolicy policy, string addonName) =>
        policy.CanInspectAddon(addonName);
}
