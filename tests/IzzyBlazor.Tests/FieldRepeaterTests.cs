using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.Tests;

// FieldRepeater: rows that post as a list, Add and Remove within the limits, and each row validated with the form.
// Its static posts (Add and Remove as form posts) are checked in a browser.
public class FieldRepeaterTests : BunitContext
{
    [Fact]
    public void Rows_post_as_the_list_and_the_first_row_heads_the_table()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var form = Render<RepeaterProbe>();

        var names = form.FindAll(".ib-repeat-row input[name]").Select(i => i.GetAttribute("name")).ToList();
        Assert.Contains("Order.Lines[0].Description", names);
        Assert.Contains("Order.Lines[0].Quantity", names);
        Assert.Equal(["Description *", "Quantity"], form.FindAll(".ib-repeat-th").Select(h => h.TextContent.Trim()).Where(t => t.Length > 0));
        Assert.Equal("Item 1", form.Find(".ib-repeat-row").GetAttribute("aria-label"));

        // One row is the minimum: no Remove. Add posts its command and skips the browser's checks.
        Assert.Empty(form.FindAll(".ib-repeat-remove"));
        var add = form.Find(".ib-repeat-foot button");
        Assert.Equal("ib-repeat", add.GetAttribute("name"));
        Assert.Equal("Order.Lines:add", add.GetAttribute("value"));
        Assert.True(add.HasAttribute("formnovalidate"));
    }

    [Fact]
    public void Add_and_Remove_keep_to_the_limits_and_each_row_is_validated()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var form = Render<RepeaterProbe>();

        form.Find(".ib-repeat-foot button").Click();
        form.Find(".ib-repeat-foot button").Click();
        Assert.Equal(3, form.Instance.Order.Lines.Count);
        Assert.Empty(form.FindAll(".ib-repeat-foot button")); // At MaxItems, no Add.

        form.Find("button[aria-label='Remove Item 3']").Click();
        Assert.Equal(2, form.Instance.Order.Lines.Count);

        // The new row has no description: the form doesn't save, and the summary says which row.
        form.Find("form").Submit();
        Assert.Equal(0, form.Instance.Saved);
        Assert.Contains("Item 2, Description: Enter a description.", form.FindAll(".ib-form-errors li").Select(li => li.TextContent.Trim()));
    }
}
