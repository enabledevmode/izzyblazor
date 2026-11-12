using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

/// <summary>
/// The buttons in a chart's own tools row. Download: the button that saves a chart as a PNG, in the tools row across from its title.
/// chart.ts's click handler loads the code (chart-export.ts) on the first press; a button
/// of the app's own with <c>data-ib-chart-download="the chart's id"</c> works the same way. With
/// JavaScript off the button isn't shown (data-ib-script).
/// </summary>
internal static class ChartTools
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

    /// <summary>
    /// A chart's table of values: a "Show data" button that opens it in a popover over the chart
    /// (<see cref="ChartDataTable.Popover"/>, the default), or a "Show data" disclosure under the
    /// chart (<see cref="ChartDataTable.Collapsed"/>, or open with <see cref="ChartDataTable.Visible"/>).
    /// </summary>
    public static RenderFragment Data(ChartDataTable mode, string labelId, string? title, string? titledBy, IzzyText text, RenderFragment table) => builder =>
    {
        if (mode == ChartDataTable.None)
        {
            return;
        }

        // Its name: "Show data", then the chart's title (hidden) or the title it's named by.
        var labelledBy = titledBy is null ? null : $"{labelId}-show {titledBy}";
        RenderFragment name = b =>
        {
            b.OpenElement(0, "span");
            b.AddAttribute(1, "id", $"{labelId}-show");
            b.AddContent(2, text.ChartShowData);
            if (title is not null)
            {
                b.OpenElement(3, "span");
                b.AddAttribute(4, "class", "ib-visually-hidden");
                b.AddContent(5, $", {title}");
                b.CloseElement();
            }

            b.CloseElement();
        };

        if (mode == ChartDataTable.Popover)
        {
            builder.OpenComponent<Popover>(10);
            builder.AddComponentParameter(11, nameof(Popover.Placement), Placement.TopStart);
            builder.AddComponentParameter(12, nameof(Popover.Title), title ?? text.ChartShowData);
            builder.AddComponentParameter(13, nameof(Popover.Class), "ib-chart-data-pop");
            // As wide as the table, up to the screen's width.
            builder.AddComponentParameter(16, nameof(Popover.PanelWidth), Length.MaxContent);
            builder.AddComponentParameter(14, nameof(Popover.Trigger), (RenderFragment<IReadOnlyDictionary<string, object>>)(wire => b =>
            {
                b.OpenElement(0, "button");
                b.AddMultipleAttributes(1, wire);
                b.AddAttribute(2, "type", "button");
                b.AddAttribute(3, "class", "ib-btn ib-btn-neutral ib-btn-ghost ib-btn-sm ib-chart-data-button");
                b.AddAttribute(4, "aria-labelledby", labelledBy);
                b.OpenComponent<Icon>(5);
                b.AddComponentParameter(6, nameof(Icon.Name), Lucide.Table2);
                b.CloseComponent();
                b.AddContent(7, name);
                b.CloseElement();
            }));
            builder.AddComponentParameter(15, nameof(Popover.ChildContent), table);
            builder.CloseComponent();
            return;
        }

        builder.OpenElement(20, "details");
        builder.AddAttribute(21, "class", "ib-collapsible ib-disclosure ib-chart-data");
        builder.AddAttribute(22, "open", mode == ChartDataTable.Visible);
        builder.OpenElement(23, "summary");
        builder.AddAttribute(24, "class", "ib-collapsible-trigger");
        builder.AddAttribute(25, "aria-labelledby", labelledBy);
        builder.OpenComponent<Icon>(26);
        builder.AddComponentParameter(27, nameof(Icon.Purpose), IconPurpose.ChevronRight);
        builder.AddComponentParameter(28, nameof(Icon.Class), "ib-collapsible-chevron");
        builder.CloseComponent();
        builder.AddContent(29, name);
        builder.CloseElement();
        builder.AddContent(30, table);
        builder.CloseElement();
    };
}
