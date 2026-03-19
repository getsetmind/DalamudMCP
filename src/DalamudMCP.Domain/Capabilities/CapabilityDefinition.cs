namespace DalamudMCP.Domain.Capabilities;

public sealed record CapabilityDefinition
{
    public CapabilityDefinition(
        CapabilityId id,
        string displayName,
        string description,
        CapabilityCategory category,
        SensitivityLevel sensitivity,
        ProfileType profile,
        bool defaultEnabled,
        bool requiresConsent,
        bool denied,
        bool supportsTool,
        bool supportsResource,
        string version,
        params string[] tags)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("DisplayName cannot be null or whitespace.", nameof(displayName));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description cannot be null or whitespace.", nameof(description));
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("Version cannot be null or whitespace.", nameof(version));
        }

        if (!supportsTool && !supportsResource)
        {
            throw new ArgumentException("Capability must support at least one surface.");
        }

        if (denied && defaultEnabled)
        {
            throw new ArgumentException("Denied capability cannot be enabled by default.");
        }

        if (denied && sensitivity is not SensitivityLevel.Blocked)
        {
            throw new ArgumentException("Denied capability must have blocked sensitivity.");
        }

        if (profile is ProfileType.Action && supportsResource)
        {
            throw new ArgumentException("Action profile capability cannot expose resources.");
        }

        if (profile is ProfileType.Action && defaultEnabled)
        {
            throw new ArgumentException("Action profile capability cannot be enabled by default.");
        }

        Id = id;
        DisplayName = displayName.Trim();
        Description = description.Trim();
        Category = category;
        Sensitivity = sensitivity;
        Profile = profile;
        DefaultEnabled = defaultEnabled;
        RequiresConsent = requiresConsent;
        Denied = denied;
        SupportsTool = supportsTool;
        SupportsResource = supportsResource;
        Version = version.Trim();
        Tags = tags
            .Where(static tag => !string.IsNullOrWhiteSpace(tag))
            .Select(static tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public CapabilityId Id { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public CapabilityCategory Category { get; }

    public SensitivityLevel Sensitivity { get; }

    public ProfileType Profile { get; }

    public bool DefaultEnabled { get; }

    public bool RequiresConsent { get; }

    public bool Denied { get; }

    public bool SupportsTool { get; }

    public bool SupportsResource { get; }

    public string Version { get; }

    public IReadOnlyList<string> Tags { get; }
}
