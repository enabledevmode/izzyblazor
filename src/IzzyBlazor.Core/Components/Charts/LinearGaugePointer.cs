namespace IzzyBlazor;

/// <summary>How a <see cref="LinearGauge"/> draws its value.</summary>
public enum LinearGaugePointer
{
    /// <summary>A bar beside the track, from the scale's start to the value: an amount, such as a tank's level.</summary>
    Bar,

    /// <summary>A triangle at the value: a reading that isn't an amount, such as air pressure.</summary>
    Marker,
}
