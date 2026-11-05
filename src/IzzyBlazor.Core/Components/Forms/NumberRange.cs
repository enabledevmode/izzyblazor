using System.Numerics;

namespace IzzyBlazor;

/// <summary>
/// Two numbers, both included, such as a price filter's lowest and highest. A
/// <see cref="RangeSlider{TNumber}"/> binds to one. The properties are settable for form binding on a static post.
/// </summary>
/// <typeparam name="T">The number type, such as <c>int</c>, <c>decimal</c> or <c>double</c>.</typeparam>
public sealed record NumberRange<T> where T : struct, INumber<T>
{
    /// <summary>A range from <paramref name="start"/> to <paramref name="end"/>, both included. One constructor, as form binding takes no more.</summary>
    public NumberRange(T start = default, T end = default) => (Start, End) = (start, end);

    /// <summary>The lowest value.</summary>
    public T Start { get; set; }

    /// <summary>The highest value, included.</summary>
    public T End { get; set; }

    /// <summary>Whether this value is in the range, both ends included.</summary>
    public bool Contains(T value) => value >= Start && value <= End;
}
