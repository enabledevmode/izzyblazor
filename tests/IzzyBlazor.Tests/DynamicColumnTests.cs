using System.Data;
using System.Dynamic;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

// Columns known only at run time, read by name from rows of each shape it takes.
public class DynamicColumnTests : BunitContext
{
    public DynamicColumnTests()
    {
        Services.AddSingleton<IRequestCookies>(new NoCookies());
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
    }

    private static readonly (string Name, object? Qty)[] Data = [("Bolts", 2), ("Nuts", "30"), ("Washers", 4), ("Rivets", null)];

    public static TheoryData<string> Shapes => ["dictionary", "dynamic", "DataRow"];

    [Theory]
    [MemberData(nameof(Shapes))]
    public void Columns_from_a_list_read_convert_sort_and_filter_by_type(string shape)
    {
        Services.GetRequiredService<BunitNavigationManager>().NavigateTo("http://localhost/?sort=-qty&filter=qty%20gt%203%20or%20qty%20eq%20null");

        var cells = shape switch
        {
            "dictionary" => Cells(Data.Select(d => (IDictionary<string, object?>)new Dictionary<string, object?> { ["Name"] = d.Name, ["Qty"] = d.Qty })),
            "dynamic" => Cells(Data.Select(d =>
            {
                dynamic row = new ExpandoObject();
                row.Name = d.Name;
                row.Qty = d.Qty;
                return (object)row;
            })),
            _ => Cells(Table().Rows.Cast<DataRow>()),
        };

        // "30" was text, and a missing quantity sorts last and matches "is empty".
        Assert.Equal(["Nuts 30", "Washers 4", "Rivets "], cells);
    }

    // A CSV with both columns: the keys stay apart, so each header sorts by its own.
    [Fact]
    public void Margin_and_margin_percent_sort_by_their_own_values()
    {
        Services.GetRequiredService<BunitNavigationManager>().NavigateTo("http://localhost/?sort=margin_");
        IDictionary<string, object?> Row(string name, int margin, double percent) =>
            new Dictionary<string, object?> { ["Name"] = name, ["Margin"] = margin, ["Margin %"] = percent };
        var cut = Render<DataGrid<IDictionary<string, object?>>>(p => p
            .Add(x => x.Items, [Row("Bolts", 900, 30.5), Row("Nuts", 200, 45.0), Row("Washers", 500, 12.0)])
            .Add(x => x.ChildContent, (RenderFragment)(b =>
            {
                Column<IDictionary<string, object?>>(b, "Name", typeof(string));
                Column<IDictionary<string, object?>>(b, "Margin", typeof(int));
                Column<IDictionary<string, object?>>(b, "Margin %", typeof(double));
            })));

        Assert.Equal(["Washers", "Bolts", "Nuts"], cut.FindAll("tbody tr").Select(r => r.Children[0].TextContent.Trim()));
    }

    // A "show quarter" picker changes the column's Field: the rows and the total come from the new one.
    [Fact]
    public void Changing_the_field_reloads_the_rows_and_totals()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        IDictionary<string, object?> Row(string region, decimal q1, decimal q2) =>
            new Dictionary<string, object?> { ["Region"] = region, ["Q1"] = q1, ["Q2"] = q2 };
        IDictionary<string, object?>[] rows = [Row("North", 10, 5), Row("South", 2, 40)];
        RenderFragment Quarter(string field) => b =>
        {
            Column<IDictionary<string, object?>>(b, "Region", typeof(string));
            b.OpenComponent<DynamicColumn<IDictionary<string, object?>>>(10);
            b.AddComponentParameter(11, "Field", field);
            b.AddComponentParameter(12, "Type", typeof(decimal));
            b.AddComponentParameter(13, "Title", "Revenue");
            b.AddComponentParameter(14, "SortKey", "revenue");
            b.AddComponentParameter(15, "Sortable", true);
            b.AddComponentParameter(16, "Aggregate", DataAggregate.Sum);
            b.CloseComponent();
        };
        var cut = Render<DataGrid<IDictionary<string, object?>>>(p => p
            .Add(x => x.Items, rows)
            .Add(x => x.InitialSort, "-revenue")
            .Add(x => x.ChildContent, Quarter("Q1")));
        Assert.Equal("North", cut.Find("tbody tr").Children[0].TextContent.Trim());
        Assert.Contains("12", cut.Find("tfoot").TextContent);

        cut.Render(p => p.Add(x => x.ChildContent, Quarter("Q2")));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("South", cut.Find("tbody tr").Children[0].TextContent.Trim());
            Assert.Contains("45", cut.Find("tfoot").TextContent);
        });
    }

    private static DataTable Table()
    {
        var table = new DataTable();
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Qty", typeof(string));
        foreach (var (name, qty) in Data)
        {
            table.Rows.Add(name, qty ?? DBNull.Value);
        }

        return table;
    }

    private string[] Cells<T>(IEnumerable<T> rows)
    {
        var cut = Render<DataGrid<T>>(p => p
            .Add(x => x.Items, rows.ToList())
            .Add(x => x.FilterBuilder, true)
            .Add(x => x.ChildContent, (RenderFragment)(b =>
            {
                Column<T>(b, "Name", typeof(string));
                Column<T>(b, "Qty", typeof(int));
            })));
        return cut.FindAll("tbody tr").Select(r => $"{r.Children[0].TextContent.Trim()} {r.Children[1].TextContent.Trim()}").ToArray();
    }

    private static void Column<T>(RenderTreeBuilder b, string field, Type type)
    {
        b.OpenComponent<DynamicColumn<T>>(0);
        b.AddComponentParameter(1, "Field", field);
        b.AddComponentParameter(2, "Type", type);
        b.AddComponentParameter(3, "Sortable", true);
        b.AddComponentParameter(4, "Filterable", true);
        b.SetKey(field);
        b.CloseComponent();
    }

    private sealed class NoCookies : IRequestCookies
    {
        public string? Get(string name) => null;
    }
}
