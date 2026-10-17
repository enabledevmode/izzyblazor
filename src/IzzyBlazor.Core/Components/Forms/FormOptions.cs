namespace IzzyBlazor;

/// <summary>What a <see cref="Form"/> passes down to its fields.</summary>
internal sealed record FormOptions(bool Disabled, bool ReadOnly, RequiredMark RequiredMark);

/// <summary>Which fields a <see cref="Form"/> marks beside their labels.</summary>
public enum RequiredMark
{
    /// <summary>Required fields get an asterisk (the default).</summary>
    Required,

    /// <summary>Fields that aren't required get "(optional)"; required ones get nothing. Suits forms where most fields are required.</summary>
    Optional,

    /// <summary>No marks.</summary>
    None,
}
