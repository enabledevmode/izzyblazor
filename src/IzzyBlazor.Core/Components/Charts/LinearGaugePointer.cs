namespace IzzyBlazor;

/// <summary>How a <see cref="LinearGauge"/> draws its value.</summary>
public enum LinearGaugePointer
{
    /// <summary>A bar beside the track, from the scale's start to the value: an amount, such as a tank's level.</summary>
    Bar,

    /// <summary>A triangle at the value: a reading that isn't an amount, such as air pressure.</summary>
    Marker,

    /// <summary>A needle at the value, into a thick track: the bands are the gauge, such as where a share of calories falls.</summary>
    Needle,
}

/// <summary>Where a scale's ticks are drawn, such as a <see cref="LinearGauge"/>'s.</summary>
public enum TickPlacement
{
    /// <summary>Beside the track, with their numbers.</summary>
    Outside,

    /// <summary>Over the track, from the edge the numbers are on.</summary>
    Inside,
}

/// <summary>Which side of a scale its numbers are on, such as a <see cref="LinearGauge"/>'s.</summary>
public enum ScaleSide
{
    /// <summary>Above a scale across, left of one standing up.</summary>
    Start,

    /// <summary>Below a scale across, right of one standing up.</summary>
    End,
}
