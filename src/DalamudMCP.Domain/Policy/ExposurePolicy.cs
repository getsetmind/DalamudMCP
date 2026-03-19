using DalamudMCP.Domain.Capabilities;

namespace DalamudMCP.Domain.Policy;

public sealed record ExposurePolicy
{
    public static readonly ExposurePolicy Default = new(
        enabledTools: [],
        enabledResources: [],
        enabledAddons: [],
        observationProfileEnabled: true,
        actionProfileEnabled: false);

    public ExposurePolicy(
        IEnumerable<string>? enabledTools = null,
        IEnumerable<string>? enabledResources = null,
        IEnumerable<string>? enabledAddons = null,
        bool observationProfileEnabled = true,
        bool actionProfileEnabled = false)
    {
        ObservationProfileEnabled = observationProfileEnabled;
        ActionProfileEnabled = actionProfileEnabled;
        EnabledTools = ToSet(enabledTools);
        EnabledResources = ToSet(enabledResources);
        EnabledAddons = ToSet(enabledAddons);
    }

    public bool ObservationProfileEnabled { get; }

    public bool ActionProfileEnabled { get; }

    public IReadOnlySet<string> EnabledTools { get; }

    public IReadOnlySet<string> EnabledResources { get; }

    public IReadOnlySet<string> EnabledAddons { get; }

    public bool CanExposeTool(CapabilityDefinition capability, string toolName)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        if (!capability.SupportsTool || capability.Denied)
        {
            return false;
        }

        if (capability.Profile is ProfileType.Observation && !ObservationProfileEnabled)
        {
            return false;
        }

        if (capability.Profile is ProfileType.Action && !ActionProfileEnabled)
        {
            return false;
        }

        return EnabledTools.Contains(toolName);
    }

    public bool CanExposeResource(CapabilityDefinition capability, string uriTemplate)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentException.ThrowIfNullOrWhiteSpace(uriTemplate);

        if (!capability.SupportsResource || capability.Denied)
        {
            return false;
        }

        if (capability.Profile is not ProfileType.Observation || !ObservationProfileEnabled)
        {
            return false;
        }

        return EnabledResources.Contains(uriTemplate);
    }

    public bool CanInspectAddon(string addonName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addonName);
        return EnabledAddons.Contains(addonName);
    }

    public ExposurePolicy EnableTool(string toolName) =>
        new(WithAdded(EnabledTools, toolName), EnabledResources, EnabledAddons, ObservationProfileEnabled, ActionProfileEnabled);

    public ExposurePolicy DisableTool(string toolName) =>
        new(WithRemoved(EnabledTools, toolName), EnabledResources, EnabledAddons, ObservationProfileEnabled, ActionProfileEnabled);

    public ExposurePolicy EnableResource(string uriTemplate) =>
        new(EnabledTools, WithAdded(EnabledResources, uriTemplate), EnabledAddons, ObservationProfileEnabled, ActionProfileEnabled);

    public ExposurePolicy DisableResource(string uriTemplate) =>
        new(EnabledTools, WithRemoved(EnabledResources, uriTemplate), EnabledAddons, ObservationProfileEnabled, ActionProfileEnabled);

    public ExposurePolicy EnableAddon(string addonName) =>
        new(EnabledTools, EnabledResources, WithAdded(EnabledAddons, addonName), ObservationProfileEnabled, ActionProfileEnabled);

    public ExposurePolicy DisableAddon(string addonName) =>
        new(EnabledTools, EnabledResources, WithRemoved(EnabledAddons, addonName), ObservationProfileEnabled, ActionProfileEnabled);

    public ExposurePolicy ReplaceSelections(
        IEnumerable<string>? enabledTools,
        IEnumerable<string>? enabledResources,
        IEnumerable<string>? enabledAddons) =>
        new(enabledTools, enabledResources, enabledAddons, ObservationProfileEnabled, ActionProfileEnabled);

    public ExposurePolicy WithProfiles(bool observationProfileEnabled, bool actionProfileEnabled) =>
        new(EnabledTools, EnabledResources, EnabledAddons, observationProfileEnabled, actionProfileEnabled);

    private static HashSet<string> ToSet(IEnumerable<string>? values)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (values is null)
        {
            return set;
        }

        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                set.Add(value.Trim());
            }
        }

        return set;
    }

    private static HashSet<string> WithAdded(IReadOnlySet<string> current, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var next = new HashSet<string>(current, StringComparer.OrdinalIgnoreCase)
        {
            value.Trim(),
        };
        return next;
    }

    private static HashSet<string> WithRemoved(IReadOnlySet<string> current, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var next = new HashSet<string>(current, StringComparer.OrdinalIgnoreCase);
        next.Remove(value.Trim());
        return next;
    }
}
