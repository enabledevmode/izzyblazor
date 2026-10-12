namespace IzzyBlazor;

/// <summary>
/// The responsive breakpoints. Mobile first: a value at a breakpoint applies from
/// that width upward.
/// </summary>
/// <remarks>Keep in sync with <c>BREAKPOINTS</c> in <c>build/build-assets.mjs</c>.</remarks>
public enum Breakpoint
{
    /// <summary>640px and wider.</summary>
    Sm,

    /// <summary>768px and wider.</summary>
    Md,

    /// <summary>1024px and wider.</summary>
    Lg,

    /// <summary>1280px and wider.</summary>
    Xl,
}

internal static class BreakpointExtensions
{
    /// <summary>The short name used in class names and custom properties: <c>sm</c>, <c>md</c>, <c>lg</c>, <c>xl</c>.</summary>
    public static string Name(this Breakpoint breakpoint) => breakpoint switch
    {
        Breakpoint.Sm => "sm",
        Breakpoint.Md => "md",
        Breakpoint.Lg => "lg",
        Breakpoint.Xl => "xl",
        _ => throw new ArgumentOutOfRangeException(nameof(breakpoint)),
    };
}
