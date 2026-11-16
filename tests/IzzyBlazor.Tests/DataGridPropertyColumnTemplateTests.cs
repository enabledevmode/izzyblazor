using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

public class DataGridPropertyColumnTemplateTests : BunitContext
{
    public enum Stage { Draft, Sent, Paid }

    public sealed record Row(string Number, Stage Stage);

    private static readonly Row[] Rows = [new("INV-1", Stage.Draft), new("INV-2", Stage.Paid), new("INV-3", Stage.Sent)];

    [Fact]
    public void A_templated_enum_column_draws_its_cells_and_still_filters_by_its_values()
    {
        Services.GetRequiredService<BunitNavigationManager>().NavigateTo("/invoices?f-stage=Paid");
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        RenderFragment columns = b =>
        {
            b.OpenComponent<PropertyColumn<Row, string>>(0);
            b.AddComponentParameter(1, "Property", (System.Linq.Expressions.Expression<Func<Row, string>>)(r => r.Number));
            b.CloseComponent();
            b.OpenComponent<PropertyColumn<Row, Stage>>(2);
            b.AddComponentParameter(3, "Property", (System.Linq.Expressions.Expression<Func<Row, Stage>>)(r => r.Stage));
            b.AddComponentParameter(4, "Filterable", true);
            b.AddComponentParameter(5, "ChildContent", (RenderFragment<Row>)(row => c =>
            {
                c.OpenElement(0, "mark");
                c.AddContent(1, row.Stage.ToString());
                c.CloseElement();
            }));
            b.CloseComponent();
        };

        var cut = Render<DataGrid<Row>>(p => p.Add(g => g.Items, Rows).Add(g => g.Columns, columns));

        var marks = cut.FindAll("tbody td mark");
        Assert.Single(marks);
        Assert.Equal("Paid", marks[0].TextContent);
        // The enum's own filter: a checkbox per value, as for a column with no template.
        Assert.Equal(3, cut.FindAll(".ib-table-filter-panel input[type=checkbox][name='f-stage']").Count);
    }
}
