namespace IzzyBlazor;

/// <summary>
/// The responsive breakpoints. Mobile first: a value at a breakpoint applies from
/// that width upward.
/// </summary>
/// <remarks>
/// Keep Sm to Xl in sync with <c>BREAKPOINTS</c> in <c>build/build-assets.mjs</c>, which generates
/// the responsive style props; Xxl is written by hand, for AppAside (shell.css) and
/// HiddenFrom/VisibleFrom (layout.css) only.
/// </remarks>
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

    /// <summary>
    /// 1536px and wider: for where an AppAside shows, and HiddenFrom/VisibleFrom. AppSidebar's and
    /// SiteNav's breakpoints, and responsive values (<see cref="Responsive{T}"/>), stop at Xl.
    /// </summary>
    Xxl,
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
        Breakpoint.Xxl => "xxl",
        _ => throw new ArgumentOutOfRangeException(nameof(breakpoint)),
    };
}
