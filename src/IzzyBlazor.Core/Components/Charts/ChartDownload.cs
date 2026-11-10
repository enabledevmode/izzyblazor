using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// Chart Download: the button that saves a chart as a PNG, in the tools row across from its title.
/// chart.ts's click handler loads the code (chart-export.ts) on the first press; a button
/// of the app's own with <c>data-ib-chart-download="the chart's id"</c> works the same way. With
/// JavaScript off the button isn't shown (data-ib-script).
/// </summary>
internal static class ChartDownload
{
    /// <summary>The download button, for a tools row that has other buttons (Chart's zoom and pause).</summary>
    public static RenderFragment Button(IzzyText text) => builder =>
    {
        builder.OpenElement(0, "button");
        builder.AddAttribute(1, "type", "button");
        builder.AddAttribute(2, "class", "ib-btn ib-btn-neutral ib-btn-outline ib-btn-sm ib-btn-icon-only ib-chart-download");
        builder.AddAttribute(3, "data-ib-script", true);
        builder.AddAttribute(4, "data-ib-chart-download", "");
        builder.AddAttribute(5, "aria-label", text.ChartDownload);
        builder.AddAttribute(6, "title", text.ChartDownload);
        builder.OpenComponent<Icon>(7);
        builder.AddComponentParameter(8, nameof(Icon.Purpose), IconPurpose.Export);
        builder.CloseComponent();
        builder.CloseElement();
    };

    /// <summary>A tools row with only the download button, for the charts that have no other tools.</summary>
    public static RenderFragment Tools(IzzyText text) => builder =>
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "ib-chart-tools");
        builder.AddContent(2, Button(text));
        builder.CloseElement();
    };
}
