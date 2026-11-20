using System.Diagnostics.CodeAnalysis;

namespace IzzyBlazor;

/// <summary>
/// The rule every <c>For</c> parameter follows: a bare value is an id; a value that starts with
/// <c>#</c>, <c>.</c> or <c>[</c> is a CSS selector. The scripts' side is <c>forSelector</c> (for.ts).
/// </summary>
internal static class ForTarget
{
    /// <summary>
    /// The id a <c>For</c> names, for a component that points at one element: "invite" and "#invite"
    /// are both <c>invite</c>. Any other selector throws, naming <paramref name="owner"/>.
    /// </summary>
    [return: NotNullIfNotNull(nameof(value))]
    public static string? Id(string? value, string owner, string parameter = "For")
    {
        if (value is null)
        {
            return null;
        }

        var id = value.StartsWith('#') ? value[1..] : value;
        if (id.Length == 0 || id[0] is '.' or '[' || id.AsSpan().IndexOfAny(' ', '>', ',') >= 0)
        {
            throw new InvalidOperationException($"{owner}'s {parameter} names one element by its id, \"invite\" or \"#invite\"; \"{value}\" isn't one.");
        }

        return id;
    }
}
