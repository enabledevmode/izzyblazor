using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.Tests;

// A table that stacks its rows into cards when narrow, by a container query the server writes.
public class TableStackTests : BunitContext
{
    public sealed record Order(int Id, string Customer);

    public TableStackTests() => SetRendererInfo(new RendererInfo("Static", isInteractive: false));

    [Fact]
    public void Narrower_than_its_columns_need_each_cell_shows_its_columns_title_and_the_parts_keep_their_roles()
    {
        var cut = Render<Table<Order>>(p => p
            .Add(x => x.Items, [new Order(1, "Ada \"A\" <b>")])
            .Add(x => x.SmallScreen, TableSmallScreen.Stack)
            .Add(x => x.ChildContent, (RenderFragment)(builder =>
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

        // 6rem and 8rem for the one with no width.
        var css = cut.Find("style").TextContent;
        Assert.StartsWith("@layer izzy.components{@container (width < 14rem){", css);
        Assert.Contains(""">td:nth-child(2):not([colspan])::before{content:"Who \"is\" \3c /style>";content:"Who \"is\" \3c /style>"/""}""", css);
        Assert.Equal("table", cut.Find("table").GetAttribute("role"));
        Assert.Equal(["row", "row"], cut.FindAll("tr").Select(r => r.GetAttribute("role")));
        Assert.Equal(["cell", "cell"], cut.FindAll("tbody td").Select(c => c.GetAttribute("role")));
    }
}
