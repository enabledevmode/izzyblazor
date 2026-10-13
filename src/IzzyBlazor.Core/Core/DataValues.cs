namespace IzzyBlazor;

/// <summary>How a <see cref="Card"/> is drawn.</summary>
public enum CardVariant
{
    /// <summary>A border on the page surface (the default).</summary>
    Outline,

    /// <summary>A raised surface with a shadow.</summary>
    Elevated,

    /// <summary>A tinted surface with no border.</summary>
    Filled,
}

/// <summary>How a table cell's content lines up.</summary>
public enum CellAlign
{
    /// <summary>The start (left in left-to-right languages). The default for text.</summary>
    Start,

    /// <summary>Centered.</summary>
    Center,

    /// <summary>The end (right in left-to-right languages). The default for numbers.</summary>
    End,
}

/// <summary>Which way a <see cref="Stat"/>'s figure moved.</summary>
public enum StatTrend
{
    /// <summary>No arrow (the default).</summary>
    None,

    /// <summary>Went up. Shown in the success color unless the stat's <c>ChangeStatus</c> says otherwise.</summary>
    Up,

    /// <summary>Went down. Shown in the danger color unless the stat's <c>ChangeStatus</c> says otherwise.</summary>
    Down,

    /// <summary>Stayed the same.</summary>
    Flat,
}
