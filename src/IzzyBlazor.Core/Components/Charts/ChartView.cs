namespace IzzyBlazor;

/// <summary>
/// The part of a chart's argument axis in view: from one argument to another, such as two dates,
/// two numbers or two names (both included). A null end is the data's own end. A Chart's
/// <c>View</c> opens it zoomed in, and its <c>ViewChanged</c> says where the reader zoomed to.
/// </summary>
/// <param name="From">The first argument in view, or null for the first item's.</param>
/// <param name="To">The last argument in view, or null for the last item's.</param>
public sealed record ChartView(object? From, object? To);
