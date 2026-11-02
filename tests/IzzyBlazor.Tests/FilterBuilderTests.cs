using System.Text.Json;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor.Tests;

public class FilterBuilderTests : BunitContext
{
    public enum Kind { A, B }

    public sealed record Row(string Name, int Total, Kind Kind, DateOnly Day, Guid Id);

    private static readonly IReadOnlyList<DataField<Row>> Fields = DataFields.Of<Row>();

    private static JsonElement Model(string text) =>
        JsonDocument.Parse(FilterBuilderModel.Write(Filter.Parse(text), Fields)).RootElement;

    [Fact]
    public void A_filter_draws_in_the_shape_it_was()
    {
        var top = Model("contains(name,'a') and (kind in ('A','B') or between(total,1,5))");

        Assert.Equal("all", top.GetProperty("logic").GetString());
        var items = top.GetProperty("items");
        Assert.Equal("contains", items[0].GetProperty("op").GetString());
        Assert.Equal("any", items[1].GetProperty("logic").GetString());
        Assert.Equal("in", items[1].GetProperty("items")[0].GetProperty("op").GetString());
        var between = items[1].GetProperty("items")[1];
        Assert.Equal("1", between.GetProperty("values")[0].GetString());
        Assert.Equal("5", between.GetProperty("to").GetString());
    }

    // Nothing in a filter is lost by opening it in the builder.
    [Theory]
    [InlineData("nosuchfield eq 1")]
    public void What_cant_be_a_rule_stays_as_its_text(string text) =>
        Assert.Equal(Filter.Parse(text).ToString(), Model(text).GetProperty("items")[0].GetProperty("raw").GetString());

    [Fact]
    public void Each_field_offers_what_its_type_takes()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        Services.GetRequiredService<BunitNavigationManager>().NavigateTo("/orders");
        var cut = Render<FilterBuilder<Row>>(p => p.Add(b => b.Fields, Fields));

        string[] Words(string key) => cut.Find($"[data-ib-fb-field='{key}'] [data-ib-fb-op]").QuerySelectorAll("option").Select(o => o.GetAttribute("value")!).ToArray();

        Assert.Equal("contains", Words("name")[0]);
        Assert.Contains("between", Words("total"));
        Assert.DoesNotContain("between", Words("name"));
        Assert.Equal("in", Words("kind")[0]);
        Assert.Empty(cut.FindAll("[data-ib-fb-field='id']"));
    }

    [Fact]
    public void A_filter_in_the_address_is_drawn()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        Services.GetRequiredService<BunitNavigationManager>().NavigateTo("/orders?filter=total%20gt%205");
        var cut = Render<FilterBuilder<Row>>(p => p.Add(b => b.Fields, Fields));

        Assert.Contains("\"op\":\"gt\"", cut.Find("ib-filter-builder").GetAttribute("data-ib-value"));
    }
}
