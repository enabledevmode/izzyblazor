namespace IzzyBlazor;

/// <summary>Which way a <see cref="SpeedDial"/>'s actions open from its button.</summary>
public enum SpeedDialDirection
{
    /// <summary>Above the button (the default).</summary>
    Up,

    /// <summary>Below the button, for one at the top of an area.</summary>
    Down,

    /// <summary>Toward the start of the line (left, in left-to-right languages). Icons only.</summary>
    Start,

    /// <summary>Toward the end of the line. Icons only.</summary>
    End,
}

/// <summary>What opens a <see cref="SpeedDial"/>.</summary>
public enum SpeedDialTrigger
{
    /// <summary>A click or tap on the button (the default). Works with JavaScript off.</summary>
    Click,

    /// <summary>The pointer coming over the button, with script; a click still opens it.</summary>
    Hover,
}
