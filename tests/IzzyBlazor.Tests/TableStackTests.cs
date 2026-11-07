using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.Tests;

// A table that stacks its rows into cards when narrow, by a container query the server writes.
public class TableStackTests : BunitContext
{
    public sealed record Order(int Id, string Customer);

    public TableStackTests() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    [Fact]
    public void A_stacking_tables_titles_stay_inside_its_style_and_its_parts_keep_their_roles()
    {
        var cut = Render<Table<Order>>(p => p
            .Add(x => x.Items, [new Order(1, "Ada \"A\" <b>")])
            .Add(x => x.SmallScreen, TableSmallScreen.Stack)
            .Add(x => x.Columns, (RenderFragment)(builder =>
            {
                builder.OpenComponent<PropertyColumn<Order, int>>(0);
                builder.AddComponentParameter(1, "Property", (Expression<Func<Order, int>>)(o => o.Id));
                builder.AddComponentParameter(2, "Width", Length.Rem(6));
                builder.CloseComponent();
                builder.OpenComponent<PropertyColumn<Order, string>>(3);
                builder.AddComponentParameter(4, "Property", (Expression<Func<Order, string>>)(o => o.Customer));
                builder.AddComponentParameter(5, "Title", "Who \"is\" </style>");
                builder.CloseComponent();
            })));

        // A title can't end the style element it's written into.
        Assert.DoesNotContain("</style", cut.Find("style").TextContent);
        Assert.Equal("table", cut.Find("table").GetAttribute("role"));
        Assert.Equal(["row", "row"], cut.FindAll("tr").Select(r => r.GetAttribute("role")));
        Assert.Equal(["cell", "cell"], cut.FindAll("tbody td").Select(c => c.GetAttribute("role")));
    }
}
