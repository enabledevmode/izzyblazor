namespace IzzyBlazor;

/// <summary>How a <see cref="Diff"/> lays out the two texts.</summary>
public enum DiffLayout
{
    /// <summary>One column: each change's removed lines, then its added lines.</summary>
    Unified,

    /// <summary>The old text beside the new, each line beside the one it became; unified where the diff is narrow.</summary>
    Split,
}
