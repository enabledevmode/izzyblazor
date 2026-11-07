using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.Tests;

// A column's aggregate covers all the rows the filters match, whatever the page.
public class DataGridAggregateTests : BunitContext
{
    public sealed record Row(string Name, int Qty, decimal Price);

    private static readonly Row[] Rows = [new("a", 1, 10m), new("b", 2, 20m), new("c", 3, 30m), new("d", 4, 40m)];

    public DataGridAggregateTests() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    private static RenderFragment Columns => builder =>
    {
        builder.OpenComponent<PropertyColumn<Row, string>>(0);
        builder.AddComponentParameter(1, "Property", (Expression<Func<Row, string>>)(r => r.Name));
        builder.AddComponentParameter(2, "FooterText", "Total");
        builder.CloseComponent();
        builder.OpenComponent<PropertyColumn<Row, int>>(3);
        builder.AddComponentParameter(4, "Property", (Expression<Func<Row, int>>)(r => r.Qty));
        builder.AddComponentParameter(5, "Aggregate", (DataAggregate?)DataAggregate.Sum);
        builder.AddComponentParameter(6, "Format", "N1");
        builder.CloseComponent();
        builder.OpenComponent<TemplateColumn<Row>>(7);
        builder.AddComponentParameter(8, "Title", "Worth");
        builder.AddComponentParameter(9, "AggregateBy", (Expression<Func<IEnumerable<Row>, object?>>)(g => g.Sum(r => r.Qty * r.Price)));
        builder.AddComponentParameter(10, "AggregateFormat", "N0");
        builder.AddComponentParameter(11, "ChildContent", (RenderFragment<Row>)(_ => _ => { }));
        builder.CloseComponent();
    };

    [Fact]
    public void The_footer_covers_every_page_with_the_columns_own_format()
    {
        var cut = Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, Rows)
            .Add(x => x.PageSize, 2)
            .Add(x => x.Columns, Columns));

        var cells = cut.FindAll("tfoot td");
        Assert.Equal(2, cut.FindAll("tbody tr").Count);
        Assert.Equal("Total", cells[0].TextContent);
        Assert.Equal("Sum: 10.0", cells[1].TextContent.Trim());
        Assert.Equal("300", cells[2].TextContent.Trim());
    }

    [Fact]
    public void A_provider_answers_for_its_own_footer()
    {
        DataGroupRequest<Row>? asked = null;
        var cut = Render<DataGrid<Row>>(p => p
            .Add(x => x.ItemsProvider, r => ValueTask.FromResult(new DataResult<Row>(Rows.Take(2).ToList(), 4)))
            .Add(x => x.GroupsProvider, r =>
            {
                asked = r;
                return ValueTask.FromResult(new DataGroupResult([new DataGroup([], 4) { Values = new Dictionary<string, object?> { ["qty:sum"] = JsonSerializer.Deserialize<JsonElement>("99") } }]));
            })
            .Add(x => x.PageSize, 2)
            .Add(x => x.Columns, Columns));

        Assert.Empty(asked!.Keys);
        Assert.Contains(new DataAggregateField("qty", DataAggregate.Sum), asked.Aggregates);
        Assert.Equal("Sum: 99.0", cut.FindAll("tfoot td")[1].TextContent.Trim());
    }

    [Fact]
    public void A_provider_without_groups_says_what_it_needs()
    {
        var error = Assert.ThrowsAny<Exception>(() => Render<DataGrid<Row>>(p => p
            .Add(x => x.ItemsProvider, r => ValueTask.FromResult(new DataResult<Row>(Rows, 4)))
            .Add(x => x.Columns, Columns)));

        Assert.Contains("GroupsProvider", error.GetBaseException().Message);
    }

    [Theory]
    [InlineData("AggregateBy", "no place shows")]
    [InlineData("AggregateLabel", "different aggregates")]
    public void A_column_set_up_to_show_nothing_or_one_label_for_two_says_so(string parameter, string message)
    {
        RenderFragment columns = builder =>
        {
            builder.OpenComponent<PropertyColumn<Row, int>>(0);
            builder.AddComponentParameter(1, "Property", (Expression<Func<Row, int>>)(r => r.Qty));
            builder.AddComponentParameter(2, "GroupAggregate", (DataAggregate?)DataAggregate.Sum);
            builder.AddComponentParameter(3, "Aggregate", parameter == "AggregateLabel" ? (DataAggregate?)DataAggregate.Max : null);
            builder.AddComponentParameter(4, parameter, parameter == "AggregateBy"
                ? (object)(Expression<Func<IEnumerable<Row>, object?>>)(g => g.Sum(r => r.Qty))
                : "Latest");
            builder.CloseComponent();
        };

        var error = Assert.ThrowsAny<Exception>(() => Render<DataGrid<Row>>(p => p
            .Add(x => x.Items, Rows)
            .Add(x => x.Columns, columns)));

        Assert.Contains(message, error.GetBaseException().Message);
    }
}
