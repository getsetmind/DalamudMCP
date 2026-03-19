namespace DalamudMCP.Domain.Capabilities;

public readonly record struct CapabilityId
{
    public CapabilityId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("CapabilityId cannot be null or whitespace.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}
